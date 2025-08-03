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
            // Test case 1: 123 -> 一二三
            var result1 = normalizer.NormalizeNumbers("123", ChineseTextNormalizer.NumberFormat.Individual);
            Assert.AreEqual("一二三", result1, "Failed to convert '123' to individual Chinese");

            // Test case 2: 2024 -> 二零二四
            var result2 = normalizer.NormalizeNumbers("2024", ChineseTextNormalizer.NumberFormat.Individual);
            Assert.AreEqual("二零二四", result2, "Failed to convert '2024' to individual Chinese");

            // Test case 3: 9876 -> 九八七六
            var result3 = normalizer.NormalizeNumbers("9876", ChineseTextNormalizer.NumberFormat.Individual);
            Assert.AreEqual("九八七六", result3, "Failed to convert '9876' to individual Chinese");
        }

        [Test]
        public void Normalizer_NumberConversion_Formal()
        {
            // Test individual numbers
            Assert.AreEqual("零", normalizer.NormalizeNumbers("0", ChineseTextNormalizer.NumberFormat.Formal), "Failed to convert '0' to formal Chinese");
            Assert.AreEqual("一", normalizer.NormalizeNumbers("1", ChineseTextNormalizer.NumberFormat.Formal), "Failed to convert '1' to formal Chinese");
            Assert.AreEqual("十", normalizer.NormalizeNumbers("10", ChineseTextNormalizer.NumberFormat.Formal), "Failed to convert '10' to formal Chinese");
            Assert.AreEqual("十一", normalizer.NormalizeNumbers("11", ChineseTextNormalizer.NumberFormat.Formal), "Failed to convert '11' to formal Chinese");
            Assert.AreEqual("二十", normalizer.NormalizeNumbers("20", ChineseTextNormalizer.NumberFormat.Formal), "Failed to convert '20' to formal Chinese");
            Assert.AreEqual("一百", normalizer.NormalizeNumbers("100", ChineseTextNormalizer.NumberFormat.Formal), "Failed to convert '100' to formal Chinese");
            Assert.AreEqual("一百二十三", normalizer.NormalizeNumbers("123", ChineseTextNormalizer.NumberFormat.Formal), "Failed to convert '123' to formal Chinese");
            Assert.AreEqual("一千", normalizer.NormalizeNumbers("1000", ChineseTextNormalizer.NumberFormat.Formal), "Failed to convert '1000' to formal Chinese");
            Assert.AreEqual("一千二百三十四", normalizer.NormalizeNumbers("1234", ChineseTextNormalizer.NumberFormat.Formal), "Failed to convert '1234' to formal Chinese");
            Assert.AreEqual("一万", normalizer.NormalizeNumbers("10000", ChineseTextNormalizer.NumberFormat.Formal), "Failed to convert '10000' to formal Chinese");
            Assert.AreEqual("一万零一", normalizer.NormalizeNumbers("10001", ChineseTextNormalizer.NumberFormat.Formal), "Failed to convert '10001' to formal Chinese");
            Assert.AreEqual("一万二千三百四十五", normalizer.NormalizeNumbers("12345", ChineseTextNormalizer.NumberFormat.Formal), "Failed to convert '12345' to formal Chinese");
        }

        [Test]
        public void Normalizer_PunctuationNormalization()
        {
            // Test Chinese punctuation normalization
            Assert.AreEqual("你好,世界!", normalizer.NormalizePunctuation("你好，世界！"), "Failed to normalize punctuation in '你好，世界！'");
            Assert.AreEqual("这是一个句子.", normalizer.NormalizePunctuation("这是一个句子。"), "Failed to normalize punctuation in '这是一个句子。'");
            Assert.AreEqual("什么?", normalizer.NormalizePunctuation("什么？"), "Failed to normalize punctuation in '什么？'");
            Assert.AreEqual("(括号)", normalizer.NormalizePunctuation("（括号）"), "Failed to normalize punctuation in '（括号）'");
            Assert.AreEqual("[标题]", normalizer.NormalizePunctuation("【标题】"), "Failed to normalize punctuation in '【标题】'");
            Assert.AreEqual("<书名>", normalizer.NormalizePunctuation("《书名》"), "Failed to normalize punctuation in '《书名》'");
        }

        [Test]
        public void Normalizer_MixedTextSplitting()
        {
            var text = "这是Chinese text with English words混合在一起";
            var segments = normalizer.SplitMixedText(text);

            // Debug output to see actual segments
            for (int i = 0; i < segments.Length; i++)
            {
                Debug.Log($"Segment {i}: chinese='{segments[i].chinese}', english='{segments[i].english}'");
            }

            Assert.AreEqual(3, segments.Length, "Should have 3 segments");

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
            Assert.IsTrue(result.Contains("二千零二十四年"), "Should contain year in formal Chinese");
            Assert.IsTrue(result.Contains("十二月"), "Should contain month in Chinese");
            Assert.IsTrue(result.Contains("二十五日"), "Should contain day in Chinese");

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