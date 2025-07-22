using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core;
using uPiper.Core.IL2CPP;
using Debug = UnityEngine.Debug;

namespace uPiper.Tests.Runtime.Performance
{
    /// <summary>
    /// Performance tests for IL2CPP vs Mono comparison
    /// </summary>
    public class IL2CPPPerformanceTest
    {
        private PiperConfig _config;
        private Stopwatch _stopwatch;

        [SetUp]
        public void SetUp()
        {
            _config = PiperConfig.CreateDefault();
            _stopwatch = new Stopwatch();
        }

        [Test]
        public void RuntimeInfo_DisplayCurrentBackend()
        {
            Debug.Log("=== Runtime Information ===");
            Debug.Log($"Is IL2CPP: {IL2CPPCompatibility.PlatformSettings.IsIL2CPP}");
            Debug.Log($"Unity Version: {Application.unityVersion}");
            Debug.Log($"Platform: {Application.platform}");
            Debug.Log($"Processor Count: {SystemInfo.processorCount}");
            Debug.Log($"System Memory: {SystemInfo.systemMemorySize} MB");
            Debug.Log($"Recommended Worker Threads: {IL2CPPCompatibility.PlatformSettings.GetRecommendedWorkerThreads()}");
            Debug.Log($"Recommended Cache Size: {IL2CPPCompatibility.PlatformSettings.GetRecommendedCacheSizeMB()} MB");
        }

        [Test]
        public void Marshalling_StringToIntPtr_Performance()
        {
            const int iterations = 1000;
            var testStrings = new[]
            {
                "Hello World",
                "こんにちは世界",
                "A very long string that contains multiple words and should test the marshalling performance",
                "短い",
                "Mixed 混合 Text テキスト"
            };

            // Warm-up
            foreach (var str in testStrings)
            {
                var ptr = IL2CPPCompatibility.MarshallingHelpers.StringToHGlobalUTF8(str);
                IL2CPPCompatibility.MarshallingHelpers.FreeHGlobalUTF8(ptr);
            }

            // Actual test
            _stopwatch.Restart();
            for (int i = 0; i < iterations; i++)
            {
                foreach (var str in testStrings)
                {
                    var ptr = IL2CPPCompatibility.MarshallingHelpers.StringToHGlobalUTF8(str);
                    IL2CPPCompatibility.MarshallingHelpers.FreeHGlobalUTF8(ptr);
                }
            }
            _stopwatch.Stop();

            var totalOperations = iterations * testStrings.Length;
            var avgTimeUs = (_stopwatch.ElapsedMilliseconds * 1000.0) / totalOperations;

            Debug.Log($"String Marshalling Performance:");
            Debug.Log($"  Total operations: {totalOperations}");
            Debug.Log($"  Total time: {_stopwatch.ElapsedMilliseconds} ms");
            Debug.Log($"  Average time per operation: {avgTimeUs:F2} μs");
            Debug.Log($"  Operations per second: {(totalOperations * 1000.0 / _stopwatch.ElapsedMilliseconds):F0}");

            // Performance assertion
            Assert.Less(avgTimeUs, 100, "String marshalling should complete within 100 microseconds");
        }

        [Test]
        public void Collections_DictionaryPerformance()
        {
            const int itemCount = 10000;
            var dictionary = new Dictionary<string, AudioClip>();

            // Test insertion
            _stopwatch.Restart();
            for (int i = 0; i < itemCount; i++)
            {
                dictionary[$"key_{i}"] = null; // Using null for AudioClip in test
            }
            _stopwatch.Stop();
            var insertTime = _stopwatch.ElapsedMilliseconds;

            // Test lookup
            _stopwatch.Restart();
            for (int i = 0; i < itemCount; i++)
            {
                var value = dictionary[$"key_{i}"];
            }
            _stopwatch.Stop();
            var lookupTime = _stopwatch.ElapsedMilliseconds;

            // Test removal
            _stopwatch.Restart();
            for (int i = 0; i < itemCount / 2; i++)
            {
                dictionary.Remove($"key_{i}");
            }
            _stopwatch.Stop();
            var removeTime = _stopwatch.ElapsedMilliseconds;

            Debug.Log($"Dictionary Performance ({itemCount} items):");
            Debug.Log($"  Insertion: {insertTime} ms ({(itemCount * 1000.0 / insertTime):F0} ops/sec)");
            Debug.Log($"  Lookup: {lookupTime} ms ({(itemCount * 1000.0 / lookupTime):F0} ops/sec)");
            Debug.Log($"  Removal: {removeTime} ms ({(itemCount * 500.0 / removeTime):F0} ops/sec)");

            // Performance assertions
            Assert.Less(insertTime, 100, "Dictionary insertion should complete within 100ms");
            Assert.Less(lookupTime, 50, "Dictionary lookup should complete within 50ms");
            Assert.Less(removeTime, 50, "Dictionary removal should complete within 50ms");
        }

        [UnityTest]
        public IEnumerator<object> AsyncPerformance_TaskCreation()
        {
            const int taskCount = 100;
            var tasks = new List<Task>();

            _stopwatch.Restart();
            for (int i = 0; i < taskCount; i++)
            {
                int index = i;
                tasks.Add(Task.Run(async () =>
                {
                    await Task.Delay(1);
                    return index * 2;
                }));
            }

            // Wait for all tasks
            yield return new WaitUntil(() => Task.WhenAll(tasks).IsCompleted);

            _stopwatch.Stop();

            Debug.Log($"Async Task Performance:");
            Debug.Log($"  Created {taskCount} tasks");
            Debug.Log($"  Total time: {_stopwatch.ElapsedMilliseconds} ms");
            Debug.Log($"  Average time per task: {(_stopwatch.ElapsedMilliseconds / (double)taskCount):F2} ms");

            // Performance assertion
            Assert.Less(_stopwatch.ElapsedMilliseconds, 500, "Task creation and execution should complete within 500ms");
        }

        [Test]
        public void Memory_AllocationPattern()
        {
            if (!IL2CPPCompatibility.PlatformSettings.IsIL2CPP)
            {
                Debug.Log("Memory allocation test - Running on Mono");
            }
            else
            {
                Debug.Log("Memory allocation test - Running on IL2CPP");
            }

            var beforeGC = System.GC.GetTotalMemory(false);

            // Allocate various objects
            var lists = new List<List<float>>();
            for (int i = 0; i < 100; i++)
            {
                var list = new List<float>(1000);
                for (int j = 0; j < 1000; j++)
                {
                    list.Add(j * 0.1f);
                }
                lists.Add(list);
            }

            var afterAlloc = System.GC.GetTotalMemory(false);

            // Force garbage collection
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();

            var afterGC = System.GC.GetTotalMemory(false);

            Debug.Log($"Memory Allocation Pattern:");
            Debug.Log($"  Before allocation: {beforeGC / 1024:F0} KB");
            Debug.Log($"  After allocation: {afterAlloc / 1024:F0} KB");
            Debug.Log($"  After GC: {afterGC / 1024:F0} KB");
            Debug.Log($"  Allocated: {(afterAlloc - beforeGC) / 1024:F0} KB");
            Debug.Log($"  Collected: {(afterAlloc - afterGC) / 1024:F0} KB");
        }

        [Test]
        public void PInvoke_OverheadEstimation()
        {
            // Note: This is a simulation since we can't measure actual P/Invoke overhead directly
            const int callCount = 10000;

            // Simulate P/Invoke call overhead by measuring delegate invocation
            Func<int, int> nativeSimulation = (x) => x * 2;

            // Warm-up
            for (int i = 0; i < 100; i++)
            {
                nativeSimulation(i);
            }

            _stopwatch.Restart();
            for (int i = 0; i < callCount; i++)
            {
                var result = nativeSimulation(i);
            }
            _stopwatch.Stop();

            var avgTimeNs = (_stopwatch.ElapsedTicks * 1000000000.0 / Stopwatch.Frequency) / callCount;

            Debug.Log($"P/Invoke Overhead Estimation:");
            Debug.Log($"  Total calls: {callCount}");
            Debug.Log($"  Total time: {_stopwatch.ElapsedMilliseconds} ms");
            Debug.Log($"  Average time per call: {avgTimeNs:F0} ns");
            Debug.Log($"  Calls per second: {(callCount * 1000.0 / _stopwatch.ElapsedMilliseconds):F0}");

            if (IL2CPPCompatibility.PlatformSettings.IsIL2CPP)
            {
                Debug.Log("  Note: IL2CPP typically has lower P/Invoke overhead than Mono");
            }
        }
    }
}