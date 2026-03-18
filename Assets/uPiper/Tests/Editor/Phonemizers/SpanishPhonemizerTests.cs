using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using uPiper.Core.Phonemizers.Backend;
using uPiper.Core.Phonemizers.Backend.Spanish;

namespace uPiper.Tests.Editor.Phonemizers
{
    [TestFixture]
    public class SpanishPhonemizerTests
    {
        private SpanishPhonemizerBackend _phonemizer;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _phonemizer = new SpanishPhonemizerBackend();
            Task.Run(() => _phonemizer.InitializeAsync()).GetAwaiter().GetResult();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _phonemizer?.Dispose();
        }

        // ── Helper ──────────────────────────────────────────────────────

        private PhonemeResult Phonemize(string text)
        {
            return Task.Run(() => _phonemizer.PhonemizeAsync(text, "es"))
                .GetAwaiter().GetResult();
        }

        // ── Basic Words ─────────────────────────────────────────────────

        [Test]
        public void TestBasicWords_Hola()
        {
            var result = Phonemize("hola");

            Assert.IsTrue(result.Success, "Phonemization should succeed");
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0, "Should return phonemes");
            // h is silent in Spanish; expect o, l, a
            Assert.IsTrue(result.Phonemes.Contains("o"), "Should contain 'o'");
            Assert.IsTrue(result.Phonemes.Contains("l"), "Should contain 'l'");
            Assert.IsTrue(result.Phonemes.Contains("a"), "Should contain 'a'");
            Assert.IsFalse(result.Phonemes.Contains("h"), "'h' should be silent");
        }

        [Test]
        public void TestBasicWords_Casa()
        {
            var result = Phonemize("casa");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("k"), "c before a should produce 'k'");
            Assert.IsTrue(result.Phonemes.Contains("a"), "Should contain 'a'");
            Assert.IsTrue(result.Phonemes.Contains("s"), "Should contain 's'");
        }

        [Test]
        public void TestBasicWords_Perro()
        {
            var result = Phonemize("perro");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("p"), "Should contain 'p'");
            Assert.IsTrue(result.Phonemes.Contains("e"), "Should contain 'e'");
            // rr is mapped to PUA \uE01D
            Assert.IsTrue(
                result.Phonemes.Contains("\uE01D") || result.Phonemes.Contains("rr"),
                "Should contain trilled rr (as PUA or digraph)");
        }

        // ── Stress Detection ────────────────────────────────────────────

        [Test]
        public void TestStressDetection_AccentedVowel()
        {
            // cafe has accent on e -> stress marker should appear
            var result = Phonemize("caf\u00E9");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Length > 0);
            // ProsodyA2 should have a value of 2 for stressed phonemes
            Assert.IsTrue(
                result.ProsodyA2.Any(a2 => a2 == 2),
                "Should have stressed phonemes (a2=2) for accented word");
        }

        [TestCase("m\u00E1s")]    // mas with accent on a
        [TestCase("canci\u00F3n")] // cancion with accent on o
        [TestCase("caf\u00E9")]    // cafe with accent on e
        public void TestStressDetection_VariousAccents(string word)
        {
            var result = Phonemize(word);

            Assert.IsTrue(result.Success);
            Assert.IsTrue(
                result.ProsodyA2.Any(a2 => a2 == 2),
                $"Should have stressed phonemes for '{word}'");
        }

        // ── Digraphs ───────────────────────────────────────────────────

        [Test]
        public void TestDigraphs_Ch_Chico()
        {
            var result = Phonemize("chico");

            Assert.IsTrue(result.Success);
            // ch -> t-sh affricate, mapped to PUA \uE054
            Assert.IsTrue(
                result.Phonemes.Contains("\uE054") || result.Phonemes.Any(p => p.Contains("t\u0283")),
                "ch should produce voiceless postalveolar affricate");
        }

        [Test]
        public void TestDigraphs_Ll_Lluvia()
        {
            var result = Phonemize("lluvia");

            Assert.IsTrue(result.Success);
            // ll -> j-palatal (yeismo)
            Assert.IsTrue(
                result.Phonemes.Contains("\u029D"),
                "ll should produce palatal fricative (yeismo)");
        }

        [Test]
        public void TestDigraphs_Rr_Perro()
        {
            var result = Phonemize("perro");

            Assert.IsTrue(result.Success);
            // rr digraph -> trilled rr (PUA \uE01D)
            Assert.IsTrue(
                result.Phonemes.Contains("\uE01D") || result.Phonemes.Contains("rr"),
                "rr should produce trill");
        }

        // ── Allophone Rules ────────────────────────────────────────────

        [Test]
        public void TestAllophoneRules_IntervocalicB()
        {
            // Intervocalic b/v -> beta (fricative)
            var result = Phonemize("aba");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(
                result.Phonemes.Contains("\u03B2"),
                "Intervocalic b should produce beta (fricative)");
        }

        [Test]
        public void TestAllophoneRules_IntervocalicD()
        {
            // Intervocalic d -> eth (fricative)
            var result = Phonemize("cada");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(
                result.Phonemes.Contains("\u00F0"),
                "Intervocalic d should produce eth (fricative)");
        }

        [Test]
        public void TestAllophoneRules_IntervocalicG()
        {
            // Intervocalic g -> gamma (fricative)
            var result = Phonemize("lago");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(
                result.Phonemes.Contains("\u0263"),
                "Intervocalic g should produce gamma (fricative)");
        }

        [Test]
        public void TestAllophoneRules_InitialB_IsStop()
        {
            var result = Phonemize("bueno");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(
                result.Phonemes.Contains("b"),
                "Word-initial b should be stop [b]");
        }

        // ── Syllabification ────────────────────────────────────────────

        [Test]
        public void TestSyllabification_MultiSyllable()
        {
            // "mucho" should produce phonemes for mu-cho
            var result = Phonemize("mucho");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Length >= 3,
                "Multi-syllable word should produce multiple phonemes");
        }

        [Test]
        public void TestSyllabification_EneSound()
        {
            // nino -> palatal nasal
            var result = Phonemize("ni\u00F1o");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(
                result.Phonemes.Contains("\u0272"),
                "Should contain palatal nasal for 'ni\u00F1o'");
        }

        // ── Punctuation ────────────────────────────────────────────────

        [Test]
        public void TestPunctuation_BasicSentence()
        {
            var result = Phonemize("hola, mundo.");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains(","), "Comma should pass through");
            Assert.IsTrue(result.Phonemes.Contains("."), "Period should pass through");
        }

        [Test]
        public void TestPunctuation_InvertedMarks()
        {
            var result = Phonemize("\u00BFc\u00F3mo est\u00E1s?");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("\u00BF"),
                "Inverted question mark should pass through");
            Assert.IsTrue(result.Phonemes.Contains("?"),
                "Question mark should pass through");
        }

        // ── Supported Languages ────────────────────────────────────────

        [TestCase("es")]
        [TestCase("es-ES")]
        [TestCase("es-MX")]
        public void TestSupportedLanguages(string language)
        {
            Assert.IsTrue(_phonemizer.SupportsLanguage(language),
                $"Should support language '{language}'");
        }

        [Test]
        public void TestUnsupportedLanguage()
        {
            Assert.IsFalse(_phonemizer.SupportsLanguage("en"),
                "Should not support 'en'");
            Assert.IsFalse(_phonemizer.SupportsLanguage("fr"),
                "Should not support 'fr'");
        }

        // ── Empty Input ────────────────────────────────────────────────

        [Test]
        public void TestEmptyInput_EmptyString()
        {
            var result = Phonemize("");

            // Empty string should return a result (possibly failed validation)
            Assert.IsNotNull(result);
        }

        [Test]
        public void TestEmptyInput_NullString()
        {
            var result = Task.Run(() => _phonemizer.PhonemizeAsync(null, "es"))
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
        }

        // ── Prosody Output ─────────────────────────────────────────────

        [Test]
        public void TestProsodyOutput_ArrayLengthsMatch()
        {
            var result = Phonemize("hola mundo");

            Assert.IsTrue(result.Success);
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA1.Length,
                "Phonemes and ProsodyA1 should have same length");
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA2.Length,
                "Phonemes and ProsodyA2 should have same length");
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA3.Length,
                "Phonemes and ProsodyA3 should have same length");
        }

        [Test]
        public void TestProsodyOutput_A1IsZero()
        {
            var result = Phonemize("hola");

            Assert.IsTrue(result.Success);
            // For Spanish, a1 should be 0
            Assert.IsTrue(result.ProsodyA1.All(a1 => a1 == 0),
                "ProsodyA1 should be 0 for all phonemes");
        }

        [Test]
        public void TestProsodyOutput_A2StressValues()
        {
            var result = Phonemize("caf\u00E9");

            Assert.IsTrue(result.Success);
            // a2 should be 0 (unstressed) or 2 (stressed)
            Assert.IsTrue(result.ProsodyA2.All(a2 => a2 == 0 || a2 == 2),
                "ProsodyA2 should only contain 0 or 2");
            Assert.IsTrue(result.ProsodyA2.Any(a2 => a2 == 2),
                "Accented word should have at least one stressed phoneme");
        }

        [Test]
        public void TestProsodyOutput_A3WordPhonemeCount()
        {
            var result = Phonemize("hola");

            Assert.IsTrue(result.Success);
            // a3 should be > 0 for word phonemes
            var wordA3 = result.ProsodyA3.Where(a3 => a3 > 0).ToArray();
            Assert.IsTrue(wordA3.Length > 0,
                "Word phonemes should have a3 > 0 (word phoneme count)");
        }

        // ── Specific phoneme rules ─────────────────────────────────────

        [Test]
        public void TestSeseo_CBeforeE()
        {
            // Latin American seseo: c before e/i -> s
            var result = Phonemize("cena");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("s"),
                "c before e should produce 's' (seseo)");
        }

        [Test]
        public void TestSeseo_ZAlwaysS()
        {
            var result = Phonemize("zapato");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("s"),
                "z should produce 's' (seseo)");
        }

        [Test]
        public void TestSilentH()
        {
            var result = Phonemize("hombre");

            Assert.IsTrue(result.Success);
            Assert.IsFalse(result.Phonemes.Contains("h"),
                "'h' should be silent");
        }

        [Test]
        public void TestJota_XSound()
        {
            var result = Phonemize("jota");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("x"),
                "j should produce velar fricative 'x'");
        }

        [Test]
        public void TestQu_ProducesK()
        {
            var result = Phonemize("queso");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("k"),
                "qu should produce 'k'");
        }

        [Test]
        public void TestFunctionWord_NoStress()
        {
            var result = Phonemize("el");

            Assert.IsTrue(result.Success);
            Assert.IsFalse(result.ProsodyA2.Any(a2 => a2 == 2),
                "Function word 'el' should not have stress marker");
        }

        // ── Backend Properties ─────────────────────────────────────────

        [Test]
        public void TestBackendProperties()
        {
            Assert.AreEqual("SpanishG2P", _phonemizer.Name);
            Assert.AreEqual("1.0.0", _phonemizer.Version);
            Assert.AreEqual("MIT", _phonemizer.License);
            Assert.IsTrue(_phonemizer.IsAvailable, "Should be available after init");
        }

        [Test]
        public void TestBackendCapabilities()
        {
            var caps = _phonemizer.GetCapabilities();

            Assert.IsTrue(caps.SupportsIPA, "Should support IPA");
            Assert.IsTrue(caps.SupportsStress, "Should support stress");
            Assert.IsTrue(caps.SupportsSyllables, "Should support syllables");
            Assert.IsFalse(caps.RequiresNetwork, "Should not require network");
            Assert.IsTrue(caps.IsThreadSafe, "Should be thread-safe");
        }
    }
}