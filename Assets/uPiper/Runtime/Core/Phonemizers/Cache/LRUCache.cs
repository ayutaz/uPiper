using System;
using System.Collections.Generic;
using System.Threading;
using uPiper.Core.Logging;

namespace uPiper.Core.Phonemizers.Cache
{
    /// <summary>
    /// Thread-safe Least Recently Used (LRU) cache implementation.
    /// </summary>
    /// <typeparam name="TKey">The type of cache key.</typeparam>
    /// <typeparam name="TValue">The type of cached value.</typeparam>
    public class LRUCache<TKey, TValue> : ICache<TKey, TValue>
    {
        private readonly int _capacity;
        private readonly Dictionary<TKey, LinkedListNode<CacheItem<TKey, TValue>>> _cacheMap;
        private readonly LinkedList<CacheItem<TKey, TValue>> _lruList;
        private readonly ReaderWriterLockSlim _lock;
        private bool _disposed;

        /// <summary>
        /// Gets the maximum capacity of the cache.
        /// </summary>
        public int Capacity => _capacity;

        /// <summary>
        /// Gets the current number of items in the cache.
        /// </summary>
        public int Count
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return _cacheMap.Count;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }

        /// <summary>
        /// Creates a new LRU cache with specified capacity.
        /// </summary>
        /// <param name="capacity">Maximum number of items to store.</param>
        public LRUCache(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentException("Capacity must be greater than 0", nameof(capacity));
            }

            _capacity = capacity;
            _cacheMap = new Dictionary<TKey, LinkedListNode<CacheItem<TKey, TValue>>>(capacity);
            _lruList = new LinkedList<CacheItem<TKey, TValue>>();
            _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        }

        /// <summary>
        /// Tries to get a value from the cache.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <param name="value">The cached value if found.</param>
        /// <returns>True if the value was found in cache.</returns>
        public bool TryGet(TKey key, out TValue value)
        {
            if (key == null)
            {
                value = default;
                return false;
            }

            _lock.EnterUpgradeableReadLock();
            try
            {
                if (_cacheMap.TryGetValue(key, out var node))
                {
                    _lock.EnterWriteLock();
                    try
                    {
                        // Move to front (most recently used)
                        _lruList.Remove(node);
                        _lruList.AddFirst(node);
                        node.Value.RecordAccess();
                    }
                    finally
                    {
                        _lock.ExitWriteLock();
                    }

                    value = node.Value.Value;
                    return true;
                }

                value = default;
                return false;
            }
            finally
            {
                _lock.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Adds or updates a value in the cache.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <param name="value">The value to cache.</param>
        public void Add(TKey key, TValue value)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            _lock.EnterWriteLock();
            try
            {
                if (_cacheMap.TryGetValue(key, out var existingNode))
                {
                    // Update existing item
                    _lruList.Remove(existingNode);
                    existingNode.Value.Value = value;
                    existingNode.Value.RecordAccess();
                    _lruList.AddFirst(existingNode);
                }
                else
                {
                    // Add new item
                    var cacheItem = new CacheItem<TKey, TValue>(key, value);
                    var newNode = new LinkedListNode<CacheItem<TKey, TValue>>(cacheItem);
                    _lruList.AddFirst(newNode);
                    _cacheMap[key] = newNode;

                    // Remove least recently used if at capacity
                    if (_cacheMap.Count > _capacity)
                    {
                        var lru = _lruList.Last;
                        _lruList.RemoveLast();
                        _cacheMap.Remove(lru.Value.Key);

                        PiperLogger.LogDebug($"LRU Cache evicted item with key: {lru.Value.Key}");
                    }
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Removes a specific item from the cache.
        /// </summary>
        /// <param name="key">The cache key to remove.</param>
        /// <returns>True if the item was removed.</returns>
        public bool Remove(TKey key)
        {
            if (key == null)
            {
                return false;
            }

            _lock.EnterWriteLock();
            try
            {
                if (_cacheMap.TryGetValue(key, out var node))
                {
                    _lruList.Remove(node);
                    _cacheMap.Remove(key);
                    return true;
                }
                return false;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Clears all items from the cache.
        /// </summary>
        public void Clear()
        {
            _lock.EnterWriteLock();
            try
            {
                _cacheMap.Clear();
                _lruList.Clear();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Checks if a key exists in the cache.
        /// </summary>
        /// <param name="key">The cache key to check.</param>
        /// <returns>True if the key exists in cache.</returns>
        public bool ContainsKey(TKey key)
        {
            if (key == null)
            {
                return false;
            }

            _lock.EnterReadLock();
            try
            {
                return _cacheMap.ContainsKey(key);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Gets cache statistics for monitoring.
        /// </summary>
        /// <returns>Dictionary of statistics.</returns>
        public Dictionary<string, object> GetStatistics()
        {
            _lock.EnterReadLock();
            try
            {
                var stats = new Dictionary<string, object>
                {
                    ["Count"] = _cacheMap.Count,
                    ["Capacity"] = _capacity,
                    ["FillRate"] = (double)_cacheMap.Count / _capacity
                };

                if (_lruList.First != null)
                {
                    stats["MostRecentKey"] = _lruList.First.Value.Key;
                }

                if (_lruList.Last != null)
                {
                    stats["LeastRecentKey"] = _lruList.Last.Value.Key;
                }

                return stats;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Disposes the cache and releases resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes the cache.
        /// </summary>
        /// <param name="disposing">True if disposing managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    Clear();
                    _lock?.Dispose();
                }
                _disposed = true;
            }
        }
    }
}