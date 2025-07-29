using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core.Logging;

namespace uPiper.Tests.Runtime.Core
{
    public class PiperLoggerTest
    {
        private PiperLogger.LogLevel _originalLevel;

        [SetUp]
        public void Setup()
        {
            // Save original log level
            _originalLevel = GetCurrentLogLevel();
        }

        [TearDown]
        public void TearDown()
        {
            // Restore original log level
            PiperLogger.SetMinimumLevel(_originalLevel);
        }

        [Test]
        public void LogInfo_WithValidMessage_OutputsToConsole()
        {
            // Arrange
            PiperLogger.SetMinimumLevel(PiperLogger.LogLevel.Info);

            // Act & Assert
            LogAssert.Expect(LogType.Log, "[uPiper] Test info message");
            PiperLogger.LogInfo("Test info message");
        }

        [Test]
        public void LogInfo_WithParameters_FormatsCorrectly()
        {
            // Arrange
            PiperLogger.SetMinimumLevel(PiperLogger.LogLevel.Info);

            // Act & Assert
            LogAssert.Expect(LogType.Log, "[uPiper] Loading model: test.onnx for language: ja");
            PiperLogger.LogInfo("Loading model: {0} for language: {1}", "test.onnx", "ja");
        }

        [Test]
        public void LogWarning_WithValidMessage_OutputsWarning()
        {
            // Arrange
            PiperLogger.SetMinimumLevel(PiperLogger.LogLevel.Warning);

            // Act & Assert
            LogAssert.Expect(LogType.Warning, "[uPiper] Test warning message");
            PiperLogger.LogWarning("Test warning message");
        }

        [Test]
        public void LogError_WithValidMessage_OutputsError()
        {
            // Arrange
            PiperLogger.SetMinimumLevel(PiperLogger.LogLevel.Error);

            // Act & Assert
            LogAssert.Expect(LogType.Error, "[uPiper] Test error message");
            PiperLogger.LogError("Test error message");
        }

        [Test]
        public void LogDebug_InEditor_OutputsDebugMessage()
        {
            // This test only runs in editor or development builds
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Arrange
            PiperLogger.SetMinimumLevel(PiperLogger.LogLevel.Debug);

            // Act & Assert
            LogAssert.Expect(LogType.Log, "[uPiper] Debug message");
            PiperLogger.LogDebug("Debug message");
#else
            // In release builds, LogDebug should not output anything
            Assert.Pass("LogDebug is disabled in release builds");
#endif
        }

        [Test]
        public void SetMinimumLevel_ToWarning_FiltersInfoMessages()
        {
            // Arrange
            PiperLogger.SetMinimumLevel(PiperLogger.LogLevel.Warning);

            // Act
            PiperLogger.LogInfo("This should be filtered");

            // Assert - no log should be output
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SetMinimumLevel_ToError_FiltersWarningMessages()
        {
            // Arrange
            PiperLogger.SetMinimumLevel(PiperLogger.LogLevel.Error);

            // Act
            PiperLogger.LogWarning("This should be filtered");

            // Assert - no log should be output
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SetMinimumLevel_ToDebug_AllowsAllMessages()
        {
            // Arrange
            PiperLogger.SetMinimumLevel(PiperLogger.LogLevel.Debug);

            // Act & Assert
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogAssert.Expect(LogType.Log, "[uPiper] Debug test");
            PiperLogger.LogDebug("Debug test");
#endif

            LogAssert.Expect(LogType.Log, "[uPiper] Info test");
            PiperLogger.LogInfo("Info test");

            LogAssert.Expect(LogType.Warning, "[uPiper] Warning test");
            PiperLogger.LogWarning("Warning test");

            LogAssert.Expect(LogType.Error, "[uPiper] Error test");
            PiperLogger.LogError("Error test");
        }

        [Test]
        public void Initialize_SetsCorrectDefaultLevel()
        {
            // Act
            PiperLogger.Initialize();

            // Assert
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // In development, should allow debug messages
            LogAssert.Expect(LogType.Log, "[uPiper] Debug should work");
            PiperLogger.LogDebug("Debug should work");
#else
            // In release, debug should be filtered
            PiperLogger.SetMinimumLevel(PiperLogger.LogLevel.Info);
            PiperLogger.LogDebug("Debug should be filtered");
            LogAssert.NoUnexpectedReceived();
#endif
        }

        [Test]
        public void LogWithNullMessage_HandlesGracefully()
        {
            // Arrange
            PiperLogger.SetMinimumLevel(PiperLogger.LogLevel.Info);

            // Act & Assert - should not throw
            LogAssert.Expect(LogType.Log, "[uPiper] ");
            PiperLogger.LogInfo(null);
        }

        [Test]
        public void LogWithEmptyParameters_HandlesGracefully()
        {
            // Arrange
            PiperLogger.SetMinimumLevel(PiperLogger.LogLevel.Info);

            // Act & Assert
            LogAssert.Expect(LogType.Log, "[uPiper] Message with {0} parameters");
            PiperLogger.LogInfo("Message with {0} parameters");
        }

        // Helper method to get current log level
        private PiperLogger.LogLevel GetCurrentLogLevel()
        {
            return PiperLogger.MinimumLevel;
        }
    }
}