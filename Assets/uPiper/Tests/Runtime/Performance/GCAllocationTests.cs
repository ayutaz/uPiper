using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using uPiper.Core.Phonemizers.Cache;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace uPiper.Tests.Runtime.Performance
{
    [TestFixture]
    [Category("Performance")]
    [Category("GCAllocation")]
    public class GCAllocationTests
    {
        [Test]
        public void LRUCache_Performance_Benchmark()
        {
            var cache = new LRUCache<string, string>(1000);

            // Prepare test data
            var testKeys = new string[100];
            var testValues = new string[100];
            for (var i = 0; i < 100; i++)
            {
                testKeys[i] = $"key{i}";
                testValues[i] = $"value{i}";
                cache.Add(testKeys[i], testValues[i]);
            }

            // Warm up
            for (var i = 0; i < 100; i++)
            {
                cache.TryGet(testKeys[i % 100], out _);
                cache.ContainsKey(testKeys[i % 100]);
            }

            // Benchmark TryGet
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            for (var i = 0; i < 100000; i++)
            {
                cache.TryGet(testKeys[i % 100], out _);
            }
            stopwatch.Stop();

            Assert.Less(stopwatch.ElapsedMilliseconds, 1000,
                $"TryGet performance: {stopwatch.ElapsedMilliseconds}ms for 100k operations");

            // Benchmark ContainsKey
            stopwatch.Restart();
            for (var i = 0; i < 100000; i++)
            {
                cache.ContainsKey(testKeys[i % 100]);
            }
            stopwatch.Stop();

            Assert.Less(stopwatch.ElapsedMilliseconds, 1000,
                $"ContainsKey performance: {stopwatch.ElapsedMilliseconds}ms for 100k operations");
        }

        [Test]
        public void LRUCache_GetStatistics_Allocation()
        {
            var cache = new LRUCache<string, string>(100);

            // Prepare cache with data
            for (var i = 0; i < 50; i++)
            {
                cache.Add($"key{i}", $"value{i}");
            }

            // GetStatistics creates a new Dictionary, so we expect allocation
            Assert.That(() =>
            {
                var stats = cache.GetStatistics();
            }, Is.AllocatingGCMemory());
        }

        [Test]
        public void LRUCache_ConcurrentAccess_Performance()
        {
            var cache = new LRUCache<string, string>(1000);
            var testKeys = new string[100];

            // Prepare data
            for (var i = 0; i < 100; i++)
            {
                testKeys[i] = $"key{i}";
                cache.Add(testKeys[i], $"value{i}");
            }

            // Test concurrent read performance
            var tasks = new System.Threading.Tasks.Task[4];
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            for (var t = 0; t < tasks.Length; t++)
            {
                var threadId = t;
                tasks[t] = System.Threading.Tasks.Task.Run(() =>
                {
                    for (var i = 0; i < 25000; i++)
                    {
                        cache.TryGet(testKeys[(i + threadId * 25) % 100], out _);
                    }
                });
            }

            System.Threading.Tasks.Task.WaitAll(tasks);
            stopwatch.Stop();

            Assert.Less(stopwatch.ElapsedMilliseconds, 2000,
                $"Concurrent access performance: {stopwatch.ElapsedMilliseconds}ms for 100k operations across 4 threads");
        }
    }
}