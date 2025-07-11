using NUnit.Framework;
using uPiper.Core;
using Unity.Sentis;

namespace uPiper.Tests
{
    [TestFixture]
    public class PiperConfigTests
    {
        [Test]
        public void DefaultConfig_ShouldHaveValidDefaults()
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
        }

        [Test]
        public void Validate_WithValidConfig_ShouldReturnTrue()
        {
            var config = new PiperConfig
            {
                ModelPath = "path/to/model.onnx",
                SampleRate = 22050,
                Channels = 1
            };

            string errorMessage;
            var isValid = config.Validate(out errorMessage);

            Assert.IsTrue(isValid);
            Assert.IsNull(errorMessage);
        }

        [Test]
        public void Validate_WithoutModelPath_ShouldReturnFalse()
        {
            var config = new PiperConfig
            {
                ModelPath = null
            };

            string errorMessage;
            var isValid = config.Validate(out errorMessage);

            Assert.IsFalse(isValid);
            Assert.AreEqual("Model path is required", errorMessage);
        }

        [Test]
        public void Validate_WithInvalidSampleRate_ShouldReturnFalse()
        {
            var config = new PiperConfig
            {
                ModelPath = "model.onnx",
                SampleRate = 0
            };

            string errorMessage;
            var isValid = config.Validate(out errorMessage);

            Assert.IsFalse(isValid);
            Assert.AreEqual("Sample rate must be positive", errorMessage);
        }

        [Test]
        public void Validate_WithInvalidChannels_ShouldReturnFalse()
        {
            var config = new PiperConfig
            {
                ModelPath = "model.onnx",
                Channels = 3
            };

            string errorMessage;
            var isValid = config.Validate(out errorMessage);

            Assert.IsFalse(isValid);
            Assert.AreEqual("Channels must be 1 (mono) or 2 (stereo)", errorMessage);
        }

        [Test]
        public void Validate_WithInvalidCacheSize_ShouldReturnFalse()
        {
            var config = new PiperConfig
            {
                ModelPath = "model.onnx",
                MaxCacheSizeMB = -1
            };

            string errorMessage;
            var isValid = config.Validate(out errorMessage);

            Assert.IsFalse(isValid);
            Assert.AreEqual("Cache size must be positive", errorMessage);
        }

        [Test]
        public void Validate_WithInvalidTimeout_ShouldReturnFalse()
        {
            var config = new PiperConfig
            {
                ModelPath = "model.onnx",
                TimeoutMs = 0
            };

            string errorMessage;
            var isValid = config.Validate(out errorMessage);

            Assert.IsFalse(isValid);
            Assert.AreEqual("Timeout must be positive", errorMessage);
        }
    }
}