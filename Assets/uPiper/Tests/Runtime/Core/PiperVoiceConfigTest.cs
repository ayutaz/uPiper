using NUnit.Framework;
using UnityEngine.TestTools;
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
            LogAssert.Expect(UnityEngine.LogType.Error, "[uPiper] Voice ID is required");
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
            LogAssert.Expect(UnityEngine.LogType.Error, "[uPiper] Model path is required");
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
            LogAssert.Expect(UnityEngine.LogType.Error, "[uPiper] Language is required");
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
        
        [Test]
        public void DefaultValues_AreCorrect()
        {
            // Arrange & Act
            var config = new PiperVoiceConfig();
            
            // Assert
            Assert.AreEqual(22050, config.SampleRate);
            Assert.AreEqual(VoiceGender.Neutral, config.Gender);
            Assert.AreEqual(VoiceAge.Adult, config.AgeGroup);
            Assert.AreEqual(SpeakingStyle.Normal, config.Style);
            Assert.AreEqual(ModelQuality.Medium, config.Quality);
            Assert.IsTrue(config.SupportsStreaming);
        }
        
        [Test]
        public void VoiceGender_EnumValues()
        {
            // Assert
            Assert.AreEqual(0, (int)VoiceGender.Neutral);
            Assert.AreEqual(1, (int)VoiceGender.Male);
            Assert.AreEqual(2, (int)VoiceGender.Female);
        }
        
        [Test]
        public void VoiceAge_EnumValues()
        {
            // Assert
            Assert.AreEqual(0, (int)VoiceAge.Child);
            Assert.AreEqual(1, (int)VoiceAge.Teen);
            Assert.AreEqual(2, (int)VoiceAge.Adult);
            Assert.AreEqual(3, (int)VoiceAge.Senior);
        }
        
        [Test]
        public void ModelQuality_EnumValues()
        {
            // Assert
            Assert.AreEqual(0, (int)ModelQuality.Low);
            Assert.AreEqual(1, (int)ModelQuality.Medium);
            Assert.AreEqual(2, (int)ModelQuality.High);
            Assert.AreEqual(3, (int)ModelQuality.Ultra);
        }
        
        [Test]
        public void FromModelPath_HandlesSimpleFileName()
        {
            // Act
            var config = PiperVoiceConfig.FromModelPath(
                "model.onnx",
                "model.json"
            );
            
            // Assert
            Assert.AreEqual("model", config.VoiceId);
            Assert.AreEqual("model", config.DisplayName);
            // Language parsing fails for simple names, which is expected
        }
    }
}