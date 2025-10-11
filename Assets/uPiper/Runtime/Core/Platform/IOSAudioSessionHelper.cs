#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using UnityEngine;
using uPiper.Core.Logging;

namespace uPiper.Core.Platform
{
    /// <summary>
    /// Helper class for managing iOS AVAudioSession.
    /// On iOS, apps must configure AVAudioSession to enable audio playback.
    /// This class provides a simple interface to:
    /// - Initialize AudioSession with Playback category (overrides silent switch)
    /// - Check AudioSession status
    /// - Get current volume and category
    /// </summary>
    public static class IOSAudioSessionHelper
    {
        // P/Invoke declarations for native functions
        [DllImport("__Internal")]
        private static extern void InitializeAudioSessionForPlayback();

        [DllImport("__Internal")]
        private static extern bool IsAudioSessionActive();

        [DllImport("__Internal")]
        private static extern IntPtr GetAudioSessionCategory();

        [DllImport("__Internal")]
        private static extern float GetOutputVolume();

        [DllImport("__Internal")]
        private static extern void DeactivateAudioSession();

        private static bool _isInitialized = false;

        /// <summary>
        /// Initialize AVAudioSession for audio playback.
        /// This should be called once at app startup or before first audio playback.
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized)
            {
                PiperLogger.LogDebug("[IOSAudioSession] Already initialized, skipping");
                return;
            }

            try
            {
                // Log current state before initialization
                var currentCategory = GetCategoryName();
                var currentVolume = GetOutputVolume();
                PiperLogger.LogInfo($"[IOSAudioSession] Current state - Category: {currentCategory}, Volume: {currentVolume:F2}");

                // Initialize AudioSession with Playback category
                InitializeAudioSessionForPlayback();

                // Log new state after initialization
                var newCategory = GetCategoryName();
                var isActive = IsAudioSessionActive();
                PiperLogger.LogInfo($"[IOSAudioSession] Initialized - Category: {newCategory}, Active: {isActive}");

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                PiperLogger.LogError($"[IOSAudioSession] Failed to initialize: {ex.Message}");
                PiperLogger.LogError($"[IOSAudioSession] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Ensure AudioSession is active before playing audio.
        /// Call this right before AudioSource.Play() if needed.
        /// </summary>
        public static void EnsureActive()
        {
            if (!_isInitialized)
            {
                PiperLogger.LogWarning("[IOSAudioSession] Not initialized, initializing now...");
                Initialize();
                return;
            }

            try
            {
                if (!IsAudioSessionActive())
                {
                    PiperLogger.LogWarning("[IOSAudioSession] Session not active (other audio playing?), reinitializing...");
                    _isInitialized = false;
                    Initialize();
                }
            }
            catch (Exception ex)
            {
                PiperLogger.LogError($"[IOSAudioSession] Failed to ensure active: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the current AudioSession category name.
        /// Useful for debugging audio issues.
        /// </summary>
        public static string GetCategoryName()
        {
            try
            {
                IntPtr ptr = GetAudioSessionCategory();
                if (ptr == IntPtr.Zero)
                {
                    return "Unknown";
                }

                string category = Marshal.PtrToStringAnsi(ptr);
                // Free the memory allocated by strdup in native code
                Marshal.FreeHGlobal(ptr);
                return category ?? "Unknown";
            }
            catch (Exception ex)
            {
                PiperLogger.LogError($"[IOSAudioSession] Failed to get category: {ex.Message}");
                return "Error";
            }
        }

        /// <summary>
        /// Get the current hardware output volume (0.0 to 1.0).
        /// This reflects the volume buttons, not the AudioSource volume.
        /// </summary>
        public static float GetVolume()
        {
            try
            {
                return GetOutputVolume();
            }
            catch (Exception ex)
            {
                PiperLogger.LogError($"[IOSAudioSession] Failed to get volume: {ex.Message}");
                return 0.0f;
            }
        }

        /// <summary>
        /// Deactivate AudioSession when done with audio.
        /// This allows other apps to resume their audio.
        /// Note: Unity typically manages this automatically.
        /// </summary>
        public static void Deactivate()
        {
            try
            {
                DeactivateAudioSession();
                _isInitialized = false;
                PiperLogger.LogInfo("[IOSAudioSession] Deactivated");
            }
            catch (Exception ex)
            {
                PiperLogger.LogError($"[IOSAudioSession] Failed to deactivate: {ex.Message}");
            }
        }

        /// <summary>
        /// Log detailed AudioSession information for debugging.
        /// </summary>
        public static void LogStatus()
        {
            try
            {
                var category = GetCategoryName();
                var volume = GetVolume();
                var isActive = IsAudioSessionActive();

                PiperLogger.LogInfo("[IOSAudioSession] === Status ===");
                PiperLogger.LogInfo($"  Initialized: {_isInitialized}");
                PiperLogger.LogInfo($"  Category: {category}");
                PiperLogger.LogInfo($"  Volume: {volume:F2}");
                PiperLogger.LogInfo($"  Active: {isActive}");
            }
            catch (Exception ex)
            {
                PiperLogger.LogError($"[IOSAudioSession] Failed to log status: {ex.Message}");
            }
        }
    }
}
#endif