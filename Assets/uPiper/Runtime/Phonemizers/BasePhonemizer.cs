using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace uPiper.Phonemizers
{
    /// <summary>
    /// Base class for phonemizers with common functionality
    /// </summary>
    public abstract class BasePhonemizer : IPhonemizer
    {
        protected readonly Dictionary<string, string[]> _cache;
        protected readonly bool _useCache;
        protected readonly int _maxCacheSize;
        private readonly object _cacheLock = new object();

        protected BasePhonemizer(bool useCache = true, int maxCacheSize = 1000)
        {
            _useCache = useCache;
            _maxCacheSize = maxCacheSize;
            _cache = new Dictionary<string, string[]>();
        }

        public virtual string[] Phonemize(string text, string language)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Array.Empty<string>();
            }

            // Normalize text
            text = NormalizeText(text);

            // Check cache
            if (_useCache)
            {
                var cacheKey = GetCacheKey(text, language);
                lock (_cacheLock)
                {
                    if (_cache.TryGetValue(cacheKey, out var cachedResult))
                    {
                        return cachedResult;
                    }
                }
            }

            // Perform phonemization
            var phonemes = PerformPhonemization(text, language);

            // Cache result
            if (_useCache && phonemes != null)
            {
                CacheResult(text, language, phonemes);
            }

            return phonemes;
        }

        /// <summary>
        /// Performs the actual phonemization
        /// </summary>
        protected abstract string[] PerformPhonemization(string text, string language);

        /// <summary>
        /// Normalizes input text
        /// </summary>
        protected virtual string NormalizeText(string text)
        {
            // Remove extra whitespace
            text = Regex.Replace(text, @"\s+", " ");
            
            // Trim
            text = text.Trim();

            return text;
        }

        /// <summary>
        /// Gets cache key for the given text and language
        /// </summary>
        protected virtual string GetCacheKey(string text, string language)
        {
            return $"{language}:{text}";
        }

        /// <summary>
        /// Caches phonemization result
        /// </summary>
        protected virtual void CacheResult(string text, string language, string[] phonemes)
        {
            var cacheKey = GetCacheKey(text, language);
            
            lock (_cacheLock)
            {
                // Check cache size limit
                if (_cache.Count >= _maxCacheSize)
                {
                    // Simple eviction - remove first item
                    // In production, use LRU or similar strategy
                    var firstKey = _cache.Keys.GetEnumerator();
                    if (firstKey.MoveNext())
                    {
                        _cache.Remove(firstKey.Current);
                    }
                }

                _cache[cacheKey] = phonemes;
            }
        }

        /// <summary>
        /// Clears the phonemization cache
        /// </summary>
        public virtual void ClearCache()
        {
            lock (_cacheLock)
            {
                _cache.Clear();
            }
        }

        public virtual void Dispose()
        {
            ClearCache();
        }
    }
}