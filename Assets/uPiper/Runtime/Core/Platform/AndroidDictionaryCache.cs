using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using uPiper.Core.Logging;

namespace uPiper.Core.Platform
{
    /// <summary>
    /// Android固有の辞書ファイルプリキャッシュ。
    /// StreamingAssets（APK内 jar:file://）からpersistentDataPathにコピーし、
    /// 2回目以降はローカルファイルシステムから高速読み込みする。
    /// </summary>
    /// <remarks>
    /// Android上ではStreamingAssetsはAPK内にあり、毎回UnityWebRequestでの読み取りが必要。
    /// 初回起動時にpersistentDataPathへコピーすることで、2回目以降はFile.ReadAllTextで
    /// 直接読み込みが可能になり、辞書ロードが大幅に高速化される。
    /// アプリ更新時はバージョンマーカーファイルで検出し、辞書を再キャッシュする。
    /// </remarks>
    internal static class AndroidDictionaryCache
    {
        private const string Tag = "[AndroidDictionaryCache]";

        /// <summary>
        /// キャッシュディレクトリ（persistentDataPath/uPiper/Dictionaries/）。
        /// </summary>
        private static string CacheDir =>
            Path.Combine(Application.persistentDataPath, "uPiper", "Dictionaries");

        /// <summary>
        /// バージョンマーカーファイル名。Application.versionをキーとして
        /// アプリ更新時のキャッシュ再構築を判定する。
        /// </summary>
        private const string VersionMarkerFileName = ".cache_version";

        /// <summary>
        /// UnityWebRequestのタイムアウト秒数。
        /// </summary>
        private const int TimeoutSeconds = 30;

        /// <summary>
        /// 辞書ファイルがキャッシュ済みかチェックし、未キャッシュなら
        /// StreamingAssetsからコピーする。
        /// アプリバージョンが変わった場合はキャッシュを再構築する。
        /// </summary>
        /// <param name="fileNames">キャッシュ対象の辞書ファイル名一覧</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        public static async Task EnsureCachedAsync(
            string[] fileNames,
            CancellationToken cancellationToken = default)
        {
            if (fileNames == null || fileNames.Length == 0)
            {
                return;
            }

            try
            {
                var cacheDir = CacheDir;

                // キャッシュディレクトリ作成
                if (!Directory.Exists(cacheDir))
                {
                    Directory.CreateDirectory(cacheDir);
                    PiperLogger.LogInfo($"{Tag} Created cache directory: {cacheDir}");
                }

                // バージョンマーカーを確認し、アプリ更新時はキャッシュを再構築
                if (IsVersionStale(cacheDir))
                {
                    PiperLogger.LogInfo(
                        $"{Tag} App version changed, clearing stale cache");
                    ClearCache(cacheDir);
                    Directory.CreateDirectory(cacheDir);
                }

                var cachedCount = 0;
                var skippedCount = 0;

                foreach (var fileName in fileNames)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var cachedPath = Path.Combine(cacheDir, fileName);
                    if (File.Exists(cachedPath))
                    {
                        skippedCount++;
                        continue;
                    }

                    // StreamingAssetsからUnityWebRequestで読み取り
                    try
                    {
                        var relativePath = $"uPiper/Dictionaries/{fileName}";
                        var url = Application.streamingAssetsPath + "/" + relativePath;
                        var text = await LoadTextFromStreamingAssetsAsync(
                            url, relativePath, cancellationToken);

                        // persistentDataPathに書き込み
                        File.WriteAllText(cachedPath, text);
                        cachedCount++;
                        PiperLogger.LogInfo(
                            $"{Tag} Cached: {fileName} ({text.Length} chars)");
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        PiperLogger.LogWarning(
                            $"{Tag} Failed to cache {fileName}: {e.Message}");
                        // キャッシュ失敗は致命的ではない — 通常のStreamingAssetsロードにフォールバック
                    }
                }

                // バージョンマーカーを更新
                WriteVersionMarker(cacheDir);

                PiperLogger.LogInfo(
                    $"{Tag} Cache complete: {cachedCount} cached, " +
                    $"{skippedCount} already cached");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                PiperLogger.LogWarning(
                    $"{Tag} Cache initialization failed: {e.Message}. " +
                    "Falling back to StreamingAssets loading.");
            }
        }

        /// <summary>
        /// キャッシュ済みファイルのパスを返す。未キャッシュならnull。
        /// </summary>
        /// <param name="fileName">辞書ファイル名（例: "default_tech_dict.json"）</param>
        /// <returns>キャッシュ済みファイルのフルパス。未キャッシュまたはバージョン不一致時はnull</returns>
        public static string GetCachedPath(string fileName)
        {
            var path = Path.Combine(CacheDir, fileName);
            if (File.Exists(path))
            {
                PiperLogger.LogInfo($"{Tag} Cache hit: {fileName}");
                return path;
            }

            PiperLogger.LogInfo($"{Tag} Cache miss: {fileName}");
            return null;
        }

        /// <summary>
        /// キャッシュディレクトリを手動でクリアする。
        /// テストや辞書更新時に使用。
        /// </summary>
        public static void ClearAllCache()
        {
            var cacheDir = CacheDir;
            ClearCache(cacheDir);
            PiperLogger.LogInfo($"{Tag} Cache cleared");
        }

        /// <summary>
        /// 指定ディレクトリ内のファイルを全削除する。
        /// </summary>
        private static void ClearCache(string cacheDir)
        {
            try
            {
                if (Directory.Exists(cacheDir))
                {
                    Directory.Delete(cacheDir, true);
                }
            }
            catch (Exception e)
            {
                PiperLogger.LogWarning(
                    $"{Tag} Failed to clear cache directory: {e.Message}");
            }
        }

        /// <summary>
        /// バージョンマーカーが現在のアプリバージョンと一致しないかチェックする。
        /// </summary>
        private static bool IsVersionStale(string cacheDir)
        {
            var markerPath = Path.Combine(cacheDir, VersionMarkerFileName);
            if (!File.Exists(markerPath))
            {
                return false; // 初回キャッシュ — staleness check不要
            }

            try
            {
                var cachedVersion = File.ReadAllText(markerPath).Trim();
                return cachedVersion != Application.version;
            }
            catch (Exception)
            {
                return true; // 読み取り失敗 → 安全のため再キャッシュ
            }
        }

        /// <summary>
        /// バージョンマーカーファイルを書き込む。
        /// </summary>
        private static void WriteVersionMarker(string cacheDir)
        {
            try
            {
                var markerPath = Path.Combine(cacheDir, VersionMarkerFileName);
                File.WriteAllText(markerPath, Application.version);
            }
            catch (Exception e)
            {
                PiperLogger.LogWarning(
                    $"{Tag} Failed to write version marker: {e.Message}");
            }
        }

        /// <summary>
        /// StreamingAssetsからテキストをUnityWebRequestで読み取る。
        /// </summary>
        private static async Task<string> LoadTextFromStreamingAssetsAsync(
            string url,
            string relativePath,
            CancellationToken cancellationToken)
        {
            using var request = UnityWebRequest.Get(url);
            request.timeout = TimeoutSeconds;

            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException(
                    $"Failed to load '{relativePath}' from StreamingAssets: " +
                    $"{request.error} (HTTP {request.responseCode})");
            }

            return request.downloadHandler.text;
        }
    }
}