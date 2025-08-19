using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using uPiper.Core.Logging;
using uPiper.Core.Performance;

namespace uPiper.Core.Platform
{
    /// <summary>
    /// 最適化されたAndroid向けパス解決クラス
    /// 辞書データの圧縮展開と遅延ロードを実装
    /// </summary>
    public static class OptimizedAndroidPathResolver
    {
        private static readonly string DICT_VERSION = "1.11";
        private static readonly string DICT_ZIP_NAME = "naist_jdic.zip";
        private static readonly string DICT_EXTRACTED_MARKER = ".extracted";

        private static readonly object _lock = new();

        /// <summary>
        /// アプリ起動時に非同期で辞書展開を開始
        /// </summary>
        public static async void PreloadDictionaryAsync()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // メインスレッドで実行（UnityWebRequestを使用するため）
            var persistentPath = Application.persistentDataPath;
            var streamingPath = Application.streamingAssetsPath;
            await ExtractDictionaryIfNeededInternalAsync(persistentPath, streamingPath);
#else
            await Task.CompletedTask;
#endif
        }

        /// <summary>
        /// 辞書パスを取得（必要に応じて展開完了を待つ）
        /// </summary>
        public static async Task<string> GetDictionaryPathAsync()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // メインスレッドでパスを取得
            var persistentPath = Application.persistentDataPath;
            var streamingPath = Application.streamingAssetsPath;
            var dictPath = Path.Combine(persistentPath, "uPiper", "OpenJTalk", "naist_jdic");
            
            // 既に展開済みかチェック
            var markerPath = Path.Combine(dictPath, DICT_EXTRACTED_MARKER);
            if (File.Exists(markerPath))
            {
                var version = File.ReadAllText(markerPath).Trim();
                if (version == DICT_VERSION)
                {
                    return dictPath;
                }
            }
            
            // まだ展開されていない場合は展開
            await ExtractDictionaryIfNeededInternalAsync(persistentPath, streamingPath);
            
            return dictPath;
#else
            // 非Android環境ではStreamingAssetsから直接パスを返す
            return await Task.FromResult(Path.Combine(Application.streamingAssetsPath, "uPiper", "OpenJTalk", "naist_jdic", "open_jtalk_dic_utf_8-1.11"));
#endif
        }

        /// <summary>
        /// 辞書データを圧縮形式から展開（内部メソッド、パスを引数で受け取る）
        /// </summary>
        private static async Task<bool> ExtractDictionaryIfNeededInternalAsync(string persistentDataPath, string streamingAssetsPath)
        {
            var profiler = new AndroidPerformanceProfiler();

            using (profiler.BeginProfile("Dictionary Extraction"))
            {
                try
                {
                    var destPath = Path.Combine(persistentDataPath, "uPiper", "OpenJTalk", "naist_jdic");
                    var markerPath = Path.Combine(destPath, DICT_EXTRACTED_MARKER);

                    // 既に展開済みかチェック
                    if (File.Exists(markerPath))
                    {
                        var version = File.ReadAllText(markerPath).Trim();
                        if (version == DICT_VERSION)
                        {
                            PiperLogger.LogInfo($"[OptimizedAndroid] Dictionary already extracted (version {version})");
                            return true;
                        }
                    }

                    // ディレクトリを準備
                    if (Directory.Exists(destPath))
                    {
                        try
                        {
                            // Androidでは削除に失敗することがあるため、ファイルを個別に削除
                            var files = Directory.GetFiles(destPath, "*", SearchOption.AllDirectories);
                            foreach (var file in files)
                            {
                                File.SetAttributes(file, FileAttributes.Normal);
                                File.Delete(file);
                            }

                            var dirs = Directory.GetDirectories(destPath, "*", SearchOption.AllDirectories);
                            // 深い階層から削除
                            Array.Reverse(dirs);
                            foreach (var dir in dirs)
                            {
                                Directory.Delete(dir, false);
                            }

                            Directory.Delete(destPath, false);
                        }
                        catch (Exception deleteEx)
                        {
                            PiperLogger.LogWarning($"[OptimizedAndroid] Failed to clean directory: {deleteEx.Message}. Attempting to continue...");
                        }
                    }

                    if (!Directory.Exists(destPath))
                    {
                        Directory.CreateDirectory(destPath);
                    }

                    // Note: AndroidPerformanceProfiler.LogMemoryUsage cannot be called from background thread
                    // AndroidPerformanceProfiler.LogMemoryUsage("Before Extraction");

                    // ZIPファイルをStreamingAssetsから読み込み
                    var zipPath = Path.Combine(streamingAssetsPath, "uPiper", "OpenJTalk", DICT_ZIP_NAME);

                    byte[] zipData = null;

                    // Unity 2022互換: TaskCompletionSourceを使用
                    var tcs = new TaskCompletionSource<byte[]>();
                    using (var request = UnityWebRequest.Get(zipPath))
                    {
                        var operation = request.SendWebRequest();

                        // コルーチンの代わりにタスクで待機
                        operation.completed += _ =>
                        {
                            if (request.result == UnityWebRequest.Result.Success)
                            {
                                tcs.SetResult(request.downloadHandler.data);
                            }
                            else
                            {
                                tcs.SetException(new Exception($"Failed to load dictionary ZIP: {request.error}"));
                            }
                        };

                        try
                        {
                            zipData = await tcs.Task;
                        }
                        catch (Exception ex)
                        {
                            PiperLogger.LogError($"[OptimizedAndroid] {ex.Message}");
                            return false;
                        }
                    }

                    // メモリ効率的な展開
                    using var zipStream = new MemoryStream(zipData);
                    using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
                    PiperLogger.LogInfo($"[OptimizedAndroid] Extracting {archive.Entries.Count} files...");

                    var extracted = 0;
                    foreach (var entry in archive.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name))
                            continue; // ディレクトリエントリをスキップ

                        var targetPath = Path.Combine(destPath, entry.FullName);
                        var targetDir = Path.GetDirectoryName(targetPath);

                        if (!Directory.Exists(targetDir))
                        {
                            Directory.CreateDirectory(targetDir);
                        }

                        using (var entryStream = entry.Open())
                        using (var fileStream = File.Create(targetPath))
                        {
                            await entryStream.CopyToAsync(fileStream);
                        }

                        extracted++;

                        // 定期的にGCを実行してメモリ使用量を抑える
                        if (extracted % 100 == 0)
                        {
                            GC.Collect();
                            await Task.Yield();
                        }
                    }

                    PiperLogger.LogInfo($"[OptimizedAndroid] Extracted {extracted} files");

                    // 展開完了マーカーを作成
                    File.WriteAllText(markerPath, DICT_VERSION);

                    // Note: AndroidPerformanceProfiler.LogMemoryUsage cannot be called from background thread
                    // AndroidPerformanceProfiler.LogMemoryUsage("After Extraction");

                    // 最終的なGC
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();

                    return true;
                }
                catch (Exception e)
                {
                    PiperLogger.LogError($"[OptimizedAndroid] Dictionary extraction failed: {e}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 辞書データのサイズを取得
        /// </summary>
        public static long GetDictionarySize()
        {
            var dictPath = Path.Combine(Application.persistentDataPath, "uPiper", "OpenJTalk", "naist_jdic");

            if (!Directory.Exists(dictPath))
                return 0;

            long totalSize = 0;
            var files = Directory.GetFiles(dictPath, "*", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                var info = new FileInfo(file);
                totalSize += info.Length;
            }

            return totalSize;
        }

        /// <summary>
        /// 不要なキャッシュをクリア
        /// </summary>
        public static void ClearCache()
        {
            var dictPath = Path.Combine(Application.persistentDataPath, "uPiper", "OpenJTalk", "naist_jdic");

            if (Directory.Exists(dictPath))
            {
                try
                {
                    Directory.Delete(dictPath, true);
                    PiperLogger.LogInfo("[OptimizedAndroid] Dictionary cache cleared");
                }
                catch (Exception e)
                {
                    PiperLogger.LogError($"[OptimizedAndroid] Failed to clear cache: {e}");
                }
            }

        }
    }
}