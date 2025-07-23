using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Scripting;

namespace uPiper.Core.IL2CPP
{
    /// <summary>
    /// IL2CPP-compatible async enumerable implementation
    /// Provides fallback for platforms where IAsyncEnumerable might not be fully supported
    /// </summary>
    [Preserve]
    public class AsyncEnumerableCompat<T>
    {
        private readonly Func<CancellationToken, Task<Queue<T>>> _producer;
        private readonly int _bufferSize;

        [Preserve]
        public AsyncEnumerableCompat(Func<CancellationToken, Task<Queue<T>>> producer, int bufferSize = 10)
        {
            _producer = producer ?? throw new ArgumentNullException(nameof(producer));
            _bufferSize = Math.Max(1, bufferSize);
        }

        /// <summary>
        /// Convert to IAsyncEnumerable if supported, otherwise use compatibility mode
        /// </summary>
        [Preserve]
        public async IAsyncEnumerable<T> ToAsyncEnumerable([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
#if UNITY_2023_1_OR_NEWER && !ENABLE_IL2CPP
            // Use native IAsyncEnumerable on supported platforms
            var items = await _producer(cancellationToken);
            while (items.Count > 0 && !cancellationToken.IsCancellationRequested)
            {
                yield return items.Dequeue();
            }
#else
            // IL2CPP compatibility mode using TaskCompletionSource
            var items = await _producer(cancellationToken);
            foreach (var item in items)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                yield return item;
            }
#endif
        }

        /// <summary>
        /// Alternative method that returns items as a list for IL2CPP compatibility
        /// </summary>
        [Preserve]
        public async Task<List<T>> ToListAsync(CancellationToken cancellationToken = default)
        {
            var items = await _producer(cancellationToken);
            return new List<T>(items);
        }

        /// <summary>
        /// Process items with a callback for maximum compatibility
        /// </summary>
        [Preserve]
        public async Task ProcessAsync(Action<T> onItem, CancellationToken cancellationToken = default)
        {
            if (onItem == null)
                throw new ArgumentNullException(nameof(onItem));

            var items = await _producer(cancellationToken);
            while (items.Count > 0 && !cancellationToken.IsCancellationRequested)
            {
                onItem(items.Dequeue());
            }
        }
    }

    /// <summary>
    /// Helper methods for async enumerable operations
    /// </summary>
    [Preserve]
    public static class AsyncEnumerableHelpers
    {
        /// <summary>
        /// Create an IL2CPP-compatible async enumerable from an async generator function
        /// </summary>
        [Preserve]
        public static AsyncEnumerableCompat<T> Create<T>(Func<CancellationToken, Task<Queue<T>>> producer, int bufferSize = 10)
        {
            return new AsyncEnumerableCompat<T>(producer, bufferSize);
        }

        /// <summary>
        /// Convert IAsyncEnumerable to List for IL2CPP compatibility
        /// </summary>
        [Preserve]
        public static async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> source, CancellationToken cancellationToken = default)
        {
            var list = new List<T>();

#if UNITY_2023_1_OR_NEWER
            await foreach (var item in source.WithCancellation(cancellationToken))
            {
                list.Add(item);
            }
#else
            // Fallback for older Unity versions
            var enumerator = source.GetAsyncEnumerator(cancellationToken);
            try
            {
                while (await enumerator.MoveNextAsync())
                {
                    list.Add(enumerator.Current);
                }
            }
            finally
            {
                await enumerator.DisposeAsync();
            }
#endif

            return list;
        }

        /// <summary>
        /// Process async enumerable items with IL2CPP-safe callback
        /// </summary>
        [Preserve]
        public static async Task ProcessAsync<T>(IAsyncEnumerable<T> source, Action<T> onItem, CancellationToken cancellationToken = default)
        {
            if (onItem == null)
                throw new ArgumentNullException(nameof(onItem));

#if UNITY_2023_1_OR_NEWER
            await foreach (var item in source.WithCancellation(cancellationToken))
            {
                onItem(item);
            }
#else
            var enumerator = source.GetAsyncEnumerator(cancellationToken);
            try
            {
                while (await enumerator.MoveNextAsync())
                {
                    onItem(enumerator.Current);
                }
            }
            finally
            {
                await enumerator.DisposeAsync();
            }
#endif
        }
    }
}