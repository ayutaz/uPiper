using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace uPiper.OpenJTalk
{
    /// <summary>
    /// Simple test script for OpenJTalk native library
    /// </summary>
    public class OpenJTalkTest : MonoBehaviour
    {
        // P/Invoke declarations
        [DllImport("openjtalk_wrapper")]
        private static extern IntPtr openjtalk_create(string dict_path);
        
        [DllImport("openjtalk_wrapper")]
        private static extern void openjtalk_destroy(IntPtr handle);
        
        [DllImport("openjtalk_wrapper")]
        private static extern IntPtr openjtalk_phonemize(IntPtr handle, string text);
        
        [DllImport("openjtalk_wrapper")]
        private static extern void openjtalk_free_result(IntPtr result);
        
        [DllImport("openjtalk_wrapper")]
        private static extern string openjtalk_get_version();
        
        // PhonemeResult structure (simplified)
        [StructLayout(LayoutKind.Sequential)]
        private struct PhonemeResult
        {
            public IntPtr phonemes;
            public IntPtr phoneme_ids;
            public int phoneme_count;
            public IntPtr durations;
            public float total_duration;
        }
        
        private IntPtr openjtalkHandle = IntPtr.Zero;
        
        void Start()
        {
            TestOpenJTalk();
        }
        
        void TestOpenJTalk()
        {
            try
            {
                // Get version
                string version = openjtalk_get_version();
                Debug.Log($"OpenJTalk Version: {version}");
                
                // Create instance
                openjtalkHandle = openjtalk_create(null);
                if (openjtalkHandle == IntPtr.Zero)
                {
                    Debug.LogError("Failed to create OpenJTalk instance");
                    return;
                }
                
                Debug.Log("OpenJTalk instance created successfully");
                
                // Test texts
                string[] testTexts = {
                    "こんにちは",
                    "今日は良い天気です",
                    "日本語のテスト",
                    "ユニティで音声合成"
                };
                
                foreach (string text in testTexts)
                {
                    TestPhonemization(text);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"OpenJTalk test failed: {e.Message}");
            }
        }
        
        void TestPhonemization(string text)
        {
            IntPtr resultPtr = openjtalk_phonemize(openjtalkHandle, text);
            if (resultPtr == IntPtr.Zero)
            {
                Debug.LogError($"Failed to phonemize: {text}");
                return;
            }
            
            // Marshal the result
            PhonemeResult result = Marshal.PtrToStructure<PhonemeResult>(resultPtr);
            
            // Get phoneme string
            string phonemes = Marshal.PtrToStringAnsi(result.phonemes);
            
            // Get phoneme IDs
            int[] phonemeIds = new int[result.phoneme_count];
            Marshal.Copy(result.phoneme_ids, phonemeIds, 0, result.phoneme_count);
            
            // Log results
            Debug.Log($"Text: '{text}'");
            Debug.Log($"  Phoneme count: {result.phoneme_count}");
            Debug.Log($"  Phonemes: {phonemes}");
            Debug.Log($"  Total duration: {result.total_duration:F2} seconds");
            Debug.Log($"  IDs: {string.Join(", ", phonemeIds)}");
            
            // Free the result
            openjtalk_free_result(resultPtr);
        }
        
        void OnDestroy()
        {
            if (openjtalkHandle != IntPtr.Zero)
            {
                openjtalk_destroy(openjtalkHandle);
                openjtalkHandle = IntPtr.Zero;
                Debug.Log("OpenJTalk instance destroyed");
            }
        }
    }
}