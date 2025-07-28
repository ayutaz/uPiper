using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Profiling;
using uPiper.Core.Phonemizers.Backend;
using uPiper.Core.Phonemizers.Backend.RuleBased;
using uPiper.Core.Phonemizers.Backend.Flite;
using uPiper.Core.Phonemizers.Threading;
using uPiper.Core.Phonemizers.Caching;
using Debug = UnityEngine.Debug;

namespace uPiper.Tests.Phonemizers
{
    /// <summary>
    /// Performance tests for the phonemizer system
    /// </summary>
    [TestFixture]
    public class PhonemizerPerformanceTests
    {
        private List<string> testSentences;
        private List<string> testWords;
        private Stopwatch stopwatch;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            stopwatch = new Stopwatch();
            
            // Prepare test data
            testSentences = new List<string>
            {
                "The quick brown fox jumps over the lazy dog.",
                "Pack my box with five dozen liquor jugs.",
                "How vexingly quick daft zebras jump!",
                "The five boxing wizards jump quickly.",
                "Sphinx of black quartz, judge my vow.",
                "Two driven jocks help fax my big quiz.",
                "Five quacking zephyrs jolt my wax bed.",
                "The jay, pig, fox, zebra and my wolves quack!",
                "Jinxed wizards pluck ivy from the big quilt.",
                "Crazy Frederick bought many very exquisite opal jewels."
            };

            testWords = new List<string>
            {
                "hello", "world", "computer", "software", "hardware",
                "algorithm", "performance", "optimization", "benchmark", "testing",
                "unity", "engine", "phoneme", "synthesis", "audio",
                "language", "processing", "natural", "speech", "recognition"
            };
        }

        #region Memory Performance Tests

        [Test]
        public async Task Memory_PhonemizerShouldNotLeak()
        {
            var backend = new RuleBasedPhonemizer();
            await backend.InitializeAsync(Application.temporaryCachePath);

            // Get initial memory
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
            long initialMemory = System.GC.GetTotalMemory(false);

            // Perform many operations
            for (int i = 0; i < 1000; i++)
            {
                var text = testSentences[i % testSentences.Count];
                await backend.PhonemizeAsync(text, "en-US");
            }

            // Clean up and measure
            backend.Dispose();
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
            long finalMemory = System.GC.GetTotalMemory(false);

            long memoryDelta = finalMemory - initialMemory;
            float memoryDeltaMB = memoryDelta / (1024f * 1024f);

            Debug.Log($"Memory delta after 1000 operations: {memoryDeltaMB:F2} MB");
            Assert.Less(memoryDeltaMB, 10f, "Memory usage should not increase by more than 10MB");
        }

        [Test]
        public void Memory_CacheShouldRespectMemoryLimit()
        {
            const long memoryLimit = 1024 * 1024; // 1MB
            var cache = new LRUCache<string, PhonemeResult>(1000, memoryLimit);

            // Add items until memory limit is reached
            int itemsAdded = 0;
            for (int i = 0; i < 1000; i++)
            {
                var key = $"key_{i}";
                var result = new PhonemeResult
                {
                    Phonemes = Enumerable.Range(0, 100).Select(j => $"phoneme_{j}").ToList()
                };

                cache.Set(key, result);
                itemsAdded++;

                var stats = cache.GetStatistics();
                if (stats.memoryBytes >= memoryLimit)
                {
                    break;
                }
            }

            var finalStats = cache.GetStatistics();
            Debug.Log($"Cache stats - Items: {finalStats.count}, Memory: {finalStats.memoryBytes / 1024f:F2} KB");
            
            Assert.LessOrEqual(finalStats.memoryBytes, memoryLimit * 1.1f, 
                "Cache should not exceed memory limit by more than 10%");
            Assert.Greater(itemsAdded, 10, "Should be able to add reasonable number of items");
        }

        #endregion

        #region Throughput Tests

        [Test]
        public async Task Throughput_RuleBasedPhonemizer()
        {
            var backend = new RuleBasedPhonemizer();
            await backend.InitializeAsync(Application.temporaryCachePath);

            const int iterations = 1000;
            stopwatch.Restart();

            for (int i = 0; i < iterations; i++)
            {
                var text = testWords[i % testWords.Count];
                await backend.PhonemizeAsync(text, "en-US");
            }

            stopwatch.Stop();

            double opsPerSecond = iterations / stopwatch.Elapsed.TotalSeconds;
            Debug.Log($"RuleBased throughput: {opsPerSecond:F0} operations/second");

            Assert.Greater(opsPerSecond, 100, "Should process at least 100 words per second");
            backend.Dispose();
        }

        [Test]
        public async Task Throughput_FlitePhonemizer()
        {
            var backend = new FlitePhonemizerBackend();

            const int iterations = 1000;
            stopwatch.Restart();

            for (int i = 0; i < iterations; i++)
            {
                var text = testWords[i % testWords.Count];
                await backend.PhonemizeAsync(text, "en-US");
            }

            stopwatch.Stop();

            double opsPerSecond = iterations / stopwatch.Elapsed.TotalSeconds;
            Debug.Log($"Flite throughput: {opsPerSecond:F0} operations/second");

            Assert.Greater(opsPerSecond, 200, "Should process at least 200 words per second");
            backend.Dispose();
        }

        #endregion

        #region Concurrency Tests

        [Test]
        public async Task Concurrency_ThreadPoolShouldHandleParallelRequests()
        {
            const int poolSize = 4;
            const int totalRequests = 100;
            var pool = new ThreadSafePhonemizerPool(poolSize);

            var tasks = new List<Task<PhonemeResult>>();
            stopwatch.Restart();

            // Create parallel tasks
            for (int i = 0; i < totalRequests; i++)
            {
                int index = i;
                var task = Task.Run(async () =>
                {
                    var backend = pool.Rent();
                    try
                    {
                        var text = testSentences[index % testSentences.Count];
                        return await backend.PhonemizeAsync(text, "en-US");
                    }
                    finally
                    {
                        pool.Return(backend);
                    }
                });
                tasks.Add(task);
            }

            var results = await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Verify all completed successfully
            Assert.AreEqual(totalRequests, results.Length);
            Assert.IsTrue(results.All(r => r != null && r.Phonemes.Count > 0));

            double totalSeconds = stopwatch.Elapsed.TotalSeconds;
            double requestsPerSecond = totalRequests / totalSeconds;
            Debug.Log($"Concurrent processing: {requestsPerSecond:F0} requests/second with pool size {poolSize}");

            Assert.Greater(requestsPerSecond, 50, "Should handle at least 50 concurrent requests per second");
            pool.Dispose();
        }

        [UnityTest]
        public IEnumerator Concurrency_UnityServiceShouldHandleMultipleCoroutines()
        {
            const int concurrentRequests = 10;
            var completedCount = 0;
            var errors = new List<string>();

            // Start multiple concurrent coroutines
            for (int i = 0; i < concurrentRequests; i++)
            {
                int index = i;
                var text = testSentences[index % testSentences.Count];
                
                UnityPhonemizerService.Instance.PhonemizeAsync(
                    text, "en-US",
                    result => 
                    { 
                        completedCount++;
                        Assert.IsNotEmpty(result.Phonemes);
                    },
                    error => 
                    { 
                        errors.Add($"Request {index}: {error.Message}");
                        completedCount++;
                    }
                );
            }

            // Wait for all to complete
            float timeout = 10f;
            float elapsed = 0f;
            while (completedCount < concurrentRequests && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(concurrentRequests, completedCount, "All requests should complete");
            Assert.IsEmpty(errors, $"No errors should occur: {string.Join(", ", errors)}");
        }

        #endregion

        #region Latency Tests

        [Test]
        public async Task Latency_FirstCallShouldBeReasonable()
        {
            var backend = new FlitePhonemizerBackend();
            
            stopwatch.Restart();
            var result = await backend.PhonemizeAsync("Hello world", "en-US");
            stopwatch.Stop();

            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result.Phonemes);

            double latencyMs = stopwatch.Elapsed.TotalMilliseconds;
            Debug.Log($"First call latency: {latencyMs:F2} ms");

            Assert.Less(latencyMs, 100, "First call should complete within 100ms");
            backend.Dispose();
        }

        [Test]
        public async Task Latency_SubsequentCallsShouldBeFaster()
        {
            var backend = new RuleBasedPhonemizer();
            await backend.InitializeAsync(Application.temporaryCachePath);

            // Warm up
            await backend.PhonemizeAsync("warm up", "en-US");

            // Measure subsequent calls
            var latencies = new List<double>();
            for (int i = 0; i < 10; i++)
            {
                stopwatch.Restart();
                await backend.PhonemizeAsync(testWords[i], "en-US");
                stopwatch.Stop();
                latencies.Add(stopwatch.Elapsed.TotalMilliseconds);
            }

            double avgLatency = latencies.Average();
            double maxLatency = latencies.Max();

            Debug.Log($"Average latency: {avgLatency:F2} ms, Max: {maxLatency:F2} ms");

            Assert.Less(avgLatency, 10, "Average latency should be under 10ms");
            Assert.Less(maxLatency, 20, "Max latency should be under 20ms");
            
            backend.Dispose();
        }

        #endregion

        #region Scalability Tests

        [Test]
        public async Task Scalability_LargeTextProcessing()
        {
            var backend = new FlitePhonemizerBackend();
            
            // Create a large text (simulate a paragraph)
            var largeText = string.Join(" ", Enumerable.Repeat(testSentences, 10).SelectMany(x => x));
            var wordCount = largeText.Split(' ').Length;

            stopwatch.Restart();
            var result = await backend.PhonemizeAsync(largeText, "en-US");
            stopwatch.Stop();

            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result.Phonemes);

            double processingTime = stopwatch.Elapsed.TotalSeconds;
            double wordsPerSecond = wordCount / processingTime;

            Debug.Log($"Large text processing: {wordCount} words in {processingTime:F2}s = {wordsPerSecond:F0} words/second");

            Assert.Greater(wordsPerSecond, 100, "Should process at least 100 words per second for large texts");
            backend.Dispose();
        }

        [UnityTest]
        public IEnumerator Scalability_BatchProcessingEfficiency()
        {
            int[] batchSizes = { 1, 5, 10, 20, 50 };
            var efficiencies = new Dictionary<int, float>();

            foreach (int batchSize in batchSizes)
            {
                var texts = testSentences.Take(batchSize).ToList();
                float startTime = Time.realtimeSinceStartup;
                List<PhonemeResult> results = null;

                UnityPhonemizerService.Instance.PhonemizeBatch(
                    texts, "en-US",
                    r => results = r
                );

                yield return new WaitUntil(() => results != null);

                float elapsedTime = Time.realtimeSinceStartup - startTime;
                float timePerItem = elapsedTime / batchSize;
                efficiencies[batchSize] = timePerItem;

                Debug.Log($"Batch size {batchSize}: {timePerItem * 1000:F2} ms per item");
            }

            // Verify that larger batches are more efficient
            Assert.Less(efficiencies[10], efficiencies[1], 
                "Batch processing should be more efficient than individual processing");
        }

        #endregion

        #region Resource Usage Tests

        [Test]
        public void ResourceUsage_ObjectPoolEfficiency()
        {
            const int poolSize = 4;
            const int iterations = 1000;
            var pool = new ThreadSafeObjectPool<TestResource>(
                () => new TestResource(),
                poolSize
            );

            int createdCount = TestResource.CreatedCount;
            stopwatch.Restart();

            // Simulate high-frequency rent/return
            for (int i = 0; i < iterations; i++)
            {
                var resource = pool.Rent();
                Assert.IsNotNull(resource);
                pool.Return(resource);
            }

            stopwatch.Stop();

            int actualCreated = TestResource.CreatedCount - createdCount;
            double opsPerSecond = iterations / stopwatch.Elapsed.TotalSeconds;

            Debug.Log($"Object pool: {actualCreated} objects created for {iterations} operations");
            Debug.Log($"Pool efficiency: {opsPerSecond:F0} rent/return operations per second");

            Assert.LessOrEqual(actualCreated, poolSize * 2, 
                "Pool should reuse objects efficiently");
            Assert.Greater(opsPerSecond, 10000, 
                "Pool should handle at least 10,000 operations per second");
        }

        private class TestResource
        {
            private static int createdCount = 0;
            public static int CreatedCount => createdCount;

            public TestResource()
            {
                System.Threading.Interlocked.Increment(ref createdCount);
            }
        }

        #endregion

        #region Mobile Performance Tests

        [UnityTest]
        [UnityPlatform(RuntimePlatform.Android, RuntimePlatform.IPhonePlayer)]
        public IEnumerator Mobile_ShouldMaintainFrameRate()
        {
            const int targetFPS = 30;
            const float testDuration = 5f;
            var frameTimes = new List<float>();

            // Start continuous phonemization
            bool keepRunning = true;
            int processedCount = 0;

            var phonemizationTask = Task.Run(async () =>
            {
                while (keepRunning)
                {
                    await UnityPhonemizerService.Instance.PhonemizeAsync(
                        testSentences[processedCount % testSentences.Count], 
                        "en-US"
                    );
                    processedCount++;
                }
            });

            // Monitor frame rate
            float startTime = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startTime < testDuration)
            {
                frameTimes.Add(Time.deltaTime);
                yield return null;
            }

            keepRunning = false;
            yield return new WaitUntil(() => phonemizationTask.IsCompleted);

            // Analyze frame rate
            float avgFrameTime = frameTimes.Average();
            float avgFPS = 1f / avgFrameTime;
            float minFPS = 1f / frameTimes.Max();

            Debug.Log($"Mobile performance - Avg FPS: {avgFPS:F1}, Min FPS: {minFPS:F1}, Processed: {processedCount}");

            Assert.Greater(avgFPS, targetFPS * 0.9f, 
                $"Average FPS should be at least 90% of target ({targetFPS})");
            Assert.Greater(minFPS, targetFPS * 0.5f, 
                "Minimum FPS should not drop below 50% of target");
        }

        #endregion
    }
}