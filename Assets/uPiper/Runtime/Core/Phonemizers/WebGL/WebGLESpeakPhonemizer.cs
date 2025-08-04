#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using uPiper.Core.Phonemizers.Backend;

namespace uPiper.Core.Phonemizers.WebGL
{
    /// <summary>
    /// WebGL implementation of multi-language phonemizer using eSpeak-ng WebAssembly
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
                        Debug.Log("[WebGLESpeakPhonemizer] Initializing WebGL eSpeak-ng...");
                        
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
                            Debug.Log("[WebGLESpeakPhonemizer] Waiting for async initialization...");
                            
                            // Poll for initialization completion
                            int maxAttempts = 100; // 10 seconds max
                            for (int i = 0; i < maxAttempts; i++)
                            {
                                await Task.Delay(100, cancellationToken);
                                if (WebGLInterop.IsESpeakInitialized())
                                {
                                    LoadSupportedLanguages();
                                    isInitialized = true;
                                    Debug.Log($"[WebGLESpeakPhonemizer] Initialization successful. Supported languages: {string.Join(", ", supportedLanguages)}");
                                    return true;
                                }
                            }
                            
                            Debug.LogError("[WebGLESpeakPhonemizer] Initialization timeout");
                            return false;
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
            }, cancellationToken);
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

            return await Task.Run(() =>
            {
                try
                {
                    // Normalize language code
                    string normalizedLang = NormalizeLanguageCode(language);
                    
                    // Call JavaScript function
                    IntPtr resultPtr = WebGLInterop.PhonemizeEnglishText(text, normalizedLang);
                    var result = WebGLInterop.ParseJSONResult<ESpeakResult>(resultPtr);

                    if (!result.success)
                    {
                        Debug.LogError($"[WebGLESpeakPhonemizer] Phonemization failed: {result.error}");
                        throw new Exception(result.error);
                    }

                    // Process phonemes based on language
                    var processedPhonemes = ProcessPhonemes(result.phonemes, normalizedLang);

                    return new PhonemeResult
                    {
                        Phonemes = processedPhonemes,
                        Language = language,
                        Success = true
                    };
                }
                catch (Exception e)
                {
                    Debug.LogError($"[WebGLESpeakPhonemizer] Error: {e.Message}");
                    throw;
                }
            }, cancellationToken);
        }

        private string NormalizeLanguageCode(string language)
        {
            // Convert language codes to eSpeak-ng format
            switch (language.ToLower())
            {
                case "ja":
                case "ja-jp":
                    // Japanese should use OpenJTalk, not eSpeak
                    Debug.LogWarning("[WebGLESpeakPhonemizer] Japanese should use WebGLOpenJTalkPhonemizer");
                    return "ja";
                    
                case "en":
                case "en-us":
                    return "en";
                    
                case "en-gb":
                    return "en-gb";
                    
                case "zh":
                case "zh-cn":
                    return "zh";
                    
                case "zh-tw":
                case "zh-hk":
                    return "zh-yue"; // Cantonese
                    
                default:
                    return language.ToLower();
            }
        }

        private string[] ProcessPhonemes(string[] phonemes, string language)
        {
            // Apply language-specific processing if needed
            if (language.StartsWith("zh"))
            {
                // Chinese might need special handling for tones
                return ProcessChinesePhonemes(phonemes);
            }
            
            return phonemes;
        }

        private string[] ProcessChinesePhonemes(string[] phonemes)
        {
            // Process Chinese phonemes for eSpeak-ng format
            // This is a simplified version - actual implementation would need proper tone handling
            return phonemes;
        }

        public override long GetMemoryUsage()
        {
            // Estimate based on WebAssembly module size
            return 20 * 1024 * 1024; // 20MB estimate for eSpeak-ng
        }

        public override BackendCapabilities GetCapabilities()
        {
            return new BackendCapabilities
            {
                SupportsIPA = true,
                SupportsStress = true,
                SupportsSyllables = false,
                SupportsTones = true, // eSpeak-ng supports tones for tonal languages
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
            supportedLanguages = Array.Empty<string>();
        }

        /// <summary>
        /// Extended result structure for eSpeak
        /// </summary>
        [Serializable]
        private class ESpeakResult
        {
            public bool success;
            public string error;
            public string[] phonemes;
            public string language;
        }
    }
}
#endif