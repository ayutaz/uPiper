#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;

namespace uPiper.Core.Phonemizers.WebGL
{
    /// <summary>
    /// Unity WebGL用OpenJTalk音素化クラス
    /// M3: Unity統合ラッパー経由でOpenJTalkを使用
    /// </summary>
    public class WebGLOpenJTalkUnityPhonemizer : IPhonmizer
    {
        // JavaScript関数のインポート
        [DllImport("__Internal")]
        private static extern int InitializeOpenJTalkUnity();

        [DllImport("__Internal")]
        private static extern int IsOpenJTalkUnityInitialized();

        [DllImport("__Internal")]
        private static extern IntPtr PhonemizeWithOpenJTalk(string text);

        [DllImport("__Internal")]
        private static extern void FreeOpenJTalkMemory(IntPtr ptr);

        [DllImport("__Internal")]
        private static extern void DisposeOpenJTalkUnity();

        [DllImport("__Internal")]
        private static extern IntPtr GetOpenJTalkDebugInfo();

        private bool _isInitialized = false;
        private bool _isInitializing = false;

        /// <summary>
        /// 音素化結果のJSONレスポンス
        /// </summary>
        [Serializable]
        private class PhonemizeResponse
        {
            public bool success;
            public string[] phonemes;
            public string error;
            public int count;
        }

        /// <summary>
        /// デバッグ情報のJSON
        /// </summary>
        [Serializable]
        private class DebugInfo
        {
            public bool moduleLoaded;
            public bool apiLoaded;
            public bool initialized;
            public string version;
        }

        /// <summary>
        /// 初期化
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                Debug.Log("[WebGLOpenJTalkUnity] Already initialized");
                return;
            }

            if (_isInitializing)
            {
                Debug.Log("[WebGLOpenJTalkUnity] Initialization in progress, waiting...");
                while (_isInitializing)
                {
                    await Task.Delay(100);
                }
                return;
            }

            _isInitializing = true;
            Debug.Log("[WebGLOpenJTalkUnity] Initializing...");

            try
            {
                // JavaScriptで初期化（非同期）
                int result = await Task.Run(() => InitializeOpenJTalkUnity());
                
                if (result == 0)
                {
                    _isInitialized = true;
                    Debug.Log("[WebGLOpenJTalkUnity] Initialization successful");
                    
                    // デバッグ情報を取得
                    LogDebugInfo();
                }
                else
                {
                    throw new Exception($"Failed to initialize OpenJTalk Unity (code: {result})");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WebGLOpenJTalkUnity] Initialization failed: {ex.Message}");
                throw;
            }
            finally
            {
                _isInitializing = false;
            }
        }

        /// <summary>
        /// 同期的な初期化（互換性のため）
        /// </summary>
        public void Initialize()
        {
            InitializeAsync().Wait();
        }

        /// <summary>
        /// テキストを音素に変換
        /// </summary>
        public async Task<string[]> TextToPhonemesAsync(string text)
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("Phonemizer not initialized. Call InitializeAsync() first.");
            }

            if (string.IsNullOrEmpty(text))
            {
                return new string[] { "^", "$" };
            }

            Debug.Log($"[WebGLOpenJTalkUnity] Phonemizing: {text}");

            IntPtr resultPtr = IntPtr.Zero;
            
            try
            {
                // JSLibを呼び出して音素化
                resultPtr = await Task.Run(() => PhonemizeWithOpenJTalk(text));
                
                if (resultPtr == IntPtr.Zero)
                {
                    throw new Exception("Failed to phonemize text (null result)");
                }

                // JSON結果を取得
                string jsonResult = Marshal.PtrToStringUTF8(resultPtr);
                Debug.Log($"[WebGLOpenJTalkUnity] Result: {jsonResult}");

                // JSONパース
                var response = JsonConvert.DeserializeObject<PhonemizeResponse>(jsonResult);
                
                if (response.success)
                {
                    Debug.Log($"[WebGLOpenJTalkUnity] Phonemes count: {response.count}");
                    return response.phonemes;
                }
                else
                {
                    throw new Exception($"Phonemization failed: {response.error}");
                }
            }
            finally
            {
                // メモリ解放
                if (resultPtr != IntPtr.Zero)
                {
                    FreeOpenJTalkMemory(resultPtr);
                }
            }
        }

        /// <summary>
        /// 同期的な音素化（互換性のため）
        /// </summary>
        public string[] TextToPhonemes(string text)
        {
            return TextToPhonemesAsync(text).Result;
        }

        /// <summary>
        /// 初期化状態の確認
        /// </summary>
        public bool IsInitialized()
        {
            return _isInitialized && IsOpenJTalkUnityInitialized() == 1;
        }

        /// <summary>
        /// デバッグ情報のログ出力
        /// </summary>
        private void LogDebugInfo()
        {
            IntPtr debugPtr = IntPtr.Zero;
            
            try
            {
                debugPtr = GetOpenJTalkDebugInfo();
                if (debugPtr != IntPtr.Zero)
                {
                    string jsonDebug = Marshal.PtrToStringUTF8(debugPtr);
                    var debugInfo = JsonConvert.DeserializeObject<DebugInfo>(jsonDebug);
                    
                    Debug.Log($"[WebGLOpenJTalkUnity] Debug Info:");
                    Debug.Log($"  - Module Loaded: {debugInfo.moduleLoaded}");
                    Debug.Log($"  - API Loaded: {debugInfo.apiLoaded}");
                    Debug.Log($"  - Initialized: {debugInfo.initialized}");
                    Debug.Log($"  - Version: {debugInfo.version}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WebGLOpenJTalkUnity] Failed to get debug info: {ex.Message}");
            }
            finally
            {
                if (debugPtr != IntPtr.Zero)
                {
                    FreeOpenJTalkMemory(debugPtr);
                }
            }
        }

        /// <summary>
        /// リソースの解放
        /// </summary>
        public void Dispose()
        {
            if (_isInitialized)
            {
                DisposeOpenJTalkUnity();
                _isInitialized = false;
                Debug.Log("[WebGLOpenJTalkUnity] Disposed");
            }
        }
    }
}
#endif