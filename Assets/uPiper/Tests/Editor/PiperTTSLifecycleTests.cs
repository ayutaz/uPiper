using System;
using System.Threading;
using NUnit.Framework;
using uPiper.Core;

namespace uPiper.Tests.Editor
{
    [TestFixture]
    public class PiperTTSLifecycleTests
    {
        [Test]
        public void InitializeAsync_AfterDispose_ThrowsObjectDisposedException()
        {
            var tts = new PiperTTS(new PiperConfig());
            tts.Dispose();

            Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await tts.InitializeAsync());
        }

        [Test]
        public void GenerateAudioAsync_AfterDispose_ThrowsObjectDisposedException()
        {
            var tts = new PiperTTS(new PiperConfig());
            tts.Dispose();

            Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await tts.GenerateAudioAsync("test"));
        }

        [Test]
        public void DoubleDispose_DoesNotThrow()
        {
            var tts = new PiperTTS(new PiperConfig());
            tts.Dispose();

            Assert.DoesNotThrow(() => tts.Dispose());
        }

        [Test]
        public void InitializeAsync_AlreadyCancelled_ThrowsOperationCanceledException()
        {
            using var tts = new PiperTTS(new PiperConfig());
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(
                async () => await tts.InitializeAsync(cts.Token));
        }

        [Test]
        public void CreateAsync_AlreadyCancelled_ThrowsOperationCanceledException()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(
                async () => await PiperTTS.CreateAsync(cts.Token));
        }

        [Test]
        public void GetPhonemesAsync_AfterDispose_ThrowsObjectDisposedException()
        {
            var tts = new PiperTTS(new PiperConfig());
            tts.Dispose();

            Assert.ThrowsAsync<ObjectDisposedException>(
                async () => await tts.GetPhonemesAsync("test"));
        }

        [Test]
        public void IsInitialized_AfterConstruction_IsFalse()
        {
            using var tts = new PiperTTS(new PiperConfig());
            Assert.That(tts.IsInitialized, Is.False);
        }

        [Test]
        public void AvailableVoices_AfterConstruction_IsEmpty()
        {
            using var tts = new PiperTTS(new PiperConfig());
            Assert.That(tts.AvailableVoices, Is.Empty);
        }
    }
}