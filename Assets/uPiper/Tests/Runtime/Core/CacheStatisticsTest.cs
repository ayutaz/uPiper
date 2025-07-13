using System;
using NUnit.Framework;
using uPiper.Core;

namespace uPiper.Tests.Runtime.Core
{
    public class CacheStatisticsTest
    {
        [Test]
        public void TotalSizeMB_CalculatesCorrectly()
        {
            // Arrange
            var stats = new CacheStatistics
            {
                TotalSizeBytes = 10 * 1024 * 1024 // 10MB
            };
            
            // Assert
            Assert.AreEqual(10f, stats.TotalSizeMB, 0.001f);
        }
        
        [Test]
        public void MaxSizeMB_CalculatesCorrectly()
        {
            // Arrange
            var stats = new CacheStatistics
            {
                MaxSizeBytes = 100 * 1024 * 1024 // 100MB
            };
            
            // Assert
            Assert.AreEqual(100f, stats.MaxSizeMB, 0.001f);
        }
        
        [Test]
        public void HitRate_CalculatesCorrectly()
        {
            // Arrange
            var stats = new CacheStatistics
            {
                HitCount = 75,
                MissCount = 25
            };
            
            // Assert
            Assert.AreEqual(0.75f, stats.HitRate, 0.001f);
        }
        
        [Test]
        public void HitRate_ReturnsZeroWhenNoRequests()
        {
            // Arrange
            var stats = new CacheStatistics();
            
            // Assert
            Assert.AreEqual(0f, stats.HitRate);
        }
        
        [Test]
        public void UsagePercentage_CalculatesCorrectly()
        {
            // Arrange
            var stats = new CacheStatistics
            {
                TotalSizeBytes = 25 * 1024 * 1024,  // 25MB
                MaxSizeBytes = 100 * 1024 * 1024    // 100MB
            };
            
            // Assert
            Assert.AreEqual(0.25f, stats.UsagePercentage, 0.001f);
        }
        
        [Test]
        public void UsagePercentage_ReturnsZeroWhenMaxSizeIsZero()
        {
            // Arrange
            var stats = new CacheStatistics
            {
                TotalSizeBytes = 1000,
                MaxSizeBytes = 0
            };
            
            // Assert
            Assert.AreEqual(0f, stats.UsagePercentage);
        }
        
        [Test]
        public void AverageEntrySizeBytes_CalculatesCorrectly()
        {
            // Arrange
            var stats = new CacheStatistics
            {
                EntryCount = 100,
                TotalSizeBytes = 50000
            };
            
            // Assert
            Assert.AreEqual(500f, stats.AverageEntrySizeBytes, 0.001f);
        }
        
        [Test]
        public void AverageEntrySizeBytes_ReturnsZeroWhenNoEntries()
        {
            // Arrange
            var stats = new CacheStatistics
            {
                EntryCount = 0,
                TotalSizeBytes = 0
            };
            
            // Assert
            Assert.AreEqual(0f, stats.AverageEntrySizeBytes);
        }
        
        [Test]
        public void RecordHit_IncrementsHitCount()
        {
            // Arrange
            var stats = new CacheStatistics { HitCount = 10 };
            
            // Act
            stats.RecordHit();
            
            // Assert
            Assert.AreEqual(11, stats.HitCount);
        }
        
        [Test]
        public void RecordMiss_IncrementsMissCount()
        {
            // Arrange
            var stats = new CacheStatistics { MissCount = 5 };
            
            // Act
            stats.RecordMiss();
            
            // Assert
            Assert.AreEqual(6, stats.MissCount);
        }
        
        [Test]
        public void RecordEviction_IncrementsEvictionCount()
        {
            // Arrange
            var stats = new CacheStatistics { EvictionCount = 2 };
            
            // Act
            stats.RecordEviction(3);
            
            // Assert
            Assert.AreEqual(5, stats.EvictionCount);
        }
        
        [Test]
        public void Reset_ClearsAllStatistics()
        {
            // Arrange
            var stats = new CacheStatistics
            {
                EntryCount = 100,
                TotalSizeBytes = 1000000,
                HitCount = 50,
                MissCount = 10,
                EvictionCount = 5
            };
            
            // Act
            stats.Reset();
            
            // Assert
            Assert.AreEqual(0, stats.EntryCount);
            Assert.AreEqual(0, stats.TotalSizeBytes);
            Assert.AreEqual(0, stats.HitCount);
            Assert.AreEqual(0, stats.MissCount);
            Assert.AreEqual(0, stats.EvictionCount);
            Assert.LessOrEqual((DateTime.Now - stats.LastClearTime).TotalSeconds, 1);
        }
        
        [Test]
        public void UpdateSize_UpdatesValues()
        {
            // Arrange
            var stats = new CacheStatistics();
            
            // Act
            stats.UpdateSize(50, 2000000);
            
            // Assert
            Assert.AreEqual(50, stats.EntryCount);
            Assert.AreEqual(2000000, stats.TotalSizeBytes);
        }
        
        [Test]
        public void ToString_FormatsCorrectly()
        {
            // Arrange
            var stats = new CacheStatistics
            {
                EntryCount = 100,
                TotalSizeBytes = 50 * 1024 * 1024,
                MaxSizeBytes = 100 * 1024 * 1024,
                HitCount = 800,
                MissCount = 200,
                EvictionCount = 25
            };
            
            // Act
            var result = stats.ToString();
            
            // Assert
            Assert.IsTrue(result.Contains("100 entries"));
            Assert.IsTrue(result.Contains("50.00/100.00 MB"));
            Assert.IsTrue(result.Contains("50%"));
            Assert.IsTrue(result.Contains("80.0%"));
            Assert.IsTrue(result.Contains("800 hits"));
            Assert.IsTrue(result.Contains("200 misses"));
            Assert.IsTrue(result.Contains("25"));
        }
    }
}