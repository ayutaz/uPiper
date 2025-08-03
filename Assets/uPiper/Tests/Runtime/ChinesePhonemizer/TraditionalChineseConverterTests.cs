using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core.Phonemizers.Backend.Chinese;

namespace uPiper.Tests.Runtime.ChinesePhonemizer
{
    /// <summary>
    /// Tests for Traditional Chinese to Simplified Chinese converter
    /// </summary>
    public class TraditionalChineseConverterTests
    {
        private TraditionalChineseConverter converter;

        [SetUp]
        public void SetUp()
        {
            converter = new TraditionalChineseConverter();
        }

        [Test]
        public void Converter_ShouldInitialize()
        {
            Assert.IsNotNull(converter);

            var (mappingCount, traditionalCount) = converter.GetStatistics();
            Debug.Log($"[TraditionalTest] Initialized with {mappingCount} mappings, {traditionalCount} traditional characters");

            Assert.Greater(mappingCount, 200, "Should have at least 200 character mappings");
            Assert.AreEqual(mappingCount, traditionalCount, "Mapping count should equal traditional character count");
        }

        [Test]
        public void ConvertToSimplified_BasicCharacters()
        {
            // Test basic character conversions
            Assert.AreEqual("学", converter.ConvertToSimplified("學")); // learn
            Assert.AreEqual("习", converter.ConvertToSimplified("習")); // practice
            Assert.AreEqual("国", converter.ConvertToSimplified("國")); // country
            Assert.AreEqual("爱", converter.ConvertToSimplified("愛")); // love
            Assert.AreEqual("体", converter.ConvertToSimplified("體")); // body
            Assert.AreEqual("语", converter.ConvertToSimplified("語")); // language
            Assert.AreEqual("书", converter.ConvertToSimplified("書")); // book
            Assert.AreEqual("读", converter.ConvertToSimplified("讀")); // read
            Assert.AreEqual("写", converter.ConvertToSimplified("寫")); // write
            Assert.AreEqual("说", converter.ConvertToSimplified("說")); // speak
        }

        [Test]
        public void ConvertToSimplified_Words()
        {
            // Test word conversions
            Assert.AreEqual("学习", converter.ConvertToSimplified("學習"));         // study
            Assert.AreEqual("中国", converter.ConvertToSimplified("中國"));         // China
            Assert.AreEqual("台湾", converter.ConvertToSimplified("臺灣"));         // Taiwan
            Assert.AreEqual("语言", converter.ConvertToSimplified("語言"));         // language
            Assert.AreEqual("爱国", converter.ConvertToSimplified("愛國"));         // patriotic
            Assert.AreEqual("体育", converter.ConvertToSimplified("體育"));         // physical education
            Assert.AreEqual("图书馆", converter.ConvertToSimplified("圖書館"));     // library
            Assert.AreEqual("电脑", converter.ConvertToSimplified("電腦"));         // computer
            Assert.AreEqual("网络", converter.ConvertToSimplified("網絡"));         // network
            Assert.AreEqual("飞机", converter.ConvertToSimplified("飛機"));         // airplane
        }

        [Test]
        public void ConvertToSimplified_Sentences()
        {
            // Test full sentences
            Assert.AreEqual("我爱学习中国语言。", converter.ConvertToSimplified("我愛學習中國語言。"));
            Assert.AreEqual("这是一个测试。", converter.ConvertToSimplified("這是一個測試。"));
            Assert.AreEqual("欢迎来到台湾！", converter.ConvertToSimplified("歡迎來到臺灣！"));
            Assert.AreEqual("请问这里是图书馆吗？", converter.ConvertToSimplified("請問這裡是圖書館嗎？"));
            Assert.AreEqual("我们一起去飞机场。", converter.ConvertToSimplified("我們一起去飛機場。"));
        }

        [Test]
        public void ConvertToSimplified_MixedText_EnglishSimplified()
        {
            // Test 1: English + Simplified (no change)
            var result = converter.ConvertToSimplified("Hello 世界！");
            Assert.AreEqual("Hello 世界！", result);
        }

        [Test]
        public void ConvertToSimplified_MixedText_TraditionalEnglish()
        {
            // Test 2: Traditional + English
            var result = converter.ConvertToSimplified("學習English");
            Assert.AreEqual("学习English", result);
        }

        [Test]
        public void ConvertToSimplified_MixedText_Mixed()
        {
            // Test 3: Mixed
            var result = converter.ConvertToSimplified("我love臺灣");
            Assert.AreEqual("我love台湾", result);
        }

        [Test]
        public void ConvertToSimplified_MixedText_NumbersTraditional()
        {
            // Test 4: Numbers + Traditional
            var result = converter.ConvertToSimplified("123書本456");
            Assert.AreEqual("123书本456", result);
        }

        [Test]
        public void ConvertToSimplified_MixedText_LettersTraditional()
        {
            // Test 5: Letters + Traditional
            var result = converter.ConvertToSimplified("ABC語言XYZ");
            Assert.AreEqual("ABC语言XYZ", result);
        }

        [Test]
        public void IsTraditionalCharacter_ShouldIdentifyCorrectly()
        {
            // Traditional characters
            Assert.IsTrue(converter.IsTraditionalCharacter('學'));
            Assert.IsTrue(converter.IsTraditionalCharacter('國'));
            Assert.IsTrue(converter.IsTraditionalCharacter('愛'));

            // Simplified characters
            Assert.IsFalse(converter.IsTraditionalCharacter('学'));
            Assert.IsFalse(converter.IsTraditionalCharacter('国'));
            Assert.IsFalse(converter.IsTraditionalCharacter('爱'));

            // Common characters (same in both)
            Assert.IsFalse(converter.IsTraditionalCharacter('我'));
            Assert.IsFalse(converter.IsTraditionalCharacter('你'));
            Assert.IsFalse(converter.IsTraditionalCharacter('好'));

            // Non-Chinese characters
            Assert.IsFalse(converter.IsTraditionalCharacter('A'));
            Assert.IsFalse(converter.IsTraditionalCharacter('1'));
            Assert.IsFalse(converter.IsTraditionalCharacter('!'));
        }

        [Test]
        public void ContainsTraditional_ShouldDetectCorrectly()
        {
            // Contains traditional
            Assert.IsTrue(converter.ContainsTraditional("我愛你"));
            Assert.IsTrue(converter.ContainsTraditional("學習中文"));
            Assert.IsTrue(converter.ContainsTraditional("Hello 臺灣"));

            // No traditional
            Assert.IsFalse(converter.ContainsTraditional("我爱你"));
            Assert.IsFalse(converter.ContainsTraditional("学习中文"));
            Assert.IsFalse(converter.ContainsTraditional("Hello 台湾"));
            Assert.IsFalse(converter.ContainsTraditional("Hello World"));
            Assert.IsFalse(converter.ContainsTraditional("12345"));

            // Edge cases
            Assert.IsFalse(converter.ContainsTraditional(""));
            Assert.IsFalse(converter.ContainsTraditional(null));
        }

        [Test]
        public void ConvertToSimplified_EdgeCases()
        {
            // Empty and null
            Assert.AreEqual("", converter.ConvertToSimplified(""));
            Assert.AreEqual(null, converter.ConvertToSimplified(null));

            // Only punctuation
            Assert.AreEqual("！？。，", converter.ConvertToSimplified("！？。，"));

            // Only numbers
            Assert.AreEqual("12345", converter.ConvertToSimplified("12345"));

            // Already simplified
            Assert.AreEqual("我爱学习", converter.ConvertToSimplified("我爱学习"));
        }

        [Test]
        public void ConvertToSimplified_CommonPhrases()
        {
            // Test common phrases that might appear in TTS
            Assert.AreEqual("请稍等一下", converter.ConvertToSimplified("請稍等一下"));       // Please wait a moment
            Assert.AreEqual("谢谢您的帮助", converter.ConvertToSimplified("謝謝您的幫助"));   // Thank you for your help
            Assert.AreEqual("对不起，我听不懂", converter.ConvertToSimplified("對不起，我聽不懂")); // Sorry, I don't understand
            Assert.AreEqual("欢迎光临", converter.ConvertToSimplified("歡迎光臨"));           // Welcome
            Assert.AreEqual("再见", converter.ConvertToSimplified("再見"));                   // Goodbye
            Assert.AreEqual("没问题", converter.ConvertToSimplified("沒問題"));               // No problem
            Assert.AreEqual("请问怎么走？", converter.ConvertToSimplified("請問怎麼走？"));   // How do I get there?
            Assert.AreEqual("多少钱？", converter.ConvertToSimplified("多少錢？"));           // How much?
        }

        [Test]
        public void ConvertToSimplified_TechnicalTerms()
        {
            // Test technical terms that might be different
            var result1 = converter.ConvertToSimplified("電腦軟體");
            Debug.Log($"[TraditionalTest] '電腦軟體' → '{result1}'");

            var result2 = converter.ConvertToSimplified("網際網路");
            Debug.Log($"[TraditionalTest] '網際網路' → '{result2}'");

            var result3 = converter.ConvertToSimplified("人工智慧");
            Debug.Log($"[TraditionalTest] '人工智慧' → '{result3}'");

            var result4 = converter.ConvertToSimplified("機器學習");
            Debug.Log($"[TraditionalTest] '機器學習' → '{result4}'");
            Assert.AreEqual("机器学习", result4);

            var result5 = converter.ConvertToSimplified("數據庫");
            Debug.Log($"[TraditionalTest] '數據庫' → '{result5}'");
            Assert.AreEqual("数据库", result5);

            var result6 = converter.ConvertToSimplified("程式設計");
            Debug.Log($"[TraditionalTest] '程式設計' → '{result6}'");
            // Note: Some technical terms might have regional variations
        }

        [Test]
        public void Performance_LargeText()
        {
            // Test performance with larger text
            var largeText = @"這是一個測試繁體中文轉換的長文本。
                           我們需要確保轉換器能夠快速處理大量文字。
                           包括各種不同的繁體字：學習、國家、愛情、體育、語言等。
                           還有一些複雜的句子結構和標點符號。";

            var startTime = Time.realtimeSinceStartup;
            var result = converter.ConvertToSimplified(largeText);
            var elapsed = Time.realtimeSinceStartup - startTime;

            Assert.IsNotNull(result);
            Assert.Greater(result.Length, 0);
            Assert.Less(elapsed, 0.01f, "Conversion should be very fast (< 10ms)");

            Debug.Log($"[TraditionalTest] Converted {largeText.Length} characters in {elapsed * 1000:F2}ms");
        }
    }
}