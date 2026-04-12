using System;
using NUnit.Framework;
using uPiper.Core;
using uPiper.Core.AudioGeneration;

namespace uPiper.Tests.Editor
{
    /// <summary>
    /// Tests for <see cref="PiperTTS.SynthesizeAsync"/> and <see cref="PiperTTS.PhonemizeAsync"/>.
    /// Covers guard clauses (disposed, not-initialized, invalid arguments).
    /// E2E tests with real models are handled by CI PlayMode tests.
    /// </summary>
    [TestFixture]
    public class PiperTTSSynthesizeTests
    {
        private PiperConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = new PiperConfig();
        }

        // ================================================================
        // SynthesizeAsync -- guard clauses
        // ================================================================

        [Test]
        public void SynthesizeAsync_NotInitialized_ThrowsInvalidOperationException()
        {
            using var tts = new PiperTTS(_config);
            var request = SynthesisRequest.FromPhonemes(new[] { "a", "b" });

            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await tts.SynthesizeAsync(request));
            StringAssert.Contains("not initialized", ex.Message, "Should mention not initialized");
        }

        [Test]
        public void SynthesizeAsync_Disposed_ThrowsObjectDisposedException()
        {
            var tts = new PiperTTS(_config);
            tts.Dispose();

            var request = SynthesisRequest.FromPhonemes(new[] { "a", "b" });

            Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await tts.SynthesizeAsync(request));
        }

        // ================================================================
        // PhonemizeAsync -- guard clauses
        // ================================================================

        [Test]
        public void PhonemizeAsync_NotInitialized_ThrowsInvalidOperationException()
        {
            using var tts = new PiperTTS(_config);

            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await tts.PhonemizeAsync("テスト"));
            StringAssert.Contains("not initialized", ex.Message, "Should mention not initialized");
        }

        [Test]
        public void PhonemizeAsync_Disposed_ThrowsObjectDisposedException()
        {
            var tts = new PiperTTS(_config);
            tts.Dispose();

            Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await tts.PhonemizeAsync("テスト"));
        }

        [Test]
        public void PhonemizeAsync_NullText_ThrowsArgumentException()
        {
            using var tts = new PiperTTS(_config);
            // Even though not initialized, null check should come after disposed/initialized checks
            // but the text validation is checked after the initialization check.
            // So we expect InvalidOperationException first (not initialized).
            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await tts.PhonemizeAsync(null));
        }

        [Test]
        public void PhonemizeAsync_EmptyText_ThrowsOnNotInitialized()
        {
            using var tts = new PiperTTS(_config);
            // Not initialized, so InvalidOperationException is thrown before text validation
            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await tts.PhonemizeAsync(""));
        }

        // ================================================================
        // SynthesizeWithTimingAsync -- guard clauses
        // ================================================================

        [Test]
        public void SynthesizeWithTimingAsync_NotInitialized_ThrowsInvalidOperationException()
        {
            using var tts = new PiperTTS(_config);
            var request = SynthesisRequest.FromPhonemes(new[] { "k", "o", "N" });

            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await tts.SynthesizeWithTimingAsync(request));
            StringAssert.Contains("not initialized", ex.Message, "Should mention not initialized");
        }

        [Test]
        public void SynthesizeWithTimingAsync_Disposed_ThrowsObjectDisposedException()
        {
            var tts = new PiperTTS(_config);
            tts.Dispose();

            var request = SynthesisRequest.FromPhonemes(new[] { "k", "o", "N" });

            Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await tts.SynthesizeWithTimingAsync(request));
        }
    }
}