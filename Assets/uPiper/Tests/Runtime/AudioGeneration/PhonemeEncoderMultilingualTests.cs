using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using uPiper.Core;
using uPiper.Core.AudioGeneration;

namespace uPiper.Tests.Runtime.AudioGeneration
{
    /// <summary>
    /// Multilingual PhonemeEncoder tests for Phase 6.
    /// Validates encoding behaviour with phoneme_id_maps that contain phonemes
    /// from multiple languages (Japanese, Chinese, Korean, Spanish, French).
    /// </summary>
    [TestFixture]
    public class PhonemeEncoderMultilingualTests
    {
        private PhonemeEncoder _ipaEncoder;
        private PiperVoiceConfig _ipaConfig;

        private PhonemeEncoder _puaEncoder;
        private PiperVoiceConfig _puaConfig;

        /// <summary>
        /// Build a mock multilingual IPA phoneme_id_map (173 entries representative).
        /// Contains phonemes from JA, ZH, KO, ES, FR plus special tokens.
        /// The map includes the IPA marker character "ɕ" so the encoder detects IPA mode.
        /// </summary>
        private static Dictionary<string, int> BuildMultilingualIpaMap()
        {
            return new Dictionary<string, int>
            {
                // ── Special tokens ──
                { "_", 0 },   // PAD
                { "^", 1 },   // BOS
                { "$", 2 },   // EOS

                // ── Shared basic phonemes (used across many languages) ──
                { "a", 3 }, { "i", 4 }, { "u", 5 }, { "e", 6 }, { "o", 7 },
                { "b", 8 }, { "d", 9 }, { "f", 10 }, { "g", 11 }, { "h", 12 },
                { "j", 13 }, { "k", 14 }, { "l", 15 }, { "m", 16 }, { "n", 17 },
                { "p", 18 }, { "r", 19 }, { "s", 20 }, { "t", 21 }, { "v", 22 },
                { "w", 23 }, { "y", 24 }, { "z", 25 },
                { " ", 26 }, { ".", 27 }, { ",", 28 }, { "?", 29 }, { "!", 30 },

                // ── Japanese IPA phonemes ──
                { "N", 31 },     // Japanese moraic nasal (ASCII)
                { "\u0274", 32 }, // ɴ  uvular nasal (IPA)
                { "\u0255", 33 }, // ɕ  voiceless alveolo-palatal fricative (IPA detection key)
                { "t\u0255", 34 }, // tɕ  alveolo-palatal affricate
                { "q", 35 },      // glottal stop (sokuon)
                { "\u026F", 36 }, // ɯ  close back unrounded vowel
                { "\u027E", 37 }, // ɾ  alveolar tap
                { "\u0291", 38 }, // ʑ  voiced alveolo-palatal fricative
                { "k\u02B2", 39 }, // kʲ  palatalized velar
                { "\u0261\u02B2", 40 }, // ɡʲ  voiced palatalized velar
                { "d\u02B2", 41 }, // dʲ  palatalized alveolar
                { "p\u02B2", 42 }, // pʲ  palatalized bilabial
                { "b\u02B2", 43 }, // bʲ  palatalized voiced bilabial
                { "h\u02B2", 44 }, // hʲ  palatalized glottal
                { "\u0272", 45 }, // ɲ  palatal nasal
                { "m\u02B2", 46 }, // mʲ  palatalized bilabial nasal
                { "\u027D", 47 }, // ɽ  retroflex flap
                { "\u00E7", 48 }, // ç  voiceless palatal fricative
                { "\u0283", 49 }, // ʃ  voiceless postalveolar fricative

                // ── Chinese phonemes ──
                { "p\u02B0", 50 },   // pʰ  aspirated bilabial
                { "t\u02B0", 51 },   // tʰ  aspirated alveolar
                { "k\u02B0", 52 },   // kʰ  aspirated velar
                { "t\u0255\u02B0", 53 }, // tɕʰ  aspirated alveolo-palatal affricate
                { "t\u0282", 54 },   // tʂ  retroflex affricate
                { "t\u0282\u02B0", 55 }, // tʂʰ  aspirated retroflex affricate
                { "ts\u02B0", 56 },  // tsʰ  aspirated alveolar affricate
                { "a\u026A", 57 },   // aɪ  diphthong ai
                { "e\u026A", 58 },   // eɪ  diphthong ei
                { "a\u028A", 59 },   // aʊ  diphthong ao
                { "o\u028A", 60 },   // oʊ  diphthong ou
                { "an", 61 },        // an  nasal final
                { "\u0259n", 62 },   // ən  nasal final en
                { "a\u014B", 63 },   // aŋ  nasal final ang
                { "\u0259\u014B", 64 }, // əŋ  nasal final eng
                { "tone1", 65 },     // high level tone
                { "tone2", 66 },     // rising tone
                { "tone3", 67 },     // dipping tone
                { "tone4", 68 },     // falling tone
                { "tone5", 69 },     // neutral tone

                // ── Korean phonemes ──
                { "p\u0348", 70 },   // p͈  tense bilabial
                { "t\u0348", 71 },   // t͈  tense alveolar
                { "k\u0348", 72 },   // k͈  tense velar
                { "s\u0348", 73 },   // s͈  tense sibilant
                { "t\u0348\u0255", 74 }, // t͈ɕ  tense alveolo-palatal affricate
                { "k\u031A", 75 },   // k̚  unreleased velar
                { "t\u031A", 76 },   // t̚  unreleased alveolar
                { "p\u031A", 77 },   // p̚  unreleased bilabial

                // ── Spanish phonemes ──
                { "rr", 78 },        // alveolar trill (mapped via PUA 0xE01D)
                { "t\u0283", 79 },   // tʃ  voiceless postalveolar affricate
                { "d\u0292", 80 },   // dʒ  voiced postalveolar affricate
                { "\u014B", 81 },    // ŋ  velar nasal (shared)

                // ── French nasal vowels ──
                { "\u025B\u0303", 82 }, // ɛ̃  nasal open-mid front unrounded
                { "\u0251\u0303", 83 }, // ɑ̃  nasal open back unrounded
                { "\u0254\u0303", 84 }, // ɔ̃  nasal open-mid back rounded

                // ── French/Chinese shared ──
                { "y_vowel", 85 },   // close front rounded vowel [y]

                // ── Extended question markers ──
                { "\ue016", 86 },    // ?! PUA
                { "\ue017", 87 },    // ?. PUA
                { "\ue018", 88 },    // ?~ PUA
            };
        }

        /// <summary>
        /// Build a mock multilingual PUA-only phoneme_id_map (no IPA characters).
        /// Uses PUA codepoints for multi-character phonemes.
        /// Does NOT contain "ɕ", so encoder stays in PUA mode.
        /// </summary>
        private static Dictionary<string, int> BuildMultilingualPuaMap()
        {
            return new Dictionary<string, int>
            {
                // ── Special tokens ──
                { "_", 0 },   // PAD
                { "^", 1 },   // BOS
                { "$", 2 },   // EOS

                // ── Basic shared phonemes ──
                { "a", 3 }, { "i", 4 }, { "u", 5 }, { "e", 6 }, { "o", 7 },
                { "b", 8 }, { "d", 9 }, { "f", 10 }, { "g", 11 }, { "h", 12 },
                { "k", 13 }, { "m", 14 }, { "n", 15 }, { "p", 16 }, { "r", 17 },
                { "s", 18 }, { "t", 19 }, { "w", 20 }, { "y", 21 }, { "z", 22 },
                { "N", 23 },
                { " ", 24 }, { ".", 25 }, { ",", 26 }, { "?", 27 },

                // ── Japanese PUA phonemes ──
                { "\ue000", 28 }, // a:
                { "\ue001", 29 }, // i:
                { "\ue002", 30 }, // u:
                { "\ue003", 31 }, // e:
                { "\ue004", 32 }, // o:
                { "\ue005", 33 }, // cl (sokuon)
                { "\ue006", 34 }, // ky
                { "\ue008", 35 }, // gy
                { "\ue00a", 36 }, // ty
                { "\ue00b", 37 }, // dy
                { "\ue00c", 38 }, // py
                { "\ue00d", 39 }, // by
                { "\ue00e", 40 }, // ch
                { "\ue00f", 41 }, // ts
                { "\ue010", 42 }, // sh
                { "\ue011", 43 }, // zy
                { "\ue012", 44 }, // hy
                { "\ue013", 45 }, // ny
                { "\ue014", 46 }, // my
                { "\ue015", 47 }, // ry

                // ── Chinese PUA phonemes ──
                { "\ue020", 48 }, // pʰ
                { "\ue021", 49 }, // tʰ
                { "\ue022", 50 }, // kʰ
                { "\ue023", 51 }, // tɕ (ZH j)
                { "\ue028", 52 }, // aɪ
                { "\ue029", 53 }, // eɪ
                { "\ue046", 54 }, // tone1
                { "\ue047", 55 }, // tone2
                { "\ue048", 56 }, // tone3
                { "\ue049", 57 }, // tone4
                { "\ue04a", 58 }, // tone5

                // ── Korean PUA phonemes ──
                { "\ue04b", 59 }, // tense bilabial
                { "\ue04c", 60 }, // tense alveolar
                { "\ue04d", 61 }, // tense velar
                { "\ue04e", 62 }, // tense sibilant
                { "\ue050", 63 }, // unreleased velar
                { "\ue051", 64 }, // unreleased alveolar
                { "\ue052", 65 }, // unreleased bilabial

                // ── Spanish PUA phonemes ──
                { "\ue01d", 66 }, // rr (trill)
                { "\ue054", 67 }, // tʃ voiceless postalveolar affricate
                { "\ue055", 68 }, // dʒ voiced postalveolar affricate

                // ── French PUA nasal vowels ──
                { "\ue056", 69 }, // ɛ̃
                { "\ue057", 70 }, // ɑ̃
                { "\ue058", 71 }, // ɔ̃

                // ── Extended question markers ──
                { "\ue016", 72 }, // ?!
                { "\ue017", 73 }, // ?.
                { "\ue018", 74 }, // ?~
            };
        }

        [SetUp]
        public void Setup()
        {
            // IPA-based multilingual encoder
            _ipaConfig = new PiperVoiceConfig
            {
                VoiceId = "multilingual-ipa-test",
                PhonemeType = "openjtalk",
                SampleRate = 22050,
                PhonemeIdMap = BuildMultilingualIpaMap()
            };
            _ipaEncoder = new PhonemeEncoder(_ipaConfig);

            // PUA-based multilingual encoder
            _puaConfig = new PiperVoiceConfig
            {
                VoiceId = "ja_JP-multilingual-pua-test",
                PhonemeType = "openjtalk",
                SampleRate = 22050,
                PhonemeIdMap = BuildMultilingualPuaMap()
            };
            _puaEncoder = new PhonemeEncoder(_puaConfig);
        }

        #region Multilingual PhonemeIdMap Initialization

        [Test]
        public void Constructor_MultilingualMap_Initializes()
        {
            // Assert - encoder should initialise without errors
            Assert.IsNotNull(_ipaEncoder);
            Assert.IsNotNull(_puaEncoder);
            Assert.Greater(_ipaEncoder.PhonemeCount, 0);
            Assert.Greater(_puaEncoder.PhonemeCount, 0);
        }

        [Test]
        public void PhonemeCount_MultilingualMap_ReturnsCorrectCount()
        {
            // IPA map has 89 entries in BuildMultilingualIpaMap
            Assert.AreEqual(89, _ipaEncoder.PhonemeCount,
                $"IPA encoder phoneme count mismatch (got {_ipaEncoder.PhonemeCount})");

            // PUA map has 75 entries in BuildMultilingualPuaMap
            Assert.AreEqual(75, _puaEncoder.PhonemeCount,
                $"PUA encoder phoneme count mismatch (got {_puaEncoder.PhonemeCount})");
        }

        [Test]
        public void ContainsPhoneme_JapanesePhoneme_ReturnsTrue()
        {
            // IPA encoder: Japanese IPA phonemes
            Assert.IsTrue(_ipaEncoder.ContainsPhoneme("N"), "IPA encoder should contain 'N' (moraic nasal)");
            Assert.IsTrue(_ipaEncoder.ContainsPhoneme("\u0274"), "IPA encoder should contain 'ɴ' (uvular nasal)");
            Assert.IsTrue(_ipaEncoder.ContainsPhoneme("q"), "IPA encoder should contain 'q' (sokuon)");
            Assert.IsTrue(_ipaEncoder.ContainsPhoneme("\u026F"), "IPA encoder should contain 'ɯ'");

            // PUA encoder: Japanese PUA phonemes
            Assert.IsTrue(_puaEncoder.ContainsPhoneme("N"), "PUA encoder should contain 'N'");
            Assert.IsTrue(_puaEncoder.ContainsPhoneme("\ue005"), "PUA encoder should contain PUA cl (0xE005)");
            Assert.IsTrue(_puaEncoder.ContainsPhoneme("\ue00e"), "PUA encoder should contain PUA ch (0xE00E)");
        }

        [Test]
        public void ContainsPhoneme_ChinesePhoneme_ReturnsTrue()
        {
            // IPA encoder: Chinese aspirated/affricate phonemes
            Assert.IsTrue(_ipaEncoder.ContainsPhoneme("t\u0255"), "IPA encoder should contain 'tɕ'");
            Assert.IsTrue(_ipaEncoder.ContainsPhoneme("p\u02B0"), "IPA encoder should contain 'pʰ' (aspirated)");
            Assert.IsTrue(_ipaEncoder.ContainsPhoneme("t\u0282"), "IPA encoder should contain 'tʂ' (retroflex)");
            Assert.IsTrue(_ipaEncoder.ContainsPhoneme("tone1"), "IPA encoder should contain 'tone1'");
            Assert.IsTrue(_ipaEncoder.ContainsPhoneme("tone5"), "IPA encoder should contain 'tone5'");

            // PUA encoder: Chinese PUA phonemes
            Assert.IsTrue(_puaEncoder.ContainsPhoneme("\ue020"), "PUA encoder should contain PUA pʰ (0xE020)");
            Assert.IsTrue(_puaEncoder.ContainsPhoneme("\ue023"), "PUA encoder should contain PUA tɕ (0xE023)");
            Assert.IsTrue(_puaEncoder.ContainsPhoneme("\ue046"), "PUA encoder should contain PUA tone1 (0xE046)");
        }

        [Test]
        public void ContainsPhoneme_KoreanPhoneme_ReturnsTrue()
        {
            // IPA encoder: Korean tense/unreleased consonants
            Assert.IsTrue(_ipaEncoder.ContainsPhoneme("k\u0348"),
                "IPA encoder should contain 'k͈' (tense velar)");
            Assert.IsTrue(_ipaEncoder.ContainsPhoneme("p\u0348"),
                "IPA encoder should contain 'p͈' (tense bilabial)");
            Assert.IsTrue(_ipaEncoder.ContainsPhoneme("k\u031A"),
                "IPA encoder should contain 'k̚' (unreleased velar)");

            // PUA encoder: Korean PUA phonemes
            Assert.IsTrue(_puaEncoder.ContainsPhoneme("\ue04b"),
                "PUA encoder should contain PUA tense bilabial (0xE04B)");
            Assert.IsTrue(_puaEncoder.ContainsPhoneme("\ue04d"),
                "PUA encoder should contain PUA tense velar (0xE04D)");
            Assert.IsTrue(_puaEncoder.ContainsPhoneme("\ue050"),
                "PUA encoder should contain PUA unreleased velar (0xE050)");
        }

        [Test]
        public void ContainsPhoneme_SpanishPhoneme_ReturnsTrue()
        {
            // IPA encoder: Spanish trill and affricates
            Assert.IsTrue(_ipaEncoder.ContainsPhoneme("rr"),
                "IPA encoder should contain 'rr' (alveolar trill)");
            Assert.IsTrue(_ipaEncoder.ContainsPhoneme("t\u0283"),
                "IPA encoder should contain 'tʃ' (postalveolar affricate)");
            Assert.IsTrue(_ipaEncoder.ContainsPhoneme("d\u0292"),
                "IPA encoder should contain 'dʒ' (voiced postalveolar affricate)");

            // PUA encoder: Spanish PUA phonemes
            Assert.IsTrue(_puaEncoder.ContainsPhoneme("\ue01d"),
                "PUA encoder should contain PUA rr (0xE01D)");
            Assert.IsTrue(_puaEncoder.ContainsPhoneme("\ue054"),
                "PUA encoder should contain PUA tʃ (0xE054)");
        }

        [Test]
        public void ContainsPhoneme_FrenchNasalVowel_ReturnsTrue()
        {
            // IPA encoder: French nasal vowels
            Assert.IsTrue(_ipaEncoder.ContainsPhoneme("\u025B\u0303"),
                "IPA encoder should contain 'ɛ̃' (nasal open-mid front)");
            Assert.IsTrue(_ipaEncoder.ContainsPhoneme("\u0251\u0303"),
                "IPA encoder should contain 'ɑ̃' (nasal open back)");
            Assert.IsTrue(_ipaEncoder.ContainsPhoneme("\u0254\u0303"),
                "IPA encoder should contain 'ɔ̃' (nasal open-mid back rounded)");

            // PUA encoder: French PUA nasal vowels
            Assert.IsTrue(_puaEncoder.ContainsPhoneme("\ue056"),
                "PUA encoder should contain PUA ɛ̃ (0xE056)");
            Assert.IsTrue(_puaEncoder.ContainsPhoneme("\ue057"),
                "PUA encoder should contain PUA ɑ̃ (0xE057)");
            Assert.IsTrue(_puaEncoder.ContainsPhoneme("\ue058"),
                "PUA encoder should contain PUA ɔ̃ (0xE058)");
        }

        #endregion

        #region IPA Detection

        [Test]
        public void UseIpaMapping_MultilingualMap_WithIpaChars_ReturnsTrue()
        {
            // The IPA map contains "ɕ" (U+0255), so IPA detection should be true.
            // We verify indirectly: if ch (\ue00e) is PUA-input but gets mapped to tɕ,
            // that means IPA mapping is active.
            Assert.IsTrue(_ipaEncoder.ContainsPhoneme("\u0255"),
                "IPA encoder must contain 'ɕ' to trigger IPA mode");

            // Encode a PUA ch (\ue00e) -- in IPA mode it should be converted to tɕ (ID 34)
            var phonemes = new[] { "\ue00e" }; // PUA for "ch"
            var ids = _ipaEncoder.Encode(phonemes);

            // BOS + tɕ + EOS = 3
            Assert.AreEqual(3, ids.Length, $"Expected 3 IDs but got {ids.Length}: [{string.Join(", ", ids)}]");
            Assert.AreEqual(34, ids[1], "PUA ch should be mapped to IPA tɕ (ID 34) in IPA mode");
        }

        [Test]
        public void UseIpaMapping_PuaOnlyMap_ReturnsFalse()
        {
            // The PUA map does NOT contain "ɕ", so IPA detection should be false.
            Assert.IsFalse(_puaEncoder.ContainsPhoneme("\u0255"),
                "PUA encoder must NOT contain 'ɕ'");

            // Encode multi-char "ch" -- in PUA mode it should map to PUA \ue00e (ID 40)
            var phonemes = new[] { "ch" };
            var ids = _puaEncoder.Encode(phonemes);

            // BOS + PUA_ch + EOS = 3
            Assert.AreEqual(3, ids.Length, $"Expected 3 IDs but got {ids.Length}: [{string.Join(", ", ids)}]");
            Assert.AreEqual(40, ids[1], "Multi-char 'ch' should be mapped to PUA (ID 40) in PUA mode");
        }

        #endregion

        #region Encoding

        [Test]
        public void Encode_JapanesePhonemes_ReturnsCorrectIds()
        {
            // Simulate "きょう" (kyou) phonemes via IPA encoder
            // ky -> kʲ (ID 39), o (ID 7), u (ID 5)
            // Using PUA input \ue006 for "ky" which gets reversed to "ky" then mapped to kʲ
            var phonemes = new[] { "\ue006", "o", "u" };
            var ids = _ipaEncoder.Encode(phonemes);

            // BOS(1) + kʲ(39) + o(7) + u(5) + EOS(2) = 5
            Assert.AreEqual(5, ids.Length, $"Expected 5 IDs: [{string.Join(", ", ids)}]");
            Assert.AreEqual(1, ids[0], "BOS");
            Assert.AreEqual(39, ids[1], "kʲ from PUA ky");
            Assert.AreEqual(7, ids[2], "o");
            Assert.AreEqual(5, ids[3], "u");
            Assert.AreEqual(2, ids[4], "EOS");
        }

        [Test]
        public void Encode_ChineseToneMarkers_ReturnsCorrectIds()
        {
            // Chinese syllable "ma" with tone markers
            var phonemes = new[] { "m", "a", "tone1" };
            var ids = _ipaEncoder.Encode(phonemes);

            // BOS(1) + m(16) + a(3) + tone1(65) + EOS(2) = 5
            Assert.AreEqual(5, ids.Length, $"Expected 5 IDs: [{string.Join(", ", ids)}]");
            Assert.AreEqual(1, ids[0], "BOS");
            Assert.AreEqual(16, ids[1], "m");
            Assert.AreEqual(3, ids[2], "a");
            Assert.AreEqual(65, ids[3], "tone1");
            Assert.AreEqual(2, ids[4], "EOS");
        }

        [Test]
        public void Encode_SpanishPhonemes_WithStressMarker_ReturnsIds()
        {
            // Spanish "perro" (dog) -- contains trill rr
            // p(18) e(6) rr(78) o(7)
            var phonemes = new[] { "p", "e", "rr", "o" };
            var ids = _ipaEncoder.Encode(phonemes);

            // BOS(1) + p(18) + e(6) + rr(78) + o(7) + EOS(2) = 6
            Assert.AreEqual(6, ids.Length, $"Expected 6 IDs: [{string.Join(", ", ids)}]");
            Assert.AreEqual(1, ids[0], "BOS");
            Assert.AreEqual(18, ids[1], "p");
            Assert.AreEqual(6, ids[2], "e");
            Assert.AreEqual(78, ids[3], "rr (trill)");
            Assert.AreEqual(7, ids[4], "o");
            Assert.AreEqual(2, ids[5], "EOS");
        }

        [Test]
        public void Encode_FrenchNasalVowels_ReturnsIds()
        {
            // French "bon" -- contains nasal ɔ̃
            // b(8) + ɔ̃(84) + n(17)
            var phonemes = new[] { "b", "\u0254\u0303", "n" };
            var ids = _ipaEncoder.Encode(phonemes);

            // BOS(1) + b(8) + ɔ̃(84) + n(17) + EOS(2) = 5
            Assert.AreEqual(5, ids.Length, $"Expected 5 IDs: [{string.Join(", ", ids)}]");
            Assert.AreEqual(1, ids[0], "BOS");
            Assert.AreEqual(8, ids[1], "b");
            Assert.AreEqual(84, ids[2], "\u0254\u0303 (nasal open-mid back rounded)");
            Assert.AreEqual(17, ids[3], "n");
            Assert.AreEqual(2, ids[4], "EOS");
        }

        [Test]
        public void Encode_SharedPhonemes_SameIdAcrossLanguages()
        {
            // Phonemes shared between languages should resolve to the same ID
            // regardless of which "language" context they come from.

            // "a" is shared: JA /a/, ZH /a/, ES /a/, FR /a/
            var jaPhonemes = new[] { "a" };
            var jaIds = _ipaEncoder.Encode(jaPhonemes);

            var zhPhonemes = new[] { "a" };
            var zhIds = _ipaEncoder.Encode(zhPhonemes);

            // Both should yield BOS + a(3) + EOS
            Assert.AreEqual(jaIds[1], zhIds[1],
                "Shared phoneme 'a' should produce the same ID regardless of language context");
            Assert.AreEqual(3, jaIds[1], "Shared phoneme 'a' should map to ID 3");
        }

        [Test]
        public void Encode_BOS_EOS_PAD_CorrectlyInserted()
        {
            // Verify BOS is always first, EOS always last for OpenJTalk model
            var phonemes = new[] { "a", "b" };
            var ids = _ipaEncoder.Encode(phonemes);

            // BOS(1) + a(3) + b(8) + EOS(2) = 4
            Assert.AreEqual(4, ids.Length);
            Assert.AreEqual(1, ids[0], "First ID should be BOS (^)");
            Assert.AreEqual(2, ids[ids.Length - 1], "Last ID should be EOS ($)");

            // PAD should NOT appear between phonemes for OpenJTalk model
            for (var i = 1; i < ids.Length - 1; i++)
            {
                Assert.AreNotEqual(0, ids[i], $"PAD (0) should not appear at index {i} for OpenJTalk model");
            }
        }

        [Test]
        public void Encode_EosLikeTokens_CorrectlyHandled()
        {
            // When last phoneme is an EOS-like token (?!, ?., ?~), no separate EOS is added.
            // These are multi-char and get mapped to PUA in PUA mode.

            // Test with PUA encoder (PUA mode)
            var testCases = new[]
            {
                ("?!", 72, "emphatic question"),
                ("?.", 73, "declarative question"),
                ("?~", 74, "confirmatory question"),
            };

            foreach (var (marker, expectedId, description) in testCases)
            {
                var phonemes = new[] { "a", marker };
                var ids = _puaEncoder.Encode(phonemes);

                // BOS + a + marker = 3 (no EOS added because marker is EOS-like)
                Assert.AreEqual(3, ids.Length,
                    $"Expected 3 IDs for {description} (no extra EOS), got {ids.Length}: [{string.Join(", ", ids)}]");
                Assert.AreEqual(1, ids[0], $"BOS for {description}");
                Assert.AreEqual(3, ids[1], $"'a' for {description}");
                Assert.AreEqual(expectedId, ids[2], $"'{marker}' should map to PUA ID {expectedId} for {description}");
            }
        }

        [Test]
        public void Encode_UnknownPhoneme_GracefulHandling()
        {
            // Unknown phonemes should be skipped without throwing exceptions
            var phonemes = new[] { "a", "NONEXISTENT_PHONEME", "b" };
            var ids = _ipaEncoder.Encode(phonemes);

            // BOS(1) + a(3) + b(8) + EOS(2) = 4 (unknown skipped)
            Assert.AreEqual(4, ids.Length,
                $"Expected 4 IDs (unknown skipped): [{string.Join(", ", ids)}]");
            Assert.AreEqual(1, ids[0], "BOS");
            Assert.AreEqual(3, ids[1], "a");
            Assert.AreEqual(8, ids[2], "b");
            Assert.AreEqual(2, ids[3], "EOS");
        }

        #endregion

        #region PUA <-> IPA Conversion

        [Test]
        public void Encode_PuaInputWithIpaModel_ConvertsToIpa()
        {
            // When PUA characters are fed to an IPA-mode encoder, they should be
            // reverse-mapped to the original phoneme, then mapped to IPA.

            // PUA \ue005 = cl -> IPA q (ID 35)
            var clIds = _ipaEncoder.Encode(new[] { "\ue005" });
            Assert.AreEqual(3, clIds.Length, "cl: BOS + q + EOS");
            Assert.AreEqual(35, clIds[1], "PUA cl should map to IPA 'q' (ID 35) in IPA mode");

            // PUA \ue00e = ch -> IPA tɕ (ID 34)
            var chIds = _ipaEncoder.Encode(new[] { "\ue00e" });
            Assert.AreEqual(3, chIds.Length, "ch: BOS + tɕ + EOS");
            Assert.AreEqual(34, chIds[1], "PUA ch should map to IPA 'tɕ' (ID 34) in IPA mode");

            // PUA \ue010 = sh -> IPA ʃ (ID 49)
            var shIds = _ipaEncoder.Encode(new[] { "\ue010" });
            Assert.AreEqual(3, shIds.Length, "sh: BOS + ʃ + EOS");
            Assert.AreEqual(49, shIds[1], "PUA sh should map to IPA 'ʃ' (ID 49) in IPA mode");

            // PUA \ue006 = ky -> IPA kʲ (ID 39)
            var kyIds = _ipaEncoder.Encode(new[] { "\ue006" });
            Assert.AreEqual(3, kyIds.Length, "ky: BOS + kʲ + EOS");
            Assert.AreEqual(39, kyIds[1], "PUA ky should map to IPA 'kʲ' (ID 39) in IPA mode");

            // PUA \ue013 = ny -> IPA ɲ (ID 45)
            var nyIds = _ipaEncoder.Encode(new[] { "\ue013" });
            Assert.AreEqual(3, nyIds.Length, "ny: BOS + ɲ + EOS");
            Assert.AreEqual(45, nyIds[1], "PUA ny should map to IPA 'ɲ' (ID 45) in IPA mode");
        }

        [Test]
        public void Encode_IpaInputWithPuaModel_ConvertsToPua()
        {
            // When multi-char phonemes (not PUA) are fed to a PUA-mode encoder,
            // they should be mapped to PUA codepoints.

            // "ch" -> PUA \ue00e (ID 40)
            var chIds = _puaEncoder.Encode(new[] { "ch" });
            Assert.AreEqual(3, chIds.Length, "ch: BOS + PUA + EOS");
            Assert.AreEqual(40, chIds[1], "Multi-char 'ch' should map to PUA ID 40");

            // "sh" -> PUA \ue010 (ID 42)
            var shIds = _puaEncoder.Encode(new[] { "sh" });
            Assert.AreEqual(3, shIds.Length, "sh: BOS + PUA + EOS");
            Assert.AreEqual(42, shIds[1], "Multi-char 'sh' should map to PUA ID 42");

            // "ky" -> PUA \ue006 (ID 34)
            var kyIds = _puaEncoder.Encode(new[] { "ky" });
            Assert.AreEqual(3, kyIds.Length, "ky: BOS + PUA + EOS");
            Assert.AreEqual(34, kyIds[1], "Multi-char 'ky' should map to PUA ID 34");

            // "cl" -> PUA \ue005 (ID 33)
            var clIds = _puaEncoder.Encode(new[] { "cl" });
            Assert.AreEqual(3, clIds.Length, "cl: BOS + PUA + EOS");
            Assert.AreEqual(33, clIds[1], "Multi-char 'cl' should map to PUA ID 33");

            // "ny" -> PUA \ue013 (ID 45)
            var nyIds = _puaEncoder.Encode(new[] { "ny" });
            Assert.AreEqual(3, nyIds.Length, "ny: BOS + PUA + EOS");
            Assert.AreEqual(45, nyIds[1], "Multi-char 'ny' should map to PUA ID 45");
        }

        #endregion

        #region Cross-language Consistency

        [Test]
        public void Encode_PadToken_SameId_AllLanguages()
        {
            // PAD token "_" should always be ID 0 across all encoder configurations
            Assert.IsTrue(_ipaEncoder.ContainsPhoneme("_"), "IPA encoder should contain PAD '_'");
            Assert.IsTrue(_puaEncoder.ContainsPhoneme("_"), "PUA encoder should contain PAD '_'");

            // Encode an empty-like input that falls back to PAD
            // Both encoders should produce the same PAD ID
            var ipaIds = _ipaEncoder.Encode(new[] { "a" });
            var puaIds = _puaEncoder.Encode(new[] { "a" });

            // Verify PAD is not inserted between phonemes (OpenJTalk mode)
            // and that PAD token (0) is recognized in both encoders
            Assert.IsFalse(ipaIds.Contains(0), "IPA OpenJTalk encoder should not insert PAD between phonemes");
            Assert.IsFalse(puaIds.Contains(0), "PUA OpenJTalk encoder should not insert PAD between phonemes");
        }

        [Test]
        public void Encode_BosEosToken_SameId_AllLanguages()
        {
            // BOS (^) should always be ID 1, EOS ($) should always be ID 2
            // across both IPA and PUA multilingual encoders

            var phonemes = new[] { "a" };

            var ipaIds = _ipaEncoder.Encode(phonemes);
            var puaIds = _puaEncoder.Encode(phonemes);

            // Both: BOS(1) + a + EOS(2)
            Assert.AreEqual(3, ipaIds.Length, "IPA: BOS + a + EOS");
            Assert.AreEqual(3, puaIds.Length, "PUA: BOS + a + EOS");

            Assert.AreEqual(1, ipaIds[0], "IPA BOS should be ID 1");
            Assert.AreEqual(1, puaIds[0], "PUA BOS should be ID 1");

            Assert.AreEqual(2, ipaIds[2], "IPA EOS should be ID 2");
            Assert.AreEqual(2, puaIds[2], "PUA EOS should be ID 2");

            Assert.AreEqual(ipaIds[0], puaIds[0], "BOS ID should be identical across IPA and PUA encoders");
            Assert.AreEqual(ipaIds[2], puaIds[2], "EOS ID should be identical across IPA and PUA encoders");
        }

        #endregion

        #region Additional Multilingual Scenarios

        [Test]
        public void Encode_ChineseDiphthong_ReturnsCorrectIds()
        {
            // Chinese diphthongs are multi-character and exist in the IPA map
            // "ai" diphthong: aɪ (ID 57)
            var phonemes = new[] { "a\u026A" }; // aɪ
            var ids = _ipaEncoder.Encode(phonemes);

            Assert.AreEqual(3, ids.Length, "BOS + aɪ + EOS");
            Assert.AreEqual(57, ids[1], "Chinese diphthong 'aɪ' should map to ID 57");
        }

        [Test]
        public void Encode_KoreanTenseConsonant_ReturnsCorrectIds()
        {
            // Korean tense consonant k͈ (ID 72) in IPA map
            var phonemes = new[] { "k\u0348", "a" }; // k͈ + a
            var ids = _ipaEncoder.Encode(phonemes);

            Assert.AreEqual(4, ids.Length, "BOS + k͈ + a + EOS");
            Assert.AreEqual(1, ids[0], "BOS");
            Assert.AreEqual(72, ids[1], "Korean tense 'k͈' should map to ID 72");
            Assert.AreEqual(3, ids[2], "a");
            Assert.AreEqual(2, ids[3], "EOS");
        }

        [Test]
        public void Encode_MixedLanguageSequence_ReturnsIds()
        {
            // Simulate a sequence mixing phonemes from different languages
            // JA: kʲ(39) + o(7) | ZH: tone2(66) | FR: ɛ̃(82)
            var phonemes = new[] { "k\u02B2", "o", "tone2", "\u025B\u0303" };
            var ids = _ipaEncoder.Encode(phonemes);

            // BOS(1) + kʲ(39) + o(7) + tone2(66) + ɛ̃(82) + EOS(2) = 6
            Assert.AreEqual(6, ids.Length, $"Expected 6 IDs: [{string.Join(", ", ids)}]");
            Assert.AreEqual(1, ids[0], "BOS");
            Assert.AreEqual(39, ids[1], "kʲ (JA palatalized velar)");
            Assert.AreEqual(7, ids[2], "o (shared)");
            Assert.AreEqual(66, ids[3], "tone2 (ZH rising tone)");
            Assert.AreEqual(82, ids[4], "ɛ̃ (FR nasal vowel)");
            Assert.AreEqual(2, ids[5], "EOS");
        }

        [Test]
        public void Encode_PuaModel_ChineseToneMarkers_ReturnsCorrectIds()
        {
            // Chinese tone markers in PUA mode are stored as PUA codepoints
            // tone1 -> \ue046 (ID 54), tone2 -> \ue047 (ID 55)
            var phonemes = new[] { "m", "a" };
            var ids = _puaEncoder.Encode(phonemes);

            // Basic phonemes should work in PUA mode too
            Assert.AreEqual(4, ids.Length, "BOS + m + a + EOS");
            Assert.AreEqual(14, ids[1], "m (ID 14)");
            Assert.AreEqual(3, ids[2], "a (ID 3)");
        }

        [Test]
        public void Encode_NullPhonemes_ReturnsEmptyArray()
        {
            // Null input should return empty array without throwing
            var ids = _ipaEncoder.Encode(null);
            Assert.IsNotNull(ids);
            Assert.AreEqual(0, ids.Length, "Null input should return empty array");
        }

        [Test]
        public void Encode_EmptyStringPhonemes_SkipsEmpty()
        {
            // Empty strings within the phoneme array should be skipped
            var phonemes = new[] { "a", "", "b" };
            var ids = _ipaEncoder.Encode(phonemes);

            // BOS(1) + a(3) + b(8) + EOS(2) = 4 (empty string skipped)
            Assert.AreEqual(4, ids.Length, $"Expected 4 IDs (empty skipped): [{string.Join(", ", ids)}]");
            Assert.AreEqual(3, ids[1], "a");
            Assert.AreEqual(8, ids[2], "b");
        }

        [Test]
        public void EncodeWithProsody_MultilingualPhonemes_ExpandsProsodyArrays()
        {
            // Verify that prosody arrays are correctly expanded for multilingual phonemes
            var phonemes = new[] { "a", "b", "k" };
            var prosodyA1 = new[] { 1, 2, 3 };
            var prosodyA2 = new[] { 4, 5, 6 };
            var prosodyA3 = new[] { 7, 8, 9 };

            var result = _ipaEncoder.EncodeWithProsody(phonemes, prosodyA1, prosodyA2, prosodyA3);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.PhonemeIds);
            Assert.IsNotNull(result.ExpandedProsodyA1);
            Assert.IsNotNull(result.ExpandedProsodyA2);
            Assert.IsNotNull(result.ExpandedProsodyA3);

            // All arrays should have the same length: BOS + 3 phonemes + EOS = 5
            Assert.AreEqual(result.PhonemeIds.Length, result.ExpandedProsodyA1.Length,
                "ProsodyA1 should match PhonemeIds length");
            Assert.AreEqual(result.PhonemeIds.Length, result.ExpandedProsodyA2.Length,
                "ProsodyA2 should match PhonemeIds length");
            Assert.AreEqual(result.PhonemeIds.Length, result.ExpandedProsodyA3.Length,
                "ProsodyA3 should match PhonemeIds length");

            // BOS prosody should be 0
            Assert.AreEqual(0, result.ExpandedProsodyA1[0], "BOS prosody A1 should be 0");
            Assert.AreEqual(0, result.ExpandedProsodyA2[0], "BOS prosody A2 should be 0");
            Assert.AreEqual(0, result.ExpandedProsodyA3[0], "BOS prosody A3 should be 0");

            // Phoneme prosody values should be preserved
            Assert.AreEqual(1, result.ExpandedProsodyA1[1], "A1 for 'a'");
            Assert.AreEqual(5, result.ExpandedProsodyA2[2], "A2 for 'b'");
            Assert.AreEqual(9, result.ExpandedProsodyA3[3], "A3 for 'k'");
        }

        #endregion
    }
}
