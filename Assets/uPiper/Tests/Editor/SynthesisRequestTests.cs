using System;
using NUnit.Framework;
using uPiper.Core.AudioGeneration;

namespace uPiper.Tests.Editor
{
    /// <summary>
    /// Unit tests for <see cref="SynthesisRequest"/> factory methods.
    /// Covers validation (null, empty, length mismatch) and default parameter values.
    /// </summary>
    [TestFixture]
    public class SynthesisRequestTests
    {
        private static readonly string[] ValidPhonemes = { "k", "o", "N_uvular" };

        // ================================================================
        // FromPhonemes -- valid inputs
        // ================================================================

        [Test]
        public void FromPhonemes_ValidPhonemes_CreatesRequest()
        {
            var request = SynthesisRequest.FromPhonemes(ValidPhonemes);

            Assert.IsNotNull(request.Phonemes);
            Assert.AreEqual(3, request.Phonemes.Length);
            Assert.AreEqual("k", request.Phonemes[0]);
            Assert.AreEqual("o", request.Phonemes[1]);
            Assert.AreEqual("N_uvular", request.Phonemes[2]);
            Assert.IsFalse(request.HasProsody);
            Assert.IsNull(request.ProsodyFlat);
        }

        [Test]
        public void FromPhonemes_DefaultParameters_UsesDefaults()
        {
            var request = SynthesisRequest.FromPhonemes(ValidPhonemes);

            Assert.AreEqual(1.0f, request.LengthScale);
            Assert.AreEqual(0.667f, request.NoiseScale);
            Assert.AreEqual(0.8f, request.NoiseW);
            Assert.AreEqual(0, request.SpeakerId);
            Assert.AreEqual(0, request.LanguageId);
        }

        [Test]
        public void FromPhonemes_CustomParameters_SetsCorrectly()
        {
            var request = SynthesisRequest.FromPhonemes(
                ValidPhonemes,
                lengthScale: 0.8f,
                noiseScale: 0.5f,
                noiseW: 0.6f,
                speakerId: 2,
                languageId: 1);

            Assert.AreEqual(0.8f, request.LengthScale);
            Assert.AreEqual(0.5f, request.NoiseScale);
            Assert.AreEqual(0.6f, request.NoiseW);
            Assert.AreEqual(2, request.SpeakerId);
            Assert.AreEqual(1, request.LanguageId);
        }

        [Test]
        public void FromPhonemes_DefensiveCopy_OriginalArrayUnaffected()
        {
            var phonemes = new[] { "a", "b", "c" };
            var request = SynthesisRequest.FromPhonemes(phonemes);

            // Mutate the original array -- request should not be affected
            phonemes[0] = "MUTATED";

            Assert.AreEqual("a", request.Phonemes[0]);
        }

        // ================================================================
        // FromPhonemes -- invalid inputs
        // ================================================================

        [Test]
        public void FromPhonemes_NullPhonemes_ThrowsArgumentException()
        {
            var ex = Assert.Throws<ArgumentException>(() => SynthesisRequest.FromPhonemes(null));
            Assert.AreEqual("phonemes", ex.ParamName);
        }

        [Test]
        public void FromPhonemes_EmptyPhonemes_ThrowsArgumentException()
        {
            var ex = Assert.Throws<ArgumentException>(
                () => SynthesisRequest.FromPhonemes(Array.Empty<string>()));
            Assert.AreEqual("phonemes", ex.ParamName);
        }

        // ================================================================
        // FromPhonemesWithProsody -- valid inputs
        // ================================================================

        [Test]
        public void FromPhonemesWithProsody_ValidProsody_CreatesRequest()
        {
            var phonemes = new[] { "k", "o", "N_uvular" };
            // stride=3, so 3 phonemes * 3 = 9 elements
            var prosodyFlat = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 };

            var request = SynthesisRequest.FromPhonemesWithProsody(phonemes, prosodyFlat);

            Assert.IsTrue(request.HasProsody);
            Assert.IsNotNull(request.ProsodyFlat);
            Assert.AreEqual(9, request.ProsodyFlat.Length);
            Assert.AreEqual(3, request.Phonemes.Length);
        }

        [Test]
        public void FromPhonemesWithProsody_NullProsody_CreatesRequestWithoutProsody()
        {
            var request = SynthesisRequest.FromPhonemesWithProsody(ValidPhonemes, null);

            Assert.IsFalse(request.HasProsody);
            Assert.IsNull(request.ProsodyFlat);
            Assert.AreEqual(3, request.Phonemes.Length);
        }

        [Test]
        public void FromPhonemesWithProsody_CustomParameters_SetsCorrectly()
        {
            var prosodyFlat = new[] { 0, 0, 0, 1, 1, 1, 2, 2, 2 };

            var request = SynthesisRequest.FromPhonemesWithProsody(
                ValidPhonemes,
                prosodyFlat,
                lengthScale: 1.2f,
                noiseScale: 0.4f,
                noiseW: 0.7f,
                speakerId: 3,
                languageId: 2);

            Assert.AreEqual(1.2f, request.LengthScale);
            Assert.AreEqual(0.4f, request.NoiseScale);
            Assert.AreEqual(0.7f, request.NoiseW);
            Assert.AreEqual(3, request.SpeakerId);
            Assert.AreEqual(2, request.LanguageId);
            Assert.IsTrue(request.HasProsody);
        }

        [Test]
        public void FromPhonemesWithProsody_DefensiveCopy_OriginalArraysUnaffected()
        {
            var phonemes = new[] { "a", "b" };
            var prosody = new[] { 1, 2, 3, 4, 5, 6 };
            var request = SynthesisRequest.FromPhonemesWithProsody(phonemes, prosody);

            // Mutate original arrays
            phonemes[0] = "MUTATED";
            prosody[0] = 999;

            Assert.AreEqual("a", request.Phonemes[0]);
            Assert.AreEqual(1, request.ProsodyFlat[0]);
        }

        // ================================================================
        // FromPhonemesWithProsody -- invalid inputs
        // ================================================================

        [Test]
        public void FromPhonemesWithProsody_NullPhonemes_ThrowsArgumentException()
        {
            var ex = Assert.Throws<ArgumentException>(
                () => SynthesisRequest.FromPhonemesWithProsody(null, new[] { 1, 2, 3 }));
            Assert.AreEqual("phonemes", ex.ParamName);
        }

        [Test]
        public void FromPhonemesWithProsody_EmptyPhonemes_ThrowsArgumentException()
        {
            var ex = Assert.Throws<ArgumentException>(
                () => SynthesisRequest.FromPhonemesWithProsody(Array.Empty<string>(), null));
            Assert.AreEqual("phonemes", ex.ParamName);
        }

        [Test]
        public void FromPhonemesWithProsody_MismatchedLength_ThrowsArgumentException()
        {
            var phonemes = new[] { "a", "b", "c" }; // 3 phonemes
            var prosody = new[] { 1, 2, 3, 4, 5, 6 }; // 6 elements (should be 9)

            var ex = Assert.Throws<ArgumentException>(
                () => SynthesisRequest.FromPhonemesWithProsody(phonemes, prosody));
            Assert.AreEqual("prosodyFlat", ex.ParamName);
            StringAssert.Contains("9", ex.Message); // expected length
        }

        // ================================================================
        // Internal constructor -- accessible via InternalsVisibleTo
        // ================================================================

        [Test]
        public void InternalConstructor_StillAccessible_FromTestAssembly()
        {
            // Verify InternalsVisibleTo allows test assembly to use internal constructor
            var request = new SynthesisRequest(
                new[] { "a" }, null, 1.0f, 0.667f, 0.8f, 0, 0);

            Assert.AreEqual(1, request.Phonemes.Length);
            Assert.AreEqual("a", request.Phonemes[0]);
        }
    }
}