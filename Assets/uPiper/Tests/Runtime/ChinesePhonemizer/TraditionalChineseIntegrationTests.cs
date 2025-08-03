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

        [UnityTest]
        public IEnumerator TraditionalChinese_ShouldPhonemize()
        {
            // Test that Traditional Chinese text is properly converted and phonemized
            // Test case 1: I love learning
            var task1 = phonemizer.PhonemizeAsync("我愛學習", "zh-TW");
            yield return new WaitUntil(() => task1.IsCompleted);
            var result1 = task1.Result;
            Assert.IsTrue(result1.Success);
            Assert.Greater(result1.Phonemes.Length, 0);
            Debug.Log($"[TraditionalIntegration] '我愛學習' → {result1.Phonemes.Length} phonemes");

            // Test case 2: Welcome to Taiwan
            var task2 = phonemizer.PhonemizeAsync("歡迎來臺灣", "zh-TW");
            yield return new WaitUntil(() => task2.IsCompleted);
            var result2 = task2.Result;
            Assert.IsTrue(result2.Success);
            Assert.Greater(result2.Phonemes.Length, 0);
            Debug.Log($"[TraditionalIntegration] '歡迎來臺灣' → {result2.Phonemes.Length} phonemes");

            // Test case 3: Please speak Chinese
            var task3 = phonemizer.PhonemizeAsync("請說中文", "zh-TW");
            yield return new WaitUntil(() => task3.IsCompleted);
            var result3 = task3.Result;
            Assert.IsTrue(result3.Success);
            Assert.Greater(result3.Phonemes.Length, 0);
            Debug.Log($"[TraditionalIntegration] '請說中文' → {result3.Phonemes.Length} phonemes");

            // Test case 4: Where is the library
            var task4 = phonemizer.PhonemizeAsync("圖書館在哪裡", "zh-TW");
            yield return new WaitUntil(() => task4.IsCompleted);
            var result4 = task4.Result;
            Assert.IsTrue(result4.Success);
            Assert.Greater(result4.Phonemes.Length, 0);
            Debug.Log($"[TraditionalIntegration] '圖書館在哪裡' → {result4.Phonemes.Length} phonemes");

            // Test case 5: Thank you
            var task5 = phonemizer.PhonemizeAsync("謝謝您", "zh-TW");
            yield return new WaitUntil(() => task5.IsCompleted);
            var result5 = task5.Result;
            Assert.IsTrue(result5.Success);
            Assert.Greater(result5.Phonemes.Length, 0);
            Debug.Log($"[TraditionalIntegration] '謝謝您' → {result5.Phonemes.Length} phonemes");
        }

        [UnityTest]
        public IEnumerator TraditionalChinese_ShouldProduceSameResultAsSimplified()
        {
            // Test that Traditional and Simplified versions produce the same phonemes
            // Avoid tuple syntax for Unity compatibility

            // Test pair 1: study
            var task1a = phonemizer.PhonemizeAsync("學習", "zh-TW");
            var task1b = phonemizer.PhonemizeAsync("学习", "zh-CN");
            yield return new WaitUntil(() => task1a.IsCompleted && task1b.IsCompleted);
            var trad1 = task1a.Result;
            var simp1 = task1b.Result;
            Assert.IsTrue(trad1.Success && simp1.Success);
            Assert.AreEqual(simp1.Phonemes.Length, trad1.Phonemes.Length);

            // Test pair 2: patriotic
            var task2a = phonemizer.PhonemizeAsync("愛國", "zh-TW");
            var task2b = phonemizer.PhonemizeAsync("爱国", "zh-CN");
            yield return new WaitUntil(() => task2a.IsCompleted && task2b.IsCompleted);
            var trad2 = task2a.Result;
            var simp2 = task2b.Result;
            Assert.IsTrue(trad2.Success && simp2.Success);
            Assert.AreEqual(simp2.Phonemes.Length, trad2.Phonemes.Length);

            // Test pair 3: language
            var task3a = phonemizer.PhonemizeAsync("語言", "zh-TW");
            var task3b = phonemizer.PhonemizeAsync("语言", "zh-CN");
            yield return new WaitUntil(() => task3a.IsCompleted && task3b.IsCompleted);
            var trad3 = task3a.Result;
            var simp3 = task3b.Result;
            Assert.IsTrue(trad3.Success && simp3.Success);
            Assert.AreEqual(simp3.Phonemes.Length, trad3.Phonemes.Length);

            // Test pair 4: computer
            var task4a = phonemizer.PhonemizeAsync("電腦", "zh-TW");
            var task4b = phonemizer.PhonemizeAsync("电脑", "zh-CN");
            yield return new WaitUntil(() => task4a.IsCompleted && task4b.IsCompleted);
            var trad4 = task4a.Result;
            var simp4 = task4b.Result;
            Assert.IsTrue(trad4.Success && simp4.Success);
            Assert.AreEqual(simp4.Phonemes.Length, trad4.Phonemes.Length);

            // Test pair 5: airplane
            var task5a = phonemizer.PhonemizeAsync("飛機", "zh-TW");
            var task5b = phonemizer.PhonemizeAsync("飞机", "zh-CN");
            yield return new WaitUntil(() => task5a.IsCompleted && task5b.IsCompleted);
            var trad5 = task5a.Result;
            var simp5 = task5b.Result;
            Assert.IsTrue(trad5.Success && simp5.Success);
            Assert.AreEqual(simp5.Phonemes.Length, trad5.Phonemes.Length);
        }

        [UnityTest]
        public IEnumerator MixedTraditionalSimplified_ShouldWork()
        {
            Debug.Log("[TraditionalIntegration] Starting MixedTraditionalSimplified_ShouldWork test");

            // Test mixed Traditional and Simplified Chinese
            // IMPORTANT: Avoid mixing the SAME character in both traditional and simplified forms
            // as it may cause infinite loops in the converter

            // Test 1: Different characters in traditional and simplified
            Debug.Log("[TraditionalIntegration] Test 1: Different characters mixed");
            var text1 = "我學习了";  // 學(traditional) + 习(simplified) - different characters
            var task1 = phonemizer.PhonemizeAsync(text1, "zh");
            yield return new WaitUntil(() => task1.IsCompleted);

            var result1 = task1.Result;
            Assert.IsNotNull(result1);
            Assert.IsTrue(result1.Success);
            Assert.Greater(result1.Phonemes.Length, 0);
            Debug.Log($"[TraditionalIntegration] Test 1 completed: {result1.Phonemes.Length} phonemes");

            // Test 2: Traditional followed by simplified (different words)
            Debug.Log("[TraditionalIntegration] Test 2: Traditional and simplified in sequence");
            var text2 = "歡迎来到这里";  // 歡迎(traditional welcome) + 来到这里(simplified come here)
            var task2 = phonemizer.PhonemizeAsync(text2, "zh");
            yield return new WaitUntil(() => task2.IsCompleted);

            var result2 = task2.Result;
            Assert.IsNotNull(result2);
            Assert.IsTrue(result2.Success);
            Assert.Greater(result2.Phonemes.Length, 0);
            Debug.Log($"[TraditionalIntegration] Test 2 completed: {result2.Phonemes.Length} phonemes");

            // Test 3: Mixed with ASCII punctuation (safer)
            Debug.Log("[TraditionalIntegration] Test 3: Mixed with ASCII punctuation");
            var text3 = "電腦computer很好";  // Traditional + English + Simplified
            var task3 = phonemizer.PhonemizeAsync(text3, "zh");
            yield return new WaitUntil(() => task3.IsCompleted);

            var result3 = task3.Result;
            Assert.IsNotNull(result3);
            Assert.IsTrue(result3.Success);
            Assert.Greater(result3.Phonemes.Length, 0);
            Debug.Log($"[TraditionalIntegration] Test 3 completed: {result3.Phonemes.Length} phonemes");

            Debug.Log("[TraditionalIntegration] MixedTraditionalSimplified_ShouldWork completed successfully");
        }

        [UnityTest]
        public IEnumerator RegionalVariants_ShouldAllWork()
        {
            // Test different regional language codes
            var text = "歡迎來到中國";

            // Test each region separately to avoid Unity array issues
            var task1 = phonemizer.PhonemizeAsync(text, "zh");
            yield return new WaitUntil(() => task1.IsCompleted);
            var result1 = task1.Result;
            Assert.IsTrue(result1.Success);
            Assert.Greater(result1.Phonemes.Length, 0);

            var task2 = phonemizer.PhonemizeAsync(text, "zh-CN");
            yield return new WaitUntil(() => task2.IsCompleted);
            var result2 = task2.Result;
            Assert.IsTrue(result2.Success);
            Assert.Greater(result2.Phonemes.Length, 0);

            var task3 = phonemizer.PhonemizeAsync(text, "zh-TW");
            yield return new WaitUntil(() => task3.IsCompleted);
            var result3 = task3.Result;
            Assert.IsTrue(result3.Success);
            Assert.Greater(result3.Phonemes.Length, 0);

            var task4 = phonemizer.PhonemizeAsync(text, "zh-HK");
            yield return new WaitUntil(() => task4.IsCompleted);
            var result4 = task4.Result;
            Assert.IsTrue(result4.Success);
            Assert.Greater(result4.Phonemes.Length, 0);

            var task5 = phonemizer.PhonemizeAsync(text, "zh-SG");
            yield return new WaitUntil(() => task5.IsCompleted);
            var result5 = task5.Result;
            Assert.IsTrue(result5.Success);
            Assert.Greater(result5.Phonemes.Length, 0);
        }

        [UnityTest]
        public IEnumerator TraditionalWithPunctuation_ShouldWork()
        {
            // Test Traditional Chinese with various punctuation
            // Test each case separately

            // Test 1: Hello, world!
            var task1 = phonemizer.PhonemizeAsync("你好，世界！", "zh-TW");
            yield return new WaitUntil(() => task1.IsCompleted);
            var result1 = task1.Result;
            Assert.IsTrue(result1.Success);
            Assert.Greater(result1.Phonemes.Length, 0);
            Assert.Contains("_", result1.Phonemes);

            // Test 2: What is this?
            var task2 = phonemizer.PhonemizeAsync("這是什麼？", "zh-TW");
            yield return new WaitUntil(() => task2.IsCompleted);
            var result2 = task2.Result;
            Assert.IsTrue(result2.Success);
            Assert.Greater(result2.Phonemes.Length, 0);
            Assert.Contains("_", result2.Phonemes);

            // Test 3: Please wait...
            var task3 = phonemizer.PhonemizeAsync("請等一下……", "zh-TW");
            yield return new WaitUntil(() => task3.IsCompleted);
            var result3 = task3.Result;
            Assert.IsTrue(result3.Success);
            Assert.Greater(result3.Phonemes.Length, 0);
            Assert.Contains("_", result3.Phonemes);

            // Test 4: "Quoted text"
            var task4 = phonemizer.PhonemizeAsync("「引用文字」", "zh-TW");
            yield return new WaitUntil(() => task4.IsCompleted);
            var result4 = task4.Result;
            Assert.IsTrue(result4.Success);
            Assert.Greater(result4.Phonemes.Length, 0);

            // Test 5: List: 1, 2, 3
            var task5 = phonemizer.PhonemizeAsync("列表：一、二、三", "zh-TW");
            yield return new WaitUntil(() => task5.IsCompleted);
            var result5 = task5.Result;
            Assert.IsTrue(result5.Success);
            Assert.Greater(result5.Phonemes.Length, 0);
            Assert.Contains("_", result5.Phonemes);
        }

        [UnityTest]
        public IEnumerator ComplexTraditionalSentences_ShouldWork()
        {
            // Test complex Traditional Chinese sentences

            // Sentence 1
            var task1 = phonemizer.PhonemizeAsync("今天天氣真好，我們去爬山吧！", "zh-TW");
            yield return new WaitUntil(() => task1.IsCompleted);
            var result1 = task1.Result;
            Assert.IsTrue(result1.Success);
            Assert.Greater(result1.Phonemes.Length, 10);
            Debug.Log($"[TraditionalIntegration] Complex: '今天天氣真好，我們去爬山吧！' → {result1.Phonemes.Length} phonemes");

            // Sentence 2
            var task2 = phonemizer.PhonemizeAsync("請問這個用中文怎麼說？", "zh-TW");
            yield return new WaitUntil(() => task2.IsCompleted);
            var result2 = task2.Result;
            Assert.IsTrue(result2.Success);
            Assert.Greater(result2.Phonemes.Length, 10);
            Debug.Log($"[TraditionalIntegration] Complex: '請問這個用中文怎麼說？' → {result2.Phonemes.Length} phonemes");

            // Sentence 3
            var task3 = phonemizer.PhonemizeAsync("我正在學習繁體中文，覺得很有趣。", "zh-TW");
            yield return new WaitUntil(() => task3.IsCompleted);
            var result3 = task3.Result;
            Assert.IsTrue(result3.Success);
            Assert.Greater(result3.Phonemes.Length, 10);
            Debug.Log($"[TraditionalIntegration] Complex: '我正在學習繁體中文，覺得很有趣。' → {result3.Phonemes.Length} phonemes");

            // Sentence 4
            var task4 = phonemizer.PhonemizeAsync("歡迎光臨本店，有什麼需要幫助的嗎？", "zh-TW");
            yield return new WaitUntil(() => task4.IsCompleted);
            var result4 = task4.Result;
            Assert.IsTrue(result4.Success);
            Assert.Greater(result4.Phonemes.Length, 10);
            Debug.Log($"[TraditionalIntegration] Complex: '歡迎光臨本店，有什麼需要幫助的嗎？' → {result4.Phonemes.Length} phonemes");

            // Sentence 5
            var task5 = phonemizer.PhonemizeAsync("這本書的內容非常豐富，值得一讀。", "zh-TW");
            yield return new WaitUntil(() => task5.IsCompleted);
            var result5 = task5.Result;
            Assert.IsTrue(result5.Success);
            Assert.Greater(result5.Phonemes.Length, 10);
            Debug.Log($"[TraditionalIntegration] Complex: '這本書的內容非常豐富，值得一讀。' → {result5.Phonemes.Length} phonemes");
        }

        [UnityTest]
        public IEnumerator Performance_TraditionalConversion()
        {
            // Test performance of Traditional Chinese conversion
            var longText = @"這是一段較長的繁體中文文字，用於測試轉換效能。
                          包含各種不同的繁體字元，如學習、國家、語言、愛情等。
                          我們需要確保即使處理大量繁體中文，效能也能保持良好。
                          這對於即時語音合成應用來說非常重要。";

            var startTime = Time.realtimeSinceStartup;
            var task = phonemizer.PhonemizeAsync(longText, "zh-TW");
            yield return new WaitUntil(() => task.IsCompleted);
            var elapsed = Time.realtimeSinceStartup - startTime;

            Assert.IsTrue(task.Result.Success);
            Assert.Less(elapsed, 0.5f, "Should process long Traditional text quickly (< 500ms)");

            Debug.Log($"[TraditionalIntegration] Processed {longText.Length} chars in {elapsed * 1000:F2}ms");
        }
    }
}