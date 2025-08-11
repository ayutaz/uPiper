using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using uPiper.Core;
using uPiper.Core.Logging;
using uPiper.Core.Phonemizers.Backend;

namespace uPiper.Core.Phonemizers.WebGL
{
    /// <summary>
    /// WebGL環境でOpenJTalk WASMを使用する音素化器
    /// </summary>
    public class WebGLOpenJTalkPhonemizer : IPhonemizer
    {
        #region P/Invoke Declarations
        
        [DllImport("__Internal")]
        private static extern void OpenJTalk_Initialize(Action<int> callback);
        
        [DllImport("__Internal")]
        private static extern void OpenJTalk_Phonemize(string text, Action<int, string> callback);
        
        [DllImport("__Internal")]
        private static extern void OpenJTalk_Dispose();
        
        #endregion
        
        private bool _isInitialized;
        private static TaskCompletionSource<bool> _initTcs;
        private static TaskCompletionSource<PhonemeResult> _phonemizeTcs;
        
        public bool IsInitialized => _isInitialized;
        
        // IPhonemizer interface properties
        public string Name => "OpenJTalk WebGL";
        public string Version => "1.0.0";
        public string[] SupportedLanguages => new[] { "ja", "ja_JP" };
        public bool UseCache { get; set; } = false; // WebGL doesn't support caching yet
        
        /// <summary>
        /// Initialize OpenJTalk WASM
        /// </summary>
        public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_isInitialized)
                return true;
            
            PiperLogger.LogInfo("[WebGLOpenJTalkPhonemizer] Initializing OpenJTalk WASM...");
            
            _initTcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(() => _initTcs.TrySetCanceled());
            
            OpenJTalk_Initialize(OnInitialized);
            
            var result = await _initTcs.Task;
            _isInitialized = result;
            
            if (result)
            {
                PiperLogger.LogInfo("[WebGLOpenJTalkPhonemizer] OpenJTalk WASM initialized successfully");
            }
            else
            {
                PiperLogger.LogError("[WebGLOpenJTalkPhonemizer] Failed to initialize OpenJTalk WASM");
            }
            
            return result;
        }
        
        /// <summary>
        /// Static callback for initialization
        /// </summary>
        [AOT.MonoPInvokeCallback(typeof(Action<int>))]
        private static void OnInitialized(int success)
        {
            PiperLogger.LogInfo($"[WebGLOpenJTalkPhonemizer] Initialize callback: {success}");
            _initTcs?.TrySetResult(success != 0);
        }
        
        /// <summary>
        /// Phonemize text using OpenJTalk WASM
        /// </summary>
        public async Task<PhonemeResult> PhonemizeAsync(string text, string language = "ja", CancellationToken cancellationToken = default)
        {
            if (!_isInitialized)
            {
                PiperLogger.LogError("[WebGLOpenJTalkPhonemizer] Not initialized");
                return new PhonemeResult { Phonemes = new string[0] };
            }
            
            if (string.IsNullOrEmpty(text))
            {
                return new PhonemeResult { Phonemes = new string[0] };
            }
            
            // Only support Japanese
            if (language != "ja" && language != "ja_JP")
            {
                PiperLogger.LogWarning($"[WebGLOpenJTalkPhonemizer] Language '{language}' not supported, using fallback");
                return new PhonemeResult { Phonemes = new string[] { text } };
            }
            
            PiperLogger.LogInfo($"[WebGLOpenJTalkPhonemizer] Phonemizing text: {text}");
            
            _phonemizeTcs = new TaskCompletionSource<PhonemeResult>();
            cancellationToken.Register(() => _phonemizeTcs.TrySetCanceled());
            
            OpenJTalk_Phonemize(text, OnPhonemized);
            
            return await _phonemizeTcs.Task;
        }
        
        /// <summary>
        /// Static callback for phonemization
        /// </summary>
        [AOT.MonoPInvokeCallback(typeof(Action<int, string>))]
        private static void OnPhonemized(int success, string phonemes)
        {
            PiperLogger.LogInfo($"[WebGLOpenJTalkPhonemizer] Phonemize callback: success={success}, phonemes={phonemes}");
            
            if (success != 0 && !string.IsNullOrEmpty(phonemes))
            {
                // Split phonemes by space
                var phonemeArray = phonemes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var result = new PhonemeResult
                {
                    Phonemes = phonemeArray,
                    OriginalText = null // Will be set by caller
                };
                _phonemizeTcs?.TrySetResult(result);
            }
            else
            {
                PiperLogger.LogError("[WebGLOpenJTalkPhonemizer] Phonemization failed");
                _phonemizeTcs?.TrySetResult(new PhonemeResult { Phonemes = new string[0] });
            }
        }
        
        /// <summary>
        /// Synchronous phonemization (not supported in WebGL)
        /// </summary>
        public PhonemeResult Phonemize(string text, string language = "ja")
        {
            PiperLogger.LogError("[WebGLOpenJTalkPhonemizer] Synchronous phonemization not supported in WebGL");
            return new PhonemeResult { Phonemes = new string[0] };
        }
        
        /// <summary>
        /// Batch phonemization
        /// </summary>
        public async Task<PhonemeResult[]> PhonemizeBatchAsync(string[] texts, string language = "ja", CancellationToken cancellationToken = default)
        {
            var results = new PhonemeResult[texts.Length];
            for (int i = 0; i < texts.Length; i++)
            {
                results[i] = await PhonemizeAsync(texts[i], language, cancellationToken);
            }
            return results;
        }
        
        /// <summary>
        /// Clear cache (not implemented in WebGL)
        /// </summary>
        public void ClearCache()
        {
            // Cache not supported in WebGL
        }
        
        /// <summary>
        /// Get cache statistics
        /// </summary>
        public CacheStatistics GetCacheStatistics()
        {
            return new CacheStatistics
            {
                EntryCount = 0,
                TotalSizeBytes = 0,
                MaxSizeBytes = 0,
                HitCount = 0,
                MissCount = 0,
                EvictionCount = 0
            };
        }
        
        /// <summary>
        /// Check if language is supported
        /// </summary>
        public bool IsLanguageSupported(string language)
        {
            return language == "ja" || language == "ja_JP";
        }
        
        /// <summary>
        /// Get language info
        /// </summary>
        public LanguageInfo GetLanguageInfo(string language)
        {
            if (IsLanguageSupported(language))
            {
                return new LanguageInfo
                {
                    Code = language,
                    Name = "Japanese",
                    NativeName = "日本語",
                    PhonemeSetType = "OpenJTalk",
                    SupportsAccent = true,
                    RequiresPreprocessing = true,
                    Direction = TextDirection.LeftToRight
                };
            }
            return null;
        }
        
        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            if (_isInitialized)
            {
                OpenJTalk_Dispose();
                _isInitialized = false;
                PiperLogger.LogInfo("[WebGLOpenJTalkPhonemizer] Disposed");
            }
        }
    }
}