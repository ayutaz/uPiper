using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.InferenceEngine;
using uPiper.Core;
using uPiper.Core.AudioGeneration;
using uPiper.Core.Phonemizers.Implementations;

namespace uPiper.Editor
{
    /// <summary>
    /// Development version of Basic TTS Demo
    /// This is a simplified version for use within the development project
    /// </summary>
    public class BasicTTSDemoDev : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_InputField _inputField;
        [SerializeField] private Button _generateButton;
        [SerializeField] private TextMeshProUGUI _statusText;
        [SerializeField] private AudioSource _audioSource;
        
        [Header("Model Settings")]
        [SerializeField] private ModelAsset _modelAsset;
        [SerializeField] private float _speakingRate = 1.0f;
        [SerializeField] private int _sampleRate = 22050;
        
        private InferenceAudioGenerator _audioGenerator;
        private OpenJTalkPhonemizer _phonemizer;
        private AudioClipBuilder _audioClipBuilder;
        private bool _isProcessing;

        private void Start()
        {
            // Set default text
            if (_inputField != null)
                _inputField.text = "こんにちは、これはuPiperのテストです。";
            
            // Initialize components
            StartCoroutine(Initialize());
            
            // Add button listener
            if (_generateButton != null)
                _generateButton.onClick.AddListener(OnGenerateButtonClicked);
        }

        private IEnumerator Initialize()
        {
            UpdateStatus("Initializing...");
            if (_generateButton != null)
                _generateButton.interactable = false;
            
            // Initialize phonemizer (コンストラクタで初期化される)
            _phonemizer = new OpenJTalkPhonemizer();
            
            // Initialize audio clip builder
            _audioClipBuilder = new AudioClipBuilder();
            
            // Initialize audio generator
            if (_modelAsset == null)
            {
                // Try to load model from Resources
                _modelAsset = Resources.Load<ModelAsset>("Models/ja_JP-test-medium");
                if (_modelAsset == null)
                {
                    UpdateStatus("Model asset not found! Please assign in inspector.", Color.red);
                    yield break;
                }
            }
            
            _audioGenerator = new InferenceAudioGenerator();
            
            // Create voice config
            var voiceConfig = new PiperVoiceConfig
            {
                ModelPath = "ja_JP-test-medium.onnx",
                Language = "ja_JP",
                SampleRate = _sampleRate,
                NumSpeakers = 1,
                VoiceId = "ja_JP-test-medium",
                DisplayName = "Japanese Test Medium"
            };
            
            var initTask = _audioGenerator.InitializeAsync(_modelAsset, voiceConfig);
            yield return new WaitUntil(() => initTask.IsCompleted);
            
            if (!initTask.IsCompletedSuccessfully)
            {
                UpdateStatus("Failed to initialize audio generator!", Color.red);
                yield break;
            }
            
            UpdateStatus("Ready", Color.green);
            if (_generateButton != null)
                _generateButton.interactable = true;
        }

        private void OnGenerateButtonClicked()
        {
            if (_isProcessing || _inputField == null || string.IsNullOrWhiteSpace(_inputField.text))
                return;
                
            StartCoroutine(GenerateSpeech());
        }

        private IEnumerator GenerateSpeech()
        {
            _isProcessing = true;
            if (_generateButton != null)
                _generateButton.interactable = false;
            
            var text = _inputField.text.Trim();
            UpdateStatus($"Processing: {text}");
            
            // Phonemize text
            var phonemizeTask = _phonemizer.PhonemizeAsync(text);
            yield return new WaitUntil(() => phonemizeTask.IsCompleted);
            
            if (!phonemizeTask.IsCompletedSuccessfully)
            {
                UpdateStatus("Failed to phonemize text!", Color.red);
                if (_generateButton != null)
                    _generateButton.interactable = true;
                _isProcessing = false;
                yield break;
            }
            
            var phonemeResult = phonemizeTask.Result;
            UpdateStatus($"Phonemes: {string.Join(" ", phonemeResult.Phonemes)}");
            
            // Generate audio
            var generateTask = _audioGenerator.GenerateAudioAsync(
                phonemeResult.PhonemeIds,
                _speakingRate
            );
            yield return new WaitUntil(() => generateTask.IsCompleted);
            
            if (!generateTask.IsCompletedSuccessfully)
            {
                UpdateStatus("Failed to generate audio!", Color.red);
                if (_generateButton != null)
                    _generateButton.interactable = true;
                _isProcessing = false;
                yield break;
            }
            
            var audioData = generateTask.Result;
            
            // Create and play audio clip
            var audioClip = _audioClipBuilder.BuildAudioClip(audioData, _sampleRate, "GeneratedSpeech");
            if (_audioSource != null)
            {
                _audioSource.clip = audioClip;
                _audioSource.Play();
            }
            
            UpdateStatus("Playing audio...", Color.green);
            
            // Wait for audio to finish
            if (_audioSource != null)
                yield return new WaitWhile(() => _audioSource.isPlaying);
            
            UpdateStatus("Ready", Color.green);
            if (_generateButton != null)
                _generateButton.interactable = true;
            _isProcessing = false;
        }

        private void UpdateStatus(string message, Color? color = null)
        {
            if (_statusText != null)
            {
                _statusText.text = message;
                _statusText.color = color ?? Color.yellow;
            }
            Debug.Log($"[BasicTTSDemo] {message}");
        }

        private void OnDestroy()
        {
            _phonemizer?.Dispose();
            _audioGenerator?.Dispose();
        }
    }
}