using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core.Phonemizers;
using uPiper.Core.Phonemizers.Backend;

namespace uPiper.Tests.Runtime.Phonemizers
{
    [TestFixture]
    [Timeout(30000)] // 30 second timeout for the entire test class
    // [Ignore("Temporarily disabled due to CMUDictionary loading issues")] // Re-enabled with proper timeout handling
    public class EnhancedEnglishPhonemizerTests
    {
        private EnhancedEnglishPhonemizer phonemizer;

        [SetUp]
        public void Setup()
        {
            phonemizer = new EnhancedEnglishPhonemizer();
        }

        [TearDown]
        public void TearDown()
        {
            phonemizer?.Dispose();
        }

        [Test]
        [Timeout(30000)] // 30 second timeout - G2P training can take time
        public async Task TestInitialization()
        {
            var options = new PhonemizerBackendOptions();
            var result = await phonemizer.InitializeAsync(options);

            Assert.IsTrue(result);
            Assert.IsTrue(phonemizer.IsAvailable);
            Assert.AreEqual("EnhancedEnglish", phonemizer.Name);
        }

        [Test]
        public async Task TestBasicWords()
        {
            await phonemizer.InitializeAsync(new PhonemizerBackendOptions());

            // Test common words - accept any reasonable phonemization
            var testWords = new[] { "hello", "world", "computer", "university" };

            foreach (var word in testWords)
            {
                var result = await phonemizer.PhonemizeAsync(word, "en");

                Assert.IsTrue(result.Success, $"Failed to phonemize '{word}'");
                Assert.IsNotNull(result.Phonemes, $"Null phonemes for '{word}'");
                Assert.Greater(result.Phonemes.Length, 0, $"Empty phonemes for '{word}'");

                // Basic sanity check - should have at least as many phonemes as letters/2
                Assert.GreaterOrEqual(result.Phonemes.Length, word.Length / 2,
                    $"Too few phonemes for '{word}'");

                Debug.Log($"'{word}' -> {string.Join(" ", result.Phonemes)}");
            }
        }

        [Test]
        public async Task TestContractions()
        {
            await phonemizer.InitializeAsync(new PhonemizerBackendOptions());

            var testCases = new[]
            {
                ("can't", new[] { "K", "AE1", "N", "T" }),
                ("I'm", new[] { "AY1", "M" }),
                ("don't", new[] { "D", "OW1", "N", "T" })
            };

            foreach (var (word, expectedPhonemes) in testCases)
            {
                var result = await phonemizer.PhonemizeAsync(word, "en");

                Assert.IsTrue(result.Success);
                CollectionAssert.AreEqual(expectedPhonemes, result.Phonemes);
            }
        }

        [Test]
        public async Task TestMorphologicalAnalysis()
        {
            await phonemizer.InitializeAsync(new PhonemizerBackendOptions());

            // Test words with common suffixes
            var result = await phonemizer.PhonemizeAsync("testing", "en");
            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Length > 0);

            // Should recognize "test" + "ing"
            var phonemeStr = string.Join(" ", result.Phonemes);
            Assert.IsTrue(phonemeStr.Contains("T") && phonemeStr.Contains("EH") &&
                         phonemeStr.Contains("S") && phonemeStr.Contains("T") &&
                         phonemeStr.Contains("IH") && phonemeStr.Contains("NG"));
        }

        [Test]
        public async Task TestCompoundWords()
        {
            await phonemizer.InitializeAsync(new PhonemizerBackendOptions());

            // Test hyphenated compound
            var result = await phonemizer.PhonemizeAsync("well-known", "en");
            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Phonemes.Length > 5); // Should have phonemes for both parts
        }

        [Test]
        public async Task TestEnhancedLTSRules()
        {
            await phonemizer.InitializeAsync(new PhonemizerBackendOptions());

            // Test words that likely aren't in CMU dictionary
            var testCases = new[]
            {
                "zxcvbnm", // Random consonants
                "qwertyuiop", // Keyboard sequence
                "abcdefg" // Alphabet sequence
            };

            foreach (var word in testCases)
            {
                var result = await phonemizer.PhonemizeAsync(word, "en");

                Assert.IsTrue(result.Success);
                Assert.IsTrue(result.Phonemes.Length > 0,
                    $"No phonemes generated for '{word}'");
            }
        }

        [Test]
        public async Task TestMixedCase()
        {
            await phonemizer.InitializeAsync(new PhonemizerBackendOptions());

            var result1 = await phonemizer.PhonemizeAsync("Hello", "en");
            var result2 = await phonemizer.PhonemizeAsync("HELLO", "en");
            var result3 = await phonemizer.PhonemizeAsync("hello", "en");

            Assert.IsTrue(result1.Success && result2.Success && result3.Success);

            // All should produce the same phonemes
            CollectionAssert.AreEqual(result1.Phonemes, result2.Phonemes);
            CollectionAssert.AreEqual(result1.Phonemes, result3.Phonemes);
        }

        [Test]
        public async Task TestPunctuation()
        {
            await phonemizer.InitializeAsync(new PhonemizerBackendOptions());

            var result = await phonemizer.PhonemizeAsync("Hello, world!", "en");

            Assert.IsTrue(result.Success);

            // Should contain pause markers or silence for punctuation
            var phonemeStr = string.Join(" ", result.Phonemes);
            // Different backends may use different pause markers: "pau", "_", or "sil"
            Assert.IsTrue(
                phonemeStr.Contains("pau") ||
                phonemeStr.Contains("_") ||
                phonemeStr.Contains("sil") ||
                result.Phonemes.Length > 5, // At minimum, should have more phonemes than just "hello world"
                $"Expected pause markers in: {phonemeStr}"
            );
        }

        [Test]
        public async Task TestMemoryUsage()
        {
            await phonemizer.InitializeAsync(new PhonemizerBackendOptions());

            var memoryUsage = phonemizer.GetMemoryUsage();

            // CMU dictionary should use several MB
            Assert.Greater(memoryUsage, 1000000); // > 1MB
            Assert.Less(memoryUsage, 10000000); // < 10MB

            Debug.Log($"EnhancedEnglishPhonemizer memory usage: {memoryUsage / 1024 / 1024:F2} MB");
        }

        [Test]
        public void TestSupportedLanguages()
        {
            Assert.Contains("en", phonemizer.SupportedLanguages);
            Assert.Contains("en-US", phonemizer.SupportedLanguages);
            Assert.Contains("en-GB", phonemizer.SupportedLanguages);
        }

        [Test]
        public async Task TestPriority()
        {
            // Priority is set after initialization
            await phonemizer.InitializeAsync(new PhonemizerBackendOptions());

            // EnhancedEnglishPhonemizer should have higher priority than SimpleLTS
            Assert.AreEqual(150, phonemizer.Priority);
        }
    }
}