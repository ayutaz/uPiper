using System;

namespace uPiper.Core.Phonemizers.Cache
{
    /// <summary>
    /// Represents an item stored in the cache.
    /// </summary>
    /// <typeparam name="TKey">The type of cache key.</typeparam>
    /// <typeparam name="TValue">The type of cached value.</typeparam>
    internal class CacheItem<TKey, TValue>
    {
        /// <summary>
        /// Gets or sets the cache key.
        /// </summary>
        public TKey Key { get; set; }

        /// <summary>
        /// Gets or sets the cached value.
        /// </summary>
        public TValue Value { get; set; }

        /// <summary>
        /// Gets or sets when the item was added to cache.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets when the item was last accessed.
        /// </summary>
        public DateTime LastAccessedAt { get; set; }

        /// <summary>
        /// Gets or sets the number of times this item has been accessed.
        /// </summary>
        public int AccessCount { get; set; }

        /// <summary>
        /// Gets or sets the size of this cache item in bytes (optional).
        /// </summary>
        public long SizeInBytes { get; set; }

        /// <summary>
        /// Creates a new cache item.
        /// </summary>
        /// <param name="key">The cache key.</param>
        /// <param name="value">The value to cache.</param>
        public CacheItem(TKey key, TValue value)
        {
            Key = key;
            Value = value;
            CreatedAt = DateTime.UtcNow;
            LastAccessedAt = CreatedAt;
            AccessCount = 0;
        }

        /// <summary>
        /// Updates the last accessed time and increments access count.
        /// </summary>
        public void RecordAccess()
        {
            LastAccessedAt = DateTime.UtcNow;
            AccessCount++;
        }

        /// <summary>
        /// Gets the age of this cache item.
        /// </summary>
        /// <returns>The time since the item was created.</returns>
        public TimeSpan GetAge()
        {
            return DateTime.UtcNow - CreatedAt;
        }

        /// <summary>
        /// Gets the time since this item was last accessed.
        /// </summary>
        /// <returns>The time since last access.</returns>
        public TimeSpan GetTimeSinceLastAccess()
        {
            return DateTime.UtcNow - LastAccessedAt;
        }
    }
}