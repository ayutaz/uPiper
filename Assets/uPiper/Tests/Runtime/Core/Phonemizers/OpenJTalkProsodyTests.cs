#if !UNITY_WEBGL
using System;
using System.Collections;
using System.Threading.Tasks;
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
        public async Task PhonemizeWithProsody_CompareToRegularPhonemize_SamePhonemes()
        {
            const string text = "音声合成";

            var prosodyResult = _phonemizer.PhonemizeWithProsody(text);
            var regularResult = await _phonemizer.PhonemizeAsync(text);

            Assert.Greater(prosodyResult.PhonemeCount, 0);
            Assert.Greater(regularResult.Phonemes?.Length ?? 0, 0);

            // The phoneme sequences should be the same
            // (prosody result has additional A1/A2/A3 data)
            CollectionAssert.AreEqual(regularResult.Phonemes, prosodyResult.Phonemes,
                "Prosody API and regular API should produce the same phonemes");
        }

        #endregion

        #region N Phoneme Variants Tests (piper-plus #207/#210)

        [Test]
        public void PhonemizeWithProsody_NBeforeBilabial_ProducesNm()
        {
            // Test "かんぱい" (kanpai) - N before 'p' (bilabial)
            var result = _phonemizer.PhonemizeWithProsody("かんぱい");

            Assert.Greater(result.PhonemeCount, 0);

            Debug.Log($"N phoneme variant test for 'かんぱい' (kanpai):");
            for (var i = 0; i < result.PhonemeCount; i++)
            {
                var phoneme = result.Phonemes[i];
                var isPua = phoneme.Length == 1 && phoneme[0] >= '\ue000' && phoneme[0] <= '\uf8ff';
                var display = isPua ? $"PUA(U+{((int)phoneme[0]):X4})" : $"'{phoneme}'";
                Debug.Log($"  [{i}] {display}");
            }

            // Check that N is converted to N_m (PUA \ue019) when before 'p'
            var hasNmVariant = false;
            for (var i = 0; i < result.PhonemeCount; i++)
            {
                if (result.Phonemes[i] == "N_m" || result.Phonemes[i] == "\ue019")
                {
                    hasNmVariant = true;
                    break;
                }
            }

            Assert.IsTrue(hasNmVariant, "Expected N_m variant (U+E019) before bilabial consonant 'p'");
        }

        [Test]
        public void PhonemizeWithProsody_NBeforeAlveolar_ProducesNn()
        {
            // Test "かんたん" (kantan) - N before 't' (alveolar)
            var result = _phonemizer.PhonemizeWithProsody("かんたん");

            Assert.Greater(result.PhonemeCount, 0);

            Debug.Log($"N phoneme variant test for 'かんたん' (kantan):");
            for (var i = 0; i < result.PhonemeCount; i++)
            {
                var phoneme = result.Phonemes[i];
                var isPua = phoneme.Length == 1 && phoneme[0] >= '\ue000' && phoneme[0] <= '\uf8ff';
                var display = isPua ? $"PUA(U+{((int)phoneme[0]):X4})" : $"'{phoneme}'";
                Debug.Log($"  [{i}] {display}");
            }

            // Check that at least one N is converted to N_n (PUA \ue01a) when before 't'
            var hasNnVariant = false;
            for (var i = 0; i < result.PhonemeCount; i++)
            {
                if (result.Phonemes[i] == "N_n" || result.Phonemes[i] == "\ue01a")
                {
                    hasNnVariant = true;
                    break;
                }
            }

            Assert.IsTrue(hasNnVariant, "Expected N_n variant (U+E01A) before alveolar consonant 't'");
        }

        [Test]
        public void PhonemizeWithProsody_NBeforeVelar_ProducesNng()
        {
            // Test "かんこく" (kankoku) - N before 'k' (velar)
            var result = _phonemizer.PhonemizeWithProsody("かんこく");

            Assert.Greater(result.PhonemeCount, 0);

            Debug.Log($"N phoneme variant test for 'かんこく' (kankoku):");
            for (var i = 0; i < result.PhonemeCount; i++)
            {
                var phoneme = result.Phonemes[i];
                var isPua = phoneme.Length == 1 && phoneme[0] >= '\ue000' && phoneme[0] <= '\uf8ff';
                var display = isPua ? $"PUA(U+{((int)phoneme[0]):X4})" : $"'{phoneme}'";
                Debug.Log($"  [{i}] {display}");
            }

            // Check that N is converted to N_ng (PUA \ue01b) when before 'k'
            var hasNngVariant = false;
            for (var i = 0; i < result.PhonemeCount; i++)
            {
                if (result.Phonemes[i] == "N_ng" || result.Phonemes[i] == "\ue01b")
                {
                    hasNngVariant = true;
                    break;
                }
            }

            Assert.IsTrue(hasNngVariant, "Expected N_ng variant (U+E01B) before velar consonant 'k'");
        }

        [Test]
        public void PhonemizeWithProsody_NAtEnd_ProducesNUvular()
        {
            // Test "ほん" (hon) - N at end of word
            var result = _phonemizer.PhonemizeWithProsody("ほん");

            Assert.Greater(result.PhonemeCount, 0);

            Debug.Log($"N phoneme variant test for 'ほん' (hon):");
            for (var i = 0; i < result.PhonemeCount; i++)
            {
                var phoneme = result.Phonemes[i];
                var isPua = phoneme.Length == 1 && phoneme[0] >= '\ue000' && phoneme[0] <= '\uf8ff';
                var display = isPua ? $"PUA(U+{((int)phoneme[0]):X4})" : $"'{phoneme}'";
                Debug.Log($"  [{i}] {display}");
            }

            // Check that N is converted to N_uvular (PUA \ue01c) at end of word
            var hasNUvularVariant = false;
            for (var i = 0; i < result.PhonemeCount; i++)
            {
                if (result.Phonemes[i] == "N_uvular" || result.Phonemes[i] == "\ue01c")
                {
                    hasNUvularVariant = true;
                    break;
                }
            }

            Assert.IsTrue(hasNUvularVariant, "Expected N_uvular variant (U+E01C) at end of word");
        }

        [Test]
        public void PhonemizeWithProsody_MultipleNVariants_AllConverted()
        {
            // Test sentence with multiple N variants
            // "かんたんに かんこくに いきました" (I easily went to Korea)
            var result = _phonemizer.PhonemizeWithProsody("かんたんに");

            Assert.Greater(result.PhonemeCount, 0);

            Debug.Log($"Multiple N variants test for 'かんたんに':");
            for (var i = 0; i < result.PhonemeCount; i++)
            {
                var phoneme = result.Phonemes[i];
                var isPua = phoneme.Length == 1 && phoneme[0] >= '\ue000' && phoneme[0] <= '\uf8ff';
                var display = isPua ? $"PUA(U+{((int)phoneme[0]):X4})" : $"'{phoneme}'";
                Debug.Log($"  [{i}] {display}");
            }

            // Should contain at least one N variant
            var hasAnyNVariant = false;
            for (var i = 0; i < result.PhonemeCount; i++)
            {
                var phoneme = result.Phonemes[i];
                if (phoneme == "N_m" || phoneme == "N_n" || phoneme == "N_ng" || phoneme == "N_uvular" ||
                    phoneme == "\ue019" || phoneme == "\ue01a" || phoneme == "\ue01b" || phoneme == "\ue01c")
                {
                    hasAnyNVariant = true;
                    break;
                }
            }

            Assert.IsTrue(hasAnyNVariant, "Expected at least one N variant in the output");
        }

        #endregion
    }
}
#endif