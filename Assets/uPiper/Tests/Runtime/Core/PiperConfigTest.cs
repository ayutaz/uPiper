using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core;

namespace uPiper.Tests.Runtime.Core
{
    public class PiperConfigTest
    {
        [Test]
        public void CreateDefault_ReturnsValidConfig()
        {
            // Act
            var config = PiperConfig.CreateDefault();

            // Assert
            Assert.IsNotNull(config);
            Assert.IsFalse(config.EnableDebugLogging);
            Assert.AreEqual("ja", config.DefaultLanguage);
            Assert.AreEqual(100, config.MaxCacheSizeMB);
            Assert.IsTrue(config.EnablePhonemeCache);
            Assert.AreEqual(0, config.WorkerThreads);
            Assert.AreEqual(InferenceBackend.Auto, config.Backend);
            Assert.AreEqual(22050, config.SampleRate);
            Assert.IsTrue(config.NormalizeAudio);
            Assert.AreEqual(-20f, config.TargetRMSLevel);
        }

        [Test]
        public void Validate_AdjustsInvalidCacheSize()
        {
            // Arrange
            var config = new PiperConfig { MaxCacheSizeMB = 5 };

            // Act
            config.Validate();

            // Assert
            Assert.AreEqual(10, config.MaxCacheSizeMB);
        }

        [Test]
        public void Validate_WarnsAboutNonStandardSampleRate()
        {
            // Arrange
            var config = new PiperConfig { SampleRate = 11025 };

            // Act & Assert (should log warning but not throw)
            Assert.DoesNotThrow(() => config.Validate());
        }

        [Test]
        public void Validate_SetsWorkerThreadsAutomatically()
        {
            // Arrange
            var config = new PiperConfig { WorkerThreads = 0 };

            // Act
            config.Validate();

            // Assert
            Assert.Greater(config.WorkerThreads, 0);
            Assert.LessOrEqual(config.WorkerThreads, SystemInfo.processorCount);
        }

        [TestCase(16000)]
        [TestCase(22050)]
        [TestCase(44100)]
        [TestCase(48000)]
        public void Validate_AcceptsStandardSampleRates(int sampleRate)
        {
            // Arrange
            var config = new PiperConfig { SampleRate = sampleRate };

            // Act & Assert
            Assert.DoesNotThrow(() => config.Validate());
        }

        [Test]
        public void AdvancedSettings_HaveCorrectDefaults()
        {
            // Arrange
            var config = new PiperConfig();

            // Assert
            Assert.AreEqual(30000, config.TimeoutMs);
            Assert.IsFalse(config.EnableMultiThreadedInference);
            Assert.AreEqual(1, config.InferenceBatchSize);
        }

        [Test]
        public void InferenceBackend_EnumValues()
        {
            // Assert all enum values are defined
            Assert.AreEqual(0, (int)InferenceBackend.Auto);
            Assert.AreEqual(1, (int)InferenceBackend.CPU);
            Assert.AreEqual(2, (int)InferenceBackend.GPUCompute);
            Assert.AreEqual(3, (int)InferenceBackend.GPUPixel);
        }

        // New validation tests
        [Test]
        public void Validate_AdjustsLargeCacheSize()
        {
            // Arrange
            var config = new PiperConfig { MaxCacheSizeMB = 1000 };

            // Act
            LogAssert.Expect(LogType.Warning, "[uPiper] MaxCacheSizeMB too large (1000MB), setting to maximum 500MB");
            config.Validate();

            // Assert
            Assert.AreEqual(500, config.MaxCacheSizeMB);
        }

        [TestCase(7999)]
        [TestCase(48001)]
        public void Validate_ThrowsForInvalidSampleRate(int sampleRate)
        {
            // Arrange
            var config = new PiperConfig { SampleRate = sampleRate };

            // Act & Assert
            var ex = Assert.Throws<PiperException>(() => config.Validate());
            Assert.That(ex.Message, Does.Contain("Invalid sample rate"));
            Assert.That(ex.Message, Does.Contain("Must be between 8000-48000Hz"));
        }

        [Test]
        public void Validate_ThrowsForNegativeWorkerThreads()
        {
            // Arrange
            var config = new PiperConfig { WorkerThreads = -1 };

            // Act & Assert
            var ex = Assert.Throws<PiperException>(() => config.Validate());
            Assert.That(ex.Message, Does.Contain("Invalid WorkerThreads"));
        }

        [Test]
        public void Validate_WarnsForExcessiveWorkerThreads()
        {
            // Arrange
            var config = new PiperConfig { WorkerThreads = 20 };

            // Act
            LogAssert.Expect(LogType.Warning, "[uPiper] WorkerThreads (20) exceeds recommended maximum of 16");
            config.Validate();

            // Assert
            Assert.AreEqual(20, config.WorkerThreads); // Should not change, just warn
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Validate_ThrowsForInvalidLanguage(string language)
        {
            // Arrange
            var config = new PiperConfig { DefaultLanguage = language };

            // Act & Assert
            var ex = Assert.Throws<PiperException>(() => config.Validate());
            Assert.That(ex.Message, Does.Contain("DefaultLanguage cannot be null or empty"));
        }

        [Test]
        public void Validate_NormalizesLanguageCode()
        {
            // Arrange
            var config = new PiperConfig { DefaultLanguage = " JA " };

            // Act
            config.Validate();

            // Assert
            Assert.AreEqual("ja", config.DefaultLanguage);
        }

        [Test]
        public void Validate_WarnsForUnusualLanguageFormat()
        {
            // Arrange
            var config = new PiperConfig { DefaultLanguage = "japanese" };

            // Act
            LogAssert.Expect(LogType.Warning, "[uPiper] Unusual language code format: 'japanese'. Expected format: 'ja' or 'ja-JP'");
            config.Validate();
        }

        [Test]
        public void Validate_ThrowsForNegativeTimeout()
        {
            // Arrange
            var config = new PiperConfig { TimeoutMs = -1 };

            // Act & Assert
            var ex = Assert.Throws<PiperException>(() => config.Validate());
            Assert.That(ex.Message, Does.Contain("Invalid TimeoutMs"));
        }

        [Test]
        public void Validate_WarnsForShortTimeout()
        {
            // Arrange
            var config = new PiperConfig { TimeoutMs = 500 };

            // Act
            LogAssert.Expect(LogType.Warning, "[uPiper] TimeoutMs (500ms) is very short. Recommended minimum: 1000ms");
            config.Validate();
        }

        [Test]
        public void Validate_AdjustsInvalidBatchSize()
        {
            // Arrange
            var config = new PiperConfig { InferenceBatchSize = 0 };

            // Act
            LogAssert.Expect(LogType.Warning, "[uPiper] InferenceBatchSize too small (0), setting to 1");
            config.Validate();

            // Assert
            Assert.AreEqual(1, config.InferenceBatchSize);
        }

        [Test]
        public void Validate_AdjustsLargeBatchSize()
        {
            // Arrange
            var config = new PiperConfig { InferenceBatchSize = 50 };

            // Act
            LogAssert.Expect(LogType.Warning, "[uPiper] InferenceBatchSize too large (50), setting to 32");
            config.Validate();

            // Assert
            Assert.AreEqual(32, config.InferenceBatchSize);
        }

        [Test]
        public void Validate_AdjustsPositiveRMSLevel()
        {
            // Arrange
            var config = new PiperConfig { NormalizeAudio = true, TargetRMSLevel = 10 };

            // Act
            LogAssert.Expect(LogType.Warning, "[uPiper] TargetRMSLevel (10dB) is positive, setting to 0dB");
            config.Validate();

            // Assert
            Assert.AreEqual(0f, config.TargetRMSLevel);
        }

        [Test]
        public void Validate_AdjustsLowRMSLevel()
        {
            // Arrange
            var config = new PiperConfig { NormalizeAudio = true, TargetRMSLevel = -50 };

            // Act
            LogAssert.Expect(LogType.Warning, "[uPiper] TargetRMSLevel (-50dB) is too low, setting to -40dB");
            config.Validate();

            // Assert
            Assert.AreEqual(-40f, config.TargetRMSLevel);
        }

        [Test]
        public void Validate_IgnoresRMSLevelWhenNotNormalizing()
        {
            // Arrange
            var config = new PiperConfig { NormalizeAudio = false, TargetRMSLevel = 100 };

            // Act & Assert (no warnings expected)
            config.Validate();
            Assert.AreEqual(100f, config.TargetRMSLevel); // Should not change
        }

        [Test]
        public void Validate_LogsSuccessMessage()
        {
            // Arrange
            var config = PiperConfig.CreateDefault();

            // Act
            LogAssert.Expect(LogType.Log, "[uPiper] Auto-detected " + Mathf.Max(1, SystemInfo.processorCount - 1) + " worker threads");
            LogAssert.Expect(LogType.Log, "[uPiper] PiperConfig validated successfully");
            config.Validate();
        }
    }
}