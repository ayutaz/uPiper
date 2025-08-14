using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using uPiper.Core.Phonemizers.Backend;
using uPiper.Core.Phonemizers.Backend.Flite;
using uPiper.Core.Phonemizers.Backend.RuleBased;

namespace uPiper.Core.Phonemizers.Multilingual
{
    /// <summary>
    /// Multilingual phonemizer service with fallback support
    /// </summary>
    public class MultilingualPhonemizerService : IMultilingualPhonemizerService
    {
        private readonly Dictionary<string, List<IPhonemizerBackend>> backendsByLanguage;
        private readonly Dictionary<string, LanguageCapabilities> languageCapabilities;
        private readonly object syncLock = new();

        public MultilingualPhonemizerService()
        {
            backendsByLanguage = new Dictionary<string, List<IPhonemizerBackend>>();
            languageCapabilities = new Dictionary<string, LanguageCapabilities>();

            InitializeBackends();
            BuildLanguageCapabilities();
        }

        /// <summary>
        /// Initialize phonemizer backends
        /// </summary>
        private void InitializeBackends()
        {
            // Create Flite backend for English variants
            Task.Run(async () =>
            {
                try
                {
                    var fliteBackend = new Backend.Flite.FlitePhonemizerBackend();
                    await fliteBackend.InitializeAsync(new PhonemizerBackendOptions());
                    foreach (var lang in fliteBackend.SupportedLanguages)
                    {
                        AddBackend(lang, fliteBackend);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to initialize Flite backend: {ex.Message}");
                }
            });

            // Create rule-based backend
            Task.Run(async () =>
            {
                try
                {
                    var ruleBasedBackend = new uPiper.Core.Phonemizers.Backend.RuleBased.RuleBasedPhonemizer();
                    await ruleBasedBackend.InitializeAsync(new PhonemizerBackendOptions());
                    foreach (var lang in ruleBasedBackend.SupportedLanguages)
                    {
                        AddBackend(lang, ruleBasedBackend);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to initialize rule-based backend: {ex.Message}");
                }
            });

            // Add other language backends
            Task.Run(() =>
            {
                try
                {
                    // Currently only Japanese (via OpenJTalk) and English are supported
                    // Additional language backends can be added here in the future
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to initialize language backends: {ex.Message}");
                }
            });
        }

        private void AddBackend(string language, IPhonemizerBackend backend)
        {
            lock (syncLock)
            {
                if (!backendsByLanguage.ContainsKey(language))
                {
                    backendsByLanguage[language] = new List<IPhonemizerBackend>();
                }
                backendsByLanguage[language].Add(backend);
            }
        }

        /// <summary>
        /// Build language capabilities based on registered backends
        /// </summary>
        private void BuildLanguageCapabilities()
        {
            lock (syncLock)
            {
                foreach (var kvp in backendsByLanguage)
                {
                    var language = kvp.Key;
                    var backends = kvp.Value;

                    var capabilities = new LanguageCapabilities
                    {
                        Language = language,
                        SupportedBackends = backends.Select(b => b.Name).ToList(),
                        SupportsIPA = backends.Any(b => b.GetCapabilities().SupportsIPA),
                        SupportsStress = backends.Any(b => b.GetCapabilities().SupportsStress),
                        SupportsSyllables = backends.Any(b => b.GetCapabilities().SupportsSyllables),
                        SupportsTones = backends.Any(b => b.GetCapabilities().SupportsTones)
                    };

                    languageCapabilities[language] = capabilities;
                }
            }
        }

        /// <inheritdoc/>
        public Dictionary<string, LanguageCapabilities> GetSupportedLanguages()
        {
            lock (syncLock)
            {
                return new Dictionary<string, LanguageCapabilities>(languageCapabilities);
            }
        }

        /// <inheritdoc/>
        public IPhonemizerBackend GetPreferredBackend(string language)
        {
            lock (syncLock)
            {
                if (backendsByLanguage.TryGetValue(language, out var backends))
                {
                    return backends.FirstOrDefault();
                }

                // Try language code without region
                var langCode = language.Split('-')[0];
                if (backendsByLanguage.TryGetValue(langCode, out backends))
                {
                    return backends.FirstOrDefault();
                }

                return null;
            }
        }

        /// <inheritdoc/>
        public async Task<PhonemeResult> PhonemizeWithFallbackAsync(
            string text,
            string language,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new PhonemeResult
                {
                    Phonemes = new string[0],
                    Success = true,
                    Language = language
                };
            }

            var backend = GetPreferredBackend(language);
            if (backend == null)
            {
                return new PhonemeResult
                {
                    Phonemes = new string[0],
                    Success = false,
                    Language = language,
                    Error = $"No backend found for language: {language}"
                };
            }

            try
            {
                return await backend.PhonemizeAsync(text, language, null, cancellationToken);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Phonemization failed for {language}: {ex.Message}");
                return new PhonemeResult
                {
                    Phonemes = new string[0],
                    Success = false,
                    Language = language,
                    Error = ex.Message
                };
            }
        }

        /// <inheritdoc/>
        public void RegisterBackend(string language, IPhonemizerBackend backend)
        {
            if (backend == null)
                throw new ArgumentNullException(nameof(backend));

            AddBackend(language, backend);
            BuildLanguageCapabilities();
        }

        /// <inheritdoc/>
        public void SetLanguageFallback(string language, string[] fallbackLanguages)
        {
            // Not implemented in simplified version
        }

        /// <inheritdoc/>
        public Dictionary<string, List<IPhonemizerBackend>> GetAllBackends()
        {
            lock (syncLock)
            {
                return new Dictionary<string, List<IPhonemizerBackend>>(backendsByLanguage);
            }
        }

        /// <inheritdoc/>
        public Task<Dictionary<string, PhonemeResult>> PhonemizeBatchAsync(
            Dictionary<string, string> textsByLanguage,
            CancellationToken cancellationToken = default)
        {
            var tasks = textsByLanguage.Select(kvp =>
                PhonemizeWithFallbackAsync(kvp.Value, kvp.Key, cancellationToken)
                    .ContinueWith(t => new { Language = kvp.Key, t.Result })
            ).ToArray();

            return Task.WhenAll(tasks).ContinueWith(t =>
            {
                var results = new Dictionary<string, PhonemeResult>();
                foreach (var item in t.Result)
                {
                    results[item.Language] = item.Result;
                }
                return results;
            });
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            lock (syncLock)
            {
                foreach (var backend in backendsByLanguage.Values.SelectMany(b => b).Distinct())
                {
                    backend.Dispose();
                }
                backendsByLanguage.Clear();
                languageCapabilities.Clear();
            }
        }
    }
}