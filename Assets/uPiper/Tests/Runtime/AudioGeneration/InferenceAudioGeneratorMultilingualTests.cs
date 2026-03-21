using System;
using System.Threading;
using NUnit.Framework;
using uPiper.Core;
using uPiper.Core.AudioGeneration;

namespace uPiper.Tests.Runtime.AudioGeneration
{
    [TestFixture]
    public class InferenceAudioGeneratorMultilingualTests
    {
        private InferenceAudioGenerator _generator;
        private PiperVoiceConfig _config;

        [SetUp]
        public void Setup()
        {
            _generator = new InferenceAudioGenerator();
            _config = new PiperVoiceConfig
            {
                VoiceId = "multilingual_test_voice",
                SampleRate = 22050,
                NumSpeakers = 3,
                NumLanguages = 7,
                Language = "ja"
            };
        }

        [TearDown]
        public void TearDown()
        {
            _generator?.Dispose();
        }

        // ============================================================
        // Constructor / Default state
        // ============================================================

        [Test]
        public void Constructor_CreatesInstance()
        {
            Assert.IsNotNull(_generator);
        }

        [Test]
        public void IsInitialized_Default_IsFalse()
        {
            Assert.IsFalse(_generator.IsInitialized);
        }

        // ============================================================
        // Property tests (pre-initialization)
        // ============================================================

        [Test]
        public void SupportsLanguageId_BeforeInit_ReturnsFalse()
        {
            // SupportsLanguageId is determined by inspecting model inputs during InitializeAsync.
            // Before initialization, it should default to false.
            Assert.IsFalse(_generator.SupportsLanguageId);
        }

        [Test]
        public void SupportsMultiSpeaker_BeforeInit_ReturnsFalse()
        {
            Assert.IsFalse(_generator.SupportsMultiSpeaker);
        }

        [Test]
        public void SupportsProsody_BeforeInit_ReturnsFalse()
        {
            Assert.IsFalse(_generator.SupportsProsody);
        }

        // ============================================================
        // GenerateAudioAsync with language parameter
        // ============================================================

        [Test]
        public void GenerateAudioAsync_WithLanguageId_NotInitialized_ThrowsInvalidOperationException()
        {
            // Arrange
            var phonemeIds = new[] { 1, 3, 4, 5, 2 };

            // Act & Assert - calling with a languageId on an uninitialized generator
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _generator.GenerateAudioAsync(phonemeIds, languageId: 3));
        }

        [Test]
        public void GenerateAudioAsync_NullPhonemeIds_Throws()
        {
            // ValidateGenerationPrerequisites checks disposed/initialized before phonemeIds,
            // so without initialization, InvalidOperationException is thrown first.
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _generator.GenerateAudioAsync(null, languageId: 0));
        }

        [Test]
        public void GenerateAudioAsync_EmptyPhonemeIds_Throws()
        {
            // Empty array should also fail validation.
            // Without initialization, InvalidOperationException is thrown first.
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _generator.GenerateAudioAsync(new int[0], languageId: 0));
        }

        [Test]
        public void GenerateAudioAsync_WithCancellationToken_NotInitialized_Throws()
        {
            // Arrange
            var phonemeIds = new[] { 1, 2, 3 };
            using var cts = new CancellationTokenSource();

            // Act & Assert - CancellationToken parameter is accepted but
            // not-initialized check fires first.
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _generator.GenerateAudioAsync(
                    phonemeIds,
                    cancellationToken: cts.Token));
        }

        // ============================================================
        // GenerateAudioWithProsodyAsync with language parameter
        // ============================================================

        [Test]
        public void GenerateAudioWithProsodyAsync_NotInitialized_ThrowsInvalidOperationException()
        {
            // Arrange
            var phonemeIds = new[] { 1, 3, 4, 5, 2 };
            var prosodyA1 = new[] { 0, 1, 2, 3, 0 };
            var prosodyA2 = new[] { 2, 2, 2, 2, 0 };
            var prosodyA3 = new[] { 1, 1, 1, 1, 0 };

            // Act & Assert
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _generator.GenerateAudioWithProsodyAsync(
                    phonemeIds, prosodyA1, prosodyA2, prosodyA3,
                    languageId: 0));
        }

        [Test]
        public void GenerateAudioWithProsodyAsync_NullProsodyArrays_NotInitialized_Throws()
        {
            // Arrange - null prosody arrays should be handled gracefully by CreateProsodyTensor,
            // but the not-initialized guard fires first.
            var phonemeIds = new[] { 1, 2, 3, 4, 5 };

            // Act & Assert
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _generator.GenerateAudioWithProsodyAsync(
                    phonemeIds, null, null, null,
                    languageId: 0));
        }

        [Test]
        public void GenerateAudioWithProsodyAsync_MismatchedArrayLengths_NotInitialized_Throws()
        {
            // Arrange - mismatched lengths between phonemeIds and prosody arrays.
            // CreateProsodyTensor safely handles short arrays by zero-filling, but
            // the not-initialized guard fires first.
            var phonemeIds = new[] { 1, 2, 3, 4, 5 };
            var prosodyA1 = new[] { 0, 1 }; // shorter than phonemeIds
            var prosodyA2 = new[] { 2, 2, 2, 2, 2, 2, 2 }; // longer than phonemeIds
            var prosodyA3 = new[] { 1, 1, 1 }; // different length

            // Act & Assert
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _generator.GenerateAudioWithProsodyAsync(
                    phonemeIds, prosodyA1, prosodyA2, prosodyA3,
                    languageId: 0));
        }

        // ============================================================
        // Language ID validation
        // ============================================================

        [Test]
        public void GenerateAudioAsync_LanguageId0_NotInitialized_Throws()
        {
            // languageId=0 corresponds to Japanese in multilingual models.
            // Without initialization, this should throw.
            var phonemeIds = new[] { 1, 3, 4, 5, 2 };

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _generator.GenerateAudioAsync(phonemeIds, languageId: 0));
        }

        [Test]
        public void GenerateAudioAsync_LanguageId6_NotInitialized_Throws()
        {
            // languageId=6 corresponds to Korean in multilingual models.
            // Without initialization, this should throw.
            var phonemeIds = new[] { 1, 3, 4, 5, 2 };

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _generator.GenerateAudioAsync(phonemeIds, languageId: 6));
        }

        [Test]
        public void GenerateAudioAsync_NegativeLanguageId_NotInitialized_Throws()
        {
            // Negative languageId is invalid, but the not-initialized guard fires first.
            // When initialized with a real model, the tensor would receive the negative value;
            // validation is the caller's responsibility.
            var phonemeIds = new[] { 1, 3, 4, 5, 2 };

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _generator.GenerateAudioAsync(phonemeIds, languageId: -1));
        }

        // ============================================================
        // Backward compatibility
        // ============================================================

        [Test]
        public void GenerateAudioAsync_NoLanguageId_BackwardCompatible()
        {
            // Calling GenerateAudioAsync without the languageId parameter should compile
            // and behave identically to the original API (default languageId=0).
            var phonemeIds = new[] { 1, 3, 4, 5, 2 };

            // Should throw InvalidOperationException for not-initialized,
            // NOT a compilation error or argument error.
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _generator.GenerateAudioAsync(phonemeIds));
        }

        [Test]
        public void GenerateAudioAsync_NoSpeakerId_BackwardCompatible()
        {
            // Calling GenerateAudioAsync without the speakerId parameter should compile
            // and behave identically to the original API (default speakerId=0).
            var phonemeIds = new[] { 1, 3, 4, 5, 2 };

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _generator.GenerateAudioAsync(
                    phonemeIds,
                    lengthScale: 1.0f,
                    noiseScale: 0.667f,
                    noiseW: 0.8f));
        }

        // ============================================================
        // Dispose
        // ============================================================

        [Test]
        public void Dispose_MultipleCalls_NoError()
        {
            Assert.DoesNotThrow(() =>
            {
                _generator.Dispose();
                _generator.Dispose();
                _generator.Dispose();
            });
        }

        [Test]
        public void GenerateAudioAsync_AfterDispose_ThrowsObjectDisposedException()
        {
            // Arrange
            _generator.Dispose();
            var phonemeIds = new[] { 1, 2, 3 };

            // Act & Assert - disposed check fires before the not-initialized check
            Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                await _generator.GenerateAudioAsync(phonemeIds, languageId: 0));
        }

        [Test]
        public void GenerateAudioWithProsodyAsync_AfterDispose_ThrowsObjectDisposedException()
        {
            // Arrange
            _generator.Dispose();
            var phonemeIds = new[] { 1, 2, 3 };
            var prosody = new[] { 0, 0, 0 };

            // Act & Assert
            Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                await _generator.GenerateAudioWithProsodyAsync(
                    phonemeIds, prosody, prosody, prosody,
                    languageId: 2));
        }

        [Test]
        public void SupportsLanguageId_AfterDispose_ReturnsFalse()
        {
            // Properties should remain accessible after dispose (no throw),
            // and return their default/pre-init values.
            _generator.Dispose();
            Assert.IsFalse(_generator.SupportsLanguageId);
        }

        [Test]
        public void SupportsMultiSpeaker_AfterDispose_ReturnsFalse()
        {
            _generator.Dispose();
            Assert.IsFalse(_generator.SupportsMultiSpeaker);
        }
    }
}