using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core.Phonemizers.Backend.RuleBased;
using Debug = UnityEngine.Debug;

namespace uPiper.Tests.Phonemizers
{
    /// <summary>
    /// Performance tests for full CMU dictionary (134,000+ words)
    /// </summary>
    [TestFixture]
    public class FullDictionaryPerformanceTests
    {
        private RuleBasedPhonemizer phonemizer;
        private Stopwatch stopwatch;
        
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            stopwatch = new Stopwatch();
        }
        
        [SetUp]
        public async Task SetUp()
        {
            phonemizer = new RuleBasedPhonemizer();
            await phonemizer.InitializeAsync(null);
        }
        
        [TearDown]
        public void TearDown()
        {
            phonemizer?.Dispose();
        }
        
        [Test]
        public async Task FullDictionary_LoadTime()
        {
            // Test dictionary load time
            var newPhonemizer = new RuleBasedPhonemizer();
            
            stopwatch.Restart();
            bool initialized = await newPhonemizer.InitializeAsync(null);
            stopwatch.Stop();
            
            Assert.IsTrue(initialized, "Should initialize successfully");
            
            double loadTimeMs = stopwatch.Elapsed.TotalMilliseconds;
            Debug.Log($"Full CMU dictionary load time: {loadTimeMs:F0} ms");
            
            // Desktop: expect under 500ms, Mobile: under 1500ms
            #if UNITY_ANDROID || UNITY_IOS
            Assert.Less(loadTimeMs, 1500, "Mobile load time should be under 1.5 seconds");
            #else
            Assert.Less(loadTimeMs, 500, "Desktop load time should be under 500ms");
            #endif
            
            newPhonemizer.Dispose();
        }
        
        [Test]
        public async Task FullDictionary_MemoryUsage()
        {
            // Measure memory before and after loading
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
            
            long memoryBefore = System.GC.GetTotalMemory(false);
            
            var newPhonemizer = new RuleBasedPhonemizer();
            await newPhonemizer.InitializeAsync(null);
            
            long memoryAfter = System.GC.GetTotalMemory(false);
            long memoryUsed = memoryAfter - memoryBefore;
            double memoryUsedMB = memoryUsed / (1024.0 * 1024.0);
            
            Debug.Log($"Full dictionary memory usage: {memoryUsedMB:F1} MB");
            
            // Should use less than 20MB for full dictionary
            Assert.Less(memoryUsedMB, 20, "Memory usage should be under 20MB");
            
            newPhonemizer.Dispose();
        }
        
        [Test]
        public async Task FullDictionary_LookupPerformance()
        {
            // Test lookup performance with various words
            var testWords = new[]
            {
                // Common words
                "hello", "world", "computer", "software",
                // Complex words
                "internationalization", "pharmaceutical", "entrepreneurship",
                // Technical terms
                "algorithm", "asynchronous", "polymorphism",
                // Random words
                "serendipity", "quintessential", "perpendicular"
            };
            
            // Warm up
            await phonemizer.PhonemizeAsync("warmup", "en-US");
            
            // Test individual lookups
            var lookupTimes = new List<double>();
            foreach (var word in testWords)
            {
                stopwatch.Restart();
                var result = await phonemizer.PhonemizeAsync(word, "en-US");
                stopwatch.Stop();
                
                Assert.IsNotNull(result, $"Should get result for '{word}'");
                Assert.IsNotEmpty(result.Phonemes, $"Should have phonemes for '{word}'");
                
                lookupTimes.Add(stopwatch.Elapsed.TotalMilliseconds);
                Debug.Log($"'{word}' -> {string.Join(" ", result.Phonemes)} ({stopwatch.Elapsed.TotalMilliseconds:F2} ms)");
            }
            
            double avgLookupTime = lookupTimes.Average();
            double maxLookupTime = lookupTimes.Max();
            
            Debug.Log($"Average lookup time: {avgLookupTime:F2} ms, Max: {maxLookupTime:F2} ms");
            
            Assert.Less(avgLookupTime, 5, "Average lookup should be under 5ms");
            Assert.Less(maxLookupTime, 10, "Max lookup should be under 10ms");
        }
        
        [Test]
        public async Task FullDictionary_BatchProcessing()
        {
            // Test batch processing performance
            const int batchSize = 1000;
            var words = new List<string>();
            
            // Generate test batch - mix of dictionary and non-dictionary words
            for (int i = 0; i < batchSize; i++)
            {
                if (i % 3 == 0)
                {
                    // Non-dictionary word to test G2P
                    words.Add($"nonword{i}xyz");
                }
                else
                {
                    // Common English words likely in dictionary
                    words.Add(GetCommonWord(i));
                }
            }
            
            stopwatch.Restart();
            var tasks = words.Select(w => phonemizer.PhonemizeAsync(w, "en-US")).ToArray();
            var results = await Task.WhenAll(tasks);
            stopwatch.Stop();
            
            Assert.AreEqual(batchSize, results.Length);
            Assert.IsTrue(results.All(r => r != null && r.Phonemes.Count > 0));
            
            double totalSeconds = stopwatch.Elapsed.TotalSeconds;
            double wordsPerSecond = batchSize / totalSeconds;
            
            Debug.Log($"Batch processing: {batchSize} words in {totalSeconds:F2}s = {wordsPerSecond:F0} words/second");
            
            Assert.Greater(wordsPerSecond, 500, "Should process at least 500 words per second");
        }
        
        [Test]
        public async Task FullDictionary_ComplexSentences()
        {
            // Test with complex sentences
            var sentences = new[]
            {
                "The pharmaceutical company's internationalization strategy involved establishing subsidiaries in multiple jurisdictions.",
                "Quantum computing algorithms demonstrate exponential speedup for certain cryptographic applications.",
                "The entrepreneur's innovative blockchain solution revolutionized supply chain transparency.",
                "Neuroplasticity research indicates that cognitive rehabilitation can significantly improve outcomes.",
                "The symbiotic relationship between mycorrhizal fungi and plant roots enhances nutrient absorption."
            };
            
            foreach (var sentence in sentences)
            {
                stopwatch.Restart();
                var result = await phonemizer.PhonemizeAsync(sentence, "en-US");
                stopwatch.Stop();
                
                Assert.IsNotNull(result);
                Assert.IsNotEmpty(result.Phonemes);
                
                int wordCount = sentence.Split(' ').Length;
                double timePerWord = stopwatch.Elapsed.TotalMilliseconds / wordCount;
                
                Debug.Log($"Sentence ({wordCount} words) processed in {stopwatch.Elapsed.TotalMilliseconds:F0} ms ({timePerWord:F1} ms/word)");
                Debug.Log($"Phonemes: {string.Join(" ", result.Phonemes.Take(20))}...");
            }
        }
        
        private string GetCommonWord(int index)
        {
            var commonWords = new[]
            {
                "the", "be", "to", "of", "and", "a", "in", "that", "have", "I",
                "it", "for", "not", "on", "with", "he", "as", "you", "do", "at",
                "this", "but", "his", "by", "from", "they", "we", "say", "her", "she",
                "or", "an", "will", "my", "one", "all", "would", "there", "their", "what",
                "so", "up", "out", "if", "about", "who", "get", "which", "go", "me",
                "when", "make", "can", "like", "time", "no", "just", "him", "know", "take",
                "people", "into", "year", "your", "good", "some", "could", "them", "see", "other",
                "than", "then", "now", "look", "only", "come", "its", "over", "think", "also",
                "back", "after", "use", "two", "how", "our", "work", "first", "well", "way",
                "even", "new", "want", "because", "any", "these", "give", "day", "most", "us"
            };
            
            return commonWords[index % commonWords.Length];
        }
    }
}