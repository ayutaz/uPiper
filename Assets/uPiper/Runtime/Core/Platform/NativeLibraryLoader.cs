using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using uPiper.Core.Logging;

namespace uPiper.Core.Platform
{
    /// <summary>
    /// Handles dynamic loading of native libraries across platforms
    /// </summary>
    public static class NativeLibraryLoader
    {
        #region Platform-specific imports

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        [DllImport("kernel32", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32")]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll")]
        private static extern uint GetLastError();
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
        [DllImport("__Internal")]
        private static extern IntPtr dlopen(string filename, int flags);

        [DllImport("__Internal")]
        private static extern int dlclose(IntPtr handle);

        [DllImport("__Internal")]
        private static extern IntPtr dlsym(IntPtr handle, string symbol);

        [DllImport("__Internal")]
        private static extern IntPtr dlerror();

        private const int RTLD_NOW = 2;
        private const int RTLD_GLOBAL = 8;
#endif

        #endregion

        #region Fields

        private static readonly Dictionary<string, IntPtr> _loadedLibraries = new Dictionary<string, IntPtr>();
        private static readonly object _lockObject = new object();

        #endregion

        #region Public Methods

        /// <summary>
        /// Load a native library
        /// </summary>
        /// <param name="libraryName">Library name without extension</param>
        /// <param name="searchPaths">Optional additional search paths</param>
        /// <returns>Handle to the loaded library</returns>
        public static IntPtr LoadLibrary(string libraryName, params string[] searchPaths)
        {
            if (string.IsNullOrEmpty(libraryName))
                throw new ArgumentNullException(nameof(libraryName));

            lock (_lockObject)
            {
                // Check if already loaded
                if (_loadedLibraries.TryGetValue(libraryName, out var existingHandle))
                {
                    PiperLogger.LogDebug("Library already loaded: {0}", libraryName);
                    return existingHandle;
                }

                // Get platform-specific library filename
                var libraryFileName = GetPlatformLibraryName(libraryName);
                
                // Try to load the library
                var handle = TryLoadLibrary(libraryFileName, searchPaths);
                
                if (handle != IntPtr.Zero)
                {
                    _loadedLibraries[libraryName] = handle;
                    PiperLogger.LogInfo("Successfully loaded native library: {0}", libraryName);
                }
                else
                {
                    var error = GetLastErrorMessage();
                    PiperLogger.LogError("Failed to load native library '{0}': {1}", libraryName, error);
                    throw new DllNotFoundException($"Could not load native library '{libraryName}': {error}");
                }

                return handle;
            }
        }

        /// <summary>
        /// Unload a native library
        /// </summary>
        /// <param name="libraryName">Library name</param>
        /// <returns>True if successfully unloaded</returns>
        public static bool UnloadLibrary(string libraryName)
        {
            if (string.IsNullOrEmpty(libraryName))
                return false;

            lock (_lockObject)
            {
                if (!_loadedLibraries.TryGetValue(libraryName, out var handle))
                {
                    PiperLogger.LogWarning("Library not loaded: {0}", libraryName);
                    return false;
                }

                bool result = UnloadLibraryInternal(handle);
                
                if (result)
                {
                    _loadedLibraries.Remove(libraryName);
                    PiperLogger.LogInfo("Successfully unloaded native library: {0}", libraryName);
                }
                else
                {
                    PiperLogger.LogError("Failed to unload native library: {0}", libraryName);
                }

                return result;
            }
        }

        /// <summary>
        /// Get function pointer from loaded library
        /// </summary>
        /// <param name="libraryName">Library name</param>
        /// <param name="functionName">Function name</param>
        /// <returns>Function pointer</returns>
        public static IntPtr GetFunctionPointer(string libraryName, string functionName)
        {
            lock (_lockObject)
            {
                if (!_loadedLibraries.TryGetValue(libraryName, out var handle))
                {
                    throw new InvalidOperationException($"Library '{libraryName}' is not loaded");
                }

                return GetFunctionPointerInternal(handle, functionName);
            }
        }

        /// <summary>
        /// Check if a library is loaded
        /// </summary>
        /// <param name="libraryName">Library name</param>
        /// <returns>True if loaded</returns>
        public static bool IsLibraryLoaded(string libraryName)
        {
            lock (_lockObject)
            {
                return _loadedLibraries.ContainsKey(libraryName);
            }
        }

        /// <summary>
        /// Unload all loaded libraries
        /// </summary>
        public static void UnloadAll()
        {
            lock (_lockObject)
            {
                foreach (var kvp in _loadedLibraries)
                {
                    UnloadLibraryInternal(kvp.Value);
                }
                
                _loadedLibraries.Clear();
                PiperLogger.LogInfo("Unloaded all native libraries");
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Get platform-specific library filename
        /// </summary>
        private static string GetPlatformLibraryName(string libraryName)
        {
            if (PlatformHelper.IsWindows)
            {
                return libraryName + ".dll";
            }
            else if (PlatformHelper.IsMacOS)
            {
                return libraryName.StartsWith("lib") ? libraryName + ".dylib" : "lib" + libraryName + ".dylib";
            }
            else if (PlatformHelper.IsLinux)
            {
                return libraryName.StartsWith("lib") ? libraryName + ".so" : "lib" + libraryName + ".so";
            }
            else
            {
                throw new PlatformNotSupportedException($"Platform {PlatformHelper.Platform} does not support dynamic library loading");
            }
        }

        /// <summary>
        /// Try to load library from various paths
        /// </summary>
        private static IntPtr TryLoadLibrary(string libraryFileName, string[] searchPaths)
        {
            var searchLocations = new List<string>();

            // Add custom search paths
            if (searchPaths != null)
            {
                searchLocations.AddRange(searchPaths);
            }

            // Add default search paths
            searchLocations.Add(PlatformHelper.GetNativeLibraryDirectory());
            searchLocations.Add(Path.Combine(Application.dataPath, "Plugins"));
            searchLocations.Add(Path.Combine(Application.dataPath, "uPiper", "Plugins"));
            searchLocations.Add(Application.streamingAssetsPath);
            searchLocations.Add(Path.Combine(Application.streamingAssetsPath, "uPiper", "Native"));

            // Platform-specific subdirectories
            if (PlatformHelper.IsWindows)
            {
                searchLocations.Add(Path.Combine(Application.dataPath, "Plugins", "x86_64"));
            }
            else if (PlatformHelper.IsMacOS)
            {
                searchLocations.Add(Path.Combine(Application.dataPath, "Plugins", "macOS"));
            }
            else if (PlatformHelper.IsLinux)
            {
                searchLocations.Add(Path.Combine(Application.dataPath, "Plugins", "x86_64"));
            }

            // Try each search location
            foreach (var searchPath in searchLocations)
            {
                if (string.IsNullOrEmpty(searchPath))
                    continue;

                var fullPath = Path.Combine(searchPath, libraryFileName);
                fullPath = Path.GetFullPath(fullPath); // Normalize path

                if (File.Exists(fullPath))
                {
                    PiperLogger.LogDebug("Attempting to load library from: {0}", fullPath);
                    var handle = LoadLibraryInternal(fullPath);
                    
                    if (handle != IntPtr.Zero)
                    {
                        PiperLogger.LogDebug("Successfully loaded from: {0}", fullPath);
                        return handle;
                    }
                    else
                    {
                        PiperLogger.LogWarning("Failed to load from existing file: {0}", fullPath);
                    }
                }
            }

            // Try loading without path (system library paths)
            PiperLogger.LogDebug("Attempting to load library from system paths: {0}", libraryFileName);
            return LoadLibraryInternal(libraryFileName);
        }

        /// <summary>
        /// Platform-specific library loading
        /// </summary>
        private static IntPtr LoadLibraryInternal(string path)
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            return LoadLibrary(path);
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
            return dlopen(path, RTLD_NOW | RTLD_GLOBAL);
#else
            throw new PlatformNotSupportedException("Dynamic library loading not supported on this platform");
#endif
        }

        /// <summary>
        /// Platform-specific library unloading
        /// </summary>
        private static bool UnloadLibraryInternal(IntPtr handle)
        {
            if (handle == IntPtr.Zero)
                return false;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            return FreeLibrary(handle);
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
            return dlclose(handle) == 0;
#else
            return false;
#endif
        }

        /// <summary>
        /// Platform-specific function pointer retrieval
        /// </summary>
        private static IntPtr GetFunctionPointerInternal(IntPtr handle, string functionName)
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            return GetProcAddress(handle, functionName);
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
            return dlsym(handle, functionName);
#else
            return IntPtr.Zero;
#endif
        }

        /// <summary>
        /// Get last error message
        /// </summary>
        private static string GetLastErrorMessage()
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            uint error = GetLastError();
            return $"Windows error code: {error}";
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
            IntPtr errorPtr = dlerror();
            if (errorPtr != IntPtr.Zero)
            {
                return Marshal.PtrToStringAnsi(errorPtr);
            }
            return "Unknown error";
#else
            return "Platform not supported";
#endif
        }

        #endregion
    }
}