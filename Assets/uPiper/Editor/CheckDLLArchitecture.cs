#if UNITY_EDITOR && UNITY_EDITOR_WIN
using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

namespace uPiper.Editor
{
    public static class CheckDLLArchitecture
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct IMAGE_DOS_HEADER
        {
            public ushort e_magic;    // Magic number
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 29)]
            public ushort[] e_res;
            public uint e_lfanew;     // File address of new exe header
        }

        [MenuItem("uPiper/Tools/Check DLL Architecture", false, 330)]
        private static void CheckArchitecture()
        {
            var dllPath = Path.Combine(Application.dataPath, "uPiper/Plugins/Windows/x86_64/openjtalk_wrapper.dll");

            Debug.Log("[CheckDLLArchitecture] === DLL Architecture Analysis ===");
            Debug.Log($"[CheckDLLArchitecture] Checking: {dllPath}");

            if (!File.Exists(dllPath))
            {
                Debug.LogError("[CheckDLLArchitecture] DLL not found!");
                return;
            }

            try
            {
                using var stream = new FileStream(dllPath, FileMode.Open, FileAccess.Read);
                using var reader = new BinaryReader(stream);
                // Read DOS header
                var dosHeader = new IMAGE_DOS_HEADER
                {
                    e_magic = reader.ReadUInt16()
                };

                if (dosHeader.e_magic != 0x5A4D) // "MZ"
                {
                    Debug.LogError("[CheckDLLArchitecture] Invalid DOS header - not a valid PE file!");
                    return;
                }

                // Skip to e_lfanew position
                stream.Seek(0x3C, SeekOrigin.Begin);
                dosHeader.e_lfanew = reader.ReadUInt32();

                // Seek to PE header
                stream.Seek(dosHeader.e_lfanew, SeekOrigin.Begin);

                // Read PE signature
                var peSignature = reader.ReadUInt32();
                if (peSignature != 0x00004550) // "PE\0\0"
                {
                    Debug.LogError("[CheckDLLArchitecture] Invalid PE signature!");
                    return;
                }

                // Read machine type
                var machine = reader.ReadUInt16();

                var architecture = machine switch
                {
                    0x014c => "x86 (32-bit)",
                    0x8664 => "x64 (64-bit)",
                    0x01c0 => "ARM",
                    0xaa64 => "ARM64",
                    _ => $"Unknown (0x{machine:X4})"
                };

                Debug.Log($"[CheckDLLArchitecture] DLL Architecture: {architecture}");

                if (machine == 0x014c)
                {
                    Debug.LogError("[CheckDLLArchitecture] This is a 32-bit DLL! Unity is running in 64-bit mode.");
                    Debug.LogError("[CheckDLLArchitecture] You need a 64-bit version of the DLL.");
                }
                else if (machine == 0x8664)
                {
                    Debug.Log("[CheckDLLArchitecture] ✓ Correct architecture for 64-bit Unity.");
                }

                // Read more PE header info
                var sizeOfOptionalHeader = reader.ReadUInt16();
                var characteristics = reader.ReadUInt16();

                Debug.Log($"[CheckDLLArchitecture] Characteristics: 0x{characteristics:X4}");

                if ((characteristics & 0x2000) != 0)
                    Debug.Log("[CheckDLLArchitecture] - DLL flag is set");
                if ((characteristics & 0x0020) != 0)
                    Debug.Log("[CheckDLLArchitecture] - Large address aware");

                // Check if it's a managed assembly
                var currentPos = stream.Position;
                stream.Seek(currentPos + 14, SeekOrigin.Begin); // Skip to Magic
                var magic = reader.ReadUInt16();

                if (magic == 0x10b)
                    Debug.Log("[CheckDLLArchitecture] PE32 format (32-bit)");
                else if (magic == 0x20b)
                    Debug.Log("[CheckDLLArchitecture] PE32+ format (64-bit)");
            }
            catch (Exception e)
            {
                Debug.LogError($"[CheckDLLArchitecture] Error reading DLL: {e.Message}");
            }

            // Also check the Unity process
            Debug.Log($"\n[CheckDLLArchitecture] Unity Process Info:");
            Debug.Log($"  - Is64BitProcess: {Environment.Is64BitProcess}");
            Debug.Log($"  - IntPtr.Size: {IntPtr.Size} bytes");

            // Check if this might be a MinGW/MSYS2 built DLL
            CheckDLLDependencies(dllPath);
        }

        private static void CheckDLLDependencies(string dllPath)
        {
            Debug.Log("\n[CheckDLLArchitecture] Checking for MinGW/MSYS2 dependencies:");

            // Common MinGW/MSYS2 runtime dependencies
            string[] mingwDlls = {
                "libgcc_s_seh-1.dll",
                "libwinpthread-1.dll",
                "libstdc++-6.dll",
                "msys-2.0.dll"
            };

            var dllDir = Path.GetDirectoryName(dllPath);
            foreach (var dll in mingwDlls)
            {
                var path = Path.Combine(dllDir, dll);
                if (File.Exists(path))
                {
                    Debug.LogWarning($"[CheckDLLArchitecture] Found MinGW dependency: {dll}");
                    Debug.LogWarning("[CheckDLLArchitecture] This DLL requires MinGW runtime libraries!");
                }
            }

            // Try to load with SetDllDirectory
            Debug.Log("\n[CheckDLLArchitecture] Suggesting solution:");
            Debug.Log("  1. The DLL might be built with MinGW and requires runtime libraries");
            Debug.Log("  2. Consider rebuilding with MSVC for better compatibility");
            Debug.Log("  3. Or ensure all MinGW runtime DLLs are in the same directory");
        }
    }
}
#endif