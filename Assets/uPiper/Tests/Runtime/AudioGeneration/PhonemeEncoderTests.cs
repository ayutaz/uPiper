using System.Linq;
using NUnit.Framework;
using uPiper.Core.AudioGeneration;
using uPiper.Core.Phonemizers;

namespace uPiper.Tests.Runtime.AudioGeneration
{
    /// <summary>
    /// Tests for PhonemeEncoder
    /// </summary>
    public class PhonemeEncoderTests
    {
        private PhonemeEncoder _encoder;

        [SetUp]
        public void SetUp()
        {
            _encoder = new PhonemeEncoder();
        }

        [Test]
        public void Constructor_InitializesWithDefaults()
        {
            Assert.Greater(_encoder.VocabularySize, 0);
            Assert.AreEqual(0, _encoder.PadTokenId);
            Assert.AreEqual(1, _encoder.UnknownTokenId);
        }

        [Test]
        public void EncodePhonemes_EmptyArray_ReturnsEmptyArray()
        {
            var result = _encoder.EncodePhonemes(new string[0]);
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Length);
        }

        [Test]
        public void EncodePhonemes_NullArray_ReturnsEmptyArray()
        {
            var result = _encoder.EncodePhonemes((string[])null);
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Length);
        }

        [Test]
        public void EncodePhonemes_KnownPhonemes_ReturnsCorrectIds()
        {
            var phonemes = new[] { "sil", "pau", "sp" };
            var result = _encoder.EncodePhonemes(phonemes);
            
            Assert.AreEqual(3, result.Length);
            // Check that IDs are not unknown token
            Assert.AreNotEqual(_encoder.UnknownTokenId, result[0]);
            Assert.AreNotEqual(_encoder.UnknownTokenId, result[1]);
            Assert.AreNotEqual(_encoder.UnknownTokenId, result[2]);
        }

        [Test]
        public void EncodePhonemes_UnknownPhonemes_ReturnsUnknownTokenId()
        {
            var phonemes = new[] { "xyz123", "unknown_phoneme" };
            var result = _encoder.EncodePhonemes(phonemes);
            
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual(_encoder.UnknownTokenId, result[0]);
            Assert.AreEqual(_encoder.UnknownTokenId, result[1]);
        }

        [Test]
        public void EncodePhonemes_PhonemeResult_UsesProvidedIds()
        {
            var phonemeResult = new PhonemeResult
            {
                Phonemes = new[] { "a", "b", "c" },
                PhonemeIds = new[] { 10, 20, 30 }
            };
            
            var result = _encoder.EncodePhonemes(phonemeResult);
            
            Assert.AreEqual(3, result.Length);
            Assert.AreEqual(10, result[0]);
            Assert.AreEqual(20, result[1]);
            Assert.AreEqual(30, result[2]);
        }

        [Test]
        public void AddPadding_ShorterSequence_PadsCorrectly()
        {
            var ids = new[] { 1, 2, 3 };
            var result = _encoder.AddPadding(ids, 5);
            
            Assert.AreEqual(5, result.Length);
            Assert.AreEqual(1, result[0]);
            Assert.AreEqual(2, result[1]);
            Assert.AreEqual(3, result[2]);
            Assert.AreEqual(_encoder.PadTokenId, result[3]);
            Assert.AreEqual(_encoder.PadTokenId, result[4]);
        }

        [Test]
        public void AddPadding_LongerSequence_Truncates()
        {
            var ids = new[] { 1, 2, 3, 4, 5 };
            var result = _encoder.AddPadding(ids, 3);
            
            Assert.AreEqual(3, result.Length);
            Assert.AreEqual(1, result[0]);
            Assert.AreEqual(2, result[1]);
            Assert.AreEqual(3, result[2]);
        }

        [Test]
        public void AddPadding_CustomPadId_UsesCustomId()
        {
            var ids = new[] { 1, 2 };
            var result = _encoder.AddPadding(ids, 4, padId: 99);
            
            Assert.AreEqual(4, result.Length);
            Assert.AreEqual(99, result[2]);
            Assert.AreEqual(99, result[3]);
        }

        [Test]
        public void AddSpecialTokens_NoTokens_ReturnsOriginal()
        {
            var ids = new[] { 1, 2, 3 };
            var result = _encoder.AddSpecialTokens(ids);
            
            Assert.AreEqual(3, result.Length);
            CollectionAssert.AreEqual(ids, result);
        }

        [Test]
        public void AddSpecialTokens_WithTokens_AddsCorrectly()
        {
            var ids = new[] { 1, 2, 3 };
            var result = _encoder.AddSpecialTokens(ids, startToken: 100, endToken: 200);
            
            Assert.AreEqual(5, result.Length);
            Assert.AreEqual(100, result[0]);
            Assert.AreEqual(1, result[1]);
            Assert.AreEqual(2, result[2]);
            Assert.AreEqual(3, result[3]);
            Assert.AreEqual(200, result[4]);
        }

        [Test]
        public void DecodeIds_ValidIds_ReturnsPhonemes()
        {
            var encoder = new PhonemeEncoder();
            
            // First encode some known phonemes
            var phonemes = new[] { "sil", "pau" };
            var ids = encoder.EncodePhonemes(phonemes);
            
            // Then decode them back
            var decoded = encoder.DecodeIds(ids);
            
            Assert.AreEqual(2, decoded.Length);
            Assert.AreEqual("sil", decoded[0]);
            Assert.AreEqual("pau", decoded[1]);
        }

        [Test]
        public void DecodeIds_UnknownIds_ReturnsUnknownMarker()
        {
            var encoder = new PhonemeEncoder();
            var ids = new[] { 99999 }; // ID that doesn't exist
            
            var decoded = encoder.DecodeIds(ids);
            
            Assert.AreEqual(1, decoded.Length);
            Assert.AreEqual("<UNK>", decoded[0]);
        }
    }
}