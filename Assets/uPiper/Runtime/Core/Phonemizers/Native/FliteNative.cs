using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace uPiper.Core.Phonemizers.Native
{
    /// <summary>
    /// P/Invoke bindings for the Flite native library.
    /// Provides access to Flite's Letter-to-Sound (LTS) functionality.
    /// </summary>
    public static class FliteNative
    {

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        private const string LIBRARY_NAME = "flite_unity";
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        private const string LIBRARY_NAME = "flite_unity";
#elif UNITY_ANDROID
        private const string LIBRARY_NAME = "flite_unity";
#elif UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
        private const string LIBRARY_NAME = "flite_unity";
#else
        private const string LIBRARY_NAME = "flite_unity";
#endif

        /// <summary>
        /// Initialize Flite and create a context.
        /// </summary>
        /// <returns>Pointer to Flite context, or IntPtr.Zero on failure.</returns>
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr flite_unity_init();

        /// <summary>
        /// Convert text to phonemes using Flite.
        /// </summary>
        /// <param name="context">Flite context from flite_unity_init.</param>
        /// <param name="text">Text to convert to phonemes.</param>
        /// <returns>Space-separated phoneme string. Must be freed with flite_unity_free_string.</returns>
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr flite_unity_text_to_phones(
            IntPtr context,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string text
        );

        /// <summary>
        /// Check if a word exists in the CMU lexicon.
        /// </summary>
        /// <param name="context">Flite context.</param>
        /// <param name="word">Word to check.</param>
        /// <returns>1 if word exists in lexicon, 0 otherwise.</returns>
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int flite_unity_word_in_lexicon(
            IntPtr context,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string word
        );

        /// <summary>
        /// Apply Letter-to-Sound rules to a single word.
        /// </summary>
        /// <param name="context">Flite context.</param>
        /// <param name="word">Word to apply LTS rules to.</param>
        /// <returns>Space-separated phoneme string. Must be freed with flite_unity_free_string.</returns>
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr flite_unity_lts_apply(
            IntPtr context,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string word
        );

        /// <summary>
        /// Free a string allocated by Flite.
        /// </summary>
        /// <param name="str">String pointer to free.</param>
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void flite_unity_free_string(IntPtr str);

        /// <summary>
        /// Get Flite version string.
        /// </summary>
        /// <returns>Version string (does not need to be freed).</returns>
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr flite_unity_get_version();

        /// <summary>
        /// Clean up Flite context and free resources.
        /// </summary>
        /// <param name="context">Flite context to clean up.</param>
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void flite_unity_cleanup(IntPtr context);

        /// <summary>
        /// Helper method to safely get string from native pointer.
        /// </summary>
        public static string GetString(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
                return null;

            try
            {
                return Marshal.PtrToStringUTF8(ptr);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to marshal string from native: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Helper method to safely get and free string from native.
        /// </summary>
        public static string GetAndFreeString(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
                return null;

            try
            {
                var result = Marshal.PtrToStringUTF8(ptr);
                flite_unity_free_string(ptr);
                return result;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to marshal string from native: {e.Message}");
                // Still try to free the native memory
                try { flite_unity_free_string(ptr); } catch { }
                return null;
            }
        }

        /// <summary>
        /// Check if Flite native library is available.
        /// </summary>
        public static bool IsAvailable()
        {
            try
            {
                var versionPtr = flite_unity_get_version();
                return versionPtr != IntPtr.Zero;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get Flite version as managed string.
        /// </summary>
        public static string GetVersion()
        {
            try
            {
                var versionPtr = flite_unity_get_version();
                return GetString(versionPtr);
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}