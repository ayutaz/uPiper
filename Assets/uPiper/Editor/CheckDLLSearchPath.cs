#if UNITY_EDITOR
using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEditor;

namespace uPiper.Editor
{
    public static class CheckDLLSearchPath
    {
        [MenuItem("uPiper/Debug/Check DLL Search Path")]
        static void CheckSearchPath()
        {
            Debug.Log("[CheckDLLSearchPath] === DLL Search Path Analysis ===");
            
            // Get PATH environment variable
            string pathEnv = Environment.GetEnvironmentVariable("PATH");
            Debug.Log($"[CheckDLLSearchPath] PATH environment variable:");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                var paths = pathEnv.Split(';');
                foreach (var path in paths)
                {
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        Debug.Log($"  - {path}");
                    }
                }
            }
            
            // Check Unity's expected plugin locations
            Debug.Log("\n[CheckDLLSearchPath] Unity Plugin Locations:");
            
            // Editor mode paths
            string[] unityPaths = {
                Application.dataPath,
                Path.Combine(Application.dataPath, "Plugins"),
                Path.Combine(Application.dataPath, "uPiper", "Plugins"),
                Path.Combine(Application.dataPath, "uPiper", "Plugins", "Windows"),
                Path.Combine(Application.dataPath, "uPiper", "Plugins", "Windows", "x86_64"),
                Path.GetDirectoryName(Application.dataPath), // Project root
                EditorApplication.applicationPath, // Unity Editor path
                Path.GetDirectoryName(EditorApplication.applicationPath) // Unity Editor directory
            };
            
            foreach (var path in unityPaths)
            {
                Debug.Log($"  - {path} (Exists: {Directory.Exists(path)})");
            }
            
            // Check if our DLL is in any of these locations
            Debug.Log("\n[CheckDLLSearchPath] Searching for openjtalk_wrapper.dll:");
            
            foreach (var basePath in unityPaths)
            {
                if (Directory.Exists(basePath))
                {
                    try
                    {
                        var dllFiles = Directory.GetFiles(basePath, "openjtalk_wrapper.dll", SearchOption.AllDirectories);
                        foreach (var dll in dllFiles)
                        {
                            var fileInfo = new FileInfo(dll);
                            Debug.Log($"  FOUND: {dll} (Size: {fileInfo.Length} bytes)");
                        }
                    }
                    catch (Exception)
                    {
                        // Ignore access denied errors
                    }
                }
            }
            
            // Check current working directory
            Debug.Log($"\n[CheckDLLSearchPath] Current Working Directory: {Directory.GetCurrentDirectory()}");
            
            // Check if running in 64-bit mode
            Debug.Log($"\n[CheckDLLSearchPath] Process Architecture:");
            Debug.Log($"  - Is64BitProcess: {Environment.Is64BitProcess}");
            Debug.Log($"  - IntPtr.Size: {IntPtr.Size} bytes");
            Debug.Log($"  - Processor Architecture: {Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")}");
        }
        
        [MenuItem("uPiper/Debug/Force Reimport DLL")]
        static void ForceReimportDLL()
        {
            var dllPath = "Assets/uPiper/Plugins/Windows/x86_64/openjtalk_wrapper.dll";
            
            if (File.Exists(Path.Combine(Application.dataPath, "..", dllPath)))
            {
                Debug.Log($"[CheckDLLSearchPath] Force reimporting: {dllPath}");
                AssetDatabase.ImportAsset(dllPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.Refresh();
                Debug.Log("[CheckDLLSearchPath] Reimport complete. You may need to restart Unity Editor.");
            }
            else
            {
                Debug.LogError($"[CheckDLLSearchPath] DLL not found at: {dllPath}");
            }
        }
    }
}
#endif