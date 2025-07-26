using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using uPiper.Core.Phonemizers.Backend;
using uPiper.Core.Phonemizers.Backend.RuleBased;
using uPiper.Core.Phonemizers.ErrorHandling;
using uPiper.Phonemizers.Data;

namespace uPiper.Core.Phonemizers.Services
{
    /// <summary>
    /// Main service for phonemization with automatic backend selection and error handling.
    /// </summary>
    public class PhonemizerService : IPhonemizerService
    {
        private readonly PhonemizerBackendFactory backendFactory;
        private readonly Dictionary<string, SafePhonemizerWrapper> safeBackends;
        private readonly LRUCache<string, PhonemeResult> cache;
        private readonly SemaphoreSlim semaphore;
        private readonly PhonemizerServiceOptions options;
        private readonly PhonemizerDataManager dataManager;

        /// <summary>
        /// Gets the singleton instance.
        /// </summary>
        public static PhonemizerService Instance { get; } = new PhonemizerService();

        /// <summary>
        /// Creates a new phonemizer service.
        /// </summary>
        /// <param name="options">Service options.</param>
        public PhonemizerService(PhonemizerServiceOptions options = null)
        {
            this.options = options ?? PhonemizerServiceOptions.Default;
            this.backendFactory = PhonemizerBackendFactory.Instance;
            this.safeBackends = new Dictionary<string, SafePhonemizerWrapper>();
            this.cache = new LRUCache<string, PhonemeResult>(this.options.CacheSize);
            this.semaphore = new SemaphoreSlim(this.options.MaxConcurrency);
            
            // Initialize data manager
            string dataPath = Application.persistentDataPath + "/uPiper/PhonemizerData";
            this.dataManager = new PhonemizerDataManager(dataPath);

            // Register default backends
            RegisterDefaultBackends();
        }

        /// <inheritdoc/>
        public async Task<PhonemeResult> PhonemizeAsync(
            string text,
            string language = null,
            PhonemeOptions options = null)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new PhonemeResult
                {
                    Success = false,
                    ErrorMessage = "Text is null or empty",
                    Backend = "None"
                };
            }

            // Auto-detect language if not specified
            language ??= DetectLanguage(text);

            // Check cache
            var cacheKey = GenerateCacheKey(text, language, options);
            if (cache.TryGet(cacheKey, out var cachedResult))
            {
                cachedResult.FromCache = true;
                return cachedResult;
            }

            // Get appropriate backend
            var backend = GetOrCreateSafeBackend(language);
            if (backend == null)
            {
                return new PhonemeResult
                {
                    Success = false,
                    ErrorMessage = $"No backend available for language: {language}",
                    Language = language,
                    Backend = "None"
                };
            }

            // Execute with concurrency control
            await semaphore.WaitAsync();
            try
            {
                var result = await backend.PhonemizeAsync(text, language, options);
                
                // Cache successful results
                if (result.Success)
                {
                    cache.Add(cacheKey, result);
                }

                return result;
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <inheritdoc/>
        public async Task<PhonemeResult[]> PhonemizeBatchAsync(
            string[] texts,
            string language = null,
            PhonemeOptions options = null)
        {
            if (texts == null || texts.Length == 0)
            {
                return Array.Empty<PhonemeResult>();
            }

            // Process in parallel with concurrency limit
            var tasks = texts.Select(text => PhonemizeAsync(text, language, options));
            return await Task.WhenAll(tasks);
        }

        /// <inheritdoc/>
        public string[] GetAvailableLanguages()
        {
            return backendFactory.GetAvailableLanguages();
        }

        /// <inheritdoc/>
        public IDataManager DataManager => dataManager;

        /// <inheritdoc/>
        public CacheStatistics GetCacheStatistics()
        {
            return cache.GetStatistics();
        }

        /// <summary>
        /// Initializes all registered backends.
        /// </summary>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            var options = new PhonemizerBackendOptions
            {
                DataPath = Application.streamingAssetsPath,
                EnableDebugLogging = Debug.isDebugBuild
            };

            await backendFactory.InitializeAllBackendsAsync(options, cancellationToken);
        }

        /// <summary>
        /// Registers a custom backend.
        /// </summary>
        public void RegisterBackend(IPhonemizerBackend backend)
        {
            backendFactory.RegisterBackend(backend);
        }

        /// <summary>
        /// Gets backend information for debugging.
        /// </summary>
        public BackendInfo[] GetBackendInfo()
        {
            return backendFactory.GetBackendInfo();
        }

        /// <summary>
        /// Clears the phoneme cache.
        /// </summary>
        public void ClearCache()
        {
            cache.Clear();
        }

        private void RegisterDefaultBackends()
        {
            // Register rule-based backend (MIT licensed) - English
            var ruleBasedBackend = new RuleBasedPhonemizer();
            backendFactory.RegisterBackend(ruleBasedBackend);

            // Register Spanish backend (MIT licensed)
            var spanishBackend = new Backend.Spanish.SpanishPhonemizer();
            backendFactory.RegisterBackend(spanishBackend);

            // Register Chinese backend (MIT licensed)
            var chineseBackend = new ChinesePhonemizerProxy();
            backendFactory.RegisterBackend(chineseBackend);

            // Register Korean backend (MIT licensed)
            var koreanBackend = new Backend.Korean.KoreanPhonemizer();
            backendFactory.RegisterBackend(koreanBackend);

            // Register fallback backend
            var fallbackBackend = new FallbackPhonemizer();
            backendFactory.RegisterBackend(fallbackBackend);

            // Initialize in background
            _ = InitializeAsync();
        }

        private SafePhonemizerWrapper GetOrCreateSafeBackend(string language)
        {
            lock (safeBackends)
            {
                if (safeBackends.TryGetValue(language, out var existing))
                {
                    return existing;
                }

                // Get primary backend for language
                var primary = backendFactory.GetBackend(language, "MIT");
                if (primary == null)
                {
                    Debug.LogWarning($"No MIT-licensed backend found for language: {language}");
                    return null;
                }

                // Get fallback
                var fallback = backendFactory.GetBackendByName("Fallback");

                // Create safe wrapper
                var safeBackend = primary.WithSafety(
                    fallback,
                    options.CircuitBreakerThreshold,
                    TimeSpan.FromSeconds(options.CircuitBreakerTimeoutSeconds));

                safeBackends[language] = safeBackend;
                return safeBackend;
            }
        }

        private string DetectLanguage(string text)
        {
            // Simple language detection based on character ranges
            foreach (char c in text)
            {
                if (c >= 0x3040 && c <= 0x309F || c >= 0x30A0 && c <= 0x30FF)
                    return "ja"; // Japanese
                if (c >= 0x4E00 && c <= 0x9FFF)
                    return "zh"; // Chinese
                if (c >= 0xAC00 && c <= 0xD7AF)
                    return "ko"; // Korean
                if (c >= 0x0600 && c <= 0x06FF)
                    return "ar"; // Arabic
                if (c >= 0x0400 && c <= 0x04FF)
                    return "ru"; // Cyrillic
            }

            return "en"; // Default to English
        }

        private string GenerateCacheKey(string text, string language, PhonemeOptions options)
        {
            var optionsHash = options?.GetHashCode() ?? 0;
            return $"{language}:{text}:{optionsHash}";
        }
    }

    /// <summary>
    /// Options for the phonemizer service.
    /// </summary>
    public class PhonemizerServiceOptions
    {
        /// <summary>
        /// Maximum concurrent phonemization operations.
        /// </summary>
        public int MaxConcurrency { get; set; } = 4;

        /// <summary>
        /// Cache size for phoneme results.
        /// </summary>
        public int CacheSize { get; set; } = 1000;

        /// <summary>
        /// Circuit breaker failure threshold.
        /// </summary>
        public int CircuitBreakerThreshold { get; set; } = 3;

        /// <summary>
        /// Circuit breaker timeout in seconds.
        /// </summary>
        public int CircuitBreakerTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Gets default options.
        /// </summary>
        public static PhonemizerServiceOptions Default => new PhonemizerServiceOptions();
    }

    /// <summary>
    /// Interface for phonemizer service.
    /// </summary>
    public interface IPhonemizerService
    {
        Task<PhonemeResult> PhonemizeAsync(string text, string language = null, PhonemeOptions options = null);
        Task<PhonemeResult[]> PhonemizeBatchAsync(string[] texts, string language = null, PhonemeOptions options = null);
        string[] GetAvailableLanguages();
        IDataManager DataManager { get; }
        CacheStatistics GetCacheStatistics();
    }

    /// <summary>
    /// Placeholder for data manager interface.
    /// </summary>
    public interface IDataManager
    {
        // To be implemented
    }
}