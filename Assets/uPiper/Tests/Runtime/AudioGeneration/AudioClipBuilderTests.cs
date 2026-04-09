using NUnit.Framework;
using Unity.Collections;
using uPiper.Core.AudioGeneration;

namespace uPiper.Tests.Runtime.AudioGeneration
{
    [TestFixture]
    public class AudioClipBuilderTests
    {
        private AudioClipBuilder _builder;

        [SetUp]
        public void Setup()
        {
            _builder = new AudioClipBuilder();
        }

        // ── Legacy float[] overload tests (kept for backward compatibility) ──

#pragma warning disable CS0618 // Obsolete: testing legacy float[] overload
        [Test]
        public void BuildAudioClip_ValidData_CreatesClip()
        {
            // Arrange
            var audioData = new float[] { 0.1f, 0.2f, 0.3f, -0.1f, -0.2f };
            var sampleRate = 22050;

            // Act
            var clip = _builder.BuildAudioClip(audioData, sampleRate, "TestClip");

            // Assert
            Assert.IsNotNull(clip);
            Assert.AreEqual("TestClip", clip.name);
            Assert.AreEqual(audioData.Length, clip.samples);
            Assert.AreEqual(sampleRate, clip.frequency);
            Assert.AreEqual(1, clip.channels);
        }

        [Test]
        public void BuildAudioClip_EmptyData_ThrowsException()
        {
            // Arrange
            var audioData = new float[0];
            var sampleRate = 22050;

            // Act & Assert
            Assert.Throws<System.ArgumentException>(() =>
                _builder.BuildAudioClip(audioData, sampleRate));
        }

        [Test]
        public void BuildAudioClip_InvalidSampleRate_ThrowsException()
        {
            // Arrange
            var audioData = new float[] { 0.1f, 0.2f };
            var sampleRate = 0;

            // Act & Assert
            Assert.Throws<System.ArgumentException>(() =>
                _builder.BuildAudioClip(audioData, sampleRate));
        }
#pragma warning restore CS0618

        // ── NativeArray overload tests ─────────────────────────────

        [Test]
        public void BuildAudioClip_NativeArray_ValidData_CreatesClip()
        {
            // Arrange
            var audioData = new NativeArray<float>(
                new float[] { 0.1f, 0.2f, 0.3f, -0.1f, -0.2f }, Allocator.Persistent);
            var sampleRate = 22050;

            try
            {
                // Act
                var clip = _builder.BuildAudioClip(audioData, sampleRate, "TestClipNative");

                // Assert
                Assert.IsNotNull(clip);
                Assert.AreEqual("TestClipNative", clip.name);
                Assert.AreEqual(audioData.Length, clip.samples);
                Assert.AreEqual(sampleRate, clip.frequency);
                Assert.AreEqual(1, clip.channels);
            }
            finally
            {
                audioData.Dispose();
            }
        }

        [Test]
        public void BuildAudioClip_NativeArray_EmptyData_ThrowsException()
        {
            // Arrange
            var audioData = new NativeArray<float>(0, Allocator.Persistent);
            var sampleRate = 22050;

            try
            {
                // Act & Assert
                Assert.Throws<System.ArgumentException>(() =>
                    _builder.BuildAudioClip(audioData, sampleRate));
            }
            finally
            {
                audioData.Dispose();
            }
        }

        [Test]
        public void BuildAudioClip_NativeArray_InvalidSampleRate_ThrowsException()
        {
            // Arrange
            var audioData = new NativeArray<float>(
                new float[] { 0.1f, 0.2f }, Allocator.Persistent);
            var sampleRate = 0;

            try
            {
                // Act & Assert
                Assert.Throws<System.ArgumentException>(() =>
                    _builder.BuildAudioClip(audioData, sampleRate));
            }
            finally
            {
                audioData.Dispose();
            }
        }
    }
}