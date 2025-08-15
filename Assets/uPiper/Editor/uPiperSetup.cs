using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace uPiper.Editor
{
    /// <summary>
    /// Initial setup wizard for uPiper package
    /// Copies required files from package to project Assets
    /// </summary>
    public static class uPiperSetup
    {
        private const string SETUP_COMPLETE_KEY = "uPiper_InitialSetupComplete_v1";
        private const string PACKAGE_NAME = "com.ayutaz.upiper";
        
        // Target paths in Assets
        private const string TARGET_PLUGINS_PATH = "Assets/uPiper/Plugins";
        private const string TARGET_STREAMING_ASSETS_PATH = "Assets/StreamingAssets/uPiper";
        
        [InitializeOnLoadMethod]
        static void CheckFirstTimeSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (!IsSetupComplete() && IsPackageInstalled())
                {
                    if (EditorUtility.DisplayDialog(
                        "uPiper Initial Setup Required",
                        "uPiper needs to copy required files to your project.\n\n" +
                        "This will copy:\n" +
                        "• Native plugins (OpenJTalk)\n" +
                        "• OpenJTalk dictionary files\n" +
                        "• Phonemizer data (CMU dictionary)\n\n" +
                        "This is required for the package to work correctly.\n\n" +
                        "Continue with setup?",
                        "Setup Now", "Later"))
                    {
                        RunInitialSetup();
                    }
                }
            };
        }
        
        [MenuItem("uPiper/Setup/Run Initial Setup", false, 1)]
        public static void RunInitialSetupMenu()
        {
            if (EditorUtility.DisplayDialog(
                "uPiper Setup",
                "This will copy required files from the package to your project.\n\n" +
                "Files will be copied to:\n" +
                "• Assets/uPiper/Plugins/\n" +
                "• Assets/StreamingAssets/uPiper/\n\n" +
                "Existing files will be overwritten.\n\n" +
                "Continue?",
                "Setup", "Cancel"))
            {
                RunInitialSetup();
            }
        }
        
        [MenuItem("uPiper/Setup/Check Setup Status", false, 2)]
        public static void CheckSetupStatus()
        {
            var status = GetSetupStatus();
            
            var message = "uPiper Setup Status:\n\n";
            message += $"• Plugins: {(status.pluginsExist ? "✓ Installed" : "✗ Not found")}\n";
            message += $"• OpenJTalk Dictionary: {(status.dictionaryExists ? "✓ Installed" : "✗ Not found")}\n";
            message += $"• CMU Dictionary: {(status.cmuDictExists ? "✓ Installed" : "✗ Not found")}\n";
            message += $"• Setup Complete: {(status.isComplete ? "✓ Yes" : "✗ No")}\n";
            
            if (!status.isComplete)
            {
                message += "\nSome files are missing. Run 'uPiper/Setup/Run Initial Setup' to fix.";
            }
            
            EditorUtility.DisplayDialog("uPiper Setup Status", message, "OK");
        }
        
        public static void RunInitialSetup()
        {
            try
            {
                var packagePath = GetPackagePath();
                if (string.IsNullOrEmpty(packagePath))
                {
                    EditorUtility.DisplayDialog("Error", 
                        "Could not find uPiper package. Please ensure it's properly installed via Package Manager.", 
                        "OK");
                    return;
                }
                
                Debug.Log($"[uPiper Setup] Starting setup from package: {packagePath}");
                
                var success = true;
                var copiedFiles = 0;
                
                // Copy plugins
                var pluginsResult = CopyPlugins(packagePath);
                success &= pluginsResult.success;
                copiedFiles += pluginsResult.fileCount;
                
                // Copy OpenJTalk dictionary
                var dictResult = CopyOpenJTalkDictionary(packagePath);
                success &= dictResult.success;
                copiedFiles += dictResult.fileCount;
                
                // Copy phonemizer data
                var phonemizerResult = CopyPhonemizerData(packagePath);
                success &= phonemizerResult.success;
                copiedFiles += phonemizerResult.fileCount;
                
                if (success)
                {
                    MarkSetupComplete();
                    AssetDatabase.Refresh();
                    
                    EditorUtility.DisplayDialog("Setup Complete", 
                        $"uPiper setup completed successfully!\n\n" +
                        $"Copied {copiedFiles} files to your project.\n\n" +
                        $"The package is now ready to use.",
                        "OK");
                    
                    Debug.Log($"[uPiper Setup] Setup completed. Copied {copiedFiles} files.");
                }
                else
                {
                    EditorUtility.DisplayDialog("Setup Failed", 
                        "Some files could not be copied. Check the Console for details.", 
                        "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[uPiper Setup] Setup failed: {ex.Message}\n{ex.StackTrace}");
                EditorUtility.DisplayDialog("Setup Error", 
                    $"An error occurred during setup:\n{ex.Message}", 
                    "OK");
            }
        }
        
        private static (bool success, int fileCount) CopyPlugins(string packagePath)
        {
            var sourcePath = Path.Combine(packagePath, "Assets", "uPiper", "Plugins");
            var targetPath = TARGET_PLUGINS_PATH;
            
            if (!Directory.Exists(sourcePath))
            {
                // Try alternate path
                sourcePath = Path.Combine(packagePath, "Plugins");
                if (!Directory.Exists(sourcePath))
                {
                    Debug.LogError($"[uPiper Setup] Plugins source not found at: {sourcePath}");
                    return (false, 0);
                }
            }
            
            return CopyDirectory(sourcePath, targetPath, "Plugins");
        }
        
        private static (bool success, int fileCount) CopyOpenJTalkDictionary(string packagePath)
        {
            var sourcePath = Path.Combine(packagePath, "Assets", "StreamingAssets", "uPiper", "OpenJTalk");
            var targetPath = Path.Combine(TARGET_STREAMING_ASSETS_PATH, "OpenJTalk");
            
            if (!Directory.Exists(sourcePath))
            {
                // Try alternate path
                sourcePath = Path.Combine(packagePath, "StreamingAssets", "uPiper", "OpenJTalk");
                if (!Directory.Exists(sourcePath))
                {
                    Debug.LogError($"[uPiper Setup] OpenJTalk dictionary source not found at: {sourcePath}");
                    return (false, 0);
                }
            }
            
            return CopyDirectory(sourcePath, targetPath, "OpenJTalk Dictionary");
        }
        
        private static (bool success, int fileCount) CopyPhonemizerData(string packagePath)
        {
            var sourcePath = Path.Combine(packagePath, "Assets", "StreamingAssets", "uPiper", "Phonemizers");
            var targetPath = Path.Combine(TARGET_STREAMING_ASSETS_PATH, "Phonemizers");
            
            if (!Directory.Exists(sourcePath))
            {
                // Try alternate path
                sourcePath = Path.Combine(packagePath, "StreamingAssets", "uPiper", "Phonemizers");
                if (!Directory.Exists(sourcePath))
                {
                    Debug.LogError($"[uPiper Setup] Phonemizer data source not found at: {sourcePath}");
                    return (false, 0);
                }
            }
            
            return CopyDirectory(sourcePath, targetPath, "Phonemizer Data");
        }
        
        private static (bool success, int fileCount) CopyDirectory(string sourcePath, string targetPath, string description)
        {
            try
            {
                Debug.Log($"[uPiper Setup] Copying {description} from: {sourcePath} to: {targetPath}");
                
                // Create target directory
                Directory.CreateDirectory(targetPath);
                
                var fileCount = 0;
                
                // Copy all files recursively
                foreach (var sourceFile in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
                {
                    // Skip meta files from source
                    if (sourceFile.EndsWith(".meta"))
                        continue;
                    
                    var relativePath = GetRelativePath(sourcePath, sourceFile);
                    var targetFile = Path.Combine(targetPath, relativePath);
                    
                    // Create directory if needed
                    var targetDir = Path.GetDirectoryName(targetFile);
                    if (!Directory.Exists(targetDir))
                        Directory.CreateDirectory(targetDir);
                    
                    File.Copy(sourceFile, targetFile, true);
                    fileCount++;
                    
                    // Show progress for large copies
                    if (fileCount % 10 == 0)
                    {
                        EditorUtility.DisplayProgressBar("uPiper Setup", 
                            $"Copying {description}... ({fileCount} files)", 
                            0.5f);
                    }
                }
                
                EditorUtility.ClearProgressBar();
                Debug.Log($"[uPiper Setup] Successfully copied {fileCount} files for {description}");
                return (true, fileCount);
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[uPiper Setup] Failed to copy {description}: {ex.Message}");
                return (false, 0);
            }
        }
        
        private static string GetRelativePath(string basePath, string fullPath)
        {
            var baseUri = new Uri(basePath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
            var fullUri = new Uri(fullPath);
            var relativeUri = baseUri.MakeRelativeUri(fullUri);
            return Uri.UnescapeDataString(relativeUri.ToString().Replace('/', Path.DirectorySeparatorChar));
        }
        
        private static bool IsPackageInstalled()
        {
            return !string.IsNullOrEmpty(GetPackagePath());
        }
        
        private static string GetPackagePath()
        {
            // Check if uPiper is in Assets (local installation)
            var localPath = Path.Combine(Application.dataPath, "uPiper");
            if (Directory.Exists(localPath))
            {
                // Local installation, files should already be in place
                return null;
            }
            
            // Search in PackageCache
            var packageCachePath = Path.Combine(Application.dataPath, "..", "Library", "PackageCache");
            if (Directory.Exists(packageCachePath))
            {
                var packageDirs = Directory.GetDirectories(packageCachePath, $"{PACKAGE_NAME}@*");
                if (packageDirs.Length > 0)
                {
                    return packageDirs[0];
                }
            }
            
            // Try using PackageManager API
            try
            {
                var listRequest = UnityEditor.PackageManager.Client.List(true);
                while (!listRequest.IsCompleted)
                    System.Threading.Thread.Sleep(10);
                
                if (listRequest.Status == UnityEditor.PackageManager.StatusCode.Success)
                {
                    foreach (var package in listRequest.Result)
                    {
                        if (package.name == PACKAGE_NAME)
                        {
                            return package.resolvedPath;
                        }
                    }
                }
            }
            catch { }
            
            return null;
        }
        
        private static bool IsSetupComplete()
        {
            // Check if marked as complete
            if (!EditorPrefs.GetBool(SETUP_COMPLETE_KEY, false))
                return false;
            
            // Verify files actually exist
            var status = GetSetupStatus();
            return status.isComplete;
        }
        
        private static void MarkSetupComplete()
        {
            EditorPrefs.SetBool(SETUP_COMPLETE_KEY, true);
        }
        
        private static SetupStatus GetSetupStatus()
        {
            var status = new SetupStatus();
            
            // Check plugins
            var windowsPlugin = Path.Combine(TARGET_PLUGINS_PATH, "Windows", "x86_64", "openjtalk_wrapper.dll");
            var macPlugin = Path.Combine(TARGET_PLUGINS_PATH, "macOS", "openjtalk_wrapper.bundle");
            var linuxPlugin = Path.Combine(TARGET_PLUGINS_PATH, "Linux", "x86_64", "libopenjtalk_wrapper.so");
            status.pluginsExist = File.Exists(windowsPlugin) || Directory.Exists(macPlugin) || File.Exists(linuxPlugin);
            
            // Check OpenJTalk dictionary
            var dictPath = Path.Combine(TARGET_STREAMING_ASSETS_PATH, "OpenJTalk", "naist_jdic", "open_jtalk_dic_utf_8-1.11");
            status.dictionaryExists = Directory.Exists(dictPath);
            
            // Check CMU dictionary
            var cmuPath = Path.Combine(TARGET_STREAMING_ASSETS_PATH, "Phonemizers", "cmudict-0.7b.txt");
            status.cmuDictExists = File.Exists(cmuPath);
            
            status.isComplete = status.pluginsExist && status.dictionaryExists && status.cmuDictExists;
            
            return status;
        }
        
        private struct SetupStatus
        {
            public bool pluginsExist;
            public bool dictionaryExists;
            public bool cmuDictExists;
            public bool isComplete;
        }
    }
}