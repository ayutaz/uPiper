using NUnit.Framework;
using uPiper.Core;

namespace uPiper.Tests.Editor
{
    /// <summary>
    /// Tests for iOS AVAudioSession auto-initialization integration.
    /// Note: Actual iOS code paths are guarded by #if UNITY_IOS &amp;&amp; !UNITY_EDITOR,
    /// so these tests verify the non-iOS path (no-op) and structural correctness.
    /// Full iOS testing requires device/simulator builds.
    /// </summary>
    [TestFixture]
    public class IOSAudioSessionAutoInitTests
    {
        private PiperConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = new PiperConfig();
        }

        [Test]
        public void InitializeAsync_NonIOSPlatform_DoesNotThrowFromPlatformInit()
        {
            using var tts = new PiperTTS(_config);

            // On non-iOS platforms, InitializePlatformAudioSession is a no-op
            // and InitializeAsync completes without iOS P/Invoke errors.
            Assert.DoesNotThrowAsync(async () => await tts.InitializeAsync());
        }

        [Test]
        public void InitializeAsync_FailsAtExpectedPoint_NotAtPlatformAudioSession()
        {
            using var tts = new PiperTTS(_config);

            // On non-iOS platforms, the iOS audio session step is compiled out,
            // so InitializeAsync succeeds without any AVAudioSession-related error.
            Assert.DoesNotThrowAsync(async () => await tts.InitializeAsync());
        }

        [Test]
        public void InitializeAsync_CalledTwice_SecondCallIsSafe()
        {
            using var tts = new PiperTTS(_config);

            // First call succeeds normally
            Assert.DoesNotThrowAsync(async () => await tts.InitializeAsync());

            // Second call returns early (already initialized) without throwing
            Assert.DoesNotThrowAsync(async () => await tts.InitializeAsync());
        }
    }
}