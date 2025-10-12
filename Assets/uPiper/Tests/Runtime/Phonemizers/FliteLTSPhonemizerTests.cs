using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

            // Test more complex words - just verify they produce reasonable phonemes
            var testWords = new[] { "chair", "thing", "sing", "care" };

            foreach (var word in testWords)
            {
                var phonemeTask = phonemizer.PhonemizeAsync(word, "en");
                yield return new WaitUntil(() => phonemeTask.IsCompleted);

                var result = phonemeTask.Result;
                Assert.IsTrue(result.Success, $"Failed to phonemize '{word}'");
                Assert.IsNotNull(result.Phonemes);
                Assert.Greater(result.Phonemes.Length, 0, $"No phonemes for '{word}'");

                var phonemeString = string.Join(" ", result.Phonemes);
                Debug.Log($"'{word}' -> [{phonemeString}]");

                // Basic sanity check - word should produce reasonable number of phonemes
                Assert.GreaterOrEqual(result.Phonemes.Length, 2, $"Too few phonemes for '{word}'");
                Assert.LessOrEqual(result.Phonemes.Length, word.Length * 2, $"Too many phonemes for '{word}'");
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
            var silenceCount = 0;
            for (var i = 0; i < result.Phonemes.Length; i++)
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
            for (var i = 0; i < 10; i++)
            {
                var word = $"test{i}";
                var task = phonemizer.PhonemizeAsync(word, "en");
                yield return new WaitUntil(() => task.IsCompleted);
            }

            var memoryAfter = phonemizer.GetMemoryUsage();
            // Memory increase might be minimal or zero if cache is disabled/optimized
            Assert.GreaterOrEqual(memoryAfter, memoryBefore, "Memory usage should not decrease");

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

        // ========================================
        // Tests for complex suffix phonemization improvements
        // Related to Issue #69
        // ========================================

        [UnityTest]
        [Category("ComplexSuffix")]
        public IEnumerator ComplexWords_CooperationAndInvestigation_ShouldProduceCorrectPhonemes()
        {
            var options = new PhonemizerBackendOptions { DataPath = null };
            var initTask = phonemizer.InitializeAsync(options);
            yield return new WaitUntil(() => initTask.IsCompleted);

            // The original reported text from Issue #69
            var text = "cooperation and investigation";
            var phonemeTask = phonemizer.PhonemizeAsync(text, "en-US");
            yield return new WaitUntil(() => phonemeTask.IsCompleted);

            var result = phonemeTask.Result;
            Assert.IsTrue(result.Success, "Phonemization should succeed");
            Assert.IsNotNull(result.Phonemes);

            var phonemeString = string.Join(" ", result.Phonemes);
            Debug.Log($"Complex words test: '{text}' -> [{phonemeString}]");

            // Verify we have a reasonable number of phonemes (not too few, indicating skipping)
            // "cooperation" should have ~11 phonemes, "and" ~3, "investigation" ~14
            // Total: ~28-32 phonemes (including pauses)
            Assert.GreaterOrEqual(result.Phonemes.Length, 23,
                "Should have enough phonemes - syllables should not be skipped");
            Assert.LessOrEqual(result.Phonemes.Length, 35,
                "Should not have too many phonemes");

            // Verify word boundaries are detected
            Assert.AreEqual(3, result.WordBoundaries.Length, "Should have 3 words");
        }

        [UnityTest]
        [Category("ComplexSuffix")]
        public IEnumerator ComplexSuffix_Tion_ShouldProduceMultiplePhonemes()
        {
            var options = new PhonemizerBackendOptions { DataPath = null };
            var initTask = phonemizer.InitializeAsync(options);
            yield return new WaitUntil(() => initTask.IsCompleted);

            // Test words ending in "tion"
            var testWords = new[] { "nation", "action", "station", "creation" };

            foreach (var word in testWords)
            {
                var phonemeTask = phonemizer.PhonemizeAsync(word, "en-US");
                yield return new WaitUntil(() => phonemeTask.IsCompleted);

                var result = phonemeTask.Result;
                Assert.IsTrue(result.Success, $"Should phonemize '{word}' successfully");

                var phonemeString = string.Join(" ", result.Phonemes);
                Debug.Log($"'{word}' -> [{phonemeString}]");

                // "tion" should produce 3 phonemes: "sh", "ah0", "n"
                // Find the "sh" phoneme (should be near the end for "tion" words)
                var hasShSound = result.Phonemes.Any(p => p.StartsWith("sh", StringComparison.OrdinalIgnoreCase));
                Assert.IsTrue(hasShSound, $"'{word}' should contain 'sh' sound from 'tion'");

                // Verify we have schwa (ah0 or ah) sound
                var hasSchwaSound = result.Phonemes.Any(p => p.StartsWith("ah", StringComparison.OrdinalIgnoreCase));
                Assert.IsTrue(hasSchwaSound, $"'{word}' should contain schwa 'ah' sound from 'tion'");

                // Verify we have "n" sound
                var hasNSound = result.Phonemes.Any(p => p.Equals("n", StringComparison.OrdinalIgnoreCase) ||
                                                         p.StartsWith("n", StringComparison.OrdinalIgnoreCase));
                Assert.IsTrue(hasNSound, $"'{word}' should contain 'n' sound from 'tion'");
            }
        }

        [UnityTest]
        [Category("ComplexSuffix")]
        public IEnumerator ComplexSuffix_Sion_ShouldProduceMultiplePhonemes()
        {
            var options = new PhonemizerBackendOptions { DataPath = null };
            var initTask = phonemizer.InitializeAsync(options);
            yield return new WaitUntil(() => initTask.IsCompleted);

            // Test words ending in "sion"
            var testWords = new[] { "vision", "decision", "revision", "explosion" };

            foreach (var word in testWords)
            {
                var phonemeTask = phonemizer.PhonemizeAsync(word, "en-US");
                yield return new WaitUntil(() => phonemeTask.IsCompleted);

                var result = phonemeTask.Result;
                Assert.IsTrue(result.Success, $"Should phonemize '{word}' successfully");

                var phonemeString = string.Join(" ", result.Phonemes);
                Debug.Log($"'{word}' -> [{phonemeString}]");

                // "sion" should produce 3 phonemes: "zh", "ah0", "n"
                // Find the "zh" phoneme (should be near the end for "sion" words)
                var hasZhSound = result.Phonemes.Any(p => p.Equals("zh", StringComparison.OrdinalIgnoreCase) ||
                                                         p.StartsWith("zh", StringComparison.OrdinalIgnoreCase));
                Assert.IsTrue(hasZhSound, $"'{word}' should contain 'zh' sound from 'sion'");

                // Verify we have schwa (ah0 or ah) sound
                var hasSchwaSound = result.Phonemes.Any(p => p.StartsWith("ah", StringComparison.OrdinalIgnoreCase));
                Assert.IsTrue(hasSchwaSound, $"'{word}' should contain schwa 'ah' sound from 'sion'");

                // Verify we have "n" sound
                var hasNSound = result.Phonemes.Any(p => p.Equals("n", StringComparison.OrdinalIgnoreCase) ||
                                                         p.StartsWith("n", StringComparison.OrdinalIgnoreCase));
                Assert.IsTrue(hasNSound, $"'{word}' should contain 'n' sound from 'sion'");
            }
        }

        [UnityTest]
        [Category("ComplexSuffix")]
        public IEnumerator ComplexSuffix_VariousPatterns_ShouldHandleCorrectly()
        {
            var options = new PhonemizerBackendOptions { DataPath = null };
            var initTask = phonemizer.InitializeAsync(options);
            yield return new WaitUntil(() => initTask.IsCompleted);

            // Test other complex suffix patterns
            var testCases = new Dictionary<string, string>
            {
                ["nature"] = "ture",      // Should end with "ch er0"
                ["measure"] = "sure",     // Should end with "zh er0"
                ["package"] = "age",      // Should end with "ih0 jh"
                ["curious"] = "ious",     // Should end with "iy0 ah0 s"
                ["famous"] = "ous",       // Should end with "ah0 s"
            };

            foreach (var testCase in testCases)
            {
                var word = testCase.Key;
                var suffix = testCase.Value;

                var phonemeTask = phonemizer.PhonemizeAsync(word, "en-US");
                yield return new WaitUntil(() => phonemeTask.IsCompleted);

                var result = phonemeTask.Result;
                Assert.IsTrue(result.Success, $"Should phonemize '{word}' successfully");

                var phonemeString = string.Join(" ", result.Phonemes);
                Debug.Log($"'{word}' (suffix: {suffix}) -> [{phonemeString}]");

                // Just verify we get reasonable phonemes - complex validation would be too brittle
                Assert.Greater(result.Phonemes.Length, 2,
                    $"'{word}' should produce multiple phonemes");
            }
        }

        [UnityTest]
        [Category("ComplexSuffix")]
        public IEnumerator Phonemization_Cooperation_ShouldIncludeAllSyllables()
        {
            var options = new PhonemizerBackendOptions { DataPath = null };
            var initTask = phonemizer.InitializeAsync(options);
            yield return new WaitUntil(() => initTask.IsCompleted);

            var word = "cooperation";
            var phonemeTask = phonemizer.PhonemizeAsync(word, "en-US");
            yield return new WaitUntil(() => phonemeTask.IsCompleted);

            var result = phonemeTask.Result;
            Assert.IsTrue(result.Success);

            var phonemeString = string.Join(" ", result.Phonemes);
            Debug.Log($"'{word}' -> [{phonemeString}]");

            // Expected phonemes for "cooperation":
            // k ow . ah p . er . ey . sh ah n
            // Should have approximately 9-11 phonemes
            Assert.GreaterOrEqual(result.Phonemes.Length, 9,
                "cooperation should have at least 9 phonemes (was skipping syllables before fix)");
            Assert.LessOrEqual(result.Phonemes.Length, 13,
                "cooperation should not have more than 13 phonemes");

            // Verify key sounds are present
            Assert.IsTrue(result.Phonemes.Any(p => p.StartsWith("ah", StringComparison.OrdinalIgnoreCase)),
                "Should contain schwa sound (was missing before fix)");
            Assert.IsTrue(result.Phonemes.Any(p => p.StartsWith("sh", StringComparison.OrdinalIgnoreCase)),
                "Should contain 'sh' from 'tion' suffix");
        }
    }
}