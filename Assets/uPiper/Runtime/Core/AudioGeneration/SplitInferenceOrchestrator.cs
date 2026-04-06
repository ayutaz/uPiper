using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using uPiper.Core.Logging;

namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// 沈黙句分割のオーケストレーションを担当する。
    /// 音素列を沈黙トークンの位置で分割し、句ごとに独立推論を行い、
    /// 句間にゼロサンプルの無音区間を挿入して結合する。
    /// </summary>
    public class SplitInferenceOrchestrator
    {
        private readonly IInferenceAudioGenerator _generator;

        public SplitInferenceOrchestrator(IInferenceAudioGenerator generator)
        {
            _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        }

        /// <summary>
        /// 沈黙句分割付きで音声を生成する。
        /// </summary>
        public async Task<float[]> GenerateWithSilenceSplitAsync(
            int[] phonemeIds,
            int[] prosodyA1,
            int[] prosodyA2,
            int[] prosodyA3,
            IReadOnlyDictionary<string, float> phonemeSilence,
            Dictionary<string, int> phonemeIdMap,
            int sampleRate,
            float lengthScale = 1.0f,
            float noiseScale = 0.667f,
            float noiseW = 0.8f,
            int speakerId = 0,
            int languageId = 0,
            CancellationToken cancellationToken = default)
        {
            var phrases = PhonemeSilenceProcessor.SplitAtPhonemeSilence(
                phonemeIds, prosodyA1, prosodyA2, prosodyA3,
                (Dictionary<string, float>)phonemeSilence, phonemeIdMap, sampleRate);

            PiperLogger.LogInfo(
                $"[SplitInferenceOrchestrator] Silence split: {phrases.Count} phrases from {phonemeIds.Length} phonemes");

            if (phrases.Count == 0)
            {
                PiperLogger.LogWarning("[SplitInferenceOrchestrator] No phrases after silence split");
                return Array.Empty<float>();
            }

            var segments = new List<(float[] Audio, int SilenceSamples)>();
            var totalLength = 0;

            for (var p = 0; p < phrases.Count; p++)
            {
                var phrase = phrases[p];
                if (phrase.PhonemeIds == null || phrase.PhonemeIds.Length == 0)
                    continue;

                cancellationToken.ThrowIfCancellationRequested();

                var phraseAudio = await _generator.GenerateAudioAsync(
                    phrase.PhonemeIds,
                    phrase.ProsodyA1, phrase.ProsodyA2, phrase.ProsodyA3,
                    lengthScale, noiseScale, noiseW,
                    speakerId, languageId,
                    cancellationToken);

                segments.Add((phraseAudio, phrase.SilenceSamples));
                totalLength += phraseAudio.Length + phrase.SilenceSamples;

                PiperLogger.LogDebug(
                    $"[SplitInferenceOrchestrator] Phrase {p + 1}/{phrases.Count}: " +
                    $"{phraseAudio.Length} samples + {phrase.SilenceSamples} silence");
            }

            var result = new float[totalLength];
            var offset = 0;
            foreach (var (audio, silenceSamples) in segments)
            {
                Array.Copy(audio, 0, result, offset, audio.Length);
                offset += audio.Length;
                offset += silenceSamples; // Zero-initialized (silence)
            }

            PiperLogger.LogInfo(
                $"[SplitInferenceOrchestrator] Silence split complete: " +
                $"{totalLength} total samples ({segments.Count} phrases)");
            return result;
        }
    }
}