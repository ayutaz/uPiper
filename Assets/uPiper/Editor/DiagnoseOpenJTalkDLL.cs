#if UNITY_EDITOR
using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEditor;
using uPiper.Core.Platform;

namespace uPiper.Editor
{
    public class DiagnoseOpenJTalkDLL : EditorWindow
    {
        [DllImport("openjtalk_wrapper", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr openjtalk_get_version();
        
        // Also try with full path
        [DllImport("Assets/uPiper/Plugins/Windows/x86_64/openjtalk_wrapper.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr openjtalk_get_version_fullpath();
        
        // Windows API for checking dependencies
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);
        
        [MenuItem("uPiper/Diagnose OpenJTalk DLL")]
        static void Init()
        {
            var window = GetWindow<DiagnoseOpenJTalkDLL>();
            window.titleContent = new GUIContent("OpenJTalk DLL Diagnostics");
            window.Show();
        }
        
        void OnGUI()
        {
            EditorGUILayout.LabelField("OpenJTalk DLL Diagnostics", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            // Check DLL path
            var dllPath = Path.Combine(Application.dataPath, "uPiper/Plugins/Windows/x86_64/openjtalk_wrapper.dll");
            EditorGUILayout.LabelField("Expected DLL Path:");
            EditorGUILayout.TextField(dllPath);
            
            // Log to console for debugging
            Debug.Log($"[DiagnoseOpenJTalkDLL] Checking DLL at: {dllPath}");
            
            if (File.Exists(dllPath))
            {
                var fileInfo = new FileInfo(dllPath);
                EditorGUILayout.LabelField($"DLL exists: Yes");
                EditorGUILayout.LabelField($"File size: {fileInfo.Length:N0} bytes ({fileInfo.Length / 1024.0 / 1024.0:F2} MB)");
                EditorGUILayout.LabelField($"Last modified: {fileInfo.LastWriteTime}");
                
                Debug.Log($"[DiagnoseOpenJTalkDLL] DLL found - Size: {fileInfo.Length} bytes, Modified: {fileInfo.LastWriteTime}");
                
                // Check if it's the mock or full version
                if (fileInfo.Length < 100000) // Less than 100KB
                {
                    EditorGUILayout.HelpBox("This appears to be the mock/simplified version of the DLL!", MessageType.Error);
                    Debug.LogError("[DiagnoseOpenJTalkDLL] DLL is too small! This appears to be the mock version.");
                }
                else if (fileInfo.Length > 1000000) // More than 1MB
                {
                    EditorGUILayout.HelpBox("This appears to be the full version of the DLL.", MessageType.Info);
                    Debug.Log("[DiagnoseOpenJTalkDLL] DLL size indicates full version.");
                }
            }
            else
            {
                EditorGUILayout.LabelField("DLL exists: NO");
                EditorGUILayout.HelpBox("DLL file not found!", MessageType.Error);
                Debug.LogError($"[DiagnoseOpenJTalkDLL] DLL NOT FOUND at: {dllPath}");
            }
            
            EditorGUILayout.Space();
            
            // Try to get version
            if (GUILayout.Button("Test DLL Load"))
            {
                TestDLLLoad();
            }
            
            EditorGUILayout.Space();
            
            // Check ENABLE_PINVOKE define
            EditorGUILayout.LabelField("Compilation Defines:");
#if ENABLE_PINVOKE
            EditorGUILayout.HelpBox("ENABLE_PINVOKE is defined ✓", MessageType.Info);
#else
            EditorGUILayout.HelpBox("ENABLE_PINVOKE is NOT defined! P/Invoke calls are disabled!", MessageType.Error);
#endif
            
            EditorGUILayout.Space();
            
            // Unity restart reminder
            EditorGUILayout.HelpBox(
                "After making changes:\n\n" +
                "1. If you changed .asmdef files, Unity will recompile automatically\n" +
                "2. If you replaced the DLL, restart Unity Editor\n" +
                "3. Make sure no Unity processes are running\n" +
                "4. Try running tests again",
                MessageType.Warning);
            
            if (GUILayout.Button("Open Plugins Folder"))
            {
                var pluginsPath = Path.GetDirectoryName(dllPath);
                EditorUtility.RevealInFinder(pluginsPath);
            }
        }
        
        void TestDLLLoad()
        {
            Debug.Log("[DiagnoseOpenJTalkDLL] === Starting DLL Load Test ===");
            
            // Log environment info
            Debug.Log($"[DiagnoseOpenJTalkDLL] Application.dataPath: {Application.dataPath}");
            Debug.Log($"[DiagnoseOpenJTalkDLL] Application.platform: {Application.platform}");
            Debug.Log($"[DiagnoseOpenJTalkDLL] IntPtr.Size: {IntPtr.Size} (64-bit: {IntPtr.Size == 8})");
            Debug.Log($"[DiagnoseOpenJTalkDLL] Environment.Is64BitProcess: {Environment.Is64BitProcess}");
            
            // Check all possible DLL locations
            string[] possiblePaths = {
                Path.Combine(Application.dataPath, "uPiper/Plugins/Windows/x86_64/openjtalk_wrapper.dll"),
                Path.Combine(Application.dataPath, "Plugins/Windows/x86_64/openjtalk_wrapper.dll"),
                Path.Combine(Application.dataPath, "../Library/PackageCache/com.upiper/Plugins/Windows/x86_64/openjtalk_wrapper.dll"),
                "openjtalk_wrapper.dll"
            };
            
            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    Debug.Log($"[DiagnoseOpenJTalkDLL] Found DLL at: {path}");
                }
                else
                {
                    Debug.Log($"[DiagnoseOpenJTalkDLL] No DLL at: {path}");
                }
            }
            
            try
            {
                var versionPtr = openjtalk_get_version();
                if (versionPtr != IntPtr.Zero)
                {
                    var version = Marshal.PtrToStringAnsi(versionPtr);
                    EditorUtility.DisplayDialog("Success", $"DLL loaded successfully!\nVersion: {version}", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", "DLL loaded but version is null", "OK");
                }
            }
            catch (DllNotFoundException e)
            {
                Debug.LogError($"[DiagnoseOpenJTalkDLL] DLL Not Found: {e.Message}");
                Debug.LogError($"[DiagnoseOpenJTalkDLL] Full exception: {e}");
                
                // Try alternate method
                Debug.Log("Trying full path method...");
                try
                {
                    var versionPtr2 = openjtalk_get_version_fullpath();
                    if (versionPtr2 != IntPtr.Zero)
                    {
                        var version = Marshal.PtrToStringAnsi(versionPtr2);
                        EditorUtility.DisplayDialog("Success with Full Path", 
                            $"DLL loaded with full path!\nVersion: {version}", "OK");
                    }
                }
                catch (Exception e2)
                {
                    Debug.LogError($"[DiagnoseOpenJTalkDLL] Full path method also failed: {e2.Message}");
                    
                    // Check Windows system dependencies
                    Debug.Log("[DiagnoseOpenJTalkDLL] Checking system dependencies...");
                    string[] systemDlls = { "kernel32.dll", "msvcrt.dll", "vcruntime140.dll" };
                    foreach (var dll in systemDlls)
                    {
                        try
                        {
                            var handle = LoadLibrary(dll);
                            if (handle != IntPtr.Zero)
                            {
                                Debug.Log($"[DiagnoseOpenJTalkDLL] System DLL {dll}: OK");
                                FreeLibrary(handle);
                            }
                            else
                            {
                                Debug.LogError($"[DiagnoseOpenJTalkDLL] System DLL {dll}: FAILED TO LOAD");
                            }
                        }
                        catch (Exception sysEx)
                        {
                            Debug.LogError($"[DiagnoseOpenJTalkDLL] Error checking {dll}: {sysEx.Message}");
                        }
                    }
                    
                    EditorUtility.DisplayDialog("DLL Not Found", 
                        $"Cannot load openjtalk_wrapper.dll\n\n" +
                        $"Method 1: {e.Message}\n" +
                        $"Method 2: {e2.Message}\n\n" +
                        $"Check console log for detailed diagnostics.",
                        "OK");
                }
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error", 
                    $"Failed to load DLL\n\n" +
                    $"Type: {e.GetType().Name}\n" +
                    $"Message: {e.Message}",
                    "OK");
            }
        }
    }
}
#endif