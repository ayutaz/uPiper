using System;
using NUnit.Framework;
using uPiper.Core.Phonemizers.Multilingual;

namespace uPiper.Tests.Editor.Phonemizers
{
    /// <summary>
    /// Comprehensive tests for <see cref="LanguageConstants"/>.
    /// Covers language ID constants, language code constants, grouping arrays,
    /// and all lookup/classification methods.
    /// </summary>
    [TestFixture]
    public class LanguageConstantsTests
    {
        // ── Language ID Constants ─────────────────────────────────────────────

        [Test]
        public void LanguageIdJapanese_Is0()
        {
            Assert.AreEqual(0, LanguageConstants.LanguageIdJapanese);
        }

        [Test]
        public void LanguageIdEnglish_Is1()
        {
            Assert.AreEqual(1, LanguageConstants.LanguageIdEnglish);
        }

        [Test]
        public void LanguageIdChinese_Is2()
        {
            Assert.AreEqual(2, LanguageConstants.LanguageIdChinese);
        }

        [Test]
        public void LanguageIdSpanish_Is3()
        {
            Assert.AreEqual(3, LanguageConstants.LanguageIdSpanish);
        }

        [Test]
        public void LanguageIdFrench_Is4()
        {
            Assert.AreEqual(4, LanguageConstants.LanguageIdFrench);
        }

        [Test]
        public void LanguageIdPortuguese_Is5()
        {
            Assert.AreEqual(5, LanguageConstants.LanguageIdPortuguese);
        }

        [Test]
        public void LanguageIdKorean_Is6()
        {
            Assert.AreEqual(6, LanguageConstants.LanguageIdKorean);
        }

        // ── Language Code Constants ───────────────────────────────────────────

        [Test]
        public void CodeJapanese_IsJa()
        {
            Assert.AreEqual("ja", LanguageConstants.CodeJapanese);
        }

        [Test]
        public void CodeEnglish_IsEn()
        {
            Assert.AreEqual("en", LanguageConstants.CodeEnglish);
        }

        [Test]
        public void CodeChinese_IsZh()
        {
            Assert.AreEqual("zh", LanguageConstants.CodeChinese);
        }

        [Test]
        public void CodeSpanish_IsEs()
        {
            Assert.AreEqual("es", LanguageConstants.CodeSpanish);
        }

        [Test]
        public void CodeFrench_IsFr()
        {
            Assert.AreEqual("fr", LanguageConstants.CodeFrench);
        }

        [Test]
        public void CodePortuguese_IsPt()
        {
            Assert.AreEqual("pt", LanguageConstants.CodePortuguese);
        }

        [Test]
        public void CodeKorean_IsKo()
        {
            Assert.AreEqual("ko", LanguageConstants.CodeKorean);
        }

        // ── AllLanguages Array ────────────────────────────────────────────────

        [Test]
        public void AllLanguages_Contains7Languages()
        {
            Assert.AreEqual(7, LanguageConstants.AllLanguages.Length);
        }

        [Test]
        public void AllLanguages_ContainsAllExpectedCodes()
        {
            var expected = new[] { "ja", "en", "zh", "es", "fr", "pt", "ko" };
            CollectionAssert.AreEquivalent(expected, LanguageConstants.AllLanguages);
        }

        // ── LatinLanguages / CjkLanguages Arrays ─────────────────────────────

        [Test]
        public void LatinLanguages_ContainsEnEsFrPt()
        {
            var expected = new[] { "en", "es", "fr", "pt" };
            CollectionAssert.AreEquivalent(expected, LanguageConstants.LatinLanguages);
        }

        [Test]
        public void CjkLanguages_ContainsJaZhKo()
        {
            var expected = new[] { "ja", "zh", "ko" };
            CollectionAssert.AreEquivalent(expected, LanguageConstants.CjkLanguages);
        }

        // ── GetLanguageId ─────────────────────────────────────────────────────

        [TestCase("ja", 0)]
        [TestCase("en", 1)]
        [TestCase("zh", 2)]
        [TestCase("es", 3)]
        [TestCase("fr", 4)]
        [TestCase("pt", 5)]
        [TestCase("ko", 6)]
        public void GetLanguageId_ValidCode_ReturnsCorrectId(string code, int expectedId)
        {
            Assert.AreEqual(expectedId, LanguageConstants.GetLanguageId(code));
        }

        [Test]
        public void GetLanguageId_InvalidCode_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => LanguageConstants.GetLanguageId("de"));
        }

        [Test]
        public void GetLanguageId_CaseIsExact_UppercaseThrows()
        {
            // The implementation uses exact string comparison; uppercase codes are not recognized.
            Assert.Throws<ArgumentException>(() => LanguageConstants.GetLanguageId("JA"));
        }

        [Test]
        public void GetLanguageId_EmptyString_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => LanguageConstants.GetLanguageId(""));
        }

        [Test]
        public void GetLanguageId_Null_ThrowsException()
        {
            // Null key causes ArgumentNullException from Dictionary.TryGetValue(null).
            Assert.Throws<ArgumentNullException>(() => LanguageConstants.GetLanguageId(null));
        }

        // ── GetLanguageCode ───────────────────────────────────────────────────

        [TestCase(0, "ja")]
        [TestCase(1, "en")]
        [TestCase(2, "zh")]
        [TestCase(3, "es")]
        [TestCase(4, "fr")]
        [TestCase(5, "pt")]
        [TestCase(6, "ko")]
        public void GetLanguageCode_ValidId_ReturnsCorrectCode(int id, string expectedCode)
        {
            Assert.AreEqual(expectedCode, LanguageConstants.GetLanguageCode(id));
        }

        [Test]
        public void GetLanguageCode_InvalidId_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => LanguageConstants.GetLanguageCode(99));
        }

        [Test]
        public void GetLanguageCode_NegativeId_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => LanguageConstants.GetLanguageCode(-1));
        }

        // ── IsLatinLanguage ───────────────────────────────────────────────────

        [Test]
        public void IsLatinLanguage_En_ReturnsTrue()
        {
            Assert.IsTrue(LanguageConstants.IsLatinLanguage("en"));
        }

        [Test]
        public void IsLatinLanguage_Es_ReturnsTrue()
        {
            Assert.IsTrue(LanguageConstants.IsLatinLanguage("es"));
        }

        [Test]
        public void IsLatinLanguage_Fr_ReturnsTrue()
        {
            Assert.IsTrue(LanguageConstants.IsLatinLanguage("fr"));
        }

        [Test]
        public void IsLatinLanguage_Pt_ReturnsTrue()
        {
            Assert.IsTrue(LanguageConstants.IsLatinLanguage("pt"));
        }

        [Test]
        public void IsLatinLanguage_Ja_ReturnsFalse()
        {
            Assert.IsFalse(LanguageConstants.IsLatinLanguage("ja"));
        }

        [Test]
        public void IsLatinLanguage_Zh_ReturnsFalse()
        {
            Assert.IsFalse(LanguageConstants.IsLatinLanguage("zh"));
        }

        [Test]
        public void IsLatinLanguage_Ko_ReturnsFalse()
        {
            Assert.IsFalse(LanguageConstants.IsLatinLanguage("ko"));
        }

        // ── IsCjkLanguage ─────────────────────────────────────────────────────

        [Test]
        public void IsCjkLanguage_Ja_ReturnsTrue()
        {
            Assert.IsTrue(LanguageConstants.IsCjkLanguage("ja"));
        }

        [Test]
        public void IsCjkLanguage_Zh_ReturnsTrue()
        {
            Assert.IsTrue(LanguageConstants.IsCjkLanguage("zh"));
        }

        [Test]
        public void IsCjkLanguage_Ko_ReturnsTrue()
        {
            Assert.IsTrue(LanguageConstants.IsCjkLanguage("ko"));
        }

        [Test]
        public void IsCjkLanguage_En_ReturnsFalse()
        {
            Assert.IsFalse(LanguageConstants.IsCjkLanguage("en"));
        }

        [Test]
        public void IsCjkLanguage_Es_ReturnsFalse()
        {
            Assert.IsFalse(LanguageConstants.IsCjkLanguage("es"));
        }

        // ── IsSupportedLanguage ───────────────────────────────────────────────

        [TestCase("ja")]
        [TestCase("en")]
        [TestCase("zh")]
        [TestCase("es")]
        [TestCase("fr")]
        [TestCase("pt")]
        [TestCase("ko")]
        public void IsSupportedLanguage_AllSupported_ReturnTrue(string code)
        {
            Assert.IsTrue(LanguageConstants.IsSupportedLanguage(code));
        }

        [Test]
        public void IsSupportedLanguage_Unsupported_ReturnsFalse()
        {
            Assert.IsFalse(LanguageConstants.IsSupportedLanguage("de"));
        }

        [Test]
        public void IsSupportedLanguage_EmptyString_ReturnsFalse()
        {
            Assert.IsFalse(LanguageConstants.IsSupportedLanguage(""));
        }

        // ── Cross-consistency checks ──────────────────────────────────────────

        [Test]
        public void LatinAndCjk_AreDisjoint()
        {
            foreach (var latin in LanguageConstants.LatinLanguages)
            {
                CollectionAssert.DoesNotContain(LanguageConstants.CjkLanguages, latin,
                    $"'{latin}' should not appear in both LatinLanguages and CjkLanguages.");
            }
        }

        [Test]
        public void LatinAndCjk_CoverAllLanguages()
        {
            var combined = new string[LanguageConstants.LatinLanguages.Length + LanguageConstants.CjkLanguages.Length];
            LanguageConstants.LatinLanguages.CopyTo(combined, 0);
            LanguageConstants.CjkLanguages.CopyTo(combined, LanguageConstants.LatinLanguages.Length);

            CollectionAssert.AreEquivalent(LanguageConstants.AllLanguages, combined);
        }

        [Test]
        public void GetLanguageId_And_GetLanguageCode_AreInverses()
        {
            foreach (var code in LanguageConstants.AllLanguages)
            {
                var id = LanguageConstants.GetLanguageId(code);
                var roundTripped = LanguageConstants.GetLanguageCode(id);
                Assert.AreEqual(code, roundTripped,
                    $"Round-trip failed: GetLanguageCode(GetLanguageId(\"{code}\")) returned \"{roundTripped}\".");
            }
        }
    }
}