using NUnit.Framework;
using uPiper.Core.Phonemizers.Backend.Chinese;
using UnityEngine;

namespace uPiper.Tests.Runtime.ChinesePhonemizer
{
    public class ChineseTextNormalizerTests
    {
        private ChineseTextNormalizer normalizer;
        
        [SetUp]
        public void Setup()
        {
            normalizer = new ChineseTextNormalizer();
        }
        
        [Test]
        public void Normalizer_NumberConversion_Individual()
        {
            var testCases = new (string input, string expected)[]
            {
                ("123", "一二三"),
                ("2024", "二零二四"),
                ("9876", "九八七六")
            };
            
            foreach (var (input, expected) in testCases)
            {
                var result = normalizer.NormalizeNumbers(input, ChineseTextNormalizer.NumberFormat.Individual);
                Assert.AreEqual(expected, result, $"Failed to convert '{input}' to individual Chinese");
            }
        }
        
        [Test]
        public void Normalizer_NumberConversion_Formal()
        {
            var testCases = new (string input, string expected)[]
            {
                ("0", "零"),
                ("1", "一"),
                ("10", "十"),
                ("11", "十一"),
                ("20", "二十"),
                ("100", "一百"),
                ("123", "一百二十三"),
                ("1000", "一千"),
                ("1234", "一千二百三十四"),
                ("10000", "一万"),
                ("10001", "一万零一"),
                ("12345", "一万二千三百四十五")
            };
            
            foreach (var (input, expected) in testCases)
            {
                var result = normalizer.NormalizeNumbers(input, ChineseTextNormalizer.NumberFormat.Formal);
                Assert.AreEqual(expected, result, $"Failed to convert '{input}' to formal Chinese");
            }
        }
        
        [Test]
        public void Normalizer_PunctuationNormalization()
        {
            var testCases = new (string input, string expected)[]
            {
                ("你好，世界！", "你好,世界!"),
                ("这是一个句子。", "这是一个句子."),
                ("什么？", "什么?"),
                ("（括号）", "(括号)"),
                ("【标题】", "[标题]"),
                ("《书名》", "<书名>")
            };
            
            foreach (var (input, expected) in testCases)
            {
                var result = normalizer.NormalizePunctuation(input);
                Assert.AreEqual(expected, result, $"Failed to normalize punctuation in '{input}'");
            }
        }
        
        [Test]
        public void Normalizer_MixedTextSplitting()
        {
            var text = "这是Chinese text with English words混合在一起";
            var segments = normalizer.SplitMixedText(text);
            
            Assert.AreEqual(4, segments.Length, "Should have 4 segments");
            
            Assert.AreEqual("这是", segments[0].chinese);
            Assert.AreEqual("", segments[0].english);
            
            Assert.AreEqual("", segments[1].chinese);
            Assert.AreEqual("Chinese text with English words", segments[1].english);
            
            Assert.AreEqual("混合在一起", segments[2].chinese);
            Assert.AreEqual("", segments[2].english);
        }
        
        [Test]
        public void Normalizer_CompleteNormalization()
        {
            var text = "今天是2024年12月25日，temperature是-5°C。";
            var result = normalizer.Normalize(text, ChineseTextNormalizer.NumberFormat.Formal);
            
            Debug.Log($"Original: {text}");
            Debug.Log($"Normalized: {result}");
            
            // Should convert numbers to Chinese
            Assert.IsTrue(result.Contains("二零二四年"));
            Assert.IsTrue(result.Contains("十二月"));
            Assert.IsTrue(result.Contains("二十五日"));
            
            // Should preserve English word
            Assert.IsTrue(result.Contains("temperature"));
        }
        
        [Test]
        public void Normalizer_WhitespaceHandling()
        {
            var text = "你好  ，  世界   ！   ";
            var result = normalizer.Normalize(text);
            
            // Should remove extra spaces around punctuation
            Assert.AreEqual("你好,世界!", result);
        }
        
        [Test]
        public void Normalizer_EmptyText()
        {
            var result = normalizer.Normalize("");
            Assert.AreEqual("", result);
            
            result = normalizer.Normalize(null);
            Assert.IsNull(result);
        }
        
        [Test]
        public void Normalizer_SpecialCharacters()
        {
            var text = "Mr. Smith说：'Hello!'";
            var result = normalizer.Normalize(text);
            
            // Should replace Mr. with 先生
            Assert.IsTrue(result.Contains("先生"));
            Assert.IsTrue(result.Contains("Smith"));
        }
    }
}