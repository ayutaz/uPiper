using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core;

namespace uPiper.Tests.Runtime.Integration
{
    /// <summary>
    /// End-to-end integration tests for PiperTTS
    /// </summary>
    public class PiperTTSIntegrationTests
    {
        private PiperTTS _piperTTS;
        private PiperConfig _config;

        [SetUp]
        public void SetUp()
        {
            // Force mock mode for testing to avoid native library issues
            System.Environment.SetEnvironmentVariable("PIPER_MOCK_MODE", "1");
            
            _config = new PiperConfig
            {
                DefaultLanguage = "ja",
                SampleRate = 22050,
                EnableMultiThreadedInference = false,
                EnablePhonemeCache = true,
                MaxCacheSizeMB = 10,
                TimeoutMs = 60000  // 60 seconds timeout for Windows tests
            };
            
            _piperTTS = new PiperTTS(_config);
        }

        [TearDown]
        public void TearDown()
        {
            _piperTTS?.Dispose();
        }

        [UnityTest]
        public IEnumerator InitializeAsync_Succeeds()
        {
            bool initCompleted = false;
            bool initSuccess = false;
            
            _piperTTS.OnInitialized += success => 
            {
                initCompleted = true;
                initSuccess = success;
            };
            
            var initTask = _piperTTS.InitializeAsync();
            
            yield return new WaitUntil(() => initTask.IsCompleted || initCompleted);
            
            Assert.IsFalse(initTask.IsFaulted, $"Initialization failed: {initTask.Exception?.GetBaseException()?.Message}");
            Assert.IsTrue(initTask.IsCompletedSuccessfully, "Init task should complete successfully");
            Assert.IsTrue(_piperTTS.IsInitialized, "PiperTTS should be initialized");
            Assert.IsTrue(initSuccess, "OnInitialized callback should report success");
        }

        [UnityTest]
        public IEnumerator GenerateAudioAsync_SimpleText_GeneratesAudio()
        {
            // Initialize first
            var initTask = _piperTTS.InitializeAsync();
            yield return new WaitUntil(() => initTask.IsCompleted);
            
            if (initTask.IsFaulted)
            {
                Assert.Fail($"Initialization failed: {initTask.Exception?.GetBaseException().Message}");
            }
            
            // Load a voice
            var voice = new PiperVoiceConfig
            {
                VoiceId = "test-ja",
                Language = "ja",
                ModelPath = System.IO.Path.Combine(Application.streamingAssetsPath, "uPiper", "Models", "ja_JP-test-medium.onnx")
            };
            
            var loadVoiceTask = _piperTTS.LoadVoiceAsync(voice);
            yield return new WaitUntil(() => loadVoiceTask.IsCompleted);
            
            if (loadVoiceTask.IsFaulted)
            {
                Assert.Fail($"Voice loading failed: {loadVoiceTask.Exception?.GetBaseException().Message}");
            }

            // Generate audio
            var text = "こんにちは";
            var generateTask = _piperTTS.GenerateAudioAsync(text);
            
            yield return new WaitUntil(() => generateTask.IsCompleted);
            
            Assert.IsFalse(generateTask.IsFaulted);
            
            var audioClip = generateTask.Result;
            Assert.IsNotNull(audioClip);
            Assert.Greater(audioClip.length, 0);
            Assert.AreEqual(_config.SampleRate, audioClip.frequency);
        }

        [UnityTest]
        public IEnumerator GenerateAudio_Synchronous_GeneratesAudio()
        {
            // Initialize first
            var initTask = _piperTTS.InitializeAsync();
            yield return new WaitUntil(() => initTask.IsCompleted);
            
            if (initTask.IsFaulted)
            {
                Assert.Fail($"Initialization failed: {initTask.Exception?.GetBaseException().Message}");
            }
            
            // Load a voice
            var voice = new PiperVoiceConfig
            {
                VoiceId = "test-ja",
                Language = "ja",
                ModelPath = System.IO.Path.Combine(Application.streamingAssetsPath, "uPiper", "Models", "ja_JP-test-medium.onnx")
            };
            
            var loadVoiceTask = _piperTTS.LoadVoiceAsync(voice);
            yield return new WaitUntil(() => loadVoiceTask.IsCompleted);
            
            if (loadVoiceTask.IsFaulted)
            {
                Assert.Fail($"Voice loading failed: {loadVoiceTask.Exception?.GetBaseException().Message}");
            }

            // Generate audio synchronously on main thread
            // In mock mode, this should return a valid AudioClip
            AudioClip audioClip = null;
            Exception error = null;
            
            try
            {
                audioClip = _piperTTS.GenerateAudio("テスト");
            }
            catch (System.Exception ex)
            {
                error = ex;
            }
            
            Assert.IsNull(error, $"Error generating audio: {error?.Message}");
            Assert.IsNotNull(audioClip, "Audio clip should not be null in mock mode");
            Assert.Greater(audioClip.length, 0, "Audio clip should have non-zero length");
        }

        [UnityTest]
        public IEnumerator StreamAudioAsync_GeneratesChunks()
        {
            // Initialize first
            var initTask = _piperTTS.InitializeAsync();
            yield return new WaitUntil(() => initTask.IsCompleted);
            
            if (initTask.IsFaulted)
            {
                Assert.Fail($"Initialization failed: {initTask.Exception?.GetBaseException().Message}");
            }
            
            // Load a voice
            var voice = new PiperVoiceConfig
            {
                VoiceId = "test-ja",
                Language = "ja",
                ModelPath = System.IO.Path.Combine(Application.streamingAssetsPath, "uPiper", "Models", "ja_JP-test-medium.onnx")
            };
            
            var loadVoiceTask = _piperTTS.LoadVoiceAsync(voice);
            yield return new WaitUntil(() => loadVoiceTask.IsCompleted);
            
            if (loadVoiceTask.IsFaulted)
            {
                Assert.Fail($"Voice loading failed: {loadVoiceTask.Exception?.GetBaseException().Message}");
            }

            // Stream audio
            var text = "これはストリーミングテストです";
            int chunkCount = 0;
            float totalDuration = 0;
            
            var streamTask = Task.Run(async () =>
            {
                await foreach (var chunk in _piperTTS.StreamAudioAsync(text))
                {
                    chunkCount++;
                    totalDuration += chunk.StartTime;
                    
                    Assert.IsNotNull(chunk.Samples);
                    Assert.Greater(chunk.Samples.Length, 0);
                    Assert.AreEqual(_config.SampleRate, chunk.SampleRate);
                }
            });
            
            yield return new WaitUntil(() => streamTask.IsCompleted);
            
            Assert.IsFalse(streamTask.IsFaulted);
            Assert.Greater(chunkCount, 0);
        }

        [UnityTest]
        public IEnumerator CacheSystem_CachesPhonemes()
        {
            // Initialize first
            var initTask = _piperTTS.InitializeAsync();
            yield return new WaitUntil(() => initTask.IsCompleted);
            
            if (initTask.IsFaulted)
            {
                Assert.Fail($"Initialization failed: {initTask.Exception?.GetBaseException().Message}");
            }
            
            // Load a voice
            var voice = new PiperVoiceConfig
            {
                VoiceId = "test-ja",
                Language = "ja",
                ModelPath = System.IO.Path.Combine(Application.streamingAssetsPath, "uPiper", "Models", "ja_JP-test-medium.onnx")
            };
            
            var loadVoiceTask = _piperTTS.LoadVoiceAsync(voice);
            yield return new WaitUntil(() => loadVoiceTask.IsCompleted);
            
            if (loadVoiceTask.IsFaulted)
            {
                Assert.Fail($"Voice loading failed: {loadVoiceTask.Exception?.GetBaseException().Message}");
            }
            
            var text = "キャッシュテスト";
            
            // First generation
            var task1 = _piperTTS.GenerateAudioAsync(text);
            yield return new WaitUntil(() => task1.IsCompleted);
            
            var stats1 = _piperTTS.GetCacheStatistics();
            
            // Second generation (should hit cache)
            var task2 = _piperTTS.GenerateAudioAsync(text);
            yield return new WaitUntil(() => task2.IsCompleted);
            
            var stats2 = _piperTTS.GetCacheStatistics();
            
            // Cache hit count should increase
            Assert.Greater(stats2.HitCount, stats1.HitCount);
        }

        [Test]
        public void Dispose_MultipleCallsDoNotThrow()
        {
            _piperTTS.Dispose();
            Assert.DoesNotThrow(() => _piperTTS.Dispose());
        }

        [UnityTest]
        public IEnumerator ErrorHandling_InvalidText_HandlesGracefully()
        {
            // Initialize first
            var initTask = _piperTTS.InitializeAsync();
            yield return new WaitUntil(() => initTask.IsCompleted);
            
            if (initTask.IsFaulted)
            {
                Assert.Fail($"Initialization failed: {initTask.Exception?.GetBaseException().Message}");
            }
            
            // Load a voice
            var voice = new PiperVoiceConfig
            {
                VoiceId = "test-ja",
                Language = "ja",
                ModelPath = System.IO.Path.Combine(Application.streamingAssetsPath, "uPiper", "Models", "ja_JP-test-medium.onnx")
            };
            
            var loadVoiceTask = _piperTTS.LoadVoiceAsync(voice);
            yield return new WaitUntil(() => loadVoiceTask.IsCompleted);
            
            if (loadVoiceTask.IsFaulted)
            {
                Assert.Fail($"Voice loading failed: {loadVoiceTask.Exception?.GetBaseException().Message}");
            }
            
            // Try with null text
            var task = _piperTTS.GenerateAudioAsync(null);
            yield return new WaitUntil(() => task.IsCompleted);
            
            Assert.IsTrue(task.IsFaulted);
            Assert.IsInstanceOf<System.ArgumentNullException>(task.Exception.InnerException);
        }

        [UnityTest]
        public IEnumerator ProgressReporting_ReportsProgress()
        {
            // Initialize first
            var initTask = _piperTTS.InitializeAsync();
            yield return new WaitUntil(() => initTask.IsCompleted);
            
            if (initTask.IsFaulted)
            {
                Assert.Fail($"Initialization failed: {initTask.Exception?.GetBaseException().Message}");
            }
            
            // Load a voice
            var voice = new PiperVoiceConfig
            {
                VoiceId = "test-ja",
                Language = "ja",
                ModelPath = System.IO.Path.Combine(Application.streamingAssetsPath, "uPiper", "Models", "ja_JP-test-medium.onnx")
            };
            
            var loadVoiceTask = _piperTTS.LoadVoiceAsync(voice);
            yield return new WaitUntil(() => loadVoiceTask.IsCompleted);
            
            if (loadVoiceTask.IsFaulted)
            {
                Assert.Fail($"Voice loading failed: {loadVoiceTask.Exception?.GetBaseException().Message}");
            }
            
            float lastProgress = 0;
            int progressUpdateCount = 0;
            
            _piperTTS.OnProcessingProgress += progress =>
            {
                Assert.GreaterOrEqual(progress, lastProgress);
                lastProgress = progress;
                progressUpdateCount++;
            };
            
            var task = _piperTTS.GenerateAudioAsync("プログレステスト");
            yield return new WaitUntil(() => task.IsCompleted);
            
            Assert.Greater(progressUpdateCount, 0);
            Assert.AreEqual(1.0f, lastProgress, 0.01f);
        }

        [UnityTest]
        public IEnumerator MultipleLanguages_HandlesDifferentLanguages()
        {
            // Initialize first
            var initTask = _piperTTS.InitializeAsync();
            yield return new WaitUntil(() => initTask.IsCompleted);
            
            if (initTask.IsFaulted)
            {
                Assert.Fail($"Initialization failed: {initTask.Exception?.GetBaseException().Message}");
            }
            
            // Load a voice
            var voice = new PiperVoiceConfig
            {
                VoiceId = "test-ja",
                Language = "ja",
                ModelPath = System.IO.Path.Combine(Application.streamingAssetsPath, "uPiper", "Models", "ja_JP-test-medium.onnx")
            };
            
            var loadVoiceTask = _piperTTS.LoadVoiceAsync(voice);
            yield return new WaitUntil(() => loadVoiceTask.IsCompleted);
            
            if (loadVoiceTask.IsFaulted)
            {
                Assert.Fail($"Voice loading failed: {loadVoiceTask.Exception?.GetBaseException().Message}");
            }
            
            // Test with Japanese
            var taskJa = _piperTTS.GenerateAudioAsync("日本語のテスト");
            yield return new WaitUntil(() => taskJa.IsCompleted);
            Assert.IsFalse(taskJa.IsFaulted);
            
            // Test with English (will use mock phonemizer)
            var taskEn = _piperTTS.GenerateAudioAsync("English test");
            yield return new WaitUntil(() => taskEn.IsCompleted);
            Assert.IsFalse(taskEn.IsFaulted);
        }
    }
}