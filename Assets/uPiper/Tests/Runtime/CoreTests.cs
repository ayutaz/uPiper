using System;
using NUnit.Framework;
using uPiper.Core;
using Unity.Sentis;

namespace uPiper.Tests
{
    [TestFixture]
    public class CoreTests
    {
        [Test]
        public void PiperConfig_Validation_WithValidConfig_ReturnsTrue()
        {
            var config = new PiperConfig
            {
                ModelPath = "path/to/model.onnx",
                SampleRate = 22050,
                Channels = 1,
                MaxCacheSizeMB = 100,
                TimeoutMs = 5000
            };

            string errorMessage;
            bool isValid = config.Validate(out errorMessage);

            Assert.IsTrue(isValid);
            Assert.IsNull(errorMessage);
        }

        [Test]
        public void PiperConfig_Validation_WithEmptyModelPath_ReturnsFalse()
        {
            var config = new PiperConfig
            {
                ModelPath = ""
            };

            string errorMessage;
            bool isValid = config.Validate(out errorMessage);

            Assert.IsFalse(isValid);
            Assert.AreEqual("Model path is required", errorMessage);
        }

        [Test]
        public void PiperConfig_Validation_WithNullModelPath_ReturnsFalse()
        {
            var config = new PiperConfig
            {
                ModelPath = null
            };

            string errorMessage;
            bool isValid = config.Validate(out errorMessage);

            Assert.IsFalse(isValid);
            Assert.AreEqual("Model path is required", errorMessage);
        }

        [Test]
        public void PiperConfig_Validation_WithInvalidSampleRate_ReturnsFalse()
        {
            var config = new PiperConfig
            {
                ModelPath = "model.onnx",
                SampleRate = 0
            };

            string errorMessage;
            bool isValid = config.Validate(out errorMessage);

            Assert.IsFalse(isValid);
            Assert.AreEqual("Sample rate must be positive", errorMessage);
        }

        [Test]
        public void PiperConfig_Validation_WithNegativeSampleRate_ReturnsFalse()
        {
            var config = new PiperConfig
            {
                ModelPath = "model.onnx",
                SampleRate = -1
            };

            string errorMessage;
            bool isValid = config.Validate(out errorMessage);

            Assert.IsFalse(isValid);
            Assert.AreEqual("Sample rate must be positive", errorMessage);
        }

        [Test]
        public void PiperConfig_Validation_WithInvalidChannels_ReturnsFalse()
        {
            var config = new PiperConfig
            {
                ModelPath = "model.onnx",
                Channels = 3
            };

            string errorMessage;
            bool isValid = config.Validate(out errorMessage);

            Assert.IsFalse(isValid);
            Assert.AreEqual("Channels must be 1 (mono) or 2 (stereo)", errorMessage);
        }

        [Test]
        public void PiperConfig_Validation_WithZeroChannels_ReturnsFalse()
        {
            var config = new PiperConfig
            {
                ModelPath = "model.onnx",
                Channels = 0
            };

            string errorMessage;
            bool isValid = config.Validate(out errorMessage);

            Assert.IsFalse(isValid);
            Assert.AreEqual("Channels must be 1 (mono) or 2 (stereo)", errorMessage);
        }

        [Test]
        public void PiperConfig_Validation_WithInvalidCacheSize_ReturnsFalse()
        {
            var config = new PiperConfig
            {
                ModelPath = "model.onnx",
                MaxCacheSizeMB = 0
            };

            string errorMessage;
            bool isValid = config.Validate(out errorMessage);

            Assert.IsFalse(isValid);
            Assert.AreEqual("Cache size must be positive", errorMessage);
        }

        [Test]
        public void PiperConfig_Validation_WithInvalidTimeout_ReturnsFalse()
        {
            var config = new PiperConfig
            {
                ModelPath = "model.onnx",
                TimeoutMs = 0
            };

            string errorMessage;
            bool isValid = config.Validate(out errorMessage);

            Assert.IsFalse(isValid);
            Assert.AreEqual("Timeout must be positive", errorMessage);
        }

        [Test]
        public void PiperConfig_DefaultValues_AreValid()
        {
            var config = new PiperConfig();

            Assert.AreEqual("ja", config.Language);
            Assert.IsTrue(config.UseCache);
            Assert.AreEqual(100, config.MaxCacheSizeMB);
            Assert.AreEqual(BackendType.GPUCompute, config.SentisBackend);
            Assert.AreEqual(22050, config.SampleRate);
            Assert.AreEqual(1, config.Channels);
            Assert.IsFalse(config.EnableDebugLogging);
            Assert.AreEqual(5000, config.TimeoutMs);
            Assert.IsFalse(config.TestMode);
        }

        [Test]
        public void PiperConfig_SentisBackend_CanBeChanged()
        {
            var config = new PiperConfig
            {
                SentisBackend = BackendType.CPU
            };

            Assert.AreEqual(BackendType.CPU, config.SentisBackend);
        }

        [Test]
        public void PiperConfig_Language_CanBeChanged()
        {
            var config = new PiperConfig
            {
                Language = "en"
            };

            Assert.AreEqual("en", config.Language);
        }

        [Test]
        public void PiperConfig_TestMode_CanBeEnabled()
        {
            var config = new PiperConfig
            {
                TestMode = true
            };

            Assert.IsTrue(config.TestMode);
        }

        [Test]
        public void PiperTTS_CanBeCreated()
        {
            var tts = new PiperTTS();
            
            Assert.IsNotNull(tts);
            Assert.IsFalse(tts.IsInitialized);
            Assert.IsNull(tts.CurrentConfig);
            
            tts.Dispose();
        }

        [Test]
        public void PiperTTS_Dispose_CanBeCalledMultipleTimes()
        {
            var tts = new PiperTTS();
            
            Assert.DoesNotThrow(() =>
            {
                tts.Dispose();
                tts.Dispose();
                tts.Dispose();
            });
        }

        [Test]
        public void PiperTTS_GenerateSpeech_WithoutInit_ThrowsException()
        {
            var tts = new PiperTTS();
            
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await tts.GenerateSpeechAsync("test");
            });
            
            tts.Dispose();
        }

        [Test]
        public void PiperTTS_InitializeAsync_WithInvalidConfig_ThrowsException()
        {
            var tts = new PiperTTS();
            var config = new PiperConfig { ModelPath = null };
            
            Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await tts.InitializeAsync(config);
            });
            
            tts.Dispose();
        }
    }
}