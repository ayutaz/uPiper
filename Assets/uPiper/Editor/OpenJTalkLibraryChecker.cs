using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

namespace uPiper.Editor
{
    /// <summary>
    /// Editor tool to check OpenJTalk native library status
    /// </summary>
    public class OpenJTalkLibraryChecker : EditorWindow
    {
        [DllImport("openjtalk_wrapper", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr openjtalk_get_version();
        
        [MenuItem("uPiper/Debug/Check OpenJTalk Library")]
        public static void CheckLibrary()
        {
            Debug.Log("=== OpenJTalk Library Check ===");
            
            // Check library files
            string pluginsPath = Path.Combine(Application.dataPath, "uPiper", "Plugins", "macOS");
            Debug.Log($"Plugins path: {pluginsPath}");
            
            if (Directory.Exists(pluginsPath))
            {
                string[] files = Directory.GetFiles(pluginsPath, "*.dylib");
                foreach (string file in files)
                {
                    Debug.Log($"Found library: {Path.GetFileName(file)}");
                    FileInfo fi = new FileInfo(file);
                    Debug.Log($"  Size: {fi.Length} bytes");
                    Debug.Log($"  Last modified: {fi.LastWriteTime}");
                }
            }
            else
            {
                Debug.LogError("Plugins/macOS directory not found!");
            }
            
            // Try to load library
            Debug.Log("\nAttempting to call native function...");
            try
            {
                IntPtr versionPtr = openjtalk_get_version();
                if (versionPtr != IntPtr.Zero)
                {
                    string version = Marshal.PtrToStringAnsi(versionPtr);
                    Debug.Log($"SUCCESS: OpenJTalk version: {version}");
                }
                else
                {
                    Debug.LogError("Version pointer is null");
                }
            }
            catch (DllNotFoundException ex)
            {
                Debug.LogError($"DllNotFoundException: {ex.Message}");
                Debug.LogError("Library file exists but cannot be loaded. Possible reasons:");
                Debug.LogError("- Library architecture mismatch");
                Debug.LogError("- Missing dependencies");
                Debug.LogError("- Library not properly signed (macOS)");
            }
            catch (EntryPointNotFoundException ex)
            {
                Debug.LogError($"EntryPointNotFoundException: {ex.Message}");
                Debug.LogError("Library loaded but function not found");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Unexpected error: {ex.GetType().Name}: {ex.Message}");
            }
            
            // Check dictionary
            Debug.Log("\n=== Dictionary Check ===");
            string dictPath = Path.Combine(Application.dataPath, "uPiper", "Native", "OpenJTalk", "test_dictionary");
            if (Directory.Exists(dictPath))
            {
                Debug.Log($"Test dictionary found at: {dictPath}");
                string[] requiredFiles = { "sys.dic", "unk.dic", "char.bin", "matrix.bin" };
                foreach (string file in requiredFiles)
                {
                    string filePath = Path.Combine(dictPath, file);
                    if (File.Exists(filePath))
                    {
                        FileInfo fi = new FileInfo(filePath);
                        Debug.Log($"  {file}: {fi.Length} bytes");
                    }
                    else
                    {
                        Debug.LogError($"  {file}: NOT FOUND");
                    }
                }
            }
            else
            {
                Debug.LogError($"Test dictionary not found at: {dictPath}");
            }
        }
    }
}