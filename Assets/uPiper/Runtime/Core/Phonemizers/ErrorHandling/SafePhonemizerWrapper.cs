using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using uPiper.Core.Phonemizers.Backend;

namespace uPiper.Core.Phonemizers.ErrorHandling
{
    /// <summary>
    /// Safe wrapper for phonemizer backends with circuit breaker and fallback support.
    /// </summary>
    public class SafePhonemizerWrapper : PhonemizerBackendBase
    {
        private readonly IPhonemizerBackend primaryBackend;
        private readonly IPhonemizerBackend fallbackBackend;
        private readonly ICircuitBreaker circuitBreaker;
        private readonly int maxRetries;
        private readonly TimeSpan retryDelay;

        /// <inheritdoc/>
        public override string Name => $"Safe({primaryBackend?.Name ?? "None"})";

        /// <inheritdoc/>
        public override string Version => "1.0.0";

        /// <inheritdoc/>
        public override string License => primaryBackend?.License ?? "MIT";

        /// <inheritdoc/>
        public override string[] SupportedLanguages =>
            primaryBackend?.SupportedLanguages ?? new[] { "en" };

        /// <summary>
        /// Creates a safe wrapper with circuit breaker protection.
        /// </summary>
        /// <param name="primaryBackend">Primary backend to use.</param>
        /// <param name="fallbackBackend">Fallback backend for failures.</param>
        /// <param name="circuitBreaker">Circuit breaker instance.</param>
        /// <param name="maxRetries">Maximum retry attempts.</param>
        /// <param name="retryDelay">Delay between retries.</param>
        public SafePhonemizerWrapper(
            IPhonemizerBackend primaryBackend,
            IPhonemizerBackend fallbackBackend = null,
            ICircuitBreaker circuitBreaker = null,
            int maxRetries = 3,
            TimeSpan? retryDelay = null)
        {
            this.primaryBackend = primaryBackend ?? throw new ArgumentNullException(nameof(primaryBackend));
            this.fallbackBackend = fallbackBackend;
            this.circuitBreaker = circuitBreaker ?? new CircuitBreaker();
            this.maxRetries = maxRetries;
            this.retryDelay = retryDelay ?? TimeSpan.FromMilliseconds(100);
        }

        /// <inheritdoc/>
        protected override async Task<bool> InitializeInternalAsync(
            PhonemizerBackendOptions options,
            CancellationToken cancellationToken)
        {
            var primaryInit = false;
            var fallbackInit = false;

            // Initialize primary backend
            try
            {
                primaryInit = await primaryBackend.InitializeAsync(options, cancellationToken);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize primary backend: {ex.Message}");
            }

            // Initialize fallback backend if available
            if (fallbackBackend != null)
            {
                try
                {
                    fallbackInit = await fallbackBackend.InitializeAsync(options, cancellationToken);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to initialize fallback backend: {ex.Message}");
                }
            }

            return primaryInit || fallbackInit;
        }

        /// <inheritdoc/>
        public override async Task<PhonemeResult> PhonemizeAsync(
            string text,
            string language,
            PhonemeOptions options = null,
            CancellationToken cancellationToken = default)
        {
            // Check if circuit breaker allows execution
            if (!circuitBreaker.CanExecute())
            {
                Debug.LogWarning("Circuit breaker is open, using fallback");
                return await UseFallback(text, language, options, cancellationToken);
            }

            // Try primary backend with retries
            Exception lastException = null;
            for (var attempt = 0; attempt < maxRetries; attempt++)
            {
                if (attempt > 0)
                {
                    await Task.Delay(retryDelay, cancellationToken);
                }

                try
                {
                    var result = await primaryBackend.PhonemizeAsync(
                        text, language, options, cancellationToken);

                    circuitBreaker.OnSuccess();
                    return result;
                }
                catch (OperationCanceledException)
                {
                    // Don't retry on cancellation
                    throw;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    Debug.LogWarning($"Attempt {attempt + 1} failed: {ex.Message}");

                    if (attempt == maxRetries - 1)
                    {
                        circuitBreaker.OnFailure(ex);
                    }
                }
            }

            // All retries failed, use fallback
            Debug.LogError($"All retry attempts failed, using fallback. Last error: {lastException?.Message}");
            return await UseFallback(text, language, options, cancellationToken);
        }

        /// <inheritdoc/>
        public override long GetMemoryUsage()
        {
            long total = 0;

            if (primaryBackend != null)
                total += primaryBackend.GetMemoryUsage();

            if (fallbackBackend != null)
                total += fallbackBackend.GetMemoryUsage();

            return total;
        }

        /// <inheritdoc/>
        public override BackendCapabilities GetCapabilities()
        {
            // Return primary capabilities, or fallback if primary not available
            return primaryBackend?.GetCapabilities() ??
                   fallbackBackend?.GetCapabilities() ??
                   new BackendCapabilities();
        }

        /// <inheritdoc/>
        protected override void DisposeInternal()
        {
            // Don't dispose backends as they might be shared
            // Just reset the circuit breaker
            circuitBreaker?.Reset();
        }

        private async Task<PhonemeResult> UseFallback(
            string text,
            string language,
            PhonemeOptions options,
            CancellationToken cancellationToken)
        {
            if (fallbackBackend != null && fallbackBackend.IsAvailable)
            {
                try
                {
                    var result = await fallbackBackend.PhonemizeAsync(
                        text, language, options, cancellationToken);

                    // Mark that fallback was used
                    result.Metadata ??= new Dictionary<string, object>();
                    result.Metadata["FallbackUsed"] = true;
                    return result;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Fallback backend also failed: {ex.Message}");
                }
            }

            // Both backends failed, return error result
            return CreateErrorResult(
                "Both primary and fallback backends failed",
                language);
        }

        /// <summary>
        /// Gets circuit breaker statistics.
        /// </summary>
        public CircuitBreakerStatistics GetCircuitBreakerStats()
        {
            return circuitBreaker.GetStatistics();
        }

        /// <summary>
        /// Manually resets the circuit breaker.
        /// </summary>
        public void ResetCircuitBreaker()
        {
            circuitBreaker.Reset();
        }
    }
}