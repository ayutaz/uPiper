#if !UNITY_WEBGL

// Define a constant to control P/Invoke usage
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX || UNITY_ANDROID || UNITY_IOS
#define ENABLE_PINVOKE
#endif

using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Scripting;

namespace uPiper.Core.Phonemizers.Native
{
    /// <summary>
    /// P/Invoke bindings for the OpenJTalk native library.
    /// Provides access to Japanese text-to-phoneme conversion functionality.
    /// </summary>
    [Preserve]
    public static class OpenJTalkNativeInterop
    {
        #region Library Names

#if UNITY_IOS && !UNITY_EDITOR
        private const string LIBRARY_NAME = "__Internal";
#else
        private const string LIBRARY_NAME = "openjtalk_wrapper";
#endif

        #endregion

        #region Native Structures

        /// <summary>
        /// Native structure for phoneme results.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        [Preserve]
        public struct NativePhonemeResult
        {
            public IntPtr phonemes;      // char* - space-separated phoneme string (UTF-8)
            public IntPtr phoneme_ids;   // int*
            public int phoneme_count;
            public IntPtr durations;     // float*
            public float total_duration;
        }

        /// <summary>
        /// Native structure for phoneme results with prosody features (A1/A2/A3).
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        [Preserve]
        public struct NativeProsodyPhonemeResult
        {
            public IntPtr phonemes;      // char* - space-separated phoneme string (UTF-8)
            public IntPtr prosody_a1;    // int* - A1: relative position from accent nucleus
            public IntPtr prosody_a2;    // int* - A2: position in accent phrase (1-based)
            public IntPtr prosody_a3;    // int* - A3: total morae in accent phrase
            public int phoneme_count;
        }

        #endregion

        #region Windows DLL Loading

#if ENABLE_PINVOKE && UNITY_EDITOR_WIN
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetDllDirectory(string lpPathName);
#else
        public static bool SetDllDirectory(string lpPathName) => true;
#endif

        #endregion

        #region P/Invoke Declarations

#if ENABLE_PINVOKE
        /// <summary>
        /// Create an OpenJTalk handle with the specified dictionary path.
        /// </summary>
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern IntPtr openjtalk_create([MarshalAs(UnmanagedType.LPStr)] string dict_path);

        /// <summary>
        /// Destroy an OpenJTalk handle and free resources.
        /// </summary>
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void openjtalk_destroy(IntPtr handle);

        /// <summary>
        /// Phonemize text using OpenJTalk.
        /// </summary>
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr openjtalk_phonemize(IntPtr handle, string text);

        /// <summary>
        /// Free a phoneme result allocated by openjtalk_phonemize.
        /// </summary>
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void openjtalk_free_result(IntPtr result);

        /// <summary>
        /// Phonemize text with prosody (A1/A2/A3) extraction.
        /// </summary>
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr openjtalk_phonemize_with_prosody(IntPtr handle, string text);

        /// <summary>
        /// Free a prosody phoneme result.
        /// </summary>
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void openjtalk_free_prosody_result(IntPtr result);

        /// <summary>
        /// Get the OpenJTalk version string.
        /// </summary>
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr openjtalk_get_version();

        /// <summary>
        /// Get the last error code from an OpenJTalk handle.
        /// </summary>
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int openjtalk_get_last_error(IntPtr handle);

        /// <summary>
        /// Get the error string for an error code.
        /// </summary>
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr openjtalk_get_error_string(int error_code);

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Initialize OpenJTalk with UTF-8 encoded dictionary path (Android specific).
        /// </summary>
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr openjtalk_initialize_utf8(byte[] dictPath, int dictPathLength);

        /// <summary>
        /// Analyze text with UTF-8 encoding (Android specific).
        /// </summary>
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr openjtalk_analyze_utf8(IntPtr handle, byte[] text, int textLength);

        /// <summary>
        /// Free a string allocated by UTF-8 functions (Android specific).
        /// </summary>
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void openjtalk_free_string(IntPtr result);
#endif

#else
        // Stub implementations for unsupported platforms
        public static IntPtr openjtalk_create(string dict_path)
            => throw new NotSupportedException("OpenJTalk is not supported on this platform. P/Invoke is not available.");
        public static void openjtalk_destroy(IntPtr handle)
            => throw new NotSupportedException("OpenJTalk is not supported on this platform. P/Invoke is not available.");
        public static IntPtr openjtalk_phonemize(IntPtr handle, string text)
            => throw new NotSupportedException("OpenJTalk is not supported on this platform. P/Invoke is not available.");
        public static void openjtalk_free_result(IntPtr result)
            => throw new NotSupportedException("OpenJTalk is not supported on this platform. P/Invoke is not available.");
        public static IntPtr openjtalk_phonemize_with_prosody(IntPtr handle, string text)
            => throw new NotSupportedException("OpenJTalk is not supported on this platform. P/Invoke is not available.");
        public static void openjtalk_free_prosody_result(IntPtr result)
            => throw new NotSupportedException("OpenJTalk is not supported on this platform. P/Invoke is not available.");
        public static IntPtr openjtalk_get_version()
            => throw new NotSupportedException("OpenJTalk is not supported on this platform. P/Invoke is not available.");
        public static int openjtalk_get_last_error(IntPtr handle)
            => throw new NotSupportedException("OpenJTalk is not supported on this platform. P/Invoke is not available.");
        public static IntPtr openjtalk_get_error_string(int error_code)
            => throw new NotSupportedException("OpenJTalk is not supported on this platform. P/Invoke is not available.");
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
                return Marshal.PtrToStringUTF8(ptr);
            }
            catch (Exception e)
            {
                Debug.LogError($"[OpenJTalkNativeInterop] Failed to marshal string from native: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Helper method to safely get ANSI string from native pointer.
        /// </summary>
        public static string GetAnsiString(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
                return null;

            try
            {
                return Marshal.PtrToStringAnsi(ptr);
            }
            catch (Exception e)
            {
                Debug.LogError($"[OpenJTalkNativeInterop] Failed to marshal ANSI string from native: {e.Message}");
                return null;
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
                return GetAnsiString(versionPtr) ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// Safely marshal int array from native pointer.
        /// </summary>
        public static int[] MarshalIntArray(IntPtr ptr, int count)
        {
            if (ptr == IntPtr.Zero || count <= 0)
                return Array.Empty<int>();

            var result = new int[count];
            Marshal.Copy(ptr, result, 0, count);
            return result;
        }

        /// <summary>
        /// Safely marshal float array from native pointer.
        /// </summary>
        public static float[] MarshalFloatArray(IntPtr ptr, int count)
        {
            if (ptr == IntPtr.Zero || count <= 0)
                return Array.Empty<float>();

            var result = new float[count];
            Marshal.Copy(ptr, result, 0, count);
            return result;
        }

        #endregion
    }
}

#endif
