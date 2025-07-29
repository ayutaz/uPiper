using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace uPiper.Core.Platform
{
    /// <summary>
    /// Helper class for platform-specific operations.
    /// </summary>
    public static class PlatformHelper
    {
        /// <summary>
        /// Gets the current platform.
        /// </summary>
        public static RuntimePlatform Platform => Application.platform;

        /// <summary>
        /// Checks if running on Windows.
        /// </summary>
        public static bool IsWindows =>
            Platform == RuntimePlatform.WindowsPlayer ||
            Platform == RuntimePlatform.WindowsEditor;

        /// <summary>
        /// Checks if running on macOS.
        /// </summary>
        public static bool IsMacOS =>
            Platform == RuntimePlatform.OSXPlayer ||
            Platform == RuntimePlatform.OSXEditor;

        /// <summary>
        /// Checks if running on Linux.
        /// </summary>
        public static bool IsLinux =>
            Platform == RuntimePlatform.LinuxPlayer ||
            Platform == RuntimePlatform.LinuxEditor;

        /// <summary>
        /// Checks if running on Android.
        /// </summary>
        public static bool IsAndroid =>
            Platform == RuntimePlatform.Android;

        /// <summary>
        /// Checks if running on iOS.
        /// </summary>
        public static bool IsIOS =>
            Platform == RuntimePlatform.IPhonePlayer;

        /// <summary>
        /// Checks if running on WebGL.
        /// </summary>
        public static bool IsWebGL =>
            Platform == RuntimePlatform.WebGLPlayer;

        /// <summary>
        /// Gets the native library extension for the current platform.
        /// </summary>
        public static string NativeLibraryExtension
        {
            get
            {
                if (IsWindows) return ".dll";
                if (IsMacOS) return ".dylib";
                if (IsLinux) return ".so";
                if (IsAndroid) return ".so";
                if (IsIOS) return ".a"; // Static library
                return "";
            }
        }

        /// <summary>
        /// Gets the proper library name for DllImport based on platform.
        /// </summary>
        /// <param name="baseName">Base library name without extension</param>
        /// <returns>Platform-specific library name</returns>
        public static string GetNativeLibraryName(string baseName)
        {
            // On Windows, just return the base name (DllImport adds .dll automatically)
            if (IsWindows) return baseName;

            // On Unix-like systems, we might need to add "lib" prefix
            if (IsMacOS || IsLinux)
            {
                // Check if the library already has lib prefix
                if (!baseName.StartsWith("lib"))
                    return "lib" + baseName;
            }

            return baseName;
        }

        /// <summary>
        /// Gets the native library directory path.
        /// </summary>
        public static string GetNativeLibraryDirectory()
        {
            var basePath = Application.dataPath;

            // In editor
            if (Application.isEditor)
            {
                return Path.Combine(basePath, "uPiper", "Plugins");
            }

            // In builds
            var dataPath = Application.dataPath;
            if (IsWindows)
            {
                // Windows: Data/Plugins
                return Path.Combine(Path.GetDirectoryName(dataPath), "Plugins");
            }
            else if (IsMacOS)
            {
                // macOS: Contents/Plugins
                return Path.Combine(dataPath, "Plugins");
            }
            else if (IsLinux)
            {
                // Linux: Data/Plugins
                return Path.Combine(Path.GetDirectoryName(dataPath), "Plugins");
            }
            else if (IsAndroid)
            {
                // Android: lib/{architecture}
                return "lib";
            }
            else if (IsIOS)
            {
                // iOS: Frameworks
                return Path.Combine(dataPath, "Frameworks");
            }

            return Path.Combine(dataPath, "Plugins");
        }

        /// <summary>
        /// Gets the architecture string for the current platform.
        /// </summary>
        public static string GetArchitecture()
        {
            if (IsWindows)
            {
                // Windows: 64-bit only
                return "x64";
            }
            else if (IsLinux || IsMacOS)
            {
                return IntPtr.Size == 8 ? "x64" : "x86";
            }
            else if (IsAndroid)
            {
                // This is simplified; in practice, you'd check the actual architecture
                return "arm64-v8a";
            }
            else if (IsIOS)
            {
                return "arm64";
            }

            return "unknown";
        }

        /// <summary>
        /// Checks if the platform supports native plugins.
        /// </summary>
        public static bool SupportsNativePlugins =>
            !IsWebGL &&
            (IsWindows || IsMacOS || IsLinux || IsAndroid || IsIOS);

        /// <summary>
        /// Gets the path separator for the current platform.
        /// </summary>
        public static char PathSeparator => Path.DirectorySeparatorChar;

        /// <summary>
        /// Normalizes a path for the current platform.
        /// </summary>
        public static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            // Replace all separators with the current platform's separator
            return path.Replace('\\', PathSeparator).Replace('/', PathSeparator);
        }

        /// <summary>
        /// Gets the streaming assets path for native libraries.
        /// </summary>
        public static string GetStreamingAssetsNativePath()
        {
            return Path.Combine(Application.streamingAssetsPath, "uPiper", "Native");
        }

        /// <summary>
        /// Logs platform information for debugging.
        /// </summary>
        public static void LogPlatformInfo()
        {
            Debug.Log($"[PlatformHelper] Platform: {Platform}");
            Debug.Log($"[PlatformHelper] Architecture: {GetArchitecture()}");
            Debug.Log($"[PlatformHelper] Native Library Extension: {NativeLibraryExtension}");
            Debug.Log($"[PlatformHelper] Native Library Directory: {GetNativeLibraryDirectory()}");
            Debug.Log($"[PlatformHelper] Supports Native Plugins: {SupportsNativePlugins}");
        }
    }
}