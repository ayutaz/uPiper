using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace uPiper.Core.Phonemizers.Multilingual
{
    /// <summary>
    /// Unicode-based language detector for multilingual TTS.
    /// Ported from piper-plus Python implementation (UnicodeLanguageDetector).
    /// Uses character range checks (no regex) for zero-allocation, high-speed detection.
    /// </summary>
    public class UnicodeLanguageDetector
    {
        private readonly IReadOnlyList<string> _languages;
        private readonly string _defaultLatinLanguage;
        private readonly bool _hasJa;
        private readonly bool _hasZh;
        private readonly bool _hasKo;

        /// <summary>
        /// Gets the default Latin language code (e.g., "en").
        /// </summary>
        public string DefaultLatinLanguage => _defaultLatinLanguage;

        /// <summary>
        /// Gets the list of supported languages.
        /// </summary>
        public IReadOnlyList<string> Languages => _languages;

        /// <summary>
        /// Initializes a new UnicodeLanguageDetector.
        /// </summary>
        /// <param name="languages">List of language codes to support (e.g., ["ja", "en", "zh"]).</param>
        /// <param name="defaultLatinLanguage">Default language for Latin characters (default: "en").</param>
        public UnicodeLanguageDetector(IReadOnlyList<string> languages, string defaultLatinLanguage = "en")
        {
            if (languages == null || languages.Count == 0)
                throw new ArgumentException("At least one language must be specified.", nameof(languages));

            _languages = languages;
            _defaultLatinLanguage = defaultLatinLanguage;
            _hasJa = ContainsLanguage("ja");
            _hasZh = ContainsLanguage("zh");
            _hasKo = ContainsLanguage("ko");
        }

        // ── Static character classifiers ──────────────────────────────────────

        /// <summary>
        /// Returns true if the character is Hiragana (U+3040-309F),
        /// Katakana (U+30A0-30FF), or Katakana Phonetic Extensions (U+31F0-31FF).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsKana(char ch)
            => (ch >= '\u3040' && ch <= '\u309F') ||
               (ch >= '\u30A0' && ch <= '\u30FF') ||
               (ch >= '\u31F0' && ch <= '\u31FF');

        /// <summary>
        /// Returns true if the character is a CJK Unified Ideograph
        /// (U+4E00-9FFF), CJK Extension A (U+3400-4DBF),
        /// or CJK Compatibility Ideograph (U+F900-FAFF).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsCJK(char ch)
            => (ch >= '\u4E00' && ch <= '\u9FFF') ||
               (ch >= '\u3400' && ch <= '\u4DBF') ||
               (ch >= '\uF900' && ch <= '\uFAFF');

        /// <summary>
        /// Returns true if the character is Hangul:
        /// Syllables (U+AC00-D7AF), Jamo (U+1100-11FF), or Compat Jamo (U+3130-318F).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsHangul(char ch)
            => (ch >= '\uAC00' && ch <= '\uD7AF') ||
               (ch >= '\u1100' && ch <= '\u11FF') ||
               (ch >= '\u3130' && ch <= '\u318F');

        /// <summary>
        /// Returns true if the character is a Fullwidth Latin letter
        /// (U+FF21-FF3A: Ａ-Ｚ, U+FF41-FF5A: ａ-ｚ).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFullwidthLatin(char ch)
            => (ch >= '\uFF21' && ch <= '\uFF3A') ||
               (ch >= '\uFF41' && ch <= '\uFF5A');

        /// <summary>
        /// Returns true if the character is a Latin letter:
        /// A-Z, a-z, Latin-1 Supplement (U+00C0-00D6, U+00D8-00F6, U+00F8-00FF),
        /// Latin Extended-A (U+0100-017F), or Latin Extended-B (U+0180-024F).
        /// Excludes × (U+00D7) and ÷ (U+00F7).
        /// Latin Extended ranges include accented characters used in French (e.g., OE ligature),
        /// Portuguese (e.g., nasal vowels), and Spanish (e.g., inverted punctuation letters).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLatin(char ch)
            => (ch >= 'A' && ch <= 'Z') ||
               (ch >= 'a' && ch <= 'z') ||
               (ch >= '\u00C0' && ch <= '\u00D6') ||
               (ch >= '\u00D8' && ch <= '\u00F6') ||
               (ch >= '\u00F8' && ch <= '\u00FF') ||
               (ch >= '\u0100' && ch <= '\u017F') ||
               (ch >= '\u0180' && ch <= '\u024F');

        /// <summary>
        /// Returns true if the character is in Latin Extended-A (U+0100-017F)
        /// or Latin Extended-B (U+0180-024F).
        /// These ranges contain accented and special characters used in French,
        /// Portuguese, Spanish, and other European languages.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLatinExtended(char ch)
            => (ch >= '\u0100' && ch <= '\u017F') ||
               (ch >= '\u0180' && ch <= '\u024F');

        /// <summary>
        /// Returns true if the character is a CJK punctuation or fullwidth form character.
        /// CJK Symbols and Punctuation (U+3000-303F),
        /// or Fullwidth Forms (U+FF00-FF20, U+FF3B-FF40, U+FF5B-FFEF).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsCjkPunct(char ch)
            => (ch >= '\u3000' && ch <= '\u303F') ||
               (ch >= '\uFF00' && ch <= '\uFF20') ||
               (ch >= '\uFF3B' && ch <= '\uFF40') ||
               (ch >= '\uFF5B' && ch <= '\uFFEF');

        // ── Instance methods ──────────────────────────────────────────────────

        /// <summary>
        /// Scans the entire text to check if it contains any Kana characters.
        /// Used to resolve CJK ambiguity (Japanese vs Chinese).
        /// </summary>
        /// <param name="text">Text to scan.</param>
        /// <returns>True if text contains Hiragana, Katakana, or Katakana Phonetic Extensions.</returns>
        public bool HasKana(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
            for (var i = 0; i < text.Length; i++)
            {
                if (IsKana(text[i]))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Detects the language of a single character.
        /// Detection priority:
        /// 1. Kana → "ja"
        /// 2. Hangul → "ko"
        /// 3. CJK ideographs → "ja" or "zh" based on contextHasKana
        /// 4. Fullwidth Latin → defaultLatinLanguage
        /// 5. CJK punctuation → appropriate CJK language (ja/zh based on context)
        /// 6. Latin characters → defaultLatinLanguage
        /// 7. Neutral (whitespace, digits, punctuation) → null
        /// </summary>
        /// <param name="ch">Character to detect.</param>
        /// <param name="contextHasKana">Whether the surrounding text contains Kana (for CJK disambiguation).</param>
        /// <returns>Language code, or null for neutral characters.</returns>
        public string DetectChar(char ch, bool contextHasKana = false)
        {
            // Priority 1: Kana → always Japanese
            if (_hasJa && IsKana(ch))
                return "ja";

            // Priority 2: Hangul → Korean
            if (_hasKo && IsHangul(ch))
                return "ko";

            // Priority 3: CJK ideographs → disambiguate by context
            if (IsCJK(ch))
            {
                if (_hasJa && _hasZh)
                    return contextHasKana ? "ja" : "zh";
                if (_hasJa) return "ja";
                if (_hasZh) return "zh";
                return null;
            }

            // Priority 4: Fullwidth Latin → default Latin language
            if (IsFullwidthLatin(ch))
                return ContainsLanguage(_defaultLatinLanguage) ? _defaultLatinLanguage : null;

            // Priority 5: CJK punctuation → appropriate CJK language
            // When both Japanese and Chinese are in the language list, CJK punctuation
            // (e.g., 。、「」) is disambiguated using Kana context, same as CJK ideographs.
            if (IsCjkPunct(ch))
            {
                if (_hasJa && _hasZh)
                    return contextHasKana ? "ja" : "zh";
                if (_hasJa) return "ja";
                if (_hasZh) return "zh";
                return null;
            }

            // Priority 6: Latin characters → default Latin language
            if (IsLatin(ch))
                return ContainsLanguage(_defaultLatinLanguage) ? _defaultLatinLanguage : null;

            // Neutral: whitespace, digits, other punctuation
            return null;
        }

        /// <summary>
        /// Segments text into language-specific chunks.
        /// Neutral characters (whitespace, digits, punctuation) are absorbed
        /// into the preceding language segment.
        /// </summary>
        /// <param name="text">Input text (may contain mixed languages).</param>
        /// <returns>
        /// List of (languageCode, segmentText) tuples in order of appearance.
        /// Returns empty list for null/empty input.
        /// </returns>
        public List<(string language, string text)> SegmentText(string text)
        {
            var result = new List<(string, string)>();
            if (string.IsNullOrEmpty(text))
                return result;

            // Pre-scan for Kana to resolve CJK ambiguity
            var contextHasKana = _hasJa && HasKana(text);

            string currentLang = null;
            var currentText = new StringBuilder();

            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];
                var lang = DetectChar(ch, contextHasKana);

                if (lang == null)
                {
                    // Neutral character: absorb into current segment
                    currentText.Append(ch);
                }
                else if (lang == currentLang)
                {
                    // Same language: continue building segment
                    currentText.Append(ch);
                }
                else
                {
                    // Language change detected
                    if (currentLang != null && currentText.Length > 0)
                    {
                        result.Add((currentLang, currentText.ToString()));
                        currentText.Clear();
                    }
                    // Note: pending neutral chars at start are included in first real segment
                    currentLang = lang;
                    currentText.Append(ch);
                }
            }

            // Flush remaining characters
            if (currentText.Length > 0)
            {
                var finalLang = currentLang ?? _defaultLatinLanguage;
                result.Add((finalLang, currentText.ToString()));
            }

            // Defensive fallback: this branch is unreachable for non-empty text because the
            // "Flush remaining characters" block above always emits at least one segment when
            // text.Length > 0. Kept as a safety net against future refactoring.
            if (result.Count == 0 && !string.IsNullOrEmpty(text))
                result.Add((_defaultLatinLanguage, text));

            return result;
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private bool ContainsLanguage(string lang)
        {
            for (var i = 0; i < _languages.Count; i++)
            {
                if (_languages[i] == lang)
                    return true;
            }
            return false;
        }
    }
}