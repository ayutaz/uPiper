using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using uPiper.Phonemizers.Backend;
using uPiper.Phonemizers.Backend.RuleBased;
using uPiper.Phonemizers.Backend.Flite;
using uPiper.Phonemizers.Configuration;
using uPiper.Phonemizers.Threading;

namespace uPiper.Phonemizers.Multilingual
{
    /// <summary>
    /// Multilingual phonemizer service with fallback support
    /// </summary>
    public class MultilingualPhonemizerService : IMultilingualPhonemizerService
    {
        private readonly Dictionary<string, List<IPhonemizerBackend>> backendsByLanguage;
        private readonly Dictionary<string, LanguageCapabilities> languageCapabilities;
        private readonly Dictionary<string, string[]> fallbackChains;
        private readonly LanguageDetector languageDetector;
        private readonly BackendFactory backendFactory;
        private readonly object syncLock = new object();

        // Language groups for intelligent fallback
        private readonly Dictionary<string, LanguageGroup> languageGroups;

        public MultilingualPhonemizerService(PhonemizerSettings settings = null)
        {
            backendsByLanguage = new Dictionary<string, List<IPhonemizerBackend>>();
            languageCapabilities = new Dictionary<string, LanguageCapabilities>();
            fallbackChains = new Dictionary<string, string[]>();
            languageDetector = new LanguageDetector();
            backendFactory = new BackendFactory();
            languageGroups = new Dictionary<string, LanguageGroup>();

            InitializeLanguageGroups();
            InitializeBackends(settings);
            BuildLanguageCapabilities();
        }

        /// <summary>
        /// Initialize language groups for fallback
        /// </summary>
        private void InitializeLanguageGroups()
        {
            // Germanic languages
            var germanic = new LanguageGroup
            {
                GroupName = "Germanic",
                Languages = new List<string> { "en-US", "en-GB", "en-IN", "de-DE", "nl-NL" },
                CommonScript = "Latin",
                SharePhonemeSet = true
            };
            germanic.SimilarityScores[("en-US", "en-GB")] = 0.95f;
            germanic.SimilarityScores[("en-US", "en-IN")] = 0.90f;
            germanic.SimilarityScores[("en-US", "de-DE")] = 0.60f;
            germanic.SimilarityScores[("de-DE", "nl-NL")] = 0.70f;
            languageGroups["Germanic"] = germanic;

            // Romance languages
            var romance = new LanguageGroup
            {
                GroupName = "Romance",
                Languages = new List<string> { "es-ES", "fr-FR", "it-IT", "pt-BR", "pt-PT" },
                CommonScript = "Latin",
                SharePhonemeSet = true
            };
            romance.SimilarityScores[("es-ES", "pt-BR")] = 0.85f;
            romance.SimilarityScores[("pt-BR", "pt-PT")] = 0.95f;
            romance.SimilarityScores[("es-ES", "it-IT")] = 0.75f;
            romance.SimilarityScores[("fr-FR", "it-IT")] = 0.70f;
            languageGroups["Romance"] = romance;

            // East Asian languages
            var eastAsian = new LanguageGroup
            {
                GroupName = "EastAsian",
                Languages = new List<string> { "ja-JP", "zh-CN", "zh-TW", "ko-KR" },
                CommonScript = "Mixed",
                SharePhonemeSet = false
            };
            eastAsian.SimilarityScores[("zh-CN", "zh-TW")] = 0.90f;
            eastAsian.SimilarityScores[("ja-JP", "zh-CN")] = 0.30f;
            eastAsian.SimilarityScores[("ko-KR", "ja-JP")] = 0.25f;
            languageGroups["EastAsian"] = eastAsian;
        }

        /// <summary>
        /// Initialize phonemizer backends
        /// </summary>
        private void InitializeBackends(PhonemizerSettings settings)
        {
            // Create Flite backend for English variants
            var fliteBackend = new FlitePhonemizerBackend();
            foreach (var lang in fliteBackend.SupportedLanguages)
            {
                AddBackend(lang, fliteBackend);
            }

            // Create rule-based backend for broader support
            var ruleBasedBackend = new RuleBasedPhonemizer();
            foreach (var lang in ruleBasedBackend.SupportedLanguages)
            {
                AddBackend(lang, ruleBasedBackend);
            }

            // Set up default fallback chains
            SetLanguageFallbackChain("en-IN", "en-GB", "en-US");
            SetLanguageFallbackChain("en-GB", "en-US");
            SetLanguageFallbackChain("es-MX", "es-ES");
            SetLanguageFallbackChain("pt-BR", "pt-PT", "es-ES");
            SetLanguageFallbackChain("zh-TW", "zh-CN");
            SetLanguageFallbackChain("fr-CA", "fr-FR");
        }

        /// <summary>
        /// Add a backend for a language
        /// </summary>
        private void AddBackend(string language, IPhonemizerBackend backend)
        {
            lock (syncLock)
            {
                if (!backendsByLanguage.ContainsKey(language))
                {
                    backendsByLanguage[language] = new List<IPhonemizerBackend>();
                }
                
                if (!backendsByLanguage[language].Contains(backend))
                {
                    backendsByLanguage[language].Add(backend);
                }
            }
        }

        /// <summary>
        /// Build language capabilities information
        /// </summary>
        private void BuildLanguageCapabilities()
        {
            lock (syncLock)
            {
                foreach (var (language, backends) in backendsByLanguage)
                {
                    var capabilities = new LanguageCapabilities
                    {
                        LanguageCode = language,
                        DisplayName = GetLanguageDisplayName(language),
                        NativeName = GetLanguageNativeName(language),
                        AvailableBackends = backends.Select(b => b.Name).ToList(),
                        PreferredBackend = DeterminePreferredBackend(language, backends),
                        SupportsStress = backends.Any(b => b.SupportsStress),
                        SupportsTone = IsTonalLanguage(language),
                        SupportsG2P = backends.Any(b => b.SupportsG2P),
                        RequiresNormalization = RequiresTextNormalization(language),
                        Script = GetLanguageScript(language),
                        OverallQuality = CalculateOverallQuality(language, backends)
                    };

                    languageCapabilities[language] = capabilities;
                }
            }
        }

        public IReadOnlyDictionary<string, LanguageCapabilities> GetSupportedLanguages()
        {
            lock (syncLock)
            {
                return new Dictionary<string, LanguageCapabilities>(languageCapabilities);
            }
        }

        public bool IsLanguageSupported(string languageCode)
        {
            lock (syncLock)
            {
                return backendsByLanguage.ContainsKey(languageCode) || 
                       fallbackChains.ContainsKey(languageCode);
            }
        }

        public IPhonemizerBackend GetBackendForLanguage(string languageCode)
        {
            lock (syncLock)
            {
                // Try direct match
                if (backendsByLanguage.TryGetValue(languageCode, out var backends))
                {
                    return SelectBestBackend(languageCode, backends);
                }

                // Try fallback chain
                if (fallbackChains.TryGetValue(languageCode, out var fallbackLanguages))
                {
                    foreach (var fallbackLang in fallbackLanguages)
                    {
                        if (backendsByLanguage.TryGetValue(fallbackLang, out backends))
                        {
                            return SelectBestBackend(fallbackLang, backends);
                        }
                    }
                }

                // Try language group fallback
                var groupFallback = FindGroupFallback(languageCode);
                if (groupFallback != null)
                {
                    return groupFallback;
                }

                return null;
            }
        }

        public async Task<MultilingualPhonemeResult> PhonemizeAutoDetectAsync(
            string text, 
            CancellationToken cancellationToken = default)
        {
            // Detect language
            var detectionResult = languageDetector.DetectLanguage(text);
            
            if (!detectionResult.IsReliable)
            {
                Debug.LogWarning($"Language detection unreliable (confidence: {detectionResult.Confidence})");
            }

            var language = detectionResult.DetectedLanguage;
            var backend = GetBackendForLanguage(language);
            
            if (backend == null)
            {
                // Use default fallback
                language = "en-US";
                backend = GetBackendForLanguage(language);
                
                if (backend == null)
                {
                    throw new NotSupportedException("No phonemizer backend available");
                }
            }

            try
            {
                var result = await backend.PhonemizeAsync(text, language, null, cancellationToken);
                
                return new MultilingualPhonemeResult
                {
                    Phonemes = result.Phonemes,
                    Durations = result.Durations,
                    Stresses = result.Stresses,
                    WordBoundaries = result.WordBoundaries,
                    Metadata = result.Metadata,
                    DetectedLanguage = detectionResult.DetectedLanguage,
                    LanguageConfidence = detectionResult.Confidence,
                    LanguageScores = detectionResult.LanguageScores,
                    UsedBackend = backend.Name,
                    UsedFallback = language != detectionResult.DetectedLanguage,
                    FallbackReason = language != detectionResult.DetectedLanguage ? 
                        "Original language not supported" : null
                };
            }
            catch (Exception ex)
            {
                Debug.LogError($"Phonemization failed for {language}: {ex.Message}");
                
                // Try fallback
                var fallbackResult = await TryFallbackPhonemization(text, language, cancellationToken);
                if (fallbackResult != null)
                {
                    fallbackResult.DetectedLanguage = detectionResult.DetectedLanguage;
                    fallbackResult.LanguageConfidence = detectionResult.Confidence;
                    fallbackResult.LanguageScores = detectionResult.LanguageScores;
                    return fallbackResult;
                }
                
                throw;
            }
        }

        public async Task<Dictionary<string, PhonemeResult>> PhonemizeMultilingualAsync(
            Dictionary<string, string> textByLanguage,
            CancellationToken cancellationToken = default)
        {
            var results = new Dictionary<string, PhonemeResult>();
            var tasks = new List<Task<(string language, PhonemeResult result)>>();

            foreach (var (language, text) in textByLanguage)
            {
                tasks.Add(PhonemizeSingleLanguageAsync(language, text, cancellationToken));
            }

            var completedTasks = await Task.WhenAll(tasks);
            
            foreach (var (language, result) in completedTasks)
            {
                results[language] = result;
            }

            return results;
        }

        private async Task<(string language, PhonemeResult result)> PhonemizeSingleLanguageAsync(
            string language, 
            string text, 
            CancellationToken cancellationToken)
        {
            var backend = GetBackendForLanguage(language);
            if (backend == null)
            {
                throw new NotSupportedException($"Language {language} is not supported");
            }

            var result = await backend.PhonemizeAsync(text, language, null, cancellationToken);
            return (language, result);
        }

        public void SetLanguageFallbackChain(string languageCode, params string[] fallbackLanguages)
        {
            lock (syncLock)
            {
                fallbackChains[languageCode] = fallbackLanguages;
            }
        }

        public float GetQualityScore(string languageCode, string backendName)
        {
            lock (syncLock)
            {
                if (backendsByLanguage.TryGetValue(languageCode, out var backends))
                {
                    var backend = backends.FirstOrDefault(b => b.Name == backendName);
                    if (backend != null)
                    {
                        return CalculateBackendQuality(languageCode, backend);
                    }
                }
                return 0f;
            }
        }

        /// <summary>
        /// Select the best backend for a language
        /// </summary>
        private IPhonemizerBackend SelectBestBackend(string language, List<IPhonemizerBackend> backends)
        {
            if (backends.Count == 1)
                return backends[0];

            // Score each backend
            var scores = new Dictionary<IPhonemizerBackend, float>();
            foreach (var backend in backends)
            {
                scores[backend] = CalculateBackendQuality(language, backend);
            }

            // Return highest scoring backend
            return scores.OrderByDescending(kvp => kvp.Value).First().Key;
        }

        /// <summary>
        /// Calculate quality score for a backend/language combination
        /// </summary>
        private float CalculateBackendQuality(string language, IPhonemizerBackend backend)
        {
            float score = 0.5f; // Base score

            // Native support is best
            if (backend.SupportedLanguages.Contains(language))
                score += 0.3f;

            // Prefer specialized backends
            if (backend.Name == "Flite" && language.StartsWith("en-"))
                score += 0.2f;

            // G2P support is valuable
            if (backend.SupportsG2P)
                score += 0.1f;

            // Stress support for English
            if (backend.SupportsStress && language.StartsWith("en-"))
                score += 0.1f;

            return Math.Min(1f, score);
        }

        /// <summary>
        /// Try fallback phonemization
        /// </summary>
        private async Task<MultilingualPhonemeResult> TryFallbackPhonemization(
            string text, 
            string originalLanguage, 
            CancellationToken cancellationToken)
        {
            // Try fallback chain
            if (fallbackChains.TryGetValue(originalLanguage, out var fallbackLanguages))
            {
                foreach (var fallbackLang in fallbackLanguages)
                {
                    var backend = GetBackendForLanguage(fallbackLang);
                    if (backend != null)
                    {
                        try
                        {
                            var result = await backend.PhonemizeAsync(text, fallbackLang, null, cancellationToken);
                            return new MultilingualPhonemeResult
                            {
                                Phonemes = result.Phonemes,
                                Durations = result.Durations,
                                Stresses = result.Stresses,
                                WordBoundaries = result.WordBoundaries,
                                Metadata = result.Metadata,
                                UsedBackend = backend.Name,
                                UsedFallback = true,
                                FallbackReason = $"Fallback from {originalLanguage} to {fallbackLang}"
                            };
                        }
                        catch
                        {
                            // Continue to next fallback
                        }
                    }
                }
            }

            // Try group fallback
            var groupBackend = FindGroupFallback(originalLanguage);
            if (groupBackend != null)
            {
                var fallbackLang = groupBackend.SupportedLanguages.First();
                try
                {
                    var result = await groupBackend.PhonemizeAsync(text, fallbackLang, null, cancellationToken);
                    return new MultilingualPhonemeResult
                    {
                        Phonemes = result.Phonemes,
                        Durations = result.Durations,
                        Stresses = result.Stresses,
                        WordBoundaries = result.WordBoundaries,
                        Metadata = result.Metadata,
                        UsedBackend = groupBackend.Name,
                        UsedFallback = true,
                        FallbackReason = $"Group fallback from {originalLanguage} to {fallbackLang}"
                    };
                }
                catch
                {
                    // Fallback failed
                }
            }

            return null;
        }

        /// <summary>
        /// Find a fallback backend based on language group
        /// </summary>
        private IPhonemizerBackend FindGroupFallback(string language)
        {
            foreach (var group in languageGroups.Values)
            {
                if (group.Languages.Contains(language))
                {
                    // Find best available language in the group
                    var availableLanguages = group.Languages
                        .Where(lang => backendsByLanguage.ContainsKey(lang))
                        .OrderByDescending(lang => GetLanguageSimilarity(language, lang, group))
                        .ToList();

                    if (availableLanguages.Any())
                    {
                        return GetBackendForLanguage(availableLanguages.First());
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Get similarity score between two languages
        /// </summary>
        private float GetLanguageSimilarity(string lang1, string lang2, LanguageGroup group)
        {
            if (lang1 == lang2) return 1f;

            var key = (lang1, lang2);
            if (group.SimilarityScores.TryGetValue(key, out float score))
                return score;

            // Try reverse
            key = (lang2, lang1);
            if (group.SimilarityScores.TryGetValue(key, out score))
                return score;

            // Default similarity within group
            return 0.5f;
        }

        // Helper methods
        private string GetLanguageDisplayName(string languageCode)
        {
            return languageCode switch
            {
                "en-US" => "English (US)",
                "en-GB" => "English (UK)",
                "en-IN" => "English (India)",
                "ja-JP" => "Japanese",
                "zh-CN" => "Chinese (Simplified)",
                "zh-TW" => "Chinese (Traditional)",
                "ko-KR" => "Korean",
                "es-ES" => "Spanish (Spain)",
                "fr-FR" => "French (France)",
                "de-DE" => "German (Germany)",
                _ => languageCode
            };
        }

        private string GetLanguageNativeName(string languageCode)
        {
            return languageCode switch
            {
                "en-US" => "English",
                "en-GB" => "English",
                "en-IN" => "English",
                "ja-JP" => "日本語",
                "zh-CN" => "简体中文",
                "zh-TW" => "繁體中文",
                "ko-KR" => "한국어",
                "es-ES" => "Español",
                "fr-FR" => "Français",
                "de-DE" => "Deutsch",
                _ => GetLanguageDisplayName(languageCode)
            };
        }

        private string GetLanguageScript(string languageCode)
        {
            return languageCode switch
            {
                var l when l.StartsWith("en-") => "Latin",
                var l when l.StartsWith("es-") => "Latin",
                var l when l.StartsWith("fr-") => "Latin",
                var l when l.StartsWith("de-") => "Latin",
                "ja-JP" => "Japanese",
                var l when l.StartsWith("zh-") => "Chinese",
                "ko-KR" => "Hangul",
                "ar-SA" => "Arabic",
                "ru-RU" => "Cyrillic",
                "hi-IN" => "Devanagari",
                _ => "Latin"
            };
        }

        private bool IsTonalLanguage(string languageCode)
        {
            return languageCode.StartsWith("zh-") || languageCode == "vi-VN";
        }

        private bool RequiresTextNormalization(string languageCode)
        {
            return languageCode switch
            {
                "ar-SA" => true, // Arabic requires special handling
                "he-IL" => true, // Hebrew is RTL
                _ => false
            };
        }

        private string DeterminePreferredBackend(string language, List<IPhonemizerBackend> backends)
        {
            var scores = backends.ToDictionary(b => b, b => CalculateBackendQuality(language, b));
            return scores.OrderByDescending(kvp => kvp.Value).First().Key.Name;
        }

        private float CalculateOverallQuality(string language, List<IPhonemizerBackend> backends)
        {
            if (!backends.Any()) return 0f;
            
            var bestScore = backends.Max(b => CalculateBackendQuality(language, b));
            return bestScore;
        }
    }
}