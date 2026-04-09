using System.Diagnostics;

namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// 音声合成リクエストを表す不変データオブジェクト。
    /// TTSSynthesisOrchestrator.SynthesizeAsync のパラメータを集約する。
    /// </summary>
    internal readonly struct SynthesisRequest
    {
        public readonly string[] Phonemes;

        /// <summary>
        /// Prosody flat array (stride=3): [a1_0, a2_0, a3_0, a1_1, a2_1, a3_1, ...].
        /// Length = Phonemes.Length * 3. Null when prosody is not available.
        /// </summary>
        public readonly int[] ProsodyFlat;

        public readonly float LengthScale;
        public readonly float NoiseScale;
        public readonly float NoiseW;
        public readonly int SpeakerId;
        public readonly int LanguageId;

        public SynthesisRequest(
            string[] phonemes,
            int[] prosodyFlat,
            float lengthScale,
            float noiseScale,
            float noiseW,
            int speakerId,
            int languageId)
        {
            Debug.Assert(
                prosodyFlat == null || prosodyFlat.Length == phonemes.Length * PhonemeEncoder.ProsodyStride,
                "ProsodyFlat length must be Phonemes.Length * ProsodyStride");

            Phonemes = phonemes;
            ProsodyFlat = prosodyFlat;
            LengthScale = lengthScale;
            NoiseScale = noiseScale;
            NoiseW = noiseW;
            SpeakerId = speakerId;
            LanguageId = languageId;
        }

        public bool HasProsody => ProsodyFlat != null;
    }
}