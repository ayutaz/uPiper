using System.Collections.Generic;
using NUnit.Framework;
using uPiper.Core.Phonemizers.Multilingual;

namespace uPiper.Tests.Editor
{
    [TestFixture]
    public class UnicodeLanguageDetectorTests
    {
        // ── Static method: IsKana ─────────────────────────────────────────────

        [Test]
        public void IsKana_Hiragana_ReturnsTrue()
        {
            Assert.IsTrue(UnicodeLanguageDetector.IsKana('あ'));
        }

        [Test]
        public void IsKana_Katakana_ReturnsTrue()
        {
            Assert.IsTrue(UnicodeLanguageDetector.IsKana('ア'));
        }

        [Test]
        public void IsKana_KatakanaPhonetic_ReturnsTrue()
        {
            Assert.IsTrue(UnicodeLanguageDetector.IsKana('\u31F0'));
        }

        [Test]
        public void IsKana_LatinChar_ReturnsFalse()
        {
            Assert.IsFalse(UnicodeLanguageDetector.IsKana('a'));
        }

        // ── Static method: IsCJK ──────────────────────────────────────────────

        [Test]
        public void IsCJK_KanjiBasic_ReturnsTrue()
        {
            Assert.IsTrue(UnicodeLanguageDetector.IsCJK('漢'));
        }

        [Test]
        public void IsCJK_ExtensionA_ReturnsTrue()
        {
            Assert.IsTrue(UnicodeLanguageDetector.IsCJK('\u3400'));
        }

        [Test]
        public void IsCJK_Compatibility_ReturnsTrue()
        {
            Assert.IsTrue(UnicodeLanguageDetector.IsCJK('\uF900'));
        }

        [Test]
        public void IsCJK_Hiragana_ReturnsFalse()
        {
            Assert.IsFalse(UnicodeLanguageDetector.IsCJK('あ'));
        }

        // ── Static method: IsHangul ───────────────────────────────────────────

        [Test]
        public void IsHangul_Syllable_ReturnsTrue()
        {
            // '한' = U+D55C (Hangul Syllables range U+AC00-D7AF)
            Assert.IsTrue(UnicodeLanguageDetector.IsHangul('\uD55C'));
        }

        [Test]
        public void IsHangul_Jamo_ReturnsTrue()
        {
            Assert.IsTrue(UnicodeLanguageDetector.IsHangul('\u1100'));
        }

        // ── Static method: IsLatin ────────────────────────────────────────────

        [Test]
        public void IsLatin_BasicLatin_ReturnsTrue()
        {
            Assert.IsTrue(UnicodeLanguageDetector.IsLatin('a'));
            Assert.IsTrue(UnicodeLanguageDetector.IsLatin('Z'));
        }

        [Test]
        public void IsLatin_ExtendedLatin_ReturnsTrue()
        {
            // 'é' = U+00E9 (Latin Extended range U+00F8-00FF... actually U+00D8-00F6)
            Assert.IsTrue(UnicodeLanguageDetector.IsLatin('\u00E9'));
        }

        [Test]
        public void IsLatin_Times_ReturnsFalse()
        {
            // '×' = U+00D7 (excluded)
            Assert.IsFalse(UnicodeLanguageDetector.IsLatin('\u00D7'));
        }

        [Test]
        public void IsLatin_Divide_ReturnsFalse()
        {
            // '÷' = U+00F7 (excluded)
            Assert.IsFalse(UnicodeLanguageDetector.IsLatin('\u00F7'));
        }

        // ── Instance method: HasKana ──────────────────────────────────────────

        [Test]
        public void HasKana_JapaneseText_ReturnsTrue()
        {
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en" });
            Assert.IsTrue(detector.HasKana("こんにちは"));
        }

        [Test]
        public void HasKana_ChineseOnly_ReturnsFalse()
        {
            var detector = new UnicodeLanguageDetector(new[] { "zh", "en" });
            Assert.IsFalse(detector.HasKana("你好"));
        }

        [Test]
        public void HasKana_EmptyString_ReturnsFalse()
        {
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en" });
            Assert.IsFalse(detector.HasKana(""));
        }

        // ── Instance method: DetectChar ───────────────────────────────────────

        [Test]
        public void DetectChar_Kana_ReturnsJa()
        {
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en", "zh" });
            Assert.AreEqual("ja", detector.DetectChar('あ'));
        }

        [Test]
        public void DetectChar_Hangul_ReturnsKo()
        {
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en", "zh", "ko" });
            // '한' = U+D55C
            Assert.AreEqual("ko", detector.DetectChar('\uD55C'));
        }

        [Test]
        public void DetectChar_CJK_WithKanaContext_ReturnsJa()
        {
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en", "zh" });
            Assert.AreEqual("ja", detector.DetectChar('漢', contextHasKana: true));
        }

        [Test]
        public void DetectChar_CJK_WithoutKanaContext_ReturnsZh()
        {
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en", "zh" });
            Assert.AreEqual("zh", detector.DetectChar('漢', contextHasKana: false));
        }

        [Test]
        public void DetectChar_CJK_JaOnly_ReturnsJa()
        {
            // ja+en のみの検出器では CJK は "ja" に帰属
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en" });
            Assert.AreEqual("ja", detector.DetectChar('漢'));
        }

        [Test]
        public void DetectChar_Latin_ReturnsEn()
        {
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en" });
            Assert.AreEqual("en", detector.DetectChar('a'));
        }

        [Test]
        public void DetectChar_ExtendedLatin_ReturnsEn()
        {
            // 'é' = U+00E9
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en" });
            Assert.AreEqual("en", detector.DetectChar('\u00E9'));
        }

        [Test]
        public void DetectChar_Space_ReturnsNull()
        {
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en" });
            Assert.IsNull(detector.DetectChar(' '));
        }

        [Test]
        public void DetectChar_Digit_ReturnsNull()
        {
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en" });
            Assert.IsNull(detector.DetectChar('1'));
        }

        [Test]
        public void DetectChar_JapanesePunct_ReturnsJa()
        {
            // '。' = U+3002 (CJK Symbols and Punctuation: U+3000-303F)
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en" });
            Assert.AreEqual("ja", detector.DetectChar('\u3002'));
        }

        // ── Instance method: SegmentText ──────────────────────────────────────

        [Test]
        public void SegmentText_JapaneseOnly_ReturnsSingleJaSegment()
        {
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en" });
            var result = detector.SegmentText("こんにちは");

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("ja", result[0].language);
            Assert.AreEqual("こんにちは", result[0].text);
        }

        [Test]
        public void SegmentText_EnglishOnly_ReturnsSingleEnSegment()
        {
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en" });
            var result = detector.SegmentText("hello world");

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("en", result[0].language);
            Assert.AreEqual("hello world", result[0].text);
        }

        [Test]
        public void SegmentText_MixedJaEn_ReturnsTwoSegments()
        {
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en" });
            var result = detector.SegmentText("Hello こんにちは");

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("en", result[0].language);
            Assert.AreEqual("Hello ", result[0].text);
            Assert.AreEqual("ja", result[1].language);
            Assert.AreEqual("こんにちは", result[1].text);
        }

        [Test]
        public void SegmentText_NeutralCharsAbsorbed_ToNextSegment()
        {
            // 先頭スペースは最初の言語セグメントに吸収される
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en" });
            var result = detector.SegmentText("   こんにちは");

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("ja", result[0].language);
            Assert.AreEqual("   こんにちは", result[0].text);
        }

        [Test]
        public void SegmentText_Empty_ReturnsEmpty()
        {
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en" });
            var result = detector.SegmentText("");

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void SegmentText_OnlyDigits_ReturnsDefaultLatin()
        {
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en" });
            var result = detector.SegmentText("123");

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("en", result[0].language);
            Assert.AreEqual("123", result[0].text);
        }

        [Test]
        public void SegmentText_JaEnJa_ReturnsThreeSegments()
        {
            // スペースは前のセグメントに吸収される
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en" });
            var result = detector.SegmentText("おはよう good morning ですね");

            Assert.AreEqual(3, result.Count);
            Assert.AreEqual("ja", result[0].language);
            Assert.AreEqual("おはよう ", result[0].text);
            Assert.AreEqual("en", result[1].language);
            Assert.AreEqual("good morning ", result[1].text);
            Assert.AreEqual("ja", result[2].language);
            Assert.AreEqual("ですね", result[2].text);
        }

        // ── Latin Extended Detection (Phase 5) ──────────────────────────────

        [Test]
        public void IsLatin_LatinExtendedA_ReturnsTrue()
        {
            // ĉ = U+0109, ő = U+0151, ā = U+0101
            Assert.IsTrue(UnicodeLanguageDetector.IsLatin('\u0109'));
            Assert.IsTrue(UnicodeLanguageDetector.IsLatin('\u0151'));
            Assert.IsTrue(UnicodeLanguageDetector.IsLatin('\u0101'));
        }

        [Test]
        public void IsLatin_LatinExtendedB_ReturnsTrue()
        {
            // U+0180 = ƀ (start of Extended-B), U+024F = ɏ (end of Extended-B)
            Assert.IsTrue(UnicodeLanguageDetector.IsLatin('\u0180'));
            Assert.IsTrue(UnicodeLanguageDetector.IsLatin('\u024F'));
            Assert.IsTrue(UnicodeLanguageDetector.IsLatin('\u01A0'));
        }

        [Test]
        public void IsLatin_AccentedSpanish_ReturnsTrue()
        {
            // ñ = U+00F1, á = U+00E1, é = U+00E9, í = U+00ED, ó = U+00F3, ú = U+00FA
            Assert.IsTrue(UnicodeLanguageDetector.IsLatin('\u00F1'));
            Assert.IsTrue(UnicodeLanguageDetector.IsLatin('\u00E1'));
            Assert.IsTrue(UnicodeLanguageDetector.IsLatin('\u00E9'));
            Assert.IsTrue(UnicodeLanguageDetector.IsLatin('\u00ED'));
            Assert.IsTrue(UnicodeLanguageDetector.IsLatin('\u00F3'));
            Assert.IsTrue(UnicodeLanguageDetector.IsLatin('\u00FA'));
        }

        [Test]
        public void IsLatin_AccentedFrench_ReturnsTrue()
        {
            // ç = U+00E7, è = U+00E8, ê = U+00EA, ë = U+00EB
            // î = U+00EE, ï = U+00EF, ô = U+00F4, û = U+00FB, ü = U+00FC
            Assert.IsTrue(UnicodeLanguageDetector.IsLatin('\u00E7'));
            Assert.IsTrue(UnicodeLanguageDetector.IsLatin('\u00E8'));
            Assert.IsTrue(UnicodeLanguageDetector.IsLatin('\u00EA'));
            Assert.IsTrue(UnicodeLanguageDetector.IsLatin('\u00EB'));
            Assert.IsTrue(UnicodeLanguageDetector.IsLatin('\u00EE'));
            Assert.IsTrue(UnicodeLanguageDetector.IsLatin('\u00EF'));
            Assert.IsTrue(UnicodeLanguageDetector.IsLatin('\u00F4'));
            Assert.IsTrue(UnicodeLanguageDetector.IsLatin('\u00FB'));
            Assert.IsTrue(UnicodeLanguageDetector.IsLatin('\u00FC'));
        }

        [Test]
        public void IsLatin_AccentedPortuguese_ReturnsTrue()
        {
            // ã = U+00E3, õ = U+00F5, à = U+00E0, â = U+00E2
            Assert.IsTrue(UnicodeLanguageDetector.IsLatin('\u00E3'));
            Assert.IsTrue(UnicodeLanguageDetector.IsLatin('\u00F5'));
            Assert.IsTrue(UnicodeLanguageDetector.IsLatin('\u00E0'));
            Assert.IsTrue(UnicodeLanguageDetector.IsLatin('\u00E2'));
        }

        // ── IsLatinExtended ─────────────────────────────────────────────────

        [Test]
        public void IsLatinExtended_InRange_ReturnsTrue()
        {
            // Latin Extended-A boundaries and mid-range
            Assert.IsTrue(UnicodeLanguageDetector.IsLatinExtended('\u0100'));
            Assert.IsTrue(UnicodeLanguageDetector.IsLatinExtended('\u017F'));
            // Latin Extended-B boundaries
            Assert.IsTrue(UnicodeLanguageDetector.IsLatinExtended('\u0180'));
            Assert.IsTrue(UnicodeLanguageDetector.IsLatinExtended('\u024F'));
        }

        [Test]
        public void IsLatinExtended_OutOfRange_ReturnsFalse()
        {
            // Basic ASCII
            Assert.IsFalse(UnicodeLanguageDetector.IsLatinExtended('a'));
            Assert.IsFalse(UnicodeLanguageDetector.IsLatinExtended('Z'));
            // Latin-1 Supplement (before Extended-A)
            Assert.IsFalse(UnicodeLanguageDetector.IsLatinExtended('\u00FF'));
            // Just after Extended-B
            Assert.IsFalse(UnicodeLanguageDetector.IsLatinExtended('\u0250'));
        }

        // ── IsCjkPunct ──────────────────────────────────────────────────────

        [Test]
        public void IsCjkPunct_ChinesePeriod_ReturnsTrue()
        {
            // 。= U+3002
            Assert.IsTrue(UnicodeLanguageDetector.IsCjkPunct('\u3002'));
        }

        [Test]
        public void IsCjkPunct_FullwidthComma_ReturnsTrue()
        {
            // ， = U+FF0C
            Assert.IsTrue(UnicodeLanguageDetector.IsCjkPunct('\uFF0C'));
        }

        [Test]
        public void IsCjkPunct_AsciiPeriod_ReturnsFalse()
        {
            // '.' = U+002E (basic ASCII, not CJK punctuation)
            Assert.IsFalse(UnicodeLanguageDetector.IsCjkPunct('.'));
        }

        // ── CJK Punctuation Context Disambiguation ──────────────────────────

        [Test]
        public void DetectChar_CjkPunct_WithJaAndZh_ContextHasKana_ReturnsJa()
        {
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en", "zh" });
            // 。= U+3002, context has Kana → ja
            Assert.AreEqual("ja", detector.DetectChar('\u3002', contextHasKana: true));
        }

        [Test]
        public void DetectChar_CjkPunct_WithJaAndZh_NoKana_ReturnsZh()
        {
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en", "zh" });
            // 。= U+3002, no Kana context → zh
            Assert.AreEqual("zh", detector.DetectChar('\u3002', contextHasKana: false));
        }

        [Test]
        public void DetectChar_CjkPunct_WithJaOnly_ReturnsJa()
        {
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en" });
            // Only ja in language list, no zh → ja regardless of context
            Assert.AreEqual("ja", detector.DetectChar('\u3002', contextHasKana: false));
        }

        [Test]
        public void DetectChar_CjkPunct_WithZhOnly_ReturnsZh()
        {
            var detector = new UnicodeLanguageDetector(new[] { "zh", "en" });
            // Only zh in language list, no ja → zh
            Assert.AreEqual("zh", detector.DetectChar('\u3002'));
        }

        // ── Language Flag Tests ─────────────────────────────────────────────

        [Test]
        public void Constructor_WithEsFrPt_InitializesFlags()
        {
            // Verify es/fr/pt are recognized as valid languages
            var detector = new UnicodeLanguageDetector(new[] { "es", "fr", "pt", "en" });
            Assert.AreEqual(4, detector.Languages.Count);
            Assert.AreEqual("en", detector.DefaultLatinLanguage);
        }

        [Test]
        public void DetectChar_Latin_WithSpanishDefault_ReturnsEs()
        {
            var detector = new UnicodeLanguageDetector(new[] { "es", "en" }, defaultLatinLanguage: "es");
            Assert.AreEqual("es", detector.DetectChar('a'));
            Assert.AreEqual("es", detector.DetectChar('\u00F1')); // ñ
        }

        [Test]
        public void DetectChar_Latin_WithFrenchDefault_ReturnsFr()
        {
            var detector = new UnicodeLanguageDetector(new[] { "fr", "en" }, defaultLatinLanguage: "fr");
            Assert.AreEqual("fr", detector.DetectChar('a'));
            Assert.AreEqual("fr", detector.DetectChar('\u00E7')); // ç
        }

        // ── Segment Tests with New Languages ────────────────────────────────

        [Test]
        public void SegmentText_ChineseOnly_ReturnsZh()
        {
            var detector = new UnicodeLanguageDetector(new[] { "zh", "en" });
            var result = detector.SegmentText("你好世界");

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("zh", result[0].language);
            Assert.AreEqual("你好世界", result[0].text);
        }

        [Test]
        public void SegmentText_KoreanOnly_ReturnsKo()
        {
            var detector = new UnicodeLanguageDetector(new[] { "ko", "en" });
            // 안녕하세요
            var result = detector.SegmentText("\uC548\uB155\uD558\uC138\uC694");

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("ko", result[0].language);
            Assert.AreEqual("\uC548\uB155\uD558\uC138\uC694", result[0].text);
        }

        [Test]
        public void SegmentText_MixedJapaneseKorean()
        {
            // "日本語와한국어" - CJK ideographs mixed with Hangul
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en", "ko" });
            var result = detector.SegmentText("日本語\uC640\uD55C\uAD6D\uC5B4");

            Assert.GreaterOrEqual(result.Count, 2);
            // CJK ideographs with ja in language list (no zh) → ja
            Assert.AreEqual("ja", result[0].language);
            // Hangul part → ko
            var lastSegment = result[result.Count - 1];
            Assert.AreEqual("ko", lastSegment.language);
        }

        [Test]
        public void SegmentText_MixedChineseEnglish()
        {
            // "Hello你好World"
            var detector = new UnicodeLanguageDetector(new[] { "zh", "en" });
            var result = detector.SegmentText("Hello你好World");

            Assert.AreEqual(3, result.Count);
            Assert.AreEqual("en", result[0].language);
            Assert.AreEqual("Hello", result[0].text);
            Assert.AreEqual("zh", result[1].language);
            Assert.AreEqual("你好", result[1].text);
            Assert.AreEqual("en", result[2].language);
            Assert.AreEqual("World", result[2].text);
        }

        [Test]
        public void SegmentText_AllSevenLanguages()
        {
            // Create detector with all 7 supported languages
            var detector = new UnicodeLanguageDetector(
                new[] { "ja", "en", "zh", "ko", "es", "fr", "pt" });

            // Mixed text: Kana (ja) + Latin (en default) + Hangul (ko)
            var result = detector.SegmentText("こんにちは Hello \uD55C\uAD6D\uC5B4");

            Assert.GreaterOrEqual(result.Count, 3);
            Assert.AreEqual("ja", result[0].language);
            Assert.AreEqual("en", result[1].language);
            Assert.AreEqual("ko", result[2].language);
        }

        [Test]
        public void SegmentText_CjkPunctWithChineseContext()
        {
            // Chinese text with 。，: "你好。世界，"
            var detector = new UnicodeLanguageDetector(new[] { "zh", "en" });
            var result = detector.SegmentText("你好\u3002世界\uFF0C");

            // All characters should be zh (CJK ideographs + CJK punctuation)
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("zh", result[0].language);
            Assert.AreEqual("你好\u3002世界\uFF0C", result[0].text);
        }

        // ── Edge Cases ──────────────────────────────────────────────────────

        [Test]
        public void SegmentText_OnlyPunctuation_DefaultLang()
        {
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en" });
            var result = detector.SegmentText("...,,,!!!");

            // All ASCII punctuation is neutral, falls back to default Latin language
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("en", result[0].language);
            Assert.AreEqual("...,,,!!!", result[0].text);
        }

        [Test]
        public void SegmentText_EmojisAndText()
        {
            // Emojis are neutral (surrogate pairs or high-Unicode), text is Latin
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en" });
            var result = detector.SegmentText("\uD83D\uDE0Ahello\uD83D\uDE0A");

            // Should contain "en" segment for "hello"; emojis absorbed as neutral
            Assert.GreaterOrEqual(result.Count, 1);
            var foundEn = false;
            foreach (var seg in result)
            {
                if (seg.language == "en" && seg.text.Contains("hello"))
                    foundEn = true;
            }
            Assert.IsTrue(foundEn, "Expected to find 'en' segment containing 'hello'");
        }

        [Test]
        public void SegmentText_FullwidthLatinWithJapanese()
        {
            // "Ａｐｐとアプリ" - Fullwidth Latin + Japanese
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en" });
            var result = detector.SegmentText("\uFF21\uFF50\uFF50\u3068\u30A2\u30D7\u30EA");

            Assert.AreEqual(2, result.Count);
            // Fullwidth Latin → defaultLatinLanguage (en)
            Assert.AreEqual("en", result[0].language);
            Assert.AreEqual("\uFF21\uFF50\uFF50", result[0].text);
            // と + katakana → ja
            Assert.AreEqual("ja", result[1].language);
            Assert.AreEqual("\u3068\u30A2\u30D7\u30EA", result[1].text);
        }

        // ── Halfwidth character detection ───────────────────────────────────

        [Test]
        public void HalfwidthKatakana_DetectedAsJapanese()
        {
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en" });
            var segments = detector.SegmentText("ｱｲｳｴｵ");
            Assert.AreEqual(1, segments.Count);
            Assert.AreEqual("ja", segments[0].language);
        }

        [Test]
        public void HalfwidthHangul_DetectedAsKorean()
        {
            var detector = new UnicodeLanguageDetector(new[] { "ko", "en" });
            var segments = detector.SegmentText("ﾠﾡﾢ");
            Assert.AreEqual(1, segments.Count);
            Assert.AreEqual("ko", segments[0].language);
        }

        // ── Segment-level CJK disambiguation (ja vs zh) ────────────────────

        [Test]
        public void SegmentText_MixedJaZh_JapanesePartHasKanaNearby()
        {
            // Separate Japanese and Chinese parts by more than WindowRadius characters
            // so the Chinese CJK characters have no kana in their local window.
            // "漢字のテスト" (ja, 6 chars) + 12 spaces + "中文内容测试" (zh, 6 chars)
            // The rightmost kana ト is at index 5; the leftmost Chinese 中 is at index 18.
            // Distance = 18 - 5 = 13 > WindowRadius(10), so 中 has no nearby kana.
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en", "zh" });
            var segments = detector.SegmentText("漢字のテスト            中文内容测试");

            // Expect at least one ja segment and one zh segment
            var hasJa = false;
            var hasZh = false;
            foreach (var seg in segments)
            {
                if (seg.language == "ja") hasJa = true;
                if (seg.language == "zh") hasZh = true;
            }
            Assert.IsTrue(hasJa, "Expected Japanese segment for kanji near kana");
            Assert.IsTrue(hasZh, "Expected Chinese segment for isolated CJK");
        }

        [Test]
        public void SegmentText_PureChinese_ClassifiedAsZh()
        {
            // "中文内容测试" — no kana anywhere → all CJK classified as zh
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en", "zh" });
            var segments = detector.SegmentText("中文内容测试");

            Assert.AreEqual(1, segments.Count);
            Assert.AreEqual("zh", segments[0].language);
            Assert.AreEqual("中文内容测试", segments[0].text);
        }

        [Test]
        public void SegmentText_JapaneseKanjiWithKana_ClassifiedAsJa()
        {
            // "漢字のテスト" — kanji adjacent to kana → ja
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en", "zh" });
            var segments = detector.SegmentText("漢字のテスト");

            Assert.AreEqual(1, segments.Count);
            Assert.AreEqual("ja", segments[0].language);
            Assert.AreEqual("漢字のテスト", segments[0].text);
        }

        [Test]
        public void SegmentText_KanjiOnlyNoKana_ClassifiedAsZh()
        {
            // "漢字" — no kana nearby, both ja+zh supported → zh
            // This is the edge case: standalone kanji without kana context
            // defaults to Chinese when zh is available.
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en", "zh" });
            var segments = detector.SegmentText("漢字");

            Assert.AreEqual(1, segments.Count);
            Assert.AreEqual("zh", segments[0].language);
        }

        [Test]
        public void SegmentText_KanjiOnlyNoKana_JaOnlyLanguage_ClassifiedAsJa()
        {
            // "漢字" — no zh in language list → always ja regardless of kana context
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en" });
            var segments = detector.SegmentText("漢字");

            Assert.AreEqual(1, segments.Count);
            Assert.AreEqual("ja", segments[0].language);
        }

        [Test]
        public void SegmentText_ChineseWithJapaneseProperNoun()
        {
            // Chinese text with a Japanese proper noun (kana) embedded.
            // Separate the Chinese portion from the kana by more than WindowRadius
            // so the Chinese CJK characters are not influenced by the kana.
            // "中文内容测试中文内容测试" (12 CJK) + "さくら" (3 kana)
            // 中(0)..试(11), さ(12)..ら(14)
            // Characters at index 0-1 are > 10 away from さ(12), so classified as zh.
            // Characters at indices 2-11 are within 10 of さ(12), so classified as ja.
            // We use a longer prefix to ensure some chars fall outside the window.
            // "中文内容测试再来一些中文" (12 chars, indices 0-11) + "さくら" (indices 12-14)
            // Index 0: distance to さ(12) = 12 > 10 → zh
            // Index 1: distance to さ(12) = 11 > 10 → zh
            // Index 2: distance to さ(12) = 10 = WindowRadius → ja (inclusive)
            // So only indices 0-1 are zh. Let's verify the logic works.
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en", "zh" });
            var segments = detector.SegmentText("中文内容测试再来一些中文さくら");

            // Must have at least one ja segment (さくら + nearby CJK) and at least one zh segment
            var hasJa = false;
            var hasZh = false;
            foreach (var seg in segments)
            {
                if (seg.language == "ja") hasJa = true;
                if (seg.language == "zh") hasZh = true;
            }
            Assert.IsTrue(hasJa, "Expected Japanese segment for kana proper noun");
            Assert.IsTrue(hasZh, "Expected Chinese segment for CJK far from kana");
        }

        [Test]
        public void SegmentText_FarSeparatedJaAndZh_CorrectlyDisambiguated()
        {
            // Japanese kana at start, then enough spacing so that CJK chars
            // at the end are beyond WindowRadius from any kana.
            // "あいう" (indices 0-2) + 20 spaces (indices 3-22) + "中文" (indices 23-24)
            // Distance from う(2) to 中(23) = 21 > WindowRadius(10).
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en", "zh" });
            var input = "あいう" + new string(' ', 20) + "中文";

            var segments = detector.SegmentText(input);

            // Last segment should be zh (中文 is far from kana)
            var lastSegment = segments[segments.Count - 1];
            Assert.AreEqual("zh", lastSegment.language,
                "CJK chars far from kana should be classified as zh");
        }

        [Test]
        public void HasKanaNearby_KanaAtExactRadius_ReturnsTrue()
        {
            // Place kana at exactly WindowRadius distance from the CJK character
            var text = new string(' ', UnicodeLanguageDetector.WindowRadius) + "漢"
                       + new string(' ', UnicodeLanguageDetector.WindowRadius - 1) + "あ";
            var kanjiIndex = UnicodeLanguageDetector.WindowRadius;
            var kanaIndex = kanjiIndex + UnicodeLanguageDetector.WindowRadius;

            // kana is at exactly WindowRadius away — should be found
            Assert.IsTrue(UnicodeLanguageDetector.HasKanaNearby(text, kanjiIndex));
        }

        [Test]
        public void HasKanaNearby_KanaBeyondRadius_ReturnsFalse()
        {
            // Place kana just beyond WindowRadius
            var text = new string(' ', UnicodeLanguageDetector.WindowRadius) + "漢"
                       + new string(' ', UnicodeLanguageDetector.WindowRadius) + "あ";
            var kanjiIndex = UnicodeLanguageDetector.WindowRadius;

            // kana is at WindowRadius + 1 away — should NOT be found
            Assert.IsFalse(UnicodeLanguageDetector.HasKanaNearby(text, kanjiIndex));
        }

        [Test]
        public void HasKanaNearby_KanaBeforeTarget_ReturnsTrue()
        {
            // "あ漢" — kana is 1 char before the CJK character
            Assert.IsTrue(UnicodeLanguageDetector.HasKanaNearby("あ漢", 1));
        }

        [Test]
        public void HasKanaNearby_NoKana_ReturnsFalse()
        {
            Assert.IsFalse(UnicodeLanguageDetector.HasKanaNearby("中文内容", 0));
        }

        [Test]
        public void SegmentText_ZhOnlyLanguage_CjkAlwaysZh()
        {
            // When only zh is supported (no ja), all CJK → zh regardless of kana
            // (kana would not be detected anyway since _hasJa is false)
            var detector = new UnicodeLanguageDetector(new[] { "zh", "en" });
            var segments = detector.SegmentText("中文内容测试");

            Assert.AreEqual(1, segments.Count);
            Assert.AreEqual("zh", segments[0].language);
        }
    }
}