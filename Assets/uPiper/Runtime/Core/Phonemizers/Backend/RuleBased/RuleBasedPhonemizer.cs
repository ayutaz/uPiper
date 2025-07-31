using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace uPiper.Core.Phonemizers.Backend.RuleBased
{
    /// <summary>
    /// Rule-based phonemizer implementation using CMU dictionary and G2P rules.
    /// This is a MIT-licensed implementation without any GPL dependencies.
    /// </summary>
    public class RuleBasedPhonemizer : PhonemizerBackendBase
    {
        private CMUDictionary cmuDictionary;
        private G2PEngine g2pEngine;
        private TextNormalizer textNormalizer;

        /// <inheritdoc/>
        public override string Name => "RuleBased";

        /// <inheritdoc/>
        public override string Version => "1.0.0";

        /// <inheritdoc/>
        public override string License => "MIT";

        /// <inheritdoc/>
        public override string[] SupportedLanguages => new[] { "en", "en-US", "en-GB" };

        /// <inheritdoc/>
        protected override async Task<bool> InitializeInternalAsync(
            PhonemizerBackendOptions options,
            CancellationToken cancellationToken)
        {
            try
            {
                // Initialize components
                textNormalizer = new TextNormalizer();
                cmuDictionary = new CMUDictionary();
                g2pEngine = new G2PEngine();

                // Load dictionary
                var dictPath = options?.DataPath ?? GetDefaultDictionaryPath();
                await cmuDictionary.LoadAsync(dictPath, cancellationToken);

                // Initialize G2P rules
                g2pEngine.Initialize();

                Priority = 100; // High priority as default backend

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize RuleBasedPhonemizer: {ex.Message}");
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
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                EnsureInitialized();

                if (!ValidateInput(text, language, out var error))
                {
                    return CreateErrorResult(error, language);
                }

                options ??= PhonemeOptions.Default;

                // Normalize text
                var normalizedText = options.NormalizeText
                    ? textNormalizer.Normalize(text)
                    : text;

                // Tokenize into words
                var words = TokenizeText(normalizedText);
                var phonemeTokens = new List<PhonemeToken>();

                foreach (var word in words)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (IsPunctuation(word))
                    {
                        // Handle punctuation as silence markers
                        phonemeTokens.Add(CreateSilenceToken(word));
                        continue;
                    }

                    // Try dictionary lookup first
                    var cleanWord = CleanWord(word);
                    if (cmuDictionary.TryGetPronunciation(cleanWord, out var pronunciation))
                    {
                        phonemeTokens.AddRange(ConvertToPhonemeTokens(pronunciation, options));
                    }
                    else if (options.UseG2PFallback)
                    {
                        // Use G2P for out-of-vocabulary words
                        var g2pPhonemes = await g2pEngine.PredictAsync(cleanWord, cancellationToken);
                        phonemeTokens.AddRange(ConvertToPhonemeTokens(g2pPhonemes, options));
                    }
                    else
                    {
                        // Skip unknown words
                        Debug.LogWarning($"Unknown word: {cleanWord}");
                    }
                }

                // Convert to result format
                var result = new PhonemeResult
                {
                    Success = true,
                    OriginalText = text,
                    Language = language,
                    Backend = Name,
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                    ProcessingTime = stopwatch.Elapsed
                };

                // Convert phoneme tokens to arrays
                ConvertTokensToArrays(phonemeTokens, result, options);

                return result;
            }
            catch (Exception ex)
            {
                return CreateErrorResult($"Phonemization failed: {ex.Message}", language);
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        /// <inheritdoc/>
        public override long GetMemoryUsage()
        {
            long total = 0;

            if (cmuDictionary != null)
                total += cmuDictionary.GetMemoryUsage();

            if (g2pEngine != null)
                total += g2pEngine.GetMemoryUsage();

            return total;
        }

        /// <inheritdoc/>
        public override BackendCapabilities GetCapabilities()
        {
            return new BackendCapabilities
            {
                SupportsIPA = false, // Uses ARPABET
                SupportsStress = true,
                SupportsSyllables = false,
                SupportsTones = false,
                SupportsDuration = false,
                SupportsBatchProcessing = true,
                IsThreadSafe = true,
                RequiresNetwork = false
            };
        }

        /// <inheritdoc/>
        protected override void DisposeInternal()
        {
            cmuDictionary?.Dispose();
            g2pEngine?.Dispose();
            textNormalizer = null;
        }

        private string[] TokenizeText(string text)
        {
            // Simple word tokenization with punctuation preservation
            var pattern = @"(\w+|[^\w\s])";
            var matches = Regex.Matches(text, pattern);
            return matches.Cast<Match>().Select(m => m.Value).ToArray();
        }

        private bool IsPunctuation(string token)
        {
            return token.Length == 1 && char.IsPunctuation(token[0]);
        }

        private string CleanWord(string word)
        {
            // Remove non-alphabetic characters and convert to uppercase
            return Regex.Replace(word, @"[^a-zA-Z]", "").ToUpper();
        }

        private PhonemeToken CreateSilenceToken(string punctuation)
        {
            // Map punctuation to silence duration
            var duration = punctuation switch
            {
                "." => 0.3f,
                "," => 0.15f,
                "?" => 0.3f,
                "!" => 0.3f,
                ";" => 0.2f,
                ":" => 0.2f,
                _ => 0.1f
            };

            return new PhonemeToken
            {
                Phoneme = "_", // Silence marker
                Duration = duration,
                IsSilence = true
            };
        }

        private List<PhonemeToken> ConvertToPhonemeTokens(string[] arpabetPhonemes, PhonemeOptions options)
        {
            var tokens = new List<PhonemeToken>();

            foreach (var phoneme in arpabetPhonemes)
            {
                var token = new PhonemeToken { Phoneme = phoneme };

                // Extract stress level from ARPABET (e.g., "AH0", "AH1", "AH2")
                if (char.IsDigit(phoneme[^1]))
                {
                    token.StressLevel = int.Parse(phoneme[^1].ToString());
                    token.Phoneme = phoneme[..^1];
                }

                tokens.Add(token);
            }

            return tokens;
        }

        private void ConvertTokensToArrays(List<PhonemeToken> tokens, PhonemeResult result, PhonemeOptions options)
        {
            var phonemes = new List<string>();
            var phonemeIds = new List<int>();
            var durations = new List<float>();

            foreach (var token in tokens)
            {
                if (token.IsSilence)
                {
                    phonemes.Add("_");
                    phonemeIds.Add(0); // Silence ID
                }
                else
                {
                    // Convert based on format preference
                    var phoneme = options.Format == PhonemeFormat.ARPABET
                        ? token.Phoneme + (options.IncludeStress ? token.StressLevel.ToString() : "")
                        : ConvertToPiperFormat(token.Phoneme);

                    phonemes.Add(phoneme);
                    phonemeIds.Add(GetPhonemeId(phoneme));
                }

                if (token.Duration.HasValue)
                {
                    durations.Add(token.Duration.Value);
                }
            }

            result.Phonemes = phonemes.ToArray();
            result.PhonemeIds = phonemeIds.ToArray();
            if (durations.Count > 0)
            {
                result.Durations = durations.ToArray();
            }
        }

        private string ConvertToPiperFormat(string arpabet)
        {
            // Convert ARPABET to Piper-compatible format
            // This is a simplified mapping - can be extended
            return ArpabetToPiperMap.TryGetValue(arpabet, out var piper) ? piper : arpabet.ToLower();
        }

        private int GetPhonemeId(string phoneme)
        {
            // Map phoneme to ID for model input
            // This should match the model's phoneme vocabulary
            return PhonemeToIdMap.TryGetValue(phoneme, out var id) ? id : 0;
        }

        private string GetDefaultDictionaryPath()
        {
            // Path to CMU dictionary in StreamingAssets
            // Using full CMU dictionary (134,000+ words)
            return System.IO.Path.Combine(Application.streamingAssetsPath, "uPiper", "Phonemizers", "cmudict-0.7b.txt");
        }

        // Mapping tables (simplified - should be loaded from config)
        private static readonly Dictionary<string, string> ArpabetToPiperMap = new()
        {
            ["AA"] = "ɑ",
            ["AE"] = "æ",
            ["AH"] = "ʌ",
            ["AO"] = "ɔ",
            ["AW"] = "aʊ",
            ["AY"] = "aɪ",
            ["EH"] = "ɛ",
            ["ER"] = "ɝ",
            ["EY"] = "eɪ",
            ["IH"] = "ɪ",
            ["IY"] = "i",
            ["OW"] = "oʊ",
            ["OY"] = "ɔɪ",
            ["UH"] = "ʊ",
            ["UW"] = "u",
            // Add more mappings as needed
        };

        private static readonly Dictionary<string, int> PhonemeToIdMap = new()
        {
            ["_"] = 0,
            ["ɑ"] = 1,
            ["æ"] = 2,
            ["ʌ"] = 3,
            ["ɔ"] = 4,
            // Add complete mapping based on model vocabulary
        };
    }

    /// <summary>
    /// Internal class to represent phoneme tokens with metadata.
    /// </summary>
    internal class PhonemeToken
    {
        public string Phoneme { get; set; }
        public int StressLevel { get; set; }
        public float? Duration { get; set; }
        public bool IsSilence { get; set; }
    }
}