using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace uPiper.Tests.Runtime.Performance
{
    [TestFixture]
    [Category("Performance")]
    [Category("GCAllocation")]
    public class GCAllocationTests
    {
        [Test]
        public void LRUCache_TryGet_NoGCAllocation()
        {
            var cache = new Core.Phonemizers.LRUCache<string, string>(100);
            
            // Prepare cache with data
            for (int i = 0; i < 50; i++)
            {
                cache.Add($"key{i}", $"value{i}");
            }

            // Test that TryGet doesn't allocate memory
            Assert.That(() =>
            {
                for (int i = 0; i < 10; i++)
                {
                    cache.TryGet($"key{i}", out _);
                }
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void LRUCache_ContainsKey_NoGCAllocation()
        {
            var cache = new Core.Phonemizers.LRUCache<string, string>(100);
            
            // Prepare cache with data
            for (int i = 0; i < 50; i++)
            {
                cache.Add($"key{i}", $"value{i}");
            }

            // Test that ContainsKey doesn't allocate memory
            Assert.That(() =>
            {
                for (int i = 0; i < 10; i++)
                {
                    var exists = cache.ContainsKey($"key{i}");
                }
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void PhonemizerCache_Get_NoGCAllocation()
        {
            var phonemizer = new Core.Phonemizers.JapanesePhonemizerG2p();
            var cache = new Core.Phonemizers.PhonemizerCache(phonemizer, 100);
            
            // Prepare cache with data
            var testPhrases = new[] { "こんにちは", "ありがとう", "さようなら", "おはよう", "こんばんは" };
            foreach (var phrase in testPhrases)
            {
                cache.GetPhonemes(phrase);
            }

            // Test that getting cached phonemes doesn't allocate memory
            Assert.That(() =>
            {
                foreach (var phrase in testPhrases)
                {
                    var phonemes = cache.GetPhonemes(phrase);
                }
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void AudioQueue_TryDequeue_NoGCAllocation()
        {
            var queue = new Core.AudioQueue<float[]>(100);
            var testData = new float[1024];
            
            // Fill queue with test data
            for (int i = 0; i < 50; i++)
            {
                queue.Enqueue(testData);
            }

            // Test that TryDequeue doesn't allocate memory
            Assert.That(() =>
            {
                for (int i = 0; i < 10; i++)
                {
                    queue.TryDequeue(out _);
                }
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void AudioQueue_TryPeek_NoGCAllocation()
        {
            var queue = new Core.AudioQueue<float[]>(100);
            var testData = new float[1024];
            
            // Fill queue with test data
            for (int i = 0; i < 50; i++)
            {
                queue.Enqueue(testData);
            }

            // Test that TryPeek doesn't allocate memory
            Assert.That(() =>
            {
                for (int i = 0; i < 10; i++)
                {
                    queue.TryPeek(out _);
                }
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void CircularBuffer_Read_NoGCAllocation()
        {
            var buffer = new Core.CircularBuffer<float>(4096);
            var testData = new float[512];
            var readBuffer = new float[512];
            
            // Fill buffer with test data
            for (int i = 0; i < testData.Length; i++)
            {
                testData[i] = i * 0.1f;
            }
            buffer.Write(testData);

            // Test that Read doesn't allocate memory when using pre-allocated buffer
            Assert.That(() =>
            {
                buffer.Read(readBuffer, 0, readBuffer.Length);
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void CircularBuffer_Peek_NoGCAllocation()
        {
            var buffer = new Core.CircularBuffer<float>(4096);
            var testData = new float[512];
            var peekBuffer = new float[256];
            
            // Fill buffer with test data
            for (int i = 0; i < testData.Length; i++)
            {
                testData[i] = i * 0.1f;
            }
            buffer.Write(testData);

            // Test that Peek doesn't allocate memory when using pre-allocated buffer
            Assert.That(() =>
            {
                buffer.Peek(peekBuffer, 0, peekBuffer.Length);
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void TextProcessor_Process_MinimalGCAllocation()
        {
            var processor = new Core.TextProcessing.TextProcessor();
            
            // Warm up processor
            processor.Process("テストテキスト");

            // Test that processing simple text has minimal allocation
            // Note: Some allocation may be unavoidable due to string operations
            Assert.That(() =>
            {
                processor.Process("こんにちは世界");
            }, Is.AllocatingGCMemory(NUnit.Framework.Is.LessThan(1000)));
        }

        [Test]
        public void AudioBuffer_GetBuffer_NoGCAllocation()
        {
            var buffer = new Core.AudioBuffer(4096);
            
            // Test that getting buffer reference doesn't allocate
            Assert.That(() =>
            {
                var data = buffer.Data;
                var length = buffer.Length;
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void ModelRunner_ProcessCached_NoGCAllocation()
        {
            // This test would verify that cached model inference doesn't allocate
            // Note: Actual implementation depends on model runner structure
            Assert.Inconclusive("Model runner GC allocation test requires actual model implementation");
        }
    }
}