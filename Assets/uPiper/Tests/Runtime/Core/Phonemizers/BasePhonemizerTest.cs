using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core;
using uPiper.Core.Phonemizers;
using uPiper.Core.Phonemizers.Implementations;

namespace uPiper.Tests.Runtime.Core.Phonemizers
{
    public class BasePhonemizerTest
    {
        private TestPhonemizer _phonemizer;

        [SetUp]
        public void Setup()
        {
            _phonemizer = new TestPhonemizer();
        }

        [TearDown]
        public void TearDown()
        {
            _phonemizer?.Dispose();
        }

        #region Caching Tests

        [Test]
        public async Task Caching_EnabledByDefault()
        {
            Assert.IsTrue(_phonemizer.UseCache);
            
            // First call
            var result1 = await _phonemizer.PhonemizeAsync("test", "en");
            Assert.IsFalse(result1.FromCache);
            Assert.AreEqual(1, _phonemizer.InternalCallCount);
            
            // Second call - should be cached
            var result2 = await _phonemizer.PhonemizeAsync("test", "en");
            Assert.IsTrue(result2.FromCache);
            Assert.AreEqual(1, _phonemizer.InternalCallCount); // No additional internal call
        }

        [Test]
        public async Task Caching_CanBeDisabled()
        {
            _phonemizer.UseCache = false;
            
            // First call
            var result1 = await _phonemizer.PhonemizeAsync("test", "en");
            Assert.IsFalse(result1.FromCache);
            Assert.AreEqual(1, _phonemizer.InternalCallCount);
            
            // Second call - should NOT be cached
            var result2 = await _phonemizer.PhonemizeAsync("test", "en");
            Assert.IsFalse(result2.FromCache);
            Assert.AreEqual(2, _phonemizer.InternalCallCount);
        }

        [Test]
        public async Task Caching_DifferentLanguages_CachedSeparately()
        {
            var textEn = await _phonemizer.PhonemizeAsync("hello", "en");
            var textJa = await _phonemizer.PhonemizeAsync("hello", "ja");
            
            Assert.AreEqual("en", textEn.Language);
            Assert.AreEqual("ja", textJa.Language);
            Assert.AreEqual(2, _phonemizer.InternalCallCount);
            
            // Retrieve from cache
            var textEnCached = await _phonemizer.PhonemizeAsync("hello", "en");
            var textJaCached = await _phonemizer.PhonemizeAsync("hello", "ja");
            
            Assert.IsTrue(textEnCached.FromCache);
            Assert.IsTrue(textJaCached.FromCache);
            Assert.AreEqual(2, _phonemizer.InternalCallCount); // No new calls
        }

        [Test]
        public void ClearCache_RemovesAllCachedItems()
        {
            _phonemizer.Phonemize("test1", "en");
            _phonemizer.Phonemize("test2", "en");
            
            var stats = _phonemizer.GetCacheStatistics();
            Assert.Greater(stats.EntryCount, 0);
            
            _phonemizer.ClearCache();
            
            stats = _phonemizer.GetCacheStatistics();
            Assert.AreEqual(0, stats.EntryCount);
            Assert.AreEqual(0, stats.HitCount);
            Assert.AreEqual(0, stats.MissCount);
        }

        #endregion

        #region Text Normalization Tests

        [Test]
        public async Task TextNormalization_AppliedBeforePhonemization()
        {
            var result = await _phonemizer.PhonemizeAsync("  Hello   World  ", "en");
            
            // Check that the normalized text was used internally
            Assert.AreEqual("hello world", _phonemizer.LastNormalizedText);
            Assert.AreEqual("  Hello   World  ", result.OriginalText); // Original preserved
        }

        [Test]
        public async Task TextNormalization_SameNormalizedText_UsesCachedResult()
        {
            var result1 = await _phonemizer.PhonemizeAsync("Hello World", "en");
            var result2 = await _phonemizer.PhonemizeAsync("hello   world", "en");
            var result3 = await _phonemizer.PhonemizeAsync("HELLO WORLD", "en");
            
            // All should normalize to "hello world" and use same cache entry
            Assert.AreEqual(1, _phonemizer.InternalCallCount);
            Assert.IsFalse(result1.FromCache);
            Assert.IsTrue(result2.FromCache);
            Assert.IsTrue(result3.FromCache);
        }

        #endregion

        #region Error Handling Tests

        [Test]
        public void UnsupportedLanguage_ThrowsException()
        {
            // Unity Test Framework doesn't support Assert.ThrowsAsync properly
            // Using synchronous method instead
            var ex = Assert.Throws<PiperPhonemizationException>(
                () => _phonemizer.Phonemize("test", "unsupported")
            );
            
            Assert.That(ex.Message, Does.Contain("not supported"));
            Assert.AreEqual("test", ex.InputText);
            Assert.AreEqual("unsupported", ex.Language);
        }

        [Test]
        public async Task EmptyText_ReturnsEmptyResult()
        {
            var result = await _phonemizer.PhonemizeAsync("", "en");
            
            Assert.AreEqual("", result.OriginalText);
            Assert.AreEqual(0, result.Phonemes.Length);
            Assert.AreEqual(TimeSpan.Zero, result.ProcessingTime);
            Assert.IsFalse(result.FromCache);
        }

        [Test]
        public async Task NullText_ReturnsEmptyResult()
        {
            var result = await _phonemizer.PhonemizeAsync(null, "en");
            
            Assert.AreEqual("", result.OriginalText);
            Assert.AreEqual(0, result.Phonemes.Length);
        }

        [Test]
        public void InternalError_WrappedInPhonemizationException()
        {
            _phonemizer.SimulateError = true;
            
            // Unity Test Framework doesn't support Assert.ThrowsAsync properly
            // Using synchronous method instead
            var ex = Assert.Throws<PiperPhonemizationException>(
                () => _phonemizer.Phonemize("test", "en")
            );
            
            Assert.That(ex.Message, Does.Contain("Failed to phonemize"));
            Assert.IsNotNull(ex.InnerException);
            Assert.That(ex.InnerException.Message, Does.Contain("Simulated error"));
        }

        #endregion

        #region Cancellation Tests

        [Test]
        public void Cancellation_ThrowsOperationCanceledException()
        {
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                
                // Unity Test Framework doesn't support Assert.ThrowsAsync properly
                // Test cancellation behavior without async assertions
                try
                {
                    var task = _phonemizer.PhonemizeAsync("test", "en", cts.Token);
                    task.Wait();
                    Assert.Fail("Expected OperationCanceledException");
                }
                catch (AggregateException ae)
                {
                    Assert.IsInstanceOf<OperationCanceledException>(ae.InnerException);
                }
            }
        }

        [Test]
        public async Task Cancellation_DoesNotCache()
        {
            using (var cts = new CancellationTokenSource())
            {
                _phonemizer.DelayMilliseconds = 10; // Short delay
                
                var task = _phonemizer.PhonemizeAsync("test", "en", cts.Token);
                await Task.Delay(1); // Give it a moment to start
                cts.Cancel();
                
                try
                {
                    await task;
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
                
                // Result should not be cached
                _phonemizer.DelayMilliseconds = 0;
                var result = await _phonemizer.PhonemizeAsync("test", "en");
                Assert.IsFalse(result.FromCache);
            }
        }

        #endregion

        #region Batch Processing Tests

        [Test]
        public async Task PhonemizeBatch_ProcessesAllTexts()
        {
            var texts = new[] { "hello", "world", "test" };
            var results = await _phonemizer.PhonemizeBatchAsync(texts, "en");
            
            Assert.AreEqual(3, results.Length);
            Assert.AreEqual("hello", results[0].OriginalText);
            Assert.AreEqual("world", results[1].OriginalText);
            Assert.AreEqual("test", results[2].OriginalText);
            
            foreach (var result in results)
            {
                Assert.Greater(result.Phonemes.Length, 0);
            }
        }

        [Test]
        public async Task PhonemizeBatch_EmptyArray_ReturnsEmptyArray()
        {
            var results = await _phonemizer.PhonemizeBatchAsync(new string[0], "en");
            Assert.AreEqual(0, results.Length);
            
            results = await _phonemizer.PhonemizeBatchAsync(null, "en");
            Assert.AreEqual(0, results.Length);
        }

        #endregion

        #region Language Support Tests

        [Test]
        public void IsLanguageSupported_ChecksCorrectly()
        {
            Assert.IsTrue(_phonemizer.IsLanguageSupported("en"));
            Assert.IsTrue(_phonemizer.IsLanguageSupported("ja"));
            Assert.IsFalse(_phonemizer.IsLanguageSupported("unsupported"));
            Assert.IsFalse(_phonemizer.IsLanguageSupported(""));
            Assert.IsFalse(_phonemizer.IsLanguageSupported(null));
        }

        [Test]
        public void GetLanguageInfo_ReturnsCorrectInfo()
        {
            var enInfo = _phonemizer.GetLanguageInfo("en");
            Assert.IsNotNull(enInfo);
            Assert.AreEqual("en", enInfo.Code);
            Assert.AreEqual("English", enInfo.Name);
            
            var jaInfo = _phonemizer.GetLanguageInfo("ja");
            Assert.IsNotNull(jaInfo);
            Assert.AreEqual("ja", jaInfo.Code);
            Assert.AreEqual("Japanese", jaInfo.Name);
            
            var unknownInfo = _phonemizer.GetLanguageInfo("unknown");
            Assert.IsNull(unknownInfo);
        }

        #endregion

        #region Performance Tests

        [Test]
        public async Task ProcessingTime_RecordedCorrectly()
        {
            _phonemizer.DelayMilliseconds = 5; // Very short delay
            
            var result = await _phonemizer.PhonemizeAsync("test", "en");
            
            Assert.GreaterOrEqual(result.ProcessingTime.TotalMilliseconds, 0);
            Assert.LessOrEqual(result.ProcessingTime.TotalMilliseconds, 50); // Allow some overhead
        }

        [Test]
        public async Task CachedResult_HasFasterProcessingTime()
        {
            _phonemizer.DelayMilliseconds = 5; // Short delay
            
            var result1 = await _phonemizer.PhonemizeAsync("test", "en");
            
            _phonemizer.DelayMilliseconds = 0; // Remove delay
            
            var result2 = await _phonemizer.PhonemizeAsync("test", "en");
            
            Assert.IsTrue(result2.FromCache);
            // Cached result should be retrieved much faster
            Assert.LessOrEqual(result2.ProcessingTime.TotalMilliseconds, 5);
        }

        #endregion

        #region Sync Method Tests

        [Test]
        public void Phonemize_SyncMethod_Works()
        {
            var result = _phonemizer.Phonemize("test", "en");
            
            Assert.AreEqual("test", result.OriginalText);
            Assert.Greater(result.Phonemes.Length, 0);
            Assert.IsFalse(result.FromCache);
        }

        #endregion

        #region Disposal Tests

        [Test]
        public void Dispose_CleansUpResources()
        {
            var phonemizer = new TestPhonemizer();
            phonemizer.Phonemize("test", "en");
            
            phonemizer.Dispose();
            
            // Should not throw
            phonemizer.Dispose();
        }

        #endregion

        /// <summary>
        /// Test implementation of BasePhonemizer for testing.
        /// </summary>
        private class TestPhonemizer : BasePhonemizer
        {
            public override string Name => "Test Phonemizer";
            public override string Version => "1.0.0";
            public override string[] SupportedLanguages => new[] { "en", "ja" };
            
            public int InternalCallCount { get; private set; }
            public string LastNormalizedText { get; private set; }
            public bool SimulateError { get; set; }
            public int DelayMilliseconds { get; set; }

            protected override async Task<PhonemeResult> PhonemizeInternalAsync(
                string normalizedText, 
                string language, 
                CancellationToken cancellationToken)
            {
                InternalCallCount++;
                LastNormalizedText = normalizedText;
                
                if (SimulateError)
                {
                    throw new InvalidOperationException("Simulated error");
                }
                
                if (DelayMilliseconds > 0)
                {
                    await Task.Delay(DelayMilliseconds, cancellationToken);
                }
                
                // Simple mock phonemization
                var phonemes = normalizedText.ToCharArray();
                var phonemeStrings = new string[phonemes.Length];
                var phonemeIds = new int[phonemes.Length];
                
                for (int i = 0; i < phonemes.Length; i++)
                {
                    phonemeStrings[i] = phonemes[i].ToString();
                    phonemeIds[i] = (int)phonemes[i];
                }
                
                return new PhonemeResult
                {
                    Phonemes = phonemeStrings,
                    PhonemeIds = phonemeIds,
                    Durations = new float[phonemes.Length],
                    Pitches = new float[phonemes.Length]
                };
            }
        }
    }
}