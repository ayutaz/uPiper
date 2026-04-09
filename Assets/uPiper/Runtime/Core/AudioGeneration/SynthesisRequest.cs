using System;
using System.Diagnostics;

namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// 音声合成リクエストを表す不変データオブジェクト。
    /// 音素列と合成パラメータを集約する。
    /// 外部からはファクトリメソッド <see cref="FromPhonemes"/> / <see cref="FromPhonemesWithProsody"/>
    /// 経由で構築する。
    /// </summary>
    /// <example>
    /// <code>
    /// // 音素直接入力（Prosodyなし）
    /// var request = SynthesisRequest.FromPhonemes(
    ///     new[] { "k", "o", "N_uvular", "n", "i", "ch", "w", "a" },
    ///     lengthScale: 0.9f);
    /// var clip = await piperTTS.SynthesizeAsync(request);
    ///
    /// // PhonemizeAsync経由でProsody付きリクエストを構築
    /// var result = await piperTTS.PhonemizeAsync("こんにちは");
    /// var request2 = SynthesisRequest.FromPhonemesWithProsody(
    ///     result.Phonemes, result.ProsodyFlat, lengthScale: 0.8f);
    /// var clip2 = await piperTTS.SynthesizeAsync(request2);
    /// </code>
    /// </example>
    public readonly struct SynthesisRequest
    {
        /// <summary>音素配列。</summary>
        public string[] Phonemes { get; }

        /// <summary>
        /// Prosody flat array (stride=3): [a1_0, a2_0, a3_0, a1_1, a2_1, a3_1, ...].
        /// Length = Phonemes.Length * 3. Null when prosody is not available.
        /// </summary>
        public int[] ProsodyFlat { get; }

        /// <summary>話速スケール（デフォルト: 1.0）。</summary>
        public float LengthScale { get; }

        /// <summary>ノイズスケール（デフォルト: 0.667）。</summary>
        public float NoiseScale { get; }

        /// <summary>ノイズ幅（デフォルト: 0.8）。</summary>
        public float NoiseW { get; }

        /// <summary>スピーカーID（デフォルト: 0）。</summary>
        public int SpeakerId { get; }

        /// <summary>言語ID（デフォルト: 0）。</summary>
        public int LanguageId { get; }

        /// <summary>Prosodyデータが利用可能かどうか。</summary>
        public bool HasProsody => ProsodyFlat != null;

        /// <summary>
        /// 内部コンストラクタ。外部からはファクトリメソッド経由で構築すること。
        /// </summary>
        internal SynthesisRequest(
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

        /// <summary>
        /// テキストなし・音素直接入力のリクエストを作成する（Prosodyなし）。
        /// </summary>
        /// <param name="phonemes">音素配列（PUA文字またはIPA文字）。</param>
        /// <param name="lengthScale">話速スケール（デフォルト: 1.0）。</param>
        /// <param name="noiseScale">ノイズスケール（デフォルト: 0.667）。</param>
        /// <param name="noiseW">ノイズ幅（デフォルト: 0.8）。</param>
        /// <param name="speakerId">スピーカーID（デフォルト: 0）。</param>
        /// <param name="languageId">言語ID（デフォルト: 0）。</param>
        /// <returns>構築された <see cref="SynthesisRequest"/>。</returns>
        /// <exception cref="ArgumentException"><paramref name="phonemes"/> が null または空の場合。</exception>
        /// <example>
        /// <code>
        /// var request = SynthesisRequest.FromPhonemes(
        ///     new[] { "k", "o", "N_uvular", "n", "i", "ch", "w", "a" });
        /// var clip = await piperTTS.SynthesizeAsync(request);
        /// </code>
        /// </example>
        public static SynthesisRequest FromPhonemes(
            string[] phonemes,
            float lengthScale = 1.0f,
            float noiseScale = 0.667f,
            float noiseW = 0.8f,
            int speakerId = 0,
            int languageId = 0)
        {
            if (phonemes == null || phonemes.Length == 0)
                throw new ArgumentException("Phonemes cannot be null or empty.", nameof(phonemes));

            var copied = (string[])phonemes.Clone();
            return new SynthesisRequest(copied, null, lengthScale, noiseScale, noiseW, speakerId, languageId);
        }

        /// <summary>
        /// Prosody付き音素直接入力のリクエストを作成する。
        /// </summary>
        /// <param name="phonemes">音素配列（PUA文字またはIPA文字）。</param>
        /// <param name="prosodyFlat">
        /// Prosodyフラット配列（stride=3）。
        /// 長さは <paramref name="phonemes"/>.Length * 3 でなければならない。
        /// null の場合はProsodyなしとして扱う。
        /// </param>
        /// <param name="lengthScale">話速スケール（デフォルト: 1.0）。</param>
        /// <param name="noiseScale">ノイズスケール（デフォルト: 0.667）。</param>
        /// <param name="noiseW">ノイズ幅（デフォルト: 0.8）。</param>
        /// <param name="speakerId">スピーカーID（デフォルト: 0）。</param>
        /// <param name="languageId">言語ID（デフォルト: 0）。</param>
        /// <returns>構築された <see cref="SynthesisRequest"/>。</returns>
        /// <exception cref="ArgumentException">
        /// <paramref name="phonemes"/> が null または空の場合、
        /// または <paramref name="prosodyFlat"/> の長さが不整合の場合。
        /// </exception>
        /// <example>
        /// <code>
        /// var result = await piperTTS.PhonemizeAsync("こんにちは");
        /// var request = SynthesisRequest.FromPhonemesWithProsody(
        ///     result.Phonemes, result.ProsodyFlat, lengthScale: 0.8f);
        /// var clip = await piperTTS.SynthesizeAsync(request);
        /// </code>
        /// </example>
        public static SynthesisRequest FromPhonemesWithProsody(
            string[] phonemes,
            int[] prosodyFlat,
            float lengthScale = 1.0f,
            float noiseScale = 0.667f,
            float noiseW = 0.8f,
            int speakerId = 0,
            int languageId = 0)
        {
            if (phonemes == null || phonemes.Length == 0)
                throw new ArgumentException("Phonemes cannot be null or empty.", nameof(phonemes));

            if (prosodyFlat != null && prosodyFlat.Length != phonemes.Length * PhonemeEncoder.ProsodyStride)
                throw new ArgumentException(
                    $"ProsodyFlat length ({prosodyFlat.Length}) must be " +
                    $"Phonemes.Length * {PhonemeEncoder.ProsodyStride} ({phonemes.Length * PhonemeEncoder.ProsodyStride}).",
                    nameof(prosodyFlat));

            var copiedPhonemes = (string[])phonemes.Clone();
            var copiedProsody = (int[])prosodyFlat?.Clone();
            return new SynthesisRequest(
                copiedPhonemes, copiedProsody, lengthScale, noiseScale, noiseW, speakerId, languageId);
        }
    }
}