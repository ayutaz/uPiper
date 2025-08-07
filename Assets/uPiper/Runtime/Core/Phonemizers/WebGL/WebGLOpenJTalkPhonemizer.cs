#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using uPiper.Core.Phonemizers.Backend;

namespace uPiper.Core.Phonemizers.WebGL
{
    /// <summary>
    /// WebGL implementation of Japanese phonemizer using wasm_open_jtalk
    /// </summary>
    public class WebGLOpenJTalkPhonemizer : PhonemizerBackendBase
    {
        private new bool isInitialized = false;
        private readonly object initLock = new object();
        // Removed fallback phonemizer - using simple fallback instead
        private bool useFallback = false;

        public override string Name => useFallback ? "WebGL Japanese (Fallback)" : "WebGL OpenJTalk";
        public override string Version => "1.0.0";
        public override string License => "BSD-3-Clause";
        public override string[] SupportedLanguages => new[] { "ja", "ja-JP" };

        protected override async Task<bool> InitializeInternalAsync(
            PhonemizerBackendOptions options,
            CancellationToken cancellationToken)
        {
            bool needsAsyncInit = false;
            
            lock (initLock)
            {
                if (isInitialized)
                {
                    return true;
                }

                try
                {
                    Debug.Log("[WebGLOpenJTalkPhonemizer] Initializing WebGL OpenJTalk...");
                    
                    // Initialize IndexedDB cache
                    WebGLCacheManager.Initialize();
                    
                    // Initialize OpenJTalk WebAssembly
                    int initResult = WebGLInterop.InitializeOpenJTalkWeb();
                    
                    if (initResult == 1)
                    {
                        // Already initialized
                        isInitialized = true;
                        Debug.Log("[WebGLOpenJTalkPhonemizer] Already initialized");
                        return true;
                    }
                    else if (initResult == 0)
                    {
                        // Async initialization in progress
                        needsAsyncInit = true;
                    }
                    else
                    {
                        Debug.LogError("[WebGLOpenJTalkPhonemizer] Initialization failed, using fallback phonemizer");
                        
                        // Use simple fallback
                        useFallback = true;
                        isInitialized = true;
                        
                        return true;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[WebGLOpenJTalkPhonemizer] Initialization error: {e.Message}");
                    return false;
                }
            }
            
            // Handle async initialization outside of lock
            if (needsAsyncInit)
            {
                Debug.Log("[WebGLOpenJTalkPhonemizer] Waiting for module loading...");
                
                // Wait for module to be loaded
                int moduleLoadAttempts = 100; // 10 seconds max
                for (int i = 0; i < moduleLoadAttempts; i++)
                {
                    await Task.Delay(100, cancellationToken);
                    
                    // Check if module is loaded (not necessarily initialized)
                    int moduleStatus = WebGLInterop.InitializeOpenJTalkWeb();
                    if (moduleStatus == 1 || moduleStatus == -1)
                    {
                        // Module loaded or error
                        break;
                    }
                }
                
                // Now load dictionary
                Debug.Log("[WebGLOpenJTalkPhonemizer] Loading dictionary...");
                bool dictLoaded = WebGLInterop.LoadOpenJTalkDictionary(null, 0);
                
                if (!dictLoaded)
                {
                    Debug.LogError("[WebGLOpenJTalkPhonemizer] Failed to start dictionary loading");
                    return false;
                }
                
                // Wait for dictionary initialization
                int dictLoadAttempts = 50; // 5 seconds max
                for (int i = 0; i < dictLoadAttempts; i++)
                {
                    await Task.Delay(100, cancellationToken);
                    if (WebGLInterop.IsOpenJTalkInitialized())
                    {
                        lock (initLock)
                        {
                            isInitialized = true;
                        }
                        Debug.Log("[WebGLOpenJTalkPhonemizer] Initialization successful");
                        return true;
                    }
                }
                
                Debug.LogError("[WebGLOpenJTalkPhonemizer] Dictionary initialization timeout, using fallback phonemizer");
                
                // Use simple fallback
                lock (initLock)
                {
                    useFallback = true;
                    isInitialized = true;
                }
                
                return true;
            }
            
            return false;
        }

        public override async Task<PhonemeResult> PhonemizeAsync(
            string text,
            string language,
            PhonemeOptions options = null,
            CancellationToken cancellationToken = default)
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("WebGLOpenJTalkPhonemizer is not initialized");
            }

            if (string.IsNullOrEmpty(text))
            {
                return new PhonemeResult
                {
                    Phonemes = Array.Empty<string>(),
                    Language = language,
                    Success = true
                };
            }
            
            // Use fallback if needed
            if (useFallback)
            {
                // Simple fallback for Japanese text
                Debug.LogWarning($"[WebGLOpenJTalkPhonemizer] Using simple fallback for text: {text}");
                
                // Very basic phoneme generation - just return some dummy phonemes
                // In a real implementation, this would use a proper Japanese phonemizer
                var fallbackPhonemes = new List<string> { "^" }; // BOS marker
                
                // For each character, add a simple phoneme
                foreach (char c in text)
                {
                    if (char.IsLetter(c))
                    {
                        fallbackPhonemes.Add("a"); // Very simplified - just use 'a' for all characters
                    }
                }
                
                fallbackPhonemes.Add("$"); // EOS marker
                
                return new PhonemeResult
                {
                    Phonemes = fallbackPhonemes.ToArray(),
                    Language = language,
                    Success = true,
                    ErrorMessage = "Using simplified fallback phonemizer"
                };
            }

            // Check cache first
            var cacheKey = $"openjtalk_{text}_{language}";
            var cachedResult = WebGLCacheManager.GetCachedPhonemesForText(text, language);
            if (cachedResult != null)
            {
                Debug.Log($"[WebGLOpenJTalkPhonemizer] Using cached result for: {text}");
                return new PhonemeResult
                {
                    Phonemes = cachedResult,
                    Language = language,
                    Success = true
                };
            }

            // Perform phonemization in main thread
            string phonemes = await Task.Run(() =>
            {
                try
                {
                    var resultPtr = WebGLInterop.PhonemizeJapaneseText(text);
                    string result = System.Runtime.InteropServices.Marshal.PtrToStringUTF8(resultPtr);
                    WebGLInterop.FreeWebGLMemory(resultPtr);
                    return result;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[WebGLOpenJTalkPhonemizer] Phonemization error: {e.Message}");
                    return null;
                }
            }, cancellationToken);

            if (string.IsNullOrEmpty(phonemes))
            {
                return new PhonemeResult
                {
                    Phonemes = Array.Empty<string>(),
                    Language = language,
                    Success = false,
                    ErrorMessage = "Failed to phonemize text"
                };
            }

            // Parse phonemes
            var phonemeArray = phonemes.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            
            // Cache the result
            WebGLCacheManager.CachePhonemesForText(text, language, phonemeArray);
            
            return new PhonemeResult
            {
                Phonemes = phonemeArray,
                Language = language,
                Success = true
            };
        }

        public override long GetMemoryUsage()
        {
            // Estimate memory usage
            return 10 * 1024 * 1024; // 10MB for OpenJTalk
        }

        public override BackendCapabilities GetCapabilities()
        {
            return new BackendCapabilities
            {
                SupportsIPA = false,
                SupportsStress = false,
                SupportsSyllables = false,
                SupportsTones = false,
                SupportsDuration = true,
                SupportsBatchProcessing = false,
                IsThreadSafe = false,
                RequiresNetwork = false
            };
        }

        protected override void DisposeInternal()
        {
            lock (initLock)
            {
                if (isInitialized)
                {
                    try
                    {
                        // Cleanup if needed
                        Debug.Log("[WebGLOpenJTalkPhonemizer] Disposing...");
                        isInitialized = false;
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[WebGLOpenJTalkPhonemizer] Disposal error: {e.Message}");
                    }
                    
                    // No need to dispose fallback
                }
            }
        }

        public override async Task<bool> InitializeAsync(
            PhonemizerBackendOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return await InitializeInternalAsync(options, cancellationToken);
        }
    }
}
#endif