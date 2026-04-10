using System;
using System.Collections.Generic;
using NUnit.Framework;
using uPiper.Core;
using uPiper.Core.AudioGeneration;
using uPiper.Core.Phonemizers.Multilingual;

namespace uPiper.Tests.Editor
{
    [TestFixture]
    public class SilentFailureTests
    {
        #region PhonemeEncoder — PhonemeIdMap null/empty

        [Test]
        public void PhonemeEncoder_NullPhonemeIdMap_ThrowsConfigurationException()
        {
            var config = new PiperVoiceConfig { PhonemeIdMap = null };
            var mapper = new PuaTokenMapper();

            Assert.Throws<PiperConfigurationException>(() =>
                new PhonemeEncoder(config, mapper));
        }

        [Test]
        public void PhonemeEncoder_EmptyPhonemeIdMap_ThrowsConfigurationException()
        {
            var config = new PiperVoiceConfig
            {
                PhonemeIdMap = new Dictionary<string, int[]>()
            };
            var mapper = new PuaTokenMapper();

            Assert.Throws<PiperConfigurationException>(() =>
                new PhonemeEncoder(config, mapper));
        }

        [Test]
        public void PhonemeEncoder_ValidPhonemeIdMap_DoesNotThrow()
        {
            var config = CreateValidVoiceConfig();
            var mapper = new PuaTokenMapper();

            Assert.DoesNotThrow(() => new PhonemeEncoder(config, mapper));
        }

        #endregion

        #region PhonemeEncoder — Unknown phoneme threshold

        [Test]
        public void EncodeWithProsody_AllPhonemesMapped_NoException()
        {
            var config = CreateValidVoiceConfig();
            var mapper = new PuaTokenMapper();
            var encoder = new PhonemeEncoder(config, mapper);

            // Use phonemes that exist in our map
            var result = encoder.EncodeWithProsody(new[] { "a", "b" }, null);

            Assert.That(result.PhonemeIds, Is.Not.Null);
            Assert.That(result.PhonemeIds.Length, Is.GreaterThan(0));
        }

        [Test]
        public void EncodeWithProsody_Over50PercentUnknown_ThrowsConfigurationException()
        {
            var config = CreateMinimalVoiceConfig();
            var mapper = new PuaTokenMapper();
            var encoder = new PhonemeEncoder(config, mapper);

            // 3 phonemes, none in the minimal map -> 100% unknown -> should throw
            var ex = Assert.Throws<PiperConfigurationException>(() =>
                encoder.EncodeWithProsody(new[] { "zzz", "yyy", "xxx" }, null));

            StringAssert.Contains("unknown to the model", ex.Message);
        }

        [Test]
        public void EncodeWithProsody_SingleUnknownPhoneme_DoesNotThrow()
        {
            var config = CreateValidVoiceConfig();
            var mapper = new PuaTokenMapper();
            var encoder = new PhonemeEncoder(config, mapper);

            // 3 phonemes: 2 known + 1 unknown = 33% -> under threshold
            var result = encoder.EncodeWithProsody(new[] { "a", "b", "unknown_phoneme" }, null);

            Assert.That(result.PhonemeIds, Is.Not.Null);
        }

        #endregion

        #region Helper Methods

        private static PiperVoiceConfig CreateValidVoiceConfig()
        {
            return new PiperVoiceConfig
            {
                PhonemeIdMap = new Dictionary<string, int[]>
                {
                    { "_", new[] { 0 } },
                    { "^", new[] { 1 } },
                    { "$", new[] { 2 } },
                    { "a", new[] { 3 } },
                    { "b", new[] { 4 } },
                    { "c", new[] { 5 } },
                    { "d", new[] { 6 } },
                    { "e", new[] { 7 } },
                    { "f", new[] { 8 } },
                    { "g", new[] { 9 } },
                    { "h", new[] { 10 } },
                    { "i", new[] { 11 } },
                    { "j", new[] { 12 } },
                    { "k", new[] { 13 } },
                    { "l", new[] { 14 } },
                    { "m", new[] { 15 } },
                    { "n", new[] { 16 } },
                    { "o", new[] { 17 } },
                    { "p", new[] { 18 } },
                    { "r", new[] { 19 } },
                    { "s", new[] { 20 } },
                    { "t", new[] { 21 } },
                }
            };
        }

        private static PiperVoiceConfig CreateMinimalVoiceConfig()
        {
            // Only special tokens, no real phonemes
            return new PiperVoiceConfig
            {
                PhonemeIdMap = new Dictionary<string, int[]>
                {
                    { "_", new[] { 0 } },
                    { "^", new[] { 1 } },
                    { "$", new[] { 2 } },
                    { " ", new[] { 3 } },
                }
            };
        }

        #endregion
    }
}