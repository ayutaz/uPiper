using System.IO;
using UnityEditor;
using UnityEngine;

namespace uPiper.Editor.Build
{
    /// <summary>
    /// Validates Android native libraries and their import settings
    /// </summary>
    [InitializeOnLoad]
    public static class AndroidLibraryValidator
    {
        static AndroidLibraryValidator()
        {
            // Run validation on editor load
            EditorApplication.delayCall += ValidateAndroidLibraries;
        }

        [MenuItem("uPiper/Android/Validate Native Libraries")]
        public static void ValidateAndroidLibraries()
        {
            Debug.Log("[uPiper] Validating Android native libraries...");

            string[] abis = { "arm64-v8a", "armeabi-v7a", "x86", "x86_64" };
            string baseLibPath = "Assets/uPiper/Plugins/Android/libs";
            bool hasIssues = false;

            foreach (var abi in abis)
            {
                string libPath = Path.Combine(baseLibPath, abi, "libopenjtalk_wrapper.so");
                string fullPath = Path.GetFullPath(libPath);

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
                        bool needsReimport = false;

                        if (!importer.GetCompatibleWithPlatform(BuildTarget.Android))
                        {
                            importer.SetCompatibleWithAnyPlatform(false);
                            importer.SetCompatibleWithPlatform(BuildTarget.Android, true);
                            needsReimport = true;
                        }

                        // Set CPU architecture
                        string expectedCPU = GetExpectedCPU(abi);
                        string currentCPU = importer.GetPlatformData(BuildTarget.Android, "CPU");

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

        [MenuItem("uPiper/Android/Fix Library Import Settings")]
        public static void FixLibraryImportSettings()
        {
            AssetDatabase.Refresh();
            ValidateAndroidLibraries();
        }
    }
}