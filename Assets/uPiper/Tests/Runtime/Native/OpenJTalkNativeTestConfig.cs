using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace uPiper.Tests.Runtime.Native
{
    /// <summary>
    /// Configuration helper for OpenJTalk native tests
    /// Provides platform-specific library loading support
    /// </summary>
    public static class OpenJTalkNativeTestConfig
    {
        // Try to preload the library with different names
        static OpenJTalkNativeTestConfig()
        {
            try
            {
                // Try to load library explicitly for Linux environments
                if (Application.platform == RuntimePlatform.LinuxPlayer || 
                    Application.platform == RuntimePlatform.LinuxEditor ||
                    (Application.platform == RuntimePlatform.WindowsEditor && IsRunningInDocker()))
                {
                    PreloadLibrary();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[OpenJTalkNativeTestConfig] Failed to preload library: {e.Message}");
            }
        }

        private static bool IsRunningInDocker()
        {
            // Check if running in Docker by looking for .dockerenv file
            return System.IO.File.Exists("/.dockerenv") || 
                   !string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));
        }

        private static void PreloadLibrary()
        {
            string[] possiblePaths = {
                // Standard Unity plugin paths
                "Assets/uPiper/Plugins/Linux/x86_64/libopenjtalk_wrapper.so",
                "Plugins/Linux/x86_64/libopenjtalk_wrapper.so",
                
                // Docker volume paths
                "/github/workspace/Assets/uPiper/Plugins/Linux/x86_64/libopenjtalk_wrapper.so",
                "/github/workspace/libopenjtalk_wrapper.so",
                
                // System paths
                "/usr/local/lib/libopenjtalk_wrapper.so",
                "/usr/lib/libopenjtalk_wrapper.so",
                
                // Current directory
                "./libopenjtalk_wrapper.so",
                "libopenjtalk_wrapper.so"
            };

            foreach (string path in possiblePaths)
            {
                try
                {
                    if (System.IO.File.Exists(path))
                    {
                        Debug.Log($"[OpenJTalkNativeTestConfig] Found library at: {path}");
                        var handle = dlopen(path, RTLD_NOW | RTLD_GLOBAL);
                        if (handle != IntPtr.Zero)
                        {
                            Debug.Log($"[OpenJTalkNativeTestConfig] Successfully loaded library from: {path}");
                            return;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[OpenJTalkNativeTestConfig] Failed to load from {path}: {e.Message}");
                }
            }

            // If no explicit loading worked, rely on standard DllImport mechanism
            Debug.Log("[OpenJTalkNativeTestConfig] Will rely on standard DllImport loading");
        }

        // P/Invoke for dlopen on Linux
        private const int RTLD_NOW = 2;
        private const int RTLD_GLOBAL = 256;

        [DllImport("libdl.so.2", EntryPoint = "dlopen")]
        private static extern IntPtr dlopen(string filename, int flags);

        [DllImport("libdl.so.2", EntryPoint = "dlerror")]
        private static extern IntPtr dlerror();

        public static string GetDlError()
        {
            IntPtr errorPtr = dlerror();
            return errorPtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(errorPtr) : null;
        }
    }
}