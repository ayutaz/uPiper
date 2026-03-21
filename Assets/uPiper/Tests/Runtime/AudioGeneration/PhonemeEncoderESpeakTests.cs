using System.Collections.Generic;
using NUnit.Framework;
using uPiper.Core;
using uPiper.Core.AudioGeneration;

namespace uPiper.Tests.Runtime.AudioGeneration
{
    [TestFixture]
    public class PhonemeEncoderESpeakTests
    {
        private PhonemeEncoder _encoder;
        private PiperVoiceConfig _config;

        [SetUp]
        public void Setup()
        {
            _config = new PiperVoiceConfig
            {
                VoiceId = "multilingual-test-medium", // eSpeak形式のモデル
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
        public void Encode_ESpeakModel_AddsBOSPADEOS()
        {
            // Arrange
            var phonemes = new[] { "a", "b", "c" };

            // Act
            var ids = _encoder.Encode(phonemes);

            // Assert
            Assert.IsNotNull(ids);
            // BOS + PAD + (a + PAD) + (b + PAD) + (c + PAD) + EOS = 9
            // PAD is inserted after BOS for eSpeak/multilingual models (piper-plus post_process_ids)
            Assert.AreEqual(9, ids.Length);

            // Check structure
            Assert.AreEqual(1, ids[0]); // BOS (^)
            Assert.AreEqual(0, ids[1]); // PAD (_) after BOS
            Assert.AreEqual(3, ids[2]); // a
            Assert.AreEqual(0, ids[3]); // PAD (_)
            Assert.AreEqual(4, ids[4]); // b
            Assert.AreEqual(0, ids[5]); // PAD (_)
            Assert.AreEqual(5, ids[6]); // c
            Assert.AreEqual(0, ids[7]); // PAD (_)
            Assert.AreEqual(2, ids[8]); // EOS ($)
        }

        [Test]
        public void Encode_ESpeakModel_UnknownPhoneme_SkipsWithoutPAD()
        {
            // Arrange
            var phonemes = new[] { "a", "unknown", "b" };

            // Act
            var ids = _encoder.Encode(phonemes);

            // Assert
            Assert.IsNotNull(ids);
            // BOS + PAD + (a + PAD) + (b + PAD) + EOS = 7
            // PAD is inserted after BOS for eSpeak/multilingual models
            Assert.AreEqual(7, ids.Length);

            Assert.AreEqual(1, ids[0]); // BOS (^)
            Assert.AreEqual(0, ids[1]); // PAD (_) after BOS
            Assert.AreEqual(3, ids[2]); // a
            Assert.AreEqual(0, ids[3]); // PAD (_)
            Assert.AreEqual(4, ids[4]); // b
            Assert.AreEqual(0, ids[5]); // PAD (_)
            Assert.AreEqual(2, ids[6]); // EOS ($)
        }

        [Test]
        public void Encode_ESpeakModel_EmptyPhonemes_ReturnsEmptyArray()
        {
            // Arrange
            var phonemes = new string[0];

            // Act
            var ids = _encoder.Encode(phonemes);

            // Assert
            Assert.IsNotNull(ids);
            // 空の音素配列は空の配列を返す（実装に合わせて修正）
            Assert.AreEqual(0, ids.Length);
        }

    }
}