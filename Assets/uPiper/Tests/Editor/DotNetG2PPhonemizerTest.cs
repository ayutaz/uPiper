using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using uPiper.Core.Phonemizers.Implementations;

namespace uPiper.Tests.Editor
{
    [TestFixture]
    public class DotNetG2PPhonemizerTest
    {
        private DotNetG2PPhonemizer _phonemizer;
        private bool _dictionaryAvailable;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Check if dictionary is available before running tests
            try
            {
                _phonemizer = new DotNetG2PPhonemizer(loadCustomDictionary: false);
                _dictionaryAvailable = true;
            }
            catch (Exception)
            {
                _dictionaryAvailable = false;
            }
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _phonemizer?.Dispose();
        }

        private void SkipIfNoDictionary()
        {
            if (!_dictionaryAvailable)
                Assert.Ignore("Dictionary not available, skipping test");
        }

        #region Basic Phonemization

        [Test]
        public void PhonemizeAsync_Konnichiwa_ReturnsPhonemes()
        {
            SkipIfNoDictionary();

            var result = Task.Run(() => _phonemizer.PhonemizeAsync("こんにちは")).GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0, "Should return phonemes");
            Assert.AreEqual("ja", result.Language);
        }

        [Test]
        public void PhonemizeAsync_EmptyString_ReturnsEmpty()
        {
            SkipIfNoDictionary();

            var result = Task.Run(() => _phonemizer.PhonemizeAsync("")).GetAwaiter().GetResult();

            Assert.IsNotNull(result);
            Assert.IsTrue(
                result.Phonemes == null || result.Phonemes.Length == 0,
                "Should return empty phonemes for empty input");
        }

        [Test]
        public void PhonemizeAsync_ReturnsQuestionMarker_ForStatement()
        {
            SkipIfNoDictionary();

            var result = Task.Run(() => _phonemizer.PhonemizeAsync("こんにちは")).GetAwaiter().GetResult();

            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0);
            // Last phoneme should be "$" (statement end marker)
            Assert.AreEqual("$", result.Phonemes.Last());
        }

        [Test]
        public void PhonemizeAsync_ReturnsQuestionMarker_ForQuestion()
        {
            SkipIfNoDictionary();

            var result = Task.Run(() => _phonemizer.PhonemizeAsync("元気ですか？")).GetAwaiter().GetResult();

            Assert.IsNotNull(result.Phonemes);
            Assert.IsTrue(result.Phonemes.Length > 0);
            Assert.AreEqual("?", result.Phonemes.Last());
        }

        #endregion

        #region Prosody API

        [Test]
        public void PhonemizeWithProsody_ReturnsValidResult()
        {
            SkipIfNoDictionary();

            var result = _phonemizer.PhonemizeWithProsody("こんにちは");

            Assert.IsTrue(result.PhonemeCount > 0, "Should return phonemes");
            Assert.AreEqual(result.PhonemeCount, result.Phonemes.Length);
            Assert.AreEqual(result.PhonemeCount, result.ProsodyA1.Length);
            Assert.AreEqual(result.PhonemeCount, result.ProsodyA2.Length);
            Assert.AreEqual(result.PhonemeCount, result.ProsodyA3.Length);
        }

        [Test]
        public void PhonemizeWithProsody_EmptyString_ReturnsEmpty()
        {
            SkipIfNoDictionary();

            var result = _phonemizer.PhonemizeWithProsody("");

            Assert.AreEqual(0, result.PhonemeCount);
            Assert.AreEqual(0, result.Phonemes.Length);
        }

        [Test]
        public void PhonemizeWithProsody_QuestionMarkerHasZeroProsody()
        {
            SkipIfNoDictionary();

            var result = _phonemizer.PhonemizeWithProsody("東京");

            Assert.IsTrue(result.PhonemeCount > 0);
            // Last element (question marker) should have 0 prosody values
            var lastIdx = result.PhonemeCount - 1;
            Assert.AreEqual(0, result.ProsodyA1[lastIdx]);
            Assert.AreEqual(0, result.ProsodyA2[lastIdx]);
            Assert.AreEqual(0, result.ProsodyA3[lastIdx]);
        }

        [Test]
        public void PhonemizeWithProsody_ArrayLengthsMatch()
        {
            SkipIfNoDictionary();

            var result = _phonemizer.PhonemizeWithProsody("今日はいい天気ですね");

            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA1.Length,
                "Phonemes and A1 should have same length");
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA2.Length,
                "Phonemes and A2 should have same length");
            Assert.AreEqual(result.Phonemes.Length, result.ProsodyA3.Length,
                "Phonemes and A3 should have same length");
        }

        #endregion

    }
}