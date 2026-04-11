#pragma warning disable CS0618 // Suppress Obsolete warnings for test code

using System;
using NUnit.Framework;
using uPiper.Core;

namespace uPiper.Tests.Editor
{
    [TestFixture]
    public class StreamAudioTests
    {
        [Test]
        public void StreamAudioAsync_ThrowsNotSupportedException()
        {
            using var tts = new PiperTTS(new PiperConfig());

            Assert.Throws<NotSupportedException>(
                () => tts.StreamAudioAsync("Hello world"));
        }

        [Test]
        public void StreamAudioAsync_WithVoiceConfig_ThrowsNotSupportedException()
        {
            using var tts = new PiperTTS(new PiperConfig());
            var voiceConfig = new PiperVoiceConfig { VoiceId = "test-voice" };

            Assert.Throws<NotSupportedException>(
                () => tts.StreamAudioAsync("Hello world", voiceConfig));
        }

        [Test]
        public void StreamAudioAsync_AfterDispose_ThrowsNotSupportedException()
        {
            var tts = new PiperTTS(new PiperConfig());
            tts.Dispose();

            // The [Obsolete] method throws NotSupportedException before any state check,
            // so it should NOT throw ObjectDisposedException even after dispose.
            Assert.Throws<NotSupportedException>(
                () => tts.StreamAudioAsync("Hello world"));
        }

        [Test]
        public void StreamAudioAsync_WithNullText_ThrowsNotSupportedException()
        {
            using var tts = new PiperTTS(new PiperConfig());

            Assert.Throws<NotSupportedException>(
                () => tts.StreamAudioAsync(null));
        }

        [Test]
        public void StreamAudioAsync_WithEmptyText_ThrowsNotSupportedException()
        {
            using var tts = new PiperTTS(new PiperConfig());

            Assert.Throws<NotSupportedException>(
                () => tts.StreamAudioAsync(""));
        }
    }
}
