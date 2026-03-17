using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using uPiper.Core.Phonemizers.Backend;
using uPiper.Core.Phonemizers.Backend.Portuguese;

namespace uPiper.Tests.Editor.Phonemizers
{
    [TestFixture]
    public class PortuguesePhonemizerTests
    {
        private PortuguesePhonemizerBackend _phonemizer;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _phonemizer = new PortuguesePhonemizerBackend();
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
            return Task.Run(() => _phonemizer.PhonemizeAsync(text, "pt"))
                .GetAwaiter().GetResult();
        }

        // ── Basic Words ─────────────────────────────────────────────────

        [Test]
        public void TestBasicWords_Ola()
        {
            var result = Phonemize("ol\u00E1");

            Assert.IsTrue(result.Success, "Phonemization should succeed");
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0, "Should return phonemes");
        }

        [Test]
        public void TestBasicWords_Obrigado()
        {
            var result = Phonemize("obrigado");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Length > 0, "Should return phonemes");
        }

        [Test]
        public void TestBasicWords_Casa()
        {
            var result = Phonemize("casa");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("k"),
                "c before a should produce 'k'");
            Assert.IsTrue(result.Phonemes.Contains("a"),
                "Should contain 'a'");
        }

        // ── Nasal Vowels ────────────────────────────────────────────────

        [Test]
        public void TestNasalVowels_Tilde()
        {
            // mao with tilde -> nasal a
            var result = Phonemize("m\u00E3o");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("\u00E3"),
                "Should contain nasal 'a' (\u00E3) from tilde");
        }

        [Test]
        public void TestNasalVowels_BeforeN()
        {
            // "banco" -> nasal a before n+consonant
            var result = Phonemize("banco");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(
                result.Phonemes.Any(p => p == "\u00E3" || p == "\u1EBD"
                    || p == "\u0129" || p == "\u00F5" || p == "\u0169"),
                "Should contain a nasal vowel in 'banco'");
        }

        [Test]
        public void TestNasalVowels_WordFinalM()
        {
            // "bom" -> nasal o (m absorbed into nasal vowel)
            var result = Phonemize("bom");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("\u00F5"),
                "'bom' should contain nasal 'o' (\u00F5)");
            // The trailing m should be absorbed -- not present as a separate phoneme
            // at the end of the word
            var lastPhoneme = result.Phonemes.Last();
            Assert.AreNotEqual("m", lastPhoneme,
                "Final m should be absorbed into nasal vowel");
        }

        // ── Consonant Clusters / Digraphs ──────────────────────────────

        [Test]
        public void TestConsonantClusters_Nh()
        {
            var result = Phonemize("banho");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("\u0272"),
                "nh should produce palatal nasal (\u0272)");
        }

        [Test]
        public void TestConsonantClusters_Lh()
        {
            var result = Phonemize("filho");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("\u028E"),
                "lh should produce palatal lateral (\u028E)");
        }

        [Test]
        public void TestConsonantClusters_Ch()
        {
            var result = Phonemize("chave");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("\u0283"),
                "ch should produce voiceless postalveolar fricative (\u0283)");
        }

        [Test]
        public void TestConsonantClusters_Rr()
        {
            var result = Phonemize("carro");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("\u0281"),
                "rr should produce uvular fricative (\u0281)");
        }

        // ── Post-Processing: Coda-l Vocalization ───────────────────────

        [Test]
        public void TestPostProcessing_CodalVocalization_Brasil()
        {
            var result = Phonemize("brasil");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("w"),
                "Coda l should vocalize to 'w' in 'brasil'");
            Assert.IsFalse(result.Phonemes.Contains("l"),
                "Should not contain 'l' in 'brasil' (vocalized to w)");
        }

        [Test]
        public void TestPostProcessing_CodalVocalization_Alto()
        {
            var result = Phonemize("alto");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("w"),
                "Coda l before consonant should vocalize to 'w' in 'alto'");
            Assert.IsFalse(result.Phonemes.Contains("l"),
                "Should not contain 'l' in 'alto'");
        }

        [Test]
        public void TestPostProcessing_OnsetL_Preserved()
        {
            // Onset l (before vowel) should stay as [l]
            var result = Phonemize("lua");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("l"),
                "Onset l in 'lua' should remain as 'l'");
        }

        // ── Post-Processing: T/D Palatalization ────────────────────────

        [Test]
        public void TestPostProcessing_TDPalatalization_Gente()
        {
            // t before unstressed final -e -> affricate
            var result = Phonemize("gente");

            Assert.IsTrue(result.Success);
            // tsh is PUA \uE054
            Assert.IsTrue(
                result.Phonemes.Contains("\uE054") || result.Phonemes.Any(p => p.Contains("t\u0283")),
                "'gente' should have palatalized t (affricate)");
        }

        [Test]
        public void TestPostProcessing_TDPalatalization_Cidade()
        {
            // d before unstressed final -e -> affricate
            var result = Phonemize("cidade");

            Assert.IsTrue(result.Success);
            // dzh is PUA \uE055
            Assert.IsTrue(
                result.Phonemes.Contains("\uE055") || result.Phonemes.Any(p => p.Contains("d\u0292")),
                "'cidade' should have palatalized d (affricate)");
        }

        // ── Post-Processing: Final Vowel Reduction ─────────────────────

        [Test]
        public void TestPostProcessing_FinalVowelReduction_EtoI()
        {
            // Unstressed final e -> i: "quase"
            var result = Phonemize("quase");

            Assert.IsTrue(result.Success);
            var lastPhoneme = result.Phonemes.Last();
            Assert.AreEqual("i", lastPhoneme,
                "Unstressed final 'e' should reduce to 'i' in 'quase'");
        }

        [Test]
        public void TestPostProcessing_FinalVowelReduction_OtoU()
        {
            // Unstressed final o -> u: "gato"
            var result = Phonemize("gato");

            Assert.IsTrue(result.Success);
            var lastPhoneme = result.Phonemes.Last();
            Assert.AreEqual("u", lastPhoneme,
                "Unstressed final 'o' should reduce to 'u' in 'gato'");
        }

        // ── Stress Detection ────────────────────────────────────────────

        [Test]
        public void TestStressDetection_AccentMark()
        {
            // Accented vowel should receive stress
            var result = Phonemize("caf\u00E9");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.ProsodyA2.Any(a2 => a2 == 2),
                "Accented word should have stressed phonemes");
        }

        [Test]
        public void TestStressDetection_DefaultRules_Paroxytone()
        {
            // Words ending in vowel: penultimate stress (paroxytone)
            var result = Phonemize("casa");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.ProsodyA2.Any(a2 => a2 == 2),
                "Should have stressed phonemes");
        }

        [Test]
        public void TestStressDetection_DefaultRules_Oxytone()
        {
            // Words ending in consonant (except s): final stress (oxytone)
            var result = Phonemize("animal");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.ProsodyA2.Any(a2 => a2 == 2),
                "Should have stressed phonemes");
        }

        // ── Supported Languages ────────────────────────────────────────

        [TestCase("pt")]
        [TestCase("pt-BR")]
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
        }

        [Test]
        public void TestEmptyInput_NullString()
        {
            var result = Task.Run(() => _phonemizer.PhonemizeAsync(null, "pt"))
                .GetAwaiter().GetResult();

            Assert.IsNotNull(result);
        }

        // ── Prosody Output ─────────────────────────────────────────────

        [Test]
        public void TestProsodyOutput_ArrayLengthsMatch()
        {
            var result = Phonemize("ol\u00E1 mundo");

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
            var result = Phonemize("casa");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.ProsodyA1.All(a1 => a1 == 0),
                "ProsodyA1 should be 0 for all phonemes");
        }

        [Test]
        public void TestProsodyOutput_A2StressValues()
        {
            var result = Phonemize("ol\u00E1");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.ProsodyA2.All(a2 => a2 == 0 || a2 == 2),
                "ProsodyA2 should only contain 0 or 2");
            Assert.IsTrue(result.ProsodyA2.Any(a2 => a2 == 2),
                "Accented word should have at least one stressed phoneme");
        }

        [Test]
        public void TestProsodyOutput_A3WordPhonemeCount()
        {
            var result = Phonemize("casa");

            Assert.IsTrue(result.Success);
            var wordA3 = result.ProsodyA3.Where(a3 => a3 > 0).ToArray();
            Assert.IsTrue(wordA3.Length > 0,
                "Word phonemes should have a3 > 0 (word phoneme count)");
        }

        // ── Specific Portuguese rules ──────────────────────────────────

        [Test]
        public void TestCedilla()
        {
            var result = Phonemize("ca\u00E7a");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("s"),
                "cedilla should produce 's'");
        }

        [Test]
        public void TestInitialR_Uvular()
        {
            var result = Phonemize("rio");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("\u0281"),
                "Initial r should produce uvular fricative (\u0281)");
        }

        [Test]
        public void TestIntervocalicR_Tap()
        {
            var result = Phonemize("caro");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("\u027E"),
                "Intervocalic r should produce tap (\u027E)");
            Assert.IsFalse(result.Phonemes.Contains("\u0281"),
                "Intervocalic r should NOT produce uvular");
        }

        [Test]
        public void TestCodaR_Uvular()
        {
            // Word-final r should be uvular
            var result = Phonemize("mar");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("\u0281"),
                "Coda r should produce uvular fricative (\u0281)");
        }

        [Test]
        public void TestIntervocalicS_Voicing()
        {
            var result = Phonemize("casa");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("z"),
                "Intervocalic s should voice to /z/ in 'casa'");
        }

        [Test]
        public void TestJ_VoicedPostalveolar()
        {
            var result = Phonemize("jogo");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("\u0292"),
                "j should produce voiced postalveolar fricative (\u0292)");
        }

        [Test]
        public void TestG_BeforeE()
        {
            var result = Phonemize("gente");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("\u0292"),
                "g before e should produce \u0292");
        }

        [Test]
        public void TestNoNasalizationBeforeNh()
        {
            // Vowel before nh should NOT be nasalized
            var result = Phonemize("banho");

            Assert.IsTrue(result.Success);
            Assert.IsFalse(result.Phonemes.Contains("\u00E3"),
                "Should NOT have nasal 'a' before nh in 'banho'");
            Assert.IsTrue(result.Phonemes.Contains("a"),
                "Should have oral 'a' before nh in 'banho'");
        }

        [Test]
        public void TestQuBeforeE_SilentU()
        {
            // qu before e/i: u is silent
            var result = Phonemize("que");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("k"),
                "'que' should start with 'k'");
            Assert.IsFalse(result.Phonemes.Contains("w"),
                "'que' should NOT have 'w' (u is silent before e)");
        }

        [Test]
        public void TestQuBeforeA_ProducesKw()
        {
            // qu before a/o: u is pronounced as /w/
            var result = Phonemize("quase");

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Contains("k"),
                "'quase' should have 'k'");
            Assert.IsTrue(result.Phonemes.Contains("w"),
                "'quase' should have 'w' (qu before a)");
        }

        // ── Backend Properties ─────────────────────────────────────────

        [Test]
        public void TestBackendProperties()
        {
            Assert.AreEqual("Portuguese", _phonemizer.Name);
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
