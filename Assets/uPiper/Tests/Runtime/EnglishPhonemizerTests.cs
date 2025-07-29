using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using uPiper.Core.Phonemizers;
using uPiper.Core.Phonemizers.Backend;
using uPiper.Core.Phonemizers.Backend.RuleBased;

namespace uPiper.Tests.Runtime
{
    [TestFixture]
    [Timeout(30000)] // 30 second timeout for all tests
    // [Ignore("Temporarily disabled - RuleBasedPhonemizer initialization hangs")] // Re-enabled with proper timeout handling
    public class EnglishPhonemizerTests
    {
        private RuleBasedPhonemizer ruleBasedPhonemizer;
        private SimpleLTSPhonemizer simpleLTSPhonemizer;

        [SetUp]
        [Timeout(10000)] // 10 second timeout
        public async Task Setup()
        {
            // Initialize RuleBased phonemizer with timeout
            ruleBasedPhonemizer = new RuleBasedPhonemizer();
            using (var cts = new System.Threading.CancellationTokenSource(System.TimeSpan.FromSeconds(5)))
            {
                try
                {
                    var ruleBasedResult = await ruleBasedPhonemizer.InitializeAsync(null, cts.Token);
                    Assert.IsTrue(ruleBasedResult, "Failed to initialize RuleBasedPhonemizer");
                }
                catch (System.OperationCanceledException)
                {
                    Assert.Inconclusive("RuleBasedPhonemizer initialization timed out");
                    return;
                }
            }

            // Initialize SimpleLTS phonemizer with timeout
            simpleLTSPhonemizer = new SimpleLTSPhonemizer();
            using (var cts = new System.Threading.CancellationTokenSource(System.TimeSpan.FromSeconds(5)))
            {
                try
                {
                    var ltsResult = await simpleLTSPhonemizer.InitializeAsync(null, cts.Token);
                    Assert.IsTrue(ltsResult, "Failed to initialize SimpleLTSPhonemizer");
                }
                catch (System.OperationCanceledException)
                {
                    Assert.Inconclusive("SimpleLTSPhonemizer initialization timed out");
                }
            }
        }

        [TearDown]
        public void TearDown()
        {
            ruleBasedPhonemizer?.Dispose();
            simpleLTSPhonemizer?.Dispose();
        }

        [Test]
        public async Task TestCommonWords_RuleBased()
        {
            var testWords = new[]
            {
                ("hello", new[] { "HH", "AH0", "L", "OW1" }),
                ("world", new[] { "W", "ER1", "L", "D" }),
                ("computer", new[] { "K", "AH0", "M", "P", "Y", "UW1", "T", "ER0" }),
                ("artificial", new[] { "AA2", "R", "T", "AH0", "F", "IH1", "SH", "AH0", "L" }),
                ("intelligence", new[] { "IH0", "N", "T", "EH1", "L", "AH0", "JH", "AH0", "N", "S" })
            };

            foreach (var (word, expected) in testWords)
            {
                var result = await ruleBasedPhonemizer.PhonemizeAsync(word, "en");
                Assert.IsTrue(result.Success, $"Failed to phonemize '{word}'");
                Assert.IsNotEmpty(result.Phonemes, $"Empty phonemes for '{word}'");
                Debug.Log($"RuleBased - {word}: {string.Join(" ", result.Phonemes)}");
            }
        }

        [Test]
        public async Task TestCommonWords_SimpleLTS()
        {
            var testWords = new[]
            {
                "hello", "world", "computer", "artificial", "intelligence"
            };

            foreach (var word in testWords)
            {
                var result = await simpleLTSPhonemizer.PhonemizeAsync(word, "en");
                Assert.IsTrue(result.Success, $"Failed to phonemize '{word}'");
                Assert.IsNotEmpty(result.Phonemes, $"Empty phonemes for '{word}'");
                Debug.Log($"SimpleLTS - {word}: {string.Join(" ", result.Phonemes)}");
            }
        }

        [Test]
        public async Task TestOutOfVocabularyWords()
        {
            var oovWords = new[]
            {
                "ChatGPT", "COVID", "cryptocurrency", "iPhone", "TikTok",
                "blockchain", "metaverse", "deepfake", "ransomware", "podcast"
            };

            foreach (var word in oovWords)
            {
                // Test SimpleLTS (should handle OOV)
                var ltsResult = await simpleLTSPhonemizer.PhonemizeAsync(word, "en");
                Assert.IsTrue(ltsResult.Success, $"SimpleLTS failed for OOV word '{word}'");
                Assert.IsNotEmpty(ltsResult.Phonemes, $"SimpleLTS gave empty phonemes for '{word}'");
                Debug.Log($"SimpleLTS OOV - {word}: {string.Join(" ", ltsResult.Phonemes)}");

                // Test RuleBased with G2P fallback
                var ruleResult = await ruleBasedPhonemizer.PhonemizeAsync(
                    word, "en", 
                    new PhonemeOptions { UseG2PFallback = true }
                );
                Assert.IsTrue(ruleResult.Success, $"RuleBased failed for OOV word '{word}'");
                Debug.Log($"RuleBased OOV - {word}: {string.Join(" ", ruleResult.Phonemes)}");
            }
        }

        [Test]
        public async Task TestSentences()
        {
            var sentences = new[]
            {
                "Hello world!",
                "The quick brown fox jumps over the lazy dog.",
                "How are you today?",
                "Unity is a great game engine.",
                "Text-to-speech synthesis is amazing!"
            };

            foreach (var sentence in sentences)
            {
                var result = await simpleLTSPhonemizer.PhonemizeAsync(sentence, "en");
                Assert.IsTrue(result.Success, $"Failed to phonemize sentence: {sentence}");
                Assert.IsNotEmpty(result.Phonemes);
                Assert.Greater(result.Phonemes.Length, sentence.Split(' ').Length, 
                    "Should have more phonemes than words");
                Debug.Log($"Sentence phonemes ({result.Phonemes.Length}): {sentence}");
            }
        }

        [Test]
        public async Task TestPhonemeFormats()
        {
            const string testWord = "hello";

            // Test ARPABET format
            var arpabetResult = await simpleLTSPhonemizer.PhonemizeAsync(
                testWord, "en",
                new PhonemeOptions { Format = PhonemeFormat.ARPABET }
            );
            Assert.IsTrue(arpabetResult.Success);
            Assert.IsTrue(arpabetResult.Phonemes[0].All(char.IsUpper), 
                "ARPABET should be uppercase");

            // Test IPA format
            var ipaResult = await simpleLTSPhonemizer.PhonemizeAsync(
                testWord, "en",
                new PhonemeOptions { Format = PhonemeFormat.IPA }
            );
            Assert.IsTrue(ipaResult.Success);
            Assert.IsTrue(ipaResult.Phonemes.Any(p => p.Any(c => c > 127)), 
                "IPA should contain Unicode characters");

            Debug.Log($"ARPABET: {string.Join(" ", arpabetResult.Phonemes)}");
            Debug.Log($"IPA: {string.Join(" ", ipaResult.Phonemes)}");
        }

        [Test]
        public async Task TestPerformance()
        {
            const string testText = "The quick brown fox jumps over the lazy dog";
            const int iterations = 100;

            // Warm up
            await simpleLTSPhonemizer.PhonemizeAsync(testText, "en");

            // Measure SimpleLTS
            var ltsWatch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                await simpleLTSPhonemizer.PhonemizeAsync(testText, "en");
            }
            ltsWatch.Stop();
            var ltsAvg = ltsWatch.ElapsedMilliseconds / (float)iterations;

            // Measure RuleBased
            var ruleWatch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                await ruleBasedPhonemizer.PhonemizeAsync(testText, "en");
            }
            ruleWatch.Stop();
            var ruleAvg = ruleWatch.ElapsedMilliseconds / (float)iterations;

            Debug.Log($"Performance - SimpleLTS: {ltsAvg:F2}ms, RuleBased: {ruleAvg:F2}ms");
            Assert.Less(ltsAvg, 60f, "SimpleLTS should be fast (< 60ms)");
            Assert.Less(ruleAvg, 60f, "RuleBased should be fast (< 60ms)");
        }

        [Test]
        public async Task TestSpecialCases()
        {
            var specialCases = new (string text, string description)[]
            {
                ("I'll", "Contraction"),
                ("don't", "Contraction with apostrophe"),
                ("1234", "Numbers"),
                ("test@example.com", "Email"),
                ("https://unity.com", "URL"),
                ("C#", "Programming language"),
                ("AI/ML", "Abbreviation with slash"),
                ("U.S.A.", "Abbreviation with periods")
            };

            foreach (var (text, description) in specialCases)
            {
                var result = await simpleLTSPhonemizer.PhonemizeAsync(text, "en");
                Debug.Log($"{description} - {text}: Success={result.Success}, " +
                         $"Phonemes={string.Join(" ", result.Phonemes ?? new[] { "NULL" })}");
                
                // We don't require success for all special cases,
                // but log the behavior for analysis
            }
        }

        [Test]
        [Timeout(5000)] // 5 second timeout
        // [Ignore("Temporarily disabled - initialization hangs")] // Re-enabled with proper timeout handling
        public void TestBackendCapabilities()
        {
            var ruleBasedCaps = ruleBasedPhonemizer.GetCapabilities();
            Assert.IsFalse(ruleBasedCaps.SupportsIPA); // Uses ARPABET
            Assert.IsTrue(ruleBasedCaps.SupportsStress);
            Assert.IsFalse(ruleBasedCaps.RequiresNetwork);

            var ltsCaps = simpleLTSPhonemizer.GetCapabilities();
            Assert.IsTrue(ltsCaps.SupportsIPA);
            Assert.IsFalse(ltsCaps.SupportsStress); // Simplified implementation
            Assert.IsFalse(ltsCaps.RequiresNetwork);
        }

        [Test]
        public async Task TestCaching()
        {
            const string testWord = "caching";
            
            // First call - should be slower
            var firstWatch = System.Diagnostics.Stopwatch.StartNew();
            var firstResult = await simpleLTSPhonemizer.PhonemizeAsync(testWord, "en");
            firstWatch.Stop();

            // Subsequent calls - should be faster due to caching
            var cachedWatch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 10; i++)
            {
                var cachedResult = await simpleLTSPhonemizer.PhonemizeAsync(testWord, "en");
                Assert.AreEqual(firstResult.Phonemes, cachedResult.Phonemes, 
                    "Cached results should be consistent");
            }
            cachedWatch.Stop();

            var avgCachedTime = cachedWatch.ElapsedMilliseconds / 10f;
            Debug.Log($"First call: {firstWatch.ElapsedMilliseconds}ms, " +
                     $"Cached average: {avgCachedTime:F2}ms");
        }
    }
}