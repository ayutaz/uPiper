using NUnit.Framework;
using UnityEngine;
using uPiper.Core.AudioGeneration;

namespace uPiper.Tests.Runtime.AudioGeneration
{
    [TestFixture]
    public class AudioNormalizerTests
    {
        [Test]
        public void NormalizeInPlace_ValidData_NormalizesToTarget()
        {
            // Arrange
            var audioData = new[] { 0.5f, -0.5f, 0.25f };
            var targetPeak = 0.95f;

            // Act
            AudioNormalizer.NormalizeInPlace(audioData, targetPeak);

            // Assert
            var maxAbs = 0f;
            foreach (var sample in audioData)
            {
                var abs = Mathf.Abs(sample);
                if (abs > maxAbs) maxAbs = abs;
            }

            Assert.AreEqual(targetPeak, maxAbs, 0.001f);
        }

        [Test]
        public void NormalizeInPlace_SilentAudio_NoChange()
        {
            // Arrange
            var audioData = new[] { 0f, 0f, 0f };
            var original = (float[])audioData.Clone();

            // Act
            AudioNormalizer.NormalizeInPlace(audioData);

            // Assert
            Assert.AreEqual(original, audioData);
        }

        [Test]
        public void NormalizeInPlace_AlreadyNormalized_NoChange()
        {
            // Arrange
            var audioData = new[] { 0.95f, -0.95f };
            var original = (float[])audioData.Clone();

            // Act
            AudioNormalizer.NormalizeInPlace(audioData, 0.95f);

            // Assert
            Assert.AreEqual(original, audioData);
        }

        [Test]
        public void NormalizeInPlace_NullArray_NoException()
        {
            // Act & Assert - should not throw
            Assert.DoesNotThrow(() => AudioNormalizer.NormalizeInPlace(null));
        }

        [Test]
        public void NormalizeInPlace_EmptyArray_NoException()
        {
            // Arrange
            var audioData = new float[0];

            // Act & Assert - should not throw
            Assert.DoesNotThrow(() => AudioNormalizer.NormalizeInPlace(audioData));
        }

        [Test]
        public void NormalizeInPlace_TargetPeakClamped_ClampedTo01()
        {
            // Arrange
            var audioData = new[] { 0.5f, -0.5f };
            var targetPeak = 1.5f; // should be clamped to 1.0

            // Act
            AudioNormalizer.NormalizeInPlace(audioData, targetPeak);

            // Assert - clamped to 1.0, so max should be 1.0
            var maxAbs = 0f;
            foreach (var sample in audioData)
            {
                var abs = Mathf.Abs(sample);
                if (abs > maxAbs) maxAbs = abs;
            }

            Assert.AreEqual(1.0f, maxAbs, 0.001f);
        }

        [Test]
        public void Normalize_ValidData_ReturnsNewArray()
        {
            // Arrange
            var audioData = new[] { 0.5f, -0.5f };
            var originalCopy = (float[])audioData.Clone();

            // Act
            var result = AudioNormalizer.Normalize(audioData, 0.95f);

            // Assert - original array is unchanged
            Assert.AreEqual(originalCopy, audioData);
            // result is a different array reference
            Assert.AreNotSame(audioData, result);
        }

        [Test]
        public void Normalize_ValidData_NormalizesToTarget()
        {
            // Arrange
            var audioData = new[] { 0.5f, -0.5f, 0.25f, -0.25f };
            var targetPeak = 0.95f;

            // Act
            var normalized = AudioNormalizer.Normalize(audioData, targetPeak);

            // Assert
            Assert.IsNotNull(normalized);
            Assert.AreEqual(audioData.Length, normalized.Length);

            var maxValue = 0f;
            foreach (var sample in normalized)
            {
                maxValue = Mathf.Max(maxValue, Mathf.Abs(sample));
            }

            Assert.AreEqual(targetPeak, maxValue, 0.001f);
        }

        [Test]
        public void Normalize_SilentAudio_ReturnsSameArray()
        {
            // Arrange
            var audioData = new[] { 0f, 0f };

            // Act
            var result = AudioNormalizer.Normalize(audioData);

            // Assert - same reference returned (no copy needed)
            Assert.AreSame(audioData, result);
        }
    }
}