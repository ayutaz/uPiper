using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core.Phonemizers.Backend.Chinese;

namespace uPiper.Tests.Runtime.ChinesePhonemizer
{
    /// <summary>
    /// Basic tests for Chinese dictionary that work with minimal dictionary
    /// </summary>
    public class ChineseBasicDictionaryTests
    {
        private ChinesePinyinDictionary dictionary;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Use cached fallback dictionary to avoid loading issues
            dictionary = ChineseDictionaryTestCache.GetDictionary();
            yield return null;
        }

        [Test]
        public void BasicDictionary_ShouldLoadSuccessfully()
        {
            Assert.IsNotNull(dictionary, "Dictionary should be loaded");
            Assert.Greater(dictionary.CharacterCount, 0, "Should have at least some characters");
            Assert.Greater(dictionary.IPACount, 0, "Should have IPA mappings");

            Debug.Log($"[BasicDictionary] Loaded: {dictionary.CharacterCount} characters, " +
                     $"{dictionary.PhraseCount} phrases, {dictionary.IPACount} IPA mappings");
        }

        [Test]
        public void BasicDictionary_ShouldHandleCommonCharacters()
        {
            // Test very common characters that should be in any dictionary
            var commonChars = new[] { "你", "好", "我", "是", "的", "了", "在", "有" };

            int foundCount = 0;
            foreach (var ch in commonChars)
            {
                if (dictionary.TryGetCharacterPinyin(ch[0], out var pinyin))
                {
                    foundCount++;
                    Assert.Greater(pinyin.Length, 0, $"Character '{ch}' should have pinyin");
                    Debug.Log($"[BasicDictionary] {ch} -> {string.Join(", ", pinyin)}");
                }
            }

            Assert.GreaterOrEqual(foundCount, commonChars.Length / 2,
                "Should find at least half of common characters");
        }

        [Test]
        public void BasicDictionary_ShouldHandleBasicPhrases()
        {
            // Test basic phrases
            var testPhrases = new[] { "你好", "中国" };

            foreach (var phrase in testPhrases)
            {
                if (dictionary.TryGetPhrasePinyin(phrase, out var pinyin))
                {
                    Debug.Log($"[BasicDictionary] Found phrase: {phrase} -> {pinyin}");
                }
                else
                {
                    Debug.Log($"[BasicDictionary] Phrase not found: {phrase}");
                }
            }
        }

        [Test]
        public void BasicDictionary_IPAConversion_ShouldWork()
        {
            // Test basic IPA conversion
            var testSyllables = new[] { "ni", "hao", "wo", "shi" };

            int convertedCount = 0;
            foreach (var syllable in testSyllables)
            {
                if (dictionary.TryGetIPA(syllable, out var ipa))
                {
                    convertedCount++;
                    Assert.IsNotEmpty(ipa, $"IPA for '{syllable}' should not be empty");
                    Debug.Log($"[BasicDictionary] {syllable} -> {ipa}");
                }
            }

            Assert.Greater(convertedCount, 0, "Should convert at least some syllables to IPA");
        }

        [Test]
        public void BasicDictionary_Performance_ShouldBeAcceptable()
        {
            // Simple performance test with basic text
            var testText = "你好世界";
            var normalizer = new ChineseTextNormalizer();
            var converter = new PinyinConverter(dictionary);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < 100; i++)
            {
                var normalized = normalizer.Normalize(testText);
                var pinyin = converter.GetPinyin(normalized);
            }

            stopwatch.Stop();
            var avgMs = stopwatch.ElapsedMilliseconds / 100.0;

            Debug.Log($"[BasicDictionary] Average processing time: {avgMs:F2}ms for '{testText}'");
            Assert.Less(avgMs, 10.0, "Basic text should process in less than 10ms");
        }

        [TearDown]
        public void TearDown()
        {
            dictionary = null;
        }
    }
}