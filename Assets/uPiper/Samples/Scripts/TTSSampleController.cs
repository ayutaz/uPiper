using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using uPiper.Core;

namespace uPiper.Samples
{
    /// <summary>
    /// Sample controller for demonstrating uPiper TTS functionality
    /// </summary>
    public class TTSSampleController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private InputField textInput;
        [SerializeField] private Button generateButton;
        [SerializeField] private Button playButton;
        [SerializeField] private Button stopButton;
        [SerializeField] private Dropdown languageDropdown;
        [SerializeField] private Slider progressSlider;
        [SerializeField] private Text statusText;
        [SerializeField] private Text performanceText;

        [Header("TTS Configuration")]
        [SerializeField] private int sampleRate = 22050;
        [SerializeField] private bool enableCache = true;
        [SerializeField] private int maxCacheSizeMB = 50;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;

        private PiperTTS _piperTTS;
        private AudioClip _generatedClip;
        private bool _isInitialized;
        private bool _isGenerating;

        private void Start()
        {
            SetupUI();
            StartCoroutine(InitializeTTS());
        }

        private void SetupUI()
        {
            // Setup button listeners
            generateButton.onClick.AddListener(OnGenerateButtonClicked);
            playButton.onClick.AddListener(OnPlayButtonClicked);
            stopButton.onClick.AddListener(OnStopButtonClicked);
            
            // Setup language dropdown
            languageDropdown.ClearOptions();
            languageDropdown.AddOptions(new System.Collections.Generic.List<string> 
            { 
                "Japanese (ja)", 
                "English (en)",
                "Spanish (es)",
                "French (fr)",
                "German (de)"
            });
            languageDropdown.value = 0; // Default to Japanese
            
            // Initial UI state
            generateButton.interactable = false;
            playButton.interactable = false;
            stopButton.interactable = false;
            progressSlider.value = 0;
            
            // Default text
            textInput.text = "こんにちは、これはテキスト音声合成のデモです。";
            
            UpdateStatus("Initializing...");
        }

        private IEnumerator InitializeTTS()
        {
            // Create TTS configuration
            var config = new PiperConfig
            {
                DefaultLanguage = GetSelectedLanguageCode(),
                SampleRate = sampleRate,
                EnablePhonemeCache = enableCache,
                MaxCacheSizeMB = maxCacheSizeMB,
                EnableMultiThreadedInference = true,
                WorkerThreads = 2
            };

            // Create TTS instance
            _piperTTS = new PiperTTS(config);
            
            // Subscribe to events
            _piperTTS.OnInitialized += OnTTSInitialized;
            _piperTTS.OnError += OnTTSError;
            _piperTTS.OnProcessingProgress += OnProcessingProgress;

            // Initialize asynchronously
            var initTask = _piperTTS.InitializeAsync();
            
            yield return new WaitUntil(() => initTask.IsCompleted);
            
            if (initTask.IsFaulted)
            {
                UpdateStatus($"Initialization failed: {initTask.Exception?.GetBaseException().Message}");
                Debug.LogError($"TTS initialization failed: {initTask.Exception}");
            }
        }

        private void OnTTSInitialized(bool success)
        {
            _isInitialized = success;
            
            if (success)
            {
                UpdateStatus("Ready");
                generateButton.interactable = true;
                
                // Log available voices
                Debug.Log($"Available voices: {string.Join(", ", _piperTTS.AvailableVoices)}");
            }
            else
            {
                UpdateStatus("Initialization failed");
            }
        }

        private void OnTTSError(PiperException error)
        {
            UpdateStatus($"Error: {error.Message}");
            Debug.LogError($"TTS Error: {error}");
        }

        private void OnProcessingProgress(float progress)
        {
            progressSlider.value = progress;
        }

        private void OnGenerateButtonClicked()
        {
            if (!_isInitialized || _isGenerating)
                return;

            var text = textInput.text;
            if (string.IsNullOrWhiteSpace(text))
            {
                UpdateStatus("Please enter some text");
                return;
            }

            StartCoroutine(GenerateAudioCoroutine(text));
        }

        private IEnumerator GenerateAudioCoroutine(string text)
        {
            _isGenerating = true;
            generateButton.interactable = false;
            playButton.interactable = false;
            progressSlider.value = 0;
            
            UpdateStatus("Generating audio...");
            
            var startTime = Time.realtimeSinceStartup;
            
            // Generate audio
            var generateTask = _piperTTS.GenerateAudioAsync(text);
            
            yield return new WaitUntil(() => generateTask.IsCompleted);
            
            var generationTime = Time.realtimeSinceStartup - startTime;
            
            if (generateTask.IsFaulted)
            {
                UpdateStatus($"Generation failed: {generateTask.Exception?.GetBaseException().Message}");
                performanceText.text = "";
            }
            else
            {
                _generatedClip = generateTask.Result;
                audioSource.clip = _generatedClip;
                
                UpdateStatus("Audio generated successfully");
                playButton.interactable = true;
                
                // Update performance metrics
                var audioDuration = _generatedClip.length;
                var realTimeFactor = audioDuration / generationTime;
                performanceText.text = $"Generation time: {generationTime:F2}s | Audio duration: {audioDuration:F2}s | RTF: {realTimeFactor:F2}x";
                
                // Update cache statistics
                var cacheStats = _piperTTS.GetCacheStatistics();
                Debug.Log($"Cache stats - Hits: {cacheStats.HitCount}, Misses: {cacheStats.MissCount}, Size: {cacheStats.TotalSizeBytes / 1024f / 1024f:F2}MB");
            }
            
            generateButton.interactable = true;
            _isGenerating = false;
            progressSlider.value = 0;
        }

        private void OnPlayButtonClicked()
        {
            if (_generatedClip != null && audioSource != null)
            {
                audioSource.Play();
                stopButton.interactable = true;
                playButton.interactable = false;
                UpdateStatus("Playing audio...");
            }
        }

        private void OnStopButtonClicked()
        {
            if (audioSource != null)
            {
                audioSource.Stop();
                stopButton.interactable = false;
                playButton.interactable = true;
                UpdateStatus("Stopped");
            }
        }

        private void Update()
        {
            // Check if audio finished playing
            if (audioSource != null && audioSource.isPlaying == false && stopButton.interactable)
            {
                stopButton.interactable = false;
                playButton.interactable = true;
                UpdateStatus("Ready");
            }
        }

        private void UpdateStatus(string status)
        {
            if (statusText != null)
            {
                statusText.text = $"Status: {status}";
            }
        }

        private string GetSelectedLanguageCode()
        {
            switch (languageDropdown.value)
            {
                case 0: return "ja";
                case 1: return "en";
                case 2: return "es";
                case 3: return "fr";
                case 4: return "de";
                default: return "ja";
            }
        }

        private void OnDestroy()
        {
            _piperTTS?.Dispose();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && audioSource != null && audioSource.isPlaying)
            {
                audioSource.Pause();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // Can be used to handle focus changes if needed
        }
    }
}