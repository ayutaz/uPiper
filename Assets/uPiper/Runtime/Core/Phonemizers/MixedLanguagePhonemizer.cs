using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using uPiper.Core.Phonemizers.Backend;
using Debug = UnityEngine.Debug;

namespace uPiper.Core.Phonemizers
{
    /// <summary>
    /// Phonemizer that handles mixed Japanese-English text.
    /// Automatically detects language boundaries and uses appropriate backends.
    /// </summary>
    public class MixedLanguagePhonemizer : IPhonemizerBackend
    {
        private readonly LanguageDetector languageDetector;
        private readonly Dictionary<string, IPhonemizerBackend> backends;
        private bool isInitialized;
        private readonly object lockObject = new();

        public string Name => "MixedLanguage";
        public string Version => "1.0.0";
        public string License => "MIT";
        public string[] SupportedLanguages => new[] { "ja", "en", "mixed", "auto" };
        public int Priority => 100;
        public bool IsAvailable => isInitialized;
        public bool IsInitialized => isInitialized;

        public MixedLanguagePhonemizer()
        {
            languageDetector = new LanguageDetector();
            backends = new Dictionary<string, IPhonemizerBackend>();
        }

        public async Task<bool> InitializeAsync(PhonemizerBackendOptions options = null, CancellationToken cancellationToken = default)
        {
            try
            {
                // Initialize Japanese backend (OpenJTalk)
                var jaBackend = CreateOpenJTalkBackend();
                if (jaBackend == null)
                {
                    Debug.LogError("Failed to create Japanese backend");
                    return false;
                }
                var jaResult = await jaBackend.InitializeAsync(options, cancellationToken);
                if (!jaResult)
                {
                    Debug.LogError("Failed to initialize Japanese phonemizer");
                    return false;
                }
                backends["ja"] = jaBackend;

                // Initialize English backend (SimpleLTS as primary, RuleBased as fallback)
                IPhonemizerBackend enBackend = null;

                // Try SimpleLTS first
                var simpleLts = CreateSimpleLTSBackend();
                if (simpleLts != null && await simpleLts.InitializeAsync(options, cancellationToken))
                {
                    enBackend = simpleLts;
                    Debug.Log("Using SimpleLTS for English phonemization");
                }
                else
                {
                    // Fallback to RuleBased
                    var ruleBased = new Backend.RuleBased.RuleBasedPhonemizer();
                    if (await ruleBased.InitializeAsync(options, cancellationToken))
                    {
                        enBackend = ruleBased;
                        Debug.Log("Using RuleBased for English phonemization");
                    }
                }

                if (enBackend == null)
                {
                    Debug.LogError("Failed to initialize any English phonemizer");
                    return false;
                }

                backends["en"] = enBackend;
                isInitialized = true;

                Debug.Log($"MixedLanguagePhonemizer initialized with {backends.Count} backends");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize MixedLanguagePhonemizer: {ex.Message}");
                return false;
            }
        }

        public async Task<PhonemeResult> PhonemizeAsync(
            string text,
            string language = "auto",
            PhonemeOptions options = null,
            CancellationToken cancellationToken = default)
        {
            if (!isInitialized)
                throw new InvalidOperationException("Phonemizer not initialized");

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Handle simple cases
                if (string.IsNullOrEmpty(text))
                {
                    return new PhonemeResult
                    {
                        Success = true,
                        OriginalText = text,
                        Phonemes = Array.Empty<string>(),
                        PhonemeIds = Array.Empty<int>(),
                        Language = language
                    };
                }

                // If specific language is requested and not mixed, use that backend directly
                if (language != "auto" && language != "mixed" && backends.ContainsKey(language))
                {
                    return await backends[language].PhonemizeAsync(text, language, options, cancellationToken);
                }

                // Detect language segments
                var segments = languageDetector.DetectSegments(text);
                if (segments.Count == 0)
                {
                    // Fallback to English if no segments detected
                    return await backends["en"].PhonemizeAsync(text, "en", options, cancellationToken);
                }

                // Process each segment
                var allPhonemes = new List<string>();
                var allPhonemeIds = new List<int>();
                var allDurations = new List<float>();
                var wordBoundaries = new List<int>();
                var errors = new List<string>();

                foreach (var segment in segments)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Skip pure whitespace
                    if (string.IsNullOrWhiteSpace(segment.Text))
                        continue;

                    // Handle punctuation
                    if (segment.IsPunctuation)
                    {
                        HandlePunctuation(segment.Text, allPhonemes, allPhonemeIds, allDurations);
                        continue;
                    }

                    // Get appropriate backend
                    var backendLang = segment.Language;
                    if (backendLang == "neutral" || !backends.ContainsKey(backendLang))
                    {
                        backendLang = languageDetector.DetectPrimaryLanguage(text);
                        if (!backends.ContainsKey(backendLang))
                            backendLang = "en"; // Default to English
                    }

                    var backend = backends[backendLang];

                    // Mark word boundary
                    wordBoundaries.Add(allPhonemes.Count);

                    try
                    {
                        // Phonemize segment
                        var segmentResult = await backend.PhonemizeAsync(
                            segment.Text,
                            backendLang,
                            options,
                            cancellationToken
                        );

                        if (segmentResult.Success && segmentResult.Phonemes != null)
                        {
                            allPhonemes.AddRange(segmentResult.Phonemes);

                            if (segmentResult.PhonemeIds != null)
                                allPhonemeIds.AddRange(segmentResult.PhonemeIds);

                            if (segmentResult.Durations != null)
                                allDurations.AddRange(segmentResult.Durations);
                        }
                        else
                        {
                            errors.Add($"Failed to phonemize '{segment.Text}' as {backendLang}: {segmentResult.Error}");
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Error processing '{segment.Text}': {ex.Message}");
                    }
                }

                stopwatch.Stop();

                // Build result
                var result = new PhonemeResult
                {
                    Success = errors.Count == 0,
                    OriginalText = text,
                    Phonemes = allPhonemes.ToArray(),
                    PhonemeIds = allPhonemeIds.Count > 0 ? allPhonemeIds.ToArray() : null,
                    Durations = allDurations.Count > 0 ? allDurations.ToArray() : null,
                    WordBoundaries = wordBoundaries.ToArray(),
                    Language = "mixed",
                    Backend = Name,
                    ProcessingTimeMs = (float)stopwatch.ElapsedMilliseconds,
                    ProcessingTime = stopwatch.Elapsed,
                    Error = errors.Count > 0 ? string.Join("; ", errors) : null,
                    Metadata = new Dictionary<string, object>
                    {
                        ["segments"] = segments.Count,
                        ["backends_used"] = GetBackendsUsed(segments)
                    }
                };

                return result;
            }
            catch (Exception ex)
            {
                return new PhonemeResult
                {
                    Success = false,
                    OriginalText = text,
                    Language = language,
                    Error = $"Phonemization failed: {ex.Message}",
                    ProcessingTimeMs = (float)stopwatch.ElapsedMilliseconds
                };
            }
        }

        private void HandlePunctuation(string punctuation, List<string> phonemes, List<int> phonemeIds, List<float> durations)
        {
            // Map punctuation to silence duration
            foreach (var ch in punctuation)
            {
                var duration = ch switch
                {
                    '。' or '.' => 0.3f,
                    '、' or ',' => 0.15f,
                    '？' or '?' => 0.3f,
                    '！' or '!' => 0.3f,
                    '；' or ';' => 0.2f,
                    '：' or ':' => 0.2f,
                    '）' or ')' or '」' or '』' => 0.1f,
                    '（' or '(' or '「' or '『' => 0.1f,
                    _ => 0.05f
                };

                if (duration > 0)
                {
                    phonemes.Add("_"); // Silence marker
                    phonemeIds.Add(0); // Silence ID
                    durations.Add(duration);
                }
            }
        }

        private string[] GetBackendsUsed(List<LanguageDetector.LanguageSegment> segments)
        {
            return segments
                .Where(s => !s.IsPunctuation && backends.ContainsKey(s.Language))
                .Select(s => backends[s.Language].Name)
                .Distinct()
                .ToArray();
        }

        public bool SupportsLanguage(string language)
        {
            return SupportedLanguages.Contains(language.ToLower());
        }

        public BackendCapabilities GetCapabilities()
        {
            return new BackendCapabilities
            {
                SupportsIPA = true,
                SupportsStress = true,
                SupportsSyllables = false,
                SupportsTones = false,
                SupportsDuration = true,
                SupportsBatchProcessing = true,
                IsThreadSafe = true,
                RequiresNetwork = false
            };
        }

        public PhonemeOptions GetDefaultOptions()
        {
            return new PhonemeOptions
            {
                Format = PhonemeFormat.IPA,
                IncludeStress = true,
                IncludeTones = false,
                NormalizeText = true,
                UseG2PFallback = true
            };
        }

        public long GetMemoryUsage()
        {
            long total = 0;
            foreach (var backend in backends.Values)
            {
                total += backend.GetMemoryUsage();
            }
            return total;
        }

        public void Dispose()
        {
            lock (lockObject)
            {
                foreach (var backend in backends.Values)
                {
                    backend?.Dispose();
                }
                backends.Clear();
                isInitialized = false;
            }
        }

        /// <summary>
        /// Creates an OpenJTalk backend using reflection to avoid compile-time dependency.
        /// </summary>
        private IPhonemizerBackend CreateOpenJTalkBackend()
        {
            try
            {
                var type = System.Type.GetType("uPiper.Core.Phonemizers.Backend.OpenJTalkBackendAdapter, uPiper.Runtime");
                if (type != null)
                {
                    return Activator.CreateInstance(type) as IPhonemizerBackend;
                }
                Debug.LogError("OpenJTalkBackendAdapter type not found");
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to create OpenJTalkBackendAdapter: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Creates a SimpleLTS backend using reflection to avoid compile-time dependency.
        /// </summary>
        private IPhonemizerBackend CreateSimpleLTSBackend()
        {
            try
            {
                var type = System.Type.GetType("uPiper.Core.Phonemizers.Backend.SimpleLTSPhonemizer, uPiper.Runtime");
                if (type != null)
                {
                    return Activator.CreateInstance(type) as IPhonemizerBackend;
                }
                Debug.LogError("SimpleLTSPhonemizer type not found");
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to create SimpleLTSPhonemizer: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets statistics about the text composition.
        /// </summary>
        public Dictionary<string, object> AnalyzeText(string text)
        {
            var segments = languageDetector.DetectSegments(text);
            var stats = new Dictionary<string, object>();

            var languageCounts = segments
                .Where(s => !s.IsPunctuation)
                .GroupBy(s => s.Language)
                .ToDictionary(g => g.Key, g => g.Count());

            var languageChars = segments
                .Where(s => !s.IsPunctuation)
                .GroupBy(s => s.Language)
                .ToDictionary(g => g.Key, g => g.Sum(s => s.Text.Length));

            stats["segment_count"] = segments.Count;
            stats["language_segments"] = languageCounts;
            stats["language_characters"] = languageChars;
            stats["primary_language"] = languageDetector.DetectPrimaryLanguage(text);

            return stats;
        }
    }
}