using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.InferenceEngine;

namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// <see cref="IInferenceAudioGenerator"/> のデコレータ。短テキスト緩和（Strategy A/B）を
    /// <see cref="GenerateAudioAsync"/> の前後に透過的に適用する。
    /// <see cref="TTSSynthesisOrchestrator"/>/<see cref="SplitInferenceOrchestrator"/> の
    /// 重複ロジックを排除する。
    /// </summary>
    /// <remarks>
    /// デコレータは <paramref name="inner"/> の所有権を持たない。
    /// <see cref="Dispose"/> は inner に委譲しない。
    /// </remarks>
    internal sealed class ShortTextMitigatingGenerator : IInferenceAudioGenerator
    {
        private readonly IInferenceAudioGenerator _inner;

        public ShortTextMitigatingGenerator(IInferenceAudioGenerator inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        // ── IInferenceAudioGenerator プロパティ委譲 ──

        public bool IsInitialized => _inner.IsInitialized;
        public int SampleRate => _inner.SampleRate;
        public bool SupportsProsody => _inner.SupportsProsody;
        public bool SupportsMultiSpeaker => _inner.SupportsMultiSpeaker;
        public bool SupportsLanguageId => _inner.SupportsLanguageId;

        // ── InitializeAsync 委譲 ──

        public Task InitializeAsync(
            ModelAsset modelAsset,
            PiperVoiceConfig config,
            CancellationToken cancellationToken = default)
            => _inner.InitializeAsync(modelAsset, config, cancellationToken);

        public Task InitializeAsync(
            ModelAsset modelAsset,
            PiperVoiceConfig config,
            PiperConfig piperConfig,
            CancellationToken cancellationToken = default)
            => _inner.InitializeAsync(modelAsset, config, piperConfig, cancellationToken);

        // ── GenerateAudioAsync: 短テキスト緩和を適用 ──

        public async Task<NativeArray<float>> GenerateAudioAsync(
            int[] phonemeIds,
            int[] prosodyFlat = null,
            float lengthScale = 1.0f,
            float noiseScale = 0.667f,
            float noiseW = 0.8f,
            int speakerId = 0,
            int languageId = 0,
            CancellationToken cancellationToken = default)
        {
            var originalCount = phonemeIds.Length;
            var wasPadded = ShortTextProcessor.NeedsPadding(phonemeIds);

            if (wasPadded)
            {
                (phonemeIds, prosodyFlat) = ShortTextProcessor.PadPhonemeIds(
                    phonemeIds, prosodyFlat);
            }

            if (originalCount < ShortTextProcessor.MinPhonemeIds)
            {
                (noiseScale, noiseW) = ShortTextProcessor.AdjustScales(
                    originalCount, noiseScale, noiseW);
            }

            var audio = await _inner.GenerateAudioAsync(
                phonemeIds, prosodyFlat,
                lengthScale, noiseScale, noiseW,
                speakerId, languageId, cancellationToken);

            if (wasPadded)
            {
                audio = ShortTextProcessor.TrimSilence(audio);
            }

            return audio;
        }

        // ── Dispose: デコレータは inner を Dispose しない ──

        public void Dispose()
        {
            // デコレータは _inner の所有権を持たないため、Dispose しない。
            // _inner の Dispose は元の所有者（PiperTTS）が行う。
        }
    }
}
