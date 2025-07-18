#if !UNITY_WEBGL && (UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX)
#define ENABLE_NATIVE_TESTS
#endif

using System;
using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEngine;

namespace uPiper.Tests.Runtime.Native
{
    /// <summary>
    /// Tests for OpenJTalk native library integration
    /// </summary>
    [Category("NativeTests")]
    [Category("RequiresNativeLibrary")]
    public class OpenJTalkNativeTest
    {
        #if ENABLE_NATIVE_TESTS
        // P/Invoke declarations matching openjtalk_wrapper.h
        [DllImport("openjtalk_wrapper", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr openjtalk_create(string dict_path);
        
        [DllImport("openjtalk_wrapper", CallingConvention = CallingConvention.Cdecl)]
        private static extern void openjtalk_destroy(IntPtr handle);
        
        [DllImport("openjtalk_wrapper", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr openjtalk_get_version();
        
        [DllImport("openjtalk_wrapper", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr openjtalk_phonemize(IntPtr handle, string text);
        
        [DllImport("openjtalk_wrapper", CallingConvention = CallingConvention.Cdecl)]
        private static extern void openjtalk_free_result(IntPtr result);
        
        [DllImport("openjtalk_wrapper", CallingConvention = CallingConvention.Cdecl)]
        private static extern int openjtalk_get_last_error(IntPtr handle);
        
        [DllImport("openjtalk_wrapper", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr openjtalk_get_error_string(int error_code);
        
        [DllImport("openjtalk_wrapper", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int openjtalk_set_option(IntPtr handle, string key, string value);
        
        [DllImport("openjtalk_wrapper", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr openjtalk_get_option(IntPtr handle, string key);
        
        // PhonemeResult structure
        [StructLayout(LayoutKind.Sequential)]
        private struct PhonemeResult
        {
            public IntPtr phonemes;      // char*
            public IntPtr phoneme_ids;   // int*
            public int phoneme_count;
            public IntPtr durations;     // float*
            public float total_duration;
        }
        
        private IntPtr handle = IntPtr.Zero;
        
        [SetUp]
        public void SetUp()
        {
            try
            {
                Debug.Log("[OpenJTalkNativeTest] Starting SetUp");
                
                // Log current directory
                Debug.Log($"[OpenJTalkNativeTest] Current directory: {System.IO.Directory.GetCurrentDirectory()}");
                Debug.Log($"[OpenJTalkNativeTest] Application.dataPath: {Application.dataPath}");
                
                // Try to find test dictionary
                string dictPath = GetTestDictionaryPath();
                Debug.Log($"[OpenJTalkNativeTest] Test dictionary path: {dictPath}");
                if (!System.IO.Directory.Exists(dictPath))
                {
                    Assert.Ignore("Test dictionary not found. Run create_test_dict.py first.");
                }
                
                // Check if native library is available
                Debug.Log("[OpenJTalkNativeTest] Checking native library availability...");
                if (!IsNativeLibraryAvailable())
                {
                    Assert.Ignore("Native library not available. Skipping native tests.");
                }
                
                // Verify dictionary files exist
                string[] requiredFiles = { "sys.dic", "unk.dic", "char.bin", "matrix.bin" };
                foreach (string file in requiredFiles)
                {
                    string filePath = System.IO.Path.Combine(dictPath, file);
                    if (!System.IO.File.Exists(filePath))
                    {
                        Debug.LogError($"Required dictionary file not found: {filePath}");
                        Assert.Ignore($"Dictionary file missing: {file}");
                    }
                }
                
                handle = openjtalk_create(dictPath);
                Debug.Log($"OpenJTalk handle created: {handle} (0x{handle.ToInt64():X})");
                if (handle == IntPtr.Zero)
                {
                    // Get error information
                    int errorCode = openjtalk_get_last_error(IntPtr.Zero);
                    IntPtr errorStrPtr = openjtalk_get_error_string(errorCode);
                    string errorStr = errorStrPtr != IntPtr.Zero ? 
                        Marshal.PtrToStringAnsi(errorStrPtr) : "Unknown error";
                    
                    Debug.LogError($"Failed to create OpenJTalk instance. Error: {errorStr} (code: {errorCode})");
                    Assert.Ignore($"Failed to create OpenJTalk instance: {errorStr}");
                }
            }
            catch (DllNotFoundException)
            {
                Assert.Ignore("OpenJTalk native library not found. Skipping native tests.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Setup failed: {ex.Message}");
                Assert.Ignore($"Native test setup failed: {ex.Message}");
            }
        }
        
        [TearDown]
        public void TearDown()
        {
            if (handle != IntPtr.Zero)
            {
                openjtalk_destroy(handle);
                handle = IntPtr.Zero;
            }
        }
        
        [Test]
        [Category("NativeTests")]
        public void TestVersion()
        {
            IntPtr versionPtr = openjtalk_get_version();
            Assert.AreNotEqual(IntPtr.Zero, versionPtr);
            
            string version = Marshal.PtrToStringAnsi(versionPtr);
            Assert.IsNotEmpty(version);
            Assert.That(version, Does.StartWith("2."));
            Debug.Log($"OpenJTalk version: {version}");
        }
        
        [Test]
        [Category("NativeTests")]
        public void TestBasicPhonemization()
        {
            string text = "こんにちは";
            IntPtr resultPtr = openjtalk_phonemize(handle, text);
            
            Assert.AreNotEqual(IntPtr.Zero, resultPtr);
            
            PhonemeResult result = Marshal.PtrToStructure<PhonemeResult>(resultPtr);
            Assert.Greater(result.phoneme_count, 0);
            Assert.AreNotEqual(IntPtr.Zero, result.phonemes);
            Assert.Greater(result.total_duration, 0f);
            
            string phonemes = Marshal.PtrToStringAnsi(result.phonemes);
            Assert.IsNotEmpty(phonemes);
            Debug.Log($"Phonemes for '{text}': {phonemes}");
            Debug.Log($"Phoneme count: {result.phoneme_count}, Duration: {result.total_duration}s");
            
            openjtalk_free_result(resultPtr);
        }
        
        [Test]
        [Category("NativeTests")]
        public void TestErrorHandling()
        {
            // Test with null text
            IntPtr resultPtr = openjtalk_phonemize(handle, null);
            Assert.AreEqual(IntPtr.Zero, resultPtr);
            
            int error = openjtalk_get_last_error(handle);
            Assert.AreNotEqual(0, error); // Should have an error
            
            IntPtr errorStrPtr = openjtalk_get_error_string(error);
            string errorStr = Marshal.PtrToStringAnsi(errorStrPtr);
            Assert.IsNotEmpty(errorStr);
            Debug.Log($"Error string: {errorStr}");
        }
        
        [Test]
        [Category("NativeTests")]
        public void TestOptions()
        {
            // Debug: Check handle validity
            Assert.AreNotEqual(IntPtr.Zero, handle, "Handle should not be null");
            
            // Test setting option
            int result = openjtalk_set_option(handle, "speech_rate", "1.5");
            Debug.Log($"openjtalk_set_option result: {result}, handle: {handle}");
            Assert.AreEqual(0, result); // OPENJTALK_SUCCESS
            
            // Test getting option
            IntPtr valuePtr = openjtalk_get_option(handle, "speech_rate");
            Assert.AreNotEqual(IntPtr.Zero, valuePtr);
            
            string value = Marshal.PtrToStringAnsi(valuePtr);
            Assert.AreEqual("1.50", value);
        }
        
        [Test]
        [Category("NativeTests")]
        public void TestMultiplePhonemizations()
        {
            string[] testTexts = { "ありがとう", "テスト", "日本語" };
            
            foreach (string text in testTexts)
            {
                IntPtr resultPtr = openjtalk_phonemize(handle, text);
                if (resultPtr != IntPtr.Zero)
                {
                    PhonemeResult result = Marshal.PtrToStructure<PhonemeResult>(resultPtr);
                    string phonemes = Marshal.PtrToStringAnsi(result.phonemes);
                    Debug.Log($"'{text}' -> {phonemes} ({result.phoneme_count} phonemes)");
                    openjtalk_free_result(resultPtr);
                }
            }
        }
        
        [Test]
        [Category("NativeTests")]
        [Category("Performance")]
        public void TestPerformance()
        {
            string text = "今日は良い天気です";
            int iterations = 100;
            
            var sw = System.Diagnostics.Stopwatch.StartNew();
            
            for (int i = 0; i < iterations; i++)
            {
                IntPtr resultPtr = openjtalk_phonemize(handle, text);
                if (resultPtr != IntPtr.Zero)
                {
                    openjtalk_free_result(resultPtr);
                }
            }
            
            sw.Stop();
            double avgMs = sw.ElapsedMilliseconds / (double)iterations;
            
            Debug.Log($"Average processing time: {avgMs:F3} ms");
            Assert.Less(avgMs, 10.0, "Processing should be under 10ms per sentence");
        }
        
        private string GetTestDictionaryPath()
        {
            // First try the main dictionary (which we know works)
            string mainDictPath = System.IO.Path.Combine(
                Application.dataPath, "uPiper", "Native", "OpenJTalk", "dictionary"
            );
            if (System.IO.Directory.Exists(mainDictPath))
            {
                Debug.Log($"Using main dictionary at: {mainDictPath}");
                return mainDictPath;
            }
            
            // Try different possible locations for the test dictionary
            string[] possiblePaths = {
                "Assets/uPiper/Native/OpenJTalk/dictionary",
                "Assets/uPiper/Native/OpenJTalk/test_dictionary",
                "Packages/com.upiper.native/OpenJTalk/dictionary"
            };
            
            foreach (string path in possiblePaths)
            {
                string fullPath = System.IO.Path.GetFullPath(path);
                if (System.IO.Directory.Exists(fullPath))
                {
                    Debug.Log($"Found dictionary at: {fullPath}");
                    return fullPath;
                }
            }
            
            return "dictionary"; // Fallback
        }
        
        private bool IsNativeLibraryAvailable()
        {
            try
            {
                Debug.Log("[OpenJTalkNativeTest] Attempting to call openjtalk_get_version...");
                // Try to call version function to check if library is loaded
                IntPtr versionPtr = openjtalk_get_version();
                Debug.Log($"[OpenJTalkNativeTest] Version pointer: {versionPtr} (0x{versionPtr.ToInt64():X})");
                
                if (versionPtr != IntPtr.Zero)
                {
                    string version = Marshal.PtrToStringAnsi(versionPtr);
                    Debug.Log($"[OpenJTalkNativeTest] OpenJTalk version: {version}");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[OpenJTalkNativeTest] Failed to check library availability: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }
        
        #else // !ENABLE_NATIVE_TESTS
        
        [Test]
        public void NativeTestsDisabled()
        {
            Assert.Ignore("Native tests are disabled on this platform or configuration.");
        }
        
        #endif // ENABLE_NATIVE_TESTS
    }
}