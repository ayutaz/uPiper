using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core.Phonemizers;
using uPiper.Core.Phonemizers.Backend;

namespace uPiper.Tests.Runtime.Core.Phonemizers
{
    [TestFixture]
    [Timeout(30000)] // 30 second timeout for the entire test class
    // [Ignore("Temporarily disabled due to CMUDictionary loading issues")] // Re-enabled with proper timeout handling
    public class MixedLanguagePhonemizerTests
    {
        private MixedLanguagePhonemizer phonemizer;

        [SetUp]
        public void SetUp()
        {
            phonemizer = new MixedLanguagePhonemizer();
        }

        [TearDown]
        public void TearDown()
        {
            phonemizer?.Dispose();
        }

        [UnityTest]
        [Timeout(10000)] // 10 second timeout
        public async Task Initialize_ShouldSucceed()
        {
            // Act
            var result = await phonemizer.InitializeAsync();

            // Assert
            Assert.IsTrue(result, "Initialization should succeed");
            Assert.IsTrue(phonemizer.IsInitialized);
            
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("MixedLanguagePhonemizer initialized"));
        }

        [UnityTest]
        public async Task PhonemizeAsync_MixedJapaneseEnglish_ShouldProcessBothLanguages()
        {
            // Arrange
            await phonemizer.InitializeAsync();
            var text = "これはtest文章です。This is a mixed sentence.";
            
            // Act
            var result = await phonemizer.PhonemizeAsync(text, "mixed");

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success, $"Should succeed. Error: {result.Error}");
            Assert.IsNotNull(result.Phonemes);
            Assert.Greater(result.Phonemes.Length, 0);
            Assert.AreEqual("mixed", result.Language);
            
            // Check metadata
            Assert.IsNotNull(result.Metadata);
            Assert.IsTrue(result.Metadata.ContainsKey("segments"));
            Assert.IsTrue(result.Metadata.ContainsKey("backends_used"));
            
            Debug.Log($"Mixed phonemes: {string.Join(" ", result.Phonemes)}");
            Debug.Log($"Backends used: {string.Join(", ", (string[])result.Metadata["backends_used"])}");
        }

        [UnityTest]
        public async Task PhonemizeAsync_JapaneseOnly_ShouldUseJapaneseBackend()
        {
            // Arrange
            await phonemizer.InitializeAsync();
            var text = "日本語のテキストです";
            
            // Act
            var result = await phonemizer.PhonemizeAsync(text, "ja");

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);
            Assert.Greater(result.Phonemes.Length, 0);
            
            Debug.Log($"Japanese phonemes: {string.Join(" ", result.Phonemes)}");
        }

        [UnityTest]
        public async Task PhonemizeAsync_EnglishOnly_ShouldUseEnglishBackend()
        {
            // Arrange
            await phonemizer.InitializeAsync();
            var text = "This is English text";
            
            // Act
            var result = await phonemizer.PhonemizeAsync(text, "en");

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);
            Assert.Greater(result.Phonemes.Length, 0);
            
            Debug.Log($"English phonemes: {string.Join(" ", result.Phonemes)}");
        }

        [UnityTest]
        public async Task PhonemizeAsync_WithPunctuation_ShouldHandleCorrectly()
        {
            // Arrange
            await phonemizer.InitializeAsync();
            var text = "Hello! こんにちは。How are you? 元気ですか？";
            
            // Act
            var result = await phonemizer.PhonemizeAsync(text);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);
            Assert.Greater(result.Phonemes.Length, 0);
            
            // Should contain silence markers for punctuation
            Assert.Contains("_", result.Phonemes);
        }

        [Test]
        public void AnalyzeText_ShouldReturnCorrectStatistics()
        {
            // Arrange
            var text = "Hello world! これは日本語です。Mixed text example.";
            
            // Act
            var stats = phonemizer.AnalyzeText(text);

            // Assert
            Assert.IsNotNull(stats);
            Assert.IsTrue(stats.ContainsKey("segment_count"));
            Assert.IsTrue(stats.ContainsKey("language_segments"));
            Assert.IsTrue(stats.ContainsKey("primary_language"));
            
            Debug.Log($"Text analysis: segment_count={stats["segment_count"]}, primary_language={stats["primary_language"]}");
        }

        [UnityTest]
        public async Task PhonemizeAsync_EmptyText_ShouldReturnEmptyResult()
        {
            // Arrange
            await phonemizer.InitializeAsync();
            
            // Act
            var result = await phonemizer.PhonemizeAsync("");

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, result.Phonemes.Length);
        }

        [Test]
        public void GetCapabilities_ShouldReturnCorrectCapabilities()
        {
            // Act
            var caps = phonemizer.GetCapabilities();

            // Assert
            Assert.IsNotNull(caps);
            Assert.IsTrue(caps.SupportsIPA);
            Assert.IsTrue(caps.SupportsDuration);
            Assert.IsTrue(caps.SupportsBatchProcessing);
            Assert.IsTrue(caps.IsThreadSafe);
            Assert.IsFalse(caps.RequiresNetwork);
        }

        [Test]
        public void SupportsLanguage_ShouldCheckCorrectly()
        {
            // Assert
            Assert.IsTrue(phonemizer.SupportsLanguage("ja"));
            Assert.IsTrue(phonemizer.SupportsLanguage("en"));
            Assert.IsTrue(phonemizer.SupportsLanguage("mixed"));
            Assert.IsTrue(phonemizer.SupportsLanguage("auto"));
            Assert.IsFalse(phonemizer.SupportsLanguage("xyz"));
        }
    }
}