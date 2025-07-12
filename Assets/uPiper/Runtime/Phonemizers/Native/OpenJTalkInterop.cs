using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace uPiper.Phonemizers.Native
{
    /// <summary>
    /// P/Invoke wrapper for OpenJTalk native library
    /// </summary>
    public static class OpenJTalkInterop
    {
        private const string DLL_NAME = "openjtalk_wrapper";

        // Handle type
        public struct OpenJTalkHandle
        {
            public IntPtr ptr;
            
            public bool IsValid => ptr != IntPtr.Zero;
            
            public static OpenJTalkHandle Invalid => new OpenJTalkHandle { ptr = IntPtr.Zero };
        }

        // Native function imports
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern OpenJTalkHandle openjtalk_create();

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void openjtalk_destroy(OpenJTalkHandle handle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int openjtalk_is_available();

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int openjtalk_ensure_dictionary();

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int openjtalk_text_to_phonemes(
            OpenJTalkHandle handle,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string text,
            out IntPtr phonemes_out,
            out int phonemes_len
        );

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void openjtalk_free_phonemes(IntPtr phonemes);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr openjtalk_get_last_error();

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr openjtalk_get_version();

        // High-level wrapper methods
        public static OpenJTalkHandle Create()
        {
            try
            {
                return openjtalk_create();
            }
            catch (DllNotFoundException)
            {
                Debug.LogError($"[uPiper] OpenJTalk native library '{DLL_NAME}' not found. " +
                    "Please ensure the native library is in the Plugins folder.");
                return OpenJTalkHandle.Invalid;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[uPiper] Failed to create OpenJTalk instance: {ex.Message}");
                return OpenJTalkHandle.Invalid;
            }
        }

        public static void Destroy(OpenJTalkHandle handle)
        {
            if (handle.IsValid)
            {
                try
                {
                    openjtalk_destroy(handle);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[uPiper] Failed to destroy OpenJTalk instance: {ex.Message}");
                }
            }
        }

        public static bool IsAvailable()
        {
            try
            {
                return openjtalk_is_available() != 0;
            }
            catch (DllNotFoundException)
            {
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[uPiper] Error checking OpenJTalk availability: {ex.Message}");
                return false;
            }
        }

        public static bool EnsureDictionary()
        {
            try
            {
                return openjtalk_ensure_dictionary() != 0;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[uPiper] Failed to ensure dictionary: {ex.Message}");
                return false;
            }
        }

        public static string TextToPhonemes(OpenJTalkHandle handle, string text)
        {
            if (!handle.IsValid)
            {
                throw new InvalidOperationException("Invalid OpenJTalk handle");
            }

            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            IntPtr phonemesPtr = IntPtr.Zero;
            
            try
            {
                int phonemesLen;
                int result = openjtalk_text_to_phonemes(handle, text, out phonemesPtr, out phonemesLen);
                
                if (result == 0)
                {
                    string error = GetLastError();
                    throw new Exception($"Failed to convert text to phonemes: {error}");
                }

                if (phonemesPtr == IntPtr.Zero || phonemesLen == 0)
                {
                    return string.Empty;
                }

                // Convert the native string to managed string
                byte[] buffer = new byte[phonemesLen];
                Marshal.Copy(phonemesPtr, buffer, 0, phonemesLen);
                return Encoding.UTF8.GetString(buffer);
            }
            finally
            {
                // Always free the allocated memory
                if (phonemesPtr != IntPtr.Zero)
                {
                    openjtalk_free_phonemes(phonemesPtr);
                }
            }
        }

        public static string GetLastError()
        {
            try
            {
                IntPtr errorPtr = openjtalk_get_last_error();
                if (errorPtr == IntPtr.Zero)
                {
                    return "Unknown error";
                }
                return Marshal.PtrToStringAnsi(errorPtr) ?? "Unknown error";
            }
            catch
            {
                return "Failed to get error message";
            }
        }

        public static string GetVersion()
        {
            try
            {
                IntPtr versionPtr = openjtalk_get_version();
                if (versionPtr == IntPtr.Zero)
                {
                    return "Unknown";
                }
                return Marshal.PtrToStringAnsi(versionPtr) ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}