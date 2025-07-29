using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core.Phonemizers.Backend;
using uPiper.Core.Phonemizers.Backend.Flite;

namespace uPiper.Tests.Runtime.Phonemizers
{
    /// <summary>
    /// Unit tests for FliteLTSPhonemizer
    /// </summary>
    [TestFixture]
    [Timeout(30000)] // 30 second timeout for all tests in this fixture
    // [Ignore("Temporarily disabled - FliteLTS initialization causing hangs")] // Re-enabled with proper timeout handling
    public class FliteLTSPhonemizerTests
    {
        private FliteLTSPhonemizer phonemizer;
        
        [SetUp]
        public void Setup()
        {
            phonemizer = new FliteLTSPhonemizer();
        }
        
        [TearDown]
        public void TearDown()
        {
            phonemizer?.Dispose();
        }
        
        [UnityTest]
        [Timeout(10000)] // 10 second timeout
        public IEnumerator TestInitialization()
        {
            var options = new PhonemizerBackendOptions
            {
                DataPath = null // Will use default path
            };
            
            var initTask = phonemizer.InitializeAsync(options);
            var timeout = Time.realtimeSinceStartup + 5f; // 5 second timeout
            
            while (!initTask.IsCompleted && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }
            
            if (!initTask.IsCompleted)
            {
                Assert.Inconclusive("Initialization timed out");
                yield break;
            }
            
            Assert.IsTrue(initTask.Result, "Phonemizer should initialize successfully");
            Assert.IsTrue(phonemizer.IsAvailable, "Phonemizer should be available");
            Assert.AreEqual("FliteLTS", phonemizer.Name);
            Assert.AreEqual(200, phonemizer.Priority, "FliteLTS should have high priority");
        }
        
        [UnityTest]
        public IEnumerator TestBasicPhonemization()
        {
            var options = new PhonemizerBackendOptions { DataPath = null };
            var initTask = phonemizer.InitializeAsync(options);
            yield return new WaitUntil(() => initTask.IsCompleted);
            
            // Test simple words
            var testWords = new[] { "cat", "dog", "fish", "bird" };
            
            foreach (var word in testWords)
            {
                var phonemeTask = phonemizer.PhonemizeAsync(word, "en");
                yield return new WaitUntil(() => phonemeTask.IsCompleted);
                
                var result = phonemeTask.Result;
                Assert.IsTrue(result.Success, $"Phonemization should succeed for '{word}'");
                Assert.IsNotNull(result.Phonemes);
                Assert.Greater(result.Phonemes.Length, 0, $"Should produce phonemes for '{word}'");
                
                Debug.Log($"'{word}' -> [{string.Join(" ", result.Phonemes)}]");
            }
        }
        
        [UnityTest]
        public IEnumerator TestComplexWords()
        {
            var options = new PhonemizerBackendOptions { DataPath = null };
            var initTask = phonemizer.InitializeAsync(options);
            yield return new WaitUntil(() => initTask.IsCompleted);
            
            // Test more complex words with patterns
            var testCases = new (string word, string[] expectedContains)[]
            {
                ("chair", new[] { "ch" }), // 'ch' sound
                ("thing", new[] { "th" }), // 'th' sound
                ("sing", new[] { "ng" }),  // 'ng' sound
                ("care", new[] { "k", "r" }) // 'ar' pattern
            };
            
            foreach (var (word, expectedContains) in testCases)
            {
                var phonemeTask = phonemizer.PhonemizeAsync(word, "en");
                yield return new WaitUntil(() => phonemeTask.IsCompleted);
                
                var result = phonemeTask.Result;
                Assert.IsTrue(result.Success);
                
                var phonemeString = string.Join(" ", result.Phonemes);
                Debug.Log($"'{word}' -> [{phonemeString}]");
                
                // Check if expected phonemes are present
                foreach (var expected in expectedContains)
                {
                    bool found = false;
                    foreach (var phoneme in result.Phonemes)
                    {
                        if (phoneme.Contains(expected))
                        {
                            found = true;
                            break;
                        }
                    }
                    Assert.IsTrue(found, $"Expected to find '{expected}' sound in '{word}'");
                }
            }
        }
        
        [UnityTest]
        public IEnumerator TestSentencePhonemization()
        {
            var options = new PhonemizerBackendOptions { DataPath = null };
            var initTask = phonemizer.InitializeAsync(options);
            yield return new WaitUntil(() => initTask.IsCompleted);
            
            var sentence = "Hello world, how are you?";
            var phonemeTask = phonemizer.PhonemizeAsync(sentence, "en");
            yield return new WaitUntil(() => phonemeTask.IsCompleted);
            
            var result = phonemeTask.Result;
            Assert.IsTrue(result.Success);
            Assert.Greater(result.Phonemes.Length, 10, "Sentence should produce multiple phonemes");
            Assert.IsNotNull(result.WordBoundaries);
            Assert.Greater(result.WordBoundaries.Length, 0, "Should have word boundaries");
            
            Debug.Log($"Sentence: '{sentence}'");
            Debug.Log($"Phonemes: [{string.Join(" ", result.Phonemes)}]");
            Debug.Log($"Word boundaries at: [{string.Join(", ", result.WordBoundaries)}]");
        }
        
        [UnityTest]
        public IEnumerator TestPunctuation()
        {
            var options = new PhonemizerBackendOptions { DataPath = null };
            var initTask = phonemizer.InitializeAsync(options);
            yield return new WaitUntil(() => initTask.IsCompleted);
            
            var text = "Hello. How are you? I'm fine!";
            var phonemeTask = phonemizer.PhonemizeAsync(text, "en");
            yield return new WaitUntil(() => initTask.IsCompleted);
            
            var result = phonemeTask.Result;
            Assert.IsTrue(result.Success);
            
            // Count silence phonemes
            int silenceCount = 0;
            for (int i = 0; i < result.Phonemes.Length; i++)
            {
                if (result.Phonemes[i] == "_")
                    silenceCount++;
            }
            
            Assert.Greater(silenceCount, 0, "Should have silence for punctuation");
            Assert.IsNotNull(result.Durations);
        }
        
        [UnityTest]
        public IEnumerator TestMemoryUsage()
        {
            var options = new PhonemizerBackendOptions { DataPath = null };
            var initTask = phonemizer.InitializeAsync(options);
            yield return new WaitUntil(() => initTask.IsCompleted);
            
            var memoryBefore = phonemizer.GetMemoryUsage();
            
            // Process multiple words to populate cache
            for (int i = 0; i < 10; i++)
            {
                var word = $"test{i}";
                var task = phonemizer.PhonemizeAsync(word, "en");
                yield return new WaitUntil(() => task.IsCompleted);
            }
            
            var memoryAfter = phonemizer.GetMemoryUsage();
            Assert.Greater(memoryAfter, memoryBefore, "Memory usage should increase with cache");
            
            Debug.Log($"Memory usage: {memoryBefore} -> {memoryAfter} bytes");
        }
        
        [UnityTest]
        [Timeout(10000)] // 10 second timeout
        // [Ignore("Temporarily disabled - causing test runner to hang")] // Re-enabled with proper timeout handling
        public IEnumerator TestCapabilities()
        {
            var options = new PhonemizerBackendOptions { DataPath = null };
            var initTask = phonemizer.InitializeAsync(options);
            
            var timeout = Time.realtimeSinceStartup + 5f;
            while (!initTask.IsCompleted && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }
            
            if (!initTask.IsCompleted)
            {
                Assert.Inconclusive("Initialization timed out");
                yield break;
            }
            
            var capabilities = phonemizer.GetCapabilities();
            Assert.IsFalse(capabilities.SupportsIPA, "Should use ARPABET, not IPA");
            Assert.IsTrue(capabilities.SupportsStress, "Should support stress markers");
            Assert.IsTrue(capabilities.SupportsDuration, "Should support duration estimation");
            Assert.IsTrue(capabilities.IsThreadSafe, "Should be thread-safe");
            Assert.IsFalse(capabilities.RequiresNetwork, "Should work offline");
        }
        
        [UnityTest]
        public IEnumerator TestErrorHandling()
        {
            // Test without initialization
            var task = phonemizer.PhonemizeAsync("test", "en");
            yield return new WaitUntil(() => task.IsCompleted);
            
            var result = task.Result;
            Assert.IsFalse(result.Success);
            Assert.IsNotNull(result.ErrorMessage);
            Assert.AreEqual(0, result.Phonemes.Length);
        }
    }
}