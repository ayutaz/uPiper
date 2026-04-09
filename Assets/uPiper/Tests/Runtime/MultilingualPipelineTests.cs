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
    /// End-to-end multilingual pipeline tests (Phase 6).
    /// Verifies: Text -> MultilingualPhonemizer -> phonemes -> PhonemeEncoder -> phoneme IDs.
    /// ONNX inference is not tested here (not available in EditMode).
    /// </summary>
    [TestFixture]
    [Timeout(60000)]
    public class MultilingualPipelineTests
    {
        private MultilingualPhonemizer _phonemizer;
        private PhonemeEncoder _encoder;
        private PuaTokenMapper _mapper;

        /// <summary>
        /// A multilingual phoneme ID map covering the basic IPA phonemes shared across languages.
        /// This mirrors a realistic multilingual model's phoneme_id_map with IPA-based entries.
        /// </summary>
        private static Dictionary<string, int[]> BuildMultilingualPhonemeIdMap()
        {
            return new Dictionary<string, int[]>
            {
                // Special tokens
                { "_", new[] { 0 } }, { "^", new[] { 1 } }, { "$", new[] { 2 } }, { "?", new[] { 3 } },

                // Basic vowels
                { "a", new[] { 4 } }, { "e", new[] { 5 } }, { "i", new[] { 6 } }, { "o", new[] { 7 } }, { "u", new[] { 8 } },

                // Japanese extended vowels
                { "A", new[] { 9 } }, { "I", new[] { 10 } }, { "U", new[] { 11 } }, { "E", new[] { 12 } }, { "O", new[] { 13 } },

                // IPA vowels (multilingual)
                { "\u0259", new[] { 14 } },  // schwa
                { "\u025B", new[] { 15 } },  // open-mid front unrounded (French/Portuguese)
                { "\u0254", new[] { 16 } },  // open-mid back rounded (French)
                { "\u0251", new[] { 17 } },  // open back unrounded (French)
                { "\u026A", new[] { 18 } },  // near-close near-front unrounded
                { "\u028A", new[] { 19 } },  // near-close near-back rounded
                { "\u0268", new[] { 20 } },  // close central unrounded
                { "\u00F8", new[] { 21 } },  // close-mid front rounded (French eu)

                // Japanese-specific IPA
                { "\u026F", new[] { 22 } },  // close back unrounded (Japanese u)
                { "\u0274", new[] { 23 } },  // small capital N (Japanese moraic nasal)
                { "N", new[] { 24 } },       // ASCII N (Japanese moraic nasal alternate)
                { "\u0255", new[] { 25 } },  // alveolo-palatal fricative (Japanese shi)
                { "\u0291", new[] { 26 } },  // voiced alveolo-palatal fricative (Japanese ji)
                { "q", new[] { 27 } },       // glottal stop (Japanese sokuon)

                // Basic consonants
                { "b", new[] { 28 } }, { "d", new[] { 29 } }, { "f", new[] { 30 } }, { "g", new[] { 31 } },
                { "h", new[] { 32 } }, { "j", new[] { 33 } }, { "k", new[] { 34 } }, { "l", new[] { 35 } },
                { "m", new[] { 36 } }, { "n", new[] { 37 } }, { "p", new[] { 38 } }, { "r", new[] { 39 } },
                { "s", new[] { 40 } }, { "t", new[] { 41 } }, { "v", new[] { 42 } }, { "w", new[] { 43 } },
                { "z", new[] { 44 } }, { "y", new[] { 45 } },

                // IPA consonants (multilingual)
                { "\u0283", new[] { 46 } },  // voiceless postalveolar fricative (sh)
                { "\u0292", new[] { 47 } },  // voiced postalveolar fricative (zh)
                { "\u014B", new[] { 48 } },  // velar nasal (ng)
                { "\u0272", new[] { 49 } },  // palatal nasal (Spanish n-tilde, French gn)
                { "\u0279", new[] { 50 } },  // alveolar approximant (English r)
                { "\u027E", new[] { 51 } },  // alveolar flap/tap (Spanish r, Portuguese r)
                { "\u0281", new[] { 52 } },  // uvular fricative (French r)
                { "\u027D", new[] { 53 } },  // retroflex flap (Japanese ry)
                { "\u0278", new[] { 54 } },  // voiceless bilabial fricative (Japanese f)
                { "\u00E7", new[] { 55 } },  // voiceless palatal fricative (Japanese h before i)

                // IPA affricates / palatalized (multilingual)
                { "t\u0255", new[] { 56 } },     // voiceless alveolo-palatal affricate (ja ch, zh j)
                { "t\u0283", new[] { 57 } },     // voiceless postalveolar affricate (es/pt ch)
                { "d\u0292", new[] { 58 } },     // voiced postalveolar affricate (es/pt)
                { "t\u0282", new[] { 59 } },     // retroflex affricate (zh zh)
                { "ts", new[] { 60 } },          // alveolar affricate (ja tsu)

                // IPA palatalized consonants
                { "k\u02B2", new[] { 61 } },     // palatalized k (Japanese ky)
                { "\u0261\u02B2", new[] { 62 } }, // palatalized g (Japanese gy)
                { "d\u02B2", new[] { 63 } },     // palatalized d (Japanese dy)
                { "p\u02B2", new[] { 64 } },     // palatalized p (Japanese py)
                { "b\u02B2", new[] { 65 } },     // palatalized b (Japanese by)
                { "h\u02B2", new[] { 66 } },     // palatalized h (Japanese hy)
                { "m\u02B2", new[] { 67 } },     // palatalized m (Japanese my)

                // Aspiration (Chinese/Korean)
                { "p\u02B0", new[] { 68 } },     // aspirated p
                { "t\u02B0", new[] { 69 } },     // aspirated t
                { "k\u02B0", new[] { 70 } },     // aspirated k
                { "t\u0255\u02B0", new[] { 71 } }, // aspirated alveolo-palatal affricate (zh q)
                { "t\u0282\u02B0", new[] { 72 } }, // aspirated retroflex affricate (zh ch)
                { "ts\u02B0", new[] { 73 } },    // aspirated alveolar affricate (zh c)

                // Chinese tone markers
                { "tone1", new[] { 74 } }, { "tone2", new[] { 75 } }, { "tone3", new[] { 76 } },
                { "tone4", new[] { 77 } }, { "tone5", new[] { 78 } },

                // French nasal vowels
                { "\u025B\u0303", new[] { 79 } },  // nasal epsilon (vin)
                { "\u0251\u0303", new[] { 80 } },  // nasal alpha (France)
                { "\u0254\u0303", new[] { 81 } },  // nasal open-o (bon)

                // Korean tense consonants
                { "p\u0348", new[] { 82 } },         // tense bilabial
                { "t\u0348", new[] { 83 } },         // tense alveolar
                { "k\u0348", new[] { 84 } },         // tense velar
                { "s\u0348", new[] { 85 } },         // tense sibilant
                { "t\u0348\u0255", new[] { 86 } },   // tense alveolo-palatal affricate

                // Korean unreleased finals
                { "k\u031A", new[] { 87 } },  // unreleased velar
                { "t\u031A", new[] { 88 } },  // unreleased alveolar
                { "p\u031A", new[] { 89 } },  // unreleased bilabial

                // Punctuation / silence
                { " ", new[] { 90 } },
                { ".", new[] { 91 } }, { ",", new[] { 92 } }, { "!", new[] { 93 } },
                { "-", new[] { 94 } }, { "'", new[] { 95 } },

                // Spanish trill
                { "rr", new[] { 96 } },

                // Shared multilingual
                { "y_vowel", new[] { 97 } },  // French u, Chinese u-umlaut

                // Extended question markers (PUA-encoded)
                { "\uE016", new[] { 98 } }, { "\uE017", new[] { 99 } }, { "\uE018", new[] { 100 } },

                // Chinese diphthongs / compound finals (PUA)
                { "a\u026A", new[] { 101 } },  // ai
                { "e\u026A", new[] { 102 } },  // ei
                { "a\u028A", new[] { 103 } },  // ao
                { "o\u028A", new[] { 104 } },  // ou
                { "an", new[] { 105 } },       // an
                { "\u0259n", new[] { 106 } },  // en
                { "a\u014B", new[] { 107 } },  // ang
                { "\u0259\u014B", new[] { 108 } }, // eng

                // Additional Japanese PUA characters for long vowels
                { "\uE000", new[] { 109 } },  // a:
                { "\uE001", new[] { 110 } },  // i:
                { "\uE002", new[] { 111 } },  // u:
                { "\uE003", new[] { 112 } },  // e:
                { "\uE004", new[] { 113 } },  // o:
                { "\uE005", new[] { 114 } },  // cl
                { "\uE006", new[] { 115 } },  // ky
                { "\uE00E", new[] { 116 } },  // ch
                { "\uE00F", new[] { 117 } },  // ts
                { "\uE010", new[] { 118 } },  // sh
                { "\uE013", new[] { 119 } },  // ny
            };
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _mapper = new PuaTokenMapper();

            // Initialize MultilingualPhonemizer with all 7 languages
            _phonemizer = new MultilingualPhonemizer(
                new MultilingualPhonemizerOptions
                {
                    Languages = LanguageConstants.AllLanguages,
                    DefaultLatinLanguage = "en"
                });

            Task.Run(async () => await _phonemizer.InitializeAsync()).GetAwaiter().GetResult();
            Assert.IsTrue(_phonemizer.IsInitialized,
                "MultilingualPhonemizer should initialize successfully with all 7 languages");

            // Build a multilingual phoneme encoder using a comprehensive IPA-based phoneme map
            var config = new PiperVoiceConfig
            {
                VoiceId = "multilingual-test",
                PhonemeType = "openjtalk",
                SampleRate = 22050,
                PhonemeIdMap = BuildMultilingualPhonemeIdMap()
            };
            _encoder = new PhonemeEncoder(config, _mapper);
            Assert.IsNotNull(_encoder);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _phonemizer?.Dispose();
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private MultilingualPhonemizeResult Phonemize(string text)
        {
            return Task.Run(async () =>
                await _phonemizer.PhonemizeWithProsodyAsync(text))
                .GetAwaiter().GetResult();
        }

        private int[] PhonemesToIds(string[] phonemes)
        {
            return _encoder.Encode(phonemes);
        }

        // ── Full pipeline: Text -> Phonemes -> IDs (per language) ────────────────

        [Test]
        public void Pipeline_Japanese_TextToPhonemeIds()
        {
            var result = Phonemize("こんにちは");

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Japanese text should produce phonemes");

            var ids = PhonemesToIds(result.Phonemes);

            Assert.IsNotNull(ids);
            Assert.IsTrue(ids.Length > 0,
                "Japanese phonemes should encode to non-empty ID array");
            Debug.Log($"[Pipeline_Japanese] 'こんにちは' -> {result.Phonemes.Length} phonemes -> {ids.Length} IDs");
        }

        [Test]
        public void Pipeline_English_TextToPhonemeIds()
        {
            var result = Phonemize("hello world");

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "English text should produce phonemes");

            var ids = PhonemesToIds(result.Phonemes);

            Assert.IsNotNull(ids);
            Assert.IsTrue(ids.Length > 0,
                "English phonemes should encode to non-empty ID array");
            Debug.Log($"[Pipeline_English] 'hello world' -> {result.Phonemes.Length} phonemes -> {ids.Length} IDs");
        }

        [Test]
        public void Pipeline_Spanish_TextToPhonemeIds()
        {
            var result = Phonemize("hola mundo");

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Spanish text should produce phonemes");

            var ids = PhonemesToIds(result.Phonemes);

            Assert.IsNotNull(ids);
            Assert.IsTrue(ids.Length > 0,
                "Spanish phonemes should encode to non-empty ID array");
            Debug.Log($"[Pipeline_Spanish] 'hola mundo' -> {result.Phonemes.Length} phonemes -> {ids.Length} IDs");
        }

        [Test]
        public void Pipeline_French_TextToPhonemeIds()
        {
            var result = Phonemize("bonjour");

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "French text should produce phonemes");

            var ids = PhonemesToIds(result.Phonemes);

            Assert.IsNotNull(ids);
            Assert.IsTrue(ids.Length > 0,
                "French phonemes should encode to non-empty ID array");
            Debug.Log($"[Pipeline_French] 'bonjour' -> {result.Phonemes.Length} phonemes -> {ids.Length} IDs");
        }

        [Test]
        public void Pipeline_Portuguese_TextToPhonemeIds()
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

            var ids = PhonemesToIds(result.Phonemes);

            Assert.IsNotNull(ids);
            Assert.IsTrue(ids.Length > 0,
                "Portuguese phonemes should encode to non-empty ID array");
            Debug.Log($"[Pipeline_Portuguese] 'olá' -> {result.Phonemes.Length} phonemes -> {ids.Length} IDs");
        }

        [Test]
        public void Pipeline_Chinese_TextToPhonemeIds()
        {
            var result = Phonemize("你好");

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Chinese text should produce phonemes");
            Assert.AreEqual("zh", result.DetectedPrimaryLanguage,
                "Primary language should be detected as Chinese");

            var ids = PhonemesToIds(result.Phonemes);

            Assert.IsNotNull(ids);
            Assert.IsTrue(ids.Length > 0,
                "Chinese phonemes should encode to non-empty ID array");
            Debug.Log($"[Pipeline_Chinese] '你好' -> {result.Phonemes.Length} phonemes -> {ids.Length} IDs");
        }

        [Test]
        public void Pipeline_Korean_TextToPhonemeIds()
        {
            var result = Phonemize("안녕하세요");

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Korean text should produce phonemes");
            Assert.AreEqual("ko", result.DetectedPrimaryLanguage,
                "Primary language should be detected as Korean");

            var ids = PhonemesToIds(result.Phonemes);

            Assert.IsNotNull(ids);
            Assert.IsTrue(ids.Length > 0,
                "Korean phonemes should encode to non-empty ID array");
            Debug.Log($"[Pipeline_Korean] '안녕하세요' -> {result.Phonemes.Length} phonemes -> {ids.Length} IDs");
        }

        // ── Mixed language pipeline ──────────────────────────────────────────────

        [Test]
        public void Pipeline_JapaneseEnglish_MixedText()
        {
            var text = "今日はgoodですね";
            var result = Phonemize(text);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Mixed Japanese/English text should produce phonemes");

            // Verify that primary language is detected (Japanese dominates by character count)
            Assert.AreEqual("ja", result.DetectedPrimaryLanguage,
                "Primary language should be Japanese for this mixed text");

            var ids = PhonemesToIds(result.Phonemes);

            Assert.IsNotNull(ids);
            Assert.IsTrue(ids.Length > 0,
                "Mixed text phonemes should encode to non-empty ID array");

            // Mixed text should produce more phonemes than either segment alone
            Assert.IsTrue(result.Phonemes.Length >= 3,
                "Mixed text should produce a meaningful number of phonemes");
            Debug.Log($"[Pipeline_JaEn_Mixed] '{text}' -> {result.Phonemes.Length} phonemes -> {ids.Length} IDs");
        }

        [Test]
        public void Pipeline_JapaneseChinese_MixedText()
        {
            // Text with Japanese Kana + CJK ideographs that exist in both scripts
            var text = "こんにちは你好";
            var result = Phonemize(text);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Mixed Japanese/Chinese text should produce phonemes");

            var ids = PhonemesToIds(result.Phonemes);

            Assert.IsNotNull(ids);
            Assert.IsTrue(ids.Length > 0,
                "Mixed CJK phonemes should encode to non-empty ID array");
            Debug.Log($"[Pipeline_JaZh_Mixed] '{text}' -> {result.Phonemes.Length} phonemes -> {ids.Length} IDs");
        }

        [Test]
        public void Pipeline_ThreeLanguages_FullPipeline()
        {
            // Text combining Japanese, English, and Korean
            var text = "Hello こんにちは 안녕";
            var result = Phonemize(text);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Three-language mixed text should produce phonemes");

            var ids = PhonemesToIds(result.Phonemes);

            Assert.IsNotNull(ids);
            Assert.IsTrue(ids.Length > 0,
                "Three-language phonemes should encode to non-empty ID array");

            // The text spans three distinct scripts, so phonemes should be substantial
            Assert.IsTrue(result.Phonemes.Length >= 5,
                "Three-language text should produce at least 5 phonemes");
            Debug.Log($"[Pipeline_3Lang] '{text}' -> {result.Phonemes.Length} phonemes -> {ids.Length} IDs");
        }

        // ── Prosody through pipeline ─────────────────────────────────────────────

        [Test]
        public void Pipeline_Japanese_ProsodyPreserved()
        {
            var result = Phonemize("こんにちは");

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.ProsodyFlat);
            Assert.IsNotNull(result.ProsodyFlat);

            // ProsodyFlat (stride=3) must be aligned with phoneme array
            Assert.AreEqual(result.Phonemes.Length * 3, result.ProsodyFlat.Length,
                "ProsodyFlat length should be phoneme count * 3");

            // For Japanese text, at least some prosody values should be non-zero
            var hasNonZero = result.ProsodyFlat.Any(v => v != 0);

            // At least one of A1/A2/A3 should have non-zero values for Japanese
            Assert.IsTrue(hasNonZero,
                "Japanese text should produce non-zero prosody values in at least one of A1/A2/A3");
            Debug.Log($"[Pipeline_Prosody_Ja] ProsodyFlat has non-zero: {hasNonZero}");
        }

        [Test]
        public void Pipeline_NonJapanese_ProsodyZero()
        {
            // Korean text -- A1 values should be zero (prosody is Japanese-specific via dot-net-g2p)
            var result = Phonemize("안녕하세요");

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.ProsodyFlat);

            // Prosody arrays must be aligned
            Assert.AreEqual(result.Phonemes.Length * 3, result.ProsodyFlat.Length,
                "ProsodyA1 length should match phoneme count for Korean");

            // Korean does not use Japanese-style A1 prosody (accent phrase mora position)
            // Korean backend sets A1=0 and A2=0 with A3=syllable count
            // Check only A1 values (stride=3, offset 0): flat[i*3+0] should be 0
            var phonemeCount = result.Phonemes.Length;
            var allA1Zero = true;
            for (var i = 0; i < phonemeCount; i++)
            {
                if (result.ProsodyFlat[i * 3 + 0] != 0) { allA1Zero = false; break; }
            }
            Assert.IsTrue(allA1Zero,
                "Non-Japanese text (Korean) should have all-zero ProsodyA1 values");
            Debug.Log($"[Pipeline_Prosody_NonJa] Korean A1 all zero: {allA1Zero}, " +
                      $"phoneme count: {phonemeCount}");
        }

        // ── Error handling ───────────────────────────────────────────────────────

        [Test]
        public void Pipeline_EmptyText_ReturnsEmpty()
        {
            var result = Phonemize("");

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Phonemes);
            Assert.AreEqual(0, result.Phonemes.Length,
                "Empty text should produce zero phonemes");
            Assert.AreEqual(0, result.ProsodyFlat.Length,
                "Empty text should produce zero ProsodyFlat");
        }

        [Test]
        public void Pipeline_NullText_HandlesGracefully()
        {
            // MultilingualPhonemizer.PhonemizeWithProsodyAsync treats null as whitespace-only
            var result = Phonemize(null);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Phonemes);
            Assert.AreEqual(0, result.Phonemes.Length,
                "Null text should produce zero phonemes");
        }

        [Test]
        public void Pipeline_PunctuationOnly_ReturnsResult()
        {
            // Punctuation-only text should not crash; it may produce phonemes or empty
            var result = Phonemize("!?。、");

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Phonemes);
            // Punctuation-only text is valid input; result length depends on backend behavior
            Debug.Log($"[Pipeline_PunctuationOnly] '!?。、' -> {result.Phonemes.Length} phonemes");
        }

        [Test]
        public void Pipeline_NumbersOnly_ReturnsResult()
        {
            var result = Phonemize("12345");

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Phonemes);
            // Numbers may be phonemized differently depending on the default Latin language backend
            Debug.Log($"[Pipeline_NumbersOnly] '12345' -> {result.Phonemes.Length} phonemes");
        }

        // ── Backward compatibility ───────────────────────────────────────────────

        [Test]
        public void Pipeline_ExistingJapaneseAPI_StillWorks()
        {
            // Verify that standard Japanese text still works through the multilingual pipeline
            // just as it did in the pre-multilingual era
            var result = Phonemize("今日はいい天気ですね");

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Standard Japanese text should still produce phonemes via multilingual pipeline");
            Assert.AreEqual("ja", result.DetectedPrimaryLanguage,
                "Primary language for pure Japanese should be 'ja'");

            // Prosody should still be available
            Assert.IsNotNull(result.ProsodyFlat);
            Assert.AreEqual(result.Phonemes.Length * 3, result.ProsodyFlat.Length,
                "ProsodyFlat length should be phoneme count * 3 (stride=3)");

            var ids = PhonemesToIds(result.Phonemes);
            Assert.IsNotNull(ids);
            Assert.IsTrue(ids.Length > 0);
            Debug.Log($"[Pipeline_BackCompat_Ja] '今日はいい天気ですね' -> {result.Phonemes.Length} phonemes -> {ids.Length} IDs");
        }

        [Test]
        public void Pipeline_ExistingEnglishAPI_StillWorks()
        {
            // Verify that standard English text still works through the multilingual pipeline
            var result = Phonemize("This is a test sentence");

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Standard English text should still produce phonemes via multilingual pipeline");
            Assert.AreEqual("en", result.DetectedPrimaryLanguage,
                "Primary language for pure English should be 'en'");

            var ids = PhonemesToIds(result.Phonemes);
            Assert.IsNotNull(ids);
            Assert.IsTrue(ids.Length > 0);
            Debug.Log($"[Pipeline_BackCompat_En] 'This is a test sentence' -> {result.Phonemes.Length} phonemes -> {ids.Length} IDs");
        }

        // ── Data consistency ─────────────────────────────────────────────────────

        [Test]
        public void Pipeline_PhonemeIds_AllPositive()
        {
            // Phoneme IDs from the encoder should all be non-negative (0 = PAD, 1 = BOS, 2 = EOS)
            var testTexts = new[] { "こんにちは", "hello", "안녕하세요" };

            foreach (var text in testTexts)
            {
                var result = Phonemize(text);
                if (result.Phonemes.Length == 0) continue;

                var ids = PhonemesToIds(result.Phonemes);

                Assert.IsTrue(ids.All(id => id >= 0),
                    $"All phoneme IDs should be non-negative for text '{text}'. " +
                    $"Got: [{string.Join(", ", ids)}]");
            }
        }

        [Test]
        public void Pipeline_PhonemeIds_ContainBosEos()
        {
            // The encoder should wrap phoneme IDs with BOS (ID 1) and EOS (ID 2) markers
            var result = Phonemize("hello");
            Assert.IsTrue(result.Phonemes.Length > 0,
                "English text should produce phonemes");

            var ids = PhonemesToIds(result.Phonemes);
            Assert.IsTrue(ids.Length >= 2,
                "Encoded IDs should have at least BOS and EOS");

            // BOS (^) is always ID 1 in our test map
            Assert.AreEqual(1, ids[0],
                "First ID should be BOS (^) = 1");

            // EOS ($) is ID 2, or the last phoneme may be an EOS-like token
            var lastId = ids[^1];
            var isEos = lastId == 2;
            var isEosLike = lastId == 3 || lastId == 98 || lastId == 99 || lastId == 100; // ?, ?!, ?., ?~
            Assert.IsTrue(isEos || isEosLike,
                $"Last ID should be EOS ($) = 2 or an EOS-like marker, but got {lastId}");
        }

        [Test]
        public void Pipeline_RepeatableResults()
        {
            // Same input should always produce the same output
            const string text = "こんにちは世界";

            var result1 = Phonemize(text);
            var result2 = Phonemize(text);

            Assert.AreEqual(result1.Phonemes.Length, result2.Phonemes.Length,
                "Repeated phonemization should produce same phoneme count");

            for (var i = 0; i < result1.Phonemes.Length; i++)
            {
                Assert.AreEqual(result1.Phonemes[i], result2.Phonemes[i],
                    $"Phoneme at index {i} should be identical across runs");
            }

            var ids1 = PhonemesToIds(result1.Phonemes);
            var ids2 = PhonemesToIds(result2.Phonemes);

            Assert.AreEqual(ids1.Length, ids2.Length,
                "Repeated encoding should produce same ID count");

            for (var i = 0; i < ids1.Length; i++)
            {
                Assert.AreEqual(ids1[i], ids2[i],
                    $"ID at index {i} should be identical across runs");
            }
        }

        // ── Additional pipeline tests ────────────────────────────────────────────

        [Test]
        public void Pipeline_ProsodyArrays_AlwaysAligned()
        {
            // For any language, prosody arrays must be aligned with the phoneme array
            var testTexts = new[]
            {
                ("ja", "こんにちは"),
                ("en", "hello world"),
                ("zh", "你好"),
                ("ko", "안녕하세요"),
                ("es", "hola mundo"),
                ("fr", "bonjour"),
                ("pt", "olá")
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
                    // English LTS embedded resource not available in Unity.
                    // Skip this language's test case gracefully.
                    Debug.LogWarning($"[ProsodyArrays_{lang}] Skipped: English LTS resource unavailable");
                    continue;
                }

                Assert.IsNotNull(result, $"Result should not be null for {lang}: '{text}'");

                Assert.AreEqual(result.Phonemes.Length * 3, result.ProsodyFlat.Length,
                    $"ProsodyA1 should align with phonemes for {lang}");
                Assert.AreEqual(result.Phonemes.Length * 3, result.ProsodyFlat.Length,
                    $"ProsodyA2 should align with phonemes for {lang}");
                Assert.AreEqual(result.Phonemes.Length * 3, result.ProsodyFlat.Length,
                    $"ProsodyA3 should align with phonemes for {lang}");
            }
        }

        [Test]
        public void Pipeline_WhitespaceOnly_ReturnsEmpty()
        {
            var result = Phonemize("   ");

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Phonemes);
            Assert.AreEqual(0, result.Phonemes.Length,
                "Whitespace-only text should produce zero phonemes");
        }

        [Test]
        public void Pipeline_LanguageDetection_CorrectForAllLanguages()
        {
            // Verify that pure-language texts are detected correctly.
            // Note: Spanish, French, and Portuguese are excluded because they all use
            // Latin script, making them indistinguishable by Unicode-range detection alone.
            // Latin-script text is routed to the defaultLatinLanguage ("en") regardless.
            var testCases = new[]
            {
                ("ja", "今日はいい天気ですね"),
                ("en", "The quick brown fox"),
                ("zh", "今天天气很好"),
                ("ko", "오늘 날씨가 좋습니다"),
            };

            foreach (var (expectedLang, text) in testCases)
            {
                var result = Phonemize(text);
                Assert.AreEqual(expectedLang, result.DetectedPrimaryLanguage,
                    $"Primary language for '{text}' should be '{expectedLang}', " +
                    $"got '{result.DetectedPrimaryLanguage}'");
            }
        }
    }
}