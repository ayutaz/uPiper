using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using uPiper.Core;
using uPiper.Core.Logging;
using uPiper.Core.Performance;
using uPiper.Core.Phonemizers.Backend;
using uPiper.Core.Platform;

namespace uPiper.Core.Phonemizers.Implementations
{
    /// <summary>
    /// 最適化されたOpenJTalk音素化実装
    /// メモリ効率とパフォーマンスを改善
    /// </summary>
    public class OptimizedOpenJTalkPhonemizer : BasePhonemizer
    {
        // P/Invoke宣言
        private const string LIBRARY_NAME = "openjtalk_wrapper";

        // UTF-8最適化版（Android用）
#if UNITY_ANDROID && !UNITY_EDITOR
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr openjtalk_initialize_utf8(byte[] dictPath, int dictPathLength);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr openjtalk_analyze_utf8(IntPtr handle, byte[] text, int textLength);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void openjtalk_free_string(IntPtr result);
#endif

        // 標準版（Windows/Mac/Linux用）
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr openjtalk_create([MarshalAs(UnmanagedType.LPStr)] string dict_path);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern void openjtalk_destroy(IntPtr handle);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr openjtalk_phonemize(IntPtr handle, [MarshalAs(UnmanagedType.LPStr)] string text);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr openjtalk_get_version();

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void openjtalk_free_result(IntPtr result);

        // PhonemeResult構造体
        [StructLayout(LayoutKind.Sequential)]
        private struct NativePhonemeResult
        {
            public IntPtr phonemes;
            public IntPtr phoneme_ids;
            public int phoneme_count;
            public IntPtr durations;
            public float total_duration;
        }

        private IntPtr _handle = IntPtr.Zero;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private bool _isInitialized = false;
        private readonly AndroidPerformanceProfiler _profiler = new();

        // バイト配列バッファのプール（GCを減らすため）
        private readonly Queue<byte[]> _bufferPool = new();
        private readonly object _poolLock = new();
        private const int MAX_POOL_SIZE = 10;
        private const int BUFFER_SIZE = 1024;

        public override string Name => "OptimizedOpenJTalk";
        public override string Version => "3.0.0";
        public override string[] SupportedLanguages => new[] { "ja" };

        public OptimizedOpenJTalkPhonemizer() : base(cacheSize: 500) // キャッシュサイズを最適化
        {
        }

        protected override async Task<PhonemeResult> PhonemizeInternalAsync(string normalizedText, string language, CancellationToken cancellationToken)
        {
            if (!_isInitialized)
            {
                var initialized = await InitializeAsync();
                if (!initialized)
                {
                    return new PhonemeResult
                    {
                        Phonemes = new string[] { },
                        PhonemeIds = new int[] { },
                        Durations = new float[] { },
                        Pitches = new float[] { }
                    };
                }
            }

            return await Task.Run(() =>
            {
                using (_profiler.BeginProfile("Phonemize"))
                {
                    byte[] buffer = null;
                    try
                    {
                        // バッファプールから取得
                        buffer = GetBuffer();

                        // UTF-8エンコード（最適化: 事前確保されたバッファを使用）
                        var byteCount = Encoding.UTF8.GetByteCount(normalizedText);
                        if (byteCount > buffer.Length)
                        {
                            // バッファが小さすぎる場合は新しく作成
                            ReturnBuffer(buffer);
                            buffer = new byte[byteCount];
                        }

                        var actualBytes = Encoding.UTF8.GetBytes(normalizedText, 0, normalizedText.Length, buffer, 0);

                        // ネイティブ呼び出し
                        IntPtr resultPtr;
                        using (_profiler.BeginProfile("Native Analyze"))
                        {
                            try
                            {
#if UNITY_ANDROID && !UNITY_EDITOR
                                // Androidの場合はUTF-8関数を試す
                                try
                                {
                                    var analyzeResultPtr = openjtalk_analyze_utf8(_handle, buffer, actualBytes);
                                    if (analyzeResultPtr != IntPtr.Zero)
                                    {
                                        // UTF-8版は文字列を返す
                                        var resultStr = Marshal.PtrToStringAnsi(analyzeResultPtr);
                                        openjtalk_free_string(analyzeResultPtr);
                                        return ParsePhonemeResult(resultStr);
                                    }
                                }
                                catch (EntryPointNotFoundException)
                                {
                                    // UTF-8関数がない場合は通常版にフォールバック
                                }
#endif

                                // 通常版を使用
                                resultPtr = openjtalk_phonemize(_handle, normalizedText);
                            }
                            catch (EntryPointNotFoundException)
                            {
                                // フォールバック
                                resultPtr = openjtalk_phonemize(_handle, normalizedText);
                            }
                        }

                        if (resultPtr == IntPtr.Zero)
                        {
                            PiperLogger.LogWarning($"[OptimizedOpenJTalk] Failed to analyze text: {normalizedText}");
                            return new PhonemeResult
                            {
                                Phonemes = new string[] { },
                                PhonemeIds = new int[] { },
                                Durations = new float[] { },
                                Pitches = new float[] { }
                            };
                        }

                        try
                        {
                            // 結果を解析
                            return ParseNativePhonemeResult(resultPtr);
                        }
                        finally
                        {
                            openjtalk_free_result(resultPtr);
                        }
                    }
                    finally
                    {
                        if (buffer != null)
                        {
                            ReturnBuffer(buffer);
                        }
                    }
                }
            }, cancellationToken);
        }

        public async Task<bool> InitializeAsync(Dictionary<string, object> options = null)
        {
            await _initLock.WaitAsync();
            try
            {
                if (_isInitialized)
                    return true;

                using (_profiler.BeginProfile("Initialize"))
                {
                    PiperLogger.LogInfo("[OptimizedOpenJTalk] Initializing...");

                    // Android向け最適化: 非同期で辞書パスを取得
                    string dictPath;
#if UNITY_EDITOR && UPIPER_DEVELOPMENT
                    // Development environment: Load directly from Samples~
                    dictPath = uPiperPaths.GetDevelopmentOpenJTalkPath();
                    if (!Directory.Exists(dictPath))
                    {
                        Debug.LogWarning($"[OptimizedOpenJTalk] Development mode: Dictionary not found at: {dictPath}, falling back to StreamingAssets");
                        dictPath = uPiperPaths.GetRuntimeOpenJTalkPath();
                    }
                    else
                    {
                        Debug.Log($"[OptimizedOpenJTalk] Development mode: Loading from Samples~: {dictPath}");
                    }
#elif UNITY_ANDROID && !UNITY_EDITOR
                    dictPath = await OptimizedAndroidPathResolver.GetDictionaryPathAsync();
#else
                    // 非Android環境ではStreamingAssetsからパスを取得
                    dictPath = Path.Combine(Application.streamingAssetsPath, "uPiper", "OpenJTalk", "naist_jdic", "open_jtalk_dic_utf_8-1.11");
#endif

                    if (!System.IO.Directory.Exists(dictPath))
                    {
                        PiperLogger.LogError($"Dictionary not found at: {dictPath}");
                        return false;
                    }

                    // UTF-8バイトで直接渡す
                    var dictPathBytes = Encoding.UTF8.GetBytes(dictPath);

                    using (_profiler.BeginProfile("Native Initialize"))
                    {
#if UNITY_ANDROID && !UNITY_EDITOR
                        try
                        {
                            // Androidの場合はUTF-8最適化版を使用
                            _handle = openjtalk_initialize_utf8(dictPathBytes, dictPathBytes.Length);
                        }
                        catch (EntryPointNotFoundException)
                        {
                            // フォールバック
                            PiperLogger.LogWarning("[OptimizedOpenJTalk] UTF-8 functions not found, falling back to standard version");
                            _handle = openjtalk_create(dictPath);
                        }
#else
                        // Android以外は通常版を使用
                        _handle = openjtalk_create(dictPath);
#endif
                    }

                    if (_handle == IntPtr.Zero)
                    {
                        PiperLogger.LogError("Failed to initialize OpenJTalk");
                        return false;
                    }

                    // バージョン確認
                    var versionPtr = openjtalk_get_version();
                    var version = Marshal.PtrToStringAnsi(versionPtr);
                    PiperLogger.LogInfo($"[OptimizedOpenJTalk] Initialized with version: {version}");

                    _isInitialized = true;

                    // 初期バッファをプールに追加
                    for (var i = 0; i < 5; i++)
                    {
                        _bufferPool.Enqueue(new byte[BUFFER_SIZE]);
                    }

                    return true;
                }
            }
            catch (Exception e)
            {
                PiperLogger.LogError($"[OptimizedOpenJTalk] Initialization failed: {e.Message}");
                return false;
            }
            finally
            {
                _initLock.Release();
            }
        }

        private void FreeNativePhonemeResult(IntPtr resultPtr)
        {
            if (resultPtr == IntPtr.Zero) return;

            // Just use the native free function - it will handle all internal pointers
            openjtalk_free_result(resultPtr);
        }

        private PhonemeResult ParseNativePhonemeResult(IntPtr resultPtr)
        {
            if (resultPtr == IntPtr.Zero)
            {
                return new PhonemeResult
                {
                    Phonemes = new string[] { },
                    PhonemeIds = new int[] { },
                    Durations = new float[] { },
                    Pitches = new float[] { }
                };
            }

            var nativeResult = Marshal.PtrToStructure<NativePhonemeResult>(resultPtr);

            // Parse phonemes string
            var phonemeStr = Marshal.PtrToStringAnsi(nativeResult.phonemes);
            var phonemes = phonemeStr?.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries) ?? new string[0];

            // Copy arrays
            var phonemeIds = new int[nativeResult.phoneme_count];
            var durations = new float[nativeResult.phoneme_count];
            var pitches = new float[nativeResult.phoneme_count];

            if (nativeResult.phoneme_ids != IntPtr.Zero)
                Marshal.Copy(nativeResult.phoneme_ids, phonemeIds, 0, nativeResult.phoneme_count);

            if (nativeResult.durations != IntPtr.Zero)
                Marshal.Copy(nativeResult.durations, durations, 0, nativeResult.phoneme_count);

            // Set default pitches
            for (var i = 0; i < pitches.Length; i++)
                pitches[i] = 0.0f;

            return new PhonemeResult
            {
                Phonemes = phonemes,
                PhonemeIds = phonemeIds,
                Durations = durations,
                Pitches = pitches
            };
        }


        private PhonemeResult ParsePhonemeResult(string resultStr)
        {
            try
            {
                // スペース区切りの音素文字列を解析
                var phonemes = resultStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var phonemeIds = new int[phonemes.Length];
                var durations = new float[phonemes.Length];
                var pitches = new float[phonemes.Length];

                // デフォルト値を設定
                for (var i = 0; i < phonemes.Length; i++)
                {
                    phonemeIds[i] = 1; // デフォルトID
                    durations[i] = 0.05f; // 50ms
                    pitches[i] = 0.0f; // デフォルトピッチ
                }

                return new PhonemeResult
                {
                    Phonemes = phonemes,
                    PhonemeIds = phonemeIds,
                    Durations = durations,
                    Pitches = pitches
                };
            }
            catch (Exception e)
            {
                PiperLogger.LogError($"[OptimizedOpenJTalk] Failed to parse result: {e.Message}");
                return new PhonemeResult
                {
                    Phonemes = new string[] { },
                    PhonemeIds = new int[] { },
                    Durations = new float[] { },
                    Pitches = new float[] { }
                };
            }
        }

        private byte[] GetBuffer()
        {
            lock (_poolLock)
            {
                if (_bufferPool.Count > 0)
                {
                    return _bufferPool.Dequeue();
                }
            }
            return new byte[BUFFER_SIZE];
        }

        private void ReturnBuffer(byte[] buffer)
        {
            if (buffer.Length != BUFFER_SIZE)
                return; // 標準サイズ以外は返却しない

            lock (_poolLock)
            {
                if (_bufferPool.Count < MAX_POOL_SIZE)
                {
                    Array.Clear(buffer, 0, buffer.Length); // セキュリティのためクリア
                    _bufferPool.Enqueue(buffer);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_handle != IntPtr.Zero)
                {
                    openjtalk_destroy(_handle);
                    _handle = IntPtr.Zero;
                    _isInitialized = false;
                }

                lock (_poolLock)
                {
                    _bufferPool.Clear();
                }

                _initLock?.Dispose();
            }

            base.Dispose(disposing);
        }


        public static bool IsAvailable()
        {
            try
            {
                var versionPtr = openjtalk_get_version();
                return versionPtr != IntPtr.Zero;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// パフォーマンスレポートを生成
        /// </summary>
        public string GeneratePerformanceReport()
        {
            return _profiler.GenerateReport();
        }
    }
}