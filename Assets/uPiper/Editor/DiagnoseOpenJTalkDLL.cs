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
            
            if (File.Exists(dllPath))
            {
                var fileInfo = new FileInfo(dllPath);
                EditorGUILayout.LabelField($"DLL exists: Yes");
                EditorGUILayout.LabelField($"File size: {fileInfo.Length:N0} bytes ({fileInfo.Length / 1024.0 / 1024.0:F2} MB)");
                EditorGUILayout.LabelField($"Last modified: {fileInfo.LastWriteTime}");
                
                // Check if it's the mock or full version
                if (fileInfo.Length < 100000) // Less than 100KB
                {
                    EditorGUILayout.HelpBox("This appears to be the mock/simplified version of the DLL!", MessageType.Error);
                }
                else if (fileInfo.Length > 1000000) // More than 1MB
                {
                    EditorGUILayout.HelpBox("This appears to be the full version of the DLL.", MessageType.Info);
                }
            }
            else
            {
                EditorGUILayout.LabelField("DLL exists: NO");
                EditorGUILayout.HelpBox("DLL file not found!", MessageType.Error);
            }
            
            EditorGUILayout.Space();
            
            // Try to get version
            if (GUILayout.Button("Test DLL Load"))
            {
                TestDLLLoad();
            }
            
            EditorGUILayout.Space();
            
            // Unity restart reminder
            EditorGUILayout.HelpBox(
                "After replacing the DLL, you MUST restart Unity Editor for changes to take effect!\n\n" +
                "1. Close Unity Editor completely\n" +
                "2. Make sure no Unity processes are running\n" +
                "3. Restart Unity Editor\n" +
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
                EditorUtility.DisplayDialog("DLL Not Found", 
                    $"Cannot load openjtalk_wrapper.dll\n\n" +
                    $"Error: {e.Message}\n\n" +
                    $"Please restart Unity Editor after replacing the DLL!",
                    "OK");
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