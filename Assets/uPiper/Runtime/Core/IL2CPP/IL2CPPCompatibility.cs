using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace uPiper.Core.IL2CPP
{
    /// <summary>
    /// IL2CPP compatibility helpers and attributes
    /// </summary>
    public static class IL2CPPCompatibility
    {
        /// <summary>
        /// Ensures that generic types are properly instantiated for IL2CPP
        /// </summary>
        [Preserve]
        public static class GenericTypePreservation
        {
            [Preserve]
            static GenericTypePreservation()
            {
                // Force instantiation of generic types used by uPiper
                PreserveCollectionTypes();
                PreserveTaskTypes();
                PreservePhonemizerTypes();
            }

            [Preserve]
            private static void PreserveCollectionTypes()
            {
                // Dictionary types
                _ = new System.Collections.Generic.Dictionary<string, AudioClip>();
                _ = new System.Collections.Generic.Dictionary<string, byte[]>();
                _ = new System.Collections.Generic.Dictionary<string, PiperVoiceConfig>();
                _ = new System.Collections.Generic.Dictionary<string, Unity.InferenceEngine.Model>();
                _ = new System.Collections.Generic.Dictionary<string, Unity.InferenceEngine.Worker>();

                // List types
                _ = new System.Collections.Generic.List<AudioChunk>();
                _ = new System.Collections.Generic.List<string>();
                _ = new System.Collections.Generic.List<float>();
                _ = new System.Collections.Generic.List<int>();

                // Queue types
                _ = new System.Collections.Generic.Queue<Unity.InferenceEngine.Worker>();
                _ = new System.Collections.Generic.Queue<AudioChunk>();

                // LinkedList for LRU cache
                _ = new System.Collections.Generic.LinkedList<string>();
            }

            [Preserve]
            private static void PreserveTaskTypes()
            {
                // Task types
                _ = typeof(System.Threading.Tasks.Task<AudioClip>);
                _ = typeof(System.Threading.Tasks.Task<bool>);
                _ = typeof(System.Threading.Tasks.Task<PiperVoiceConfig>);
                _ = typeof(System.Threading.Tasks.TaskCompletionSource<AudioClip>);

                // Async enumerable (if available)
#if UNITY_2023_1_OR_NEWER
                _ = typeof(System.Collections.Generic.IAsyncEnumerable<AudioChunk>);
                _ = typeof(System.Collections.Generic.IAsyncEnumerator<AudioChunk>);
#endif
            }

            [Preserve]
            private static void PreservePhonemizerTypes()
            {
                // Phonemizer result types
                _ = typeof(Phonemizers.Backend.PhonemeResult);
                _ = typeof(Phonemizers.Cache.CacheItem<string, Phonemizers.Backend.PhonemeResult>);
                _ = typeof(Phonemizers.Cache.LRUCache<string, Phonemizers.Backend.PhonemeResult>);
            }
        }

        /// <summary>
        /// Platform-specific IL2CPP settings
        /// </summary>
        public static class PlatformSettings
        {
            /// <summary>
            /// Check if running under IL2CPP
            /// </summary>
            public static bool IsIL2CPP
            {
                get
                {
#if ENABLE_IL2CPP
                    return true;
#else
                    return false;
#endif
                }
            }

            /// <summary>
            /// Get recommended worker thread count for IL2CPP
            /// </summary>
            public static int GetRecommendedWorkerThreads()
            {
                if (!IsIL2CPP)
                {
                    // Mono can handle more threads efficiently
                    return Mathf.Max(2, SystemInfo.processorCount - 1);
                }
                else
                {
                    // IL2CPP benefits from fewer threads due to marshaling overhead
                    return Mathf.Min(2, SystemInfo.processorCount);
                }
            }

            /// <summary>
            /// Get recommended cache size for IL2CPP
            /// </summary>
            public static int GetRecommendedCacheSizeMB()
            {
                if (!IsIL2CPP)
                {
                    return 100; // Default for Mono
                }
                else
                {
                    // IL2CPP may have different memory characteristics
                    return Application.platform switch
                    {
                        RuntimePlatform.Android => 50,
                        RuntimePlatform.IPhonePlayer => 50,
                        RuntimePlatform.WebGLPlayer => 25,
                        _ => 100
                    };
                }
            }
        }

        /// <summary>
        /// IL2CPP-safe marshaling helpers
        /// </summary>
        public static class MarshallingHelpers
        {
            /// <summary>
            /// Safely marshal a string for P/Invoke with IL2CPP
            /// </summary>
            [Preserve]
            public static IntPtr StringToHGlobalUTF8(string str)
            {
                if (string.IsNullOrEmpty(str))
                    return IntPtr.Zero;

                var bytes = System.Text.Encoding.UTF8.GetBytes(str);
                var ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(bytes.Length + 1);
                System.Runtime.InteropServices.Marshal.Copy(bytes, 0, ptr, bytes.Length);
                System.Runtime.InteropServices.Marshal.WriteByte(ptr, bytes.Length, 0); // null terminator
                return ptr;
            }

            /// <summary>
            /// Free memory allocated by StringToHGlobalUTF8
            /// </summary>
            [Preserve]
            public static void FreeHGlobalUTF8(IntPtr ptr)
            {
                if (ptr != IntPtr.Zero)
                {
                    System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
                }
            }
        }
    }
}