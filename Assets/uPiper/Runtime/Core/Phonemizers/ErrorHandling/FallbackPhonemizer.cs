using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using uPiper.Core.Phonemizers.Backend;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace uPiper.Core.Phonemizers.ErrorHandling
{
    /// <summary>
    /// Simple fallback phonemizer that provides basic phoneme approximation.
    /// This ensures that TTS can always produce some output, even if not perfect.
    /// </summary>
    public class FallbackPhonemizer : PhonemizerBackendBase
    {
        private static readonly Dictionary<char, string> BasicLetterToPhoneme = new()
        {
            // Vowels
            ['a'] = "AH",
            ['e'] = "EH",
            ['i'] = "IH",
            ['o'] = "AA",
            ['u'] = "UH",
            ['A'] = "EY",
            ['E'] = "IY",
            ['I'] = "AY",
            ['O'] = "OW",
            ['U'] = "UW",

            // Consonants
            ['b'] = "B",
            ['c'] = "K",
            ['d'] = "D",
            ['f'] = "F",
            ['g'] = "G",
            ['h'] = "HH",
            ['j'] = "JH",
            ['k'] = "K",
            ['l'] = "L",
            ['m'] = "M",
            ['n'] = "N",
            ['p'] = "P",
            ['q'] = "K",
            ['r'] = "R",
            ['s'] = "S",
            ['t'] = "T",
            ['v'] = "V",
            ['w'] = "W",
            ['x'] = "K S",
            ['y'] = "Y",
            ['z'] = "Z",

            ['B'] = "B",
            ['C'] = "K",
            ['D'] = "D",
            ['F'] = "F",
            ['G'] = "G",
            ['H'] = "HH",
            ['J'] = "JH",
            ['K'] = "K",
            ['L'] = "L",
            ['M'] = "M",
            ['N'] = "N",
            ['P'] = "P",
            ['Q'] = "K",
            ['R'] = "R",
            ['S'] = "S",
            ['T'] = "T",
            ['V'] = "V",
            ['W'] = "W",
            ['X'] = "K S",
            ['Y'] = "Y",
            ['Z'] = "Z"
        };

        /// <inheritdoc/>
        public override string Name => "Fallback";

        /// <inheritdoc/>
        public override string Version => "1.0.0";

        /// <inheritdoc/>
        public override string License => "MIT";

        /// <inheritdoc/>
        public override string[] SupportedLanguages => new[] { "*" }; // Supports any language as fallback

        /// <inheritdoc/>
        protected override Task<bool> InitializeInternalAsync(
            PhonemizerBackendOptions options,
            CancellationToken cancellationToken)
        {
            // No initialization needed for simple fallback
            Priority = 1; // Lowest priority
            return Task.FromResult(true);
        }

        /// <inheritdoc/>
        public override Task<PhonemeResult> PhonemizeAsync(
            string text,
            string language,
            PhonemeOptions options = null,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                if (string.IsNullOrEmpty(text))
                {
                    return Task.FromResult(CreateErrorResult("Empty text", language));
                }

                var phonemes = new List<string>();
                var words = Regex.Split(text, @"\s+");

                foreach (var word in words)
                {
                    if (string.IsNullOrWhiteSpace(word))
                        continue;

                    // Add word boundary
                    if (phonemes.Count > 0)
                    {
                        phonemes.Add("_"); // Short pause between words
                    }

                    // Convert each character to phoneme
                    foreach (var c in word)
                    {
                        if (char.IsLetter(c))
                        {
                            if (BasicLetterToPhoneme.TryGetValue(c, out var phoneme))
                            {
                                // Split multi-phoneme entries
                                var parts = phoneme.Split(' ');
                                phonemes.AddRange(parts);
                            }
                            else
                            {
                                // Unknown character, skip
                                UnityEngine.Debug.LogWarning($"Fallback: Unknown character '{c}'");
                            }
                        }
                        else if (char.IsPunctuation(c))
                        {
                            // Add pause for punctuation
                            phonemes.Add("_");
                        }
                        // Skip other characters
                    }
                }

                // Apply basic stress pattern (stress every other vowel)
                ApplySimpleStress(phonemes);

                var result = new PhonemeResult
                {
                    Success = true,
                    OriginalText = text,
                    Language = language,
                    Backend = Name,
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                    ProcessingTime = stopwatch.Elapsed,
                    Phonemes = phonemes.ToArray(),
                    PhonemeIds = ConvertToPhonemeIds(phonemes),
                    Metadata = new Dictionary<string, object>
                    {
                        { "Type", "Fallback" },
                        { "Note", "Quality may be limited" }
                    }
                };

                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                return Task.FromResult(CreateErrorResult($"Fallback failed: {ex.Message}", language));
            }
        }

        /// <inheritdoc/>
        public override long GetMemoryUsage()
        {
            // Minimal memory usage
            return 1024; // 1KB
        }

        /// <inheritdoc/>
        public override BackendCapabilities GetCapabilities()
        {
            return new BackendCapabilities
            {
                SupportsIPA = false,
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
            // Nothing to dispose
        }

        /// <inheritdoc/>
        public override bool SupportsLanguage(string language)
        {
            // Fallback supports any language (poorly)
            return true;
        }

        private void ApplySimpleStress(List<string> phonemes)
        {
            var vowelPhonemes = new HashSet<string>
            {
                "AH", "EH", "IH", "AA", "UH", "EY", "IY", "AY", "OW", "UW"
            };

            var firstVowel = true;
            for (var i = 0; i < phonemes.Count; i++)
            {
                if (vowelPhonemes.Contains(phonemes[i]))
                {
                    // Add stress to first vowel, no stress to others
                    phonemes[i] = phonemes[i] + (firstVowel ? "1" : "0");
                    firstVowel = false;
                }
            }
        }

        private int[] ConvertToPhonemeIds(List<string> phonemes)
        {
            // Simple mapping - in real implementation, this should match the model's vocabulary
            var ids = new List<int>();

            foreach (var phoneme in phonemes)
            {
                // Remove stress markers for ID mapping
                var basePhoneme = phoneme.TrimEnd('0', '1', '2');

                // Simple hash-based ID assignment
                var id = Math.Abs(basePhoneme.GetHashCode()) % 100 + 1;
                ids.Add(id);
            }

            return ids.ToArray();
        }
    }

    /// <summary>
    /// Factory method extensions for creating safe wrappers.
    /// </summary>
    public static class SafePhonemizerExtensions
    {
        /// <summary>
        /// Wraps a phonemizer backend with safety features.
        /// </summary>
        public static SafePhonemizerWrapper WithSafety(
            this IPhonemizerBackend backend,
            IPhonemizerBackend fallback = null,
            int failureThreshold = 3,
            TimeSpan? timeout = null)
        {
            var circuitBreaker = new CircuitBreaker(failureThreshold, timeout);
            return new SafePhonemizerWrapper(backend, fallback ?? new FallbackPhonemizer(), circuitBreaker);
        }
    }
}