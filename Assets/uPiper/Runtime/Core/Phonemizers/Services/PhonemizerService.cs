using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using uPiper.Core.Phonemizers.Backend;
using uPiper.Core.Phonemizers.Backend.RuleBased;

namespace uPiper.Core.Phonemizers.Services
{
    /// <summary>
    /// Main service for phonemization with automatic backend selection.
    /// </summary>
    public class PhonemizerService
    {
        private readonly Dictionary<string, IPhonemizerBackend> backends;
        private readonly SemaphoreSlim semaphore;

        /// <summary>
        /// Gets the singleton instance.
        /// </summary>
        public static PhonemizerService Instance { get; } = new PhonemizerService();

        /// <summary>
        /// Creates a new phonemizer service.
        /// </summary>
        public PhonemizerService()
        {
            backends = new Dictionary<string, IPhonemizerBackend>();
            semaphore = new SemaphoreSlim(1);

            // Register default backends
            RegisterDefaultBackends();
        }

        /// <summary>
        /// Registers a phonemizer backend.
        /// </summary>
        public void RegisterBackend(IPhonemizerBackend backend)
        {
            if (backend == null)
                throw new ArgumentNullException(nameof(backend));

            foreach (var lang in backend.SupportedLanguages)
            {
                backends[lang] = backend;
            }
        }

        /// <summary>
        /// Phonemizes text asynchronously.
        /// </summary>
        public async Task<PhonemeResult> PhonemizeAsync(
            string text,
            string language = "en-US",
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

            await semaphore.WaitAsync(cancellationToken);
            try
            {
                // Find backend for language
                var backend = GetBackendForLanguage(language);
                if (backend == null)
                {
                    Debug.LogWarning($"No phonemizer backend found for language: {language}");
                    return new PhonemeResult
                    {
                        Phonemes = new string[0],
                        Success = false,
                        Language = language,
                        Error = $"No backend for language: {language}"
                    };
                }

                // Phonemize
                return await backend.PhonemizeAsync(text, language, null, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        }

        private IPhonemizerBackend GetBackendForLanguage(string language)
        {
            // Exact match
            if (backends.TryGetValue(language, out var backend))
                return backend;

            // Try language code without region
            var langCode = language.Split('-')[0];
            if (backends.TryGetValue(langCode, out backend))
                return backend;

            // Find any backend that supports this language
            foreach (var kvp in backends)
            {
                if (kvp.Key.StartsWith(langCode + "-"))
                    return kvp.Value;
            }

            return null;
        }

        private void RegisterDefaultBackends()
        {
            // Register rule-based backend for English
            var ruleBasedBackend = new uPiper.Core.Phonemizers.Backend.RuleBased.RuleBasedPhonemizer();
            Task.Run(async () =>
            {
                try
                {
                    await ruleBasedBackend.InitializeAsync(new PhonemizerBackendOptions());
                    RegisterBackend(ruleBasedBackend);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to initialize rule-based backend: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Disposes the service and all backends.
        /// </summary>
        public void Dispose()
        {
            foreach (var backend in backends.Values.Distinct())
            {
                backend.Dispose();
            }
            backends.Clear();
            semaphore?.Dispose();
        }
    }
}