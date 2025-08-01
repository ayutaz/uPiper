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
        private readonly object dictLock = new();

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
            return await Task.Run(() =>
            {
                if (string.IsNullOrEmpty(text))
                {
                    return new PhonemeResult { Phonemes = new string[0] };
                }

                try
                {
                    // Step 1: Normalize text
                    var normalized = textNormalizer.Normalize(text, ChineseTextNormalizer.NumberFormat.Formal);
                    
                    // Step 2: Split mixed Chinese-English text
                    var segments = textNormalizer.SplitMixedText(normalized);
                    var phonemes = new List<string>();
                    
                    foreach (var (chinese, english) in segments)
                    {
                        if (!string.IsNullOrEmpty(chinese))
                        {
                            // Process Chinese text
                            var pinyinArray = pinyinConverter.GetPinyin(chinese, usePhrase: true);
                            
                            // Convert each pinyin to IPA
                            foreach (var pinyin in pinyinArray)
                            {
                                if (ChineseTextNormalizer.IsChinese(pinyin[0]))
                                {
                                    // It's still a Chinese character (no pinyin found)
                                    Debug.LogWarning($"No pinyin for character: {pinyin}");
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
            }, cancellationToken);
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
        }
    }
}