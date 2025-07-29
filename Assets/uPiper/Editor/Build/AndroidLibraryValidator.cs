using System.IO;
using UnityEditor;
using UnityEngine;

namespace uPiper.Editor.Build
{
    /// <summary>
    /// Validates Android native libraries and their import settings
    /// </summary>
    public static class AndroidLibraryValidator
    {
        // 自動実行を完全に削除 - 必要な時に手動で実行

        [MenuItem("uPiper/Android/Validate Native Libraries")]
        public static void ValidateAndroidLibraries()
        {
            Debug.Log("[uPiper] Validating Android native libraries...");

            string[] abis = { "arm64-v8a", "armeabi-v7a", "x86", "x86_64" };
            var baseLibPath = "Assets/uPiper/Plugins/Android/libs";
            var hasIssues = false;

            foreach (var abi in abis)
            {
                var libPath = Path.Combine(baseLibPath, abi, "libopenjtalk_wrapper.so");
                var fullPath = Path.GetFullPath(libPath);

                if (File.Exists(fullPath))
                {
                    // Check if asset exists in Unity's database
                    var importer = AssetImporter.GetAtPath(libPath) as PluginImporter;

                    if (importer == null)
                    {
                        Debug.LogWarning($"[uPiper] Library not imported: {libPath}");
                        AssetDatabase.ImportAsset(libPath);
                        importer = AssetImporter.GetAtPath(libPath) as PluginImporter;
                    }

                    if (importer != null)
                    {
                        // Ensure correct settings
                        var needsReimport = false;

                        if (!importer.GetCompatibleWithPlatform(BuildTarget.Android))
                        {
                            importer.SetCompatibleWithAnyPlatform(false);
                            importer.SetCompatibleWithPlatform(BuildTarget.Android, true);
                            needsReimport = true;
                        }

                        // Set CPU architecture
                        var expectedCPU = GetExpectedCPU(abi);
                        var currentCPU = importer.GetPlatformData(BuildTarget.Android, "CPU");

                        if (currentCPU != expectedCPU)
                        {
                            importer.SetPlatformData(BuildTarget.Android, "CPU", expectedCPU);
                            needsReimport = true;
                        }

                        if (needsReimport)
                        {
                            importer.SaveAndReimport();
                            Debug.Log($"[uPiper] Fixed settings for: {abi}");
                        }
                        else
                        {
                            var fileInfo = new FileInfo(fullPath);
                            Debug.Log($"[uPiper] ✓ {abi}: {fileInfo.Length / 1024}KB - Settings OK");
                        }
                    }
                }
                else
                {
                    Debug.LogError($"[uPiper] ✗ Missing library: {libPath}");
                    hasIssues = true;
                }
            }

            if (!hasIssues)
            {
                Debug.Log("[uPiper] All Android native libraries validated successfully!");
            }
            else
            {
                Debug.LogError("[uPiper] Some Android libraries are missing. Run NativePlugins/OpenJTalk/build_all_android_abis.sh");
            }
        }

        private static string GetExpectedCPU(string abi)
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

        [MenuItem("uPiper/Android/Fix Library Import Settings")]
        public static void FixLibraryImportSettings()
        {
            AssetDatabase.Refresh();
            ValidateAndroidLibraries();
        }
    }
}