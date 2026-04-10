using System;
using System.Collections.Generic;
using NUnit.Framework;
using uPiper.Core;
using uPiper.Core.AudioGeneration;
using uPiper.Core.Phonemizers.Multilingual;
using uPiper.Tests.Editor.TestHelpers;

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
            var config = TestVoiceConfigFactory.CreateValid();
            var mapper = new PuaTokenMapper();

            Assert.DoesNotThrow(() => new PhonemeEncoder(config, mapper));
        }

        #endregion

        #region PhonemeEncoder — Unknown phoneme threshold

        [Test]
        public void EncodeWithProsody_AllPhonemesMapped_NoException()
        {
            var config = TestVoiceConfigFactory.CreateValid();
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
            // Minimal config with space entry to match original test setup
            var minimalMap = TestPhonemeIdMapFactory.CreateMinimal();
            minimalMap[" "] = new[] { 3 };
            var config = new PiperVoiceConfig { PhonemeIdMap = minimalMap };
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
            var config = TestVoiceConfigFactory.CreateValid();
            var mapper = new PuaTokenMapper();
            var encoder = new PhonemeEncoder(config, mapper);

            // 3 phonemes: 2 known + 1 unknown = 33% -> under threshold
            var result = encoder.EncodeWithProsody(new[] { "a", "b", "unknown_phoneme" }, null);

            Assert.That(result.PhonemeIds, Is.Not.Null);
        }

        #endregion
    }
}