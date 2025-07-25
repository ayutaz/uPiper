using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace uPiper.Core.Phonemizers.Caching
{
    /// <summary>
    /// Thread-safe Least Recently Used (LRU) cache implementation.
    /// </summary>
    /// <typeparam name="TKey">Type of cache keys.</typeparam>
    /// <typeparam name="TValue">Type of cached values.</typeparam>
    public class LRUCache<TKey, TValue> : IDisposable
    {
        private class CacheNode
        {
            public TKey Key { get; set; }
            public TValue Value { get; set; }
            public DateTime LastAccess { get; set; }
            public long Size { get; set; }
        }

        private readonly Dictionary<TKey, LinkedListNode<CacheNode>> cache;
        private readonly LinkedList<CacheNode> lruList;
        private readonly ReaderWriterLockSlim lockSlim;
        private readonly int maxSize;
        private readonly long maxMemoryBytes;
        private readonly Func<TValue, long> sizeCalculator;
        
        private long currentMemoryUsage;
        private long hitCount;
        private long missCount;
        private long evictionCount;

        /// <summary>
        /// Gets the number of items in the cache.
        /// </summary>
        public int Count
        {
            get
            {
                lockSlim.EnterReadLock();
                try
                {
                    return cache.Count;
                }
                finally
                {
                    lockSlim.ExitReadLock();
                }
            }
        }

        /// <summary>
        /// Gets the current memory usage in bytes.
        /// </summary>
        public long MemoryUsage => Interlocked.Read(ref currentMemoryUsage);

        /// <summary>
        /// Creates a new LRU cache.
        /// </summary>
        /// <param name="maxSize">Maximum number of items to cache.</param>
        /// <param name="maxMemoryBytes">Maximum memory usage in bytes (0 = unlimited).</param>
        /// <param name="sizeCalculator">Function to calculate item size in bytes.</param>
        public LRUCache(int maxSize = 1000, long maxMemoryBytes = 0, Func<TValue, long> sizeCalculator = null)
        {
            this.maxSize = maxSize;
            this.maxMemoryBytes = maxMemoryBytes;
            this.sizeCalculator = sizeCalculator;
            this.cache = new Dictionary<TKey, LinkedListNode<CacheNode>>(maxSize);
            this.lruList = new LinkedList<CacheNode>();
            this.lockSlim = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        }

        /// <summary>
        /// Tries to get a value from the cache.
        /// </summary>
        /// <param name="key">The key to look up.</param>
        /// <param name="value">The value if found.</param>
        /// <returns>True if the key was found.</returns>
        public bool TryGet(TKey key, out TValue value)
        {
            lockSlim.EnterUpgradeableReadLock();
            try
            {
                if (cache.TryGetValue(key, out var node))
                {
                    // Move to front (most recently used)
                    lockSlim.EnterWriteLock();
                    try
                    {
                        lruList.Remove(node);
                        lruList.AddFirst(node);
                        node.Value.LastAccess = DateTime.UtcNow;
                        Interlocked.Increment(ref hitCount);
                    }
                    finally
                    {
                        lockSlim.ExitWriteLock();
                    }

                    value = node.Value.Value;
                    return true;
                }

                Interlocked.Increment(ref missCount);
                value = default;
                return false;
            }
            finally
            {
                lockSlim.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Adds or updates an item in the cache.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value to cache.</param>
        public void Add(TKey key, TValue value)
        {
            var size = sizeCalculator?.Invoke(value) ?? 0;

            lockSlim.EnterWriteLock();
            try
            {
                if (cache.TryGetValue(key, out var existingNode))
                {
                    // Update existing
                    lruList.Remove(existingNode);
                    Interlocked.Add(ref currentMemoryUsage, -existingNode.Value.Size);
                }

                // Add new node at front
                var newNode = new CacheNode
                {
                    Key = key,
                    Value = value,
                    LastAccess = DateTime.UtcNow,
                    Size = size
                };

                var listNode = lruList.AddFirst(newNode);
                cache[key] = listNode;
                Interlocked.Add(ref currentMemoryUsage, size);

                // Evict if necessary
                EvictIfNecessary();
            }
            finally
            {
                lockSlim.ExitWriteLock();
            }
        }

        /// <summary>
        /// Removes an item from the cache.
        /// </summary>
        /// <param name="key">The key to remove.</param>
        /// <returns>True if the item was removed.</returns>
        public bool Remove(TKey key)
        {
            lockSlim.EnterWriteLock();
            try
            {
                if (cache.TryGetValue(key, out var node))
                {
                    cache.Remove(key);
                    lruList.Remove(node);
                    Interlocked.Add(ref currentMemoryUsage, -node.Value.Size);
                    return true;
                }
                return false;
            }
            finally
            {
                lockSlim.ExitWriteLock();
            }
        }

        /// <summary>
        /// Clears all items from the cache.
        /// </summary>
        public void Clear()
        {
            lockSlim.EnterWriteLock();
            try
            {
                cache.Clear();
                lruList.Clear();
                Interlocked.Exchange(ref currentMemoryUsage, 0);
                Interlocked.Exchange(ref hitCount, 0);
                Interlocked.Exchange(ref missCount, 0);
                Interlocked.Exchange(ref evictionCount, 0);
            }
            finally
            {
                lockSlim.ExitWriteLock();
            }
        }

        /// <summary>
        /// Gets cache statistics.
        /// </summary>
        public CacheStatistics GetStatistics()
        {
            lockSlim.EnterReadLock();
            try
            {
                var total = hitCount + missCount;
                return new CacheStatistics
                {
                    ItemCount = cache.Count,
                    HitCount = hitCount,
                    MissCount = missCount,
                    EvictionCount = evictionCount,
                    HitRate = total > 0 ? (double)hitCount / total : 0,
                    MemoryUsageBytes = currentMemoryUsage,
                    MaxMemoryBytes = maxMemoryBytes,
                    MaxSize = maxSize
                };
            }
            finally
            {
                lockSlim.ExitReadLock();
            }
        }

        /// <summary>
        /// Trims the cache to a specific size.
        /// </summary>
        /// <param name="targetSize">Target number of items.</param>
        public void Trim(int targetSize)
        {
            lockSlim.EnterWriteLock();
            try
            {
                while (cache.Count > targetSize && lruList.Count > 0)
                {
                    EvictLeastRecentlyUsed();
                }
            }
            finally
            {
                lockSlim.ExitWriteLock();
            }
        }

        /// <summary>
        /// Removes items that haven't been accessed within the specified timespan.
        /// </summary>
        /// <param name="maxAge">Maximum age of items to keep.</param>
        public void RemoveStale(TimeSpan maxAge)
        {
            var cutoffTime = DateTime.UtcNow - maxAge;
            
            lockSlim.EnterWriteLock();
            try
            {
                var nodesToRemove = new List<LinkedListNode<CacheNode>>();
                
                // Start from the end (least recently used)
                var current = lruList.Last;
                while (current != null)
                {
                    if (current.Value.LastAccess < cutoffTime)
                    {
                        nodesToRemove.Add(current);
                        current = current.Previous;
                    }
                    else
                    {
                        break; // All remaining items are newer
                    }
                }

                foreach (var node in nodesToRemove)
                {
                    cache.Remove(node.Value.Key);
                    lruList.Remove(node);
                    Interlocked.Add(ref currentMemoryUsage, -node.Value.Size);
                    Interlocked.Increment(ref evictionCount);
                }
            }
            finally
            {
                lockSlim.ExitWriteLock();
            }
        }

        private void EvictIfNecessary()
        {
            // Note: Must be called while holding write lock
            
            // Check count limit
            while (cache.Count > maxSize && lruList.Count > 0)
            {
                EvictLeastRecentlyUsed();
            }

            // Check memory limit
            if (maxMemoryBytes > 0)
            {
                while (currentMemoryUsage > maxMemoryBytes && lruList.Count > 0)
                {
                    EvictLeastRecentlyUsed();
                }
            }
        }

        private void EvictLeastRecentlyUsed()
        {
            // Note: Must be called while holding write lock
            var node = lruList.Last;
            if (node != null)
            {
                cache.Remove(node.Value.Key);
                lruList.RemoveLast();
                Interlocked.Add(ref currentMemoryUsage, -node.Value.Size);
                Interlocked.Increment(ref evictionCount);
                
                Debug.Log($"Evicted cache item: {node.Value.Key}");
            }
        }

        public void Dispose()
        {
            lockSlim?.Dispose();
        }
    }

    /// <summary>
    /// Cache statistics.
    /// </summary>
    public class CacheStatistics
    {
        public int ItemCount { get; set; }
        public long HitCount { get; set; }
        public long MissCount { get; set; }
        public long EvictionCount { get; set; }
        public double HitRate { get; set; }
        public long MemoryUsageBytes { get; set; }
        public long MaxMemoryBytes { get; set; }
        public int MaxSize { get; set; }

        public override string ToString()
        {
            return $"Cache Stats: {ItemCount} items, {HitRate:P1} hit rate, " +
                   $"{MemoryUsageBytes / 1024.0 / 1024.0:F2} MB used";
        }
    }
}