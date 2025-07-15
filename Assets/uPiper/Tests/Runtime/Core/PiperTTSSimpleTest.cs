using System;
using NUnit.Framework;
using UnityEngine;
using uPiper.Core;

namespace uPiper.Tests.Runtime.Core
{
    /// <summary>
    /// Simple synchronous tests for PiperTTS that avoid Unity Test Framework async issues
    /// </summary>
    public class PiperTTSSimpleTest
    {
        private PiperTTS _piperTTS;
        private PiperConfig _config;
        
        [SetUp]
        public void SetUp()
        {
            _config = PiperConfig.CreateDefault();
            _piperTTS = new PiperTTS(_config);
        }
        
        [TearDown]
        public void TearDown()
        {
            _piperTTS?.Dispose();
        }
        
        #region Constructor Tests
        
        [Test]
        public void Constructor_WithValidConfig_CreatesInstance()
        {
            // Assert
            Assert.IsNotNull(_piperTTS);
            Assert.AreEqual(_config, _piperTTS.Configuration);
            Assert.IsFalse(_piperTTS.IsInitialized);
            Assert.IsFalse(_piperTTS.IsProcessing);
            Assert.IsNull(_piperTTS.CurrentVoiceId);
            Assert.IsNull(_piperTTS.CurrentVoice);
            Assert.IsEmpty(_piperTTS.AvailableVoices);
        }
        
        [Test]
        public void Constructor_WithNullConfig_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new PiperTTS(null));
        }
        
        [Test]
        public void Constructor_ValidatesConfig()
        {
            // Arrange
            var invalidConfig = new PiperConfig { SampleRate = 100 }; // Invalid sample rate
            
            // Act & Assert
            Assert.Throws<PiperException>(() => new PiperTTS(invalidConfig));
        }
        
        #endregion
        
        #region Property Tests
        
        [Test]
        public void Configuration_ReturnsProvidedConfig()
        {
            // Assert
            Assert.AreEqual(_config, _piperTTS.Configuration);
        }
        
        [Test]
        public void IsInitialized_DefaultsToFalse()
        {
            // Assert
            Assert.IsFalse(_piperTTS.IsInitialized);
        }
        
        [Test]
        public void IsProcessing_DefaultsToFalse()
        {
            // Assert
            Assert.IsFalse(_piperTTS.IsProcessing);
        }
        
        [Test]
        public void CurrentVoiceId_DefaultsToNull()
        {
            // Assert
            Assert.IsNull(_piperTTS.CurrentVoiceId);
        }
        
        [Test]
        public void CurrentVoice_DefaultsToNull()
        {
            // Assert
            Assert.IsNull(_piperTTS.CurrentVoice);
        }
        
        [Test]
        public void AvailableVoices_DefaultsToEmpty()
        {
            // Assert
            Assert.IsEmpty(_piperTTS.AvailableVoices);
        }
        
        #endregion
        
        #region Non-Async Method Tests
        
        [Test]
        public void SetCurrentVoice_NotInitialized_ThrowsInvalidOperationException()
        {
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _piperTTS.SetCurrentVoice("test"));
        }
        
        [Test]
        public void GetVoiceConfig_UnknownVoice_ThrowsPiperException()
        {
            // Act & Assert
            Assert.Throws<PiperException>(() => _piperTTS.GetVoiceConfig("unknown"));
        }
        
        [Test]
        public void GetAvailableVoices_ReturnsEmptyList()
        {
            // Act
            var voices = _piperTTS.GetAvailableVoices();
            
            // Assert
            Assert.IsNotNull(voices);
            Assert.AreEqual(0, voices.Count);
        }
        
        #endregion
        
        #region Cache Tests
        
        [Test]
        public void GetCacheStatistics_ReturnsEmptyStats()
        {
            // Act
            var stats = _piperTTS.GetCacheStatistics();
            
            // Assert
            Assert.AreEqual(0, stats.EntryCount);
            Assert.AreEqual(0, stats.TotalSizeBytes);
            Assert.AreEqual(0, stats.HitCount);
            Assert.AreEqual(0, stats.MissCount);
            Assert.AreEqual(0, stats.EvictionCount);
        }
        
        [Test]
        public void ClearCache_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _piperTTS.ClearCache());
        }
        
        #endregion
        
        #region Disposal Tests
        
        [Test]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                _piperTTS.Dispose();
                _piperTTS.Dispose();
                _piperTTS.Dispose();
            });
        }
        
        [Test]
        public void Dispose_PreventsOperations()
        {
            // Arrange
            _piperTTS.Dispose();
            
            // Act & Assert
            Assert.Throws<ObjectDisposedException>(() => _piperTTS.ClearCache());
            Assert.Throws<ObjectDisposedException>(() => _piperTTS.GetCacheStatistics());
            Assert.Throws<ObjectDisposedException>(() => _piperTTS.GetAvailableVoices());
            Assert.Throws<ObjectDisposedException>(() => _piperTTS.GetVoiceConfig("test"));
            Assert.Throws<ObjectDisposedException>(() => _piperTTS.SetCurrentVoice("test"));
        }
        
        #endregion
    }
}