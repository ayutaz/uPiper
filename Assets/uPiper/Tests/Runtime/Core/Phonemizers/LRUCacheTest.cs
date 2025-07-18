using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using uPiper.Core.Phonemizers.Cache;

namespace uPiper.Tests.Runtime.Core.Phonemizers
{
    public class LRUCacheTest
    {
        private LRUCache<string, string> _cache;

        [SetUp]
        public void Setup()
        {
            _cache = new LRUCache<string, string>(3); // Small capacity for testing
        }

        [TearDown]
        public void TearDown()
        {
            _cache?.Dispose();
        }

        [Test]
        public void Constructor_WithValidCapacity_CreatesCache()
        {
            Assert.AreEqual(3, _cache.Capacity);
            Assert.AreEqual(0, _cache.Count);
        }

        [Test]
        public void Constructor_WithInvalidCapacity_ThrowsException()
        {
            Assert.Throws<ArgumentException>(() => new LRUCache<string, string>(0));
            Assert.Throws<ArgumentException>(() => new LRUCache<string, string>(-1));
        }

        [Test]
        public void Add_NewItem_IncreasesCount()
        {
            _cache.Add("key1", "value1");
            Assert.AreEqual(1, _cache.Count);

            _cache.Add("key2", "value2");
            Assert.AreEqual(2, _cache.Count);
        }

        [Test]
        public void Add_ExistingItem_UpdatesValue()
        {
            _cache.Add("key1", "value1");
            _cache.Add("key1", "updated");

            Assert.IsTrue(_cache.TryGet("key1", out var value));
            Assert.AreEqual("updated", value);
            Assert.AreEqual(1, _cache.Count);
        }

        [Test]
        public void Add_NullKey_ThrowsException()
        {
            Assert.Throws<ArgumentNullException>(() => _cache.Add(null, "value"));
        }

        [Test]
        public void TryGet_ExistingItem_ReturnsTrue()
        {
            _cache.Add("key1", "value1");

            Assert.IsTrue(_cache.TryGet("key1", out var value));
            Assert.AreEqual("value1", value);
        }

        [Test]
        public void TryGet_NonExistingItem_ReturnsFalse()
        {
            Assert.IsFalse(_cache.TryGet("nonexistent", out var value));
            Assert.IsNull(value);
        }

        [Test]
        public void TryGet_NullKey_ReturnsFalse()
        {
            Assert.IsFalse(_cache.TryGet(null, out var value));
            Assert.IsNull(value);
        }

        [Test]
        public void LRU_Eviction_RemovesLeastRecentlyUsed()
        {
            // Fill cache to capacity
            _cache.Add("key1", "value1");
            _cache.Add("key2", "value2");
            _cache.Add("key3", "value3");

            // Access key1 and key2 to make them more recently used
            _cache.TryGet("key1", out _);
            _cache.TryGet("key2", out _);

            // Add new item, should evict key3
            _cache.Add("key4", "value4");

            Assert.AreEqual(3, _cache.Count);
            Assert.IsFalse(_cache.TryGet("key3", out _));
            Assert.IsTrue(_cache.TryGet("key1", out _));
            Assert.IsTrue(_cache.TryGet("key2", out _));
            Assert.IsTrue(_cache.TryGet("key4", out _));
        }

        [Test]
        public void Remove_ExistingItem_ReturnsTrue()
        {
            _cache.Add("key1", "value1");

            Assert.IsTrue(_cache.Remove("key1"));
            Assert.AreEqual(0, _cache.Count);
            Assert.IsFalse(_cache.TryGet("key1", out _));
        }

        [Test]
        public void Remove_NonExistingItem_ReturnsFalse()
        {
            Assert.IsFalse(_cache.Remove("nonexistent"));
        }

        [Test]
        public void Remove_NullKey_ReturnsFalse()
        {
            Assert.IsFalse(_cache.Remove(null));
        }

        [Test]
        public void Clear_RemovesAllItems()
        {
            _cache.Add("key1", "value1");
            _cache.Add("key2", "value2");
            _cache.Add("key3", "value3");

            _cache.Clear();

            Assert.AreEqual(0, _cache.Count);
            Assert.IsFalse(_cache.TryGet("key1", out _));
            Assert.IsFalse(_cache.TryGet("key2", out _));
            Assert.IsFalse(_cache.TryGet("key3", out _));
        }

        [Test]
        public void ContainsKey_ExistingItem_ReturnsTrue()
        {
            _cache.Add("key1", "value1");
            Assert.IsTrue(_cache.ContainsKey("key1"));
        }

        [Test]
        public void ContainsKey_NonExistingItem_ReturnsFalse()
        {
            Assert.IsFalse(_cache.ContainsKey("nonexistent"));
        }

        [Test]
        public void ContainsKey_NullKey_ReturnsFalse()
        {
            Assert.IsFalse(_cache.ContainsKey(null));
        }

        [Test]
        public void GetStatistics_ReturnsCorrectInfo()
        {
            _cache.Add("key1", "value1");
            _cache.Add("key2", "value2");

            var stats = _cache.GetStatistics();

            Assert.AreEqual(2, stats["Count"]);
            Assert.AreEqual(3, stats["Capacity"]);
            Assert.AreEqual(2.0 / 3.0, stats["FillRate"]);
        }

        [Test]
        public void ThreadSafety_ConcurrentOperations()
        {
            var tasks = new List<Task>();
            var itemCount = 100;

            // Multiple threads adding items
            for (int i = 0; i < 10; i++)
            {
                var threadId = i;
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < itemCount; j++)
                    {
                        _cache.Add($"thread{threadId}_item{j}", $"value_{threadId}_{j}");
                    }
                }));
            }

            // Multiple threads reading items
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < itemCount * 10; j++)
                    {
                        _cache.TryGet($"thread{j % 10}_item{j % itemCount}", out _);
                    }
                }));
            }

            Task.WaitAll(tasks.ToArray());

            // Cache should still be in valid state
            Assert.LessOrEqual(_cache.Count, _cache.Capacity);
        }

        [Test]
        public void Dispose_MultipleCalls_DoesNotThrow()
        {
            var cache = new LRUCache<string, string>(10);
            cache.Add("key", "value");

            cache.Dispose();
            Assert.DoesNotThrow(() => cache.Dispose());
        }

        [Test]
        public void NullValues_AreAllowed()
        {
            _cache.Add("key1", null);

            Assert.IsTrue(_cache.TryGet("key1", out var value));
            Assert.IsNull(value);
        }

        [Test]
        public void LRU_UpdateMakesItemMostRecent()
        {
            // Fill cache to capacity
            _cache.Add("key1", "value1");
            _cache.Add("key2", "value2");
            _cache.Add("key3", "value3");

            // Update key1 to make it most recent
            _cache.Add("key1", "updated");

            // Add new item, should evict key2 (key3 was accessed more recently than key2)
            _cache.Add("key4", "value4");

            Assert.IsTrue(_cache.TryGet("key1", out _));
            Assert.IsFalse(_cache.TryGet("key2", out _));
            Assert.IsTrue(_cache.TryGet("key3", out _));
            Assert.IsTrue(_cache.TryGet("key4", out _));
        }
    }
}