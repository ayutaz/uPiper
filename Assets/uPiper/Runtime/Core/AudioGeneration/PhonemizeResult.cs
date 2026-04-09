namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// <see cref="uPiper.Core.PiperTTS.PhonemizeAsync"/> の戻り値。
    /// 音素配列・Prosody情報・検出言語を保持する。
    /// <see cref="SynthesisRequest.FromPhonemes"/> または
    /// <see cref="SynthesisRequest.FromPhonemesWithProsody"/> でリクエスト構築に使用する。
    /// </summary>
    public sealed class PhonemizeResult
    {
        /// <summary>音素配列。</summary>
        public string[] Phonemes { get; }

        /// <summary>
        /// Prosodyフラット配列（stride=3）。Prosodyが利用不可の場合は null。
        /// </summary>
        public int[] ProsodyFlat { get; }

        /// <summary>検出された言語コード（例: "ja", "en"）。</summary>
        public string DetectedLanguage { get; }

        /// <summary>解決済み言語ID（モデルの LanguageIdMap に基づく）。</summary>
        public int ResolvedLanguageId { get; }

        /// <summary>Prosodyデータが利用可能かどうか。</summary>
        public bool HasProsody => ProsodyFlat != null;

        /// <summary>
        /// PhonemizeResult を構築する。
        /// </summary>
        internal PhonemizeResult(string[] phonemes, int[] prosodyFlat, string detectedLanguage, int resolvedLanguageId)
        {
            Phonemes = phonemes;
            ProsodyFlat = prosodyFlat;
            DetectedLanguage = detectedLanguage;
            ResolvedLanguageId = resolvedLanguageId;
        }
    }
}