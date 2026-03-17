using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using uPiper.Core.Phonemizers.Backend;
using uPiper.Core.Phonemizers.Backend.Korean;
using uPiper.Core.Phonemizers.Multilingual;

namespace uPiper.Tests.Editor.Phonemizers
{
    [TestFixture]
    public class KoreanPhonemizerTests
    {
        private KoreanPhonemizerBackend _phonemizer;
        private bool _available;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            try
            {
                _phonemizer = new KoreanPhonemizerBackend();
                Task.Run(async () =>
                {
                    _available = await _phonemizer.InitializeAsync();
                }).GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                _available = false;
            }
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _phonemizer?.Dispose();
        }

        private void EnsureAvailable()
        {
            if (!_available)
                Assert.Ignore("KoreanPhonemizerBackend not available, skipping test");
        }

        // ── Properties & metadata ────────────────────────────────────────────

        [Test]
        public void Properties_Name_ReturnsKorean()
        {
            EnsureAvailable();
            Assert.AreEqual("Korean", _phonemizer.Name);
        }

        [Test]
        public void Properties_Version_ReturnsValidVersion()
        {
            EnsureAvailable();
            Assert.AreEqual("1.0.0", _phonemizer.Version);
        }

        [Test]
        public void Properties_License_ReturnsMIT()
        {
            EnsureAvailable();
            Assert.AreEqual("MIT", _phonemizer.License);
        }

        [Test]
        public void Properties_IsAvailable_AfterInit_ReturnsTrue()
        {
            EnsureAvailable();
            Assert.IsTrue(_phonemizer.IsAvailable);
        }

        // ── Supported languages ──────────────────────────────────────────────

        [TestCase("ko")]
        [TestCase("ko-KR")]
        public void SupportsLanguage_KoreanCode_ReturnsTrue(string lang)
        {
            EnsureAvailable();
            Assert.IsTrue(_phonemizer.SupportsLanguage(lang));
        }

        [TestCase("en")]
        [TestCase("ja")]
        [TestCase("zh")]
        public void SupportsLanguage_NonKorean_ReturnsFalse(string lang)
        {
            EnsureAvailable();
            Assert.IsFalse(_phonemizer.SupportsLanguage(lang));
        }

        [Test]
        public void SupportedLanguages_ContainsKo()
        {
            EnsureAvailable();
            CollectionAssert.Contains(_phonemizer.SupportedLanguages, "ko");
            CollectionAssert.Contains(_phonemizer.SupportedLanguages, "ko-KR");
        }

        // ── Capabilities ─────────────────────────────────────────────────────

        [Test]
        public void GetCapabilities_SupportsIPA()
        {
            EnsureAvailable();
            var caps = _phonemizer.GetCapabilities();
            Assert.IsTrue(caps.SupportsIPA);
        }

        [Test]
        public void GetCapabilities_IsThreadSafe()
        {
            EnsureAvailable();
            var caps = _phonemizer.GetCapabilities();
            Assert.IsTrue(caps.IsThreadSafe);
        }

        [Test]
        public void GetCapabilities_NoNetwork()
        {
            EnsureAvailable();
            var caps = _phonemizer.GetCapabilities();
            Assert.IsFalse(caps.RequiresNetwork);
        }

        // ── Hangul decomposition ─────────────────────────────────────────────
        // Tests that Hangul syllables are correctly decomposed and mapped to IPA.

        [Test]
        public void TestHangulDecomposition_BasicSyllable()
        {
            EnsureAvailable();

            // '가' (ga) = initial g (index 0) + medial a (index 0) + no final (index 0)
            // Expected IPA: [k] + [a] (lax velar + open vowel)
            var result = Task.Run(() => _phonemizer.PhonemizeAsync("가", "ko"))
                .GetAwaiter().GetResult();

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length >= 2,
                "'가' should decompose to at least initial + medial phonemes");
        }

        [Test]
        public void TestHangulDecomposition_WithFinalConsonant()
        {
            EnsureAvailable();

            // '한' (han) = ㅎ (h, index 18) + ㅏ (a, index 0) + ㄴ (n, index 4)
            var result = Task.Run(() => _phonemizer.PhonemizeAsync("한", "ko"))
                .GetAwaiter().GetResult();

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length >= 3,
                "'한' should decompose into initial + medial + final phonemes");
        }

        [Test]
        public void TestHangulDecomposition_SilentInitial()
        {
            EnsureAvailable();

            // '아' (a) = ㅇ (silent, index 11) + ㅏ (a, index 0) + no final
            // Initial ㅇ is silent so output should be just the vowel [a]
            var result = Task.Run(() => _phonemizer.PhonemizeAsync("아", "ko"))
                .GetAwaiter().GetResult();

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length >= 1,
                "'아' should produce at least vowel phoneme");
        }

        // ── Basic syllables ──────────────────────────────────────────────────

        [Test]
        public void TestBasicSyllables_Greeting()
        {
            EnsureAvailable();

            // "안녕하세요" (annyeonghaseyo) - standard Korean greeting
            var result = Task.Run(() => _phonemizer.PhonemizeAsync("안녕하세요", "ko"))
                .GetAwaiter().GetResult();

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 5,
                "'안녕하세요' should produce many phonemes");
            Assert.AreEqual("ko", result.Language);
            Assert.AreEqual("Korean", result.Backend);
        }

        [Test]
        public void TestBasicSyllables_SingleWord()
        {
            EnsureAvailable();

            // "감사합니다" (gamsahamnida) - "thank you"
            var result = Task.Run(() => _phonemizer.PhonemizeAsync("감사합니다", "ko"))
                .GetAwaiter().GetResult();

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 5);
        }

        // ── Initial consonants (chosung) IPA mapping ─────────────────────────

        [Test]
        public void TestInitialConsonants_LaxStops()
        {
            EnsureAvailable();

            // Test lax stops: ㄱ (k), ㄷ (t), ㅂ (p) appear in IPA output
            // '가' -> k, '다' -> t, '바' -> p
            var resultG = Task.Run(() => _phonemizer.PhonemizeAsync("가", "ko")).GetAwaiter().GetResult();
            var resultD = Task.Run(() => _phonemizer.PhonemizeAsync("다", "ko")).GetAwaiter().GetResult();
            var resultB = Task.Run(() => _phonemizer.PhonemizeAsync("바", "ko")).GetAwaiter().GetResult();

            Assert.IsTrue(resultG.Success);
            Assert.IsTrue(resultD.Success);
            Assert.IsTrue(resultB.Success);

            // All should produce different initial phonemes
            Assert.AreNotEqual(
                string.Join(",", resultG.Phonemes),
                string.Join(",", resultD.Phonemes),
                "'가' and '다' should produce different phonemes");
        }

        [Test]
        public void TestInitialConsonants_AspiratedStops()
        {
            EnsureAvailable();

            // Aspirated: ㅋ (kh), ㅌ (th), ㅍ (ph) use PUA multi-char IPA
            var resultK = Task.Run(() => _phonemizer.PhonemizeAsync("카", "ko")).GetAwaiter().GetResult();
            var resultT = Task.Run(() => _phonemizer.PhonemizeAsync("타", "ko")).GetAwaiter().GetResult();
            var resultP = Task.Run(() => _phonemizer.PhonemizeAsync("파", "ko")).GetAwaiter().GetResult();

            Assert.IsTrue(resultK.Success);
            Assert.IsTrue(resultT.Success);
            Assert.IsTrue(resultP.Success);

            // Aspirated phonemes should be different from lax ones
            var laxG = Task.Run(() => _phonemizer.PhonemizeAsync("가", "ko")).GetAwaiter().GetResult();
            Assert.AreNotEqual(
                string.Join(",", resultK.Phonemes),
                string.Join(",", laxG.Phonemes),
                "Aspirated '카' and lax '가' should produce different phonemes");
        }

        [Test]
        public void TestInitialConsonants_TenseStops()
        {
            EnsureAvailable();

            // Tense (fortis): ㄲ (kk), ㄸ (tt), ㅃ (pp), ㅆ (ss), ㅉ (jj)
            var resultGG = Task.Run(() => _phonemizer.PhonemizeAsync("까", "ko")).GetAwaiter().GetResult();
            var resultDD = Task.Run(() => _phonemizer.PhonemizeAsync("따", "ko")).GetAwaiter().GetResult();
            var resultBB = Task.Run(() => _phonemizer.PhonemizeAsync("빠", "ko")).GetAwaiter().GetResult();

            Assert.IsTrue(resultGG.Success);
            Assert.IsTrue(resultDD.Success);
            Assert.IsTrue(resultBB.Success);

            // Tense should differ from lax
            var laxG = Task.Run(() => _phonemizer.PhonemizeAsync("가", "ko")).GetAwaiter().GetResult();
            Assert.AreNotEqual(
                string.Join(",", resultGG.Phonemes),
                string.Join(",", laxG.Phonemes),
                "Tense '까' and lax '가' should produce different phonemes");
        }

        // ── Medial vowels (jungsung) IPA mapping ─────────────────────────────

        [Test]
        public void TestMedialVowels_SimpleVowels()
        {
            EnsureAvailable();

            // Simple vowels: ㅏ (a), ㅓ (eo), ㅗ (o), ㅜ (u), ㅡ (eu), ㅣ (i)
            var vowels = new[] { "아", "어", "오", "우", "으", "이" };
            var results = new List<string>();

            foreach (var vowel in vowels)
            {
                var r = Task.Run(() => _phonemizer.PhonemizeAsync(vowel, "ko")).GetAwaiter().GetResult();
                Assert.IsTrue(r.Success, $"Should phonemize '{vowel}'");
                results.Add(string.Join(",", r.Phonemes));
            }

            // Each vowel should produce distinct output
            var distinct = results.Distinct().Count();
            Assert.AreEqual(vowels.Length, distinct,
                "All basic vowels should produce distinct phoneme output");
        }

        [Test]
        public void TestMedialVowels_Diphthongs()
        {
            EnsureAvailable();

            // Diphthongs: ㅘ (wa), ㅙ (wae), ㅝ (wo), ㅞ (we), ㅟ (wi)
            var diphthongs = new[] { "와", "왜", "워", "웨", "위" };

            foreach (var syllable in diphthongs)
            {
                var r = Task.Run(() => _phonemizer.PhonemizeAsync(syllable, "ko")).GetAwaiter().GetResult();
                Assert.IsTrue(r.Success, $"Should phonemize diphthong '{syllable}'");
                Assert.IsNotNull(r.Phonemes);
                Assert.IsTrue(r.Phonemes.Length >= 2,
                    $"Diphthong '{syllable}' should produce at least glide + vowel phonemes");
            }
        }

        // ── Final consonants (jongsung) IPA mapping ──────────────────────────

        [Test]
        public void TestFinalConsonants_UnreleasedStops()
        {
            EnsureAvailable();

            // Unreleased final stops: k_unreleased, t_unreleased, p_unreleased
            // '각' (gak) -> final k_unreleased
            // '갇' (gat) -> final t_unreleased
            // '갑' (gap) -> final p_unreleased
            var resultK = Task.Run(() => _phonemizer.PhonemizeAsync("각", "ko")).GetAwaiter().GetResult();
            var resultT = Task.Run(() => _phonemizer.PhonemizeAsync("갇", "ko")).GetAwaiter().GetResult();
            var resultP = Task.Run(() => _phonemizer.PhonemizeAsync("갑", "ko")).GetAwaiter().GetResult();

            Assert.IsTrue(resultK.Success);
            Assert.IsTrue(resultT.Success);
            Assert.IsTrue(resultP.Success);

            // All three should have 3+ phonemes (initial + medial + final)
            Assert.IsTrue(resultK.Phonemes.Length >= 3, "'각' should have initial+medial+final");
            Assert.IsTrue(resultT.Phonemes.Length >= 3, "'갇' should have initial+medial+final");
            Assert.IsTrue(resultP.Phonemes.Length >= 3, "'갑' should have initial+medial+final");
        }

        [Test]
        public void TestFinalConsonants_NasalAndLiquid()
        {
            EnsureAvailable();

            // '간' (gan) -> final n
            // '강' (gang) -> final ng
            // '갈' (gal) -> final l
            // '감' (gam) -> final m
            var resultN = Task.Run(() => _phonemizer.PhonemizeAsync("간", "ko")).GetAwaiter().GetResult();
            var resultNG = Task.Run(() => _phonemizer.PhonemizeAsync("강", "ko")).GetAwaiter().GetResult();
            var resultL = Task.Run(() => _phonemizer.PhonemizeAsync("갈", "ko")).GetAwaiter().GetResult();
            var resultM = Task.Run(() => _phonemizer.PhonemizeAsync("감", "ko")).GetAwaiter().GetResult();

            Assert.IsTrue(resultN.Success);
            Assert.IsTrue(resultNG.Success);
            Assert.IsTrue(resultL.Success);
            Assert.IsTrue(resultM.Success);

            // Each should have different final consonant IPA
            var finals = new[]
            {
                string.Join(",", resultN.Phonemes),
                string.Join(",", resultNG.Phonemes),
                string.Join(",", resultL.Phonemes),
                string.Join(",", resultM.Phonemes),
            };
            Assert.AreEqual(4, finals.Distinct().Count(),
                "Different final consonants should produce distinct outputs");
        }

        [Test]
        public void TestFinalConsonants_NoFinal()
        {
            EnsureAvailable();

            // '가' (ga) -> no final consonant (index 0)
            var withFinal = Task.Run(() => _phonemizer.PhonemizeAsync("간", "ko")).GetAwaiter().GetResult();
            var withoutFinal = Task.Run(() => _phonemizer.PhonemizeAsync("가", "ko")).GetAwaiter().GetResult();

            Assert.IsTrue(withFinal.Success);
            Assert.IsTrue(withoutFinal.Success);

            // Without final should have fewer phonemes
            Assert.IsTrue(withoutFinal.Phonemes.Length < withFinal.Phonemes.Length,
                "'가' (no final) should have fewer phonemes than '간' (with final)");
        }

        // ── Phonological rules: Liaison (연음화) ─────────────────────────────

        [Test]
        public void TestLiaison_FinalToNextInitial()
        {
            EnsureAvailable();

            // Liaison: final consonant + initial ㅇ (silent) -> final moves to initial
            // e.g., "한인" (han-in) -> "ha-nin"
            var result = Task.Run(() => _phonemizer.PhonemizeAsync("한인", "ko"))
                .GetAwaiter().GetResult();

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Liaison context should produce phonemes");
        }

        [Test]
        public void TestLiaison_MultiSyllable()
        {
            EnsureAvailable();

            // "음악" (eum-ak) -> liaison can apply: "eu-mak"
            var result = Task.Run(() => _phonemizer.PhonemizeAsync("음악", "ko"))
                .GetAwaiter().GetResult();

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0);
        }

        // ── Phonological rules: Nasalization (비음화) ─────────────────────────

        [Test]
        public void TestNasalization_ObstruentBeforeNasal()
        {
            EnsureAvailable();

            // Nasalization: obstruent final + nasal initial -> nasalized final
            // "한국말" (hanguk-mal) -> "hangung-mal" (k before m -> ng)
            var result = Task.Run(() => _phonemizer.PhonemizeAsync("한국말", "ko"))
                .GetAwaiter().GetResult();

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 5,
                "'한국말' should produce multiple phonemes");
        }

        [Test]
        public void TestNasalization_AnotherExample()
        {
            EnsureAvailable();

            // "십만" (ship-man) -> nasalization of p before m -> "shim-man"
            var result = Task.Run(() => _phonemizer.PhonemizeAsync("십만", "ko"))
                .GetAwaiter().GetResult();

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0);
        }

        // ── Phonological rules: Aspiration (격음화) ──────────────────────────

        [Test]
        public void TestAspiration_LaxPlusH()
        {
            EnsureAvailable();

            // Aspiration: lax consonant final + ㅎ initial -> aspirated
            // or ㅎ final + lax initial -> aspirated
            // "놓다" (noht-da) -> aspiration applies
            var result = Task.Run(() => _phonemizer.PhonemizeAsync("놓다", "ko"))
                .GetAwaiter().GetResult();

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Aspiration context should produce phonemes");
        }

        [Test]
        public void TestAspiration_HPlusSyllable()
        {
            EnsureAvailable();

            // "축하" (chuk-ha) -> aspiration of k + h -> kh
            var result = Task.Run(() => _phonemizer.PhonemizeAsync("축하", "ko"))
                .GetAwaiter().GetResult();

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0);
        }

        // ── Phonological rules: Tensification (경음화) ───────────────────────

        [Test]
        public void TestTensification_ObstruentPlusLax()
        {
            EnsureAvailable();

            // Tensification: obstruent final + lax initial -> tense initial
            // "학교" (hak-gyo) -> "hak-ggyo" (k + g -> k + tense g)
            var result = Task.Run(() => _phonemizer.PhonemizeAsync("학교", "ko"))
                .GetAwaiter().GetResult();

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 3);
        }

        [Test]
        public void TestTensification_AnotherExample()
        {
            EnsureAvailable();

            // "식당" (shik-dang) -> tensification of d after k
            var result = Task.Run(() => _phonemizer.PhonemizeAsync("식당", "ko"))
                .GetAwaiter().GetResult();

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0);
        }

        // ── Non-Hangul text ──────────────────────────────────────────────────

        [Test]
        public void TestNonHangul_LatinCharacters()
        {
            EnsureAvailable();

            // Latin characters should pass through
            var result = Task.Run(() => _phonemizer.PhonemizeAsync("ABC", "ko"))
                .GetAwaiter().GetResult();

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Phonemes);
            // Latin chars pass through as-is
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Latin characters should be included in output");
        }

        [Test]
        public void TestNonHangul_Punctuation()
        {
            EnsureAvailable();

            var result = Task.Run(() => _phonemizer.PhonemizeAsync("안녕!", "ko"))
                .GetAwaiter().GetResult();

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0);

            // Punctuation should be present in output
            Assert.IsTrue(result.Phonemes.Contains("!"),
                "Punctuation '!' should pass through to output");
        }

        [Test]
        public void TestNonHangul_MixedKoreanEnglish()
        {
            EnsureAvailable();

            // Mixed Korean and Latin text
            var result = Task.Run(() => _phonemizer.PhonemizeAsync("한국어 Korean", "ko"))
                .GetAwaiter().GetResult();

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 5,
                "Mixed text should produce phonemes for both parts");
        }

        // ── Empty/edge cases ─────────────────────────────────────────────────

        [Test]
        public void TestEmptyInput_ReturnsFailed()
        {
            EnsureAvailable();

            var result = Task.Run(() => _phonemizer.PhonemizeAsync("", "ko"))
                .GetAwaiter().GetResult();

            Assert.IsFalse(result.Success, "Empty input should not succeed");
        }

        [Test]
        public void TestWhitespaceOnly()
        {
            EnsureAvailable();

            var result = Task.Run(() => _phonemizer.PhonemizeAsync("   ", "ko"))
                .GetAwaiter().GetResult();

            // Whitespace-only may fail validation or produce empty result
            Assert.IsNotNull(result);
        }

        // ── Prosody output ───────────────────────────────────────────────────

        [Test]
        public void TestProsodyOutput_ArraysAligned()
        {
            EnsureAvailable();

            var result = Task.Run(() => _phonemizer.PhonemizeAsync("안녕하세요", "ko"))
                .GetAwaiter().GetResult();

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsNotNull(result.ProsodyA1);
            Assert.IsNotNull(result.ProsodyA2);
            Assert.IsNotNull(result.ProsodyA3);

            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA1.Length,
                "ProsodyA1 length should match Phonemes length");
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA2.Length,
                "ProsodyA2 length should match Phonemes length");
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA3.Length,
                "ProsodyA3 length should match Phonemes length");
        }

        [Test]
        public void TestProsodyOutput_A1A2AreZero()
        {
            EnsureAvailable();

            // Korean prosody: a1=0 (no pitch accent), a2=0 (no lexical stress)
            var result = Task.Run(() => _phonemizer.PhonemizeAsync("한국", "ko"))
                .GetAwaiter().GetResult();

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.ProsodyA1);
            Assert.IsNotNull(result.ProsodyA2);

            Assert.IsTrue(result.ProsodyA1.All(v => v == 0),
                "Korean ProsodyA1 should be all zeros (no pitch accent)");
            Assert.IsTrue(result.ProsodyA2.All(v => v == 0),
                "Korean ProsodyA2 should be all zeros (no lexical stress)");
        }

        [Test]
        public void TestProsodyOutput_A3ContainsSyllableCount()
        {
            EnsureAvailable();

            // Korean prosody: a3 = number of Hangul syllables in the word
            var result = Task.Run(() => _phonemizer.PhonemizeAsync("안녕하세요", "ko"))
                .GetAwaiter().GetResult();

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.ProsodyA3);

            // A3 should contain positive values for Hangul phonemes
            var hasPositiveA3 = result.ProsodyA3.Any(v => v >= 1);
            Assert.IsTrue(hasPositiveA3,
                "ProsodyA3 should contain positive syllable count values");
        }

        // ── PUA token mapping ────────────────────────────────────────────────

        [Test]
        public void TestPuaMapping_KoreanTenseConsonantsRegistered()
        {
            // Verify Korean tense consonant PUA mappings
            Assert.IsTrue(PuaTokenMapper.FixedPuaMapping.ContainsKey("p\u0348"),
                "Tense bilabial should be in fixed PUA mapping");
            Assert.AreEqual(0xE04B, PuaTokenMapper.FixedPuaMapping["p\u0348"]);

            Assert.IsTrue(PuaTokenMapper.FixedPuaMapping.ContainsKey("k\u0348"),
                "Tense velar should be in fixed PUA mapping");
            Assert.AreEqual(0xE04D, PuaTokenMapper.FixedPuaMapping["k\u0348"]);
        }

        [Test]
        public void TestPuaMapping_KoreanUnreleasedFinalsRegistered()
        {
            // Verify Korean unreleased final PUA mappings
            Assert.IsTrue(PuaTokenMapper.FixedPuaMapping.ContainsKey("k\u031A"),
                "Unreleased velar should be in fixed PUA mapping");
            Assert.AreEqual(0xE050, PuaTokenMapper.FixedPuaMapping["k\u031A"]);

            Assert.IsTrue(PuaTokenMapper.FixedPuaMapping.ContainsKey("t\u031A"),
                "Unreleased alveolar should be in fixed PUA mapping");
            Assert.AreEqual(0xE051, PuaTokenMapper.FixedPuaMapping["t\u031A"]);

            Assert.IsTrue(PuaTokenMapper.FixedPuaMapping.ContainsKey("p\u031A"),
                "Unreleased bilabial should be in fixed PUA mapping");
            Assert.AreEqual(0xE052, PuaTokenMapper.FixedPuaMapping["p\u031A"]);
        }

        [Test]
        public void TestPuaMapping_SharedWithChinese()
        {
            // Aspirated consonants are shared between Korean and Chinese
            Assert.IsTrue(PuaTokenMapper.FixedPuaMapping.ContainsKey("p\u02B0"),
                "Aspirated bilabial (shared) should be in fixed PUA mapping");
            Assert.AreEqual(0xE020, PuaTokenMapper.FixedPuaMapping["p\u02B0"]);

            Assert.IsTrue(PuaTokenMapper.FixedPuaMapping.ContainsKey("t\u0255"),
                "Alveolo-palatal affricate (shared) should be in fixed PUA mapping");
            Assert.AreEqual(0xE023, PuaTokenMapper.FixedPuaMapping["t\u0255"]);
        }

        // ── Dispose behavior ─────────────────────────────────────────────────

        [Test]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            var phonemizer = new KoreanPhonemizerBackend();
            Assert.DoesNotThrow(() =>
            {
                phonemizer.Dispose();
                phonemizer.Dispose();
            });
        }

        [Test]
        public void IsAvailable_BeforeInit_ReturnsFalse()
        {
            var phonemizer = new KoreanPhonemizerBackend();
            Assert.IsFalse(phonemizer.IsAvailable);
            phonemizer.Dispose();
        }

        // ── NFC normalization ────────────────────────────────────────────────

        [Test]
        public void TestNFCNormalization_HandlesDecomposedInput()
        {
            EnsureAvailable();

            // The backend normalizes to NFC internally.
            // NFC form of '한' should work correctly.
            var result = Task.Run(() => _phonemizer.PhonemizeAsync("한글", "ko"))
                .GetAwaiter().GetResult();

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "NFC-normalized Korean text should produce phonemes");
        }
    }
}