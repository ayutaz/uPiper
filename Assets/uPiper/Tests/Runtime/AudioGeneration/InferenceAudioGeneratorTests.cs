using System;
using NUnit.Framework;
using UnityEngine;
using uPiper.Core;
using uPiper.Core.AudioGeneration;

namespace uPiper.Tests.Runtime.AudioGeneration
{
    [TestFixture]
    public class InferenceAudioGeneratorTests
    {
        private InferenceAudioGenerator _generator;
        private PiperVoiceConfig _config;

        [SetUp]
        public void Setup()
        {
            _generator = new InferenceAudioGenerator();
            _config = new PiperVoiceConfig
            {
                VoiceId = "test_voice",
                SampleRate = 22050,
                NumSpeakers = 1
            };
        }

        [TearDown]
        public void TearDown()
        {
            _generator?.Dispose();
        }

        [Test]
        public void Constructor_CreatesInstance()
        {
            // Assert
            Assert.IsNotNull(_generator);
            Assert.IsFalse(_generator.IsInitialized);
        }

        [Test]
        public void IsInitialized_BeforeInit_ReturnsFalse()
        {
            // Assert
            Assert.IsFalse(_generator.IsInitialized);
        }

        [Test]
        public void SampleRate_BeforeInit_ReturnsDefault()
        {
            // Assert
            Assert.AreEqual(22050, _generator.SampleRate);
        }

        [Test]
        public void GenerateAudioAsync_NotInitialized_ThrowsException()
        {
            // Arrange
            var phonemeIds = new[] { 1, 3, 4, 5, 2 };

            // Act & Assert
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _generator.GenerateAudioAsync(phonemeIds));
        }

        [Test]
        public void GenerateAudioAsync_NullPhonemeIds_ThrowsException()
        {
            // Note: 実際のSentisモデルがないため、初期化後のテストはモックが必要
            // このテストは引数検証のみをテスト
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await _generator.GenerateAudioAsync(null));
        }

        [Test]
        public void Dispose_MultipleCalls_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                _generator.Dispose();
                _generator.Dispose();
            });
        }

        [Test]
        public void Dispose_AfterDispose_MethodsThrow()
        {
            // Arrange
            _generator.Dispose();
            var phonemeIds = new[] { 1, 2, 3 };

            // Act & Assert
            Assert.ThrowsAsync<ObjectDisposedException>(async () =>
                await _generator.GenerateAudioAsync(phonemeIds));
        }

        // Note: InitializeAsyncの完全なテストには実際のModelAssetが必要
        // CI環境では、モックやテスト用の軽量モデルを使用することを推奨
    }
}