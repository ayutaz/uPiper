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
            var testCases = new[]
            {
                ("不是", new[] { "bu2", "shi4" }), // 不 + 4th tone
                ("不好", new[] { "bu4", "hao3" }), // 不 + 3rd tone
                ("不对", new[] { "bu2", "dui4" }), // 不 + 4th tone
                ("不要", new[] { "bu2", "yao4" })  // 不 + 4th tone
            };

            foreach (var (text, expectedPinyin) in testCases)
            {
                var segments = segmenter.SegmentWithPinyinV2(text);
                Debug.Log($"[IntegratedTest] '{text}' segmented as: {string.Join(" | ", segments.Select(s => $"{s.word}[{string.Join(" ", s.pinyin)}]"))}");
                
                // Verify tone sandhi is applied
                var allPinyin = segments.SelectMany(s => s.pinyin).ToArray();
                
                // Check first pinyin (不)
                if (expectedPinyin.Length > 0 && allPinyin.Length > 0)
                {
                    Assert.AreEqual(expectedPinyin[0], allPinyin[0], 
                        $"不 in '{text}' should be pronounced as {expectedPinyin[0]}");
                }
            }
        }

        [Test]
        public void ToneSandhi_Yi_InContext()
        {
            // Test cases with 一
            var testCases = new[]
            {
                ("一个", new[] { "yi2", "ge4" }),     // 一 + 4th tone → yi2
                ("一起", new[] { "yi4", "qi3" }),     // 一 + 3rd tone → yi4
                ("一定", new[] { "yi2", "ding4" }),   // 一 + 4th tone → yi2
                ("一般", new[] { "yi4", "ban1" })     // 一 + 1st tone → yi4
            };

            foreach (var (text, expectedPinyin) in testCases)
            {
                var segments = segmenter.SegmentWithPinyinV2(text);
                Debug.Log($"[IntegratedTest] '{text}' segmented as: {string.Join(" | ", segments.Select(s => $"{s.word}[{string.Join(" ", s.pinyin)}]"))}");
                
                var allPinyin = segments.SelectMany(s => s.pinyin).ToArray();
                
                if (expectedPinyin.Length > 0 && allPinyin.Length > 0)
                {
                    Assert.AreEqual(expectedPinyin[0], allPinyin[0], 
                        $"一 in '{text}' should be pronounced as {expectedPinyin[0]}");
                }
            }
        }

        [Test]
        public void MultiTone_De_InPhrases()
        {
            // Test 的 in different contexts
            var testCases = new[]
            {
                ("我的", "de5"),    // Possessive
                ("的确", "di2"),    // 的确 (indeed)
                ("目的", "di4"),    // 目的 (purpose)
                ("他的书", "de5")   // Possessive in phrase
            };

            foreach (var (text, expectedPinyin) in testCases)
            {
                var segments = segmenter.SegmentWithPinyinV2(text);
                Debug.Log($"[IntegratedTest] '{text}' segmented as: {string.Join(" | ", segments.Select(s => $"{s.word}[{string.Join(" ", s.pinyin)}]"))}");
                
                // Find 的 in the result
                var deFound = false;
                foreach (var (word, pinyinArray) in segments)
                {
                    for (int i = 0; i < word.Length; i++)
                    {
                        if (word[i] == '的' && i < pinyinArray.Length)
                        {
                            Assert.AreEqual(expectedPinyin, pinyinArray[i], 
                                $"的 in '{text}' should be pronounced as {expectedPinyin}");
                            deFound = true;
                            break;
                        }
                    }
                }
                
                Assert.IsTrue(deFound, $"的 should be found in '{text}'");
            }
        }

        [Test]
        public void ComplexPhrase_WithMultipleToneSandhi()
        {
            // Test complex phrases with multiple tone sandhi
            var testCases = new[]
            {
                "不一定",     // bu + yi + ding (multiple tone sandhi)
                "一不小心",   // yi + bu + xiao + xin
                "不是一个",   // bu + shi + yi + ge
                "一行不行"    // yi + xing/hang + bu + xing
            };

            foreach (var text in testCases)
            {
                var segments = segmenter.SegmentWithPinyinV2(text);
                Debug.Log($"[IntegratedTest] Complex phrase '{text}':");
                
                foreach (var (word, pinyin) in segments)
                {
                    Debug.Log($"  Word: '{word}' → Pinyin: [{string.Join(" ", pinyin)}]");
                }
                
                // Verify no crashes and reasonable output
                Assert.Greater(segments.Count, 0, $"Should segment '{text}' into words");
                
                var totalChars = segments.Sum(s => s.word.Length);
                Assert.AreEqual(text.Length, totalChars, "All characters should be accounted for");
            }
        }

        [Test]
        public void WordSegmentation_WithMultiTone()
        {
            // Test that word segmentation works with multi-tone characters
            var text = "银行行长的目的不是这个";
            
            var segments = segmenter.SegmentWithPinyinV2(text);
            Debug.Log($"[IntegratedTest] Segmentation of '{text}':");
            
            foreach (var (word, pinyin) in segments)
            {
                Debug.Log($"  '{word}' → [{string.Join(" ", pinyin)}]");
                
                // Check specific pronunciations
                if (word.Contains("行"))
                {
                    var xingIndex = word.IndexOf('行');
                    if (xingIndex >= 0 && xingIndex < pinyin.Length)
                    {
                        var xingPinyin = pinyin[xingIndex];
                        Debug.Log($"    行 pronounced as: {xingPinyin}");
                        
                        // In 银行, should be hang2
                        if (word == "银行")
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
            var testTexts = new[]
            {
                "不要这样",
                "一个人的生活",
                "我了解你的意思",
                "银行不行"
            };

            foreach (var text in testTexts)
            {
                var task = phonemizer.PhonemizeAsync(text, "zh-CN");
                yield return new WaitUntil(() => task.IsCompleted);
                
                var result = task.Result;
                
                Assert.IsTrue(result.Success, $"Phonemization should succeed for '{text}'");
                Assert.Greater(result.Phonemes.Length, 0, $"Should produce phonemes for '{text}'");
                
                Debug.Log($"[IntegratedTest] '{text}' → {result.Phonemes.Length} phonemes");
                Debug.Log($"  Phonemes: {string.Join(" ", result.Phonemes.Take(20))}..."); // Show first 20
            }
        }

        [Test]
        public void EdgeCases_MultiTone()
        {
            // Test edge cases
            var edgeCases = new[]
            {
                "",              // Empty string
                "不",            // Single multi-tone character
                "的的的",        // Repeated multi-tone character
                "不不不",        // Repeated tone sandhi character
                "一一一",        // Repeated number character
                "。。。"         // Punctuation only
            };

            foreach (var text in edgeCases)
            {
                try
                {
                    var segments = segmenter.SegmentWithPinyinV2(text);
                    Debug.Log($"[IntegratedTest] Edge case '{text}' → {segments.Count} segments");
                    
                    // Should not crash
                    Assert.IsNotNull(segments);
                }
                catch (System.Exception ex)
                {
                    Assert.Fail($"Should handle edge case '{text}' without exception: {ex.Message}");
                }
            }
        }
    }
}