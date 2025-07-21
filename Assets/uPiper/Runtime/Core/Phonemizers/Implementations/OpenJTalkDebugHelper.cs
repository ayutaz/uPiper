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
            
            // Check Plugins directory
#if !UNITY_EDITOR
            var pluginsPath = Path.Combine(Application.dataPath, "..", "Contents", "Plugins");
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