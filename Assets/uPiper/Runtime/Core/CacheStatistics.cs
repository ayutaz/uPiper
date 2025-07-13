using System;
using UnityEngine;

namespace uPiper.Core
{
    /// <summary>
    /// Statistics for phoneme cache
    /// </summary>
    [Serializable]
    public class CacheStatistics
    {
        /// <summary>
        /// Total number of cached entries
        /// </summary>
        public int EntryCount { get; set; }

        /// <summary>
        /// Total cache size in bytes
        /// </summary>
        public long TotalSizeBytes { get; set; }

        /// <summary>
        /// Total cache size in MB
        /// </summary>
        public float TotalSizeMB => TotalSizeBytes / (1024f * 1024f);

        /// <summary>
        /// Number of cache hits
        /// </summary>
        public long HitCount { get; set; }

        /// <summary>
        /// Number of cache misses
        /// </summary>
        public long MissCount { get; set; }

        /// <summary>
        /// Cache hit rate (0.0 to 1.0)
        /// </summary>
        public float HitRate
        {
            get
            {
                var total = HitCount + MissCount;
                return total > 0 ? (float)HitCount / total : 0f;
            }
        }

        /// <summary>
        /// Maximum cache size in bytes
        /// </summary>
        public long MaxSizeBytes { get; set; }

        /// <summary>
        /// Maximum cache size in MB
        /// </summary>
        public float MaxSizeMB => MaxSizeBytes / (1024f * 1024f);

        /// <summary>
        /// Cache usage percentage (0.0 to 1.0)
        /// </summary>
        public float UsagePercentage => MaxSizeBytes > 0 ? (float)TotalSizeBytes / MaxSizeBytes : 0f;

        /// <summary>
        /// Number of evicted entries
        /// </summary>
        public long EvictionCount { get; set; }

        /// <summary>
        /// Last cache clear time
        /// </summary>
        public DateTime LastClearTime { get; set; }

        /// <summary>
        /// Average entry size in bytes
        /// </summary>
        public float AverageEntrySizeBytes => EntryCount > 0 ? (float)TotalSizeBytes / EntryCount : 0f;

        /// <summary>
        /// Time since last clear
        /// </summary>
        public TimeSpan TimeSinceLastClear => DateTime.Now - LastClearTime;

        /// <summary>
        /// Reset statistics
        /// </summary>
        public void Reset()
        {
            EntryCount = 0;
            TotalSizeBytes = 0;
            HitCount = 0;
            MissCount = 0;
            EvictionCount = 0;
            LastClearTime = DateTime.Now;
        }

        /// <summary>
        /// Record a cache hit
        /// </summary>
        public void RecordHit()
        {
            HitCount++;
        }

        /// <summary>
        /// Record a cache miss
        /// </summary>
        public void RecordMiss()
        {
            MissCount++;
        }

        /// <summary>
        /// Record an eviction
        /// </summary>
        public void RecordEviction(int count = 1)
        {
            EvictionCount += count;
        }

        /// <summary>
        /// Update cache size
        /// </summary>
        public void UpdateSize(int entryCount, long totalSizeBytes)
        {
            EntryCount = entryCount;
            TotalSizeBytes = totalSizeBytes;
        }

        /// <summary>
        /// Get formatted statistics string
        /// </summary>
        public override string ToString()
        {
            return $"Cache Stats: {EntryCount} entries, {TotalSizeMB:F2}/{MaxSizeMB:F2} MB ({UsagePercentage:P0}), " +
                   $"Hit Rate: {HitRate:P1} ({HitCount} hits, {MissCount} misses), " +
                   $"Evictions: {EvictionCount}";
        }

        /// <summary>
        /// Log statistics to Unity console
        /// </summary>
        public void LogStatistics()
        {
            Debug.Log($"[uPiper Cache Statistics]");
            Debug.Log($"  Entries: {EntryCount}");
            Debug.Log($"  Size: {TotalSizeMB:F2} / {MaxSizeMB:F2} MB ({UsagePercentage:P0} used)");
            Debug.Log($"  Hit Rate: {HitRate:P1} ({HitCount} hits, {MissCount} misses)");
            Debug.Log($"  Average Entry Size: {AverageEntrySizeBytes:F0} bytes");
            Debug.Log($"  Evictions: {EvictionCount}");
            Debug.Log($"  Time Since Last Clear: {TimeSinceLastClear:g}");
        }
    }
}