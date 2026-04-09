using System.Collections.Generic;
using NUnit.Framework;
using uPiper.Core.Phonemizers.Multilingual;

namespace uPiper.Tests.Editor
{
    [TestFixture]
    public class TrigramLanguageDetectorTests
    {
        private TrigramLanguageDetector _detector;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var profiles = TrigramProfileLoader.LoadSync();
            // If profiles are not available in the test environment, create minimal ones
            if (profiles == null)
            {
                profiles = CreateMinimalProfiles();
            }
            _detector = new TrigramLanguageDetector(profiles);
        }

        // ── Trigram extraction ────────────────────────────────────────────────

        [Test]
        public void ExtractTrigrams_SimpleText_ReturnsCorrectTrigrams()
        {
            var normalized = TrigramLanguageDetector.NormalizeText("hello");
            var trigrams = TrigramLanguageDetector.ExtractTrigrams(normalized);

            // " hello " -> " he", "hel", "ell", "llo", "lo "
            Assert.IsTrue(trigrams.ContainsKey(" he"));
            Assert.IsTrue(trigrams.ContainsKey("hel"));
            Assert.IsTrue(trigrams.ContainsKey("ell"));
            Assert.IsTrue(trigrams.ContainsKey("llo"));
            Assert.IsTrue(trigrams.ContainsKey("lo "));
        }

        [Test]
        public void ExtractTrigrams_EmptyText_ReturnsEmpty()
        {
            var trigrams = TrigramLanguageDetector.ExtractTrigrams("");
            Assert.AreEqual(0, trigrams.Count);
        }

        [Test]
        public void ExtractTrigrams_NullText_ReturnsEmpty()
        {
            var trigrams = TrigramLanguageDetector.ExtractTrigrams(null);
            Assert.AreEqual(0, trigrams.Count);
        }

        [Test]
        public void ExtractTrigrams_ShortText_ReturnsEmpty()
        {
            var trigrams = TrigramLanguageDetector.ExtractTrigrams("ab");
            Assert.AreEqual(0, trigrams.Count);
        }

        [Test]
        public void ExtractTrigrams_AccentedText_NormalizesAccents()
        {
            var normalizedAccented = TrigramLanguageDetector.NormalizeText("caf\u00e9");
            var normalizedPlain = TrigramLanguageDetector.NormalizeText("cafe");

            var trigramsAccented = TrigramLanguageDetector.ExtractTrigrams(normalizedAccented);
            var trigramsPlain = TrigramLanguageDetector.ExtractTrigrams(normalizedPlain);

            // After normalization, both should produce the same trigrams
            Assert.AreEqual(trigramsPlain.Count, trigramsAccented.Count);
            foreach (var key in trigramsPlain.Keys)
            {
                Assert.IsTrue(trigramsAccented.ContainsKey(key),
                    $"Trigram '{key}' missing from accented version");
            }
        }

        // ── Text normalization ───────────────────────────────────────────────

        [Test]
        public void NormalizeText_UpperCase_Lowercased()
        {
            var result = TrigramLanguageDetector.NormalizeText("HELLO");
            Assert.AreEqual(" hello ", result);
        }

        [Test]
        public void NormalizeText_NonAlphabetic_ReplacedWithSpace()
        {
            var result = TrigramLanguageDetector.NormalizeText("hello, world!");
            Assert.AreEqual(" hello world ", result);
        }

        [Test]
        public void NormalizeText_ConsecutiveSpaces_Collapsed()
        {
            var result = TrigramLanguageDetector.NormalizeText("hello   world");
            Assert.AreEqual(" hello world ", result);
        }

        [Test]
        public void NormalizeText_EmptyString_ReturnsEmpty()
        {
            var result = TrigramLanguageDetector.NormalizeText("");
            Assert.AreEqual(string.Empty, result);
        }

        [Test]
        public void NormalizeText_AccentsRemoved()
        {
            var result = TrigramLanguageDetector.NormalizeText("\u00e9l\u00e8ve");
            // e + accent removed -> "eleve"
            Assert.AreEqual(" eleve ", result);
        }

        // ── Language detection ───────────────────────────────────────────────

        [Test]
        public void Detect_EnglishText_ReturnsEn()
        {
            var result = _detector.Detect(
                "The quick brown fox jumps over the lazy dog and the weather is beautiful today " +
                "with the children playing in the park throughout the afternoon");
            Assert.IsTrue(result.IsConfident, "Should be confident detection");
            Assert.AreEqual("en", result.Language);
            Assert.GreaterOrEqual(result.Score, TrigramLanguageDetector.DefaultConfidenceThreshold);
        }

        [Test]
        public void Detect_SpanishText_ReturnsEs()
        {
            var result = _detector.Detect(
                "Todos los seres humanos nacen libres e iguales en dignidad y derechos " +
                "y dotados como estan de razon y conciencia deben comportarse fraternalmente " +
                "los unos con los otros");
            Assert.IsTrue(result.IsConfident, "Should be confident detection");
            Assert.AreEqual("es", result.Language);
        }

        [Test]
        public void Detect_FrenchText_ReturnsFr()
        {
            var result = _detector.Detect(
                "Tous les etres humains naissent libres et egaux en dignite et en droits " +
                "ils sont doues de raison et de conscience et doivent agir les uns envers " +
                "les autres dans un esprit de fraternite");
            Assert.IsTrue(result.IsConfident, "Should be confident detection");
            Assert.AreEqual("fr", result.Language);
        }

        [Test]
        public void Detect_PortugueseText_ReturnsPt()
        {
            var result = _detector.Detect(
                "Todos os seres humanos nascem livres e iguais em dignidade e em direitos " +
                "dotados de razao e de consciencia devem agir uns para com os outros " +
                "em espirito de fraternidade");
            Assert.IsTrue(result.IsConfident, "Should be confident detection");
            Assert.AreEqual("pt", result.Language);
        }

        [Test]
        public void Detect_ShortText_ReturnsFallback()
        {
            var result = _detector.Detect("Hi");
            Assert.IsFalse(result.IsConfident, "Short text should not be confident");
        }

        [Test]
        public void Detect_EmptyText_ReturnsFallback()
        {
            var result = _detector.Detect("");
            Assert.IsFalse(result.IsConfident);
            Assert.IsNull(result.Language);
        }

        [Test]
        public void Detect_NullText_ReturnsFallback()
        {
            var result = _detector.Detect(null);
            Assert.IsFalse(result.IsConfident);
            Assert.IsNull(result.Language);
        }

        [Test]
        public void Detect_TextWithAccentedBorrowings_StillDetectsCorrectly()
        {
            // English text with some accented borrowings
            var result = _detector.Detect(
                "I went to the cafe and had a wonderful resume of the situation with my friends " +
                "and then we discussed the matter throughout the evening");
            Assert.IsTrue(result.IsConfident, "Should be confident detection");
            Assert.AreEqual("en", result.Language);
        }

        // ── TrigramProfile tests ─────────────────────────────────────────────

        [Test]
        public void ComputeSimilarity_IdenticalProfile_ReturnsHigh()
        {
            var trigrams = new string[] { " th", "the", "he ", " an", "and" };
            var profile = new TrigramProfile("en", trigrams);

            var input = new Dictionary<string, int>
            {
                [" th"] = 5,
                ["the"] = 4,
                ["he "] = 3,
                [" an"] = 2,
                ["and"] = 1
            };

            var score = profile.ComputeSimilarity(input);
            Assert.Greater(score, 0.8f, "Identical ranking should produce high score");
        }

        [Test]
        public void ComputeSimilarity_DifferentProfile_ReturnsLow()
        {
            var englishTrigrams = new string[] { " th", "the", "he ", " an", "and" };
            var profile = new TrigramProfile("en", englishTrigrams);

            // Input with completely different trigrams
            var input = new Dictionary<string, int>
            {
                [" de"] = 5,
                ["de "] = 4,
                [" la"] = 3,
                ["os "] = 2,
                ["ent"] = 1
            };

            var score = profile.ComputeSimilarity(input);
            Assert.Less(score, 0.5f, "Different trigrams should produce low score");
        }

        [Test]
        public void ComputeSimilarity_EmptyInput_ReturnsZero()
        {
            var profile = new TrigramProfile("en", new[] { " th", "the", "he " });
            var score = profile.ComputeSimilarity(new Dictionary<string, int>());
            Assert.AreEqual(0f, score);
        }

        [Test]
        public void ComputeSimilarity_NullInput_ReturnsZero()
        {
            var profile = new TrigramProfile("en", new[] { " th", "the", "he " });
            var score = profile.ComputeSimilarity(null);
            Assert.AreEqual(0f, score);
        }

        // ── LatinSegmentRefiner tests ────────────────────────────────────────

        [Test]
        public void Refine_NonLatinSegment_PassesThrough()
        {
            var profiles = CreateMinimalProfiles();
            var trigramDetector = new TrigramLanguageDetector(profiles);
            var refiner = new LatinSegmentRefiner(trigramDetector);

            var segments = new List<(string, string)>
            {
                ("ja", "\u3053\u3093\u306b\u3061\u306f"),
                ("zh", "\u4f60\u597d\u4e16\u754c")
            };

            var result = refiner.Refine(segments, "en");

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("ja", result[0].language);
            Assert.AreEqual("zh", result[1].language);
        }

        [Test]
        public void Refine_ShortLatinSegment_KeepsDefault()
        {
            var profiles = CreateMinimalProfiles();
            var trigramDetector = new TrigramLanguageDetector(profiles);
            var refiner = new LatinSegmentRefiner(trigramDetector);

            var segments = new List<(string, string)>
            {
                ("en", "Hi")
            };

            var result = refiner.Refine(segments, "en");

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("en", result[0].language);
        }

        [Test]
        public void Refine_LongLatinSegment_Reclassifies()
        {
            var profiles = TrigramProfileLoader.LoadSync() ?? CreateMinimalProfiles();
            var trigramDetector = new TrigramLanguageDetector(profiles);
            var refiner = new LatinSegmentRefiner(trigramDetector);

            var segments = new List<(string, string)>
            {
                ("en", "Bonjour comment allez vous aujourd hui nous sommes tres contents")
            };

            var result = refiner.Refine(segments, "en");

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("fr", result[0].language,
                "French text should be reclassified from en to fr");
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
                    "ter", " no", "nt ", "an ", " wh", " de", "es ", "was", "as ",
                    "all", " pr", "not", "ot ", "his", " hi", "are", " ar", "ons",
                    "men", "ver", " ma", " se", "pro", " ca", "rea", "ear", " di",
                    "ted", "com", " or", " al", "ll ", "se ", "igh", "ght", "hts",
                    "ts ", "rig", " ri", "ne ", " ne", "en ", "est", "ess", " mo",
                    " te", "nce", " me", "ce ", " so", " as", "ble", "le ", " ch",
                    "per", " pe", "hou", "out", "ut ", "hro", "rou", "oug", "ugh"
                }),
                ["es"] = new TrigramProfile("es", new[]
                {
                    " de", "de ", " la", " en", "os ", "la ", "ion", "aci", "cion",
                    " lo", "as ", "el ", " el", "es ", "ent", "en ", " co", "do ",
                    "der", "ere", "rec", "ech", "cho", "ho ", " se", "to ", " a ",
                    "con", "nte", " to", "tod", "oda", "da ", " pe", "per", "ers",
                    "rso", "son", "ona", "na ", " po", "por", "or ", " su", " qu",
                    "que", "ue ", " pr", "pro", "ra ", "te ", " li", "lib", "ibe",
                    "ber", "ert", "rta", "tad", "ad ", " re", "est", "al ", "cia",
                    "tra", "res", " na", "nac", "cio", "one", "nes", " di", "ar ",
                    " un", "ida", "dad", " so", " pa", "par", "nci", "ien", "nal",
                    " le", "ley", "ey ", "sta", " es", "era", "ade", "des", " in",
                    "les", "ser", " al", "ant", " ti", "tie", "ene", "ne ", "com"
                }),
                ["fr"] = new TrigramProfile("fr", new[]
                {
                    " de", "de ", " le", "les", " la", "es ", "ent", "le ", " et",
                    "et ", "la ", " co", " dr", "dro", "roi", "oit", "it ", " a ",
                    "ion", "tio", "on ", "ati", " to", "tou", "out", "ut ", " qu",
                    "que", "ue ", " en", "nt ", " au", "des", " pe", "per",
                    "ers", "rso", "son", "onn", "nne", "ne ", " li", "lib", "ibe",
                    "ber", "ert", "rte", " pa", "par", " un", "une", " so",
                    " da", "dan", "ans", "ns ", " le", " di", "ign", "gni", "nit",
                    " do", "doi", "oiv", "ive", "ven", " ag", "agi", "gir", "ir ",
                    " il", "ils", "ls ", " so", "son", "ont", " ra", "rai", "ais",
                    "sce", "enc", " co", "ons", "nsc", "sci", "ien", "oir", " fr",
                    "fra", "rat", "ate", "ern", "rni"
                }),
                ["pt"] = new TrigramProfile("pt", new[]
                {
                    " de", "de ", " do", "os ", " da", " co", "ao ", " a ",
                    "as ", "em ", " em", "ent", " to", "tod", "oda", "da ", " se",
                    " di", "dir", "ire", "rei", "eit", "ito", "to ", " qu", "que",
                    "ue ", " pe", "ess", "sso", "soa", "oa ", " ou", "ou ", " pr",
                    "pro", " li", "lib", "ibe", "ber", "erd", "rda", "dad", "ade",
                    "er ", " na", "nac", "aci", "cio", "ion", "ona", "nal",
                    "asc", "sce", "cem", " li", "liv", "ivr", "vre", "res", " ig",
                    "igu", "gua", "uai", "ais", "ign", "gni", "nid", "ida", " ra",
                    "raz", "aza", "zao", " pa", "par", "ara", " co", "com", " ou",
                    "utr", "tro", "ros", " es", "esp", "spi", "pir", "iri", "rit"
                })
            };
        }
    }
}