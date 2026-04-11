using System;
using System.Threading;
using NUnit.Framework;
using uPiper.Core;
using uPiper.Core.AudioGeneration;

namespace uPiper.Tests.Editor
{
    /// <summary>
    /// Comprehensive cancellation token tests for <see cref="PiperTTS"/> async methods.
    /// Validates guard clause ordering (dispose check, init check, cancellation check)
    /// and pre-cancelled token behavior across all public async entry points.
    /// </summary>
    [TestFixture]
    public class CancellationTests
    {
        private PiperConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = new PiperConfig();
        }

        // ================================================================
        // Pre-cancelled token on uninitialized instance
        // Guard clause order: ThrowIfDisposed → ThrowIfNotInitialized → (no explicit cancellation check)
        // Expected: InvalidOperationException (init check comes before any cancellation logic)
        // ================================================================

        [Test]
        public void GenerateAudioAsync_PreCancelledToken_ThrowsInvalidOperationException()
        {
            using var tts = new PiperTTS(_config);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await tts.GenerateAudioAsync("test", cts.Token));
        }

        [Test]
        public void PhonemizeAsync_PreCancelledToken_ThrowsInvalidOperationException()
        {
            using var tts = new PiperTTS(_config);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await tts.PhonemizeAsync("test", cts.Token));
        }

        [Test]
        public void SynthesizeAsync_PreCancelledToken_ThrowsInvalidOperationException()
        {
            using var tts = new PiperTTS(_config);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var request = SynthesisRequest.FromPhonemes(new[] { "a", "b" });

            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await tts.SynthesizeAsync(request, cts.Token));
        }

        [Test]
        public void LoadVoiceAsync_PreCancelledToken_OnUninitializedInstance_ThrowsInvalidOperationException()
        {
            using var tts = new PiperTTS(_config);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var voice = new PiperVoiceConfig { VoiceId = "test" };

            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await tts.LoadVoiceAsync(voice, cts.Token));
        }

        // ================================================================
        // Pre-cancelled token on CreateAsync static methods
        // Guard clause order: null check → cancellationToken.ThrowIfCancellationRequested()
        // Expected: OperationCanceledException (cancellation check is explicit at entry)
        // ================================================================

        [Test]
        public void CreateAsync_DefaultOverload_PreCancelledToken_ThrowsOperationCanceledException()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(
                async () => await PiperTTS.CreateAsync(cts.Token));
        }

        [Test]
        public void CreateAsync_WithVoiceConfig_PreCancelledToken_ThrowsOperationCanceledException()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var voice = new PiperVoiceConfig { VoiceId = "test" };

            Assert.ThrowsAsync<OperationCanceledException>(
                async () => await PiperTTS.CreateAsync(new PiperConfig(), voice, cts.Token));
        }

        // ================================================================
        // Dispose check takes precedence over cancellation check
        // Guard clause order: ThrowIfDisposed → cancellationToken.ThrowIfCancellationRequested()
        // Expected: ObjectDisposedException (dispose check is first)
        // ================================================================

        [Test]
        public void InitializeAsync_Disposed_PreCancelledToken_ThrowsObjectDisposedException()
        {
            var tts = new PiperTTS(_config);
            tts.Dispose();

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await tts.InitializeAsync(cts.Token));
        }

        [Test]
        public void GenerateAudioAsync_Disposed_PreCancelledToken_ThrowsObjectDisposedException()
        {
            var tts = new PiperTTS(_config);
            tts.Dispose();

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await tts.GenerateAudioAsync("test", cts.Token));
        }

        // ================================================================
        // Default CancellationToken does not interfere with construction
        // ================================================================

        [Test]
        public void CancellationTokenSource_DefaultToken_DoesNotThrow_OnConstruction()
        {
            Assert.DoesNotThrow(() =>
            {
                using var tts = new PiperTTS(_config);
                Assert.That(tts.IsInitialized, Is.False,
                    "IsInitialized should be false after construction with default token");
            });
        }

        // ================================================================
        // CreateAsync with PiperConfigAsset (ScriptableObject)
        // Cannot construct ScriptableObject in EditMode tests without UnityEngine context,
        // so we verify null config asset throws ArgumentNullException before cancellation.
        // ================================================================

        [Test]
        public void CreateAsync_NullConfigAsset_PreCancelledToken_ThrowsArgumentNullException()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAsync<ArgumentNullException>(
                async () => await PiperTTS.CreateAsync((PiperConfigAsset)null, cts.Token));
        }
    }
}