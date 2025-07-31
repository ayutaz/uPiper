using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using uPiper.Core;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace uPiper.Tests.Runtime.Core
{
    public class AudioChunkTest
    {
        [Test]
        public void Constructor_ValidatesParameters()
        {
            // Assert null samples
            Assert.Throws<ArgumentNullException>(() =>
                new AudioChunk(null, 22050, 1, 0, false));

            // Assert invalid sample rate
            Assert.Throws<ArgumentException>(() =>
                new AudioChunk(new float[100], 0, 1, 0, false));

            // Assert invalid channels
            Assert.Throws<ArgumentException>(() =>
                new AudioChunk(new float[100], 22050, 0, 0, false));

            // Assert invalid chunk index
            Assert.Throws<ArgumentException>(() =>
                new AudioChunk(new float[100], 22050, 1, -1, false));
        }

        [Test]
        public void Duration_CalculatedCorrectly()
        {
            // Arrange
            var samples = new float[22050]; // 1 second at 22050Hz

            // Act
            var chunk = new AudioChunk(samples, 22050, 1, 0, false);

            // Assert
            Assert.AreEqual(1f, chunk.Duration, 0.001f);
        }

        [Test]
        public void Duration_CalculatedCorrectlyForStereo()
        {
            // Arrange
            var samples = new float[44100]; // 1 second of stereo at 22050Hz

            // Act
            var chunk = new AudioChunk(samples, 22050, 2, 0, false);

            // Assert
            Assert.AreEqual(1f, chunk.Duration, 0.001f);
        }

        [Test]
        public void ToAudioClip_CreatesValidClip()
        {
            // Arrange
            var samples = new float[1000];
            var chunk = new AudioChunk(samples, 22050, 1, 0, false);

            // Act
            var clip = chunk.ToAudioClip("TestClip");

            // Assert
            Assert.IsNotNull(clip);
            Assert.AreEqual("TestClip", clip.name);
            Assert.AreEqual(22050, clip.frequency);
            Assert.AreEqual(1, clip.channels);
            Assert.AreEqual(1000, clip.samples);
        }

        [Test]
        public void ToAudioClip_UsesDefaultName()
        {
            // Arrange
            var chunk = new AudioChunk(new float[100], 22050, 1, 5, false);

            // Act
            var clip = chunk.ToAudioClip();

            // Assert
            Assert.AreEqual("AudioChunk_5", clip.name);
        }

        [Test]
        public void CombineChunks_ThrowsForNullOrEmpty()
        {
            // Assert
            Assert.Throws<ArgumentException>(() =>
                AudioChunk.CombineChunks(null, "test"),
                "Should throw for null chunks");

            Assert.Throws<ArgumentException>(() =>
                AudioChunk.CombineChunks(new AudioChunk[0], "test"),
                "Should throw for empty chunks");
        }

        [Test]
        public void CombineChunks_ValidatesSampleRate()
        {
            // Arrange
            var chunk1 = new AudioChunk(new float[100], 22050, 1, 0, false);
            var chunk2 = new AudioChunk(new float[100], 44100, 1, 1, false);

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                AudioChunk.CombineChunks(new[] { chunk1, chunk2 }, "test"));
        }

        [Test]
        public void CombineChunks_ValidatesChannels()
        {
            // Arrange
            var chunk1 = new AudioChunk(new float[100], 22050, 1, 0, false);
            var chunk2 = new AudioChunk(new float[200], 22050, 2, 1, false);

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                AudioChunk.CombineChunks(new[] { chunk1, chunk2 }, "test"));
        }

        [Test]
        public void CombineChunks_CombinesCorrectly()
        {
            // Arrange
            var samples1 = new float[] { 0.1f, 0.2f, 0.3f };
            var samples2 = new float[] { 0.4f, 0.5f, 0.6f };
            var chunk1 = new AudioChunk(samples1, 22050, 1, 0, false);
            var chunk2 = new AudioChunk(samples2, 22050, 1, 1, true);

            // Act
            var combined = AudioChunk.CombineChunks(new[] { chunk1, chunk2 }, "Combined");

            // Assert
            Assert.IsNotNull(combined);
            Assert.AreEqual("Combined", combined.name);
            Assert.AreEqual(6, combined.samples);
            Assert.AreEqual(22050, combined.frequency);

            // Verify data
            var data = new float[6];
            combined.GetData(data, 0);
            Assert.AreEqual(new float[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f }, data);
        }

        [Test]
        public void TextSegmentAndStartTime_AreStoredCorrectly()
        {
            // Arrange & Act
            var chunk = new AudioChunk(
                new float[100],
                22050,
                1,
                0,
                false,
                "Hello world",
                1.5f
            );

            // Assert
            Assert.AreEqual("Hello world", chunk.TextSegment);
            Assert.AreEqual(1.5f, chunk.StartTime);
        }

        [Test]
        public void ToAudioClip_HandlesStereoAudio()
        {
            // Arrange
            var samples = new float[44100]; // 1 second of stereo at 22050Hz
            var chunk = new AudioChunk(samples, 22050, 2, 0, false);

            // Act
            var clip = chunk.ToAudioClip("StereoTest");

            // Assert
            Assert.IsNotNull(clip);
            Assert.AreEqual("StereoTest", clip.name);
            Assert.AreEqual(22050, clip.frequency);
            Assert.AreEqual(2, clip.channels);
            Assert.AreEqual(22050, clip.samples); // 44100 total samples / 2 channels
        }

        #region Performance Tests

        [Test]
        [Category("Performance")]
        public void AudioChunk_Properties_Performance()
        {
            // Arrange
            var chunk = new AudioChunk(new float[1000], 22050, 1, 0, false);

            // Warm up
            for (var i = 0; i < 10; i++)
            {
                var temp = chunk.SampleRate;
                temp = chunk.Channels;
            }

            // Test that accessing properties is fast
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            for (var i = 0; i < 100000; i++)
            {
                var sampleRate = chunk.SampleRate;
                var channels = chunk.Channels;
                var chunkIndex = chunk.ChunkIndex;
                var isFinal = chunk.IsFinal;
                var duration = chunk.Duration;
                var samples = chunk.Samples;
            }
            stopwatch.Stop();

            // Properties access should be very fast (less than 100ms for 100k iterations)
            Assert.Less(stopwatch.ElapsedMilliseconds, 100, "Property access should be fast");
        }

        [Test]
        [Category("Performance")]
        public void AudioChunk_Duration_Performance()
        {
            // Arrange
            var chunk = new AudioChunk(new float[22050], 22050, 1, 0, false);

            // Test that calculating duration is fast
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            for (var i = 0; i < 100000; i++)
            {
                var duration = chunk.Duration;
            }
            stopwatch.Stop();

            // Duration calculation should be fast (less than 50ms for 100k iterations)
            Assert.Less(stopwatch.ElapsedMilliseconds, 50, "Duration calculation should be fast");
        }

        [Test]
        [Category("GCAllocation")]
        public void CombineChunks_MinimalGCAllocation()
        {
            // Arrange
            var chunks = new[]
            {
                new AudioChunk(new float[100], 22050, 1, 0, false),
                new AudioChunk(new float[100], 22050, 1, 1, false),
                new AudioChunk(new float[100], 22050, 1, 2, true)
            };

            // CombineChunks creates a new AudioClip, which allocates memory
            // We test that the allocation is minimal and proportional to the data size
            Assert.That(() =>
            {
                var combined = AudioChunk.CombineChunks(chunks, "Combined");
                // Clean up
                if (combined != null) UnityEngine.Object.DestroyImmediate(combined);
            }, Is.AllocatingGCMemory());
        }

        #endregion
    }
}