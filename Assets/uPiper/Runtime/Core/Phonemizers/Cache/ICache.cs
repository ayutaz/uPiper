using System;

namespace uPiper.Core.Phonemizers.Cache
{
    /// <summary>
    /// Interface for cache implementations.
    /// </summary>
    /// <typeparam name="TKey">The type of cache key.</typeparam>
    /// <typeparam name="TValue">The type of cached value.</typeparam>
    public interface ICache<TKey, TValue> : IDisposable
    {
        /// <summary>
        /// Gets the maximum capacity of the cache.
        /// </summary>
        int Capacity { get; }

        /// <summary>
        /// Gets the current number of items in the cache.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Tries to get a value from the cache.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <param name="value">The cached value if found.</param>
        /// <returns>True if the value was found in cache.</returns>
        bool TryGet(TKey key, out TValue value);

        /// <summary>
        /// Adds or updates a value in the cache.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <param name="value">The value to cache.</param>
        void Add(TKey key, TValue value);

        /// <summary>
        /// Removes a specific item from the cache.
        /// </summary>
        /// <param name="key">The cache key to remove.</param>
        /// <returns>True if the item was removed.</returns>
        bool Remove(TKey key);

        /// <summary>
        /// Clears all items from the cache.
        /// </summary>
        void Clear();

        /// <summary>
        /// Checks if a key exists in the cache.
        /// </summary>
        /// <param name="key">The cache key to check.</param>
        /// <returns>True if the key exists in cache.</returns>
        bool ContainsKey(TKey key);
    }
}