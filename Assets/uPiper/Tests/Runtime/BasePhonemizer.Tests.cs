using System.Linq;
using NUnit.Framework;
using uPiper.Phonemizers;

namespace uPiper.Tests
{
    [TestFixture]
    public class BasePhonemizer Tests
    {
        private class TestPhonemizer : BasePhonemizer
        {
            public int PhonemizationCount { get; private set; }

            public TestPhonemizer(bool useCache = true) : base(useCache)
            {
            }

            protected override string[] PerformPhonemization(string text, string language)
            {
                PhonemizationCount++;
                // Simple test implementation - split by characters
                return text.ToCharArray().Select(c => c.ToString()).ToArray();
            }
        }

        [Test]
        public void Phonemize_WithEmptyText_ShouldReturnEmptyArray()
        {
            var phonemizer = new TestPhonemizer();
            
            var result = phonemizer.Phonemize("", "ja");
            
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Length);
        }

        [Test]
        public void Phonemize_WithNullText_ShouldReturnEmptyArray()
        {
            var phonemizer = new TestPhonemizer();
            
            var result = phonemizer.Phonemize(null, "ja");
            
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Length);
        }

        [Test]
        public void Phonemize_WithCache_ShouldCacheResults()
        {
            var phonemizer = new TestPhonemizer(useCache: true);
            var text = "test";
            
            // First call
            var result1 = phonemizer.Phonemize(text, "ja");
            Assert.AreEqual(1, phonemizer.PhonemizationCount);
            
            // Second call - should use cache
            var result2 = phonemizer.Phonemize(text, "ja");
            Assert.AreEqual(1, phonemizer.PhonemizationCount);
            
            // Results should be the same
            CollectionAssert.AreEqual(result1, result2);
        }

        [Test]
        public void Phonemize_WithoutCache_ShouldNotCacheResults()
        {
            var phonemizer = new TestPhonemizer(useCache: false);
            var text = "test";
            
            // First call
            phonemizer.Phonemize(text, "ja");
            Assert.AreEqual(1, phonemizer.PhonemizationCount);
            
            // Second call - should not use cache
            phonemizer.Phonemize(text, "ja");
            Assert.AreEqual(2, phonemizer.PhonemizationCount);
        }

        [Test]
        public void Phonemize_WithDifferentLanguages_ShouldCacheSeparately()
        {
            var phonemizer = new TestPhonemizer(useCache: true);
            var text = "test";
            
            // Call with Japanese
            phonemizer.Phonemize(text, "ja");
            Assert.AreEqual(1, phonemizer.PhonemizationCount);
            
            // Call with English - should not use cache
            phonemizer.Phonemize(text, "en");
            Assert.AreEqual(2, phonemizer.PhonemizationCount);
            
            // Call with Japanese again - should use cache
            phonemizer.Phonemize(text, "ja");
            Assert.AreEqual(2, phonemizer.PhonemizationCount);
        }

        [Test]
        public void NormalizeText_ShouldRemoveExtraWhitespace()
        {
            var phonemizer = new TestPhonemizer();
            var text = "  Hello   World  ";
            
            var result = phonemizer.Phonemize(text, "en");
            
            // The normalized text should be "Hello World"
            var expectedPhonemes = "Hello World".ToCharArray().Select(c => c.ToString()).ToArray();
            CollectionAssert.AreEqual(expectedPhonemes, result);
        }

        [Test]
        public void ClearCache_ShouldRemoveAllCachedResults()
        {
            var phonemizer = new TestPhonemizer(useCache: true);
            var text = "test";
            
            // First call - should cache
            phonemizer.Phonemize(text, "ja");
            Assert.AreEqual(1, phonemizer.PhonemizationCount);
            
            // Clear cache
            phonemizer.ClearCache();
            
            // Second call - should not use cache
            phonemizer.Phonemize(text, "ja");
            Assert.AreEqual(2, phonemizer.PhonemizationCount);
        }
    }
}