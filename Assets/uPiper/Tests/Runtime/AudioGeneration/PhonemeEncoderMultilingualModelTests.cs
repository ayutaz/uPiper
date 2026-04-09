using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using uPiper.Core;
using uPiper.Core.AudioGeneration;
using uPiper.Core.Phonemizers.Multilingual;

namespace uPiper.Tests.Runtime.AudioGeneration
{
    /// <summary>
    /// Tests for PhonemeEncoder with the multilingual model (phoneme_type = "multilingual").
    /// Validates intersperse PAD insertion, PUA/IPA passthrough, N variant preservation,
    /// and backward compatibility with existing Japanese and IPA models.
    /// Based on multilingual-test-medium.onnx.json phoneme_id_map (173 symbols).
    /// </summary>
    [TestFixture]
    public class PhonemeEncoderMultilingualModelTests
    {
        private PhonemeEncoder _multilingualEncoder;
        private PiperVoiceConfig _multilingualConfig;

        private PhonemeEncoder _jaEncoder;
        private PiperVoiceConfig _jaConfig;

        private PhonemeEncoder _espeakEncoder;
        private PiperVoiceConfig _espeakConfig;

        private PhonemeEncoder _tsukuyomiEncoder;
        private PiperVoiceConfig _tsukuyomiConfig;
        private PuaTokenMapper _mapper;

        /// <summary>
        /// Build a mock multilingual phoneme_id_map derived from
        /// multilingual-test-medium.onnx.json (subset of 173 entries).
        /// </summary>
        private static Dictionary<string, int[]> BuildMultilingualModelMap()
        {
            return new Dictionary<string, int[]>
            {
                // ── Special tokens ──
                { "_", new[] { 0 } },   // PAD
                { "^", new[] { 1 } },   // BOS
                { "$", new[] { 2 } },   // EOS
                { "?", new[] { 3 } },   // Question

                // ── Extended question markers ──
                { "\ue016", new[] { 4 } },  // ?!
                { "\ue017", new[] { 5 } },  // ?.
                { "\ue018", new[] { 6 } },  // ?~

                // ── Prosody boundary tokens ──
                { "#", new[] { 7 } },   // Word boundary
                { "[", new[] { 8 } },   // Accent phrase start
                { "]", new[] { 9 } },   // Accent phrase end

                // ── Japanese vowels (lowercase = normal, uppercase = devoiced) ──
                { "a", new[] { 10 } }, { "i", new[] { 11 } }, { "u", new[] { 12 } }, { "e", new[] { 13 } }, { "o", new[] { 14 } },
                { "A", new[] { 15 } }, { "I", new[] { 16 } }, { "U", new[] { 17 } }, { "E", new[] { 18 } }, { "O", new[] { 19 } },

                // ── Long vowels (PUA) ──
                { "\ue000", new[] { 20 } }, // a:
                { "\ue001", new[] { 21 } }, // i:
                { "\ue002", new[] { 22 } }, // u:
                { "\ue003", new[] { 23 } }, // e:
                { "\ue004", new[] { 24 } }, // o:

                // ── N and N variants ──
                { "N", new[] { 25 } },
                { "\ue019", new[] { 26 } }, // N_m  (bilabial assimilation)
                { "\ue01a", new[] { 27 } }, // N_n  (alveolar assimilation)
                { "\ue01b", new[] { 28 } }, // N_ng (velar assimilation)
                { "\ue01c", new[] { 29 } }, // N_uvular

                // ── Japanese consonants and palatalized consonants (PUA) ──
                { "\ue005", new[] { 30 } }, // cl (sokuon)
                { "q", new[] { 31 } },     // glottal stop
                { "k", new[] { 32 } },
                { "\ue006", new[] { 33 } }, // ky
                { "\ue007", new[] { 34 } }, // kw
                { "g", new[] { 35 } },
                { "\ue008", new[] { 36 } }, // gy
                { "\ue009", new[] { 37 } }, // gw
                { "t", new[] { 38 } },
                { "\ue00a", new[] { 39 } }, // ty
                { "d", new[] { 40 } },
                { "\ue00b", new[] { 41 } }, // dy
                { "p", new[] { 42 } },
                { "\ue00c", new[] { 43 } }, // py
                { "b", new[] { 44 } },
                { "\ue00d", new[] { 45 } }, // by
                { "\ue00e", new[] { 46 } }, // ch
                { "\ue00f", new[] { 47 } }, // ts
                { "s", new[] { 48 } },
                { "\ue010", new[] { 49 } }, // sh
                { "z", new[] { 50 } },
                { "j", new[] { 51 } },
                { "\ue011", new[] { 52 } }, // zy
                { "f", new[] { 53 } },
                { "h", new[] { 54 } },
                { "\ue012", new[] { 55 } }, // hy
                { "v", new[] { 56 } },
                { "n", new[] { 57 } },
                { "\ue013", new[] { 58 } }, // ny
                { "m", new[] { 59 } },
                { "\ue014", new[] { 60 } }, // my
                { "r", new[] { 61 } },
                { "\ue015", new[] { 62 } }, // ry
                { "w", new[] { 63 } },
                { "y", new[] { 64 } },

                // ── English vowels (IPA) ──
                { "\u0251", new[] { 65 } }, // ɑ
                { "\u00e6", new[] { 66 } }, // æ
                { "\u028c", new[] { 67 } }, // ʌ
                { "\u0259", new[] { 68 } }, // ə
                { "\u0254", new[] { 69 } }, // ɔ
                { "\u025b", new[] { 70 } }, // ɛ
                { "\u025a", new[] { 71 } }, // ɚ
                { "\u025c", new[] { 72 } }, // ɜ
                { "\u026a", new[] { 73 } }, // ɪ
                { "\u028a", new[] { 74 } }, // ʊ
                { "\u02d0", new[] { 75 } }, // ː (length mark)

                // ── English consonants ──
                { "\ue053", new[] { 76 } }, // PUA (English-specific)
                { "l", new[] { 77 } },
                { "\u0261", new[] { 78 } }, // ɡ
                { "\u014b", new[] { 79 } }, // ŋ
                { "\u0279", new[] { 80 } }, // ɹ
                { "\u0283", new[] { 81 } }, // ʃ
                { "\u0292", new[] { 82 } }, // ʒ
                { "\u03b8", new[] { 83 } }, // θ
                { "\u00f0", new[] { 84 } }, // ð

                // ── English affricates (PUA) ──
                { "\ue054", new[] { 85 } }, // tʃ
                { "\ue055", new[] { 86 } }, // dʒ

                // ── English stress markers ──
                { "\u02c8", new[] { 87 } }, // ˈ primary stress
                { "\u02cc", new[] { 88 } }, // ˌ secondary stress

                // ── Punctuation ──
                { " ", new[] { 89 } },
                { ",", new[] { 90 } },
                { ".", new[] { 91 } },
                { ";", new[] { 92 } },
                { ":", new[] { 93 } },
                { "!", new[] { 94 } },
                { "-", new[] { 95 } },
                { "'", new[] { 96 } },

                // ── Chinese phonemes (PUA) ──
                { "\ue020", new[] { 97 } },  // pʰ
                { "\ue021", new[] { 98 } },  // tʰ
                { "\ue022", new[] { 99 } },  // kʰ
                { "\ue023", new[] { 100 } }, // tɕ (ZH j)
                { "\ue024", new[] { 101 } }, // tɕʰ (ZH q)
                { "\u0255", new[] { 102 } }, // ɕ (ZH x)
                { "\ue025", new[] { 103 } }, // PUA ZH
                { "\ue026", new[] { 104 } }, // PUA ZH
                { "\u0282", new[] { 105 } }, // ʂ (retroflex)
                { "\u027b", new[] { 106 } }, // ɻ
                { "\ue027", new[] { 107 } }, // PUA ZH

                // ── Chinese vowels/diphthongs (PUA) ──
                { "\ue028", new[] { 111 } }, // aɪ
                { "\ue029", new[] { 112 } }, // eɪ

                // ── Chinese tone markers (PUA) ──
                { "\ue046", new[] { 142 } }, // tone1
                { "\ue047", new[] { 143 } }, // tone2
                { "\ue048", new[] { 144 } }, // tone3
                { "\ue049", new[] { 145 } }, // tone4
                { "\ue04a", new[] { 146 } }, // tone5

                // ── Spanish / shared phonemes ──
                { "\u0272", new[] { 147 } }, // ɲ
                { "\u027e", new[] { 148 } }, // ɾ
                { "\ue01d", new[] { 149 } }, // rr (trill)

                // ── French nasal vowels (PUA) ──
                { "\ue056", new[] { 155 } }, // ɛ̃
                { "\ue057", new[] { 156 } }, // ɑ̃
                { "\ue058", new[] { 157 } }, // ɔ̃
            };
        }

        /// <summary>
        /// Build a standard Japanese (OpenJTalk) phoneme_id_map for backward compatibility tests.
        /// </summary>
        private static Dictionary<string, int[]> BuildJapaneseModelMap()
        {
            return new Dictionary<string, int[]>
            {
                { "_", new[] { 0 } }, { "^", new[] { 1 } }, { "$", new[] { 2 } },
                { "a", new[] { 3 } }, { "i", new[] { 4 } }, { "u", new[] { 5 } }, { "e", new[] { 6 } }, { "o", new[] { 7 } },
                { "k", new[] { 8 } }, { "g", new[] { 9 } }, { "s", new[] { 10 } }, { "z", new[] { 11 } },
                { "t", new[] { 12 } }, { "d", new[] { 13 } }, { "n", new[] { 14 } }, { "h", new[] { 15 } },
                { "b", new[] { 16 } }, { "p", new[] { 17 } }, { "m", new[] { 18 } }, { "y", new[] { 19 } },
                { "r", new[] { 20 } }, { "w", new[] { 21 } }, { "N", new[] { 22 } },
                { "\ue005", new[] { 23 } }, // cl
                { "\ue006", new[] { 26 } }, // ky
                { "\ue008", new[] { 27 } }, // gy
                { "\ue00e", new[] { 39 } }, // ch
                { "\ue00f", new[] { 40 } }, // ts
                { "\ue010", new[] { 42 } }, // sh
                { "\ue013", new[] { 43 } }, // ny
                { " ", new[] { 44 } }, { ".", new[] { 45 } }, { "?", new[] { 46 } },
            };
        }

        /// <summary>
        /// Build a mock eSpeak English phoneme_id_map for comparison.
        /// </summary>
        private static Dictionary<string, int[]> BuildESpeakModelMap()
        {
            return new Dictionary<string, int[]>
            {
                { "_", new[] { 0 } }, { "^", new[] { 1 } }, { "$", new[] { 2 } },
                { "a", new[] { 3 } }, { "b", new[] { 4 } }, { "d", new[] { 5 } }, { "e", new[] { 6 } },
                { "f", new[] { 7 } }, { "k", new[] { 8 } }, { "l", new[] { 9 } }, { "m", new[] { 10 } },
                { "n", new[] { 11 } }, { "p", new[] { 12 } }, { "s", new[] { 13 } }, { "t", new[] { 14 } },
                { " ", new[] { 15 } },
            };
        }

        /// <summary>
        /// Build a mock IPA-based Japanese phoneme_id_map for backward compatibility.
        /// </summary>
        private static Dictionary<string, int[]> BuildTsukuyomiModelMap()
        {
            return new Dictionary<string, int[]>
            {
                { "_", new[] { 0 } }, { "^", new[] { 1 } }, { "$", new[] { 2 } }, { "?", new[] { 3 } },
                { "a", new[] { 7 } }, { "i", new[] { 8 } }, { "u", new[] { 9 } }, { "e", new[] { 10 } }, { "o", new[] { 11 } },
                { "k", new[] { 25 } }, { "g", new[] { 28 } }, { "t", new[] { 31 } }, { "d", new[] { 33 } },
                { "p", new[] { 35 } }, { "b", new[] { 37 } }, { "s", new[] { 41 } }, { "z", new[] { 43 } },
                { "n", new[] { 50 } }, { "m", new[] { 52 } }, { "r", new[] { 54 } }, { "w", new[] { 56 } }, { "y", new[] { 57 } },
                { "N", new[] { 22 } }, { "q", new[] { 24 } }, { "f", new[] { 46 } }, { "h", new[] { 47 } }, { "j", new[] { 44 } },
                // IPA characters (presence of "ɕ" triggers IPA mode)
                { "\u0255", new[] { 18 } }, // ɕ
                { "t\u0255", new[] { 32 } }, // tɕ
                { "k\u02B2", new[] { 26 } }, // kʲ
                { "\u0272", new[] { 45 } }, // ɲ
                { "\u0283", new[] { 42 } }, // ʃ
                { "\u0274", new[] { 20 } }, // ɴ
                { "\u026F", new[] { 19 } }, // ɯ
                { "\u027E", new[] { 21 } }, // ɾ
                { "\u027D", new[] { 55 } }, // ɽ
                { " ", new[] { 58 } },
            };
        }

        [SetUp]
        public void Setup()
        {
            _mapper = new PuaTokenMapper();

            // Multilingual model encoder
            _multilingualConfig = new PiperVoiceConfig
            {
                VoiceId = "multilingual-test-medium",
                PhonemeType = "multilingual",
                SampleRate = 22050,
                PhonemeIdMap = BuildMultilingualModelMap()
            };
            _multilingualEncoder = new PhonemeEncoder(_multilingualConfig, _mapper);

            // Japanese (OpenJTalk) model encoder
            _jaConfig = new PiperVoiceConfig
            {
                VoiceId = "multilingual-test-medium",
                PhonemeType = "openjtalk",
                SampleRate = 22050,
                PhonemeIdMap = BuildJapaneseModelMap()
            };
            _jaEncoder = new PhonemeEncoder(_jaConfig, _mapper);

            // eSpeak English model encoder
            _espeakConfig = new PiperVoiceConfig
            {
                VoiceId = "multilingual-test-medium",
                PhonemeType = "espeak",
                SampleRate = 22050,
                PhonemeIdMap = BuildESpeakModelMap()
            };
            _espeakEncoder = new PhonemeEncoder(_espeakConfig, _mapper);

            // IPA-based Japanese model encoder
            _tsukuyomiConfig = new PiperVoiceConfig
            {
                VoiceId = "multilingual-test-medium",
                PhonemeType = "openjtalk",
                SampleRate = 22050,
                PhonemeIdMap = BuildTsukuyomiModelMap()
            };
            _tsukuyomiEncoder = new PhonemeEncoder(_tsukuyomiConfig, _mapper);
        }

        #region NeedsInterspersePadding

        /// <summary>
        /// Multilingual model (phoneme_type = "multilingual") must insert PAD between phonemes,
        /// matching piper-plus post_process_ids behaviour.
        /// Verified by checking that PAD (0) appears between encoded phonemes.
        /// </summary>
        [Test]
        public void NeedsInterspersePadding_MultilingualModel_ReturnsTrue()
        {
            // Arrange
            var phonemes = new[] { "a", "b" };

            // Act
            var ids = _multilingualEncoder.Encode(phonemes);

            // Assert: BOS(1) + PAD(0) + a(10) + PAD(0) + b(44) + PAD(0) + EOS(2) = 7
            Assert.IsTrue(ids.Contains(0),
                "Multilingual model must insert intersperse PAD tokens between phonemes");
        }

        /// <summary>
        /// eSpeak model (phoneme_type = "espeak") must also insert PAD between phonemes.
        /// </summary>
        [Test]
        public void NeedsInterspersePadding_ESpeakModel_ReturnsTrue()
        {
            // Arrange
            var phonemes = new[] { "a", "b" };

            // Act
            var ids = _espeakEncoder.Encode(phonemes);

            // Assert: PAD (0) should appear between phonemes
            Assert.IsTrue(ids.Contains(0),
                "eSpeak model must insert intersperse PAD tokens between phonemes");
        }

        /// <summary>
        /// Japanese OpenJTalk model (phoneme_type = "openjtalk") must NOT insert PAD between phonemes.
        /// </summary>
        [Test]
        public void NeedsInterspersePadding_JapaneseModel_ReturnsFalse()
        {
            // Arrange
            var phonemes = new[] { "a", "k" };

            // Act
            var ids = _jaEncoder.Encode(phonemes);

            // Assert: BOS(1) + a(3) + k(8) + EOS(2) = 4, no PAD
            Assert.IsFalse(ids.Contains(0),
                "Japanese OpenJTalk model must NOT insert intersperse PAD tokens");
            Assert.AreEqual(4, ids.Length,
                $"Expected 4 IDs (BOS + a + k + EOS), got {ids.Length}: [{string.Join(", ", ids)}]");
        }

        #endregion

        #region Intersperse PAD insertion

        /// <summary>
        /// Verify that the multilingual model inserts PAD (0) between every encoded phoneme,
        /// producing the pattern: BOS, PAD, phoneme1, PAD, phoneme2, PAD, ..., EOS.
        /// </summary>
        [Test]
        public void Encode_MultilingualModel_InsertsInterspersePad()
        {
            // Arrange
            var phonemes = new[] { "a", "k", "o" };

            // Act
            var ids = _multilingualEncoder.Encode(phonemes);

            // Assert: BOS(1) + PAD(0) + a(10) + PAD(0) + k(32) + PAD(0) + o(14) + PAD(0) + EOS(2) = 9
            Assert.AreEqual(9, ids.Length,
                $"Expected 9 IDs with intersperse PAD, got {ids.Length}: [{string.Join(", ", ids)}]");

            Assert.AreEqual(1, ids[0], "ids[0] should be BOS (^)");
            Assert.AreEqual(0, ids[1], "ids[1] should be PAD after BOS");
            Assert.AreEqual(10, ids[2], "ids[2] should be 'a' (ID 10)");
            Assert.AreEqual(0, ids[3], "ids[3] should be PAD after 'a'");
            Assert.AreEqual(32, ids[4], "ids[4] should be 'k' (ID 32)");
            Assert.AreEqual(0, ids[5], "ids[5] should be PAD after 'k'");
            Assert.AreEqual(14, ids[6], "ids[6] should be 'o' (ID 14)");
            Assert.AreEqual(0, ids[7], "ids[7] should be PAD after 'o'");
            Assert.AreEqual(2, ids[8], "ids[8] should be EOS ($)");
        }

        /// <summary>
        /// Verify that PAD is inserted immediately after BOS for multilingual model,
        /// matching piper-plus post_process_ids: [BOS, PAD, phoneme1, ...].
        /// </summary>
        [Test]
        public void Encode_MultilingualModel_PadAfterBOS()
        {
            // Arrange
            var phonemes = new[] { "a" };

            // Act
            var ids = _multilingualEncoder.Encode(phonemes);

            // Assert: BOS(1) + PAD(0) + a(10) + PAD(0) + EOS(2) = 5
            Assert.AreEqual(5, ids.Length,
                $"Expected 5 IDs, got {ids.Length}: [{string.Join(", ", ids)}]");
            Assert.AreEqual(1, ids[0], "ids[0] should be BOS");
            Assert.AreEqual(0, ids[1], "ids[1] should be PAD immediately after BOS");
            Assert.AreEqual(10, ids[2], "ids[2] should be 'a' (ID 10)");
        }

        /// <summary>
        /// When a phoneme itself resolves to the PAD ID (0), the encoder should skip
        /// adding an additional PAD after it to prevent triple-zero sequences.
        /// This tests the guard: phonemeId != _padId.
        /// </summary>
        [Test]
        public void Encode_MultilingualModel_SkipPadAfterExistingPad()
        {
            // Arrange: Feed "_" (PAD token) as a regular phoneme.
            // The encoder maps "_" to ID 0, and should NOT add another PAD after it.
            var phonemes = new[] { "a", "_", "b" };

            // Act
            var ids = _multilingualEncoder.Encode(phonemes);

            // Verify no triple-zero sequence exists
            for (var i = 0; i < ids.Length - 2; i++)
            {
                var tripleZero = ids[i] == 0 && ids[i + 1] == 0 && ids[i + 2] == 0;
                Assert.IsFalse(tripleZero,
                    $"Triple-zero (PAD, PAD, PAD) found at index {i}: [{string.Join(", ", ids)}]");
            }
        }

        #endregion

        #region MapPhoneme multilingual passthrough

        /// <summary>
        /// PUA characters (e.g., \ue006 for ky) should pass through to the phoneme_id_map
        /// directly without conversion, since the multilingual model's map contains PUA entries.
        /// </summary>
        [Test]
        public void MapPhoneme_MultilingualModel_PassesPuaThrough()
        {
            // Arrange: PUA \ue006 (ky) should map to ID 33 directly
            var phonemes = new[] { "\ue006" };

            // Act
            var ids = _multilingualEncoder.Encode(phonemes);

            // Assert: BOS(1) + PAD(0) + ky(33) + PAD(0) + EOS(2) = 5
            Assert.AreEqual(5, ids.Length,
                $"Expected 5 IDs, got {ids.Length}: [{string.Join(", ", ids)}]");
            Assert.AreEqual(33, ids[2],
                $"PUA \\ue006 (ky) should map directly to ID 33, got {ids[2]}");
        }

        /// <summary>
        /// Single-character IPA phonemes present in the multilingual model's phoneme_id_map
        /// (e.g., ɕ U+0255, ʃ U+0283) should pass through without conversion.
        /// </summary>
        [Test]
        public void MapPhoneme_MultilingualModel_PassesSingleIpaThrough()
        {
            // Arrange: ɕ (U+0255) should map to ID 102 directly
            var phonemes = new[] { "\u0255" }; // ɕ

            // Act
            var ids = _multilingualEncoder.Encode(phonemes);

            // Assert: BOS(1) + PAD(0) + ɕ(102) + PAD(0) + EOS(2) = 5
            Assert.AreEqual(5, ids.Length,
                $"Expected 5 IDs, got {ids.Length}: [{string.Join(", ", ids)}]");
            Assert.AreEqual(102, ids[2],
                $"IPA 'ɕ' (U+0255) should map directly to ID 102 in multilingual model, got {ids[2]}");
        }

        /// <summary>
        /// Multilingual model must NOT use the puaToPhonemeMap reverse-mapping that IPA models use.
        /// PUA characters should be looked up directly in the phoneme_id_map, not reversed to
        /// their original phoneme name (e.g., \ue005 should NOT become "cl" then "q").
        /// </summary>
        [Test]
        public void MapPhoneme_MultilingualModel_NoJapaneseReverseMapping()
        {
            // Arrange: PUA \ue005 (cl) in the multilingual model has its own ID (30).
            // In IPA mode, it would be reversed to "cl" then mapped to "q".
            // In multilingual mode, it should stay as \ue005 -> ID 30.
            var phonemes = new[] { "\ue005" };

            // Act
            var ids = _multilingualEncoder.Encode(phonemes);

            // Assert: BOS(1) + PAD(0) + cl_PUA(30) + PAD(0) + EOS(2) = 5
            Assert.AreEqual(5, ids.Length,
                $"Expected 5 IDs, got {ids.Length}: [{string.Join(", ", ids)}]");
            Assert.AreEqual(30, ids[2],
                $"PUA \\ue005 (cl) should map to ID 30 (not q=31) in multilingual model, got {ids[2]}");

            // Verify it did NOT get mapped to "q" (ID 31) via puaToPhonemeMap reverse lookup
            Assert.AreNotEqual(31, ids[2],
                "Multilingual model should NOT reverse-map PUA to 'cl' then to 'q' (IPA mode behaviour)");
        }

        #endregion

        #region N variant preservation

        /// <summary>
        /// The multilingual model has distinct phoneme_id_map entries for N variants:
        /// N (25), N_m (26), N_n (27), N_ng (28), N_uvular (29).
        /// Verify each variant is encoded to its own unique ID.
        /// </summary>
        [Test]
        public void Encode_MultilingualModel_PreservesNVariants()
        {
            // Arrange: N variant PUA codepoints from multiCharPhonemeMap
            var nVariants = new[]
            {
                ("N", 25, "N (plain)"),
                ("N_m", 26, "N_m (bilabial, PUA \\ue019)"),
                ("N_n", 27, "N_n (alveolar, PUA \\ue01a)"),
                ("N_ng", 28, "N_ng (velar, PUA \\ue01b)"),
                ("N_uvular", 29, "N_uvular (PUA \\ue01c)"),
            };

            var encodedIds = new List<int>();

            foreach (var (phoneme, expectedId, description) in nVariants)
            {
                var ids = _multilingualEncoder.Encode(new[] { phoneme });

                // BOS(1) + PAD(0) + N_variant + PAD(0) + EOS(2) = 5
                Assert.AreEqual(5, ids.Length,
                    $"Expected 5 IDs for {description}, got {ids.Length}: [{string.Join(", ", ids)}]");
                Assert.AreEqual(expectedId, ids[2],
                    $"{description} should map to ID {expectedId}, got {ids[2]}");

                encodedIds.Add(ids[2]);
            }

            // Verify all N variant IDs are distinct
            Assert.AreEqual(encodedIds.Count, encodedIds.Distinct().Count(),
                $"All N variant IDs should be distinct: [{string.Join(", ", encodedIds)}]");
        }

        #endregion

        #region Backward compatibility

        /// <summary>
        /// Verify that existing Japanese OpenJTalk model still encodes
        /// correctly without intersperse PAD, preserving existing behaviour.
        /// </summary>
        [Test]
        public void Encode_JapaneseModel_StillWorksAsExpected()
        {
            // Arrange: "かお" (kao) phonemes
            var phonemes = new[] { "k", "a", "o" };

            // Act
            var ids = _jaEncoder.Encode(phonemes);

            // Assert: BOS(1) + k(8) + a(3) + o(7) + EOS(2) = 5, no PAD
            Assert.AreEqual(5, ids.Length,
                $"Expected 5 IDs (no PAD), got {ids.Length}: [{string.Join(", ", ids)}]");
            Assert.AreEqual(1, ids[0], "BOS");
            Assert.AreEqual(8, ids[1], "k (ID 8)");
            Assert.AreEqual(3, ids[2], "a (ID 3)");
            Assert.AreEqual(7, ids[3], "o (ID 7)");
            Assert.AreEqual(2, ids[4], "EOS");

            // Verify no PAD between phonemes
            for (var i = 1; i < ids.Length - 1; i++)
            {
                Assert.AreNotEqual(0, ids[i],
                    $"PAD (0) should not appear at index {i} for Japanese OpenJTalk model");
            }
        }

        /// <summary>
        /// Verify that existing IPA-based Japanese model still maps PUA input to IPA phonemes
        /// correctly via puaToPhonemeMap -> multiCharToIpaMap pipeline.
        /// </summary>
        [Test]
        public void Encode_TsukuyomiModel_StillWorksAsExpected()
        {
            // Arrange: PUA \ue006 (ky) should be reverse-mapped to "ky" then to IPA "kʲ" (ID 26)
            var phonemes = new[] { "\ue006", "o" };

            // Act
            var ids = _tsukuyomiEncoder.Encode(phonemes);

            // Assert: BOS(1) + kʲ(26) + o(11) + EOS(2) = 4 (no PAD for OpenJTalk)
            Assert.AreEqual(4, ids.Length,
                $"Expected 4 IDs (no PAD for tsukuyomi), got {ids.Length}: [{string.Join(", ", ids)}]");
            Assert.AreEqual(1, ids[0], "BOS");
            Assert.AreEqual(26, ids[1],
                $"PUA ky should map to IPA kʲ (ID 26) in IPA mode, got {ids[1]}");
            Assert.AreEqual(11, ids[2], "o (ID 11)");
            Assert.AreEqual(2, ids[3], "EOS");

            // Verify no PAD between phonemes (OpenJTalk model)
            Assert.IsFalse(ids.Contains(0),
                "IPA-based Japanese (OpenJTalk) model should not insert PAD between phonemes");
        }

        #endregion

        #region Additional encoding tests

        /// <summary>
        /// Verify encoding of a complete Japanese word with prosody markers through multilingual model.
        /// </summary>
        [Test]
        public void Encode_MultilingualModel_JapaneseWordWithProsodyMarkers()
        {
            // Arrange: "[" + k + o + n + n + i + ch(\ue00e) + i + "]"
            // Corresponds to "こんにちは" with accent boundaries
            var phonemes = new[] { "[", "k", "o", "n", "n", "i", "\ue00e", "i", "]" };

            // Act
            var ids = _multilingualEncoder.Encode(phonemes);

            // Assert: Each phoneme should have PAD after it
            // BOS(1) + PAD(0) + [(8) + PAD(0) + k(32) + PAD(0) + o(14) + PAD(0)
            // + n(57) + PAD(0) + n(57) + PAD(0) + i(11) + PAD(0) + ch(46) + PAD(0)
            // + i(11) + PAD(0) + ](9) + PAD(0) + EOS(2) = 21
            Assert.AreEqual(21, ids.Length,
                $"Expected 21 IDs with intersperse PAD, got {ids.Length}: [{string.Join(", ", ids)}]");

            // Verify BOS and EOS
            Assert.AreEqual(1, ids[0], "First ID should be BOS");
            Assert.AreEqual(2, ids[ids.Length - 1], "Last ID should be EOS");

            // Verify PAD after BOS
            Assert.AreEqual(0, ids[1], "PAD should follow BOS");
        }

        /// <summary>
        /// Verify English IPA vowels are encoded correctly in multilingual model
        /// without being converted or dropped.
        /// </summary>
        [Test]
        public void Encode_MultilingualModel_EnglishIpaVowels()
        {
            // Arrange: English schwa ə (U+0259, ID 68) and open-mid ɛ (U+025B, ID 70)
            var phonemes = new[] { "\u0259", "\u025b" };

            // Act
            var ids = _multilingualEncoder.Encode(phonemes);

            // Assert: BOS(1) + PAD(0) + ə(68) + PAD(0) + ɛ(70) + PAD(0) + EOS(2) = 7
            Assert.AreEqual(7, ids.Length,
                $"Expected 7 IDs, got {ids.Length}: [{string.Join(", ", ids)}]");
            Assert.AreEqual(68, ids[2], "ə (U+0259) should map to ID 68");
            Assert.AreEqual(70, ids[4], "ɛ (U+025B) should map to ID 70");
        }

        /// <summary>
        /// Verify Chinese tone markers encoded via PUA are handled correctly.
        /// </summary>
        [Test]
        public void Encode_MultilingualModel_ChineseToneMarkers()
        {
            // Arrange: tone1 (\ue046, ID 142) through tone5 (\ue04a, ID 146)
            var toneTests = new[]
            {
                ("\ue046", 142, "tone1"),
                ("\ue047", 143, "tone2"),
                ("\ue048", 144, "tone3"),
                ("\ue049", 145, "tone4"),
                ("\ue04a", 146, "tone5"),
            };

            foreach (var (pua, expectedId, description) in toneTests)
            {
                var ids = _multilingualEncoder.Encode(new[] { pua });

                // BOS(1) + PAD(0) + tone(id) + PAD(0) + EOS(2) = 5
                Assert.AreEqual(5, ids.Length,
                    $"Expected 5 IDs for {description}, got {ids.Length}: [{string.Join(", ", ids)}]");
                Assert.AreEqual(expectedId, ids[2],
                    $"{description} (PUA {pua}) should map to ID {expectedId}, got {ids[2]}");
            }
        }

        /// <summary>
        /// Verify French nasal vowels (PUA) are correctly encoded in multilingual model.
        /// </summary>
        [Test]
        public void Encode_MultilingualModel_FrenchNasalVowels()
        {
            // Arrange: ɛ̃ (\ue056, ID 155), ɑ̃ (\ue057, ID 156), ɔ̃ (\ue058, ID 157)
            var nasalTests = new[]
            {
                ("\ue056", 155, "\u025B\u0303 (nasal front)"),
                ("\ue057", 156, "\u0251\u0303 (nasal back)"),
                ("\ue058", 157, "\u0254\u0303 (nasal rounded)"),
            };

            foreach (var (pua, expectedId, description) in nasalTests)
            {
                var ids = _multilingualEncoder.Encode(new[] { pua });

                Assert.AreEqual(5, ids.Length,
                    $"Expected 5 IDs for {description}, got {ids.Length}: [{string.Join(", ", ids)}]");
                Assert.AreEqual(expectedId, ids[2],
                    $"{description} should map to ID {expectedId}, got {ids[2]}");
            }
        }

        /// <summary>
        /// Verify that multi-char phonemes (e.g., "ky", "ch") are converted to PUA
        /// before lookup in the multilingual model, since _useIpaMapping is false.
        /// </summary>
        [Test]
        public void Encode_MultilingualModel_MultiCharPhonemeConvertsToPua()
        {
            // Arrange: "ky" should be mapped to PUA \ue006 (ID 33) via multiCharPhonemeMap
            var phonemes = new[] { "ky" };

            // Act
            var ids = _multilingualEncoder.Encode(phonemes);

            // Assert: BOS(1) + PAD(0) + ky_PUA(33) + PAD(0) + EOS(2) = 5
            Assert.AreEqual(5, ids.Length,
                $"Expected 5 IDs, got {ids.Length}: [{string.Join(", ", ids)}]");
            Assert.AreEqual(33, ids[2],
                $"Multi-char 'ky' should be converted to PUA then mapped to ID 33, got {ids[2]}");
        }

        /// <summary>
        /// Verify that the multilingual model does NOT detect IPA mode, even though
        /// the phoneme_id_map contains the IPA character ɕ (U+0255).
        /// IPA detection is explicitly skipped for multilingual models.
        /// </summary>
        [Test]
        public void Encode_MultilingualModel_DoesNotActivateIpaMode()
        {
            // The multilingual map contains ɕ (U+0255, ID 102),
            // but _useIpaMapping should be false because _isMultilingualModel is true.
            // Verify by checking that PUA \ue00e (ch) is NOT reverse-mapped to "ch" then "tɕ".
            // Instead it should map directly to ID 46 (the PUA entry in the map).
            var phonemes = new[] { "\ue00e" }; // PUA for "ch"

            var ids = _multilingualEncoder.Encode(phonemes);

            Assert.AreEqual(5, ids.Length,
                $"Expected 5 IDs, got {ids.Length}: [{string.Join(", ", ids)}]");
            Assert.AreEqual(46, ids[2],
                $"PUA ch should map to ID 46 (direct PUA lookup), not IPA tɕ. Got {ids[2]}");
        }

        /// <summary>
        /// Verify EOS-like tokens (?, ?!, ?., ?~) are handled correctly in multilingual model.
        /// When the last phoneme is EOS-like, no separate EOS token is appended.
        /// </summary>
        [Test]
        public void Encode_MultilingualModel_EosLikeTokenHandling()
        {
            // Arrange: phonemes ending with "?" (EOS-like)
            var phonemes = new[] { "a", "?" };

            // Act
            var ids = _multilingualEncoder.Encode(phonemes);

            // Assert: BOS(1) + PAD(0) + a(10) + PAD(0) + ?(3) + PAD(0) = 6
            // Note: "?" is EOS-like, so no separate EOS ($) is added
            var lastNonPadId = ids.Last(id => id != 0);
            Assert.AreEqual(3, lastNonPadId,
                $"Last non-PAD ID should be '?' (ID 3), got {lastNonPadId}. IDs: [{string.Join(", ", ids)}]");

            // EOS ($, ID 2) should NOT be present
            Assert.IsFalse(ids.Contains(2),
                $"EOS ($) should not be present when last phoneme is '?'. IDs: [{string.Join(", ", ids)}]");
        }

        /// <summary>
        /// Verify stress markers (primary and secondary) are encoded correctly
        /// for English phonemes in the multilingual model.
        /// </summary>
        [Test]
        public void Encode_MultilingualModel_EnglishStressMarkers()
        {
            // Arrange: primary stress (U+02C8, ID 87) + a + secondary stress (U+02CC, ID 88)
            var phonemes = new[] { "\u02c8", "a", "\u02cc" };

            // Act
            var ids = _multilingualEncoder.Encode(phonemes);

            // Assert: BOS(1) + PAD(0) + stress1(87) + PAD(0) + a(10) + PAD(0) + stress2(88) + PAD(0) + EOS(2) = 9
            Assert.AreEqual(9, ids.Length,
                $"Expected 9 IDs, got {ids.Length}: [{string.Join(", ", ids)}]");
            Assert.AreEqual(87, ids[2], "Primary stress (U+02C8) should map to ID 87");
            Assert.AreEqual(10, ids[4], "'a' should map to ID 10");
            Assert.AreEqual(88, ids[6], "Secondary stress (U+02CC) should map to ID 88");
        }

        /// <summary>
        /// Verify encoding of an empty phoneme array returns empty result,
        /// consistent across all model types.
        /// </summary>
        [Test]
        public void Encode_MultilingualModel_EmptyPhonemes_ReturnsEmpty()
        {
            // Act
            var ids = _multilingualEncoder.Encode(new string[0]);

            // Assert
            Assert.IsNotNull(ids);
            Assert.AreEqual(0, ids.Length, "Empty phoneme array should produce empty ID array");
        }

        /// <summary>
        /// Verify encoding with prosody arrays correctly expands for multilingual model,
        /// including PAD tokens which get prosody value 0.
        /// </summary>
        [Test]
        public void EncodeWithProsody_MultilingualModel_ExpandsWithPadProsody()
        {
            // Arrange
            var phonemes = new[] { "a", "k" };
            // stride=3 flat: [a1_0, a2_0, a3_0, a1_1, a2_1, a3_1]
            var prosodyFlat = new[] { 1, 3, 5, 2, 4, 6 };

            // Act
            var result = _multilingualEncoder.EncodeWithProsody(phonemes, prosodyFlat);

            // Assert
            Assert.IsNotNull(result);

            // ExpandedProsodyFlat.Length = PhonemeIds.Length * 3
            Assert.AreEqual(result.PhonemeIds.Length * 3, result.ExpandedProsodyFlat.Length,
                "ExpandedProsodyFlat length should be PhonemeIds.Length * 3");

            // BOS prosody should be 0 (all components)
            Assert.AreEqual(0, result.ExpandedProsodyFlat[0 * 3 + 0], "BOS prosody A1 should be 0");

            // PAD prosody should be 0
            Assert.AreEqual(0, result.ExpandedProsodyFlat[1 * 3 + 0], "PAD after BOS prosody A1 should be 0");

            // Verify phoneme prosody values are preserved at correct positions
            // IDs: BOS(1) + PAD(0) + a(10) + PAD(0) + k(32) + PAD(0) + EOS(2)
            Assert.AreEqual(1, result.ExpandedProsodyFlat[2], "Prosody A1 for 'a' should be 1");
            Assert.AreEqual(4, result.ExpandedProsodyFlat[4], "Prosody A2 for 'k' should be 4");
        }

        #endregion
    }
}