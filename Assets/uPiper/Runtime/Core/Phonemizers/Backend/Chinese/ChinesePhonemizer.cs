using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using uPiper.Core.Phonemizers.Backend;

namespace uPiper.Core.Phonemizers.Backend.Chinese
{
    /// <summary>
    /// Chinese (Mandarin) phonemizer implementation using pinyin-based approach.
    /// Supports Simplified and Traditional Chinese.
    /// </summary>
    public class ChinesePhonemizer : PhonemizerBackendBase
    {
        private Dictionary<char, string[]> pinyinDict;
        private PinyinToPhonemeMapper phonemeMapper;
        private ChineseTextSegmenter segmenter;
        private ChineseTextNormalizer normalizer;
        private readonly object dictLock = new object();
        
        /// <inheritdoc/>
        public override string Name => "Chinese";

        /// <inheritdoc/>
        public override string Version => "1.0.0";

        /// <inheritdoc/>
        public override string License => "MIT";

        /// <inheritdoc/>
        public override string[] SupportedLanguages => new[] 
        { 
            "zh", "zh-CN", "zh-TW", "zh-HK", "zh-SG"
        };

        /// <inheritdoc/>
        protected override async Task<bool> InitializeInternalAsync(
            PhonemizerBackendOptions options,
            CancellationToken cancellationToken)
        {
            try
            {
                // Initialize components
                normalizer = new ChineseTextNormalizer();
                segmenter = new ChineseTextSegmenter();
                phonemeMapper = new PinyinToPhonemeMapper();
                pinyinDict = new Dictionary<char, string[]>();

                // Load pinyin dictionary
                var dictPath = GetDictionaryPath(options?.DataPath);
                if (File.Exists(dictPath))
                {
                    await LoadPinyinDictionaryAsync(dictPath, cancellationToken);
                    Debug.Log($"Chinese pinyin dictionary loaded: {pinyinDict.Count} entries");
                }
                else
                {
                    Debug.LogWarning($"Chinese pinyin dictionary not found at: {dictPath}");
                    // Initialize with basic mappings
                    InitializeBasicPinyinMappings();
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize Chinese phonemizer: {ex.Message}");
                return false;
            }
        }

        /// <inheritdoc/>
        public override async Task<PhonemeResult> PhonemizeAsync(
            string text, 
            string language, 
            PhonemeOptions options = null, 
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new PhonemeResult { Phonemes = new string[0] };
            }

            try
            {
                // 1. Normalize text (numbers, punctuation, etc.)
                var normalized = normalizer.Normalize(text);
                
                // 2. Segment text into words/characters
                var segments = segmenter.Segment(normalized);
                
                // 3. Convert to pinyin and then to phonemes
                var phonemes = new List<string>();
                var timings = new List<PhonemeInfo>();
                double currentTime = 0;

                foreach (var segment in segments)
                {
                    if (string.IsNullOrWhiteSpace(segment))
                    {
                        // Add pause
                        phonemes.Add("_");
                        timings.Add(new PhonemeInfo 
                        { 
                            Phoneme = "_", 
                            StartTime = currentTime, 
                            Duration = 0.1 
                        });
                        currentTime += 0.1;
                        continue;
                    }

                    // Process each character in the segment
                    foreach (char ch in segment)
                    {
                        if (IsChinese(ch))
                        {
                            var pinyinOptions = GetPinyin(ch);
                            if (pinyinOptions != null && pinyinOptions.Length > 0)
                            {
                                // Use the first pronunciation by default
                                // In a more sophisticated implementation, use context
                                var pinyin = pinyinOptions[0];
                                var ipa = phonemeMapper.PinyinToIPA(pinyin);
                                
                                // Add phonemes
                                foreach (var phoneme in ipa)
                                {
                                    phonemes.Add(phoneme);
                                    timings.Add(new PhonemeInfo 
                                    { 
                                        Phoneme = phoneme, 
                                        StartTime = currentTime, 
                                        Duration = 0.05 
                                    });
                                    currentTime += 0.05;
                                }
                            }
                        }
                        else if (char.IsLetter(ch))
                        {
                            // Handle English letters in Chinese text
                            phonemes.Add(ch.ToString().ToLower());
                            timings.Add(new PhonemeInfo 
                            { 
                                Phoneme = ch.ToString().ToLower(), 
                                StartTime = currentTime, 
                                Duration = 0.05 
                            });
                            currentTime += 0.05;
                        }
                        else if (char.IsPunctuation(ch))
                        {
                            // Add pause for punctuation
                            phonemes.Add("_");
                            timings.Add(new PhonemeInfo 
                            { 
                                Phoneme = "_", 
                                StartTime = currentTime, 
                                Duration = 0.2 
                            });
                            currentTime += 0.2;
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

        private async Task LoadPinyinDictionaryAsync(string path, CancellationToken cancellationToken)
        {
            var lines = await File.ReadAllLinesAsync(path, Encoding.UTF8, cancellationToken);
            
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                var parts = line.Split('\t');
                if (parts.Length >= 2 && parts[0].Length == 1)
                {
                    var character = parts[0][0];
                    var pinyinList = parts[1].Split(',').Select(p => p.Trim()).ToArray();
                    
                    lock (dictLock)
                    {
                        pinyinDict[character] = pinyinList;
                    }
                }
            }
        }

        private void InitializeBasicPinyinMappings()
        {
            // Initialize with some basic common characters
            // This is a fallback for when dictionary is not available
            lock (dictLock)
            {
                pinyinDict['你'] = new[] { "ni3" };
                pinyinDict['好'] = new[] { "hao3" };
                pinyinDict['中'] = new[] { "zhong1" };
                pinyinDict['国'] = new[] { "guo2" };
                pinyinDict['人'] = new[] { "ren2" };
                pinyinDict['我'] = new[] { "wo3" };
                pinyinDict['是'] = new[] { "shi4" };
                pinyinDict['的'] = new[] { "de5" };
                pinyinDict['一'] = new[] { "yi1" };
                pinyinDict['不'] = new[] { "bu4" };
                // Add more basic mappings as needed
            }
        }

        private string[] GetPinyin(char character)
        {
            lock (dictLock)
            {
                if (pinyinDict.TryGetValue(character, out var pinyin))
                {
                    return pinyin;
                }
            }
            
            // Fallback: return null or generate approximate pronunciation
            Debug.LogWarning($"No pinyin found for character: {character}");
            return null;
        }

        private bool IsChinese(char ch)
        {
            // Check if character is in CJK Unified Ideographs range
            return (ch >= 0x4E00 && ch <= 0x9FFF) ||     // CJK Unified Ideographs
                   (ch >= 0x3400 && ch <= 0x4DBF) ||     // CJK Extension A
                   (ch >= 0x20000 && ch <= 0x2A6DF) ||   // CJK Extension B
                   (ch >= 0x2A700 && ch <= 0x2B73F) ||   // CJK Extension C
                   (ch >= 0x2B740 && ch <= 0x2B81F) ||   // CJK Extension D
                   (ch >= 0x2B820 && ch <= 0x2CEAF) ||   // CJK Extension E
                   (ch >= 0xF900 && ch <= 0xFAFF) ||     // CJK Compatibility Ideographs
                   (ch >= 0x2F800 && ch <= 0x2FA1F);     // CJK Compatibility Ideographs Supplement
        }

        private string GetDictionaryPath(string customPath)
        {
            if (!string.IsNullOrEmpty(customPath))
            {
                return Path.Combine(customPath, "pinyin_dict.txt");
            }

            // Default path in StreamingAssets
            return Path.Combine(Application.streamingAssetsPath, 
                "uPiper", "Languages", "Chinese", "pinyin_dict_sample.txt");
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                pinyinDict?.Clear();
                segmenter = null;
                phonemeMapper = null;
                normalizer = null;
            }
            base.Dispose(disposing);
        }
    }
}