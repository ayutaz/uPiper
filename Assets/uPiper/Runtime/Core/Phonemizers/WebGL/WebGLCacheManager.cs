#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace uPiper.Core.Phonemizers.WebGL
{
    /// <summary>
    /// Manages IndexedDB caching for WebGL platform
    /// </summary>
    public static class WebGLCacheManager
    {
        // IndexedDB JavaScript functions
        [DllImport("__Internal")]
        private static extern bool InitializeIndexedDBCache();

        [DllImport("__Internal")]
        private static extern bool CachePhonemes(string key, string phonemesJson);

        [DllImport("__Internal")]
        private static extern IntPtr GetCachedPhonemes(string key);

        [DllImport("__Internal")]
        private static extern bool ClearOldCache(string storeName, int maxAgeMs);

        [DllImport("__Internal")]
        private static extern IntPtr GetCacheStats();

        private static bool isInitialized = false;
        
        /// <summary>
        /// Initialize the IndexedDB cache system
        /// </summary>
        public static bool Initialize()
        {
            if (isInitialized)
            {
                return true;
            }

            try
            {
                bool success = InitializeIndexedDBCache();
                if (success)
                {
                    isInitialized = true;
                    Debug.Log("[WebGLCacheManager] IndexedDB cache initialized");
                }
                else
                {
                    Debug.LogError("[WebGLCacheManager] Failed to initialize IndexedDB cache");
                }
                return success;
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebGLCacheManager] Error initializing cache: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Cache phonemes for a given text key
        /// </summary>
        public static bool CachePhonemesForText(string text, string language, string[] phonemes)
        {
            if (!isInitialized)
            {
                Debug.LogWarning("[WebGLCacheManager] Cache not initialized");
                return false;
            }

            try
            {
                string key = GenerateCacheKey(text, language);
                string phonemesJson = JsonUtility.ToJson(new PhonemeArray { phonemes = phonemes });
                return CachePhonemes(key, phonemesJson);
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebGLCacheManager] Error caching phonemes: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Retrieve cached phonemes for a given text
        /// </summary>
        public static string[] GetCachedPhonemesForText(string text, string language)
        {
            if (!isInitialized)
            {
                return null;
            }

            try
            {
                string key = GenerateCacheKey(text, language);
                IntPtr resultPtr = GetCachedPhonemes(key);
                
                if (resultPtr == IntPtr.Zero)
                {
                    return null;
                }

                string resultJson = Marshal.PtrToStringUTF8(resultPtr);
                WebGLInterop.FreeWebGLMemory(resultPtr);

                var result = JsonUtility.FromJson<CachedPhonemeResult>(resultJson);
                
                if (result.found)
                {
                    return result.phonemes;
                }

                return null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebGLCacheManager] Error retrieving cached phonemes: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Clear old cache entries
        /// </summary>
        public static void ClearOldEntries(int maxAgeDays = 30)
        {
            if (!isInitialized)
            {
                return;
            }

            try
            {
                int maxAgeMs = maxAgeDays * 24 * 60 * 60 * 1000;
                ClearOldCache("phonemeCache", maxAgeMs);
                ClearOldCache("audioCache", maxAgeMs);
                Debug.Log($"[WebGLCacheManager] Cleared cache entries older than {maxAgeDays} days");
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebGLCacheManager] Error clearing old cache: {e.Message}");
            }
        }

        /// <summary>
        /// Get cache statistics
        /// </summary>
        public static CacheStats GetStats()
        {
            if (!isInitialized)
            {
                return new CacheStats();
            }

            try
            {
                IntPtr resultPtr = GetCacheStats();
                if (resultPtr == IntPtr.Zero)
                {
                    return new CacheStats();
                }

                string resultJson = Marshal.PtrToStringUTF8(resultPtr);
                WebGLInterop.FreeWebGLMemory(resultPtr);

                return JsonUtility.FromJson<CacheStats>(resultJson);
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebGLCacheManager] Error getting cache stats: {e.Message}");
                return new CacheStats();
            }
        }

        /// <summary>
        /// Generate a cache key from text and language
        /// </summary>
        private static string GenerateCacheKey(string text, string language)
        {
            // Simple hash-based key generation
            int hash = 17;
            hash = hash * 31 + text.GetHashCode();
            hash = hash * 31 + language.GetHashCode();
            return $"{language}_{hash:X8}";
        }

        [Serializable]
        private class PhonemeArray
        {
            public string[] phonemes;
        }

        [Serializable]
        private class CachedPhonemeResult
        {
            public bool found;
            public string[] phonemes;
        }

        [Serializable]
        public class CacheStats
        {
            public int phonemeCount;
            public int audioCount;
            public int modelCount;
            public long totalSize;
            public string error;
        }
    }
}
#endif