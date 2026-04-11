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
    /// Asynchronous loader for StreamingAssets that works on both WebGL and non-WebGL platforms.
    /// On WebGL, files must be loaded via UnityWebRequest since direct file system access is not available.
    /// On other platforms, standard file I/O is used.
    /// </summary>
    public static class WebGLStreamingAssetsLoader
    {
        /// <summary>
        /// Default timeout in seconds for dictionary/small file requests.
        /// </summary>
        public const int DefaultTimeoutSeconds = 30;

        /// <summary>
        /// Timeout in seconds for large file requests (e.g. ONNX models).
        /// </summary>
        public const int ModelTimeoutSeconds = 120;

        /// <summary>
        /// Default maximum number of retry attempts on network failure.
        /// </summary>
        public const int DefaultMaxRetries = 3;

        /// <summary>
        /// Asynchronously loads a file from StreamingAssets as a byte array.
        /// </summary>
        /// <param name="relativePath">Relative path within StreamingAssets (e.g. "uPiper/Dictionaries/dict.json")</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>File contents as byte array</returns>
        public static async Task<byte[]> LoadBytesAsync(
            string relativePath,
            CancellationToken cancellationToken = default)
        {
            return await LoadBytesAsync(relativePath, null, cancellationToken);
        }

        /// <summary>
        /// Asynchronously loads a file from StreamingAssets as a byte array with progress reporting.
        /// </summary>
        /// <param name="relativePath">Relative path within StreamingAssets (e.g. "uPiper/Dictionaries/dict.json")</param>
        /// <param name="progress">Progress reporter (0.0 to 1.0)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>File contents as byte array</returns>
        public static async Task<byte[]> LoadBytesAsync(
            string relativePath,
            IProgress<float> progress,
            CancellationToken cancellationToken = default)
        {
            return await LoadBytesAsync(
                relativePath, progress, DefaultMaxRetries, DefaultTimeoutSeconds,
                cancellationToken);
        }

        /// <summary>
        /// Asynchronously loads a file from StreamingAssets as a byte array with retry and timeout.
        /// </summary>
        /// <param name="relativePath">Relative path within StreamingAssets (e.g. "uPiper/Dictionaries/dict.json")</param>
        /// <param name="progress">Progress reporter (0.0 to 1.0)</param>
        /// <param name="maxRetries">Maximum number of retry attempts (default: 3)</param>
        /// <param name="timeoutSeconds">Timeout per request in seconds (default: 30)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>File contents as byte array</returns>
        public static async Task<byte[]> LoadBytesAsync(
            string relativePath,
            IProgress<float> progress,
            int maxRetries,
            int timeoutSeconds,
            CancellationToken cancellationToken = default)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var url = Application.streamingAssetsPath + "/" + relativePath.Replace('\\', '/');
            return await SendWithRetryAsync(
                url, relativePath, progress, maxRetries, timeoutSeconds,
                r => r.downloadHandler.data, cancellationToken);
#elif UNITY_ANDROID && !UNITY_EDITOR
            var url = Application.streamingAssetsPath + "/" + relativePath.Replace('\\', '/');
            return await SendWithRetryAsync(
                url, relativePath, progress, maxRetries, timeoutSeconds,
                r => r.downloadHandler.data, cancellationToken);
#else
            var fullPath = Path.Combine(Application.streamingAssetsPath, relativePath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(
                    $"StreamingAssets file not found: '{relativePath}'", fullPath);
            }

            progress?.Report(0f);
            var data = await File.ReadAllBytesAsync(fullPath, cancellationToken);
            progress?.Report(1f);
            return data;
#endif
        }

        /// <summary>
        /// Asynchronously loads a file from StreamingAssets as a string.
        /// </summary>
        /// <param name="relativePath">Relative path within StreamingAssets (e.g. "uPiper/Dictionaries/dict.json")</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>File contents as string</returns>
        public static async Task<string> LoadTextAsync(
            string relativePath,
            CancellationToken cancellationToken = default)
        {
            return await LoadTextAsync(relativePath, null, cancellationToken);
        }

        /// <summary>
        /// Asynchronously loads a file from StreamingAssets as a string with progress reporting.
        /// </summary>
        /// <param name="relativePath">Relative path within StreamingAssets (e.g. "uPiper/Dictionaries/dict.json")</param>
        /// <param name="progress">Progress reporter (0.0 to 1.0)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>File contents as string</returns>
        public static async Task<string> LoadTextAsync(
            string relativePath,
            IProgress<float> progress,
            CancellationToken cancellationToken = default)
        {
            return await LoadTextAsync(
                relativePath, progress, DefaultMaxRetries, DefaultTimeoutSeconds,
                cancellationToken);
        }

        /// <summary>
        /// Asynchronously loads a file from StreamingAssets as a string with retry and timeout.
        /// </summary>
        /// <param name="relativePath">Relative path within StreamingAssets (e.g. "uPiper/Dictionaries/dict.json")</param>
        /// <param name="progress">Progress reporter (0.0 to 1.0)</param>
        /// <param name="maxRetries">Maximum number of retry attempts (default: 3)</param>
        /// <param name="timeoutSeconds">Timeout per request in seconds (default: 30)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>File contents as string</returns>
        public static async Task<string> LoadTextAsync(
            string relativePath,
            IProgress<float> progress,
            int maxRetries,
            int timeoutSeconds,
            CancellationToken cancellationToken = default)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var url = Application.streamingAssetsPath + "/" + relativePath.Replace('\\', '/');
            return await SendWithRetryAsync(
                url, relativePath, progress, maxRetries, timeoutSeconds,
                r => r.downloadHandler.text, cancellationToken);
#elif UNITY_ANDROID && !UNITY_EDITOR
            var url = Application.streamingAssetsPath + "/" + relativePath.Replace('\\', '/');
            return await SendWithRetryAsync(
                url, relativePath, progress, maxRetries, timeoutSeconds,
                r => r.downloadHandler.text, cancellationToken);
#else
            var fullPath = Path.Combine(Application.streamingAssetsPath, relativePath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(
                    $"StreamingAssets file not found: '{relativePath}'", fullPath);
            }

            progress?.Report(0f);
            var text = await File.ReadAllTextAsync(fullPath, cancellationToken);
            progress?.Report(1f);
            return text;
#endif
        }

#if (UNITY_WEBGL || UNITY_ANDROID) && !UNITY_EDITOR
        /// <summary>
        /// Sends a UnityWebRequest with retry logic, timeout, and progress reporting.
        /// On network failure, retries up to <paramref name="maxRetries"/> times with exponential backoff
        /// (1s, 2s, 4s, ...).
        /// </summary>
        /// <typeparam name="T">Return type extracted from the response</typeparam>
        /// <param name="url">Full URL to request</param>
        /// <param name="relativePath">Original relative path (for error messages)</param>
        /// <param name="progress">Progress reporter (0.0 to 1.0)</param>
        /// <param name="maxRetries">Maximum number of retry attempts</param>
        /// <param name="timeoutSeconds">Timeout per request in seconds</param>
        /// <param name="extractor">Function to extract the result from a successful response</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Extracted result of type T</returns>
        private static async Task<T> SendWithRetryAsync<T>(
            string url,
            string relativePath,
            IProgress<float> progress,
            int maxRetries,
            int timeoutSeconds,
            Func<UnityWebRequest, T> extractor,
            CancellationToken cancellationToken)
        {
            string lastError = null;
            long lastResponseCode = 0;

            for (var attempt = 0; attempt <= maxRetries; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var request = UnityWebRequest.Get(url);
                request.timeout = timeoutSeconds;

                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(operation.progress);
                    await Task.Yield();
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    progress?.Report(1f);
                    return extractor(request);
                }

                lastError = request.error;
                lastResponseCode = request.responseCode;

                if (attempt < maxRetries)
                {
                    var delayMs = (int)Math.Pow(2, attempt) * 1000;
                    PiperLogger.LogWarning(
                        $"[WebGLStreamingAssetsLoader] Retry {attempt + 1}/{maxRetries} " +
                        $"for '{relativePath}' (error: {lastError}, " +
                        $"HTTP {lastResponseCode}). " +
                        $"Waiting {delayMs}ms before next attempt.");
                    await Task.Delay(delayMs, cancellationToken);
                }
            }

            var message =
                $"Failed to load StreamingAssets file '{relativePath}' " +
                $"after {maxRetries} retries. " +
                $"URL: {url}, HTTP status: {lastResponseCode}, " +
                $"error: {lastError}";
            PiperLogger.LogError($"[WebGLStreamingAssetsLoader] {message}");
            throw new InvalidOperationException(message);
        }
#endif
    }
}