#if UNITY_WEBGL && !UNITY_EDITOR
using System;
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
        private bool isInitialized = false;
        private readonly object initLock = new object();

        public override string Name => "WebGL OpenJTalk";
        public override string Version => "1.0.0";
        public override string License => "BSD-3-Clause";
        public override string[] SupportedLanguages => new[] { "ja", "ja-JP" };

        protected override async Task<bool> InitializeInternalAsync(
            PhonemizerBackendOptions options,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
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
                            // Async initialization in progress, wait for module to load
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
                                    isInitialized = true;
                                    Debug.Log("[WebGLOpenJTalkPhonemizer] Initialization successful");
                                    return true;
                                }
                            }
                            
                            Debug.LogError("[WebGLOpenJTalkPhonemizer] Dictionary initialization timeout");
                            return false;
                        }
                        else
                        {
                            Debug.LogError("[WebGLOpenJTalkPhonemizer] Initialization failed");
                            return false;
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[WebGLOpenJTalkPhonemizer] Initialization error: {e.Message}");
                        return false;
                    }
                }
            }, cancellationToken);
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

            return await Task.Run(() =>
            {
                try
                {
                    // Check cache first
                    var cachedPhonemes = WebGLCacheManager.GetCachedPhonemesForText(text, language);
                    if (cachedPhonemes != null)
                    {
                        Debug.Log($"[WebGLOpenJTalkPhonemizer] Using cached phonemes for text: {text.Substring(0, Math.Min(20, text.Length))}...");
                        return new PhonemeResult
                        {
                            Phonemes = cachedPhonemes,
                            Language = language,
                            Success = true
                        };
                    }

                    // Call JavaScript function
                    IntPtr resultPtr = WebGLInterop.PhonemizeJapaneseText(text);
                    var result = WebGLInterop.ParseJSONResult<WebGLInterop.PhonemeResult>(resultPtr);

                    if (!result.success)
                    {
                        Debug.LogError($"[WebGLOpenJTalkPhonemizer] Phonemization failed: {result.error}");
                        throw new Exception(result.error);
                    }

                    // Convert to PUA format if needed
                    var phonemes = ConvertToPUAFormat(result.phonemes);
                    
                    // Cache the result
                    WebGLCacheManager.CachePhonemesForText(text, language, phonemes);

                    return new PhonemeResult
                    {
                        Phonemes = phonemes,
                        Language = language,
                        Success = true
                    };
                }
                catch (Exception e)
                {
                    Debug.LogError($"[WebGLOpenJTalkPhonemizer] Error: {e.Message}");
                    throw;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Convert multi-character phonemes to PUA format
        /// </summary>
        private string[] ConvertToPUAFormat(string[] phonemes)
        {
            // The JavaScript side already converts to PUA format, so just return as is
            // This function is kept for consistency with the interface
            return phonemes;
        }

        public override long GetMemoryUsage()
        {
            // Estimate based on WebAssembly module size
            return 50 * 1024 * 1024; // 50MB estimate
        }

        public override BackendCapabilities GetCapabilities()
        {
            return new BackendCapabilities
            {
                SupportsIPA = false,
                SupportsStress = false,
                SupportsSyllables = false,
                SupportsTones = false,
                SupportsDuration = false,
                SupportsBatchProcessing = false,
                IsThreadSafe = false, // JavaScript is single-threaded
                RequiresNetwork = false
            };
        }

        protected override void DisposeInternal()
        {
            // WebAssembly resources are managed by the browser
            isInitialized = false;
        }
    }
}
#endif