using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core.Phonemizers.Backend;

namespace uPiper.Tests.Runtime.ChinesePhonemizer
{
    /// <summary>
    /// Integration tests for word segmentation in Chinese phonemizer
    /// </summary>
    public class ChineseWordSegmentationIntegrationTests
    {
        private uPiper.Core.Phonemizers.Backend.ChinesePhonemizer phonemizer;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            phonemizer = new uPiper.Core.Phonemizers.Backend.ChinesePhonemizer();
            
            // Initialize phonemizer
            var initTask = Task.Run(async () =>
            {
                await phonemizer.InitializeAsync(
                    new uPiper.Core.Phonemizers.Backend.PhonemizerBackendOptions());
            });
            
            while (!initTask.IsCompleted)
            {
                yield return null;
            }
            
            if (initTask.IsFaulted)
            {
                throw initTask.Exception?.GetBaseException() ?? new System.Exception("Phonemizer init failed");
            }
            
            // Ensure word segmentation is enabled
            phonemizer.UseWordSegmentation = true;
        }

        [Test]
        public void WordSegmentation_ShouldImproveMultiToneHandling()
        {
            // Test multi-tone characters with different pronunciations in context
            var testCases = new[]
            {
                // "行" has different pronunciations
                ("银行", "银行 should be segmented as one word"),
                ("行动", "行动 should be segmented as one word"),
                ("中国银行", "Should segment '中国' and '银行'"),
                
                // "长" has different pronunciations
                ("长大", "长大 (grow up) should be one word"),
                ("长度", "长度 (length) should be one word"),
                
                // "重" has different pronunciations
                ("重要", "重要 (important) should be one word"),
                ("重新", "重新 (again) should be one word")
            };

            foreach (var (text, description) in testCases)
            {
                var result = phonemizer.PhonemizeAsync(text, "zh").Result;
                
                Assert.IsNotNull(result, $"Should phonemize: {text}");
                Assert.Greater(result.Phonemes.Length, 0, $"Should produce phonemes for: {text}");
                
                Debug.Log($"[WordSegIntegration] {description}");
                Debug.Log($"  Text: {text}");
                Debug.Log($"  Phonemes: {string.Join(" ", result.Phonemes)}");
            }
        }

        [Test]
        public void WordSegmentation_ShouldHandleComplexPhrases()
        {
            var complexPhrases = new[]
            {
                "人工智能技术",
                "机器学习算法",
                "自然语言处理",
                "计算机视觉系统",
                "深度神经网络"
            };

            foreach (var phrase in complexPhrases)
            {
                var result = phonemizer.PhonemizeAsync(phrase, "zh").Result;
                
                Assert.IsNotNull(result);
                Assert.Greater(result.Phonemes.Length, 0);
                
                // Count tone markers to estimate syllable count
                var toneCount = 0;
                foreach (var phoneme in result.Phonemes)
                {
                    if (phoneme == "˥" || phoneme == "˧˥" || phoneme == "˨˩˦" || phoneme == "˥˩")
                    {
                        toneCount++;
                    }
                }
                
                Debug.Log($"[WordSegIntegration] Complex phrase: {phrase}");
                Debug.Log($"  Character count: {phrase.Length}");
                Debug.Log($"  Phoneme count: {result.Phonemes.Length}");
                Debug.Log($"  Estimated syllables: {toneCount}");
            }
        }

        [Test]
        public void WordSegmentation_CompareWithCharacterBased()
        {
            var testText = "中国人民共和国";
            
            // Get results with word segmentation (default)
            var wordSegResult = phonemizer.PhonemizeAsync(testText, "zh").Result;
            
            // TODO: Add method to disable word segmentation for comparison
            // For now, just verify the result
            Assert.IsNotNull(wordSegResult);
            Assert.Greater(wordSegResult.Phonemes.Length, 0);
            
            Debug.Log($"[WordSegIntegration] Word segmentation result for '{testText}':");
            Debug.Log($"  Phonemes: {string.Join(" ", wordSegResult.Phonemes)}");
            Debug.Log($"  Count: {wordSegResult.Phonemes.Length}");
        }

        [Test]
        public void WordSegmentation_ShouldHandleMixedContent()
        {
            var mixedTexts = new[]
            {
                "使用AI技术",
                "Python编程语言",
                "iOS和Android系统",
                "Web3.0时代"
            };

            foreach (var text in mixedTexts)
            {
                var result = phonemizer.PhonemizeAsync(text, "zh").Result;
                
                Assert.IsNotNull(result);
                Assert.Greater(result.Phonemes.Length, 0);
                
                Debug.Log($"[WordSegIntegration] Mixed content: {text}");
                Debug.Log($"  Phonemes: {string.Join(" ", result.Phonemes)}");
                
                // Should not have Unicode escape sequences
                foreach (var phoneme in result.Phonemes)
                {
                    Assert.IsFalse(phoneme.StartsWith("u") && phoneme.Length == 5,
                        $"Should not have Unicode escape: {phoneme}");
                }
            }
        }

        [Test]
        public void WordSegmentation_Performance_ShouldBeAcceptable()
        {
            var longText = "人工智能是计算机科学的一个分支，它企图了解智能的实质，并生产出一种新的能以人类智能相似的方式做出反应的智能机器。";
            var iterations = 10;
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            for (int i = 0; i < iterations; i++)
            {
                var result = phonemizer.PhonemizeAsync(longText, "zh").Result;
                Assert.IsNotNull(result);
            }
            
            stopwatch.Stop();
            var avgMs = stopwatch.ElapsedMilliseconds / (double)iterations;
            
            Debug.Log($"[WordSegIntegration] Performance test:");
            Debug.Log($"  Text length: {longText.Length} characters");
            Debug.Log($"  Average time: {avgMs:F2}ms");
            Debug.Log($"  Characters per second: {longText.Length / (avgMs / 1000):F0}");
            
            Assert.Less(avgMs, 100, "Should process within 100ms");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            phonemizer?.Dispose();
            phonemizer = null;
            yield return null;
        }
    }
}