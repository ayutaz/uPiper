using System;
using System.IO;
using UnityEngine;

namespace uPiper.Editor.Utilities
{
    /// <summary>
    /// Helper class for file and directory operations in the editor.
    /// </summary>
    public static class FileOperationHelper
    {
        /// <summary>
        /// Copy a directory recursively, optionally skipping .meta files.
        /// </summary>
        /// <param name="sourceDir">Source directory path.</param>
        /// <param name="destDir">Destination directory path.</param>
        /// <param name="skipMetaFiles">If true, skip .meta files (default: true).</param>
        /// <param name="overwrite">If true, overwrite existing files (default: true).</param>
        public static void CopyDirectory(string sourceDir, string destDir, bool skipMetaFiles = true, bool overwrite = true)
        {
            if (!Directory.Exists(sourceDir))
            {
                throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");
            }

            if (!Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            // Copy files
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(file);

                // Skip .meta files if requested
                if (skipMetaFiles && fileName.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    continue;

                var destFile = Path.Combine(destDir, fileName);
                File.Copy(file, destFile, overwrite);
            }

            // Copy subdirectories recursively
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(dir);
                var destSubDir = Path.Combine(destDir, dirName);
                CopyDirectory(dir, destSubDir, skipMetaFiles, overwrite);
            }
        }

        /// <summary>
        /// Copy a directory recursively with progress reporting.
        /// </summary>
        /// <param name="sourceDir">Source directory path.</param>
        /// <param name="destDir">Destination directory path.</param>
        /// <param name="description">Description for logging.</param>
        /// <param name="skipMetaFiles">If true, skip .meta files (default: true).</param>
        /// <returns>Tuple of (success, fileCount).</returns>
        public static (bool success, int fileCount) CopyDirectoryWithCount(string sourceDir, string destDir, string description, bool skipMetaFiles = true)
        {
            if (!Directory.Exists(sourceDir))
            {
                Debug.LogWarning($"[FileOperationHelper] Source directory not found for {description}: {sourceDir}");
                return (false, 0);
            }

            try
            {
                if (!Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                var fileCount = CopyDirectoryRecursive(sourceDir, destDir, skipMetaFiles);

                Debug.Log($"[FileOperationHelper] Copied {fileCount} files for {description}");
                return (true, fileCount);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FileOperationHelper] Failed to copy {description}: {ex.Message}");
                return (false, 0);
            }
        }

        private static int CopyDirectoryRecursive(string sourceDir, string destDir, bool skipMetaFiles)
        {
            var fileCount = 0;

            // Copy files
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(file);

                // Skip .meta files if requested
                if (skipMetaFiles && fileName.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    continue;

                var destFile = Path.Combine(destDir, fileName);
                File.Copy(file, destFile, true);
                fileCount++;
            }

            // Copy subdirectories recursively
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(dir);
                var destSubDir = Path.Combine(destDir, dirName);

                if (!Directory.Exists(destSubDir))
                {
                    Directory.CreateDirectory(destSubDir);
                }

                fileCount += CopyDirectoryRecursive(dir, destSubDir, skipMetaFiles);
            }

            return fileCount;
        }

        /// <summary>
        /// Delete a directory and all its contents safely.
        /// </summary>
        /// <param name="directoryPath">Directory to delete.</param>
        /// <param name="deleteMetaFile">If true, also delete the associated .meta file.</param>
        /// <returns>True if successfully deleted.</returns>
        public static bool DeleteDirectorySafe(string directoryPath, bool deleteMetaFile = true)
        {
            try
            {
                if (Directory.Exists(directoryPath))
                {
                    Directory.Delete(directoryPath, true);
                }

                if (deleteMetaFile)
                {
                    var metaPath = directoryPath + ".meta";
                    if (File.Exists(metaPath))
                    {
                        File.Delete(metaPath);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FileOperationHelper] Failed to delete directory {directoryPath}: {ex.Message}");
                return false;
            }
        }
    }
}
