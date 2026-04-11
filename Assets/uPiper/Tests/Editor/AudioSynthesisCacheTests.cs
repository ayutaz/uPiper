using NUnit.Framework;
using uPiper.Core.AudioGeneration;

namespace uPiper.Tests.Editor
{
    /// <summary>
    /// Unit tests for <see cref="AudioSynthesisCache"/>.
    /// Covers LRU eviction, key generation, hit/miss counting, and edge cases.
    /// </summary>
    [TestFixture]
    public class AudioSynthesisCacheTests
    {
        private static readonly int[] SamplePhonemeIds = { 1, 2, 3, 4, 5 };
        private static readonly int[] SampleProsody = { 0, 0, 0, 1, 1, 1, 2, 2, 2 };
        private static readonly float[] SampleAudio = { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f };

        // ================================================================
        // GenerateKey tests
        // ================================================================

        [Test]
        public void GenerateKey_SameInput_ReturnsSameHash()
        {
            var key1 = AudioSynthesisCache.GenerateKey(
                SamplePhonemeIds, SampleProsody, 1.0f, 0.667f, 0.8f, 0, 0);
            var key2 = AudioSynthesisCache.GenerateKey(
                SamplePhonemeIds, SampleProsody, 1.0f, 0.667f, 0.8f, 0, 0);

            Assert.AreEqual(key1, key2);
        }

        [Test]
        public void GenerateKey_DifferentInput_ReturnsDifferentHash()
        {
            var key1 = AudioSynthesisCache.GenerateKey(
                new[] { 1, 2, 3 }, null, 1.0f, 0.667f, 0.8f, 0, 0);
            var key2 = AudioSynthesisCache.GenerateKey(
                new[] { 4, 5, 6 }, null, 1.0f, 0.667f, 0.8f, 0, 0);

            Assert.AreNotEqual(key1, key2);
        }

        [Test]
        public void GenerateKey_DifferentParams_ReturnsDifferentHash()
        {
            var key1 = AudioSynthesisCache.GenerateKey(
                SamplePhonemeIds, null, 1.0f, 0.667f, 0.8f, 0, 0);
            var key2 = AudioSynthesisCache.GenerateKey(
                SamplePhonemeIds, null, 1.0f, 0.5f, 0.8f, 0, 0);

            Assert.AreNotEqual(key1, key2);
        }

        [Test]
        public void GenerateKey_NullProsody_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                AudioSynthesisCache.GenerateKey(
                    SamplePhonemeIds, null, 1.0f, 0.667f, 0.8f, 0, 0);
            });
        }

        // ================================================================
        // TryGet tests
        // ================================================================

        [Test]
        public void TryGet_EmptyCache_ReturnsFalse()
        {
            var cache = new AudioSynthesisCache();
            var key = AudioSynthesisCache.GenerateKey(
                SamplePhonemeIds, null, 1.0f, 0.667f, 0.8f, 0, 0);

            var result = cache.TryGet(key, out var samples, out var sampleRate);

            Assert.IsFalse(result);
            Assert.IsNull(samples);
            Assert.AreEqual(0, sampleRate);
        }

        // ================================================================
        // Set + TryGet tests
        // ================================================================

        [Test]
        public void Set_ThenGet_ReturnsCachedData()
        {
            var cache = new AudioSynthesisCache();
            var key = AudioSynthesisCache.GenerateKey(
                SamplePhonemeIds, null, 1.0f, 0.667f, 0.8f, 0, 0);

            cache.Set(key, SampleAudio, 22050);
            var result = cache.TryGet(key, out var samples, out var sampleRate);

            Assert.IsTrue(result);
            Assert.AreEqual(SampleAudio, samples);
            Assert.AreEqual(22050, sampleRate);
        }

        [Test]
        public void Set_ExceedsMaxEntries_EvictsLRU()
        {
            var cache = new AudioSynthesisCache(maxEntries: 2, maxMemoryMB: 100);

            var key1 = 1L;
            var key2 = 2L;
            var key3 = 3L;

            cache.Set(key1, new float[] { 0.1f }, 22050);
            cache.Set(key2, new float[] { 0.2f }, 22050);
            cache.Set(key3, new float[] { 0.3f }, 22050);

            // key1 should have been evicted (LRU)
            Assert.IsFalse(cache.TryGet(key1, out _, out _));
            Assert.IsTrue(cache.TryGet(key2, out _, out _));
            Assert.IsTrue(cache.TryGet(key3, out _, out _));
            Assert.AreEqual(1, cache.EvictionCount);
        }

        [Test]
        public void TryGet_UpdatesLRUOrder()
        {
            var cache = new AudioSynthesisCache(maxEntries: 2, maxMemoryMB: 100);

            var key1 = 1L;
            var key2 = 2L;
            var key3 = 3L;

            cache.Set(key1, new float[] { 0.1f }, 22050);
            cache.Set(key2, new float[] { 0.2f }, 22050);

            // Access key1 to move it to front
            cache.TryGet(key1, out _, out _);

            // Now add key3 — should evict key2 (not key1)
            cache.Set(key3, new float[] { 0.3f }, 22050);

            Assert.IsTrue(cache.TryGet(key1, out _, out _));
            Assert.IsFalse(cache.TryGet(key2, out _, out _));
            Assert.IsTrue(cache.TryGet(key3, out _, out _));
        }

        // ================================================================
        // Clear tests
        // ================================================================

        [Test]
        public void Clear_ResetsAllStats()
        {
            var cache = new AudioSynthesisCache();
            var key = 1L;

            cache.Set(key, SampleAudio, 22050);
            cache.TryGet(key, out _, out _);
            cache.TryGet(999L, out _, out _);
            cache.Clear();

            Assert.AreEqual(0, cache.Count);
            Assert.AreEqual(0, cache.HitCount);
            Assert.AreEqual(0, cache.MissCount);
            Assert.AreEqual(0, cache.EvictionCount);
            Assert.AreEqual(0, cache.CurrentMemoryBytes);
        }

        // ================================================================
        // Edge case tests
        // ================================================================

        [Test]
        public void Set_NullSamples_DoesNotCache()
        {
            var cache = new AudioSynthesisCache();
            cache.Set(1L, null, 22050);

            Assert.AreEqual(0, cache.Count);
        }

        [Test]
        public void Set_EmptySamples_DoesNotCache()
        {
            var cache = new AudioSynthesisCache();
            cache.Set(1L, new float[0], 22050);

            Assert.AreEqual(0, cache.Count);
        }

        [Test]
        public void Set_DuplicateKey_UpdatesEntry()
        {
            var cache = new AudioSynthesisCache();
            var audio1 = new float[] { 0.1f };
            var audio2 = new float[] { 0.9f };

            cache.Set(1L, audio1, 22050);
            cache.Set(1L, audio2, 22050);

            Assert.AreEqual(1, cache.Count);
            cache.TryGet(1L, out var samples, out _);
            Assert.AreEqual(audio2, samples);
        }

        // ================================================================
        // Counter tests
        // ================================================================

        [Test]
        public void Count_ReflectsEntries()
        {
            var cache = new AudioSynthesisCache();

            Assert.AreEqual(0, cache.Count);

            cache.Set(1L, new float[] { 0.1f }, 22050);
            Assert.AreEqual(1, cache.Count);

            cache.Set(2L, new float[] { 0.2f }, 22050);
            Assert.AreEqual(2, cache.Count);
        }

        [Test]
        public void HitMiss_CountsAccurate()
        {
            var cache = new AudioSynthesisCache();
            cache.Set(1L, SampleAudio, 22050);

            cache.TryGet(1L, out _, out _);   // hit
            cache.TryGet(1L, out _, out _);   // hit
            cache.TryGet(999L, out _, out _); // miss

            Assert.AreEqual(2, cache.HitCount);
            Assert.AreEqual(1, cache.MissCount);
        }
    }
}