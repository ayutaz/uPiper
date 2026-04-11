using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.InferenceEngine;
using UnityEngine;
using uPiper.Core.AudioGeneration;
using uPiper.Core.Logging;
using uPiper.Core.Phonemizers;
using uPiper.Core.Phonemizers.Backend;
using uPiper.Core.Phonemizers.Implementations;

namespace uPiper.Core
{
    /// <summary>
    /// Main implementation of the Piper TTS interface
    /// </summary>
    public partial class PiperTTS : IPiperTTS
    {
        #region Fields

        private readonly PiperConfig _config;
        private IPiperConfigReadOnly _validatedConfig;
        private readonly Dictionary<string, PiperVoiceConfig> _voices;
        private string _currentVoiceId;
        private bool _isInitialized;
        private bool _isDisposed;
        private readonly object _lockObject = new();

        // Event backing fields
        private event Action<bool> _onInitialized;
        private event Action<PiperVoiceConfig> _onVoiceLoaded;
        private event Action<PiperException> _onError;
        private event Action<float> _onProcessingProgress;
        private event Action<string> _onLanguageDetected;
        private event Action<UnsupportedLanguageEventArgs> _onUnsupportedLanguageDetected;

        // Inference Engine related
        private BackendType _inferenceBackend;
        private readonly Dictionary<string, Model> _loadedModels;
        private readonly Dictionary<string, Worker> _workers;
        private readonly Queue<Worker> _workerPool;

        // Cache system
        private readonly Dictionary<string, byte[]> _phonemeCache;
        private long _currentCacheSize;
        private long _cacheHitCount;
        private long _cacheMissCount;
        private long _cacheEvictionCount;

        // Phonemizer
        private IPhonemizer _phonemizer;
        private readonly Phonemizers.Multilingual.PuaTokenMapper _tokenMapper = new();

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
        /// Available voice configurations.
        /// </summary>
        public IReadOnlyList<PiperVoiceConfig> AvailableVoices
        {
            get
            {
                lock (_lockObject)
                {
                    return new List<PiperVoiceConfig>(_voices.Values);
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
        /// Current configuration (defensive copy).
        /// 外部から内部状態を変更されないよう、アクセスごとにコピーを返す。
        /// </summary>
        public PiperConfig Configuration => _config?.Clone();

        /// <summary>
        /// Current cache size in bytes
        /// </summary>
        public long CurrentCacheSize => _currentCacheSize;

        /// <summary>
        /// Number of cache hits
        /// </summary>
        public long CacheHitCount => _cacheHitCount;

        /// <summary>
        /// Number of cache misses
        /// </summary>
        public long CacheMissCount => _cacheMissCount;

        /// <summary>
        /// Number of cache evictions
        /// </summary>
        public long CacheEvictionCount => _cacheEvictionCount;

        /// <summary>Audio synthesis cache hit count</summary>
        public long AudioCacheHitCount => _audioSynthesisCache?.HitCount ?? 0;

        /// <summary>Audio synthesis cache miss count</summary>
        public long AudioCacheMissCount => _audioSynthesisCache?.MissCount ?? 0;

        /// <summary>Audio synthesis cache entry count</summary>
        public int AudioCacheEntryCount => _audioSynthesisCache?.Count ?? 0;

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

        /// <inheritdoc/>
        public event Action<string> OnLanguageDetected
        {
            add
            {
                lock (_lockObject)
                {
                    _onLanguageDetected += value;
                }
            }
            remove
            {
                lock (_lockObject)
                {
                    _onLanguageDetected -= value;
                }
            }
        }

        /// <inheritdoc/>
        public event Action<UnsupportedLanguageEventArgs> OnUnsupportedLanguageDetected
        {
            add
            {
                lock (_lockObject)
                {
                    _onUnsupportedLanguageDetected += value;
                }
            }
            remove
            {
                lock (_lockObject)
                {
                    _onUnsupportedLanguageDetected -= value;
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
            _phonemeCache = new Dictionary<string, byte[]>();
            _currentCacheSize = 0;
            _cacheHitCount = 0;
            _cacheMissCount = 0;
            _cacheEvictionCount = 0;

            // Validate configuration on construction and create immutable snapshot
            _validatedConfig = _config.ToValidated(); // Validate()を内部で呼ぶ

            PiperLogger.LogInfo("PiperTTS instance created with config: SampleRate={0}Hz, Language={1}",
                _validatedConfig.Audio.SampleRate, _config.DefaultLanguage);
        }

        #endregion

        #region Static Factory Methods

        /// <summary>
        /// デフォルト設定でPiperTTSを作成・初期化し、利用可能なモデルを自動ロードする。
        /// </summary>
        public static async Task<PiperTTS> CreateAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await CreateAsync(PiperConfig.CreateDefault(), cancellationToken);
        }

        /// <summary>
        /// 指定した設定でPiperTTSを作成・初期化し、利用可能なモデルを自動ロードする。
        /// </summary>
        public static async Task<PiperTTS> CreateAsync(
            PiperConfig config,
            CancellationToken cancellationToken = default)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            cancellationToken.ThrowIfCancellationRequested();

            var tts = new PiperTTS(config);
            var success = false;
            try
            {
                await tts.InitializeAsync(cancellationToken);
                await tts.LoadDefaultVoiceAsync(cancellationToken);
                success = true;
                return tts;
            }
            finally
            {
                if (!success)
                {
                    tts.Dispose();
                }
            }
        }

        /// <summary>
        /// ScriptableObject設定アセットからPiperTTSを作成・初期化する。
        /// 内部でディープコピーを作成するため、アセットのオリジナルデータは変更されない。
        /// </summary>
        public static async Task<PiperTTS> CreateAsync(
            PiperConfigAsset configAsset,
            CancellationToken cancellationToken = default)
        {
            if (configAsset == null)
                throw new ArgumentNullException(nameof(configAsset));

            return await CreateAsync(configAsset.CreateRuntimeCopy(), cancellationToken);
        }

        /// <summary>
        /// 指定したボイス設定でPiperTTSを作成・初期化する。
        /// </summary>
        public static async Task<PiperTTS> CreateAsync(
            PiperConfig config,
            PiperVoiceConfig voiceConfig,
            CancellationToken cancellationToken = default)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            if (voiceConfig == null)
                throw new ArgumentNullException(nameof(voiceConfig));
            cancellationToken.ThrowIfCancellationRequested();

            var tts = new PiperTTS(config);
            var success = false;
            try
            {
                await tts.InitializeAsync(cancellationToken);
                await tts.LoadVoiceAsync(voiceConfig, cancellationToken);
                success = true;
                return tts;
            }
            finally
            {
                if (!success)
                {
                    tts.Dispose();
                }
            }
        }

        #endregion

        #region Public Methods - Initialization

        /// <summary>
        /// Initialize the TTS engine asynchronously
        /// </summary>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            if (_isInitialized)
            {
                PiperLogger.LogWarning("PiperTTS is already initialized");
                return;
            }

            try
            {
                PiperLogger.LogInfo("Starting PiperTTS initialization...");

                // Platform information summary
                LogPlatformInfo();

                // Run initialization validation
                var validationResult = InitializationValidator.ValidateForInitialize(_config);
                HandleValidationResult(validationResult);

                // Validate runtime environment first (must be on main thread)
                ValidateRuntimeEnvironment();

                // Initialize platform-specific audio session (iOS)
                InitializePlatformAudioSession();

                // Initialize Inference Engine backend
                await InitializeInferenceEngineAsync(cancellationToken);

                // Initialize worker thread pool if multi-threaded inference is enabled
                if (_validatedConfig.Performance.EnableMultiThreadedInference
                    && _validatedConfig.Performance.WorkerThreads > 1)
                {
                    await InitializeWorkerPoolAsync(cancellationToken);
                }

                // Initialize cache system if enabled
                if (_validatedConfig.Performance.EnablePhonemeCache)
                {
                    InitializeCacheSystem();
                }

                // Initialize phonemizer based on language
                await InitializePhonemizerAsync(cancellationToken);

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
            catch (PiperException)
            {
                // PiperInitializationException etc. — rethrow without wrapping
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
        /// Load a voice configuration.
        /// </summary>
        /// <remarks>
        /// カレントボイスが未設定の場合、最初にロードされたボイスが
        /// 自動的にカレントボイスとして選択される。
        /// </remarks>
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

                // Load model asset asynchronously to avoid blocking the main thread
                var modelPath = $"Models/{voice.VoiceId}";
                var request = Resources.LoadAsync<ModelAsset>(modelPath);
                // Use polling loop for Unity version compatibility (await ResourceRequest may not work in all versions)
                while (!request.isDone)
                {
                    await Task.Delay(1);
                }
                var modelAsset = request.asset as ModelAsset ?? throw new PiperException($"Model asset not found: {modelPath}");

                // Initialize audio generator if not already done
                _inferenceGenerator ??= new InferenceAudioGenerator();

                // Initialize the audio generator with the model and config
                // Note: IInferenceAudioGenerator interface only supports 3 parameters
                // The implementation will use default PiperConfig internally
                await _inferenceGenerator.InitializeAsync(modelAsset, voice, cancellationToken);
                PiperLogger.LogInfo("Audio generator initialized for voice: {0}", voice.VoiceId);

                // Initialize phoneme encoder
                _phonemeEncoder = new PhonemeEncoder(voice, _tokenMapper);
                PiperLogger.LogInfo("Phoneme encoder initialized with {0} phonemes", voice.PhonemeIdMap?.Count ?? 0);

                lock (_lockObject)
                {
                    _voices[voice.VoiceId] = voice;
                    _currentVoiceId ??= voice.VoiceId;
                }

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
        /// Resources/Models/ から利用可能なモデルを自動検出してロードする内部メソッド。
        /// </summary>
        private async Task LoadDefaultVoiceAsync(CancellationToken cancellationToken = default)
        {
            var modelAssets = Resources.LoadAll<ModelAsset>("Models");
            if (modelAssets == null || modelAssets.Length == 0)
            {
                throw new PiperException(
                    "No model assets found in Resources/Models/. " +
                    "Please import a voice model (.onnx) into Assets/uPiper/Resources/Models/.");
            }

            ModelAsset selectedModel = null;
            PiperVoiceConfig voiceConfig = null;

            foreach (var modelAsset in modelAssets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Try to load matching JSON config
                var configPath = $"Models/{modelAsset.name}.onnx";
                var configJson = Resources.Load<TextAsset>(configPath);

                voiceConfig = BuildVoiceConfigFromAsset(modelAsset, configJson);
                if (voiceConfig != null)
                {
                    selectedModel = modelAsset;
                    break;
                }
            }

            if (selectedModel == null || voiceConfig == null)
            {
                throw new PiperException(
                    $"Found {modelAssets.Length} model asset(s) in Resources/Models/, " +
                    "but none could be loaded. Ensure a matching .onnx.json config file exists.");
            }

            PiperLogger.LogInfo("Auto-selected default voice: {0}", voiceConfig.VoiceId);

            await InitializeWithInferenceAsync(selectedModel, voiceConfig, cancellationToken);
        }

        /// <summary>
        /// ModelAsset と オプションのJSON TextAsset から PiperVoiceConfig を構築する。
        /// </summary>
        private static PiperVoiceConfig BuildVoiceConfigFromAsset(ModelAsset modelAsset, TextAsset configJson)
        {
            if (modelAsset == null) return null;

            var voiceId = modelAsset.name;

            if (configJson != null)
            {
                try
                {
                    return ParseModelConfig(voiceId, configJson.text);
                }
                catch (Exception ex)
                {
                    PiperLogger.LogWarning(
                        "Failed to parse config for model '{0}': {1}. Trying next model.",
                        voiceId, ex.Message);
                    return null;
                }
            }

            PiperLogger.LogWarning(
                "No config JSON found for model '{0}'. Using default voice config.", voiceId);
            return PiperVoiceConfig.FromModelPath(voiceId, null);
        }

        /// <summary>
        /// モデルJSON設定ファイルをパースしてPiperVoiceConfigを構築する。
        /// 実装は <see cref="ModelConfigParser.Parse"/> に委譲。
        /// </summary>
        internal static PiperVoiceConfig ParseModelConfig(string voiceId, string json)
        {
            return ModelConfigParser.Parse(voiceId, json);
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

        /// <summary>
        /// Get available voices
        /// </summary>
        [Obsolete("Use AvailableVoices property instead. This method will be removed in v3.0.")]
        public IReadOnlyList<PiperVoiceConfig> GetAvailableVoices()
        {
            ThrowIfDisposed();
            return AvailableVoices;
        }

        /// <inheritdoc/>
        public IReadOnlyList<string> GetSupportedLanguages()
        {
            var languages = _config?.SupportedLanguages;
            if (languages != null && languages.Count > 0)
                return languages.AsReadOnly();
            // Fallback: derive from current voice
            var voiceLang = CurrentVoice?.Language;
            return voiceLang != null
                ? new System.Collections.Generic.List<string> { voiceLang }.AsReadOnly()
                : new System.Collections.Generic.List<string> { _config?.DefaultLanguage ?? "ja" }.AsReadOnly();
        }

        /// <inheritdoc/>
        public string DetectLanguage(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return _config?.DefaultLanguage ?? "ja";

            var languages = GetSupportedLanguages();
            // Resolve the default Latin language from supported languages.
            // DefaultLanguage may be non-Latin (e.g. "ja"), which would cause
            // Latin text to be misclassified. Pick the first supported Latin language instead.
            var defaultLatin = "en";
            for (var i = 0; i < languages.Count; i++)
            {
                if (Phonemizers.Multilingual.LanguageConstants.IsLatinLanguage(languages[i]))
                {
                    defaultLatin = languages[i];
                    break;
                }
            }
            var detector = new Phonemizers.Multilingual.UnicodeLanguageDetector(
                languages,
                defaultLatin);

            var segments = detector.SegmentText(text);
            if (segments.Count == 0)
                return _config?.DefaultLanguage ?? "ja";

            // Return the language with the most characters
            var langCounts = new System.Collections.Generic.Dictionary<string, int>();
            foreach (var (lang, seg) in segments)
            {
                if (!langCounts.ContainsKey(lang))
                    langCounts[lang] = 0;
                langCounts[lang] += seg.Length;
            }
            var primary = "ja";
            var max = 0;
            foreach (var kvp in langCounts)
            {
                if (kvp.Value > max)
                {
                    max = kvp.Value;
                    primary = kvp.Key;
                }
            }
            return primary;
        }

        /// <summary>
        /// Resolves a language code to a numeric language ID using the current voice config's LanguageIdMap.
        /// Falls back to DefaultLanguage's ID when the detected language is not in the map,
        /// rather than hardcoding 0 (Japanese).
        /// </summary>
        private int ResolveLanguageId(string languageCode)
        {
            if (_currentVoiceConfig?.LanguageIdMap != null &&
                _currentVoiceConfig.LanguageIdMap.TryGetValue(languageCode, out var lid))
            {
                return lid;
            }

            // Fallback: use DefaultLanguage's ID instead of hardcoded 0
            var defaultLang = _config?.DefaultLanguage ?? "ja";
            if (_currentVoiceConfig?.LanguageIdMap != null &&
                _currentVoiceConfig.LanguageIdMap.TryGetValue(defaultLang, out var defaultLid))
            {
                PiperLogger.LogWarning(
                    "Language '{0}' not found in LanguageIdMap, falling back to DefaultLanguage '{1}' (lid={2})",
                    languageCode, defaultLang, defaultLid);
                return defaultLid;
            }

            PiperLogger.LogWarning(
                "Language '{0}' not found in LanguageIdMap and DefaultLanguage '{1}' also missing, using lid=0",
                languageCode, defaultLang);
            return 0;
        }

        #endregion

        #region Public Methods - Phonemization

        /// <summary>
        /// Convert text to phonemes
        /// </summary>
        public async Task<PhonemeResult> GetPhonemesAsync(string text, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ThrowIfNotInitialized();

            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentNullException(nameof(text));

            if (_phonemizer == null)
            {
                PiperLogger.LogError(
                    "Phonemizer is not initialized. Ensure InitializeAsync or " +
                    "InitializeWithInferenceAsync has been called successfully.");
                throw new PiperPhonemizationException(
                    text,
                    CurrentVoice?.Language ?? "ja",
                    "Phonemizer is not initialized. Call InitializeAsync or " +
                    "InitializeWithInferenceAsync before calling GetPhonemesAsync.");
            }

            var language = CurrentVoice?.Language ?? "ja";
            return await _phonemizer.PhonemizeAsync(text, language, cancellationToken);
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

            // Unified pipeline: PhonemizeAsync → SynthesizeAsync via TTSSynthesisOrchestrator.
            // This ensures all paths benefit from ShortTextMitigatingGenerator and AudioNormalizer.
            try
            {
                IsProcessing = true;
                PiperLogger.LogInfo("Generating audio for text: \"{0}\" (length: {1})",
                    text.Length > 50 ? text[..50] + "..." : text,
                    text.Length);

                _onProcessingProgress?.Invoke(0.1f);

                var phonemizeResult = await PhonemizeAsync(text, cancellationToken);

                _onProcessingProgress?.Invoke(0.5f);

                var hasProsody = phonemizeResult.ProsodyFlat != null
                    && phonemizeResult.ProsodyFlat.Length > 0;
                var request = hasProsody
                    ? SynthesisRequest.FromPhonemesWithProsody(
                        phonemizeResult.Phonemes, phonemizeResult.ProsodyFlat,
                        languageId: phonemizeResult.ResolvedLanguageId)
                    : SynthesisRequest.FromPhonemes(
                        phonemizeResult.Phonemes,
                        languageId: phonemizeResult.ResolvedLanguageId);

                var audioClip = await SynthesizeAsync(request, cancellationToken);

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

        /// <inheritdoc/>
        public async Task<AudioClip> GenerateAudioAsync(
            string text,
            string language,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ThrowIfNotInitialized();

            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentNullException(nameof(text));

            // Resolve effective language
            var effectiveLang = language?.Trim().ToLowerInvariant() ?? string.Empty;
            if (effectiveLang == "auto" || string.IsNullOrEmpty(effectiveLang))
            {
                effectiveLang = DetectLanguage(text);
                _onLanguageDetected?.Invoke(effectiveLang);
            }

            // Resolve language ID from current voice config
            var languageId = -1;
            if (_currentVoiceConfig?.LanguageIdMap != null &&
                _currentVoiceConfig.LanguageIdMap.TryGetValue(effectiveLang, out var lid))
            {
                languageId = lid;
            }

            // Delegate to multilingual method if available, otherwise fall back
            if (IsInferenceInitialized)
            {
                return await GenerateAudioWithMultilingualAsync(
                    text,
                    languageId: languageId,
                    cancellationToken: cancellationToken);
            }

            // Non-inference fallback
            return await GenerateAudioAsync(text, cancellationToken);
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

            if (!_validatedConfig.Performance.EnablePhonemeCache)
            {
                PiperLogger.LogWarning("Phoneme cache is disabled. PreloadTextAsync has no effect.");
                return;
            }

            try
            {
                PiperLogger.LogInfo("Preloading text for caching (length: {0})", text.Length);

                // Perform actual phonemization which will cache the result in PhonemeCache.Instance
                if (_phonemizer != null)
                {
                    var result = await _phonemizer.PhonemizeAsync(text, _config.DefaultLanguage, cancellationToken);

                    if (result.Success)
                    {
                        PiperLogger.LogInfo("Text preloaded and cached ({0} phonemes)", result.Phonemes?.Length ?? 0);
                    }
                    else
                    {
                        PiperLogger.LogWarning("Failed to preload text: {0}", result.ErrorMessage);
                    }
                }
                else
                {
                    PiperLogger.LogWarning("No phonemizer available for preloading");
                }
            }
            catch (OperationCanceledException)
            {
                PiperLogger.LogWarning("Text preloading was cancelled");
                throw;
            }
        }

        /// <summary>
        /// Get cache statistics from PhonemeCache (LRU cache with accurate statistics)
        /// </summary>
        public CacheStatistics GetCacheStatistics()
        {
            ThrowIfDisposed();

            // Use PhonemeCache.Instance for accurate statistics
            var phonemeCacheStats = PhonemeCache.Instance.GetStatistics();

            var stats = new CacheStatistics
            {
                EntryCount = phonemeCacheStats.EntryCount,
                TotalSizeBytes = phonemeCacheStats.MemoryUsage,
                HitCount = phonemeCacheStats.HitCount,
                MissCount = phonemeCacheStats.MissCount,
                EvictionCount = phonemeCacheStats.EvictionCount,
                MaxSizeBytes = _validatedConfig.Performance.MaxCacheSizeMB * 1024L * 1024L
            };
            return stats;
        }

        /// <summary>
        /// Clear the cache (both local and PhonemeCache)
        /// </summary>
        public void ClearCache()
        {
            ThrowIfDisposed();

            // Clear PhonemeCache.Instance (primary cache)
            PhonemeCache.Instance.Clear();

            lock (_lockObject)
            {
                // Clear local cache for backwards compatibility
                _phonemeCache.Clear();
                _currentCacheSize = 0;

                // Clear audio synthesis cache
                _audioSynthesisCache?.Clear();

                PiperLogger.LogInfo("Cache cleared");
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
                    _onLanguageDetected = null;
                    _onUnsupportedLanguageDetected = null;

                    // Clear voices
                    _voices.Clear();

                    // Clear cache
                    _phonemeCache.Clear();
                    _currentCacheSize = 0;
                    _cacheHitCount = 0;
                    _cacheMissCount = 0;
                    _cacheEvictionCount = 0;

                    // Clear audio synthesis cache
                    _audioSynthesisCache?.Clear();
                    _audioSynthesisCache = null;

                    // Dispose phonemizer
                    _phonemizer?.Dispose();
                    _phonemizer = null;

                    // Dispose partial class resources
                    DisposePartialInference();

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
            _inferenceBackend = _validatedConfig.Inference.Backend switch
            {
                InferenceBackend.CPU => BackendType.CPU,
                InferenceBackend.GPUCompute => BackendType.GPUCompute,
                InferenceBackend.GPUPixel => BackendType.GPUPixel,
                InferenceBackend.Auto => BackendType.CPU, // Default to CPU for now
                _ => BackendType.CPU
            };

            // Backend validation is handled by InferenceAudioGenerator

            PiperLogger.LogInfo("Inference Engine initialized with backend: {0}", _inferenceBackend);

            // Log backend selection summary for diagnostics
            try
            {
                var platform = PlatformInfo.FromCurrentEnvironment();
                BackendSelector.LogSelectionSummary(
                    _validatedConfig.Inference.Backend, _inferenceBackend, platform);
            }
            catch (Exception ex)
            {
                PiperLogger.LogWarning(
                    "Could not log backend selection summary: {0}", ex.Message);
            }

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
                // For Japanese, use DotNetG2PPhonemizer (pure C#, no native plugin dependency)
                if (_config.DefaultLanguage == "ja" || _config.DefaultLanguage == "jp" ||
                    _config.DefaultLanguage == "japanese")
                {
#if UNITY_WEBGL && !UNITY_EDITOR
                    var webGlPhonemizer = new DotNetG2PPhonemizer();
                    await webGlPhonemizer.InitializeAsync(cancellationToken);
                    _phonemizer = webGlPhonemizer;
                    PiperLogger.LogInfo("Initialized DotNetG2PPhonemizer for Japanese (WebGL async)");
#else
                    _phonemizer = new DotNetG2PPhonemizer();
                    PiperLogger.LogInfo("Initialized DotNetG2PPhonemizer for Japanese");
#endif
                }
                else
                {
                    // For other languages, phonemizer is not available yet
                    // espeak-ng phonemizer will be implemented in a future phase
                    _phonemizer = null;
                    PiperLogger.LogWarning("No phonemizer available for language: {0}. Text-to-speech will use fallback mode.", _config.DefaultLanguage);
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
        /// Initialize worker thread pool
        /// </summary>
        private async Task InitializeWorkerPoolAsync(CancellationToken cancellationToken)
        {
            PiperLogger.LogInfo("Initializing worker pool with {0} threads", _validatedConfig.Performance.WorkerThreads);

            // Worker pool initialization will be implemented when we have actual models
            // For now, just log the intention

            PiperLogger.LogInfo("Worker pool initialization completed");

            // Small delay to simulate async operation
            await Task.Yield();
        }

        /// <summary>
        /// Initialize platform-specific audio session.
        /// On iOS, configures AVAudioSession for Playback category.
        /// On other platforms, this is a no-op.
        /// </summary>
        private static void InitializePlatformAudioSession()
        {
#if UNITY_IOS && !UNITY_EDITOR
            try
            {
                Platform.IOSAudioSessionHelper.Initialize();
                PiperLogger.LogInfo("iOS AVAudioSession initialized automatically");
            }
            catch (Exception ex)
            {
                PiperLogger.LogWarning(
                    $"Failed to initialize iOS AVAudioSession: {ex.Message}. " +
                    "Audio playback may not work correctly. " +
                    "If audio is silent, call IOSAudioSessionHelper.Initialize() manually before PiperTTS initialization.");
            }
#endif
        }

        /// <summary>
        /// Process validation result: throw on errors, log warnings.
        /// </summary>
        private void HandleValidationResult(InitializationValidationResult result)
        {
            if (result.HasWarnings)
            {
                PiperLogger.LogWarning(result.FormatWarningSummary());
            }

            if (result.HasErrors)
            {
                var errorSummary = result.FormatErrorSummary();
                PiperLogger.LogError(errorSummary);
                throw new PiperInitializationException(errorSummary, result);
            }
        }

        /// <summary>
        /// Log platform and hardware information for diagnostics.
        /// </summary>
        private static void LogPlatformInfo()
        {
            try
            {
                var platform = PlatformInfo.FromCurrentEnvironment();
                PiperLogger.LogInfo("[PiperTTS] Platform Summary:");
                PiperLogger.LogInfo("[PiperTTS]   Graphics: {0}", platform.GraphicsDeviceType);
                PiperLogger.LogInfo("[PiperTTS]   Compute Shaders: {0}", platform.SupportsComputeShaders);
                PiperLogger.LogInfo("[PiperTTS]   GPU Memory: {0} MB", platform.GraphicsMemorySize);
                if (platform.IsWebGL)
                    PiperLogger.LogInfo("[PiperTTS]   WebGL Mode: {0}",
                        platform.IsWebGPU ? "WebGPU" : "WebGL2");
                if (platform.IsMobile)
                    PiperLogger.LogInfo("[PiperTTS]   Mobile Platform: true");
            }
            catch (Exception ex)
            {
                // SystemInfo access can fail in test environments
                PiperLogger.LogWarning("[PiperTTS] Could not retrieve platform info: {0}",
                    ex.Message);
            }
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
            PiperLogger.LogInfo("Initializing cache system with max size: {0}MB", _validatedConfig.Performance.MaxCacheSizeMB);

            // Clear any existing cache
            _phonemeCache.Clear();
            _currentCacheSize = 0;
            _cacheHitCount = 0;
            _cacheMissCount = 0;
            _cacheEvictionCount = 0;

            // Initialize audio synthesis cache
            if (_validatedConfig.Performance.EnableAudioCache)
            {
                _audioSynthesisCache = new AudioGeneration.AudioSynthesisCache(
                    _validatedConfig.Performance.MaxAudioCacheEntries,
                    _validatedConfig.Performance.MaxCacheSizeMB);
                PiperLogger.LogInfo("Audio synthesis cache initialized (max {0} entries)",
                    _validatedConfig.Performance.MaxAudioCacheEntries);
            }
            else
            {
                _audioSynthesisCache = null;
            }

            PiperLogger.LogInfo("Cache system initialized");
        }

        #endregion
    }
}