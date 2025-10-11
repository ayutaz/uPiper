using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace uPiper.Core.Phonemizers.Implementations
{
    /// <summary>
    /// Debug helper for OpenJTalk native library loading issues
    /// </summary>
    public static class OpenJTalkDebugHelper
    {
        [DllImport("openjtalk_wrapper")]
        private static extern IntPtr openjtalk_get_version();

        public static void DebugLibraryLoading()
        {
            Debug.Log("[OpenJTalkDebug] Starting library loading debug...");

            // Log environment info
            Debug.Log($"[OpenJTalkDebug] Platform: {Application.platform}");
            Debug.Log($"[OpenJTalkDebug] Unity version: {Application.unityVersion}");
            Debug.Log($"[OpenJTalkDebug] Is Editor: {Application.isEditor}");
            Debug.Log($"[OpenJTalkDebug] Data path: {Application.dataPath}");
            Debug.Log($"[OpenJTalkDebug] Persistent data path: {Application.persistentDataPath}");

#if UNITY_IOS && !UNITY_EDITOR
            // iOSでは静的ライブラリなので、動的ライブラリのロードチェックはスキップ
            Debug.Log("[OpenJTalkDebug] iOS platform - using static library (__Internal)");
            Debug.Log("[OpenJTalkDebug] Native library is statically linked into the app binary");
            return;
#endif

            // Check Plugins directory (not applicable for Android)
#if !UNITY_EDITOR && !UNITY_ANDROID
            string pluginsPath;
            if (Application.platform == RuntimePlatform.WindowsPlayer)
            {
                // Windows: DLLs are in the same directory as the .exe
                pluginsPath = Path.Combine(Application.dataPath, "Plugins");
            }
            else if (Application.platform == RuntimePlatform.OSXPlayer)
            {
                // macOS: Plugins are in Contents/Plugins
                pluginsPath = Path.Combine(Application.dataPath, "..", "Contents", "Plugins");
            }
            else if (Application.platform == RuntimePlatform.LinuxPlayer)
            {
                // Linux: Plugins are in the same directory as the executable
                pluginsPath = Path.Combine(Application.dataPath, "Plugins");
            }
            else
            {
                pluginsPath = Path.Combine(Application.dataPath, "Plugins");
            }

            if (Directory.Exists(pluginsPath))
            {
                Debug.Log($"[OpenJTalkDebug] Plugins directory exists: {pluginsPath}");
                var files = Directory.GetFiles(pluginsPath, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    Debug.Log($"[OpenJTalkDebug] Found: {file}");
                }
            }
            else
            {
                Debug.LogError($"[OpenJTalkDebug] Plugins directory not found: {pluginsPath}");
            }
#elif UNITY_ANDROID && !UNITY_EDITOR
            Debug.Log("[OpenJTalkDebug] Android platform - native libraries are loaded automatically from APK");
#endif

            // Try to load the library
            try
            {
                var version = Marshal.PtrToStringAnsi(openjtalk_get_version());
                Debug.Log($"[OpenJTalkDebug] SUCCESS! Library loaded. Version: {version}");
            }
            catch (DllNotFoundException ex)
            {
                Debug.LogError($"[OpenJTalkDebug] DllNotFoundException: {ex.Message}");
                Debug.LogError("[OpenJTalkDebug] The native library could not be found.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[OpenJTalkDebug] Exception: {ex.GetType().Name} - {ex.Message}");
                Debug.LogError($"[OpenJTalkDebug] Stack trace: {ex.StackTrace}");
            }
        }
    }
}