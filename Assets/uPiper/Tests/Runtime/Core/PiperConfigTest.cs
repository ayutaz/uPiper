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
        public void ToValidated_ClampsInvalidCacheSize()
        {
            // Arrange
            var config = new PiperConfig { MaxCacheSizeMB = 5 };

            // Act
            LogAssert.Expect(LogType.Warning,
                "[uPiper] MaxCacheSizeMB too small (5MB), clamped to minimum 10MB");
            var validated = config.ToValidated();

            // Assert
            Assert.AreEqual(5, config.MaxCacheSizeMB);
            Assert.AreEqual(10, validated.Performance.MaxCacheSizeMB);
        }

        [Test]
        public void ToValidated_WarnsAboutNonStandardSampleRate()
        {
            // Arrange
            var config = new PiperConfig { SampleRate = 11025 };

            // Act & Assert (should log warning but not throw)
            Assert.DoesNotThrow(() => config.ToValidated());
        }

        [Test]
        public void ToValidated_AutoDetectsWorkerThreads()
        {
            // Arrange
            var config = new PiperConfig { WorkerThreads = 0 };

            // Act
            var validated = config.ToValidated();

            // Assert
            Assert.AreEqual(0, config.WorkerThreads);
            Assert.Greater(validated.Performance.WorkerThreads, 0);
            Assert.LessOrEqual(validated.Performance.WorkerThreads, SystemInfo.processorCount);
        }

        [TestCase(16000)]
        [TestCase(22050)]
        [TestCase(44100)]
        [TestCase(48000)]
        public void ToValidated_AcceptsStandardSampleRates(int sampleRate)
        {
            // Arrange
            var config = new PiperConfig { SampleRate = sampleRate };

            // Act & Assert
            Assert.DoesNotThrow(() => config.ToValidated());
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
        public void ToValidated_ClampsLargeCacheSize()
        {
            // Arrange
            var config = new PiperConfig { MaxCacheSizeMB = 1000 };

            // Act
            LogAssert.Expect(LogType.Warning,
                "[uPiper] MaxCacheSizeMB too large (1000MB), clamped to maximum 500MB");
            var validated = config.ToValidated();

            // Assert
            Assert.AreEqual(1000, config.MaxCacheSizeMB);
            Assert.AreEqual(500, validated.Performance.MaxCacheSizeMB);
        }

        [TestCase(7999)]
        [TestCase(48001)]
        public void ToValidated_ThrowsForInvalidSampleRate(int sampleRate)
        {
            // Arrange
            var config = new PiperConfig { SampleRate = sampleRate };

            // Act & Assert
            var ex = Assert.Throws<PiperException>(() => config.ToValidated());
            Assert.That(ex.Message, Does.Contain("Invalid sample rate"));
            Assert.That(ex.Message, Does.Contain("Must be between 8000-48000Hz"));
        }

        [Test]
        public void ToValidated_ThrowsForNegativeWorkerThreads()
        {
            // Arrange
            var config = new PiperConfig { WorkerThreads = -1 };

            // Act & Assert
            var ex = Assert.Throws<PiperException>(() => config.ToValidated());
            Assert.That(ex.Message, Does.Contain("Invalid WorkerThreads"));
        }

        [Test]
        public void ToValidated_WarnsForExcessiveWorkerThreads()
        {
            // Arrange
            var config = new PiperConfig { WorkerThreads = 20 };

            // Act
            LogAssert.Expect(LogType.Warning,
                "[uPiper] WorkerThreads (20) exceeds recommended maximum of 16");
            config.ToValidated();

            // Assert
            Assert.AreEqual(20, config.WorkerThreads); // Should not change, just warn
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void ToValidated_ThrowsForInvalidLanguage(string language)
        {
            // Arrange
            var config = new PiperConfig { DefaultLanguage = language };

            // Act & Assert
            var ex = Assert.Throws<PiperException>(() => config.ToValidated());
            Assert.That(ex.Message, Does.Contain("DefaultLanguage cannot be null or empty"));
        }

        [Test]
        public void ToValidated_NormalizesLanguageCode()
        {
            // Arrange
            var config = new PiperConfig { DefaultLanguage = " JA " };

            // Act
            var validated = config.ToValidated();

            // Assert
            Assert.AreEqual(" JA ", config.DefaultLanguage);
            Assert.AreEqual("ja", validated.Language.DefaultLanguage);
        }

        [Test]
        public void ToValidated_WarnsForUnusualLanguageFormat()
        {
            // Arrange
            var config = new PiperConfig { DefaultLanguage = "japanese" };

            // Act
            LogAssert.Expect(LogType.Warning,
                "[uPiper] Unusual language code format: 'japanese'. Expected format: 'ja' or 'ja-JP'");
            config.ToValidated();
        }

        [Test]
        public void ToValidated_ThrowsForNegativeTimeout()
        {
            // Arrange
            var config = new PiperConfig { TimeoutMs = -1 };

            // Act & Assert
            var ex = Assert.Throws<PiperException>(() => config.ToValidated());
            Assert.That(ex.Message, Does.Contain("Invalid TimeoutMs"));
        }

        [Test]
        public void ToValidated_WarnsForShortTimeout()
        {
            // Arrange
            var config = new PiperConfig { TimeoutMs = 500 };

            // Act
            LogAssert.Expect(LogType.Warning,
                "[uPiper] TimeoutMs (500ms) is very short. Recommended minimum: 1000ms");
            config.ToValidated();
        }

        [Test]
        public void ToValidated_ClampsInvalidBatchSize()
        {
            // Arrange
            var config = new PiperConfig { InferenceBatchSize = 0 };

            // Act
            LogAssert.Expect(LogType.Warning,
                "[uPiper] InferenceBatchSize too small (0), clamped to 1");
            var validated = config.ToValidated();

            // Assert
            Assert.AreEqual(0, config.InferenceBatchSize);
            Assert.AreEqual(1, validated.Performance.InferenceBatchSize);
        }

        [Test]
        public void ToValidated_ClampsLargeBatchSize()
        {
            // Arrange
            var config = new PiperConfig { InferenceBatchSize = 50 };

            // Act
            LogAssert.Expect(LogType.Warning,
                "[uPiper] InferenceBatchSize too large (50), clamped to 32");
            var validated = config.ToValidated();

            // Assert
            Assert.AreEqual(50, config.InferenceBatchSize);
            Assert.AreEqual(32, validated.Performance.InferenceBatchSize);
        }

        [Test]
        public void ToValidated_ClampsPositiveRMSLevel()
        {
            // Arrange
            var config = new PiperConfig { NormalizeAudio = true, TargetRMSLevel = 10 };

            // Act
            LogAssert.Expect(LogType.Warning,
                "[uPiper] TargetRMSLevel (10dB) is positive, clamped to 0dB");
            var validated = config.ToValidated();

            // Assert
            Assert.AreEqual(10f, config.TargetRMSLevel);
            Assert.AreEqual(0f, validated.Audio.TargetRMSLevel);
        }

        [Test]
        public void ToValidated_ClampsLowRMSLevel()
        {
            // Arrange
            var config = new PiperConfig { NormalizeAudio = true, TargetRMSLevel = -50 };

            // Act
            LogAssert.Expect(LogType.Warning,
                "[uPiper] TargetRMSLevel (-50dB) is too low, clamped to -40dB");
            var validated = config.ToValidated();

            // Assert
            Assert.AreEqual(-50f, config.TargetRMSLevel);
            Assert.AreEqual(-40f, validated.Audio.TargetRMSLevel);
        }

        [Test]
        public void ToValidated_IgnoresRMSLevelWhenNotNormalizing()
        {
            // Arrange
            var config = new PiperConfig { NormalizeAudio = false, TargetRMSLevel = 100 };

            // Act & Assert (no warnings expected)
            var validated = config.ToValidated();
            Assert.AreEqual(100f, config.TargetRMSLevel);
            Assert.AreEqual(100f, validated.Audio.TargetRMSLevel);
        }

        [Test]
        public void ToValidated_LogsSuccessMessage()
        {
            // Arrange
            var config = PiperConfig.CreateDefault();

            // Act
            LogAssert.Expect(LogType.Log,
                "[uPiper] Auto-detected " + Mathf.Max(1, SystemInfo.processorCount - 1) + " worker threads");
            LogAssert.Expect(LogType.Log, "[uPiper] PiperConfig validated successfully");
            config.ToValidated();
        }

        // ================================================================
        // PhonemeSilence validation tests
        // ================================================================

        [Test]
        public void ToValidated_EnablePhonemeSilence_ValidSpec_DoesNotThrow()
        {
            var config = new PiperConfig
            {
                EnablePhonemeSilence = true,
                PhonemeSilenceSpec = "_ 0.5"
            };
            Assert.DoesNotThrow(() => config.ToValidated());
        }

        [Test]
        public void ToValidated_EnablePhonemeSilence_MultipleEntries_DoesNotThrow()
        {
            var config = new PiperConfig
            {
                EnablePhonemeSilence = true,
                PhonemeSilenceSpec = "_ 0.5,# 0.3"
            };
            Assert.DoesNotThrow(() => config.ToValidated());
        }

        [Test]
        public void ToValidated_EnablePhonemeSilence_InvalidSpec_ThrowsPiperException()
        {
            var config = new PiperConfig
            {
                EnablePhonemeSilence = true,
                PhonemeSilenceSpec = "invalid"
            };
            Assert.Throws<PiperException>(() => config.ToValidated());
        }

        [Test]
        public void ToValidated_EnablePhonemeSilence_EmptySpec_ThrowsPiperException()
        {
            var config = new PiperConfig
            {
                EnablePhonemeSilence = true,
                PhonemeSilenceSpec = ""
            };
            Assert.Throws<PiperException>(() => config.ToValidated());
        }

        [Test]
        public void ToValidated_DisablePhonemeSilence_DoesNotThrow()
        {
            var config = new PiperConfig
            {
                EnablePhonemeSilence = false,
                PhonemeSilenceSpec = "_ 0.5"
            };
            Assert.DoesNotThrow(() => config.ToValidated());
        }

        // ================================================================
        // PhonemeSilence default value tests
        // ================================================================

        [Test]
        public void PiperConfig_EnablePhonemeSilence_DefaultFalse()
        {
            var config = new PiperConfig();
            Assert.IsFalse(config.EnablePhonemeSilence);
        }

        [Test]
        public void PiperConfig_PhonemeSilenceSpec_DefaultValue()
        {
            var config = new PiperConfig();
            Assert.AreEqual("_ 0.5", config.PhonemeSilenceSpec);
        }

        // ================================================================
        // Warmup default value tests
        // ================================================================

        [Test]
        public void PiperConfig_EnableWarmup_DefaultFalse()
        {
            var config = new PiperConfig();
            Assert.IsFalse(config.EnableWarmup);
        }

        [Test]
        public void PiperConfig_WarmupIterations_DefaultTwo()
        {
            var config = new PiperConfig();
            Assert.AreEqual(2, config.WarmupIterations);
        }

        [Test]
        public void ToValidated_WarmupEnabled_IterationsZero_ClampsToOne()
        {
            var config = new PiperConfig
            {
                EnableWarmup = true,
                WarmupIterations = 0
            };
            LogAssert.Expect(LogType.Warning,
                "[uPiper] WarmupIterations (0) is less than 1, clamped to 1");
            var validated = config.ToValidated();
            Assert.AreEqual(0, config.WarmupIterations);
            Assert.AreEqual(1, validated.Inference.WarmupIterations);
        }

        [Test]
        public void ToValidated_WarmupDisabled_IterationsZero_NotClamped()
        {
            var config = new PiperConfig
            {
                EnableWarmup = false,
                WarmupIterations = 0
            };
            var validated = config.ToValidated();
            Assert.AreEqual(0, config.WarmupIterations);
            Assert.AreEqual(0, validated.Inference.WarmupIterations);
        }

        // ================================================================
        // New tests: PiperConfig immutability after ToValidated()
        // ================================================================

        [Test]
        public void ToValidated_DoesNotModifyOriginalConfig()
        {
            // Arrange: create a config with values that will trigger clamping/normalization
            var config = new PiperConfig
            {
                DefaultLanguage = " JA ",
                MaxCacheSizeMB = 5,
                WorkerThreads = 0,
                InferenceBatchSize = 50,
                NormalizeAudio = true,
                TargetRMSLevel = 10f,
                EnableWarmup = true,
                WarmupIterations = 0,
                SampleRate = 22050,
                EnablePhonemeCache = true,
                Backend = InferenceBackend.Auto,
                EnableMultiThreadedInference = false,
                EnableDebugLogging = false,
                TimeoutMs = 30000,
                AutoDetectLanguage = false,
                EnablePhonemeSilence = false,
                AllowFallbackToCPU = true
            };
            config.GPUSettings = new GPUInferenceSettings { MaxMemoryMB = 50 };

            // Capture original values
            var origDefaultLanguage = config.DefaultLanguage;
            var origMaxCacheSizeMB = config.MaxCacheSizeMB;
            var origWorkerThreads = config.WorkerThreads;
            var origInferenceBatchSize = config.InferenceBatchSize;
            var origTargetRMSLevel = config.TargetRMSLevel;
            var origWarmupIterations = config.WarmupIterations;
            var origGPUMaxMemoryMB = config.GPUSettings.MaxMemoryMB;

            // Act
            config.ToValidated();

            // Assert: PiperConfig fields must not change
            Assert.AreEqual(origDefaultLanguage, config.DefaultLanguage);
            Assert.AreEqual(origMaxCacheSizeMB, config.MaxCacheSizeMB);
            Assert.AreEqual(origWorkerThreads, config.WorkerThreads);
            Assert.AreEqual(origInferenceBatchSize, config.InferenceBatchSize);
            Assert.AreEqual(origTargetRMSLevel, config.TargetRMSLevel);
            Assert.AreEqual(origWarmupIterations, config.WarmupIterations);
            Assert.AreEqual(origGPUMaxMemoryMB, config.GPUSettings.MaxMemoryMB);
        }

        [Test]
        public void ToValidated_ClampsGPUMaxMemoryMB_Low()
        {
            // Arrange
            var config = new PiperConfig();
            config.GPUSettings = new GPUInferenceSettings { MaxMemoryMB = 50 };

            // Act
            var validated = config.ToValidated();

            // Assert
            Assert.AreEqual(50, config.GPUSettings.MaxMemoryMB);
            Assert.AreEqual(128, validated.Inference.GPUSettings.MaxMemoryMB);
        }

        [Test]
        public void ToValidated_ClampsGPUMaxMemoryMB_High()
        {
            // Arrange
            var config = new PiperConfig();
            config.GPUSettings = new GPUInferenceSettings { MaxMemoryMB = 10000 };

            // Act
            var validated = config.ToValidated();

            // Assert
            Assert.AreEqual(10000, config.GPUSettings.MaxMemoryMB);
            Assert.AreEqual(2048, validated.Inference.GPUSettings.MaxMemoryMB);
        }

        [Test]
        public void ToValidated_Idempotent()
        {
            // Arrange: config with values that trigger clamping
            var config = new PiperConfig
            {
                MaxCacheSizeMB = 5,
                InferenceBatchSize = 50,
                DefaultLanguage = " JA ",
                NormalizeAudio = true,
                TargetRMSLevel = 10f,
                EnableWarmup = true,
                WarmupIterations = 0,
                WorkerThreads = 0
            };
            config.GPUSettings = new GPUInferenceSettings { MaxMemoryMB = 50 };

            // Act: call ToValidated() twice
            var validated1 = config.ToValidated();
            var validated2 = config.ToValidated();

            // Assert: both results must be identical
            Assert.AreEqual(validated1.Language.DefaultLanguage, validated2.Language.DefaultLanguage);
            Assert.AreEqual(validated1.Performance.MaxCacheSizeMB, validated2.Performance.MaxCacheSizeMB);
            Assert.AreEqual(validated1.Performance.WorkerThreads, validated2.Performance.WorkerThreads);
            Assert.AreEqual(validated1.Performance.InferenceBatchSize, validated2.Performance.InferenceBatchSize);
            Assert.AreEqual(validated1.Audio.TargetRMSLevel, validated2.Audio.TargetRMSLevel);
            Assert.AreEqual(validated1.Inference.WarmupIterations, validated2.Inference.WarmupIterations);
            Assert.AreEqual(
                validated1.Inference.GPUSettings.MaxMemoryMB,
                validated2.Inference.GPUSettings.MaxMemoryMB);
        }
    }
}