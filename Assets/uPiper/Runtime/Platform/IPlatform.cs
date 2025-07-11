using System;

namespace uPiper.Platform
{
    /// <summary>
    /// Platform abstraction interface
    /// </summary>
    public interface IPlatform
    {
        /// <summary>
        /// Gets the current platform type
        /// </summary>
        PlatformType Type { get; }

        /// <summary>
        /// Gets whether native phonemization is supported
        /// </summary>
        bool SupportsNativePhonemization { get; }

        /// <summary>
        /// Gets the native library path for the current platform
        /// </summary>
        string GetNativeLibraryPath(string libraryName);

        /// <summary>
        /// Initializes platform-specific resources
        /// </summary>
        void Initialize();

        /// <summary>
        /// Cleans up platform-specific resources
        /// </summary>
        void Cleanup();
    }

    /// <summary>
    /// Supported platform types
    /// </summary>
    public enum PlatformType
    {
        Windows,
        Linux,
        macOS,
        Android,
        iOS,
        WebGL,
        Unknown
    }
}