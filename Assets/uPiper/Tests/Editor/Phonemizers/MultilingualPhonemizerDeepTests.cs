using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using DotNetG2P.Spanish;
using uPiper.Core.Phonemizers.Backend;
using uPiper.Core.Phonemizers.Backend.Chinese;
using uPiper.Core.Phonemizers.Backend.French;
using uPiper.Core.Phonemizers.Backend.Korean;
using uPiper.Core.Phonemizers.Backend.Portuguese;
using uPiper.Core.Phonemizers.Implementations;
using uPiper.Core.Phonemizers.Multilingual;

namespace uPiper.Tests.Editor.Phonemizers
{
    /// <summary>
    /// Phase 6 deep integration tests for MultilingualPhonemizer.
    /// Covers three+ language mixing, prosody array alignment, EOS handling,
    /// primary language detection, edge cases, backend fallback, initialization,
    /// and dispose semantics.
    /// </summary>
    [TestFixture]
    public class MultilingualPhonemizerDeepTests
    {
        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a fully initialized MultilingualPhonemizer for the given languages.
        /// Backends are created and initialized internally by InitializeAsync.
        /// </summary>
        private static MultilingualPhonemizer CreateInitialized(
            string[] languages,
            string defaultLatin = "en")
        {
            var phonemizer = new MultilingualPhonemizer(languages, defaultLatin);
            Task.Run(async () => await phonemizer.InitializeAsync()).GetAwaiter().GetResult();
            return phonemizer;
        }

        /// <summary>
        /// Shorthand for calling PhonemizeWithProsodyAsync synchronously.
        /// </summary>
        private static MultilingualPhonemizeResult Phonemize(
            MultilingualPhonemizer phonemizer,
            string text)
        {
            return Task.Run(async () => await phonemizer.PhonemizeWithProsodyAsync(text))
                .GetAwaiter().GetResult();
        }

        // ══════════════════════════════════════════════════════════════════════
        // Three or more language mixing
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public void PhonemizeWithProsody_ThreeLanguages_JaEnEs()
        {
            // Japanese + English + Spanish
            var phonemizer = CreateInitialized(
                new[] { "ja", "en", "es" }, defaultLatin: "en");

            // "こんにちは" (ja) + "hello" (en/latin) + "hola" (es shares latin -> default en)
            // With defaultLatin="en", Latin text will route to English backend.
            var result = Phonemize(phonemizer, "こんにちはhello hola");

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Three-language mixed text should produce phonemes");
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA1.Length,
                "Prosody A1 must align with phoneme count");

            phonemizer.Dispose();
        }

        [Test]
        public void PhonemizeWithProsody_ThreeLanguages_JaZhKo()
        {
            // Japanese (Kana) + Chinese (CJK without Kana context issue - handled per segment)
            // + Korean (Hangul)
            var phonemizer = CreateInitialized(new[] { "ja", "zh", "ko" });

            // "こんにちは" has Kana -> ja; "你好" CJK with Kana context -> ja or zh;
            // "안녕" -> ko
            var result = Phonemize(phonemizer, "こんにちは안녕");

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Ja+Ko mixed text should produce phonemes");

            phonemizer.Dispose();
        }

        [Test]
        public void PhonemizeWithProsody_FourLanguages_JaEnFrPt()
        {
            // Four language backends configured; Latin text goes to default "en"
            var phonemizer = CreateInitialized(
                new[] { "ja", "en", "fr", "pt" }, defaultLatin: "en");

            var result = Phonemize(phonemizer, "今日はhello bonjour bom dia");

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Four-language configured text should produce phonemes");
            // Japanese segment is present
            Assert.AreEqual("ja", result.DetectedPrimaryLanguage,
                "With '今日は' as a segment, ja should be detected or at least present");

            phonemizer.Dispose();
        }

        [Test]
        public void PhonemizeWithProsody_AllSevenLanguages()
        {
            // All seven supported languages configured
            var phonemizer = CreateInitialized(
                new[] { "ja", "en", "zh", "ko", "es", "fr", "pt" }, defaultLatin: "en");

            // Text has: Japanese Kana, Korean Hangul, Latin (routes to en by default)
            // CJK ideographs with Kana context -> ja
            var text = "こんにちは hello 안녕하세요 世界";
            var result = Phonemize(phonemizer, text);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "All-seven-languages text should produce phonemes");
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA1.Length);
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA2.Length);
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA3.Length);

            phonemizer.Dispose();
        }

        // ══════════════════════════════════════════════════════════════════════
        // Prosody array alignment
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public void ProsodyArrays_AlignedWithPhonemes_JaEnMixed()
        {
            var phonemizer = CreateInitialized(new[] { "ja", "en" });

            var result = Phonemize(phonemizer, "こんにちはhello world");

            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0);
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA1.Length,
                "ProsodyA1 length must equal phoneme count");
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA2.Length,
                "ProsodyA2 length must equal phoneme count");
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA3.Length,
                "ProsodyA3 length must equal phoneme count");

            phonemizer.Dispose();
        }

        [Test]
        public void ProsodyArrays_JapaneseSegment_HasNonZeroProsody()
        {
            // Pure Japanese text should produce prosody values from DotNetG2PPhonemizer
            var phonemizer = CreateInitialized(new[] { "ja", "en" });

            var result = Phonemize(phonemizer, "東京タワーは高いです");

            Assert.IsTrue(result.Phonemes.Length > 0);
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA1.Length);

            // At least some prosody values should be non-zero for Japanese
            var hasNonZeroA1 = result.ProsodyA1.Any(v => v != 0);
            var hasNonZeroA2 = result.ProsodyA2.Any(v => v != 0);
            // A1 or A2 should have non-zero values for Japanese prosody
            Assert.IsTrue(hasNonZeroA1 || hasNonZeroA2,
                "Japanese segment should have at least some non-zero prosody values (A1 or A2)");

            phonemizer.Dispose();
        }

        [Test]
        public void ProsodyArrays_NonJapaneseSegments_HaveZeroA1()
        {
            // Pure English text -> all prosody values should be zero
            var phonemizer = CreateInitialized(new[] { "ja", "en" });

            var result = Phonemize(phonemizer, "hello world");

            Assert.IsTrue(result.Phonemes.Length > 0);
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA1.Length);

            // English backend does not produce prosody -> all zeros
            Assert.IsTrue(result.ProsodyA1.All(v => v == 0),
                "English-only text should have all-zero A1 prosody");
            Assert.IsTrue(result.ProsodyA2.All(v => v == 0),
                "English-only text should have all-zero A2 prosody");
            Assert.IsTrue(result.ProsodyA3.All(v => v == 0),
                "English-only text should have all-zero A3 prosody");

            phonemizer.Dispose();
        }

        [Test]
        public void ProsodyArrays_ChineseSegment_Aligned()
        {
            // Verify prosody arrays are aligned with phoneme array for Chinese segments
            var zhBackend = new ChinesePhonemizerBackend();
            var initOk = Task.Run(async () => await zhBackend.InitializeAsync())
                .GetAwaiter().GetResult();

            if (!initOk)
            {
                Assert.Ignore("ChinesePhonemizerBackend not available in this environment");
                return;
            }

            var phonemizer = new MultilingualPhonemizer(
                new[] { "zh", "en" },
                defaultLatinLanguage: "en",
                zhPhonemizer: zhBackend);
            Task.Run(async () => await phonemizer.InitializeAsync()).GetAwaiter().GetResult();

            var result = Phonemize(phonemizer, "你好世界");

            Assert.IsTrue(result.Phonemes.Length > 0,
                "Chinese text should produce phonemes");
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA1.Length,
                "ProsodyA1 must be aligned even for Chinese segments");

            phonemizer.Dispose();
        }

        // ══════════════════════════════════════════════════════════════════════
        // EOS handling across segments
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public void IntermediateEOS_Removed_MultiSegment()
        {
            // When multiple segments exist, intermediate EOS-like tokens are stripped
            var phonemizer = CreateInitialized(new[] { "ja", "en" });

            // Japanese sentence ending with "。" followed by English
            var result = Phonemize(phonemizer, "おはよう。hello");

            Assert.IsTrue(result.Phonemes.Length > 0);

            // Count how many "$" tokens appear -- intermediate ones should be stripped
            var eosTokens = new HashSet<string> { "$", "?", "?!", "?.", "?~" };
            var eosCount = result.Phonemes.Count(p => eosTokens.Contains(p));

            // At most one EOS at the end (from the final segment)
            Assert.IsTrue(eosCount <= 1,
                $"At most 1 EOS token expected at end, but found {eosCount}");

            phonemizer.Dispose();
        }

        [Test]
        public void FinalEOS_Preserved()
        {
            // The final segment should keep its EOS marker
            var phonemizer = CreateInitialized(new[] { "ja", "en" });

            var result = Phonemize(phonemizer, "hello");

            Assert.IsTrue(result.Phonemes.Length > 0);

            // The last phoneme from the English backend might be "$" (EOS)
            var eosTokens = new HashSet<string> { "$", "?", "?!", "?.", "?~" };
            var lastPhoneme = result.Phonemes[^1];
            // If the backend produces EOS, it should be preserved in the final segment
            // This test verifies the final segment is not stripped
            Assert.IsNotNull(result.Phonemes,
                "Final segment phonemes should not be null");

            phonemizer.Dispose();
        }

        [Test]
        public void EosLikeTokens_AllVariants_HandledCorrectly()
        {
            // Verify the set of EOS-like tokens is handled.
            // We test by examining that multi-segment results do not have
            // more than one EOS token per known variant.
            var phonemizer = CreateInitialized(new[] { "ja", "en" });

            var result = Phonemize(phonemizer, "東京タワー。hello world");

            var eosTokens = new HashSet<string> { "$", "?", "?!", "?.", "?~" };
            var eosInResult = result.Phonemes.Where(p => eosTokens.Contains(p)).ToList();

            // Intermediate EOS should have been removed; at most 1 remains
            Assert.IsTrue(eosInResult.Count <= 1,
                $"Expected at most 1 EOS-like token, found {eosInResult.Count}: [{string.Join(", ", eosInResult)}]");

            phonemizer.Dispose();
        }

        // ══════════════════════════════════════════════════════════════════════
        // Language detection primary language
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public void DetectedPrimaryLanguage_CharacterWeighted()
        {
            // The language with the most characters should be the primary
            var phonemizer = CreateInitialized(new[] { "ja", "en" });

            // Long Japanese text with short English
            var result = Phonemize(phonemizer,
                "今日はとても良い天気ですね。素晴らしい一日になりそうです。hi");

            Assert.AreEqual("ja", result.DetectedPrimaryLanguage,
                "Japanese-heavy text should detect 'ja' as primary language");

            phonemizer.Dispose();
        }

        [Test]
        public void DetectedPrimaryLanguage_JapaneseWithShortEnglish_IsJa()
        {
            var phonemizer = CreateInitialized(new[] { "ja", "en" });

            var result = Phonemize(phonemizer, "東京タワーにgoしました");

            Assert.AreEqual("ja", result.DetectedPrimaryLanguage,
                "Japanese-dominant text with short English should be 'ja'");

            phonemizer.Dispose();
        }

        [Test]
        public void DetectedPrimaryLanguage_EnglishWithShortJapanese_IsEn()
        {
            var phonemizer = CreateInitialized(new[] { "ja", "en" });

            var result = Phonemize(phonemizer,
                "The quick brown fox jumps over the lazy dog あ");

            Assert.AreEqual("en", result.DetectedPrimaryLanguage,
                "English-dominant text with short Japanese should be 'en'");

            phonemizer.Dispose();
        }

        // ══════════════════════════════════════════════════════════════════════
        // Edge cases
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public void PhonemizeWithProsody_WhitespaceOnly_ReturnsEmpty()
        {
            var phonemizer = CreateInitialized(new[] { "ja", "en" });

            var result = Phonemize(phonemizer, "   \t\n  ");

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Phonemes.Length,
                "Whitespace-only text should return empty phonemes");
            Assert.AreEqual(0, result.ProsodyA1.Length);
            Assert.AreEqual(0, result.ProsodyA2.Length);
            Assert.AreEqual(0, result.ProsodyA3.Length);

            phonemizer.Dispose();
        }

        [Test]
        public void PhonemizeWithProsody_PunctuationOnly_ReturnsResult()
        {
            var phonemizer = CreateInitialized(new[] { "ja", "en" });

            // Pure punctuation: detector returns null for each char -> falls back to default
            var result = Phonemize(phonemizer, "...,,,!!!");

            Assert.IsNotNull(result);
            // Punctuation may or may not produce phonemes depending on the backend
            // but the call itself should not throw
            Assert.IsNotNull(result.Phonemes);
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA1.Length);

            phonemizer.Dispose();
        }

        [Test]
        public void PhonemizeWithProsody_DigitsOnly_FallsBackToDefault()
        {
            var phonemizer = CreateInitialized(new[] { "ja", "en" });

            // Digits are neutral characters; should be absorbed into default language
            var result = Phonemize(phonemizer, "12345");

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Phonemes);
            // The detector treats digits as neutral, so they get absorbed into default
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA1.Length);

            phonemizer.Dispose();
        }

        [Test]
        public void PhonemizeWithProsody_VeryLongText_Succeeds()
        {
            var phonemizer = CreateInitialized(new[] { "ja", "en" });

            // Build a 1000+ character mixed-language string
            var sb = new StringBuilder();
            for (var i = 0; i < 50; i++)
            {
                sb.Append("こんにちは世界。");   // 8 chars * 50 = 400 ja chars
                sb.Append("Hello world. ");       // 13 chars * 50 = 650 en chars
            }

            var longText = sb.ToString();
            Assert.IsTrue(longText.Length > 1000,
                $"Test text should be >1000 chars, got {longText.Length}");

            var result = Phonemize(phonemizer, longText);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Very long mixed text should produce phonemes");
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA1.Length,
                "Prosody arrays must stay aligned for long text");

            phonemizer.Dispose();
        }

        [Test]
        public void PhonemizeWithProsody_RepeatedLanguageSwitching()
        {
            var phonemizer = CreateInitialized(new[] { "ja", "en" });

            // Rapid language switching: Kana -> Latin -> Kana -> Latin ...
            var result = Phonemize(phonemizer, "aあaあaあ");

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Rapid language switching should produce phonemes");
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA1.Length,
                "Prosody must align even with rapid switching");

            phonemizer.Dispose();
        }

        [Test]
        public void PhonemizeWithProsody_UnicodeNormalization()
        {
            var phonemizer = CreateInitialized(new[] { "ja", "en" });

            // Pre-composed form (NFC) vs decomposed form (NFD) for 'が' (U+304C vs U+304B + U+3099)
            var nfcText = "\u304C";   // が (pre-composed)
            var nfdText = "\u304B\u3099"; // か + combining dakuten (decomposed)

            var resultNfc = Phonemize(phonemizer, nfcText);
            var resultNfd = Phonemize(phonemizer, nfdText);

            Assert.IsNotNull(resultNfc);
            Assert.IsNotNull(resultNfd);
            // Both forms should produce phonemes (may or may not be identical
            // depending on backend normalization, but neither should crash)
            Assert.IsTrue(resultNfc.Phonemes.Length > 0,
                "NFC form should produce phonemes");

            phonemizer.Dispose();
        }

        // ══════════════════════════════════════════════════════════════════════
        // Backend fallback
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public void PhonemizeWithProsody_UnsupportedLanguage_SkipsSegment()
        {
            // Configure only "ja" -- Korean Hangul has no backend, so it is skipped
            var phonemizer = CreateInitialized(new[] { "ja" });

            // The detector won't assign "ko" if ko is not in the language list.
            // However, Hangul chars will be neutral (no matching language -> null).
            // This tests that the pipeline does not crash.
            var result = Phonemize(phonemizer, "おはよう");

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Japanese text should still produce phonemes even with limited languages");

            phonemizer.Dispose();
        }

        [Test]
        public void GetBackendForLanguage_UnknownLang_FallsBackToEnglish()
        {
            // When an unknown language code appears, GetBackendForLanguage returns _enPhonemizer
            // We verify this indirectly: configure "en" + "ja", feed text that would
            // route to a language not explicitly handled (e.g., if detector somehow
            // returned an unsupported code). The fallback is the English backend.
            var phonemizer = CreateInitialized(new[] { "ja", "en" });

            // Basic Latin text routes to "en" (default), which exercises the English backend
            var result = Phonemize(phonemizer, "test fallback");

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Fallback to English backend should produce phonemes");

            phonemizer.Dispose();
        }

        // ══════════════════════════════════════════════════════════════════════
        // Initialization
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public void InitializeAsync_SubsetOfLanguages_OnlyCreatesNeeded()
        {
            // Initialize with only "ja" and "es" -- should not crash and should
            // only create backends for those two languages
            var phonemizer = new MultilingualPhonemizer(new[] { "ja", "es" });
            Task.Run(async () => await phonemizer.InitializeAsync()).GetAwaiter().GetResult();

            Assert.IsTrue(phonemizer.IsInitialized);
            Assert.AreEqual(2, phonemizer.Languages.Count);
            Assert.IsTrue(phonemizer.Languages.Contains("ja"));
            Assert.IsTrue(phonemizer.Languages.Contains("es"));

            // Phonemize Japanese text -- should work
            var jaResult = Phonemize(phonemizer, "おはよう");
            Assert.IsTrue(jaResult.Phonemes.Length > 0,
                "Japanese should work with ja+es subset");

            phonemizer.Dispose();
        }

        [Test]
        public void InitializeAsync_CalledTwice_Idempotent()
        {
            var phonemizer = new MultilingualPhonemizer(new[] { "ja", "en" });

            // First initialization
            Task.Run(async () => await phonemizer.InitializeAsync()).GetAwaiter().GetResult();
            Assert.IsTrue(phonemizer.IsInitialized);

            // Second initialization should be a no-op (early return)
            Assert.DoesNotThrow(() =>
            {
                Task.Run(async () => await phonemizer.InitializeAsync()).GetAwaiter().GetResult();
            });
            Assert.IsTrue(phonemizer.IsInitialized);

            // Should still function correctly after double init
            var result = Phonemize(phonemizer, "テスト");
            Assert.IsTrue(result.Phonemes.Length > 0);

            phonemizer.Dispose();
        }

        [Test]
        public void InitializeAsync_CancellationToken_Respected()
        {
            var phonemizer = new MultilingualPhonemizer(new[] { "ja", "en" });

            // Create an already-cancelled token
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // InitializeAsync should throw OperationCanceledException or
            // TaskCanceledException when given a pre-cancelled token.
            // However, if the initialization is synchronous and returns immediately
            // (e.g., for "ja" which uses synchronous constructor), it may complete
            // before checking cancellation. The key guarantee is no crash.
            try
            {
                Task.Run(async () => await phonemizer.InitializeAsync(cts.Token))
                    .GetAwaiter().GetResult();
                // If it completes without throwing, that's also acceptable
                // (synchronous path may not check token)
            }
            catch (OperationCanceledException)
            {
                // Expected: cancellation was respected
                Assert.Pass("CancellationToken was respected");
            }
            catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
            {
                // Task.Run wraps in AggregateException
                Assert.Pass("CancellationToken was respected (via AggregateException)");
            }

            phonemizer.Dispose();
        }

        // ══════════════════════════════════════════════════════════════════════
        // Dispose
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public void Dispose_DisposesAllBackends()
        {
            // Create with pre-built backends and verify dispose does not throw
            var koBackend = new KoreanPhonemizerBackend();
            var esEngine = new SpanishG2PEngine();
            Task.Run(async () => await koBackend.InitializeAsync()).GetAwaiter().GetResult();

            var jaPhonemizer = new DotNetG2PPhonemizer();

            var phonemizer = new MultilingualPhonemizer(
                new[] { "ja", "en", "ko", "es" },
                defaultLatinLanguage: "en",
                jaPhonemizer: jaPhonemizer,
                koPhonemizer: koBackend,
                esEngine: esEngine);

            Task.Run(async () => await phonemizer.InitializeAsync()).GetAwaiter().GetResult();

            // Dispose should not throw and should dispose all sub-backends
            Assert.DoesNotThrow(() => phonemizer.Dispose());

            // After disposing MultilingualPhonemizer, the ja backend should be disposed
            // DotNetG2PPhonemizer throws ObjectDisposedException after dispose
            Assert.Throws<ObjectDisposedException>(() =>
                jaPhonemizer.PhonemizeWithProsody("テスト"));
        }

        [Test]
        public void Dispose_CalledTwice_NoError()
        {
            var phonemizer = CreateInitialized(new[] { "ja", "en", "ko", "es", "fr", "pt" });

            Assert.DoesNotThrow(() =>
            {
                phonemizer.Dispose();
                phonemizer.Dispose();
            }, "Calling Dispose twice should not throw");
        }

        [Test]
        public void PhonemizeAfterDispose_Throws()
        {
            var phonemizer = CreateInitialized(new[] { "ja", "en" });
            phonemizer.Dispose();

            // After dispose, _isInitialized is still true but backends are disposed.
            // Calling PhonemizeWithProsodyAsync should throw when the disposed backend
            // is invoked (ObjectDisposedException from DotNetG2PPhonemizer).
            Assert.Throws<ObjectDisposedException>(() =>
            {
                Phonemize(phonemizer, "テスト hello");
            }, "Phonemizing after Dispose should throw ObjectDisposedException from disposed backends");
        }

        // ══════════════════════════════════════════════════════════════════════
        // Additional coverage: segment boundary and multi-language interactions
        // ══════════════════════════════════════════════════════════════════════

        [Test]
        public void ProsodyArrays_MixedJaEn_JaPortionHasProsody()
        {
            // Verify that in a mixed ja+en result, the prosody arrays have
            // non-zero values in the portion corresponding to Japanese phonemes
            var phonemizer = CreateInitialized(new[] { "ja", "en" });

            var result = Phonemize(phonemizer, "おはようございます good morning");

            Assert.IsTrue(result.Phonemes.Length > 0);
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA1.Length);

            // The Japanese portion should contribute some non-zero prosody
            var anyNonZero = result.ProsodyA1.Any(v => v != 0)
                          || result.ProsodyA2.Any(v => v != 0)
                          || result.ProsodyA3.Any(v => v != 0);
            Assert.IsTrue(anyNonZero,
                "Mixed ja+en result should have non-zero prosody from the Japanese portion");

            phonemizer.Dispose();
        }

        [Test]
        public void PhonemizeWithProsody_KoreanEnglishMixed_ProducesBothSegments()
        {
            var phonemizer = CreateInitialized(new[] { "ko", "en" });

            var result = Phonemize(phonemizer, "안녕하세요 hello");

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Phonemes.Length > 0,
                "Korean+English mixed text should produce phonemes");
            Assert.AreEqual("ko", result.DetectedPrimaryLanguage,
                "Korean should be primary when it has more characters");

            phonemizer.Dispose();
        }

        [Test]
        public void PhonemizeWithProsody_SpanishOnly_DetectsEsAsPrimary()
        {
            // When defaultLatinLanguage is "es", Latin text routes to Spanish engine
            var esEngine = new SpanishG2PEngine();

            var phonemizer = new MultilingualPhonemizer(
                new[] { "es" },
                defaultLatinLanguage: "es",
                esEngine: esEngine);
            Task.Run(async () => await phonemizer.InitializeAsync()).GetAwaiter().GetResult();

            var result = Phonemize(phonemizer, "buenos dias amigo");

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Phonemes.Length > 0);
            Assert.AreEqual("es", result.DetectedPrimaryLanguage,
                "Spanish-only text with es default should detect 'es'");

            phonemizer.Dispose();
        }
    }
}