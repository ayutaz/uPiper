using UnityEngine;

namespace uPiper.Platform
{
    /// <summary>
    /// Factory for creating platform-specific implementations
    /// </summary>
    public static class PlatformFactory
    {
        /// <summary>
        /// Creates a platform implementation for the current runtime platform
        /// </summary>
        public static IPlatform CreatePlatform()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor:
                    return new WindowsPlatform();

                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.LinuxEditor:
                    return new LinuxPlatform();

                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.OSXEditor:
                    return new MacOSPlatform();

                case RuntimePlatform.Android:
                    return new AndroidPlatform();

                case RuntimePlatform.IPhonePlayer:
                    return new IOSPlatform();

                case RuntimePlatform.WebGLPlayer:
                    return new WebGLPlatform();

                default:
                    Debug.LogWarning($"[uPiper] Unsupported platform: {Application.platform}");
                    return new UnsupportedPlatform();
            }
        }
    }
}