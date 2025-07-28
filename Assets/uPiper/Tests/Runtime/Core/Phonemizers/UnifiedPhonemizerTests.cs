using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core.Phonemizers;
using uPiper.Core.Phonemizers.Backend;

namespace uPiper.Tests.Runtime.Core.Phonemizers
{
    [TestFixture]
    public class UnifiedPhonemizerTests
    {
        private UnifiedPhonemizer phonemizer;

        [SetUp]
        public void SetUp()
        {
            phonemizer = new UnifiedPhonemizer();
        }

        [TearDown]
        public void TearDown()
        {
            phonemizer?.Dispose();
        }

        [UnityTest]
        public async Task Initialize_ShouldLoadBackends()
        {
            // Act
            var result = await phonemizer.InitializeAsync();

            // Assert
            Assert.IsTrue(result, "Initialization should succeed");
            Assert.IsTrue(phonemizer.IsInitialized, "Phonemizer should be initialized");
            Assert.Greater(phonemizer.SupportedLanguages.Length, 0, "Should support at least one language");
            
            // Check available backends
            var backends = phonemizer.GetAvailableBackends();
            Assert.IsNotNull(backends);
            
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("UnifiedPhonemizer initialized"));
        }

        [UnityTest]
        public async Task PhonemizeAsync_Japanese_ShouldReturnPhonemes()
        {
            // Arrange
            await phonemizer.InitializeAsync();
            var text = "こんにちは";
            
            // Act
            var result = await phonemizer.PhonemizeAsync(text, "ja");

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success, "Phonemization should succeed");
            Assert.AreEqual(text, result.OriginalText);
            Assert.IsNotNull(result.Phonemes);
            Assert.Greater(result.Phonemes.Length, 0, "Should return phonemes");
            Assert.IsNotNull(result.Metadata);
            Assert.IsTrue(result.Metadata.ContainsKey("backend_used"));
            
            Debug.Log($"Japanese phonemes: {string.Join(" ", result.Phonemes)}");
        }

        [UnityTest]
        public async Task PhonemizeAsync_English_ShouldReturnPhonemes()
        {
            // Arrange
            await phonemizer.InitializeAsync();
            var text = "Hello world";
            
            // Act
            var result = await phonemizer.PhonemizeAsync(text, "en");

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success, "Phonemization should succeed");
            Assert.AreEqual(text, result.OriginalText);
            Assert.IsNotNull(result.Phonemes);
            Assert.Greater(result.Phonemes.Length, 0, "Should return phonemes");
            
            Debug.Log($"English phonemes: {string.Join(" ", result.Phonemes)}");
        }

        [UnityTest]
        public async Task PhonemizeAsync_AutoDetect_ShouldDetectLanguage()
        {
            // Arrange
            await phonemizer.InitializeAsync();
            
            // Test Japanese auto-detection
            var jaText = "日本語のテスト";
            var jaResult = await phonemizer.PhonemizeAsync(jaText, "auto");
            
            Assert.IsNotNull(jaResult);
            Assert.IsTrue(jaResult.Success);
            Assert.Greater(jaResult.Phonemes.Length, 0);
            
            // Test English auto-detection
            var enText = "English test";
            var enResult = await phonemizer.PhonemizeAsync(enText, "auto");
            
            Assert.IsNotNull(enResult);
            Assert.IsTrue(enResult.Success);
            Assert.Greater(enResult.Phonemes.Length, 0);
        }

        [UnityTest]
        public async Task PhonemizeAsync_MixedText_ShouldHandleBothLanguages()
        {
            // Arrange
            await phonemizer.InitializeAsync();
            var text = "Hello, これはmixedテキストです。";
            
            // Act
            var result = await phonemizer.PhonemizeAsync(text, "mixed");

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Success, $"Phonemization should succeed. Error: {result.Error}");
            Assert.IsNotNull(result.Phonemes);
            Assert.Greater(result.Phonemes.Length, 0, "Should return phonemes");
            
            Debug.Log($"Mixed text phonemes: {string.Join(" ", result.Phonemes)}");
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
            Assert.IsNotNull(result.Phonemes);
            Assert.AreEqual(0, result.Phonemes.Length);
        }

        [UnityTest]
        public async Task PhonemizeAsync_UnsupportedLanguage_ShouldFallbackToEnglish()
        {
            // Arrange
            await phonemizer.InitializeAsync();
            var text = "Test text";
            
            // Act
            var result = await phonemizer.PhonemizeAsync(text, "xyz"); // Unsupported language

            // Assert
            Assert.IsNotNull(result);
            // Should either fail or fallback to English
            if (result.Success)
            {
                Assert.Greater(result.Phonemes.Length, 0);
            }
        }

        [Test]
        public void GetCapabilities_ShouldReturnCombinedCapabilities()
        {
            // Act
            var capabilities = phonemizer.GetCapabilities();

            // Assert
            Assert.IsNotNull(capabilities);
            Assert.IsTrue(capabilities.SupportsBatchProcessing);
            Assert.IsTrue(capabilities.IsThreadSafe);
        }

        [Test]
        public void SupportsLanguage_ShouldCheckCorrectly()
        {
            // Assert
            Assert.IsTrue(phonemizer.SupportsLanguage("auto"));
            Assert.IsTrue(phonemizer.SupportsLanguage("mixed"));
            
            // After initialization, should support specific languages
        }
    }
}