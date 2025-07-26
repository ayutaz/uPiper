using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using uPiper.Core.Phonemizers;
using uPiper.Core.Phonemizers.Backend;

namespace uPiper.Core.Phonemizers.Backend.Proxy
{
    /// <summary>
    /// Proxy class for Korean phonemizer to avoid namespace resolution issues
    /// </summary>
    public class KoreanPhonemizerProxy : Backend.PhonemizerBackendBase
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
                var normalized = NormalizeKoreanText(text);
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
        
        protected override void DisposeInternal()
        {
            exceptionDict?.Clear();
        }
    }
}