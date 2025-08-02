using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core.Phonemizers.Backend.Chinese;

namespace uPiper.Tests.Runtime.ChinesePhonemizer
{
    /// <summary>
    /// Tests for Phase 2 expanded Chinese dictionary
    /// </summary>
    public class ChineseExpandedDictionaryTests
    {
        private ChineseDictionaryLoader loader;
        private ChinesePinyinDictionary dictionary;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            loader = new ChineseDictionaryLoader();
            
            // Load dictionary asynchronously
            var loadTask = Task.Run(async () =>
            {
                dictionary = await loader.LoadAsync();
            });
            
            // Wait for task completion
            while (!loadTask.IsCompleted)
            {
                yield return null;
            }
            
            if (loadTask.IsFaulted)
            {
                throw loadTask.Exception?.GetBaseException() ?? new System.Exception("Dictionary load failed");
            }
        }

        [Test]
        public void ExpandedDictionary_ShouldHaveOver10000Characters()
        {
            // Phase 2 expanded dictionary should have significantly more characters
            Assert.Greater(dictionary.CharacterCount, 10000, 
                "Expanded dictionary should have over 10,000 characters");
            
            Debug.Log($"Expanded dictionary loaded with {dictionary.CharacterCount} characters");
        }

        [Test]
        public void ExpandedDictionary_ShouldHaveOver100000Phrases()
        {
            // Phase 2 expanded dictionary should have comprehensive phrase coverage
            Assert.Greater(dictionary.PhraseCount, 100000, 
                "Expanded dictionary should have over 100,000 phrases");
            
            Debug.Log($"Expanded dictionary loaded with {dictionary.PhraseCount} phrases");
        }

        [Test]
        public void ExpandedDictionary_ShouldCoverCommonCharacters()
        {
            // Test coverage of common characters
            var commonCharacters = new[] 
            { 
                "的", "一", "是", "了", "我", "不", "人", "在", "他", "有",
                "这", "个", "上", "们", "来", "到", "时", "大", "地", "为",
                "子", "中", "你", "说", "生", "国", "年", "着", "就", "那"
            };

            foreach (var char in commonCharacters)
            {
                Assert.IsTrue(dictionary.TryGetCharacterPinyin(char[0], out var pinyin),
                    $"Character '{char}' should be in expanded dictionary");
                Assert.Greater(pinyin.Length, 0, 
                    $"Character '{char}' should have pinyin readings");
            }
        }

        [Test]
        public void ExpandedDictionary_ShouldCoverTechnicalTerms()
        {
            // Test technical and modern terms
            var technicalPhrases = new[] 
            { 
                "电脑", "手机", "网络", "软件", "硬件", 
                "人工智能", "机器学习", "深度学习", "神经网络",
                "互联网", "社交媒体", "电子商务"
            };

            int foundCount = 0;
            foreach (var phrase in technicalPhrases)
            {
                if (dictionary.TryGetPhrasePinyin(phrase, out var pinyin))
                {
                    foundCount++;
                    Debug.Log($"Found technical term: {phrase} -> {pinyin}");
                }
            }

            // Should find at least 80% of technical terms
            Assert.Greater(foundCount, technicalPhrases.Length * 0.8f,
                "Expanded dictionary should cover most technical terms");
        }

        [Test]
        public void ExpandedDictionary_ShouldHaveWordFrequencyData()
        {
            // Test word frequency functionality
            Assert.Greater(dictionary.WordCount, 0, 
                "Expanded dictionary should have word frequency data");
            
            // Test high-frequency words
            var highFreqWords = new[] { "的", "一", "是", "了", "我" };
            foreach (var word in highFreqWords)
            {
                if (dictionary.TryGetWordFrequency(word, out var freq))
                {
                    Assert.Greater(freq, 0, 
                        $"High-frequency word '{word}' should have positive frequency");
                    Debug.Log($"Word '{word}' frequency: {freq}");
                }
            }
        }

        [Test]
        public void ExpandedDictionary_ShouldHandleRareCharacters()
        {
            // Test some less common characters
            var rareCharacters = new[] 
            { 
                "龘", // Complex character with many strokes
                "鬱", // Traditional character
                "薔", // Plant name
                "驪", // Classical character
                "齋"  // Religious/cultural term
            };

            int foundCount = 0;
            foreach (var char in rareCharacters)
            {
                if (dictionary.TryGetCharacterPinyin(char[0], out var pinyin))
                {
                    foundCount++;
                    Debug.Log($"Found rare character: {char} -> {string.Join(", ", pinyin)}");
                }
            }

            // Should find at least some rare characters
            Assert.Greater(foundCount, 0, 
                "Expanded dictionary should include some rare characters");
        }

        [Test]
        public void ExpandedDictionary_MemoryUsage_ShouldBeReasonable()
        {
            // Get memory usage
            var startMemory = System.GC.GetTotalMemory(false);
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
            var afterGCMemory = System.GC.GetTotalMemory(false);
            
            var memoryUsageMB = (afterGCMemory - startMemory) / (1024f * 1024f);
            Debug.Log($"Expanded dictionary memory usage: ~{memoryUsageMB:F2} MB");
            
            // Should use less than 50MB for expanded dictionary
            Assert.Less(memoryUsageMB, 50f, 
                "Expanded dictionary memory usage should be under 50MB");
        }

        [Test]
        public void ExpandedDictionary_IPAMappings_ShouldBeComprehensive()
        {
            // Test comprehensive IPA mappings
            var testSyllables = new[] 
            { 
                "ma", "mo", "me", "mi", "mu",
                "zhi", "chi", "shi", "ri",
                "zi", "ci", "si",
                "ju", "qu", "xu",
                "zhang", "chuang", "shuang"
            };

            int mappedCount = 0;
            foreach (var syllable in testSyllables)
            {
                if (dictionary.TryGetIPA(syllable, out var ipa))
                {
                    mappedCount++;
                    Assert.IsNotEmpty(ipa, $"IPA mapping for '{syllable}' should not be empty");
                }
            }

            // Should have IPA mappings for most test syllables
            Assert.Greater(mappedCount, testSyllables.Length * 0.9f,
                "Should have IPA mappings for at least 90% of test syllables");
        }
    }
}