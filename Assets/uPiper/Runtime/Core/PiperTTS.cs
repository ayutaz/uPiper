using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Sentis;
using uPiper.Phonemizers;

namespace uPiper.Core
{
    public class PiperTTS : IPiperTTS
    {
        private PiperConfig _config;
        private bool _isInitialized;
        private IPhonemizer _phonemizer;
        private Model _sentisModel;
        private IWorker _sentisWorker;
        
        public bool IsInitialized => _isInitialized;
        public PiperConfig CurrentConfig => _config;

        public event Action<bool> OnInitializationStateChanged;
        public event Action<string> OnError;

        public async Task InitializeAsync(PiperConfig config)
        {
            if (_isInitialized)
            {
                throw new InvalidOperationException("TTS engine is already initialized");
            }

            if (!config.Validate(out string errorMessage))
            {
                throw new ArgumentException($"Invalid configuration: {errorMessage}");
            }

            _config = config;

            try
            {
                // Load ONNX model
                await LoadModelAsync(config.ModelPath);

                // Initialize phonemizer based on language
                InitializePhonemizer(config.Language);

                _isInitialized = true;
                OnInitializationStateChanged?.Invoke(true);

                if (_config.EnableDebugLogging)
                {
                    Debug.Log($"[uPiper] Initialized with language: {config.Language}, backend: {config.SentisBackend}");
                }
            }
            catch (Exception ex)
            {
                var error = $"Failed to initialize TTS engine: {ex.Message}";
                OnError?.Invoke(error);
                Debug.LogError($"[uPiper] {error}");
                throw;
            }
        }

        public async Task<AudioClip> GenerateSpeechAsync(string text, string language = "ja")
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("TTS engine is not initialized");
            }

            if (string.IsNullOrEmpty(text))
            {
                throw new ArgumentException("Text cannot be null or empty");
            }

            try
            {
                // Convert text to phonemes
                var phonemes = await Task.Run(() => _phonemizer.Phonemize(text, language));

                if (_config.EnableDebugLogging)
                {
                    Debug.Log($"[uPiper] Phonemized text: {string.Join(" ", phonemes)}");
                }

                // Generate audio using Sentis
                var audioData = await GenerateAudioFromPhonemesAsync(phonemes);

                // Create AudioClip
                var audioClip = CreateAudioClip(audioData, text);

                return audioClip;
            }
            catch (Exception ex)
            {
                var error = $"Failed to generate speech: {ex.Message}";
                OnError?.Invoke(error);
                Debug.LogError($"[uPiper] {error}");
                throw;
            }
        }

        private async Task LoadModelAsync(string modelPath)
        {
            await Task.Run(() =>
            {
                // Load the ONNX model
                _sentisModel = ModelLoader.Load(modelPath);
                
                // Create worker with specified backend
                _sentisWorker = WorkerFactory.CreateWorker(_config.SentisBackend, _sentisModel);
            });
        }

        private void InitializePhonemizer(string language)
        {
            // TODO: Implement phonemizer factory
            // For now, we'll create a placeholder
            _phonemizer = new PlaceholderPhonemizer();
        }

        private async Task<float[]> GenerateAudioFromPhonemesAsync(string[] phonemes)
        {
            // TODO: Implement actual Sentis inference
            // This is a placeholder implementation
            await Task.Yield();
            
            // Generate silence for now
            int sampleCount = _config.SampleRate * 2; // 2 seconds
            var audioData = new float[sampleCount];
            
            return audioData;
        }

        private AudioClip CreateAudioClip(float[] audioData, string name)
        {
            var audioClip = AudioClip.Create(
                name: $"TTS_{name.Substring(0, Math.Min(name.Length, 20))}",
                lengthSamples: audioData.Length,
                channels: _config.Channels,
                frequency: _config.SampleRate,
                stream: false
            );

            audioClip.SetData(audioData, 0);
            return audioClip;
        }

        public void Dispose()
        {
            if (_sentisWorker != null)
            {
                _sentisWorker.Dispose();
                _sentisWorker = null;
            }

            if (_sentisModel != null)
            {
                _sentisModel = null;
            }

            _phonemizer?.Dispose();
            _phonemizer = null;

            _isInitialized = false;
            OnInitializationStateChanged?.Invoke(false);
        }
    }

    // Temporary placeholder implementation
    internal class PlaceholderPhonemizer : IPhonemizer
    {
        public string[] Phonemize(string text, string language)
        {
            // Simple placeholder that returns characters as phonemes
            return text.ToCharArray().Select(c => c.ToString()).ToArray();
        }

        public void Dispose()
        {
            // Nothing to dispose in placeholder
        }
    }
}