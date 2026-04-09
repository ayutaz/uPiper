using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using uPiper.Core.Logging;

namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// 沈黙句分割のオーケストレーションを担当する。
    /// 音素列を沈黙トークンの位置で分割し、句ごとに独立推論を行い、
    /// 句間にゼロサンプルの無音区間を挿入して結合する。
    /// </summary>
    internal class SplitInferenceOrchestrator
    {
        private readonly IInferenceAudioGenerator _generator;

        public SplitInferenceOrchestrator(IInferenceAudioGenerator generator)
        {
            _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        }

        /// <summary>
        /// 沈黙句分割付きで音声を生成する。
        /// </summary>
        /// <remarks>Caller owns and must Dispose the returned NativeArray.</remarks>
        public async Task<NativeArray<float>> GenerateWithSilenceSplitAsync(
            int[] phonemeIds,
            int[] prosodyFlat,
            IReadOnlyDictionary<string, float> phonemeSilence,
            IReadOnlyDictionary<string, int[]> phonemeIdMap,
            int sampleRate,
            float lengthScale = 1.0f,
            float noiseScale = 0.667f,
            float noiseW = 0.8f,
            int speakerId = 0,
            int languageId = 0,
            CancellationToken cancellationToken = default)
        {
            var phrases = PhonemeSilenceProcessor.SplitAtPhonemeSilence(
                phonemeIds, prosodyFlat,
                phonemeSilence, phonemeIdMap, sampleRate);

            PiperLogger.LogInfo(
                $"[SplitInferenceOrchestrator] Silence split: {phrases.Count} phrases from {phonemeIds.Length} phonemes");

            if (phrases.Count == 0)
            {
                PiperLogger.LogWarning("[SplitInferenceOrchestrator] No phrases after silence split");
                return new NativeArray<float>(0, Allocator.Persistent);
            }

            var segments = new List<(NativeArray<float> Audio, int SilenceSamples)>();
            NativeArray<float> combined = default;
            try
            {
                var totalLength = 0;

                for (var p = 0; p < phrases.Count; p++)
                {
                    var phrase = phrases[p];
                    if (phrase.PhonemeIds == null || phrase.PhonemeIds.Length == 0)
                        continue;

                    cancellationToken.ThrowIfCancellationRequested();

                    // ── Short text mitigation (per phrase) ──
                    var phrasePhonemeIds = phrase.PhonemeIds;
                    var phraseProsodyFlat = phrase.ProsodyFlat;
                    var phraseNoiseScale = noiseScale;
                    var phraseNoiseW = noiseW;
                    var originalCount = phrasePhonemeIds.Length;
                    var wasPadded = ShortTextProcessor.NeedsPadding(phrasePhonemeIds);

                    if (wasPadded)
                    {
                        (phrasePhonemeIds, phraseProsodyFlat) = ShortTextProcessor.PadPhonemeIds(
                            phrasePhonemeIds, phraseProsodyFlat);
                    }

                    if (originalCount < ShortTextProcessor.MinPhonemeIds)
                    {
                        (phraseNoiseScale, phraseNoiseW) = ShortTextProcessor.AdjustScales(
                            originalCount, phraseNoiseScale, phraseNoiseW);
                    }

                    var phraseAudio = await _generator.GenerateAudioAsync(
                        phrasePhonemeIds, phraseProsodyFlat,
                        lengthScale, phraseNoiseScale, phraseNoiseW,
                        speakerId, languageId,
                        cancellationToken);

                    if (wasPadded)
                    {
                        phraseAudio = ShortTextProcessor.TrimSilence(phraseAudio);
                    }

                    segments.Add((phraseAudio, phrase.SilenceSamples));
                    totalLength += phraseAudio.Length + phrase.SilenceSamples;

                    PiperLogger.LogDebug(
                        $"[SplitInferenceOrchestrator] Phrase {p + 1}/{phrases.Count}: " +
                        $"{phraseAudio.Length} samples + {phrase.SilenceSamples} silence");
                }

                // ClearMemory ensures silence gaps are zero-initialized
                combined = new NativeArray<float>(
                    totalLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                var offset = 0;
                foreach (var (audio, silenceSamples) in segments)
                {
                    NativeArray<float>.Copy(audio, 0, combined, offset, audio.Length);
                    offset += audio.Length;
                    offset += silenceSamples; // Zero-initialized (silence)
                }

                PiperLogger.LogInfo(
                    $"[SplitInferenceOrchestrator] Silence split complete: " +
                    $"{totalLength} total samples ({segments.Count} phrases)");
                return combined;
            }
            catch
            {
                if (combined.IsCreated)
                    combined.Dispose();
                throw;
            }
            finally
            {
                // Dispose per-phrase NativeArrays (ownership transferred to combined)
                foreach (var (audio, _) in segments)
                {
                    if (audio.IsCreated)
                        audio.Dispose();
                }
            }
        }
    }
}