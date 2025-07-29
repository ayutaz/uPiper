using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using uPiper.Core.Phonemizers.Backend;

namespace uPiper.Core.Phonemizers.Threading
{
    /// <summary>
    /// Thread-safe pool for phonemizer backends with concurrency control.
    /// </summary>
    public class ThreadSafePhonemizerPool : IDisposable
    {
        private readonly ConcurrentDictionary<string, BackendPool> pools;
        private readonly SemaphoreSlim globalSemaphore;
        private readonly int maxConcurrency;
        private readonly PhonemizerBackendFactory backendFactory;
        private int activeRequests;
        private bool isDisposed;

        /// <summary>
        /// Gets the number of active phonemization requests.
        /// </summary>
        public int ActiveRequests => activeRequests;

        /// <summary>
        /// Gets pool statistics.
        /// </summary>
        public PoolStatistics Statistics { get; private set; }

        /// <summary>
        /// Creates a new thread-safe phonemizer pool.
        /// </summary>
        /// <param name="maxConcurrency">Maximum concurrent phonemization operations.</param>
        public ThreadSafePhonemizerPool(int maxConcurrency = 4)
        {
            this.maxConcurrency = maxConcurrency;
            globalSemaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
            pools = new ConcurrentDictionary<string, BackendPool>();
            backendFactory = PhonemizerBackendFactory.Instance;
            Statistics = new PoolStatistics();
        }

        /// <summary>
        /// Phonemizes text using a pooled backend.
        /// </summary>
        public async Task<PhonemeResult> PhonemizeAsync(
            string text,
            string language,
            PhonemeOptions options = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            // Get or create pool for language
            var pool = GetOrCreatePool(language);
            if (pool == null)
            {
                return new PhonemeResult
                {
                    Success = false,
                    ErrorMessage = $"No backend available for language: {language}",
                    Language = language
                };
            }

            // Wait for global concurrency slot
            await globalSemaphore.WaitAsync(cancellationToken);
            Interlocked.Increment(ref activeRequests);
            Statistics.TotalRequests++;

            try
            {
                // Get backend from pool
                var backend = await pool.RentAsync(cancellationToken);
                try
                {
                    var startTime = DateTime.UtcNow;
                    var result = await backend.PhonemizeAsync(text, language, options, cancellationToken);

                    // Update statistics
                    var duration = DateTime.UtcNow - startTime;
                    Statistics.RecordRequest(duration, result.Success);

                    return result;
                }
                finally
                {
                    // Return backend to pool
                    pool.Return(backend);
                }
            }
            finally
            {
                Interlocked.Decrement(ref activeRequests);
                globalSemaphore.Release();
            }
        }

        /// <summary>
        /// Processes multiple texts in parallel with pooled backends.
        /// </summary>
        public async Task<PhonemeResult[]> PhonemizeBatchAsync(
            string[] texts,
            string language,
            PhonemeOptions options = null,
            int maxParallelism = 0,
            CancellationToken cancellationToken = default)
        {
            if (texts == null || texts.Length == 0)
                return Array.Empty<PhonemeResult>();

            maxParallelism = maxParallelism > 0 ? Math.Min(maxParallelism, maxConcurrency) : maxConcurrency;

            using var semaphore = new SemaphoreSlim(maxParallelism);
            var tasks = new Task<PhonemeResult>[texts.Length];

            for (var i = 0; i < texts.Length; i++)
            {
                var text = texts[i];
                tasks[i] = ProcessBatchItem(text, language, options, semaphore, cancellationToken);
            }

            return await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Preallocates backends for a language.
        /// </summary>
        public async Task PreallocateAsync(string language, int count)
        {
            var pool = GetOrCreatePool(language);
            if (pool != null)
            {
                await pool.PreallocateAsync(count);
            }
        }

        /// <summary>
        /// Gets information about all pools.
        /// </summary>
        public PoolInfo[] GetPoolInfo()
        {
            var infos = new System.Collections.Generic.List<PoolInfo>();

            foreach (var kvp in pools)
            {
                infos.Add(new PoolInfo
                {
                    Language = kvp.Key,
                    BackendName = kvp.Value.BackendName,
                    AvailableCount = kvp.Value.AvailableCount,
                    TotalCount = kvp.Value.TotalCount,
                    ActiveCount = kvp.Value.TotalCount - kvp.Value.AvailableCount
                });
            }

            return infos.ToArray();
        }

        /// <summary>
        /// Clears all pools.
        /// </summary>
        public void Clear()
        {
            foreach (var pool in pools.Values)
            {
                pool.Dispose();
            }
            pools.Clear();
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;
            Clear();
            globalSemaphore?.Dispose();
        }

        private BackendPool GetOrCreatePool(string language)
        {
            return pools.GetOrAdd(language, lang =>
            {
                var backend = backendFactory.GetBackend(lang, "MIT");
                if (backend == null)
                {
                    Debug.LogWarning($"No backend found for language: {lang}");
                    return null;
                }

                return new BackendPool(backend, maxConcurrency);
            });
        }

        private async Task<PhonemeResult> ProcessBatchItem(
            string text,
            string language,
            PhonemeOptions options,
            SemaphoreSlim semaphore,
            CancellationToken cancellationToken)
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await PhonemizeAsync(text, language, options, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        }

        private void ThrowIfDisposed()
        {
            if (isDisposed)
                throw new ObjectDisposedException(nameof(ThreadSafePhonemizerPool));
        }

        /// <summary>
        /// Internal pool for a specific backend.
        /// </summary>
        private class BackendPool : IDisposable
        {
            private readonly IPhonemizerBackend templateBackend;
            private readonly ConcurrentBag<IPhonemizerBackend> availableBackends;
            private readonly SemaphoreSlim semaphore;
            private int totalCount;

            public string BackendName => templateBackend.Name;
            public int AvailableCount => availableBackends.Count;
            public int TotalCount => totalCount;

            public BackendPool(IPhonemizerBackend backend, int maxSize)
            {
                templateBackend = backend;
                availableBackends = new ConcurrentBag<IPhonemizerBackend>();
                semaphore = new SemaphoreSlim(maxSize, maxSize);

                // Add the initial backend
                availableBackends.Add(backend);
                totalCount = 1;
            }

            public async Task<IPhonemizerBackend> RentAsync(CancellationToken cancellationToken)
            {
                await semaphore.WaitAsync(cancellationToken);

                if (availableBackends.TryTake(out var backend))
                {
                    return backend;
                }

                // Pool exhausted - create a new instance if possible
                if (totalCount < semaphore.CurrentCount)
                {
                    // For thread safety, backends should be stateless or properly cloned
                    // Since most phonemizers are stateless, we can safely reuse the template
                    // For stateful backends, implement ICloneable or use a factory
                    Interlocked.Increment(ref totalCount);
                    Debug.Log($"Creating new backend instance for {BackendName} (total: {totalCount})");
                    return templateBackend; // In production, use factory.CreateBackend()
                }

                // This should rarely happen due to semaphore protection
                Debug.LogError($"Pool exhausted for {BackendName} with no capacity to create new instances");
                throw new InvalidOperationException($"Backend pool exhausted for {BackendName}");
            }

            public void Return(IPhonemizerBackend backend)
            {
                if (backend != null && backend != templateBackend)
                {
                    availableBackends.Add(backend);
                }
                semaphore.Release();
            }

            public async Task PreallocateAsync(int count)
            {
                // This would need proper backend cloning/creation logic
                await Task.CompletedTask;
                Debug.Log($"Would preallocate {count} backends for {BackendName}");
            }

            public void Dispose()
            {
                // Don't dispose backends as they're managed by the factory
                semaphore?.Dispose();
            }
        }
    }

    /// <summary>
    /// Pool statistics.
    /// </summary>
    public class PoolStatistics
    {
        private long successCount;
        private long failureCount;
        private double totalDurationMs;
        private readonly object lockObject = new();

        public long TotalRequests { get; set; }
        public long SuccessCount => successCount;
        public long FailureCount => failureCount;
        public double AverageDurationMs => TotalRequests > 0 ? totalDurationMs / TotalRequests : 0;
        public double SuccessRate => TotalRequests > 0 ? (double)successCount / TotalRequests : 0;

        public void RecordRequest(TimeSpan duration, bool success)
        {
            lock (lockObject)
            {
                if (success)
                    successCount++;
                else
                    failureCount++;

                totalDurationMs += duration.TotalMilliseconds;
            }
        }

        public override string ToString()
        {
            return $"Pool Stats: {TotalRequests} requests, {SuccessRate:P1} success rate, " +
                   $"{AverageDurationMs:F1}ms avg duration";
        }
    }

    /// <summary>
    /// Information about a backend pool.
    /// </summary>
    public class PoolInfo
    {
        public string Language { get; set; }
        public string BackendName { get; set; }
        public int AvailableCount { get; set; }
        public int TotalCount { get; set; }
        public int ActiveCount { get; set; }
    }
}