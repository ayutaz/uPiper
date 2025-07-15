using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using uPiper.Core;

namespace uPiper.Tests.Runtime.Core
{
    /// <summary>
    /// Basic tests for PiperTTS with minimal async usage
    /// </summary>
    public class PiperTTSBasicTest
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
        
        #region Basic Tests
        
        [Test]
        public void Constructor_CreatesInstance()
        {
            Assert.IsNotNull(_piperTTS);
            Assert.IsFalse(_piperTTS.IsInitialized);
        }
        
        [Test]
        public void Initialize_SetsIsInitialized()
        {
            // Act - Use synchronous wait pattern
            var task = _piperTTS.InitializeAsync();
            
            // Simple busy wait with timeout
            var timeout = DateTime.UtcNow.AddSeconds(5);
            while (!task.IsCompleted && DateTime.UtcNow < timeout)
            {
                Thread.Sleep(10);
            }
            
            // Assert
            Assert.IsTrue(task.IsCompleted, "Task should complete");
            Assert.IsFalse(task.IsFaulted, "Task should not fault");
            Assert.IsTrue(_piperTTS.IsInitialized);
        }
        
        [Test]
        public void Initialize_FiresEvent()
        {
            // Arrange
            bool eventFired = false;
            _piperTTS.OnInitialized += result => eventFired = true;
            
            // Act
            var task = _piperTTS.InitializeAsync();
            
            // Simple busy wait
            var timeout = DateTime.UtcNow.AddSeconds(5);
            while (!task.IsCompleted && DateTime.UtcNow < timeout)
            {
                Thread.Sleep(10);
            }
            
            // Assert
            Assert.IsTrue(eventFired);
        }
        
        [Test]
        public void LoadVoice_RequiresInitialization()
        {
            // Arrange
            var voice = new PiperVoiceConfig
            {
                VoiceId = "test",
                ModelPath = "test.onnx",
                Language = "ja",
                SampleRate = 22050
            };
            
            // Act & Assert
            var task = _piperTTS.LoadVoiceAsync(voice);
            
            // Wait briefly
            var timeout = DateTime.UtcNow.AddSeconds(1);
            while (!task.IsCompleted && DateTime.UtcNow < timeout)
            {
                Thread.Sleep(10);
            }
            
            Assert.IsTrue(task.IsFaulted);
            Assert.IsInstanceOf<InvalidOperationException>(task.Exception?.InnerException);
        }
        
        #endregion
    }
}