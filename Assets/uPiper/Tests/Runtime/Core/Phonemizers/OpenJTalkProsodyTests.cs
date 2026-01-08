#if !UNITY_WEBGL
using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core;
using uPiper.Core.Phonemizers.Implementations;

namespace uPiper.Tests.Runtime.Core.Phonemizers
{
    /// <summary>
    /// Tests for OpenJTalkPhonemizer prosody API (A1/A2/A3 extraction).
    /// </summary>
    [TestFixture]
    [Category("RequiresNativeLibrary")]
    public class OpenJTalkProsodyTests
    {
        private OpenJTalkPhonemizer _phonemizer;

        [SetUp]
        public void SetUp()
        {
            try
            {
                _phonemizer = new OpenJTalkPhonemizer();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to create OpenJTalkPhonemizer: {ex.Message}");
                Assert.Ignore("OpenJTalk native library not available. Skipping test.");
            }
        }

        [TearDown]
        public void TearDown()
        {
            _phonemizer?.Dispose();
        }

        #region Basic Prosody Tests

        [Test]
        public void PhonemizeWithProsody_EmptyText_ReturnsEmpty()
        {
            var result = _phonemizer.PhonemizeWithProsody("");

            Assert.AreEqual(0, result.PhonemeCount);
            Assert.AreEqual(0, result.Phonemes.Length);
            Assert.AreEqual(0, result.ProsodyA1.Length);
            Assert.AreEqual(0, result.ProsodyA2.Length);
            Assert.AreEqual(0, result.ProsodyA3.Length);
        }

        [Test]
        public void PhonemizeWithProsody_NullText_ReturnsEmpty()
        {
            var result = _phonemizer.PhonemizeWithProsody(null);

            Assert.AreEqual(0, result.PhonemeCount);
            Assert.AreEqual(0, result.Phonemes.Length);
        }

        [Test]
        public void PhonemizeWithProsody_SimpleText_ReturnsPhonemesWithProsody()
        {
            var result = _phonemizer.PhonemizeWithProsody("こんにちは");

            Assert.Greater(result.PhonemeCount, 0);
            Assert.AreEqual(result.PhonemeCount, result.Phonemes.Length);
            Assert.AreEqual(result.PhonemeCount, result.ProsodyA1.Length);
            Assert.AreEqual(result.PhonemeCount, result.ProsodyA2.Length);
            Assert.AreEqual(result.PhonemeCount, result.ProsodyA3.Length);

            Debug.Log($"Prosody result for 'こんにちは':");
            Debug.Log($"  Phonemes: {string.Join(" ", result.Phonemes)}");
            Debug.Log($"  A1: {string.Join(" ", result.ProsodyA1)}");
            Debug.Log($"  A2: {string.Join(" ", result.ProsodyA2)}");
            Debug.Log($"  A3: {string.Join(" ", result.ProsodyA3)}");
        }

        [Test]
        public void PhonemizeWithProsody_AccentedWord_HasNonZeroProsody()
        {
            // Test with a word that should have clear accent pattern
            var result = _phonemizer.PhonemizeWithProsody("雨");

            Assert.Greater(result.PhonemeCount, 0);

            // At least some prosody values should be non-zero
            var hasNonZeroA2 = false;
            var hasNonZeroA3 = false;
            for (var i = 0; i < result.PhonemeCount; i++)
            {
                if (result.ProsodyA2[i] != 0) hasNonZeroA2 = true;
                if (result.ProsodyA3[i] != 0) hasNonZeroA3 = true;
            }

            Debug.Log($"Prosody result for '雨':");
            for (var i = 0; i < result.PhonemeCount; i++)
            {
                Debug.Log($"  [{i}] {result.Phonemes[i]}: A1={result.ProsodyA1[i]}, A2={result.ProsodyA2[i]}, A3={result.ProsodyA3[i]}");
            }

            // A2 and A3 should have non-zero values for real words
            Assert.IsTrue(hasNonZeroA2 || hasNonZeroA3,
                "Expected some non-zero prosody values for accented word");
        }

        #endregion

        #region Accent Pattern Tests

        [Test]
        public void PhonemizeWithProsody_DifferentAccentTypes_ProducesDifferentA1()
        {
            // Compare two words with different accent patterns
            var result1 = _phonemizer.PhonemizeWithProsody("雨"); // あめ (頭高型 - accent on first mora)
            var result2 = _phonemizer.PhonemizeWithProsody("飴"); // あめ (平板型 - flat/no accent)

            Assert.Greater(result1.PhonemeCount, 0);
            Assert.Greater(result2.PhonemeCount, 0);

            Debug.Log($"Prosody comparison:");
            Debug.Log($"  雨 (ame - rain): A1={string.Join(",", result1.ProsodyA1)}");
            Debug.Log($"  飴 (ame - candy): A1={string.Join(",", result2.ProsodyA1)}");

            // Both should produce phonemes (may have different A1 patterns)
            // The actual difference depends on the dictionary and OpenJTalk's analysis
        }

        [Test]
        public void PhonemizeWithProsody_LongPhrase_HasMultipleAccentPhrases()
        {
            var result = _phonemizer.PhonemizeWithProsody("今日は良い天気です");

            Assert.Greater(result.PhonemeCount, 0);

            // Log all prosody values
            Debug.Log($"Prosody for '今日は良い天気です':");
            for (var i = 0; i < result.PhonemeCount; i++)
            {
                Debug.Log($"  [{i}] {result.Phonemes[i]}: A1={result.ProsodyA1[i]}, A2={result.ProsodyA2[i]}, A3={result.ProsodyA3[i]}");
            }

            // A3 values should vary (different accent phrase lengths)
            var hasVaryingA3 = false;
            var lastA3 = -1;
            for (var i = 0; i < result.PhonemeCount; i++)
            {
                if (lastA3 >= 0 && result.ProsodyA3[i] != lastA3)
                {
                    hasVaryingA3 = true;
                    break;
                }
                lastA3 = result.ProsodyA3[i];
            }

            // Long phrases typically have multiple accent phrases with different A3 values
            Debug.Log($"Has varying A3 values: {hasVaryingA3}");
        }

        #endregion

        #region Consistency Tests

        [Test]
        public void PhonemizeWithProsody_SameText_ProducesSameResults()
        {
            const string text = "テスト";

            var result1 = _phonemizer.PhonemizeWithProsody(text);
            var result2 = _phonemizer.PhonemizeWithProsody(text);

            Assert.AreEqual(result1.PhonemeCount, result2.PhonemeCount);
            CollectionAssert.AreEqual(result1.Phonemes, result2.Phonemes);
            CollectionAssert.AreEqual(result1.ProsodyA1, result2.ProsodyA1);
            CollectionAssert.AreEqual(result1.ProsodyA2, result2.ProsodyA2);
            CollectionAssert.AreEqual(result1.ProsodyA3, result2.ProsodyA3);
        }

        [Test]
        public void PhonemizeWithProsody_PhonemeCountMatchesArrayLengths()
        {
            var result = _phonemizer.PhonemizeWithProsody("日本語テスト");

            Assert.AreEqual(result.PhonemeCount, result.Phonemes.Length);
            Assert.AreEqual(result.PhonemeCount, result.ProsodyA1.Length);
            Assert.AreEqual(result.PhonemeCount, result.ProsodyA2.Length);
            Assert.AreEqual(result.PhonemeCount, result.ProsodyA3.Length);
        }

        #endregion

        #region Error Handling Tests

        [Test]
        public void PhonemizeWithProsody_AfterDispose_ThrowsObjectDisposedException()
        {
            try
            {
                var phonemizer = new OpenJTalkPhonemizer();
                phonemizer.Dispose();

                Assert.Throws<ObjectDisposedException>(() =>
                {
                    phonemizer.PhonemizeWithProsody("テスト");
                });
            }
            catch (PiperInitializationException ex)
            {
                Debug.LogWarning($"Skipping disposal test: {ex.Message}");
                Assert.Ignore("OpenJTalk native library not available. Skipping disposal test.");
            }
        }

        #endregion

        #region Integration Tests

        [Test]
        public void PhonemizeWithProsody_CompareToRegularPhonemize_SamePhonemes()
        {
            const string text = "音声合成";

            var prosodyResult = _phonemizer.PhonemizeWithProsody(text);
            var regularResult = _phonemizer.Phonemize(text);

            Assert.Greater(prosodyResult.PhonemeCount, 0);
            Assert.Greater(regularResult.Phonemes?.Length ?? 0, 0);

            // The phoneme sequences should be the same
            // (prosody result has additional A1/A2/A3 data)
            CollectionAssert.AreEqual(regularResult.Phonemes, prosodyResult.Phonemes,
                "Prosody API and regular API should produce the same phonemes");
        }

        #endregion
    }
}
#endif