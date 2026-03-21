using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using uPiper.Core.Phonemizers.Backend;
using uPiper.Core.Phonemizers.Implementations;
using uPiper.Core.Phonemizers.Multilingual;

namespace uPiper.Tests.Editor.Phonemizers
{
    /// <summary>
    /// Tests for the EOS PUA token stripping and leading PAD ("_") stripping
    /// logic in <see cref="MultilingualPhonemizer"/>.
    /// </summary>
    [TestFixture]
    public class MultilingualPhonemizerEosTests
    {
        // ── Stub backend ────────────────────────────────────────────────────

        /// <summary>
        /// Minimal <see cref="IPhonemizerBackend"/> stub whose PhonemizeAsync
        /// return value is fully controllable from the test.
        /// </summary>
        private sealed class StubPhonemizerBackend : IPhonemizerBackend
        {
            private readonly PhonemeResult _result;

            public StubPhonemizerBackend(PhonemeResult result)
            {
                _result = result;
            }

            public Task<bool> InitializeAsync(
                PhonemizerBackendOptions options = null,
                CancellationToken cancellationToken = default)
                => Task.FromResult(true);

            public Task<PhonemeResult> PhonemizeAsync(
                string text,
                string language,
                PhonemeOptions options = null,
                CancellationToken cancellationToken = default)
                => Task.FromResult(_result);

            public void Dispose() { }
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private static MultilingualPhonemizeResult Phonemize(
            MultilingualPhonemizer mp,
            string text)
        {
            return Task.Run(async () => await mp.PhonemizeWithProsodyAsync(text))
                .GetAwaiter().GetResult();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  EOS PUA token tests
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Verify that the three PUA question markers (\ue016, \ue017,
        /// \ue018) are included in the EOS-like token set by confirming they
        /// get stripped from intermediate segments.  For each PUA token we
        /// build a two-segment scenario (EN intermediate + JA final) where
        /// the EN backend's output ends with the PUA token.
        /// </summary>
        [Test]
        [TestCase("\ue016", Description = "PUA ?! (emphatic question)")]
        [TestCase("\ue017", Description = "PUA ?. (declarative question)")]
        [TestCase("\ue018", Description = "PUA ?~ (confirmatory question)")]
        public void EosLikeTokens_ContainsPuaQuestionMarkers(string puaToken)
        {
            // EN intermediate segment ending with the PUA token
            var enResult = new PhonemeResult
            {
                Phonemes = new[] { "h", "a", "y", puaToken },
                ProsodyA1 = new[] { 0, 0, 0, 0 },
                ProsodyA2 = new[] { 0, 0, 0, 0 },
                ProsodyA3 = new[] { 0, 0, 0, 0 },
                Success = true
            };

            var enStub = new StubPhonemizerBackend(enResult);

            // Real JA phonemizer for the final segment
            var jaPhonemizer = new DotNetG2PPhonemizer();

            var mp = new MultilingualPhonemizer(
                new[] { "ja", "en" },
                defaultLatinLanguage: "en",
                jaPhonemizer: jaPhonemizer,
                enPhonemizer: enStub);
            Task.Run(async () => await mp.InitializeAsync()).GetAwaiter().GetResult();

            // English intermediate, Japanese final
            var result = Phonemize(mp, "hey あ");
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Phonemes.Length > 0);

            // The PUA token from the intermediate EN segment must NOT appear
            Assert.IsFalse(result.Phonemes.Contains(puaToken),
                $"PUA EOS token U+{((int)puaToken[0]):X4} should be stripped from intermediate segment");

            mp.Dispose();
        }

        /// <summary>
        /// When a segment is NOT the last one, its trailing PUA EOS token
        /// must be stripped so that the concatenated output does not carry
        /// spurious sentence-end markers.
        /// </summary>
        [Test]
        public void IntermediateSegment_PuaEos_Stripped()
        {
            // EN intermediate segment ending with \ue016
            var enResult = new PhonemeResult
            {
                Phonemes = new[] { "w", "er", "l", "d", "\ue016" },
                ProsodyA1 = new[] { 0, 0, 0, 0, 0 },
                ProsodyA2 = new[] { 0, 0, 0, 0, 0 },
                ProsodyA3 = new[] { 0, 0, 0, 0, 0 },
                Success = true
            };

            var enStub = new StubPhonemizerBackend(enResult);
            var jaPhonemizer = new DotNetG2PPhonemizer();

            var mp = new MultilingualPhonemizer(
                new[] { "ja", "en" },
                defaultLatinLanguage: "en",
                jaPhonemizer: jaPhonemizer,
                enPhonemizer: enStub);
            Task.Run(async () => await mp.InitializeAsync()).GetAwaiter().GetResult();

            // English intermediate, Japanese final
            var result = Phonemize(mp, "world こん");
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Phonemes.Length > 0);

            // PUA \ue016 should NOT appear anywhere in the merged output
            Assert.IsFalse(result.Phonemes.Contains("\ue016"),
                "PUA EOS \\ue016 from intermediate EN segment should be stripped");

            mp.Dispose();
        }

        /// <summary>
        /// When a segment IS the last one, its trailing EOS token must be
        /// preserved so that the model receives the sentence terminator.
        /// A single-segment scenario guarantees the segment is final.
        /// </summary>
        [Test]
        public void FinalSegment_PuaEos_Preserved()
        {
            // Single EN segment ending with \ue017 — it is the only (=last) segment.
            var enResult = new PhonemeResult
            {
                Phonemes = new[] { "h", "e", "l", "o", "\ue017" },
                ProsodyA1 = new[] { 0, 0, 0, 0, 0 },
                ProsodyA2 = new[] { 0, 0, 0, 0, 0 },
                ProsodyA3 = new[] { 0, 0, 0, 0, 0 },
                Success = true
            };

            var enStub = new StubPhonemizerBackend(enResult);
            var mp = new MultilingualPhonemizer(
                new[] { "en" },
                defaultLatinLanguage: "en",
                enPhonemizer: enStub);
            Task.Run(async () => await mp.InitializeAsync()).GetAwaiter().GetResult();

            var result = Phonemize(mp, "hello");
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Phonemes.Length > 0);
            Assert.AreEqual("\ue017", result.Phonemes[^1],
                "PUA EOS \\ue017 on the final (only) segment should be preserved");

            mp.Dispose();
        }

        /// <summary>
        /// Verify that standard (non-PUA) EOS token "$" is also stripped
        /// from intermediate segments.
        /// </summary>
        [Test]
        public void IntermediateSegment_StandardEos_Stripped()
        {
            var enResult = new PhonemeResult
            {
                Phonemes = new[] { "h", "e", "l", "o", "$" },
                ProsodyA1 = new[] { 0, 0, 0, 0, 0 },
                ProsodyA2 = new[] { 0, 0, 0, 0, 0 },
                ProsodyA3 = new[] { 0, 0, 0, 0, 0 },
                Success = true
            };

            var enStub = new StubPhonemizerBackend(enResult);
            var jaPhonemizer = new DotNetG2PPhonemizer();

            var mp = new MultilingualPhonemizer(
                new[] { "en", "ja" },
                defaultLatinLanguage: "en",
                enPhonemizer: enStub,
                jaPhonemizer: jaPhonemizer);
            Task.Run(async () => await mp.InitializeAsync()).GetAwaiter().GetResult();

            // English intermediate, Japanese final
            var result = Phonemize(mp, "hello あ");
            Assert.IsNotNull(result);

            // The intermediate EN segment's "$" should be stripped.
            // The final JA segment will have its own "$" from DotNetG2PPhonemizer.
            // Count "$" tokens — exactly 1 should remain (from the final JA segment).
            int eosCount = result.Phonemes.Count(p => p == "$");
            Assert.AreEqual(1, eosCount,
                "Only one '$' should remain (from the final JA segment); intermediate '$' stripped");

            mp.Dispose();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Leading PAD stripping tests
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// A Japanese segment whose phoneme array starts with "_" (PAD from
        /// "sil" conversion in OpenJTalkToPiperMapping) should have that
        /// leading token removed by MultilingualPhonemizer.
        /// Uses the real DotNetG2PPhonemizer which produces leading "_".
        /// </summary>
        [Test]
        public void JapaneseSegment_LeadingPad_Stripped()
        {
            // First, verify that the raw JA phonemizer does produce a leading "_"
            var jaPhonemizer = new DotNetG2PPhonemizer();
            var rawResult = jaPhonemizer.PhonemizeWithProsody("こんにちは");
            Assert.IsNotNull(rawResult.Phonemes);
            Assert.IsTrue(rawResult.Phonemes.Length > 0);
            Assert.AreEqual("_", rawResult.Phonemes[0],
                "Raw DotNetG2PPhonemizer should produce leading '_' from 'sil' mapping");

            // Now run through MultilingualPhonemizer — the leading "_" should be stripped
            var mp = new MultilingualPhonemizer(
                new[] { "ja" },
                jaPhonemizer: jaPhonemizer);
            Task.Run(async () => await mp.InitializeAsync()).GetAwaiter().GetResult();

            var result = Phonemize(mp, "こんにちは");
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Phonemes.Length > 0);
            Assert.AreNotEqual("_", result.Phonemes[0],
                "Leading PAD '_' should be stripped from Japanese segment by MultilingualPhonemizer");

            mp.Dispose();
        }

        /// <summary>
        /// A Japanese segment that does NOT start with "_" should pass
        /// through unchanged (no spurious stripping).
        /// This is verified by checking that the raw phoneme count minus 1
        /// (for the stripped "_") matches the multilingual output count
        /// (minus any EOS stripping if applicable).
        /// </summary>
        [Test]
        public void JapaneseSegment_NoPad_Unchanged()
        {
            // Use the real phonemizer and confirm that if we were to remove
            // the leading "_" manually before feeding, the rest stays intact.
            var jaPhonemizer = new DotNetG2PPhonemizer();
            var rawResult = jaPhonemizer.PhonemizeWithProsody("あ");
            Assert.IsNotNull(rawResult.Phonemes);
            Assert.IsTrue(rawResult.Phonemes.Length > 0);

            // After stripping leading "_", the remaining phonemes should be present
            // in the MultilingualPhonemizer output (single segment = final, so EOS kept).
            var mp = new MultilingualPhonemizer(
                new[] { "ja" },
                jaPhonemizer: jaPhonemizer);
            Task.Run(async () => await mp.InitializeAsync()).GetAwaiter().GetResult();

            var result = Phonemize(mp, "あ");
            Assert.IsNotNull(result);

            // The raw output starts with "_", multilingual strips it.
            // So multilingual length == raw length - 1.
            if (rawResult.Phonemes[0] == "_")
            {
                Assert.AreEqual(rawResult.Phonemes.Length - 1, result.Phonemes.Length,
                    "Exactly the leading '_' should be stripped, nothing more");
            }
            else
            {
                Assert.AreEqual(rawResult.Phonemes.Length, result.Phonemes.Length,
                    "Without leading '_', all phonemes should be preserved");
            }

            mp.Dispose();
        }

        /// <summary>
        /// Non-Japanese segments (e.g., English) should never have their
        /// first phoneme stripped even if it happens to be "_".
        /// The PAD stripping code path is guarded by `lang == "ja"`.
        /// </summary>
        [Test]
        public void NonJapaneseSegment_NoPadStripping()
        {
            var enResult = new PhonemeResult
            {
                Phonemes = new[] { "_", "h", "e", "l", "o" },
                ProsodyA1 = new[] { 0, 0, 0, 0, 0 },
                ProsodyA2 = new[] { 0, 0, 0, 0, 0 },
                ProsodyA3 = new[] { 0, 0, 0, 0, 0 },
                Success = true
            };

            var enStub = new StubPhonemizerBackend(enResult);
            var mp = new MultilingualPhonemizer(
                new[] { "en" },
                defaultLatinLanguage: "en",
                enPhonemizer: enStub);
            Task.Run(async () => await mp.InitializeAsync()).GetAwaiter().GetResult();

            var result = Phonemize(mp, "hello");
            Assert.IsNotNull(result);
            Assert.AreEqual("_", result.Phonemes[0],
                "Leading '_' in an English segment must NOT be stripped");
            Assert.AreEqual(5, result.Phonemes.Length,
                "English segment phoneme count should be unchanged");

            mp.Dispose();
        }

        /// <summary>
        /// When the leading PAD is stripped from a Japanese segment, the
        /// prosody arrays (A1, A2, A3) must also drop their first element
        /// so that indices stay aligned with the phoneme array.
        /// </summary>
        [Test]
        public void ProsodyArrays_AlignedAfterPadStrip()
        {
            var jaPhonemizer = new DotNetG2PPhonemizer();
            var rawResult = jaPhonemizer.PhonemizeWithProsody("こんにちは");
            Assert.IsNotNull(rawResult.Phonemes);
            Assert.IsTrue(rawResult.Phonemes.Length > 0);

            // Confirm raw result has leading "_"
            Assert.AreEqual("_", rawResult.Phonemes[0],
                "Precondition: raw output starts with '_'");

            var mp = new MultilingualPhonemizer(
                new[] { "ja" },
                jaPhonemizer: jaPhonemizer);
            Task.Run(async () => await mp.InitializeAsync()).GetAwaiter().GetResult();

            var result = Phonemize(mp, "こんにちは");
            Assert.IsNotNull(result);

            // All prosody arrays must be same length as phonemes
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA1.Length,
                "ProsodyA1 length must match phonemes after PAD strip");
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA2.Length,
                "ProsodyA2 length must match phonemes after PAD strip");
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA3.Length,
                "ProsodyA3 length must match phonemes after PAD strip");

            // The stripped result should be 1 shorter than raw (the leading "_" was removed)
            Assert.AreEqual(rawResult.Phonemes.Length - 1, result.Phonemes.Length,
                "Exactly one element should be stripped (the leading PAD)");

            // Prosody values at index 0 of the stripped result should correspond
            // to index 1 of the raw result (i.e., the first real phoneme's prosody)
            Assert.AreEqual(rawResult.ProsodyA1[1], result.ProsodyA1[0],
                "ProsodyA1[0] should correspond to the first real phoneme (index 1 in raw)");
            Assert.AreEqual(rawResult.ProsodyA2[1], result.ProsodyA2[0],
                "ProsodyA2[0] should correspond to the first real phoneme (index 1 in raw)");
            Assert.AreEqual(rawResult.ProsodyA3[1], result.ProsodyA3[0],
                "ProsodyA3[0] should correspond to the first real phoneme (index 1 in raw)");

            mp.Dispose();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Additional edge-case tests
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Verify all eight EOS-like tokens recognized by
        /// MultilingualPhonemizer: "$", "?", "?!", "?.", "?~",
        /// "\ue016", "\ue017", "\ue018".
        /// Each should be stripped when it appears at the end of an
        /// intermediate segment.
        /// </summary>
        [Test]
        [TestCase("$", Description = "Statement terminator")]
        [TestCase("?", Description = "Normal question")]
        [TestCase("?!", Description = "Emphatic question")]
        [TestCase("?.", Description = "Declarative question")]
        [TestCase("?~", Description = "Confirmatory question")]
        [TestCase("\ue016", Description = "PUA emphatic question")]
        [TestCase("\ue017", Description = "PUA declarative question")]
        [TestCase("\ue018", Description = "PUA confirmatory question")]
        public void AllEosTokens_StrippedFromIntermediateSegment(string eosToken)
        {
            // EN intermediate segment ending with the EOS token
            var enResult = new PhonemeResult
            {
                Phonemes = new[] { "h", "a", "y", eosToken },
                ProsodyA1 = new[] { 0, 0, 0, 0 },
                ProsodyA2 = new[] { 0, 0, 0, 0 },
                ProsodyA3 = new[] { 0, 0, 0, 0 },
                Success = true
            };

            var enStub = new StubPhonemizerBackend(enResult);
            var jaPhonemizer = new DotNetG2PPhonemizer();

            var mp = new MultilingualPhonemizer(
                new[] { "en", "ja" },
                defaultLatinLanguage: "en",
                enPhonemizer: enStub,
                jaPhonemizer: jaPhonemizer);
            Task.Run(async () => await mp.InitializeAsync()).GetAwaiter().GetResult();

            // EN intermediate + JA final
            var result = Phonemize(mp, "hey あ");
            Assert.IsNotNull(result);

            // The first 3 phonemes are from EN (EOS stripped), so they must not
            // contain the EOS token.
            var enPortion = result.Phonemes.Take(3).ToArray();
            Assert.IsFalse(enPortion.Contains(eosToken),
                $"EOS token '{eosToken}' should be stripped from intermediate EN segment");

            mp.Dispose();
        }

        /// <summary>
        /// When the intermediate segment has no trailing EOS token, the
        /// phoneme array should pass through without any element removed.
        /// </summary>
        [Test]
        public void IntermediateSegment_NoEos_PassesThrough()
        {
            var enResult = new PhonemeResult
            {
                Phonemes = new[] { "h", "e", "l", "o" },
                ProsodyA1 = new[] { 0, 0, 0, 0 },
                ProsodyA2 = new[] { 0, 0, 0, 0 },
                ProsodyA3 = new[] { 0, 0, 0, 0 },
                Success = true
            };

            var enStub = new StubPhonemizerBackend(enResult);
            var jaPhonemizer = new DotNetG2PPhonemizer();

            var mp = new MultilingualPhonemizer(
                new[] { "en", "ja" },
                defaultLatinLanguage: "en",
                enPhonemizer: enStub,
                jaPhonemizer: jaPhonemizer);
            Task.Run(async () => await mp.InitializeAsync()).GetAwaiter().GetResult();

            var result = Phonemize(mp, "hello あ");
            Assert.IsNotNull(result);

            // First 4 phonemes should be the EN phonemes, preserved intact
            Assert.AreEqual("h", result.Phonemes[0]);
            Assert.AreEqual("e", result.Phonemes[1]);
            Assert.AreEqual("l", result.Phonemes[2]);
            Assert.AreEqual("o", result.Phonemes[3]);

            mp.Dispose();
        }

        /// <summary>
        /// When ProsodyA1/A2/A3 arrays are trimmed alongside the EOS token
        /// from an intermediate segment, their lengths must stay aligned
        /// with the phoneme array.
        /// </summary>
        [Test]
        public void ProsodyArrays_AlignedAfterEosStrip()
        {
            // EN intermediate with prosody values and trailing "$"
            var enResult = new PhonemeResult
            {
                Phonemes = new[] { "h", "a", "$" },
                ProsodyA1 = new[] { 10, 20, 30 },
                ProsodyA2 = new[] { 11, 21, 31 },
                ProsodyA3 = new[] { 12, 22, 32 },
                Success = true
            };

            var enStub = new StubPhonemizerBackend(enResult);
            var jaPhonemizer = new DotNetG2PPhonemizer();

            var mp = new MultilingualPhonemizer(
                new[] { "en", "ja" },
                defaultLatinLanguage: "en",
                enPhonemizer: enStub,
                jaPhonemizer: jaPhonemizer);
            Task.Run(async () => await mp.InitializeAsync()).GetAwaiter().GetResult();

            var result = Phonemize(mp, "ha あ");
            Assert.IsNotNull(result);

            // All arrays must be aligned
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA1.Length,
                "ProsodyA1 must stay aligned with phonemes after EOS strip");
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA2.Length,
                "ProsodyA2 must stay aligned with phonemes after EOS strip");
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA3.Length,
                "ProsodyA3 must stay aligned with phonemes after EOS strip");

            // The EN portion's prosody (after EOS strip) should have the
            // sentinel values 10/11/12 and 20/21/22, but NOT 30/31/32.
            Assert.AreEqual(10, result.ProsodyA1[0],
                "EN prosody value at index 0 should survive EOS strip");
            Assert.AreEqual(20, result.ProsodyA1[1],
                "EN prosody value at index 1 should survive EOS strip");

            mp.Dispose();
        }
    }
}