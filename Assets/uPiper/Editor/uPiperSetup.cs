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
    /// 
    /// Note: This setup is disabled when UPIPER_DEVELOPMENT define is set,
    /// as development environment already has files in correct locations.
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
#if !UPIPER_DEVELOPMENT
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
#endif
        }
        
        [MenuItem("uPiper/Setup/Run Initial Setup", false, 1)]
        public static void RunInitialSetupMenu()
        {
#if UPIPER_DEVELOPMENT
            EditorUtility.DisplayDialog(
                "Development Mode",
                "Setup is not required in development mode.\n\n" +
                "Files are already in the correct locations.",
                "OK");
            return;
#else
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
#endif
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
            
#if UPIPER_DEVELOPMENT
            message += "\n[Development Mode - Setup not required]";
#else
            if (!status.isComplete)
            {
                message += "\nSome files are missing. Run 'uPiper/Setup/Run Initial Setup' to fix.";
            }
#endif
            
            EditorUtility.DisplayDialog("uPiper Setup Status", message, "OK");
        }
        
        public static void RunInitialSetup()
        {
            try
            {
                var packagePath = GetPackagePath();
                
                // Check if this is a local installation
                if (packagePath == null)
                {
                    // Local installation - files should already be in place
                    var localPath = Path.Combine(Application.dataPath, "uPiper");
                    if (Directory.Exists(localPath))
                    {
                        Debug.Log("[uPiper Setup] Local installation detected. Files should already be in place.");
                        
                        // Check if files exist
                        var status = GetSetupStatus();
                        if (status.isComplete)
                        {
                            MarkSetupComplete();
                            EditorUtility.DisplayDialog("Setup Complete", 
                                "uPiper is already properly installed in your project.", 
                                "OK");
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("Setup Required", 
                                "Some files are missing. Please check:\n" +
                                "• Assets/uPiper/Plugins/\n" +
                                "• Assets/StreamingAssets/uPiper/\n\n" +
                                "For local installation, these files should be included in the project.",
                                "OK");
                        }
                        return;
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Error", 
                            "Could not find uPiper package. Please ensure it's properly installed.", 
                            "OK");
                        return;
                    }
                }
                
                Debug.Log($"[uPiper Setup] Starting setup from package: {packagePath}");
                
                // Debug: List package structure
                if (Directory.Exists(packagePath))
                {
                    Debug.Log($"[uPiper Setup] Package directory contents:");
                    var directories = Directory.GetDirectories(packagePath);
                    foreach (var dir in directories)
                    {
                        Debug.Log($"  - {Path.GetFileName(dir)}/");
                    }
                }
                
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
            // Try multiple possible paths for Plugins location
            var possiblePaths = new[]
            {
                Path.Combine(packagePath, "Assets", "uPiper", "Plugins"),
                Path.Combine(packagePath, "Runtime", "Plugins"),  
                Path.Combine(packagePath, "Plugins"),
            };
            
            string sourcePath = null;
            foreach (var path in possiblePaths)
            {
                if (Directory.Exists(path))
                {
                    sourcePath = path;
                    Debug.Log($"[uPiper Setup] Found Plugins at: {path}");
                    break;
                }
            }
            
            if (sourcePath == null)
            {
                Debug.LogError($"[uPiper Setup] Plugins source not found. Checked paths:");
                foreach (var path in possiblePaths)
                {
                    Debug.LogError($"  - {path}");
                }
                return (false, 0);
            }
            
            var targetPath = TARGET_PLUGINS_PATH;
            return CopyDirectory(sourcePath, targetPath, "Plugins");
        }
        
        private static (bool success, int fileCount) CopyOpenJTalkDictionary(string packagePath)
        {
            // Try multiple possible paths for StreamingAssets location
            var possiblePaths = new[]
            {
                Path.Combine(packagePath, "Assets", "StreamingAssets", "uPiper", "OpenJTalk"),
                Path.Combine(packagePath, "Runtime", "StreamingAssets", "uPiper", "OpenJTalk"),
                Path.Combine(packagePath, "StreamingAssets", "uPiper", "OpenJTalk"),
            };
            
            string sourcePath = null;
            foreach (var path in possiblePaths)
            {
                if (Directory.Exists(path))
                {
                    sourcePath = path;
                    Debug.Log($"[uPiper Setup] Found OpenJTalk dictionary at: {path}");
                    break;
                }
            }
            
            if (sourcePath == null)
            {
                // If not found in package, check if it's already in the project's StreamingAssets
                var projectStreamingAssetsPath = Path.Combine(Application.dataPath, "StreamingAssets", "uPiper", "OpenJTalk");
                if (Directory.Exists(projectStreamingAssetsPath))
                {
                    Debug.Log($"[uPiper Setup] OpenJTalk dictionary already exists in project at: {projectStreamingAssetsPath}");
                    // Count files for reporting
                    var fileCount = Directory.GetFiles(projectStreamingAssetsPath, "*", SearchOption.AllDirectories)
                        .Where(f => !f.EndsWith(".meta")).Count();
                    return (true, fileCount);
                }
                
                Debug.LogError($"[uPiper Setup] OpenJTalk dictionary source not found. Checked paths:");
                foreach (var path in possiblePaths)
                {
                    Debug.LogError($"  - {path}");
                }
                return (false, 0);
            }
            
            var targetPath = Path.Combine(TARGET_STREAMING_ASSETS_PATH, "OpenJTalk");
            return CopyDirectory(sourcePath, targetPath, "OpenJTalk Dictionary");
        }
        
        private static (bool success, int fileCount) CopyPhonemizerData(string packagePath)
        {
            // Try multiple possible paths for StreamingAssets location
            var possiblePaths = new[]
            {
                Path.Combine(packagePath, "Assets", "StreamingAssets", "uPiper", "Phonemizers"),
                Path.Combine(packagePath, "Runtime", "StreamingAssets", "uPiper", "Phonemizers"),
                Path.Combine(packagePath, "StreamingAssets", "uPiper", "Phonemizers"),
            };
            
            string sourcePath = null;
            foreach (var path in possiblePaths)
            {
                if (Directory.Exists(path))
                {
                    sourcePath = path;
                    Debug.Log($"[uPiper Setup] Found Phonemizer data at: {path}");
                    break;
                }
            }
            
            if (sourcePath == null)
            {
                // If not found in package, check if it's already in the project's StreamingAssets
                var projectStreamingAssetsPath = Path.Combine(Application.dataPath, "StreamingAssets", "uPiper", "Phonemizers");
                if (Directory.Exists(projectStreamingAssetsPath))
                {
                    Debug.Log($"[uPiper Setup] Phonemizer data already exists in project at: {projectStreamingAssetsPath}");
                    // Count files for reporting
                    var fileCount = Directory.GetFiles(projectStreamingAssetsPath, "*", SearchOption.AllDirectories)
                        .Where(f => !f.EndsWith(".meta")).Count();
                    return (true, fileCount);
                }
                
                Debug.LogError($"[uPiper Setup] Phonemizer data source not found. Checked paths:");
                foreach (var path in possiblePaths)
                {
                    Debug.LogError($"  - {path}");
                }
                return (false, 0);
            }
            
            var targetPath = Path.Combine(TARGET_STREAMING_ASSETS_PATH, "Phonemizers");
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
            // Always return true since we need to setup regardless of installation method
            return true;
        }
        
        private static string GetPackagePath()
        {
            // Check if uPiper is in Assets (local installation)
            var localPath = Path.Combine(Application.dataPath, "uPiper");
            if (Directory.Exists(localPath))
            {
                // Local installation - files are already in the project
                // Return null to indicate no package copy is needed
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
#if UPIPER_DEVELOPMENT
            // In development mode, setup is not required
            return true;
#else
            // Check if marked as complete
            if (!EditorPrefs.GetBool(SETUP_COMPLETE_KEY, false))
                return false;
            
            // Verify files actually exist
            var status = GetSetupStatus();
            return status.isComplete;
#endif
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