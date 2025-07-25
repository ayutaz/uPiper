using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace uPiper.Editor.Build
{
    /// <summary>
    /// Post-build processor for Android platform
    /// </summary>
    public class AndroidPostBuildProcessor : IPostprocessBuildWithReport
    {
        public int callbackOrder { get { return 100; } }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android)
                return;

            Debug.Log("[uPiper] Android post-build processing...");

            // Verify native libraries are included
            VerifyNativeLibraries(report);

            // Log APK size information
            LogAPKSize(report);

            Debug.Log("[uPiper] Android post-build processing completed.");
        }

        private void VerifyNativeLibraries(BuildReport report)
        {
            bool hasIssues = false;
            string[] requiredLibraries = { "libopenjtalk_wrapper.so" };
            string[] supportedAbis = { "arm64-v8a", "armeabi-v7a", "x86", "x86_64" };

            // Check build report files if available
            if (report.GetFiles() != null)
            {
                foreach (var file in report.GetFiles())
                {
                    if (file.path.Contains("libopenjtalk_wrapper.so"))
                    {
                        Debug.Log($"[uPiper] Found native library: {file.path} (size: {file.size / 1024}KB)");
                    }
                }
            }

            // Check if libraries exist in the project
            string androidPluginsPath = Path.Combine(Application.dataPath, "uPiper/Plugins/Android/libs");
            if (Directory.Exists(androidPluginsPath))
            {
                foreach (var abi in supportedAbis)
                {
                    string abiPath = Path.Combine(androidPluginsPath, abi);
                    if (Directory.Exists(abiPath))
                    {
                        foreach (var lib in requiredLibraries)
                        {
                            string libPath = Path.Combine(abiPath, lib);
                            if (File.Exists(libPath))
                            {
                                var fileInfo = new FileInfo(libPath);
                                Debug.Log($"[uPiper] Native library verified: {abi}/{lib} (size: {fileInfo.Length / 1024}KB)");
                            }
                            else
                            {
                                Debug.LogWarning($"[uPiper] Missing native library: {abi}/{lib}");
                                hasIssues = true;
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[uPiper] Missing ABI directory: {abi}");
                        hasIssues = true;
                    }
                }
            }
            else
            {
                Debug.LogError($"[uPiper] Android plugins directory not found: {androidPluginsPath}");
                hasIssues = true;
            }

            if (hasIssues)
            {
                Debug.LogError("[uPiper] Some native libraries are missing. Please run the Android build scripts in NativePlugins/OpenJTalk/");
            }
        }

        private void LogAPKSize(BuildReport report)
        {
            long totalSize = 0;
            long nativeLibSize = 0;
            long dictionarySize = 0;

            // Check if files property is available
            if (report.GetFiles() != null)
            {
                foreach (var file in report.GetFiles())
                {
                    totalSize += (long)file.size;

                    if (file.path.Contains("libopenjtalk_wrapper.so"))
                    {
                        nativeLibSize += (long)file.size;
                    }
                    else if (file.path.Contains("OpenJTalk") && file.path.Contains("dic"))
                    {
                        dictionarySize += (long)file.size;
                    }
                }
            }
            else
            {
                // Fallback: use summary information
                totalSize = (long)report.summary.totalSize;
                Debug.Log("[uPiper] Detailed file information not available in build report.");
            }

            Debug.Log($"[uPiper] APK Total Size: {totalSize / 1024 / 1024}MB");
            Debug.Log($"[uPiper] Native Libraries Size: {nativeLibSize / 1024}KB");
            Debug.Log($"[uPiper] Dictionary Size: {dictionarySize / 1024}KB");

            if (totalSize > 100 * 1024 * 1024) // 100MB warning
            {
                Debug.LogWarning("[uPiper] APK size exceeds 100MB. Consider using APK expansion files or App Bundle for distribution.");
            }
        }
    }
}