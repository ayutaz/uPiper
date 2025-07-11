using System.IO;
using UnityEngine;

namespace uPiper.Platform
{
    /// <summary>
    /// Linux platform implementation
    /// </summary>
    public class LinuxPlatform : IPlatform
    {
        public PlatformType Type => PlatformType.Linux;
        public bool SupportsNativePhonemization => true;

        public string GetNativeLibraryPath(string libraryName)
        {
            // Linux uses .so extension
            var soName = $"lib{libraryName}.so";
            
            // Check StreamingAssets first
            var streamingAssetsPath = Path.Combine(Application.streamingAssetsPath, "uPiper", "Linux", soName);
            if (File.Exists(streamingAssetsPath))
            {
                return streamingAssetsPath;
            }

            // Check plugin folder
            var pluginPath = Path.Combine(Application.dataPath, "uPiper", "Runtime", "Plugins", "Linux", "x86_64", soName);
            if (File.Exists(pluginPath))
            {
                return pluginPath;
            }

            Debug.LogError($"[uPiper] Native library not found: {soName}");
            return null;
        }

        public void Initialize()
        {
            Debug.Log("[uPiper] Linux platform initialized");
        }

        public void Cleanup()
        {
            // No specific cleanup needed for Linux
        }
    }
}