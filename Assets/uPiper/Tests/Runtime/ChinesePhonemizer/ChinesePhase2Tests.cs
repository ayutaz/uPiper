using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core.Phonemizers.Backend.Chinese;

namespace uPiper.Tests.Runtime.ChinesePhonemizer
{
    /// <summary>
    /// Tests for Phase 2 Chinese support features
    /// Currently using fallback dictionary for Unity Editor stability
    /// </summary>
    public class ChinesePhase2Tests
    {
        private ChinesePinyinDictionary dictionary;
        private ChineseTextNormalizer normalizer;
        private PinyinConverter converter;
        private PinyinToIPAConverter ipaConverter;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Use cached fallback dictionary to avoid loading issues
            dictionary = ChineseDictionaryTestCache.GetDictionary();
            normalizer = new ChineseTextNormalizer();
            converter = new PinyinConverter(dictionary);
            ipaConverter = new PinyinToIPAConverter(dictionary);
            yield return null;
        }

        [Test]
        public void BasicDictionary_ShouldLoadSuccessfully()
        {
            Assert.IsNotNull(dictionary, "Dictionary should be loaded");
            Assert.Greater(dictionary.CharacterCount, 0, "Should have at least some characters");
            Assert.Greater(dictionary.IPACount, 0, "Should have IPA mappings");

            Debug.Log($"[Phase2Tests] Dictionary loaded: {dictionary.CharacterCount} characters, " +
                     $"{dictionary.PhraseCount} phrases, {dictionary.IPACount} IPA mappings");
        }

        [Test]
        public void ChineseNormalizer_ShouldNormalizeText()
        {
            // Test basic normalization
            var result1 = normalizer.Normalize("你好！");
            Assert.IsNotNull(result1);
            Debug.Log($"[Phase2Tests] '你好！' normalized to '{result1}'");

            var result2 = normalizer.Normalize("Hello世界");
            Assert.IsNotNull(result2);
            Debug.Log($"[Phase2Tests] 'Hello世界' normalized to '{result2}'");

            var result3 = normalizer.Normalize("123中国456");
            Assert.IsNotNull(result3);
            Debug.Log($"[Phase2Tests] '123中国456' normalized to '{result3}'");

            // Just verify that normalization works without specific expectations
            Assert.IsTrue(result1.Contains("你") && result1.Contains("好"));
            Assert.IsTrue(result2.Contains("世") && result2.Contains("界"));
            Assert.IsTrue(result3.Contains("中") && result3.Contains("国"));
        }

        [Test]
        public void PinyinConverter_ShouldConvertBasicText()
        {
            var testText = "你好世界";
            var pinyin = converter.GetPinyin(testText);

            Assert.IsNotNull(pinyin);
            Assert.Greater(pinyin.Length, 0, "Should produce pinyin output");

            Debug.Log($"[Phase2Tests] '{testText}' -> {string.Join(" ", pinyin)}");
        }

        [Test]
        public void IPAConverter_ShouldConvertPinyin()
        {
            var testPinyin = new[] { "ni3", "hao3" };
            var ipa = ipaConverter.ConvertMultipleToIPA(testPinyin);

            Assert.IsNotNull(ipa);
            Assert.Greater(ipa.Length, 0, "Should produce IPA phonemes");

            // ConvertMultipleToIPA returns individual phonemes, not syllables
            Debug.Log($"[Phase2Tests] Input pinyin: {string.Join(" ", testPinyin)}");
            Debug.Log($"[Phase2Tests] Output IPA phonemes: {string.Join(" ", ipa)} (count: {ipa.Length})");

            // Each syllable is split into multiple phonemes (consonant + vowel + tone)
            foreach (var ipaItem in ipa)
            {
                Assert.IsNotEmpty(ipaItem, "IPA phoneme should not be empty");
            }
        }

        [Test]
        public void FullPipeline_ShouldProcessChineseText()
        {
            var testText = "你好世界";

            // Full pipeline
            var normalized = normalizer.Normalize(testText);
            var pinyin = converter.GetPinyin(normalized);
            var ipa = ipaConverter.ConvertMultipleToIPA(pinyin);

            Assert.IsNotNull(normalized);
            Assert.IsNotNull(pinyin);
            Assert.IsNotNull(ipa);
            Assert.Greater(ipa.Length, 0, "Should produce IPA output");

            Debug.Log($"[Phase2Tests] Full pipeline:");
            Debug.Log($"  Input: {testText}");
            Debug.Log($"  Normalized: {normalized}");
            Debug.Log($"  Pinyin: {string.Join(" ", pinyin)}");
            Debug.Log($"  IPA: {string.Join(" ", ipa)}");
        }

        [Test]
        public void Performance_BasicText_ShouldBeAcceptable()
        {
            var testText = "你好世界";
            var iterations = 100;

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < iterations; i++)
            {
                var normalized = normalizer.Normalize(testText);
                var pinyin = converter.GetPinyin(normalized);
                var ipa = ipaConverter.ConvertMultipleToIPA(pinyin);
            }

            stopwatch.Stop();
            var avgMs = stopwatch.ElapsedMilliseconds / (double)iterations;

            Debug.Log($"[Phase2Tests] Average processing time: {avgMs:F2}ms for '{testText}'");
            Assert.Less(avgMs, 10.0, "Basic text should process quickly");
        }

        [Test]
        public void CharacterCoverage_ShouldHandleBasicSet()
        {
            // Test basic characters that should be in fallback dictionary
            var basicChars = "你好我是中国人世界";
            var foundCount = 0;
            var totalCount = basicChars.Length;

            foreach (char ch in basicChars)
            {
                if (dictionary.TryGetCharacterPinyin(ch, out var pinyin))
                {
                    foundCount++;
                }
            }

            var coverage = foundCount / (float)totalCount * 100;
            Debug.Log($"[Phase2Tests] Basic character coverage: {foundCount}/{totalCount} ({coverage:F1}%)");

            Assert.Greater(coverage, 80f, "Should cover most basic characters");
        }

        [Test]
        public void DictionaryInfo_ShouldShowCurrentStatus()
        {
            Debug.Log($"[Phase2Tests] Dictionary Status:");
            Debug.Log($"  Character count: {dictionary.CharacterCount}");
            Debug.Log($"  Phrase count: {dictionary.PhraseCount}");
            Debug.Log($"  IPA mapping count: {dictionary.IPACount}");
            Debug.Log($"  Word frequency count: {dictionary.WordCount}");

            // Note about expanded dictionary
            if (dictionary.CharacterCount < 100)
            {
                Debug.Log($"  Note: Using fallback dictionary. Expanded dictionary (11,000+ chars) available but disabled for Unity Editor stability.");
                Debug.Log($"  To use expanded dictionary, set useExpandedDictionaries=true in ChineseDictionaryLoader");
            }
        }
    }
}