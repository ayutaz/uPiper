using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using uPiper.Core.Phonemizers.Backend;

namespace uPiper.Core.Phonemizers
{
    /// <summary>
    /// Thread-safe LRU cache for phoneme results.
    /// Shared across all phonemizer backends for performance optimization.
    /// </summary>
    public class PhonemeCache
    {
        private class CacheEntry
        {
            public PhonemeResult Result { get; set; }
            public DateTime LastAccess { get; set; }
            public int AccessCount { get; set; }
        }

        private readonly Dictionary<string, CacheEntry> cache;
        private readonly LinkedList<string> lruList;
        private readonly object lockObject = new();
        private readonly int maxSize;
        private readonly TimeSpan maxAge;

        private long hitCount;
        private long missCount;
        private long evictionCount;

        /// <summary>
        /// Gets the singleton instance of the phoneme cache.
        /// </summary>
        public static PhonemeCache Instance { get; } = new PhonemeCache(5000);

        /// <summary>
        /// Creates a new phoneme cache.
        /// </summary>
        /// <param name="maxSize">Maximum number of entries to cache.</param>
        /// <param name="maxAge">Maximum age of cache entries.</param>
        public PhonemeCache(int maxSize, TimeSpan? maxAge = null)
        {
            this.maxSize = maxSize;
            this.maxAge = maxAge ?? TimeSpan.FromHours(1);
            cache = new Dictionary<string, CacheEntry>(maxSize);
            lruList = new LinkedList<string>();
        }

        /// <summary>
        /// Tries to get a cached phoneme result.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet(string text, string language, out PhonemeResult result)
        {
            var key = GetCacheKey(text, language);

            lock (lockObject)
            {
                if (cache.TryGetValue(key, out var entry))
                {
                    // Check if entry is still valid
                    if (DateTime.UtcNow - entry.LastAccess < maxAge)
                    {
                        // Update LRU
                        lruList.Remove(key);
                        lruList.AddFirst(key);

                        // Update stats
                        entry.LastAccess = DateTime.UtcNow;
                        entry.AccessCount++;
                        hitCount++;

                        // Clone result to prevent modification
                        result = entry.Result.Clone();
                        result.FromCache = true;
                        return true;
                    }
                    else
                    {
                        // Entry expired
                        RemoveEntry(key);
                    }
                }

                missCount++;
                result = null;
                return false;
            }
        }

        /// <summary>
        /// Adds or updates a phoneme result in the cache.
        /// </summary>
        public void Set(string text, string language, PhonemeResult result)
        {
            if (result == null || !result.Success)
                return;

            var key = GetCacheKey(text, language);

            lock (lockObject)
            {
                if (cache.ContainsKey(key))
                {
                    // Update existing entry
                    cache[key].Result = result.Clone();
                    cache[key].LastAccess = DateTime.UtcNow;
                    cache[key].AccessCount++;

                    // Move to front of LRU
                    lruList.Remove(key);
                    lruList.AddFirst(key);
                }
                else
                {
                    // Add new entry
                    if (cache.Count >= maxSize)
                    {
                        // Evict least recently used
                        EvictLRU();
                    }

                    cache[key] = new CacheEntry
                    {
                        Result = result.Clone(),
                        LastAccess = DateTime.UtcNow,
                        AccessCount = 1
                    };

                    lruList.AddFirst(key);
                }
            }
        }

        /// <summary>
        /// Clears all cached entries.
        /// </summary>
        public void Clear()
        {
            lock (lockObject)
            {
                cache.Clear();
                lruList.Clear();
                Debug.Log($"PhonemeCache cleared. Stats - Hits: {hitCount}, Misses: {missCount}, Evictions: {evictionCount}");
                hitCount = missCount = evictionCount = 0;
            }
        }

        /// <summary>
        /// Gets cache statistics.
        /// </summary>
        public CacheStatistics GetStatistics()
        {
            lock (lockObject)
            {
                var totalRequests = hitCount + missCount;
                return new CacheStatistics
                {
                    EntryCount = cache.Count,
                    MaxSize = maxSize,
                    HitCount = hitCount,
                    MissCount = missCount,
                    EvictionCount = evictionCount,
                    HitRate = totalRequests > 0 ? (float)hitCount / totalRequests : 0,
                    MemoryUsage = EstimateMemoryUsage()
                };
            }
        }

        /// <summary>
        /// Removes old entries that haven't been accessed recently.
        /// </summary>
        public void PruneExpiredEntries()
        {
            lock (lockObject)
            {
                var now = DateTime.UtcNow;
                var keysToRemove = new List<string>();

                foreach (var kvp in cache)
                {
                    if (now - kvp.Value.LastAccess > maxAge)
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    RemoveEntry(key);
                }

                if (keysToRemove.Count > 0)
                {
                    Debug.Log($"Pruned {keysToRemove.Count} expired cache entries");
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string GetCacheKey(string text, string language)
        {
            // Simple but effective cache key
            return $"{language}:{text.GetHashCode():X8}:{text.Length}";
        }

        private void EvictLRU()
        {
            if (lruList.Count == 0)
                return;

            var keyToRemove = lruList.Last.Value;
            RemoveEntry(keyToRemove);
            evictionCount++;
        }

        private void RemoveEntry(string key)
        {
            cache.Remove(key);
            lruList.Remove(key);
        }

        private long EstimateMemoryUsage()
        {
            long total = 0;

            foreach (var entry in cache.Values)
            {
                if (entry.Result.Phonemes != null)
                {
                    total += entry.Result.Phonemes.Length * 20; // Estimate per phoneme
                }
                total += 100; // Overhead per entry
            }

            return total;
        }

        /// <summary>
        /// Cache statistics.
        /// </summary>
        public struct CacheStatistics
        {
            public int EntryCount { get; set; }
            public int MaxSize { get; set; }
            public long HitCount { get; set; }
            public long MissCount { get; set; }
            public long EvictionCount { get; set; }
            public float HitRate { get; set; }
            public long MemoryUsage { get; set; }

            public override readonly string ToString()
            {
                return $"Cache Stats - Entries: {EntryCount}/{MaxSize}, " +
                       $"Hit Rate: {HitRate:P1}, " +
                       $"Memory: {MemoryUsage / 1024}KB";
            }
        }
    }

    /// <summary>
    /// Extension to make PhonemeResult cloneable.
    /// </summary>
    public static class PhonemeResultExtensions
    {
        public static PhonemeResult Clone(this PhonemeResult original)
        {
            if (original == null)
                return null;

            return new PhonemeResult
            {
                Success = original.Success,
                OriginalText = original.OriginalText,
                Phonemes = original.Phonemes?.ToArray(),
                PhonemeIds = original.PhonemeIds?.ToArray(),
                Stresses = original.Stresses?.ToArray(),
                Durations = original.Durations?.ToArray(),
                Pitches = original.Pitches?.ToArray(),
                WordBoundaries = original.WordBoundaries?.ToArray(),
                Language = original.Language,
                Backend = original.Backend,
                ProcessingTime = original.ProcessingTime,
                ProcessingTimeMs = original.ProcessingTimeMs,
                FromCache = original.FromCache,
                Error = original.Error,
                ErrorMessage = original.ErrorMessage,
                Metadata = original.Metadata != null
                    ? new Dictionary<string, object>(original.Metadata)
                    : null
            };
        }

        private static T[] ToArray<T>(this T[] source)
        {
            if (source == null)
                return null;

            var copy = new T[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }
    }
}