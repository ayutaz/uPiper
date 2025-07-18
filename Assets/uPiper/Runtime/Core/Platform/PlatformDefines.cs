namespace uPiper.Core.Platform
{
    /// <summary>
    /// Platform-specific compile-time constants and feature flags.
    /// </summary>
    public static class PlatformDefines
    {
        /// <summary>
        /// Indicates whether native OpenJTalk library is supported on the current platform.
        /// </summary>
#if UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS
        public const bool OPENJTALK_NATIVE_SUPPORTED = false;
#else
        public const bool OPENJTALK_NATIVE_SUPPORTED = true;
#endif

        /// <summary>
        /// Indicates whether the platform supports native P/Invoke calls.
        /// </summary>
#if UNITY_WEBGL
        public const bool SUPPORTS_NATIVE_PLUGINS = false;
#else
        public const bool SUPPORTS_NATIVE_PLUGINS = true;
#endif

        /// <summary>
        /// Indicates whether the platform is mobile.
        /// </summary>
#if UNITY_ANDROID || UNITY_IOS
        public const bool IS_MOBILE_PLATFORM = true;
#else
        public const bool IS_MOBILE_PLATFORM = false;
#endif

        /// <summary>
        /// Indicates whether the platform is WebGL.
        /// </summary>
#if UNITY_WEBGL
        public const bool IS_WEBGL_PLATFORM = true;
#else
        public const bool IS_WEBGL_PLATFORM = false;
#endif
    }
}