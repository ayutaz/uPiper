using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using uPiper.Core.Phonemizers;

namespace uPiper.Core.Phonemizers.Backend
{
    /// <summary>
    /// Chinese phonemizer implementation
    /// </summary>
    public class ChinesePhonemizer : PhonemizerBackendBase
    {
        private Dictionary<char, string[]> pinyinDict;
        private readonly object dictLock = new object();
        
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
                // Initialize components
                pinyinDict = new Dictionary<char, string[]>();

                // Initialize with basic mappings
                InitializeBasicPinyinMappings();
                
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
            if (string.IsNullOrEmpty(text))
            {
                return new PhonemeResult { Phonemes = new string[0] };
            }

            try
            {
                var normalized = NormalizeText(text);
                var segments = SegmentText(normalized);
                var phonemes = new List<string>();

                foreach (var segment in segments)
                {
                    if (string.IsNullOrWhiteSpace(segment))
                    {
                        phonemes.Add("_");
                        continue;
                    }

                    foreach (char ch in segment)
                    {
                        if (IsChinese(ch))
                        {
                            var pinyinOptions = GetPinyin(ch);
                            if (pinyinOptions != null && pinyinOptions.Length > 0)
                            {
                                var pinyin = pinyinOptions[0];
                                var ipa = PinyinToIPA(pinyin);
                                phonemes.AddRange(ipa);
                            }
                        }
                        else if (char.IsLetter(ch))
                        {
                            phonemes.Add(ch.ToString().ToLower());
                        }
                        else if (char.IsPunctuation(ch))
                        {
                            phonemes.Add("_");
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
        
        private void InitializeBasicPinyinMappings()
        {
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
            return null;
        }
        
        private bool IsChinese(char ch)
        {
            return (ch >= 0x4E00 && ch <= 0x9FFF) ||
                   (ch >= 0x3400 && ch <= 0x4DBF) ||
                   (ch >= 0xF900 && ch <= 0xFAFF);
        }
        
        public override long GetMemoryUsage()
        {
            return pinyinDict?.Count * 100 ?? 0;
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
        
        private string NormalizeText(string text)
        {
            // Simple normalization
            return text.Trim();
        }
        
        private List<string> SegmentText(string text)
        {
            // Simple character-based segmentation
            var segments = new List<string>();
            foreach (char ch in text)
            {
                segments.Add(ch.ToString());
            }
            return segments;
        }
        
        private string[] PinyinToIPA(string pinyin)
        {
            // Simple conversion - in real implementation would be more complex
            var tone = pinyin[pinyin.Length - 1];
            var syllable = pinyin.Substring(0, pinyin.Length - 1);
            return new[] { syllable };
        }
        
        protected override void DisposeInternal()
        {
            pinyinDict?.Clear();
        }
    }
}