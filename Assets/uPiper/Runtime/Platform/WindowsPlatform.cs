using System.IO;
using UnityEngine;

namespace uPiper.Platform
{
    /// <summary>
    /// Windows platform implementation
    /// </summary>
    public class WindowsPlatform : IPlatform
    {
        public PlatformType Type => PlatformType.Windows;
        public bool SupportsNativePhonemization => true;

        public string GetNativeLibraryPath(string libraryName)
        {
            // Windows uses .dll extension
            var dllName = $"{libraryName}.dll";
            
            // Check StreamingAssets first
            var streamingAssetsPath = Path.Combine(Application.streamingAssetsPath, "uPiper", "Windows", dllName);
            if (File.Exists(streamingAssetsPath))
            {
                return streamingAssetsPath;
            }

            // Check plugin folder
            var pluginPath = Path.Combine(Application.dataPath, "uPiper", "Runtime", "Plugins", "Windows", "x86_64", dllName);
            if (File.Exists(pluginPath))
            {
                return pluginPath;
            }

            Debug.LogError($"[uPiper] Native library not found: {dllName}");
            return null;
        }

        public void Initialize()
        {
            Debug.Log("[uPiper] Windows platform initialized");
        }

        public void Cleanup()
        {
            // No specific cleanup needed for Windows
        }
    }
}