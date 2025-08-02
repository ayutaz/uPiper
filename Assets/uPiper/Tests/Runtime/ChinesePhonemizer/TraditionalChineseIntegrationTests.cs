using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core.Phonemizers.Backend;

namespace uPiper.Tests.Runtime.ChinesePhonemizer
{
    /// <summary>
    /// Integration tests for Traditional Chinese support in ChinesePhonemizer
    /// </summary>
    public class TraditionalChineseIntegrationTests
    {
        private uPiper.Core.Phonemizers.Backend.ChinesePhonemizer phonemizer;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            phonemizer = new uPiper.Core.Phonemizers.Backend.ChinesePhonemizer();
            
            var initTask = phonemizer.InitializeAsync(
                new uPiper.Core.Phonemizers.Backend.PhonemizerBackendOptions());
            
            while (!initTask.IsCompleted)
            {
                yield return null;
            }
            
            if (initTask.IsFaulted)
            {
                throw initTask.Exception?.GetBaseException() ?? new System.Exception("Phonemizer init failed");
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            phonemizer?.Dispose();
            yield return null;
        }

        [Test]
        public async Task TraditionalChinese_ShouldPhonemize()
        {
            // Test that Traditional Chinese text is properly converted and phonemized
            var testCases = new[]
            {
                "我愛學習",      // I love learning
                "歡迎來臺灣",    // Welcome to Taiwan
                "請說中文",      // Please speak Chinese
                "圖書館在哪裡",  // Where is the library
                "謝謝您"         // Thank you
            };

            foreach (var text in testCases)
            {
                var result = await phonemizer.PhonemizeAsync(text, "zh-TW");
                
                Assert.IsTrue(result.Success, $"Should successfully phonemize '{text}'");
                Assert.Greater(result.Phonemes.Length, 0, $"Should produce phonemes for '{text}'");
                
                Debug.Log($"[TraditionalIntegration] '{text}' → {result.Phonemes.Length} phonemes");
            }
        }

        [Test]
        public async Task TraditionalChinese_ShouldProduceSameResultAsSimplified()
        {
            // Test that Traditional and Simplified versions produce the same phonemes
            var testPairs = new[]
            {
                ("學習", "学习"),     // study
                ("愛國", "爱国"),     // patriotic
                ("語言", "语言"),     // language
                ("電腦", "电脑"),     // computer
                ("飛機", "飞机")      // airplane
            };

            foreach (var (traditional, simplified) in testPairs)
            {
                var traditionalResult = await phonemizer.PhonemizeAsync(traditional, "zh-TW");
                var simplifiedResult = await phonemizer.PhonemizeAsync(simplified, "zh-CN");
                
                Assert.IsTrue(traditionalResult.Success);
                Assert.IsTrue(simplifiedResult.Success);
                
                // Check if phoneme counts are the same
                Assert.AreEqual(simplifiedResult.Phonemes.Length, traditionalResult.Phonemes.Length,
                    $"'{traditional}' and '{simplified}' should produce same number of phonemes");
                
                // Check if phonemes are identical
                for (int i = 0; i < traditionalResult.Phonemes.Length; i++)
                {
                    Assert.AreEqual(simplifiedResult.Phonemes[i], traditionalResult.Phonemes[i],
                        $"Phoneme mismatch at position {i} for '{traditional}' vs '{simplified}'");
                }
                
                Debug.Log($"[TraditionalIntegration] '{traditional}' == '{simplified}' ✓");
            }
        }

        [Test]
        public async Task MixedTraditionalSimplified_ShouldWork()
        {
            // Test mixed Traditional and Simplified Chinese
            var mixedTexts = new[]
            {
                "我愛你，但是我也爱她",  // Mixed traditional and simplified
                "學習编程很有趣",        // Mixed characters
                "歡迎welcome來到中国"    // Mixed with English
            };

            foreach (var text in mixedTexts)
            {
                var result = await phonemizer.PhonemizeAsync(text, "zh");
                
                Assert.IsTrue(result.Success, $"Should handle mixed text: '{text}'");
                Assert.Greater(result.Phonemes.Length, 0);
                
                Debug.Log($"[TraditionalIntegration] Mixed text '{text}' → {result.Phonemes.Length} phonemes");
            }
        }

        [Test]
        public async Task RegionalVariants_ShouldAllWork()
        {
            // Test different regional language codes
            var text = "歡迎來到中國";
            var regions = new[] { "zh", "zh-CN", "zh-TW", "zh-HK", "zh-SG" };

            foreach (var region in regions)
            {
                var result = await phonemizer.PhonemizeAsync(text, region);
                
                Assert.IsTrue(result.Success, $"Should support region: {region}");
                Assert.Greater(result.Phonemes.Length, 0);
                
                Debug.Log($"[TraditionalIntegration] Region {region} → {result.Phonemes.Length} phonemes");
            }
        }

        [Test]
        public async Task TraditionalWithPunctuation_ShouldWork()
        {
            // Test Traditional Chinese with various punctuation
            var testCases = new[]
            {
                "你好，世界！",           // Hello, world!
                "這是什麼？",             // What is this?
                "請等一下……",           // Please wait...
                "「引用文字」",           // "Quoted text"
                "列表：一、二、三"        // List: 1, 2, 3
            };

            foreach (var text in testCases)
            {
                var result = await phonemizer.PhonemizeAsync(text, "zh-TW");
                
                Assert.IsTrue(result.Success);
                Assert.Greater(result.Phonemes.Length, 0);
                
                // Should contain pause markers for punctuation
                var hasPause = false;
                foreach (var phoneme in result.Phonemes)
                {
                    if (phoneme == "_")
                    {
                        hasPause = true;
                        break;
                    }
                }
                
                Assert.IsTrue(hasPause, $"Should have pause markers for punctuation in '{text}'");
            }
        }

        [Test]
        public async Task ComplexTraditionalSentences_ShouldWork()
        {
            // Test complex Traditional Chinese sentences
            var sentences = new[]
            {
                "今天天氣真好，我們去爬山吧！",
                "請問這個用中文怎麼說？",
                "我正在學習繁體中文，覺得很有趣。",
                "歡迎光臨本店，有什麼需要幫助的嗎？",
                "這本書的內容非常豐富，值得一讀。"
            };

            foreach (var sentence in sentences)
            {
                var result = await phonemizer.PhonemizeAsync(sentence, "zh-TW");
                
                Assert.IsTrue(result.Success);
                Assert.Greater(result.Phonemes.Length, 10, 
                    $"Complex sentence should produce many phonemes");
                
                Debug.Log($"[TraditionalIntegration] Complex: '{sentence}' → {result.Phonemes.Length} phonemes");
            }
        }

        [Test]
        public void Performance_TraditionalConversion()
        {
            // Test performance of Traditional Chinese conversion
            var longText = @"這是一段較長的繁體中文文字，用於測試轉換效能。
                          包含各種不同的繁體字元，如學習、國家、語言、愛情等。
                          我們需要確保即使處理大量繁體中文，效能也能保持良好。
                          這對於即時語音合成應用來說非常重要。";

            var startTime = Time.realtimeSinceStartup;
            var task = phonemizer.PhonemizeAsync(longText, "zh-TW");
            task.Wait();
            var elapsed = Time.realtimeSinceStartup - startTime;
            
            Assert.IsTrue(task.Result.Success);
            Assert.Less(elapsed, 0.1f, "Should process long Traditional text quickly (< 100ms)");
            
            Debug.Log($"[TraditionalIntegration] Processed {longText.Length} chars in {elapsed * 1000:F2}ms");
        }
    }
}