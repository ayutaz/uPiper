using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core.AudioGeneration;
using uPiper.Core.Phonemizers;

namespace uPiper.Tests.Runtime.AudioGeneration
{
    /// <summary>
    /// Tests for SentisAudioGenerator
    /// </summary>
    public class SentisAudioGeneratorTests
    {
        private SentisAudioGenerator _generator;
        private MockPhonemeEncoder _mockEncoder;
        private MockAudioClipBuilder _mockClipBuilder;

        [SetUp]
        public void SetUp()
        {
            _mockEncoder = new MockPhonemeEncoder();
            _mockClipBuilder = new MockAudioClipBuilder();
            _generator = new SentisAudioGenerator(_mockEncoder, _mockClipBuilder);
        }

        [TearDown]
        public void TearDown()
        {
            _generator?.Dispose();
        }

        [Test]
        public void Constructor_CreatesInstance()
        {
            Assert.IsNotNull(_generator);
            Assert.IsFalse(_generator.IsInitialized);
            Assert.IsNull(_generator.CurrentModel);
        }

        [Test]
        public void Dispose_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _generator.Dispose());
            
            // Disposing again should not throw
            Assert.DoesNotThrow(() => _generator.Dispose());
        }

        [UnityTest]
        public IEnumerator GenerateAudioAsync_ThrowsWhenNotInitialized()
        {
            var phonemeIds = new[] { 1, 2, 3, 4, 5 };
            
            var task = _generator.GenerateAudioAsync(phonemeIds);
            yield return new WaitUntil(() => task.IsCompleted);
            
            Assert.IsTrue(task.IsFaulted);
            Assert.IsInstanceOf<InvalidOperationException>(task.Exception.InnerException);
        }

        [Test]
        public void GetStatistics_ReturnsEmptyStatistics()
        {
            var stats = _generator.GetStatistics();
            
            Assert.IsNotNull(stats);
            Assert.AreEqual(0, stats.TotalGenerations);
            Assert.AreEqual(0, stats.AverageGenerationTimeMs);
            Assert.AreEqual(0, stats.ErrorCount);
        }

        [Test]
        public void Events_CanBeSubscribedAndUnsubscribed()
        {
            bool progressCalled = false;
            bool errorCalled = false;
            
            Action<float> progressHandler = p => progressCalled = true;
            Action<Exception> errorHandler = e => errorCalled = true;
            
            // Subscribe
            _generator.OnProgress += progressHandler;
            _generator.OnError += errorHandler;
            
            // Unsubscribe
            _generator.OnProgress -= progressHandler;
            _generator.OnError -= errorHandler;
            
            // No events should have been called
            Assert.IsFalse(progressCalled);
            Assert.IsFalse(errorCalled);
        }

        /// <summary>
        /// Mock phoneme encoder for testing
        /// </summary>
        private class MockPhonemeEncoder : IPhonemeEncoder
        {
            public int VocabularySize => 128;
            public int PadTokenId => 0;
            public int UnknownTokenId => 1;

            public int[] EncodePhonemes(string[] phonemes)
            {
                if (phonemes == null) return new int[0];
                
                var ids = new int[phonemes.Length];
                for (int i = 0; i < phonemes.Length; i++)
                {
                    ids[i] = (i + 2) % VocabularySize; // Simple encoding
                }
                return ids;
            }

            public int[] EncodePhonemes(PhonemeResult phonemeResult)
            {
                return phonemeResult?.PhonemeIds ?? new int[0];
            }

            public int[] AddPadding(int[] phonemeIds, int targetLength, int padId = 0)
            {
                if (phonemeIds.Length >= targetLength)
                    return phonemeIds;
                
                var padded = new int[targetLength];
                Array.Copy(phonemeIds, padded, phonemeIds.Length);
                for (int i = phonemeIds.Length; i < targetLength; i++)
                {
                    padded[i] = padId;
                }
                return padded;
            }

            public int[] AddSpecialTokens(int[] phonemeIds, int? startToken = null, int? endToken = null)
            {
                return phonemeIds;
            }
        }

        /// <summary>
        /// Mock audio clip builder for testing
        /// </summary>
        private class MockAudioClipBuilder : IAudioClipBuilder
        {
            public AudioClip CreateAudioClip(float[] samples, int sampleRate, int channels = 1, string clipName = "GeneratedAudio")
            {
                return AudioClip.Create(clipName, samples.Length / channels, channels, sampleRate, false);
            }

            public AudioClip CreateAudioClipNormalized(float[] normalizedSamples, int sampleRate, int channels = 1, string clipName = "GeneratedAudio")
            {
                return CreateAudioClip(normalizedSamples, sampleRate, channels, clipName);
            }

            public float[] NormalizeSamples(float[] samples)
            {
                return samples;
            }

            public float[] PostProcess(float[] samples, AudioProcessingOptions options)
            {
                return samples;
            }
        }
    }
}