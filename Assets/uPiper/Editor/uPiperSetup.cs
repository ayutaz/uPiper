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

        // Target paths in Assets (made public for shared use)
        public const string TARGET_STREAMING_ASSETS_PATH = "Assets/StreamingAssets/uPiper";

        [InitializeOnLoadMethod]
        private static void CheckFirstTimeSetup()
        {
#if !UPIPER_DEVELOPMENT
            EditorApplication.delayCall += () =>
            {
                if (!IsSetupComplete() && IsPackageInstalled())
                {
                    var samplesStatus = CheckImportedSamples();

                    if (samplesStatus.hasAnyImportedSamples)
                    {
                        // Samples are imported, offer to install them
                        if (EditorUtility.DisplayDialog(
                            "uPiper Setup - Samples Detected",
                            "uPiper samples have been imported but not installed.\n\n" +
                            "Detected samples:\n" +
                            (samplesStatus.hasOpenJTalkSample ? "• OpenJTalk Dictionary\n" : "") +
                            (samplesStatus.hasCMUSample ? "• CMU Dictionary\n" : "") +
                            (samplesStatus.hasModelsSample ? "• Voice Models\n" : "") +
                            "\nWould you like to install them now?",
                            "Install Now", "Later"))
                        {
                            InstallSamplesData();
                        }
                    }
                    else
                    {
                        // No samples imported, show instructions
                        if (EditorUtility.DisplayDialog(
                            "uPiper Initial Setup Required",
                            "Welcome to uPiper!\n\n" +
                            "To use uPiper, you need to import the required data:\n\n" +
                            "1. Open Window > Package Manager\n" +
                            "2. Select 'In Project' from the dropdown\n" +
                            "3. Find and select 'uPiper'\n" +
                            "4. In the package details, find 'Samples'\n" +
                            "5. Import the following:\n" +
                            "   • OpenJTalk Dictionary Data (Required for Japanese)\n" +
                            "   • CMU Pronouncing Dictionary (Required for English)\n" +
                            "   • Voice Models (Optional - for high quality voices)\n" +
                            "6. After importing, run 'uPiper/Setup/Install from Samples'",
                            "Open Package Manager", "Later"))
                        {
                            UnityEditor.PackageManager.UI.Window.Open("com.ayutaz.upiper");
                        }
                    }
                }
            };
#endif
        }

        [MenuItem("uPiper/Setup/Check Setup Status", false, 2)]
        public static void CheckSetupStatus()
        {
            var status = GetSetupStatus();

            var message = "uPiper Setup Status:\n\n";
            message += $"• Native Plugins: ✓ Available (from package)\n";
            message += $"• OpenJTalk Dictionary: {(status.dictionaryExists ? "✓ Installed" : "✗ Not found")}\n";
            message += $"• CMU Dictionary: {(status.cmuDictExists ? "✓ Installed" : "✗ Not found")}\n";
            message += $"• Voice Models: {(status.modelsExist ? "✓ Installed" : "✗ Not found")}\n";
            message += $"• Setup Complete: {(status.isComplete ? "✓ Yes" : "✗ No")}\n";

            // Check for imported samples
            var samplesStatus = CheckImportedSamples();
            if (samplesStatus.hasAnyImportedSamples)
            {
                message += "\n\nImported Samples detected:\n";
                if (samplesStatus.hasOpenJTalkSample)
                    message += "• OpenJTalk Dictionary (from Samples)\n";
                if (samplesStatus.hasCMUSample)
                    message += "• CMU Dictionary (from Samples)\n";
                if (samplesStatus.hasModelsSample)
                    message += "• Voice Models (from Samples)\n";

                if (!status.isComplete)
                {
                    message += "\n⚠️ Samples imported but not installed to project.\n";
                    message += "Run 'uPiper/Setup/Install from Samples' to complete setup.";
                }
            }

#if UPIPER_DEVELOPMENT
            message += "\n[Development Mode - Setup not required]";
#else
            if (!status.isComplete && !samplesStatus.hasAnyImportedSamples)
            {
                message += "\n⚠️ Some files are missing. Import samples from Package Manager:\n";
                message += "1. Open Package Manager\n";
                message += "2. Select uPiper package\n";
                message += "3. Import required samples (Dictionary Data, Voice Models)\n";
                message += "4. Run 'uPiper/Setup/Install from Samples'";
            }

#endif

            EditorUtility.DisplayDialog("uPiper Setup Status", message, "OK");
        }

        [MenuItem("uPiper/Setup/Install from Samples", false, 1)]
        public static void InstallFromSamples()
        {
#if UPIPER_DEVELOPMENT
            EditorUtility.DisplayDialog(
                "Development Mode",
                "Setup is not required in development mode.",
                "OK");
            return;
#else
            var samplesStatus = CheckImportedSamples();

            if (!samplesStatus.hasAnyImportedSamples)
            {
                EditorUtility.DisplayDialog(
                    "No Samples Found",
                    "No imported samples detected.\n\n" +
                    "Please import samples from Package Manager first:\n" +
                    "1. Open Package Manager\n" +
                    "2. Select uPiper package\n" +
                    "3. Import required samples\n" +
                    "4. Run this command again",
                    "OK");
                return;
            }

            if (EditorUtility.DisplayDialog(
                "Install from Samples",
                "This will copy data from imported samples to your project:\n\n" +
                (samplesStatus.hasOpenJTalkSample ? "• OpenJTalk Dictionary\n" : "") +
                (samplesStatus.hasCMUSample ? "• CMU Dictionary\n" : "") +
                (samplesStatus.hasModelsSample ? "• Voice Models\n" : "") +
                "\nFiles will be copied to:\n" +
                "• Assets/StreamingAssets/uPiper/\n" +
                "• Assets/uPiper/Resources/Models/\n\n" +
                "Continue?",
                "Install", "Cancel"))
            {
                InstallSamplesData();
            }
#endif
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
            // First, check if this is a true local installation
            // For local installation, both Plugins and StreamingAssets should exist
            var localPluginsPath = Path.Combine(Application.dataPath, "uPiper", "Plugins");
            var localStreamingAssetsPath = Path.Combine(Application.dataPath, "StreamingAssets", "uPiper");

            // Check if we have actual plugin files (not just empty folders)
            bool hasPluginFiles = false;
            if (Directory.Exists(localPluginsPath))
            {
                // Check for actual DLL/SO files
                var pluginFiles = Directory.GetFiles(localPluginsPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(".dll") || f.EndsWith(".so") || f.EndsWith(".bundle") || f.EndsWith(".dylib"))
                    .ToArray();
                hasPluginFiles = pluginFiles.Length > 0;
            }

            bool hasStreamingAssets = false;
            if (Directory.Exists(localStreamingAssetsPath))
            {
                // Check for dictionary or phonemizer data
                var dataFiles = Directory.GetFiles(localStreamingAssetsPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => !f.EndsWith(".meta"))
                    .ToArray();
                hasStreamingAssets = dataFiles.Length > 0;
            }

            // If both exist with actual files, this is a local installation
            if (hasPluginFiles && hasStreamingAssets)
            {
                Debug.Log("[uPiper Setup] Local installation detected with existing files.");
                return null; // Local installation, no package copy needed
            }

            // Not a complete local installation, check for Package Manager installation
            // Search in PackageCache
            var packageCachePath = Path.Combine(Application.dataPath, "..", "Library", "PackageCache");
            if (Directory.Exists(packageCachePath))
            {
                var packageDirs = Directory.GetDirectories(packageCachePath, $"{PACKAGE_NAME}@*");
                if (packageDirs.Length > 0)
                {
                    Debug.Log($"[uPiper Setup] Package Manager installation found: {packageDirs[0]}");
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
                            Debug.Log($"[uPiper Setup] Package Manager installation found via API: {package.resolvedPath}");
                            return package.resolvedPath;
                        }
                    }
                }
            }
            catch { }

            // No package found
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

            // Check OpenJTalk dictionary
            var dictPath = Path.Combine(TARGET_STREAMING_ASSETS_PATH, "OpenJTalk", "naist_jdic", "open_jtalk_dic_utf_8-1.11");
            status.dictionaryExists = Directory.Exists(dictPath);

            // Check CMU dictionary
            var cmuPath = Path.Combine(TARGET_STREAMING_ASSETS_PATH, "Phonemizers", "cmudict-0.7b.txt");
            status.cmuDictExists = File.Exists(cmuPath);

            // Check voice models (check both old and new locations)
            var modelPath1 = Path.Combine(Application.dataPath, "Resources", "uPiper", "Models", "ja_JP-test-medium.onnx");
            var modelPath2 = Path.Combine(Application.dataPath, "Resources", "uPiper", "Models", "en_US-ljspeech-medium.onnx");
            var oldModelPath1 = Path.Combine(Application.dataPath, "uPiper", "Resources", "Models", "ja_JP-test-medium.onnx");
            var oldModelPath2 = Path.Combine(Application.dataPath, "uPiper", "Resources", "Models", "en_US-ljspeech-medium.onnx");
            status.modelsExist = File.Exists(modelPath1) || File.Exists(modelPath2) ||
                                 File.Exists(oldModelPath1) || File.Exists(oldModelPath2);

            status.isComplete = (status.dictionaryExists || status.cmuDictExists) && status.modelsExist;

            return status;
        }

        private struct SetupStatus
        {
            public bool dictionaryExists;
            public bool cmuDictExists;
            public bool modelsExist;
            public bool isComplete;
        }

        private struct SamplesStatus
        {
            public bool hasOpenJTalkSample;
            public bool hasCMUSample;
            public bool hasModelsSample;
            public bool hasAnyImportedSamples => hasOpenJTalkSample || hasCMUSample || hasModelsSample;
        }

        private static SamplesStatus CheckImportedSamples()
        {
            var status = new SamplesStatus();

            // Check for imported OpenJTalk sample
            var openJTalkSamplePath = Path.Combine(Application.dataPath, "Samples", "uPiper", "0.1.0", "OpenJTalk Dictionary Data");
            if (!Directory.Exists(openJTalkSamplePath))
            {
                // Try without version number
                openJTalkSamplePath = Path.Combine(Application.dataPath, "Samples", "uPiper", "OpenJTalk Dictionary Data");
            }
            status.hasOpenJTalkSample = Directory.Exists(openJTalkSamplePath) &&
                Directory.Exists(Path.Combine(openJTalkSamplePath, "naist_jdic"));

            // Check for imported CMU sample
            var cmuSamplePath = Path.Combine(Application.dataPath, "Samples", "uPiper", "0.1.0", "CMU Pronouncing Dictionary");
            if (!Directory.Exists(cmuSamplePath))
            {
                // Try without version number
                cmuSamplePath = Path.Combine(Application.dataPath, "Samples", "uPiper", "CMU Pronouncing Dictionary");
            }
            status.hasCMUSample = Directory.Exists(cmuSamplePath) &&
                File.Exists(Path.Combine(cmuSamplePath, "cmudict-0.7b.txt"));

            // Check for imported voice models sample
            var modelsSamplePath = Path.Combine(Application.dataPath, "Samples", "uPiper", "0.1.0", "Voice Models");
            if (!Directory.Exists(modelsSamplePath))
            {
                // Try without version number
                modelsSamplePath = Path.Combine(Application.dataPath, "Samples", "uPiper", "Voice Models");
            }
            status.hasModelsSample = Directory.Exists(modelsSamplePath) &&
                (File.Exists(Path.Combine(modelsSamplePath, "ja_JP-test-medium.onnx")) ||
                 File.Exists(Path.Combine(modelsSamplePath, "en_US-ljspeech-medium.onnx")));

            return status;
        }

        private static void InstallSamplesData()
        {
            try
            {
                var samplesStatus = CheckImportedSamples();
                var installedCount = 0;

                // Install OpenJTalk dictionary
                if (samplesStatus.hasOpenJTalkSample)
                {
                    var sourcePaths = new[]
                    {
                        Path.Combine(Application.dataPath, "Samples", "uPiper", "0.1.0", "OpenJTalk Dictionary Data"),
                        Path.Combine(Application.dataPath, "Samples", "uPiper", "OpenJTalk Dictionary Data")
                    };

                    string sourcePath = null;
                    foreach (var path in sourcePaths)
                    {
                        if (Directory.Exists(path))
                        {
                            sourcePath = path;
                            break;
                        }
                    }

                    if (sourcePath != null)
                    {
                        var targetPath = Path.Combine(TARGET_STREAMING_ASSETS_PATH, "OpenJTalk");
                        var result = CopyDirectory(sourcePath, targetPath, "OpenJTalk Dictionary");
                        if (result.success)
                        {
                            installedCount += result.fileCount;
                            Debug.Log($"[uPiper Setup] Installed OpenJTalk dictionary ({result.fileCount} files)");
                        }
                    }
                }

                // Install CMU dictionary
                if (samplesStatus.hasCMUSample)
                {
                    var sourcePaths = new[]
                    {
                        Path.Combine(Application.dataPath, "Samples", "uPiper", "0.1.0", "CMU Pronouncing Dictionary"),
                        Path.Combine(Application.dataPath, "Samples", "uPiper", "CMU Pronouncing Dictionary")
                    };

                    string sourcePath = null;
                    foreach (var path in sourcePaths)
                    {
                        if (Directory.Exists(path))
                        {
                            sourcePath = path;
                            break;
                        }
                    }

                    if (sourcePath != null)
                    {
                        var targetPath = Path.Combine(TARGET_STREAMING_ASSETS_PATH, "Phonemizers");
                        var result = CopyDirectory(sourcePath, targetPath, "CMU Dictionary");
                        if (result.success)
                        {
                            installedCount += result.fileCount;
                            Debug.Log($"[uPiper Setup] Installed CMU dictionary ({result.fileCount} files)");
                        }
                    }
                }

                // Install voice models
                if (samplesStatus.hasModelsSample)
                {
                    var sourcePaths = new[]
                    {
                        Path.Combine(Application.dataPath, "Samples", "uPiper", "0.1.0", "Voice Models"),
                        Path.Combine(Application.dataPath, "Samples", "uPiper", "Voice Models")
                    };

                    string sourcePath = null;
                    foreach (var path in sourcePaths)
                    {
                        if (Directory.Exists(path))
                        {
                            sourcePath = path;
                            break;
                        }
                    }

                    if (sourcePath != null)
                    {
                        // Copy to project Assets instead of package folder to avoid immutable package warnings
                        var targetPath = Path.Combine(Application.dataPath, "Resources", "uPiper", "Models");
                        var result = CopyDirectory(sourcePath, targetPath, "Voice Models");
                        if (result.success)
                        {
                            installedCount += result.fileCount;
                            Debug.Log($"[uPiper Setup] Installed voice models ({result.fileCount} files) to Assets/Resources/uPiper/Models/");
                        }
                    }
                }

                if (installedCount > 0)
                {
                    MarkSetupComplete();
                    AssetDatabase.Refresh();

                    EditorUtility.DisplayDialog(
                        "Installation Complete",
                        $"Successfully installed {installedCount} files from samples.\n\n" +
                        "uPiper is now ready to use!",
                        "OK");

                    Debug.Log($"[uPiper Setup] Installation from samples completed. Total files: {installedCount}");
                }
                else
                {
                    EditorUtility.DisplayDialog(
                        "No Files Installed",
                        "No files were installed. Please check that samples are properly imported.",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[uPiper Setup] Failed to install from samples: {ex.Message}\n{ex.StackTrace}");
                EditorUtility.DisplayDialog(
                    "Installation Error",
                    $"An error occurred during installation:\n{ex.Message}",
                    "OK");
            }
        }

    }
}