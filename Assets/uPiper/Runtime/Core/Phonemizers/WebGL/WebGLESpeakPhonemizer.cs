#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using uPiper.Core.Phonemizers.Backend;

namespace uPiper.Core.Phonemizers.WebGL
{
    /// <summary>
    /// WebGL implementation of multilingual phonemizer using eSpeak-ng
    /// </summary>
    public class WebGLESpeakPhonemizer : PhonemizerBackendBase
    {
        private bool isInitialized = false;
        private readonly object initLock = new object();
        private string[] supportedLanguages = Array.Empty<string>();

        public override string Name => "WebGL eSpeak-ng";
        public override string Version => "1.0.0";
        public override string License => "GPL-3.0";
        public override string[] SupportedLanguages => supportedLanguages;

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
                    Debug.Log("[WebGLESpeakPhonemizer] Initializing WebGL eSpeak-ng...");
                    
                    // Initialize IndexedDB cache
                    WebGLCacheManager.Initialize();
                    
                    // Initialize eSpeak-ng WebAssembly
                    int initResult = WebGLInterop.InitializeESpeakWeb();
                    
                    if (initResult == 1)
                    {
                        // Already initialized
                        LoadSupportedLanguages();
                        isInitialized = true;
                        Debug.Log($"[WebGLESpeakPhonemizer] Already initialized. Supported languages: {string.Join(", ", supportedLanguages)}");
                        return true;
                    }
                    else if (initResult == 0)
                    {
                        // Async initialization in progress
                        needsAsyncInit = true;
                    }
                    else
                    {
                        Debug.LogError("[WebGLESpeakPhonemizer] Initialization failed");
                        return false;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[WebGLESpeakPhonemizer] Initialization error: {e.Message}");
                    return false;
                }
            }
            
            // Handle async initialization outside of lock
            if (needsAsyncInit)
            {
                Debug.Log("[WebGLESpeakPhonemizer] Waiting for async initialization...");
                
                // Poll for initialization completion
                int maxAttempts = 100; // 10 seconds max
                for (int i = 0; i < maxAttempts; i++)
                {
                    await Task.Delay(100, cancellationToken);
                    if (WebGLInterop.IsESpeakInitialized())
                    {
                        LoadSupportedLanguages();
                        lock (initLock)
                        {
                            isInitialized = true;
                        }
                        Debug.Log($"[WebGLESpeakPhonemizer] Initialization successful. Supported languages: {string.Join(", ", supportedLanguages)}");
                        return true;
                    }
                }
                
                Debug.LogError("[WebGLESpeakPhonemizer] Initialization timeout");
                return false;
            }
            
            return false;
        }

        private void LoadSupportedLanguages()
        {
            try
            {
                IntPtr resultPtr = WebGLInterop.GetESpeakSupportedLanguages();
                var languages = JsonUtility.FromJson<string[]>(Marshal.PtrToStringUTF8(resultPtr));
                WebGLInterop.FreeWebGLMemory(resultPtr);
                
                supportedLanguages = languages ?? Array.Empty<string>();
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebGLESpeakPhonemizer] Failed to load supported languages: {e.Message}");
                // Default languages
                supportedLanguages = new[] { "en", "en-US", "en-GB", "zh", "zh-CN", "es", "fr", "de" };
            }
        }

        public override async Task<PhonemeResult> PhonemizeAsync(
            string text,
            string language,
            PhonemeOptions options = null,
            CancellationToken cancellationToken = default)
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("WebGLESpeakPhonemizer is not initialized");
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

            // Map language codes to eSpeak language codes
            string espeakLanguage = MapToESpeakLanguage(language);

            // Check cache first
            var cacheKey = $"espeak_{text}_{espeakLanguage}";
            var cachedResult = WebGLCacheManager.GetCachedPhonemesForText(cacheKey);
            if (cachedResult != null)
            {
                Debug.Log($"[WebGLESpeakPhonemizer] Using cached result for: {text}");
                return new PhonemeResult
                {
                    Phonemes = cachedResult.Split(' '),
                    PhonemeString = cachedResult,
                    Language = language,
                    Success = true
                };
            }

            // Perform phonemization
            string phonemes = await Task.Run(() =>
            {
                try
                {
                    return WebGLInterop.PhonemizeESpeak(text, espeakLanguage);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[WebGLESpeakPhonemizer] Phonemization error: {e.Message}");
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

            // Cache the result
            WebGLCacheManager.CachePhonemesForText(cacheKey, phonemes);

            // Parse phonemes
            var phonemeArray = phonemes.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            
            return new PhonemeResult
            {
                Phonemes = phonemeArray,
                PhonemeString = phonemes,
                Language = language,
                Success = true
            };
        }

        private string MapToESpeakLanguage(string language)
        {
            // Map common language codes to eSpeak format
            return language switch
            {
                "en" => "en",
                "en-US" => "en-us",
                "en-GB" => "en-gb",
                "zh" => "zh",
                "zh-CN" => "zh",
                "zh-TW" => "zh-yue",
                "ko" => "ko",
                "ko-KR" => "ko",
                "es" => "es",
                "fr" => "fr",
                "de" => "de",
                "it" => "it",
                "pt" => "pt",
                "ru" => "ru",
                _ => language.ToLower()
            };
        }

        public override long GetMemoryUsage()
        {
            // Estimate memory usage
            return 5 * 1024 * 1024; // 5MB for eSpeak-ng
        }

        public override PhonemizerCapabilities GetCapabilities()
        {
            return new PhonemizerCapabilities
            {
                SupportsBatchProcessing = false,
                SupportsStreaming = false,
                MaxTextLength = 5000,
                RequiresNetwork = false,
                EstimatedLatencyMs = 30
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
                        Debug.Log("[WebGLESpeakPhonemizer] Disposing...");
                        isInitialized = false;
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[WebGLESpeakPhonemizer] Disposal error: {e.Message}");
                    }
                }
            }
        }

        public async Task<bool> InitializeAsync(
            PhonemizerBackendOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return await InitializeInternalAsync(options, cancellationToken);
        }
    }
}
#endif