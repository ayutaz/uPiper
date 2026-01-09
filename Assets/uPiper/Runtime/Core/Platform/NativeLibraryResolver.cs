#if !UNITY_WEBGL

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace uPiper.Core.Platform
{
    /// <summary>
    /// OpenJTalk native library path resolution utility.
    /// Centralizes platform-specific library and dictionary path handling.
    /// </summary>
    public static class NativeLibraryResolver
    {
        #region Constants

        private const string LIBRARY_BASE_NAME = "openjtalk_wrapper";

        #endregion

        #region Properties

        /// <summary>
        /// Detects if running in CI/Docker environment (GitHub Actions, batch mode, etc.)
        /// </summary>
        public static bool IsCIEnvironment =>
            Application.isBatchMode || Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != null;

        #endregion

        #region Library Path Resolution

        /// <summary>
        /// Gets the expected library path for the current platform.
        /// </summary>
        /// <returns>The expected path to the native library</returns>
        public static string GetExpectedLibraryPath()
        {
            // Check for CI/Docker environment first (regardless of UNITY_EDITOR)
            if (PlatformHelper.IsWindows && IsCIEnvironment)
            {
                var ciPath = FindLibraryInCIEnvironment();
                if (!string.IsNullOrEmpty(ciPath))
                {
                    return ciPath;
                }
            }

#if UNITY_EDITOR
            return GetEditorLibraryPath();
#else
            return GetRuntimeLibraryPath();
#endif
        }

        /// <summary>
        /// Gets alternative library paths for fallback search.
        /// </summary>
        /// <returns>List of alternative paths to check</returns>
        public static List<string> GetAlternativeLibraryPaths()
        {
            var paths = new List<string>();

#if UNITY_EDITOR
            var (libraryFileName, platformFolder) = GetPlatformLibraryInfo();

            // Primary path after setup
            paths.Add(Path.Combine(Application.dataPath, "uPiper", "Plugins", platformFolder, libraryFileName));

            // PackageCache path for UPM installations
            var packageCachePath = Path.Combine(Application.dataPath, "..", "Library", "PackageCache");
            if (Directory.Exists(packageCachePath))
            {
                try
                {
                    var packageDirs = Directory.GetDirectories(packageCachePath, "com.ayutaz.upiper@*");
                    foreach (var packageDir in packageDirs)
                    {
                        var packagePluginPath = Path.Combine(packageDir, "Plugins", platformFolder, libraryFileName);
                        paths.Add(packagePluginPath);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[NativeLibraryResolver] Exception while searching PackageCache: {ex}");
                }
            }

            // Legacy paths for backward compatibility
            if (PlatformHelper.IsWindows)
            {
                paths.Add(Path.Combine(Application.dataPath, "Plugins", "x86_64", "openjtalk_wrapper.dll"));
            }
#endif

            return paths;
        }

        /// <summary>
        /// Checks if the native library is available on the current platform.
        /// </summary>
        /// <returns>True if the library is available</returns>
        public static bool IsNativeLibraryAvailable()
        {
            try
            {
                var libraryPath = GetExpectedLibraryPath();
                if (string.IsNullOrEmpty(libraryPath))
                {
                    Debug.LogError("[NativeLibraryResolver] No library path defined for current platform");
                    return false;
                }

#if UNITY_EDITOR
                return CheckEditorLibraryAvailability(libraryPath);
#else
                return CheckRuntimeLibraryAvailability(libraryPath);
#endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NativeLibraryResolver] Error checking library availability: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Package Path Resolution

        /// <summary>
        /// Gets the UPM package path if installed via Package Manager.
        /// </summary>
        /// <returns>Package path or null if not found</returns>
        public static string GetPackagePath()
        {
#if UNITY_EDITOR
            // Method 1: Check if uPiper assets exist in Assets folder
            var localPath = Path.Combine(Application.dataPath, "uPiper");
            if (!Directory.Exists(localPath))
            {
                // Not in Assets, likely a package installation
                // Try to find in PackageCache
                var packageCachePath = Path.Combine(Application.dataPath, "..", "Library", "PackageCache");
                if (Directory.Exists(packageCachePath))
                {
                    // Search for uPiper package
                    var packageDirs = Directory.GetDirectories(packageCachePath, "com.ayutaz.upiper@*");
                    if (packageDirs.Length > 0)
                    {
                        var packagePath = packageDirs[0]; // Use first match
                        Debug.Log($"[NativeLibraryResolver] Detected package installation at: {packagePath}");
                        return packagePath;
                    }
                }
            }

            // Method 2: Try using UnityEditor.PackageManager (if available)
            try
            {
                var packageListRequest = UnityEditor.PackageManager.Client.List(true);
                while (!packageListRequest.IsCompleted)
                {
                    System.Threading.Thread.Sleep(10);
                }

                if (packageListRequest.Status == UnityEditor.PackageManager.StatusCode.Success)
                {
                    foreach (var package in packageListRequest.Result)
                    {
                        if (package.name == "com.ayutaz.upiper")
                        {
                            Debug.Log($"[NativeLibraryResolver] Found package via PackageManager at: {package.resolvedPath}");
                            return package.resolvedPath;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Log($"[NativeLibraryResolver] Could not use PackageManager API: {ex.Message}");
            }
#endif
            return null;
        }

        #endregion

        #region Private Helpers

        private static (string libraryFileName, string platformFolder) GetPlatformLibraryInfo()
        {
            if (PlatformHelper.IsWindows)
            {
                return ("openjtalk_wrapper.dll", Path.Combine("Windows", "x86_64"));
            }
            else if (PlatformHelper.IsMacOS)
            {
                return ("openjtalk_wrapper.bundle", "macOS");
            }
            else if (PlatformHelper.IsLinux)
            {
                return ("libopenjtalk_wrapper.so", Path.Combine("Linux", "x86_64"));
            }

            return (LIBRARY_BASE_NAME, "");
        }

        private static string FindLibraryInCIEnvironment()
        {
            var possiblePaths = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "openjtalk_wrapper.dll"),
                Path.Combine(Application.dataPath, "Plugins", "x86_64", "openjtalk_wrapper.dll"),
                Path.Combine(Directory.GetCurrentDirectory(), "Library", "ScriptAssemblies", "openjtalk_wrapper.dll"),
                Path.Combine(Application.dataPath, "uPiper", "Plugins", "Windows", "x86_64", "openjtalk_wrapper.dll")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    Debug.Log($"[NativeLibraryResolver] Found library in CI environment: {path}");
                    return path;
                }
            }

            // If not found, log all searched paths for debugging
            Debug.LogError("[NativeLibraryResolver] DLL not found in CI environment. Searched paths:");
            foreach (var path in possiblePaths)
            {
                Debug.LogError($"  - {path} (exists: {File.Exists(path)})");
            }

            return null;
        }

#if UNITY_EDITOR
        private static string GetEditorLibraryPath()
        {
            var (libraryFileName, platformFolder) = GetPlatformLibraryInfo();

            // Primary path after setup
            var primaryPath = Path.Combine(Application.dataPath, "uPiper", "Plugins", platformFolder, libraryFileName);

            // Check primary path first
            if (CheckLibraryExists(primaryPath, libraryFileName))
            {
                Debug.Log($"[NativeLibraryResolver] Found library at: {primaryPath}");
                return primaryPath;
            }

            // Check legacy paths for backward compatibility
            if (PlatformHelper.IsWindows)
            {
                var legacyPath = Path.Combine(Application.dataPath, "Plugins", "x86_64", "openjtalk_wrapper.dll");
                if (File.Exists(legacyPath))
                {
                    Debug.Log($"[NativeLibraryResolver] Found library at legacy path: {legacyPath}");
                    return legacyPath;
                }
            }

            // Return primary path even if not found (caller will handle error)
            return primaryPath;
        }

        private static bool CheckLibraryExists(string path, string libraryFileName)
        {
            if (PlatformHelper.IsMacOS && libraryFileName.EndsWith(".bundle"))
            {
                if (Directory.Exists(path))
                {
                    var binaryPath = Path.Combine(path, "Contents", "MacOS", "openjtalk_wrapper");
                    return File.Exists(binaryPath);
                }
                return false;
            }

            return File.Exists(path);
        }

        private static bool CheckEditorLibraryAvailability(string libraryPath)
        {
            var (libraryFileName, _) = GetPlatformLibraryInfo();

            // For bundle format on macOS, check if directory exists
            var libraryExists = CheckLibraryExists(libraryPath, libraryFileName);

            if (!libraryExists)
            {
                // Try alternative paths before giving up
                var alternativePaths = GetAlternativeLibraryPaths();
                foreach (var altPath in alternativePaths)
                {
                    if (CheckLibraryExists(altPath, libraryFileName))
                    {
                        Debug.Log($"[NativeLibraryResolver] Found library at alternative location: {altPath}");
                        return true;
                    }
                }

                // Library not found - provide helpful error message
                Debug.LogError("[NativeLibraryResolver] Native library not found. Searched locations:");
                Debug.LogError($"  Primary: {libraryPath}");
                foreach (var altPath in alternativePaths)
                {
                    Debug.LogError($"  - {altPath}");
                }

                Debug.LogError("[NativeLibraryResolver] Please run 'uPiper/Setup/Run Initial Setup' from the menu to copy required files.");

                return false;
            }

            Debug.Log($"[NativeLibraryResolver] Native library found at: {libraryPath}");
            return true;
        }
#endif

        private static string GetRuntimeLibraryPath()
        {
            // In built application, Unity automatically loads native plugins
            // We just need to return the expected library name
            if (PlatformHelper.IsWindows)
                return "openjtalk_wrapper.dll";
            else if (PlatformHelper.IsMacOS)
                return "libopenjtalk_wrapper.dylib";
            else if (PlatformHelper.IsLinux)
                return "libopenjtalk_wrapper.so";
            else if (PlatformHelper.IsAndroid)
                return "libopenjtalk_wrapper.so";
            else if (PlatformHelper.IsIOS)
                return "libopenjtalk_wrapper.a";
            else
                return LIBRARY_BASE_NAME; // Fallback for unknown platforms
        }

#if !UNITY_EDITOR
        private static bool CheckRuntimeLibraryAvailability(string libraryPath)
        {
            // For Android and iOS, the library is always available at runtime
            // (statically linked or included in APK/IPA)
            if (PlatformHelper.IsAndroid || PlatformHelper.IsIOS)
            {
                Debug.Log($"[NativeLibraryResolver] Native library is statically linked on {(PlatformHelper.IsAndroid ? "Android" : "iOS")}");
                return true;
            }

            // For other platforms, we can't easily check without P/Invoke
            // The actual availability will be determined when trying to use the library
            return true;
        }
#endif

        #endregion

        #region Logging Helpers

        /// <summary>
        /// Logs detailed environment information for debugging.
        /// </summary>
        public static void LogEnvironmentInfo()
        {
            Debug.Log("[NativeLibraryResolver] Environment info:");
            Debug.Log($"  UNITY_EDITOR: {Application.isEditor}");
            Debug.Log($"  Batch mode: {Application.isBatchMode}");
            Debug.Log($"  Platform: {Application.platform}");
            Debug.Log($"  Data path: {Application.dataPath}");
            Debug.Log($"  Current directory: {Directory.GetCurrentDirectory()}");
            Debug.Log($"  GITHUB_ACTIONS env: {Environment.GetEnvironmentVariable("GITHUB_ACTIONS")}");
            Debug.Log($"  Is CI Environment: {IsCIEnvironment}");
        }

        /// <summary>
        /// Logs plugin directory contents for debugging.
        /// </summary>
        public static void LogPluginDirectoryContents()
        {
            var pluginsPath = Path.Combine(Application.dataPath, "uPiper", "Plugins");
            if (Directory.Exists(pluginsPath))
            {
                Debug.Log($"[NativeLibraryResolver] Plugins directory exists: {pluginsPath}");
                foreach (var file in Directory.GetFiles(pluginsPath, "*", SearchOption.AllDirectories))
                {
                    Debug.Log($"[NativeLibraryResolver]   Found: {file}");
                }
            }
            else
            {
                Debug.LogError($"[NativeLibraryResolver] Plugins directory not found: {pluginsPath}");
            }
        }

        #endregion
    }
}

#endif