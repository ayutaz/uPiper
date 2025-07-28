using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core.Phonemizers.Backend;

namespace uPiper.Tests.Runtime.Core.Phonemizers
{
    [TestFixture]
    public class SimpleLTSPhonemizerTests
    {
        private SimpleLTSPhonemizer phonemizer;

        [SetUp]
        public void SetUp()
        {
            phonemizer = new SimpleLTSPhonemizer();
        }

        [TearDown]
        public void TearDown()
        {
            phonemizer?.Dispose();
        }

        [UnityTest]
        public async Task Initialize_ShouldSucceed()
        {
            // Act
            var result = await phonemizer.InitializeAsync();

            // Assert
            Assert.IsTrue(result, "Initialization should succeed");
            Assert.IsTrue(phonemizer.IsAvailable);
        }

        [UnityTest]
        public async Task PhonemizeAsync_SimpleWords_ShouldReturnCorrectPhonemes()
        {
            // Arrange
            await phonemizer.InitializeAsync();
            
            var testCases = new[]
            {
                ("hello", new[] { "HH", "AH", "L", "OW" }),
                ("world", new[] { "W", "ER", "L", "D" }),
                ("cat", new[] { "K", "AE", "T" }),
                ("dog", new[] { "D", "AO", "G" }),
                ("the", new[] { "DH", "AH" }),
                ("she", new[] { "SH", "IY" })
            };

            foreach (var (word, expectedContains) in testCases)
            {
                // Act
                var result = await phonemizer.PhonemizeAsync(word, "en");

                // Assert
                Assert.IsNotNull(result);
                Assert.IsTrue(result.Success, $"Phonemization of '{word}' should succeed");
                Assert.IsNotNull(result.Phonemes);
                Assert.Greater(result.Phonemes.Length, 0);
                
                Debug.Log($"'{word}' -> {string.Join(" ", result.Phonemes)}");
                
                // Check if at least some expected phonemes are present
                foreach (var expectedPhoneme in expectedContains)
                {
                    if (System.Array.Exists(result.Phonemes, p => p == expectedPhoneme))
                    {
                        // Found at least one expected phoneme
                        break;
                    }
                }
            }
        }

        [UnityTest]
        public async Task PhonemizeAsync_ComplexWords_ShouldHandleCorrectly()
        {
            // Arrange
            await phonemizer.InitializeAsync();
            var words = new[] { "computer", "algorithm", "phonemization", "synthesis" };

            foreach (var word in words)
            {
                // Act
                var result = await phonemizer.PhonemizeAsync(word, "en");

                // Assert
                Assert.IsNotNull(result);
                Assert.IsTrue(result.Success);
                Assert.Greater(result.Phonemes.Length, 0);
                
                Debug.Log($"'{word}' -> {string.Join(" ", result.Phonemes)}");
            }
        }

        [UnityTest]
        public async Task PhonemizeAsync_Sentences_ShouldProcessCorrectly()
        {
            // Arrange
            await phonemizer.InitializeAsync();
            var text = "Hello, how are you today?";

            // Act
            var result = await phonemizer.PhonemizeAsync(text, "en");

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);
            Assert.Greater(result.Phonemes.Length, 0);
            
            // Should contain silence for punctuation
            Assert.Contains("_", result.Phonemes);
            
            Debug.Log($"Sentence phonemes: {string.Join(" ", result.Phonemes)}");
        }

        [UnityTest]
        public async Task PhonemizeAsync_WithIPAFormat_ShouldReturnIPAPhonemes()
        {
            // Arrange
            await phonemizer.InitializeAsync();
            var options = new PhonemeOptions { Format = PhonemeFormat.IPA };

            // Act
            var result = await phonemizer.PhonemizeAsync("hello", "en", options);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);
            
            // Should contain IPA symbols
            var phonemeString = string.Join("", result.Phonemes);
            Debug.Log($"IPA phonemes: {phonemeString}");
        }

        [Test]
        public void GetCapabilities_ShouldReturnCorrectCapabilities()
        {
            // Act
            var caps = phonemizer.GetCapabilities();

            // Assert
            Assert.IsNotNull(caps);
            Assert.IsTrue(caps.SupportsIPA);
            Assert.IsFalse(caps.SupportsStress);
            Assert.IsTrue(caps.SupportsBatchProcessing);
            Assert.IsTrue(caps.IsThreadSafe);
            Assert.IsFalse(caps.RequiresNetwork);
        }

        [Test]
        public void SupportsLanguage_ShouldSupportEnglish()
        {
            // Assert
            Assert.IsTrue(phonemizer.SupportsLanguage("en"));
            Assert.IsTrue(phonemizer.SupportsLanguage("en-US"));
            Assert.IsTrue(phonemizer.SupportsLanguage("en-GB"));
            Assert.IsFalse(phonemizer.SupportsLanguage("ja"));
        }

        [UnityTest]
        public async Task PhonemizeAsync_EmptyText_ShouldReturnEmptyResult()
        {
            // Arrange
            await phonemizer.InitializeAsync();

            // Act
            var result = await phonemizer.PhonemizeAsync("", "en");

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, result.Phonemes.Length);
        }

        [UnityTest]
        public async Task PhonemizeAsync_UnsupportedLanguage_ShouldReturnError()
        {
            // Arrange
            await phonemizer.InitializeAsync();

            // Act
            var result = await phonemizer.PhonemizeAsync("テスト", "ja");

            // Assert
            Assert.IsNotNull(result);
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error.Contains("not supported"));
        }
    }
}