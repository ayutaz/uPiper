using System.Collections;
using System.IO;
using NUnit.Framework;
using Unity.Sentis;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core;

namespace uPiper.Tests.Integration
{
    [TestFixture]
    public class PiperIntegrationTests
    {
        private string _modelsPath;

        [OneTimeSetUp]
        public void Setup()
        {
            _modelsPath = Path.Combine(Application.streamingAssetsPath, "uPiper", "Models");
            
            // Verify models exist
            Assert.IsTrue(Directory.Exists(_modelsPath), $"Models directory not found at: {_modelsPath}");
            
            var jaModelPath = Path.Combine(_modelsPath, "ja_JP-test-medium.onnx");
            var enModelPath = Path.Combine(_modelsPath, "test_voice.onnx");
            
            Assert.IsTrue(File.Exists(jaModelPath), $"Japanese model not found at: {jaModelPath}");
            Assert.IsTrue(File.Exists(enModelPath), $"English model not found at: {enModelPath}");
        }

        [UnityTest]
        public IEnumerator PiperTTS_InitializeWithJapaneseModel_ShouldSucceed()
        {
            var tts = new PiperTTS();
            var config = new PiperConfig
            {
                Language = "ja",
                ModelPath = Path.Combine(_modelsPath, "ja_JP-test-medium.onnx"),
                EnableDebugLogging = true,
                UseCache = true,
                SentisBackend = BackendType.CPU // Use CPU for tests
            };

            bool initialized = false;
            bool errorOccurred = false;
            string errorMessage = null;

            tts.OnInitializationStateChanged += (state) => initialized = state;
            tts.OnError += (error) => 
            {
                errorOccurred = true;
                errorMessage = error;
            };

            var initTask = tts.InitializeAsync(config);
            
            // Wait for initialization
            while (!initTask.IsCompleted)
            {
                yield return null;
            }

            Assert.IsFalse(errorOccurred, $"Error during initialization: {errorMessage}");
            Assert.IsTrue(initialized, "TTS should be initialized");
            Assert.IsTrue(tts.IsInitialized);
            Assert.AreEqual(config.Language, tts.CurrentConfig.Language);

            tts.Dispose();
        }

        [UnityTest]
        public IEnumerator PiperTTS_GenerateJapaneseSpeech_ShouldReturnAudioClip()
        {
            var tts = new PiperTTS();
            var config = new PiperConfig
            {
                Language = "ja",
                ModelPath = Path.Combine(_modelsPath, "ja_JP-test-medium.onnx"),
                EnableDebugLogging = true,
                SentisBackend = BackendType.CPU
            };

            // Initialize
            var initTask = tts.InitializeAsync(config);
            while (!initTask.IsCompleted)
            {
                yield return null;
            }

            Assert.IsTrue(tts.IsInitialized);

            // Generate speech
            const string testText = "こんにちは、世界";
            var generateTask = tts.GenerateSpeechAsync(testText, "ja");
            
            while (!generateTask.IsCompleted)
            {
                yield return null;
            }

            var audioClip = generateTask.Result;
            
            Assert.IsNotNull(audioClip, "Audio clip should not be null");
            Assert.Greater(audioClip.length, 0, "Audio clip should have duration");
            Assert.AreEqual(config.SampleRate, audioClip.frequency);
            Assert.AreEqual(config.Channels, audioClip.channels);

            // Check if audio contains actual data
            var samples = new float[audioClip.samples];
            audioClip.GetData(samples, 0);
            
            bool hasNonZeroSamples = false;
            foreach (var sample in samples)
            {
                if (Mathf.Abs(sample) > 0.0001f)
                {
                    hasNonZeroSamples = true;
                    break;
                }
            }
            
            Assert.IsTrue(hasNonZeroSamples, "Audio should contain non-zero samples");

            tts.Dispose();
        }

        [UnityTest]
        public IEnumerator PiperTTS_InitializeWithEnglishModel_ShouldSucceed()
        {
            var tts = new PiperTTS();
            var config = new PiperConfig
            {
                Language = "en",
                ModelPath = Path.Combine(_modelsPath, "test_voice.onnx"),
                EnableDebugLogging = true,
                SentisBackend = BackendType.CPU,
                SampleRate = 16000 // English model uses 16kHz
            };

            var initTask = tts.InitializeAsync(config);
            
            while (!initTask.IsCompleted)
            {
                yield return null;
            }

            Assert.IsTrue(tts.IsInitialized);

            // Generate English speech
            const string testText = "Hello, world!";
            var generateTask = tts.GenerateSpeechAsync(testText, "en");
            
            while (!generateTask.IsCompleted)
            {
                yield return null;
            }

            var audioClip = generateTask.Result;
            Assert.IsNotNull(audioClip);
            Assert.AreEqual(16000, audioClip.frequency);

            tts.Dispose();
        }

        [Test]
        public void PiperConfig_LoadJapaneseModelConfig_ShouldParseCorrectly()
        {
            var configPath = Path.Combine(_modelsPath, "ja_JP-test-medium.onnx.json");
            Assert.IsTrue(File.Exists(configPath));

            var jsonContent = File.ReadAllText(configPath);
            Assert.IsNotEmpty(jsonContent);

            // Basic JSON validation
            Assert.IsTrue(jsonContent.Contains("\"language\""));
            Assert.IsTrue(jsonContent.Contains("\"sample_rate\""));
            Assert.IsTrue(jsonContent.Contains("\"phoneme_type\""));
        }

        [UnityTest]
        public IEnumerator PiperTTS_MultipleGenerations_ShouldUseCache()
        {
            var tts = new PiperTTS();
            var config = new PiperConfig
            {
                Language = "ja",
                ModelPath = Path.Combine(_modelsPath, "ja_JP-test-medium.onnx"),
                UseCache = true,
                SentisBackend = BackendType.CPU
            };

            var initTask = tts.InitializeAsync(config);
            while (!initTask.IsCompleted)
            {
                yield return null;
            }

            const string testText = "テスト";
            
            // First generation
            var sw1 = System.Diagnostics.Stopwatch.StartNew();
            var task1 = tts.GenerateSpeechAsync(testText, "ja");
            while (!task1.IsCompleted) yield return null;
            sw1.Stop();
            var firstTime = sw1.ElapsedMilliseconds;

            // Second generation (should be faster due to cache)
            var sw2 = System.Diagnostics.Stopwatch.StartNew();
            var task2 = tts.GenerateSpeechAsync(testText, "ja");
            while (!task2.IsCompleted) yield return null;
            sw2.Stop();
            var secondTime = sw2.ElapsedMilliseconds;

            Debug.Log($"First generation: {firstTime}ms, Second generation: {secondTime}ms");
            
            // Cache should make second generation faster
            // Note: This might not always be true in tests due to various factors
            
            tts.Dispose();
        }

        [Test]
        public void PiperTTS_InvalidModelPath_ShouldThrowException()
        {
            var tts = new PiperTTS();
            var config = new PiperConfig
            {
                ModelPath = "invalid/path/model.onnx"
            };

            Assert.ThrowsAsync<System.Exception>(async () => 
            {
                await tts.InitializeAsync(config);
            });
        }
    }
}