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
        
        [Test]
        public void InitializeAsync_Success()
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
            SyncTestHelpers.RunSync(() => _piperTTS.InitializeAsync());
            
            // Assert
            Assert.IsTrue(_piperTTS.IsInitialized);
            Assert.IsTrue(eventFired);
            Assert.IsTrue(eventResult);
        }
        
        [Test]
        public void InitializeAsync_AlreadyInitialized_DoesNothing()
        {
            // Arrange
            SyncTestHelpers.RunSync(() => _piperTTS.InitializeAsync());
            
            int eventCount = 0;
            _piperTTS.OnInitialized += _ => eventCount++;
            
            // Act
            LogAssert.Expect(LogType.Warning, "[uPiper] PiperTTS is already initialized");
            SyncTestHelpers.RunSync(() => _piperTTS.InitializeAsync());
            
            // Assert
            Assert.AreEqual(0, eventCount); // Event should not fire again
        }
        
        [Test]
        public void InitializeAsync_Cancellation_ThrowsOperationCanceledException()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            
            // Act & Assert
            SyncTestHelpers.RunSyncExpectException<OperationCanceledException>(
                () => _piperTTS.InitializeAsync(cts.Token));
            
            Assert.IsFalse(_piperTTS.IsInitialized);
        }
        
        #endregion
        
        #region Voice Management Tests
        
        [Test]
        public void LoadVoiceAsync_Success()
        {
            // Arrange
            SyncTestHelpers.RunSync(() => _piperTTS.InitializeAsync());
            
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
            SyncTestHelpers.RunSync(() => _piperTTS.LoadVoiceAsync(voice));
            
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
        
        [Test]
        public void LoadVoiceAsync_NotInitialized_ThrowsInvalidOperationException()
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
            SyncTestHelpers.RunSyncExpectException<InvalidOperationException>(
                () => _piperTTS.LoadVoiceAsync(voice));
        }
        
        [Test]
        public void LoadVoiceAsync_NullVoice_ThrowsArgumentNullException()
        {
            // Arrange
            SyncTestHelpers.RunSync(() => _piperTTS.InitializeAsync());
            
            // Act & Assert
            SyncTestHelpers.RunSyncExpectException<ArgumentNullException>(
                () => _piperTTS.LoadVoiceAsync(null));
        }
        
        [Test]
        public void LoadVoiceAsync_InvalidVoice_ThrowsPiperException()
        {
            // Arrange
            SyncTestHelpers.RunSync(() => _piperTTS.InitializeAsync());
            
            var invalidVoice = new PiperVoiceConfig
            {
                VoiceId = "", // Invalid
                ModelPath = "test.onnx",
                Language = "ja",
                SampleRate = 22050
            };
            
            // Act & Assert
            SyncTestHelpers.RunSyncExpectException<PiperException>(
                () => _piperTTS.LoadVoiceAsync(invalidVoice));
        }
        
        [Test]
        public void SetCurrentVoice_Success()
        {
            // Arrange
            SyncTestHelpers.RunSync(() => _piperTTS.InitializeAsync());
            
            SyncTestHelpers.RunSync(() => _piperTTS.LoadVoiceAsync(new PiperVoiceConfig
            {
                VoiceId = "voice1",
                ModelPath = "test1.onnx",
                Language = "ja",
                SampleRate = 22050
            }));
            
            SyncTestHelpers.RunSync(() => _piperTTS.LoadVoiceAsync(new PiperVoiceConfig
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
        
        [Test]
        public void SetCurrentVoice_UnknownVoice_ThrowsPiperException()
        {
            // Arrange
            SyncTestHelpers.RunSync(() => _piperTTS.InitializeAsync());
            
            // Act & Assert
            Assert.Throws<PiperException>(() => _piperTTS.SetCurrentVoice("unknown"));
        }
        
        [Test]
        public void GetVoiceConfig_Success()
        {
            // Arrange
            SyncTestHelpers.RunSync(() => _piperTTS.InitializeAsync());
            
            var voice = new PiperVoiceConfig
            {
                VoiceId = "test-voice",
                ModelPath = "test.onnx",
                Language = "ja",
                SampleRate = 22050
            };
            SyncTestHelpers.RunSync(() => _piperTTS.LoadVoiceAsync(voice));
            
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
        
        [Test]
        public void GenerateAudioAsync_NoVoiceLoaded_ThrowsInvalidOperationException()
        {
            // Arrange
            SyncTestHelpers.RunSync(() => _piperTTS.InitializeAsync());
            
            // Act & Assert
            SyncTestHelpers.RunSyncExpectException<InvalidOperationException>(
                () => _piperTTS.GenerateAudioAsync("test"));
        }
        
        [Test]
        public void GenerateAudioAsync_WithVoice_ReturnsAudioClip()
        {
            // Arrange
            SyncTestHelpers.RunSync(() => _piperTTS.InitializeAsync());
            
            SyncTestHelpers.RunSync(() => _piperTTS.LoadVoiceAsync(new PiperVoiceConfig
            {
                VoiceId = "test-voice",
                ModelPath = "test.onnx",
                Language = "ja",
                SampleRate = 22050
            }));
            
            // Act
            var audioClip = SyncTestHelpers.RunSync(() => _piperTTS.GenerateAudioAsync("Hello, world!"));
            
            // Assert
            Assert.IsNotNull(audioClip);
            Assert.AreEqual(22050, audioClip.frequency);
            Assert.AreEqual(1, audioClip.channels); // Mono
            Assert.Greater(audioClip.samples, 0);
        }
        
        [Test]
        public void GenerateAudio_WithVoice_ReturnsAudioClip()
        {
            // Arrange
            SyncTestHelpers.RunSync(() => _piperTTS.InitializeAsync());
            
            SyncTestHelpers.RunSync(() => _piperTTS.LoadVoiceAsync(new PiperVoiceConfig
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
        
        [Test]
        public void StreamAudioAsync_WithVoice_ReturnsChunks()
        {
            // Arrange
            SyncTestHelpers.RunSync(() => _piperTTS.InitializeAsync());
            
            SyncTestHelpers.RunSync(() => _piperTTS.LoadVoiceAsync(new PiperVoiceConfig
            {
                VoiceId = "test-voice",
                ModelPath = "test.onnx",
                Language = "ja",
                SampleRate = 22050
            }));
            
            // Act
            var chunks = new List<AudioChunk>();
            var enumerator = _piperTTS.StreamAudioAsync("Hello. World!").GetAsyncEnumerator();
            try
            {
                while (SyncTestHelpers.RunSync(() => enumerator.MoveNextAsync().AsTask()))
                {
                    chunks.Add(enumerator.Current);
                }
            }
            finally
            {
                SyncTestHelpers.RunSync(() => enumerator.DisposeAsync().AsTask());
            }
            
            // Assert
            Assert.Greater(chunks.Count, 0);
            Assert.IsTrue(chunks.Last().IsFinal);
            Assert.AreEqual(22050, chunks.First().SampleRate);
        }
        
        [Test]
        public void GenerateAudioAsync_WithVoiceConfig_Overload()
        {
            // Arrange
            SyncTestHelpers.RunSync(() => _piperTTS.InitializeAsync());
            
            var voice = new PiperVoiceConfig
            {
                VoiceId = "test-voice-2",
                ModelPath = "test2.onnx",
                Language = "en",
                SampleRate = 16000
            };
            
            // Act
            var audioClip = SyncTestHelpers.RunSync(() => _piperTTS.GenerateAudioAsync("Hello", voice));
            
            // Assert
            Assert.IsNotNull(audioClip);
            Assert.AreEqual(16000, audioClip.frequency); // Should use the provided voice's sample rate
        }
        
        [Test]
        public void GenerateAudio_WithVoiceConfig_Overload()
        {
            // Arrange
            SyncTestHelpers.RunSync(() => _piperTTS.InitializeAsync());
            
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
        
        [Test]
        public void StreamAudioAsync_WithVoiceConfig_Overload()
        {
            // Arrange
            SyncTestHelpers.RunSync(() => _piperTTS.InitializeAsync());
            
            var voice = new PiperVoiceConfig
            {
                VoiceId = "test-voice-4",
                ModelPath = "test4.onnx",
                Language = "ja",
                SampleRate = 48000
            };
            
            // Act
            var chunks = new List<AudioChunk>();
            var enumerator = _piperTTS.StreamAudioAsync("Test", voice).GetAsyncEnumerator();
            try
            {
                while (SyncTestHelpers.RunSync(() => enumerator.MoveNextAsync().AsTask()))
                {
                    chunks.Add(enumerator.Current);
                }
            }
            finally
            {
                SyncTestHelpers.RunSync(() => enumerator.DisposeAsync().AsTask());
            }
            
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
        
        [Test]
        public void PreloadTextAsync_WithVoice_UpdatesCache()
        {
            // Arrange
            SyncTestHelpers.RunSync(() => _piperTTS.InitializeAsync());
            
            SyncTestHelpers.RunSync(() => _piperTTS.LoadVoiceAsync(new PiperVoiceConfig
            {
                VoiceId = "test-voice",
                ModelPath = "test.onnx",
                Language = "ja",
                SampleRate = 22050
            }));
            
            // Act
            SyncTestHelpers.RunSync(() => _piperTTS.PreloadTextAsync("Test text for caching"));
            
            var stats = _piperTTS.GetCacheStatistics();
            
            // Assert
            Assert.AreEqual(1, stats.EntryCount);
            Assert.Greater(stats.TotalSizeBytes, 0);
        }
        
        [Test]
        public void PreloadTextAsync_WithoutVoice_ThrowsInvalidOperationException()
        {
            // Arrange
            SyncTestHelpers.RunSync(() => _piperTTS.InitializeAsync());
            
            // Act & Assert
            SyncTestHelpers.RunSyncExpectException<InvalidOperationException>(
                () => _piperTTS.PreloadTextAsync("test"));
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
            SyncTestHelpers.RunSync(() => _piperTTS.InitializeAsync());
            
            _piperTTS.Dispose();
            
            // Act & Assert
            Assert.Throws<ObjectDisposedException>(() => _piperTTS.ClearCache());
            
            SyncTestHelpers.RunSyncExpectException<ObjectDisposedException>(
                () => _piperTTS.InitializeAsync());
        }
        
        #endregion
        
        #region Thread Safety Tests
        
        [Test]
        public void ConcurrentAccess_IsThreadSafe()
        {
            // Arrange
            SyncTestHelpers.RunSync(() => _piperTTS.InitializeAsync());
            
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
            
            Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(30));
            
            // Assert
            Assert.IsEmpty(errors, "No exceptions should occur during concurrent access");
            Assert.AreEqual(10, _piperTTS.AvailableVoices.Count);
        }
        
        #endregion
    }
}