using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using uPiper.Core;
using uPiper.Core.AudioGeneration;

namespace uPiper.Tests.Editor
{
    /// <summary>
    /// Concurrency tests for <see cref="PiperTTS"/> and <see cref="AudioSynthesisCache"/>.
    /// Validates guard clause behavior under concurrent access and thread-safety of cache operations.
    /// Since PiperTTS requires Unity runtime initialization (model loading), these tests focus on
    /// API guard level behaviors (without full initialization).
    /// </summary>
    [TestFixture]
    public class ConcurrencyTests
    {
        private PiperConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = new PiperConfig();
        }

        [TearDown]
        public void TearDown()
        {
            _config = null;
        }

        // ================================================================
        // PiperTTS: Concurrent calls before initialization
        // ================================================================

        [Test]
        public void MultipleGenerateAudioAsync_BeforeInit_AllThrowInvalidOperation()
        {
            using var tts = new PiperTTS(_config);

            var tasks = Enumerable.Range(0, 5)
                .Select(_ => Task.Run(async () =>
                    await tts.GenerateAudioAsync("test")))
                .ToArray();

            var ex = Assert.Throws<AggregateException>(() => Task.WaitAll(tasks));
            Assert.That(ex.InnerExceptions, Has.Count.EqualTo(5));
            Assert.That(ex.InnerExceptions,
                Has.All.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void MultipleSynthesizeAsync_BeforeInit_AllThrowInvalidOperation()
        {
            using var tts = new PiperTTS(_config);
            var request = SynthesisRequest.FromPhonemes(new[] { "a", "b" });

            var tasks = Enumerable.Range(0, 5)
                .Select(_ => Task.Run(async () =>
                    await tts.SynthesizeAsync(request)))
                .ToArray();

            var ex = Assert.Throws<AggregateException>(() => Task.WaitAll(tasks));
            Assert.That(ex.InnerExceptions, Has.Count.EqualTo(5));
            Assert.That(ex.InnerExceptions,
                Has.All.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void MultiplePhonemizeAsync_BeforeInit_AllThrowInvalidOperation()
        {
            using var tts = new PiperTTS(_config);

            var tasks = Enumerable.Range(0, 5)
                .Select(_ => Task.Run(async () =>
                    await tts.PhonemizeAsync("test")))
                .ToArray();

            var ex = Assert.Throws<AggregateException>(() => Task.WaitAll(tasks));
            Assert.That(ex.InnerExceptions, Has.Count.EqualTo(5));
            Assert.That(ex.InnerExceptions,
                Has.All.TypeOf<InvalidOperationException>());
        }

        // ================================================================
        // PiperTTS: IsProcessing default state
        // ================================================================

        [Test]
        public void IsProcessing_DefaultFalse()
        {
            using var tts = new PiperTTS(_config);
            Assert.That(tts.IsProcessing, Is.False,
                "IsProcessing should be false on a new instance");
        }

        // ================================================================
        // PiperTTS: Concurrent calls after dispose
        // ================================================================

        [Test]
        public void GenerateAudioAsync_AfterDispose_ConcurrentCalls_AllThrowObjectDisposed()
        {
            var tts = new PiperTTS(_config);
            tts.Dispose();

            var tasks = Enumerable.Range(0, 5)
                .Select(_ => Task.Run(async () =>
                    await tts.GenerateAudioAsync("test")))
                .ToArray();

            var ex = Assert.Throws<AggregateException>(() => Task.WaitAll(tasks));
            Assert.That(ex.InnerExceptions, Has.Count.EqualTo(5));
            Assert.That(ex.InnerExceptions,
                Has.All.TypeOf<ObjectDisposedException>());
        }

        // ================================================================
        // PiperTTS: ClearCache thread safety
        // ================================================================

        [Test]
        public void ClearCache_ThreadSafe_DoesNotThrow()
        {
            using var tts = new PiperTTS(_config);

            var tasks = Enumerable.Range(0, 10)
                .Select(_ => Task.Run(() => tts.ClearCache()))
                .ToArray();

            Assert.DoesNotThrow(() => Task.WaitAll(tasks));
        }

        // ================================================================
        // AudioSynthesisCache: Concurrent Set and TryGet
        // ================================================================

        [Test]
        public void AudioSynthesisCache_ConcurrentSetAndGet_DoesNotCorrupt()
        {
            var cache = new AudioSynthesisCache(maxEntries: 100, maxMemoryMB: 100);
            const int taskCount = 20;

            // Pre-populate keys and data
            var keys = Enumerable.Range(0, taskCount)
                .Select(i => AudioSynthesisCache.GenerateKey(
                    new[] { i, i + 1, i + 2 }, null, 1.0f, 0.667f, 0.8f, 0, 0))
                .ToArray();

            var audioData = Enumerable.Range(0, taskCount)
                .Select(i => Enumerable.Range(0, 100)
                    .Select(j => (float)(i * 100 + j) / 10000f)
                    .ToArray())
                .ToArray();

            // Concurrent Set
            var setTasks = Enumerable.Range(0, taskCount)
                .Select(i => Task.Run(() =>
                    cache.Set(keys[i], audioData[i], 22050)))
                .ToArray();

            Assert.DoesNotThrow(() => Task.WaitAll(setTasks));

            // Concurrent TryGet — verify data integrity for entries that remain in cache
            var getTasks = Enumerable.Range(0, taskCount)
                .Select(i => Task.Run(() =>
                {
                    if (cache.TryGet(keys[i], out var samples, out var sampleRate, out _))
                    {
                        Assert.That(sampleRate, Is.EqualTo(22050),
                            $"SampleRate corrupted for key index {i}");
                        Assert.That(samples, Is.Not.Null,
                            $"Samples null for key index {i}");
                        Assert.That(samples.Length, Is.EqualTo(100),
                            $"Samples length corrupted for key index {i}");
                    }
                }))
                .ToArray();

            Assert.DoesNotThrow(() => Task.WaitAll(getTasks));
        }

        // ================================================================
        // AudioSynthesisCache: Concurrent Set count consistency
        // ================================================================

        [Test]
        public void AudioSynthesisCache_ConcurrentSet_CountConsistent()
        {
            const int maxEntries = 50;
            var cache = new AudioSynthesisCache(maxEntries: maxEntries, maxMemoryMB: 100);
            const int taskCount = 30;

            // Use distinct keys to avoid duplicate-key replacement logic
            var keys = Enumerable.Range(0, taskCount)
                .Select(i => (long)(i + 1))
                .ToArray();

            var setTasks = Enumerable.Range(0, taskCount)
                .Select(i => Task.Run(() =>
                    cache.Set(keys[i], new float[] { 0.1f, 0.2f, 0.3f }, 22050)))
                .ToArray();

            Task.WaitAll(setTasks);

            // After all sets complete, count should be between 1 and min(taskCount, maxEntries)
            // and should not exceed maxEntries
            Assert.That(cache.Count, Is.GreaterThan(0),
                "Cache should contain at least one entry after concurrent sets");
            Assert.That(cache.Count, Is.LessThanOrEqualTo(Math.Min(taskCount, maxEntries)),
                "Cache count should not exceed the lesser of task count and max entries");
        }
    }
}