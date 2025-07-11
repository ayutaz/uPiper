using System;
using UnityEngine;
using UnityEngine.UI;
using uPiper.Core;

namespace uPiper.Samples
{
    /// <summary>
    /// Basic example of using uPiper TTS
    /// </summary>
    public class TTSExample : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private InputField textInput;
        [SerializeField] private Button generateButton;
        [SerializeField] private Button playButton;
        [SerializeField] private Text statusText;
        [SerializeField] private Dropdown languageDropdown;

        [Header("TTS Configuration")]
        [SerializeField] private string modelPath = "Models/japanese_voice_v1.onnx";
        [SerializeField] private bool enableDebugLogging = true;

        private IPiperTTS _tts;
        private AudioSource _audioSource;
        private AudioClip _currentClip;

        private async void Start()
        {
            // Get or add AudioSource component
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }

            // Setup UI
            SetupUI();
            UpdateStatus("Initializing TTS...");

            try
            {
                // Initialize TTS
                _tts = new PiperTTS();
                var config = new PiperConfig
                {
                    Language = "ja",
                    ModelPath = modelPath,
                    EnableDebugLogging = enableDebugLogging,
                    UseCache = true
                };

                await _tts.InitializeAsync(config);
                UpdateStatus("TTS Ready");
                generateButton.interactable = true;
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error: {ex.Message}");
                Debug.LogError($"[TTSExample] Failed to initialize: {ex}");
            }
        }

        private void SetupUI()
        {
            // Setup generate button
            generateButton.onClick.AddListener(OnGenerateClicked);
            generateButton.interactable = false;

            // Setup play button
            playButton.onClick.AddListener(OnPlayClicked);
            playButton.interactable = false;

            // Setup language dropdown
            languageDropdown.ClearOptions();
            languageDropdown.AddOptions(new System.Collections.Generic.List<string> { "Japanese (ja)", "English (en)" });
            languageDropdown.value = 0;

            // Set default text
            if (string.IsNullOrEmpty(textInput.text))
            {
                textInput.text = "こんにちは、世界！";
            }
        }

        private async void OnGenerateClicked()
        {
            if (_tts == null || !_tts.IsInitialized)
            {
                UpdateStatus("TTS not initialized");
                return;
            }

            var text = textInput.text;
            if (string.IsNullOrEmpty(text))
            {
                UpdateStatus("Please enter text");
                return;
            }

            try
            {
                generateButton.interactable = false;
                playButton.interactable = false;
                UpdateStatus("Generating speech...");

                // Get selected language
                var language = languageDropdown.value == 0 ? "ja" : "en";

                // Generate speech
                _currentClip = await _tts.GenerateSpeechAsync(text, language);

                UpdateStatus("Speech generated successfully");
                playButton.interactable = true;
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error: {ex.Message}");
                Debug.LogError($"[TTSExample] Generation failed: {ex}");
            }
            finally
            {
                generateButton.interactable = true;
            }
        }

        private void OnPlayClicked()
        {
            if (_currentClip == null || _audioSource == null)
            {
                UpdateStatus("No audio to play");
                return;
            }

            if (_audioSource.isPlaying)
            {
                _audioSource.Stop();
                UpdateStatus("Playback stopped");
            }
            else
            {
                _audioSource.clip = _currentClip;
                _audioSource.Play();
                UpdateStatus("Playing audio...");
            }
        }

        private void UpdateStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = $"Status: {message}";
            }
        }

        private void OnDestroy()
        {
            _tts?.Dispose();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && _audioSource != null && _audioSource.isPlaying)
            {
                _audioSource.Pause();
            }
        }
    }
}