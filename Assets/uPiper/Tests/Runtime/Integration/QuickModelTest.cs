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
            
            // Create TTS
            var config = new PiperConfig
            {
                DefaultLanguage = "ja",
                SampleRate = 22050,
                EnablePhonemeCache = false,
                EnableMultiThreadedInference = false
            };
            
            var piperTTS = new PiperTTS(config);
            
            // Initialize
            Debug.Log("Initializing PiperTTS...");
            var initTask = piperTTS.InitializeAsync();
            
            float timeout = 10f;
            float elapsed = 0f;
            while (!initTask.IsCompleted && elapsed < timeout)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
            
            if (initTask.IsFaulted)
            {
                Debug.LogError($"Initialization failed: {initTask.Exception?.GetBaseException()}");
                Assert.Fail($"Init failed: {initTask.Exception?.GetBaseException().Message}");
            }
            
            Debug.Log("PiperTTS initialized successfully");
            
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