using System;
using System.Collections.Generic;

namespace uPiper.Core.Phonemizers.Multilingual
{
    /// <summary>
    /// Language code and ID constants for multilingual TTS.
    /// Language IDs match the multilingual ONNX model's lid (language_id) input tensor.
    /// The mapping is defined in piper-plus preprocess.py as:
    /// {lang: idx for idx, lang in enumerate(lang_parts)}
    /// where lang_parts = ["ja", "en", "zh", "es", "fr", "pt"] for the 6-language model.
    /// </summary>
    public static class LanguageConstants
    {
        // ── Language IDs matching the multilingual ONNX model's lid input ────

        /// <summary>Language ID for Japanese (ja). Always 0 in the standard 6-language model.</summary>
        public const int LanguageIdJapanese = 0;

        /// <summary>Language ID for English (en).</summary>
        public const int LanguageIdEnglish = 1;

        /// <summary>Language ID for Chinese (zh).</summary>
        public const int LanguageIdChinese = 2;

        /// <summary>Language ID for Spanish (es).</summary>
        public const int LanguageIdSpanish = 3;

        /// <summary>Language ID for French (fr).</summary>
        public const int LanguageIdFrench = 4;

        /// <summary>Language ID for Portuguese (pt).</summary>
        public const int LanguageIdPortuguese = 5;

        /// <summary>Language ID for Korean (ko). Used when the model includes Korean support.</summary>
        public const int LanguageIdKorean = 6;

        // ── Language codes (ISO 639-1) ──────────────────────────────────────

        /// <summary>ISO 639-1 code for Japanese.</summary>
        public const string CodeJapanese = "ja";

        /// <summary>ISO 639-1 code for English.</summary>
        public const string CodeEnglish = "en";

        /// <summary>ISO 639-1 code for Chinese.</summary>
        public const string CodeChinese = "zh";

        /// <summary>ISO 639-1 code for Spanish.</summary>
        public const string CodeSpanish = "es";

        /// <summary>ISO 639-1 code for French.</summary>
        public const string CodeFrench = "fr";

        /// <summary>ISO 639-1 code for Portuguese.</summary>
        public const string CodePortuguese = "pt";

        /// <summary>ISO 639-1 code for Korean.</summary>
        public const string CodeKorean = "ko";

        // ── Language groupings ──────────────────────────────────────────────

        /// <summary>All supported language codes.</summary>
        public static readonly string[] AllLanguages = { "ja", "en", "zh", "es", "fr", "pt", "ko" };

        /// <summary>
        /// Latin-script languages that require explicit language hints for disambiguation.
        /// When multiple Latin-script languages are supported, the detector cannot
        /// distinguish them by Unicode range alone.
        /// </summary>
        public static readonly string[] LatinLanguages = { "en", "es", "fr", "pt" };

        /// <summary>
        /// CJK languages. Each uses distinct script features for detection:
        /// Japanese (Kana), Chinese (CJK ideographs without Kana), Korean (Hangul).
        /// </summary>
        public static readonly string[] CjkLanguages = { "ja", "zh", "ko" };

        // ── Lookup dictionaries ───────────────────────────────────────────

        private static readonly Dictionary<string, int> CodeToId = new(7)
        {
            [CodeJapanese] = LanguageIdJapanese,
            [CodeEnglish] = LanguageIdEnglish,
            [CodeChinese] = LanguageIdChinese,
            [CodeSpanish] = LanguageIdSpanish,
            [CodeFrench] = LanguageIdFrench,
            [CodePortuguese] = LanguageIdPortuguese,
            [CodeKorean] = LanguageIdKorean
        };

        private static readonly Dictionary<int, string> IdToCode = new(7)
        {
            [LanguageIdJapanese] = CodeJapanese,
            [LanguageIdEnglish] = CodeEnglish,
            [LanguageIdChinese] = CodeChinese,
            [LanguageIdSpanish] = CodeSpanish,
            [LanguageIdFrench] = CodeFrench,
            [LanguageIdPortuguese] = CodePortuguese,
            [LanguageIdKorean] = CodeKorean
        };

        // ── Methods ─────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the numeric language ID for a given ISO 639-1 language code.
        /// </summary>
        /// <param name="languageCode">ISO 639-1 language code (e.g., "ja", "en").</param>
        /// <returns>Language ID for the ONNX model's lid input.</returns>
        /// <exception cref="ArgumentException">Thrown when the language code is not supported.</exception>
        public static int GetLanguageId(string languageCode)
        {
            if (CodeToId.TryGetValue(languageCode, out var id))
                return id;

            throw new ArgumentException(
                $"Unsupported language code: '{languageCode}'. " +
                $"Supported codes: {string.Join(", ", AllLanguages)}",
                nameof(languageCode));
        }

        /// <summary>
        /// Returns the ISO 639-1 language code for a given numeric language ID.
        /// </summary>
        /// <param name="languageId">Numeric language ID from the ONNX model.</param>
        /// <returns>ISO 639-1 language code.</returns>
        /// <exception cref="ArgumentException">Thrown when the language ID is not recognized.</exception>
        public static string GetLanguageCode(int languageId)
        {
            if (IdToCode.TryGetValue(languageId, out var code))
                return code;

            throw new ArgumentException(
                $"Unsupported language ID: {languageId}. " +
                $"Supported IDs: 0 (ja), 1 (en), 2 (zh), 3 (es), 4 (fr), 5 (pt), 6 (ko)",
                nameof(languageId));
        }

        /// <summary>
        /// Returns true if the language uses Latin script.
        /// Latin-script languages cannot be distinguished by Unicode range alone.
        /// </summary>
        /// <param name="languageCode">ISO 639-1 language code.</param>
        /// <returns>True for en, es, fr, pt.</returns>
        public static bool IsLatinLanguage(string languageCode)
        {
            // Inline check for performance (avoids array iteration)
            return languageCode == CodeEnglish ||
                   languageCode == CodeSpanish ||
                   languageCode == CodeFrench ||
                   languageCode == CodePortuguese;
        }

        /// <summary>
        /// Returns true if the language belongs to the CJK group.
        /// </summary>
        /// <param name="languageCode">ISO 639-1 language code.</param>
        /// <returns>True for ja, zh, ko.</returns>
        public static bool IsCjkLanguage(string languageCode)
        {
            return languageCode == CodeJapanese ||
                   languageCode == CodeChinese ||
                   languageCode == CodeKorean;
        }

        /// <summary>
        /// Returns true if the language code is one of the supported languages.
        /// </summary>
        /// <param name="languageCode">ISO 639-1 language code.</param>
        /// <returns>True if the language is supported.</returns>
        public static bool IsSupportedLanguage(string languageCode)
        {
            return CodeToId.ContainsKey(languageCode);
        }
    }
}