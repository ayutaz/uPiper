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
    }
}
