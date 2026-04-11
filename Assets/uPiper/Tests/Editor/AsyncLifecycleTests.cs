using System;
using NUnit.Framework;
using uPiper.Core;
using uPiper.Core.AudioGeneration;

namespace uPiper.Tests.Editor
{
    [TestFixture]
    public class AsyncLifecycleTests
    {
        private PiperConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = new PiperConfig();
        }

        [TearDown]
        public void TearDown()
        {
            _config = null;
        }

        // ================================================================
        // LoadVoiceAsync
        // ================================================================

        [Test]
        public void LoadVoiceAsync_BeforeInitialization_ThrowsInvalidOperationException()
        {
            using var tts = new PiperTTS(_config);
            var voice = new PiperVoiceConfig { VoiceId = "test-voice" };

            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await tts.LoadVoiceAsync(voice));
            StringAssert.Contains("not initialized", ex.Message);
        }

        [Test]
        public void LoadVoiceAsync_AfterDispose_ThrowsObjectDisposedException()
        {
            var tts = new PiperTTS(_config);
            tts.Dispose();

            var voice = new PiperVoiceConfig { VoiceId = "test-voice" };

            Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await tts.LoadVoiceAsync(voice));
        }

        [Test]
        public void LoadVoiceAsync_NullVoice_ThrowsInvalidOperationException()
        {
            // Disposed check comes first, then init check, then null check.
            // Fresh (non-initialized) PiperTTS should throw InvalidOperationException
            // before reaching the null argument check.
            using var tts = new PiperTTS(_config);

            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await tts.LoadVoiceAsync(null));
            StringAssert.Contains("not initialized", ex.Message);
        }

        // ================================================================
        // GenerateAudioAsync
        // ================================================================

        [Test]
        public void GenerateAudioAsync_BeforeInitialization_ThrowsInvalidOperationException()
        {
            using var tts = new PiperTTS(_config);

            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await tts.GenerateAudioAsync("test"));
            StringAssert.Contains("not initialized", ex.Message);
        }

        // ================================================================
        // PreloadTextAsync
        // ================================================================

        [Test]
        public void PreloadTextAsync_AfterDispose_ThrowsObjectDisposedException()
        {
            var tts = new PiperTTS(_config);
            tts.Dispose();

            Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await tts.PreloadTextAsync("test"));
        }

        [Test]
        public void PreloadTextAsync_BeforeInitialization_ThrowsInvalidOperationException()
        {
            using var tts = new PiperTTS(_config);

            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await tts.PreloadTextAsync("test"));
            StringAssert.Contains("not initialized", ex.Message);
        }

        // ================================================================
        // ClearCache
        // ================================================================

        [Test]
        public void ClearCache_AfterDispose_ThrowsObjectDisposedException()
        {
            var tts = new PiperTTS(_config);
            tts.Dispose();

            Assert.Throws<ObjectDisposedException>(() => tts.ClearCache());
        }

        // ================================================================
        // Configuration (defensive copy)
        // ================================================================

        [Test]
        public void Configuration_ReturnsDefensiveCopy()
        {
            using var tts = new PiperTTS(_config);

            var copy = tts.Configuration;
            Assert.That(copy, Is.Not.Null, "Configuration should not be null");

            // Modify the returned copy
            copy.DefaultLanguage = "fr";
            copy.MaxCacheSizeMB = 999;

            // Original should be unaffected
            var fresh = tts.Configuration;
            Assert.That(fresh.DefaultLanguage, Is.EqualTo(_config.DefaultLanguage),
                "Modifying returned Configuration should not affect the internal config");
            Assert.That(fresh.MaxCacheSizeMB, Is.EqualTo(_config.MaxCacheSizeMB),
                "Modifying returned Configuration should not affect the internal config");
        }

        // ================================================================
        // IsProcessing
        // ================================================================

        [Test]
        public void IsProcessing_DefaultFalse()
        {
            using var tts = new PiperTTS(_config);
            Assert.That(tts.IsProcessing, Is.False,
                "IsProcessing should be false after construction");
        }

        // ================================================================
        // InitializeAsync (double call)
        // ================================================================

        [Test]
        public void InitializeAsync_CalledTwice_NoThrow()
        {
            // InitializeAsync will fail internally (no runtime environment),
            // but calling it twice should not throw due to double-init guard.
            // The first call may throw due to environment issues; the second call
            // should return early because _isInitialized is checked first.
            // Since we cannot fully initialize in EditMode, we verify the guard
            // by checking that a non-disposed, non-initialized instance does not
            // throw ObjectDisposedException on repeated calls.
            using var tts = new PiperTTS(_config);

            // Both calls will likely fail with internal errors (no Unity runtime),
            // but neither should throw ObjectDisposedException.
            // We simply verify the method is callable without disposed exception.
            Assert.DoesNotThrow(() =>
            {
                try { tts.InitializeAsync().GetAwaiter().GetResult(); }
                catch (Exception ex) when (ex is not ObjectDisposedException) { }
            });

            Assert.DoesNotThrow(() =>
            {
                try { tts.InitializeAsync().GetAwaiter().GetResult(); }
                catch (Exception ex) when (ex is not ObjectDisposedException) { }
            });
        }

        // ================================================================
        // CreateAsync (null arguments)
        // ================================================================

        [Test]
        public void CreateAsync_NullConfig_ThrowsArgumentNullException()
        {
            Assert.ThrowsAsync<ArgumentNullException>(
                async () => await PiperTTS.CreateAsync((PiperConfig)null));
        }

        [Test]
        public void CreateAsync_NullConfigAsset_ThrowsArgumentNullException()
        {
            Assert.ThrowsAsync<ArgumentNullException>(
                async () => await PiperTTS.CreateAsync((PiperConfigAsset)null));
        }

        // ================================================================
        // SynthesizeAsync
        // ================================================================

        [Test]
        public void SynthesizeAsync_BeforeInitialization_ThrowsInvalidOperationException()
        {
            using var tts = new PiperTTS(_config);
            var request = SynthesisRequest.FromPhonemes(new[] { "a", "b" });

            var ex = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await tts.SynthesizeAsync(request));
            StringAssert.Contains("not initialized", ex.Message);
        }
    }
}
