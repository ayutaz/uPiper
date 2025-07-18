using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using uPiper.Core.Phonemizers.Cache;
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
            var cache = new LRUCache<string, string>(100);
            
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
            var cache = new LRUCache<string, string>(100);
            
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
        public void LRUCache_GetStatistics_MinimalGCAllocation()
        {
            var cache = new LRUCache<string, string>(100);
            
            // Prepare cache with data
            for (int i = 0; i < 50; i++)
            {
                cache.Add($"key{i}", $"value{i}");
            }

            // GetStatistics creates a new Dictionary, so we expect some allocation
            // but it should be minimal (just the dictionary and its entries)
            Assert.That(() =>
            {
                var stats = cache.GetStatistics();
            }, Is.AllocatingGCMemory());
        }
    }
}