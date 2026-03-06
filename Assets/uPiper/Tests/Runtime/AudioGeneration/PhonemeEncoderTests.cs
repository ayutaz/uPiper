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

        #region Extended Question Markers Tests (piper-plus #210)

        [Test]
        public void Encode_WithEosMarker_DoesNotAddExtraEos()
        {
            // Arrange: phonemes ending with "$" (EOS marker from DotNetG2PPhonemizer)
            var phonemes = new[] { "a", "b", "$" };

            // Act
            var ids = _encoder.Encode(phonemes);

            // Assert
            Assert.IsNotNull(ids);
            // BOS + 3 phonemes (including $) = 4 (no extra EOS added)
            Assert.AreEqual(4, ids.Length);
            Assert.AreEqual(1, ids[0]); // BOS (^)
            Assert.AreEqual(3, ids[1]); // a
            Assert.AreEqual(4, ids[2]); // b
            Assert.AreEqual(2, ids[3]); // $ (EOS, not duplicated)
        }

        [Test]
        public void Encode_WithQuestionMarker_DoesNotAddEos()
        {
            // Arrange: Create encoder with question marker support
            var configWithQuestion = new PiperVoiceConfig
            {
                VoiceId = "ja_JP-test-medium",
                PhonemeType = "openjtalk",
                SampleRate = 22050,
                PhonemeIdMap = new Dictionary<string, int>
                {
                    { "_", 0 },  // PAD
                    { "^", 1 },  // BOS
                    { "$", 2 },  // EOS
                    { "?", 3 },  // Question marker
                    { "a", 4 },
                    { "b", 5 }
                }
            };
            var encoder = new PhonemeEncoder(configWithQuestion);

            // Arrange: phonemes ending with "?" (question marker from DotNetG2PPhonemizer)
            var phonemes = new[] { "a", "b", "?" };

            // Act
            var ids = encoder.Encode(phonemes);

            // Assert
            Assert.IsNotNull(ids);
            // BOS + 3 phonemes (including ?) = 4 (no EOS added because ? is EOS-like)
            Assert.AreEqual(4, ids.Length);
            Assert.AreEqual(1, ids[0]); // BOS (^)
            Assert.AreEqual(4, ids[1]); // a
            Assert.AreEqual(5, ids[2]); // b
            Assert.AreEqual(3, ids[3]); // ? (question marker, no extra $ added)
        }

        [Test]
        public void Encode_WithExtendedQuestionMarkers_DoesNotAddEos()
        {
            // Arrange: Create encoder with extended question marker support
            // Note: Extended question markers are converted to PUA characters by PhonemeEncoder
            // So the PhonemeIdMap needs to include the PUA characters, not the ASCII markers
            var configWithExtendedQuestions = new PiperVoiceConfig
            {
                VoiceId = "ja_JP-test-medium",
                PhonemeType = "openjtalk",
                SampleRate = 22050,
                PhonemeIdMap = new Dictionary<string, int>
                {
                    { "_", 0 },       // PAD
                    { "^", 1 },       // BOS
                    { "$", 2 },       // EOS
                    { "?", 3 },       // Normal question
                    { "\ue016", 4 },  // ?! Emphatic question (PUA) (piper-plus #210)
                    { "\ue017", 5 },  // ?. Declarative question (PUA) (piper-plus #210)
                    { "\ue018", 6 },  // ?~ Confirmatory question (PUA) (piper-plus #210)
                    { "a", 7 },
                    { "b", 8 }
                }
            };
            var encoder = new PhonemeEncoder(configWithExtendedQuestions);

            // Test each extended question marker
            // Note: The markers are converted to PUA, so we expect the PUA IDs
            var testCases = new[]
            {
                ("?!", 4, "emphatic question"),
                ("?.", 5, "declarative question"),
                ("?~", 6, "confirmatory question")
            };

            foreach (var (marker, expectedId, description) in testCases)
            {
                var phonemes = new[] { "a", "b", marker };
                var ids = encoder.Encode(phonemes);

                Assert.IsNotNull(ids, $"Failed for {description}");
                // BOS + 3 phonemes (including marker) = 4 (no EOS added)
                Assert.AreEqual(4, ids.Length, $"Expected 4 IDs for {description}, got {ids.Length}");
                Assert.AreEqual(1, ids[0], $"BOS check failed for {description}");
                Assert.AreEqual(7, ids[1], $"'a' check failed for {description}");
                Assert.AreEqual(8, ids[2], $"'b' check failed for {description}");
                Assert.AreEqual(expectedId, ids[3], $"'{marker}' check failed for {description}");
            }
        }

        [Test]
        public void Encode_WithoutEosLikeMarker_AddsEos()
        {
            // Arrange: phonemes NOT ending with EOS-like marker
            var phonemes = new[] { "a", "b", "c" };

            // Act
            var ids = _encoder.Encode(phonemes);

            // Assert
            Assert.IsNotNull(ids);
            // BOS + 3 phonemes + EOS = 5 (EOS is added normally)
            Assert.AreEqual(5, ids.Length);
            Assert.AreEqual(1, ids[0]); // BOS (^)
            Assert.AreEqual(3, ids[1]); // a
            Assert.AreEqual(4, ids[2]); // b
            Assert.AreEqual(5, ids[3]); // c
            Assert.AreEqual(2, ids[4]); // EOS ($) added
        }

        #endregion
    }
}