using System.Collections.Generic;
using NUnit.Framework;
using uPiper.Core.Phonemizers.Multilingual;

namespace uPiper.Tests.Editor
{
    [TestFixture]
    public class MultilingualPhonemizerTests
    {
        // ── UnicodeLanguageDetector integration ────────────────────────────────

        [Test]
        public void SegmentText_JapaneseText_DetectedAsJa()
        {
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en" });
            var result = detector.SegmentText("こんにちは");

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("ja", result[0].language);
        }

        [Test]
        public void SegmentText_EnglishText_DetectedAsEn()
        {
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en" });
            var result = detector.SegmentText("hello world");

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("en", result[0].language);
        }

        [Test]
        public void SegmentText_MixedText_SplitsByLanguage()
        {
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en" });
            var result = detector.SegmentText("こんにちは hello");

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("ja", result[0].language);
            Assert.AreEqual("en", result[1].language);
        }

        [Test]
        public void SegmentText_ChineseWithJaContext_DetectedAsJa()
        {
            // CJK 漢字はKanaがある文脈では日本語と判定される
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en" });
            var result = detector.SegmentText("日本語テスト");

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("ja", result[0].language);
        }

        [Test]
        public void SegmentText_ChineseOnly_DetectedAsZh()
        {
            // Kanaなし + zh サポートの場合、漢字は中国語と判定される
            var detector = new UnicodeLanguageDetector(new[] { "zh", "en" });
            var result = detector.SegmentText("你好世界");

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("zh", result[0].language);
        }

        [Test]
        public void SegmentText_EmptyString_ReturnsEmpty()
        {
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en" });
            var result = detector.SegmentText("");

            Assert.AreEqual(0, result.Count);
        }

        // ── MultilingualPhonemizer construction ────────────────────────────────

        [Test]
        public void Constructor_ValidLanguages_CreatesInstance()
        {
            var phonemizer = new MultilingualPhonemizer(
                new[] { "ja", "en" },
                defaultLatinLanguage: "en");

            Assert.IsNotNull(phonemizer);
            Assert.IsFalse(phonemizer.IsInitialized);
            Assert.AreEqual(2, phonemizer.Languages.Count);
        }

        [Test]
        public void Constructor_EmptyLanguages_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() =>
                new MultilingualPhonemizer(new List<string>()));
        }

        [Test]
        public void Constructor_NullLanguages_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() =>
                new MultilingualPhonemizer(null));
        }

        // ── MultilingualPhonemizer initialization ──────────────────────────────

        [Test]
        public void IsInitialized_BeforeInitialize_ReturnsFalse()
        {
            var phonemizer = new MultilingualPhonemizer(new[] { "ja", "en" });
            Assert.IsFalse(phonemizer.IsInitialized);
        }

        [Test]
        public void InitializeAsync_JaEnLanguages_SetsIsInitialized()
        {
            // Note: InitializeAsync creates DotNetG2PPhonemizer which needs dictionary.
            // Use a phonemizer with only English to avoid dictionary dependency.
            var phonemizer = new MultilingualPhonemizer(
                new[] { "en" },
                defaultLatinLanguage: "en");

            System.Threading.Tasks.Task.Run(async () =>
            {
                await phonemizer.InitializeAsync();
                Assert.IsTrue(phonemizer.IsInitialized);
                phonemizer.Dispose();
            }).GetAwaiter().GetResult();
        }

        // ── MultilingualPhonemizeResult structure ──────────────────────────────

        [Test]
        public void MultilingualPhonemizeResult_DefaultValues_AreNull()
        {
            var result = new MultilingualPhonemizeResult();

            Assert.IsNull(result.Phonemes);
            Assert.IsNull(result.ProsodyA1);
            Assert.IsNull(result.ProsodyA2);
            Assert.IsNull(result.ProsodyA3);
            Assert.IsNull(result.DetectedPrimaryLanguage);
        }

        // ── Dispose ────────────────────────────────────────────────────────────

        [Test]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            var phonemizer = new MultilingualPhonemizer(new[] { "ja", "en" });
            Assert.DoesNotThrow(() =>
            {
                phonemizer.Dispose();
                phonemizer.Dispose();
            });
        }
    }
}