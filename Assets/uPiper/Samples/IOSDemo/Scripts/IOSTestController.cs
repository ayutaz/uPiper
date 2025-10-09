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
        private PiperVoiceConfig voiceConfig;
        private bool isInitialized = false;
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
            StartCoroutine(InitializeTTSCoroutine());

            UpdateStatus("iOS Test App Ready");
            LogSystemInfo();
        }

        private IEnumerator InitializeTTSCoroutine()
        {
            UpdateStatus("Initializing PiperTTS...");

            // Create PiperTTS instance with configuration
            var config = new Core.PiperConfig
            {
                SampleRate = 22050,
                DefaultLanguage = "ja",
                Backend = Core.InferenceBackend.CPU,
                EnablePhonemeCache = true,
                MaxCacheSizeMB = 32
            };

            piperTTS = new PiperTTS(config);

            // Initialize asynchronously
            var initTask = piperTTS.InitializeAsync();
            yield return new WaitUntil(() => initTask.IsCompleted);

            if (initTask.IsFaulted)
            {
                UpdateStatus($"Failed to initialize PiperTTS: {initTask.Exception?.GetBaseException().Message}");
                Debug.LogError($"[IOSTestController] TTS initialization error: {initTask.Exception}");
            }
            else
            {
                isInitialized = true;
                UpdateStatus("PiperTTS initialized successfully");
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
            if (piperTTS == null || !isInitialized)
            {
                UpdateStatus("PiperTTS not initialized");
                return;
            }

            UpdateStatus("Testing TTS...");
            StartCoroutine(TestTTSCoroutine());
        }

        private IEnumerator TestTTSCoroutine()
        {
            // Load voice model if not already loaded
            if (!isModelLoaded)
            {
                UpdateStatus("Loading voice model...");

                // Create voice configuration
                voiceConfig = new Core.PiperVoiceConfig
                {
                    VoiceId = "kokoro-v0_19-small-ja",
                    ModelPath = modelPath,
                    Language = "ja",
                    SampleRate = 22050,
                    // Additional config if needed
                };

                // Load voice asynchronously
                var loadTask = piperTTS.LoadVoiceAsync(voiceConfig);
                yield return new WaitUntil(() => loadTask.IsCompleted);

                if (loadTask.IsFaulted)
                {
                    UpdateStatus($"Voice load failed: {loadTask.Exception?.GetBaseException().Message}");
                    UpdateResult($"Voice Error: {loadTask.Exception?.GetBaseException().Message}");
                    yield break;
                }

                isModelLoaded = true;
                UpdateStatus("Voice model loaded successfully");
            }

            // Generate audio
            UpdateStatus("Generating audio...");

            var testText = string.IsNullOrEmpty(textInput.text) ? "iOSでの音声合成テストです" : textInput.text;

            // Generate audio asynchronously
            var generateTask = piperTTS.GenerateAudioAsync(testText);
            yield return new WaitUntil(() => generateTask.IsCompleted);

            if (generateTask.IsFaulted)
            {
                UpdateStatus($"Audio generation failed: {generateTask.Exception?.GetBaseException().Message}");
                UpdateResult($"Generation Error: {generateTask.Exception?.GetBaseException().Message}");
                yield break;
            }

            var generatedClip = generateTask.Result;

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
                UpdateStatus("Audio generation returned null");
                UpdateResult("No audio generated");
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
            piperTTS?.Dispose();
        }
    }
}