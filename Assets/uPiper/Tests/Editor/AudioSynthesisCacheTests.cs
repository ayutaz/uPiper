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

            var result = cache.TryGet(key, out var samples, out var sampleRate, out _);

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
            var result = cache.TryGet(key, out var samples, out var sampleRate, out _);

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
            Assert.IsFalse(cache.TryGet(key1, out _, out _, out _));
            Assert.IsTrue(cache.TryGet(key2, out _, out _, out _));
            Assert.IsTrue(cache.TryGet(key3, out _, out _, out _));
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
            cache.TryGet(key1, out _, out _, out _);

            // Now add key3 — should evict key2 (not key1)
            cache.Set(key3, new float[] { 0.3f }, 22050);

            Assert.IsTrue(cache.TryGet(key1, out _, out _, out _));
            Assert.IsFalse(cache.TryGet(key2, out _, out _, out _));
            Assert.IsTrue(cache.TryGet(key3, out _, out _, out _));
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
            cache.TryGet(key, out _, out _, out _);
            cache.TryGet(999L, out _, out _, out _);
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
            cache.TryGet(1L, out var samples, out _, out _);
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

            cache.TryGet(1L, out _, out _, out _);   // hit
            cache.TryGet(1L, out _, out _, out _);   // hit
            cache.TryGet(999L, out _, out _, out _); // miss

            Assert.AreEqual(2, cache.HitCount);
            Assert.AreEqual(1, cache.MissCount);
        }

        // ================================================================
        // Memory limit eviction tests
        // ================================================================

        [Test]
        public void Set_ExceedsMaxMemory_EvictsLRU()
        {
            // Each float[125000] entry: 125000 * 4 + 64 = 500,064 bytes
            // maxMemoryMB=1 → 1,048,576 bytes budget
            // Two entries: 1,000,128 bytes (fits)
            // Three entries: 1,500,192 bytes (exceeds budget → evicts LRU)
            var cache = new AudioSynthesisCache(maxEntries: 100, maxMemoryMB: 1);

            var big1 = new float[125000];
            var big2 = new float[125000];
            var big3 = new float[125000];

            cache.Set(1L, big1, 22050);
            cache.Set(2L, big2, 22050);
            Assert.AreEqual(2, cache.Count);

            cache.Set(3L, big3, 22050);

            Assert.IsFalse(cache.TryGet(1L, out _, out _, out _)); // evicted
            Assert.IsTrue(cache.TryGet(2L, out _, out _, out _));  // kept
            Assert.IsTrue(cache.TryGet(3L, out _, out _, out _));  // new
            Assert.AreEqual(2, cache.Count);
        }

        [Test]
        public void Set_SingleEntryExceedsMemoryBudget_DoesNotCache()
        {
            // maxMemoryMB=1 → 1,048,576 bytes budget
            // float[300000]: 300000 * 4 + 64 = 1,200,064 bytes > budget
            var cache = new AudioSynthesisCache(maxEntries: 100, maxMemoryMB: 1);

            var huge = new float[300000];
            cache.Set(1L, huge, 22050);

            Assert.AreEqual(0, cache.Count);
            Assert.AreEqual(0, cache.CurrentMemoryBytes);
            Assert.IsFalse(cache.TryGet(1L, out _, out _, out _));
        }

        [Test]
        public void Set_MemoryTrackingAccurate_AfterAddAndEvict()
        {
            // Each float[125000] entry: 125000 * 4 + 64 = 500,064 bytes
            const long expectedEntryBytes = 125000L * sizeof(float) + 64;
            var cache = new AudioSynthesisCache(maxEntries: 100, maxMemoryMB: 1);

            cache.Set(1L, new float[125000], 22050);
            Assert.AreEqual(expectedEntryBytes, cache.CurrentMemoryBytes);

            cache.Set(2L, new float[125000], 22050);
            Assert.AreEqual(expectedEntryBytes * 2, cache.CurrentMemoryBytes);

            // Adding a third triggers eviction of entry 1
            cache.Set(3L, new float[125000], 22050);
            Assert.AreEqual(expectedEntryBytes * 2, cache.CurrentMemoryBytes);
            Assert.AreEqual(1, cache.EvictionCount);
        }

        // ================================================================
        // Timing storage tests
        // ================================================================

        [Test]
        public void Set_WithTimings_TryGetRestoresTimings()
        {
            var cache = new AudioSynthesisCache();
            var key = 1L;
            var timings = CreateSampleTimings();

            cache.Set(key, SampleAudio, 22050, timings);
            var result = cache.TryGet(key, out var samples, out var sampleRate, out var outTimings);

            Assert.IsTrue(result);
            Assert.AreEqual(SampleAudio, samples);
            Assert.AreEqual(22050, sampleRate);
            Assert.IsNotNull(outTimings);
            Assert.AreEqual(timings.Length, outTimings.Length);
            for (var i = 0; i < timings.Length; i++)
            {
                Assert.AreEqual(timings[i].Phoneme, outTimings[i].Phoneme,
                    $"Timings[{i}].Phoneme が復元されること");
                Assert.AreEqual(timings[i].StartSeconds, outTimings[i].StartSeconds, 1e-5f,
                    $"Timings[{i}].StartSeconds が復元されること");
                Assert.AreEqual(timings[i].EndSeconds, outTimings[i].EndSeconds, 1e-5f,
                    $"Timings[{i}].EndSeconds が復元されること");
            }
        }

        [Test]
        public void Set_WithoutTimings_TryGetReturnsNullTimings()
        {
            var cache = new AudioSynthesisCache();
            var key = 1L;

            cache.Set(key, SampleAudio, 22050);
            var result = cache.TryGet(key, out _, out _, out var outTimings);

            Assert.IsTrue(result);
            Assert.IsNull(outTimings, "timings なし Set の場合 TryGet で null が返ること");
        }

        [Test]
        public void Set_WithNullTimings_TryGetReturnsNullTimings()
        {
            var cache = new AudioSynthesisCache();
            var key = 1L;

            cache.Set(key, SampleAudio, 22050, timings: null);
            var result = cache.TryGet(key, out _, out _, out var outTimings);

            Assert.IsTrue(result);
            Assert.IsNull(outTimings, "明示的 null timings の場合 TryGet で null が返ること");
        }

        [Test]
        public void Set_WithTimings_EvictsLRU_TimingsAlsoEvicted()
        {
            var cache = new AudioSynthesisCache(maxEntries: 2, maxMemoryMB: 100);
            var timings1 = CreateSampleTimings();

            cache.Set(1L, new float[] { 0.1f }, 22050, timings1);
            cache.Set(2L, new float[] { 0.2f }, 22050);
            cache.Set(3L, new float[] { 0.3f }, 22050);

            // key1 が LRU 退避されていること
            Assert.IsFalse(cache.TryGet(1L, out _, out _, out var evictedTimings),
                "LRU 退避された key1 は TryGet で false を返すこと");
            Assert.IsNull(evictedTimings,
                "退避済みエントリの timings は null であること");

            // key2, key3 は残っていること
            Assert.IsTrue(cache.TryGet(2L, out _, out _, out _));
            Assert.IsTrue(cache.TryGet(3L, out _, out _, out _));
        }

        [Test]
        public void Set_WithTimings_DuplicateKey_UpdatesTimings()
        {
            var cache = new AudioSynthesisCache();
            var key = 1L;
            var timings1 = CreateSampleTimings();
            var timings2 = new[]
            {
                new PhonemeTimingEntry("a", 0.0f, 0.100f),
                new PhonemeTimingEntry("i", 0.100f, 0.200f),
            };

            cache.Set(key, SampleAudio, 22050, timings1);
            cache.Set(key, SampleAudio, 22050, timings2);

            Assert.AreEqual(1, cache.Count);
            cache.TryGet(key, out _, out _, out var outTimings);
            Assert.IsNotNull(outTimings);
            Assert.AreEqual(2, outTimings.Length,
                "上書き後の timings 長が新しい値と一致すること");
            Assert.AreEqual("a", outTimings[0].Phoneme);
            Assert.AreEqual("i", outTimings[1].Phoneme);
        }

        [Test]
        public void Set_WithTimings_Clear_RemovesTimings()
        {
            var cache = new AudioSynthesisCache();
            var key = 1L;

            cache.Set(key, SampleAudio, 22050, CreateSampleTimings());
            cache.Clear();

            var result = cache.TryGet(key, out _, out _, out var outTimings);
            Assert.IsFalse(result, "Clear 後は TryGet で false を返すこと");
            Assert.IsNull(outTimings, "Clear 後の timings は null であること");
        }

        [Test]
        public void TryGet_EmptyCache_TimingsIsNull()
        {
            var cache = new AudioSynthesisCache();
            var key = 999L;

            var result = cache.TryGet(key, out _, out _, out var outTimings);

            Assert.IsFalse(result);
            Assert.IsNull(outTimings, "空キャッシュの TryGet で timings は null であること");
        }

        [Test]
        public void Set_WithTimings_TryGetIgnoresTimingsViaDiscard()
        {
            var cache = new AudioSynthesisCache();
            var key = 1L;

            cache.Set(key, SampleAudio, 22050, CreateSampleTimings());

            // out _ で timings を破棄しても正常に動作すること
            var result = cache.TryGet(key, out var samples, out var sampleRate, out _);

            Assert.IsTrue(result);
            Assert.AreEqual(SampleAudio, samples);
            Assert.AreEqual(22050, sampleRate);
        }

        // ================================================================
        // Timing memory calculation tests
        // ================================================================

        [Test]
        public void Set_WithTimings_CurrentMemoryBytesIncludesTimings()
        {
            // Arrange
            var cache = new AudioSynthesisCache();
            var key = AudioSynthesisCache.GenerateKey(SamplePhonemeIds, null, 1f, 0.667f, 0.8f, 0, 0);
            var timings = CreateSampleTimings(); // 3 entries

            // Act: タイミングなしで保存
            var keyNoTimings = AudioSynthesisCache.GenerateKey(
                new[] { 1, 2 }, null, 1f, 0.667f, 0.8f, 0, 0);
            cache.Set(keyNoTimings, SampleAudio, 22050);
            var memWithout = cache.CurrentMemoryBytes;

            // タイミング付きで保存
            cache.Set(key, SampleAudio, 22050, timings);
            var memWith = cache.CurrentMemoryBytes;

            // Assert: タイミング付きの方がメモリ消費が大きい
            Assert.Greater(memWith, memWithout,
                "タイミング付きエントリはタイミングなしより多くのメモリを消費すること");
        }

        [Test]
        public void Set_WithTimings_EvictLRU_MemoryBytesDecreases()
        {
            // Arrange: maxEntries=1
            var cache = new AudioSynthesisCache(maxEntries: 1);
            var key1 = AudioSynthesisCache.GenerateKey(SamplePhonemeIds, null, 1f, 0.667f, 0.8f, 0, 0);
            var key2 = AudioSynthesisCache.GenerateKey(new[] { 9, 9 }, null, 1f, 0.667f, 0.8f, 0, 0);
            var timings = CreateSampleTimings();

            // Act
            cache.Set(key1, SampleAudio, 22050, timings);
            var memAfterFirst = cache.CurrentMemoryBytes;

            cache.Set(key2, new float[] { 0.1f }, 22050); // timingsなし、小さいaudio
            var memAfterEviction = cache.CurrentMemoryBytes;

            // Assert
            Assert.Less(memAfterEviction, memAfterFirst,
                "LRU退避後にメモリ消費が減少すること");
            Assert.AreEqual(1, cache.Count);
        }

        // ================================================================
        // Timing helpers
        // ================================================================

        private static PhonemeTimingEntry[] CreateSampleTimings()
        {
            return new[]
            {
                new PhonemeTimingEntry("k", 0.0f, 0.058f),
                new PhonemeTimingEntry("o", 0.058f, 0.128f),
                new PhonemeTimingEntry("N", 0.128f, 0.186f),
            };
        }
    }
}
