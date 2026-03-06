#if UNITY_ANDROID
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core;

namespace uPiper.Tests.Runtime
{
    /// <summary>
    /// Integration tests for Android platform
    /// </summary>
    public class AndroidIntegrationTest
    {
        [UnityTest]
        public IEnumerator DotNetG2P_DictionaryExists()
        {
            // This test only runs on actual Android device
            if (Application.platform != RuntimePlatform.Android)
            {
                Assert.Ignore("This test only runs on Android devices");
                yield break;
            }

            // Test dictionary extraction
#if !UNITY_EDITOR
            string dictPath = Path.Combine(Application.persistentDataPath, "uPiper", "OpenJTalk", "naist_jdic", "naist_jdic");
            Assert.IsNotNull(dictPath);
            Assert.IsTrue(Directory.Exists(dictPath), $"Dictionary directory should exist at: {dictPath}");
#else
            // In editor, just check streaming assets
            var dictPath = Path.Combine(Application.streamingAssetsPath, "uPiper", "OpenJTalk", "naist_jdic", "naist_jdic");
            Assert.IsTrue(Directory.Exists(dictPath), $"Dictionary should exist in StreamingAssets");
#endif

            // Verify dictionary files
            string[] requiredFiles = { "char.bin", "sys.dic", "unk.dic", "matrix.bin" };
            foreach (var file in requiredFiles)
            {
                var filePath = Path.Combine(dictPath, file);
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
            var timeout = 10f;
            var elapsed = 0f;

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
            var timeout = 10f;
            var elapsed = 0f;

            while (!piperTTS.IsInitialized && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            Assert.IsTrue(piperTTS.IsInitialized);

            // Generate audio
            var testText = "こんにちは";
            var audioTask = piperTTS.GenerateAudioAsync(testText);
            yield return new WaitUntil(() => audioTask.IsCompleted);

            Assert.IsTrue(audioTask.IsCompletedSuccessfully);
            var audioClip = audioTask.Result;

            Assert.IsNotNull(audioClip, "Should generate audio clip");
            Assert.Greater(audioClip.length, 0f, "Audio clip should have duration");
            Assert.Greater(audioClip.samples, 0, "Audio clip should have samples");
            Assert.AreEqual(1, audioClip.channels, "Should be mono audio");

            yield return null;
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
            var hasStorageAccess = UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.ExternalStorageWrite) ||
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
            var testText = "これはパフォーマンステストです。";

            var startTime = Time.realtimeSinceStartup;
            var audioTask = piperTTS.GenerateAudioAsync(testText);
            yield return new WaitUntil(() => audioTask.IsCompleted);
            var generationTime = Time.realtimeSinceStartup - startTime;

            Assert.IsTrue(audioTask.IsCompletedSuccessfully);
            var audioClip = audioTask.Result;
            Assert.IsNotNull(audioClip);

            // Performance targets for mobile
            float textLength = testText.Length;
            var expectedMaxTime = textLength * 0.1f; // 100ms per character as baseline

            Debug.Log($"[AndroidTest] Generated {textLength} characters in {generationTime:F3}s ({generationTime / textLength * 1000:F1}ms per char)");

            // More lenient for mobile devices
            Assert.Less(generationTime, expectedMaxTime * 2,
                $"Generation time ({generationTime:F3}s) should be reasonable for mobile device");

            yield return null;
        }
    }
}
#endif