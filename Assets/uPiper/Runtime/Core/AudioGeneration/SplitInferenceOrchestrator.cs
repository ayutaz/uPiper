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
    /// <remarks>
    /// 句分割時の durations 結合は TTSSynthesisOrchestrator が使用しないため除外。
    /// 将来の句単位タイミング対応では SplitInferenceResult 型を導入予定。
    /// </remarks>
    internal class SplitInferenceOrchestrator : ISplitInferenceOrchestrator
    {
        private readonly IInferenceAudioGenerator _generator;

        public SplitInferenceOrchestrator(IInferenceAudioGenerator generator)
        {
            _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        }

        /// <summary>
        /// 沈黙句分割付きで音声を生成する。
        /// 句ごとにIProgress&lt;float&gt;で進捗(0.0〜1.0)を報告する。
        /// </summary>
        /// <remarks>
        /// Caller owns and must Dispose the returned <see cref="InferenceOutput"/>.
        /// Durations is always <c>default</c> (not combined across phrases).
        /// </remarks>
        public async Task<InferenceOutput> GenerateWithSilenceSplitAsync(
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
            IProgress<float> progress = null,
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
                return new InferenceOutput(
                    new NativeArray<float>(0, Allocator.Persistent), default);
            }

            // Count non-empty phrases for accurate progress reporting
            var totalPhrases = 0;
            for (var i = 0; i < phrases.Count; i++)
            {
                var ph = phrases[i];
                if (ph.PhonemeIds != null && ph.PhonemeIds.Length > 0)
                    totalPhrases++;
            }

            var segments = new List<(NativeArray<float> Audio, int SilenceSamples)>();
            NativeArray<float> combinedAudio = default;
            try
            {
                var totalLength = 0;
                var completedPhrases = 0;

                for (var p = 0; p < phrases.Count; p++)
                {
                    var phrase = phrases[p];
                    if (phrase.PhonemeIds == null || phrase.PhonemeIds.Length == 0)
                        continue;

                    cancellationToken.ThrowIfCancellationRequested();

                    var phraseOutput = await _generator.GenerateAudioAsync(
                        phrase.PhonemeIds, phrase.ProsodyFlat,
                        lengthScale, noiseScale, noiseW,
                        speakerId, languageId,
                        cancellationToken);
                    var phraseAudio = phraseOutput.DetachAudio();
                    phraseOutput.Dispose();

                    segments.Add((phraseAudio, phrase.SilenceSamples));
                    totalLength += phraseAudio.Length + phrase.SilenceSamples;
                    completedPhrases++;

                    // Report per-phrase progress (0.0 to 1.0)
                    if (totalPhrases > 0)
                    {
                        progress?.Report((float)completedPhrases / totalPhrases);
                    }

                    PiperLogger.LogDebug(
                        $"[SplitInferenceOrchestrator] Phrase {completedPhrases}/{totalPhrases}: " +
                        $"{phraseAudio.Length} samples + {phrase.SilenceSamples} silence");
                }

                // ClearMemory ensures silence gaps are zero-initialized
                combinedAudio = new NativeArray<float>(
                    totalLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                var offset = 0;
                foreach (var (audio, silenceSamples) in segments)
                {
                    NativeArray<float>.Copy(audio, 0, combinedAudio, offset, audio.Length);
                    offset += audio.Length;
                    offset += silenceSamples; // Zero-initialized (silence)
                }

                PiperLogger.LogInfo(
                    $"[SplitInferenceOrchestrator] Silence split complete: " +
                    $"{totalLength} total samples ({segments.Count} phrases)");
                return new InferenceOutput(combinedAudio, default);
            }
            catch
            {
                if (combinedAudio.IsCreated)
                    combinedAudio.Dispose();
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
