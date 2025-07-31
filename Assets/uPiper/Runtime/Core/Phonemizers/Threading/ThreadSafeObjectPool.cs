using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

namespace uPiper.Core.Phonemizers.Threading
{
    /// <summary>
    /// Thread-safe object pool for reusing expensive objects.
    /// </summary>
    /// <typeparam name="T">Type of objects to pool.</typeparam>
    public class ThreadSafeObjectPool<T> : IDisposable where T : class
    {
        private readonly ConcurrentBag<T> pool;
        private readonly Func<T> objectFactory;
        private readonly Action<T> resetAction;
        private readonly Action<T> destroyAction;
        private readonly int maxSize;
        private int currentSize;
        private bool isDisposed;

        /// <summary>
        /// Gets the number of objects currently in the pool.
        /// </summary>
        public int AvailableCount => pool.Count;

        /// <summary>
        /// Gets the total number of objects created.
        /// </summary>
        public int TotalCount => currentSize;

        /// <summary>
        /// Creates a new thread-safe object pool.
        /// </summary>
        /// <param name="objectFactory">Factory function to create new objects.</param>
        /// <param name="resetAction">Action to reset an object before returning to pool.</param>
        /// <param name="destroyAction">Action to destroy an object when disposing.</param>
        /// <param name="maxSize">Maximum number of objects to keep in pool.</param>
        public ThreadSafeObjectPool(
            Func<T> objectFactory,
            Action<T> resetAction = null,
            Action<T> destroyAction = null,
            int maxSize = 100)
        {
            this.objectFactory = objectFactory ?? throw new ArgumentNullException(nameof(objectFactory));
            this.resetAction = resetAction;
            this.destroyAction = destroyAction;
            this.maxSize = maxSize;
            pool = new ConcurrentBag<T>();
            currentSize = 0;
        }

        /// <summary>
        /// Gets an object from the pool or creates a new one.
        /// </summary>
        /// <returns>An object instance.</returns>
        public T Rent()
        {
            ThrowIfDisposed();

            if (pool.TryTake(out var item))
            {
                return item;
            }

            // Create new object
            Interlocked.Increment(ref currentSize);
            try
            {
                return objectFactory();
            }
            catch
            {
                Interlocked.Decrement(ref currentSize);
                throw;
            }
        }

        /// <summary>
        /// Returns an object to the pool.
        /// </summary>
        /// <param name="item">The object to return.</param>
        public void Return(T item)
        {
            if (item == null || isDisposed)
                return;

            // Reset the object
            try
            {
                resetAction?.Invoke(item);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error resetting pooled object: {ex.Message}");
                // Don't return corrupted object to pool
                DestroyObject(item);
                Interlocked.Decrement(ref currentSize);
                return;
            }

            // Check pool size
            if (pool.Count < maxSize)
            {
                pool.Add(item);
            }
            else
            {
                // Pool is full, destroy the object
                DestroyObject(item);
                Interlocked.Decrement(ref currentSize);
            }
        }

        /// <summary>
        /// Clears all objects from the pool.
        /// </summary>
        public void Clear()
        {
            while (pool.TryTake(out var item))
            {
                DestroyObject(item);
                Interlocked.Decrement(ref currentSize);
            }
        }

        /// <summary>
        /// Preallocates objects in the pool.
        /// </summary>
        /// <param name="count">Number of objects to preallocate.</param>
        public void Preallocate(int count)
        {
            ThrowIfDisposed();

            count = Math.Min(count, maxSize - pool.Count);
            for (var i = 0; i < count; i++)
            {
                try
                {
                    var item = objectFactory();
                    Interlocked.Increment(ref currentSize);
                    Return(item);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to preallocate object: {ex.Message}");
                    break;
                }
            }
        }

        /// <summary>
        /// Disposes of the pool and all pooled objects.
        /// </summary>
        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;
            Clear();
        }

        private void DestroyObject(T item)
        {
            try
            {
                destroyAction?.Invoke(item);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error destroying pooled object: {ex.Message}");
            }

            if (item is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error disposing pooled object: {ex.Message}");
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (isDisposed)
                throw new ObjectDisposedException(GetType().Name);
        }
    }

    /// <summary>
    /// Pool policy for creating and managing objects.
    /// </summary>
    /// <typeparam name="T">Type of objects to pool.</typeparam>
    public class PooledObjectPolicy<T> where T : class
    {
        /// <summary>
        /// Creates a new object.
        /// </summary>
        public Func<T> Create { get; set; }

        /// <summary>
        /// Resets an object before returning to pool.
        /// </summary>
        public Action<T> Reset { get; set; }

        /// <summary>
        /// Destroys an object when removing from pool.
        /// </summary>
        public Action<T> Destroy { get; set; }

        /// <summary>
        /// Maximum number of objects to keep in pool.
        /// </summary>
        public int MaxSize { get; set; } = 100;
    }

    /// <summary>
    /// Scoped wrapper for automatically returning objects to pool.
    /// </summary>
    /// <typeparam name="T">Type of pooled object.</typeparam>
    public struct PooledObject<T> : IDisposable where T : class
    {
        private readonly ThreadSafeObjectPool<T> pool;
        private T value;

        /// <summary>
        /// Gets the pooled object value.
        /// </summary>
        public readonly T Value => value;

        public PooledObject(ThreadSafeObjectPool<T> pool, T value)
        {
            this.pool = pool;
            this.value = value;
        }

        /// <summary>
        /// Returns the object to the pool.
        /// </summary>
        public void Dispose()
        {
            if (value != null && pool != null)
            {
                pool.Return(value);
                value = null;
            }
        }
    }

    /// <summary>
    /// Extension methods for object pools.
    /// </summary>
    public static class ObjectPoolExtensions
    {
        /// <summary>
        /// Rents an object from the pool with automatic return on dispose.
        /// </summary>
        public static PooledObject<T> RentScoped<T>(this ThreadSafeObjectPool<T> pool) where T : class
        {
            var obj = pool.Rent();
            return new PooledObject<T>(pool, obj);
        }
    }
}