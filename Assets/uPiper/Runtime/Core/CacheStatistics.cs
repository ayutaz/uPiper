using System;
using UnityEngine;
using uPiper.Core.Logging;

namespace uPiper.Core
{
    /// <summary>
    /// Statistics for phoneme cache
    /// </summary>
    [Serializable]
    public class CacheStatistics
    {
        /// <summary>
        /// Bytes per megabyte conversion constant
        /// </summary>
        private const float BytesPerMB = 1024f * 1024f;

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
        public float TotalSizeMB => TotalSizeBytes / BytesPerMB;

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
        public float MaxSizeMB => MaxSizeBytes / BytesPerMB;

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
            // Use invariant culture to ensure consistent formatting across platforms
            var formattedString = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "Cache Stats: {0} entries, {1:F2}/{2:F2} MB ({3:P0}), " +
                "Hit Rate: {4:P1} ({5} hits, {6} misses), " +
                "Evictions: {7}",
                EntryCount, TotalSizeMB, MaxSizeMB, UsagePercentage,
                HitRate, HitCount, MissCount, EvictionCount);
            return formattedString;
        }

        /// <summary>
        /// Log statistics to Unity console
        /// </summary>
        public void LogStatistics()
        {
            PiperLogger.LogInfo("[Cache Statistics]");
            PiperLogger.LogInfo("  Entries: {0}", EntryCount);
            PiperLogger.LogInfo("  Size: {0:F2} / {1:F2} MB ({2:P0} used)", TotalSizeMB, MaxSizeMB, UsagePercentage);
            PiperLogger.LogInfo("  Hit Rate: {0:P1} ({1} hits, {2} misses)", HitRate, HitCount, MissCount);
            PiperLogger.LogInfo("  Average Entry Size: {0:F0} bytes", AverageEntrySizeBytes);
            PiperLogger.LogInfo("  Evictions: {0}", EvictionCount);
            PiperLogger.LogInfo("  Time Since Last Clear: {0:g}", TimeSinceLastClear);
        }
    }
}