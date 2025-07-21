#if UNITY_EDITOR && UNITY_EDITOR_WIN
using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEditor;

namespace uPiper.Editor
{
    public static class TestDirectDLLLoad
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);
        
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);
        
        [DllImport("kernel32.dll")]
        private static extern uint GetLastError();
        
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern uint FormatMessage(uint dwFlags, IntPtr lpSource, uint dwMessageId, 
            uint dwLanguageId, out IntPtr lpBuffer, uint nSize, IntPtr Arguments);
        
        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr hMem);
        
        // LoadLibraryEx flags
        private const uint LOAD_WITH_ALTERED_SEARCH_PATH = 0x00000008;
        private const uint LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000;
        private const uint LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR = 0x00000100;
        
        [MenuItem("uPiper/Debug/Test Direct DLL Load")]
        static void TestLoad()
        {
            Debug.Log("[TestDirectDLLLoad] === Starting Direct DLL Load Test ===");
            
            var dllPath = Path.Combine(Application.dataPath, "uPiper/Plugins/Windows/x86_64/openjtalk_wrapper.dll");
            dllPath = Path.GetFullPath(dllPath); // Get absolute path
            
            Debug.Log($"[TestDirectDLLLoad] DLL Path: {dllPath}");
            Debug.Log($"[TestDirectDLLLoad] File exists: {File.Exists(dllPath)}");
            
            if (!File.Exists(dllPath))
            {
                Debug.LogError("[TestDirectDLLLoad] DLL file does not exist!");
                return;
            }
            
            // Try different loading methods
            TestLoadMethod(dllPath, "Method 1: LoadLibraryEx with default", 0);
            TestLoadMethod(dllPath, "Method 2: LoadLibraryEx with ALTERED_SEARCH_PATH", LOAD_WITH_ALTERED_SEARCH_PATH);
            TestLoadMethod(dllPath, "Method 3: LoadLibraryEx with DLL_LOAD_DIR", LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR);
            
            // Also try loading from current directory
            var currentDir = Directory.GetCurrentDirectory();
            var dllName = "openjtalk_wrapper.dll";
            var localPath = Path.Combine(currentDir, dllName);
            
            Debug.Log($"\n[TestDirectDLLLoad] Trying from current directory: {currentDir}");
            if (File.Exists(localPath))
            {
                TestLoadMethod(localPath, "Method 4: From current directory", 0);
            }
            else
            {
                Debug.Log($"[TestDirectDLLLoad] No DLL in current directory");
                
                // Try copying DLL to current directory
                Debug.Log("[TestDirectDLLLoad] Copying DLL to current directory for test...");
                try
                {
                    File.Copy(dllPath, localPath, true);
                    TestLoadMethod(dllName, "Method 5: Just DLL name after copy", 0);
                    File.Delete(localPath); // Clean up
                }
                catch (Exception e)
                {
                    Debug.LogError($"[TestDirectDLLLoad] Failed to copy DLL: {e.Message}");
                }
            }
            
            // Check Visual C++ Redistributables
            CheckVCRedist();
        }
        
        static void TestLoadMethod(string path, string methodName, uint flags)
        {
            Debug.Log($"\n[TestDirectDLLLoad] {methodName}:");
            Debug.Log($"  Path: {path}");
            
            IntPtr handle = LoadLibraryEx(path, IntPtr.Zero, flags);
            
            if (handle != IntPtr.Zero)
            {
                Debug.Log($"  ✓ SUCCESS! Handle: 0x{handle.ToInt64():X}");
                
                // Try to get function pointer
                IntPtr procAddr = GetProcAddress(handle, "openjtalk_get_version");
                if (procAddr != IntPtr.Zero)
                {
                    Debug.Log($"  ✓ Found openjtalk_get_version at: 0x{procAddr.ToInt64():X}");
                }
                else
                {
                    Debug.LogError("  ✗ Could not find openjtalk_get_version function");
                }
                
                FreeLibrary(handle);
            }
            else
            {
                uint error = GetLastError();
                string errorMessage = GetErrorMessage(error);
                Debug.LogError($"  ✗ FAILED! Error {error}: {errorMessage}");
                
                // Common error codes
                switch (error)
                {
                    case 126: // ERROR_MOD_NOT_FOUND
                        Debug.LogError("    → Missing dependencies. Check with Dependencies Walker or dumpbin.");
                        break;
                    case 127: // ERROR_PROC_NOT_FOUND
                        Debug.LogError("    → Procedure not found. DLL may be corrupted.");
                        break;
                    case 193: // ERROR_BAD_EXE_FORMAT
                        Debug.LogError("    → Wrong architecture (32-bit vs 64-bit mismatch).");
                        break;
                }
            }
        }
        
        static string GetErrorMessage(uint errorCode)
        {
            IntPtr lpBuffer = IntPtr.Zero;
            uint size = FormatMessage(
                0x00001000 | 0x00000200, // FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM
                IntPtr.Zero,
                errorCode,
                0,
                out lpBuffer,
                0,
                IntPtr.Zero);
            
            if (size > 0 && lpBuffer != IntPtr.Zero)
            {
                string message = Marshal.PtrToStringAuto(lpBuffer);
                LocalFree(lpBuffer);
                return message.Trim();
            }
            
            return $"Unknown error {errorCode}";
        }
        
        static void CheckVCRedist()
        {
            Debug.Log("\n[TestDirectDLLLoad] Checking Visual C++ Redistributables:");
            
            string[] vcDlls = {
                "msvcp140.dll",
                "vcruntime140.dll",
                "vcruntime140_1.dll",
                "msvcrt.dll"
            };
            
            string system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
            foreach (var dll in vcDlls)
            {
                var path = Path.Combine(system32, dll);
                if (File.Exists(path))
                {
                    Debug.Log($"  ✓ {dll} found");
                }
                else
                {
                    Debug.LogError($"  ✗ {dll} NOT FOUND - Install Visual C++ Redistributable!");
                }
            }
        }
    }
}
#endif