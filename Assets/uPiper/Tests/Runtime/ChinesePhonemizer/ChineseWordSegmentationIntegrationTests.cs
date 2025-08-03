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
            Debug.Log("[WordSegIntegration] Starting SetUp");

            phonemizer = new uPiper.Core.Phonemizers.Backend.ChinesePhonemizer();

            // Initialize phonemizer
            var initTask = Task.Run(async () =>
            {
                Debug.Log("[WordSegIntegration] Initializing phonemizer...");
                var result = await phonemizer.InitializeAsync(
                    new uPiper.Core.Phonemizers.Backend.PhonemizerBackendOptions());
                Debug.Log($"[WordSegIntegration] Init result: {result}");
                return result;
            });

            while (!initTask.IsCompleted)
            {
                yield return null;
            }

            if (initTask.IsFaulted)
            {
                Debug.LogError($"[WordSegIntegration] Init failed: {initTask.Exception}");
                throw initTask.Exception?.GetBaseException() ?? new System.Exception("Phonemizer init failed");
            }

            if (!initTask.Result)
            {
                Debug.LogError("[WordSegIntegration] Phonemizer initialization returned false");
                throw new System.Exception("Phonemizer initialization failed");
            }

            // Ensure word segmentation is enabled
            phonemizer.UseWordSegmentation = true;
            Debug.Log($"[WordSegIntegration] SetUp complete. IsAvailable: {phonemizer.IsAvailable}");
        }

        [Test]
        public void BasicTest_ShouldWork()
        {
            // Very simple test to check if phonemizer works at all
            var simpleText = "你好";
            var result = phonemizer.PhonemizeAsync(simpleText, "zh").Result;

            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsTrue(result.Success, "Should succeed for simple text");
            Assert.Greater(result.Phonemes.Length, 0, "Should produce phonemes for 你好");

            Debug.Log($"[WordSegIntegration] Basic test: '{simpleText}' → {result.Phonemes.Length} phonemes");
        }

        [Test]
        public void DebugDictionary_CheckCharacters()
        {
            // Get the dictionary through reflection or other means
            var dict = ChineseDictionaryTestCache.GetDictionary();

            // Check specific characters
            var testChars = new[] { '银', '行', '你', '好' };

            foreach (var ch in testChars)
            {
                Debug.Log($"[WordSegIntegration] Checking '{ch}' (U+{((int)ch):X4}):");

                if (dict.TryGetCharacterPinyin(ch, out var pinyin))
                {
                    Debug.Log($"  Found in dictionary: {string.Join(", ", pinyin)}");
                }
                else
                {
                    Debug.LogError($"  NOT FOUND in dictionary!");
                }
            }

            // Log dictionary statistics
            Debug.Log($"[WordSegIntegration] Dictionary stats: {dict.CharacterCount} characters");
        }

        [Test]
        public void TestSpecificCharacters_ShouldWork()
        {
            // Test specific characters that are failing
            var testChars = new[] { "银", "行", "中", "国" };

            foreach (var ch in testChars)
            {
                var result = phonemizer.PhonemizeAsync(ch, "zh").Result;

                Debug.Log($"[WordSegIntegration] Testing character '{ch}':");
                Debug.Log($"  Success: {result.Success}");
                Debug.Log($"  Phonemes: {result.Phonemes?.Length ?? 0}");
                if (result.Phonemes != null && result.Phonemes.Length > 0)
                {
                    Debug.Log($"  Phoneme values: {string.Join(", ", result.Phonemes)}");
                }

                Assert.IsTrue(result.Success, $"Should succeed for character '{ch}'");
                Assert.Greater(result.Phonemes.Length, 0, $"Should produce phonemes for '{ch}'");
            }
        }

        [Test]
        public void TestWithoutWordSegmentation_ShouldWork()
        {
            // Temporarily disable word segmentation
            phonemizer.UseWordSegmentation = false;

            var text = "银行";
            var result = phonemizer.PhonemizeAsync(text, "zh").Result;

            Debug.Log($"[WordSegIntegration] Testing '{text}' WITHOUT word segmentation:");
            Debug.Log($"  Success: {result.Success}");
            Debug.Log($"  Phonemes: {result.Phonemes?.Length ?? 0}");
            if (result.Phonemes != null && result.Phonemes.Length > 0)
            {
                Debug.Log($"  Phoneme values: {string.Join(", ", result.Phonemes)}");
            }

            // Re-enable word segmentation
            phonemizer.UseWordSegmentation = true;

            Assert.IsTrue(result.Success, "Should succeed without word segmentation");
            Assert.Greater(result.Phonemes.Length, 0, "Should produce phonemes without word segmentation");
        }

        [Test]
        public void WordSegmentation_ShouldImproveMultiToneHandling()
        {
            // Test multi-tone characters with different pronunciations in context
            // Avoid tuple syntax for Unity compatibility

            // Test "行" pronunciations
            var result1 = phonemizer.PhonemizeAsync("银行", "zh").Result;
            Assert.IsNotNull(result1);
            Assert.IsTrue(result1.Success);
            Assert.Greater(result1.Phonemes.Length, 0);
            Debug.Log("[WordSegIntegration] 银行 should be segmented as one word");
            Debug.Log($"  Phonemes: {string.Join(" ", result1.Phonemes)}");

            var result2 = phonemizer.PhonemizeAsync("行动", "zh").Result;
            Assert.IsNotNull(result2);
            Assert.IsTrue(result2.Success);
            Assert.Greater(result2.Phonemes.Length, 0);
            Debug.Log("[WordSegIntegration] 行动 should be segmented as one word");
            Debug.Log($"  Phonemes: {string.Join(" ", result2.Phonemes)}");

            var result3 = phonemizer.PhonemizeAsync("中国银行", "zh").Result;
            Assert.IsNotNull(result3);
            Assert.IsTrue(result3.Success);
            Assert.Greater(result3.Phonemes.Length, 0);
            Debug.Log("[WordSegIntegration] Should segment '中国' and '银行'");
            Debug.Log($"  Phonemes: {string.Join(" ", result3.Phonemes)}");

            // Test "长" pronunciations
            var result4 = phonemizer.PhonemizeAsync("长大", "zh").Result;
            Assert.IsNotNull(result4);
            Assert.IsTrue(result4.Success);
            Assert.Greater(result4.Phonemes.Length, 0);
            Debug.Log("[WordSegIntegration] 长大 (grow up) should be one word");
            Debug.Log($"  Phonemes: {string.Join(" ", result4.Phonemes)}");

            var result5 = phonemizer.PhonemizeAsync("长度", "zh").Result;
            Assert.IsNotNull(result5);
            Assert.IsTrue(result5.Success);
            Assert.Greater(result5.Phonemes.Length, 0);
            Debug.Log("[WordSegIntegration] 长度 (length) should be one word");
            Debug.Log($"  Phonemes: {string.Join(" ", result5.Phonemes)}");

            // Test "重" pronunciations
            var result6 = phonemizer.PhonemizeAsync("重要", "zh").Result;
            Assert.IsNotNull(result6);
            Assert.IsTrue(result6.Success);
            Assert.Greater(result6.Phonemes.Length, 0);
            Debug.Log("[WordSegIntegration] 重要 (important) should be one word");
            Debug.Log($"  Phonemes: {string.Join(" ", result6.Phonemes)}");

            var result7 = phonemizer.PhonemizeAsync("重新", "zh").Result;
            Assert.IsNotNull(result7);
            Assert.IsTrue(result7.Success);
            Assert.Greater(result7.Phonemes.Length, 0);
            Debug.Log("[WordSegIntegration] 重新 (again) should be one word");
            Debug.Log($"  Phonemes: {string.Join(" ", result7.Phonemes)}");
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