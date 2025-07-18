using System;
using NUnit.Framework;
using uPiper.Core;

namespace uPiper.Tests.Runtime.Core
{
    public class PiperExceptionTest
    {
        [Test]
        public void PiperException_BasicConstructor()
        {
            // Act
            var ex = new PiperException("Test error");

            // Assert
            Assert.AreEqual("Test error", ex.Message);
            Assert.AreEqual(PiperErrorCode.Unknown, ex.ErrorCode);
            Assert.IsNull(ex.Context);
        }

        [Test]
        public void PiperException_WithInnerException()
        {
            // Arrange
            var inner = new InvalidOperationException("Inner error");

            // Act
            var ex = new PiperException("Outer error", inner);

            // Assert
            Assert.AreEqual("Outer error", ex.Message);
            Assert.AreEqual(inner, ex.InnerException);
            Assert.AreEqual(PiperErrorCode.Unknown, ex.ErrorCode);
        }

        [Test]
        public void PiperException_WithErrorCode()
        {
            // Act
            var ex = new PiperException(PiperErrorCode.ModelLoadFailed, "Failed to load");

            // Assert
            Assert.AreEqual("Failed to load", ex.Message);
            Assert.AreEqual(PiperErrorCode.ModelLoadFailed, ex.ErrorCode);
        }

        [Test]
        public void PiperException_WithContext()
        {
            // Act
            var ex = new PiperException(
                PiperErrorCode.ConfigurationError,
                "Invalid config",
                "SampleRate=99999"
            );

            // Assert
            Assert.AreEqual("Invalid config", ex.Message);
            Assert.AreEqual(PiperErrorCode.ConfigurationError, ex.ErrorCode);
            Assert.AreEqual("SampleRate=99999", ex.Context);
        }

        [Test]
        public void PiperInitializationException_SetsCorrectErrorCode()
        {
            // Act
            var ex = new PiperInitializationException("Init failed");

            // Assert
            Assert.AreEqual("Init failed", ex.Message);
            Assert.AreEqual(PiperErrorCode.InitializationFailed, ex.ErrorCode);
        }

        [Test]
        public void PiperModelLoadException_StoresModelPath()
        {
            // Act
            var ex = new PiperModelLoadException(
                "/path/to/model.onnx",
                "File not found"
            );

            // Assert
            Assert.AreEqual("File not found", ex.Message);
            Assert.AreEqual(PiperErrorCode.ModelLoadFailed, ex.ErrorCode);
            Assert.AreEqual("/path/to/model.onnx", ex.ModelPath);
        }

        [Test]
        public void PiperInferenceException_SetsCorrectErrorCode()
        {
            // Act
            var ex = new PiperInferenceException("Inference failed");

            // Assert
            Assert.AreEqual("Inference failed", ex.Message);
            Assert.AreEqual(PiperErrorCode.InferenceFailed, ex.ErrorCode);
        }

        [Test]
        public void PiperPhonemizationException_StoresInputData()
        {
            // Act
            var ex = new PiperPhonemizationException(
                "こんにちは",
                "ja",
                "Failed to phonemize"
            );

            // Assert
            Assert.AreEqual("Failed to phonemize", ex.Message);
            Assert.AreEqual(PiperErrorCode.PhonemizationFailed, ex.ErrorCode);
            Assert.AreEqual("こんにちは", ex.InputText);
            Assert.AreEqual("ja", ex.Language);
        }

        [Test]
        public void PiperConfigurationException_SetsCorrectErrorCode()
        {
            // Act
            var ex = new PiperConfigurationException("Invalid setting");

            // Assert
            Assert.AreEqual("Invalid setting", ex.Message);
            Assert.AreEqual(PiperErrorCode.ConfigurationError, ex.ErrorCode);
        }

        [Test]
        public void PiperPlatformNotSupportedException_FormatsMessage()
        {
            // Act
            var ex = new PiperPlatformNotSupportedException("WebGL");

            // Assert
            Assert.AreEqual("Platform 'WebGL' is not supported", ex.Message);
            Assert.AreEqual(PiperErrorCode.PlatformNotSupported, ex.ErrorCode);
            Assert.AreEqual("WebGL", ex.Platform);
        }

        [Test]
        public void PiperTimeoutException_FormatsMessage()
        {
            // Act
            var ex = new PiperTimeoutException(5000, "Model loading");

            // Assert
            Assert.AreEqual("Operation 'Model loading' timed out after 5000ms", ex.Message);
            Assert.AreEqual(PiperErrorCode.Timeout, ex.ErrorCode);
            Assert.AreEqual(5000, ex.TimeoutMs);
        }

        [Test]
        public void AllErrorCodes_AreDefined()
        {
            // Arrange
            var errorCodes = Enum.GetValues(typeof(PiperErrorCode));

            // Assert
            Assert.Contains(PiperErrorCode.Unknown, errorCodes);
            Assert.Contains(PiperErrorCode.InitializationFailed, errorCodes);
            Assert.Contains(PiperErrorCode.ModelLoadFailed, errorCodes);
            Assert.Contains(PiperErrorCode.InferenceFailed, errorCodes);
            Assert.Contains(PiperErrorCode.PhonemizationFailed, errorCodes);
            Assert.Contains(PiperErrorCode.ConfigurationError, errorCodes);
            Assert.Contains(PiperErrorCode.PlatformNotSupported, errorCodes);
            Assert.Contains(PiperErrorCode.Timeout, errorCodes);
            Assert.Contains(PiperErrorCode.ResourceNotFound, errorCodes);
            Assert.Contains(PiperErrorCode.InvalidInput, errorCodes);
            Assert.Contains(PiperErrorCode.MemoryError, errorCodes);
            Assert.Contains(PiperErrorCode.CacheFull, errorCodes);
            Assert.Contains(PiperErrorCode.AudioGenerationFailed, errorCodes);
        }
    }
}