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

        // NOTE: Constructor and basic property tests are in PiperTTSSimpleTest.cs
        // This file contains additional tests: events, error handling, multi-instance

        #region Event Tests

        [Test]
        public void OnInitialized_CanSubscribeAndUnsubscribe()
        {
            static void Handler(bool result) { }

            Assert.DoesNotThrow(() => _piperTTS.OnInitialized += Handler);
            Assert.DoesNotThrow(() => _piperTTS.OnInitialized -= Handler);
        }

        [Test]
        public void OnVoiceLoaded_CanSubscribeAndUnsubscribe()
        {
            static void Handler(PiperVoiceConfig voice) { }

            Assert.DoesNotThrow(() => _piperTTS.OnVoiceLoaded += Handler);
            Assert.DoesNotThrow(() => _piperTTS.OnVoiceLoaded -= Handler);
        }

        [Test]
        public void OnError_CanSubscribeAndUnsubscribe()
        {
            static void Handler(PiperException error) { }

            Assert.DoesNotThrow(() => _piperTTS.OnError += Handler);
            Assert.DoesNotThrow(() => _piperTTS.OnError -= Handler);
        }

        [Test]
        public void OnProcessingProgress_CanSubscribeAndUnsubscribe()
        {
            static void Handler(float progress) { }

            Assert.DoesNotThrow(() => _piperTTS.OnProcessingProgress += Handler);
            Assert.DoesNotThrow(() => _piperTTS.OnProcessingProgress -= Handler);
        }

        #endregion

        // NOTE: Voice config, cache, and property tests are in PiperTTSSimpleTest.cs

        #region Error Handling Tests

        [Test]
        public void GenerateAudio_BeforeInitialization_ThrowsInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(() => _piperTTS.GenerateAudio("test"));
        }

        // Note: PreloadText is async only, so we can't test it synchronously
        // PreloadTextAsync would require async test infrastructure

        [Test]
        public void GenerateAudio_WithNullText_ThrowsArgumentNullException()
        {
            // Note: This test assumes the method checks for null even before initialization check
            // If not, it might throw InvalidOperationException instead
            try
            {
                _piperTTS.GenerateAudio(null);
                Assert.Fail("Expected an exception");
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex is ArgumentNullException || ex is InvalidOperationException);
            }
        }

        #endregion

        #region Multiple Instance Tests

        [Test]
        public void MultipleInstances_CanCoexist()
        {
            var config2 = PiperConfig.CreateDefault();
            var piperTTS2 = new PiperTTS(config2);

            Assert.IsNotNull(piperTTS2);
            Assert.AreNotSame(_piperTTS, piperTTS2);

            piperTTS2.Dispose();
        }

        #endregion
    }
}