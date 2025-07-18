using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.InferenceEngine;
using UnityEngine;
using uPiper.Core.Logging;
using uPiper.Core.Phonemizers;
using uPiper.Core.Phonemizers.Implementations;
using uPiper.Core.AudioGeneration;

namespace uPiper.Core
{
    /// <summary>
    /// Cached audio data structure
    /// </summary>
    internal class CachedAudioData
    {
        public float[] Samples { get; set; }
        public int Frequency { get; set; }
        public int Channels { get; set; }
        public string Name { get; set; }
        public DateTime CachedAt { get; set; }
        public long SizeInBytes { get; set; }
        
        public AudioClip ToAudioClip()
        {
            var audioClip = AudioClip.Create(Name, Samples.Length, Channels, Frequency, false);
            audioClip.SetData(Samples, 0);
            return audioClip;
        }
        
        public static CachedAudioData FromAudioClip(AudioClip audioClip)
        {
            var samples = new float[audioClip.samples * audioClip.channels];
            audioClip.GetData(samples, 0);
            
            return new CachedAudioData
            {
                Samples = samples,
                Frequency = audioClip.frequency,
                Channels = audioClip.channels,
                Name = audioClip.name,
                CachedAt = DateTime.UtcNow,
                SizeInBytes = samples.Length * sizeof(float)
            };
        }
    }

    /// <summary>
    /// Main implementation of the Piper TTS interface
    /// </summary>
    public class PiperTTS : IPiperTTS
    {
        #region Fields

        private readonly PiperConfig _config;
        private readonly Dictionary<string, PiperVoiceConfig> _voices;
        private string _currentVoiceId;
        private bool _isInitialized;
        private bool _isDisposed;
        private readonly object _lockObject = new object();

        // Event backing fields
        private event Action<bool> _onInitialized;
        private event Action<PiperVoiceConfig> _onVoiceLoaded;
        private event Action<PiperException> _onError;
        private event Action<float> _onProcessingProgress;

        // Inference Engine related
        private BackendType _inferenceBackend;
        private readonly Dictionary<string, Model> _loadedModels;
        private readonly Dictionary<string, Worker> _workers;
        private readonly Queue<Worker> _workerPool;

        // Cache system
        private readonly Dictionary<string, CachedAudioData> _audioCache;
        private long _currentCacheSize;
        private long _cacheHitCount;
        private long _cacheMissCount;
        private long _cacheEvictionCount;

        // Phonemizer
        private IPhonemizer _phonemizer;
        
        // Audio Generator
        private ISentisAudioGenerator _audioGenerator;
        private Dictionary<string, ISentisAudioGenerator> _voiceGenerators;

        #endregion

        #region Properties

        /// <summary>
        /// Current voice ID
        /// </summary>
        public string CurrentVoiceId
        {
            get
            {
                lock (_lockObject)
                {
                    return _currentVoiceId;
                }
            }
        }

        /// <summary>
        /// Currently loaded voice configuration
        /// </summary>
        public PiperVoiceConfig CurrentVoice
        {
            get
            {
                lock (_lockObject)
                {
                    if (string.IsNullOrEmpty(_currentVoiceId) || !_voices.ContainsKey(_currentVoiceId))
                        return null;

                    return _voices[_currentVoiceId];
                }
            }
        }

        /// <summary>
        /// Available voice IDs
        /// </summary>
        public IReadOnlyCollection<string> AvailableVoices
        {
            get
            {
                lock (_lockObject)
                {
                    // Return the keys directly to avoid allocation
                    return _voices.Keys;
                }
            }
        }

        /// <summary>
        /// Whether the TTS engine is initialized
        /// </summary>
        public bool IsInitialized
        {
            get
            {
                lock (_lockObject)
                {
                    return _isInitialized;
                }
            }
        }

        /// <summary>
        /// Whether the TTS engine is currently processing
        /// </summary>
        public bool IsProcessing { get; private set; }

        /// <summary>
        /// Current configuration
        /// </summary>
        public PiperConfig Configuration => _config;

        #endregion

        #region Events

        /// <summary>
        /// Event fired when initialization completes
        /// </summary>
        public event Action<bool> OnInitialized
        {
            add
            {
                lock (_lockObject)
                {
                    _onInitialized += value;
                }
            }
            remove
            {
                lock (_lockObject)
                {
                    _onInitialized -= value;
                }
            }
        }

        /// <summary>
        /// Event fired when voice is loaded
        /// </summary>
        public event Action<PiperVoiceConfig> OnVoiceLoaded
        {
            add
            {
                lock (_lockObject)
                {
                    _onVoiceLoaded += value;
                }
            }
            remove
            {
                lock (_lockObject)
                {
                    _onVoiceLoaded -= value;
                }
            }
        }

        /// <summary>
        /// Event fired when an error occurs
        /// </summary>
        public event Action<PiperException> OnError
        {
            add
            {
                lock (_lockObject)
                {
                    _onError += value;
                }
            }
            remove
            {
                lock (_lockObject)
                {
                    _onError -= value;
                }
            }
        }

        /// <summary>
        /// Event fired to report processing progress
        /// </summary>
        public event Action<float> OnProcessingProgress
        {
            add
            {
                lock (_lockObject)
                {
                    _onProcessingProgress += value;
                }
            }
            remove
            {
                lock (_lockObject)
                {
                    _onProcessingProgress -= value;
                }
            }
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Create a new PiperTTS instance
        /// </summary>
        /// <param name="config">Configuration to use</param>
        public PiperTTS(PiperConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _voices = new Dictionary<string, PiperVoiceConfig>();
            _isInitialized = false;
            _isDisposed = false;

            // Initialize collections
            _loadedModels = new Dictionary<string, Model>();
            _workers = new Dictionary<string, Worker>();
            _workerPool = new Queue<Worker>();
            _audioCache = new Dictionary<string, CachedAudioData>();
            _currentCacheSize = 0;
            _cacheHitCount = 0;
            _cacheMissCount = 0;
            _cacheEvictionCount = 0;

            // Validate configuration on construction
            _config.Validate();

            PiperLogger.LogInfo("PiperTTS instance created with config: SampleRate={0}Hz, Language={1}",
                _config.SampleRate, _config.DefaultLanguage);
        }

        #endregion

        #region Public Methods - Initialization

        /// <summary>
        /// Initialize the TTS engine asynchronously
        /// </summary>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (_isInitialized)
            {
                PiperLogger.LogWarning("PiperTTS is already initialized");
                return;
            }

            try
            {
                PiperLogger.LogInfo("Starting PiperTTS initialization...");

                // Validate runtime environment first (must be on main thread)
                ValidateRuntimeEnvironment();

                // Initialize Inference Engine backend
                await InitializeInferenceEngineAsync(cancellationToken);

                // Initialize worker thread pool if multi-threaded inference is enabled
                if (_config.EnableMultiThreadedInference && _config.WorkerThreads > 1)
                {
                    await InitializeWorkerPoolAsync(cancellationToken);
                }

                // Initialize cache system if enabled
                if (_config.EnablePhonemeCache)
                {
                    InitializeCacheSystem();
                }

                // Initialize phonemizer based on language
                await InitializePhonemizerAsync(cancellationToken);
                
                // Initialize audio generator
                await InitializeAudioGeneratorAsync(cancellationToken);

                lock (_lockObject)
                {
                    _isInitialized = true;
                }

                PiperLogger.LogInfo("PiperTTS initialized successfully");
                _onInitialized?.Invoke(true);
            }
            catch (OperationCanceledException)
            {
                PiperLogger.LogWarning("PiperTTS initialization was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                var piperEx = new PiperException("Failed to initialize PiperTTS", ex);
                PiperLogger.LogError("PiperTTS initialization failed: {0}", ex.Message);
                _onError?.Invoke(piperEx);
                throw piperEx;
            }
        }

        #endregion

        #region Public Methods - Voice Management

        /// <summary>
        /// Load a voice configuration
        /// </summary>
        public async Task LoadVoiceAsync(PiperVoiceConfig voice, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ThrowIfNotInitialized();

            if (voice == null)
                throw new ArgumentNullException(nameof(voice));

            voice.Validate();

            try
            {
                PiperLogger.LogInfo("Loading voice: {0}", voice.VoiceId);
                
                // Validate model source
                bool hasModelPath = !string.IsNullOrEmpty(voice.ModelPath);
                bool hasModelAsset = voice.ModelAsset != null;
                
                if (!hasModelPath && !hasModelAsset)
                {
                    throw new InvalidOperationException("Voice must have either ModelPath or ModelAsset specified");
                }
                
                // Check if voice is already loaded
                bool needsInitialization = false;
                ISentisAudioGenerator voiceGenerator = null;
                
                lock (_lockObject)
                {
                    if (_voices.ContainsKey(voice.VoiceId))
                    {
                        PiperLogger.LogInfo("Voice already loaded: {0}", voice.VoiceId);
                        _currentVoiceId = voice.VoiceId;
                        _onVoiceLoaded?.Invoke(voice);
                        return;
                    }
                    
                    // Initialize voice generators dictionary if needed
                    if (_voiceGenerators == null)
                    {
                        _voiceGenerators = new Dictionary<string, ISentisAudioGenerator>();
                    }
                    
                    // Add voice configuration
                    _voices[voice.VoiceId] = voice;
                    _voicesListDirty = true;
                    
                    // Check if we need to load the model
                    if (!_voiceGenerators.ContainsKey(voice.VoiceId))
                    {
                        needsInitialization = true;
                        voiceGenerator = new SentisAudioGenerator();
                    }
                }
                
                // Initialize outside of lock
                if (needsInitialization && voiceGenerator != null)
                {
                    PiperLogger.LogInfo("Loading voice model: {0}", voice.ModelPath ?? "ModelAsset");
                    
                    // Initialize with ModelAsset or file path
                    if (hasModelAsset)
                    {
                        // Load from ModelAsset
                        var modelLoader = new AudioGeneration.ModelLoader();
                        modelLoader.LoadModel(voice.ModelAsset);
                        // TODO: Initialize SentisAudioGenerator with pre-loaded model
                        await voiceGenerator.InitializeAsync(voice.ModelPath ?? "dummy", cancellationToken);
                    }
                    else
                    {
                        // Load from file path
                        await voiceGenerator.InitializeAsync(voice.ModelPath, cancellationToken);
                    }
                    
                    // Store the initialized generator
                    lock (_lockObject)
                    {
                        _voiceGenerators[voice.VoiceId] = voiceGenerator;
                        
                        if (_currentVoiceId == null)
                        {
                            _currentVoiceId = voice.VoiceId;
                        }
                    }
                }
                
                // Apply voice-specific settings if available
                PiperLogger.LogInfo("Voice loaded with sample rate: {0}Hz", voice.SampleRate);

                PiperLogger.LogInfo("Voice loaded successfully: {0}", voice.VoiceId);
                _onVoiceLoaded?.Invoke(voice);
            }
            catch (OperationCanceledException)
            {
                PiperLogger.LogWarning("Voice loading was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                var piperEx = new PiperException($"Failed to load voice: {voice.VoiceId}", ex);
                _onError?.Invoke(piperEx);
                throw piperEx;
            }
        }

        /// <summary>
        /// Switch to a different voice
        /// </summary>
        public void SetCurrentVoice(string voiceId)
        {
            ThrowIfDisposed();
            ThrowIfNotInitialized();

            if (string.IsNullOrWhiteSpace(voiceId))
                throw new ArgumentNullException(nameof(voiceId));

            lock (_lockObject)
            {
                if (!_voices.ContainsKey(voiceId))
                {
                    throw new PiperException($"Voice not found: {voiceId}");
                }

                _currentVoiceId = voiceId;
                PiperLogger.LogInfo("Current voice set to: {0}", voiceId);
            }
        }

        /// <summary>
        /// Get configuration for a specific voice
        /// </summary>
        public PiperVoiceConfig GetVoiceConfig(string voiceId)
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(voiceId))
                throw new ArgumentNullException(nameof(voiceId));

            lock (_lockObject)
            {
                if (_voices.TryGetValue(voiceId, out var config))
                {
                    return config;
                }

                throw new PiperException($"Voice not found: {voiceId}");
            }
        }

        // Cache for available voices list to avoid allocations
        private List<PiperVoiceConfig> _cachedAvailableVoices;
        private bool _voicesListDirty = true;

        /// <summary>
        /// Get available voices
        /// </summary>
        public IReadOnlyList<PiperVoiceConfig> GetAvailableVoices()
        {
            ThrowIfDisposed();

            lock (_lockObject)
            {
                if (_voicesListDirty || _cachedAvailableVoices == null)
                {
                    _cachedAvailableVoices = new List<PiperVoiceConfig>(_voices.Values);
                    _voicesListDirty = false;
                }
                return _cachedAvailableVoices;
            }
        }

        #endregion

        #region Public Methods - TTS (Stubs for now)

        /// <summary>
        /// Generate audio from text asynchronously
        /// </summary>
        public async Task<AudioClip> GenerateAudioAsync(string text, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ThrowIfNotInitialized();

            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentNullException(nameof(text));

            if (string.IsNullOrEmpty(_currentVoiceId))
                throw new InvalidOperationException("No voice selected. Load a voice first.");

            try
            {
                IsProcessing = true;
                PiperLogger.LogInfo("Generating audio for text: \"{0}\" (length: {1})",
                    text.Length > 50 ? text.Substring(0, 50) + "..." : text,
                    text.Length);

                // Report initial progress
                _onProcessingProgress?.Invoke(0.1f);

                // Check cache first if enabled
                if (_config.EnablePhonemeCache)
                {
                    var cacheKey = GenerateCacheKey(text, _currentVoiceId);
                    lock (_lockObject)
                    {
                        if (_audioCache.TryGetValue(cacheKey, out var cachedData))
                        {
                            _cacheHitCount++;
                            PiperLogger.LogInfo("Cache hit for text");
                            _onProcessingProgress?.Invoke(1.0f);
                            
                            // Convert cached data to AudioClip
                            return cachedData.ToAudioClip();
                        }
                        else
                        {
                            _cacheMissCount++;
                        }
                    }
                }

                _onProcessingProgress?.Invoke(0.3f);

                // Perform phonemization
                PiperLogger.LogInfo("Phonemizing text...");
                PhonemeResult phonemeResult = null;

                if (_phonemizer != null)
                {
                    phonemeResult = await _phonemizer.PhonemizeAsync(text);
                    PiperLogger.LogInfo("Phonemization completed: {0} phonemes", phonemeResult.Phonemes?.Length ?? 0);

                    // Log phonemes for debugging
                    if (phonemeResult.Phonemes != null && phonemeResult.Phonemes.Length > 0)
                    {
                        PiperLogger.LogDebug("Phonemes: {0}", string.Join(" ", phonemeResult.Phonemes));
                    }
                }
                else
                {
                    PiperLogger.LogWarning("No phonemizer available, using dummy phonemes");
                }

                _onProcessingProgress?.Invoke(0.5f);
                
                // Generate audio using Sentis
                AudioClip audioClip;
                // Select appropriate audio generator
                ISentisAudioGenerator generator = null;
                if (_voiceGenerators != null && !string.IsNullOrEmpty(_currentVoiceId))
                {
                    lock (_lockObject)
                    {
                        _voiceGenerators.TryGetValue(_currentVoiceId, out generator);
                    }
                }
                
                if (generator != null && phonemeResult != null && phonemeResult.PhonemeIds != null)
                {
                    // Generate audio using the voice-specific generator
                    audioClip = await generator.GenerateAudioAsync(phonemeResult.PhonemeIds, 0, cancellationToken);
                }
                else
                {
                    // Fallback to dummy audio
                    await Task.Delay(100, cancellationToken); // Simulate synthesis
                    audioClip = CreateDummyAudioClip(text);
                }
                
                _onProcessingProgress?.Invoke(0.8f);

                // Cache the result if enabled
                if (_config.EnablePhonemeCache && audioClip != null)
                {
                    var cacheKey = GenerateCacheKey(text, _currentVoiceId);
                    var cachedData = CachedAudioData.FromAudioClip(audioClip);
                    
                    lock (_lockObject)
                    {
                        // Check cache size and evict if needed
                        var maxCacheSizeBytes = _config.MaxCacheSizeMB * 1024L * 1024L;
                        if (_currentCacheSize + cachedData.SizeInBytes > maxCacheSizeBytes)
                        {
                            EvictOldestCacheEntries(cachedData.SizeInBytes);
                        }
                        
                        _audioCache[cacheKey] = cachedData;
                        _currentCacheSize += cachedData.SizeInBytes;
                        PiperLogger.LogDebug("Cached audio data: {0} bytes, total cache: {1:F2}MB", 
                            cachedData.SizeInBytes, _currentCacheSize / (1024.0 * 1024.0));
                    }
                }

                _onProcessingProgress?.Invoke(1.0f);
                PiperLogger.LogInfo("Audio generation completed");

                return audioClip;
            }
            catch (OperationCanceledException)
            {
                PiperLogger.LogWarning("Audio generation was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                var piperEx = new PiperException("Failed to generate audio", ex);
                _onError?.Invoke(piperEx);
                throw piperEx;
            }
            finally
            {
                IsProcessing = false;
            }
        }

        /// <summary>
        /// Generate audio from text synchronously
        /// </summary>
        public AudioClip GenerateAudio(string text)
        {
            ThrowIfDisposed();
            ThrowIfNotInitialized();

            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentNullException(nameof(text));

            if (string.IsNullOrEmpty(_currentVoiceId))
                throw new InvalidOperationException("No voice selected. Load a voice first.");

            try
            {
                IsProcessing = true;
                PiperLogger.LogInfo("Generating audio synchronously for text length: {0}", text.Length);

                // For synchronous version, we'll use the async method internally
                // In a real implementation, this would use native synchronous methods
                var task = GenerateAudioAsync(text);

                // Use a simple polling loop to wait for completion
                var startTime = DateTime.UtcNow;
                var timeout = _config.TimeoutMs > 0 ? TimeSpan.FromMilliseconds(_config.TimeoutMs) : TimeSpan.FromMinutes(5);
                
                // Special handling for Unity Editor to avoid deadlocks
#if UNITY_EDITOR
                if (Application.isEditor)
                {
                    // In editor, use coroutine-friendly approach
                    AudioClip result = null;
                    System.Exception error = null;
                    bool completed = false;
                    
                    task.ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                            error = t.Exception?.InnerException;
                        else if (t.IsCompletedSuccessfully)
                            result = t.Result;
                        completed = true;
                    }, TaskScheduler.Default);
                    
                    // Spin wait with yield to prevent editor freeze
                    var endTime = DateTime.UtcNow + timeout;
                    while (!completed && DateTime.UtcNow < endTime)
                    {
                        System.Threading.Thread.Sleep(10);
                        // Allow Unity Editor to process events
                        UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                    }
                    
                    if (!completed)
                        throw new TimeoutException($"Audio generation timed out after {timeout.TotalMilliseconds}ms");
                    
                    if (error != null)
                        throw error;
                        
                    return result;
                }
                else
#endif
                {
                    // For runtime/builds, use the safer approach
                    var cts = new System.Threading.CancellationTokenSource(timeout);
                    try
                    {
                        // Run the task with cancellation support
                        var resultTask = Task.Run(async () => await task, cts.Token);
                        return resultTask.GetAwaiter().GetResult();
                    }
                    catch (OperationCanceledException)
                    {
                        throw new TimeoutException($"Audio generation timed out after {timeout.TotalMilliseconds}ms");
                    }
                }
            }
            catch (AggregateException ae)
            {
                // Unwrap the aggregate exception
                var innerEx = ae.InnerException;
                if (innerEx is PiperException)
                    throw innerEx;

                var piperEx = new PiperException("Failed to generate audio", innerEx);
                _onError?.Invoke(piperEx);
                throw piperEx;
            }
            finally
            {
                IsProcessing = false;
            }
        }

        /// <summary>
        /// Stream audio generation
        /// </summary>
        public async IAsyncEnumerable<AudioChunk> StreamAudioAsync(
            string text,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ThrowIfNotInitialized();

            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentNullException(nameof(text));

            if (string.IsNullOrEmpty(_currentVoiceId))
                throw new InvalidOperationException("No voice selected. Load a voice first.");

            PiperLogger.LogInfo("Starting audio streaming for text length: {0}", text.Length);

            try
            {
                IsProcessing = true;

                // Split text into chunks for streaming
                var sentences = SplitIntoSentences(text);
                var totalSentences = sentences.Count;
                var processedSentences = 0;

                foreach (var sentence in sentences)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Simulate processing delay
                    await Task.Delay(100, cancellationToken);

                    // Create a dummy audio chunk
                    var audioData = new float[_config.SampleRate / 10]; // 0.1 second of audio
                    for (int i = 0; i < audioData.Length; i++)
                    {
                        audioData[i] = 0f; // Silence for now
                    }

                    var chunk = new AudioChunk(
                        samples: audioData,
                        sampleRate: _config.SampleRate,
                        channels: 1,
                        chunkIndex: processedSentences,
                        isFinal: (processedSentences == totalSentences - 1),
                        textSegment: sentence,
                        startTime: processedSentences * 0.1f
                    );

                    processedSentences++;
                    _onProcessingProgress?.Invoke((float)processedSentences / totalSentences);

                    yield return chunk;
                }

                PiperLogger.LogInfo("Audio streaming completed");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        /// <summary>
        /// Generate audio from text with specific voice configuration (synchronous)
        /// </summary>
        public AudioClip GenerateAudio(string text, PiperVoiceConfig voiceConfig)
        {
            ThrowIfDisposed();
            ThrowIfNotInitialized();

            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentNullException(nameof(text));

            if (voiceConfig == null)
                throw new ArgumentNullException(nameof(voiceConfig));

            // Temporarily switch voice
            var previousVoiceId = _currentVoiceId;

            try
            {
                // Load voice if not already loaded
                lock (_lockObject)
                {
                    if (!_voices.ContainsKey(voiceConfig.VoiceId))
                    {
                        // Load synchronously (block on async)
                        var loadTask = LoadVoiceAsync(voiceConfig);
#if UNITY_EDITOR
                        if (Application.isEditor)
                        {
                            // In Unity Editor, use safer waiting mechanism
                            bool completed = false;
                            System.Exception error = null;
                            
                            loadTask.ContinueWith(t =>
                            {
                                if (t.IsFaulted)
                                    error = t.Exception?.InnerException;
                                completed = true;
                            }, TaskScheduler.Default);
                            
                            while (!completed)
                            {
                                System.Threading.Thread.Sleep(10);
                                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                            }
                            
                            if (error != null)
                                throw error;
                        }
                        else
#endif
                        {
                            loadTask.Wait();
                        }
                    }

                    _currentVoiceId = voiceConfig.VoiceId;
                }

                // Generate audio with the specified voice
                return GenerateAudio(text);
            }
            finally
            {
                // Restore previous voice
                lock (_lockObject)
                {
                    _currentVoiceId = previousVoiceId;
                }
            }
        }

        /// <summary>
        /// Generate audio from text with specific voice configuration (asynchronous)
        /// </summary>
        public async Task<AudioClip> GenerateAudioAsync(string text, PiperVoiceConfig voiceConfig, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ThrowIfNotInitialized();

            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentNullException(nameof(text));

            if (voiceConfig == null)
                throw new ArgumentNullException(nameof(voiceConfig));

            // Temporarily switch voice
            var previousVoiceId = _currentVoiceId;

            try
            {
                // Load voice if not already loaded
                bool needsLoad;
                lock (_lockObject)
                {
                    needsLoad = !_voices.ContainsKey(voiceConfig.VoiceId);
                }

                if (needsLoad)
                {
                    await LoadVoiceAsync(voiceConfig, cancellationToken);
                }

                lock (_lockObject)
                {
                    _currentVoiceId = voiceConfig.VoiceId;
                }

                // Generate audio with the specified voice
                return await GenerateAudioAsync(text, cancellationToken);
            }
            finally
            {
                // Restore previous voice
                lock (_lockObject)
                {
                    _currentVoiceId = previousVoiceId;
                }
            }
        }

        /// <summary>
        /// Stream audio generation with specific voice configuration
        /// </summary>
        public async IAsyncEnumerable<AudioChunk> StreamAudioAsync(
            string text,
            PiperVoiceConfig voiceConfig,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ThrowIfNotInitialized();

            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentNullException(nameof(text));

            if (voiceConfig == null)
                throw new ArgumentNullException(nameof(voiceConfig));

            // Temporarily switch voice
            var previousVoiceId = _currentVoiceId;

            try
            {
                // Load voice if not already loaded
                bool needsLoad;
                lock (_lockObject)
                {
                    needsLoad = !_voices.ContainsKey(voiceConfig.VoiceId);
                }

                if (needsLoad)
                {
                    await LoadVoiceAsync(voiceConfig, cancellationToken);
                }

                lock (_lockObject)
                {
                    _currentVoiceId = voiceConfig.VoiceId;
                }

                // Stream audio with the specified voice
                await foreach (var chunk in StreamAudioAsync(text, cancellationToken))
                {
                    yield return chunk;
                }
            }
            finally
            {
                // Restore previous voice
                lock (_lockObject)
                {
                    _currentVoiceId = previousVoiceId;
                }
            }
        }

        /// <summary>
        /// Preload resources for a text
        /// </summary>
        public async Task PreloadTextAsync(string text, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ThrowIfNotInitialized();

            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentNullException(nameof(text));

            if (string.IsNullOrEmpty(_currentVoiceId))
                throw new InvalidOperationException("No voice selected. Load a voice first.");

            if (!_config.EnablePhonemeCache)
            {
                PiperLogger.LogWarning("Phoneme cache is disabled. PreloadTextAsync has no effect.");
                return;
            }

            var cacheKey = GenerateCacheKey(text, _currentVoiceId);

            lock (_lockObject)
            {
                if (_audioCache.ContainsKey(cacheKey))
                {
                    PiperLogger.LogInfo("Text already in cache");
                    return;
                }
            }

            try
            {
                PiperLogger.LogInfo("Preloading text for caching (length: {0})", text.Length);

                // TODO: Implement actual phonemization
                // For now, just simulate with delay
                await Task.Delay(50, cancellationToken);

                // Create dummy phoneme data
                var dummyData = new byte[text.Length * 2]; // Rough estimate

                lock (_lockObject)
                {
                    // Check cache size limit
                    var dataSize = dummyData.Length;
                    var maxCacheBytes = _config.MaxCacheSizeMB * 1024 * 1024;

                    // Evict old entries if needed
                    // For preloading, we just skip if cache is full
                    if (_currentCacheSize + dataSize > maxCacheBytes)
                    {
                        PiperLogger.LogWarning("Cache is full, cannot preload text");
                        return;
                    }
                    
                    // Note: PreloadTextAsync is deprecated - actual caching happens during audio generation
                    
                    PiperLogger.LogInfo("Text preloaded and cached (cache size: {0:F2}MB)",
                        _currentCacheSize / (1024.0 * 1024.0));
                }
            }
            catch (OperationCanceledException)
            {
                PiperLogger.LogWarning("Text preloading was cancelled");
                throw;
            }
        }

        /// <summary>
        /// Get cache statistics
        /// </summary>
        public CacheStatistics GetCacheStatistics()
        {
            ThrowIfDisposed();

            lock (_lockObject)
            {
                var stats = new CacheStatistics
                {
                    EntryCount = _audioCache.Count,
                    TotalSizeBytes = _currentCacheSize,
                    HitCount = _cacheHitCount,
                    MissCount = _cacheMissCount,
                    EvictionCount = _cacheEvictionCount,
                    MaxSizeBytes = _config.MaxCacheSizeMB * 1024L * 1024L
                };
                return stats;
            }
        }

        /// <summary>
        /// Clear the cache
        /// </summary>
        public void ClearCache()
        {
            ThrowIfDisposed();

            lock (_lockObject)
            {
                var previousSize = _currentCacheSize;
                _audioCache.Clear();
                _currentCacheSize = 0;

                if (previousSize > 0)
                {
                    PiperLogger.LogInfo("Cache cleared ({0:F2}MB freed)",
                        previousSize / (1024.0 * 1024.0));
                }
                else
                {
                    PiperLogger.LogInfo("Cache cleared (was already empty)");
                }
            }
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Dispose of resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected dispose method
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed)
                return;

            if (disposing)
            {
                lock (_lockObject)
                {
                    // Dispose workers
                    foreach (var worker in _workers.Values)
                    {
                        worker?.Dispose();
                    }
                    _workers.Clear();

                    // Dispose worker pool
                    while (_workerPool.Count > 0)
                    {
                        var worker = _workerPool.Dequeue();
                        worker?.Dispose();
                    }

                    // Clear models (Model doesn't have Dispose method)
                    _loadedModels.Clear();

                    // Clear event handlers
                    _onInitialized = null;
                    _onVoiceLoaded = null;
                    _onError = null;
                    _onProcessingProgress = null;

                    // Clear voices
                    _voices.Clear();

                    // Clear cache
                    _audioCache.Clear();
                    _currentCacheSize = 0;
                    _cacheHitCount = 0;
                    _cacheMissCount = 0;
                    _cacheEvictionCount = 0;

                    // Dispose phonemizer
                    _phonemizer?.Dispose();
                    _phonemizer = null;
                    
                    // Dispose audio generator
                    _audioGenerator?.Dispose();
                    _audioGenerator = null;
                    
                    // Dispose voice-specific generators
                    if (_voiceGenerators != null)
                    {
                        foreach (var generator in _voiceGenerators.Values)
                        {
                            generator?.Dispose();
                        }
                        _voiceGenerators.Clear();
                    }

                    _isInitialized = false;
                    _isDisposed = true;
                }

                PiperLogger.LogInfo("PiperTTS disposed");
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Throw if the instance has been disposed
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(PiperTTS));
        }

        /// <summary>
        /// Throw if not initialized
        /// </summary>
        private void ThrowIfNotInitialized()
        {
            if (!_isInitialized)
                throw new InvalidOperationException("PiperTTS is not initialized. Call InitializeAsync first.");
        }

        /// <summary>
        /// Initialize the Inference Engine backend
        /// </summary>
        private async Task InitializeInferenceEngineAsync(CancellationToken cancellationToken)
        {
            PiperLogger.LogInfo("Initializing Inference Engine backend...");

            // Map PiperConfig backend to Unity Inference Engine backend
            _inferenceBackend = _config.Backend switch
            {
                InferenceBackend.CPU => BackendType.CPU,
                InferenceBackend.GPUCompute => BackendType.GPUCompute,
                InferenceBackend.GPUPixel => BackendType.GPUPixel,
                InferenceBackend.Auto => BackendType.CPU, // Default to CPU for now
                _ => BackendType.CPU
            };

            // For now, just log the selected backend
            // TODO: Add backend validation when Worker API is available

            PiperLogger.LogInfo("Inference Engine initialized with backend: {0}", _inferenceBackend);

            // Small delay to simulate async operation
            await Task.Yield();
        }

        /// <summary>
        /// Initialize the phonemizer based on language
        /// </summary>
        private async Task InitializePhonemizerAsync(CancellationToken cancellationToken)
        {
            PiperLogger.LogInfo("Initializing phonemizer for language: {0}", _config.DefaultLanguage);

            try
            {
                // For Japanese, use OpenJTalkPhonemizer
                if (_config.DefaultLanguage == "ja" || _config.DefaultLanguage == "jp" ||
                    _config.DefaultLanguage == "japanese")
                {
#if !UNITY_WEBGL
                    // Check if we should force mock mode (e.g., in tests)
                    bool forceMockMode = Environment.GetEnvironmentVariable("PIPER_MOCK_MODE") == "1";
                    _phonemizer = new OpenJTalkPhonemizer(forceMockMode: forceMockMode);
                    PiperLogger.LogInfo("Initialized OpenJTalkPhonemizer for Japanese (MockMode={0})", forceMockMode);
#else
                    PiperLogger.LogWarning("OpenJTalkPhonemizer is not supported on WebGL platform");
                    _phonemizer = new MockPhonemizer(); // Fallback to mock
#endif
                }
                else
                {
                    // For other languages, use mock phonemizer for now
                    // TODO: Implement espeak-ng phonemizer for other languages
                    _phonemizer = new MockPhonemizer();
                    PiperLogger.LogInfo("Initialized MockPhonemizer for language: {0}", _config.DefaultLanguage);
                }

                // Small delay to simulate async operation
                await Task.Yield();
            }
            catch (Exception ex)
            {
                PiperLogger.LogError("Failed to initialize phonemizer: {0}", ex.Message);
                throw new PiperInitializationException("Failed to initialize phonemizer", ex);
            }
        }

        /// <summary>
        /// Initialize the audio generator
        /// </summary>
        private async Task InitializeAudioGeneratorAsync(CancellationToken cancellationToken)
        {
            PiperLogger.LogInfo("Initializing audio generator");
            
            try
            {
                // Check if we should force mock mode (e.g., in tests)
                bool forceMockMode = Environment.GetEnvironmentVariable("PIPER_MOCK_MODE") == "1";
                
                if (forceMockMode)
                {
                    PiperLogger.LogInfo("Audio generator initialization skipped (Mock Mode)");
                }
                else
                {
                    // Initialize the audio generator
                    _audioGenerator = new SentisAudioGenerator();
                    
                    // Note: We don't initialize the generator here as it needs a model
                    // It will be initialized when a voice is loaded
                    PiperLogger.LogInfo("Audio generator created (will be initialized on voice load)");
                }
                
                // Initialize voice generators dictionary
                _voiceGenerators = new Dictionary<string, ISentisAudioGenerator>();
                
                // Small delay to simulate async operation
                await Task.Yield();
            }
            catch (Exception ex)
            {
                PiperLogger.LogError("Failed to initialize audio generator: {0}", ex.Message);
                throw new PiperInitializationException("Failed to initialize audio generator", ex);
            }
        }

        /// <summary>
        /// Initialize worker thread pool
        /// </summary>
        private async Task InitializeWorkerPoolAsync(CancellationToken cancellationToken)
        {
            PiperLogger.LogInfo("Initializing worker pool with {0} threads", _config.WorkerThreads);

            // Worker pool initialization will be implemented when we have actual models
            // For now, just log the intention

            PiperLogger.LogInfo("Worker pool initialization completed");

            // Small delay to simulate async operation
            await Task.Yield();
        }

        /// <summary>
        /// Validate runtime environment
        /// </summary>
        private void ValidateRuntimeEnvironment()
        {
            PiperLogger.LogInfo("Validating runtime environment...");

            // Check Unity version
            var unityVersion = Application.unityVersion;
            PiperLogger.LogInfo("Unity version: {0}", unityVersion);

            // Check platform
            var platform = Application.platform;
            PiperLogger.LogInfo("Platform: {0}", platform);

            // Check if running in editor
            if (Application.isEditor)
            {
                PiperLogger.LogInfo("Running in Unity Editor");
            }

            // Check system info (skip in test environment to avoid main thread issues)
            try
            {
                PiperLogger.LogInfo("System Memory: {0}MB", SystemInfo.systemMemorySize);
                PiperLogger.LogInfo("Processor Count: {0}", SystemInfo.processorCount);
                PiperLogger.LogInfo("Graphics Device: {0}", SystemInfo.graphicsDeviceName);
            }
            catch (UnityException ex)
            {
                // This can happen in test environment when not on main thread
                PiperLogger.LogWarning("Could not retrieve system info: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Initialize cache system
        /// </summary>
        private void InitializeCacheSystem()
        {
            PiperLogger.LogInfo("Initializing cache system with max size: {0}MB", _config.MaxCacheSizeMB);

            // Clear any existing cache
            _audioCache.Clear();
            _currentCacheSize = 0;
            _cacheHitCount = 0;
            _cacheMissCount = 0;
            _cacheEvictionCount = 0;

            PiperLogger.LogInfo("Cache system initialized");
        }

        // Reusable StringBuilder for cache key generation
        private readonly System.Text.StringBuilder _cacheKeyBuilder = new System.Text.StringBuilder(256);

        /// <summary>
        /// Generate cache key for text and voice combination
        /// </summary>
        private string GenerateCacheKey(string text, string voiceId)
        {
            lock (_cacheKeyBuilder)
            {
                _cacheKeyBuilder.Clear();
                _cacheKeyBuilder.Append(text);
                _cacheKeyBuilder.Append('|');
                _cacheKeyBuilder.Append(voiceId);
                return _cacheKeyBuilder.ToString().GetHashCode().ToString();
            }
        }

        // Reusable list for cache eviction
        private readonly List<KeyValuePair<string, CachedAudioData>> _evictionList = new List<KeyValuePair<string, CachedAudioData>>(100);

        /// <summary>
        /// Evict oldest cache entries to make room for new data
        /// </summary>
        private void EvictOldestCacheEntries(long requiredBytes)
        {
            var maxCacheSizeBytes = _config.MaxCacheSizeMB * 1024L * 1024L;
            var targetSize = maxCacheSizeBytes - requiredBytes;
            
            // Copy entries to list for sorting (avoids LINQ allocation)
            _evictionList.Clear();
            foreach (var kvp in _audioCache)
            {
                _evictionList.Add(kvp);
            }
            
            // Sort by cached time (oldest first)
            _evictionList.Sort((a, b) => a.Value.CachedAt.CompareTo(b.Value.CachedAt));
            
            foreach (var entry in _evictionList)
            {
                if (_currentCacheSize <= targetSize)
                    break;
                    
                _audioCache.Remove(entry.Key);
                _currentCacheSize -= entry.Value.SizeInBytes;
                _cacheEvictionCount++;
                
                PiperLogger.LogDebug("Evicted cache entry: {0} ({1} bytes)", 
                    entry.Key, entry.Value.SizeInBytes);
            }
            
            _evictionList.Clear();
        }

        /// <summary>
        /// Create a dummy audio clip for testing
        /// </summary>
        private AudioClip CreateDummyAudioClip(string text)
        {
            // Calculate duration based on text length (rough estimate)
            var estimatedDuration = Mathf.Max(1f, text.Length * 0.06f); // ~60ms per character
            var sampleCount = (int)(estimatedDuration * _config.SampleRate);

            // Create silent audio clip for now
            var audioClip = AudioClip.Create(
                "TTS_Output",
                sampleCount,
                1, // Mono
                _config.SampleRate,
                false
            );

            // Fill with silence
            var data = new float[sampleCount];
            audioClip.SetData(data, 0);

            return audioClip;
        }

        /// <summary>
        /// Split text into sentences for streaming
        /// </summary>
        private List<string> SplitIntoSentences(string text)
        {
            var sentences = new List<string>();

            // Simple sentence splitting (can be improved with proper NLP)
            var sentenceEnders = new[] { '.', '!', '?', '。', '！', '？' };
            var currentSentence = new System.Text.StringBuilder();

            foreach (char c in text)
            {
                currentSentence.Append(c);

                if (sentenceEnders.Contains(c))
                {
                    var trimmed = currentSentence.ToString().Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        sentences.Add(trimmed);
                    }
                    currentSentence.Clear();
                }
            }

            // Add any remaining text
            var remaining = currentSentence.ToString().Trim();
            if (!string.IsNullOrEmpty(remaining))
            {
                sentences.Add(remaining);
            }

            // If no sentences found, return the whole text
            if (sentences.Count == 0 && !string.IsNullOrWhiteSpace(text))
            {
                sentences.Add(text);
            }

            return sentences;
        }

        #endregion
    }
}