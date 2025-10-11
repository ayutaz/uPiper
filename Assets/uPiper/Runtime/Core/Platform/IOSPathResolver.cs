#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace uPiper.Core.Platform
{
    /// <summary>
    /// iOS-specific path resolver for handling StreamingAssets access.
    /// On iOS, StreamingAssets are embedded in the app bundle and require special handling.
    /// </summary>
    public static class IOSPathResolver
    {
        /// <summary>
        /// Gets the OpenJTalk dictionary path for iOS.
        /// </summary>
        public static string GetOpenJTalkDictionaryPath()
        {
            // On iOS, StreamingAssets are in Application.dataPath/Raw
            // Dictionary files are directly in naist_jdic folder
            var basePath = Path.Combine(Application.dataPath, "Raw", "uPiper", "OpenJTalk", "naist_jdic");
            return basePath;
        }

        /// <summary>
        /// Checks if the dictionary exists at the expected location.
        /// </summary>
        public static bool DictionaryExists()
        {
            var dictPath = GetOpenJTalkDictionaryPath();
            
            // Check for essential dictionary files
            var essentialFiles = new[] { "sys.dic", "unk.dic", "matrix.bin", "char.bin" };
            
            foreach (var file in essentialFiles)
            {
                var filePath = Path.Combine(dictPath, file);
                if (!File.Exists(filePath))
                {
                    Debug.LogError($"[IOSPathResolver] Essential dictionary file not found: {filePath}");
                    return false;
                }
            }
            
            return true;
        }

        /// <summary>
        /// Gets the full path to a file in StreamingAssets on iOS.
        /// </summary>
        /// <param name="relativePath">Path relative to StreamingAssets</param>
        /// <returns>Full path to the file</returns>
        public static string GetStreamingAssetsPath(string relativePath)
        {
            return Path.Combine(Application.dataPath, "Raw", relativePath);
        }

        /// <summary>
        /// Asynchronously loads a file from StreamingAssets on iOS.
        /// Uses UnityWebRequest for compatibility with iOS file system.
        /// </summary>
        public static IEnumerator LoadFileAsync(string relativePath, Action<byte[]> onSuccess, Action<string> onError)
        {
            var fullPath = GetStreamingAssetsPath(relativePath);
            
            // On iOS, we might need to use file:// protocol
            if (!fullPath.StartsWith("file://"))
            {
                fullPath = "file://" + fullPath;
            }

            using (var www = UnityWebRequest.Get(fullPath))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    onSuccess?.Invoke(www.downloadHandler.data);
                }
                else
                {
                    onError?.Invoke($"Failed to load file: {www.error}");
                }
            }
        }

        /// <summary>
        /// Synchronously loads a text file from StreamingAssets on iOS.
        /// </summary>
        public static string LoadTextFile(string relativePath)
        {
            var fullPath = GetStreamingAssetsPath(relativePath);
            
            try
            {
                return File.ReadAllText(fullPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[IOSPathResolver] Failed to load text file: {fullPath}, Error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Synchronously loads a binary file from StreamingAssets on iOS.
        /// </summary>
        public static byte[] LoadBinaryFile(string relativePath)
        {
            var fullPath = GetStreamingAssetsPath(relativePath);
            
            try
            {
                return File.ReadAllBytes(fullPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[IOSPathResolver] Failed to load binary file: {fullPath}, Error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Lists all files in a directory within StreamingAssets on iOS.
        /// </summary>
        public static string[] ListFiles(string relativePath, string searchPattern = "*")
        {
            var fullPath = GetStreamingAssetsPath(relativePath);
            
            try
            {
                if (Directory.Exists(fullPath))
                {
                    return Directory.GetFiles(fullPath, searchPattern);
                }
                else
                {
                    Debug.LogError($"[IOSPathResolver] Directory not found: {fullPath}");
                    return new string[0];
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[IOSPathResolver] Failed to list files: {fullPath}, Error: {ex.Message}");
                return new string[0];
            }
        }

        /// <summary>
        /// Gets the size of the dictionary in bytes.
        /// </summary>
        public static long GetDictionarySize()
        {
            var dictPath = GetOpenJTalkDictionaryPath();
            long totalSize = 0;

            try
            {
                var files = Directory.GetFiles(dictPath, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    totalSize += fileInfo.Length;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[IOSPathResolver] Failed to calculate dictionary size: {ex.Message}");
            }

            return totalSize;
        }

        /// <summary>
        /// Logs detailed information about the dictionary files.
        /// </summary>
        public static void LogDictionaryInfo()
        {
            Debug.Log("[IOSPathResolver] Dictionary Information:");
            Debug.Log($"  Path: {GetOpenJTalkDictionaryPath()}");
            Debug.Log($"  Exists: {DictionaryExists()}");
            
            if (DictionaryExists())
            {
                var size = GetDictionarySize();
                Debug.Log($"  Total Size: {size / 1024 / 1024}MB ({size} bytes)");
                
                var dictPath = GetOpenJTalkDictionaryPath();
                var files = Directory.GetFiles(dictPath);
                Debug.Log($"  File Count: {files.Length}");
                
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    Debug.Log($"    - {fileInfo.Name}: {fileInfo.Length} bytes");
                }
            }
        }
    }
}
#endif