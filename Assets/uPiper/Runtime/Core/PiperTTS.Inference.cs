using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.InferenceEngine;
using UnityEngine;
using uPiper.Core.AudioGeneration;
using uPiper.Core.Logging;

namespace uPiper.Core
{
    /// <summary>
    /// PiperTTSのUnity.InferenceEngine統合拡張
    /// </summary>
    public partial class PiperTTS
    {
        private IInferenceAudioGenerator _inferenceGenerator;
        private SplitInferenceOrchestrator _splitOrchestrator;
        private PhonemeEncoder _phonemeEncoder;
        private AudioClipBuilder _audioClipBuilder;
        private TTSSynthesisOrchestrator _orchestrator;
        private ModelAsset _currentModelAsset;
        private PiperVoiceConfig _currentVoiceConfig;
        private Phonemizers.Multilingual.MultilingualPhonemizer _multilingualPhonemizer;

        /// <summary>
        /// Unity.InferenceEngineモデルを使用してTTSを初期化する
        /// </summary>
        /// <param name="modelAsset">ONNXモデルアセット</param>
        /// <param name="voiceConfig">音声設定</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        public async Task InitializeWithInferenceAsync(
            ModelAsset modelAsset,
            PiperVoiceConfig voiceConfig,
            CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(PiperTTS));

            if (modelAsset == null)
                throw new ArgumentNullException(nameof(modelAsset));

            if (voiceConfig == null)
                throw new ArgumentNullException(nameof(voiceConfig));

            try
            {
                PiperLogger.LogDebug($"Initializing PiperTTS with Inference model: {modelAsset.name}");

                // Run initialization validation
                var validationResult = InitializationValidator.ValidateForInference(
                    _config, (object)modelAsset, voiceConfig);
                HandleValidationResult(validationResult);

                // Initialize platform-specific audio session (iOS)
                InitializePlatformAudioSession();

                // 既存のリソースをクリーンアップ
                DisposeInferenceResources();

                // Load pua.json if available (before PhonemeEncoder uses _tokenMapper)
                await _tokenMapper.InitializeAsync(cancellationToken);

                // Inferenceコンポーネントを初期化
                _inferenceGenerator = new InferenceAudioGenerator();
                var mitigatingGenerator = new ShortTextMitigatingGenerator(_inferenceGenerator);
                _splitOrchestrator = new SplitInferenceOrchestrator(mitigatingGenerator);
                _phonemeEncoder = new PhonemeEncoder(voiceConfig, _tokenMapper);
                _audioClipBuilder = new AudioClipBuilder();
                _orchestrator = new TTSSynthesisOrchestrator(
                    mitigatingGenerator, _splitOrchestrator, _phonemeEncoder, _audioClipBuilder,
                    _validatedConfig, voiceConfig);
                _currentModelAsset = modelAsset;

                // Inferenceジェネレーターを初期化
                await _inferenceGenerator.InitializeAsync(modelAsset, voiceConfig, cancellationToken);

                // 音声設定を保存
                _currentVoiceId = voiceConfig.VoiceId;
                _currentVoiceConfig = voiceConfig;
                if (!_voices.ContainsKey(_currentVoiceId))
                {
                    _voices[_currentVoiceId] = voiceConfig;
                }

                // Auto-promote to multilingual mode when model supports multiple languages
                var isMultilingualModel = _currentVoiceConfig?.LanguageIdMap != null
                    && _currentVoiceConfig.LanguageIdMap.Count > 1;

                if (_config != null && (_config.AutoDetectLanguage || isMultilingualModel))
                {
                    DisposeMultilingualPhonemizer();
                    var supportedLanguages = _config.SupportedLanguages ?? new System.Collections.Generic.List<string> { "ja", "en" };
                    var handlers = new System.Collections.Generic.Dictionary<string, Phonemizers.Multilingual.Handlers.ILanguageG2PHandler>();
                    if (_phonemizer is Phonemizers.Implementations.DotNetG2PPhonemizer jaPhonemizer)
                        handlers["ja"] = new Phonemizers.Multilingual.Handlers.JapaneseG2PHandler(jaPhonemizer);

                    var phonemizerOptions = new Phonemizers.Multilingual.MultilingualPhonemizerOptions
                    {
                        Languages = supportedLanguages,
                        DefaultLatinLanguage = _config.DefaultLanguage ?? "en",
                        Handlers = handlers,
                    };
                    _multilingualPhonemizer = new Phonemizers.Multilingual.MultilingualPhonemizer(phonemizerOptions);
                    await _multilingualPhonemizer.InitializeAsync(cancellationToken);
                }

                _isInitialized = true;
                _onVoiceLoaded?.Invoke(voiceConfig);
                _onInitialized?.Invoke(true);

                PiperLogger.LogInfo($"PiperTTS initialized with Inference model: {modelAsset.name}");
            }
            catch (PiperException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var piperEx = new PiperException("Failed to initialize Inference", ex);
                _onError?.Invoke(piperEx);
                throw piperEx;
            }
        }

        /// <summary>
        /// Unity.InferenceEngineを使用してテキストから音声を生成する
        /// </summary>
        /// <param name="text">生成するテキスト</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>生成されたAudioClip</returns>
        [Obsolete("Use PhonemizeAsync() + SynthesizeAsync(SynthesisRequest) instead. Will be removed in v3.0.")]
        public async Task<AudioClip> GenerateAudioWithInferenceAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            return await GenerateAudioWithInferenceAsync(
                text,
                lengthScale: 1.0f,
                noiseScale: 0.667f,
                noiseW: 0.8f,
                cancellationToken);
        }

        /// <summary>
        /// Unity.InferenceEngineを使用してテキストから音声を生成する（詳細パラメータ付き）
        /// </summary>
        [Obsolete("Use PhonemizeAsync() + SynthesizeAsync(SynthesisRequest) instead. Will be removed in v3.0.")]
        public async Task<AudioClip> GenerateAudioWithInferenceAsync(
            string text,
            float lengthScale = 1.0f,
            float noiseScale = 0.667f,
            float noiseW = 0.8f,
            CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(PiperTTS));

            if (!_isInitialized || _inferenceGenerator == null || !_inferenceGenerator.IsInitialized)
                throw new InvalidOperationException("Inference is not initialized. Call InitializeWithInferenceAsync first.");

            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Text cannot be null or empty", nameof(text));

            try
            {
                _onProcessingProgress?.Invoke(0.1f);

                // テキストを音素に変換
                PiperLogger.LogDebug($"Converting text to phonemes: {text}");
                var phonemeResult = await GetPhonemesAsync(text, cancellationToken);
                if (phonemeResult == null || phonemeResult.Phonemes == null || phonemeResult.Phonemes.Length == 0)
                {
                    throw new PiperException("Failed to convert text to phonemes");
                }

                _onProcessingProgress?.Invoke(0.3f);

                _onProcessingProgress?.Invoke(0.5f);

                // エンコード〜AudioClip生成を一括
                var request = AudioGeneration.SynthesisRequest.CreateInternal(
                    phonemeResult.Phonemes,
                    phonemeResult.ProsodyFlat,
                    lengthScale, noiseScale, noiseW,
                    0, 0);
                var audioClip = await _orchestrator.SynthesizeAsync(request, cancellationToken);

                _onProcessingProgress?.Invoke(1.0f);
                PiperLogger.LogInfo($"Successfully generated audio for text: \"{text}\" ({audioClip.samples} samples)");
                return audioClip;
            }
            catch (Exception ex)
            {
                var piperEx = new PiperException($"Failed to generate audio for text: {text}", ex);
                _onError?.Invoke(piperEx);
                throw piperEx;
            }
        }

        /// <summary>
        /// Unity.InferenceEngineを使用してテキストから音声を生成する（言語自動検出対応）
        /// </summary>
        /// <param name="text">生成するテキスト</param>
        /// <param name="languageId">言語ID（0=ja, 1=en等。-1で自動検出）</param>
        /// <param name="speakerId">スピーカーID</param>
        /// <param name="lengthScale">話速スケール</param>
        /// <param name="noiseScale">ノイズスケール</param>
        /// <param name="noiseW">ノイズ幅</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        [Obsolete("Use PhonemizeAsync() + SynthesizeAsync(SynthesisRequest) instead. Will be removed in v3.0.")]
        public async Task<AudioClip> GenerateAudioWithMultilingualAsync(
            string text,
            int languageId = -1,
            int speakerId = 0,
            float lengthScale = 1.0f,
            float noiseScale = 0.667f,
            float noiseW = 0.8f,
            CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(PiperTTS));

            if (!_isInitialized || _inferenceGenerator == null || !_inferenceGenerator.IsInitialized)
                throw new InvalidOperationException("Inference is not initialized. Call InitializeWithInferenceAsync first.");

            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Text cannot be null or empty", nameof(text));

            try
            {
                _onProcessingProgress?.Invoke(0.1f);

                string[] phonemes;
                int[] prosodyFlat = null;
                int resolvedLanguageId = languageId >= 0 ? languageId : 0;

                // 多言語PhonemizerまたはデフォルトPhonemizerで音素化
                if (_multilingualPhonemizer != null && _multilingualPhonemizer.IsInitialized)
                {
                    PiperLogger.LogDebug($"[MultilingualTTS] Phonemizing with MultilingualPhonemizer: {text}");
                    var multiResult = await _multilingualPhonemizer.PhonemizeWithProsodyAsync(text, cancellationToken);
                    phonemes = multiResult.Phonemes;
                    prosodyFlat = multiResult.ProsodyFlat;

                    // 言語IDを自動解決
                    if (languageId < 0 && _inferenceGenerator.SupportsLanguageId)
                    {
                        var detectedLang = multiResult.DetectedPrimaryLanguage;
                        if (_currentVoiceConfig?.LanguageIdMap != null &&
                            _currentVoiceConfig.LanguageIdMap.TryGetValue(detectedLang, out var detectedId))
                        {
                            resolvedLanguageId = detectedId;
                            PiperLogger.LogDebug(
                                $"[MultilingualTTS] Resolved language '{detectedLang}' → lid={resolvedLanguageId}");
                        }
                        else
                        {
                            resolvedLanguageId = ResolveLanguageId(detectedLang);
                            PiperLogger.LogWarning(
                                $"[MultilingualTTS] Detected language '{detectedLang}' is not in " +
                                $"model's LanguageIdMap. Falling back to lid={resolvedLanguageId}. " +
                                $"Supported languages: " +
                                $"{string.Join(", ", _currentVoiceConfig?.LanguageIdMap?.Keys ?? Array.Empty<string>())}");
                        }
                    }
                }
                else
                {
                    // フォールバック: 通常の音素化（PhonemizeWithProsodyまたはGetPhonemesAsync）
                    PiperLogger.LogDebug($"[MultilingualTTS] Fallback to default phonemizer: {text}");
                    var phonemeResult = await GetPhonemesAsync(text, cancellationToken);
                    if (phonemeResult?.Phonemes == null || phonemeResult.Phonemes.Length == 0)
                        throw new PiperException("Failed to convert text to phonemes");
                    phonemes = phonemeResult.Phonemes;
                }

                _onProcessingProgress?.Invoke(0.3f);

                // 音素をIDにエンコード
                if (phonemes == null || phonemes.Length == 0)
                    throw new PiperException("No phonemes generated");

                _onProcessingProgress?.Invoke(0.5f);

                // エンコード〜AudioClip生成を一括
                var request = AudioGeneration.SynthesisRequest.CreateInternal(
                    phonemes,
                    prosodyFlat,
                    lengthScale, noiseScale, noiseW,
                    speakerId, resolvedLanguageId);
                var audioClip = await _orchestrator.SynthesizeAsync(request, cancellationToken);

                _onProcessingProgress?.Invoke(1.0f);
                PiperLogger.LogInfo($"[MultilingualTTS] Generated audio for: \"{text}\" (lid={resolvedLanguageId})");
                return audioClip;
            }
            catch (Exception ex)
            {
                var piperEx = new PiperException($"Failed to generate multilingual audio for text: {text}", ex);
                _onError?.Invoke(piperEx);
                throw piperEx;
            }
        }

        /// <summary>
        /// SynthesisRequestを直接指定して音声を生成する（低レベルAPI）。
        /// 音素列は事前に <see cref="PhonemizeAsync"/> または外部G2Pで取得・構築済みであること。
        /// </summary>
        /// <param name="request">音声合成リクエスト（音素・Prosody・合成パラメータを集約）。</param>
        /// <param name="cancellationToken">キャンセルトークン。</param>
        /// <returns>生成されたAudioClip。</returns>
        /// <exception cref="ObjectDisposedException">インスタンスがDispose済みの場合。</exception>
        /// <exception cref="InvalidOperationException">Inferenceが初期化されていない場合。</exception>
        public async Task<AudioClip> SynthesizeAsync(
            SynthesisRequest request,
            CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(PiperTTS));

            if (!_isInitialized || _orchestrator == null)
                throw new InvalidOperationException(
                    "Inference is not initialized. Call InitializeWithInferenceAsync first.");

            return await _orchestrator.SynthesizeAsync(request, cancellationToken);
        }

        /// <summary>
        /// テキストを音素化し、Prosody情報付きの結果を返す。
        /// 多言語Phonemizerが利用可能な場合はそちらを優先する。
        /// 結果は <see cref="SynthesisRequest.FromPhonemesWithProsody"/> でリクエスト構築に使用できる。
        /// </summary>
        /// <param name="text">音素化するテキスト。</param>
        /// <param name="cancellationToken">キャンセルトークン。</param>
        /// <returns>音素化結果（Phonemes, ProsodyFlat等）。</returns>
        /// <exception cref="ObjectDisposedException">インスタンスがDispose済みの場合。</exception>
        /// <exception cref="InvalidOperationException">Inferenceが初期化されていない場合。</exception>
        /// <exception cref="ArgumentException">テキストがnullまたは空の場合。</exception>
        public async Task<PhonemizeResult> PhonemizeAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(PiperTTS));

            if (!_isInitialized)
                throw new InvalidOperationException(
                    "Inference is not initialized. Call InitializeWithInferenceAsync first.");

            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Text cannot be null or empty.", nameof(text));

            // 多言語Phonemizerが利用可能な場合はそちらを優先
            if (_multilingualPhonemizer != null && _multilingualPhonemizer.IsInitialized)
            {
                var multiResult = await _multilingualPhonemizer.PhonemizeWithProsodyAsync(
                    text, cancellationToken);

                int resolvedLanguageId = 0;
                if (_inferenceGenerator != null && _inferenceGenerator.SupportsLanguageId)
                {
                    var detectedLang = multiResult.DetectedPrimaryLanguage;
                    if (_currentVoiceConfig?.LanguageIdMap != null &&
                        _currentVoiceConfig.LanguageIdMap.TryGetValue(detectedLang, out var detectedId))
                    {
                        resolvedLanguageId = detectedId;
                    }
                    else
                    {
                        resolvedLanguageId = ResolveLanguageId(detectedLang);
                        PiperLogger.LogWarning(
                            $"[PhonemizeAsync] Detected language '{detectedLang}' is not in " +
                            $"model's LanguageIdMap. Falling back to lid={resolvedLanguageId}.");
                    }
                }

                return new PhonemizeResult(
                    multiResult.Phonemes,
                    multiResult.ProsodyFlat,
                    multiResult.DetectedPrimaryLanguage ?? "ja",
                    resolvedLanguageId);
            }

            // フォールバック: 通常の音素化
            var phonemeResult = await GetPhonemesAsync(text, cancellationToken);
            return new PhonemizeResult(
                phonemeResult?.Phonemes,
                phonemeResult?.ProsodyFlat,
                phonemeResult?.Language ?? "ja",
                0);
        }

        /// <summary>
        /// 現在のInferenceモデルアセットを取得
        /// </summary>
        public ModelAsset CurrentModelAsset => _currentModelAsset;

        /// <summary>
        /// Inferenceが初期化されているかどうか
        /// </summary>
        public bool IsInferenceInitialized => _inferenceGenerator?.IsInitialized ?? false;

        private void DisposeInferenceResources()
        {
            _splitOrchestrator = null;
            _orchestrator = null;
            _inferenceGenerator?.Dispose();
            _inferenceGenerator = null;
            _phonemeEncoder = null;
            _audioClipBuilder = null;
            _currentModelAsset = null;
            DisposeMultilingualPhonemizer();
        }

        // 既存のDisposeメソッドを拡張
        private void DisposePartialInference()
        {
            DisposeInferenceResources();
        }

        private void DisposeMultilingualPhonemizer()
        {
            _multilingualPhonemizer?.Dispose();
            _multilingualPhonemizer = null;
        }
    }
}