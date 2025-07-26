using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace uPiper.Core.Phonemizers.Backend.Spanish
{
    using uPiper.Core.Phonemizers;
    using uPiper.Core.Phonemizers.Backend;
    /// <summary>
    /// Spanish phonemizer implementation using dictionary lookup and G2P rules.
    /// Supports Spain Spanish (es-ES) and Latin American variants.
    /// </summary>
    public class SpanishPhonemizer : PhonemizerBackendBase
    {
        private Dictionary<string, string[]> spanishDict;
        private SpanishG2P g2pEngine;
        private SpanishTextNormalizer normalizer;
        private readonly object dictLock = new object();
        
        /// <inheritdoc/>
        public override string Name => "Spanish";

        /// <inheritdoc/>
        public override string Version => "1.0.0";

        /// <inheritdoc/>
        public override string License => "MIT";

        /// <inheritdoc/>
        public override string[] SupportedLanguages => new[] 
        { 
            "es", "es-ES", "es-MX", "es-AR", "es-CO", "es-CL", "es-PE", "es-VE", "es-EC", "es-BO", "es-UY", "es-PY"
        };

        /// <inheritdoc/>
        protected override async Task<bool> InitializeInternalAsync(
            PhonemizerBackendOptions options,
            CancellationToken cancellationToken)
        {
            try
            {
                // Initialize components
                normalizer = new SpanishTextNormalizer();
                g2pEngine = new SpanishG2P();
                spanishDict = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

                // Load Spanish dictionary
                var dictPath = GetDictionaryPath(options?.DataPath);
                if (File.Exists(dictPath))
                {
                    await LoadDictionaryAsync(dictPath, cancellationToken);
                    Debug.Log($"Spanish dictionary loaded: {spanishDict.Count} entries");
                }
                else
                {
                    Debug.LogWarning($"Spanish dictionary not found at: {dictPath}. Using G2P rules only.");
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize Spanish phonemizer: {ex.Message}");
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
                // 1. Normalize Spanish text (handle ñ, accents, etc.)
                var normalized = normalizer.Normalize(text);
                
                // 2. Tokenize
                var words = TokenizeSpanish(normalized);
                
                // 3. Look up or generate phonemes
                var phonemes = new List<string>();

                foreach (var word in words)
                {
                    if (string.IsNullOrWhiteSpace(word))
                    {
                        // Add pause for punctuation/spaces
                        phonemes.Add("_");
                        continue;
                    }

                    string[] wordPhonemes = null;
                    
                    // Try dictionary lookup first
                    lock (dictLock)
                    {
                        if (spanishDict.TryGetValue(word.ToUpper(), out var dictPhonemes))
                        {
                            wordPhonemes = dictPhonemes;
                        }
                    }
                    
                    // Fall back to G2P if not in dictionary
                    if (wordPhonemes == null)
                    {
                        wordPhonemes = g2pEngine.Grapheme2Phoneme(word);
                    }

                    // Add phonemes
                    foreach (var phoneme in wordPhonemes)
                    {
                        phonemes.Add(phoneme);
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
                Debug.LogError($"Error in Spanish phonemization: {ex.Message}");
                throw;
            }
        }

        private async Task LoadDictionaryAsync(string path, CancellationToken cancellationToken)
        {
            var lines = await File.ReadAllLinesAsync(path, cancellationToken);
            
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
                        spanishDict[word] = phonemes;
                    }
                }
            }
        }

        private string GetDictionaryPath(string customPath)
        {
            if (!string.IsNullOrEmpty(customPath))
            {
                return Path.Combine(customPath, "spanish_dict.txt");
            }

            // Default path in StreamingAssets
            return Path.Combine(Application.streamingAssetsPath, 
                "uPiper", "Languages", "Spanish", "spanish_dict_sample.txt");
        }

        private List<string> TokenizeSpanish(string text)
        {
            // Spanish-aware tokenization
            // Preserve contractions like "del" (de + el), "al" (a + el)
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
                    
                    // Add punctuation as separate tokens
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

        /// <inheritdoc/>
        public override long GetMemoryUsage()
        {
            long size = 0;
            
            if (spanishDict != null)
            {
                // Estimate dictionary memory usage
                size += spanishDict.Count * 80; // Rough estimate per entry
            }
            
            return size;
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        protected override void DisposeInternal()
        {
            spanishDict?.Clear();
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