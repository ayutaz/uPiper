using System.Collections.Generic;
using NUnit.Framework;
using uPiper.Phonemizers;

namespace uPiper.Tests
{
    [TestFixture]
    public class PhonemizersTests
    {
        private class MockPhonemizer : BasePhonemizer
        {
            public int CallCount { get; private set; }
            public List<string> ProcessedTexts { get; } = new List<string>();

            public MockPhonemizer(bool useCache = true, int maxCacheEntries = 1000) 
                : base(useCache, maxCacheEntries)
            {
            }

            protected override string[] PerformPhonemization(string text, string language)
            {
                CallCount++;
                ProcessedTexts.Add(text);
                
                // Simple mock implementation
                return text.Split(' ');
            }

            public int GetCacheSize()
            {
                return _cache.Count;
            }
        }

        [Test]
        public void BasePhonemizer_NormalizeText_RemovesExtraSpaces()
        {
            var phonemizer = new MockPhonemizer();
            
            var result = phonemizer.Phonemize("  Hello    World  ", "en");
            
            // Should normalize to "Hello World"
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual("Hello", result[0]);
            Assert.AreEqual("World", result[1]);
        }

        [Test]
        public void BasePhonemizer_EmptyText_ReturnsEmptyArray()
        {
            var phonemizer = new MockPhonemizer();
            
            var result = phonemizer.Phonemize("", "ja");
            
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Length);
            Assert.AreEqual(0, phonemizer.CallCount); // Should not call PerformPhonemization
        }

        [Test]
        public void BasePhonemizer_NullText_ReturnsEmptyArray()
        {
            var phonemizer = new MockPhonemizer();
            
            var result = phonemizer.Phonemize(null, "ja");
            
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Length);
            Assert.AreEqual(0, phonemizer.CallCount);
        }

        [Test]
        public void BasePhonemizer_Caching_PreventsDuplicateProcessing()
        {
            var phonemizer = new MockPhonemizer(useCache: true);
            
            // First call
            phonemizer.Phonemize("test text", "ja");
            Assert.AreEqual(1, phonemizer.CallCount);
            
            // Second call with same input
            phonemizer.Phonemize("test text", "ja");
            Assert.AreEqual(1, phonemizer.CallCount); // Should not increase
            
            // Different text
            phonemizer.Phonemize("different text", "ja");
            Assert.AreEqual(2, phonemizer.CallCount);
        }

        [Test]
        public void BasePhonemizer_NoCaching_AlwaysProcesses()
        {
            var phonemizer = new MockPhonemizer(useCache: false);
            
            phonemizer.Phonemize("test text", "ja");
            Assert.AreEqual(1, phonemizer.CallCount);
            
            phonemizer.Phonemize("test text", "ja");
            Assert.AreEqual(2, phonemizer.CallCount);
        }

        [Test]
        public void BasePhonemizer_CacheKey_IncludesLanguage()
        {
            var phonemizer = new MockPhonemizer(useCache: true);
            
            phonemizer.Phonemize("test", "ja");
            Assert.AreEqual(1, phonemizer.CallCount);
            
            // Same text, different language
            phonemizer.Phonemize("test", "en");
            Assert.AreEqual(2, phonemizer.CallCount);
            
            // Original language again - should use cache
            phonemizer.Phonemize("test", "ja");
            Assert.AreEqual(2, phonemizer.CallCount);
        }

        [Test]
        public void BasePhonemizer_CacheEviction_WhenLimitReached()
        {
            var phonemizer = new MockPhonemizer(useCache: true, maxCacheSize: 3);
            
            phonemizer.Phonemize("text1", "ja");
            phonemizer.Phonemize("text2", "ja");
            phonemizer.Phonemize("text3", "ja");
            
            Assert.AreEqual(3, phonemizer.GetCacheSize());
            
            // This should evict the oldest entry
            phonemizer.Phonemize("text4", "ja");
            Assert.AreEqual(3, phonemizer.GetCacheSize());
            
            // text1 should have been evicted, so it will be processed again
            phonemizer.Phonemize("text1", "ja");
            Assert.AreEqual(5, phonemizer.CallCount); // 4 unique + 1 re-process
        }

        [Test]
        public void BasePhonemizer_ClearCache_RemovesAllEntries()
        {
            var phonemizer = new MockPhonemizer(useCache: true);
            
            phonemizer.Phonemize("text1", "ja");
            phonemizer.Phonemize("text2", "ja");
            Assert.Greater(phonemizer.GetCacheSize(), 0);
            
            phonemizer.ClearCache();
            Assert.AreEqual(0, phonemizer.GetCacheSize());
            
            // Should process again after cache clear
            phonemizer.Phonemize("text1", "ja");
            Assert.AreEqual(3, phonemizer.CallCount);
        }

        [Test]
        public void BasePhonemizer_Dispose_ClearsCache()
        {
            var phonemizer = new MockPhonemizer(useCache: true);
            
            phonemizer.Phonemize("text", "ja");
            Assert.Greater(phonemizer.GetCacheSize(), 0);
            
            phonemizer.Dispose();
            Assert.AreEqual(0, phonemizer.GetCacheSize());
        }
    }
}