using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using uPiper.Core.Phonemizers.Backend.RuleBased;
using uPiper.Core.Phonemizers.Native;
using Debug = UnityEngine.Debug;

namespace uPiper.Core.Phonemizers.Backend
{
    /// <summary>
    /// Flite-based phonemizer backend for high-quality English phonemization.
    /// Uses Flite's Letter-to-Sound (LTS) system combined with CMU dictionary.
    /// </summary>
    public class FlitePhonemizerBackend : PhonemizerBackendBase
    {
        private IntPtr fliteContext;
        private CMUDictionary cmuDictionary;
        private readonly object lockObject = new();
        private Dictionary<string, string[]> ltsCache;
        private const int MaxCacheSize = 10000;

        /// <inheritdoc/>
        public override string Name => "Flite";

        /// <inheritdoc/>
        public override string Version => FliteNative.GetVersion();

        /// <inheritdoc/>
        public override string License => "BSD-style (CMU)";

        /// <inheritdoc/>
        public override string[] SupportedLanguages => new[] { "en", "en-US", "en-GB" };

        /// <inheritdoc/>
        protected override async Task<bool> InitializeInternalAsync(
            PhonemizerBackendOptions options,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Check if Flite native library is available
                    if (!FliteNative.IsAvailable())
                    {
                        Debug.LogError("Flite native library not available. Please build the native library.");
                        return false;
                    }

                    // Initialize Flite
                    Debug.Log($"Initializing Flite {Version}...");
                    fliteContext = FliteNative.flite_unity_init();
                    if (fliteContext == IntPtr.Zero)
                    {
                        Debug.LogError("Failed to initialize Flite context");
                        return false;
                    }

                    // Initialize CMU dictionary for fast lookups
                    cmuDictionary = new CMUDictionary();
                    var dictPath = options?.DataPath ?? GetDefaultDictionaryPath();

                    // Load dictionary asynchronously
                    var dictTask = cmuDictionary.LoadAsync(dictPath, cancellationToken);
                    dictTask.Wait(cancellationToken);

                    // Initialize LTS cache
                    ltsCache = new Dictionary<string, string[]>(MaxCacheSize);

                    // Set high priority since this is specialized for English
                    Priority = 150;

                    Debug.Log($"Flite initialized successfully with {cmuDictionary.WordCount} dictionary words");
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to initialize FlitePhonemizerBackend: {ex.Message}");
                    return false;
                }
            }, cancellationToken);
        }

        /// <inheritdoc/>
        public override async Task<PhonemeResult> PhonemizeAsync(
            string text,
            string language,
            PhonemeOptions options = null,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                EnsureInitialized();

                if (!ValidateInput(text, language, out var error))
                {
                    return CreateErrorResult(error, language);
                }

                options ??= PhonemeOptions.Default;

                // Process text
                var result = await Task.Run(() => ProcessText(text, language, options, cancellationToken), cancellationToken);

                stopwatch.Stop();
                result.ProcessingTimeMs = (float)stopwatch.ElapsedMilliseconds;
                result.ProcessingTime = stopwatch.Elapsed;

                return result;
            }
            catch (Exception ex)
            {
                return CreateErrorResult($"Phonemization failed: {ex.Message}", language);
            }
        }

        private PhonemeResult ProcessText(string text, string language, PhonemeOptions options, CancellationToken cancellationToken)
        {
            var phonemes = new List<string>();
            var stresses = new List<int>();
            var wordBoundaries = new List<int>();

            // Tokenize text into words
            var words = TokenizeText(text);

            foreach (var word in words)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (IsPunctuation(word))
                {
                    // Add silence for punctuation
                    phonemes.Add("_");
                    stresses.Add(0);
                    continue;
                }

                var cleanWord = CleanWord(word);
                if (string.IsNullOrEmpty(cleanWord))
                    continue;

                // Mark word boundary
                wordBoundaries.Add(phonemes.Count);

                // Try dictionary lookup first (fastest)
                if (cmuDictionary != null && cmuDictionary.TryGetPronunciation(cleanWord, out var dictPhonemes))
                {
                    AddPhonemes(phonemes, stresses, dictPhonemes, options);
                }
                else
                {
                    // Use Flite LTS for out-of-vocabulary words
                    var ltsPhonemes = GetFliteLTSPhonemes(cleanWord);
                    if (ltsPhonemes != null && ltsPhonemes.Length > 0)
                    {
                        AddPhonemes(phonemes, stresses, ltsPhonemes, options);
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to phonemize word: {cleanWord}");
                    }
                }
            }

            return new PhonemeResult
            {
                OriginalText = text,
                Phonemes = phonemes.ToArray(),
                PhonemeIds = ConvertToPhonemeIds(phonemes),
                Language = language,
                Success = true,
                Backend = Name,
                Stresses = options.IncludeStress ? stresses.ToArray() : null,
                WordBoundaries = wordBoundaries.ToArray()
            };
        }

        private string[] GetFliteLTSPhonemes(string word)
        {
            // Check cache first
            lock (lockObject)
            {
                if (ltsCache.TryGetValue(word, out var cached))
                {
                    return cached;
                }
            }

            // Apply Flite LTS
            string[] phonemes = null;
            lock (lockObject)
            {
                if (fliteContext == IntPtr.Zero)
                    return null;

                var resultPtr = FliteNative.flite_unity_lts_apply(fliteContext, word);
                if (resultPtr != IntPtr.Zero)
                {
                    var phonemeString = FliteNative.GetAndFreeString(resultPtr);
                    if (!string.IsNullOrEmpty(phonemeString))
                    {
                        phonemes = phonemeString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    }
                }
            }

            // Cache result
            if (phonemes != null)
            {
                lock (lockObject)
                {
                    // Simple cache eviction - remove oldest when full
                    if (ltsCache.Count >= MaxCacheSize)
                    {
                        var firstKey = ltsCache.Keys.First();
                        ltsCache.Remove(firstKey);
                    }
                    ltsCache[word] = phonemes;
                }
            }

            return phonemes;
        }

        private void AddPhonemes(List<string> phonemes, List<int> stresses, string[] newPhonemes, PhonemeOptions options)
        {
            foreach (var phoneme in newPhonemes)
            {
                // Extract stress from ARPABET format (e.g., "AH0", "AH1", "AH2")
                var stress = 0;
                var basePhoneme = phoneme;

                if (phoneme.Length > 1 && char.IsDigit(phoneme[^1]))
                {
                    stress = int.Parse(phoneme[^1].ToString());
                    basePhoneme = phoneme[..^1];
                }

                // Convert format if needed
                var finalPhoneme = options.Format == PhonemeFormat.ARPABET
                    ? (options.IncludeStress ? phoneme : basePhoneme)
                    : ConvertToPiperFormat(basePhoneme);

                phonemes.Add(finalPhoneme);
                stresses.Add(stress);
            }
        }

        private string[] TokenizeText(string text)
        {
            // Tokenize preserving punctuation
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

        private string ConvertToPiperFormat(string arpabet)
        {
            // Convert ARPABET to Piper/IPA format
            return ArpabetToIpaMap.TryGetValue(arpabet, out var ipa) ? ipa : arpabet.ToLower();
        }

        private int[] ConvertToPhonemeIds(List<string> phonemes)
        {
            // Convert phonemes to IDs for model input
            var ids = new int[phonemes.Count];
            for (var i = 0; i < phonemes.Count; i++)
            {
                ids[i] = PhonemeToIdMap.TryGetValue(phonemes[i], out var id) ? id : 0;
            }
            return ids;
        }

        private string GetDefaultDictionaryPath()
        {
            return System.IO.Path.Combine(
                Application.streamingAssetsPath,
                "uPiper",
                "Phonemizers",
                "cmudict-0.7b.txt"
            );
        }

        /// <inheritdoc/>
        public override long GetMemoryUsage()
        {
            long total = 0;

            if (cmuDictionary != null)
                total += cmuDictionary.GetMemoryUsage();

            if (ltsCache != null)
            {
                // Estimate cache size
                foreach (var kvp in ltsCache)
                {
                    total += kvp.Key.Length * 2; // Unicode
                    total += kvp.Value.Sum(p => p.Length * 2);
                    total += 32; // Overhead
                }
            }

            return total;
        }

        /// <inheritdoc/>
        public override BackendCapabilities GetCapabilities()
        {
            return new BackendCapabilities
            {
                SupportsIPA = true,
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
            lock (lockObject)
            {
                if (fliteContext != IntPtr.Zero)
                {
                    FliteNative.flite_unity_cleanup(fliteContext);
                    fliteContext = IntPtr.Zero;
                }

                cmuDictionary?.Dispose();
                cmuDictionary = null;

                ltsCache?.Clear();
                ltsCache = null;
            }
        }

        // ARPABET to IPA mapping
        private static readonly Dictionary<string, string> ArpabetToIpaMap = new()
        {
            // Vowels
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

            // Consonants
            ["B"] = "b",
            ["CH"] = "tʃ",
            ["D"] = "d",
            ["DH"] = "ð",
            ["F"] = "f",
            ["G"] = "g",
            ["HH"] = "h",
            ["JH"] = "dʒ",
            ["K"] = "k",
            ["L"] = "l",
            ["M"] = "m",
            ["N"] = "n",
            ["NG"] = "ŋ",
            ["P"] = "p",
            ["R"] = "r",
            ["S"] = "s",
            ["SH"] = "ʃ",
            ["T"] = "t",
            ["TH"] = "θ",
            ["V"] = "v",
            ["W"] = "w",
            ["Y"] = "j",
            ["Z"] = "z",
            ["ZH"] = "ʒ"
        };

        // Phoneme to ID mapping (simplified - should match model vocabulary)
        private static readonly Dictionary<string, int> PhonemeToIdMap = new()
        {
            ["_"] = 0, // Silence
            ["ɑ"] = 1,
            ["æ"] = 2,
            ["ʌ"] = 3,
            ["ɔ"] = 4,
            ["aʊ"] = 5,
            ["aɪ"] = 6,
            ["ɛ"] = 7,
            ["ɝ"] = 8,
            ["eɪ"] = 9,
            ["ɪ"] = 10,
            ["i"] = 11,
            ["oʊ"] = 12,
            ["ɔɪ"] = 13,
            ["ʊ"] = 14,
            ["u"] = 15,
            ["b"] = 16,
            ["tʃ"] = 17,
            ["d"] = 18,
            ["ð"] = 19,
            ["f"] = 20,
            ["g"] = 21,
            ["h"] = 22,
            ["dʒ"] = 23,
            ["k"] = 24,
            ["l"] = 25,
            ["m"] = 26,
            ["n"] = 27,
            ["ŋ"] = 28,
            ["p"] = 29,
            ["r"] = 30,
            ["s"] = 31,
            ["ʃ"] = 32,
            ["t"] = 33,
            ["θ"] = 34,
            ["v"] = 35,
            ["w"] = 36,
            ["j"] = 37,
            ["z"] = 38,
            ["ʒ"] = 39
        };
    }
}