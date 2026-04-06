using System;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly ValidatedPiperConfig _config;
        private readonly PiperVoiceConfig _voiceConfig;

        public TTSSynthesisOrchestrator(
            IInferenceAudioGenerator generator,
            SplitInferenceOrchestrator splitOrchestrator,
            PhonemeEncoder phonemeEncoder,
            AudioClipBuilder audioClipBuilder,
            ValidatedPiperConfig config,
            PiperVoiceConfig voiceConfig)
        {
            _generator = generator ?? throw new ArgumentNullException(nameof(generator));
            _splitOrchestrator = splitOrchestrator ?? throw new ArgumentNullException(nameof(splitOrchestrator));
            _phonemeEncoder = phonemeEncoder ?? throw new ArgumentNullException(nameof(phonemeEncoder));
            _audioClipBuilder = audioClipBuilder ?? throw new ArgumentNullException(nameof(audioClipBuilder));
            _config = config;
            _voiceConfig = voiceConfig;
        }

        /// <summary>
        /// 音素列からAudioClipを生成する。
        /// request.HasProsodyがfalseの場合はProsodyなしでエンコードする。
        /// </summary>
        /// <param name="request">音声合成リクエスト（音素・Prosody・合成パラメータを集約）</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        public async Task<AudioClip> SynthesizeAsync(
            SynthesisRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request.Phonemes == null || request.Phonemes.Length == 0)
                throw new ArgumentException("Phonemes cannot be null or empty.");

            // 1. 音素をIDにエンコード（Prosodyあり/なし）
            int[] phonemeIds;
            int[] expandedA1 = null, expandedA2 = null, expandedA3 = null;

            if (request.HasProsody)
            {
                var encResult = _phonemeEncoder.EncodeWithProsody(
                    request.Phonemes, request.ProsodyA1, request.ProsodyA2, request.ProsodyA3);
                phonemeIds = encResult.PhonemeIds;
                expandedA1 = encResult.ExpandedProsodyA1;
                expandedA2 = encResult.ExpandedProsodyA2;
                expandedA3 = encResult.ExpandedProsodyA3;
            }
            else
            {
                phonemeIds = _phonemeEncoder.Encode(request.Phonemes);
            }

            // 2. 音声を生成（句分割あり/なし）
            var silenceParsed = _config?.ParsedPhonemeSilence;
            var useSilenceSplit = _config is { EnablePhonemeSilence: true }
                && silenceParsed?.Count > 0
                && _voiceConfig?.PhonemeIdMap != null;

            float[] audioData;
            if (useSilenceSplit)
            {
                audioData = await _splitOrchestrator.GenerateWithSilenceSplitAsync(
                    phonemeIds, expandedA1, expandedA2, expandedA3,
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
                    phonemeIds, expandedA1, expandedA2, expandedA3,
                    request.LengthScale, request.NoiseScale, request.NoiseW,
                    request.SpeakerId, request.LanguageId,
                    cancellationToken);
            }

            // 3. 正規化してAudioClipを構築
            _audioClipBuilder.NormalizeAudioInPlace(audioData, 0.95f);
            var clip = _audioClipBuilder.BuildAudioClip(
                audioData,
                _generator.SampleRate,
                $"TTS_{System.Guid.NewGuid():N}");

            PiperLogger.LogInfo(
                $"[TTSSynthesisOrchestrator] Synthesized {audioData.Length} samples " +
                $"from {request.Phonemes.Length} phonemes");

            return clip;
        }
    }
}
