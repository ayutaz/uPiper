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

            var sourceDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../NativePlugins/OpenJTalk/output/android"));
            var targetDir = Path.Combine(Application.dataPath, "uPiper/Plugins/Android/libs");

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
            var copiedCount = 0;

            foreach (var abi in abis)
            {
                var sourceAbiDir = Path.Combine(sourceDir, abi);
                var targetAbiDir = Path.Combine(targetDir, abi);

                if (Directory.Exists(sourceAbiDir))
                {
                    if (!Directory.Exists(targetAbiDir))
                    {
                        Directory.CreateDirectory(targetAbiDir);
                    }

                    var sourceLib = Path.Combine(sourceAbiDir, "libopenjtalk_wrapper.so");
                    var targetLib = Path.Combine(targetAbiDir, "libopenjtalk_wrapper.so");

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
            var assetPath = "Assets" + libraryPath[Application.dataPath.Length..].Replace('\\', '/');

            AssetDatabase.ImportAsset(assetPath);
            var importer = AssetImporter.GetAtPath(assetPath) as PluginImporter;

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
            return abi switch
            {
                "arm64-v8a" => "ARM64",
                "armeabi-v7a" => "ARMv7",
                "x86" => "X86",
                "x86_64" => "X86_64",
                _ => "AnyCPU",
            };
        }

        [MenuItem("uPiper/Android/Verify Android Setup")]
        public static void VerifyAndroidSetup()
        {
            Debug.Log("[uPiper] Verifying Android setup...");

            var hasIssues = false;

            // Check native libraries
            var pluginsPath = Path.Combine(Application.dataPath, "uPiper/Plugins/Android/libs");
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
                    var libPath = Path.Combine(pluginsPath, abi, "libopenjtalk_wrapper.so");
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
            var manifestPath = Path.Combine(Application.dataPath, "uPiper/Plugins/Android/AndroidManifest.xml");
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
            var dictPath = Path.Combine(Application.streamingAssetsPath, "uPiper/OpenJTalk/open_jtalk_dic_utf_8-1.11");
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