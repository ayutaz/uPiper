using System;
using NUnit.Framework;
using uPiper.Core;
using uPiper.Core.AudioGeneration;
using uPiper.Core.Phonemizers.Multilingual;
using uPiper.Tests.Editor.TestHelpers;

namespace uPiper.Tests.Editor
{
    [TestFixture]
    public class PhonemeEncoderBoundaryTests
    {
        private PuaTokenMapper _mapper;

        [SetUp]
        public void SetUp()
        {
            _mapper = new PuaTokenMapper();
        }

        [Test]
        public void EncodeWithProsody_Exactly50PercentUnknown_DoesNotThrow()
        {
            // 2 known + 2 unknown = 50% = threshold exactly -> > check, so passes
            var config = TestVoiceConfigFactory.CreateValid();
            var encoder = new PhonemeEncoder(config, _mapper);

            var result = encoder.EncodeWithProsody(
                new[] { "a", "b", "xxx", "yyy" }, null);
            Assert.That(result.PhonemeIds, Is.Not.Null);
        }

        [Test]
        public void EncodeWithProsody_JustOver50PercentUnknown_Throws()
        {
            // 1 known + 2 unknown = 66% -> exception
            var config = TestVoiceConfigFactory.CreateValid();
            var encoder = new PhonemeEncoder(config, _mapper);

            Assert.Throws<PiperConfigurationException>(() =>
                encoder.EncodeWithProsody(new[] { "a", "xxx", "yyy" }, null));
        }

        [Test]
        public void EncodeWithProsody_EmptyPhonemes_ReturnsEmptyArray()
        {
            var config = TestVoiceConfigFactory.CreateValid();
            var encoder = new PhonemeEncoder(config, _mapper);

            var result = encoder.EncodeWithProsody(Array.Empty<string>(), null);
            Assert.That(result.PhonemeIds, Is.Not.Null);
            Assert.That(result.PhonemeIds, Is.Empty);
        }

        [Test]
        public void EncodeWithProsody_ProsodyFlatShorterThanExpected_DoesNotThrow()
        {
            // phonemes = 3, prosodyFlat should be 9 (3*3) but provide only 3
            var config = TestVoiceConfigFactory.CreateValid();
            var encoder = new PhonemeEncoder(config, _mapper);

            var result = encoder.EncodeWithProsody(
                new[] { "a", "b", "c" },
                new[] { 1, 2, 3 });  // Only 3 instead of 9
            Assert.That(result.PhonemeIds, Is.Not.Null);
        }
    }
}