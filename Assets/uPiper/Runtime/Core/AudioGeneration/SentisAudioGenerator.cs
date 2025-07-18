using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using UnityEngine;
using Unity.InferenceEngine;
using uPiper.Core.Logging;
using uPiper.Core.Phonemizers;

namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// Sentis-based audio generator for converting phonemes to speech
    /// </summary>
    public class SentisAudioGenerator : ISentisAudioGenerator
    {
        #region Fields

        private readonly ModelLoader _modelLoader;
        private readonly IPhonemeEncoder _phonemeEncoder;
        private readonly IAudioClipBuilder _audioClipBuilder;
        private Worker _worker;
        private Model _model;
        private ModelInfo _modelInfo;
        private bool _isInitialized;
        private bool _disposed;
        private readonly object _lockObject = new object();

        // Statistics
        private int _totalGenerations;
        private float _totalGenerationTimeMs;
        private float _totalAudioDurationSeconds;
        private long _peakMemoryUsageBytes;
        private int _errorCount;

        // Events
        private event Action<float> _onProgress;
        private event Action<Exception> _onError;

        #endregion

        #region Properties

        /// <summary>
        /// Whether the generator is initialized and ready
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Current ONNX model information
        /// </summary>
        public ModelInfo CurrentModel => _modelInfo;

        /// <summary>
        /// Event fired when generation progress updates
        /// </summary>
        public event Action<float> OnProgress
        {
            add { lock (_lockObject) { _onProgress += value; } }
            remove { lock (_lockObject) { _onProgress -= value; } }
        }

        /// <summary>
        /// Event fired when an error occurs
        /// </summary>
        public event Action<Exception> OnError
        {
            add { lock (_lockObject) { _onError += value; } }
            remove { lock (_lockObject) { _onError -= value; } }
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Create a new Sentis audio generator
        /// </summary>
        /// <param name="phonemeEncoder">Optional custom phoneme encoder</param>
        /// <param name="audioClipBuilder">Optional custom audio clip builder</param>
        public SentisAudioGenerator(
            IPhonemeEncoder phonemeEncoder = null,
            IAudioClipBuilder audioClipBuilder = null)
        {
            _modelLoader = new ModelLoader();
            _phonemeEncoder = phonemeEncoder ?? new PhonemeEncoder();
            _audioClipBuilder = audioClipBuilder ?? new AudioClipBuilder();
            _isInitialized = false;
            _disposed = false;

            PiperLogger.LogInfo("SentisAudioGenerator created");
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize the generator with a model
        /// </summary>
        public async Task InitializeAsync(string modelPath, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (_isInitialized)
            {
                PiperLogger.LogWarning("Generator already initialized");
                return;
            }

            try
            {
                PiperLogger.LogInfo("Initializing SentisAudioGenerator with model: {0}", modelPath);
                ReportProgress(0.1f);

                // Load the model
                _modelInfo = await _modelLoader.LoadModelAsync(modelPath, cancellationToken);
                _model = _modelLoader.LoadedModel;
                ReportProgress(0.5f);

                // Validate model
                var validation = _modelLoader.ValidateModel();
                if (!validation.IsValid)
                {
                    throw new PiperException($"Model validation failed: {validation.ErrorMessage}");
                }

                // Create worker with appropriate backend
                CreateWorker();
                ReportProgress(0.9f);

                _isInitialized = true;
                ReportProgress(1.0f);

                PiperLogger.LogInfo("SentisAudioGenerator initialized successfully");
            }
            catch (Exception ex)
            {
                var piperEx = new PiperException("Failed to initialize audio generator", ex);
                ReportError(piperEx);
                throw piperEx;
            }
        }

        #endregion

        #region Audio Generation

        /// <summary>
        /// Generate audio from phoneme IDs
        /// </summary>
        public async Task<AudioClip> GenerateAudioAsync(
            int[] phonemeIds,
            int speakerId = 0,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ThrowIfNotInitialized();

            if (phonemeIds == null || phonemeIds.Length == 0)
                throw new ArgumentNullException(nameof(phonemeIds));

            try
            {
                var startTime = DateTime.UtcNow;
                PiperLogger.LogInfo("Generating audio from {0} phoneme IDs", phonemeIds.Length);
                ReportProgress(0.1f);

                // Run inference
                var audioSamples = await RunInferenceAsync(phonemeIds, speakerId, cancellationToken);
                ReportProgress(0.8f);

                // Create audio clip
                var audioClip = _audioClipBuilder.CreateAudioClipNormalized(
                    audioSamples,
                    _modelInfo.SampleRate,
                    1, // Mono
                    "GeneratedSpeech"
                );
                ReportProgress(1.0f);

                // Update statistics
                var generationTime = (float)(DateTime.UtcNow - startTime).TotalMilliseconds;
                UpdateStatistics(generationTime, audioClip.length);

                PiperLogger.LogInfo("Audio generation completed in {0:F1}ms", generationTime);
                return audioClip;
            }
            catch (Exception ex)
            {
                _errorCount++;
                var piperEx = new PiperException("Failed to generate audio", ex);
                ReportError(piperEx);
                throw piperEx;
            }
        }

        /// <summary>
        /// Generate audio from phoneme result
        /// </summary>
        public async Task<AudioClip> GenerateAudioAsync(
            PhonemeResult phonemeResult,
            int speakerId = 0,
            CancellationToken cancellationToken = default)
        {
            if (phonemeResult == null)
                throw new ArgumentNullException(nameof(phonemeResult));

            // Encode phonemes to IDs
            var phonemeIds = _phonemeEncoder.EncodePhonemes(phonemeResult);
            
            // Generate audio
            return await GenerateAudioAsync(phonemeIds, speakerId, cancellationToken);
        }

        /// <summary>
        /// Stream audio generation in chunks
        /// </summary>
        public async Task<IEnumerable<AudioChunk>> StreamAudioAsync(
            int[] phonemeIds,
            int speakerId = 0,
            int chunkSize = 8192,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ThrowIfNotInitialized();

            if (phonemeIds == null || phonemeIds.Length == 0)
                throw new ArgumentNullException(nameof(phonemeIds));

            // For now, generate complete audio and split into chunks
            // TODO: Implement true streaming inference when Sentis supports it
            var audioClip = await GenerateAudioAsync(phonemeIds, speakerId, cancellationToken);
            
            // Extract samples from audio clip
            var samples = new float[audioClip.samples * audioClip.channels];
            audioClip.GetData(samples, 0);

            // Split into chunks
            var chunks = new List<AudioChunk>();
            int totalChunks = (samples.Length + chunkSize - 1) / chunkSize;
            for (int i = 0; i < totalChunks; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int startIndex = i * chunkSize;
                int endIndex = Math.Min(startIndex + chunkSize, samples.Length);
                int currentChunkSize = endIndex - startIndex;

                var chunkSamples = new float[currentChunkSize];
                Array.Copy(samples, startIndex, chunkSamples, 0, currentChunkSize);

                chunks.Add(new AudioChunk(
                    samples: chunkSamples,
                    sampleRate: audioClip.frequency,
                    channels: audioClip.channels,
                    chunkIndex: i,
                    isFinal: i == totalChunks - 1,
                    textSegment: null,
                    startTime: (float)startIndex / audioClip.frequency
                ));
            }
            
            return chunks;
        }

        #endregion

        #region Inference

        /// <summary>
        /// Run the actual inference
        /// </summary>
        private async Task<float[]> RunInferenceAsync(
            int[] phonemeIds,
            int speakerId,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                lock (_lockObject)
                {
                    if (_worker == null)
                        throw new InvalidOperationException("Worker not initialized");

                    cancellationToken.ThrowIfCancellationRequested();

                    // Prepare input tensors
                    var inputTensors = PrepareInputTensors(phonemeIds, speakerId);

                    try
                    {
                        // Run inference
                        PiperLogger.LogDebug("Running inference with {0} phoneme IDs", phonemeIds.Length);
                        
                        // Execute the model
                        foreach (var kvp in inputTensors)
                        {
                            _worker.SetInput(kvp.Key, kvp.Value);
                        }
                        _worker.Schedule();

                        // Get output tensor
                        var outputTensor = _worker.PeekOutput();
                        
                        // Convert tensor to float array
                        var audioSamples = ExtractAudioFromTensor(outputTensor);
                        
                        PiperLogger.LogDebug("Inference completed, generated {0} samples", audioSamples.Length);
                        return audioSamples;
                    }
                    finally
                    {
                        // Dispose input tensors
                        foreach (var kvp in inputTensors)
                        {
                            kvp.Value?.Dispose();
                        }
                    }
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Prepare input tensors for the model
        /// </summary>
        private Dictionary<string, Tensor> PrepareInputTensors(int[] phonemeIds, int speakerId)
        {
            var tensors = new Dictionary<string, Tensor>();

            // Log model inputs for debugging
            PiperLogger.LogDebug("Model has {0} inputs", _model.inputs.Count);
            foreach (var input in _model.inputs)
            {
                PiperLogger.LogDebug("Input: {0}, shape: {1}", input.name, input.shape.ToString());
            }

            // Piper models typically have these inputs:
            // - "input" or "input_ids": phoneme IDs [batch, sequence]
            // - "input_lengths": sequence lengths [batch]
            // - "scales": inference scales [batch, 3] for noise_scale, length_scale, noise_w

            // Find the main input name
            string inputName = "input";
            foreach (var input in _model.inputs)
            {
                if (input.name.Contains("input") || input.name == "x")
                {
                    inputName = input.name;
                    break;
                }
            }

            // Create phoneme ID tensor
            // Shape: [1, sequence_length] for batch size 1
            var phonemeTensor = new Tensor<int>(new TensorShape(1, phonemeIds.Length), phonemeIds);
            tensors[inputName] = phonemeTensor;

            // Check if model expects input_lengths
            string lengthInputName = null;
            foreach (var input in _model.inputs)
            {
                if (input.name.Contains("length"))
                {
                    lengthInputName = input.name;
                    break;
                }
            }
            if (lengthInputName != null)
            {
                var lengthTensor = new Tensor<int>(new TensorShape(1), new[] { phonemeIds.Length });
                tensors[lengthInputName] = lengthTensor;
            }

            // Check if model expects scales
            string scalesInputName = null;
            foreach (var input in _model.inputs)
            {
                if (input.name.Contains("scales"))
                {
                    scalesInputName = input.name;
                    break;
                }
            }
            if (scalesInputName != null)
            {
                // Default scales: noise_scale=0.667, length_scale=1.0, noise_w=0.8
                var scales = new float[] { 0.667f, 1.0f, 0.8f };
                var scalesTensor = new Tensor<float>(new TensorShape(1, 3), scales);
                tensors[scalesInputName] = scalesTensor;
            }

            // Add speaker ID if multi-speaker model
            string speakerInputName = null;
            foreach (var input in _model.inputs)
            {
                if (input.name.Contains("speaker") || input.name.Contains("sid"))
                {
                    speakerInputName = input.name;
                    break;
                }
            }
            if (speakerInputName != null)
            {
                var speakerTensor = new Tensor<int>(new TensorShape(1), new[] { speakerId });
                tensors[speakerInputName] = speakerTensor;
            }

            return tensors;
        }

        /// <summary>
        /// Extract audio samples from output tensor
        /// </summary>
        private float[] ExtractAudioFromTensor(Tensor outputTensor)
        {
            if (outputTensor == null)
                throw new PiperException("Model produced no output");

            // Get tensor shape
            var shape = outputTensor.shape;
            PiperLogger.LogDebug("Output tensor shape: {0}", shape.ToString());

            // Download tensor data as Tensor<float>
            var floatTensor = outputTensor as Tensor<float>;
            if (floatTensor == null)
            {
                throw new PiperException("Output tensor is not a float tensor");
            }
            
            // Most TTS models output shape [1, samples] or [1, 1, samples]
            var sampleCount = floatTensor.shape.length;
            var samples = new float[sampleCount];

            // Download tensor data to CPU
            var downloadedSamples = floatTensor.DownloadToArray();
            
            // Copy samples from tensor
            downloadedSamples.CopyTo(samples, 0);
            return samples;
        }

        /// <summary>
        /// Create the Sentis worker
        /// </summary>
        private void CreateWorker()
        {
            if (_worker != null)
            {
                _worker.Dispose();
                _worker = null;
            }

            // Create worker with specified backend
            var backend = _modelInfo?.Backend ?? BackendType.GPUCompute;
            
            try
            {
                _worker = new Worker(_model, backend);
                PiperLogger.LogInfo("Created worker with backend: {0}", backend);
            }
            catch (Exception ex)
            {
                PiperLogger.LogWarning("Failed to create worker with backend {0}, falling back to CPU: {1}", 
                    backend, ex.Message);
                    
                // Fallback to CPU if GPU fails
                _worker = new Worker(_model, BackendType.CPU);
                if (_modelInfo != null)
                    _modelInfo.Backend = BackendType.CPU;
            }
        }

        #endregion

        #region Statistics

        /// <summary>
        /// Get generation statistics
        /// </summary>
        public GenerationStatistics GetStatistics()
        {
            lock (_lockObject)
            {
                var avgGenerationTime = _totalGenerations > 0 
                    ? _totalGenerationTimeMs / _totalGenerations 
                    : 0;
                    
                var avgRealTimeFactor = avgGenerationTime > 0 
                    ? (_totalAudioDurationSeconds * 1000f) / _totalGenerationTimeMs 
                    : 0;

                return new GenerationStatistics
                {
                    TotalGenerations = _totalGenerations,
                    AverageGenerationTimeMs = avgGenerationTime,
                    AverageRealTimeFactor = avgRealTimeFactor,
                    TotalAudioDurationSeconds = _totalAudioDurationSeconds,
                    PeakMemoryUsageBytes = _peakMemoryUsageBytes,
                    ErrorCount = _errorCount
                };
            }
        }

        /// <summary>
        /// Update statistics after generation
        /// </summary>
        private void UpdateStatistics(float generationTimeMs, float audioDurationSeconds)
        {
            lock (_lockObject)
            {
                _totalGenerations++;
                _totalGenerationTimeMs += generationTimeMs;
                _totalAudioDurationSeconds += audioDurationSeconds;

                // Update peak memory usage
                var currentMemory = GC.GetTotalMemory(false);
                if (currentMemory > _peakMemoryUsageBytes)
                    _peakMemoryUsageBytes = currentMemory;
            }
        }

        #endregion

        #region Helper Methods

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SentisAudioGenerator));
        }

        private void ThrowIfNotInitialized()
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Audio generator not initialized. Call InitializeAsync first.");
        }

        private void ReportProgress(float progress)
        {
            _onProgress?.Invoke(progress);
        }

        private void ReportError(Exception error)
        {
            _onError?.Invoke(error);
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Dispose of resources
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            lock (_lockObject)
            {
                if (_worker != null)
                {
                    _worker.Dispose();
                    _worker = null;
                }

                _modelLoader?.Dispose();
                _model = null;
                _modelInfo = null;
                _isInitialized = false;
                _disposed = true;
            }

            PiperLogger.LogInfo("SentisAudioGenerator disposed");
        }

        #endregion
    }
}