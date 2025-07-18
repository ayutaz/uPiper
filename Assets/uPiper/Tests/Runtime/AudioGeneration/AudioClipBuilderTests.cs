using System;
using NUnit.Framework;
using UnityEngine;
using uPiper.Core.AudioGeneration;

namespace uPiper.Tests.Runtime.AudioGeneration
{
    /// <summary>
    /// Tests for AudioClipBuilder
    /// </summary>
    public class AudioClipBuilderTests
    {
        private AudioClipBuilder _builder;

        [SetUp]
        public void SetUp()
        {
            _builder = new AudioClipBuilder();
        }

        [Test]
        public void CreateAudioClip_ValidInput_CreatesClip()
        {
            var samples = new float[44100]; // 1 second at 44.1kHz
            var sampleRate = 44100;
            
            var clip = _builder.CreateAudioClip(samples, sampleRate);
            
            Assert.IsNotNull(clip);
            Assert.AreEqual(sampleRate, clip.frequency);
            Assert.AreEqual(samples.Length, clip.samples);
            Assert.AreEqual(1, clip.channels);
        }

        [Test]
        public void CreateAudioClip_StereoInput_CreatesCorrectClip()
        {
            var samples = new float[88200]; // 1 second stereo at 44.1kHz
            var sampleRate = 44100;
            var channels = 2;
            
            var clip = _builder.CreateAudioClip(samples, sampleRate, channels);
            
            Assert.IsNotNull(clip);
            Assert.AreEqual(sampleRate, clip.frequency);
            Assert.AreEqual(samples.Length / channels, clip.samples);
            Assert.AreEqual(channels, clip.channels);
        }

        [Test]
        public void CreateAudioClip_NullSamples_ThrowsException()
        {
            Assert.Throws<ArgumentNullException>(() => 
                _builder.CreateAudioClip(null, 44100));
        }

        [Test]
        public void CreateAudioClip_InvalidSampleRate_ThrowsException()
        {
            var samples = new float[100];
            
            Assert.Throws<ArgumentException>(() => 
                _builder.CreateAudioClip(samples, 0));
            
            Assert.Throws<ArgumentException>(() => 
                _builder.CreateAudioClip(samples, -1));
        }

        [Test]
        public void NormalizeSamples_AlreadyNormalized_ReturnsAsIs()
        {
            var samples = new float[] { -1f, -0.5f, 0f, 0.5f, 1f };
            
            var normalized = _builder.NormalizeSamples(samples);
            
            CollectionAssert.AreEqual(samples, normalized);
        }

        [Test]
        public void NormalizeSamples_NeedsNormalization_NormalizesCorrectly()
        {
            var samples = new float[] { -2f, -1f, 0f, 1f, 2f };
            
            var normalized = _builder.NormalizeSamples(samples);
            
            Assert.AreEqual(5, normalized.Length);
            Assert.AreEqual(-1f, normalized[0], 0.001f);
            Assert.AreEqual(-0.5f, normalized[1], 0.001f);
            Assert.AreEqual(0f, normalized[2], 0.001f);
            Assert.AreEqual(0.5f, normalized[3], 0.001f);
            Assert.AreEqual(1f, normalized[4], 0.001f);
        }

        [Test]
        public void NormalizeSamples_SilentAudio_ReturnsAsIs()
        {
            var samples = new float[] { 0f, 0f, 0f, 0f };
            
            var normalized = _builder.NormalizeSamples(samples);
            
            CollectionAssert.AreEqual(samples, normalized);
        }

        [Test]
        public void PostProcess_WithNormalization_AppliesCorrectly()
        {
            var samples = new float[] { -2f, 0f, 2f };
            var options = new AudioProcessingOptions
            {
                Normalize = true,
                TargetPeak = 0.5f,
                ApplyFade = false
            };
            
            var processed = _builder.PostProcess(samples, options);
            
            Assert.AreEqual(3, processed.Length);
            Assert.AreEqual(-0.5f, processed[0], 0.001f);
            Assert.AreEqual(0f, processed[1], 0.001f);
            Assert.AreEqual(0.5f, processed[2], 0.001f);
        }

        [Test]
        public void PostProcess_TrimSilence_RemovesSilentParts()
        {
            var samples = new float[] { 0f, 0f, 0.5f, 0.8f, 0.5f, 0f, 0f };
            var options = new AudioProcessingOptions
            {
                TrimSilence = true,
                SilenceThreshold = 0.1f,
                Normalize = false,
                ApplyFade = false
            };
            
            var processed = _builder.PostProcess(samples, options);
            
            Assert.AreEqual(3, processed.Length);
            Assert.AreEqual(0.5f, processed[0], 0.001f);
            Assert.AreEqual(0.8f, processed[1], 0.001f);
            Assert.AreEqual(0.5f, processed[2], 0.001f);
        }

        [Test]
        public void ConvertInt16ToFloat_ValidInput_ConvertsCorrectly()
        {
            short[] int16Samples = { -32768, -16384, 0, 16384, 32767 };
            
            var floatSamples = AudioClipBuilder.ConvertInt16ToFloat(int16Samples);
            
            Assert.AreEqual(5, floatSamples.Length);
            Assert.AreEqual(-1f, floatSamples[0], 0.001f);
            Assert.AreEqual(-0.5f, floatSamples[1], 0.001f);
            Assert.AreEqual(0f, floatSamples[2], 0.001f);
            Assert.AreEqual(0.5f, floatSamples[3], 0.001f);
            Assert.AreEqual(1f, floatSamples[4], 0.001f);
        }

        [Test]
        public void ConvertFloatToInt16_ValidInput_ConvertsCorrectly()
        {
            float[] floatSamples = { -1f, -0.5f, 0f, 0.5f, 1f };
            
            var int16Samples = AudioClipBuilder.ConvertFloatToInt16(floatSamples);
            
            Assert.AreEqual(5, int16Samples.Length);
            Assert.AreEqual(-32767, int16Samples[0]);
            Assert.AreEqual(-16383, int16Samples[1], 1);
            Assert.AreEqual(0, int16Samples[2]);
            Assert.AreEqual(16383, int16Samples[3], 1);
            Assert.AreEqual(32767, int16Samples[4]);
        }

        [Test]
        public void ConvertFloatToInt16_ClipsOutOfRange()
        {
            float[] floatSamples = { -2f, 2f };
            
            var int16Samples = AudioClipBuilder.ConvertFloatToInt16(floatSamples);
            
            Assert.AreEqual(2, int16Samples.Length);
            Assert.AreEqual(-32767, int16Samples[0]);
            Assert.AreEqual(32767, int16Samples[1]);
        }
    }
}