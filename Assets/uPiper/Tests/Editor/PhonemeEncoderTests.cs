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
    public class PhonemeEncoderTests
    {
        private PuaTokenMapper _mapper;

        [SetUp]
        public void SetUp()
        {
            _mapper = new PuaTokenMapper();
        }

        #region Validation

        [Test]
        public void Constructor_NullPhonemeIdMap_ThrowsConfigurationException()
        {
            var config = new PiperVoiceConfig { PhonemeIdMap = null };

            Assert.Throws<PiperConfigurationException>(() =>
                new PhonemeEncoder(config, _mapper));
        }

        [Test]
        public void Constructor_EmptyPhonemeIdMap_ThrowsConfigurationException()
        {
            var config = new PiperVoiceConfig
            {
                PhonemeIdMap = new Dictionary<string, int[]>()
            };

            Assert.Throws<PiperConfigurationException>(() =>
                new PhonemeEncoder(config, _mapper));
        }

        [Test]
        public void Constructor_ValidPhonemeIdMap_DoesNotThrow()
        {
            var config = TestVoiceConfigFactory.CreateValid();

            Assert.DoesNotThrow(() => new PhonemeEncoder(config, _mapper),
                "Valid PhonemeIdMap should not throw on construction");
        }

        [Test]
        public void EncodeWithProsody_EmptyPhonemes_ReturnsEmptyArray()
        {
            var config = TestVoiceConfigFactory.CreateValid();
            var encoder = new PhonemeEncoder(config, _mapper);

            var result = encoder.EncodeWithProsody(Array.Empty<string>(), null);

            Assert.That(result.PhonemeIds, Is.Not.Null,
                "PhonemeIds should not be null for empty input");
            Assert.That(result.PhonemeIds, Is.Empty,
                "PhonemeIds should be empty for empty input");
        }

        #endregion

        #region Unknown Phoneme Threshold

        [Test]
        public void EncodeWithProsody_AllPhonemesMapped_NoException()
        {
            var config = TestVoiceConfigFactory.CreateValid();
            var encoder = new PhonemeEncoder(config, _mapper);

            var result = encoder.EncodeWithProsody(new[] { "a", "b" }, null);

            Assert.That(result.PhonemeIds, Is.Not.Null,
                "PhonemeIds should not be null when all phonemes are mapped");
            Assert.That(result.PhonemeIds.Length, Is.GreaterThan(0),
                "PhonemeIds should contain elements when all phonemes are mapped");
        }

        [Test]
        public void EncodeWithProsody_SingleUnknownPhoneme_DoesNotThrow()
        {
            var config = TestVoiceConfigFactory.CreateValid();
            var encoder = new PhonemeEncoder(config, _mapper);

            // 3 phonemes: 2 known + 1 unknown = 33% -> under threshold
            var result = encoder.EncodeWithProsody(new[] { "a", "b", "unknown_phoneme" }, null);

            Assert.That(result.PhonemeIds, Is.Not.Null,
                "PhonemeIds should not be null when unknown ratio is below threshold");
        }

        [Test]
        public void EncodeWithProsody_Exactly50PercentUnknown_DoesNotThrow()
        {
            // 2 known + 2 unknown = 50% = threshold exactly -> > check, so passes
            var config = TestVoiceConfigFactory.CreateValid();
            var encoder = new PhonemeEncoder(config, _mapper);

            var result = encoder.EncodeWithProsody(
                new[] { "a", "b", "xxx", "yyy" }, null);

            Assert.That(result.PhonemeIds, Is.Not.Null,
                "PhonemeIds should not be null at exactly 50% unknown threshold");
        }

        [Test]
        public void EncodeWithProsody_JustOver50PercentUnknown_ThrowsConfigurationException()
        {
            // 1 known + 2 unknown = 66% -> exception
            var config = TestVoiceConfigFactory.CreateValid();
            var encoder = new PhonemeEncoder(config, _mapper);

            Assert.Throws<PiperConfigurationException>(() =>
                encoder.EncodeWithProsody(new[] { "a", "xxx", "yyy" }, null));
        }

        [Test]
        public void EncodeWithProsody_Over50PercentUnknown_MessageContainsUnknown()
        {
            // Minimal config with space entry to match original test setup
            var minimalMap = TestPhonemeIdMapFactory.CreateMinimal();
            minimalMap[" "] = new[] { 3 };
            var config = new PiperVoiceConfig { PhonemeIdMap = minimalMap };
            var encoder = new PhonemeEncoder(config, _mapper);

            // 3 phonemes, none in the minimal map -> 100% unknown -> should throw
            var ex = Assert.Throws<PiperConfigurationException>(() =>
                encoder.EncodeWithProsody(new[] { "zzz", "yyy", "xxx" }, null));

            Assert.That(ex.Message, Does.Contain("unknown to the model"),
                "Exception message should mention 'unknown to the model'");
        }

        #endregion

        #region ProsodyFlat

        [Test]
        public void EncodeWithProsody_ProsodyFlatShorterThanExpected_DoesNotThrow()
        {
            // phonemes = 3, prosodyFlat should be 9 (3*3) but provide only 3
            var config = TestVoiceConfigFactory.CreateValid();
            var encoder = new PhonemeEncoder(config, _mapper);

            var result = encoder.EncodeWithProsody(
                new[] { "a", "b", "c" },
                new[] { 1, 2, 3 });  // Only 3 instead of 9

            Assert.That(result.PhonemeIds, Is.Not.Null,
                "PhonemeIds should not be null even with shorter ProsodyFlat");
        }

        #endregion
    }
}