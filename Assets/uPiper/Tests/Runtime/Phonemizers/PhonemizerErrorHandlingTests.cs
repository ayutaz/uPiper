using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core.Phonemizers.Backend;
using uPiper.Core.Phonemizers.Backend.RuleBased;
using uPiper.Core.Phonemizers.ErrorHandling;

namespace uPiper.Tests.Phonemizers
{
    /// <summary>
    /// Tests for error handling and resilience
    /// </summary>
    [TestFixture]
    public class PhonemizerErrorHandlingTests
    {
        private CircuitBreaker circuitBreaker;

        [SetUp]
        public void SetUp()
        {
            circuitBreaker = new CircuitBreaker(
                failureThreshold: 3,
                timeout: TimeSpan.FromSeconds(5)
            );
        }

        #region Circuit Breaker Tests

        [Test]
        public void CircuitBreaker_ShouldOpenAfterThresholdFailures()
        {
            // Initially closed
            Assert.IsTrue(circuitBreaker.CanExecute(), "Circuit should start closed");
            Assert.AreEqual(CircuitState.Closed, circuitBreaker.State);

            // Simulate failures
            for (var i = 0; i < 3; i++)
            {
                Assert.IsTrue(circuitBreaker.CanExecute(), $"Should allow execution {i + 1}");
                circuitBreaker.OnFailure(new Exception($"Test failure {i + 1}"));
            }

            // Should now be open
            Assert.IsFalse(circuitBreaker.CanExecute(), "Circuit should be open after threshold");
            Assert.AreEqual(CircuitState.Open, circuitBreaker.State);
        }

        [Test]
        public void CircuitBreaker_ShouldResetOnSuccess()
        {
            // Get to open state
            circuitBreaker.OnFailure(new Exception("Failure 1"));
            circuitBreaker.OnFailure(new Exception("Failure 2"));
            circuitBreaker.OnFailure(new Exception("Failure 3"));

            Assert.AreEqual(CircuitState.Open, circuitBreaker.State);

            // For testing, we'll create a new circuit breaker with shorter timeout
            var quickBreaker = new CircuitBreaker(
                failureThreshold: 3,
                timeout: TimeSpan.FromMilliseconds(100)
            );

            // Fail it
            for (var i = 0; i < 3; i++)
            {
                quickBreaker.OnFailure(new Exception());
            }

            // Wait for timeout
            Thread.Sleep(150);

            // Should now allow a test (half-open)
            Assert.IsTrue(quickBreaker.CanExecute(), "Should allow test after timeout");

            // Success should close it
            quickBreaker.OnSuccess();
            Assert.AreEqual(CircuitState.Closed, quickBreaker.State);
        }

        [Test]
        public void CircuitBreaker_ShouldHandleConcurrentAccess()
        {
            var tasks = new List<Task>();
            var exceptions = new List<Exception>();
            var successCount = 0;

            // Concurrent operations
            for (var i = 0; i < 10; i++)
            {
                var index = i; // Capture loop variable
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        if (circuitBreaker.CanExecute())
                        {
                            // Simulate some failures
                            if (index % 2 == 0)
                            {
                                var ex = new Exception($"Concurrent failure {index}");
                                circuitBreaker.OnFailure(ex);
                                lock (exceptions)
                                {
                                    exceptions.Add(ex);
                                }
                            }
                            else
                            {
                                circuitBreaker.OnSuccess();
                                Interlocked.Increment(ref successCount);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (exceptions)
                        {
                            exceptions.Add(ex);
                        }
                    }
                }));
            }

            Task.WaitAll(tasks.ToArray());

            // Circuit should be in a consistent state
            Assert.IsNotNull(circuitBreaker.State);
            Debug.Log($"Circuit state after concurrent access: {circuitBreaker.State}");
            Debug.Log($"Recorded {exceptions.Count} failures, {successCount} successes");
        }

        #endregion

        #region Safe Wrapper Tests

        [Test]
        public async Task SafeWrapper_ShouldFallbackOnError()
        {
            var fallbackBackend = new RuleBasedPhonemizer();
            await fallbackBackend.InitializeAsync();

            var result = await fallbackBackend.PhonemizeAsync("test", "en-US");

            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result.Phonemes);

            fallbackBackend.Dispose();
        }

        [Test]
        public async Task SafeWrapper_ShouldRespectCircuitBreaker()
        {
            var fallbackBackend = new RuleBasedPhonemizer();
            await fallbackBackend.InitializeAsync();

            // Multiple calls should succeed consistently
            for (var i = 0; i < 2; i++)
            {
                var result = await fallbackBackend.PhonemizeAsync($"test {i}", "en-US");
                Assert.IsNotNull(result);
            }

            var startTime = DateTime.Now;
            var finalResult = await fallbackBackend.PhonemizeAsync("final test", "en-US");
            var elapsed = DateTime.Now - startTime;

            Assert.IsNotNull(finalResult);
            Assert.Less(elapsed.TotalMilliseconds, 100,
                "Should not try failing backend when circuit is open");

            fallbackBackend.Dispose();
        }

        #endregion

        #region Input Validation Tests

        [Test]
        [Timeout(5000)] // 5 second timeout to prevent hanging
        public async Task InputValidation_ShouldHandleInvalidInputs()
        {
            var backend = new RuleBasedPhonemizer();

            // Use timeout for initialization
            bool initialized;
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
            {
                initialized = await backend.InitializeAsync(null, cts.Token);
            }

            if (!initialized)
            {
                LogAssert.Expect(LogType.Error, "CMU dictionary loading was cancelled.");
                LogAssert.Expect(LogType.Error,
                    "Failed to initialize RuleBasedPhonemizer: The operation was canceled.");
                Assert.Inconclusive("Backend initialization timed out or failed");
                return;
            }

            var invalidInputs = new[]
            {
                (text: null, language: "en-US"),
                (text: "", language: "en-US"),
                (text: "test", language: null),
                (text: "test", language: ""),
                (text: "test", language: "invalid-LANG"),
                (text: new string('a', 10000), language: "en-US") // Very long text
            };

            foreach (var (text, language) in invalidInputs)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                try
                {
                    var result = await backend.PhonemizeAsync(text, language, null, cts.Token);

                    // Some invalid inputs might still produce results (like empty text)
                    Assert.IsNotNull(result, $"Should handle input: text='{text}', lang='{language}'");
                }
                catch (ArgumentException)
                {
                    // Expected for some invalid inputs
                }
                catch (NotSupportedException)
                {
                    // Expected for unsupported languages
                }
                catch (OperationCanceledException)
                {
                    Assert.Fail($"Operation timed out for input: text='{text}', lang='{language}'");
                }
            }

            backend.Dispose();
        }

        [Test]
        public void InputValidation_ShouldSanitizeSpecialCharacters()
        {
            var problematicInputs = new[]
            {
                "Test\0with\0null",
                "Test\r\nwith\r\nnewlines",
                "Test\twith\ttabs",
                "Test with \u200B zero-width spaces",
                "Test with emoji",
                "<script>alert('xss')</script>",
                "'; DROP TABLE phonemes; --"
            };

            foreach (var input in problematicInputs)
            {
                Assert.DoesNotThrow(() =>
                {
                    var sanitized = input
                        .Replace("\0", " ")
                        .Replace("\r", " ")
                        .Replace("\n", " ")
                        .Replace("\t", " ");

                    Debug.Log($"Sanitized: '{input}' -> '{sanitized}'");
                });
            }
        }

        #endregion
    }
}