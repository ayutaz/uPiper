using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core.Phonemizers.Backend.Chinese;

namespace uPiper.Tests.Runtime.ChinesePhonemizer
{
    /// <summary>
    /// Tests for Chinese word segmentation
    /// </summary>
    public class ChineseWordSegmenterTests
    {
        private ChineseWordSegmenter segmenter;
        private ChinesePinyinDictionary dictionary;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Use cached fallback dictionary
            dictionary = ChineseDictionaryTestCache.GetDictionary();
            segmenter = new ChineseWordSegmenter(dictionary);
            yield return null;
        }

        [Test]
        public void Segmenter_ShouldInitialize()
        {
            Assert.IsNotNull(segmenter);
            Debug.Log($"[SegmenterTest] Initialized with dictionary: {dictionary.PhraseCount} phrases");
        }

        [Test]
        public void SegmentMaxMatch_ShouldSegmentBasicPhrases()
        {
            var testCases = new[]
            {
                ("你好世界", new[] { "你好", "世界" }),
                ("中国人", new[] { "中国", "人" }),
                ("我是学生", new[] { "我", "是", "学", "生" }) // No "学生" in fallback dict
            };

            foreach (var (input, expected) in testCases)
            {
                var result = segmenter.SegmentMaxMatch(input);
                Debug.Log($"[SegmenterTest] MaxMatch: '{input}' -> [{string.Join(", ", result)}]");
                
                // Check if segmentation is reasonable
                Assert.IsNotNull(result);
                Assert.Greater(result.Count, 0);
                Assert.AreEqual(input, string.Join("", result), "Segmentation should preserve original text");
            }
        }

        [Test]
        public void Segment_ShouldUseDynamicProgramming()
        {
            var testCases = new[]
            {
                "你好世界",
                "中国人民",
                "人工智能",
                "机器学习"
            };

            foreach (var text in testCases)
            {
                var dpResult = segmenter.Segment(text);
                var maxMatchResult = segmenter.SegmentMaxMatch(text);
                
                Debug.Log($"[SegmenterTest] '{text}':");
                Debug.Log($"  DP: [{string.Join(", ", dpResult)}]");
                Debug.Log($"  MaxMatch: [{string.Join(", ", maxMatchResult)}]");
                
                // Both should preserve original text
                Assert.AreEqual(text, string.Join("", dpResult));
                Assert.AreEqual(text, string.Join("", maxMatchResult));
            }
        }

        [Test]
        public void SegmentWithPinyin_ShouldReturnWordAndPinyin()
        {
            var text = "你好世界";
            var result = segmenter.SegmentWithPinyin(text);
            
            Assert.IsNotNull(result);
            Assert.Greater(result.Count, 0);
            
            Debug.Log($"[SegmenterTest] Segment with pinyin for '{text}':");
            foreach (var (word, pinyin) in result)
            {
                Debug.Log($"  {word} -> {string.Join(" ", pinyin)}");
                
                // Each word should have pinyin
                Assert.IsNotNull(pinyin);
                Assert.Greater(pinyin.Length, 0);
                Assert.AreEqual(word.Length, pinyin.Length, 
                    $"Word '{word}' should have pinyin for each character");
            }
        }

        [Test]
        public void Segmenter_ShouldHandleUnknownCharacters()
        {
            var text = "Hello你好ABC世界123";
            var result = segmenter.Segment(text);
            
            Assert.IsNotNull(result);
            Assert.Greater(result.Count, 0);
            
            Debug.Log($"[SegmenterTest] Mixed text: '{text}' -> [{string.Join(", ", result)}]");
            
            // Should preserve all characters
            Assert.AreEqual(text, string.Join("", result));
        }

        [Test]
        public void Segmenter_ShouldHandleEmptyInput()
        {
            var result1 = segmenter.Segment("");
            var result2 = segmenter.Segment(null);
            
            Assert.IsNotNull(result1);
            Assert.IsNotNull(result2);
            Assert.AreEqual(0, result1.Count);
            Assert.AreEqual(0, result2.Count);
        }

        [Test]
        public void Segmenter_Performance_ShouldBeReasonable()
        {
            var longText = "中国人民共和国是世界上人口最多的国家之一";
            var iterations = 100;
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            for (int i = 0; i < iterations; i++)
            {
                var result = segmenter.Segment(longText);
            }
            
            stopwatch.Stop();
            var avgMs = stopwatch.ElapsedMilliseconds / (double)iterations;
            
            Debug.Log($"[SegmenterTest] Average segmentation time: {avgMs:F2}ms for text length {longText.Length}");
            Assert.Less(avgMs, 10.0, "Segmentation should be fast");
        }
    }
}