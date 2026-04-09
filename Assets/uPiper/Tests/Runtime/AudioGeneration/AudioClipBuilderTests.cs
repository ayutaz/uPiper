using NUnit.Framework;
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

    }
}