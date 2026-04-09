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
        private PuaTokenMapper _mapper;

        /// <summary>
        /// Build a mock multilingual phoneme_id_map based on the actual
        /// multilingual-test-medium.onnx.json (173 entries).
        /// Includes special tokens, Japanese PUA/IPA, English IPA, Chinese tones,
        /// Spanish, French, and Portuguese phonemes.
        /// </summary>
        private static Dictionary<string, int[]> BuildMultilingualModelPhonemeIdMap()
        {
            return new Dictionary<string, int[]>
            {
                // ── Special tokens ──
                { "_", new[] { 0 } },           // PAD
                { "^", new[] { 1 } },           // BOS
                { "$", new[] { 2 } },           // EOS
                { "?", new[] { 3 } },           // Question

                // ── Extended question markers (PUA) ──
                { "\ue016", new[] { 4 } },      // ?!
                { "\ue017", new[] { 5 } },      // ?.
                { "\ue018", new[] { 6 } },      // ?~

                // ── Sentence structure tokens ──
                { "#", new[] { 7 } },           // Word boundary
                { "[", new[] { 8 } },           // Phrase open
                { "]", new[] { 9 } },           // Phrase close

                // ── Japanese vowels (lowercase + uppercase long) ──
                { "a", new[] { 10 } }, { "i", new[] { 11 } }, { "u", new[] { 12 } }, { "e", new[] { 13 } }, { "o", new[] { 14 } },
                { "A", new[] { 15 } }, { "I", new[] { 16 } }, { "U", new[] { 17 } }, { "E", new[] { 18 } }, { "O", new[] { 19 } },

                // ── Japanese PUA long vowels ──
                { "\ue000", new[] { 20 } },     // a:
                { "\ue001", new[] { 21 } },     // i:
                { "\ue002", new[] { 22 } },     // u:
                { "\ue003", new[] { 23 } },     // e:
                { "\ue004", new[] { 24 } },     // o:

                // ── Japanese moraic nasal and N variants (PUA) ──
                { "N", new[] { 25 } },          // ASCII N (moraic nasal)
                { "\ue019", new[] { 26 } },     // N_m (bilabial)
                { "\ue01a", new[] { 27 } },     // N_n (alveolar)
                { "\ue01b", new[] { 28 } },     // N_ng (velar)
                { "\ue01c", new[] { 29 } },     // N_uvular

                // ── Japanese consonants / affricates (PUA) ──
                { "\ue005", new[] { 30 } },     // cl (sokuon)
                { "q", new[] { 31 } },          // glottal stop
                { "k", new[] { 32 } },
                { "\ue006", new[] { 33 } },     // ky
                { "\ue007", new[] { 34 } },     // kw
                { "g", new[] { 35 } },
                { "\ue008", new[] { 36 } },     // gy
                { "\ue009", new[] { 37 } },     // gw
                { "t", new[] { 38 } },
                { "\ue00a", new[] { 39 } },     // ty
                { "d", new[] { 40 } },
                { "\ue00b", new[] { 41 } },     // dy
                { "p", new[] { 42 } },
                { "\ue00c", new[] { 43 } },     // py
                { "b", new[] { 44 } },
                { "\ue00d", new[] { 45 } },     // by
                { "\ue00e", new[] { 46 } },     // ch
                { "\ue00f", new[] { 47 } },     // ts
                { "s", new[] { 48 } },
                { "\ue010", new[] { 49 } },     // sh
                { "z", new[] { 50 } },
                { "j", new[] { 51 } },
                { "\ue011", new[] { 52 } },     // zy
                { "f", new[] { 53 } },
                { "h", new[] { 54 } },
                { "\ue012", new[] { 55 } },     // hy
                { "v", new[] { 56 } },
                { "n", new[] { 57 } },
                { "\ue013", new[] { 58 } },     // ny
                { "m", new[] { 59 } },
                { "\ue014", new[] { 60 } },     // my
                { "r", new[] { 61 } },
                { "\ue015", new[] { 62 } },     // ry
                { "w", new[] { 63 } },
                { "y", new[] { 64 } },

                // ── English IPA vowels / consonants ──
                { "\u0251", new[] { 65 } },     // open back unrounded (father)
                { "\u00e6", new[] { 66 } },     // near-open front unrounded (cat)
                { "\u028c", new[] { 67 } },     // open-mid back unrounded (strut)
                { "\u0259", new[] { 68 } },     // schwa
                { "\u0254", new[] { 69 } },     // open-mid back rounded (thought)
                { "\u025b", new[] { 70 } },     // open-mid front unrounded (dress)
                { "\u025a", new[] { 71 } },     // rhotacised schwa
                { "\u025c", new[] { 72 } },     // open-mid central unrounded
                { "\u026a", new[] { 73 } },     // near-close near-front unrounded (kit)
                { "\u028a", new[] { 74 } },     // near-close near-back rounded (foot)
                { "\u02d0", new[] { 75 } },     // length mark
                { "\ue053", new[] { 76 } },     // (reserved)
                { "l", new[] { 77 } },
                { "\u0261", new[] { 78 } },     // voiced velar plosive (script g)
                { "\u014b", new[] { 79 } },     // velar nasal (ng)
                { "\u0279", new[] { 80 } },     // alveolar approximant (English r)
                { "\u0283", new[] { 81 } },     // voiceless postalveolar fricative (sh)
                { "\u0292", new[] { 82 } },     // voiced postalveolar fricative (zh)
                { "\u03b8", new[] { 83 } },     // voiceless dental fricative (th)
                { "\u00f0", new[] { 84 } },     // voiced dental fricative (dh)
                { "\ue054", new[] { 85 } },     // voiceless postalveolar affricate (ch, tS)
                { "\ue055", new[] { 86 } },     // voiced postalveolar affricate (dZ)
                { "\u02c8", new[] { 87 } },     // primary stress
                { "\u02cc", new[] { 88 } },     // secondary stress
                { " ", new[] { 89 } },          // space
                { ",", new[] { 90 } },
                { ".", new[] { 91 } },
                { ";", new[] { 92 } },
                { ":", new[] { 93 } },
                { "!", new[] { 94 } },
                { "-", new[] { 95 } },
                { "'", new[] { 96 } },

                // ── Chinese phonemes (PUA) ──
                { "\ue020", new[] { 97 } },     // aspirated bilabial (ph)
                { "\ue021", new[] { 98 } },     // aspirated alveolar (th)
                { "\ue022", new[] { 99 } },     // aspirated velar (kh)
                { "\ue023", new[] { 100 } },    // alveolo-palatal affricate (j)
                { "\ue024", new[] { 101 } },    // aspirated alveolo-palatal (q)
                { "\u0255", new[] { 102 } },    // voiceless alveolo-palatal fricative (x)
                { "\ue025", new[] { 103 } },    // retroflex affricate (zh)
                { "\ue026", new[] { 104 } },    // aspirated retroflex (ch)
                { "\u0282", new[] { 105 } },    // retroflex fricative (sh)
                { "\u027b", new[] { 106 } },    // retroflex approximant (r)
                { "\ue027", new[] { 107 } },    // aspirated alveolar affricate (c)
                { "x", new[] { 108 } },         // velar fricative
                { "\u0264", new[] { 109 } },    // close-mid back unrounded
                { "\ue01e", new[] { 110 } },    // (reserved Chinese)
                { "\ue028", new[] { 111 } },    // diphthong ai
                { "\ue029", new[] { 112 } },    // diphthong ei
                { "\ue02a", new[] { 113 } },    // diphthong ao
                { "\ue02b", new[] { 114 } },    // diphthong ou
                { "\ue02c", new[] { 115 } },    // nasal final an
                { "\ue02d", new[] { 116 } },    // nasal final en
                { "\ue02e", new[] { 117 } },    // nasal final ang
                { "\ue02f", new[] { 118 } },    // nasal final eng
                { "\ue030", new[] { 119 } },    // diphthong ia
                { "\ue031", new[] { 120 } },    // diphthong ie
                { "\ue032", new[] { 121 } },    // diphthong iao
                { "\ue033", new[] { 122 } },    // diphthong iu
                { "\ue034", new[] { 123 } },    // diphthong ian
                { "\ue035", new[] { 124 } },    // diphthong in
                { "\ue036", new[] { 125 } },    // diphthong iang
                { "\ue037", new[] { 126 } },    // diphthong ing
                { "\ue038", new[] { 127 } },    // diphthong ua
                { "\ue039", new[] { 128 } },    // diphthong uo
                { "\ue03a", new[] { 129 } },    // diphthong uai
                { "\ue03b", new[] { 130 } },    // diphthong ui
                { "\ue03c", new[] { 131 } },    // diphthong uan
                { "\ue03d", new[] { 132 } },    // diphthong un
                { "\ue03e", new[] { 133 } },    // diphthong uang
                { "\ue03f", new[] { 134 } },    // diphthong ong
                { "\ue040", new[] { 135 } },    // diphthong ue
                { "\ue041", new[] { 136 } },    // diphthong uan (v)
                { "\ue042", new[] { 137 } },    // diphthong un (v)
                { "\ue043", new[] { 138 } },    // diphthong iong
                { "\ue044", new[] { 139 } },    // tone1
                { "\ue045", new[] { 140 } },    // tone2
                { "\u0268", new[] { 141 } },    // close central unrounded
                { "\ue046", new[] { 142 } },    // tone3
                { "\ue047", new[] { 143 } },    // tone4
                { "\ue048", new[] { 144 } },    // tone5
                { "\ue049", new[] { 145 } },    // er final
                { "\ue04a", new[] { 146 } },    // (reserved)

                // ── Spanish / Portuguese shared phonemes ──
                { "\u0272", new[] { 147 } },    // palatal nasal (Spanish n-tilde)
                { "\u027e", new[] { 148 } },    // alveolar flap (Spanish/Portuguese r)
                { "\ue01d", new[] { 149 } },    // rr (alveolar trill, PUA)
                { "\u03b2", new[] { 150 } },    // voiced bilabial fricative (Spanish b allophone)
                { "\u0263", new[] { 151 } },    // voiced velar fricative (Spanish g allophone)
                { "\u029d", new[] { 152 } },    // voiced palatal fricative (Spanish y allophone)
                { "\u00a1", new[] { 153 } },    // inverted exclamation
                { "\u00bf", new[] { 154 } },    // inverted question

                // ── Spanish PUA affricates ──
                { "\ue056", new[] { 155 } },    // (reserved Spanish)
                { "\ue057", new[] { 156 } },    // (reserved Spanish)
                { "\ue058", new[] { 157 } },    // (reserved Spanish)

                // ── French phonemes ──
                { "\u00f8", new[] { 158 } },    // close-mid front rounded (eu)
                { "\u0153", new[] { 159 } },    // open-mid front rounded (coeur)
                { "\u0265", new[] { 160 } },    // labial-palatal approximant (huit)
                { "\u0281", new[] { 161 } },    // uvular fricative (French r)

                // ── Punctuation / typography ──
                { "\u2014", new[] { 162 } },    // em dash
                { "\u2013", new[] { 163 } },    // en dash
                { "\u2026", new[] { 164 } },    // ellipsis
                { "\u00ab", new[] { 165 } },    // left guillemet
                { "\u00bb", new[] { 166 } },    // right guillemet

                // ── Portuguese nasal vowels ──
                { "\u00e3", new[] { 167 } },    // nasal a
                { "\u1ebd", new[] { 168 } },    // nasal e
                { "\u0129", new[] { 169 } },    // nasal i
                { "\u00f5", new[] { 170 } },    // nasal o
                { "\u0169", new[] { 171 } },    // nasal u

                // ── Portuguese ──
                { "\u028e", new[] { 172 } },    // palatal lateral approximant (lh)
            };
        }

        /// <summary>
        /// Build a non-multilingual Japanese model config for backward-compatibility testing.
        /// Uses PhonemeType="openjtalk" (no intersperse PAD).
        /// </summary>
        private static Dictionary<string, int[]> BuildJapaneseOnlyPhonemeIdMap()
        {
            return new Dictionary<string, int[]>
            {
                { "_", new[] { 0 } }, { "^", new[] { 1 } }, { "$", new[] { 2 } }, { "?", new[] { 3 } },
                { "a", new[] { 4 } }, { "i", new[] { 5 } }, { "u", new[] { 6 } }, { "e", new[] { 7 } }, { "o", new[] { 8 } },
                { "A", new[] { 9 } }, { "I", new[] { 10 } }, { "U", new[] { 11 } }, { "E", new[] { 12 } }, { "O", new[] { 13 } },
                { "N", new[] { 14 } }, { "k", new[] { 15 } }, { "g", new[] { 16 } }, { "s", new[] { 17 } }, { "z", new[] { 18 } },
                { "t", new[] { 19 } }, { "d", new[] { 20 } }, { "n", new[] { 21 } }, { "h", new[] { 22 } }, { "b", new[] { 23 } },
                { "p", new[] { 24 } }, { "m", new[] { 25 } }, { "y", new[] { 26 } }, { "r", new[] { 27 } }, { "w", new[] { 28 } },
                { "j", new[] { 29 } }, { "f", new[] { 30 } }, { "v", new[] { 31 } }, { "q", new[] { 32 } },
                { "\ue005", new[] { 33 } }, // cl
                { "\ue006", new[] { 34 } }, // ky
                { "\ue00e", new[] { 35 } }, // ch
                { "\ue00f", new[] { 36 } }, // ts
                { "\ue010", new[] { 37 } }, // sh
                { "\ue013", new[] { 38 } }, // ny
                { " ", new[] { 39 } }, { ".", new[] { 40 } }, { ",", new[] { 41 } },
            };
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _mapper = new PuaTokenMapper();

            // Initialize MultilingualPhonemizer with the 6 model languages + ko
            _phonemizer = new MultilingualPhonemizer(
                new MultilingualPhonemizerOptions
                {
                    Languages = LanguageConstants.AllLanguages,
                    DefaultLatinLanguage = "en"
                });

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
            _multilingualEncoder = new PhonemeEncoder(multilingualConfig, _mapper);
            Assert.IsNotNull(_multilingualEncoder);

            // Build a non-multilingual Japanese encoder for backward-compat tests
            var japaneseConfig = new PiperVoiceConfig
            {
                VoiceId = "multilingual-test-medium",
                PhonemeType = "openjtalk",
                SampleRate = 22050,
                PhonemeIdMap = BuildJapaneseOnlyPhonemeIdMap()
            };
            _japaneseEncoder = new PhonemeEncoder(japaneseConfig, _mapper);
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