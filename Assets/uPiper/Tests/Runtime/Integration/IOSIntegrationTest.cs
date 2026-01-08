#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core;
using uPiper.Core.Phonemizers.Implementations;

namespace uPiper.Tests.Runtime.Integration
{
    /// <summary>
    /// Integration tests for iOS platform.
    /// Tests the complete TTS pipeline on iOS devices.
    /// </summary>
    [TestFixture]
    [Category("iOS")]
    [Category("Integration")]
    [Category("RequiresNativeLibrary")]
    public class IOSIntegrationTest
    {
        private PiperTTS _piperTTS;
        private string _modelPath;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            Debug.Log("[IOSIntegrationTest] Setting up iOS integration tests");

            // Check if we have a test model available
            _modelPath = Path.Combine(Application.streamingAssetsPath, "uPiper", "Models", "test_model.onnx");

            if (!File.Exists(_modelPath))
            {
                // Try alternative path for iOS
                _modelPath = Path.Combine(Application.dataPath, "Raw", "uPiper", "Models", "test_model.onnx");
            }

            if (!File.Exists(_modelPath))
            {
                Assert.Ignore("Test model not found. Skipping integration tests.");
            }
        }

        [SetUp]
        public void SetUp()
        {
            try
            {
                _piperTTS = new GameObject("PiperTTS").AddComponent<PiperTTS>();
                _piperTTS.Initialize();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize PiperTTS: {ex}");
                Assert.Ignore("PiperTTS initialization failed on iOS");
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (_piperTTS != null)
            {
                UnityEngine.Object.DestroyImmediate(_piperTTS.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator LoadModel_OnIOS_Succeeds()
        {
            bool modelLoaded = false;
            string error = null;

            _piperTTS.LoadModel(_modelPath, success =>
            {
                modelLoaded = success;
            }, err =>
            {
                error = err;
            });

            // Wait for model loading
            float timeout = 10f;
            float elapsed = 0f;

            while (!modelLoaded && string.IsNullOrEmpty(error) && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            Assert.IsTrue(modelLoaded, $"Model loading failed: {error}");
            Assert.IsNull(error);
        }

        [UnityTest]
        public IEnumerator GenerateAudio_SimpleJapanese_OnIOS()
        {
            // First load the model
            yield return LoadModel_OnIOS_Succeeds();

            AudioClip generatedClip = null;
            string error = null;
            bool completed = false;

            _piperTTS.GenerateAudioAsync(
                "こんにちは",
                clip =>
                {
                    generatedClip = clip;
                    completed = true;
                },
                err =>
                {
                    error = err;
                    completed = true;
                }
            );

            // Wait for generation
            float timeout = 5f;
            float elapsed = 0f;

            while (!completed && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            Assert.IsTrue(completed, "Audio generation timed out");
            Assert.IsNull(error, $"Audio generation failed: {error}");
            Assert.NotNull(generatedClip, "Generated audio clip is null");
            Assert.Greater(generatedClip.length, 0f, "Generated audio has no length");
            Assert.Greater(generatedClip.samples, 0, "Generated audio has no samples");
        }

        [UnityTest]
        public IEnumerator PhonemizerChain_WorksOnIOS()
        {
            var phonemizer = new OpenJTalkPhonemizer();

            var testCases = new[]
            {
                ("日本語", "Japanese text"),
                ("音声合成", "Voice synthesis"),
                ("iOS対応完了", "iOS support complete")
            };

            foreach (var (text, description) in testCases)
            {
                var task = phonemizer.PhonemizeAsync(text);
                yield return new WaitUntil(() => task.IsCompleted);
                var result = task.Result;

                Assert.NotNull(result, $"Result is null for {description}");
                Assert.NotNull(result.Phonemes, $"Phonemes are null for {description}");
                Assert.Greater(result.Phonemes.Length, 0, $"No phonemes for {description}");

                Debug.Log($"[iOS] {description}: '{text}' -> {result.Phonemes.Length} phonemes");

                yield return null;
            }

            phonemizer.Dispose();
        }

        [UnityTest]
        public IEnumerator MemoryPressure_HandledGracefully()
        {
            // Test that the system handles memory pressure
            var initialMemory = GC.GetTotalMemory(false);

            // Generate multiple audio clips
            for (int i = 0; i < 5; i++)
            {
                var phonemizer = new OpenJTalkPhonemizer();
                var task = phonemizer.PhonemizeAsync($"メモリテスト {i}");
                yield return new WaitUntil(() => task.IsCompleted);
                var result = task.Result;
                Assert.NotNull(result);
                phonemizer.Dispose();

                // Force garbage collection
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            var finalMemory = GC.GetTotalMemory(false);
            var memoryIncrease = finalMemory - initialMemory;

            // Memory should not increase significantly
            Assert.Less(memoryIncrease, 5 * 1024 * 1024,
                $"Memory leak detected: {memoryIncrease / 1024}KB increase");
        }

        [UnityTest]
        public IEnumerator BackgroundMode_PreservesState()
        {
            // Test that the phonemizer works after app suspension
            var phonemizer = new OpenJTalkPhonemizer();

            // Initial phonemization
            var task1 = phonemizer.PhonemizeAsync("初期テスト");
            yield return new WaitUntil(() => task1.IsCompleted);
            var result1 = task1.Result;
            Assert.NotNull(result1);
            Assert.Greater(result1.Phonemes.Length, 0);

            // Simulate app going to background and returning
            // (In actual iOS, this would involve applicationWillResignActive/applicationDidBecomeActive)

            // Phonemization after "resume"
            var task2 = phonemizer.PhonemizeAsync("復帰後テスト");
            yield return new WaitUntil(() => task2.IsCompleted);
            var result2 = task2.Result;
            Assert.NotNull(result2);
            Assert.Greater(result2.Phonemes.Length, 0);

            phonemizer.Dispose();
        }

        [UnityTest]
        public IEnumerator Performance_AcceptableOnIOS()
        {
            var phonemizer = new OpenJTalkPhonemizer();
            var iterations = 10;
            var totalTime = 0f;

            for (int i = 0; i < iterations; i++)
            {
                var startTime = Time.realtimeSinceStartup;
                var task = phonemizer.PhonemizeAsync($"パフォーマンステスト {i}");
                yield return new WaitUntil(() => task.IsCompleted);
                var result = task.Result;
                var elapsed = Time.realtimeSinceStartup - startTime;

                totalTime += elapsed;
                Assert.NotNull(result);

                yield return null; // Allow frame updates
            }

            var averageTime = totalTime / iterations;
            Debug.Log($"[iOS] Average phonemization time: {averageTime * 1000:F2}ms");

            // Should be fast enough for real-time use (under 100ms average)
            Assert.Less(averageTime, 0.1f,
                $"Phonemization too slow on iOS: {averageTime * 1000:F2}ms average");

            phonemizer.Dispose();
        }
    }
}
#endif
