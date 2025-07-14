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
            var task = _piperTTS.InitializeAsync();
            Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(5)), "Task should complete within timeout");
            
            // Assert
            Assert.IsTrue(task.IsCompleted && !task.IsFaulted && !task.IsCanceled);
            Assert.IsTrue(_piperTTS.IsInitialized);
            Assert.IsTrue(eventFired);
            Assert.IsTrue(eventResult);
        }
        
        [Test]
        public void InitializeAsync_AlreadyInitialized_DoesNothing()
        {
            // Arrange
            var initTask = _piperTTS.InitializeAsync();
            Assert.IsTrue(initTask.Wait(TimeSpan.FromSeconds(5)), "Initial task should complete");
            Assert.IsTrue(initTask.IsCompleted && !initTask.IsFaulted && !initTask.IsCanceled);
            
            int eventCount = 0;
            _piperTTS.OnInitialized += _ => eventCount++;
            
            // Act
            LogAssert.Expect(LogType.Warning, "[uPiper] PiperTTS is already initialized");
            var task = _piperTTS.InitializeAsync();
            Assert.IsTrue(task.Wait(TimeSpan.FromSeconds(5)), "Task should complete within timeout");
            
            // Assert
            Assert.IsTrue(task.IsCompleted && !task.IsFaulted && !task.IsCanceled);
            Assert.AreEqual(0, eventCount); // Event should not fire again
        }
        
        [Test]
        public void InitializeAsync_Cancellation_ThrowsOperationCanceledException()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            
            // Act & Assert
            var task = _piperTTS.InitializeAsync(cts.Token);
            Assert.Throws<AggregateException>(() => task.Wait(TimeSpan.FromSeconds(1)));
            Assert.IsTrue(task.IsFaulted);
            Assert.IsTrue(task.Exception.InnerException is OperationCanceledException);
            Assert.IsFalse(_piperTTS.IsInitialized);
        }
        
        #endregion
        
        #region Voice Management Tests
        
        [Test]
        public void LoadVoiceAsync_Success()
        {
            // Arrange
            var initTask = _piperTTS.InitializeAsync();
            Assert.IsTrue(initTask.Wait(TimeSpan.FromSeconds(5)), "Init task should complete");
            Assert.IsTrue(initTask.IsCompleted && !initTask.IsFaulted && !initTask.IsCanceled);
            
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
            var loadTask = _piperTTS.LoadVoiceAsync(voice);
            Assert.IsTrue(loadTask.Wait(TimeSpan.FromSeconds(5)), "Load task should complete");
            
            // Assert
            Assert.IsTrue(loadTask.IsCompleted && !loadTask.IsFaulted && !loadTask.IsCanceled);
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
            var task = _piperTTS.LoadVoiceAsync(voice);
            Assert.Throws<AggregateException>(() => task.Wait(TimeSpan.FromSeconds(1)));
            Assert.IsTrue(task.IsFaulted);
            Assert.IsTrue(task.Exception.InnerException is InvalidOperationException);
        }
        
        [Test]
        public void LoadVoiceAsync_NullVoice_ThrowsArgumentNullException()
        {
            // Arrange
            var initTask = _piperTTS.InitializeAsync();
            Assert.IsTrue(initTask.Wait(TimeSpan.FromSeconds(5)), "Init task should complete");
            Assert.IsTrue(initTask.IsCompleted && !initTask.IsFaulted && !initTask.IsCanceled);
            
            // Act & Assert
            var task = _piperTTS.LoadVoiceAsync(null);
            Assert.Throws<AggregateException>(() => task.Wait(TimeSpan.FromSeconds(1)));
            Assert.IsTrue(task.IsFaulted);
            Assert.IsTrue(task.Exception.InnerException is ArgumentNullException);
        }
        
        [Test]
        public void LoadVoiceAsync_InvalidVoice_ThrowsPiperException()
        {
            // Arrange
            var initTask = _piperTTS.InitializeAsync();
            Assert.IsTrue(initTask.Wait(TimeSpan.FromSeconds(5)), "Init task should complete");
            Assert.IsTrue(initTask.IsCompleted && !initTask.IsFaulted && !initTask.IsCanceled);
            
            var invalidVoice = new PiperVoiceConfig
            {
                VoiceId = "", // Invalid
                ModelPath = "test.onnx",
                Language = "ja",
                SampleRate = 22050
            };
            
            // Act & Assert
            var task = _piperTTS.LoadVoiceAsync(invalidVoice);
            Assert.Throws<AggregateException>(() => task.Wait(TimeSpan.FromSeconds(1)));
            Assert.IsTrue(task.IsFaulted);
            Assert.IsTrue(task.Exception.InnerException is PiperException);
        }
        
        [Test]
        public void SetCurrentVoice_Success()
        {
            // Arrange
            var initTask = _piperTTS.InitializeAsync();
            Assert.IsTrue(initTask.Wait(TimeSpan.FromSeconds(5)), "Init task should complete");
            Assert.IsTrue(initTask.IsCompleted && !initTask.IsFaulted && !initTask.IsCanceled);
            
            var voice1Task = _piperTTS.LoadVoiceAsync(new PiperVoiceConfig
            {
                VoiceId = "voice1",
                ModelPath = "test1.onnx",
                Language = "ja",
                SampleRate = 22050
            });
            Assert.IsTrue(voice1Task.Wait(TimeSpan.FromSeconds(5)), "Voice1 task should complete");
            Assert.IsTrue(voice1Task.IsCompleted && !voice1Task.IsFaulted && !voice1Task.IsCanceled);
            
            var voice2Task = _piperTTS.LoadVoiceAsync(new PiperVoiceConfig
            {
                VoiceId = "voice2",
                ModelPath = "test2.onnx",
                Language = "en",
                SampleRate = 22050
            });
            Assert.IsTrue(voice2Task.Wait(TimeSpan.FromSeconds(5)), "Voice2 task should complete");
            Assert.IsTrue(voice2Task.IsCompleted && !voice2Task.IsFaulted && !voice2Task.IsCanceled);
            
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
            var initTask = _piperTTS.InitializeAsync();
            Assert.IsTrue(initTask.Wait(TimeSpan.FromSeconds(5)), "Init task should complete");
            Assert.IsTrue(initTask.IsCompleted && !initTask.IsFaulted && !initTask.IsCanceled);
            
            // Act & Assert
            Assert.Throws<PiperException>(() => _piperTTS.SetCurrentVoice("unknown"));
        }
        
        [Test]
        public void GetVoiceConfig_Success()
        {
            // Arrange
            var initTask = _piperTTS.InitializeAsync();
            Assert.IsTrue(initTask.Wait(TimeSpan.FromSeconds(5)), "Init task should complete");
            Assert.IsTrue(initTask.IsCompleted && !initTask.IsFaulted && !initTask.IsCanceled);
            
            var voice = new PiperVoiceConfig
            {
                VoiceId = "test-voice",
                ModelPath = "test.onnx",
                Language = "ja",
                SampleRate = 22050
            };
            var loadTask = _piperTTS.LoadVoiceAsync(voice);
            Assert.IsTrue(loadTask.Wait(TimeSpan.FromSeconds(5)), "Load task should complete");
            Assert.IsTrue(loadTask.IsCompleted && !loadTask.IsFaulted && !loadTask.IsCanceled);
            
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
            var initTask = _piperTTS.InitializeAsync();
            Assert.IsTrue(initTask.Wait(TimeSpan.FromSeconds(5)), "Init task should complete");
            Assert.IsTrue(initTask.IsCompleted && !initTask.IsFaulted && !initTask.IsCanceled);
            
            // Act & Assert
            var task = _piperTTS.GenerateAudioAsync("test");
            Assert.Throws<AggregateException>(() => task.Wait(TimeSpan.FromSeconds(1)));
            Assert.IsTrue(task.IsFaulted);
            Assert.IsTrue(task.Exception.InnerException is InvalidOperationException);
        }
        
        [Test]
        public void GenerateAudioAsync_WithVoice_ReturnsAudioClip()
        {
            // Arrange
            var initTask = _piperTTS.InitializeAsync();
            Assert.IsTrue(initTask.Wait(TimeSpan.FromSeconds(5)), "Init task should complete");
            Assert.IsTrue(initTask.IsCompleted && !initTask.IsFaulted && !initTask.IsCanceled);
            
            var voiceTask = _piperTTS.LoadVoiceAsync(new PiperVoiceConfig
            {
                VoiceId = "test-voice",
                ModelPath = "test.onnx",
                Language = "ja",
                SampleRate = 22050
            });
            Assert.IsTrue(voiceTask.Wait(TimeSpan.FromSeconds(5)), "Voice task should complete");
            Assert.IsTrue(voiceTask.IsCompleted && !voiceTask.IsFaulted && !voiceTask.IsCanceled);
            
            // Act
            var audioTask = _piperTTS.GenerateAudioAsync("Hello, world!");
            Assert.IsTrue(audioTask.Wait(TimeSpan.FromSeconds(5)), "Audio task should complete");
            
            // Assert
            Assert.IsTrue(audioTask.IsCompleted && !audioTask.IsFaulted && !audioTask.IsCanceled);
            var audioClip = audioTask.Result;
            Assert.IsNotNull(audioClip);
            Assert.AreEqual(22050, audioClip.frequency);
            Assert.AreEqual(1, audioClip.channels); // Mono
            Assert.Greater(audioClip.samples, 0);
        }
        
        [Test]
        public void GenerateAudio_WithVoice_ReturnsAudioClip()
        {
            // Arrange
            var initTask = _piperTTS.InitializeAsync();
            Assert.IsTrue(initTask.Wait(TimeSpan.FromSeconds(5)), "Init task should complete");
            Assert.IsTrue(initTask.IsCompleted && !initTask.IsFaulted && !initTask.IsCanceled);
            
            var voiceTask = _piperTTS.LoadVoiceAsync(new PiperVoiceConfig
            {
                VoiceId = "test-voice",
                ModelPath = "test.onnx",
                Language = "ja",
                SampleRate = 22050
            });
            Assert.IsTrue(voiceTask.Wait(TimeSpan.FromSeconds(5)), "Voice task should complete");
            Assert.IsTrue(voiceTask.IsCompleted && !voiceTask.IsFaulted && !voiceTask.IsCanceled);
            
            // Act
            var audioClip = _piperTTS.GenerateAudio("test");
            
            // Assert
            Assert.IsNotNull(audioClip);
        }
        
        [Test]
        public void StreamAudioAsync_WithVoice_ReturnsChunks()
        {
            // Arrange
            var initTask = _piperTTS.InitializeAsync();
            Assert.IsTrue(initTask.Wait(TimeSpan.FromSeconds(5)), "Init task should complete");
            Assert.IsTrue(initTask.IsCompleted && !initTask.IsFaulted && !initTask.IsCanceled);
            
            var voiceTask = _piperTTS.LoadVoiceAsync(new PiperVoiceConfig
            {
                VoiceId = "test-voice",
                ModelPath = "test.onnx",
                Language = "ja",
                SampleRate = 22050
            });
            Assert.IsTrue(voiceTask.Wait(TimeSpan.FromSeconds(5)), "Voice task should complete");
            Assert.IsTrue(voiceTask.IsCompleted && !voiceTask.IsFaulted && !voiceTask.IsCanceled);
            
            // Act
            var chunks = new List<AudioChunk>();
            var enumerator = _piperTTS.StreamAudioAsync("Hello. World!").GetAsyncEnumerator();
            
            while (true)
            {
                var moveNextTask = enumerator.MoveNextAsync().AsTask();
                if (!moveNextTask.Wait(TimeSpan.FromSeconds(5)))
                    break;
                if (!moveNextTask.Result)
                    break;
                chunks.Add(enumerator.Current);
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
            var initTask = _piperTTS.InitializeAsync();
            Assert.IsTrue(initTask.Wait(TimeSpan.FromSeconds(5)), "Init task should complete");
            Assert.IsTrue(initTask.IsCompleted && !initTask.IsFaulted && !initTask.IsCanceled);
            
            var voice = new PiperVoiceConfig
            {
                VoiceId = "test-voice-2",
                ModelPath = "test2.onnx",
                Language = "en",
                SampleRate = 16000
            };
            
            // Act
            var audioTask = _piperTTS.GenerateAudioAsync("Hello", voice);
            Assert.IsTrue(audioTask.Wait(TimeSpan.FromSeconds(5)), "Audio task should complete");
            
            // Assert
            Assert.IsTrue(audioTask.IsCompleted && !audioTask.IsFaulted && !audioTask.IsCanceled);
            var audioClip = audioTask.Result;
            Assert.IsNotNull(audioClip);
            Assert.AreEqual(16000, audioClip.frequency); // Should use the provided voice's sample rate
        }
        
        [Test]
        public void GenerateAudio_WithVoiceConfig_Overload()
        {
            // Arrange
            var initTask = _piperTTS.InitializeAsync();
            Assert.IsTrue(initTask.Wait(TimeSpan.FromSeconds(5)), "Init task should complete");
            Assert.IsTrue(initTask.IsCompleted && !initTask.IsFaulted && !initTask.IsCanceled);
            
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
            var initTask = _piperTTS.InitializeAsync();
            Assert.IsTrue(initTask.Wait(TimeSpan.FromSeconds(5)), "Init task should complete");
            Assert.IsTrue(initTask.IsCompleted && !initTask.IsFaulted && !initTask.IsCanceled);
            
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
            
            while (true)
            {
                var moveNextTask = enumerator.MoveNextAsync().AsTask();
                if (!moveNextTask.Wait(TimeSpan.FromSeconds(5)))
                    break;
                if (!moveNextTask.Result)
                    break;
                chunks.Add(enumerator.Current);
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
            var initTask = _piperTTS.InitializeAsync();
            Assert.IsTrue(initTask.Wait(TimeSpan.FromSeconds(5)), "Init task should complete");
            Assert.IsTrue(initTask.IsCompleted && !initTask.IsFaulted && !initTask.IsCanceled);
            
            var voiceTask = _piperTTS.LoadVoiceAsync(new PiperVoiceConfig
            {
                VoiceId = "test-voice",
                ModelPath = "test.onnx",
                Language = "ja",
                SampleRate = 22050
            });
            Assert.IsTrue(voiceTask.Wait(TimeSpan.FromSeconds(5)), "Voice task should complete");
            Assert.IsTrue(voiceTask.IsCompleted && !voiceTask.IsFaulted && !voiceTask.IsCanceled);
            
            // Act
            var preloadTask = _piperTTS.PreloadTextAsync("Test text for caching");
            Assert.IsTrue(preloadTask.Wait(TimeSpan.FromSeconds(5)), "Preload task should complete");
            Assert.IsTrue(preloadTask.IsCompleted && !preloadTask.IsFaulted && !preloadTask.IsCanceled);
            
            var stats = _piperTTS.GetCacheStatistics();
            
            // Assert
            Assert.AreEqual(1, stats.EntryCount);
            Assert.Greater(stats.TotalSizeBytes, 0);
        }
        
        [Test]
        public void PreloadTextAsync_WithoutVoice_ThrowsInvalidOperationException()
        {
            // Arrange
            var initTask = _piperTTS.InitializeAsync();
            Assert.IsTrue(initTask.Wait(TimeSpan.FromSeconds(5)), "Init task should complete");
            Assert.IsTrue(initTask.IsCompleted && !initTask.IsFaulted && !initTask.IsCanceled);
            
            // Act & Assert
            var task = _piperTTS.PreloadTextAsync("test");
            Assert.Throws<AggregateException>(() => task.Wait(TimeSpan.FromSeconds(1)));
            Assert.IsTrue(task.IsFaulted);
            Assert.IsTrue(task.Exception.InnerException is InvalidOperationException);
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
            var initTask = _piperTTS.InitializeAsync();
            Assert.IsTrue(initTask.Wait(TimeSpan.FromSeconds(5)), "Init task should complete");
            Assert.IsTrue(initTask.IsCompleted && !initTask.IsFaulted && !initTask.IsCanceled);
            
            _piperTTS.Dispose();
            
            // Act & Assert
            Assert.Throws<ObjectDisposedException>(() => _piperTTS.ClearCache());
            
            var task = _piperTTS.InitializeAsync();
            Assert.Throws<AggregateException>(() => task.Wait(TimeSpan.FromSeconds(1)));
            Assert.IsTrue(task.IsFaulted);
            Assert.IsTrue(task.Exception.InnerException is ObjectDisposedException);
        }
        
        #endregion
        
        #region Thread Safety Tests
        
        [Test]
        public void ConcurrentAccess_IsThreadSafe()
        {
            // Arrange
            var initTask = _piperTTS.InitializeAsync();
            Assert.IsTrue(initTask.Wait(TimeSpan.FromSeconds(5)), "Init task should complete");
            Assert.IsTrue(initTask.IsCompleted && !initTask.IsFaulted && !initTask.IsCanceled);
            
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
            
            Assert.IsTrue(Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(30)), "All tasks should complete");
            
            // Assert
            Assert.IsEmpty(errors, "No exceptions should occur during concurrent access");
            Assert.AreEqual(10, _piperTTS.AvailableVoices.Count);
        }
        
        #endregion
    }
}