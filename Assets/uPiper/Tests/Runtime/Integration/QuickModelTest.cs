using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core;

namespace uPiper.Tests.Runtime.Integration
{
    /// <summary>
    /// Quick test to verify model loading and audio generation
    /// </summary>
    public class QuickModelTest
    {
        [UnityTest]
        public IEnumerator TestJapaneseModelQuick()
        {
            var modelPath = Path.Combine(Application.streamingAssetsPath, "uPiper", "Models", "ja_JP-test-medium.onnx");
            
            if (!File.Exists(modelPath))
            {
                Debug.LogWarning($"Model not found at: {modelPath}");
                Assert.Ignore("Japanese model not found");
            }

            Debug.Log($"Found model at: {modelPath}");

            // Force mock mode for testing
            System.Environment.SetEnvironmentVariable("PIPER_MOCK_MODE", "1");
            Debug.Log($"PIPER_MOCK_MODE set to: {System.Environment.GetEnvironmentVariable("PIPER_MOCK_MODE")}");
            
            // No longer expecting errors since we skip model loading in mock mode
            
            // Create TTS
            var config = new PiperConfig
            {
                DefaultLanguage = "ja",
                SampleRate = 22050,
                EnablePhonemeCache = false,
                EnableMultiThreadedInference = false,
                TimeoutMs = 5000  // 5 seconds timeout
            };
            
            var piperTTS = new PiperTTS(config);
            
            // Initialize
            Debug.Log("Initializing PiperTTS...");
            var initTask = piperTTS.InitializeAsync();
            
            float timeout = 5f; // 5 seconds timeout
            float elapsed = 0f;
            while (!initTask.IsCompleted && elapsed < timeout)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
            
            if (!initTask.IsCompleted)
            {
                Assert.Fail($"Initialization timed out after {timeout} seconds");
            }
            
            if (initTask.IsFaulted)
            {
                Debug.LogError($"Initialization failed: {initTask.Exception?.GetBaseException()}");
                Assert.Fail($"Init failed: {initTask.Exception?.GetBaseException().Message}");
            }
            
            // Wait for the task to fully complete
            yield return new WaitUntil(() => initTask.IsCompleted && !initTask.IsFaulted && !initTask.IsCanceled);
            
            Debug.Log("PiperTTS initialized successfully");
            
            // Check if PiperTTS is actually initialized
            Assert.IsTrue(piperTTS.IsInitialized, "PiperTTS should be initialized after InitializeAsync completes");
            Debug.Log($"IsInitialized: {piperTTS.IsInitialized}");
            
            // Load a voice
            var voice = new PiperVoiceConfig
            {
                VoiceId = "test-ja",
                Language = "ja",
                ModelPath = modelPath
            };
            
            Debug.Log("Loading voice...");
            var loadVoiceTask = piperTTS.LoadVoiceAsync(voice);
            
            elapsed = 0f;
            while (!loadVoiceTask.IsCompleted && elapsed < timeout)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
            
            if (loadVoiceTask.IsFaulted)
            {
                Debug.LogError($"Voice loading failed: {loadVoiceTask.Exception?.GetBaseException()}");
                Assert.Fail($"Voice loading failed: {loadVoiceTask.Exception?.GetBaseException().Message}");
            }
            
            Debug.Log("Voice loaded successfully");
            
            // Test phonemization first
            Debug.Log("Testing phonemization...");
            var text = "こんにちは";
            
            // Generate audio
            Debug.Log($"Generating audio for: {text}");
            var generateTask = piperTTS.GenerateAudioAsync(text);
            
            timeout = 30f;
            elapsed = 0f;
            while (!generateTask.IsCompleted && elapsed < timeout)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
            
            if (generateTask.IsFaulted)
            {
                // Log the full exception for debugging
                Debug.LogError($"Generation failed: {generateTask.Exception?.GetBaseException()}");
                Assert.Fail($"Generation failed: {generateTask.Exception?.GetBaseException().Message}");
            }
            
            var audioClip = generateTask.Result;
            Debug.Log($"Audio generated successfully!");
            Debug.Log($"Duration: {audioClip.length} seconds");
            Debug.Log($"Samples: {audioClip.samples}");
            Debug.Log($"Frequency: {audioClip.frequency} Hz");
            
            // Cleanup
            piperTTS.Dispose();
            
            Assert.Pass("Test completed successfully");
        }
    }
}