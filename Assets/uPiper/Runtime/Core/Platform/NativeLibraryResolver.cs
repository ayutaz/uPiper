#if !UNITY_WEBGL

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using uPiper.Core.Logging;

namespace uPiper.Core.Platform
{
    /// <summary>
    /// Centralized resolver for native library and dictionary paths.
    /// Handles platform-specific path resolution for OpenJTalk and other native libraries.
    /// </summary>
    public static class NativeLibraryResolver
    {
        #region Library Path Resolution

        /// <summary>
        /// Get the expected path for the OpenJTalk native library based on the current platform.
        /// </summary>
        /// <returns>The expected library path, or null if the platform is not supported.</returns>
        public static string GetExpectedLibraryPath()
        {
            // Check for CI/Docker environment first (regardless of UNITY_EDITOR)
            if (PlatformHelper.IsWindows && (Application.isBatchMode || Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != null))
            {
                // CI/Docker environment: Check multiple locations
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
                        PiperLogger.LogDebug($"[NativeLibraryResolver] Found library in CI environment: {path}");
                        return path;
                    }
                }

                // If not found, log all searched paths for debugging
                PiperLogger.LogError($"[NativeLibraryResolver] DLL not found in CI environment. Searched paths:");
                foreach (var path in possiblePaths)
                {
                    PiperLogger.LogError($"  - {path} (exists: {File.Exists(path)})");
                }
            }

#if UNITY_EDITOR
            // Define library file name based on platform
            string libraryFileName;
            string platformFolder;

            if (PlatformHelper.IsWindows)
            {
                libraryFileName = "openjtalk_wrapper.dll";
                platformFolder = Path.Combine("Windows", "x86_64");
            }
            else if (PlatformHelper.IsMacOS)
            {
                libraryFileName = "openjtalk_wrapper.bundle";
                platformFolder = "macOS";
            }
            else if (PlatformHelper.IsLinux)
            {
                libraryFileName = "libopenjtalk_wrapper.so";
                platformFolder = Path.Combine("Linux", "x86_64");
            }
            else
            {
                return null;
            }

            // After initial setup, files should be in fixed location
            var primaryPath = Path.Combine(Application.dataPath, "uPiper", "Plugins", platformFolder, libraryFileName);

            // Check primary path first
            if (PlatformHelper.IsMacOS && libraryFileName.EndsWith(".bundle"))
            {
                if (Directory.Exists(primaryPath))
                {
                    // Verify the actual binary exists inside the bundle
                    var binaryPath = Path.Combine(primaryPath, "Contents", "MacOS", "openjtalk_wrapper");
                    if (File.Exists(binaryPath))
                    {
                        PiperLogger.LogDebug($"[NativeLibraryResolver] Found library at: {primaryPath}");
                        return primaryPath;
                    }
                }
            }
            else if (File.Exists(primaryPath))
            {
                PiperLogger.LogDebug($"[NativeLibraryResolver] Found library at: {primaryPath}");
                return primaryPath;
            }

            // Check legacy paths for backward compatibility
            if (PlatformHelper.IsWindows)
            {
                var legacyPath = Path.Combine(Application.dataPath, "Plugins", "x86_64", "openjtalk_wrapper.dll");
                if (File.Exists(legacyPath))
                {
                    PiperLogger.LogDebug($"[NativeLibraryResolver] Found library at legacy path: {legacyPath}");
                    return legacyPath;
                }
            }

            // Library not found at expected path - return path anyway
            // The caller will check alternative paths and display appropriate error messages
            return primaryPath;
#else
            // In built application, Unity automatically loads native plugins
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
                return "openjtalk_wrapper";  // Fallback for unknown platforms
#endif
        }

        /// <summary>
        /// Get alternative paths where the native library might be located.
        /// </summary>
        /// <returns>List of alternative library paths to check.</returns>
        public static List<string> GetAlternativeLibraryPaths()
        {
            var paths = new List<string>();

#if UNITY_EDITOR
            string libraryFileName;
            string platformFolder;

            if (PlatformHelper.IsWindows)
            {
                libraryFileName = "openjtalk_wrapper.dll";
                platformFolder = Path.Combine("Windows", "x86_64");
            }
            else if (PlatformHelper.IsMacOS)
            {
                libraryFileName = "openjtalk_wrapper.bundle";
                platformFolder = "macOS";
            }
            else if (PlatformHelper.IsLinux)
            {
                libraryFileName = "libopenjtalk_wrapper.so";
                platformFolder = Path.Combine("Linux", "x86_64");
            }
            else
            {
                return paths;
            }

            // After initial setup, files should be in fixed location
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
                    PiperLogger.LogWarning($"[NativeLibraryResolver] Exception occurred while searching PackageCache: {ex}");
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

        #endregion

        #region Package Path Resolution

        /// <summary>
        /// Get the package installation path for uPiper.
        /// </summary>
        /// <returns>The package path if found, null otherwise.</returns>
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
                        PiperLogger.LogDebug($"[NativeLibraryResolver] Detected package installation at: {packagePath}");
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
                            PiperLogger.LogDebug($"[NativeLibraryResolver] Found package via PackageManager at: {package.resolvedPath}");
                            return package.resolvedPath;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                PiperLogger.LogDebug($"[NativeLibraryResolver] Could not use PackageManager API: {ex.Message}");
            }
#endif
            return null;
        }

        #endregion

        #region Dictionary Path Resolution

        /// <summary>
        /// Get the default dictionary path for OpenJTalk.
        /// </summary>
        /// <returns>The dictionary path.</returns>
        public static string GetDefaultDictionaryPath()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // On Android, use the persistent data path where we extract the dictionary
            return AndroidPathResolver.GetOpenJTalkDictionaryPath();
#elif UNITY_EDITOR && UPIPER_DEVELOPMENT
            // Development environment: Load directly from Samples~
            var developmentPath = uPiperPaths.GetDevelopmentOpenJTalkPath();
            if (Directory.Exists(developmentPath))
            {
                PiperLogger.LogDebug($"[NativeLibraryResolver] Development mode: Loading from Samples~: {developmentPath}");
                return developmentPath;
            }
            else
            {
                PiperLogger.LogError($"[NativeLibraryResolver] Development mode: Dictionary not found at: {developmentPath}");
                return developmentPath; // Return expected path for error messages
            }
#elif UNITY_IOS && !UNITY_EDITOR
            // On iOS, use the IOSPathResolver for proper path handling
            return IOSPathResolver.GetOpenJTalkDictionaryPath();
#else
            // After setup, files should always be in fixed locations - using consistent path structure
            var primaryPath = Path.Combine(Application.streamingAssetsPath, "uPiper", "OpenJTalk", "naist_jdic", "open_jtalk_dic_utf_8-1.11");

            // Check primary path first
            if (Directory.Exists(primaryPath))
            {
                // Verify it contains required files
                var allFilesExist = true;
                foreach (var file in OpenJTalkConstants.RequiredDictionaryFiles)
                {
                    if (!File.Exists(Path.Combine(primaryPath, file)))
                    {
                        allFilesExist = false;
                        break;
                    }
                }

                if (allFilesExist)
                {
                    PiperLogger.LogDebug($"[NativeLibraryResolver] Found dictionary at: {primaryPath}");
                    return primaryPath;
                }
            }

            // Fallback to legacy path
            var legacyPath = Path.Combine(Application.streamingAssetsPath, "uPiper", "OpenJTalk", "dictionary");
            if (Directory.Exists(legacyPath))
            {
                PiperLogger.LogDebug($"[NativeLibraryResolver] Found dictionary at legacy path: {legacyPath}");
                return legacyPath;
            }

            PiperLogger.LogError($"[NativeLibraryResolver] Dictionary not found. Please run 'uPiper/Setup/Run Initial Setup' from the menu.");
            return primaryPath; // Return expected path for error messages
#endif
        }

        #endregion

        #region Library Availability Check

        /// <summary>
        /// Check if the native library is available and can be loaded.
        /// </summary>
        /// <returns>True if the library is available, false otherwise.</returns>
        public static bool IsNativeLibraryAvailable()
        {
            try
            {
                var libraryPath = GetExpectedLibraryPath();
                if (string.IsNullOrEmpty(libraryPath))
                {
                    PiperLogger.LogError("[NativeLibraryResolver] No library path defined for current platform");
                    return false;
                }

#if UNITY_EDITOR
                // In Editor, check if the library file exists
                var libraryExists = false;
                if (PlatformHelper.IsMacOS && libraryPath.EndsWith(".bundle"))
                {
                    libraryExists = Directory.Exists(libraryPath);
                    if (libraryExists)
                    {
                        // Verify the actual binary exists inside the bundle
                        var binaryPath = Path.Combine(libraryPath, "Contents", "MacOS", "openjtalk_wrapper");
                        if (!File.Exists(binaryPath))
                        {
                            PiperLogger.LogError($"[NativeLibraryResolver] Bundle exists but binary not found: {binaryPath}");
                            libraryExists = false;
                        }
                    }
                }
                else
                {
                    libraryExists = File.Exists(libraryPath);
                }

                if (!libraryExists)
                {
                    // Try alternative paths before giving up
                    var alternativePaths = GetAlternativeLibraryPaths();
                    foreach (var altPath in alternativePaths)
                    {
                        if (PlatformHelper.IsMacOS && altPath.EndsWith(".bundle"))
                        {
                            if (Directory.Exists(altPath))
                            {
                                var binaryPath = Path.Combine(altPath, "Contents", "MacOS", "openjtalk_wrapper");
                                if (File.Exists(binaryPath))
                                {
                                    PiperLogger.LogDebug($"[NativeLibraryResolver] Found library at alternative location: {altPath}");
                                    return true;
                                }
                            }
                        }
                        else if (File.Exists(altPath))
                        {
                            PiperLogger.LogDebug($"[NativeLibraryResolver] Found library at alternative location: {altPath}");
                            return true;
                        }
                    }

                    // Library not found - provide helpful error message
                    PiperLogger.LogError($"[NativeLibraryResolver] Native library not found. Searched locations:");
                    PiperLogger.LogError($"  Primary: {libraryPath}");
                    foreach (var altPath in alternativePaths)
                    {
                        PiperLogger.LogError($"  - {altPath}");
                    }

                    PiperLogger.LogError($"[NativeLibraryResolver] Please run 'uPiper/Setup/Run Initial Setup' from the menu to copy required files.");

                    return false;
                }

                PiperLogger.LogDebug($"[NativeLibraryResolver] Native library found at: {libraryPath}");
                return true;
#else
                // In built application, try to call a simple function to check if library is loaded
                // For Android and iOS, the library is always available at runtime (statically linked or included in APK/IPA)
                if (PlatformHelper.IsAndroid || PlatformHelper.IsIOS)
                {
                    PiperLogger.LogDebug($"[NativeLibraryResolver] Native library is statically linked on {(PlatformHelper.IsAndroid ? "Android" : "iOS")}");
                    return true;
                }

                // For other platforms, we assume the library is available if we got this far
                return true;
#endif
            }
            catch (Exception ex)
            {
                PiperLogger.LogError($"[NativeLibraryResolver] Error checking library availability: {ex.Message}");
                return false;
            }
        }

        #endregion
    }
}

#endif
