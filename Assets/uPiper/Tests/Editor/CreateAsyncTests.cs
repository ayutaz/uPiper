using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;
using uPiper.Core;

namespace uPiper.Tests.Editor
{
    [TestFixture]
    public class CreateAsyncTests
    {
        #region CreateAsync Null Argument Tests

        [Test]
        public void CreateAsync_NullConfig_ThrowsArgumentNullException()
        {
            Assert.ThrowsAsync<ArgumentNullException>(
                async () => await PiperTTS.CreateAsync((PiperConfig)null));
        }

        [Test]
        public void CreateAsync_NullVoiceConfig_ThrowsArgumentNullException()
        {
            Assert.ThrowsAsync<ArgumentNullException>(
                async () => await PiperTTS.CreateAsync(
                    new PiperConfig(), (PiperVoiceConfig)null));
        }

        [Test]
        public void CreateAsync_NullConfigWithVoice_ThrowsArgumentNullException()
        {
            var voice = new PiperVoiceConfig { VoiceId = "test" };
            Assert.ThrowsAsync<ArgumentNullException>(
                async () => await PiperTTS.CreateAsync(null, voice));
        }

        #endregion

        #region ParseModelConfig Tests

        [Test]
        public void ParseModelConfig_ValidJson_ParsesCorrectly()
        {
            var json = @"{
                ""language"": { ""code"": ""en"" },
                ""audio"": { ""sample_rate"": 22050 },
                ""inference"": {
                    ""noise_scale"": 0.667,
                    ""length_scale"": 1.0,
                    ""noise_w"": 0.8
                },
                ""phoneme_type"": ""multilingual"",
                ""phoneme_id_map"": {
                    ""_"": [0],
                    ""^"": [1],
                    ""$"": [2],
                    ""a"": [3]
                },
                ""num_speakers"": 1,
                ""num_languages"": 6,
                ""language_id_map"": {
                    ""ja"": 0,
                    ""en"": 1
                }
            }";

            var config = PiperTTS.ParseModelConfig("test-model", json);

            Assert.That(config.VoiceId, Is.EqualTo("test-model"),
                "VoiceId should match the provided model name");
            Assert.That(config.Language, Is.EqualTo("en"),
                "Language should be parsed from JSON");
            Assert.That(config.SampleRate, Is.EqualTo(22050),
                "SampleRate should be parsed from JSON");
            Assert.That(config.NoiseScale, Is.EqualTo(0.667f).Within(0.001f),
                "NoiseScale should be parsed from JSON");
            Assert.That(config.PhonemeType, Is.EqualTo("multilingual"),
                "PhonemeType should be parsed from JSON");
            Assert.That(config.PhonemeIdMap, Has.Count.EqualTo(4),
                "PhonemeIdMap should contain 4 entries");
            Assert.That(config.NumSpeakers, Is.EqualTo(1),
                "NumSpeakers should be 1");
            Assert.That(config.NumLanguages, Is.EqualTo(6),
                "NumLanguages should be 6");
            Assert.That(config.LanguageIdMap, Has.Count.EqualTo(2),
                "LanguageIdMap should contain 2 entries");
            Assert.That(config.LanguageIdMap["ja"], Is.EqualTo(0),
                "Japanese language ID should be 0");
        }

        [Test]
        public void ParseModelConfig_MinimalJson_UsesDefaults()
        {
            var json = @"{}";

            var config = PiperTTS.ParseModelConfig("minimal", json);

            Assert.That(config.VoiceId, Is.EqualTo("minimal"),
                "VoiceId should match the provided model name");
            Assert.That(config.Language, Is.EqualTo("ja"),
                "Language should default to 'ja'");
            Assert.That(config.SampleRate, Is.EqualTo(22050),
                "SampleRate should default to 22050");
            Assert.That(config.PhonemeType, Is.EqualTo("espeak"),
                "PhonemeType should default to 'espeak'");
        }

        [Test]
        public void ParseModelConfig_InvalidJson_ThrowsException()
        {
            Assert.Throws<JsonReaderException>(() =>
                PiperTTS.ParseModelConfig("bad", "not json"));
        }

        #endregion

        #region AvailableVoices Type Tests

        [Test]
        public void AvailableVoices_ReturnsIReadOnlyListOfPiperVoiceConfig()
        {
            var config = new PiperConfig();
            using var tts = new PiperTTS(config);

            var voices = tts.AvailableVoices;

            Assert.That(voices, Is.InstanceOf<IReadOnlyList<PiperVoiceConfig>>(),
                "AvailableVoices should implement IReadOnlyList<PiperVoiceConfig>");
            Assert.That(voices, Is.Empty,
                "AvailableVoices should be empty before initialization");
        }

        #endregion
    }
}