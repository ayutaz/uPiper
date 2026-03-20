using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using DotNetG2P.Spanish;
using uPiper.Core.Phonemizers.Backend;
using uPiper.Core.Phonemizers.Backend.Chinese;
using uPiper.Core.Phonemizers.Backend.Korean;
using uPiper.Core.Phonemizers.Multilingual;

namespace uPiper.Tests.Editor.Phonemizers
{
    /// <summary>
    /// Phase 5 integration tests for MultilingualPhonemizer with CJK and new language backends.
    /// Verifies that Chinese, Korean, Spanish, French, and Portuguese segments route
    /// to the correct backends through the MultilingualPhonemizer pipeline.
    /// </summary>
    [TestFixture]
    public class MultilingualPhonemizerPhase5Tests
    {
        // ── Constructor & language configuration ─────────────────────────────

        [Test]
        public void Constructor_WithChineseLanguage_CreatesInstance()
        {
            var phonemizer = new MultilingualPhonemizer(
                new[] { "zh", "en" },
                defaultLatinLanguage: "en");

            Assert.IsNotNull(phonemizer);
            Assert.IsFalse(phonemizer.IsInitialized);
            Assert.AreEqual(2, phonemizer.Languages.Count);
            Assert.IsTrue(phonemizer.Languages.Contains("zh"));
            phonemizer.Dispose();
        }

        [Test]
        public void Constructor_WithKoreanLanguage_CreatesInstance()
        {
            var phonemizer = new MultilingualPhonemizer(
                new[] { "ko", "en" },
                defaultLatinLanguage: "en");

            Assert.IsNotNull(phonemizer);
            Assert.AreEqual(2, phonemizer.Languages.Count);
            Assert.IsTrue(phonemizer.Languages.Contains("ko"));
            phonemizer.Dispose();
        }

        [Test]
        public void Constructor_AllSevenLanguages_CreatesInstance()
        {
            var phonemizer = new MultilingualPhonemizer(
                new[] { "ja", "en", "zh", "ko", "es", "fr", "pt" },
                defaultLatinLanguage: "en");

            Assert.IsNotNull(phonemizer);
            Assert.AreEqual(7, phonemizer.Languages.Count);
            phonemizer.Dispose();
        }

        [Test]
        public void Constructor_WithPrebuiltChineseBackend_AcceptsIt()
        {
            var zhBackend = new ChinesePhonemizerBackend();
            var phonemizer = new MultilingualPhonemizer(
                new[] { "zh", "en" },
                defaultLatinLanguage: "en",
                zhPhonemizer: zhBackend);

            Assert.IsNotNull(phonemizer);
            Assert.AreEqual(2, phonemizer.Languages.Count);
            phonemizer.Dispose();
        }

        [Test]
        public void Constructor_WithPrebuiltKoreanBackend_AcceptsIt()
        {
            var koBackend = new KoreanPhonemizerBackend();
            var phonemizer = new MultilingualPhonemizer(
                new[] { "ko", "en" },
                defaultLatinLanguage: "en",
                koPhonemizer: koBackend);

            Assert.IsNotNull(phonemizer);
            Assert.AreEqual(2, phonemizer.Languages.Count);
            phonemizer.Dispose();
        }

        // ── Language constants & ID mappings ─────────────────────────────────

        [Test]
        public void TestLanguageConstants_StandardCodeSupported()
        {
            // Verify that all Phase 5 language codes are accepted by the constructor
            var allLangs = new[] { "ja", "en", "zh", "ko", "es", "fr", "pt" };
            var phonemizer = new MultilingualPhonemizer(allLangs);

            Assert.AreEqual(7, phonemizer.Languages.Count);
            foreach (var lang in allLangs)
            {
                Assert.IsTrue(phonemizer.Languages.Contains(lang),
                    $"Language '{lang}' should be in the language list");
            }

            phonemizer.Dispose();
        }

        [Test]
        public void TestLanguageConstants_SubsetSupported()
        {
            // A subset of languages should also work
            var phonemizer = new MultilingualPhonemizer(new[] { "zh", "ko" });
            Assert.AreEqual(2, phonemizer.Languages.Count);
            phonemizer.Dispose();
        }

        // ── Spanish segment processing ───────────────────────────────────────

        [Test]
        public void TestSpanishSegmentProcessing()
        {
            // Pre-build Spanish G2P engine and pass to MultilingualPhonemizer
            var esEngine = new SpanishG2PEngine();

            var phonemizer = new MultilingualPhonemizer(
                new[] { "es", "en" },
                defaultLatinLanguage: "es",
                esEngine: esEngine);

            Task.Run(async () => await phonemizer.InitializeAsync()).GetAwaiter().GetResult();

            var result = Task.Run(async () =>
                await phonemizer.PhonemizeWithProsodyAsync("hola mundo"))
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Spanish text should produce phonemes through multilingual pipeline");

            phonemizer.Dispose();
        }

        // ── Chinese segment processing ───────────────────────────────────────

        [Test]
        public void TestChineseSegmentProcessing()
        {
            var zhBackend = new ChinesePhonemizerBackend();
            var initOk = Task.Run(async () => await zhBackend.InitializeAsync())
                .GetAwaiter().GetResult();

            if (!initOk)
            {
                Assert.Ignore("ChinesePhonemizerBackend not available");
                return;
            }

            var phonemizer = new MultilingualPhonemizer(
                new[] { "zh", "en" },
                defaultLatinLanguage: "en",
                zhPhonemizer: zhBackend);

            Task.Run(async () => await phonemizer.InitializeAsync()).GetAwaiter().GetResult();

            var result = Task.Run(async () =>
                await phonemizer.PhonemizeWithProsodyAsync("你好世界"))
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Chinese text should produce phonemes through multilingual pipeline");
            Assert.AreEqual("zh", result.DetectedPrimaryLanguage,
                "Primary language should be detected as Chinese");

            phonemizer.Dispose();
        }

        // ── Korean segment processing ────────────────────────────────────────

        [Test]
        public void TestKoreanSegmentProcessing()
        {
            var koBackend = new KoreanPhonemizerBackend();
            var initOk = Task.Run(async () => await koBackend.InitializeAsync())
                .GetAwaiter().GetResult();

            Assert.IsTrue(initOk, "Korean backend should initialize");

            var phonemizer = new MultilingualPhonemizer(
                new[] { "ko", "en" },
                defaultLatinLanguage: "en",
                koPhonemizer: koBackend);

            Task.Run(async () => await phonemizer.InitializeAsync()).GetAwaiter().GetResult();

            var result = Task.Run(async () =>
                await phonemizer.PhonemizeWithProsodyAsync("안녕하세요"))
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Korean text should produce phonemes through multilingual pipeline");
            Assert.AreEqual("ko", result.DetectedPrimaryLanguage,
                "Primary language should be detected as Korean");

            phonemizer.Dispose();
        }

        // ── French segment processing ────────────────────────────────────────

        [Test]
        public void TestFrenchSegmentProcessing()
        {
            // French backend auto-created by MultilingualPhonemizer.InitializeAsync
            var phonemizer = new MultilingualPhonemizer(
                new[] { "fr", "en" },
                defaultLatinLanguage: "fr");

            Task.Run(async () => await phonemizer.InitializeAsync()).GetAwaiter().GetResult();
            Assert.IsTrue(phonemizer.IsInitialized);

            var result = Task.Run(async () =>
                await phonemizer.PhonemizeWithProsodyAsync("bonjour"))
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "French text should produce phonemes through multilingual pipeline");

            phonemizer.Dispose();
        }

        // ── Portuguese segment processing ────────────────────────────────────

        [Test]
        public void TestPortugueseSegmentProcessing()
        {
            var phonemizer = new MultilingualPhonemizer(
                new[] { "pt", "en" },
                defaultLatinLanguage: "pt");

            Task.Run(async () => await phonemizer.InitializeAsync()).GetAwaiter().GetResult();
            Assert.IsTrue(phonemizer.IsInitialized);

            var result = Task.Run(async () =>
                await phonemizer.PhonemizeWithProsodyAsync("bom dia"))
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Portuguese text should produce phonemes through multilingual pipeline");

            phonemizer.Dispose();
        }

        // ── Mixed language text ──────────────────────────────────────────────

        [Test]
        public void TestMixedChineseJapanese_CJKDisambiguationWithKana()
        {
            // When both ja and zh are configured, Kana presence disambiguates CJK
            var detector = new UnicodeLanguageDetector(new[] { "ja", "zh", "en" });

            // Text with Kana: CJK chars should be assigned to "ja"
            var segments = detector.SegmentText("漢字テスト");
            Assert.IsTrue(segments.Count >= 1);
            // Kana present -> CJK chars assigned to "ja"
            Assert.AreEqual("ja", segments[0].language,
                "CJK with Kana context should be detected as Japanese");
        }

        [Test]
        public void TestMixedChineseJapanese_PureChinese()
        {
            // When both ja and zh are configured, pure CJK without Kana -> "zh"
            var detector = new UnicodeLanguageDetector(new[] { "ja", "zh", "en" });

            var segments = detector.SegmentText("你好世界");
            Assert.IsTrue(segments.Count >= 1);
            Assert.AreEqual("zh", segments[0].language,
                "Pure CJK without Kana should be detected as Chinese");
        }

        [Test]
        public void TestMixedKoreanEnglish_SegmentsCorrectly()
        {
            var detector = new UnicodeLanguageDetector(new[] { "ko", "en" });

            var segments = detector.SegmentText("한국어 Korean language");
            Assert.IsTrue(segments.Count >= 2,
                "Mixed Korean/English should produce at least 2 segments");

            Assert.AreEqual("ko", segments[0].language,
                "Korean text segment should be detected as Korean");

            // The English portion should be detected as "en"
            var enSegment = segments.FirstOrDefault(s => s.language == "en");
            Assert.IsNotNull(enSegment, "Should have an English segment");
        }

        [Test]
        public void TestMixedJapaneseSpanish_SegmentsCorrectly()
        {
            // When "es" is defaultLatinLanguage, Latin text routes to Spanish
            var detector = new UnicodeLanguageDetector(
                new[] { "ja", "es" }, defaultLatinLanguage: "es");

            var segments = detector.SegmentText("今日はhola");
            Assert.IsTrue(segments.Count >= 2,
                "Mixed Japanese/Spanish should produce at least 2 segments");

            Assert.AreEqual("ja", segments[0].language,
                "Japanese portion should be detected as 'ja'");

            var latinSegment = segments.FirstOrDefault(s => s.language == "es");
            Assert.IsNotNull(latinSegment,
                "Latin portion should be detected as 'es' when defaultLatinLanguage is 'es'");
        }

        // ── Unicode detector with new languages ──────────────────────────────

        [Test]
        public void TestUnicodeDetectorWithNewLanguages_HangulDetection()
        {
            var detector = new UnicodeLanguageDetector(new[] { "ja", "en", "ko", "zh" });

            // '한' = U+D55C (Hangul Syllable)
            Assert.AreEqual("ko", detector.DetectChar('\uD55C'),
                "Hangul syllable should detect as Korean");
        }

        [Test]
        public void TestUnicodeDetectorWithNewLanguages_CJKWithZhSupport()
        {
            var detector = new UnicodeLanguageDetector(new[] { "zh", "en" });

            // CJK char without Kana context, zh supported -> "zh"
            Assert.AreEqual("zh", detector.DetectChar('\u4F60'), // '你'
                "CJK char should detect as Chinese when zh is supported and no Kana context");
        }

        [Test]
        public void TestUnicodeDetectorWithNewLanguages_LatinWithSpanishDefault()
        {
            var detector = new UnicodeLanguageDetector(
                new[] { "es", "ja" }, defaultLatinLanguage: "es");

            Assert.AreEqual("es", detector.DetectChar('a'),
                "Latin char should detect as 'es' when defaultLatinLanguage is 'es'");
        }

        [Test]
        public void TestUnicodeDetectorWithNewLanguages_AllScriptsSegmented()
        {
            var detector = new UnicodeLanguageDetector(
                new[] { "ja", "en", "zh", "ko" });

            // Segment a text with all four scripts
            var segments = detector.SegmentText("Hello こんにちは 你好 안녕");

            Assert.IsTrue(segments.Count >= 3,
                "Text with multiple scripts should produce multiple segments");

            // Check that different languages are detected
            var languages = segments.Select(s => s.language).Distinct().ToList();
            Assert.IsTrue(languages.Contains("en"), "Should detect English segment");
            Assert.IsTrue(languages.Contains("ja"), "Should detect Japanese segment");
            Assert.IsTrue(languages.Contains("ko"), "Should detect Korean segment");
        }

        // ── All languages initialization ─────────────────────────────────────

        [Test]
        public void TestAllLanguagesInitialized_EnOnly()
        {
            // Minimal test: English-only should always work
            var phonemizer = new MultilingualPhonemizer(
                new[] { "en" },
                defaultLatinLanguage: "en");

            Task.Run(async () => await phonemizer.InitializeAsync()).GetAwaiter().GetResult();
            Assert.IsTrue(phonemizer.IsInitialized);
            phonemizer.Dispose();
        }

        [Test]
        public void TestAllLanguagesInitialized_KoreanBackend()
        {
            // Korean is pure algorithmic - should always initialize successfully
            var koBackend = new KoreanPhonemizerBackend();
            var result = Task.Run(async () => await koBackend.InitializeAsync())
                .GetAwaiter().GetResult();

            Assert.IsTrue(result, "Korean backend should initialize (pure algorithmic, no data files)");
            Assert.IsTrue(koBackend.IsAvailable);
            koBackend.Dispose();
        }

        [Test]
        public void TestAllLanguagesInitialized_SpanishEngine()
        {
            // Spanish DotNetG2P engine - parameterless constructor, always available
            var esEngine = new SpanishG2PEngine();

            // Verify basic phonemization works
            var phonemes = esEngine.ToPuaPhonemes("hola");
            Assert.IsNotNull(phonemes, "SpanishG2PEngine should produce phonemes");
            Assert.IsTrue(phonemes.Length > 0, "SpanishG2PEngine should produce non-empty phonemes for 'hola'");

            esEngine.Dispose();
        }

        [Test]
        public void TestAllLanguagesInitialized_ChineseBackend()
        {
            var zhBackend = new ChinesePhonemizerBackend();
            var result = Task.Run(async () => await zhBackend.InitializeAsync())
                .GetAwaiter().GetResult();

            // Chinese backend may or may not initialize depending on environment
            Assert.IsNotNull(zhBackend);
            if (result)
            {
                Assert.IsTrue(zhBackend.IsAvailable);
            }

            zhBackend.Dispose();
        }

        // ── Prosody propagation through multilingual pipeline ────────────────

        [Test]
        public void TestProsodyPropagation_KoreanThroughPipeline()
        {
            var koBackend = new KoreanPhonemizerBackend();
            Task.Run(async () => await koBackend.InitializeAsync()).GetAwaiter().GetResult();

            var phonemizer = new MultilingualPhonemizer(
                new[] { "ko", "en" },
                defaultLatinLanguage: "en",
                koPhonemizer: koBackend);

            Task.Run(async () => await phonemizer.InitializeAsync()).GetAwaiter().GetResult();

            var result = Task.Run(async () =>
                await phonemizer.PhonemizeWithProsodyAsync("한국어"))
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.ProsodyA1);
            Assert.IsNotNull(result.ProsodyA2);
            Assert.IsNotNull(result.ProsodyA3);

            // Prosody arrays should be aligned
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA1.Length);
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA2.Length);
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA3.Length);

            phonemizer.Dispose();
        }

        [Test]
        public void TestProsodyPropagation_EmptyTextReturnsEmptyArrays()
        {
            var phonemizer = new MultilingualPhonemizer(
                new[] { "ko", "en" },
                defaultLatinLanguage: "en");

            Task.Run(async () => await phonemizer.InitializeAsync()).GetAwaiter().GetResult();

            var result = Task.Run(async () =>
                await phonemizer.PhonemizeWithProsodyAsync(""))
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Phonemes.Length);
            Assert.AreEqual(0, result.ProsodyA1.Length);
            Assert.AreEqual(0, result.ProsodyA2.Length);
            Assert.AreEqual(0, result.ProsodyA3.Length);

            phonemizer.Dispose();
        }

        // ── Dispose behavior ─────────────────────────────────────────────────

        [Test]
        public void Dispose_WithAllBackends_DoesNotThrow()
        {
            var koBackend = new KoreanPhonemizerBackend();
            var esEngine = new SpanishG2PEngine();

            var phonemizer = new MultilingualPhonemizer(
                new[] { "ko", "es", "en" },
                defaultLatinLanguage: "en",
                koPhonemizer: koBackend,
                esEngine: esEngine);

            Assert.DoesNotThrow(() => phonemizer.Dispose());
        }

        [Test]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            var phonemizer = new MultilingualPhonemizer(
                new[] { "ko", "zh", "en" });

            Assert.DoesNotThrow(() =>
            {
                phonemizer.Dispose();
                phonemizer.Dispose();
            });
        }

        // ── Error handling ───────────────────────────────────────────────────

        [Test]
        public void PhonemizeBeforeInitialize_ThrowsInvalidOperationException()
        {
            var phonemizer = new MultilingualPhonemizer(new[] { "ko", "en" });

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await phonemizer.PhonemizeWithProsodyAsync("안녕"));

            phonemizer.Dispose();
        }
    }
}