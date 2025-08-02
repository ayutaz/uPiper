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
            var testCases = new[]
            {
                ("你好！", "你好"),
                ("世界。", "世界"),
                ("Hello世界", "世界"),
                ("123中国456", "中国"),
                ("你好，世界！", "你好世界")
            };

            foreach (var (input, expected) in testCases)
            {
                var result = normalizer.Normalize(input);
                Assert.AreEqual(expected, result, 
                    $"Should normalize '{input}' to '{expected}'");
            }
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
            Assert.AreEqual(testPinyin.Length, ipa.Length, 
                "Should convert all pinyin syllables");
            
            foreach (var ipaItem in ipa)
            {
                Assert.IsNotEmpty(ipaItem, "IPA should not be empty");
            }
            
            Debug.Log($"[Phase2Tests] Pinyin -> IPA: {string.Join(" ", ipa)}");
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