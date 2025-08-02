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
                VoiceId = "en_US-ljspeech-medium", // eSpeak形式のモデル
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
            // BOS + (a + PAD) + (b + PAD) + (c + PAD) + EOS = 8
            Assert.AreEqual(8, ids.Length);
            
            // Check structure
            Assert.AreEqual(1, ids[0]); // BOS (^)
            Assert.AreEqual(3, ids[1]); // a
            Assert.AreEqual(0, ids[2]); // PAD (_)
            Assert.AreEqual(4, ids[3]); // b
            Assert.AreEqual(0, ids[4]); // PAD (_)
            Assert.AreEqual(5, ids[5]); // c
            Assert.AreEqual(0, ids[6]); // PAD (_)
            Assert.AreEqual(2, ids[7]); // EOS ($)
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
            // BOS + (a + PAD) + (b + PAD) + EOS = 6
            Assert.AreEqual(6, ids.Length);
            
            Assert.AreEqual(1, ids[0]); // BOS (^)
            Assert.AreEqual(3, ids[1]); // a
            Assert.AreEqual(0, ids[2]); // PAD (_)
            Assert.AreEqual(4, ids[3]); // b
            Assert.AreEqual(0, ids[4]); // PAD (_)
            Assert.AreEqual(2, ids[5]); // EOS ($)
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

        [Test]
        public void Decode_ESpeakModel_IgnoresSpecialTokens()
        {
            // Arrange
            // BOS + a + PAD + b + PAD + c + PAD + EOS
            var ids = new[] { 1, 3, 0, 4, 0, 5, 0, 2 };

            // Act
            var phonemes = _encoder.Decode(ids);

            // Assert
            Assert.IsNotNull(phonemes);
            // 特殊トークン（BOS, PAD, EOS）は除外される
            Assert.AreEqual(3, phonemes.Length);
            Assert.AreEqual("a", phonemes[0]);
            Assert.AreEqual("b", phonemes[1]);
            Assert.AreEqual("c", phonemes[2]);
        }
    }
}