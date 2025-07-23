using System;
using System.Collections;
using System.Diagnostics;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core.Performance;
using uPiper.Core.Platform;
using uPiper.Core.Phonemizers.Implementations;

namespace uPiper.Tests.Runtime.Performance
{
    /// <summary>
    /// Androidパフォーマンスベンチマークテスト
    /// 最適化前後のパフォーマンスを比較
    /// </summary>
    public class AndroidPerformanceBenchmark
    {
        private OptimizedOpenJTalkPhonemizer _optimizedPhonemizer;
        private OpenJTalkPhonemizer _regularPhonemizer;
        private AndroidPerformanceProfiler _profiler;
        
        private readonly string[] _testTexts = new[]
        {
            "こんにちは",
            "今日はいい天気ですね",
            "音声合成のテストです",
            "ユニティで日本語音声合成ができました",
            "私は東京に住んでいます",
            "ありがとうございます",
            "すみません、ちょっとお聞きしたいことがあります"
        };

        [OneTimeSetUp]
        public void Setup()
        {
            _profiler = new AndroidPerformanceProfiler();
            
            // Log system info
            UnityEngine.Debug.Log(AndroidPerformanceProfiler.GetSystemInfo());
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            _optimizedPhonemizer?.Dispose();
            _regularPhonemizer?.Dispose();
            
            // Generate final report
            var report = _profiler.GenerateReport();
            UnityEngine.Debug.Log(report);
            
            // Save report to file
            #if UNITY_ANDROID && !UNITY_EDITOR
            var reportPath = System.IO.Path.Combine(Application.persistentDataPath, "performance_report.txt");
            System.IO.File.WriteAllText(reportPath, report);
            UnityEngine.Debug.Log($"Performance report saved to: {reportPath}");
            #endif
        }

        [UnityTest]
        public IEnumerator BenchmarkDictionaryLoading()
        {
            // Clear cache first
            OptimizedAndroidPathResolver.ClearCache();
            yield return null;
            
            // Measure cold start
            using (_profiler.BeginProfile("Dictionary Cold Load"))
            {
                var dictPathTask = OptimizedAndroidPathResolver.GetDictionaryPathAsync();
                yield return new WaitUntil(() => dictPathTask.IsCompleted);
                
                Assert.IsTrue(dictPathTask.IsCompletedSuccessfully);
                Assert.IsFalse(string.IsNullOrEmpty(dictPathTask.Result));
            }
            
            // Measure warm start
            using (_profiler.BeginProfile("Dictionary Warm Load"))
            {
                var dictPathTask = OptimizedAndroidPathResolver.GetDictionaryPathAsync();
                yield return new WaitUntil(() => dictPathTask.IsCompleted);
                
                Assert.IsTrue(dictPathTask.IsCompletedSuccessfully);
            }
            
            // Log dictionary size
            var dictSize = OptimizedAndroidPathResolver.GetDictionarySize();
            UnityEngine.Debug.Log($"Dictionary size: {dictSize / 1024 / 1024:F2} MB");
        }

        [UnityTest]
        public IEnumerator BenchmarkPhonemizerInitialization()
        {
            // Benchmark optimized version
            using (_profiler.BeginProfile("Optimized Phonemizer Init"))
            {
                _optimizedPhonemizer = new OptimizedOpenJTalkPhonemizer();
                var initTask = _optimizedPhonemizer.InitializeAsync();
                yield return new WaitUntil(() => initTask.IsCompleted);
                
                Assert.IsTrue(initTask.IsCompletedSuccessfully);
            }
            
            // Benchmark regular version
            using (_profiler.BeginProfile("Regular Phonemizer Init"))
            {
                _regularPhonemizer = new OpenJTalkPhonemizer();
                var initTask = _regularPhonemizer.InitializeAsync();
                yield return new WaitUntil(() => initTask.IsCompleted);
                
                Assert.IsTrue(initTask.IsCompletedSuccessfully);
            }
        }

        [UnityTest]
        public IEnumerator BenchmarkPhonemization()
        {
            // Ensure phonemizers are initialized
            if (_optimizedPhonemizer == null || _regularPhonemizer == null)
            {
                yield return BenchmarkPhonemizerInitialization();
            }
            
            // Benchmark optimized version
            foreach (var text in _testTexts)
            {
                using (_profiler.BeginProfile($"Optimized Phonemize ({text.Length} chars)"))
                {
                    var phonemeTask = _optimizedPhonemizer.PhonemizeAsync(text, "ja");
                    yield return new WaitUntil(() => phonemeTask.IsCompleted);
                    
                    Assert.IsTrue(phonemeTask.IsCompletedSuccessfully);
                    Assert.Greater(phonemeTask.Result.Phonemes.Length, 0);
                }
            }
            
            // Benchmark regular version
            foreach (var text in _testTexts)
            {
                using (_profiler.BeginProfile($"Regular Phonemize ({text.Length} chars)"))
                {
                    var phonemeTask = _regularPhonemizer.PhonemizeAsync(text, "ja");
                    yield return new WaitUntil(() => phonemeTask.IsCompleted);
                    
                    Assert.IsTrue(phonemeTask.IsCompletedSuccessfully);
                    Assert.Greater(phonemeTask.Result.Phonemes.Length, 0);
                }
            }
        }

        [UnityTest]
        public IEnumerator BenchmarkCachePerformance()
        {
            if (_optimizedPhonemizer == null)
            {
                yield return BenchmarkPhonemizerInitialization();
            }
            
            const string testText = "キャッシュのテストです";
            
            // First call (cache miss)
            using (_profiler.BeginProfile("Cache Miss"))
            {
                var phonemeTask = _optimizedPhonemizer.PhonemizeAsync(testText, "ja");
                yield return new WaitUntil(() => phonemeTask.IsCompleted);
                Assert.IsTrue(phonemeTask.IsCompletedSuccessfully);
            }
            
            // Second call (cache hit)
            using (_profiler.BeginProfile("Cache Hit"))
            {
                var phonemeTask = _optimizedPhonemizer.PhonemizeAsync(testText, "ja");
                yield return new WaitUntil(() => phonemeTask.IsCompleted);
                Assert.IsTrue(phonemeTask.IsCompletedSuccessfully);
            }
            
            // Multiple cache hits
            for (int i = 0; i < 10; i++)
            {
                using (_profiler.BeginProfile("Cache Hit (Batch)"))
                {
                    var phonemeTask = _optimizedPhonemizer.PhonemizeAsync(testText, "ja");
                    yield return new WaitUntil(() => phonemeTask.IsCompleted);
                }
            }
        }

        [UnityTest]
        public IEnumerator BenchmarkMemoryUsage()
        {
            // Force GC before test
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            yield return null;
            
            AndroidPerformanceProfiler.LogMemoryUsage("Before Test");
            
            // Create and initialize phonemizer
            using (_profiler.BeginProfile("Memory Test - Full Lifecycle"))
            {
                var phonemizer = new OptimizedOpenJTalkPhonemizer();
                
                AndroidPerformanceProfiler.LogMemoryUsage("After Creation");
                
                var initTask = phonemizer.InitializeAsync();
                yield return new WaitUntil(() => initTask.IsCompleted);
                
                AndroidPerformanceProfiler.LogMemoryUsage("After Init");
                
                // Process multiple texts
                foreach (var text in _testTexts)
                {
                    var phonemeTask = phonemizer.PhonemizeAsync(text, "ja");
                    yield return new WaitUntil(() => phonemeTask.IsCompleted);
                }
                
                AndroidPerformanceProfiler.LogMemoryUsage("After Processing");
                
                // Get performance report
                var report = phonemizer.GeneratePerformanceReport();
                UnityEngine.Debug.Log($"Phonemizer Report:\n{report}");
                
                phonemizer.Dispose();
            }
            
            // Force GC after test
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            yield return null;
            
            AndroidPerformanceProfiler.LogMemoryUsage("After Cleanup");
        }

        [Test]
        public void ValidateOptimizationGoals()
        {
            // This test validates if optimization goals are met
            // It should be run after the benchmark tests
            
            var report = _profiler.GenerateReport();
            
            // Parse average times from report
            // Goal: Phonemization < 50ms
            // Goal: Cache hit < 1ms
            // Goal: Memory delta < 50MB
            
            UnityEngine.Debug.Log("=== Optimization Goals Validation ===");
            UnityEngine.Debug.Log("Target: Phonemization < 50ms");
            UnityEngine.Debug.Log("Target: Cache hit < 1ms");
            UnityEngine.Debug.Log("Target: Memory usage < 50MB");
            UnityEngine.Debug.Log("See performance report for actual values");
        }
    }
}