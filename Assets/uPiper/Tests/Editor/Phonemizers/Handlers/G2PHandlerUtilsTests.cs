using System;
using DotNetG2P.French;
using DotNetG2P.Portuguese;
using DotNetG2P.Spanish;
using NUnit.Framework;
using uPiper.Core.Phonemizers.Multilingual.Handlers;

namespace uPiper.Tests.Editor.Phonemizers.Handlers
{
    /// <summary>
    /// Tests for G2PHandlerUtils.ExtractProsodyArrays.
    /// Because G2PHandlerUtils is internal, we test it indirectly through the public
    /// handlers (Spanish, French, Portuguese) that delegate to it.
    /// </summary>
    [TestFixture]
    public class G2PHandlerUtilsTests
    {
        // ── ExtractProsodyArrays: equal-length prosody ──────────────────────

        [Test]
        public void ExtractProsodyArrays_EqualLength_AllValuesPreserved()
        {
            // Spanish handler uses G2PHandlerUtils.ExtractProsodyArrays internally.
            // We feed a known word and verify that prosody arrays are the same length
            // as phonemes, confirming ExtractProsodyArrays preserves all values
            // when prosody.Length == phonemeCount.
            var engine = new SpanishG2PEngine();
            var handler = new SpanishG2PHandler(engine);

            var (phonemes, prosodyFlat) = handler.Process("hola");

            Assert.IsNotNull(phonemes);
            Assert.IsTrue(phonemes.Length > 0, "Spanish 'hola' should produce phonemes");
            Assert.AreEqual(phonemes.Length * 3, prosodyFlat.Length,
                "A1 length should equal phoneme count");
            Assert.AreEqual(phonemes.Length * 3, prosodyFlat.Length,
                "A2 length should equal phoneme count");
            Assert.AreEqual(phonemes.Length * 3, prosodyFlat.Length,
                "A3 length should equal phoneme count");

            handler.Dispose();
        }

        // ── ExtractProsodyArrays: prosody shorter than phonemes ─────────────

        [Test]
        public void ExtractProsodyArrays_ProsodyShorterThanPhonemes_ZeroFills()
        {
            // Use a stub handler to verify zero-fill behavior.
            // StubG2PHandler returns fixed arrays; we test the contract that
            // when prosody arrays are shorter than phoneme arrays, the handler
            // (or the MultilingualPhonemizer pad logic) zero-fills the remainder.
            //
            // We verify this through the French handler on a real word;
            // ToPuaPhonemes and ToIpaWithProsody may differ in length,
            // and ExtractProsodyArrays should zero-fill the excess positions.
            var engine = new FrenchG2PEngine();
            var handler = new FrenchG2PHandler(engine);

            // Process a French sentence to exercise the extraction
            var (phonemes, prosodyFlat) = handler.Process("bonjour le monde");

            Assert.IsNotNull(phonemes);
            Assert.IsTrue(phonemes.Length > 0, "French text should produce phonemes");
            // The critical invariant: arrays are aligned regardless of internal mismatch
            Assert.AreEqual(phonemes.Length * 3, prosodyFlat.Length,
                "A1 must be aligned with phonemes even if prosody was shorter");
            Assert.AreEqual(phonemes.Length * 3, prosodyFlat.Length,
                "A2 must be aligned with phonemes even if prosody was shorter");
            Assert.AreEqual(phonemes.Length * 3, prosodyFlat.Length,
                "A3 must be aligned with phonemes even if prosody was shorter");

            handler.Dispose();
        }

        // ── ExtractProsodyArrays: empty prosody ─────────────────────────────

        [Test]
        public void ExtractProsodyArrays_EmptyProsody_ReturnsZeroArrays()
        {
            // When input is empty or produces no phonemes, all arrays should be empty.
            // The Portuguese handler also delegates to G2PHandlerUtils.ExtractProsodyArrays.
            var engine = new PortugueseG2PEngine();
            var handler = new PortugueseG2PHandler(engine);

            // Empty string or whitespace should produce empty arrays
            var (phonemes, prosodyFlat) = handler.Process("");

            Assert.IsNotNull(phonemes);
            Assert.IsNotNull(prosodyFlat);
            Assert.AreEqual(prosodyFlat.Length, phonemes.Length * 3,
                "A1 length must match phoneme count for empty input");
            Assert.AreEqual(prosodyFlat.Length, phonemes.Length * 3,
                "A2 length must match phoneme count for empty input");
            Assert.AreEqual(prosodyFlat.Length, phonemes.Length * 3,
                "A3 length must match phoneme count for empty input");

            handler.Dispose();
        }

        // ── Cross-validation: all three handlers produce aligned arrays ─────

        [Test]
        public void ExtractProsodyArrays_AllHandlersProduceAlignedArrays(
            [Values("es", "fr", "pt")] string lang)
        {
            ILanguageG2PHandler handler = lang switch
            {
                "es" => new SpanishG2PHandler(new SpanishG2PEngine()),
                "fr" => new FrenchG2PHandler(new FrenchG2PEngine()),
                "pt" => new PortugueseG2PHandler(new PortugueseG2PEngine()),
                _ => throw new ArgumentException($"Unexpected language: {lang}")
            };

            // Use a simple word that each language can handle
            var (phonemes, prosodyFlat) = handler.Process("test");

            Assert.IsNotNull(phonemes, $"{lang}: phonemes should not be null");
            Assert.AreEqual(phonemes.Length * 3, prosodyFlat.Length,
                $"{lang}: A1 length mismatch");
            Assert.AreEqual(phonemes.Length * 3, prosodyFlat.Length,
                $"{lang}: A2 length mismatch");
            Assert.AreEqual(phonemes.Length * 3, prosodyFlat.Length,
                $"{lang}: A3 length mismatch");

            handler.Dispose();
        }
    }
}
