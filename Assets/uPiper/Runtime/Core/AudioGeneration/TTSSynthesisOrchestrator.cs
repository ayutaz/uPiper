using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using uPiper.Core.Logging;

namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// 音素列→AudioClip変換パイプラインを一元管理する。
    /// PhonemeEncoder（エンコード）・IInferenceAudioGenerator（推論）・
    /// SplitInferenceOrchestrator（句分割）・AudioClipBuilder（AudioClip構築）を組み合わせる。
    /// PiperTTS.Inference.csの2メソッドにあった重複ロジックを排除する。
    /// </summary>
    internal sealed class TTSSynthesisOrchestrator
    {
        private readonly IInferenceAudioGenerator _generator;
        private readonly SplitInferenceOrchestrator _splitOrchestrator;
        private readonly PhonemeEncoder _phonemeEncoder;
        private readonly AudioClipBuilder _audioClipBuilder;
        private readonly IPiperConfigReadOnly _config;
        private readonly PiperVoiceConfig _voiceConfig;

        /// <summary>
        /// Initializes the orchestrator with the specified dependencies.
        /// </summary>
        /// <param name="generator">ONNX inference audio generator (required).</param>
        /// <param name="splitOrchestrator">Silence-based split inference orchestrator (required).</param>
        /// <param name="phonemeEncoder">Phoneme-to-ID encoder (required).</param>
        /// <param name="audioClipBuilder">Audio clip builder (required).</param>
        /// <param name="config">Optional validated config. When null, defaults to:
        /// audio normalization ON (0.95 peak), silence-based splitting OFF.
        /// This allows SynthesizeAsync to work without a PiperConfig when only basic
        /// synthesis is needed.</param>
        /// <param name="voiceConfig">Voice config with phoneme ID map (optional, required for silence split).</param>
        public TTSSynthesisOrchestrator(
            IInferenceAudioGenerator generator,
            SplitInferenceOrchestrator splitOrchestrator,
            PhonemeEncoder phonemeEncoder,
            AudioClipBuilder audioClipBuilder,
            IPiperConfigReadOnly config,
            PiperVoiceConfig voiceConfig)
        {
            _generator = generator ?? throw new ArgumentNullException(nameof(generator));
            _splitOrchestrator = splitOrchestrator ?? throw new ArgumentNullException(nameof(splitOrchestrator));
            _phonemeEncoder = phonemeEncoder ?? throw new ArgumentNullException(nameof(phonemeEncoder));
            _audioClipBuilder = audioClipBuilder ?? throw new ArgumentNullException(nameof(audioClipBuilder));
            _config = config; // nullable: defaults to normalization ON, silence split OFF
            _voiceConfig = voiceConfig;
        }

        /// <summary>
        /// 音素列からAudioClipを生成する。
        /// request.HasProsodyがfalseの場合はProsodyなしでエンコードする。
        /// NativeArrayの最終Dispose地点。AudioClip.SetData完了後にDisposeする。
        /// </summary>
        /// <param name="request">音声合成リクエスト（音素・Prosody・合成パラメータを集約）</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <remarks>Must be called from the main thread.</remarks>
        public async Task<AudioClip> SynthesizeAsync(
            SynthesisRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request.Phonemes == null || request.Phonemes.Length == 0)
                throw new ArgumentException("Phonemes cannot be null or empty.");

            // 1. 音素をIDにエンコード（Prosodyあり/なし）
            int[] phonemeIds;
            int[] expandedProsodyFlat = null;

            if (request.HasProsody)
            {
                var encResult = _phonemeEncoder.EncodeWithProsody(
                    request.Phonemes, request.ProsodyFlat);
                phonemeIds = encResult.PhonemeIds;
                expandedProsodyFlat = encResult.ExpandedProsodyFlat;
            }
            else
            {
                phonemeIds = _phonemeEncoder.Encode(request.Phonemes);
            }

            // 2. 音声を生成（句分割あり/なし）
            var silenceParsed = _config?.Silence.ParsedPhonemeSilence;
            var useSilenceSplit = _config != null
                && _config.Silence.EnablePhonemeSilence
                && silenceParsed?.Count > 0
                && _voiceConfig?.PhonemeIdMap != null;

            NativeArray<float> audioData = default;
            try
            {
                if (useSilenceSplit)
                {
                    audioData = await _splitOrchestrator.GenerateWithSilenceSplitAsync(
                        phonemeIds, expandedProsodyFlat,
                        silenceParsed,
                        _voiceConfig.PhonemeIdMap,
                        _generator.SampleRate,
                        request.LengthScale, request.NoiseScale, request.NoiseW,
                        request.SpeakerId, request.LanguageId,
                        cancellationToken);
                }
                else
                {
                    audioData = await _generator.GenerateAudioAsync(
                        phonemeIds, expandedProsodyFlat,
                        request.LengthScale, request.NoiseScale, request.NoiseW,
                        request.SpeakerId, request.LanguageId,
                        cancellationToken);
                }

                // 3. 正規化してAudioClipを構築（設定で無効化可能、後方互換のためnull時は正規化）
                if (_config == null || _config.Audio.NormalizeAudio)
                {
                    AudioNormalizer.NormalizeInPlace(audioData, 0.95f);
                }
                var clip = _audioClipBuilder.BuildAudioClip(
                    audioData,
                    _generator.SampleRate,
                    $"TTS_{Guid.NewGuid():N}");

                PiperLogger.LogInfo(
                    $"[TTSSynthesisOrchestrator] Synthesized {audioData.Length} samples " +
                    $"from {request.Phonemes.Length} phonemes");

                return clip;
            }
            finally
            {
                // NativeArray の最終 Dispose 地点。
                // AudioClip.SetData は NativeArray 内容をコピー済みのため、Dispose しても安全。
                if (audioData.IsCreated)
                    audioData.Dispose();
            }
        }
    }
}