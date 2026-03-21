using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using uPiper.Core;
using uPiper.Core.AudioGeneration;
using uPiper.Core.Phonemizers.Multilingual;

namespace uPiper.Tests.Runtime
{
    /// <summary>
    /// End-to-end integration tests for the 6-language multilingual model pipeline.
    /// Verifies: Text -> MultilingualPhonemizer -> phonemes -> PhonemeEncoder (multilingual) -> IDs.
    /// The mock config uses PhonemeType="multilingual" with a representative 173-phoneme map
    /// derived from the actual multilingual-test-medium.onnx.json.
    /// </summary>
    [TestFixture]
    [Timeout(60000)]
    public class MultilingualModelPipelineTests
    {
        private MultilingualPhonemizer _phonemizer;
        private PhonemeEncoder _multilingualEncoder;
        private PhonemeEncoder _japaneseEncoder;

        /// <summary>
        /// Build a mock multilingual phoneme_id_map based on the actual
        /// multilingual-test-medium.onnx.json (173 entries).
        /// Includes special tokens, Japanese PUA/IPA, English IPA, Chinese tones,
        /// Spanish, French, and Portuguese phonemes.
        /// </summary>
        private static Dictionary<string, int> BuildMultilingualModelPhonemeIdMap()
        {
            return new Dictionary<string, int>
            {
                // ── Special tokens ──
                { "_", 0 },           // PAD
                { "^", 1 },           // BOS
                { "$", 2 },           // EOS
                { "?", 3 },           // Question

                // ── Extended question markers (PUA) ──
                { "\ue016", 4 },      // ?!
                { "\ue017", 5 },      // ?.
                { "\ue018", 6 },      // ?~

                // ── Sentence structure tokens ──
                { "#", 7 },           // Word boundary
                { "[", 8 },           // Phrase open
                { "]", 9 },           // Phrase close

                // ── Japanese vowels (lowercase + uppercase long) ──
                { "a", 10 }, { "i", 11 }, { "u", 12 }, { "e", 13 }, { "o", 14 },
                { "A", 15 }, { "I", 16 }, { "U", 17 }, { "E", 18 }, { "O", 19 },

                // ── Japanese PUA long vowels ──
                { "\ue000", 20 },     // a:
                { "\ue001", 21 },     // i:
                { "\ue002", 22 },     // u:
                { "\ue003", 23 },     // e:
                { "\ue004", 24 },     // o:

                // ── Japanese moraic nasal and N variants (PUA) ──
                { "N", 25 },          // ASCII N (moraic nasal)
                { "\ue019", 26 },     // N_m (bilabial)
                { "\ue01a", 27 },     // N_n (alveolar)
                { "\ue01b", 28 },     // N_ng (velar)
                { "\ue01c", 29 },     // N_uvular

                // ── Japanese consonants / affricates (PUA) ──
                { "\ue005", 30 },     // cl (sokuon)
                { "q", 31 },          // glottal stop
                { "k", 32 },
                { "\ue006", 33 },     // ky
                { "\ue007", 34 },     // kw
                { "g", 35 },
                { "\ue008", 36 },     // gy
                { "\ue009", 37 },     // gw
                { "t", 38 },
                { "\ue00a", 39 },     // ty
                { "d", 40 },
                { "\ue00b", 41 },     // dy
                { "p", 42 },
                { "\ue00c", 43 },     // py
                { "b", 44 },
                { "\ue00d", 45 },     // by
                { "\ue00e", 46 },     // ch
                { "\ue00f", 47 },     // ts
                { "s", 48 },
                { "\ue010", 49 },     // sh
                { "z", 50 },
                { "j", 51 },
                { "\ue011", 52 },     // zy
                { "f", 53 },
                { "h", 54 },
                { "\ue012", 55 },     // hy
                { "v", 56 },
                { "n", 57 },
                { "\ue013", 58 },     // ny
                { "m", 59 },
                { "\ue014", 60 },     // my
                { "r", 61 },
                { "\ue015", 62 },     // ry
                { "w", 63 },
                { "y", 64 },

                // ── English IPA vowels / consonants ──
                { "\u0251", 65 },     // open back unrounded (father)
                { "\u00e6", 66 },     // near-open front unrounded (cat)
                { "\u028c", 67 },     // open-mid back unrounded (strut)
                { "\u0259", 68 },     // schwa
                { "\u0254", 69 },     // open-mid back rounded (thought)
                { "\u025b", 70 },     // open-mid front unrounded (dress)
                { "\u025a", 71 },     // rhotacised schwa
                { "\u025c", 72 },     // open-mid central unrounded
                { "\u026a", 73 },     // near-close near-front unrounded (kit)
                { "\u028a", 74 },     // near-close near-back rounded (foot)
                { "\u02d0", 75 },     // length mark
                { "\ue053", 76 },     // (reserved)
                { "l", 77 },
                { "\u0261", 78 },     // voiced velar plosive (script g)
                { "\u014b", 79 },     // velar nasal (ng)
                { "\u0279", 80 },     // alveolar approximant (English r)
                { "\u0283", 81 },     // voiceless postalveolar fricative (sh)
                { "\u0292", 82 },     // voiced postalveolar fricative (zh)
                { "\u03b8", 83 },     // voiceless dental fricative (th)
                { "\u00f0", 84 },     // voiced dental fricative (dh)
                { "\ue054", 85 },     // voiceless postalveolar affricate (ch, tS)
                { "\ue055", 86 },     // voiced postalveolar affricate (dZ)
                { "\u02c8", 87 },     // primary stress
                { "\u02cc", 88 },     // secondary stress
                { " ", 89 },          // space
                { ",", 90 },
                { ".", 91 },
                { ";", 92 },
                { ":", 93 },
                { "!", 94 },
                { "-", 95 },
                { "'", 96 },

                // ── Chinese phonemes (PUA) ──
                { "\ue020", 97 },     // aspirated bilabial (ph)
                { "\ue021", 98 },     // aspirated alveolar (th)
                { "\ue022", 99 },     // aspirated velar (kh)
                { "\ue023", 100 },    // alveolo-palatal affricate (j)
                { "\ue024", 101 },    // aspirated alveolo-palatal (q)
                { "\u0255", 102 },    // voiceless alveolo-palatal fricative (x)
                { "\ue025", 103 },    // retroflex affricate (zh)
                { "\ue026", 104 },    // aspirated retroflex (ch)
                { "\u0282", 105 },    // retroflex fricative (sh)
                { "\u027b", 106 },    // retroflex approximant (r)
                { "\ue027", 107 },    // aspirated alveolar affricate (c)
                { "x", 108 },         // velar fricative
                { "\u0264", 109 },    // close-mid back unrounded
                { "\ue01e", 110 },    // (reserved Chinese)
                { "\ue028", 111 },    // diphthong ai
                { "\ue029", 112 },    // diphthong ei
                { "\ue02a", 113 },    // diphthong ao
                { "\ue02b", 114 },    // diphthong ou
                { "\ue02c", 115 },    // nasal final an
                { "\ue02d", 116 },    // nasal final en
                { "\ue02e", 117 },    // nasal final ang
                { "\ue02f", 118 },    // nasal final eng
                { "\ue030", 119 },    // diphthong ia
                { "\ue031", 120 },    // diphthong ie
                { "\ue032", 121 },    // diphthong iao
                { "\ue033", 122 },    // diphthong iu
                { "\ue034", 123 },    // diphthong ian
                { "\ue035", 124 },    // diphthong in
                { "\ue036", 125 },    // diphthong iang
                { "\ue037", 126 },    // diphthong ing
                { "\ue038", 127 },    // diphthong ua
                { "\ue039", 128 },    // diphthong uo
                { "\ue03a", 129 },    // diphthong uai
                { "\ue03b", 130 },    // diphthong ui
                { "\ue03c", 131 },    // diphthong uan
                { "\ue03d", 132 },    // diphthong un
                { "\ue03e", 133 },    // diphthong uang
                { "\ue03f", 134 },    // diphthong ong
                { "\ue040", 135 },    // diphthong ue
                { "\ue041", 136 },    // diphthong uan (v)
                { "\ue042", 137 },    // diphthong un (v)
                { "\ue043", 138 },    // diphthong iong
                { "\ue044", 139 },    // tone1
                { "\ue045", 140 },    // tone2
                { "\u0268", 141 },    // close central unrounded
                { "\ue046", 142 },    // tone3
                { "\ue047", 143 },    // tone4
                { "\ue048", 144 },    // tone5
                { "\ue049", 145 },    // er final
                { "\ue04a", 146 },    // (reserved)

                // ── Spanish / Portuguese shared phonemes ──
                { "\u0272", 147 },    // palatal nasal (Spanish n-tilde)
                { "\u027e", 148 },    // alveolar flap (Spanish/Portuguese r)
                { "\ue01d", 149 },    // rr (alveolar trill, PUA)
                { "\u03b2", 150 },    // voiced bilabial fricative (Spanish b allophone)
                { "\u0263", 151 },    // voiced velar fricative (Spanish g allophone)
                { "\u029d", 152 },    // voiced palatal fricative (Spanish y allophone)
                { "\u00a1", 153 },    // inverted exclamation
                { "\u00bf", 154 },    // inverted question

                // ── Spanish PUA affricates ──
                { "\ue056", 155 },    // (reserved Spanish)
                { "\ue057", 156 },    // (reserved Spanish)
                { "\ue058", 157 },    // (reserved Spanish)

                // ── French phonemes ──
                { "\u00f8", 158 },    // close-mid front rounded (eu)
                { "\u0153", 159 },    // open-mid front rounded (coeur)
                { "\u0265", 160 },    // labial-palatal approximant (huit)
                { "\u0281", 161 },    // uvular fricative (French r)

                // ── Punctuation / typography ──
                { "\u2014", 162 },    // em dash
                { "\u2013", 163 },    // en dash
                { "\u2026", 164 },    // ellipsis
                { "\u00ab", 165 },    // left guillemet
                { "\u00bb", 166 },    // right guillemet

                // ── Portuguese nasal vowels ──
                { "\u00e3", 167 },    // nasal a
                { "\u1ebd", 168 },    // nasal e
                { "\u0129", 169 },    // nasal i
                { "\u00f5", 170 },    // nasal o
                { "\u0169", 171 },    // nasal u

                // ── Portuguese ──
                { "\u028e", 172 },    // palatal lateral approximant (lh)
            };
        }

        /// <summary>
        /// Build a non-multilingual Japanese model config for backward-compatibility testing.
        /// Uses PhonemeType="openjtalk" (no intersperse PAD).
        /// </summary>
        private static Dictionary<string, int> BuildJapaneseOnlyPhonemeIdMap()
        {
            return new Dictionary<string, int>
            {
                { "_", 0 }, { "^", 1 }, { "$", 2 }, { "?", 3 },
                { "a", 4 }, { "i", 5 }, { "u", 6 }, { "e", 7 }, { "o", 8 },
                { "A", 9 }, { "I", 10 }, { "U", 11 }, { "E", 12 }, { "O", 13 },
                { "N", 14 }, { "k", 15 }, { "g", 16 }, { "s", 17 }, { "z", 18 },
                { "t", 19 }, { "d", 20 }, { "n", 21 }, { "h", 22 }, { "b", 23 },
                { "p", 24 }, { "m", 25 }, { "y", 26 }, { "r", 27 }, { "w", 28 },
                { "j", 29 }, { "f", 30 }, { "v", 31 }, { "q", 32 },
                { "\ue005", 33 }, // cl
                { "\ue006", 34 }, // ky
                { "\ue00e", 35 }, // ch
                { "\ue00f", 36 }, // ts
                { "\ue010", 37 }, // sh
                { "\ue013", 38 }, // ny
                { " ", 39 }, { ".", 40 }, { ",", 41 },
            };
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Initialize MultilingualPhonemizer with the 6 model languages + ko
            _phonemizer = new MultilingualPhonemizer(
                LanguageConstants.AllLanguages,
                defaultLatinLanguage: "en");

            Task.Run(async () => await _phonemizer.InitializeAsync()).GetAwaiter().GetResult();
            Assert.IsTrue(_phonemizer.IsInitialized,
                "MultilingualPhonemizer should initialize successfully");

            // Build the multilingual encoder (PhonemeType="multilingual" -> intersperse PAD)
            var multilingualConfig = new PiperVoiceConfig
            {
                VoiceId = "multilingual-test-medium",
                PhonemeType = "multilingual",
                SampleRate = 22050,
                NumLanguages = 6,
                PhonemeIdMap = BuildMultilingualModelPhonemeIdMap()
            };
            _multilingualEncoder = new PhonemeEncoder(multilingualConfig);
            Assert.IsNotNull(_multilingualEncoder);

            // Build a non-multilingual Japanese encoder for backward-compat tests
            var japaneseConfig = new PiperVoiceConfig
            {
                VoiceId = "multilingual-test-medium",
                PhonemeType = "openjtalk",
                SampleRate = 22050,
                PhonemeIdMap = BuildJapaneseOnlyPhonemeIdMap()
            };
            _japaneseEncoder = new PhonemeEncoder(japaneseConfig);
            Assert.IsNotNull(_japaneseEncoder);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _phonemizer?.Dispose();
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private MultilingualPhonemizeResult Phonemize(string text)
        {
            return Task.Run(async () =>
                await _phonemizer.PhonemizeWithProsodyAsync(text))
                .GetAwaiter().GetResult();
        }

        private int[] EncodeMultilingual(string[] phonemes)
        {
            return _multilingualEncoder.Encode(phonemes);
        }

        private int[] EncodeJapanese(string[] phonemes)
        {
            return _japaneseEncoder.Encode(phonemes);
        }

        // ── Per-language pipeline: multilingual encoding ─────────────────────

        [Test]
        public void MultilingualModel_Japanese_EncodesCorrectly()
        {
            var result = Phonemize("こんにちは");

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Japanese text should produce phonemes");

            var ids = EncodeMultilingual(result.Phonemes);

            Assert.IsNotNull(ids);
            Assert.IsTrue(ids.Length > 0,
                "Japanese phonemes should encode to non-empty IDs with multilingual encoder");

            // Multilingual model uses intersperse PAD: IDs should be longer than phoneme count
            // Format: BOS, PAD, phoneme1, PAD, phoneme2, PAD, ..., EOS
            Assert.IsTrue(ids.Length > result.Phonemes.Length,
                $"Intersperse PAD should make IDs ({ids.Length}) longer than phoneme count ({result.Phonemes.Length})");

            // First ID must be BOS (1)
            Assert.AreEqual(1, ids[0], "First ID should be BOS (^) = 1");

            Debug.Log($"[MultilingualModel_Japanese] 'こんにちは' -> " +
                      $"{result.Phonemes.Length} phonemes -> {ids.Length} IDs " +
                      $"[{string.Join(", ", ids.Take(10))}...]");
        }

        [Test]
        public void MultilingualModel_English_EncodesCorrectly()
        {
            var result = Phonemize("hello");

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "English text should produce phonemes");

            var ids = EncodeMultilingual(result.Phonemes);

            Assert.IsNotNull(ids);
            Assert.IsTrue(ids.Length > 0,
                "English phonemes should encode to non-empty IDs with multilingual encoder");

            // With intersperse PAD, IDs are significantly longer
            Assert.IsTrue(ids.Length > result.Phonemes.Length,
                $"Intersperse PAD should expand IDs ({ids.Length}) beyond phoneme count ({result.Phonemes.Length})");

            Assert.AreEqual(1, ids[0], "First ID should be BOS");

            Debug.Log($"[MultilingualModel_English] 'hello' -> " +
                      $"{result.Phonemes.Length} phonemes -> {ids.Length} IDs " +
                      $"[{string.Join(", ", ids.Take(10))}...]");
        }

        [Test]
        public void MultilingualModel_Spanish_EncodesCorrectly()
        {
            var result = Phonemize("hola");

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Spanish text should produce phonemes");

            var ids = EncodeMultilingual(result.Phonemes);

            Assert.IsNotNull(ids);
            Assert.IsTrue(ids.Length > 0,
                "Spanish phonemes should encode to non-empty IDs with multilingual encoder");

            // All IDs must be non-negative (valid entries in the 173-phoneme map)
            Assert.IsTrue(ids.All(id => id >= 0),
                $"All IDs should be non-negative: [{string.Join(", ", ids)}]");

            Debug.Log($"[MultilingualModel_Spanish] 'hola' -> " +
                      $"{result.Phonemes.Length} phonemes -> {ids.Length} IDs " +
                      $"[{string.Join(", ", ids)}]");
        }

        [Test]
        public void MultilingualModel_French_EncodesCorrectly()
        {
            var result = Phonemize("bonjour");

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "French text should produce phonemes");

            var ids = EncodeMultilingual(result.Phonemes);

            Assert.IsNotNull(ids);
            Assert.IsTrue(ids.Length > 0,
                "French phonemes should encode to non-empty IDs with multilingual encoder");

            Assert.IsTrue(ids.All(id => id >= 0),
                $"All IDs should be non-negative: [{string.Join(", ", ids)}]");

            Debug.Log($"[MultilingualModel_French] 'bonjour' -> " +
                      $"{result.Phonemes.Length} phonemes -> {ids.Length} IDs " +
                      $"[{string.Join(", ", ids)}]");
        }

        [Test]
        public void MultilingualModel_Portuguese_EncodesCorrectly()
        {
            MultilingualPhonemizeResult result;
            try
            {
                result = Phonemize("olá");
            }
            catch (System.InvalidOperationException ex) when (ex.Message.Contains("cmu_lts_model.bin"))
            {
                Assert.Ignore("English LTS embedded resource (cmu_lts_model.bin) not available in Unity");
                return;
            }

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Portuguese text should produce phonemes");

            var ids = EncodeMultilingual(result.Phonemes);

            Assert.IsNotNull(ids);
            Assert.IsTrue(ids.Length > 0,
                "Portuguese phonemes should encode to non-empty IDs with multilingual encoder");

            Assert.IsTrue(ids.All(id => id >= 0),
                $"All IDs should be non-negative: [{string.Join(", ", ids)}]");

            Debug.Log($"[MultilingualModel_Portuguese] 'olá' -> " +
                      $"{result.Phonemes.Length} phonemes -> {ids.Length} IDs " +
                      $"[{string.Join(", ", ids)}]");
        }

        [Test]
        public void MultilingualModel_Chinese_EncodesCorrectly()
        {
            var result = Phonemize("你好");

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Chinese text should produce phonemes");

            var ids = EncodeMultilingual(result.Phonemes);

            Assert.IsNotNull(ids);
            Assert.IsTrue(ids.Length > 0,
                "Chinese phonemes should encode to non-empty IDs with multilingual encoder");

            Assert.IsTrue(ids.All(id => id >= 0),
                $"All IDs should be non-negative: [{string.Join(", ", ids)}]");

            Debug.Log($"[MultilingualModel_Chinese] '你好' -> " +
                      $"{result.Phonemes.Length} phonemes -> {ids.Length} IDs " +
                      $"[{string.Join(", ", ids)}]");
        }

        // ── Encoding structure verification ──────────────────────────────────

        [Test]
        public void MultilingualModel_HasInterspersePad()
        {
            // The multilingual encoder (PhonemeType="multilingual") must insert PAD (0)
            // between every phoneme, matching piper-plus post_process_ids behavior.
            var result = Phonemize("hello");
            Assert.IsTrue(result.Phonemes.Length > 0);

            var ids = EncodeMultilingual(result.Phonemes);

            // Count PAD tokens (ID 0) in the output
            var padCount = ids.Count(id => id == 0);
            Assert.IsTrue(padCount > 0,
                $"Multilingual encoder should insert PAD tokens, but found none in: [{string.Join(", ", ids)}]");

            // With intersperse: BOS, PAD, p1, PAD, p2, PAD, ..., pN, PAD, EOS
            // PAD count should be at least equal to phoneme count (one after each phoneme + one after BOS)
            Assert.IsTrue(padCount >= result.Phonemes.Length,
                $"Expected at least {result.Phonemes.Length} PAD tokens (one per phoneme + after BOS), " +
                $"got {padCount} in {ids.Length} total IDs");

            Debug.Log($"[MultilingualModel_HasInterspersePad] {padCount} PAD tokens among {ids.Length} total IDs");
        }

        [Test]
        public void MultilingualModel_HasPadAfterBOS()
        {
            // Multilingual/eSpeak models require PAD immediately after BOS:
            // [BOS(1), PAD(0), phoneme1, PAD(0), ...]
            var result = Phonemize("hello");
            Assert.IsTrue(result.Phonemes.Length > 0);

            var ids = EncodeMultilingual(result.Phonemes);

            Assert.IsTrue(ids.Length >= 3,
                $"IDs must have at least BOS + PAD + one phoneme, got {ids.Length}");

            Assert.AreEqual(1, ids[0],
                $"First ID must be BOS (1), got {ids[0]}");
            Assert.AreEqual(0, ids[1],
                $"Second ID must be PAD (0) after BOS, got {ids[1]}");

            Debug.Log($"[MultilingualModel_HasPadAfterBOS] IDs[0..2] = [{ids[0]}, {ids[1]}, {ids[2]}]");
        }

        [Test]
        public void MultilingualModel_NoTripleZeros()
        {
            // The encoder skips PAD insertion when the phoneme itself is already PAD,
            // so there should never be three consecutive zeros in the output.
            var testTexts = new[] { "こんにちは", "hello", "你好" };

            foreach (var text in testTexts)
            {
                var result = Phonemize(text);
                if (result.Phonemes.Length == 0) continue;

                var ids = EncodeMultilingual(result.Phonemes);

                for (var i = 0; i < ids.Length - 2; i++)
                {
                    var isTripleZero = ids[i] == 0 && ids[i + 1] == 0 && ids[i + 2] == 0;
                    Assert.IsFalse(isTripleZero,
                        $"Triple-zero pattern found at index {i} in IDs for '{text}': " +
                        $"[{string.Join(", ", ids)}]");
                }
            }
        }

        [Test]
        public void MultilingualModel_PuaPassthrough()
        {
            // For the multilingual model (PhonemeType="multilingual"), _useIpaMapping is false
            // and _isMultilingualModel is true. PUA characters in the phoneme_id_map should
            // be looked up directly, yielding the correct IDs from the model map.

            // Test direct PUA phonemes that exist in the multilingual model map
            var testCases = new[]
            {
                ("\ue005", 30, "cl (sokuon)"),
                ("\ue006", 33, "ky (palatalized velar)"),
                ("\ue00e", 46, "ch (affricate)"),
                ("\ue00f", 47, "ts (affricate)"),
                ("\ue010", 49, "sh (fricative)"),
                ("\ue013", 58, "ny (palatal nasal)"),
            };

            foreach (var (puaPhoneme, expectedId, description) in testCases)
            {
                var ids = EncodeMultilingual(new[] { puaPhoneme });

                // Format: BOS(1), PAD(0), phoneme, PAD(0), EOS(2)
                Assert.IsTrue(ids.Length >= 3,
                    $"PUA '{description}' should produce at least 3 IDs, got {ids.Length}");

                // Find the non-special-token ID (skip BOS=1, PAD=0, EOS=2)
                var phonemeIds = ids.Where(id => id != 0 && id != 1 && id != 2).ToArray();
                Assert.IsTrue(phonemeIds.Contains(expectedId),
                    $"PUA '{description}' (U+{((int)puaPhoneme[0]):X4}) should produce ID {expectedId}, " +
                    $"got phoneme IDs: [{string.Join(", ", phonemeIds)}] (full: [{string.Join(", ", ids)}])");
            }
        }

        // ── Mixed language ───────────────────────────────────────────────────

        [Test]
        public void MultilingualModel_MixedJaEn()
        {
            // Mixed Japanese + English text
            var text = "今日はgood";
            var result = Phonemize(text);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Mixed Japanese/English text should produce phonemes");

            var ids = EncodeMultilingual(result.Phonemes);

            Assert.IsNotNull(ids);
            Assert.IsTrue(ids.Length > 0,
                "Mixed text phonemes should encode to non-empty IDs");

            // Structural integrity: BOS at start, PAD after BOS
            Assert.AreEqual(1, ids[0], "First ID must be BOS");
            Assert.AreEqual(0, ids[1], "Second ID must be PAD after BOS");

            // Intersperse PAD should still apply for mixed-language content
            var padCount = ids.Count(id => id == 0);
            Assert.IsTrue(padCount > 0,
                "Mixed text should still have intersperse PAD tokens");

            // Mixed text should produce a reasonable number of phonemes
            Assert.IsTrue(result.Phonemes.Length >= 3,
                $"Mixed JA+EN should produce at least 3 phonemes, got {result.Phonemes.Length}");

            Debug.Log($"[MultilingualModel_MixedJaEn] '{text}' -> " +
                      $"{result.Phonemes.Length} phonemes -> {ids.Length} IDs");
        }

        [Test]
        public void MultilingualModel_MixedJaEs()
        {
            // Mixed Japanese + Spanish text
            var text = "東京はgrande";
            var result = Phonemize(text);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Mixed Japanese/Spanish text should produce phonemes");

            var ids = EncodeMultilingual(result.Phonemes);

            Assert.IsNotNull(ids);
            Assert.IsTrue(ids.Length > 0,
                "Mixed JA/ES phonemes should encode to non-empty IDs");

            // All IDs must be valid (non-negative)
            Assert.IsTrue(ids.All(id => id >= 0),
                $"All IDs should be non-negative: [{string.Join(", ", ids)}]");

            // Structural: BOS + PAD at the start
            Assert.AreEqual(1, ids[0], "First ID must be BOS");
            Assert.AreEqual(0, ids[1], "Second ID must be PAD after BOS");

            Debug.Log($"[MultilingualModel_MixedJaEs] '{text}' -> " +
                      $"{result.Phonemes.Length} phonemes -> {ids.Length} IDs " +
                      $"[{string.Join(", ", ids.Take(12))}...]");
        }

        // ── Backward compatibility ───────────────────────────────────────────

        [Test]
        public void JapaneseModel_StillWorks()
        {
            // A non-multilingual Japanese model (PhonemeType="openjtalk") must NOT
            // insert intersperse PAD tokens. This verifies backward compatibility.
            var result = Phonemize("こんにちは");

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Japanese text should produce phonemes");

            var ids = EncodeJapanese(result.Phonemes);

            Assert.IsNotNull(ids);
            Assert.IsTrue(ids.Length > 0,
                "Japanese-only encoder should produce non-empty IDs");

            // BOS at start
            Assert.AreEqual(1, ids[0], "First ID must be BOS");

            // For OpenJTalk model: NO PAD after BOS (no intersperse padding)
            if (ids.Length > 1)
            {
                Assert.AreNotEqual(0, ids[1],
                    $"OpenJTalk model should NOT have PAD after BOS. Got IDs: [{string.Join(", ", ids.Take(5))}...]");
            }

            // OpenJTalk model should NOT have intersperse PAD pattern (phoneme-PAD-phoneme).
            // Note: A trailing PAD from Japanese G2P silence marker ("_") is acceptable;
            // the key invariant is no *intersperse* PAD between phonemes.
            var hasInterspersePad = false;
            for (var i = 1; i < ids.Length - 2; i++)
            {
                if (ids[i] != 0 && ids[i + 1] == 0 && i + 2 < ids.Length && ids[i + 2] != 0 && ids[i + 2] != 2)
                {
                    hasInterspersePad = true;
                    break;
                }
            }
            Assert.IsFalse(hasInterspersePad,
                $"OpenJTalk model should have no intersperse PAD between phonemes " +
                $"in: [{string.Join(", ", ids)}]");

            // Compare: multilingual encoder for the same phonemes SHOULD have PADs
            var multilingualIds = EncodeMultilingual(result.Phonemes);
            var multilingualPadCount = multilingualIds.Count(id => id == 0);
            var jaInnerPadCount = ids.Skip(1).Take(ids.Length - 2).Count(id => id == 0);
            Assert.IsTrue(multilingualPadCount > jaInnerPadCount,
                $"Multilingual encoder should have more PADs ({multilingualPadCount}) " +
                $"than Japanese-only encoder ({jaInnerPadCount})");

            Debug.Log($"[JapaneseModel_StillWorks] OpenJTalk: {ids.Length} IDs, " +
                      $"Multilingual: {multilingualIds.Length} IDs ({multilingualPadCount} PADs)");
        }

        // ── Additional structural tests ──────────────────────────────────────

        [Test]
        public void MultilingualModel_AllLanguages_ProduceValidIds()
        {
            // Every supported language should produce valid (non-negative) IDs
            // when run through the full pipeline with the multilingual encoder.
            var testTexts = new[]
            {
                ("ja", "今日はいい天気です"),
                ("en", "The weather is nice today"),
                ("zh", "今天天气很好"),
                ("es", "El tiempo es bueno hoy"),
                ("fr", "Il fait beau aujourd'hui"),
                ("pt", "O tempo está bom hoje"),
            };

            foreach (var (lang, text) in testTexts)
            {
                MultilingualPhonemizeResult result;
                try
                {
                    result = Phonemize(text);
                }
                catch (System.InvalidOperationException ex) when (ex.Message.Contains("cmu_lts_model.bin"))
                {
                    // English LTS embedded resource not available in Unity (IL2CPP strips it).
                    // Skip this language's test case gracefully.
                    Debug.LogWarning($"[AllLanguages_{lang}] Skipped: English LTS resource unavailable");
                    continue;
                }

                Assert.IsNotNull(result, $"Phonemize should return non-null for {lang}: '{text}'");
                Assert.IsTrue(result.Phonemes.Length > 0,
                    $"{lang} text '{text}' should produce phonemes");

                var ids = EncodeMultilingual(result.Phonemes);
                Assert.IsNotNull(ids, $"Encode should return non-null for {lang}");
                Assert.IsTrue(ids.Length > 0,
                    $"{lang} should produce non-empty IDs");
                Assert.IsTrue(ids.All(id => id >= 0),
                    $"All IDs for {lang} should be non-negative: [{string.Join(", ", ids.Take(15))}...]");

                Debug.Log($"[AllLanguages_{lang}] '{text}' -> {result.Phonemes.Length} phonemes -> {ids.Length} IDs");
            }
        }

        [Test]
        public void MultilingualModel_RepeatableEncoding()
        {
            // Same text through the full pipeline should always produce identical results.
            const string text = "こんにちはworld";

            var result1 = Phonemize(text);
            var ids1 = EncodeMultilingual(result1.Phonemes);

            var result2 = Phonemize(text);
            var ids2 = EncodeMultilingual(result2.Phonemes);

            Assert.AreEqual(ids1.Length, ids2.Length,
                "Repeated pipeline should produce same ID count");

            for (var i = 0; i < ids1.Length; i++)
            {
                Assert.AreEqual(ids1[i], ids2[i],
                    $"ID at index {i} should be identical across runs");
            }
        }
    }
}