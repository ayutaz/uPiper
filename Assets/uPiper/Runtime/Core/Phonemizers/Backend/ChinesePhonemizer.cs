using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using uPiper.Core.Phonemizers;
using uPiper.Core.Phonemizers.Backend.Chinese;

namespace uPiper.Core.Phonemizers.Backend
{
    /// <summary>
    /// Chinese phonemizer implementation
    /// </summary>
    public class ChinesePhonemizer : PhonemizerBackendBase
    {
        private ChinesePinyinDictionary dictionary;
        private ChineseDictionaryLoader dictionaryLoader;
        private PinyinConverter pinyinConverter;
        private PinyinToIPAConverter ipaConverter;
        private ChineseTextNormalizer textNormalizer;
        private ChineseWordSegmenter wordSegmenter;
        private MultiToneProcessor multiToneProcessor;
        private TraditionalChineseConverter traditionalConverter;
        private readonly object dictLock = new();
        
        // Configuration option
        private bool useWordSegmentation = true;
        
        /// <summary>
        /// Enable or disable word segmentation
        /// </summary>
        public bool UseWordSegmentation
        {
            get => useWordSegmentation;
            set => useWordSegmentation = value;
        }

        public override string Name => "Chinese";
        public override string Version => "1.0.0";
        public override string License => "MIT";
        public override string[] SupportedLanguages => new[] { "zh", "zh-CN", "zh-TW", "zh-HK", "zh-SG" };

        protected override async Task<bool> InitializeInternalAsync(
            PhonemizerBackendOptions options,
            CancellationToken cancellationToken)
        {
            try
            {
                // Initialize dictionary loader
                dictionaryLoader = new ChineseDictionaryLoader();

                // Load dictionary data
                dictionary = await dictionaryLoader.LoadAsync(cancellationToken);

                // Initialize converters
                pinyinConverter = new PinyinConverter(dictionary);
                ipaConverter = new PinyinToIPAConverter(dictionary);
                textNormalizer = new ChineseTextNormalizer();
                wordSegmenter = new ChineseWordSegmenter(dictionary);
                multiToneProcessor = new MultiToneProcessor(dictionary);
                traditionalConverter = new TraditionalChineseConverter();

                Debug.Log($"Chinese phonemizer initialized with {dictionary.CharacterCount} characters, " +
                         $"{dictionary.PhraseCount} phrases, {dictionary.IPACount} IPA mappings");

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize Chinese phonemizer: {ex.Message}");
                return false;
            }
        }

        public override async Task<PhonemeResult> PhonemizeAsync(
            string text,
            string language,
            PhonemeOptions options = null,
            CancellationToken cancellationToken = default)
        {
            // For short text, execute synchronously to avoid Task.Run overhead
            if (text == null || text.Length < 100)
            {
                return PhonemizeInternal(text, language);
            }

            return await Task.Run(() => PhonemizeInternal(text, language), cancellationToken);
        }

        private PhonemeResult PhonemizeInternal(string text, string language)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new PhonemeResult
                {
                    Phonemes = Array.Empty<string>(),
                    Language = language,
                    Success = true
                };
            }

            // Quick check without lock
            if (dictionary == null)
            {
                throw new InvalidOperationException("Chinese phonemizer not initialized");
            }

            try
            {
                // Step 0: Convert Traditional Chinese to Simplified if needed
                if (traditionalConverter.ContainsTraditional(text))
                {
                    text = traditionalConverter.ConvertToSimplified(text);
                    Debug.Log($"[ChinesePhonemizer] Converted Traditional to Simplified Chinese");
                }
                
                // Step 1: Normalize text
                var normalized = textNormalizer.Normalize(text, ChineseTextNormalizer.NumberFormat.Formal);

                // Step 2: Split mixed Chinese-English text
                var segments = textNormalizer.SplitMixedText(normalized);
                var phonemes = new List<string>(normalized.Length * 2); // Pre-allocate capacity

                foreach (var (chinese, english) in segments)
                {
                    if (!string.IsNullOrEmpty(chinese))
                    {
                        if (useWordSegmentation)
                        {
                            // Use word segmentation for better context
                            var wordsWithPinyin = wordSegmenter.SegmentWithPinyinV2(chinese);
                            
                            Debug.Log($"[ChinesePhonemizer] Segmented '{chinese}' into {wordsWithPinyin.Count} words");
                            
                            foreach (var (word, pinyinArray) in wordsWithPinyin)
                            {
                                // Check if the word is punctuation
                                if (word.Length == 1 && char.IsPunctuation(word[0]))
                                {
                                    phonemes.Add("_");
                                    continue;
                                }
                                
                                // Convert each pinyin to IPA
                                Debug.Log($"[ChinesePhonemizer] Word '{word}' has {pinyinArray.Length} pinyin: [{string.Join(", ", pinyinArray)}]");
                                
                                foreach (var pinyin in pinyinArray)
                                {
                                    // Check if it's a punctuation character
                                    if (pinyin.Length == 1 && char.IsPunctuation(pinyin[0]))
                                    {
                                        phonemes.Add("_");
                                        continue;
                                    }
                                    
                                    if (pinyin.StartsWith("u") && pinyin.Length > 1 && pinyin.Length <= 6)
                                    {
                                        // Unicode fallback (e.g., u94f6), try to get character and retry
                                        Debug.LogWarning($"[ChinesePhonemizer] Found Unicode fallback: {pinyin} for character in '{word}'");
                                        
                                        // Try to parse the Unicode value
                                        if (pinyin.Length >= 2 && int.TryParse(pinyin.Substring(1), System.Globalization.NumberStyles.HexNumber, null, out int codePoint))
                                        {
                                            var ch = (char)codePoint;
                                            Debug.LogWarning($"[ChinesePhonemizer] Unicode {pinyin} represents character '{ch}'");
                                            
                                            // Try to get pinyin for this character from dictionary
                                            if (dictionary.TryGetCharacterPinyin(ch, out var fallbackPinyin) && fallbackPinyin.Length > 0)
                                            {
                                                var fallbackIPA = ipaConverter.ConvertToIPA(fallbackPinyin[0]);
                                                Debug.Log($"[ChinesePhonemizer] Fallback: '{ch}' → {fallbackPinyin[0]} → {fallbackIPA.Length} IPA phonemes");
                                                phonemes.AddRange(fallbackIPA);
                                            }
                                            else
                                            {
                                                Debug.LogWarning($"[ChinesePhonemizer] No pinyin found for character '{ch}' (U+{codePoint:X4})");
                                            }
                                        }
                                        continue;
                                    }
                                    
                                    var ipaPhonemes = ipaConverter.ConvertToIPA(pinyin);
                                    Debug.Log($"[ChinesePhonemizer] Pinyin '{pinyin}' → {ipaPhonemes.Length} IPA phonemes");
                                    phonemes.AddRange(ipaPhonemes);
                                }
                            }
                        }
                        else
                        {
                            // Original character-by-character processing
                            var pinyinArray = pinyinConverter.GetPinyin(chinese, usePhrase: true);

                            // Convert each pinyin to IPA
                            foreach (var pinyin in pinyinArray)
                            {
                                if (ChineseTextNormalizer.IsChinese(pinyin[0]))
                                {
                                    // It's still a Chinese character (no pinyin found)
                                    // Skip Debug.LogWarning for performance
                                }
                                else if (char.IsPunctuation(pinyin[0]))
                                {
                                    phonemes.Add("_");
                                }
                                else
                                {
                                    // Convert pinyin to IPA
                                    var ipaPhonemes = ipaConverter.ConvertToIPA(pinyin);
                                    phonemes.AddRange(ipaPhonemes);
                                }
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(english))
                    {
                        // For English text, just use letters as phonemes (simplified)
                        // TODO: Integrate with English phonemizer
                        foreach (var ch in english.ToLower())
                        {
                            if (char.IsLetter(ch))
                            {
                                phonemes.Add(ch.ToString());
                            }
                            else if (char.IsWhiteSpace(ch))
                            {
                                phonemes.Add("_");
                            }
                        }
                    }
                }

                return new PhonemeResult
                {
                    Phonemes = phonemes.ToArray(),
                    Language = language,
                    Success = true
                };
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error in Chinese phonemization: {ex.Message}");
                throw;
            }
        }


        private bool IsChinese(char ch)
        {
            return (ch >= 0x4E00 && ch <= 0x9FFF) ||
                   (ch >= 0x3400 && ch <= 0x4DBF) ||
                   (ch >= 0xF900 && ch <= 0xFAFF);
        }

        public override long GetMemoryUsage()
        {
            if (dictionary == null)
                return 0;

            // Estimate memory usage
            var charMemory = dictionary.CharacterCount * 50; // ~50 bytes per character entry
            var phraseMemory = dictionary.PhraseCount * 100; // ~100 bytes per phrase
            var ipaMemory = dictionary.IPACount * 60; // ~60 bytes per IPA mapping
            var wordMemory = dictionary.WordCount * 40; // ~40 bytes per word frequency

            return charMemory + phraseMemory + ipaMemory + wordMemory;
        }

        public override BackendCapabilities GetCapabilities()
        {
            return new BackendCapabilities
            {
                SupportsIPA = true,
                SupportsStress = false,
                SupportsSyllables = true,
                SupportsTones = true,
                SupportsDuration = false,
                SupportsBatchProcessing = false,
                IsThreadSafe = true,
                RequiresNetwork = false
            };
        }



        protected override void DisposeInternal()
        {
            dictionary = null;
            dictionaryLoader = null;
            pinyinConverter = null;
            ipaConverter = null;
            textNormalizer = null;
            wordSegmenter = null;
            multiToneProcessor = null;
            traditionalConverter = null;
        }
    }
}