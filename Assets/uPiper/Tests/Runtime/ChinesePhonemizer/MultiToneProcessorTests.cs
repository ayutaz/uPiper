using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core.Phonemizers.Backend.Chinese;

namespace uPiper.Tests.Runtime.ChinesePhonemizer
{
    /// <summary>
    /// Tests for multi-tone character processing
    /// </summary>
    public class MultiToneProcessorTests
    {
        private MultiToneProcessor processor;
        private ChinesePinyinDictionary dictionary;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Use cached fallback dictionary
            dictionary = ChineseDictionaryTestCache.GetDictionary();
            processor = new MultiToneProcessor(dictionary);
            yield return null;
        }

        [Test]
        public void MultiToneProcessor_ShouldInitialize()
        {
            Assert.IsNotNull(processor);
            
            var stats = processor.GetStatistics();
            Debug.Log($"[MultiToneTest] Statistics:");
            Debug.Log($"  Total multi-tone characters: {stats.TotalMultiToneCharacters}");
            Debug.Log($"  Characters with rules: {stats.CharactersWithRules}");
            Debug.Log($"  Total rules: {stats.TotalRules}");
            
            Assert.Greater(stats.CharactersWithRules, 0, "Should have some rules defined");
        }

        [Test]
        public void ToneSandhi_Bu_ShouldChange()
        {
            // Test 不 tone sandhi
            var testCases = new[]
            {
                // 不 + 4th tone → bu2
                (new PronunciationContext { Character = '不', NextTone = 4 }, "bu2"),
                // 不 + other tones → bu4
                (new PronunciationContext { Character = '不', NextTone = 1 }, "bu4"),
                (new PronunciationContext { Character = '不', NextTone = 2 }, "bu4"),
                (new PronunciationContext { Character = '不', NextTone = 3 }, "bu4"),
                (new PronunciationContext { Character = '不' }, "bu4") // No context
            };

            foreach (var (context, expected) in testCases)
            {
                var result = processor.GetBestPronunciation('不', context);
                Assert.AreEqual(expected, result, 
                    $"不 with next tone {context.NextTone} should be {expected}");
            }
        }

        [Test]
        public void ToneSandhi_Yi_ShouldChange()
        {
            // Test 一 tone sandhi
            var testCases = new[]
            {
                // 一 + 4th tone → yi2
                (new PronunciationContext { Character = '一', NextTone = 4 }, "yi2"),
                // 一 + 1st/2nd/3rd tone → yi4
                (new PronunciationContext { Character = '一', NextTone = 1 }, "yi4"),
                (new PronunciationContext { Character = '一', NextTone = 2 }, "yi4"),
                (new PronunciationContext { Character = '一', NextTone = 3 }, "yi4"),
                // Default
                (new PronunciationContext { Character = '一' }, "yi1")
            };

            foreach (var (context, expected) in testCases)
            {
                var result = processor.GetBestPronunciation('一', context);
                Assert.AreEqual(expected, result, 
                    $"一 with next tone {context.NextTone} should be {expected}");
            }
        }

        [Test]
        public void MultiTone_De_ShouldSelectByContext()
        {
            // Test 的 pronunciation selection
            var testCases = new[]
            {
                // 的确 → di2
                (new PronunciationContext { Character = '的', NextChar = '确' }, "di2"),
                // 目的 → di4
                (new PronunciationContext { Character = '的', PrevChar = '目' }, "di4"),
                // Default possessive → de5
                (new PronunciationContext { Character = '的' }, "de5")
            };

            foreach (var (context, expected) in testCases)
            {
                var result = processor.GetBestPronunciation('的', context);
                Assert.AreEqual(expected, result, 
                    $"的 in context should be {expected}");
            }
        }

        [Test]
        public void MultiTone_Le_ShouldSelectByContext()
        {
            // Test 了 pronunciation selection
            var testCases = new[]
            {
                // 了解 → liao3
                (new PronunciationContext { Character = '了', NextChar = '解' }, "liao3"),
                // 为了 → liao3
                (new PronunciationContext { Character = '了', PrevChar = '为' }, "liao3"),
                // Default aspectual particle → le5
                (new PronunciationContext { Character = '了' }, "le5")
            };

            foreach (var (context, expected) in testCases)
            {
                var result = processor.GetBestPronunciation('了', context);
                Assert.AreEqual(expected, result, 
                    $"了 in context should be {expected}");
            }
        }

        [Test]
        public void MultiTone_Xing_ShouldSelectByContext()
        {
            // Test 行 pronunciation selection
            var testCases = new[]
            {
                // 银行 → hang2
                (new PronunciationContext { Character = '行', PrevChar = '银' }, "hang2"),
                // 行业 → hang2
                (new PronunciationContext { Character = '行', NextChar = '业' }, "hang2"),
                // Default (to walk, OK) → xing2
                (new PronunciationContext { Character = '行' }, "xing2")
            };

            foreach (var (context, expected) in testCases)
            {
                var result = processor.GetBestPronunciation('行', context);
                Assert.AreEqual(expected, result, 
                    $"行 in context should be {expected}");
            }
        }

        [Test]
        public void IsMultiTone_ShouldIdentifyCorrectly()
        {
            // Characters that should be multi-tone
            var multiToneChars = new[] { '的', '了', '着', '不', '一' };
            foreach (var ch in multiToneChars)
            {
                Assert.IsTrue(processor.IsMultiTone(ch), 
                    $"{ch} should be identified as multi-tone");
            }
            
            // Test some single-tone characters (if they exist in fallback dict)
            var singleToneChars = new[] { '你', '好' };
            foreach (var ch in singleToneChars)
            {
                if (dictionary.TryGetCharacterPinyin(ch, out var pinyin) && pinyin.Length == 1)
                {
                    Assert.IsFalse(processor.IsMultiTone(ch), 
                        $"{ch} should not be multi-tone");
                }
            }
        }

        [Test]
        public void GetBestPronunciation_ShouldHandleUnknownCharacters()
        {
            var unknownChar = '🌟'; // Emoji, not in dictionary
            var context = new PronunciationContext { Character = unknownChar };
            
            var result = processor.GetBestPronunciation(unknownChar, context);
            Assert.IsNull(result, "Unknown characters should return null");
        }

        [Test]
        public void ComplexContext_ShouldWork()
        {
            // Test complex phrase: "不行" (bù xíng - "no good")
            var context1 = new PronunciationContext 
            { 
                Character = '不',
                NextChar = '行',
                NextTone = 2  // xing2
            };
            
            var result1 = processor.GetBestPronunciation('不', context1);
            Assert.AreEqual("bu4", result1, "不 before xing2 should be bu4");
            
            // Test: "一行" (yì háng - "one row")
            var context2 = new PronunciationContext
            {
                Character = '行',
                PrevChar = '一'
            };
            
            var result2 = processor.GetBestPronunciation('行', context2);
            // Should be hang2 based on context
            Debug.Log($"[MultiToneTest] 一行: 行 pronounced as {result2}");
        }
    }
}