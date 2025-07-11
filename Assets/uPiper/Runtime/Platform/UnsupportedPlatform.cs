using UnityEngine;

namespace uPiper.Platform
{
    /// <summary>
    /// Placeholder for unsupported platforms
    /// </summary>
    public class UnsupportedPlatform : IPlatform
    {
        public PlatformType Type => PlatformType.Unknown;
        public bool SupportsNativePhonemization => false;

        public string GetNativeLibraryPath(string libraryName)
        {
            Debug.LogWarning("[uPiper] Native libraries not supported on this platform");
            return null;
        }

        public void Initialize()
        {
            Debug.LogWarning("[uPiper] Running on unsupported platform");
        }

        public void Cleanup()
        {
            // Nothing to cleanup
        }
    }

    // Temporary placeholders for other platforms
    public class MacOSPlatform : UnsupportedPlatform
    {
        public new PlatformType Type => PlatformType.macOS;
    }

    public class AndroidPlatform : UnsupportedPlatform
    {
        public new PlatformType Type => PlatformType.Android;
    }

    public class IOSPlatform : UnsupportedPlatform
    {
        public new PlatformType Type => PlatformType.iOS;
    }

    public class WebGLPlatform : UnsupportedPlatform
    {
        public new PlatformType Type => PlatformType.WebGL;
    }
}