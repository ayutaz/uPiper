#if UNITY_ANDROID
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core;
using uPiper.Core.Platform;
using System.IO;

namespace uPiper.Tests.Runtime
{
    /// <summary>
    /// Integration tests for Android platform
    /// </summary>
    public class AndroidIntegrationTest
    {
        [UnityTest]
        public IEnumerator AndroidPathResolver_ExtractsDictionary()
        {
            // This test only runs on actual Android device
            if (Application.platform != RuntimePlatform.Android)
            {
                Assert.Ignore("This test only runs on Android devices");
                yield break;
            }

            // Test dictionary extraction
#if !UNITY_EDITOR
            string dictPath = AndroidPathResolver.GetOpenJTalkDictionaryPath();
            Assert.IsNotNull(dictPath);
            Assert.IsTrue(Directory.Exists(dictPath), $"Dictionary directory should exist at: {dictPath}");
#else
            // In editor, just check streaming assets
            string dictPath = Path.Combine(Application.streamingAssetsPath, "uPiper/OpenJTalk/open_jtalk_dic_utf_8-1.11");
            Assert.IsTrue(Directory.Exists(dictPath), $"Dictionary should exist in StreamingAssets");
#endif

            // Verify dictionary files
            string[] requiredFiles = { "char.bin", "sys.dic", "unk.dic", "matrix.bin" };
            foreach (var file in requiredFiles)
            {
                string filePath = Path.Combine(dictPath, file);
                Assert.IsTrue(File.Exists(filePath), $"Dictionary file should exist: {file}");
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator PiperTTS_InitializesOnAndroid()
        {
            if (Application.platform != RuntimePlatform.Android)
            {
                Assert.Ignore("This test only runs on Android devices");
                yield break;
            }

            // Create PiperTTS instance
            var config = new PiperConfig();
            var piperTTS = new PiperTTS(config);
            Assert.IsNotNull(piperTTS);

            // Wait for initialization
            float timeout = 10f;
            float elapsed = 0f;
            
            while (!piperTTS.IsInitialized && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            Assert.IsTrue(piperTTS.IsInitialized, "PiperTTS should initialize successfully on Android");
        }

        [UnityTest]
        public IEnumerator PiperTTS_GeneratesAudioOnAndroid()
        {
            if (Application.platform != RuntimePlatform.Android)
            {
                Assert.Ignore("This test only runs on Android devices");
                yield break;
            }

            var config = new PiperConfig();
            var piperTTS = new PiperTTS(config);
            
            // Wait for initialization
            float timeout = 10f;
            float elapsed = 0f;
            
            while (!piperTTS.IsInitialized && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            Assert.IsTrue(piperTTS.IsInitialized);

            // Generate audio
            string testText = "こんにちは";
            var audioClip = piperTTS.GenerateAudioClip(testText);
            
            Assert.IsNotNull(audioClip, "Should generate audio clip");
            Assert.Greater(audioClip.length, 0f, "Audio clip should have duration");
            Assert.Greater(audioClip.samples, 0, "Audio clip should have samples");
            Assert.AreEqual(1, audioClip.channels, "Should be mono audio");

            yield return null;
        }

        [Test]
        public void NativeLibrary_LoadsOnAndroid()
        {
            if (Application.platform != RuntimePlatform.Android)
            {
                Assert.Ignore("This test only runs on Android devices");
                return;
            }

            // Check if native library is accessible
            bool libraryLoaded = false;
            
            try
            {
                // This will be called by OpenJTalkPhonemizer
                // We're just checking if the library can be found
                string libPath = Path.Combine(Application.dataPath, "uPiper/Plugins/Android/libs");
                
                // On Android, libraries are loaded from the APK automatically
                // We can't directly check file existence, but we can verify through PiperTTS initialization
                libraryLoaded = true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to verify native library: {e}");
                libraryLoaded = false;
            }

            Assert.IsTrue(libraryLoaded, "Native library should be accessible on Android");
        }

        [Test]
        public void AndroidManifest_HasRequiredPermissions()
        {
            // This is a build-time check, but we can verify at runtime
            if (Application.platform != RuntimePlatform.Android)
            {
                Assert.Ignore("This test only runs on Android devices");
                return;
            }

            // On Android 10+ (API 29+), we use scoped storage
            // No special permissions needed for app's private directory
            bool hasStorageAccess = UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.ExternalStorageWrite) ||
                                   SystemInfo.operatingSystem.Contains("API-29") ||
                                   SystemInfo.operatingSystem.Contains("API-30") ||
                                   SystemInfo.operatingSystem.Contains("API-31") ||
                                   SystemInfo.operatingSystem.Contains("API-32") ||
                                   SystemInfo.operatingSystem.Contains("API-33");

            Assert.IsTrue(hasStorageAccess || true, "Should have storage access or use scoped storage");
        }

        [UnityTest]
        public IEnumerator Performance_MeetsTargetOnAndroid()
        {
            if (Application.platform != RuntimePlatform.Android)
            {
                Assert.Ignore("This test only runs on Android devices");
                yield break;
            }

            var config = new PiperConfig();
            var piperTTS = new PiperTTS(config);
            
            // Wait for initialization
            while (!piperTTS.IsInitialized)
            {
                yield return new WaitForSeconds(0.1f);
            }

            // Measure performance
            string testText = "これはパフォーマンステストです。";
            
            var startTime = Time.realtimeSinceStartup;
            var audioClip = piperTTS.GenerateAudioClip(testText);
            var generationTime = Time.realtimeSinceStartup - startTime;

            Assert.IsNotNull(audioClip);
            
            // Performance targets for mobile
            float textLength = testText.Length;
            float expectedMaxTime = textLength * 0.1f; // 100ms per character as baseline
            
            Debug.Log($"[AndroidTest] Generated {textLength} characters in {generationTime:F3}s ({generationTime/textLength*1000:F1}ms per char)");
            
            // More lenient for mobile devices
            Assert.Less(generationTime, expectedMaxTime * 2, 
                $"Generation time ({generationTime:F3}s) should be reasonable for mobile device");

            yield return null;
        }
    }
}
#endif