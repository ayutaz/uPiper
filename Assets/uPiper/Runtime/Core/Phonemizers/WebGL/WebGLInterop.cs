#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace uPiper.Core.Phonemizers.WebGL
{
    /// <summary>
    /// WebGL JavaScript interop for phonemization
    /// </summary>
    internal static class WebGLInterop
    {
        // OpenJTalk WebAssembly functions
        [DllImport("__Internal")]
        public static extern int InitializeOpenJTalkWeb();
        
        [DllImport("__Internal")]
        public static extern bool IsOpenJTalkInitialized();
        
        [DllImport("__Internal")]
        public static extern bool IsOpenJTalkModuleLoaded();

        [DllImport("__Internal")]
        public static extern bool LoadOpenJTalkDictionary(byte[] dictionaryData, int dataLength);

        [DllImport("__Internal")]
        public static extern IntPtr PhonemizeJapaneseText(string text);

        // eSpeak-ng WebAssembly functions
        [DllImport("__Internal")]
        public static extern int InitializeESpeakWeb();
        
        [DllImport("__Internal")]
        public static extern bool IsESpeakInitialized();

        [DllImport("__Internal")]
        public static extern bool SetESpeakLanguage(string language);

        [DllImport("__Internal")]
        public static extern IntPtr PhonemizeEnglishText(string text, string language);

        [DllImport("__Internal")]
        public static extern IntPtr GetESpeakSupportedLanguages();

        // Memory management
        [DllImport("__Internal")]
        public static extern void FreeWebGLMemory(IntPtr ptr);

        /// <summary>
        /// Helper method to convert JavaScript result to managed object
        /// </summary>
        public static T ParseJSONResult<T>(IntPtr resultPtr)
        {
            if (resultPtr == IntPtr.Zero)
            {
                throw new InvalidOperationException("Received null pointer from JavaScript");
            }

            try
            {
                string jsonResult = Marshal.PtrToStringUTF8(resultPtr);
                return JsonUtility.FromJson<T>(jsonResult);
            }
            finally
            {
                // Always free the allocated memory
                FreeWebGLMemory(resultPtr);
            }
        }

        /// <summary>
        /// Result structure for phonemization
        /// </summary>
        [Serializable]
        public class PhonemeResult
        {
            public bool success;
            public string error;
            public string[] phonemes;
        }

        /// <summary>
        /// Initialize all WebGL phonemization systems
        /// </summary>
        public static bool InitializeAll()
        {
            bool openJTalkSuccess = false;
            bool espeakSuccess = false;

            try
            {
                Debug.Log("[WebGLInterop] Initializing OpenJTalk WebAssembly...");
                openJTalkSuccess = InitializeOpenJTalkWeb() != 0;
                if (!openJTalkSuccess)
                {
                    Debug.LogWarning("[WebGLInterop] OpenJTalk initialization failed");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebGLInterop] OpenJTalk initialization error: {e.Message}");
            }

            try
            {
                Debug.Log("[WebGLInterop] Initializing eSpeak-ng WebAssembly...");
                espeakSuccess = InitializeESpeakWeb() != 0;
                if (!espeakSuccess)
                {
                    Debug.LogWarning("[WebGLInterop] eSpeak-ng initialization failed");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[WebGLInterop] eSpeak-ng initialization error: {e.Message}");
            }

            return openJTalkSuccess || espeakSuccess;
        }
    }
}
#endif