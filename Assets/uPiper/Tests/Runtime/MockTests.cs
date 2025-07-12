using System;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using uPiper.Core;
using uPiper.Phonemizers;

namespace uPiper.Tests
{
    [TestFixture]
    public class MockTests
    {
        // Mock implementation for testing without actual model loading
        private class MockPiperTTS : IPiperTTS
        {
            private PiperConfig _config;
            private bool _isInitialized;

            public bool IsInitialized => _isInitialized;
            public PiperConfig CurrentConfig => _config;

            public event Action<bool> OnInitializationStateChanged;
            public event Action<string> OnError;

            public async Task InitializeAsync(PiperConfig config)
            {
                await Task.Delay(100); // Simulate some work
                _config = config;
                _isInitialized = true;
                OnInitializationStateChanged?.Invoke(true);
            }

            public async Task<AudioClip> GenerateSpeechAsync(string text, string language = "ja")
            {
                if (!_isInitialized)
                    throw new InvalidOperationException("Not initialized");

                await Task.Delay(100); // Simulate generation time

                // Create a simple audio clip
                var audioClip = AudioClip.Create("MockAudio", 44100, 1, 44100, false);
                var data = new float[44100];
                
                // Generate simple sine wave
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = Mathf.Sin(2 * Mathf.PI * 440 * i / 44100f) * 0.1f;
                }
                
                audioClip.SetData(data, 0);
                return audioClip;
            }

            public void Dispose()
            {
                _isInitialized = false;
                OnInitializationStateChanged?.Invoke(false);
            }
        }

        [Test]
        public async Task MockTTS_InitializeAsync_ShouldSucceed()
        {
            var tts = new MockPiperTTS();
            var config = new PiperConfig
            {
                Language = "ja",
                ModelPath = "mock.onnx"
            };

            bool initialized = false;
            tts.OnInitializationStateChanged += (state) => initialized = state;

            await tts.InitializeAsync(config);

            Assert.IsTrue(initialized);
            Assert.IsTrue(tts.IsInitialized);
            Assert.AreEqual("ja", tts.CurrentConfig.Language);

            tts.Dispose();
        }

        [Test]
        public async Task MockTTS_GenerateSpeech_ShouldReturnAudioClip()
        {
            var tts = new MockPiperTTS();
            var config = new PiperConfig
            {
                Language = "ja",
                ModelPath = "mock.onnx"
            };

            await tts.InitializeAsync(config);

            var audioClip = await tts.GenerateSpeechAsync("テスト", "ja");

            Assert.IsNotNull(audioClip);
            Assert.Greater(audioClip.length, 0);
            Assert.AreEqual(44100, audioClip.frequency);

            tts.Dispose();
        }

        [Test]
        public void MockTTS_GenerateWithoutInit_ShouldThrow()
        {
            var tts = new MockPiperTTS();

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await tts.GenerateSpeechAsync("テスト");
            });
        }
    }
}