using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core.Phonemizers.Backend;
using uPiper.Core.Phonemizers.Backend.Chinese;

namespace uPiper.Tests.Runtime.ChinesePhonemizer
{
    /// <summary>
    /// Tests for integrated multi-tone processing with word segmentation
    /// </summary>
    public class IntegratedMultiToneTests
    {
        private uPiper.Core.Phonemizers.Backend.ChinesePhonemizer phonemizer;
        private ChineseWordSegmenter segmenter;
        private MultiToneProcessor multiToneProcessor;
        private ChinesePinyinDictionary dictionary;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Use cached fallback dictionary
            dictionary = ChineseDictionaryTestCache.GetDictionary();

            // Initialize components
            segmenter = new ChineseWordSegmenter(dictionary);
            multiToneProcessor = new MultiToneProcessor(dictionary);

            // Initialize phonemizer
            phonemizer = new uPiper.Core.Phonemizers.Backend.ChinesePhonemizer();
            yield return phonemizer.InitializeAsync(new uPiper.Core.Phonemizers.Backend.PhonemizerBackendOptions());

            // Enable word segmentation
            phonemizer.UseWordSegmentation = true;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            phonemizer?.Dispose();
            yield return null;
        }

        [Test]
        public void ToneSandhi_Bu_InContext()
        {
            // Test cases with 不

            // Test case 1: 不是 (不 + 4th tone)
            var segments1 = segmenter.SegmentWithPinyinV2("不是");
            Debug.Log($"[IntegratedTest] '不是' segmented as: {string.Join(" | ", segments1.Select(s => $"{s.word}[{string.Join(" ", s.pinyin)}]"))}");
            var allPinyin1 = segments1.SelectMany(s => s.pinyin).ToArray();
            Assert.Greater(allPinyin1.Length, 0);
            Assert.AreEqual("bu2", allPinyin1[0]);

            // Test case 2: 不好 (不 + 3rd tone)
            var segments2 = segmenter.SegmentWithPinyinV2("不好");
            Debug.Log($"[IntegratedTest] '不好' segmented as: {string.Join(" | ", segments2.Select(s => $"{s.word}[{string.Join(" ", s.pinyin)}]"))}");
            var allPinyin2 = segments2.SelectMany(s => s.pinyin).ToArray();
            Assert.Greater(allPinyin2.Length, 0);
            Assert.AreEqual("bu4", allPinyin2[0]);

            // Test case 3: 不对 (不 + 4th tone)
            var segments3 = segmenter.SegmentWithPinyinV2("不对");
            Debug.Log($"[IntegratedTest] '不对' segmented as: {string.Join(" | ", segments3.Select(s => $"{s.word}[{string.Join(" ", s.pinyin)}]"))}");
            var allPinyin3 = segments3.SelectMany(s => s.pinyin).ToArray();
            Assert.Greater(allPinyin3.Length, 0);
            Assert.AreEqual("bu2", allPinyin3[0]);

            // Test case 4: 不要 (不 + 4th tone)
            var segments4 = segmenter.SegmentWithPinyinV2("不要");
            Debug.Log($"[IntegratedTest] '不要' segmented as: {string.Join(" | ", segments4.Select(s => $"{s.word}[{string.Join(" ", s.pinyin)}]"))}");
            var allPinyin4 = segments4.SelectMany(s => s.pinyin).ToArray();
            Assert.Greater(allPinyin4.Length, 0);
            Assert.AreEqual("bu2", allPinyin4[0]);
        }

        [Test]
        public void ToneSandhi_Yi_InContext()
        {
            // Test cases with 一

            // Test case 1: 一个 (一 + 4th tone → yi2)
            var segments1 = segmenter.SegmentWithPinyinV2("一个");
            Debug.Log($"[IntegratedTest] '一个' segmented as: {string.Join(" | ", segments1.Select(s => $"{s.word}[{string.Join(" ", s.pinyin)}]"))}");
            var allPinyin1 = segments1.SelectMany(s => s.pinyin).ToArray();
            Assert.Greater(allPinyin1.Length, 0);
            Assert.AreEqual("yi2", allPinyin1[0]);

            // Test case 2: 一起 (一 + 3rd tone → yi4)
            var segments2 = segmenter.SegmentWithPinyinV2("一起");
            Debug.Log($"[IntegratedTest] '一起' segmented as: {string.Join(" | ", segments2.Select(s => $"{s.word}[{string.Join(" ", s.pinyin)}]"))}");
            var allPinyin2 = segments2.SelectMany(s => s.pinyin).ToArray();
            Assert.Greater(allPinyin2.Length, 0);
            Assert.AreEqual("yi4", allPinyin2[0]);

            // Test case 3: 一定 (一 + 4th tone → yi2)
            var segments3 = segmenter.SegmentWithPinyinV2("一定");
            Debug.Log($"[IntegratedTest] '一定' segmented as: {string.Join(" | ", segments3.Select(s => $"{s.word}[{string.Join(" ", s.pinyin)}]"))}");
            var allPinyin3 = segments3.SelectMany(s => s.pinyin).ToArray();
            Assert.Greater(allPinyin3.Length, 0);
            Assert.AreEqual("yi2", allPinyin3[0]);

            // Test case 4: 一般 (一 + 1st tone → yi4)
            var segments4 = segmenter.SegmentWithPinyinV2("一般");
            Debug.Log($"[IntegratedTest] '一般' segmented as: {string.Join(" | ", segments4.Select(s => $"{s.word}[{string.Join(" ", s.pinyin)}]"))}");
            var allPinyin4 = segments4.SelectMany(s => s.pinyin).ToArray();
            Assert.Greater(allPinyin4.Length, 0);
            Assert.AreEqual("yi4", allPinyin4[0]);
        }

        [Test]
        public void MultiTone_De_InPhrases()
        {
            // Test 的 in different contexts
            // Test each case separately

            // Test case 1: 我的 (Possessive)
            var segments1 = segmenter.SegmentWithPinyinV2("我的");
            Debug.Log($"[IntegratedTest] '我的' segmented as: {string.Join(" | ", segments1.Select(s => $"{s.word}[{string.Join(" ", s.pinyin)}]"))}");
            var deFound1 = false;
            foreach (var segment in segments1)
            {
                for (int i = 0; i < segment.word.Length; i++)
                {
                    if (segment.word[i] == '的' && i < segment.pinyin.Length)
                    {
                        Assert.AreEqual("de5", segment.pinyin[i]);
                        deFound1 = true;
                        break;
                    }
                }
            }
            Assert.IsTrue(deFound1);

            // Test case 2: 的确 (indeed)
            var segments2 = segmenter.SegmentWithPinyinV2("的确");
            Debug.Log($"[IntegratedTest] '的确' segmented as: {string.Join(" | ", segments2.Select(s => $"{s.word}[{string.Join(" ", s.pinyin)}]"))}");
            var deFound2 = false;
            foreach (var segment in segments2)
            {
                for (int i = 0; i < segment.word.Length; i++)
                {
                    if (segment.word[i] == '的' && i < segment.pinyin.Length)
                    {
                        Assert.AreEqual("di2", segment.pinyin[i]);
                        deFound2 = true;
                        break;
                    }
                }
            }
            Assert.IsTrue(deFound2);

            // Test case 3: 目的 (purpose)
            var segments3 = segmenter.SegmentWithPinyinV2("目的");
            Debug.Log($"[IntegratedTest] '目的' segmented as: {string.Join(" | ", segments3.Select(s => $"{s.word}[{string.Join(" ", s.pinyin)}]"))}");
            var deFound3 = false;
            foreach (var segment in segments3)
            {
                for (int i = 0; i < segment.word.Length; i++)
                {
                    if (segment.word[i] == '的' && i < segment.pinyin.Length)
                    {
                        Assert.AreEqual("di4", segment.pinyin[i]);
                        deFound3 = true;
                        break;
                    }
                }
            }
            Assert.IsTrue(deFound3);

            // Test case 4: 他的书 (Possessive in phrase)
            var segments4 = segmenter.SegmentWithPinyinV2("他的书");
            Debug.Log($"[IntegratedTest] '他的书' segmented as: {string.Join(" | ", segments4.Select(s => $"{s.word}[{string.Join(" ", s.pinyin)}]"))}");
            var deFound4 = false;
            foreach (var segment in segments4)
            {
                for (int i = 0; i < segment.word.Length; i++)
                {
                    if (segment.word[i] == '的' && i < segment.pinyin.Length)
                    {
                        Assert.AreEqual("de5", segment.pinyin[i]);
                        deFound4 = true;
                        break;
                    }
                }
            }
            Assert.IsTrue(deFound4);
        }

        [Test]
        public void ComplexPhrase_WithMultipleToneSandhi()
        {
            // Test complex phrases with multiple tone sandhi
            // Test each complex phrase separately

            // Test case 1: 不一定 (bu + yi + ding)
            var segments1 = segmenter.SegmentWithPinyinV2("不一定");
            Debug.Log("[IntegratedTest] Complex phrase '不一定':");
            foreach (var segment in segments1)
            {
                Debug.Log($"  Word: '{segment.word}' → Pinyin: [{string.Join(" ", segment.pinyin)}]");
            }
            Assert.Greater(segments1.Count, 0);
            Assert.AreEqual("不一定".Length, segments1.Sum(s => s.word.Length));

            // Test case 2: 一不小心 (yi + bu + xiao + xin)
            var segments2 = segmenter.SegmentWithPinyinV2("一不小心");
            Debug.Log("[IntegratedTest] Complex phrase '一不小心':");
            foreach (var segment in segments2)
            {
                Debug.Log($"  Word: '{segment.word}' → Pinyin: [{string.Join(" ", segment.pinyin)}]");
            }
            Assert.Greater(segments2.Count, 0);
            Assert.AreEqual("一不小心".Length, segments2.Sum(s => s.word.Length));

            // Test case 3: 不是一个 (bu + shi + yi + ge)
            var segments3 = segmenter.SegmentWithPinyinV2("不是一个");
            Debug.Log("[IntegratedTest] Complex phrase '不是一个':");
            foreach (var segment in segments3)
            {
                Debug.Log($"  Word: '{segment.word}' → Pinyin: [{string.Join(" ", segment.pinyin)}]");
            }
            Assert.Greater(segments3.Count, 0);
            Assert.AreEqual("不是一个".Length, segments3.Sum(s => s.word.Length));

            // Test case 4: 一行不行 (yi + xing/hang + bu + xing)
            var segments4 = segmenter.SegmentWithPinyinV2("一行不行");
            Debug.Log("[IntegratedTest] Complex phrase '一行不行':");
            foreach (var segment in segments4)
            {
                Debug.Log($"  Word: '{segment.word}' → Pinyin: [{string.Join(" ", segment.pinyin)}]");
            }
            Assert.Greater(segments4.Count, 0);
            Assert.AreEqual("一行不行".Length, segments4.Sum(s => s.word.Length));
        }

        [Test]
        public void WordSegmentation_WithMultiTone()
        {
            // Test that word segmentation works with multi-tone characters
            var text = "银行行长的目的不是这个";

            var segments = segmenter.SegmentWithPinyinV2(text);
            Debug.Log($"[IntegratedTest] Segmentation of '{text}':");

            foreach (var segment in segments)
            {
                Debug.Log($"  '{segment.word}' → [{string.Join(" ", segment.pinyin)}]");

                // Check specific pronunciations
                if (segment.word.Contains("行"))
                {
                    var xingIndex = segment.word.IndexOf('行');
                    if (xingIndex >= 0 && xingIndex < segment.pinyin.Length)
                    {
                        var xingPinyin = segment.pinyin[xingIndex];
                        Debug.Log($"    行 pronounced as: {xingPinyin}");

                        // In 银行, should be hang2
                        if (segment.word == "银行")
                        {
                            Assert.AreEqual("hang2", xingPinyin, "行 in 银行 should be hang2");
                        }
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator FullPipeline_WithMultiTone()
        {
            // Test full phonemization pipeline

            // Test case 1: 不要这样
            var text1 = "不要这样";
            var task1 = phonemizer.PhonemizeAsync(text1, "zh-CN");
            yield return new WaitUntil(() => task1.IsCompleted);
            var result1 = task1.Result;
            Assert.IsTrue(result1.Success, $"Phonemization should succeed for '{text1}'");
            Assert.Greater(result1.Phonemes.Length, 0, $"Should produce phonemes for '{text1}'");
            Debug.Log($"[IntegratedTest] '{text1}' → {result1.Phonemes.Length} phonemes");
            Debug.Log($"  Phonemes: {string.Join(" ", result1.Phonemes.Take(20))}..."); // Show first 20

            // Test case 2: 一个人的生活
            var text2 = "一个人的生活";
            var task2 = phonemizer.PhonemizeAsync(text2, "zh-CN");
            yield return new WaitUntil(() => task2.IsCompleted);
            var result2 = task2.Result;
            Assert.IsTrue(result2.Success, $"Phonemization should succeed for '{text2}'");
            Assert.Greater(result2.Phonemes.Length, 0, $"Should produce phonemes for '{text2}'");
            Debug.Log($"[IntegratedTest] '{text2}' → {result2.Phonemes.Length} phonemes");
            Debug.Log($"  Phonemes: {string.Join(" ", result2.Phonemes.Take(20))}..."); // Show first 20

            // Test case 3: 我了解你的意思
            var text3 = "我了解你的意思";
            var task3 = phonemizer.PhonemizeAsync(text3, "zh-CN");
            yield return new WaitUntil(() => task3.IsCompleted);
            var result3 = task3.Result;
            Assert.IsTrue(result3.Success, $"Phonemization should succeed for '{text3}'");
            Assert.Greater(result3.Phonemes.Length, 0, $"Should produce phonemes for '{text3}'");
            Debug.Log($"[IntegratedTest] '{text3}' → {result3.Phonemes.Length} phonemes");
            Debug.Log($"  Phonemes: {string.Join(" ", result3.Phonemes.Take(20))}..."); // Show first 20

            // Test case 4: 银行不行
            var text4 = "银行不行";
            var task4 = phonemizer.PhonemizeAsync(text4, "zh-CN");
            yield return new WaitUntil(() => task4.IsCompleted);
            var result4 = task4.Result;
            Assert.IsTrue(result4.Success, $"Phonemization should succeed for '{text4}'");
            Assert.Greater(result4.Phonemes.Length, 0, $"Should produce phonemes for '{text4}'");
            Debug.Log($"[IntegratedTest] '{text4}' → {result4.Phonemes.Length} phonemes");
            Debug.Log($"  Phonemes: {string.Join(" ", result4.Phonemes.Take(20))}..."); // Show first 20
        }

        [Test]
        public void EdgeCases_MultiTone()
        {
            // Test edge cases

            // Test case 1: Empty string
            try
            {
                var segments1 = segmenter.SegmentWithPinyinV2("");
                Debug.Log($"[IntegratedTest] Edge case '' → {segments1.Count} segments");
                Assert.IsNotNull(segments1);
            }
            catch (System.Exception ex)
            {
                Assert.Fail($"Should handle edge case '' without exception: {ex.Message}");
            }

            // Test case 2: Single multi-tone character
            try
            {
                var segments2 = segmenter.SegmentWithPinyinV2("不");
                Debug.Log($"[IntegratedTest] Edge case '不' → {segments2.Count} segments");
                Assert.IsNotNull(segments2);
            }
            catch (System.Exception ex)
            {
                Assert.Fail($"Should handle edge case '不' without exception: {ex.Message}");
            }

            // Test case 3: Repeated multi-tone character
            try
            {
                var segments3 = segmenter.SegmentWithPinyinV2("的的的");
                Debug.Log($"[IntegratedTest] Edge case '的的的' → {segments3.Count} segments");
                Assert.IsNotNull(segments3);
            }
            catch (System.Exception ex)
            {
                Assert.Fail($"Should handle edge case '的的的' without exception: {ex.Message}");
            }

            // Test case 4: Repeated tone sandhi character
            try
            {
                var segments4 = segmenter.SegmentWithPinyinV2("不不不");
                Debug.Log($"[IntegratedTest] Edge case '不不不' → {segments4.Count} segments");
                Assert.IsNotNull(segments4);
            }
            catch (System.Exception ex)
            {
                Assert.Fail($"Should handle edge case '不不不' without exception: {ex.Message}");
            }

            // Test case 5: Repeated number character
            try
            {
                var segments5 = segmenter.SegmentWithPinyinV2("一一一");
                Debug.Log($"[IntegratedTest] Edge case '一一一' → {segments5.Count} segments");
                Assert.IsNotNull(segments5);
            }
            catch (System.Exception ex)
            {
                Assert.Fail($"Should handle edge case '一一一' without exception: {ex.Message}");
            }

            // Test case 6: Punctuation only
            try
            {
                var segments6 = segmenter.SegmentWithPinyinV2("。。。");
                Debug.Log($"[IntegratedTest] Edge case '。。。' → {segments6.Count} segments");
                Assert.IsNotNull(segments6);
            }
            catch (System.Exception ex)
            {
                Assert.Fail($"Should handle edge case '。。。' without exception: {ex.Message}");
            }
        }
    }
}