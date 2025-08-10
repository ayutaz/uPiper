#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using uPiper.Core.Phonemizers.Backend;

namespace uPiper.Core.Phonemizers.WebGL
{
    /// <summary>
    /// Unity WebGL用OpenJTalk音素化クラス
    /// MonoBehaviourベースでコルーチンを使用
    /// </summary>
    public class WebGLOpenJTalkUnityPhonemizer : MonoBehaviour, IPhonemizer
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

        private static WebGLOpenJTalkUnityPhonemizer _instance;
        private bool _isInitialized = false;
        private readonly PhonemeCache _cache;
        private TaskCompletionSource<bool> _initializationTask;

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

        // IPhonemizer インターフェースの実装
        public string Name => "WebGL OpenJTalk Unity";
        public string Version => "1.0.0";
        public string[] SupportedLanguages => new[] { "ja" };
        public bool UseCache { get; set; } = true;

        /// <summary>
        /// シングルトンインスタンスの取得
        /// </summary>
        public static WebGLOpenJTalkUnityPhonemizer GetInstance()
        {
            if (_instance == null)
            {
                var go = new GameObject("WebGLOpenJTalkUnityPhonemizer");
                go.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<WebGLOpenJTalkUnityPhonemizer>();
            }
            return _instance;
        }

        /// <summary>
        /// MonoBehaviourの初期化
        /// </summary>
        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log($"[{Name}] Created");
        }

        /// <summary>
        /// コンストラクタ（MonoBehaviourなので実際は使われない）
        /// </summary>
        public WebGLOpenJTalkUnityPhonemizer()
        {
            // PhonemeCache を正しいパラメータで初期化
            _cache = new PhonemeCache(1000, TimeSpan.FromHours(1));
        }

        /// <summary>
        /// 初期化
        /// </summary>
        public async Task InitializeAsync()
        {
            var instance = GetInstance();
            
            if (instance._isInitialized)
            {
                Debug.Log($"[{Name}] Already initialized");
                return;
            }

            if (instance._initializationTask != null)
            {
                Debug.Log($"[{Name}] Initialization in progress, waiting...");
                await instance._initializationTask.Task;
                return;
            }

            Debug.Log($"[{Name}] Starting initialization...");
            instance._initializationTask = new TaskCompletionSource<bool>();
            
            // コルーチンを開始
            instance.StartCoroutine(instance.InitializeCoroutine());
            
            await instance._initializationTask.Task;
        }

        /// <summary>
        /// コルーチンベースの初期化処理
        /// </summary>
        private IEnumerator InitializeCoroutine()
        {
            Debug.Log($"[{Name}] Starting initialization with coroutine...");

            int maxRetries = 100; // 最大100回リトライ（20秒間）
            float baseRetryDelay = 0.2f; // 200msから開始
            bool initializationSuccessful = false;
            
            for (int i = 0; i < maxRetries; i++)
            {
                // リトライ回数が増えるごとに遅延を増やす
                float currentDelay = baseRetryDelay + (i * 0.05f); // 200ms, 250ms, 300ms...
                
                Debug.Log($"[{Name}] Initialization attempt {i + 1}/{maxRetries}");
                
                int result = -3;
                bool hasError = false;
                
                // try-catchをyield returnの外側で使用
                try
                {
                    result = InitializeOpenJTalkUnity();
                    Debug.Log($"[{Name}] Initialization result: {result}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[{Name}] Exception during initialization: {ex.Message}");
                    hasError = true;
                    _initializationTask?.SetException(ex);
                }
                
                if (hasError)
                {
                    break; // エラーが発生したらループを抜ける
                }
                
                if (result == 0)
                {
                    // 成功した後、初期化状態を確認
                    int verifyResult = IsOpenJTalkUnityInitialized();
                    Debug.Log($"[{Name}] Verification result: {verifyResult}");
                    
                    if (verifyResult == 1)
                    {
                        _isInitialized = true;
                        initializationSuccessful = true;
                        Debug.Log($"[{Name}] Initialization successful on attempt {i + 1}");
                        
                        // デバッグ情報を取得
                        LogDebugInfo();
                        
                        _initializationTask?.SetResult(true);
                        break; // 成功したらループを抜ける
                    }
                    else
                    {
                        Debug.LogWarning($"[{Name}] Initialization claimed success but verification failed");
                    }
                }
                else if (result == -1)
                {
                    // 致命的エラー
                    LogDebugInfo(); // エラー時のデバッグ情報
                    var error = new Exception($"Fatal error during initialization (code: {result})");
                    _initializationTask?.SetException(error);
                    break; // エラーなのでループを抜ける
                }
                else if (result == -2)
                {
                    // 非同期読み込み中、再試行
                    Debug.Log($"[{Name}] Scripts loading or initializing, retrying in {currentDelay:F2}s...");
                }
                else
                {
                    Debug.LogWarning($"[{Name}] Unexpected result code: {result}, retrying in {currentDelay:F2}s...");
                }
                
                // WebGL環境で適切な待機（try-catchの外）
                yield return new WaitForSecondsRealtime(currentDelay);
            }
            
            // 初期化が成功しなかった場合のエラー処理
            if (!initializationSuccessful && _initializationTask != null && !_initializationTask.Task.IsCompleted)
            {
                // 最大リトライ回数に達した
                LogDebugInfo(); // 最終的なデバッグ情報
                var timeoutError = new Exception($"Failed to initialize OpenJTalk Unity after {maxRetries} attempts");
                _initializationTask?.SetException(timeoutError);
            }
        }

        /// <summary>
        /// 非同期音素化
        /// </summary>
        public async Task<PhonemeResult> PhonemizeAsync(string text, string language = "ja", CancellationToken cancellationToken = default)
        {
            var instance = GetInstance();
            
            if (!instance._isInitialized)
            {
                await instance.InitializeAsync();
            }

            if (!instance.IsLanguageSupported(language))
            {
                throw new ArgumentException($"Language '{language}' is not supported");
            }

            if (string.IsNullOrEmpty(text))
            {
                return new PhonemeResult
                {
                    OriginalText = text,
                    Language = language,
                    Phonemes = new[] { "^", "$" },
                    PhonemeIds = new[] { 0, 1 },
                    Success = true
                };
            }

            // キャッシュチェック
            if (instance.UseCache)
            {
                PhonemeResult cached;
                if (instance._cache.TryGet(text, language, out cached))
                {
                    Debug.Log($"[{Name}] Cache hit for: {text}");
                    return cached;
                }
            }

            Debug.Log($"[{Name}] Phonemizing: {text}");

            IntPtr resultPtr = IntPtr.Zero;
            
            try
            {
                // JSLibを呼び出して音素化
                // WebGLは単一スレッドのため、Task.Runを使用せず直接呼び出す
                resultPtr = PhonemizeWithOpenJTalk(text);
                
                if (resultPtr == IntPtr.Zero)
                {
                    throw new Exception("Failed to phonemize text (null result)");
                }

                // JSON結果を取得
                string jsonResult = Marshal.PtrToStringUTF8(resultPtr);
                Debug.Log($"[{Name}] Result: {jsonResult}");

                // JSONパース
                var response = JsonConvert.DeserializeObject<PhonemizeResponse>(jsonResult);
                
                if (!response.success)
                {
                    throw new Exception($"Phonemization failed: {response.error}");
                }

                // 結果を作成
                var result = new PhonemeResult
                {
                    OriginalText = text,
                    Language = language,
                    Phonemes = response.phonemes,
                    PhonemeIds = response.phonemes.Select((p, i) => i).ToArray(),
                    ProcessingTime = TimeSpan.Zero,
                    Success = true
                };

                // キャッシュに保存
                if (instance.UseCache)
                {
                    instance._cache.Set(text, language, result);
                }

                Debug.Log($"[{Name}] Phonemes count: {response.count}");
                return result;
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
        /// 同期音素化
        /// </summary>
        public PhonemeResult Phonemize(string text, string language = "ja")
        {
            return PhonemizeAsync(text, language).GetAwaiter().GetResult();
        }

        /// <summary>
        /// バッチ音素化
        /// </summary>
        public async Task<PhonemeResult[]> PhonemizeBatchAsync(string[] texts, string language = "ja", CancellationToken cancellationToken = default)
        {
            var tasks = texts.Select(text => PhonemizeAsync(text, language, cancellationToken));
            return await Task.WhenAll(tasks);
        }

        /// <summary>
        /// キャッシュクリア
        /// </summary>
        public void ClearCache()
        {
            var instance = GetInstance();
            instance._cache.Clear();
            Debug.Log($"[{Name}] Cache cleared");
        }

        /// <summary>
        /// キャッシュ統計
        /// </summary>
        public CacheStatistics GetCacheStatistics()
        {
            var instance = GetInstance();
            var stats = instance._cache.GetStatistics();
            return new CacheStatistics
            {
                TotalSizeBytes = stats.MemoryUsage,
                EntryCount = stats.EntryCount,
                HitCount = stats.HitCount,
                MissCount = stats.MissCount
            };
        }

        /// <summary>
        /// 言語サポート確認
        /// </summary>
        public bool IsLanguageSupported(string language)
        {
            return SupportedLanguages.Contains(language.ToLower());
        }

        /// <summary>
        /// 言語情報取得
        /// </summary>
        public LanguageInfo GetLanguageInfo(string language)
        {
            if (!IsLanguageSupported(language))
            {
                return null;
            }

            return new LanguageInfo
            {
                Code = "ja",
                Name = "Japanese",
                NativeName = "日本語",
                Direction = TextDirection.LeftToRight,
                AvailableVoices = new string[0]
            };
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
                    
                    Debug.Log($"[{Name}] Debug Info:");
                    Debug.Log($"  - Module Loaded: {debugInfo.moduleLoaded}");
                    Debug.Log($"  - API Loaded: {debugInfo.apiLoaded}");
                    Debug.Log($"  - Initialized: {debugInfo.initialized}");
                    Debug.Log($"  - Version: {debugInfo.version}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{Name}] Failed to get debug info: {ex.Message}");
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
        /// シングルトンのため、通常は呼び出すべきではない
        /// </summary>
        public void Dispose()
        {
            // シングルトンパターンのため、Disposeは保護する
            Debug.LogWarning($"[{Name}] Dispose called on singleton. This is usually not necessary and may cause issues.");
            
            // アプリケーション終了時のみ実際の破棄を行う
            #if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            #else
            if (Application.isPlaying)
                return;
            #endif
            
            var instance = GetInstance();
            if (instance._isInitialized)
            {
                DisposeOpenJTalkUnity();
                instance._isInitialized = false;
                Debug.Log($"[{Name}] Disposed");
            }
        }

        /// <summary>
        /// MonoBehaviourのクリーンアップ
        /// </summary>
        void OnDestroy()
        {
            Dispose();
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
#endif