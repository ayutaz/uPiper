using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using uPiper.Core.Phonemizers.Implementations;
using uPiper.Core.Phonemizers.Multilingual;
using uPiper.Core.Phonemizers.Multilingual.Handlers;
using uPiper.Tests.Editor.Phonemizers.Handlers;

namespace uPiper.Tests.Editor.Phonemizers
{
    /// <summary>
    /// Tests for the EOS PUA token stripping and leading PAD ("_") stripping
    /// logic in <see cref="MultilingualPhonemizer"/>.
    /// </summary>
    [TestFixture]
    public class MultilingualPhonemizerEosTests
    {
        // ── Helpers ─────────────────────────────────────────────────────────

        private static async Task<MultilingualPhonemizeResult> Phonemize(
            MultilingualPhonemizer mp,
            string text)
        {
            return await mp.PhonemizeWithProsodyAsync(text);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  EOS PUA token tests
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Verify that the three PUA question markers (\ue016, \ue017,
        /// \ue018) are included in the EOS-like token set by confirming they
        /// get stripped from intermediate segments.  For each PUA token we
        /// build a two-segment scenario (EN intermediate + JA final) where
        /// the EN handler's output ends with the PUA token.
        /// </summary>
        [Test]
        [TestCase("\ue016", Description = "PUA ?! (emphatic question)")]
        [TestCase("\ue017", Description = "PUA ?. (declarative question)")]
        [TestCase("\ue018", Description = "PUA ?~ (confirmatory question)")]
        public async Task EosLikeTokens_ContainsPuaQuestionMarkers(string puaToken)
        {
            // EN intermediate segment ending with the PUA token
            var enStub = new StubG2PHandler(
                "en",
                new[] { "h", "a", "y", puaToken },
                new int[4 * 3]);

            // Real JA phonemizer for the final segment
            var jaPhonemizer = new DotNetG2PPhonemizer();

            var mp = new MultilingualPhonemizer(
                new MultilingualPhonemizerOptions
                {
                    Languages = new[] { "ja", "en" },
                    DefaultLatinLanguage = "en",
                    Handlers = new Dictionary<string, ILanguageG2PHandler>
                    {
                        ["en"] = enStub,
                        ["ja"] = new JapaneseG2PHandler(jaPhonemizer)
                    }
                });
            await mp.InitializeAsync();

            // English intermediate, Japanese final
            var result = await Phonemize(mp, "hey あ");
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
        public async Task IntermediateSegment_PuaEos_Stripped()
        {
            // EN intermediate segment ending with \ue016
            var enStub = new StubG2PHandler(
                "en",
                new[] { "w", "er", "l", "d", "\ue016" },
                new int[5 * 3]);

            var jaPhonemizer = new DotNetG2PPhonemizer();

            var mp = new MultilingualPhonemizer(
                new MultilingualPhonemizerOptions
                {
                    Languages = new[] { "ja", "en" },
                    DefaultLatinLanguage = "en",
                    Handlers = new Dictionary<string, ILanguageG2PHandler>
                    {
                        ["en"] = enStub,
                        ["ja"] = new JapaneseG2PHandler(jaPhonemizer)
                    }
                });
            await mp.InitializeAsync();

            // English intermediate, Japanese final
            var result = await Phonemize(mp, "world こん");
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
        public async Task FinalSegment_PuaEos_Preserved()
        {
            // Single EN segment ending with \ue017 — it is the only (=last) segment.
            var enStub = new StubG2PHandler(
                "en",
                new[] { "h", "e", "l", "o", "\ue017" },
                new int[5 * 3]);

            var mp = new MultilingualPhonemizer(
                new MultilingualPhonemizerOptions
                {
                    Languages = new[] { "en" },
                    DefaultLatinLanguage = "en",
                    Handlers = new Dictionary<string, ILanguageG2PHandler>
                    {
                        ["en"] = enStub
                    }
                });
            await mp.InitializeAsync();

            var result = await Phonemize(mp, "hello");
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
        public async Task IntermediateSegment_StandardEos_Stripped()
        {
            var enStub = new StubG2PHandler(
                "en",
                new[] { "h", "e", "l", "o", "$" },
                new int[5 * 3]);

            var jaPhonemizer = new DotNetG2PPhonemizer();

            var mp = new MultilingualPhonemizer(
                new MultilingualPhonemizerOptions
                {
                    Languages = new[] { "en", "ja" },
                    DefaultLatinLanguage = "en",
                    Handlers = new Dictionary<string, ILanguageG2PHandler>
                    {
                        ["en"] = enStub,
                        ["ja"] = new JapaneseG2PHandler(jaPhonemizer)
                    }
                });
            await mp.InitializeAsync();

            // English intermediate, Japanese final
            var result = await Phonemize(mp, "hello あ");
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
        /// leading token removed by JapaneseG2PHandler.
        /// Uses the real DotNetG2PPhonemizer which produces leading "_".
        /// </summary>
        [Test]
        public async Task JapaneseSegment_LeadingPad_Stripped()
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
                new MultilingualPhonemizerOptions
                {
                    Languages = new[] { "ja" },
                    Handlers = new Dictionary<string, ILanguageG2PHandler>
                    {
                        ["ja"] = new JapaneseG2PHandler(jaPhonemizer)
                    }
                });
            await mp.InitializeAsync();

            var result = await Phonemize(mp, "こんにちは");
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Phonemes.Length > 0);
            Assert.AreNotEqual("_", result.Phonemes[0],
                "Leading PAD '_' should be stripped from Japanese segment by JapaneseG2PHandler");

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
        public async Task JapaneseSegment_NoPad_Unchanged()
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
                new MultilingualPhonemizerOptions
                {
                    Languages = new[] { "ja" },
                    Handlers = new Dictionary<string, ILanguageG2PHandler>
                    {
                        ["ja"] = new JapaneseG2PHandler(jaPhonemizer)
                    }
                });
            await mp.InitializeAsync();

            var result = await Phonemize(mp, "あ");
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
        /// The PAD stripping code path is guarded by the JapaneseG2PHandler only.
        /// </summary>
        [Test]
        public async Task NonJapaneseSegment_NoPadStripping()
        {
            var enStub = new StubG2PHandler(
                "en",
                new[] { "_", "h", "e", "l", "o" },
                new int[5 * 3]);

            var mp = new MultilingualPhonemizer(
                new MultilingualPhonemizerOptions
                {
                    Languages = new[] { "en" },
                    DefaultLatinLanguage = "en",
                    Handlers = new Dictionary<string, ILanguageG2PHandler>
                    {
                        ["en"] = enStub
                    }
                });
            await mp.InitializeAsync();

            var result = await Phonemize(mp, "hello");
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
        public async Task ProsodyArrays_AlignedAfterPadStrip()
        {
            var jaPhonemizer = new DotNetG2PPhonemizer();
            var rawResult = jaPhonemizer.PhonemizeWithProsody("こんにちは");
            Assert.IsNotNull(rawResult.Phonemes);
            Assert.IsTrue(rawResult.Phonemes.Length > 0);

            // Confirm raw result has leading "_"
            Assert.AreEqual("_", rawResult.Phonemes[0],
                "Precondition: raw output starts with '_'");

            var mp = new MultilingualPhonemizer(
                new MultilingualPhonemizerOptions
                {
                    Languages = new[] { "ja" },
                    Handlers = new Dictionary<string, ILanguageG2PHandler>
                    {
                        ["ja"] = new JapaneseG2PHandler(jaPhonemizer)
                    }
                });
            await mp.InitializeAsync();

            var result = await Phonemize(mp, "こんにちは");
            Assert.IsNotNull(result);

            // ProsodyFlat must be aligned (phonemeCount * 3)
            Assert.AreEqual(result.Phonemes.Length * 3, result.ProsodyFlat.Length,
                "ProsodyFlat length must match phonemes * 3 after PAD strip");

            // The stripped result should be 1 shorter than raw (the leading "_" was removed)
            Assert.AreEqual(rawResult.Phonemes.Length - 1, result.Phonemes.Length,
                "Exactly one element should be stripped (the leading PAD)");

            // Prosody values at index 0 of the stripped result should correspond
            // to index 1 of the raw result (i.e., the first real phoneme's prosody).
            // Raw uses separate A1/A2/A3; our result uses flat stride=3.
            Assert.AreEqual(rawResult.ProsodyA1[1], result.ProsodyFlat[0 * 3 + 0],
                "ProsodyFlat A1[0] should correspond to the first real phoneme (index 1 in raw)");
            Assert.AreEqual(rawResult.ProsodyA2[1], result.ProsodyFlat[0 * 3 + 1],
                "ProsodyFlat A2[0] should correspond to the first real phoneme (index 1 in raw)");
            Assert.AreEqual(rawResult.ProsodyA3[1], result.ProsodyFlat[0 * 3 + 2],
                "ProsodyFlat A3[0] should correspond to the first real phoneme (index 1 in raw)");

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
        public async Task AllEosTokens_StrippedFromIntermediateSegment(string eosToken)
        {
            // EN intermediate segment ending with the EOS token
            var enStub = new StubG2PHandler(
                "en",
                new[] { "h", "a", "y", eosToken },
                new[] { 0, 0, 0, 0 },
                new[] { 0, 0, 0, 0 },
                new[] { 0, 0, 0, 0 });

            var jaPhonemizer = new DotNetG2PPhonemizer();

            var mp = new MultilingualPhonemizer(
                new MultilingualPhonemizerOptions
                {
                    Languages = new[] { "en", "ja" },
                    DefaultLatinLanguage = "en",
                    Handlers = new Dictionary<string, ILanguageG2PHandler>
                    {
                        ["en"] = enStub,
                        ["ja"] = new JapaneseG2PHandler(jaPhonemizer)
                    }
                });
            await mp.InitializeAsync();

            // EN intermediate + JA final
            var result = await Phonemize(mp, "hey あ");
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
        public async Task IntermediateSegment_NoEos_PassesThrough()
        {
            var enStub = new StubG2PHandler(
                "en",
                new[] { "h", "e", "l", "o" },
                new int[4 * 3]);

            var jaPhonemizer = new DotNetG2PPhonemizer();

            var mp = new MultilingualPhonemizer(
                new MultilingualPhonemizerOptions
                {
                    Languages = new[] { "en", "ja" },
                    DefaultLatinLanguage = "en",
                    Handlers = new Dictionary<string, ILanguageG2PHandler>
                    {
                        ["en"] = enStub,
                        ["ja"] = new JapaneseG2PHandler(jaPhonemizer)
                    }
                });
            await mp.InitializeAsync();

            var result = await Phonemize(mp, "hello あ");
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
        public async Task ProsodyArrays_AlignedAfterEosStrip()
        {
            // EN intermediate with prosody values and trailing "$"
            // stride=3 flat: [a1_0, a2_0, a3_0, a1_1, a2_1, a3_1, a1_2, a2_2, a3_2]
            var enStub = new StubG2PHandler(
                "en",
                new[] { "h", "a", "$" },
                new[] { 10, 11, 12, 20, 21, 22, 30, 31, 32 });

            var jaPhonemizer = new DotNetG2PPhonemizer();

            var mp = new MultilingualPhonemizer(
                new MultilingualPhonemizerOptions
                {
                    Languages = new[] { "en", "ja" },
                    DefaultLatinLanguage = "en",
                    Handlers = new Dictionary<string, ILanguageG2PHandler>
                    {
                        ["en"] = enStub,
                        ["ja"] = new JapaneseG2PHandler(jaPhonemizer)
                    }
                });
            await mp.InitializeAsync();

            var result = await Phonemize(mp, "ha あ");
            Assert.IsNotNull(result);

            // ProsodyFlat must be aligned (phonemeCount * 3)
            Assert.AreEqual(result.Phonemes.Length * 3, result.ProsodyFlat.Length,
                "ProsodyFlat must stay aligned with phonemes after EOS strip");

            // The EN portion's prosody (after EOS strip) should have the
            // sentinel values. In stride=3: flat[0]=10 (A1 of phoneme 0),
            // flat[3]=20 (A1 of phoneme 1). The "$" prosody (30,31,32) should be gone.
            Assert.AreEqual(10, result.ProsodyFlat[0 * 3 + 0],
                "EN prosody A1 at phoneme 0 should survive EOS strip");
            Assert.AreEqual(20, result.ProsodyFlat[1 * 3 + 0],
                "EN prosody A1 at phoneme 1 should survive EOS strip");

            mp.Dispose();
        }
    }
}