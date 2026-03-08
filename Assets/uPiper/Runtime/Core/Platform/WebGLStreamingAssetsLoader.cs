using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

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
#if UNITY_WEBGL && !UNITY_EDITOR
            var url = Path.Combine(Application.streamingAssetsPath, relativePath);
            using var request = UnityWebRequest.Get(url);
            await SendWebRequestWithProgressAsync(request, progress, cancellationToken);

            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException(
                    $"Failed to load StreamingAssets file '{relativePath}': {request.error}");
            }

            progress?.Report(1f);
            return request.downloadHandler.data;
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
#if UNITY_WEBGL && !UNITY_EDITOR
            var url = Path.Combine(Application.streamingAssetsPath, relativePath);
            using var request = UnityWebRequest.Get(url);
            await SendWebRequestWithProgressAsync(request, progress, cancellationToken);

            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException(
                    $"Failed to load StreamingAssets file '{relativePath}': {request.error}");
            }

            progress?.Report(1f);
            return request.downloadHandler.text;
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

#if UNITY_WEBGL && !UNITY_EDITOR
        /// <summary>
        /// Sends a UnityWebRequest with periodic progress reporting.
        /// </summary>
        private static async Task SendWebRequestWithProgressAsync(
            UnityWebRequest request,
            IProgress<float> progress,
            CancellationToken cancellationToken)
        {
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(operation.progress);
                await Task.Yield();
            }
        }

        /// <summary>
        /// Converts a UnityWebRequestAsyncOperation to a Task using TaskCompletionSource.
        /// </summary>
        private static Task SendWebRequestAsync(
            UnityWebRequest request,
            CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();

            if (cancellationToken.IsCancellationRequested)
            {
                tcs.SetCanceled();
                return tcs.Task;
            }

            var registration = cancellationToken.Register(() =>
            {
                request.Abort();
                tcs.TrySetCanceled();
            });

            request.SendWebRequest().completed += _ =>
            {
                registration.Dispose();
                if (cancellationToken.IsCancellationRequested)
                {
                    tcs.TrySetCanceled();
                }
                else
                {
                    tcs.TrySetResult(true);
                }
            };

            return tcs.Task;
        }
#endif
    }
}