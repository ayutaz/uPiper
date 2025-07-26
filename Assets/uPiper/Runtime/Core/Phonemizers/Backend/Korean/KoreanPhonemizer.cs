using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace uPiper.Core.Phonemizers.Backend.Korean
{
    using uPiper.Core.Phonemizers;
    using uPiper.Core.Phonemizers.Backend;
    /// <summary>
    /// Korean phonemizer implementation using Hangul decomposition and rule-based G2P.
    /// No external dependencies required - uses algorithmic approach.
    /// </summary>
    public class KoreanPhonemizer : PhonemizerBackendBase
    {
        private HangulProcessor hangulProcessor;
        private KoreanG2P g2pEngine;
        private KoreanTextNormalizer normalizer;
        private Dictionary<string, string[]> exceptionDict;
        private readonly object dictLock = new object();
        
        /// <inheritdoc/>
        public override string Name => "Korean";

        /// <inheritdoc/>
        public override string Version => "1.0.0";

        /// <inheritdoc/>
        public override string License => "MIT";

        /// <inheritdoc/>
        public override string[] SupportedLanguages => new[] 
        { 
            "ko", "ko-KR"
        };

        /// <inheritdoc/>
        protected override async Task<bool> InitializeInternalAsync(
            PhonemizerBackendOptions options,
            CancellationToken cancellationToken)
        {
            try
            {
                // Initialize components
                hangulProcessor = new HangulProcessor();
                g2pEngine = new KoreanG2P();
                normalizer = new KoreanTextNormalizer();
                exceptionDict = new Dictionary<string, string[]>();

                // Load exception dictionary if available
                var dictPath = GetExceptionDictionaryPath(options?.DataPath);
                if (File.Exists(dictPath))
                {
                    await LoadExceptionDictionaryAsync(dictPath, cancellationToken);
                    Debug.Log($"Korean exception dictionary loaded: {exceptionDict.Count} entries");
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize Korean phonemizer: {ex.Message}");
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
                // 1. Normalize text
                var normalized = normalizer.Normalize(text);
                
                // 2. Process text character by character
                var phonemes = new List<string>();

                for (int i = 0; i < normalized.Length; i++)
                {
                    char ch = normalized[i];
                    
                    if (hangulProcessor.IsHangul(ch))
                    {
                        // Check for exceptions first
                        string word = ExtractWord(normalized, i);
                        string[] exceptionPhonemes = null;
                        
                        lock (dictLock)
                        {
                            if (!string.IsNullOrEmpty(word) && exceptionDict.TryGetValue(word, out exceptionPhonemes))
                            {
                                // Use exception pronunciation
                                foreach (var phoneme in exceptionPhonemes)
                                {
                                    phonemes.Add(phoneme);
                                }
                                
                                // Skip the word
                                i += word.Length - 1;
                                continue;
                            }
                        }
                        
                        // Decompose Hangul syllable
                        var jamo = hangulProcessor.DecomposeHangul(ch);
                        
                        // Apply G2P rules considering context
                        char? prevChar = i > 0 ? normalized[i - 1] : (char?)null;
                        char? nextChar = i < normalized.Length - 1 ? normalized[i + 1] : (char?)null;
                        
                        var syllablePhonemes = g2pEngine.JamoToPhonemes(
                            jamo, prevChar, nextChar, i == 0);
                        
                        // Add phonemes
                        foreach (var phoneme in syllablePhonemes)
                        {
                            if (!string.IsNullOrEmpty(phoneme))
                            {
                                phonemes.Add(phoneme);
                            }
                        }
                    }
                    else if (char.IsLetter(ch) && ch < 128)
                    {
                        // English letter
                        phonemes.Add(ch.ToString().ToLower());
                    }
                    else if (char.IsWhiteSpace(ch) || char.IsPunctuation(ch))
                    {
                        // Add pause
                        phonemes.Add("_");
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
                Debug.LogError($"Error in Korean phonemization: {ex.Message}");
                throw;
            }
        }

        private string ExtractWord(string text, int startIndex)
        {
            // Extract continuous Hangul characters as a word
            var word = new StringBuilder();
            
            for (int i = startIndex; i < text.Length; i++)
            {
                if (hangulProcessor.IsHangul(text[i]))
                {
                    word.Append(text[i]);
                }
                else
                {
                    break;
                }
            }
            
            return word.ToString();
        }

        private async Task LoadExceptionDictionaryAsync(string path, CancellationToken cancellationToken)
        {
            var lines = await File.ReadAllLinesAsync(path, Encoding.UTF8, cancellationToken);
            
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                var parts = line.Split('\t');
                if (parts.Length >= 2)
                {
                    var word = parts[0].Trim();
                    var phonemes = parts[1].Trim().Split(' ');
                    
                    lock (dictLock)
                    {
                        exceptionDict[word] = phonemes;
                    }
                }
            }
        }

        private string GetExceptionDictionaryPath(string customPath)
        {
            if (!string.IsNullOrEmpty(customPath))
            {
                return Path.Combine(customPath, "korean_exceptions.txt");
            }

            // Default path in StreamingAssets
            return Path.Combine(Application.streamingAssetsPath, 
                "uPiper", "Languages", "Korean", "korean_exceptions_sample.txt");
        }

        /// <inheritdoc/>
        public override long GetMemoryUsage()
        {
            long size = 0;
            
            if (exceptionDict != null)
            {
                // Estimate dictionary memory usage
                size += exceptionDict.Count * 60; // Rough estimate per entry
            }
            
            return size;
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        protected override void DisposeInternal()
        {
            exceptionDict?.Clear();
            hangulProcessor = null;
            g2pEngine = null;
            normalizer = null;
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeInternal();
            }
            base.Dispose(disposing);
        }
    }
}