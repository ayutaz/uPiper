using NUnit.Framework;
using uPiper.Core;

namespace uPiper.Tests.Runtime.Core
{
    public class PiperVoiceConfigTest
    {
        [Test]
        public void FromModelPath_ParsesFileNameCorrectly()
        {
            // Act
            var config = PiperVoiceConfig.FromModelPath(
                "path/to/ja_JP-test-medium.onnx",
                "path/to/ja_JP-test-medium.json"
            );
            
            // Assert
            Assert.AreEqual("ja_JP-test-medium", config.VoiceId);
            Assert.AreEqual("ja", config.Language);
            Assert.AreEqual("ja JP test medium", config.DisplayName);
            Assert.AreEqual("path/to/ja_JP-test-medium.onnx", config.ModelPath);
            Assert.AreEqual("path/to/ja_JP-test-medium.json", config.ConfigPath);
        }
        
        [Test]
        public void FromModelPath_HandlesEnglishModel()
        {
            // Act
            var config = PiperVoiceConfig.FromModelPath(
                "models/en_US-amy-high.onnx",
                "models/en_US-amy-high.json"
            );
            
            // Assert
            Assert.AreEqual("en_US-amy-high", config.VoiceId);
            Assert.AreEqual("en", config.Language);
            Assert.AreEqual("en US amy high", config.DisplayName);
        }
        
        [Test]
        public void Validate_ReturnsTrueForValidConfig()
        {
            // Arrange
            var config = new PiperVoiceConfig
            {
                VoiceId = "test-voice",
                ModelPath = "/path/to/model.onnx",
                Language = "ja"
            };
            
            // Act & Assert
            Assert.IsTrue(config.Validate());
        }
        
        [Test]
        public void Validate_ReturnsFalseForMissingVoiceId()
        {
            // Arrange
            var config = new PiperVoiceConfig
            {
                ModelPath = "/path/to/model.onnx",
                Language = "ja"
            };
            
            // Act & Assert
            Assert.IsFalse(config.Validate());
        }
        
        [Test]
        public void Validate_ReturnsFalseForMissingModelPath()
        {
            // Arrange
            var config = new PiperVoiceConfig
            {
                VoiceId = "test-voice",
                Language = "ja"
            };
            
            // Act & Assert
            Assert.IsFalse(config.Validate());
        }
        
        [Test]
        public void Validate_ReturnsFalseForMissingLanguage()
        {
            // Arrange
            var config = new PiperVoiceConfig
            {
                VoiceId = "test-voice",
                ModelPath = "/path/to/model.onnx"
            };
            
            // Act & Assert
            Assert.IsFalse(config.Validate());
        }
        
        [Test]
        public void ToString_ReturnsFormattedString()
        {
            // Arrange
            var config = new PiperVoiceConfig
            {
                DisplayName = "Test Voice",
                Language = "ja"
            };
            
            // Act
            var result = config.ToString();
            
            // Assert
            Assert.AreEqual("Test Voice (ja)", result);
        }
    }
}