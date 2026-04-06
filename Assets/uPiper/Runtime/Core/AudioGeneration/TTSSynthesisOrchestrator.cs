using System;
using System.Collections.Generic;
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

        public TTSSynthesisOrchestrator(
            IInferenceAudioGenerator generator,
            SplitInferenceOrchestrator splitOrchestrator,
            PhonemeEncoder phonemeEncoder,
            AudioClipBuilder audioClipBuilder)
        {
            _generator = generator ?? throw new ArgumentNullException(nameof(generator));
            _splitOrchestrator = splitOrchestrator ?? throw new ArgumentNullException(nameof(splitOrchestrator));
            _phonemeEncoder = phonemeEncoder ?? throw new ArgumentNullException(nameof(phonemeEncoder));
            _audioClipBuilder = audioClipBuilder ?? throw new ArgumentNullException(nameof(audioClipBuilder));
        }

        /// <summary>
        /// 音素列からAudioClipを生成する。
        /// prosodyA1がnullの場合はProsodyなしでエンコードする。
        /// </summary>
        /// <param name="phonemes">音素配列（音素文字列）</param>
        /// <param name="prosodyA1">ProsodyA1配列（nullでProsodyなし）</param>
        /// <param name="prosodyA2">ProsodyA2配列</param>
        /// <param name="prosodyA3">ProsodyA3配列</param>
        /// <param name="lengthScale">話速スケール</param>
        /// <param name="noiseScale">ノイズスケール</param>
        /// <param name="noiseW">ノイズ幅</param>
        /// <param name="speakerId">スピーカーID</param>
        /// <param name="languageId">言語ID</param>
        /// <param name="config">バリデーション済み設定（句分割設定を含む）</param>
        /// <param name="voiceConfig">音声設定（PhonemeIdMap参照用）</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        public async Task<AudioClip> SynthesizeAsync(
            string[] phonemes,
            int[] prosodyA1,
            int[] prosodyA2,
            int[] prosodyA3,
            float lengthScale,
            float noiseScale,
            float noiseW,
            int speakerId,
            int languageId,
            ValidatedPiperConfig config,
            PiperVoiceConfig voiceConfig,
            CancellationToken cancellationToken = default)
        {
            if (phonemes == null || phonemes.Length == 0)
                throw new ArgumentException("Phonemes cannot be null or empty.", nameof(phonemes));

            // 1. 音素をIDにエンコード（Prosodyあり/なし）
            int[] phonemeIds;
            int[] expandedA1 = null, expandedA2 = null, expandedA3 = null;

            if (prosodyA1 != null)
            {
                var encResult = _phonemeEncoder.EncodeWithProsody(phonemes, prosodyA1, prosodyA2, prosodyA3);
                phonemeIds = encResult.PhonemeIds;
                expandedA1 = encResult.ExpandedProsodyA1;
                expandedA2 = encResult.ExpandedProsodyA2;
                expandedA3 = encResult.ExpandedProsodyA3;
            }
            else
            {
                phonemeIds = _phonemeEncoder.Encode(phonemes);
            }

            // 2. 音声を生成（句分割あり/なし）
            var silenceParsed = config?.ParsedPhonemeSilence;
            var useSilenceSplit = config is { EnablePhonemeSilence: true }
                && silenceParsed?.Count > 0
                && voiceConfig?.PhonemeIdMap != null;

            float[] audioData;
            if (useSilenceSplit)
            {
                audioData = await _splitOrchestrator.GenerateWithSilenceSplitAsync(
                    phonemeIds, expandedA1, expandedA2, expandedA3,
                    (Dictionary<string, float>)silenceParsed,
                    voiceConfig.PhonemeIdMap,
                    _generator.SampleRate,
                    lengthScale, noiseScale, noiseW,
                    speakerId, languageId,
                    cancellationToken);
            }
            else
            {
                audioData = await _generator.GenerateAudioAsync(
                    phonemeIds, expandedA1, expandedA2, expandedA3,
                    lengthScale, noiseScale, noiseW,
                    speakerId, languageId,
                    cancellationToken);
            }

            // 3. 正規化してAudioClipを構築
            _audioClipBuilder.NormalizeAudioInPlace(audioData, 0.95f);
            var clip = _audioClipBuilder.BuildAudioClip(
                audioData,
                _generator.SampleRate,
                $"TTS_{System.DateTime.Now:yyyyMMddHHmmss}");

            PiperLogger.LogInfo(
                $"[TTSSynthesisOrchestrator] Synthesized {audioData.Length} samples " +
                $"from {phonemes.Length} phonemes");

            return clip;
        }
    }
}
