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
                        bool success = WebGLInterop.InitializeOpenJTalkWeb();
                        
                        if (success)
                        {
                            isInitialized = true;
                            Debug.Log("[WebGLOpenJTalkPhonemizer] Initialization successful");
                        }
                        else
                        {
                            Debug.LogError("[WebGLOpenJTalkPhonemizer] Initialization failed");
                        }
                        
                        return success;
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
            // This should match the conversion in native OpenJTalkPhonemizer
            for (int i = 0; i < phonemes.Length; i++)
            {
                switch (phonemes[i])
                {
                    case "cl": phonemes[i] = "\ue000"; break;
                    case "pau": phonemes[i] = "\ue001"; break;
                    case "sil": phonemes[i] = "\ue002"; break;
                    case "by": phonemes[i] = "\ue003"; break;
                    case "ch": phonemes[i] = "\ue004"; break;
                    case "dy": phonemes[i] = "\ue005"; break;
                    case "gy": phonemes[i] = "\ue006"; break;
                    case "hy": phonemes[i] = "\ue007"; break;
                    case "ky": phonemes[i] = "\ue008"; break;
                    case "my": phonemes[i] = "\ue009"; break;
                    case "ny": phonemes[i] = "\ue00a"; break;
                    case "py": phonemes[i] = "\ue00b"; break;
                    case "ry": phonemes[i] = "\ue00c"; break;
                    case "sh": phonemes[i] = "\ue00d"; break;
                    case "ts": phonemes[i] = "\ue00e"; break;
                    case "ty": phonemes[i] = "\ue00f"; break;
                }
            }
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