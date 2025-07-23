using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using uPiper.Core.Logging;
using uPiper.Core.Performance;
using uPiper.Core.Platform;

namespace uPiper.Core.Phonemizers.Implementations
{
    /// <summary>
    /// 最適化されたOpenJTalk音素化実装
    /// メモリ効率とパフォーマンスを改善
    /// </summary>
    public class OptimizedOpenJTalkPhonemizer : BasePhonemizerWithCache
    {
        // P/Invoke宣言（UTF-8バイト配列を直接渡す最適化版）
        [DllImport("openjtalk_wrapper")]
        private static extern IntPtr openjtalk_initialize_utf8(byte[] dictPath, int dictPathLength);

        [DllImport("openjtalk_wrapper")]
        private static extern IntPtr openjtalk_analyze_utf8(IntPtr handle, byte[] text, int textLength);

        [DllImport("openjtalk_wrapper")]
        private static extern void openjtalk_free_result(IntPtr result);

        [DllImport("openjtalk_wrapper")]
        private static extern void openjtalk_finalize(IntPtr handle);

        [DllImport("openjtalk_wrapper")]
        private static extern IntPtr openjtalk_get_version();

        private IntPtr _handle = IntPtr.Zero;
        private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);
        private bool _isInitialized = false;
        private readonly AndroidPerformanceProfiler _profiler = new AndroidPerformanceProfiler();
        
        // バイト配列バッファのプール（GCを減らすため）
        private readonly Queue<byte[]> _bufferPool = new Queue<byte[]>();
        private readonly object _poolLock = new object();
        private const int MAX_POOL_SIZE = 10;
        private const int BUFFER_SIZE = 1024;

        public OptimizedOpenJTalkPhonemizer() : base(cacheSize: 500) // キャッシュサイズを最適化
        {
        }

        protected override string[] PhonemizeInternal(string text, string language)
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("OpenJTalk is not initialized");
            }

            using (_profiler.BeginProfile("Phonemize"))
            {
                byte[] buffer = null;
                try
                {
                    // バッファプールから取得
                    buffer = GetBuffer();
                    
                    // UTF-8エンコード（最適化: 事前確保されたバッファを使用）
                    var byteCount = Encoding.UTF8.GetByteCount(text);
                    if (byteCount > buffer.Length)
                    {
                        // バッファが小さすぎる場合は新しく作成
                        ReturnBuffer(buffer);
                        buffer = new byte[byteCount];
                    }
                    
                    var actualBytes = Encoding.UTF8.GetBytes(text, 0, text.Length, buffer, 0);
                    
                    // ネイティブ呼び出し
                    IntPtr resultPtr;
                    using (_profiler.BeginProfile("Native Analyze"))
                    {
                        resultPtr = openjtalk_analyze_utf8(_handle, buffer, actualBytes);
                    }
                    
                    if (resultPtr == IntPtr.Zero)
                    {
                        PiperLogger.LogWarning($"[OptimizedOpenJTalk] Failed to analyze text: {text}");
                        return new string[] { };
                    }

                    try
                    {
                        // 結果を解析
                        var resultJson = Marshal.PtrToStringAnsi(resultPtr);
                        var phonemes = ParsePhonemeResult(resultJson);
                        return phonemes;
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
        }

        public override async Task InitializeAsync(Dictionary<string, object> options = null)
        {
            await _initLock.WaitAsync();
            try
            {
                if (_isInitialized)
                    return;

                using (_profiler.BeginProfile("Initialize"))
                {
                    PiperLogger.LogInfo("[OptimizedOpenJTalk] Initializing...");
                    
                    // Android向け最適化: 非同期で辞書パスを取得
                    string dictPath;
                    #if UNITY_ANDROID && !UNITY_EDITOR
                    dictPath = await OptimizedAndroidPathResolver.GetDictionaryPathAsync();
                    #else
                    dictPath = AndroidPathResolver.GetOpenJTalkDictionaryPath();
                    #endif
                    
                    if (!System.IO.Directory.Exists(dictPath))
                    {
                        throw new System.IO.DirectoryNotFoundException($"Dictionary not found at: {dictPath}");
                    }

                    // UTF-8バイトで直接渡す
                    var dictPathBytes = Encoding.UTF8.GetBytes(dictPath);
                    
                    using (_profiler.BeginProfile("Native Initialize"))
                    {
                        _handle = openjtalk_initialize_utf8(dictPathBytes, dictPathBytes.Length);
                    }
                    
                    if (_handle == IntPtr.Zero)
                    {
                        throw new Exception("Failed to initialize OpenJTalk");
                    }

                    // バージョン確認
                    var versionPtr = openjtalk_get_version();
                    var version = Marshal.PtrToStringAnsi(versionPtr);
                    PiperLogger.LogInfo($"[OptimizedOpenJTalk] Initialized with version: {version}");
                    
                    _isInitialized = true;
                    
                    // 初期バッファをプールに追加
                    for (int i = 0; i < 5; i++)
                    {
                        _bufferPool.Enqueue(new byte[BUFFER_SIZE]);
                    }
                }
            }
            finally
            {
                _initLock.Release();
            }
        }

        protected override string[] GetDefaultPhonemes()
        {
            return new[] { "N" }; // 日本語の撥音
        }

        protected override string GetCacheKey(string text, string language)
        {
            // より効率的なキャッシュキー生成
            return $"optja_{GetHashCode(text)}";
        }
        
        private int GetHashCode(string text)
        {
            unchecked
            {
                int hash = 17;
                foreach (char c in text)
                {
                    hash = hash * 31 + c;
                }
                return hash;
            }
        }

        private string[] ParsePhonemeResult(string json)
        {
            try
            {
                var phonemes = new List<string>();
                var lines = json.Split('\n');
                
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    
                    var parts = line.Split('\t');
                    if (parts.Length >= 2)
                    {
                        phonemes.Add(parts[1]); // 音素を追加
                    }
                }
                
                return phonemes.ToArray();
            }
            catch (Exception e)
            {
                PiperLogger.LogError($"[OptimizedOpenJTalk] Failed to parse result: {e.Message}");
                return new string[] { };
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

        public override void Dispose()
        {
            if (_handle != IntPtr.Zero)
            {
                openjtalk_finalize(_handle);
                _handle = IntPtr.Zero;
                _isInitialized = false;
            }
            
            lock (_poolLock)
            {
                _bufferPool.Clear();
            }
            
            _initLock?.Dispose();
            base.Dispose();
        }

        public override string[] GetSupportedLanguages()
        {
            return new[] { "ja" };
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