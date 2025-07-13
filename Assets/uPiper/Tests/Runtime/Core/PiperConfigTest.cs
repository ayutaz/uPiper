using NUnit.Framework;
using UnityEngine;
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
    }
}