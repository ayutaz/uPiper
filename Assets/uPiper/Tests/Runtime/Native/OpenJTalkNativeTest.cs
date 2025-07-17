using System;
using System.Runtime.InteropServices;
using NUnit.Framework;
using UnityEngine;

namespace uPiper.Tests.Runtime.Native
{
    /// <summary>
    /// Tests for OpenJTalk native library integration
    /// </summary>
    public class OpenJTalkNativeTest
    {
        // P/Invoke declarations matching openjtalk_wrapper.h
        [DllImport("openjtalk_wrapper")]
        private static extern IntPtr openjtalk_create(string dict_path);
        
        [DllImport("openjtalk_wrapper")]
        private static extern void openjtalk_destroy(IntPtr handle);
        
        [DllImport("openjtalk_wrapper")]
        private static extern IntPtr openjtalk_get_version();
        
        [DllImport("openjtalk_wrapper")]
        private static extern IntPtr openjtalk_phonemize(IntPtr handle, string text);
        
        [DllImport("openjtalk_wrapper")]
        private static extern void openjtalk_free_result(IntPtr result);
        
        [DllImport("openjtalk_wrapper")]
        private static extern int openjtalk_get_last_error(IntPtr handle);
        
        [DllImport("openjtalk_wrapper")]
        private static extern IntPtr openjtalk_get_error_string(int error_code);
        
        [DllImport("openjtalk_wrapper")]
        private static extern int openjtalk_set_option(IntPtr handle, string key, string value);
        
        [DllImport("openjtalk_wrapper")]
        private static extern IntPtr openjtalk_get_option(IntPtr handle, string key);
        
        // PhonemeResult structure
        [StructLayout(LayoutKind.Sequential)]
        private struct PhonemeResult
        {
            public IntPtr phonemes;      // char*
            public int phoneme_count;
            public IntPtr phoneme_ids;   // int*
            public IntPtr durations;     // float*
            public float total_duration;
            public int word_count;
            public IntPtr word_boundaries; // int*
            public IntPtr accent_info;   // AccentInfo*
        }
        
        private IntPtr handle = IntPtr.Zero;
        
        [SetUp]
        public void SetUp()
        {
            // Try to find test dictionary
            string dictPath = GetTestDictionaryPath();
            if (!System.IO.Directory.Exists(dictPath))
            {
                Assert.Ignore("Test dictionary not found. Run create_test_dict.py first.");
            }
            
            handle = openjtalk_create(dictPath);
            if (handle == IntPtr.Zero)
            {
                Assert.Fail("Failed to create OpenJTalk instance");
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
        public void TestOptions()
        {
            // Test setting option
            int result = openjtalk_set_option(handle, "speech_rate", "1.5");
            Assert.AreEqual(0, result); // OPENJTALK_SUCCESS
            
            // Test getting option
            IntPtr valuePtr = openjtalk_get_option(handle, "speech_rate");
            Assert.AreNotEqual(IntPtr.Zero, valuePtr);
            
            string value = Marshal.PtrToStringAnsi(valuePtr);
            Assert.AreEqual("1.50", value);
        }
        
        [Test]
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
            // Try different possible locations for the test dictionary
            string[] possiblePaths = {
                "Assets/uPiper/Native/OpenJTalk/test_dictionary",
                "Packages/com.upiper.native/OpenJTalk/test_dictionary",
                "../Assets/uPiper/Native/OpenJTalk/test_dictionary"
            };
            
            foreach (string path in possiblePaths)
            {
                if (System.IO.Directory.Exists(path))
                {
                    return path;
                }
            }
            
            return "test_dictionary"; // Fallback
        }
    }
}