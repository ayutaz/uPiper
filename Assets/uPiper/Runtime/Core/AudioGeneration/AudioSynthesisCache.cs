using System;
using System.Collections.Generic;
using uPiper.Core.Logging;

namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// LRUベースの音声合成結果キャッシュ。
    /// 音素ID列+合成パラメータをキーとして、推論結果のfloat[]音声データをキャッシュする。
    /// キャッシュヒット時はONNX推論をスキップし、AudioClip構築のみ行う。
    /// </summary>
    internal sealed class AudioSynthesisCache
    {
        // Cache entry: audio samples + sample rate
        private readonly struct CacheEntry
        {
            public readonly float[] Samples;
            public readonly int SampleRate;
            public readonly long MemoryBytes;

            public CacheEntry(float[] samples, int sampleRate)
            {
                Samples = samples;
                SampleRate = sampleRate;
                MemoryBytes = samples.Length * sizeof(float) + 64; // 64 bytes overhead
            }
        }

        private readonly Dictionary<long, LinkedListNode<(long Key, CacheEntry Entry)>> _cache;
        private readonly LinkedList<(long Key, CacheEntry Entry)> _lruList;
        private readonly int _maxEntries;
        private readonly long _maxMemoryBytes;
        private long _currentMemoryBytes;
        private long _hitCount;
        private long _missCount;
        private long _evictionCount;

        /// <summary>
        /// Create a new audio synthesis cache.
        /// </summary>
        /// <param name="maxEntries">Maximum number of cached entries (default: 50)</param>
        /// <param name="maxMemoryMB">Maximum memory usage in MB (default: 100)</param>
        public AudioSynthesisCache(int maxEntries = 50, int maxMemoryMB = 100)
        {
            _maxEntries = Math.Max(1, maxEntries);
            _maxMemoryBytes = (long)Math.Max(1, maxMemoryMB) * 1024 * 1024;
            _cache = new Dictionary<long, LinkedListNode<(long, CacheEntry)>>(_maxEntries);
            _lruList = new LinkedList<(long, CacheEntry)>();
        }

        public long HitCount => _hitCount;
        public long MissCount => _missCount;
        public long EvictionCount => _evictionCount;
        public int Count => _cache.Count;
        public long CurrentMemoryBytes => _currentMemoryBytes;

        /// <summary>
        /// Generate a cache key from phoneme IDs and synthesis parameters.
        /// Uses FNV-1a 64-bit hash for fast, low-collision hashing.
        /// </summary>
        public static long GenerateKey(
            int[] phonemeIds,
            int[] prosodyFlat,
            float lengthScale,
            float noiseScale,
            float noiseW,
            int speakerId,
            int languageId)
        {
            unchecked
            {
                long hash = -3750763034362895579; // FNV offset basis (14695981039346656037 as signed)
                const long prime = 1099511628211;

                // Hash phoneme IDs
                if (phonemeIds != null)
                {
                    for (var i = 0; i < phonemeIds.Length; i++)
                    {
                        hash ^= phonemeIds[i];
                        hash *= prime;
                    }
                }

                // Hash prosody (null-safe)
                if (prosodyFlat != null)
                {
                    for (var i = 0; i < prosodyFlat.Length; i++)
                    {
                        hash ^= prosodyFlat[i];
                        hash *= prime;
                    }
                }

                // Hash synthesis parameters (allocation-free via union struct)
                hash ^= FloatToInt32Bits(lengthScale);
                hash *= prime;
                hash ^= FloatToInt32Bits(noiseScale);
                hash *= prime;
                hash ^= FloatToInt32Bits(noiseW);
                hash *= prime;
                hash ^= speakerId;
                hash *= prime;
                hash ^= languageId;
                hash *= prime;

                return hash;
            }
        }

        /// <summary>
        /// Try to get cached audio data.
        /// </summary>
        public bool TryGet(long key, out float[] samples, out int sampleRate)
        {
            if (_cache.TryGetValue(key, out var node))
            {
                // Move to front (most recently used)
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                _hitCount++;
                samples = node.Value.Entry.Samples;
                sampleRate = node.Value.Entry.SampleRate;
                return true;
            }

            _missCount++;
            samples = null;
            sampleRate = 0;
            return false;
        }

        /// <summary>
        /// Add audio data to the cache. Evicts LRU entries if needed.
        /// </summary>
        public void Set(long key, float[] samples, int sampleRate)
        {
            if (samples == null || samples.Length == 0)
                return;

            var entry = new CacheEntry(samples, sampleRate);

            // Don't cache if single entry exceeds memory budget
            if (entry.MemoryBytes > _maxMemoryBytes)
                return;

            // If key already exists, remove old entry first
            if (_cache.TryGetValue(key, out var existing))
            {
                _currentMemoryBytes -= existing.Value.Entry.MemoryBytes;
                _lruList.Remove(existing);
                _cache.Remove(key);
            }

            // Evict until we have space
            while (_cache.Count >= _maxEntries
                || _currentMemoryBytes + entry.MemoryBytes > _maxMemoryBytes)
            {
                if (_lruList.Count == 0) break;
                EvictLast();
            }

            var node = _lruList.AddFirst((key, entry));
            _cache[key] = node;
            _currentMemoryBytes += entry.MemoryBytes;
        }

        /// <summary>
        /// Clear all cached entries.
        /// </summary>
        public void Clear()
        {
            if (_cache.Count > 0)
            {
                PiperLogger.LogInfo(
                    "[AudioSynthesisCache] Clearing {0} entries " +
                    "(hits={1}, misses={2}, evictions={3}, memory={4:F1}MB)",
                    _cache.Count, _hitCount, _missCount, _evictionCount,
                    _currentMemoryBytes / (1024.0 * 1024.0));
            }

            _cache.Clear();
            _lruList.Clear();
            _currentMemoryBytes = 0;
            _hitCount = 0;
            _missCount = 0;
            _evictionCount = 0;
        }

        private void EvictLast()
        {
            var last = _lruList.Last;
            if (last == null) return;

            _cache.Remove(last.Value.Key);
            _currentMemoryBytes -= last.Value.Entry.MemoryBytes;
            _lruList.RemoveLast();
            _evictionCount++;
        }

        /// <summary>
        /// Reinterpret a float as int without GC allocation.
        /// Uses LayoutKind.Explicit union to avoid unsafe keyword.
        /// </summary>
        [System.Runtime.InteropServices.StructLayout(
            System.Runtime.InteropServices.LayoutKind.Explicit)]
        private struct FloatIntUnion
        {
            [System.Runtime.InteropServices.FieldOffset(0)]
            public float FloatValue;

            [System.Runtime.InteropServices.FieldOffset(0)]
            public int IntValue;
        }

        private static int FloatToInt32Bits(float value)
        {
            var union = new FloatIntUnion { FloatValue = value };
            return union.IntValue;
        }
    }
}