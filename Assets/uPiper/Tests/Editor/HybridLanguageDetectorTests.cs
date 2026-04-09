using System.Collections.Generic;
using NUnit.Framework;
using uPiper.Core.Phonemizers.Multilingual;

namespace uPiper.Tests.Editor
{
    [TestFixture]
    public class HybridLanguageDetectorTests
    {
        private HybridLanguageDetector _detector;
        private HybridLanguageDetector _detectorNoTrigram;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var languages = new List<string> { "ja", "en", "zh", "es", "fr", "pt", "ko" };

            // Full hybrid detector with trigram profiles
            var profiles = TrigramProfileLoader.LoadSync() ?? CreateMinimalProfiles();
            var trigramDetector = new TrigramLanguageDetector(profiles);
            var unicodeDetector = new UnicodeLanguageDetector(languages, "en");
            _detector = new HybridLanguageDetector(
                unicodeDetector, trigramDetector, languages, "en");

            // Unicode-only detector (trigram = null)
            var unicodeDetector2 = new UnicodeLanguageDetector(languages, "en");
            _detectorNoTrigram = new HybridLanguageDetector(
                unicodeDetector2, null, languages, "en");
        }

        // ── SegmentText: mixed CJK + Latin ───────────────────────────────────

        [Test]
        public void SegmentText_JapaneseAndFrench_CorrectSegments()
        {
            var segments = _detector.SegmentText(
                "\u3053\u3093\u306b\u3061\u306f Bonjour le monde et bienvenue dans notre pays");

            Assert.GreaterOrEqual(segments.Count, 2,
                "Should have at least Japanese and Latin segments");

            // First segment should be Japanese
            Assert.AreEqual("ja", segments[0].language);

            // Find the Latin segment and verify it's classified as French
            var hasLatinSegment = false;
            for (var i = 0; i < segments.Count; i++)
            {
                if (LanguageConstants.IsLatinLanguage(segments[i].language))
                {
                    Assert.AreEqual("fr", segments[i].language,
                        "French text should be detected as fr, not en");
                    hasLatinSegment = true;
                }
            }
            Assert.IsTrue(hasLatinSegment, "Should contain a Latin segment");
        }

        [Test]
        public void SegmentText_ChineseAndSpanish_CorrectSegments()
        {
            var segments = _detector.SegmentText(
                "\u4f60\u597d\u4e16\u754c Buenos dias como estas hoy espero que estes bien");

            Assert.GreaterOrEqual(segments.Count, 2,
                "Should have at least Chinese and Latin segments");

            // First segment should be Chinese
            Assert.AreEqual("zh", segments[0].language);

            // Find the Latin segment
            var hasLatinSegment = false;
            for (var i = 0; i < segments.Count; i++)
            {
                if (LanguageConstants.IsLatinLanguage(segments[i].language))
                {
                    Assert.AreEqual("es", segments[i].language,
                        "Spanish text should be detected as es");
                    hasLatinSegment = true;
                }
            }
            Assert.IsTrue(hasLatinSegment, "Should contain a Latin segment");
        }

        [Test]
        public void SegmentText_LatinOnly_SingleLanguageList_SkipsTrigram()
        {
            // With only ja+en, there's only 1 Latin language -> trigram is unnecessary
            var languages = new List<string> { "ja", "en" };
            var unicodeDetector = new UnicodeLanguageDetector(languages, "en");
            var detector = new HybridLanguageDetector(
                unicodeDetector, null, languages, "en");

            var segments = detector.SegmentText("Bonjour le monde");

            Assert.AreEqual(1, segments.Count);
            Assert.AreEqual("en", segments[0].language,
                "With only en as Latin language, everything Latin should be en");
        }

        [Test]
        public void SegmentText_NoProfiles_FallsBackToUnicode()
        {
            var segments = _detectorNoTrigram.SegmentText(
                "Bonjour le monde et bienvenue");

            Assert.AreEqual(1, segments.Count);
            Assert.AreEqual("en", segments[0].language,
                "Without trigram profiles, Latin text should fall back to default (en)");
        }

        // ── SegmentText: edge cases ──────────────────────────────────────────

        [Test]
        public void SegmentText_EmptyText_ReturnsEmpty()
        {
            var segments = _detector.SegmentText("");
            Assert.AreEqual(0, segments.Count);
        }

        [Test]
        public void SegmentText_NullText_ReturnsEmpty()
        {
            var segments = _detector.SegmentText(null);
            Assert.AreEqual(0, segments.Count);
        }

        [Test]
        public void SegmentText_PureEnglish_DetectsEn()
        {
            var segments = _detector.SegmentText(
                "The weather is beautiful today and we should go outside for a walk");

            Assert.AreEqual(1, segments.Count);
            Assert.AreEqual("en", segments[0].language);
        }

        [Test]
        public void SegmentText_CJKAndLatinMixed_CorrectlySplits()
        {
            var segments = _detector.SegmentText(
                "\u304a\u306f\u3088\u3046 Good morning \u3067\u3059\u306d");

            // Should have at least ja and en segments
            Assert.GreaterOrEqual(segments.Count, 2);

            var hasJa = false;
            var hasEn = false;
            for (var i = 0; i < segments.Count; i++)
            {
                if (segments[i].language == "ja") hasJa = true;
                if (segments[i].language == "en") hasEn = true;
            }
            Assert.IsTrue(hasJa, "Should detect Japanese segments");
            Assert.IsTrue(hasEn, "Should detect English segments");
        }

        // ── ILanguageDetector interface ──────────────────────────────────────

        [Test]
        public void DefaultLatinLanguage_ReturnsConfiguredValue()
        {
            Assert.AreEqual("en", _detector.DefaultLatinLanguage);
        }

        [Test]
        public void Languages_ReturnsConfiguredList()
        {
            Assert.AreEqual(7, _detector.Languages.Count);
        }

        // ── UnicodeLanguageDetector ILanguageDetector implementation ─────────

        [Test]
        public void UnicodeDetector_ImplementsILanguageDetector()
        {
            var languages = new List<string> { "ja", "en" };
            ILanguageDetector detector = new UnicodeLanguageDetector(languages, "en");

            Assert.AreEqual("en", detector.DefaultLatinLanguage);
            Assert.AreEqual(2, detector.Languages.Count);

            var segments = detector.SegmentText("Hello");
            Assert.AreEqual(1, segments.Count);
            Assert.AreEqual("en", segments[0].language);
        }

        // ── Helper ───────────────────────────────────────────────────────────

        private static Dictionary<string, TrigramProfile> CreateMinimalProfiles()
        {
            return new Dictionary<string, TrigramProfile>
            {
                ["en"] = new TrigramProfile("en", new[]
                {
                    " th", "the", "he ", " an", "and", "nd ", " to", "to ", " of",
                    "of ", " in", "in ", " is", "ion", "tio", "ati", " a ", "ed ",
                    "is ", "er ", " co", " re", " be", "or ", " wa", "on ", " ha",
                    "ent", " fo", "for", "hat", "tha", " wi", "wit", "ith", "ing",
                    "ng ", "her", " he", " it", "al ", " st", "re ", "ere", " on",
                    "ter", " no", "nt ", "an ", " wh", " de", "es ", "was", "as "
                }),
                ["es"] = new TrigramProfile("es", new[]
                {
                    " de", "de ", " la", " en", "os ", "la ", "ion", "aci", "cion",
                    " lo", "as ", "el ", " el", "es ", "ent", "en ", " co", "do ",
                    "der", "ere", "rec", "ech", "cho", "ho ", " se", "to ", " a ",
                    "con", "nte", " to", "tod", "oda", "da ", " pe", "per", "ers",
                    "rso", "son", "ona", "na ", " po", "por", "or ", " su", " qu",
                    "que", "ue ", " pr", "pro", "ra ", "te ", " li", "lib", "ibe"
                }),
                ["fr"] = new TrigramProfile("fr", new[]
                {
                    " de", "de ", " le", "les", " la", "es ", "ent", "le ", " et",
                    "et ", "la ", " co", " dr", "dro", "roi", "oit", "it ", " a ",
                    "ion", "tio", "on ", "ati", " to", "tou", "out", "ut ", " qu",
                    "que", "ue ", " en", "nt ", " au", "des", " pe", "per",
                    "ers", "rso", "son", "onn", "nne", "ne ", " li", "lib", "ibe",
                    "ber", "ert", "rte", " pa", "par", " un", "une", " so"
                }),
                ["pt"] = new TrigramProfile("pt", new[]
                {
                    " de", "de ", " do", "os ", " da", " co", "ao ", " a ",
                    "as ", "em ", " em", "ent", " to", "tod", "oda", "da ", " se",
                    " di", "dir", "ire", "rei", "eit", "ito", "to ", " qu", "que",
                    "ue ", " pe", "ess", "sso", "soa", "oa ", " ou", "ou ", " pr",
                    "pro", " li", "lib", "ibe", "ber", "erd", "rda", "dad", "ade",
                    "er ", " na", "nac", "aci", "cio", "ion", "ona", "nal"
                })
            };
        }
    }
}