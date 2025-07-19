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
        private PhonemeEncoder _phonemeEncoder;
        private AudioClipBuilder _audioClipBuilder;
        private ModelAsset _currentModelAsset;

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

                // 既存のリソースをクリーンアップ
                DisposeInferenceResources();

                // Inferenceコンポーネントを初期化
                _inferenceGenerator = new InferenceAudioGenerator();
                _phonemeEncoder = new PhonemeEncoder(voiceConfig);
                _audioClipBuilder = new AudioClipBuilder();
                _currentModelAsset = modelAsset;

                // Inferenceジェネレーターを初期化
                await _inferenceGenerator.InitializeAsync(modelAsset, voiceConfig, cancellationToken);

                // 音声設定を保存
                _currentVoiceId = voiceConfig.VoiceId;
                if (!_voices.ContainsKey(_currentVoiceId))
                {
                    _voices[_currentVoiceId] = voiceConfig;
                }

                _isInitialized = true;
                _onVoiceLoaded?.Invoke(voiceConfig);
                _onInitialized?.Invoke(true);

                PiperLogger.LogInfo($"PiperTTS initialized with Inference model: {modelAsset.name}");
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

                // 音素をIDにエンコード
                PiperLogger.LogDebug($"Encoding {phonemeResult.Phonemes.Length} phonemes");
                var phonemeIds = _phonemeEncoder.Encode(phonemeResult.Phonemes);

                _onProcessingProgress?.Invoke(0.5f);

                // Unity.InferenceEngineで音声を生成
                PiperLogger.LogDebug("Generating audio with Inference");
                var audioData = await _inferenceGenerator.GenerateAudioAsync(
                    phonemeIds,
                    lengthScale,
                    noiseScale,
                    noiseW,
                    cancellationToken);

                _onProcessingProgress?.Invoke(0.8f);

                // AudioClipを作成
                var normalizedAudio = _audioClipBuilder.NormalizeAudio(audioData, 0.95f);
                var audioClip = _audioClipBuilder.BuildAudioClip(
                    normalizedAudio,
                    _inferenceGenerator.SampleRate,
                    $"TTS_{DateTime.Now:yyyyMMddHHmmss}");

                _onProcessingProgress?.Invoke(1.0f);

                PiperLogger.LogInfo($"Successfully generated audio for text: \"{text}\" ({audioData.Length} samples)");
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
        /// 現在のInferenceモデルアセットを取得
        /// </summary>
        public ModelAsset CurrentModelAsset => _currentModelAsset;

        /// <summary>
        /// Inferenceが初期化されているかどうか
        /// </summary>
        public bool IsInferenceInitialized => _inferenceGenerator?.IsInitialized ?? false;

        private void DisposeInferenceResources()
        {
            _inferenceGenerator?.Dispose();
            _inferenceGenerator = null;
            _phonemeEncoder = null;
            _audioClipBuilder = null;
            _currentModelAsset = null;
        }

        // 既存のDisposeメソッドを拡張
        private void DisposePartialInference()
        {
            DisposeInferenceResources();
        }
    }
}