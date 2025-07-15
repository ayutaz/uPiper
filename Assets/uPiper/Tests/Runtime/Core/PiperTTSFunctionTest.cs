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
            Assert.IsFalse(_piperTTS.IsDisposed);
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
        public void IsDisposed_BeforeDispose_ReturnsFalse()
        {
            Assert.IsFalse(_piperTTS.IsDisposed);
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
        public void Dispose_SetsIsDisposedToTrue()
        {
            _piperTTS.Dispose();
            Assert.IsTrue(_piperTTS.IsDisposed);
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
        public void OnVoiceUnloaded_CanSubscribeAndUnsubscribe()
        {
            void Handler(string voiceId) { }
            
            Assert.DoesNotThrow(() => _piperTTS.OnVoiceUnloaded += Handler);
            Assert.DoesNotThrow(() => _piperTTS.OnVoiceUnloaded -= Handler);
        }
        
        [Test]
        public void OnErrorOccurred_CanSubscribeAndUnsubscribe()
        {
            void Handler(PiperException error) { }
            
            Assert.DoesNotThrow(() => _piperTTS.OnErrorOccurred += Handler);
            Assert.DoesNotThrow(() => _piperTTS.OnErrorOccurred -= Handler);
        }
        
        #endregion
        
        #region Voice Config Tests
        
        [Test]
        public void SetVoice_BeforeInitialization_ThrowsInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(() => _piperTTS.SetVoice("test-voice"));
        }
        
        [Test]
        public void UnloadVoice_BeforeInitialization_ThrowsInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(() => _piperTTS.UnloadVoice("test-voice"));
        }
        
        [Test]
        public void UnloadAllVoices_BeforeInitialization_ThrowsInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(() => _piperTTS.UnloadAllVoices());
        }
        
        #endregion
        
        #region Cache Tests
        
        [Test]
        public void GetCacheStatistics_ReturnsValidStatistics()
        {
            var stats = _piperTTS.GetCacheStatistics();
            Assert.IsNotNull(stats);
            Assert.AreEqual(0, stats.CurrentSize);
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
        
        #region Config Tests
        
        [Test]
        public void UpdateConfig_WithValidConfig_DoesNotThrow()
        {
            var newConfig = PiperConfig.CreateDefault();
            Assert.DoesNotThrow(() => _piperTTS.UpdateConfig(newConfig));
        }
        
        [Test]
        public void UpdateConfig_WithNullConfig_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _piperTTS.UpdateConfig(null));
        }
        
        #endregion
    }
}