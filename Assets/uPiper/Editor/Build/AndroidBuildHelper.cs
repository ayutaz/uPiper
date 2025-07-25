using System.IO;
using UnityEditor;
using UnityEngine;

namespace uPiper.Editor.Build
{
    /// <summary>
    /// Helper class for Android build configuration
    /// </summary>
    public static class AndroidBuildHelper
    {
        [MenuItem("uPiper/Android/Setup Android Libraries")]
        public static void SetupAndroidLibraries()
        {
            Debug.Log("[uPiper] Setting up Android native libraries...");

            string sourceDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../NativePlugins/OpenJTalk/output/android"));
            string targetDir = Path.Combine(Application.dataPath, "uPiper/Plugins/Android/libs");

            if (!Directory.Exists(sourceDir))
            {
                Debug.LogError($"[uPiper] Native library output directory not found: {sourceDir}");
                Debug.LogError("[uPiper] Please build the Android native libraries first using NativePlugins/OpenJTalk/build_all_android_abis.sh");
                return;
            }

            // Create target directory
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            // Copy libraries for each ABI
            string[] abis = { "arm64-v8a", "armeabi-v7a", "x86", "x86_64" };
            int copiedCount = 0;

            foreach (var abi in abis)
            {
                string sourceAbiDir = Path.Combine(sourceDir, abi);
                string targetAbiDir = Path.Combine(targetDir, abi);

                if (Directory.Exists(sourceAbiDir))
                {
                    if (!Directory.Exists(targetAbiDir))
                    {
                        Directory.CreateDirectory(targetAbiDir);
                    }

                    string sourceLib = Path.Combine(sourceAbiDir, "libopenjtalk_wrapper.so");
                    string targetLib = Path.Combine(targetAbiDir, "libopenjtalk_wrapper.so");

                    if (File.Exists(sourceLib))
                    {
                        File.Copy(sourceLib, targetLib, true);
                        Debug.Log($"[uPiper] Copied {abi}/libopenjtalk_wrapper.so");
                        copiedCount++;

                        // Set plugin settings
                        SetAndroidPluginSettings(targetLib, abi);
                    }
                    else
                    {
                        Debug.LogWarning($"[uPiper] Library not found: {sourceLib}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[uPiper] ABI directory not found: {sourceAbiDir}");
                }
            }

            if (copiedCount > 0)
            {
                AssetDatabase.Refresh();
                Debug.Log($"[uPiper] Successfully copied {copiedCount} native libraries.");
                Debug.Log("[uPiper] Android setup completed. Libraries are ready for build.");
            }
            else
            {
                Debug.LogError("[uPiper] No libraries were copied. Please check the build output.");
            }
        }

        private static void SetAndroidPluginSettings(string libraryPath, string abi)
        {
            string assetPath = "Assets" + libraryPath.Substring(Application.dataPath.Length).Replace('\\', '/');

            AssetDatabase.ImportAsset(assetPath);
            PluginImporter importer = AssetImporter.GetAtPath(assetPath) as PluginImporter;

            if (importer != null)
            {
                importer.SetCompatibleWithAnyPlatform(false);
                importer.SetCompatibleWithPlatform(BuildTarget.Android, true);

                // Set CPU architecture
                importer.SetPlatformData(BuildTarget.Android, "CPU", GetCPUFromABI(abi));

                importer.SaveAndReimport();
            }
        }

        private static string GetCPUFromABI(string abi)
        {
            switch (abi)
            {
                case "arm64-v8a":
                    return "ARM64";
                case "armeabi-v7a":
                    return "ARMv7";
                case "x86":
                    return "X86";
                case "x86_64":
                    return "X86_64";
                default:
                    return "AnyCPU";
            }
        }

        [MenuItem("uPiper/Android/Verify Android Setup")]
        public static void VerifyAndroidSetup()
        {
            Debug.Log("[uPiper] Verifying Android setup...");

            bool hasIssues = false;

            // Check native libraries
            string pluginsPath = Path.Combine(Application.dataPath, "uPiper/Plugins/Android/libs");
            if (!Directory.Exists(pluginsPath))
            {
                Debug.LogError("[uPiper] Android plugins directory not found.");
                hasIssues = true;
            }
            else
            {
                string[] abis = { "arm64-v8a", "armeabi-v7a", "x86", "x86_64" };
                foreach (var abi in abis)
                {
                    string libPath = Path.Combine(pluginsPath, abi, "libopenjtalk_wrapper.so");
                    if (File.Exists(libPath))
                    {
                        var fileInfo = new FileInfo(libPath);
                        Debug.Log($"[uPiper] ✓ {abi}: {fileInfo.Length / 1024}KB");
                    }
                    else
                    {
                        Debug.LogWarning($"[uPiper] ✗ {abi}: Missing library");
                        hasIssues = true;
                    }
                }
            }

            // Check AndroidManifest.xml
            string manifestPath = Path.Combine(Application.dataPath, "uPiper/Plugins/Android/AndroidManifest.xml");
            if (File.Exists(manifestPath))
            {
                Debug.Log("[uPiper] ✓ AndroidManifest.xml found");
            }
            else
            {
                Debug.LogWarning("[uPiper] ✗ AndroidManifest.xml not found");
                hasIssues = true;
            }

            // Check dictionary in StreamingAssets
            string dictPath = Path.Combine(Application.streamingAssetsPath, "uPiper/OpenJTalk/open_jtalk_dic_utf_8-1.11");
            if (Directory.Exists(dictPath))
            {
                Debug.Log("[uPiper] ✓ OpenJTalk dictionary found in StreamingAssets");
            }
            else
            {
                Debug.LogWarning("[uPiper] ✗ OpenJTalk dictionary not found in StreamingAssets");
                hasIssues = true;
            }

            if (!hasIssues)
            {
                Debug.Log("[uPiper] Android setup verification completed successfully!");
            }
            else
            {
                Debug.LogError("[uPiper] Android setup has issues. Please fix them before building.");
            }
        }
    }
}