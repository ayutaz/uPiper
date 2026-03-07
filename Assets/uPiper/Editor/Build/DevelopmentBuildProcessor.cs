#if UPIPER_DEVELOPMENT
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace uPiper.Editor.Build
{
    /// <summary>
    /// Development environment build processor that automatically copies
    /// dictionary data from Samples~ to StreamingAssets during build.
    /// This is only needed for development environment where UPIPER_DEVELOPMENT is defined.
    /// </summary>
    public class DevelopmentBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        // Paths
        private const string SAMPLES_PATH = "Assets/uPiper/Samples~";
        private const string STREAMING_ASSETS_PATH = "Assets/StreamingAssets/uPiper";
        private const string TEMP_MARKER_FILE = "Assets/StreamingAssets/.upiper_temp";

        // Source folders in Samples~
        private const string CMU_SOURCE = "CMU Pronouncing Dictionary";

        // Target folders in StreamingAssets
        private const string PHONEMIZERS_TARGET = "Phonemizers";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            Debug.Log("[DevelopmentBuildProcessor] Starting pre-build setup for development environment");

            try
            {
                // Create StreamingAssets directory structure
                CreateStreamingAssetsStructure();

                // Copy CMU dictionary
                CopyCMUDictionary();

                // Create marker file to indicate this is temporary
                File.WriteAllText(TEMP_MARKER_FILE, "Temporary files created by DevelopmentBuildProcessor");

                // Refresh AssetDatabase to recognize new files
                AssetDatabase.Refresh();

                Debug.Log("[DevelopmentBuildProcessor] Pre-build setup completed successfully");
            }
            catch (Exception e)
            {
                Debug.LogError($"[DevelopmentBuildProcessor] Failed to setup build: {e.Message}");
                throw new BuildFailedException($"Failed to setup development build: {e.Message}");
            }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            Debug.Log("[DevelopmentBuildProcessor] Starting post-build cleanup for development environment");

            try
            {
                // Only clean up if we created the temporary files
                if (File.Exists(TEMP_MARKER_FILE))
                {
                    CleanupStreamingAssets();
                    Debug.Log("[DevelopmentBuildProcessor] Post-build cleanup completed successfully");
                }
                else
                {
                    Debug.Log("[DevelopmentBuildProcessor] Skipping cleanup - StreamingAssets was not created by this processor");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DevelopmentBuildProcessor] Failed to cleanup after build: {e.Message}");
                // Don't throw exception in post-build to avoid breaking the build
            }
        }

        private void CreateStreamingAssetsStructure()
        {
            // Create main directories
            Directory.CreateDirectory(Path.Combine(STREAMING_ASSETS_PATH, PHONEMIZERS_TARGET));

            Debug.Log($"[DevelopmentBuildProcessor] Created directory structure at: {STREAMING_ASSETS_PATH}");
        }

        private void CopyCMUDictionary()
        {
            var sourceFile = Path.Combine(SAMPLES_PATH, CMU_SOURCE, "cmudict-0.7b.txt");
            var targetFile = Path.Combine(STREAMING_ASSETS_PATH, PHONEMIZERS_TARGET, "cmudict-0.7b.txt");

            Debug.Log($"[DevelopmentBuildProcessor] CMU dictionary source: {sourceFile}");
            Debug.Log($"[DevelopmentBuildProcessor] CMU dictionary target: {targetFile}");
            Debug.Log($"[DevelopmentBuildProcessor] Source file exists: {File.Exists(sourceFile)}");

            if (!File.Exists(sourceFile))
            {
                // Try to find the file
                var samplesDir = Path.Combine(SAMPLES_PATH, CMU_SOURCE);
                if (Directory.Exists(samplesDir))
                {
                    var files = Directory.GetFiles(samplesDir, "*.txt");
                    Debug.LogError($"[DevelopmentBuildProcessor] CMU dictionary not found at: {sourceFile}");
                    Debug.LogError($"[DevelopmentBuildProcessor] Available files in {samplesDir}:");
                    foreach (var file in files)
                    {
                        Debug.LogError($"  - {Path.GetFileName(file)}");
                    }
                }
                throw new FileNotFoundException($"CMU dictionary not found at: {sourceFile}");
            }

            // Ensure target directory exists
            var targetDir = Path.GetDirectoryName(targetFile);
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
                Debug.Log($"[DevelopmentBuildProcessor] Created directory: {targetDir}");
            }

            // Copy the file
            File.Copy(sourceFile, targetFile, true);

            // Verify the copy
            if (File.Exists(targetFile))
            {
                var fileInfo = new FileInfo(targetFile);
                Debug.Log($"[DevelopmentBuildProcessor] Successfully copied CMU dictionary to {targetFile} (size: {fileInfo.Length} bytes)");
            }
            else
            {
                throw new IOException($"Failed to copy CMU dictionary to {targetFile}");
            }
        }

        private void CleanupStreamingAssets()
        {
            try
            {
                // Delete the marker file first
                if (File.Exists(TEMP_MARKER_FILE))
                {
                    File.Delete(TEMP_MARKER_FILE);
                }

                // Delete the entire uPiper StreamingAssets folder
                if (Directory.Exists(STREAMING_ASSETS_PATH))
                {
                    Directory.Delete(STREAMING_ASSETS_PATH, true);
                    Debug.Log($"[DevelopmentBuildProcessor] Deleted temporary StreamingAssets at: {STREAMING_ASSETS_PATH}");
                }

                // Delete StreamingAssets folder if it's empty
                var streamingAssetsRoot = Path.GetDirectoryName(STREAMING_ASSETS_PATH);
                if (Directory.Exists(streamingAssetsRoot) &&
                    Directory.GetFiles(streamingAssetsRoot).Length == 0 &&
                    Directory.GetDirectories(streamingAssetsRoot).Length == 0)
                {
                    Directory.Delete(streamingAssetsRoot);
                    Debug.Log($"[DevelopmentBuildProcessor] Deleted empty StreamingAssets folder");
                }

                // Delete .meta files
                var metaFile = STREAMING_ASSETS_PATH + ".meta";
                if (File.Exists(metaFile))
                {
                    File.Delete(metaFile);
                }

                var rootMetaFile = streamingAssetsRoot + ".meta";
                if (File.Exists(rootMetaFile))
                {
                    File.Delete(rootMetaFile);
                }

                // Refresh AssetDatabase to remove references
                AssetDatabase.Refresh();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DevelopmentBuildProcessor] Error during cleanup: {e.Message}");
            }
        }
    }
}
#endif // UPIPER_DEVELOPMENT