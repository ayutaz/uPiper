using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core;
using uPiper.Tests.Runtime.Helpers;

namespace uPiper.Tests.Runtime.Core
{
    public class PiperTTSTest
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
        
        #region Initialization Tests
        
        [UnityTest]
        public IEnumerator InitializeAsync_Success()
        {
            // Arrange
            bool eventFired = false;
            bool eventResult = false;
            _piperTTS.OnInitialized += result => 
            {
                eventFired = true;
                eventResult = result;
            };
            
            // Act
            yield return AsyncTestHelpers.RunAsync(_piperTTS.InitializeAsync());
            
            // Assert
            Assert.IsTrue(_piperTTS.IsInitialized);
            Assert.IsTrue(eventFired);
            Assert.IsTrue(eventResult);
        }
        
        [UnityTest]
        public IEnumerator InitializeAsync_AlreadyInitialized_DoesNothing()
        {
            // Arrange
            yield return AsyncTestHelpers.RunAsync(_piperTTS.InitializeAsync());
            
            int eventCount = 0;
            _piperTTS.OnInitialized += _ => eventCount++;
            
            // Act
            LogAssert.Expect(LogType.Warning, "[uPiper] PiperTTS is already initialized");
            yield return AsyncTestHelpers.RunAsync(_piperTTS.InitializeAsync());
            
            // Assert
            Assert.AreEqual(0, eventCount); // Event should not fire again
        }
        
        [UnityTest]
        public IEnumerator InitializeAsync_Cancellation_ThrowsOperationCanceledException()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            
            // Act & Assert
            yield return AsyncTestHelpers.RunAsyncExpectException<OperationCanceledException>(
                _piperTTS.InitializeAsync(cts.Token));
            
            Assert.IsFalse(_piperTTS.IsInitialized);
        }
        
        #endregion
        
        #region Voice Management Tests
        
        [UnityTest]
        public IEnumerator LoadVoiceAsync_Success()
        {
            // Arrange
            yield return AsyncTestHelpers.RunAsync(_piperTTS.InitializeAsync());
            
            var voice = new PiperVoiceConfig
            {
                VoiceId = "test-voice",
                ModelPath = "test.onnx",
                Language = "ja",
                SampleRate = 22050
            };
            
            bool eventFired = false;
            PiperVoiceConfig loadedVoice = null;
            _piperTTS.OnVoiceLoaded += v =>
            {
                eventFired = true;
                loadedVoice = v;
            };
            
            // Act
            yield return AsyncTestHelpers.RunAsync(_piperTTS.LoadVoiceAsync(voice));
            
            // Assert
            Assert.Contains("test-voice", _piperTTS.AvailableVoices.ToList());
            Assert.AreEqual("test-voice", _piperTTS.CurrentVoiceId); // First loaded voice becomes current
            Assert.IsTrue(eventFired);
            Assert.AreEqual(voice.VoiceId, loadedVoice.VoiceId);
            
            // Check GetAvailableVoices()
            var availableVoices = _piperTTS.GetAvailableVoices();
            Assert.AreEqual(1, availableVoices.Count);
            Assert.AreEqual("test-voice", availableVoices[0].VoiceId);
        }
        
        [UnityTest]
        public IEnumerator LoadVoiceAsync_NotInitialized_ThrowsInvalidOperationException()
        {
            // Arrange
            var voice = new PiperVoiceConfig
            {
                VoiceId = "test-voice",
                ModelPath = "test.onnx",
                Language = "ja",
                SampleRate = 22050
            };
            
            // Act & Assert
            yield return AsyncTestHelpers.RunAsyncExpectException<InvalidOperationException>(
                _piperTTS.LoadVoiceAsync(voice));
        }
        
        [UnityTest]
        public IEnumerator LoadVoiceAsync_NullVoice_ThrowsArgumentNullException()
        {
            // Arrange
            yield return AsyncTestHelpers.RunAsync(_piperTTS.InitializeAsync());
            
            // Act & Assert
            yield return AsyncTestHelpers.RunAsyncExpectException<ArgumentNullException>(
                _piperTTS.LoadVoiceAsync(null));
        }
        
        [UnityTest]
        public IEnumerator LoadVoiceAsync_InvalidVoice_ThrowsPiperException()
        {
            // Arrange
            yield return AsyncTestHelpers.RunAsync(_piperTTS.InitializeAsync());
            
            var invalidVoice = new PiperVoiceConfig
            {
                VoiceId = "", // Invalid
                ModelPath = "test.onnx",
                Language = "ja",
                SampleRate = 22050
            };
            
            // Act & Assert
            yield return AsyncTestHelpers.RunAsyncExpectException<PiperException>(
                _piperTTS.LoadVoiceAsync(invalidVoice));
        }
        
        [UnityTest]
        public IEnumerator SetCurrentVoice_Success()
        {
            // Arrange
            yield return AsyncTestHelpers.RunAsync(_piperTTS.InitializeAsync());
            
            yield return AsyncTestHelpers.RunAsync(_piperTTS.LoadVoiceAsync(new PiperVoiceConfig
            {
                VoiceId = "voice1",
                ModelPath = "test1.onnx",
                Language = "ja",
                SampleRate = 22050
            }));
            
            yield return AsyncTestHelpers.RunAsync(_piperTTS.LoadVoiceAsync(new PiperVoiceConfig
            {
                VoiceId = "voice2",
                ModelPath = "test2.onnx",
                Language = "en",
                SampleRate = 22050
            }));
            
            // Act
            _piperTTS.SetCurrentVoice("voice2");
            
            // Assert
            Assert.AreEqual("voice2", _piperTTS.CurrentVoiceId);
        }
        
        [Test]
        public void SetCurrentVoice_NotInitialized_ThrowsInvalidOperationException()
        {
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _piperTTS.SetCurrentVoice("test"));
        }
        
        [UnityTest]
        public IEnumerator SetCurrentVoice_UnknownVoice_ThrowsPiperException()
        {
            // Arrange
            yield return AsyncTestHelpers.RunAsync(_piperTTS.InitializeAsync());
            
            // Act & Assert
            Assert.Throws<PiperException>(() => _piperTTS.SetCurrentVoice("unknown"));
        }
        
        [UnityTest]
        public IEnumerator GetVoiceConfig_Success()
        {
            // Arrange
            yield return AsyncTestHelpers.RunAsync(_piperTTS.InitializeAsync());
            
            var voice = new PiperVoiceConfig
            {
                VoiceId = "test-voice",
                ModelPath = "test.onnx",
                Language = "ja",
                SampleRate = 22050
            };
            yield return AsyncTestHelpers.RunAsync(_piperTTS.LoadVoiceAsync(voice));
            
            // Act
            var retrieved = _piperTTS.GetVoiceConfig("test-voice");
            
            // Assert
            Assert.AreEqual(voice.VoiceId, retrieved.VoiceId);
            Assert.AreEqual(voice.ModelPath, retrieved.ModelPath);
            Assert.AreEqual(voice.Language, retrieved.Language);
        }
        
        [Test]
        public void GetVoiceConfig_UnknownVoice_ThrowsPiperException()
        {
            // Act & Assert
            Assert.Throws<PiperException>(() => _piperTTS.GetVoiceConfig("unknown"));
        }
        
        #endregion
        
        #region TTS Stub Tests
        
        [UnityTest]
        public IEnumerator GenerateAudioAsync_NoVoiceLoaded_ThrowsInvalidOperationException()
        {
            // Arrange
            yield return AsyncTestHelpers.RunAsync(_piperTTS.InitializeAsync());
            
            // Act & Assert
            yield return AsyncTestHelpers.RunAsyncExpectException<InvalidOperationException>(
                _piperTTS.GenerateAudioAsync("test"));
        }
        
        [UnityTest]
        public IEnumerator GenerateAudioAsync_WithVoice_ReturnsAudioClip()
        {
            // Arrange
            yield return AsyncTestHelpers.RunAsync(_piperTTS.InitializeAsync());
            
            yield return AsyncTestHelpers.RunAsync(_piperTTS.LoadVoiceAsync(new PiperVoiceConfig
            {
                VoiceId = "test-voice",
                ModelPath = "test.onnx",
                Language = "ja",
                SampleRate = 22050
            }));
            
            // Act
            AudioClip audioClip = null;
            yield return AsyncTestHelpers.RunAsync(
                _piperTTS.GenerateAudioAsync("Hello, world!"),
                result => audioClip = result);
            
            // Assert
            Assert.IsNotNull(audioClip);
            Assert.AreEqual(22050, audioClip.frequency);
            Assert.AreEqual(1, audioClip.channels); // Mono
            Assert.Greater(audioClip.samples, 0);
        }
        
        [UnityTest]
        public IEnumerator GenerateAudio_WithVoice_ReturnsAudioClip()
        {
            // Arrange
            yield return AsyncTestHelpers.RunAsync(_piperTTS.InitializeAsync());
            
            yield return AsyncTestHelpers.RunAsync(_piperTTS.LoadVoiceAsync(new PiperVoiceConfig
            {
                VoiceId = "test-voice",
                ModelPath = "test.onnx",
                Language = "ja",
                SampleRate = 22050
            }));
            
            // Act
            var audioClip = _piperTTS.GenerateAudio("test");
            
            // Assert
            Assert.IsNotNull(audioClip);
        }
        
        [UnityTest]
        public IEnumerator StreamAudioAsync_WithVoice_ReturnsChunks()
        {
            // Arrange
            yield return AsyncTestHelpers.RunAsync(_piperTTS.InitializeAsync());
            
            yield return AsyncTestHelpers.RunAsync(_piperTTS.LoadVoiceAsync(new PiperVoiceConfig
            {
                VoiceId = "test-voice",
                ModelPath = "test.onnx",
                Language = "ja",
                SampleRate = 22050
            }));
            
            // Act
            var chunks = new List<AudioChunk>();
            yield return AsyncTestHelpers.ConsumeAsyncEnumerable(
                _piperTTS.StreamAudioAsync("Hello. World!"),
                chunks);
            
            // Assert
            Assert.Greater(chunks.Count, 0);
            Assert.IsTrue(chunks.Last().IsFinal);
            Assert.AreEqual(22050, chunks.First().SampleRate);
        }
        
        [UnityTest]
        public IEnumerator GenerateAudioAsync_WithVoiceConfig_Overload()
        {
            // Arrange
            yield return AsyncTestHelpers.RunAsync(_piperTTS.InitializeAsync());
            
            var voice = new PiperVoiceConfig
            {
                VoiceId = "test-voice-2",
                ModelPath = "test2.onnx",
                Language = "en",
                SampleRate = 16000
            };
            
            // Act
            AudioClip audioClip = null;
            yield return AsyncTestHelpers.RunAsync(
                _piperTTS.GenerateAudioAsync("Hello", voice),
                result => audioClip = result);
            
            // Assert
            Assert.IsNotNull(audioClip);
            Assert.AreEqual(16000, audioClip.frequency); // Should use the provided voice's sample rate
        }
        
        [UnityTest]
        public IEnumerator GenerateAudio_WithVoiceConfig_Overload()
        {
            // Arrange
            yield return AsyncTestHelpers.RunAsync(_piperTTS.InitializeAsync());
            
            var voice = new PiperVoiceConfig
            {
                VoiceId = "test-voice-3",
                ModelPath = "test3.onnx",
                Language = "en",
                SampleRate = 44100
            };
            
            // Act
            var audioClip = _piperTTS.GenerateAudio("Test", voice);
            
            // Assert
            Assert.IsNotNull(audioClip);
        }
        
        [UnityTest]
        public IEnumerator StreamAudioAsync_WithVoiceConfig_Overload()
        {
            // Arrange
            yield return AsyncTestHelpers.RunAsync(_piperTTS.InitializeAsync());
            
            var voice = new PiperVoiceConfig
            {
                VoiceId = "test-voice-4",
                ModelPath = "test4.onnx",
                Language = "ja",
                SampleRate = 48000
            };
            
            // Act
            var chunks = new List<AudioChunk>();
            yield return AsyncTestHelpers.ConsumeAsyncEnumerable(
                _piperTTS.StreamAudioAsync("Test", voice),
                chunks);
            
            // Assert
            Assert.Greater(chunks.Count, 0);
            Assert.AreEqual(48000, chunks.First().SampleRate);
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
        
        [UnityTest]
        public IEnumerator PreloadTextAsync_WithVoice_UpdatesCache()
        {
            // Arrange
            yield return AsyncTestHelpers.RunAsync(_piperTTS.InitializeAsync());
            
            yield return AsyncTestHelpers.RunAsync(_piperTTS.LoadVoiceAsync(new PiperVoiceConfig
            {
                VoiceId = "test-voice",
                ModelPath = "test.onnx",
                Language = "ja",
                SampleRate = 22050
            }));
            
            // Act
            yield return AsyncTestHelpers.RunAsync(
                _piperTTS.PreloadTextAsync("Test text for caching"));
            
            var stats = _piperTTS.GetCacheStatistics();
            
            // Assert
            Assert.AreEqual(1, stats.EntryCount);
            Assert.Greater(stats.TotalSizeBytes, 0);
        }
        
        [UnityTest]
        public IEnumerator PreloadTextAsync_WithoutVoice_ThrowsInvalidOperationException()
        {
            // Arrange
            yield return AsyncTestHelpers.RunAsync(_piperTTS.InitializeAsync());
            
            // Act & Assert
            yield return AsyncTestHelpers.RunAsyncExpectException<InvalidOperationException>(
                _piperTTS.PreloadTextAsync("test"));
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
        
        [UnityTest]
        public IEnumerator Dispose_PreventsOperations()
        {
            // Arrange
            yield return AsyncTestHelpers.RunAsync(_piperTTS.InitializeAsync());
            
            _piperTTS.Dispose();
            
            // Act & Assert
            Assert.Throws<ObjectDisposedException>(() => _piperTTS.ClearCache());
            
            yield return AsyncTestHelpers.RunAsyncExpectException<ObjectDisposedException>(
                _piperTTS.InitializeAsync());
        }
        
        #endregion
        
        #region Thread Safety Tests
        
        [UnityTest]
        public IEnumerator ConcurrentAccess_IsThreadSafe()
        {
            // Arrange
            yield return AsyncTestHelpers.RunAsync(_piperTTS.InitializeAsync());
            
            var tasks = new List<Task>();
            var errors = new List<Exception>();
            
            // Act - Perform multiple operations concurrently
            for (int i = 0; i < 10; i++)
            {
                int index = i;
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        // Load voices
                        await _piperTTS.LoadVoiceAsync(new PiperVoiceConfig
                        {
                            VoiceId = $"voice{index}",
                            ModelPath = $"test{index}.onnx",
                            Language = "ja",
                            SampleRate = 22050
                        });
                        
                        // Get available voices
                        var voices = _piperTTS.AvailableVoices;
                        
                        // Get cache stats
                        var stats = _piperTTS.GetCacheStatistics();
                        
                        // Check initialization
                        var isInit = _piperTTS.IsInitialized;
                    }
                    catch (Exception ex)
                    {
                        lock (errors)
                        {
                            errors.Add(ex);
                        }
                    }
                }));
            }
            
            yield return AsyncTestHelpers.RunAllAsync(tasks.ToArray(), 30f);
            
            // Assert
            Assert.IsEmpty(errors, "No exceptions should occur during concurrent access");
            Assert.AreEqual(10, _piperTTS.AvailableVoices.Count);
        }
        
        #endregion
    }
}