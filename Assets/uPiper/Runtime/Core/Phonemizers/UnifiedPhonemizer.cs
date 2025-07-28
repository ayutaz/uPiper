using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using uPiper.Core.Phonemizers.Backend;

namespace uPiper.Core.Phonemizers
{
    /// <summary>
    /// Unified phonemizer that automatically selects the best backend for the given text and language.
    /// This is the main entry point for phonemization in uPiper.
    /// </summary>
    public class UnifiedPhonemizer : IPhonemizerBackend
    {
        private readonly Dictionary<string, List<IPhonemizerBackend>> backendsByLanguage;
        private readonly MixedLanguagePhonemizer mixedLanguagePhonemizer;
        private readonly LanguageDetector languageDetector;
        private bool isInitialized;
        private readonly object lockObject = new object();

        public string Name => "UnifiedPhonemizer";
        public string Version => "1.0.0";
        public string License => "MIT";
        public int Priority => 200;
        public bool IsAvailable => isInitialized;
        public bool IsInitialized => isInitialized;

        public string[] SupportedLanguages
        {
            get
            {
                lock (lockObject)
                {
                    var languages = new HashSet<string> { "auto", "mixed" };
                    foreach (var lang in backendsByLanguage.Keys)
                    {
                        languages.Add(lang);
                    }
                    return languages.ToArray();
                }
            }
        }

        public UnifiedPhonemizer()
        {
            backendsByLanguage = new Dictionary<string, List<IPhonemizerBackend>>();
            mixedLanguagePhonemizer = new MixedLanguagePhonemizer();
            languageDetector = new LanguageDetector();
        }

        public async Task<bool> InitializeAsync(PhonemizerBackendOptions options = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var tasks = new List<Task<(string language, IPhonemizerBackend backend, bool success)>>();

                // Initialize Japanese backends
                tasks.Add(InitializeBackendAsync("ja", () => new OpenJTalkBackendAdapter(), options, cancellationToken));

                // Initialize English backends
                tasks.Add(InitializeBackendAsync("en", () => new SimpleLTSPhonemizer(), options, cancellationToken));
                tasks.Add(InitializeBackendAsync("en", () => new Backend.RuleBased.RuleBasedPhonemizer(), options, cancellationToken));

                // Wait for all initializations
                var results = await Task.WhenAll(tasks);

                // Process results
                foreach (var (language, backend, success) in results)
                {
                    if (success)
                    {
                        lock (lockObject)
                        {
                            if (!backendsByLanguage.ContainsKey(language))
                                backendsByLanguage[language] = new List<IPhonemizerBackend>();
                            
                            backendsByLanguage[language].Add(backend);
                        }
                        Debug.Log($"Initialized {backend.Name} for {language}");
                    }
                }

                // Initialize mixed language phonemizer
                if (await mixedLanguagePhonemizer.InitializeAsync(options, cancellationToken))
                {
                    Debug.Log("Initialized MixedLanguagePhonemizer");
                }
                else
                {
                    Debug.LogWarning("Failed to initialize MixedLanguagePhonemizer");
                }

                // Sort backends by priority
                lock (lockObject)
                {
                    foreach (var list in backendsByLanguage.Values)
                    {
                        list.Sort((a, b) => GetBackendPriority(b).CompareTo(GetBackendPriority(a)));
                    }
                }

                isInitialized = backendsByLanguage.Count > 0;
                
                if (isInitialized)
                {
                    Debug.Log($"UnifiedPhonemizer initialized with backends for: {string.Join(", ", backendsByLanguage.Keys)}");
                }
                else
                {
                    Debug.LogError("UnifiedPhonemizer failed to initialize any backends");
                }

                return isInitialized;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize UnifiedPhonemizer: {ex.Message}");
                return false;
            }
        }

        private async Task<(string language, IPhonemizerBackend backend, bool success)> InitializeBackendAsync(
            string language,
            Func<IPhonemizerBackend> backendFactory,
            PhonemizerBackendOptions options,
            CancellationToken cancellationToken)
        {
            try
            {
                var backend = backendFactory();
                var success = await backend.InitializeAsync(options, cancellationToken);
                return (language, backend, success);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize backend for {language}: {ex.Message}");
                return (language, null, false);
            }
        }

        public async Task<PhonemeResult> PhonemizeAsync(
            string text,
            string language = "auto",
            PhonemeOptions options = null,
            CancellationToken cancellationToken = default)
        {
            if (!isInitialized)
                throw new InvalidOperationException("UnifiedPhonemizer not initialized");

            try
            {
                // Handle empty text
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

                // Auto-detect language if needed
                if (language == "auto")
                {
                    language = DetectLanguage(text);
                    Debug.Log($"Auto-detected language: {language}");
                }

                // Handle mixed language text
                if (language == "mixed" || IsMixedLanguageText(text))
                {
                    if (mixedLanguagePhonemizer.IsInitialized)
                    {
                        return await mixedLanguagePhonemizer.PhonemizeAsync(text, "mixed", options, cancellationToken);
                    }
                    else
                    {
                        // Fallback to primary language
                        language = languageDetector.DetectPrimaryLanguage(text);
                    }
                }

                // Get appropriate backend
                var backend = GetBackendForLanguage(language);
                if (backend == null)
                {
                    // Try English as fallback
                    backend = GetBackendForLanguage("en");
                    if (backend == null)
                    {
                        return new PhonemeResult
                        {
                            Success = false,
                            OriginalText = text,
                            Language = language,
                            Error = $"No phonemizer available for language: {language}"
                        };
                    }
                    language = "en";
                }

                // Phonemize with selected backend
                var result = await backend.PhonemizeAsync(text, language, options, cancellationToken);
                
                // Add backend info to metadata
                if (result.Metadata == null)
                    result.Metadata = new Dictionary<string, object>();
                result.Metadata["backend_used"] = backend.Name;

                return result;
            }
            catch (Exception ex)
            {
                return new PhonemeResult
                {
                    Success = false,
                    OriginalText = text,
                    Language = language,
                    Error = $"Phonemization failed: {ex.Message}"
                };
            }
        }

        private string DetectLanguage(string text)
        {
            var segments = languageDetector.DetectSegments(text);
            
            // If mixed segments, return "mixed"
            var languages = segments
                .Where(s => !s.IsPunctuation && s.Language != "neutral")
                .Select(s => s.Language)
                .Distinct()
                .ToList();

            if (languages.Count > 1)
                return "mixed";
            
            if (languages.Count == 1)
                return languages[0];

            // Fallback to primary language detection
            return languageDetector.DetectPrimaryLanguage(text);
        }

        private bool IsMixedLanguageText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            var segments = languageDetector.DetectSegments(text);
            var languages = segments
                .Where(s => !s.IsPunctuation && s.Language != "neutral")
                .Select(s => s.Language)
                .Distinct()
                .Count();

            return languages > 1;
        }

        private IPhonemizerBackend GetBackendForLanguage(string language)
        {
            lock (lockObject)
            {
                if (backendsByLanguage.TryGetValue(language, out var backends) && backends.Count > 0)
                {
                    // Return the highest priority backend
                    return backends[0];
                }
                return null;
            }
        }

        private int GetBackendPriority(IPhonemizerBackend backend)
        {
            // Define backend priorities
            return backend.Name switch
            {
                "OpenJTalk" => 200,      // Highest for Japanese
                "Flite" => 180,          // High for English (if available)
                "SimpleLTS" => 150,      // Good for English
                "RuleBased" => 100,      // Basic fallback
                _ => 50                  // Unknown backends
            };
        }

        public bool SupportsLanguage(string language)
        {
            if (language == "auto" || language == "mixed")
                return true;

            lock (lockObject)
            {
                return backendsByLanguage.ContainsKey(language);
            }
        }

        public BackendCapabilities GetCapabilities()
        {
            // Return combined capabilities
            var caps = new BackendCapabilities
            {
                SupportsIPA = false,
                SupportsStress = false,
                SupportsSyllables = false,
                SupportsTones = false,
                SupportsDuration = false,
                SupportsBatchProcessing = true,
                IsThreadSafe = true,
                RequiresNetwork = false
            };

            lock (lockObject)
            {
                foreach (var backends in backendsByLanguage.Values)
                {
                    foreach (var backend in backends)
                    {
                        var backendCaps = backend.GetCapabilities();
                        caps.SupportsIPA |= backendCaps.SupportsIPA;
                        caps.SupportsStress |= backendCaps.SupportsStress;
                        caps.SupportsSyllables |= backendCaps.SupportsSyllables;
                        caps.SupportsTones |= backendCaps.SupportsTones;
                        caps.SupportsDuration |= backendCaps.SupportsDuration;
                        caps.RequiresNetwork |= backendCaps.RequiresNetwork;
                    }
                }
            }

            return caps;
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

            lock (lockObject)
            {
                foreach (var backends in backendsByLanguage.Values)
                {
                    foreach (var backend in backends)
                    {
                        total += backend.GetMemoryUsage();
                    }
                }
            }

            if (mixedLanguagePhonemizer.IsInitialized)
                total += mixedLanguagePhonemizer.GetMemoryUsage();

            return total;
        }

        public void Dispose()
        {
            lock (lockObject)
            {
                foreach (var backends in backendsByLanguage.Values)
                {
                    foreach (var backend in backends)
                    {
                        backend?.Dispose();
                    }
                }
                backendsByLanguage.Clear();
                
                mixedLanguagePhonemizer?.Dispose();
                isInitialized = false;
            }
        }

        /// <summary>
        /// Gets information about all available backends.
        /// </summary>
        public Dictionary<string, List<string>> GetAvailableBackends()
        {
            lock (lockObject)
            {
                var result = new Dictionary<string, List<string>>();
                
                foreach (var (language, backends) in backendsByLanguage)
                {
                    result[language] = backends.Select(b => b.Name).ToList();
                }

                if (mixedLanguagePhonemizer.IsInitialized)
                {
                    if (!result.ContainsKey("mixed"))
                        result["mixed"] = new List<string>();
                    result["mixed"].Add(mixedLanguagePhonemizer.Name);
                }

                return result;
            }
        }
    }
}