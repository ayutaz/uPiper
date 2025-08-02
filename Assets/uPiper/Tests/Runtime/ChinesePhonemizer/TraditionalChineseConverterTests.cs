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
            var testCases = new[]
            {
                ("學", "学"),    // learn
                ("習", "习"),    // practice
                ("國", "国"),    // country
                ("愛", "爱"),    // love
                ("體", "体"),    // body
                ("語", "语"),    // language
                ("書", "书"),    // book
                ("讀", "读"),    // read
                ("寫", "写"),    // write
                ("說", "说"),    // speak
            };

            foreach (var (traditional, expectedSimplified) in testCases)
            {
                var result = converter.ConvertToSimplified(traditional);
                Assert.AreEqual(expectedSimplified, result, 
                    $"'{traditional}' should convert to '{expectedSimplified}'");
            }
        }

        [Test]
        public void ConvertToSimplified_Words()
        {
            // Test word conversions
            var testCases = new[]
            {
                ("學習", "学习"),         // study
                ("中國", "中国"),         // China
                ("臺灣", "台湾"),         // Taiwan
                ("語言", "语言"),         // language
                ("愛國", "爱国"),         // patriotic
                ("體育", "体育"),         // physical education
                ("圖書館", "图书馆"),     // library
                ("電腦", "电脑"),         // computer
                ("網絡", "网络"),         // network
                ("飛機", "飞机"),         // airplane
            };

            foreach (var (traditional, expectedSimplified) in testCases)
            {
                var result = converter.ConvertToSimplified(traditional);
                Assert.AreEqual(expectedSimplified, result, 
                    $"'{traditional}' should convert to '{expectedSimplified}'");
            }
        }

        [Test]
        public void ConvertToSimplified_Sentences()
        {
            // Test full sentences
            var testCases = new[]
            {
                ("我愛學習中國語言。", "我爱学习中国语言。"),
                ("這是一個測試。", "这是一个测试。"),
                ("歡迎來到臺灣！", "欢迎来到台湾！"),
                ("請問這裡是圖書館嗎？", "请问这里是图书馆吗？"),
                ("我們一起去飛機場。", "我们一起去飞机场。")
            };

            foreach (var (traditional, expectedSimplified) in testCases)
            {
                var result = converter.ConvertToSimplified(traditional);
                Assert.AreEqual(expectedSimplified, result, 
                    $"Sentence conversion failed");
            }
        }

        [Test]
        public void ConvertToSimplified_MixedText()
        {
            // Test mixed Traditional/Simplified/English text
            var testCases = new[]
            {
                ("Hello 世界！", "Hello 世界！"),  // English + Simplified (no change)
                ("學習English", "学习English"),    // Traditional + English
                ("我love臺灣", "我love台湾"),      // Mixed
                ("123書本456", "123书本456"),      // Numbers + Traditional
                ("ABC語言XYZ", "ABC语言XYZ"),      // Letters + Traditional
            };

            foreach (var (input, expected) in testCases)
            {
                var result = converter.ConvertToSimplified(input);
                Assert.AreEqual(expected, result, 
                    $"Mixed text conversion failed for '{input}'");
            }
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
            var testCases = new[]
            {
                ("請稍等一下", "请稍等一下"),       // Please wait a moment
                ("謝謝您的幫助", "谢谢您的帮助"),   // Thank you for your help
                ("對不起，我聽不懂", "对不起，我听不懂"), // Sorry, I don't understand
                ("歡迎光臨", "欢迎光临"),           // Welcome
                ("再見", "再见"),                   // Goodbye
                ("沒問題", "没问题"),               // No problem
                ("請問怎麼走？", "请问怎么走？"),   // How do I get there?
                ("多少錢？", "多少钱？"),           // How much?
            };

            foreach (var (traditional, expectedSimplified) in testCases)
            {
                var result = converter.ConvertToSimplified(traditional);
                Assert.AreEqual(expectedSimplified, result, 
                    $"Common phrase conversion failed");
            }
        }

        [Test]
        public void ConvertToSimplified_TechnicalTerms()
        {
            // Test technical terms that might be different
            var testCases = new[]
            {
                ("電腦軟體", "电脑软体"),     // Computer software
                ("網際網路", "网际网路"),     // Internet
                ("人工智慧", "人工智慧"),     // AI (same in both)
                ("機器學習", "机器学习"),     // Machine learning
                ("數據庫", "数据库"),         // Database
                ("程式設計", "程式设计"),     // Programming
            };

            foreach (var (traditional, expectedSimplified) in testCases)
            {
                var result = converter.ConvertToSimplified(traditional);
                Debug.Log($"[TraditionalTest] '{traditional}' → '{result}'");
                // Note: Some technical terms might have regional variations
            }
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