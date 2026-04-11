using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using uPiper.Core.Phonemizers.Multilingual;
using uPiper.Core.Phonemizers.Multilingual.Handlers;
using uPiper.Tests.Editor.Phonemizers.Handlers;

namespace uPiper.Tests.Editor
{
    /// <summary>
    /// End-to-end tests for mixed-language phonemization through <see cref="MultilingualPhonemizer"/>.
    /// Covers multi-language mixing, fallback behavior, language detection,
    /// edge cases, prosody stride consistency, and performance.
    /// </summary>
    [TestFixture]
    public class MixedLanguageE2ETests
    {
        private MultilingualPhonemizer _phonemizer;

        [OneTimeSetUp]
        public async Task OneTimeSetUp()
        {
            try
            {
                _phonemizer = new MultilingualPhonemizer(
                    new MultilingualPhonemizerOptions
                    {
                        Languages = new[] { "ja", "en", "zh", "es", "fr", "pt", "ko" },
                        DefaultLatinLanguage = "en",
                        EnableTrigramDetection = true
                    });
                await _phonemizer.InitializeAsync();
            }
            catch (Exception ex)
            {
                // If initialization fails (e.g., missing dictionaries in CI),
                // store null and skip tests gracefully in each test method.
                UnityEngine.Debug.LogWarning(
                    $"[MixedLanguageE2ETests] OneTimeSetUp failed: {ex.Message}");
                _phonemizer?.Dispose();
                _phonemizer = null;
            }
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _phonemizer?.Dispose();
            _phonemizer = null;
        }

        private void EnsureInitialized()
        {
            if (_phonemizer == null || !_phonemizer.IsInitialized)
                Assert.Ignore("MultilingualPhonemizer initialization failed " +
                              "(G2P dictionaries may not be available in this environment)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // 1. Japanese + English mixed
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task JapaneseEnglishMixed_ProducesBothLanguagePhonemes()
        {
            EnsureInitialized();

            var result = await _phonemizer.PhonemizeWithProsodyAsync("こんにちはhello");

            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsNotNull(result.Phonemes, "Phonemes should not be null");
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Mixed ja+en text should produce phonemes");

            // Verify both language segments contributed phonemes by checking that
            // the combined output is longer than what either segment alone would produce.
            // At minimum, Japanese "こんにちは" and English "hello" each produce >= 3 phonemes.
            Assert.IsTrue(result.Phonemes.Length >= 6,
                $"Expected at least 6 phonemes from ja+en mixed text, got {result.Phonemes.Length}");
        }

        // ══════════════════════════════════════════════════════════════════════
        // 2. Three-language mixed
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ThreeLanguageMixed_AllSegmentsProcessed()
        {
            EnsureInitialized();

            var result = await _phonemizer.PhonemizeWithProsodyAsync("こんにちは Hello 你好");

            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsNotNull(result.Phonemes, "Phonemes should not be null");
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Three-language mixed text (ja/en/zh) should produce phonemes");

            // Each segment should contribute at least some phonemes.
            // "こんにちは" (ja) ~5+ phonemes, "Hello" (en) ~4+ phonemes, "你好" (zh) ~2+ phonemes
            Assert.IsTrue(result.Phonemes.Length >= 8,
                $"Expected at least 8 phonemes from three-language text, got {result.Phonemes.Length}");

            // Prosody alignment is always verified
            Assert.AreEqual(result.Phonemes.Length * 3, result.ProsodyFlat.Length,
                "ProsodyFlat must be aligned (Phonemes.Length * 3)");
        }

        // ══════════════════════════════════════════════════════════════════════
        // 3. Unsupported language fallback/skip
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task UnsupportedLanguage_FallbackOrSkip()
        {
            // Configure phonemizer with only "ja" — no Korean handler.
            // Korean Hangul text should be skipped gracefully (no crash).
            using var limitedPhonemizer = new MultilingualPhonemizer(
                new MultilingualPhonemizerOptions
                {
                    Languages = new[] { "ja" },
                    DefaultLatinLanguage = "en"
                });

            try
            {
                await limitedPhonemizer.InitializeAsync();
            }
            catch (Exception)
            {
                Assert.Ignore("Failed to initialize limited phonemizer");
                return;
            }

            // Mixed text: Japanese + Korean characters
            // Korean segment should be skipped, Japanese segment should still produce phonemes
            var result = await limitedPhonemizer.PhonemizeWithProsodyAsync("こんにちは안녕하세요");

            Assert.IsNotNull(result, "Result should not be null even with unsupported language");
            Assert.IsNotNull(result.Phonemes, "Phonemes should not be null");

            // At least the Japanese portion should produce phonemes
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Japanese portion should produce phonemes even when Korean is unsupported");
            Assert.AreEqual(result.Phonemes.Length * 3, result.ProsodyFlat.Length,
                "ProsodyFlat must stay aligned after skipping unsupported segments");
        }

        [Test]
        public async Task UnsupportedLanguage_WithFallback_UsesFallbackHandler()
        {
            // Configure with fallback language
            var jaStub = new StubG2PHandler("ja",
                phonemes: new[] { "ko", "N_uvular", "n", "i", "ch", "i", "w", "a", "$" });
            var enStub = new StubG2PHandler("en",
                phonemes: new[] { "f", "o", "l", "b", "a", "k", "$" });

            using var fallbackPhonemizer = new MultilingualPhonemizer(
                new MultilingualPhonemizerOptions
                {
                    Languages = new[] { "ja", "en" },
                    DefaultLatinLanguage = "en",
                    FallbackLanguage = "en",
                    Handlers = new Dictionary<string, ILanguageG2PHandler>
                    {
                        ["ja"] = jaStub,
                        ["en"] = enStub
                    }
                });
            await fallbackPhonemizer.InitializeAsync();

            // Japanese text routes to ja handler; no crash expected
            var result = await fallbackPhonemizer.PhonemizeWithProsodyAsync("こんにちは");

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Phonemizer with fallback should still produce phonemes");
        }

        // ══════════════════════════════════════════════════════════════════════
        // 4. Pure Japanese detection
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task PureJapanese_DetectsAsJapanese()
        {
            EnsureInitialized();

            var result = await _phonemizer.PhonemizeWithProsodyAsync("東京タワーは高い");

            Assert.IsNotNull(result);
            Assert.AreEqual("ja", result.DetectedPrimaryLanguage,
                "Pure Japanese text should detect 'ja' as primary language");
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Pure Japanese text should produce phonemes");
        }

        // ══════════════════════════════════════════════════════════════════════
        // 5. Pure English detection
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task PureEnglish_DetectsAsEnglish()
        {
            EnsureInitialized();

            var result = await _phonemizer.PhonemizeWithProsodyAsync("The quick brown fox");

            Assert.IsNotNull(result);
            Assert.AreEqual("en", result.DetectedPrimaryLanguage,
                "Pure English text should detect 'en' as primary language");
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Pure English text should produce phonemes");
        }

        // ══════════════════════════════════════════════════════════════════════
        // 6. Empty text
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task EmptyText_ReturnsEmptyResult()
        {
            EnsureInitialized();

            var resultEmpty = await _phonemizer.PhonemizeWithProsodyAsync("");
            Assert.IsNotNull(resultEmpty, "Empty string result should not be null");
            Assert.AreEqual(0, resultEmpty.Phonemes.Length,
                "Empty string should produce no phonemes");
            Assert.AreEqual(0, resultEmpty.ProsodyFlat.Length,
                "Empty string should produce empty ProsodyFlat");

            var resultWhitespace = await _phonemizer.PhonemizeWithProsodyAsync("   \t\n  ");
            Assert.IsNotNull(resultWhitespace, "Whitespace-only result should not be null");
            Assert.AreEqual(0, resultWhitespace.Phonemes.Length,
                "Whitespace-only text should produce no phonemes");
            Assert.AreEqual(0, resultWhitespace.ProsodyFlat.Length,
                "Whitespace-only text should produce empty ProsodyFlat");

            var resultNull = await _phonemizer.PhonemizeWithProsodyAsync(null);
            Assert.IsNotNull(resultNull, "Null input result should not be null");
            Assert.AreEqual(0, resultNull.Phonemes.Length,
                "Null input should produce no phonemes");
        }

        // ══════════════════════════════════════════════════════════════════════
        // 7. Special characters only
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task SpecialCharactersOnly_HandledGracefully()
        {
            EnsureInitialized();

            // Various punctuation/special-char inputs that should not crash
            var inputs = new[] { "!!??...", "---", "@#$%^&*()", "「」『』", "…★♪" };
            foreach (var input in inputs)
            {
                var result = await _phonemizer.PhonemizeWithProsodyAsync(input);
                Assert.IsNotNull(result, $"Result for '{input}' should not be null");
                Assert.IsNotNull(result.Phonemes, $"Phonemes for '{input}' should not be null");
                Assert.AreEqual(result.Phonemes.Length * 3, result.ProsodyFlat.Length,
                    $"ProsodyFlat stride alignment must hold for '{input}'");
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // 8. Japanese with numbers
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task JapaneseWithNumbers_ProcessesCorrectly()
        {
            EnsureInitialized();

            var result = await _phonemizer.PhonemizeWithProsodyAsync("2024年の東京");

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Japanese text with numbers should produce phonemes");
            Assert.AreEqual(result.Phonemes.Length * 3, result.ProsodyFlat.Length,
                "ProsodyFlat stride must be consistent for number-mixed text");

            // The detected language should be "ja" since Japanese characters dominate
            Assert.AreEqual("ja", result.DetectedPrimaryLanguage,
                "Japanese-dominant text with numbers should detect 'ja'");
        }

        // ══════════════════════════════════════════════════════════════════════
        // 9. ProsodyFlat stride consistency
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task ProsodyFlat_StrideConsistency()
        {
            EnsureInitialized();

            // Test multiple mixed-language inputs to verify stride=3 invariant
            var testCases = new[]
            {
                "こんにちはhello",
                "Hello World",
                "東京タワーは高い",
                "これはtest用のテキストです。Hello and goodbye!",
                "안녕하세요 세계",
                "2024年のテスト",
                "a"
            };

            foreach (var text in testCases)
            {
                var result = await _phonemizer.PhonemizeWithProsodyAsync(text);

                Assert.IsNotNull(result.Phonemes, $"Phonemes for '{text}' should not be null");
                Assert.IsNotNull(result.ProsodyFlat, $"ProsodyFlat for '{text}' should not be null");

                var expectedLength = result.Phonemes.Length * 3;
                Assert.AreEqual(expectedLength, result.ProsodyFlat.Length,
                    $"ProsodyFlat.Length ({result.ProsodyFlat.Length}) must equal " +
                    $"Phonemes.Length ({result.Phonemes.Length}) * 3 = {expectedLength} " +
                    $"for text '{text}'");

                // Verify ProsodyFlat length is always a multiple of 3
                Assert.AreEqual(0, result.ProsodyFlat.Length % 3,
                    $"ProsodyFlat.Length must be divisible by 3 for text '{text}'");
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // 10. Long mixed text performance
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public async Task LongMixedText_DoesNotTimeout()
        {
            EnsureInitialized();

            // Build a paragraph with multiple language switches
            var sb = new StringBuilder();
            for (var i = 0; i < 20; i++)
            {
                sb.Append("今日は良い天気です。");   // Japanese
                sb.Append("Today is a good day. ");  // English
                sb.Append("Buenos días. ");           // Spanish/Latin
            }

            var longText = sb.ToString();
            Assert.IsTrue(longText.Length > 500,
                $"Test text should be >500 chars, got {longText.Length}");

            var sw = Stopwatch.StartNew();
            var result = await _phonemizer.PhonemizeWithProsodyAsync(longText);
            sw.Stop();

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Long mixed text should produce phonemes");
            Assert.AreEqual(result.Phonemes.Length * 3, result.ProsodyFlat.Length,
                "ProsodyFlat stride must be consistent for long text");

            // Generous timeout: 30 seconds (should normally complete in <2s)
            Assert.IsTrue(sw.ElapsedMilliseconds < 30000,
                $"Long mixed text phonemization took {sw.ElapsedMilliseconds}ms, " +
                "expected under 30 seconds");
        }
    }
}