using NUnit.Framework;
using UnityEngine;
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

        [Test]
        public void NormalizeAudio_ValidData_NormalizesToTarget()
        {
            // Arrange
            var audioData = new float[] { 0.5f, -0.5f, 0.25f, -0.25f };
            var targetPeak = 0.95f;

            // Act
            var normalized = _builder.NormalizeAudio(audioData, targetPeak);

            // Assert
            Assert.IsNotNull(normalized);
            Assert.AreEqual(audioData.Length, normalized.Length);
            
            // 最大値を確認
            float maxValue = 0f;
            foreach (var sample in normalized)
            {
                maxValue = Mathf.Max(maxValue, Mathf.Abs(sample));
            }
            Assert.AreEqual(targetPeak, maxValue, 0.001f);
        }

        [Test]
        public void ApplyFade_ValidData_AppliesFadeCorrectly()
        {
            // Arrange
            var audioData = new float[100];
            for (int i = 0; i < audioData.Length; i++)
            {
                audioData[i] = 1.0f;
            }
            var fadeInSamples = 10;
            var fadeOutSamples = 10;

            // Act
            var faded = _builder.ApplyFade(audioData, fadeInSamples, fadeOutSamples);

            // Assert
            Assert.IsNotNull(faded);
            Assert.AreEqual(audioData.Length, faded.Length);
            
            // フェードインのチェック
            Assert.AreEqual(0f, faded[0], 0.001f);
            Assert.Less(faded[5], 1f);
            
            // 中間部分のチェック
            Assert.AreEqual(1f, faded[50], 0.001f);
            
            // フェードアウトのチェック
            Assert.Less(faded[95], 1f);
            Assert.AreEqual(0f, faded[99], 0.001f);
        }

        [Test]
        public void ConcatenateAudio_MultipleChunks_ConcatenatesCorrectly()
        {
            // Arrange
            var chunk1 = new float[] { 0.1f, 0.2f, 0.3f };
            var chunk2 = new float[] { 0.4f, 0.5f };
            var chunk3 = new float[] { 0.6f, 0.7f, 0.8f, 0.9f };
            var chunks = new float[][] { chunk1, chunk2, chunk3 };

            // Act
            var concatenated = _builder.ConcatenateAudio(chunks, 0);

            // Assert
            Assert.IsNotNull(concatenated);
            Assert.AreEqual(9, concatenated.Length); // 3 + 2 + 4
            Assert.AreEqual(0.1f, concatenated[0], 0.001f);
            Assert.AreEqual(0.4f, concatenated[3], 0.001f);
            Assert.AreEqual(0.9f, concatenated[8], 0.001f);
        }

        [Test]
        public void ConcatenateAudio_WithGap_AddsGapCorrectly()
        {
            // Arrange
            var chunk1 = new float[] { 0.1f, 0.2f };
            var chunk2 = new float[] { 0.3f, 0.4f };
            var chunks = new float[][] { chunk1, chunk2 };
            var gapSamples = 3;

            // Act
            var concatenated = _builder.ConcatenateAudio(chunks, gapSamples);

            // Assert
            Assert.IsNotNull(concatenated);
            Assert.AreEqual(7, concatenated.Length); // 2 + 3 + 2
            Assert.AreEqual(0.1f, concatenated[0], 0.001f);
            Assert.AreEqual(0.2f, concatenated[1], 0.001f);
            Assert.AreEqual(0f, concatenated[2], 0.001f); // gap
            Assert.AreEqual(0f, concatenated[3], 0.001f); // gap
            Assert.AreEqual(0f, concatenated[4], 0.001f); // gap
            Assert.AreEqual(0.3f, concatenated[5], 0.001f);
            Assert.AreEqual(0.4f, concatenated[6], 0.001f);
        }
    }
}