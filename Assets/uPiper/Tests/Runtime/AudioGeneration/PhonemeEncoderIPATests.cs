using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using uPiper.Core;
using uPiper.Core.AudioGeneration;

namespace uPiper.Tests.Runtime.AudioGeneration
{
    /// <summary>
    /// tsukuyomi-chanモデル用のIPAマッピングテスト
    /// </summary>
    [TestFixture]
    public class PhonemeEncoderIPATests
    {
        private PhonemeEncoder _encoder;
        private PiperVoiceConfig _config;

        [SetUp]
        public void Setup()
        {
            // tsukuyomi-chanモデルのphoneme_id_mapを再現
            _config = new PiperVoiceConfig
            {
                VoiceId = "tsukuyomi-chan",
                PhonemeType = "openjtalk",
                SampleRate = 22050,
                PhonemeIdMap = new Dictionary<string, int>
                {
                    { "_", 0 }, { "^", 1 }, { "$", 2 }, { "?", 3 }, { "#", 4 },
                    { "[", 5 }, { "]", 6 },
                    { "a", 7 }, { "i", 8 }, { "u", 9 }, { "e", 10 }, { "o", 11 },
                    { "A", 12 }, { "I", 13 }, { "U", 14 }, { "E", 15 }, { "O", 16 },
                    { "ç", 17 }, { "ɕ", 18 }, { "ɯ", 19 }, { "ɴ", 20 }, { "ɾ", 21 },
                    { "N", 22 }, { "ʑ", 23 }, { "q", 24 }, { "k", 25 },
                    { "kʲ", 26 }, { "ɡʲ", 27 }, { "g", 28 }, { "ɡ", 29 }, { "dʑ", 30 },
                    { "t", 31 }, { "tɕ", 32 }, { "d", 33 }, { "dʲ", 34 },
                    { "p", 35 }, { "pʲ", 36 }, { "b", 37 }, { "bʲ", 38 },
                    { "cç", 39 }, { "çː", 40 }, { "s", 41 }, { "ʃ", 42 },
                    { "z", 43 }, { "j", 44 }, { "ɲ", 45 },
                    { "f", 46 }, { "h", 47 }, { "hʲ", 48 }, { "v", 49 },
                    { "n", 50 }, { "nʲ", 51 }, { "m", 52 }, { "mʲ", 53 },
                    { "r", 54 }, { "ɽ", 55 }, { "w", 56 }, { "y", 57 }
                }
            };
            _encoder = new PhonemeEncoder(_config);
        }

        /// <summary>
        /// IPAモデル判定が正しく行われることを確認
        /// phoneme_id_mapに"ɕ"が含まれている = IPAモデル
        /// </summary>
        [Test]
        public void Constructor_WithIPAPhonemes_DetectsIPAModel()
        {
            // tsukuyomi-chanはIPA文字を含むのでIPA判定される
            Assert.IsTrue(_encoder.ContainsPhoneme("ɕ"), "IPA判定キー 'ɕ' should be recognized");
            Assert.IsTrue(_encoder.ContainsPhoneme("q"), "促音 'q' should be recognized");
            Assert.IsTrue(_encoder.ContainsPhoneme("N"), "撥音 'N' should be recognized");
        }

        /// <summary>
        /// 促音(cl)がq(ID 24)にマッピングされることを確認
        /// BUG: 現在は N(ID 22) にマッピングされている
        /// </summary>
        [Test]
        public void Encode_SokuonCl_MapsToQNotN()
        {
            // Arrange - "にっぽん" (nippon) の音素
            // OpenJTalkからはPUA経由で cl が来る ("\ue005")
            var phonemes = new[] { "n", "i", "\ue005", "p", "o", "n" };

            // Act
            var ids = _encoder.Encode(phonemes);

            // Assert
            Assert.IsNotNull(ids);
            // BOS + 6 phonemes + EOS = 8
            Assert.AreEqual(8, ids.Length, $"Expected 8 IDs but got {ids.Length}: [{string.Join(", ", ids)}]");

            // IDs: ^ n i q p o n $
            Assert.AreEqual(1, ids[0], "ids[0] should be BOS (^) = 1");
            Assert.AreEqual(50, ids[1], "ids[1] should be n = 50");
            Assert.AreEqual(8, ids[2], "ids[2] should be i = 8");
            Assert.AreEqual(24, ids[3], $"ids[3] should be q(促音) = 24, but got {ids[3]} (N=22 would be wrong!)");
            Assert.AreEqual(35, ids[4], "ids[4] should be p = 35");
            Assert.AreEqual(11, ids[5], "ids[5] should be o = 11");
            Assert.AreEqual(50, ids[6], "ids[6] should be n = 50");
            Assert.AreEqual(2, ids[7], "ids[7] should be EOS ($) = 2");
        }

        /// <summary>
        /// 促音をPUA以外の "cl" 直接入力でもq(ID 24)にマッピングされることを確認
        /// </summary>
        [Test]
        public void Encode_SokuonClDirect_MapsToQ()
        {
            // Arrange - "cl" を直接入力（PUA経由でなく）
            var phonemes = new[] { "cl" };

            // Act
            var ids = _encoder.Encode(phonemes);

            // Assert
            Assert.IsNotNull(ids);
            // BOS + 1 phoneme + EOS = 3
            Assert.AreEqual(3, ids.Length, $"Expected 3 IDs but got {ids.Length}: [{string.Join(", ", ids)}]");

            // clはq(24)にマッピングされるべき（N(22)ではない）
            Assert.AreEqual(1, ids[0], "ids[0] should be BOS (^) = 1");
            Assert.AreEqual(24, ids[1], $"ids[1] should be q(促音) = 24, but got {ids[1]} (N=22 would be wrong!)");
            Assert.AreEqual(2, ids[2], "ids[2] should be EOS ($) = 2");
        }

        /// <summary>
        /// ts(「つ」の子音)が正しく処理されることを確認
        /// tsukuyomi-chanモデルには"ts"音素がないため、t+sに分解されるべき
        /// </summary>
        [Test]
        public void Encode_TsPhoneme_SplitsToTAndS()
        {
            // Arrange - "つかう" (tsukau) の音素
            // OpenJTalkからは ts + U が来る（PUA "\ue00f" 経由）
            var phonemes = new[] { "\ue00f", "U", "k", "a", "u" };

            // Act
            var ids = _encoder.Encode(phonemes);

            // Assert
            Assert.IsNotNull(ids);

            // tsが正しく処理されているか確認
            // 期待値: [^(1), t(31), s(41), U(14), k(25), a(7), u(9), $(2)] = 8 IDs
            // または ts がスキップされて [^(1), U(14), k(25), a(7), u(9), $(2)] = 6 IDs (バグの場合)

            var idsWithoutBosEos = ids.Skip(1).Take(ids.Length - 2).ToArray();

            // "ts"が分解されてt(31)とs(41)が含まれているか確認
            Assert.IsTrue(
                idsWithoutBosEos.Contains(31) && idsWithoutBosEos.Contains(41),
                $"ts phoneme should be split into t(31) and s(41). Got IDs: [{string.Join(", ", ids)}]"
            );

            // 8 IDs expected: ^ t s U k a u $
            Assert.AreEqual(8, ids.Length,
                $"Expected 8 IDs (BOS + t + s + U + k + a + u + EOS), but got {ids.Length}: [{string.Join(", ", ids)}]");
        }

        /// <summary>
        /// ts を直接入力でも t+s に分解されることを確認
        /// </summary>
        [Test]
        public void Encode_TsDirect_SplitsToTAndS()
        {
            // Arrange - "ts" を直接入力
            var phonemes = new[] { "ts" };

            // Act
            var ids = _encoder.Encode(phonemes);

            // Assert
            Assert.IsNotNull(ids);

            // tsが正しく分解されているか確認
            // 期待値: [^(1), t(31), s(41), $(2)] = 4 IDs
            var idsWithoutBosEos = ids.Skip(1).Take(ids.Length - 2).ToArray();

            Assert.IsTrue(
                idsWithoutBosEos.Contains(31) && idsWithoutBosEos.Contains(41),
                $"ts phoneme should be split into t(31) and s(41). Got IDs: [{string.Join(", ", ids)}]"
            );
        }

        /// <summary>
        /// 「ち」(ch)が正しくtɕ(ID 32)にマッピングされることを確認
        /// </summary>
        [Test]
        public void Encode_ChPhoneme_MapsToTcp()
        {
            // Arrange - OpenJTalkからはPUA "\ue00e" で来る
            var phonemes = new[] { "\ue00e", "i" };

            // Act
            var ids = _encoder.Encode(phonemes);

            // Assert
            Assert.IsNotNull(ids);
            // BOS + tɕ + i + EOS = 4
            Assert.AreEqual(4, ids.Length, $"Expected 4 IDs but got {ids.Length}: [{string.Join(", ", ids)}]");

            Assert.AreEqual(1, ids[0], "ids[0] should be BOS (^) = 1");
            Assert.AreEqual(32, ids[1], $"ids[1] should be tɕ = 32, but got {ids[1]}");
            Assert.AreEqual(8, ids[2], "ids[2] should be i = 8");
            Assert.AreEqual(2, ids[3], "ids[3] should be EOS ($) = 2");
        }

        /// <summary>
        /// 「し」(sh)が正しくɕ(ID 18)にマッピングされることを確認
        /// </summary>
        [Test]
        public void Encode_ShPhoneme_MapsToSh()
        {
            // Arrange - OpenJTalkからはPUA "\ue010" で来る
            var phonemes = new[] { "\ue010", "i" };

            // Act
            var ids = _encoder.Encode(phonemes);

            // Assert
            Assert.IsNotNull(ids);
            // BOS + ɕ + i + EOS = 4
            Assert.AreEqual(4, ids.Length, $"Expected 4 IDs but got {ids.Length}: [{string.Join(", ", ids)}]");

            Assert.AreEqual(1, ids[0], "ids[0] should be BOS (^) = 1");
            Assert.AreEqual(18, ids[1], $"ids[1] should be ɕ = 18, but got {ids[1]}");
            Assert.AreEqual(8, ids[2], "ids[2] should be i = 8");
            Assert.AreEqual(2, ids[3], "ids[3] should be EOS ($) = 2");
        }
    }
}
