using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using uPiper.Core.Phonemizers.Backend;
using uPiper.Core.Phonemizers.Backend.French;

namespace uPiper.Tests.Editor.Phonemizers
{
    [TestFixture]
    public class FrenchPhonemizerTests
    {
        private FrenchPhonemizerBackend _phonemizer;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _phonemizer = new FrenchPhonemizerBackend();
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
            return Task.Run(() => _phonemizer.PhonemizeAsync(text, "fr"))
                .GetAwaiter().GetResult();
        }

        // ── Basic Words ─────────────────────────────────────────────────

        [Test]
        public void TestBasicWords_Bonjour()
        {
            var result = Phonemize("bonjour");

            Assert.IsTrue(result.Success, "Phonemization should succeed");
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0, "Should return phonemes");
        }

        [Test]
        public void TestBasicWords_Merci()
        {
            var result = Phonemize("merci");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Length > 0);
            // Should contain uvular r
            Assert.IsTrue(result.Phonemes.Contains("\u0281"),
                "Should contain uvular r (\u0281)");
        }

        [Test]
        public void TestBasicWords_Chat()
        {
            var result = Phonemize("chat");

            Assert.IsTrue(result.Success);
            // ch -> sh (voiceless postalveolar fricative)
            Assert.IsTrue(result.Phonemes.Contains("\u0283"),
                "ch should produce voiceless postalveolar fricative (\u0283)");
        }

        // ── Nasal Vowels ────────────────────────────────────────────────

        [Test]
        public void TestNasalVowels_France()
        {
            var result = Phonemize("france");

            Assert.IsTrue(result.Success);
            // an/am -> nasal alpha (PUA \uE057 for mapped version)
            Assert.IsTrue(
                result.Phonemes.Any(p => p == "\uE057" || p == "\u0251\u0303"),
                "France should contain nasal alpha vowel");
        }

        [Test]
        public void TestNasalVowels_Vin()
        {
            var result = Phonemize("vin");

            Assert.IsTrue(result.Success);
            // in -> nasal epsilon (PUA \uE056)
            Assert.IsTrue(
                result.Phonemes.Any(p => p == "\uE056" || p == "\u025B\u0303"),
                "vin should contain nasal epsilon vowel");
        }

        [Test]
        public void TestNasalVowels_Bon()
        {
            var result = Phonemize("bon");

            Assert.IsTrue(result.Success);
            // on -> nasal open-o (PUA \uE058)
            Assert.IsTrue(
                result.Phonemes.Any(p => p == "\uE058" || p == "\u0254\u0303"),
                "bon should contain nasal open-o vowel");
        }

        // ── Silent Letters ──────────────────────────────────────────────

        [Test]
        public void TestSilentLetters_FinalConsonant()
        {
            // "petit" -- the final 't' should be silent
            var result = Phonemize("petit");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Should produce phonemes for 'petit'");
        }

        [Test]
        public void TestSilentLetters_SilentH()
        {
            var result = Phonemize("homme");

            Assert.IsTrue(result.Success);
            Assert.IsFalse(result.Phonemes.Contains("h"),
                "h should be silent in French");
        }

        // ── Digraphs ───────────────────────────────────────────────────

        [Test]
        public void TestDigraphs_Ch()
        {
            var result = Phonemize("chat");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("\u0283"),
                "ch should produce \u0283");
        }

        [Test]
        public void TestDigraphs_Gn()
        {
            var result = Phonemize("montagne");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("\u0272"),
                "gn should produce palatal nasal (\u0272)");
        }

        [Test]
        public void TestDigraphs_Ou()
        {
            var result = Phonemize("vous");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("u"),
                "ou should produce 'u'");
        }

        [Test]
        public void TestDigraphs_Eau()
        {
            var result = Phonemize("eau");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("o"),
                "eau should produce 'o'");
        }

        [Test]
        public void TestDigraphs_Oi()
        {
            var result = Phonemize("moi");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("w"),
                "oi should produce 'w'");
            Assert.IsTrue(result.Phonemes.Contains("a"),
                "oi should produce 'a'");
        }

        // ── E Muet (Silent e) ──────────────────────────────────────────

        [Test]
        public void TestEMuet_WordFinal()
        {
            // Final 'e' is typically silent in French
            var result = Phonemize("table");

            Assert.IsTrue(result.Success);
            // Consonant before final 'e' should still be pronounced
            Assert.IsTrue(result.Phonemes.Contains("l"),
                "l before final silent e should be pronounced");
            Assert.IsTrue(result.Phonemes.Contains("b"),
                "b should be pronounced in 'table'");
        }

        // ── -er Endings ────────────────────────────────────────────────

        [Test]
        public void TestErEndings_VerbInfinitive()
        {
            // Polysyllabic -er verb -> /e/
            var result = Phonemize("parler");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("e"),
                "Verb infinitive -er ending should produce /e/");
        }

        [Test]
        public void TestErEndings_MonosyllabicException()
        {
            // Monosyllabic -er words keep /eR/: "mer"
            var result = Phonemize("mer");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("\u0281"),
                "Monosyllabic 'mer' should pronounce the r as /\u0281/");
        }

        [Test]
        public void TestErEndings_ExceptionWord()
        {
            // "hiver" is an exception: keeps /eR/ not /e/
            var result = Phonemize("hiver");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("\u0281"),
                "'hiver' should keep /\u0281/ (exception to -er rule)");
        }

        // ── -ille Words ────────────────────────────────────────────────

        [Test]
        public void TestIlleWords_Ville()
        {
            // ville -> /il/ exception
            var result = Phonemize("ville");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("l"),
                "'ville' should have /l/ (-ille exception)");
            Assert.IsFalse(result.Phonemes.Contains("j"),
                "'ville' should NOT have /j/");
        }

        [Test]
        public void TestIlleWords_Fille()
        {
            // fille -> /ij/ default
            var result = Phonemize("fille");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("j"),
                "'fille' should have /j/ (default -ille)");
        }

        [Test]
        public void TestIlleWords_Mille()
        {
            // mille -> /il/ exception
            var result = Phonemize("mille");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("l"),
                "'mille' should have /l/ (-ille exception)");
            Assert.IsFalse(result.Phonemes.Contains("j"),
                "'mille' should NOT have /j/");
        }

        // ── Supported Languages ────────────────────────────────────────

        [TestCase("fr")]
        [TestCase("fr-FR")]
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
            Assert.IsFalse(_phonemizer.SupportsLanguage("es"),
                "Should not support 'es'");
        }

        // ── Empty Input ────────────────────────────────────────────────

        [Test]
        public void TestEmptyInput_EmptyString()
        {
            var result = Phonemize("");

            Assert.IsNotNull(result);
            Assert.IsFalse(result.Success);
        }

        [Test]
        public void TestEmptyInput_NullString()
        {
            var result = Task.Run(() => _phonemizer.PhonemizeAsync(null, "fr"))
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsFalse(result.Success);
        }

        // ── Prosody Output ─────────────────────────────────────────────

        [Test]
        public void TestProsodyOutput_ArrayLengthsMatch()
        {
            var result = Phonemize("bonjour le monde");

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
            var result = Phonemize("bonjour");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.ProsodyA1.All(a1 => a1 == 0),
                "ProsodyA1 should be 0 for all phonemes");
        }

        [Test]
        public void TestProsodyOutput_A2StressValues()
        {
            var result = Phonemize("bonjour");

            Assert.IsTrue(result.Success);
            // a2 should be 0 (unstressed) or 2 (stressed)
            Assert.IsTrue(result.ProsodyA2.All(a2 => a2 == 0 || a2 == 2),
                "ProsodyA2 should only contain 0 or 2");
            // French: stress on last syllable, so at least one phoneme stressed
            Assert.IsTrue(result.ProsodyA2.Any(a2 => a2 == 2),
                "Should have at least one stressed phoneme");
        }

        [Test]
        public void TestProsodyOutput_A3WordPhonemeCount()
        {
            var result = Phonemize("bonjour");

            Assert.IsTrue(result.Success);
            var wordA3 = result.ProsodyA3.Where(a3 => a3 > 0).ToArray();
            Assert.IsTrue(wordA3.Length > 0,
                "Word phonemes should have a3 > 0 (word phoneme count)");
        }

        // ── Specific French rules ──────────────────────────────────────

        [Test]
        public void TestIntervocalicS_Voicing()
        {
            // Intervocalic s -> z: "rose"
            var result = Phonemize("rose");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("z"),
                "Intervocalic s should voice to /z/ in 'rose'");
        }

        [Test]
        public void TestDoubleSS_NotVoiced()
        {
            // Double ss stays /s/: "poisson"
            var result = Phonemize("poisson");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("s"),
                "'poisson' with double ss should have /s/");
            Assert.IsFalse(result.Phonemes.Contains("z"),
                "'poisson' should NOT have /z/");
        }

        [Test]
        public void TestUvularR()
        {
            var result = Phonemize("rouge");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("\u0281"),
                "r should produce uvular fricative (\u0281)");
        }

        [Test]
        public void TestYVowel_French_U()
        {
            // French u -> y_vowel (PUA \uE01E)
            var result = Phonemize("lune");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(
                result.Phonemes.Contains("\uE01E") || result.Phonemes.Any(p => p == "y_vowel"),
                "French u should produce y_vowel (PUA-mapped)");
        }

        [Test]
        public void TestSemiVowel_Hui()
        {
            // u + i -> turned-h semivowel
            var result = Phonemize("lui");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("\u0265"),
                "u before i should produce semivowel (\u0265)");
        }

        [Test]
        public void TestApostropheHandling()
        {
            var result = Phonemize("l'ami");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Apostrophe handling should produce phonemes");
        }

        [Test]
        public void TestDoubleR_SinglePhoneme()
        {
            // rr -> single uvular: "terre"
            var result = Phonemize("terre");

            Assert.IsTrue(result.Success);
            var rCount = result.Phonemes.Count(p => p == "\u0281");
            Assert.AreEqual(1, rCount,
                "Double rr should produce single /\u0281/ in 'terre'");
        }

        // ── Backend Properties ─────────────────────────────────────────

        [Test]
        public void TestBackendProperties()
        {
            Assert.AreEqual("FrenchRuleBased", _phonemizer.Name);
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
            Assert.IsFalse(caps.RequiresNetwork, "Should not require network");
        }
    }
}