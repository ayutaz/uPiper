using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using uPiper.Core;
using uPiper.Core.Logging;
using uPiper.Core.Phonemizers.Backend;
using uPiper.Core.Phonemizers.Cache;
using uPiper.Core.Phonemizers.Text;

namespace uPiper.Core.Phonemizers
{
    /// <summary>
    /// Base implementation of IPhonemizer with caching and normalization support.
    /// </summary>
    public abstract class BasePhonemizer : IPhonemizer
    {
        private readonly LRUCache<string, PhonemeResult> _cache;
        private readonly ITextNormalizer _textNormalizer;
        private readonly CacheStatistics _cacheStats;
        protected readonly Dictionary<string, LanguageInfo> _languageInfos;
        private bool _disposed;

        /// <summary>
        /// Gets the name of the phonemizer.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Gets the version of the phonemizer.
        /// </summary>
        public abstract string Version { get; }

        /// <summary>
        /// Gets the list of supported language codes.
        /// </summary>
        public abstract string[] SupportedLanguages { get; }

        /// <summary>
        /// Gets or sets whether to use caching.
        /// </summary>
        public bool UseCache { get; set; } = true;

        /// <summary>
        /// Gets the default cache size.
        /// </summary>
        protected virtual int DefaultCacheSize => 1000;

        /// <summary>
        /// Creates a new instance of BasePhonemizer.
        /// </summary>
        /// <param name="cacheSize">Size of the LRU cache.</param>
        /// <param name="textNormalizer">Optional custom text normalizer.</param>
        protected BasePhonemizer(int? cacheSize = null, ITextNormalizer textNormalizer = null)
        {
            _cache = new LRUCache<string, PhonemeResult>(cacheSize ?? DefaultCacheSize);
            _textNormalizer = textNormalizer ?? new TextNormalizer();
            _cacheStats = new CacheStatistics { MaxSizeBytes = (cacheSize ?? DefaultCacheSize) * 1024 }; // Estimate
            _languageInfos = new Dictionary<string, LanguageInfo>();

            InitializeLanguages();
            PiperLogger.LogInfo($"{Name} phonemizer initialized (version {Version})");
        }

        /// <summary>
        /// Converts text to phonemes asynchronously.
        /// </summary>
        public virtual async Task<PhonemeResult> PhonemizeAsync(string text, string language = "ja", CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new PhonemeResult
                {
                    OriginalText = text ?? string.Empty,
                    Language = language,
                    Phonemes = Array.Empty<string>(),
                    PhonemeIds = Array.Empty<int>(),
                    Durations = Array.Empty<float>(),
                    Pitches = Array.Empty<float>(),
                    ProcessingTime = TimeSpan.Zero,
                    FromCache = false
                };
            }

            if (!IsLanguageSupported(language))
            {
                throw new PiperPhonemizationException(text, language, $"Language '{language}' is not supported by {Name}");
            }

            var sw = Stopwatch.StartNew();

            // Normalize text
            var normalizedText = _textNormalizer.Normalize(text, language);
            var cacheKey = GenerateCacheKey(normalizedText, language);

            // Check cache
            if (UseCache && _cache.TryGet(cacheKey, out var cachedResult))
            {
                _cacheStats.RecordHit();
                PiperLogger.LogDebug($"Phonemizer cache hit for: \"{text[..Math.Min(20, text.Length)]}...\"");

                var result = cachedResult.Clone();
                result.OriginalText = text; // Preserve original text
                result.FromCache = true;
                result.ProcessingTime = sw.Elapsed;
                return result;
            }

            _cacheStats.RecordMiss();

            try
            {
                // Perform actual phonemization
                cancellationToken.ThrowIfCancellationRequested();
                var phonemeResult = await PhonemizeInternalAsync(normalizedText, language, cancellationToken);

                phonemeResult.OriginalText = text;
                phonemeResult.Language = language;
                phonemeResult.ProcessingTime = sw.Elapsed;
                phonemeResult.FromCache = false;

                // Cache the result
                if (UseCache)
                {
                    _cache.Add(cacheKey, phonemeResult.Clone());
                    _cacheStats.UpdateSize(_cache.Count, _cache.Count * 1024); // Estimate
                }

                PiperLogger.LogDebug($"Phonemized \"{text[..Math.Min(20, text.Length)]}...\" in {sw.ElapsedMilliseconds}ms");
                return phonemeResult;
            }
            catch (OperationCanceledException)
            {
                PiperLogger.LogWarning("Phonemization cancelled");
                throw;
            }
            catch (Exception ex)
            {
                PiperLogger.LogError($"Phonemization failed: {ex.Message}");
                throw new PiperPhonemizationException(text, language, $"Failed to phonemize text: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Converts text to phonemes synchronously.
        /// </summary>
        public virtual PhonemeResult Phonemize(string text, string language = "ja")
        {
            try
            {
                // Run async method synchronously using a thread pool task
                // This prevents deadlocks in Unity's synchronization context
                return Task.Run(() => PhonemizeAsync(text, language)).Result;
            }
            catch (AggregateException ex)
            {
                // Unwrap the AggregateException to throw the actual inner exception
                throw ex.InnerException ?? ex;
            }
        }

        /// <summary>
        /// Converts multiple texts to phonemes asynchronously.
        /// </summary>
        public virtual async Task<PhonemeResult[]> PhonemizeBatchAsync(string[] texts, string language = "ja", CancellationToken cancellationToken = default)
        {
            if (texts == null || texts.Length == 0)
            {
                return Array.Empty<PhonemeResult>();
            }

            var tasks = texts.Select(text => PhonemizeAsync(text, language, cancellationToken));
            return await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Clears the phonemization cache.
        /// </summary>
        public virtual void ClearCache()
        {
            _cache.Clear();
            _cacheStats.Reset();
            PiperLogger.LogInfo($"{Name} phonemizer cache cleared");
        }

        /// <summary>
        /// Gets cache statistics.
        /// </summary>
        public virtual CacheStatistics GetCacheStatistics()
        {
            _cacheStats.UpdateSize(_cache.Count, _cache.Count * 1024); // Estimate
            return _cacheStats;
        }

        /// <summary>
        /// Checks if a language is supported.
        /// </summary>
        public virtual bool IsLanguageSupported(string language)
        {
            if (string.IsNullOrEmpty(language))
            {
                return false;
            }

            return SupportedLanguages.Contains(language.ToLowerInvariant());
        }

        /// <summary>
        /// Gets detailed information about a supported language.
        /// </summary>
        public virtual LanguageInfo GetLanguageInfo(string language)
        {
            if (string.IsNullOrEmpty(language))
            {
                return null;
            }

            _languageInfos.TryGetValue(language.ToLowerInvariant(), out var info);
            return info;
        }

        /// <summary>
        /// Performs the actual phonemization. Must be implemented by derived classes.
        /// </summary>
        /// <param name="normalizedText">The normalized text to phonemize.</param>
        /// <param name="language">The language code.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The phonemization result.</returns>
        protected abstract Task<PhonemeResult> PhonemizeInternalAsync(string normalizedText, string language, CancellationToken cancellationToken);

        /// <summary>
        /// Initializes language information. Override to provide custom language info.
        /// </summary>
        protected virtual void InitializeLanguages()
        {
            // Default implementation - derived classes should override
            foreach (var lang in SupportedLanguages)
            {
                _languageInfos[lang] = new LanguageInfo
                {
                    Code = lang,
                    Name = GetLanguageName(lang),
                    NativeName = GetLanguageNativeName(lang)
                };
            }
        }

        /// <summary>
        /// Generates a cache key for the given text and language.
        /// </summary>
        protected virtual string GenerateCacheKey(string normalizedText, string language)
        {
            return $"{language}:{normalizedText.GetHashCode()}";
        }

        /// <summary>
        /// Gets the English name for a language code.
        /// </summary>
        protected virtual string GetLanguageName(string languageCode)
        {
            return languageCode switch
            {
                "ja" => "Japanese",
                "en" => "English",
                "zh" => "Chinese",
                "ko" => "Korean",
                "de" => "German",
                "fr" => "French",
                "es" => "Spanish",
                "it" => "Italian",
                "pt" => "Portuguese",
                "ru" => "Russian",
                _ => languageCode.ToUpperInvariant()
            };
        }

        /// <summary>
        /// Gets the native name for a language code.
        /// </summary>
        protected virtual string GetLanguageNativeName(string languageCode)
        {
            return languageCode switch
            {
                "ja" => "日本語",
                "en" => "English",
                "zh" => "中文",
                "ko" => "한국어",
                "de" => "Deutsch",
                "fr" => "Français",
                "es" => "Español",
                "it" => "Italiano",
                "pt" => "Português",
                "ru" => "Русский",
                _ => GetLanguageName(languageCode)
            };
        }

        /// <summary>
        /// Disposes the phonemizer and releases resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the phonemizer.
        /// </summary>
        /// <param name="disposing">True if disposing managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _cache?.Dispose();
                    PiperLogger.LogInfo($"{Name} phonemizer disposed");
                }
                _disposed = true;
            }
        }
    }
}