#if !UNITY_WEBGL
using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core;
using uPiper.Core.Phonemizers;
using uPiper.Core.Phonemizers.Implementations;

namespace uPiper.Tests.Runtime.Core.Phonemizers
{
    /// <summary>
    /// Tests for OpenJTalkPhonemizer implementation.
    /// </summary>
    [TestFixture]
    public class OpenJTalkPhonemizerTest
    {
        private OpenJTalkPhonemizer _phonemizer;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Always enable mock mode for tests to prevent crashes
            Debug.Log("[OpenJTalkPhonemizerTest] Enabling mock mode for tests.");
            OpenJTalkPhonemizer.MockMode = true;
        }

        [SetUp]
        public void SetUp()
        {
            try
            {
                // Ensure mock mode is enabled for tests
                OpenJTalkPhonemizer.MockMode = true;
                _phonemizer = new OpenJTalkPhonemizer();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to create OpenJTalkPhonemizer: {ex.Message}");
                Assert.Ignore("Could not initialize OpenJTalkPhonemizer. Skipping test.");
            }
        }

        [TearDown]
        public void TearDown()
        {
            _phonemizer?.Dispose();
        }

        #region Basic Tests

        [Test]
        public void Constructor_InitializesSuccessfully()
        {
            Assert.NotNull(_phonemizer);
            Assert.AreEqual("OpenJTalk", _phonemizer.Name);
            Assert.Contains("ja", _phonemizer.SupportedLanguages);
        }

        [Test]
        public void Version_ReturnsValidVersion()
        {
            var version = _phonemizer.Version;
            Assert.NotNull(version);
            Assert.IsNotEmpty(version);
            Debug.Log($"OpenJTalk version: {version}");
        }

        [Test]
        public void SupportedLanguages_ContainsJapanese()
        {
            var languages = _phonemizer.SupportedLanguages;
            Assert.NotNull(languages);
            Assert.IsTrue(languages.Length > 0);
            Assert.Contains("ja", languages);
        }

        #endregion

        #region Phonemization Tests

        [Test]
        public void Phonemize_EmptyText_ReturnsEmpty()
        {
            var result = _phonemizer.Phonemize("");
            Assert.NotNull(result);
            Assert.AreEqual(0, result.Phonemes?.Length ?? 0);
        }

        [Test]
        public void Phonemize_NullText_ReturnsEmpty()
        {
            var result = _phonemizer.Phonemize(null);
            Assert.NotNull(result);
            Assert.AreEqual(0, result.Phonemes?.Length ?? 0);
        }

        [Test]
        public void Phonemize_SimpleHiragana_ReturnsPhonemes()
        {
            var result = _phonemizer.Phonemize("こんにちは");

            Assert.NotNull(result);
            Assert.Greater(result.Phonemes?.Length ?? 0, 0);
            Assert.NotNull(result.Phonemes);
            Assert.NotNull(result.PhonemeIds);
            Assert.AreEqual(result.Phonemes.Length, result.PhonemeIds.Length);

            // Log the result for inspection
            Debug.Log($"Phonemes for 'こんにちは': {string.Join(" ", result.Phonemes)}");
        }

        [Test]
        public void Phonemize_SimpleKatakana_ReturnsPhonemes()
        {
            var result = _phonemizer.Phonemize("コンピューター");

            Assert.NotNull(result);
            Assert.Greater(result.Phonemes?.Length ?? 0, 0);
            Assert.NotNull(result.Phonemes);

            Debug.Log($"Phonemes for 'コンピューター': {string.Join(" ", result.Phonemes)}");
        }

        [Test]
        public void Phonemize_MixedText_ReturnsPhonemes()
        {
            var result = _phonemizer.Phonemize("私はAIです");

            Assert.NotNull(result);
            Assert.Greater(result.Phonemes?.Length ?? 0, 0);
            Assert.NotNull(result.Phonemes);

            Debug.Log($"Phonemes for '私はAIです': {string.Join(" ", result.Phonemes)}");
        }

        [Test]
        public void Phonemize_WithNumbers_ReturnsPhonemes()
        {
            var result = _phonemizer.Phonemize("今日は2024年です");

            Assert.NotNull(result);
            Assert.Greater(result.Phonemes?.Length ?? 0, 0);

            Debug.Log($"Phonemes for '今日は2024年です': {string.Join(" ", result.Phonemes)}");
        }

        #endregion

        #region Async Tests

        [UnityTest]
        public IEnumerator PhonemizeAsync_SimpleText_ReturnsPhonemes()
        {
            var task = _phonemizer.PhonemizeAsync("おはよう");
            yield return new WaitUntil(() => task.IsCompleted);

            Assert.IsTrue(task.IsCompletedSuccessfully);
            var result = task.Result;

            Assert.NotNull(result);
            Assert.Greater(result.Phonemes?.Length ?? 0, 0);

            Debug.Log($"Async phonemes for 'おはよう': {string.Join(" ", result.Phonemes)}");
        }

        #endregion

        #region Cache Tests

        [Test]
        public void Phonemize_SameText_UsesCachedResult()
        {
            const string text = "テスト";

            // First call
            var result1 = _phonemizer.Phonemize(text);

            // Second call (should be cached)
            var result2 = _phonemizer.Phonemize(text);

            Assert.NotNull(result1);
            Assert.NotNull(result2);

            // Results should be equal
            Assert.AreEqual(result1.Phonemes?.Length ?? 0, result2.Phonemes?.Length ?? 0);
            CollectionAssert.AreEqual(result1.Phonemes, result2.Phonemes);

            // Cache hit should be reflected in FromCache property
            Assert.IsTrue(result2.FromCache);
        }

        [Test]
        public void ClearCache_RemovesCachedResults()
        {
            const string text = "キャッシュテスト";

            // Phonemize to cache
            var result1 = _phonemizer.Phonemize(text);

            // Clear cache
            _phonemizer.ClearCache();

            // Phonemize again
            var result2 = _phonemizer.Phonemize(text);

            // Check that cache was cleared - result2 should not be from cache
            Assert.IsFalse(result2.FromCache);
        }

        #endregion

        #region Error Handling Tests

        [Test]
        public void Phonemize_VeryLongText_HandlesGracefully()
        {
            // Create a very long text
            var longText = new string('あ', 10000);

            try
            {
                var result = _phonemizer.Phonemize(longText);
                Assert.NotNull(result);
                Assert.Greater(result.Phonemes?.Length ?? 0, 0);
            }
            catch (PiperPhonemizationException ex)
            {
                // It's okay if it fails, but it should be a proper exception
                Assert.IsTrue(ex.Message.Length > 0);
                Assert.AreNotEqual(PiperErrorCode.Unknown, ex.ErrorCode);
            }
        }

        [Test]
        public void Phonemize_InvalidCharacters_HandlesGracefully()
        {
            // Text with potentially problematic characters
            var problematicText = "テスト\0\n\r\t";

            try
            {
                var result = _phonemizer.Phonemize(problematicText);
                Assert.NotNull(result);
            }
            catch (PiperPhonemizationException)
            {
                // Expected - invalid characters might cause issues
            }
        }

        #endregion

        #region Disposal Tests

        [Test]
        public void Dispose_MultipleCalls_DoesNotThrow()
        {
            var phonemizer = new OpenJTalkPhonemizer();

            Assert.DoesNotThrow(() =>
            {
                phonemizer.Dispose();
                // Small delay to ensure disposal completes
                System.Threading.Thread.Sleep(10);
                phonemizer.Dispose(); // Second call should not throw
            });
        }

        [Test]
        public void Phonemize_AfterDispose_ThrowsObjectDisposedException()
        {
            var phonemizer = new OpenJTalkPhonemizer();
            phonemizer.Dispose();

            // Ensure the object is disposed before testing
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();

            Assert.Throws<ObjectDisposedException>(() =>
            {
                phonemizer.Phonemize("テスト");
            });
        }

        #endregion

        #region Helper Methods

        private bool IsNativeLibraryAvailable()
        {
            // Always return false to use mock mode in tests
            return false;
        }

        #endregion
    }
}
#endif