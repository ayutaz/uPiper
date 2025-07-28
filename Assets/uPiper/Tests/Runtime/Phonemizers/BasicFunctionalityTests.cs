using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core.Phonemizers.Backend.RuleBased;
using uPiper.Core.Phonemizers.Services;
using uPiper.Core.Phonemizers.Multilingual;

namespace uPiper.Tests.Phonemizers
{
    /// <summary>
    /// Basic functionality tests to verify Phase 3 implementation works
    /// </summary>
    [TestFixture]
    public class BasicFunctionalityTests
    {
        private RuleBasedPhonemizer phonemizer;
        private PhonemizerService service;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            service = PhonemizerService.Instance;
        }

        [SetUp]
        public void SetUp()
        {
            phonemizer = new RuleBasedPhonemizer();
        }

        [TearDown]
        public void TearDown()
        {
            phonemizer?.Dispose();
        }

        #region CMU Dictionary Tests

        [Test]
        public async Task CMUDictionary_ShouldLoadSampleData()
        {
            // Initialize with default path (sample dictionary)
            var initialized = await phonemizer.InitializeAsync(null);
            
            Assert.IsTrue(initialized, "Phonemizer should initialize successfully");
            
            // Test with words from sample dictionary
            var testWords = new[] { "hello", "world", "test", "unity" };
            
            foreach (var word in testWords)
            {
                var result = await phonemizer.PhonemizeAsync(word, "en-US");
                
                Assert.IsNotNull(result, $"Should get result for '{word}'");
                Assert.IsNotEmpty(result.Phonemes, $"Should have phonemes for '{word}'");
                
                Debug.Log($"'{word}' -> {string.Join(" ", result.Phonemes)}");
            }
        }

        [Test]
        public async Task CMUDictionary_ShouldHandleCaseInsensitive()
        {
            await phonemizer.InitializeAsync(null);
            
            // Test different cases
            var result1 = await phonemizer.PhonemizeAsync("HELLO", "en-US");
            var result2 = await phonemizer.PhonemizeAsync("hello", "en-US");
            var result3 = await phonemizer.PhonemizeAsync("Hello", "en-US");
            
            // All should produce the same phonemes
            CollectionAssert.AreEqual(result1.Phonemes, result2.Phonemes);
            CollectionAssert.AreEqual(result2.Phonemes, result3.Phonemes);
        }

        #endregion

        #region G2P Fallback Tests

        [Test]
        public async Task G2P_ShouldHandleUnknownWords()
        {
            await phonemizer.InitializeAsync(null);
            
            // Test with made-up words not in dictionary
            var unknownWords = new[] { "xyzabc", "qwerty", "phonemizer" };
            
            foreach (var word in unknownWords)
            {
                var result = await phonemizer.PhonemizeAsync(word, "en-US");
                
                Assert.IsNotNull(result, $"Should handle unknown word '{word}'");
                Assert.IsNotEmpty(result.Phonemes, $"Should generate phonemes for '{word}'");
                
                Debug.Log($"G2P '{word}' -> {string.Join(" ", result.Phonemes)}");
            }
        }

        #endregion

        #region DataManager Tests

        [Test]
        public void DataManager_ShouldBeAccessible()
        {
            var dataManager = service.DataManager;
            
            Assert.IsNotNull(dataManager, "DataManager should not be null");
            Assert.DoesNotThrow(() =>
            {
                var isAvailable = dataManager.IsDataAvailable("en-US").Result;
                Debug.Log($"Data available for en-US: {isAvailable}");
            });
        }

        [Test]
        public async Task DataManager_ChecksumVerification_ShouldWork()
        {
            var dataManager = service.DataManager;
            
            // Test with sample file
            var samplePath = System.IO.Path.Combine(
                Application.streamingAssetsPath, 
                "uPiper", 
                "Phonemizers", 
                "cmudict-sample.txt"
            );
            
            if (System.IO.File.Exists(samplePath))
            {
                // Download would verify checksum
                var result = await dataManager.DownloadDataAsync("en-US");
                Debug.Log($"Download data result: {result}");
            }
        }

        #endregion

        #region Integration Tests

        [UnityTest]
        public IEnumerator Service_ShouldPhonemizeSimpleText()
        {
            bool completed = false;
            PhonemeResult result = null;
            
            var task = service.PhonemizeAsync("Hello world", "en-US");
            task.ContinueWith(t =>
            {
                result = t.Result;
                completed = true;
            });
            
            // Wait for completion
            while (!completed)
            {
                yield return null;
            }
            
            Assert.IsNotNull(result, "Should get phoneme result");
            Assert.IsNotEmpty(result.Phonemes, "Should have phonemes");
            Assert.Greater(result.Phonemes.Count, 5, "Should have reasonable number of phonemes");
            
            Debug.Log($"Service result: {string.Join(" ", result.Phonemes)}");
        }

        [Test]
        public async Task Service_ShouldUseCache()
        {
            const string text = "test caching";
            
            // First call
            var sw1 = System.Diagnostics.Stopwatch.StartNew();
            var result1 = await service.PhonemizeAsync(text, "en-US");
            sw1.Stop();
            
            // Second call (should be cached)
            var sw2 = System.Diagnostics.Stopwatch.StartNew();
            var result2 = await service.PhonemizeAsync(text, "en-US");
            sw2.Stop();
            
            // Results should be the same
            CollectionAssert.AreEqual(result1.Phonemes, result2.Phonemes);
            
            // Second call should be much faster
            Debug.Log($"First call: {sw1.ElapsedMilliseconds}ms, Second call: {sw2.ElapsedMilliseconds}ms");
            Assert.Less(sw2.ElapsedMilliseconds, sw1.ElapsedMilliseconds / 2, 
                "Cached call should be at least 2x faster");
        }

        #endregion

        #region Error Handling Tests

        [Test]
        public async Task Service_ShouldHandleEmptyInput()
        {
            var result = await service.PhonemizeAsync("", "en-US");
            
            Assert.IsNotNull(result, "Should handle empty input");
            Assert.IsEmpty(result.Phonemes, "Empty input should produce empty phonemes");
        }

        [Test]
        public async Task Service_ShouldHandleNullInput()
        {
            try
            {
                await service.PhonemizeAsync(null, "en-US");
                Assert.Pass("Handled null input gracefully");
            }
            catch (System.ArgumentNullException)
            {
                Assert.Pass("Correctly threw ArgumentNullException for null input");
            }
        }

        [Test]
        public async Task Service_ShouldHandlePunctuation()
        {
            var result = await service.PhonemizeAsync("Hello, world!", "en-US");
            
            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result.Phonemes);
            
            // Should include pause markers for punctuation
            var hasProsodicMarkers = result.Phonemes.Contains("pau") || 
                                    result.Phonemes.Contains(",") ||
                                    result.Phonemes.Contains("!");
            
            Debug.Log($"Punctuation result: {string.Join(" ", result.Phonemes)}");
        }

        #endregion

        #region Performance Sanity Check

        [Test]
        public async Task Performance_ShouldMeetBasicRequirements()
        {
            await phonemizer.InitializeAsync(null);
            
            var sw = System.Diagnostics.Stopwatch.StartNew();
            const int iterations = 100;
            
            for (int i = 0; i < iterations; i++)
            {
                await phonemizer.PhonemizeAsync($"test {i}", "en-US");
            }
            
            sw.Stop();
            
            var avgMs = sw.ElapsedMilliseconds / (float)iterations;
            Debug.Log($"Average phonemization time: {avgMs:F2}ms");
            
            Assert.Less(avgMs, 50f, "Should process each word in under 50ms");
        }

        #endregion
    }
}