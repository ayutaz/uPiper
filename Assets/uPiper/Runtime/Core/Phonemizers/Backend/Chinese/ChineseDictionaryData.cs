using System;
using System.Collections.Generic;
using UnityEngine;

namespace uPiper.Core.Phonemizers.Backend.Chinese
{
    /// <summary>
    /// Data structures for Chinese dictionary
    /// </summary>
    [Serializable]
    public class ChineseDictionaryData
    {
        /// <summary>
        /// Character to pinyin mappings
        /// </summary>
        [Serializable]
        public class CharacterPinyinEntry
        {
            public string character;
            public string[] pinyin;
        }

        /// <summary>
        /// Phrase to pinyin mappings for multi-tone character resolution
        /// </summary>
        [Serializable]
        public class PhrasePinyinEntry
        {
            public string phrase;
            public string pinyin;
        }

        /// <summary>
        /// Pinyin to IPA mappings
        /// </summary>
        [Serializable]
        public class PinyinIPAEntry
        {
            public string pinyin;
            public string ipa;
        }

        /// <summary>
        /// Word frequency data for segmentation
        /// </summary>
        [Serializable]
        public class WordFrequencyEntry
        {
            public string word;
            public float frequency;
        }

        public CharacterPinyinEntry[] characterEntries;
        public PhrasePinyinEntry[] phraseEntries;
        public PinyinIPAEntry[] pinyinIPAEntries;
        public WordFrequencyEntry[] wordFrequencies;
    }

    /// <summary>
    /// Loaded dictionary for runtime use
    /// </summary>
    public class ChinesePinyinDictionary
    {
        private readonly Dictionary<char, string[]> charToPinyin;
        private readonly Dictionary<string, string> phraseToPinyin;
        private readonly Dictionary<string, string> pinyinToIPA;
        private readonly Dictionary<string, float> wordFrequency;

        public ChinesePinyinDictionary()
        {
            charToPinyin = new Dictionary<char, string[]>();
            phraseToPinyin = new Dictionary<string, string>(StringComparer.Ordinal);
            pinyinToIPA = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            wordFrequency = new Dictionary<string, float>(StringComparer.Ordinal);
        }

        public void LoadFromData(ChineseDictionaryData data)
        {
            // Load character mappings
            if (data.characterEntries != null)
            {
                foreach (var entry in data.characterEntries)
                {
                    if (!string.IsNullOrEmpty(entry.character) && entry.character.Length == 1)
                    {
                        charToPinyin[entry.character[0]] = entry.pinyin;
                    }
                }
            }

            // Load phrase mappings
            if (data.phraseEntries != null)
            {
                foreach (var entry in data.phraseEntries)
                {
                    if (!string.IsNullOrEmpty(entry.phrase))
                    {
                        phraseToPinyin[entry.phrase] = entry.pinyin;
                    }
                }
            }

            // Load IPA mappings
            if (data.pinyinIPAEntries != null)
            {
                foreach (var entry in data.pinyinIPAEntries)
                {
                    if (!string.IsNullOrEmpty(entry.pinyin))
                    {
                        pinyinToIPA[entry.pinyin] = entry.ipa;
                    }
                }
            }

            // Load word frequencies
            if (data.wordFrequencies != null)
            {
                foreach (var entry in data.wordFrequencies)
                {
                    if (!string.IsNullOrEmpty(entry.word))
                    {
                        wordFrequency[entry.word] = entry.frequency;
                    }
                }
            }
        }

        public bool TryGetCharacterPinyin(char character, out string[] pinyin)
        {
            return charToPinyin.TryGetValue(character, out pinyin);
        }

        public bool TryGetPhrasePinyin(string phrase, out string pinyin)
        {
            return phraseToPinyin.TryGetValue(phrase, out pinyin);
        }

        public bool TryGetIPA(string pinyin, out string ipa)
        {
            // Remove tone number if present
            var pinyinNoTone = RemoveToneNumber(pinyin);
            return pinyinToIPA.TryGetValue(pinyinNoTone, out ipa);
        }

        public bool TryGetWordFrequency(string word, out float frequency)
        {
            return wordFrequency.TryGetValue(word, out frequency);
        }

        public int CharacterCount => charToPinyin.Count;
        public int PhraseCount => phraseToPinyin.Count;
        public int IPACount => pinyinToIPA.Count;
        public int WordCount => wordFrequency.Count;
        
        /// <summary>
        /// Get all phrases in the dictionary
        /// </summary>
        public IEnumerable<string> GetAllPhrases()
        {
            return phraseToPinyin.Keys;
        }

        private string RemoveToneNumber(string pinyin)
        {
            if (string.IsNullOrEmpty(pinyin))
                return pinyin;

            // Remove trailing tone number (1-5)
            if (pinyin.Length > 0 && char.IsDigit(pinyin[pinyin.Length - 1]))
            {
                return pinyin.Substring(0, pinyin.Length - 1);
            }
            return pinyin;
        }
    }
}