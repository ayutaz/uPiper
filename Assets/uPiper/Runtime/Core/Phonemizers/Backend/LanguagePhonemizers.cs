using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using uPiper.Core.Phonemizers;
using uPiper.Core.Phonemizers.Backend;

namespace uPiper.Core.Phonemizers.Backend
{
    /// <summary>
    /// Proxy class for Chinese phonemizer to avoid namespace resolution issues
    /// </summary>
    public class ChinesePhonemizerProxy : PhonemizerBackendBase
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
    
    /// <summary>
    /// Proxy class for Korean phonemizer to avoid namespace resolution issues
    /// </summary>
    public class KoreanPhonemizerProxy : PhonemizerBackendBase
    {
        private Dictionary<string, string[]> exceptionDict;
        private readonly object dictLock = new object();
        
        public override string Name => "Korean";
        public override string Version => "1.0.0";
        public override string License => "MIT";
        public override string[] SupportedLanguages => new[] { "ko", "ko-KR" };
        
        protected override async Task<bool> InitializeInternalAsync(
            PhonemizerBackendOptions options,
            CancellationToken cancellationToken)
        {
            try
            {
                exceptionDict = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize Korean phonemizer: {ex.Message}");
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
                var phonemes = new List<string>();
                var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                foreach (var word in words)
                {
                    if (phonemes.Count > 0)
                        phonemes.Add("_");

                    string[] wordPhonemes = null;
                    
                    lock (dictLock)
                    {
                        if (exceptionDict.TryGetValue(word, out var dictPhonemes))
                        {
                            wordPhonemes = dictPhonemes;
                        }
                    }
                    
                    if (wordPhonemes == null)
                    {
                        wordPhonemes = ProcessKoreanWord(word);
                    }

                    phonemes.AddRange(wordPhonemes);
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
                Debug.LogError($"Error in Korean phonemization: {ex.Message}");
                throw;
            }
        }
        
        public override long GetMemoryUsage()
        {
            return exceptionDict?.Count * 60 ?? 0;
        }
        
        public override BackendCapabilities GetCapabilities()
        {
            return new BackendCapabilities
            {
                SupportsIPA = true,
                SupportsStress = false,
                SupportsSyllables = true,
                SupportsTones = false,
                SupportsDuration = false,
                SupportsBatchProcessing = false,
                IsThreadSafe = true,
                RequiresNetwork = false
            };
        }
        
        private string NormalizeKoreanText(string text)
        {
            // Simple normalization for Korean
            return text.Trim();
        }
        
        private string[] ProcessKoreanWord(string word)
        {
            // Simple phoneme generation for Korean
            var phonemes = new List<string>();
            foreach (char ch in word)
            {
                if (ch >= 0xAC00 && ch <= 0xD7A3) // Hangul syllables
                {
                    phonemes.Add("k"); // Simplified - would decompose in real implementation
                }
                else
                {
                    phonemes.Add(ch.ToString().ToLower());
                }
            }
            return phonemes.ToArray();
        }
        
        protected override void DisposeInternal()
        {
            exceptionDict?.Clear();
        }
    }
    
    /// <summary>
    /// Proxy class for Spanish phonemizer to avoid namespace resolution issues
    /// </summary>
    public class SpanishPhonemizerProxy : PhonemizerBackendBase
    {
        private Dictionary<string, string[]> spanishDict;
        private readonly object dictLock = new object();
        
        public override string Name => "Spanish";
        public override string Version => "1.0.0";
        public override string License => "MIT";
        public override string[] SupportedLanguages => new[] 
        { 
            "es", "es-ES", "es-MX", "es-AR", "es-CO", "es-CL", "es-PE", "es-VE", "es-EC", "es-BO", "es-UY", "es-PY"
        };
        
        protected override async Task<bool> InitializeInternalAsync(
            PhonemizerBackendOptions options,
            CancellationToken cancellationToken)
        {
            try
            {
                spanishDict = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize Spanish phonemizer: {ex.Message}");
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
                var words = TokenizeSpanish(normalized);
                var phonemes = new List<string>();

                foreach (var word in words)
                {
                    if (string.IsNullOrWhiteSpace(word))
                    {
                        phonemes.Add("_");
                        continue;
                    }

                    string[] wordPhonemes = null;
                    
                    lock (dictLock)
                    {
                        if (spanishDict.TryGetValue(word.ToUpper(), out var dictPhonemes))
                        {
                            wordPhonemes = dictPhonemes;
                        }
                    }
                    
                    if (wordPhonemes == null)
                    {
                        wordPhonemes = ProcessSpanishWord(word);
                    }

                    phonemes.AddRange(wordPhonemes);
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
                Debug.LogError($"Error in Spanish phonemization: {ex.Message}");
                throw;
            }
        }
        
        private List<string> TokenizeSpanish(string text)
        {
            var words = new List<string>();
            var currentWord = "";

            foreach (char c in text)
            {
                if (char.IsLetter(c) || c == '\'' || c == '-' || c == 'ñ' || c == 'Ñ')
                {
                    currentWord += c;
                }
                else
                {
                    if (!string.IsNullOrEmpty(currentWord))
                    {
                        words.Add(currentWord);
                        currentWord = "";
                    }
                    
                    if (char.IsPunctuation(c) && c != '\'' && c != '-')
                    {
                        words.Add(c.ToString());
                    }
                }
            }

            if (!string.IsNullOrEmpty(currentWord))
            {
                words.Add(currentWord);
            }

            return words;
        }
        
        public override long GetMemoryUsage()
        {
            return spanishDict?.Count * 80 ?? 0;
        }
        
        public override BackendCapabilities GetCapabilities()
        {
            return new BackendCapabilities
            {
                SupportsIPA = true,
                SupportsStress = true,
                SupportsSyllables = true,
                SupportsTones = false,
                SupportsDuration = false,
                SupportsBatchProcessing = false,
                IsThreadSafe = true,
                RequiresNetwork = false
            };
        }
        
        private string[] ProcessSpanishWord(string word)
        {
            // Simple phoneme generation for Spanish
            var phonemes = new List<string>();
            foreach (char ch in word.ToLower())
            {
                // Basic Spanish G2P rules
                switch (ch)
                {
                    case 'a': phonemes.Add("a"); break;
                    case 'e': phonemes.Add("e"); break;
                    case 'i': phonemes.Add("i"); break;
                    case 'o': phonemes.Add("o"); break;
                    case 'u': phonemes.Add("u"); break;
                    case 'ñ': phonemes.Add("ɲ"); break;
                    case 'j': phonemes.Add("x"); break;
                    case 'r': phonemes.Add("ɾ"); break;
                    case 'v': phonemes.Add("b"); break;
                    case 'b': phonemes.Add("b"); break;
                    case 'll': phonemes.Add("ʎ"); break;
                    default:
                        if (char.IsLetter(ch))
                            phonemes.Add(ch.ToString());
                        break;
                }
            }
            return phonemes.ToArray();
        }
        
        protected override void DisposeInternal()
        {
            spanishDict?.Clear();
        }
    }
}