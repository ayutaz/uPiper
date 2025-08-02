using System.Collections;
using System.Diagnostics;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core.Phonemizers.Backend.Chinese;
using Debug = UnityEngine.Debug;

namespace uPiper.Tests.Runtime.ChinesePhonemizer
{
    /// <summary>
    /// Performance tests for Phase 2 expanded Chinese dictionary
    /// </summary>
    public class ChinesePhase2PerformanceTests
    {
        private ChineseDictionaryLoader loader;
        private ChinesePinyinDictionary dictionary;
        private ChineseTextNormalizer normalizer;
        private PinyinConverter converter;
        private PinyinToIPAConverter ipaConverter;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            loader = new ChineseDictionaryLoader();
            normalizer = new ChineseTextNormalizer();
            
            // Load dictionary asynchronously
            var loadTask = Task.Run(async () =>
            {
                dictionary = await loader.LoadAsync();
            });
            
            // Wait for task completion
            while (!loadTask.IsCompleted)
            {
                yield return null;
            }
            
            if (loadTask.IsFaulted)
            {
                throw loadTask.Exception?.GetBaseException() ?? new System.Exception("Dictionary load failed");
            }
            
            converter = new PinyinConverter(dictionary);
            ipaConverter = new PinyinToIPAConverter(dictionary);
        }

        [Test]
        public void LoadTime_ShouldBeReasonable()
        {
            // Re-measure load time
            var stopwatch = Stopwatch.StartNew();
            var loadTask = Task.Run(async () =>
            {
                var newDict = await loader.LoadAsync();
            });
            loadTask.Wait();
            stopwatch.Stop();
            
            Debug.Log($"[Phase2Performance] Dictionary load time: {stopwatch.ElapsedMilliseconds}ms");
            
            // Even with expanded dictionary, should load in reasonable time
            Assert.Less(stopwatch.ElapsedMilliseconds, 5000, 
                "Dictionary should load in less than 5 seconds");
        }

        [Test]
        public void ProcessingSpeed_WithExpandedDictionary_ShouldMeetTarget()
        {
            // Test with various text lengths
            var testTexts = new[]
            {
                "你好世界", // 4 chars
                "人工智能和机器学习是未来的技术趋势。", // 18 chars
                "中华人民共和国是世界上人口最多的国家，拥有悠久的历史和灿烂的文化。", // 32 chars
                GenerateLongText(100), // 100 chars
                GenerateLongText(500)  // 500 chars
            };

            foreach (var text in testTexts)
            {
                var stopwatch = Stopwatch.StartNew();
                
                // Full phonemization pipeline
                var normalized = normalizer.Normalize(text);
                var pinyin = converter.GetPinyin(normalized);
                var ipa = ipaConverter.ConvertMultipleToIPA(pinyin);
                
                stopwatch.Stop();
                
                var msPerChar = stopwatch.ElapsedMilliseconds / (double)text.Length;
                Debug.Log($"[Phase2Performance] {text.Length} chars: {stopwatch.ElapsedMilliseconds}ms " +
                         $"({msPerChar:F2}ms/char)");
                
                // Should process at least 2 chars per millisecond (500 chars/sec)
                Assert.Less(msPerChar, 2.0, 
                    $"Should process text faster than 2ms/char, but took {msPerChar:F2}ms/char");
            }
        }

        [Test]
        public void MemoryUsage_WithExpandedDictionary_ShouldBeAcceptable()
        {
            // Force garbage collection
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
            
            var memoryBefore = System.GC.GetTotalMemory(false);
            
            // Create multiple instances to test memory usage
            var instances = new ChinesePhonemizer.ChinesePhonemizer[10];
            for (int i = 0; i < instances.Length; i++)
            {
                instances[i] = new ChinesePhonemizer.ChinesePhonemizer();
            }
            
            var memoryAfter = System.GC.GetTotalMemory(false);
            var memoryUsedMB = (memoryAfter - memoryBefore) / (1024f * 1024f);
            
            Debug.Log($"[Phase2Performance] Memory used by 10 instances: {memoryUsedMB:F2}MB");
            
            // Should use reasonable memory even with expanded dictionary
            Assert.Less(memoryUsedMB, 100f, 
                "10 instances should use less than 100MB total");
        }

        [Test]
        public void LookupPerformance_CommonCharacters_ShouldBeFast()
        {
            // Test lookup performance for common characters
            var commonChars = "的一是了我不人在他有这个上们来到时大地为子中你说生国年着就那";
            
            var stopwatch = Stopwatch.StartNew();
            int iterations = 10000;
            
            for (int i = 0; i < iterations; i++)
            {
                foreach (char c in commonChars)
                {
                    dictionary.TryGetCharacterPinyin(c, out _);
                }
            }
            
            stopwatch.Stop();
            
            var lookupsPerSecond = (commonChars.Length * iterations) / (stopwatch.ElapsedMilliseconds / 1000.0);
            Debug.Log($"[Phase2Performance] Character lookups per second: {lookupsPerSecond:N0}");
            
            // Should handle at least 1 million lookups per second
            Assert.Greater(lookupsPerSecond, 1_000_000, 
                "Should handle at least 1M character lookups per second");
        }

        [Test]
        public void PhraseMatching_WithLargeDictionary_ShouldBeEfficient()
        {
            var testText = "人工智能机器学习深度学习神经网络自然语言处理计算机视觉";
            
            var stopwatch = Stopwatch.StartNew();
            int iterations = 1000;
            
            for (int i = 0; i < iterations; i++)
            {
                var pinyin = converter.GetPinyin(testText, usePhrase: true);
            }
            
            stopwatch.Stop();
            
            var avgMs = stopwatch.ElapsedMilliseconds / (double)iterations;
            Debug.Log($"[Phase2Performance] Phrase matching average: {avgMs:F2}ms per iteration");
            
            // Phrase matching should still be fast with large dictionary
            Assert.Less(avgMs, 5.0, 
                "Phrase matching should take less than 5ms per iteration");
        }

        [Test]
        public void ConcurrentAccess_ShouldBeThreadSafe()
        {
            var testText = "并发测试文本";
            var errors = 0;
            
            Parallel.For(0, 100, i =>
            {
                try
                {
                    var normalized = normalizer.Normalize(testText);
                    var pinyin = converter.GetPinyin(normalized);
                    var ipa = ipaConverter.ConvertMultipleToIPA(pinyin);
                    
                    if (pinyin.Length == 0 || ipa.Length == 0)
                    {
                        Interlocked.Increment(ref errors);
                    }
                }
                catch
                {
                    Interlocked.Increment(ref errors);
                }
            });
            
            Assert.AreEqual(0, errors, "Concurrent access should not cause errors");
        }

        [UnityTest]
        public IEnumerator StressTest_LongRunning_ShouldNotDegrade()
        {
            var testText = GenerateLongText(50);
            var timings = new System.Collections.Generic.List<double>();
            
            // Run for 5 seconds
            var endTime = Time.realtimeSinceStartup + 5.0f;
            int iterations = 0;
            
            while (Time.realtimeSinceStartup < endTime)
            {
                var stopwatch = Stopwatch.StartNew();
                
                var normalized = normalizer.Normalize(testText);
                var pinyin = converter.GetPinyin(normalized);
                var ipa = ipaConverter.ConvertMultipleToIPA(pinyin);
                
                stopwatch.Stop();
                timings.Add(stopwatch.ElapsedMilliseconds);
                iterations++;
                
                if (iterations % 100 == 0)
                {
                    yield return null; // Yield periodically
                }
            }
            
            // Calculate statistics
            var avgTime = timings.Count > 0 ? timings.Average() : 0;
            var minTime = timings.Count > 0 ? timings.Min() : 0;
            var maxTime = timings.Count > 0 ? timings.Max() : 0;
            
            Debug.Log($"[Phase2Performance] Stress test - Iterations: {iterations}, " +
                     $"Avg: {avgTime:F2}ms, Min: {minTime:F2}ms, Max: {maxTime:F2}ms");
            
            // Performance should not degrade significantly
            Assert.Less(maxTime, minTime * 2, 
                "Maximum time should not be more than 2x minimum time");
        }

        private string GenerateLongText(int charCount)
        {
            var sampleChars = "你好世界这是一个测试文本用于性能评估人工智能机器学习深度神经网络";
            var result = new System.Text.StringBuilder();
            
            for (int i = 0; i < charCount; i++)
            {
                result.Append(sampleChars[i % sampleChars.Length]);
            }
            
            return result.ToString();
        }
    }
}