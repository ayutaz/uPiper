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
        private const string ValidationKey = "uPiper.AndroidLibraryValidator.LastValidation";
        private const float ValidationIntervalDays = 7.0f; // 週に1回のみ自動検証

        static AndroidLibraryValidator()
        {
            // 最後の検証から一定期間経過している場合のみ実行
            var lastValidation = EditorPrefs.GetString(ValidationKey, string.Empty);
            if (string.IsNullOrEmpty(lastValidation) || ShouldValidate(lastValidation))
            {
                EditorApplication.delayCall += RunValidationOnce;
            }
        }

        private static bool ShouldValidate(string lastValidationDate)
        {
            if (System.DateTime.TryParse(lastValidationDate, out var lastDate))
            {
                var daysSinceLastValidation = (System.DateTime.Now - lastDate).TotalDays;
                return daysSinceLastValidation >= ValidationIntervalDays;
            }
            return true;
        }

        private static void RunValidationOnce()
        {
            ValidateAndroidLibrariesInternal(false); // verbose=falseで静かに実行
            EditorPrefs.SetString(ValidationKey, System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        [MenuItem("uPiper/Android/Validate Native Libraries")]
        public static void ValidateAndroidLibraries()
        {
            ValidateAndroidLibrariesInternal(true);
        }

        private static void ValidateAndroidLibrariesInternal(bool verbose)
        {
            if (verbose)
            {
                Debug.Log("[uPiper] Validating Android native libraries...");
            }

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
                        else if (verbose)
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
                if (verbose)
                {
                    Debug.Log("[uPiper] All Android native libraries validated successfully!");
                }
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

        [MenuItem("uPiper/Android/Reset Validation Timer")]
        public static void ResetValidationTimer()
        {
            EditorPrefs.DeleteKey(ValidationKey);
            Debug.Log("[uPiper] Android library validation timer reset. Validation will run on next editor load.");
        }
    }
}