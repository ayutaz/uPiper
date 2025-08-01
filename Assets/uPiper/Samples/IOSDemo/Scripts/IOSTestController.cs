using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using uPiper.Core;
using uPiper.Core.Platform;

namespace uPiper.Samples.IOSDemo
{
    /// <summary>
    /// Simple iOS test controller for validating uPiper functionality on iOS devices.
    /// </summary>
    public class IOSTestController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Text statusText;
        [SerializeField] private Text resultText;
        [SerializeField] private Button testDictionaryButton;
        [SerializeField] private Button testPhonemizerButton;
        [SerializeField] private Button testTTSButton;
        [SerializeField] private InputField textInput;
        [SerializeField] private AudioSource audioSource;

        [Header("TTS Settings")]
        [SerializeField] private string modelPath = "Models/kokoro-v0_19-small-ja.onnx";
        
        private PiperTTS piperTTS;
        private bool isModelLoaded = false;

        private void Start()
        {
            // Set default text
            if (textInput != null)
                textInput.text = "こんにちは、iOSでの音声合成テストです。";

            // Setup button listeners
            if (testDictionaryButton != null)
                testDictionaryButton.onClick.AddListener(TestDictionary);
            
            if (testPhonemizerButton != null)
                testPhonemizerButton.onClick.AddListener(TestPhonemizer);
            
            if (testTTSButton != null)
                testTTSButton.onClick.AddListener(TestTTS);

            // Initialize PiperTTS
            InitializeTTS();

            UpdateStatus("iOS Test App Ready");
            LogSystemInfo();
        }

        private void InitializeTTS()
        {
            try
            {
                // Create PiperTTS instance
                var go = new GameObject("PiperTTS");
                piperTTS = go.AddComponent<PiperTTS>();
                piperTTS.Initialize();

                UpdateStatus("PiperTTS initialized");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Failed to initialize PiperTTS: {ex.Message}");
                Debug.LogError($"[IOSTestController] TTS initialization error: {ex}");
            }
        }

        private void LogSystemInfo()
        {
            var info = $"iOS System Info:\n" +
                      $"Device Model: {SystemInfo.deviceModel}\n" +
                      $"OS: {SystemInfo.operatingSystem}\n" +
                      $"Memory: {SystemInfo.systemMemorySize}MB\n" +
                      $"CPU: {SystemInfo.processorType}\n" +
                      $"Platform: {Application.platform}";
            
            Debug.Log($"[IOSTestController] {info}");
            
            if (resultText != null)
                resultText.text = info;
        }

        private void TestDictionary()
        {
            UpdateStatus("Testing dictionary access...");

#if UNITY_IOS && !UNITY_EDITOR
            try
            {
                // Test iOS path resolver
                IOSPathResolver.LogDictionaryInfo();
                
                var exists = IOSPathResolver.DictionaryExists();
                var path = IOSPathResolver.GetOpenJTalkDictionaryPath();
                var size = IOSPathResolver.GetDictionarySize();
                
                var result = $"Dictionary Test Results:\n" +
                           $"Path: {path}\n" +
                           $"Exists: {exists}\n" +
                           $"Size: {size / 1024}KB\n";
                
                if (exists)
                {
                    // List some dictionary files
                    var files = IOSPathResolver.ListFiles("uPiper/OpenJTalk/naist_jdic/open_jtalk_dic_utf_8-1.11", "*.dic");
                    result += $"Dictionary files found: {files.Length}\n";
                    
                    foreach (var file in files)
                    {
                        result += $"  - {System.IO.Path.GetFileName(file)}\n";
                    }
                }
                
                UpdateResult(result);
                UpdateStatus(exists ? "Dictionary test passed!" : "Dictionary test failed!");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Dictionary test error: {ex.Message}");
                UpdateResult($"Error: {ex}");
            }
#else
            UpdateStatus("Dictionary test only available on iOS device");
            UpdateResult("Please run on iOS device");
#endif
        }

        private void TestPhonemizer()
        {
            UpdateStatus("Testing phonemizer...");

            try
            {
                using (var phonemizer = new Core.Phonemizers.Implementations.OpenJTalkPhonemizer())
                {
                    var testText = string.IsNullOrEmpty(textInput.text) ? "テスト" : textInput.text;
                    var result = phonemizer.Phonemize(testText);
                    
                    var output = $"Phonemizer Test Results:\n" +
                               $"Input: {testText}\n" +
                               $"Language: {result.Language}\n" +
                               $"Phoneme Count: {result.Phonemes?.Length ?? 0}\n" +
                               $"From Cache: {result.FromCache}\n" +
                               $"Processing Time: {result.ProcessingTime.TotalMilliseconds:F2}ms\n\n" +
                               $"Phonemes:\n{string.Join(" ", result.Phonemes ?? new string[0])}";
                    
                    UpdateResult(output);
                    UpdateStatus("Phonemizer test passed!");
                    
                    // Get cache statistics
                    var stats = phonemizer.GetCacheStatistics();
                    Debug.Log($"[IOSTestController] Cache stats - Hits: {stats.HitCount}, Misses: {stats.MissCount}");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Phonemizer test error: {ex.Message}");
                UpdateResult($"Error: {ex}");
                Debug.LogError($"[IOSTestController] Phonemizer error: {ex}");
            }
        }

        private void TestTTS()
        {
            if (piperTTS == null)
            {
                UpdateStatus("PiperTTS not initialized");
                return;
            }

            UpdateStatus("Testing TTS...");
            StartCoroutine(TestTTSCoroutine());
        }

        private IEnumerator TestTTSCoroutine()
        {
            // Load model if not already loaded
            if (!isModelLoaded)
            {
                UpdateStatus("Loading TTS model...");
                
                bool modelLoadComplete = false;
                bool modelLoadSuccess = false;
                string modelLoadError = null;

                var fullModelPath = System.IO.Path.Combine(Application.streamingAssetsPath, "uPiper", modelPath);
                
                piperTTS.LoadModel(fullModelPath, 
                    success =>
                    {
                        modelLoadSuccess = success;
                        modelLoadComplete = true;
                    },
                    error =>
                    {
                        modelLoadError = error;
                        modelLoadComplete = true;
                    });

                // Wait for model load
                while (!modelLoadComplete)
                {
                    yield return new WaitForSeconds(0.1f);
                }

                if (!modelLoadSuccess)
                {
                    UpdateStatus($"Model load failed: {modelLoadError}");
                    UpdateResult($"Model Error: {modelLoadError}");
                    yield break;
                }

                isModelLoaded = true;
                UpdateStatus("Model loaded successfully");
            }

            // Generate audio
            UpdateStatus("Generating audio...");
            
            var testText = string.IsNullOrEmpty(textInput.text) ? "iOSでの音声合成テストです" : textInput.text;
            AudioClip generatedClip = null;
            string generateError = null;
            bool generateComplete = false;

            piperTTS.GenerateAudioAsync(testText,
                clip =>
                {
                    generatedClip = clip;
                    generateComplete = true;
                },
                error =>
                {
                    generateError = error;
                    generateComplete = true;
                });

            // Wait for generation
            var startTime = Time.time;
            while (!generateComplete && Time.time - startTime < 10f)
            {
                yield return new WaitForSeconds(0.1f);
            }

            if (!generateComplete)
            {
                UpdateStatus("Audio generation timed out");
                UpdateResult("Timeout after 10 seconds");
                yield break;
            }

            if (generatedClip != null)
            {
                var result = $"TTS Test Results:\n" +
                           $"Input: {testText}\n" +
                           $"Audio Length: {generatedClip.length:F2}s\n" +
                           $"Sample Rate: {generatedClip.frequency}Hz\n" +
                           $"Channels: {generatedClip.channels}\n" +
                           $"Samples: {generatedClip.samples}";
                
                UpdateResult(result);
                UpdateStatus("Playing generated audio...");
                
                // Play the audio
                if (audioSource != null)
                {
                    audioSource.clip = generatedClip;
                    audioSource.Play();
                }
                
                yield return new WaitForSeconds(generatedClip.length);
                UpdateStatus("TTS test completed!");
            }
            else
            {
                UpdateStatus($"Audio generation failed: {generateError}");
                UpdateResult($"Generation Error: {generateError}");
            }
        }

        private void UpdateStatus(string message)
        {
            if (statusText != null)
                statusText.text = $"Status: {message}";
            Debug.Log($"[IOSTestController] {message}");
        }

        private void UpdateResult(string message)
        {
            if (resultText != null)
                resultText.text = message;
        }

        private void OnDestroy()
        {
            // Clean up
            if (piperTTS != null && piperTTS.gameObject != null)
            {
                Destroy(piperTTS.gameObject);
            }
        }
    }
}