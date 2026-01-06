using System.Collections.Generic;
using NUnit.Framework;
using uPiper.Core;
using uPiper.Core.AudioGeneration;

namespace uPiper.Tests.Runtime.AudioGeneration
{
    [TestFixture]
    public class PhonemeEncoderTests
    {
        private PhonemeEncoder _encoder;
        private PiperVoiceConfig _config;

        [SetUp]
        public void Setup()
        {
            _config = new PiperVoiceConfig
            {
                VoiceId = "ja_JP-test-medium",
                PhonemeType = "openjtalk",  // OpenJTalk方式（PADなし）
                SampleRate = 22050,
                PhonemeIdMap = new Dictionary<string, int>
                {
                    { "_", 0 },  // PAD
                    { "^", 1 },  // BOS
                    { "$", 2 },  // EOS
                    { "a", 3 },
                    { "b", 4 },
                    { "c", 5 },
                    { "d", 6 },
                    { "e", 7 },
                    { " ", 8 }
                }
            };
            _encoder = new PhonemeEncoder(_config);
        }

        [Test]
        public void Constructor_WithValidConfig_InitializesCorrectly()
        {
            // Assert
            Assert.IsNotNull(_encoder);
            Assert.Greater(_encoder.PhonemeCount, 0);
        }

        [Test]
        public void Encode_ValidPhonemes_ReturnsCorrectIds()
        {
            // Arrange
            var phonemes = new[] { "a", "b", "c" };

            // Act
            var ids = _encoder.Encode(phonemes);

            // Assert
            Assert.IsNotNull(ids);
            // BOS + 3 phonemes + EOS = 5 (OpenJTalk方式はPADなし)
            Assert.AreEqual(5, ids.Length);
            Assert.AreEqual(1, ids[0]); // BOS (^)
            Assert.AreEqual(3, ids[1]); // a
            Assert.AreEqual(4, ids[2]); // b
            Assert.AreEqual(5, ids[3]); // c
            Assert.AreEqual(2, ids[4]); // EOS ($)
        }

        [Test]
        public void Encode_EmptyPhonemes_ReturnsEmptyArray()
        {
            // Arrange
            var phonemes = new string[0];

            // Act
            var ids = _encoder.Encode(phonemes);

            // Assert
            Assert.IsNotNull(ids);
            Assert.AreEqual(0, ids.Length); // Empty array
        }

        [Test]
        public void Encode_UnknownPhoneme_SkipsUnknown()
        {
            // Arrange
            var phonemes = new[] { "a", "unknown", "b" };

            // Act
            var ids = _encoder.Encode(phonemes);

            // Assert
            Assert.IsNotNull(ids);
            // BOS + 2 known phonemes + EOS = 4 (unknown is skipped)
            Assert.AreEqual(4, ids.Length);
            Assert.AreEqual(1, ids[0]); // BOS (^)
            Assert.AreEqual(3, ids[1]); // a
            Assert.AreEqual(4, ids[2]); // b
            Assert.AreEqual(2, ids[3]); // EOS ($)
        }

        [Test]
        public void Decode_ValidIds_ReturnsCorrectPhonemes()
        {
            // Arrange
            var ids = new[] { 3, 4, 5 }; // a, b, c

            // Act
            var phonemes = _encoder.Decode(ids);

            // Assert
            Assert.IsNotNull(phonemes);
            Assert.AreEqual(3, phonemes.Length);
            Assert.AreEqual("a", phonemes[0]);
            Assert.AreEqual("b", phonemes[1]);
            Assert.AreEqual("c", phonemes[2]);
        }

        [Test]
        public void Decode_EmptyIds_ReturnsEmptyArray()
        {
            // Arrange
            var ids = new int[0];

            // Act
            var phonemes = _encoder.Decode(ids);

            // Assert
            Assert.IsNotNull(phonemes);
            Assert.AreEqual(0, phonemes.Length);
        }

        [Test]
        public void ContainsPhoneme_ExistingPhoneme_ReturnsTrue()
        {
            // Act & Assert
            Assert.IsTrue(_encoder.ContainsPhoneme("a"));
            Assert.IsTrue(_encoder.ContainsPhoneme("b"));
            Assert.IsTrue(_encoder.ContainsPhoneme(" "));
        }

        [Test]
        public void ContainsPhoneme_NonExistingPhoneme_ReturnsFalse()
        {
            // Act & Assert
            Assert.IsFalse(_encoder.ContainsPhoneme("x"));
            Assert.IsFalse(_encoder.ContainsPhoneme("unknown"));
        }

        [Test]
        public void PhonemeCount_ReturnsCorrectCount()
        {
            // Assert
            // 3 special tokens + 6 from config
            Assert.AreEqual(9, _encoder.PhonemeCount);
        }

        [Test]
        public void EncodeDecode_RoundTrip_PreservesPhonemes()
        {
            // Arrange
            var originalPhonemes = new[] { "a", "b", " ", "c", "d", "e" };

            // Act
            var ids = _encoder.Encode(originalPhonemes);
            var decodedPhonemes = _encoder.Decode(ids);

            // Assert
            Assert.IsNotNull(decodedPhonemes);
            Assert.AreEqual(originalPhonemes.Length, decodedPhonemes.Length);
            for (var i = 0; i < originalPhonemes.Length; i++)
            {
                Assert.AreEqual(originalPhonemes[i], decodedPhonemes[i]);
            }
        }
    }
}