using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using uPiper.Core;
using uPiper.Core.AudioGeneration;
using uPiper.Core.Phonemizers.Multilingual;

namespace uPiper.Tests.Runtime.AudioGeneration
{
    /// <summary>
    /// IPA対応モデル用のIPAマッピングテスト
    /// </summary>
    [TestFixture]
    public class PhonemeEncoderIPATests
    {
        private PhonemeEncoder _encoder;
        private PiperVoiceConfig _config;
        private PuaTokenMapper _mapper;

        [SetUp]
        public void Setup()
        {
            _mapper = new PuaTokenMapper();
            // IPA対応モデルのphoneme_id_mapを再現
            _config = new PiperVoiceConfig
            {
                VoiceId = "multilingual-test-medium",
                PhonemeType = "openjtalk",
                SampleRate = 22050,
                PhonemeIdMap = new Dictionary<string, int[]>
                {
                    { "_", new[] { 0 } }, { "^", new[] { 1 } }, { "$", new[] { 2 } }, { "?", new[] { 3 } }, { "#", new[] { 4 } },
                    { "[", new[] { 5 } }, { "]", new[] { 6 } },
                    { "a", new[] { 7 } }, { "i", new[] { 8 } }, { "u", new[] { 9 } }, { "e", new[] { 10 } }, { "o", new[] { 11 } },
                    { "A", new[] { 12 } }, { "I", new[] { 13 } }, { "U", new[] { 14 } }, { "E", new[] { 15 } }, { "O", new[] { 16 } },
                    { "ç", new[] { 17 } }, { "ɕ", new[] { 18 } }, { "ɯ", new[] { 19 } }, { "ɴ", new[] { 20 } }, { "ɾ", new[] { 21 } },
                    { "N", new[] { 22 } }, { "ʑ", new[] { 23 } }, { "q", new[] { 24 } }, { "k", new[] { 25 } },
                    { "kʲ", new[] { 26 } }, { "ɡʲ", new[] { 27 } }, { "g", new[] { 28 } }, { "ɡ", new[] { 29 } }, { "dʑ", new[] { 30 } },
                    { "t", new[] { 31 } }, { "tɕ", new[] { 32 } }, { "d", new[] { 33 } }, { "dʲ", new[] { 34 } },
                    { "p", new[] { 35 } }, { "pʲ", new[] { 36 } }, { "b", new[] { 37 } }, { "bʲ", new[] { 38 } },
                    { "cç", new[] { 39 } }, { "çː", new[] { 40 } }, { "s", new[] { 41 } }, { "ʃ", new[] { 42 } },
                    { "z", new[] { 43 } }, { "j", new[] { 44 } }, { "ɲ", new[] { 45 } },
                    { "f", new[] { 46 } }, { "h", new[] { 47 } }, { "hʲ", new[] { 48 } }, { "v", new[] { 49 } },
                    { "n", new[] { 50 } }, { "nʲ", new[] { 51 } }, { "m", new[] { 52 } }, { "mʲ", new[] { 53 } },
                    { "r", new[] { 54 } }, { "ɽ", new[] { 55 } }, { "w", new[] { 56 } }, { "y", new[] { 57 } }
                }
            };
            _encoder = new PhonemeEncoder(_config, _mapper);
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
        /// IPA対応モデルには"ts"音素がないため、t+sに分解されるべき
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
        /// 「し」(sh)が正しくʃ(ID 42)にマッピングされることを確認
        /// (学習データでは ʃ を使用、ɕ ではない)
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
            // BOS + ʃ + i + EOS = 4
            Assert.AreEqual(4, ids.Length, $"Expected 4 IDs but got {ids.Length}: [{string.Join(", ", ids)}]");

            Assert.AreEqual(1, ids[0], "ids[0] should be BOS (^) = 1");
            // sh maps to ʃ (ID 42), NOT ɕ (ID 18) - matches training data
            Assert.AreEqual(42, ids[1], $"ids[1] should be ʃ = 42, but got {ids[1]}");
            Assert.AreEqual(8, ids[2], "ids[2] should be i = 8");
            Assert.AreEqual(2, ids[3], "ids[3] should be EOS ($) = 2");
        }
    }
}