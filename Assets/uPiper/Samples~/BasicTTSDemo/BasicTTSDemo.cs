using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using uPiper.Core;
using uPiper.Core.AudioGeneration;
using uPiper.Core.Phonemizers.Implementations;

namespace uPiper.Samples.BasicTTSDemo
{
    /// <summary>
    /// Basic TTS demo showing simple text-to-speech synthesis with Japanese text
    /// </summary>
    public class BasicTTSDemo : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_InputField _inputField;
        [SerializeField] private Button _generateButton;
        [SerializeField] private TextMeshProUGUI _statusText;
        [SerializeField] private AudioSource _audioSource;
        
        [Header("Model Settings")]
        [SerializeField] private string _modelPath = "ja_JP-test-medium.onnx";
        [SerializeField] private float _speakingRate = 1.0f;
        
        private InferenceAudioGenerator _audioGenerator;
        private OpenJTalkPhonemizer _phonemizer;
        private bool _isProcessing;

        private void Start()
        {
            // Set default text
            _inputField.text = "こんにちは、これはuPiperのテストです。";
            
            // Initialize components
            StartCoroutine(Initialize());
            
            // Add button listener
            _generateButton.onClick.AddListener(OnGenerateButtonClicked);
        }

        private IEnumerator Initialize()
        {
            UpdateStatus("Initializing...");
            _generateButton.interactable = false;
            
            // Initialize phonemizer
            _phonemizer = new OpenJTalkPhonemizer();
            var phonemizerInit = _phonemizer.InitializeAsync();
            yield return new WaitUntil(() => phonemizerInit.IsCompleted);
            
            if (!phonemizerInit.IsCompletedSuccessfully)
            {
                UpdateStatus("Failed to initialize phonemizer!", Color.red);
                yield break;
            }
            
            // Initialize audio generator
            _audioGenerator = new InferenceAudioGenerator();
            var modelResourcePath = System.IO.Path.GetFileNameWithoutExtension(_modelPath);
            var initTask = _audioGenerator.InitializeAsync(modelResourcePath, 22050);
            yield return new WaitUntil(() => initTask.IsCompleted);
            
            if (!initTask.IsCompletedSuccessfully)
            {
                UpdateStatus("Failed to initialize audio generator!", Color.red);
                yield break;
            }
            
            UpdateStatus("Ready", Color.green);
            _generateButton.interactable = true;
        }

        private void OnGenerateButtonClicked()
        {
            if (_isProcessing || string.IsNullOrWhiteSpace(_inputField.text))
                return;
                
            StartCoroutine(GenerateSpeech());
        }

        private IEnumerator GenerateSpeech()
        {
            _isProcessing = true;
            _generateButton.interactable = false;
            
            var text = _inputField.text.Trim();
            UpdateStatus($"Processing: {text}");
            
            // Phonemize text
            var phonemizeTask = _phonemizer.PhonemizeAsync(text);
            yield return new WaitUntil(() => phonemizeTask.IsCompleted);
            
            if (!phonemizeTask.IsCompletedSuccessfully)
            {
                UpdateStatus("Failed to phonemize text!", Color.red);
                _generateButton.interactable = true;
                _isProcessing = false;
                yield break;
            }
            
            var phonemeResult = phonemizeTask.Result;
            UpdateStatus($"Phonemes: {string.Join(" ", phonemeResult.Phonemes)}");
            
            // Generate audio
            var generateTask = _audioGenerator.GenerateAudioAsync(
                phonemeResult.Phonemes, 
                phonemeResult.PhonemeIds,
                _speakingRate
            );
            yield return new WaitUntil(() => generateTask.IsCompleted);
            
            if (!generateTask.IsCompletedSuccessfully)
            {
                UpdateStatus("Failed to generate audio!", Color.red);
                _generateButton.interactable = true;
                _isProcessing = false;
                yield break;
            }
            
            var audioData = generateTask.Result;
            
            // Create and play audio clip
            var audioClip = AudioClipBuilder.CreateAudioClip(audioData, 22050, "GeneratedSpeech");
            _audioSource.clip = audioClip;
            _audioSource.Play();
            
            UpdateStatus("Playing audio...", Color.green);
            
            // Wait for audio to finish
            yield return new WaitWhile(() => _audioSource.isPlaying);
            
            UpdateStatus("Ready", Color.green);
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