namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// 音声合成リクエストを表す不変データオブジェクト。
    /// TTSSynthesisOrchestrator.SynthesizeAsync のパラメータを集約する。
    /// </summary>
    internal readonly struct SynthesisRequest
    {
        public readonly string[] Phonemes;
        public readonly int[] ProsodyA1;
        public readonly int[] ProsodyA2;
        public readonly int[] ProsodyA3;
        public readonly float LengthScale;
        public readonly float NoiseScale;
        public readonly float NoiseW;
        public readonly int SpeakerId;
        public readonly int LanguageId;

        public SynthesisRequest(
            string[] phonemes,
            int[] prosodyA1,
            int[] prosodyA2,
            int[] prosodyA3,
            float lengthScale,
            float noiseScale,
            float noiseW,
            int speakerId,
            int languageId)
        {
            Phonemes = phonemes;
            ProsodyA1 = prosodyA1;
            ProsodyA2 = prosodyA2;
            ProsodyA3 = prosodyA3;
            LengthScale = lengthScale;
            NoiseScale = noiseScale;
            NoiseW = noiseW;
            SpeakerId = speakerId;
            LanguageId = languageId;
        }

        public bool HasProsody => ProsodyA1 != null;
    }
}
