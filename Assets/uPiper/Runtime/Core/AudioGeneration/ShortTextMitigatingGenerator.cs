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
        public IModelCapabilities Capabilities => _inner.Capabilities;

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

        public async Task<InferenceOutput> GenerateAudioAsync(
            int[] phonemeIds,
            int[] prosodyFlat = null,
            float lengthScale = 1.0f,
            float noiseScale = 0.667f,
            float noiseW = 0.8f,
            int speakerId = 0,
            int languageId = 0,
            CancellationToken cancellationToken = default)
        {
            if (phonemeIds == null || phonemeIds.Length == 0)
                throw new ArgumentException("phonemeIds must not be null or empty.", nameof(phonemeIds));

            var originalCount = phonemeIds.Length;
            var wasPadded = ShortTextProcessor.NeedsPadding(phonemeIds);

            int afterBos = 0;
            int beforeEos = 0;
            if (wasPadded)
            {
                var deficit = ShortTextProcessor.MinPhonemeIds - phonemeIds.Length;
                afterBos = deficit / 2;
                beforeEos = deficit - afterBos;

                (phonemeIds, prosodyFlat) = ShortTextProcessor.PadPhonemeIds(
                    phonemeIds, prosodyFlat);
            }

            if (originalCount < ShortTextProcessor.MinPhonemeIds)
            {
                (noiseScale, noiseW) = ShortTextProcessor.AdjustScales(
                    originalCount, noiseScale, noiseW);
            }

            var output = await _inner.GenerateAudioAsync(
                phonemeIds, prosodyFlat,
                lengthScale, noiseScale, noiseW,
                speakerId, languageId, cancellationToken);
            try
            {
                if (wasPadded)
                {
                    output = ProcessPaddedOutput(output, afterBos, beforeEos);
                }

                return output;
            }
            catch
            {
                output?.Dispose();
                throw;
            }
        }

        /// <summary>
        /// PAD 挿入後の推論結果を処理する。
        /// <see cref="InferenceOutput.DetachAudio"/>/<see cref="InferenceOutput.DetachDurations"/>
        /// で全所有権を先に移転し、元 <paramref name="output"/> を安全に Dispose してから
        /// Audio の無音トリムと Durations の PAD 除去を行う。
        /// </summary>
        private static InferenceOutput ProcessPaddedOutput(
            InferenceOutput output, int afterBos, int beforeEos)
        {
            // 1. 元の InferenceOutput から全リソースの所有権を移転
            var audio = output.DetachAudio();
            var durations = output.HasDurations
                ? output.DetachDurations()
                : default;
            output.Dispose(); // 空になった InferenceOutput を安全に Dispose

            // 2. Audio: 無音トリム / Durations: PAD 除去
            NativeArray<float> trimmedAudio = default;
            NativeArray<float> cleanedDurations = default;
            try
            {
                trimmedAudio = ShortTextProcessor.TrimSilence(audio);
                // TrimSilence が新配列を返した場合、audio は既に Dispose 済み
                // TrimSilence がトリム不要で元配列を返した場合、audio == trimmedAudio

                if (durations.IsCreated)
                {
                    cleanedDurations = RemovePadDurations(
                        durations, afterBos, beforeEos);
                    durations.Dispose();
                    durations = default;
                }

                return new InferenceOutput(trimmedAudio, cleanedDurations);
            }
            catch
            {
                // 例外時: 新規作成したリソースのみクリーンアップ
                // trimmedAudio が audio と同一（トリム不要）の場合は audio 経由で解放
                if (trimmedAudio.IsCreated)
                    trimmedAudio.Dispose();
                else if (audio.IsCreated)
                    audio.Dispose();
                if (cleanedDurations.IsCreated)
                    cleanedDurations.Dispose();
                if (durations.IsCreated)
                    durations.Dispose();
                throw;
            }
        }

        /// <summary>
        /// PAD 挿入位置に対応する durations エントリを除去する。
        /// </summary>
        private static NativeArray<float> RemovePadDurations(
            NativeArray<float> durations, int afterBos, int beforeEos)
        {
            var totalPadCount = afterBos + beforeEos;
            if (totalPadCount <= 0 || !durations.IsCreated)
                return durations;

            var originalLength = durations.Length;
            var newLength = originalLength - totalPadCount;
            if (newLength <= 0)
                return new NativeArray<float>(0, Allocator.Persistent);

            var cleaned = new NativeArray<float>(newLength, Allocator.Persistent);
            try
            {
                // BOS（インデックス 0）をコピー
                cleaned[0] = durations[0];

                // Body（PAD をスキップした本体部分）をコピー
                var bodyStart = 1 + afterBos;
                var bodyLength = originalLength - 1 - afterBos - beforeEos - 1;
                if (bodyLength > 0)
                {
                    NativeArray<float>.Copy(durations, bodyStart, cleaned, 1, bodyLength);
                }

                // EOS（最後の要素）をコピー
                cleaned[newLength - 1] = durations[originalLength - 1];

                return cleaned;
            }
            catch
            {
                if (cleaned.IsCreated)
                    cleaned.Dispose();
                throw;
            }
        }

        // ── Dispose: デコレータは inner を Dispose しない ──

        public void Dispose()
        {
            // デコレータは _inner の所有権を持たないため、Dispose しない。
            // _inner の Dispose は元の所有者（PiperTTS）が行う。
        }
    }
}