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

        /// <summary>
        /// A multilingual phoneme ID map covering the basic IPA phonemes shared across languages.
        /// This mirrors a realistic multilingual model's phoneme_id_map with IPA-based entries.
        /// </summary>
        private static Dictionary<string, int> BuildMultilingualPhonemeIdMap()
        {
            return new Dictionary<string, int>
            {
                // Special tokens
                { "_", 0 }, { "^", 1 }, { "$", 2 }, { "?", 3 },

                // Basic vowels
                { "a", 4 }, { "e", 5 }, { "i", 6 }, { "o", 7 }, { "u", 8 },

                // Japanese extended vowels
                { "A", 9 }, { "I", 10 }, { "U", 11 }, { "E", 12 }, { "O", 13 },

                // IPA vowels (multilingual)
                { "\u0259", 14 },  // schwa
                { "\u025B", 15 },  // open-mid front unrounded (French/Portuguese)
                { "\u0254", 16 },  // open-mid back rounded (French)
                { "\u0251", 17 },  // open back unrounded (French)
                { "\u026A", 18 },  // near-close near-front unrounded
                { "\u028A", 19 },  // near-close near-back rounded
                { "\u0268", 20 },  // close central unrounded
                { "\u00F8", 21 },  // close-mid front rounded (French eu)

                // Japanese-specific IPA
                { "\u026F", 22 },  // close back unrounded (Japanese u)
                { "\u0274", 23 },  // small capital N (Japanese moraic nasal)
                { "N", 24 },       // ASCII N (Japanese moraic nasal alternate)
                { "\u0255", 25 },  // alveolo-palatal fricative (Japanese shi)
                { "\u0291", 26 },  // voiced alveolo-palatal fricative (Japanese ji)
                { "q", 27 },       // glottal stop (Japanese sokuon)

                // Basic consonants
                { "b", 28 }, { "d", 29 }, { "f", 30 }, { "g", 31 },
                { "h", 32 }, { "j", 33 }, { "k", 34 }, { "l", 35 },
                { "m", 36 }, { "n", 37 }, { "p", 38 }, { "r", 39 },
                { "s", 40 }, { "t", 41 }, { "v", 42 }, { "w", 43 },
                { "z", 44 }, { "y", 45 },

                // IPA consonants (multilingual)
                { "\u0283", 46 },  // voiceless postalveolar fricative (sh)
                { "\u0292", 47 },  // voiced postalveolar fricative (zh)
                { "\u014B", 48 },  // velar nasal (ng)
                { "\u0272", 49 },  // palatal nasal (Spanish n-tilde, French gn)
                { "\u0279", 50 },  // alveolar approximant (English r)
                { "\u027E", 51 },  // alveolar flap/tap (Spanish r, Portuguese r)
                { "\u0281", 52 },  // uvular fricative (French r)
                { "\u027D", 53 },  // retroflex flap (Japanese ry)
                { "\u0278", 54 },  // voiceless bilabial fricative (Japanese f)
                { "\u00E7", 55 },  // voiceless palatal fricative (Japanese h before i)

                // IPA affricates / palatalized (multilingual)
                { "t\u0255", 56 },     // voiceless alveolo-palatal affricate (ja ch, zh j)
                { "t\u0283", 57 },     // voiceless postalveolar affricate (es/pt ch)
                { "d\u0292", 58 },     // voiced postalveolar affricate (es/pt)
                { "t\u0282", 59 },     // retroflex affricate (zh zh)
                { "ts", 60 },          // alveolar affricate (ja tsu)

                // IPA palatalized consonants
                { "k\u02B2", 61 },     // palatalized k (Japanese ky)
                { "\u0261\u02B2", 62 }, // palatalized g (Japanese gy)
                { "d\u02B2", 63 },     // palatalized d (Japanese dy)
                { "p\u02B2", 64 },     // palatalized p (Japanese py)
                { "b\u02B2", 65 },     // palatalized b (Japanese by)
                { "h\u02B2", 66 },     // palatalized h (Japanese hy)
                { "m\u02B2", 67 },     // palatalized m (Japanese my)

                // Aspiration (Chinese/Korean)
                { "p\u02B0", 68 },     // aspirated p
                { "t\u02B0", 69 },     // aspirated t
                { "k\u02B0", 70 },     // aspirated k
                { "t\u0255\u02B0", 71 }, // aspirated alveolo-palatal affricate (zh q)
                { "t\u0282\u02B0", 72 }, // aspirated retroflex affricate (zh ch)
                { "ts\u02B0", 73 },    // aspirated alveolar affricate (zh c)

                // Chinese tone markers
                { "tone1", 74 }, { "tone2", 75 }, { "tone3", 76 },
                { "tone4", 77 }, { "tone5", 78 },

                // French nasal vowels
                { "\u025B\u0303", 79 },  // nasal epsilon (vin)
                { "\u0251\u0303", 80 },  // nasal alpha (France)
                { "\u0254\u0303", 81 },  // nasal open-o (bon)

                // Korean tense consonants
                { "p\u0348", 82 },         // tense bilabial
                { "t\u0348", 83 },         // tense alveolar
                { "k\u0348", 84 },         // tense velar
                { "s\u0348", 85 },         // tense sibilant
                { "t\u0348\u0255", 86 },   // tense alveolo-palatal affricate

                // Korean unreleased finals
                { "k\u031A", 87 },  // unreleased velar
                { "t\u031A", 88 },  // unreleased alveolar
                { "p\u031A", 89 },  // unreleased bilabial

                // Punctuation / silence
                { " ", 90 },
                { ".", 91 }, { ",", 92 }, { "!", 93 },
                { "-", 94 }, { "'", 95 },

                // Spanish trill
                { "rr", 96 },

                // Shared multilingual
                { "y_vowel", 97 },  // French u, Chinese u-umlaut

                // Extended question markers (PUA-encoded)
                { "\uE016", 98 }, { "\uE017", 99 }, { "\uE018", 100 },

                // Chinese diphthongs / compound finals (PUA)
                { "a\u026A", 101 },  // ai
                { "e\u026A", 102 },  // ei
                { "a\u028A", 103 },  // ao
                { "o\u028A", 104 },  // ou
                { "an", 105 },       // an
                { "\u0259n", 106 },  // en
                { "a\u014B", 107 },  // ang
                { "\u0259\u014B", 108 }, // eng

                // Additional Japanese PUA characters for long vowels
                { "\uE000", 109 },  // a:
                { "\uE001", 110 },  // i:
                { "\uE002", 111 },  // u:
                { "\uE003", 112 },  // e:
                { "\uE004", 113 },  // o:
                { "\uE005", 114 },  // cl
                { "\uE006", 115 },  // ky
                { "\uE00E", 116 },  // ch
                { "\uE00F", 117 },  // ts
                { "\uE010", 118 },  // sh
                { "\uE013", 119 },  // ny
            };
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Initialize MultilingualPhonemizer with all 7 languages
            _phonemizer = new MultilingualPhonemizer(
                LanguageConstants.AllLanguages,
                defaultLatinLanguage: "en");

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
            _encoder = new PhonemeEncoder(config);
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
            var result = Phonemize("olá");

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
            Assert.IsNotNull(result.ProsodyA1);
            Assert.IsNotNull(result.ProsodyA2);
            Assert.IsNotNull(result.ProsodyA3);

            // Prosody arrays must be aligned with phoneme array
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA1.Length,
                "ProsodyA1 length should match phoneme count");
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA2.Length,
                "ProsodyA2 length should match phoneme count");
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA3.Length,
                "ProsodyA3 length should match phoneme count");

            // For Japanese text, at least some prosody values should be non-zero
            var hasNonZeroA1 = result.ProsodyA1.Any(v => v != 0);
            var hasNonZeroA2 = result.ProsodyA2.Any(v => v != 0);
            var hasNonZeroA3 = result.ProsodyA3.Any(v => v != 0);

            // At least one of A1/A2/A3 should have non-zero values for Japanese
            Assert.IsTrue(hasNonZeroA1 || hasNonZeroA2 || hasNonZeroA3,
                "Japanese text should produce non-zero prosody values in at least one of A1/A2/A3");
            Debug.Log($"[Pipeline_Prosody_Ja] A1 has non-zero: {hasNonZeroA1}, " +
                      $"A2 has non-zero: {hasNonZeroA2}, A3 has non-zero: {hasNonZeroA3}");
        }

        [Test]
        public void Pipeline_NonJapanese_ProsodyZero()
        {
            // Korean text -- A1 values should be zero (prosody is Japanese-specific via dot-net-g2p)
            var result = Phonemize("안녕하세요");

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.ProsodyA1);

            // Prosody arrays must be aligned
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA1.Length,
                "ProsodyA1 length should match phoneme count for Korean");

            // Korean does not use Japanese-style A1 prosody (accent phrase mora position)
            // Korean backend sets A1=0 and A2=0 with A3=syllable count
            var allA1Zero = result.ProsodyA1.All(v => v == 0);
            Assert.IsTrue(allA1Zero,
                "Non-Japanese text (Korean) should have all-zero ProsodyA1 values");
            Debug.Log($"[Pipeline_Prosody_NonJa] Korean A1 all zero: {allA1Zero}, " +
                      $"phoneme count: {result.Phonemes.Length}");
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
            Assert.AreEqual(0, result.ProsodyA1.Length,
                "Empty text should produce zero ProsodyA1");
            Assert.AreEqual(0, result.ProsodyA2.Length,
                "Empty text should produce zero ProsodyA2");
            Assert.AreEqual(0, result.ProsodyA3.Length,
                "Empty text should produce zero ProsodyA3");
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
            Assert.IsNotNull(result.ProsodyA1);
            Assert.IsNotNull(result.ProsodyA2);
            Assert.IsNotNull(result.ProsodyA3);
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA1.Length);

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
                var result = Phonemize(text);
                Assert.IsNotNull(result, $"Result should not be null for {lang}: '{text}'");

                Assert.AreEqual(result.Phonemes.Length, result.ProsodyA1.Length,
                    $"ProsodyA1 should align with phonemes for {lang}");
                Assert.AreEqual(result.Phonemes.Length, result.ProsodyA2.Length,
                    $"ProsodyA2 should align with phonemes for {lang}");
                Assert.AreEqual(result.Phonemes.Length, result.ProsodyA3.Length,
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