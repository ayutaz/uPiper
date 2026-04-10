using System.Threading.Tasks;
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
        public async Task InitializeAsync_NonIOSPlatform_DoesNotThrowFromPlatformInit()
        {
            using var tts = new PiperTTS(_config);

            var ex = await Assert.ThrowsAsync<PiperException>(
                async () => await tts.InitializeAsync());

            Assert.That(ex.InnerException,
                Is.Not.TypeOf<System.DllNotFoundException>(),
                "Should not attempt iOS P/Invoke on non-iOS platform");
            Assert.That(ex.InnerException,
                Is.Not.TypeOf<System.EntryPointNotFoundException>(),
                "Should not attempt iOS P/Invoke on non-iOS platform");
        }

        [Test]
        public async Task InitializeAsync_FailsAtExpectedPoint_NotAtPlatformAudioSession()
        {
            using var tts = new PiperTTS(_config);

            var ex = await Assert.ThrowsAsync<PiperException>(
                async () => await tts.InitializeAsync());

            StringAssert.DoesNotContain("AVAudioSession", ex.Message);
            StringAssert.DoesNotContain("AudioSession", ex.Message);
        }

        [Test]
        public async Task InitializeAsync_CalledTwice_SecondCallIsSafe()
        {
            using var tts = new PiperTTS(_config);

            await Assert.ThrowsAsync<PiperException>(
                async () => await tts.InitializeAsync());

            await Assert.ThrowsAsync<PiperException>(
                async () => await tts.InitializeAsync());
        }
    }
}