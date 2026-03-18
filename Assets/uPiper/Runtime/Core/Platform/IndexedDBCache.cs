using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

namespace uPiper.Core.Platform
{
    /// <summary>
    /// Provides IndexedDB-based caching for WebGL builds.
    /// On non-WebGL platforms, all operations are no-ops.
    /// </summary>
    public static class IndexedDBCache
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void IndexedDB_Store(string key, byte[] data, int dataLength, string version,
            int callbackId);

        [DllImport("__Internal")]
        private static extern void IndexedDB_Load(string key, int callbackId);

        [DllImport("__Internal")]
        private static extern void IndexedDB_HasKey(string key, string version, int callbackId);

        [DllImport("__Internal")]
        private static extern void IndexedDB_Delete(string key);
#endif

        // NOTE: No locking needed. WebGL is single-threaded (no Task.Run, no thread pool).
        // All access to _nextCallbackId and the dictionaries occurs on the main thread.
        private static int _nextCallbackId;
        private static readonly Dictionary<int, TaskCompletionSource<bool>> StoreTasks = new();
        private static readonly Dictionary<int, TaskCompletionSource<byte[]>> LoadTasks = new();
        private static readonly Dictionary<int, TaskCompletionSource<bool>> HasKeyTasks = new();
        private static bool _receiverInitialized;

        /// <summary>
        /// Stores binary data in IndexedDB with a version string.
        /// </summary>
        /// <param name="key">Cache key (e.g. "sys.dic")</param>
        /// <param name="data">Binary data to store</param>
        /// <param name="version">Version string for cache invalidation</param>
        public static Task StoreAsync(string key, byte[] data, string version)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            EnsureReceiverExists();
            var id = _nextCallbackId++;
            var tcs = new TaskCompletionSource<bool>();
            StoreTasks[id] = tcs;
            IndexedDB_Store(key, data, data.Length, version, id);
            return tcs.Task;
#else
            return Task.CompletedTask;
#endif
        }

        /// <summary>
        /// Loads binary data from IndexedDB.
        /// Returns null if the key does not exist.
        /// </summary>
        /// <param name="key">Cache key</param>
        public static Task<byte[]> LoadAsync(string key)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            EnsureReceiverExists();
            var id = _nextCallbackId++;
            var tcs = new TaskCompletionSource<byte[]>();
            LoadTasks[id] = tcs;
            IndexedDB_Load(key, id);
            return tcs.Task;
#else
            return Task.FromResult<byte[]>(null);
#endif
        }

        /// <summary>
        /// Checks if a key exists in IndexedDB with the specified version.
        /// </summary>
        /// <param name="key">Cache key</param>
        /// <param name="version">Expected version string</param>
        public static Task<bool> HasKeyAsync(string key, string version)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            EnsureReceiverExists();
            var id = _nextCallbackId++;
            var tcs = new TaskCompletionSource<bool>();
            HasKeyTasks[id] = tcs;
            IndexedDB_HasKey(key, version, id);
            return tcs.Task;
#else
            return Task.FromResult(false);
#endif
        }

        /// <summary>
        /// Deletes a key from IndexedDB.
        /// </summary>
        /// <param name="key">Cache key to delete</param>
        public static void Delete(string key)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            IndexedDB_Delete(key);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private static void EnsureReceiverExists()
        {
            if (_receiverInitialized)
                return;

            var go = new GameObject("IndexedDBCallbackReceiver");
            go.AddComponent<IndexedDBCallbackReceiver>();
            UnityEngine.Object.DontDestroyOnLoad(go);
            _receiverInitialized = true;
        }
#endif

        internal static void HandleStoreComplete(int callbackId)
        {
            if (StoreTasks.TryGetValue(callbackId, out var tcs))
            {
                StoreTasks.Remove(callbackId);
                tcs.TrySetResult(true);
            }
        }

        internal static void HandleStoreError(int callbackId, string error)
        {
            if (StoreTasks.TryGetValue(callbackId, out var tcs))
            {
                StoreTasks.Remove(callbackId);
                tcs.TrySetException(new InvalidOperationException($"IndexedDB Store failed: {error}"));
            }
        }

        internal static void HandleLoadComplete(int callbackId, IntPtr dataPtr, int dataLength)
        {
            if (LoadTasks.TryGetValue(callbackId, out var tcs))
            {
                LoadTasks.Remove(callbackId);
                if (dataPtr == IntPtr.Zero || dataLength == 0)
                {
                    tcs.TrySetResult(null);
                }
                else
                {
                    var data = new byte[dataLength];
                    Marshal.Copy(dataPtr, data, 0, dataLength);
                    tcs.TrySetResult(data);
                }
            }
        }

        internal static void HandleLoadError(int callbackId, string error)
        {
            if (LoadTasks.TryGetValue(callbackId, out var tcs))
            {
                LoadTasks.Remove(callbackId);
                tcs.TrySetException(new InvalidOperationException($"IndexedDB Load failed: {error}"));
            }
        }

        internal static void HandleHasKeyComplete(int callbackId, bool hasKey)
        {
            if (HasKeyTasks.TryGetValue(callbackId, out var tcs))
            {
                HasKeyTasks.Remove(callbackId);
                tcs.TrySetResult(hasKey);
            }
        }
    }

    /// <summary>
    /// MonoBehaviour that receives SendMessage callbacks from the IndexedDB jslib plugin.
    /// </summary>
    internal class IndexedDBCallbackReceiver : MonoBehaviour
    {
        public void OnStoreComplete(string callbackIdStr)
        {
            if (int.TryParse(callbackIdStr, out var callbackId))
            {
                IndexedDBCache.HandleStoreComplete(callbackId);
            }
        }

        public void OnStoreError(string message)
        {
            var parts = message.Split('|', 2);
            if (parts.Length >= 2 && int.TryParse(parts[0], out var callbackId))
            {
                IndexedDBCache.HandleStoreError(callbackId, parts[1]);
            }
        }

        public void OnLoadComplete(string message)
        {
            var parts = message.Split('|');
            if (parts.Length >= 3 && int.TryParse(parts[0], out var callbackId))
            {
                if (long.TryParse(parts[1], out var ptrValue) && int.TryParse(parts[2], out var length))
                {
                    var ptr = new IntPtr(ptrValue);
                    IndexedDBCache.HandleLoadComplete(callbackId, ptr, length);
                }
                else
                {
                    IndexedDBCache.HandleLoadError(callbackId, $"Invalid callback format: {message}");
                }
            }
        }

        public void OnLoadError(string message)
        {
            var parts = message.Split('|', 2);
            if (parts.Length >= 2 && int.TryParse(parts[0], out var callbackId))
            {
                IndexedDBCache.HandleLoadError(callbackId, parts[1]);
            }
        }

        public void OnHasKeyComplete(string message)
        {
            var parts = message.Split('|');
            if (parts.Length >= 2 && int.TryParse(parts[0], out var callbackId))
            {
                var hasKey = parts[1] == "1";
                IndexedDBCache.HandleHasKeyComplete(callbackId, hasKey);
            }
        }
    }
}