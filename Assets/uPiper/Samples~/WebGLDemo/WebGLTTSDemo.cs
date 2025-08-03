using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using uPiper.Core;
using TMPro;

namespace uPiper.Samples.WebGL
{
    /// <summary>
    /// WebGL-specific TTS demo showcasing multilingual support
    /// </summary>
    public class WebGLTTSDemo : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private TMP_Dropdown languageDropdown;
        [SerializeField] private Button speakButton;
        [SerializeField] private Button stopButton;
        [SerializeField] private Slider speedSlider;
        [SerializeField] private TextMeshProUGUI speedLabel;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private AudioSource audioSource;
        
        [Header("TTS Configuration")]
        [SerializeField] private PiperVoiceConfig[] voiceConfigs;
        
        private IPiperTTS tts;
        private bool isInitializing = false;
        private bool isGenerating = false;
        
        // Language to voice mapping
        private readonly Dictionary<string, string> languageVoiceMap = new Dictionary<string, string>
        {
            { "Japanese", "ja_JP-test-medium" },
            { "English", "en_US-amy-medium" },
            { "Chinese", "zh_CN-huayan-medium" }
        };
        
        private void Start()
        {
            SetupUI();
            StartCoroutine(InitializeTTS());
        }
        
        private void SetupUI()
        {
            // Setup language dropdown
            languageDropdown.ClearOptions();
            languageDropdown.AddOptions(new List<string>(languageVoiceMap.Keys));
            languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
            
            // Setup buttons
            speakButton.onClick.AddListener(OnSpeakButtonClicked);
            stopButton.onClick.AddListener(OnStopButtonClicked);
            speakButton.interactable = false;
            stopButton.interactable = false;
            
            // Setup speed slider
            speedSlider.minValue = 0.5f;
            speedSlider.maxValue = 2.0f;
            speedSlider.value = 1.0f;
            speedSlider.onValueChanged.AddListener(OnSpeedChanged);
            UpdateSpeedLabel(1.0f);
            
            // Set default text based on language
            UpdateDefaultText();
            
            UpdateStatus("Initializing TTS...");
        }
        
        private IEnumerator InitializeTTS()
        {
            isInitializing = true;
            
            // Create TTS configuration
            var config = new PiperConfig
            {
                DefaultLanguage = "ja",
                Backend = InferenceBackend.Auto, // Will auto-select GPUPixel for WebGL
                UsePhonemeCache = true,
                PhonemesCacheSize = 1000,
                AudioCacheSize = 10
            };
            
            // Create voice configs dictionary
            var voiceDict = new Dictionary<string, PiperVoiceConfig>();
            foreach (var voice in voiceConfigs)
            {
                if (voice != null)
                {
                    voiceDict[voice.Id] = voice;
                }
            }
            
            // Create TTS instance
            tts = new PiperTTS(config, voiceDict);
            
            // Subscribe to events
            tts.OnInitialized += OnTTSInitialized;
            tts.OnError += OnTTSError;
            tts.OnVoiceChanged += OnVoiceChanged;
            
            // Initialize TTS
            UpdateStatus("Loading TTS models...");
            yield return tts.InitializeAsync();
            
            isInitializing = false;
        }
        
        private void OnTTSInitialized()
        {
            UpdateStatus("TTS initialized successfully!");
            speakButton.interactable = true;
            
            // Set initial voice based on dropdown
            OnLanguageChanged(languageDropdown.value);
        }
        
        private void OnTTSError(string error)
        {
            UpdateStatus($"Error: {error}");
            Debug.LogError($"[WebGLTTSDemo] TTS Error: {error}");
        }
        
        private void OnVoiceChanged(string voiceId)
        {
            UpdateStatus($"Voice changed to: {voiceId}");
        }
        
        private void OnLanguageChanged(int index)
        {
            if (tts == null || !tts.IsInitialized) return;
            
            var language = languageDropdown.options[index].text;
            if (languageVoiceMap.TryGetValue(language, out string voiceId))
            {
                StartCoroutine(ChangeVoiceCoroutine(voiceId));
            }
            
            UpdateDefaultText();
        }
        
        private IEnumerator ChangeVoiceCoroutine(string voiceId)
        {
            UpdateStatus($"Changing voice to {voiceId}...");
            yield return tts.SetVoiceAsync(voiceId);
        }
        
        private void UpdateDefaultText()
        {
            var language = languageDropdown.options[languageDropdown.value].text;
            switch (language)
            {
                case "Japanese":
                    inputField.text = "こんにちは！これはWebGLでの日本語音声合成のデモです。";
                    break;
                case "English":
                    inputField.text = "Hello! This is a demonstration of English text-to-speech in WebGL.";
                    break;
                case "Chinese":
                    inputField.text = "你好！这是WebGL中文语音合成的演示。";
                    break;
            }
        }
        
        private void OnSpeakButtonClicked()
        {
            if (isGenerating || string.IsNullOrEmpty(inputField.text)) return;
            
            StartCoroutine(GenerateSpeech());
        }
        
        private IEnumerator GenerateSpeech()
        {
            isGenerating = true;
            speakButton.interactable = false;
            stopButton.interactable = true;
            
            var text = inputField.text;
            UpdateStatus("Generating speech...");
            
            var startTime = Time.realtimeSinceStartup;
            
            // Generate speech
            var result = tts.GenerateAudioClip(text);
            yield return result;
            
            if (result.IsSuccess)
            {
                var elapsedTime = Time.realtimeSinceStartup - startTime;
                UpdateStatus($"Speech generated in {elapsedTime:F2}s");
                
                // Play audio
                audioSource.clip = result.Result;
                audioSource.pitch = speedSlider.value;
                audioSource.Play();
                
                // Wait for audio to finish
                yield return new WaitForSeconds(result.Result.length / speedSlider.value);
                
                UpdateStatus("Ready");
            }
            else
            {
                UpdateStatus($"Failed to generate speech: {result.Error}");
            }
            
            isGenerating = false;
            speakButton.interactable = true;
            stopButton.interactable = false;
        }
        
        private void OnStopButtonClicked()
        {
            if (audioSource.isPlaying)
            {
                audioSource.Stop();
                StopAllCoroutines();
                isGenerating = false;
                speakButton.interactable = true;
                stopButton.interactable = false;
                UpdateStatus("Stopped");
            }
        }
        
        private void OnSpeedChanged(float value)
        {
            UpdateSpeedLabel(value);
            if (audioSource.isPlaying)
            {
                audioSource.pitch = value;
            }
        }
        
        private void UpdateSpeedLabel(float value)
        {
            speedLabel.text = $"Speed: {value:F1}x";
        }
        
        private void UpdateStatus(string status)
        {
            statusText.text = status;
            Debug.Log($"[WebGLTTSDemo] {status}");
        }
        
        private void OnDestroy()
        {
            if (tts != null)
            {
                tts.OnInitialized -= OnTTSInitialized;
                tts.OnError -= OnTTSError;
                tts.OnVoiceChanged -= OnVoiceChanged;
                tts.Dispose();
            }
        }
        
        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && audioSource.isPlaying)
            {
                audioSource.Pause();
            }
            else if (!pauseStatus && audioSource.clip != null)
            {
                audioSource.UnPause();
            }
        }
        
        private void OnApplicationFocus(bool hasFocus)
        {
            // Handle focus changes for WebGL
            if (!hasFocus && audioSource.isPlaying)
            {
                audioSource.Pause();
            }
        }
    }
}