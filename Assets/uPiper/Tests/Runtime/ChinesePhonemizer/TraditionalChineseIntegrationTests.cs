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
            // Test case 1: I love learning
            var result1 = await phonemizer.PhonemizeAsync("我愛學習", "zh-TW");
            Assert.IsTrue(result1.Success);
            Assert.Greater(result1.Phonemes.Length, 0);
            Debug.Log($"[TraditionalIntegration] '我愛學習' → {result1.Phonemes.Length} phonemes");
            
            // Test case 2: Welcome to Taiwan
            var result2 = await phonemizer.PhonemizeAsync("歡迎來臺灣", "zh-TW");
            Assert.IsTrue(result2.Success);
            Assert.Greater(result2.Phonemes.Length, 0);
            Debug.Log($"[TraditionalIntegration] '歡迎來臺灣' → {result2.Phonemes.Length} phonemes");
            
            // Test case 3: Please speak Chinese
            var result3 = await phonemizer.PhonemizeAsync("請說中文", "zh-TW");
            Assert.IsTrue(result3.Success);
            Assert.Greater(result3.Phonemes.Length, 0);
            Debug.Log($"[TraditionalIntegration] '請說中文' → {result3.Phonemes.Length} phonemes");
            
            // Test case 4: Where is the library
            var result4 = await phonemizer.PhonemizeAsync("圖書館在哪裡", "zh-TW");
            Assert.IsTrue(result4.Success);
            Assert.Greater(result4.Phonemes.Length, 0);
            Debug.Log($"[TraditionalIntegration] '圖書館在哪裡' → {result4.Phonemes.Length} phonemes");
            
            // Test case 5: Thank you
            var result5 = await phonemizer.PhonemizeAsync("謝謝您", "zh-TW");
            Assert.IsTrue(result5.Success);
            Assert.Greater(result5.Phonemes.Length, 0);
            Debug.Log($"[TraditionalIntegration] '謝謝您' → {result5.Phonemes.Length} phonemes");
        }

        [Test]
        public async Task TraditionalChinese_ShouldProduceSameResultAsSimplified()
        {
            // Test that Traditional and Simplified versions produce the same phonemes
            // Avoid tuple syntax for Unity compatibility
            
            // Test pair 1: study
            var trad1 = await phonemizer.PhonemizeAsync("學習", "zh-TW");
            var simp1 = await phonemizer.PhonemizeAsync("学习", "zh-CN");
            Assert.IsTrue(trad1.Success && simp1.Success);
            Assert.AreEqual(simp1.Phonemes.Length, trad1.Phonemes.Length);
            
            // Test pair 2: patriotic
            var trad2 = await phonemizer.PhonemizeAsync("愛國", "zh-TW");
            var simp2 = await phonemizer.PhonemizeAsync("爱国", "zh-CN");
            Assert.IsTrue(trad2.Success && simp2.Success);
            Assert.AreEqual(simp2.Phonemes.Length, trad2.Phonemes.Length);
            
            // Test pair 3: language
            var trad3 = await phonemizer.PhonemizeAsync("語言", "zh-TW");
            var simp3 = await phonemizer.PhonemizeAsync("语言", "zh-CN");
            Assert.IsTrue(trad3.Success && simp3.Success);
            Assert.AreEqual(simp3.Phonemes.Length, trad3.Phonemes.Length);
            
            // Test pair 4: computer
            var trad4 = await phonemizer.PhonemizeAsync("電腦", "zh-TW");
            var simp4 = await phonemizer.PhonemizeAsync("电脑", "zh-CN");
            Assert.IsTrue(trad4.Success && simp4.Success);
            Assert.AreEqual(simp4.Phonemes.Length, trad4.Phonemes.Length);
            
            // Test pair 5: airplane
            var trad5 = await phonemizer.PhonemizeAsync("飛機", "zh-TW");
            var simp5 = await phonemizer.PhonemizeAsync("飞机", "zh-CN");
            Assert.IsTrue(trad5.Success && simp5.Success);
            Assert.AreEqual(simp5.Phonemes.Length, trad5.Phonemes.Length);
        }

        [Test]
        public async Task MixedTraditionalSimplified_ShouldWork()
        {
            // Test mixed Traditional and Simplified Chinese
            // Test case 1: Mixed traditional and simplified
            var result1 = await phonemizer.PhonemizeAsync("我愛你，但是我也爱她", "zh");
            Assert.IsTrue(result1.Success);
            Assert.Greater(result1.Phonemes.Length, 0);
            Debug.Log($"[TraditionalIntegration] Mixed text '我愛你，但是我也爱她' → {result1.Phonemes.Length} phonemes");
            
            // Test case 2: Mixed characters
            var result2 = await phonemizer.PhonemizeAsync("學習编程很有趣", "zh");
            Assert.IsTrue(result2.Success);
            Assert.Greater(result2.Phonemes.Length, 0);
            Debug.Log($"[TraditionalIntegration] Mixed text '學習编程很有趣' → {result2.Phonemes.Length} phonemes");
            
            // Test case 3: Mixed with English
            var result3 = await phonemizer.PhonemizeAsync("歡迎welcome來到中国", "zh");
            Assert.IsTrue(result3.Success);
            Assert.Greater(result3.Phonemes.Length, 0);
            Debug.Log($"[TraditionalIntegration] Mixed text '歡迎welcome來到中国' → {result3.Phonemes.Length} phonemes");
        }

        [Test]
        public async Task RegionalVariants_ShouldAllWork()
        {
            // Test different regional language codes
            var text = "歡迎來到中國";
            
            // Test each region separately to avoid Unity array issues
            var result1 = await phonemizer.PhonemizeAsync(text, "zh");
            Assert.IsTrue(result1.Success);
            Assert.Greater(result1.Phonemes.Length, 0);
            
            var result2 = await phonemizer.PhonemizeAsync(text, "zh-CN");
            Assert.IsTrue(result2.Success);
            Assert.Greater(result2.Phonemes.Length, 0);
            
            var result3 = await phonemizer.PhonemizeAsync(text, "zh-TW");
            Assert.IsTrue(result3.Success);
            Assert.Greater(result3.Phonemes.Length, 0);
            
            var result4 = await phonemizer.PhonemizeAsync(text, "zh-HK");
            Assert.IsTrue(result4.Success);
            Assert.Greater(result4.Phonemes.Length, 0);
            
            var result5 = await phonemizer.PhonemizeAsync(text, "zh-SG");
            Assert.IsTrue(result5.Success);
            Assert.Greater(result5.Phonemes.Length, 0);
        }

        [Test]
        public async Task TraditionalWithPunctuation_ShouldWork()
        {
            // Test Traditional Chinese with various punctuation
            // Test each case separately
            
            // Test 1: Hello, world!
            var result1 = await phonemizer.PhonemizeAsync("你好，世界！", "zh-TW");
            Assert.IsTrue(result1.Success);
            Assert.Greater(result1.Phonemes.Length, 0);
            Assert.Contains("_", result1.Phonemes);
            
            // Test 2: What is this?
            var result2 = await phonemizer.PhonemizeAsync("這是什麼？", "zh-TW");
            Assert.IsTrue(result2.Success);
            Assert.Greater(result2.Phonemes.Length, 0);
            Assert.Contains("_", result2.Phonemes);
            
            // Test 3: Please wait...
            var result3 = await phonemizer.PhonemizeAsync("請等一下……", "zh-TW");
            Assert.IsTrue(result3.Success);
            Assert.Greater(result3.Phonemes.Length, 0);
            Assert.Contains("_", result3.Phonemes);
            
            // Test 4: "Quoted text"
            var result4 = await phonemizer.PhonemizeAsync("「引用文字」", "zh-TW");
            Assert.IsTrue(result4.Success);
            Assert.Greater(result4.Phonemes.Length, 0);
            
            // Test 5: List: 1, 2, 3
            var result5 = await phonemizer.PhonemizeAsync("列表：一、二、三", "zh-TW");
            Assert.IsTrue(result5.Success);
            Assert.Greater(result5.Phonemes.Length, 0);
            Assert.Contains("_", result5.Phonemes);
        }

        [Test]
        public async Task ComplexTraditionalSentences_ShouldWork()
        {
            // Test complex Traditional Chinese sentences
            
            // Sentence 1
            var result1 = await phonemizer.PhonemizeAsync("今天天氣真好，我們去爬山吧！", "zh-TW");
            Assert.IsTrue(result1.Success);
            Assert.Greater(result1.Phonemes.Length, 10);
            Debug.Log($"[TraditionalIntegration] Complex: '今天天氣真好，我們去爬山吧！' → {result1.Phonemes.Length} phonemes");
            
            // Sentence 2
            var result2 = await phonemizer.PhonemizeAsync("請問這個用中文怎麼說？", "zh-TW");
            Assert.IsTrue(result2.Success);
            Assert.Greater(result2.Phonemes.Length, 10);
            Debug.Log($"[TraditionalIntegration] Complex: '請問這個用中文怎麼說？' → {result2.Phonemes.Length} phonemes");
            
            // Sentence 3
            var result3 = await phonemizer.PhonemizeAsync("我正在學習繁體中文，覺得很有趣。", "zh-TW");
            Assert.IsTrue(result3.Success);
            Assert.Greater(result3.Phonemes.Length, 10);
            Debug.Log($"[TraditionalIntegration] Complex: '我正在學習繁體中文，覺得很有趣。' → {result3.Phonemes.Length} phonemes");
            
            // Sentence 4
            var result4 = await phonemizer.PhonemizeAsync("歡迎光臨本店，有什麼需要幫助的嗎？", "zh-TW");
            Assert.IsTrue(result4.Success);
            Assert.Greater(result4.Phonemes.Length, 10);
            Debug.Log($"[TraditionalIntegration] Complex: '歡迎光臨本店，有什麼需要幫助的嗎？' → {result4.Phonemes.Length} phonemes");
            
            // Sentence 5
            var result5 = await phonemizer.PhonemizeAsync("這本書的內容非常豐富，值得一讀。", "zh-TW");
            Assert.IsTrue(result5.Success);
            Assert.Greater(result5.Phonemes.Length, 10);
            Debug.Log($"[TraditionalIntegration] Complex: '這本書的內容非常豐富，值得一讀。' → {result5.Phonemes.Length} phonemes");
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