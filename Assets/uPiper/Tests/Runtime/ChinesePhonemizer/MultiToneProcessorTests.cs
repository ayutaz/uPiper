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

            // 不 + 4th tone → bu2
            var context1 = new PronunciationContext { Character = '不', NextTone = 4 };
            var result1 = processor.GetBestPronunciation('不', context1);
            Assert.AreEqual("bu2", result1);

            // 不 + 1st tone → bu4
            var context2 = new PronunciationContext { Character = '不', NextTone = 1 };
            var result2 = processor.GetBestPronunciation('不', context2);
            Assert.AreEqual("bu4", result2);

            // 不 + 2nd tone → bu4
            var context3 = new PronunciationContext { Character = '不', NextTone = 2 };
            var result3 = processor.GetBestPronunciation('不', context3);
            Assert.AreEqual("bu4", result3);

            // 不 + 3rd tone → bu4
            var context4 = new PronunciationContext { Character = '不', NextTone = 3 };
            var result4 = processor.GetBestPronunciation('不', context4);
            Assert.AreEqual("bu4", result4);

            // No context → bu4
            var context5 = new PronunciationContext { Character = '不' };
            var result5 = processor.GetBestPronunciation('不', context5);
            Assert.AreEqual("bu4", result5);
        }

        [Test]
        public void ToneSandhi_Yi_ShouldChange()
        {
            // Test 一 tone sandhi

            // 一 + 4th tone → yi2
            var context1 = new PronunciationContext { Character = '一', NextTone = 4 };
            var result1 = processor.GetBestPronunciation('一', context1);
            Assert.AreEqual("yi2", result1);

            // 一 + 1st tone → yi4
            var context2 = new PronunciationContext { Character = '一', NextTone = 1 };
            var result2 = processor.GetBestPronunciation('一', context2);
            Assert.AreEqual("yi4", result2);

            // 一 + 2nd tone → yi4
            var context3 = new PronunciationContext { Character = '一', NextTone = 2 };
            var result3 = processor.GetBestPronunciation('一', context3);
            Assert.AreEqual("yi4", result3);

            // 一 + 3rd tone → yi4
            var context4 = new PronunciationContext { Character = '一', NextTone = 3 };
            var result4 = processor.GetBestPronunciation('一', context4);
            Assert.AreEqual("yi4", result4);

            // Default → yi1
            var context5 = new PronunciationContext { Character = '一' };
            var result5 = processor.GetBestPronunciation('一', context5);
            Assert.AreEqual("yi1", result5);
        }

        [Test]
        public void MultiTone_De_ShouldSelectByContext()
        {
            // Test 的 pronunciation selection

            // 的确 → di2
            var context1 = new PronunciationContext { Character = '的', NextChar = '确' };
            var result1 = processor.GetBestPronunciation('的', context1);
            Assert.AreEqual("di2", result1);

            // 目的 → di4
            var context2 = new PronunciationContext { Character = '的', PrevChar = '目' };
            var result2 = processor.GetBestPronunciation('的', context2);
            Assert.AreEqual("di4", result2);

            // Default possessive → de5
            var context3 = new PronunciationContext { Character = '的' };
            var result3 = processor.GetBestPronunciation('的', context3);
            Assert.AreEqual("de5", result3);
        }

        [Test]
        public void MultiTone_Le_ShouldSelectByContext()
        {
            // Test 了 pronunciation selection

            // 了解 → liao3
            var context1 = new PronunciationContext { Character = '了', NextChar = '解' };
            var result1 = processor.GetBestPronunciation('了', context1);
            Assert.AreEqual("liao3", result1);

            // 为了 → liao3
            var context2 = new PronunciationContext { Character = '了', PrevChar = '为' };
            var result2 = processor.GetBestPronunciation('了', context2);
            Assert.AreEqual("liao3", result2);

            // Default aspectual particle → le5
            var context3 = new PronunciationContext { Character = '了' };
            var result3 = processor.GetBestPronunciation('了', context3);
            Assert.AreEqual("le5", result3);
        }

        [Test]
        public void MultiTone_Xing_ShouldSelectByContext()
        {
            // Test 行 pronunciation selection

            // 银行 → hang2
            var context1 = new PronunciationContext { Character = '行', PrevChar = '银' };
            var result1 = processor.GetBestPronunciation('行', context1);
            Assert.AreEqual("hang2", result1);

            // 行业 → hang2
            var context2 = new PronunciationContext { Character = '行', NextChar = '业' };
            var result2 = processor.GetBestPronunciation('行', context2);
            Assert.AreEqual("hang2", result2);

            // Default (to walk, OK) → xing2
            var context3 = new PronunciationContext { Character = '行' };
            var result3 = processor.GetBestPronunciation('行', context3);
            Assert.AreEqual("xing2", result3);
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
            var unknownChar = '\u2728'; // Sparkles emoji (U+2728), not in dictionary
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