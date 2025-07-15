using System;
using NUnit.Framework;
using uPiper.Core;

namespace uPiper.Tests.Runtime.Core
{
    /// <summary>
    /// Simple function tests for PiperTTS without async operations or builds
    /// </summary>
    public class PiperTTSFunctionTest
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
            Assert.IsNotNull(_piperTTS);
            Assert.IsFalse(_piperTTS.IsInitialized);
        }
        
        [Test]
        public void Constructor_WithNullConfig_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new PiperTTS(null));
        }
        
        #endregion
        
        #region Property Tests
        
        [Test]
        public void IsInitialized_BeforeInitialization_ReturnsFalse()
        {
            Assert.IsFalse(_piperTTS.IsInitialized);
        }
        
        [Test]
        public void IsProcessing_BeforeProcessing_ReturnsFalse()
        {
            Assert.IsFalse(_piperTTS.IsProcessing);
        }
        
        [Test]
        public void CurrentVoiceId_BeforeLoadingVoice_ReturnsNull()
        {
            Assert.IsNull(_piperTTS.CurrentVoiceId);
        }
        
        [Test]
        public void AvailableVoices_BeforeLoadingVoices_ReturnsEmpty()
        {
            var voices = _piperTTS.AvailableVoices;
            Assert.IsNotNull(voices);
            Assert.AreEqual(0, voices.Count);
        }
        
        #endregion
        
        #region Dispose Tests
        
        [Test]
        public void Dispose_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _piperTTS.Dispose());
        }
        
        [Test]
        public void Dispose_CalledMultipleTimes_DoesNotThrow()
        {
            _piperTTS.Dispose();
            Assert.DoesNotThrow(() => _piperTTS.Dispose());
            Assert.DoesNotThrow(() => _piperTTS.Dispose());
        }
        
        #endregion
        
        #region Event Tests
        
        [Test]
        public void OnInitialized_CanSubscribeAndUnsubscribe()
        {
            void Handler(bool result) { }
            
            Assert.DoesNotThrow(() => _piperTTS.OnInitialized += Handler);
            Assert.DoesNotThrow(() => _piperTTS.OnInitialized -= Handler);
        }
        
        [Test]
        public void OnVoiceLoaded_CanSubscribeAndUnsubscribe()
        {
            void Handler(PiperVoiceConfig voice) { }
            
            Assert.DoesNotThrow(() => _piperTTS.OnVoiceLoaded += Handler);
            Assert.DoesNotThrow(() => _piperTTS.OnVoiceLoaded -= Handler);
        }
        
        [Test]
        public void OnError_CanSubscribeAndUnsubscribe()
        {
            void Handler(PiperException error) { }
            
            Assert.DoesNotThrow(() => _piperTTS.OnError += Handler);
            Assert.DoesNotThrow(() => _piperTTS.OnError -= Handler);
        }
        
        [Test]
        public void OnProcessingProgress_CanSubscribeAndUnsubscribe()
        {
            void Handler(float progress) { }
            
            Assert.DoesNotThrow(() => _piperTTS.OnProcessingProgress += Handler);
            Assert.DoesNotThrow(() => _piperTTS.OnProcessingProgress -= Handler);
        }
        
        #endregion
        
        #region Voice Config Tests
        
        [Test]
        public void SetCurrentVoice_BeforeInitialization_ThrowsInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(() => _piperTTS.SetCurrentVoice("test-voice"));
        }
        
        [Test]
        public void CurrentVoice_BeforeLoadingVoice_ReturnsNull()
        {
            Assert.IsNull(_piperTTS.CurrentVoice);
        }
        
        #endregion
        
        #region Cache Tests
        
        [Test]
        public void GetCacheStatistics_ReturnsValidStatistics()
        {
            var stats = _piperTTS.GetCacheStatistics();
            Assert.IsNotNull(stats);
            Assert.AreEqual(0, stats.TotalSizeBytes);
            Assert.AreEqual(0, stats.HitCount);
            Assert.AreEqual(0, stats.MissCount);
            Assert.AreEqual(0f, stats.HitRate);
        }
        
        [Test]
        public void ClearCache_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _piperTTS.ClearCache());
        }
        
        #endregion
        
        #region Additional Property Tests
        
        [Test]
        public void CurrentVoice_ReturnsNullWhenNoVoiceLoaded()
        {
            Assert.IsNull(_piperTTS.CurrentVoice);
        }
        
        [Test]
        public void IsProcessing_InitiallyFalse()
        {
            Assert.IsFalse(_piperTTS.IsProcessing);
        }
        
        #endregion
    }
}