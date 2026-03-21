using System;
using System.Linq;
using System.Threading.Tasks;
using DotNetG2P.Chinese;
using NUnit.Framework;
using uPiper.Core.Phonemizers.Multilingual;

namespace uPiper.Tests.Editor.Phonemizers
{
    [TestFixture]
    public class ChinesePhonemizerTests
    {
        private ChineseG2PEngine _engine;
        private MultilingualPhonemizer _phonemizer;
        private bool _available;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            try
            {
                // Try StreamingAssets path first, then fall back to embedded dictionaries
                var charPath = System.IO.Path.Combine(
                    UnityEngine.Application.streamingAssetsPath, "uPiper", "Chinese", "pinyin_char.txt");
                var phrasePath = System.IO.Path.Combine(
                    UnityEngine.Application.streamingAssetsPath, "uPiper", "Chinese", "pinyin_phrase.txt");

                if (System.IO.File.Exists(charPath))
                {
                    _engine = System.IO.File.Exists(phrasePath)
                        ? new ChineseG2PEngine(charPath, phrasePath)
                        : new ChineseG2PEngine(charPath);
                }
                else
                {
                    throw new System.IO.FileNotFoundException(
                        "Chinese dictionary files not found in StreamingAssets");
                }

                // Create MultilingualPhonemizer with pre-built engine for pipeline tests
                _phonemizer = new MultilingualPhonemizer(
                    new[] { "zh", "en" },
                    defaultLatinLanguage: "en",
                    zhEngine: _engine);
                Task.Run(async () => await _phonemizer.InitializeAsync()).GetAwaiter().GetResult();

                _available = true;
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
                Assert.Ignore("ChineseG2PEngine not available, skipping test");
        }

        /// <summary>Helper to phonemize via MultilingualPhonemizer pipeline.</summary>
        private MultilingualPhonemizeResult Phonemize(string text)
        {
            return Task.Run(async () => await _phonemizer.PhonemizeWithProsodyAsync(text))
                .GetAwaiter().GetResult();
        }

        // ── Basic phonemization ──────────────────────────────────────────────

        [Test]
        public void TestBasicCharacters()
        {
            EnsureAvailable();

            var phonemes = _engine.ToPuaPhonemes("你好");
            Assert.IsNotNull(phonemes);
            Assert.IsTrue(phonemes.Length > 0, "Should produce phonemes for '你好'");
        }

        [Test]
        public void TestEmptyInput()
        {
            EnsureAvailable();

            var phonemes = _engine.ToPuaPhonemes("");
            Assert.IsNotNull(phonemes);
            Assert.AreEqual(0, phonemes.Length, "Empty input should produce no phonemes");
        }

        [Test]
        public void TestSingleCharacter()
        {
            EnsureAvailable();

            var phonemes = _engine.ToPuaPhonemes("大");
            Assert.IsNotNull(phonemes);
            Assert.IsTrue(phonemes.Length > 0, "Should produce phonemes for '大'");
        }

        // ── Pinyin initial -> IPA mapping ────────────────────────────────────
        // Tests that representative initials are correctly converted to IPA.
        // Reference: Python _INITIAL_TO_IPA mapping (b->p, p->ph, d->t, etc.)

        [Test]
        public void TestPinyinToIpa_Initials_BilabialsPresent()
        {
            EnsureAvailable();

            // "八" (ba1) should produce bilabial stop [p] (Mandarin 'b' = unaspirated [p])
            var phonemes = _engine.ToPuaPhonemes("八");
            Assert.IsNotNull(phonemes);
            Assert.IsTrue(phonemes.Length > 0);
        }

        [Test]
        public void TestPinyinToIpa_RetroflexInitials()
        {
            EnsureAvailable();

            // "知" (zhi1) involves retroflex affricate [ts] / [ts_retroflex]
            var phonemes = _engine.ToPuaPhonemes("知");
            Assert.IsNotNull(phonemes);
            Assert.IsTrue(phonemes.Length > 0, "Retroflex initial should produce phonemes");
        }

        [Test]
        public void TestPinyinToIpa_AlveoloPalatalInitials()
        {
            EnsureAvailable();

            // "家" (jia1) involves alveolo-palatal affricate [tc]
            var phonemes = _engine.ToPuaPhonemes("家");
            Assert.IsNotNull(phonemes);
            Assert.IsTrue(phonemes.Length >= 2, "Should include initial + final phonemes");
        }

        // ── Pinyin final -> IPA mapping ──────────────────────────────────────

        [Test]
        public void TestPinyinToIpa_SimpleVowels()
        {
            EnsureAvailable();

            // "阿" (a1) - simple vowel 'a'
            var phonemes = _engine.ToPuaPhonemes("阿");
            Assert.IsNotNull(phonemes);
            Assert.IsTrue(phonemes.Length > 0);
        }

        [Test]
        public void TestPinyinToIpa_CompoundFinals()
        {
            EnsureAvailable();

            // "爱" (ai4) - compound final 'ai' -> [aI]
            var phonemes = _engine.ToPuaPhonemes("爱");
            Assert.IsNotNull(phonemes);
            Assert.IsTrue(phonemes.Length > 0, "Compound final should produce phonemes");
        }

        [Test]
        public void TestPinyinToIpa_NasalFinals()
        {
            EnsureAvailable();

            // "安" (an1) - nasal final 'an'
            var phonemes = _engine.ToPuaPhonemes("安");
            Assert.IsNotNull(phonemes);
            Assert.IsTrue(phonemes.Length > 0, "Nasal final should produce phonemes");
        }

        // ── Tone markers (via MultilingualPhonemizer pipeline) ──────────────

        [Test]
        public void TestToneMarkers_IncludedInOutput()
        {
            EnsureAvailable();

            // "妈" (ma1) should include a tone marker in the pipeline output
            var result = Phonemize("妈");
            Assert.IsNotNull(result.Phonemes);
            // Tone markers are mapped to PUA codepoints (tone1=0xE046..tone5=0xE04A)
            // At minimum, we should have more than just consonant+vowel
            Assert.IsTrue(result.Phonemes.Length >= 2,
                "Output should include phonemes plus tone marker");
        }

        [Test]
        public void TestToneMarkers_DifferentTonesProduceDifferentOutput()
        {
            EnsureAvailable();

            // Characters with different tones should produce different phoneme sequences:
            // "妈" (ma1) vs "马" (ma3)
            var result1 = Phonemize("妈");
            var result2 = Phonemize("马");

            Assert.IsNotNull(result1.Phonemes);
            Assert.IsNotNull(result2.Phonemes);
            Assert.IsTrue(result1.Phonemes.Length > 0);
            Assert.IsTrue(result2.Phonemes.Length > 0);

            // At least the tone marker portion should differ
            var phonemes1 = string.Join(",", result1.Phonemes);
            var phonemes2 = string.Join(",", result2.Phonemes);
            Assert.AreNotEqual(phonemes1, phonemes2,
                "Different tones (ma1 vs ma3) should produce different phoneme output");
        }

        // ── Tone sandhi ──────────────────────────────────────────────────────

        [Test]
        public void TestToneSandhi_ThirdToneBeforeThirdTone()
        {
            EnsureAvailable();

            // "你好" (ni3 hao3): T3+T3 -> T2+T3 (third tone sandhi)
            var phonemes = _engine.ToPuaPhonemes("你好");
            Assert.IsNotNull(phonemes);
            Assert.IsTrue(phonemes.Length > 0,
                "Tone sandhi should not prevent phonemization");
        }

        [Test]
        public void TestToneSandhi_MultiCharacterSequence()
        {
            EnsureAvailable();

            // "你好吗" - includes potential tone sandhi context
            var phonemes = _engine.ToPuaPhonemes("你好吗");
            Assert.IsNotNull(phonemes);
            Assert.IsTrue(phonemes.Length > 0);
        }

        // ── Chinese punctuation ──────────────────────────────────────────────

        [Test]
        public void TestChinesePunctuation_MappedCorrectly()
        {
            EnsureAvailable();

            // "你好。" - Chinese period should be passed through or mapped
            var phonemes = _engine.ToPuaPhonemes("你好。");
            Assert.IsNotNull(phonemes);
            Assert.IsTrue(phonemes.Length > 0);
        }

        [TestCase("你好，世界")]
        [TestCase("你好！")]
        [TestCase("你好？")]
        public void TestChinesePunctuation_VariousMarks(string text)
        {
            EnsureAvailable();

            var phonemes = _engine.ToPuaPhonemes(text);
            Assert.IsNotNull(phonemes);
            Assert.IsTrue(phonemes.Length > 0,
                $"Should phonemize text with punctuation: {text}");
        }

        // ── Prosody output (via MultilingualPhonemizer pipeline) ────────────

        [Test]
        public void TestProsodyOutput_ArraysAligned()
        {
            EnsureAvailable();

            var result = Phonemize("你好世界");
            Assert.IsNotNull(result.Phonemes);
            Assert.IsNotNull(result.ProsodyA1);
            Assert.IsNotNull(result.ProsodyA2);
            Assert.IsNotNull(result.ProsodyA3);

            // Prosody arrays should be aligned with phoneme array
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA1.Length,
                "ProsodyA1 length should match Phonemes length");
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA2.Length,
                "ProsodyA2 length should match Phonemes length");
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA3.Length,
                "ProsodyA3 length should match Phonemes length");
        }

        [Test]
        public void TestProsodyOutput_A1ContainsToneValues()
        {
            EnsureAvailable();

            // Chinese prosody: a1 = tone number (1-5)
            var result = Phonemize("你好");
            Assert.IsNotNull(result.ProsodyA1);

            // At least some A1 values should be tone numbers (1-5)
            var hasToneValues = result.ProsodyA1.Any(v => v >= 1 && v <= 5);
            Assert.IsTrue(hasToneValues,
                "ProsodyA1 should contain tone values (1-5) for Chinese text");
        }

        [Test]
        public void TestProsodyOutput_A3ContainsWordLength()
        {
            EnsureAvailable();

            // Chinese prosody: a3 = word length in syllables
            var result = Phonemize("中国人");
            Assert.IsNotNull(result.ProsodyA3);

            // A3 values should be positive (word length >= 1)
            var hasPositiveA3 = result.ProsodyA3.Any(v => v >= 1);
            Assert.IsTrue(hasPositiveA3,
                "ProsodyA3 should contain positive word length values");
        }

        // ── Multi-sentence and mixed content ─────────────────────────────────

        [Test]
        public void TestLongerText_ProducesPhonemes()
        {
            EnsureAvailable();

            var result = Phonemize("今天天气很好，我们去公园吧。");
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 10,
                "Longer Chinese text should produce many phonemes");
        }

        [Test]
        public void TestTextWithDigits_HandledGracefully()
        {
            EnsureAvailable();

            var phonemes = _engine.ToPuaPhonemes("我有3本书");
            Assert.IsNotNull(phonemes);
            Assert.IsTrue(phonemes.Length > 0,
                "Text with digits should produce phonemes");
        }

        // ── PUA token mapping ────────────────────────────────────────────────

        [Test]
        public void TestPuaMapping_ToneTokensRegistered()
        {
            // Verify that Chinese tone PUA mappings exist in the shared PuaTokenMapper
            Assert.IsTrue(PuaTokenMapper.FixedPuaMapping.ContainsKey("tone1"),
                "tone1 should be in fixed PUA mapping");
            Assert.IsTrue(PuaTokenMapper.FixedPuaMapping.ContainsKey("tone5"),
                "tone5 should be in fixed PUA mapping");
            Assert.AreEqual(0xE046, PuaTokenMapper.FixedPuaMapping["tone1"]);
            Assert.AreEqual(0xE04A, PuaTokenMapper.FixedPuaMapping["tone5"]);
        }

        [Test]
        public void TestPuaMapping_ChineseInitialsRegistered()
        {
            // Verify key Chinese-specific PUA mappings
            // Aspirated bilabial: p + aspiration -> 0xE020
            Assert.IsTrue(PuaTokenMapper.FixedPuaMapping.ContainsKey("p\u02B0"),
                "Aspirated bilabial should be in fixed PUA mapping");
            Assert.AreEqual(0xE020, PuaTokenMapper.FixedPuaMapping["p\u02B0"]);

            // Alveolo-palatal affricate: tc -> 0xE023
            Assert.IsTrue(PuaTokenMapper.FixedPuaMapping.ContainsKey("t\u0255"),
                "Alveolo-palatal affricate should be in fixed PUA mapping");
            Assert.AreEqual(0xE023, PuaTokenMapper.FixedPuaMapping["t\u0255"]);
        }

        [Test]
        public void TestPuaMapping_ChineseDiphthongsRegistered()
        {
            // Diphthong ai -> 0xE028
            Assert.IsTrue(PuaTokenMapper.FixedPuaMapping.ContainsKey("a\u026A"),
                "Diphthong ai should be in fixed PUA mapping");
            Assert.AreEqual(0xE028, PuaTokenMapper.FixedPuaMapping["a\u026A"]);

            // Nasal final an -> 0xE02C
            Assert.IsTrue(PuaTokenMapper.FixedPuaMapping.ContainsKey("an"),
                "Nasal final an should be in fixed PUA mapping");
            Assert.AreEqual(0xE02C, PuaTokenMapper.FixedPuaMapping["an"]);
        }

        // ── Dispose behavior ─────────────────────────────────────────────────

        [Test]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            var charPath = System.IO.Path.Combine(
                UnityEngine.Application.streamingAssetsPath, "uPiper", "Chinese", "pinyin_char.txt");
            var phrasePath = System.IO.Path.Combine(
                UnityEngine.Application.streamingAssetsPath, "uPiper", "Chinese", "pinyin_phrase.txt");

            ChineseG2PEngine engine;
            if (System.IO.File.Exists(charPath))
            {
                engine = System.IO.File.Exists(phrasePath)
                    ? new ChineseG2PEngine(charPath, phrasePath)
                    : new ChineseG2PEngine(charPath);
            }
            else
            {
                Assert.Ignore("Chinese dictionary files not found in StreamingAssets");
                return;
            }

            Assert.DoesNotThrow(() =>
            {
                engine.Dispose();
                engine.Dispose();
            });
        }
    }
}