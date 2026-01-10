#if !UNITY_WEBGL

using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace uPiper.Core.Phonemizers.Native
{
    /// <summary>
    /// P/Invoke bindings for the OpenJTalk native library.
    /// Centralizes all native function declarations for Japanese phonemization.
    /// </summary>
    public static class OpenJTalkNative
    {
        #region Library Name

#if UNITY_IOS && !UNITY_EDITOR
        private const string LIBRARY_NAME = "__Internal";
#else
        private const string LIBRARY_NAME = "openjtalk_wrapper";
#endif

        #endregion

        #region Native Structures

        /// <summary>
        /// Native structure for phoneme results
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct NativePhonemeResult
        {
            public IntPtr phonemes;      // char* - space-separated phoneme string (UTF-8)
            public IntPtr phoneme_ids;   // int*
            public int phoneme_count;
            public IntPtr durations;     // float*
            public float total_duration;
        }

        /// <summary>
        /// Native structure for phoneme results with prosody features (A1/A2/A3)
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct NativeProsodyPhonemeResult
        {
            public IntPtr phonemes;      // char* - space-separated phoneme string (UTF-8)
            public IntPtr prosody_a1;    // int* - A1: relative position from accent nucleus
            public IntPtr prosody_a2;    // int* - A2: position in accent phrase (1-based)
            public IntPtr prosody_a3;    // int* - A3: total morae in accent phrase
            public int phoneme_count;
        }

        #endregion

        #region P/Invoke Declarations

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX || UNITY_ANDROID || UNITY_IOS

#if UNITY_EDITOR_WIN
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetDllDirectory(string lpPathName);
#endif

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr openjtalk_create([MarshalAs(UnmanagedType.LPStr)] string dict_path);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void openjtalk_destroy(IntPtr handle);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr openjtalk_phonemize(IntPtr handle, string text);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void openjtalk_free_result(IntPtr result);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr openjtalk_phonemize_with_prosody(IntPtr handle, string text);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void openjtalk_free_prosody_result(IntPtr result);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr openjtalk_get_version();

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int openjtalk_get_last_error(IntPtr handle);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr openjtalk_get_error_string(int error_code);

#if UNITY_ANDROID && !UNITY_EDITOR
        // UTF-8 optimized versions for Android
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr openjtalk_initialize_utf8(byte[] dictPath, int dictPathLength);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr openjtalk_analyze_utf8(IntPtr handle, byte[] text, int textLength);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void openjtalk_free_string(IntPtr result);
#endif

#else
        // Stub implementations for unsupported platforms
        public static bool SetDllDirectory(string lpPathName)
            => throw new NotSupportedException("OpenJTalk is not supported on this platform.");
        public static IntPtr openjtalk_create(string dict_path)
            => throw new NotSupportedException("OpenJTalk is not supported on this platform.");
        public static void openjtalk_destroy(IntPtr handle)
            => throw new NotSupportedException("OpenJTalk is not supported on this platform.");
        public static IntPtr openjtalk_phonemize(IntPtr handle, string text)
            => throw new NotSupportedException("OpenJTalk is not supported on this platform.");
        public static void openjtalk_free_result(IntPtr result)
            => throw new NotSupportedException("OpenJTalk is not supported on this platform.");
        public static IntPtr openjtalk_phonemize_with_prosody(IntPtr handle, string text)
            => throw new NotSupportedException("OpenJTalk is not supported on this platform.");
        public static void openjtalk_free_prosody_result(IntPtr result)
            => throw new NotSupportedException("OpenJTalk is not supported on this platform.");
        public static IntPtr openjtalk_get_version()
            => throw new NotSupportedException("OpenJTalk is not supported on this platform.");
        public static int openjtalk_get_last_error(IntPtr handle)
            => throw new NotSupportedException("OpenJTalk is not supported on this platform.");
        public static IntPtr openjtalk_get_error_string(int error_code)
            => throw new NotSupportedException("OpenJTalk is not supported on this platform.");
#endif

        #endregion

        #region Helper Methods

        /// <summary>
        /// Helper method to safely get string from native pointer.
        /// </summary>
        public static string GetString(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
                return null;

            try
            {
                return Marshal.PtrToStringAnsi(ptr);
            }
            catch (Exception e)
            {
                Debug.LogError($"[OpenJTalkNative] Failed to marshal string from native: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get error message for the given error code.
        /// </summary>
        public static string GetErrorMessage(int errorCode)
        {
            try
            {
                var errorMsgPtr = openjtalk_get_error_string(errorCode);
                return errorMsgPtr != IntPtr.Zero
                    ? Marshal.PtrToStringAnsi(errorMsgPtr)
                    : "Unknown error";
            }
            catch
            {
                return "Unknown error";
            }
        }

        /// <summary>
        /// Check if OpenJTalk native library is available.
        /// </summary>
        public static bool IsAvailable()
        {
            try
            {
                var versionPtr = openjtalk_get_version();
                return versionPtr != IntPtr.Zero;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get OpenJTalk version as managed string.
        /// </summary>
        public static string GetVersion()
        {
            try
            {
                var versionPtr = openjtalk_get_version();
                return GetString(versionPtr) ?? "1.0.0";
            }
            catch
            {
                return "1.0.0";
            }
        }

        #endregion
    }
}

#endif
