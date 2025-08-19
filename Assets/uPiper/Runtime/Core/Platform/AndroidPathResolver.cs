#if UNITY_ANDROID && !UNITY_EDITOR

using System.IO;
using UnityEngine;

namespace uPiper.Core.Platform
{
    /// <summary>
    /// Android-specific path resolver for accessing files in APK
    /// </summary>
    public static class AndroidPathResolver
    {
        /// <summary>
        /// Get the persistent data path for extracted files
        /// </summary>
        public static string GetPersistentPath(string relativePath)
        {
            return Path.Combine(Application.persistentDataPath, relativePath);
        }

        /// <summary>
        /// Get the streaming assets path for Android
        /// </summary>
        public static string GetStreamingAssetsPath(string relativePath)
        {
            // On Android, streaming assets are inside the APK
            return Path.Combine(Application.streamingAssetsPath, relativePath);
        }

        /// <summary>
        /// Check if a file needs to be extracted from APK
        /// </summary>
        public static bool NeedsExtraction(string persistentPath)
        {
            return !File.Exists(persistentPath);
        }

        /// <summary>
        /// Extract a file from streaming assets to persistent data path
        /// </summary>
        public static void ExtractFromStreamingAssets(string relativePath)
        {
            string sourcePath = GetStreamingAssetsPath(relativePath);
            string destPath = GetPersistentPath(relativePath);

            // Create directory if needed
            string destDir = Path.GetDirectoryName(destPath);
            if (!Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            // Use UnityWebRequest for Android streaming assets
            using (var www = UnityEngine.Networking.UnityWebRequest.Get(sourcePath))
            {
                var operation = www.SendWebRequest();
                
                // Block until complete (not ideal but necessary for initialization)
                while (!operation.isDone)
                {
                    System.Threading.Thread.Sleep(10);
                }

                if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    File.WriteAllBytes(destPath, www.downloadHandler.data);
                    Debug.Log($"[AndroidPathResolver] Extracted {relativePath} to {destPath}");
                }
                else
                {
                    throw new System.Exception($"Failed to extract {relativePath}: {www.error}");
                }
            }
        }

        /// <summary>
        /// Get the correct path for OpenJTalk dictionary on Android
        /// </summary>
        public static string GetOpenJTalkDictionaryPath()
        {
            const string dictRelativePath = "uPiper/OpenJTalk/naist_jdic/open_jtalk_dic_utf_8-1.11";
            string persistentPath = GetPersistentPath(dictRelativePath);

            // Check if we need to extract the dictionary
            if (NeedsExtraction(persistentPath))
            {
                Debug.Log("[AndroidPathResolver] Extracting OpenJTalk dictionary from APK...");
                
                // Extract all dictionary files
                string[] dictFiles = new string[]
                {
                    "char.bin",
                    "left-id.def",
                    "matrix.bin",
                    "pos-id.def",
                    "rewrite.def",
                    "right-id.def",
                    "sys.dic",
                    "unk.dic"
                };

                foreach (string file in dictFiles)
                {
                    string fileRelativePath = Path.Combine(dictRelativePath, file);
                    ExtractFromStreamingAssets(fileRelativePath);
                }
            }

            return persistentPath;
        }

        /// <summary>
        /// Get the path to the CMU Pronouncing Dictionary
        /// </summary>
        public static string GetCMUDictionaryPath()
        {
            Debug.Log("[AndroidPathResolver] Getting CMU dictionary path for Android");
            
            string cmuRelativePath = Path.Combine("uPiper", "Phonemizers", "cmudict-0.7b.txt");
            string persistentPath = GetPersistentPath(cmuRelativePath);

            // Check if dictionary needs extraction
            if (NeedsExtraction(persistentPath))
            {
                Debug.Log("[AndroidPathResolver] Extracting CMU dictionary from APK...");
                ExtractFromStreamingAssets(cmuRelativePath);
                
                if (File.Exists(persistentPath))
                {
                    var fileInfo = new FileInfo(persistentPath);
                    Debug.Log($"[AndroidPathResolver] Extracted CMU dictionary to {persistentPath} (size: {fileInfo.Length} bytes)");
                }
                else
                {
                    Debug.LogError($"[AndroidPathResolver] Failed to extract CMU dictionary to {persistentPath}");
                }
            }
            else
            {
                Debug.Log($"[AndroidPathResolver] CMU dictionary already exists at {persistentPath}");
            }

            return persistentPath;
        }
    }
}

#endif