using System;
using NUnit.Framework;
using uPiper.Core.AudioGeneration;
using uPiper.Core.Phonemizers;
using uPiper.Core.Phonemizers.Multilingual;
using uPiper.Tests.Editor.TestHelpers;

namespace uPiper.Tests.Editor
{
    /// <summary>
    /// Edge case tests for AudioSynthesisCache, PhonemeEncoder, CustomDictionary,
    /// and SynthesisRequest. Covers boundary values, unusual inputs, and stress scenarios.
    /// </summary>
    [TestFixture]
    public class EdgeCaseTests
    {
        private PuaTokenMapper _mapper;

        [SetUp]
        public void SetUp()
        {
            _mapper = new PuaTokenMapper();
        }

        // ================================================================
        // AudioSynthesisCache edge cases
        // ================================================================

        [Test]
        public void GenerateKey_EmptyPhonemeIds_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
                AudioSynthesisCache.GenerateKey(
                    new int[0], null, 1.0f, 0.667f, 0.8f, 0, 0));
        }

        [Test]
        public void GenerateKey_SingleElement_ProducesValidHash()
        {
            var key = AudioSynthesisCache.GenerateKey(
                new[] { 42 }, null, 1.0f, 0.667f, 0.8f, 0, 0);

            // Hash should be deterministic and non-zero
            Assert.AreNotEqual(0L, key, "Single-element hash should not be zero");

            var key2 = AudioSynthesisCache.GenerateKey(
                new[] { 42 }, null, 1.0f, 0.667f, 0.8f, 0, 0);
            Assert.AreEqual(key, key2, "Same input should produce same hash");
        }

        [Test]
        public void GenerateKey_LargeArray_DoesNotOverflow()
        {
            var largeArray = new int[10000];
            for (var i = 0; i < largeArray.Length; i++)
            {
                largeArray[i] = i;
            }

            long key = 0;
            Assert.DoesNotThrow(() =>
            {
                key = AudioSynthesisCache.GenerateKey(
                    largeArray, null, 1.0f, 0.667f, 0.8f, 0, 0);
            }, "10000-element array should not throw on hash generation");

            // Should produce a deterministic hash
            var key2 = AudioSynthesisCache.GenerateKey(
                largeArray, null, 1.0f, 0.667f, 0.8f, 0, 0);
            Assert.AreEqual(key, key2, "Large array hash should be deterministic");
        }

        [Test]
        public void Set_MaxIntKey_DoesNotThrow()
        {
            var cache = new AudioSynthesisCache();
            var audio = new float[] { 0.1f, 0.2f };

            Assert.DoesNotThrow(() =>
            {
                cache.Set(long.MaxValue, audio, 22050);
            }, "long.MaxValue as key should not throw");

            Assert.IsTrue(cache.TryGet(long.MaxValue, out var samples, out var sampleRate),
                "Should retrieve entry with long.MaxValue key");
            Assert.AreEqual(audio, samples);
            Assert.AreEqual(22050, sampleRate);

            Assert.DoesNotThrow(() =>
            {
                cache.Set(long.MinValue, audio, 22050);
            }, "long.MinValue as key should not throw");

            Assert.IsTrue(cache.TryGet(long.MinValue, out _, out _),
                "Should retrieve entry with long.MinValue key");
        }

        [Test]
        public void TryGet_AfterManyEvictions_CountConsistent()
        {
            const int maxEntries = 10;
            const int totalInsertions = 1000;
            var cache = new AudioSynthesisCache(maxEntries: maxEntries, maxMemoryMB: 100);

            for (var i = 0; i < totalInsertions; i++)
            {
                cache.Set(i, new float[] { i * 0.1f }, 22050);
            }

            Assert.LessOrEqual(cache.Count, maxEntries,
                $"Cache count ({cache.Count}) should not exceed maxEntries ({maxEntries})");
            Assert.AreEqual(maxEntries, cache.Count,
                "Cache should be full after many insertions");
            Assert.AreEqual(totalInsertions - maxEntries, cache.EvictionCount,
                "Eviction count should equal total insertions minus max entries");
        }

        // ================================================================
        // PhonemeEncoder edge cases
        // ================================================================

        [Test]
        public void Encode_EmptyArray_ReturnsMinimalResult()
        {
            var config = TestVoiceConfigFactory.CreateValid();
            var encoder = new PhonemeEncoder(config, _mapper);

            var result = encoder.Encode(Array.Empty<string>());

            Assert.IsNotNull(result, "Encode should not return null for empty input");
            Assert.IsEmpty(result, "Encode should return empty array for empty input");
        }

        [Test]
        public void Encode_SinglePhoneme_IncludesBosAndEos()
        {
            var config = TestVoiceConfigFactory.CreateValid();
            var encoder = new PhonemeEncoder(config, _mapper);

            var result = encoder.Encode(new[] { "a" });

            Assert.IsNotNull(result, "Encode should not return null");
            Assert.GreaterOrEqual(result.Length, 3,
                "Single phoneme should produce at least 3 IDs (BOS + phoneme + EOS)");

            // BOS (^) = ID 1, EOS ($) = ID 2 per TestPhonemeIdMapFactory
            Assert.AreEqual(1, result[0], "First ID should be BOS (^) = 1");
            Assert.AreEqual(2, result[result.Length - 1],
                "Last ID should be EOS ($) = 2");
        }

        // ================================================================
        // CustomDictionary edge cases
        // ================================================================

        [Test]
        public void AddWord_EmojiInWord_HandledGracefully()
        {
            var dict = new CustomDictionary(loadDefaults: false);

            Assert.DoesNotThrow(() =>
            {
                dict.AddWord("\U0001F389", "パーティー", priority: 5);
            }, "Emoji as word should not crash");

            var stats = dict.GetStats();
            Assert.AreEqual(1, stats.TotalEntries,
                "Emoji entry should be added to dictionary");
        }

        [Test]
        public void AddWord_VeryLongWord_HandledGracefully()
        {
            var dict = new CustomDictionary(loadDefaults: false);
            var longWord = new string('A', 1000);

            Assert.DoesNotThrow(() =>
            {
                dict.AddWord(longWord, "ロング", priority: 5);
            }, "1000-char word should not crash");

            var pronunciation = dict.GetPronunciation(longWord);
            Assert.AreEqual("ロング", pronunciation,
                "Should retrieve pronunciation for very long word");
        }

        [Test]
        public void AddWord_SpecialCharacters_HandledGracefully()
        {
            var dict = new CustomDictionary(loadDefaults: false);

            Assert.DoesNotThrow(() =>
            {
                dict.AddWord("C++", "シープラスプラス", priority: 9);
                dict.AddWord("C#", "シーシャープ", priority: 9);
                dict.AddWord(".NET", "ドットネット", priority: 9);
            }, "Special characters (+, #, .) in words should not crash");

            Assert.AreEqual("シープラスプラス", dict.GetPronunciation("C++"),
                "C++ pronunciation should be retrievable");
            Assert.AreEqual("シーシャープ", dict.GetPronunciation("C#"),
                "C# pronunciation should be retrievable");
            Assert.AreEqual("ドットネット", dict.GetPronunciation(".NET"),
                ".NET pronunciation should be retrievable");
        }

        [Test]
        public void ApplyToText_EmptyText_ReturnsEmpty()
        {
            var dict = new CustomDictionary(loadDefaults: false);
            dict.AddWord("Docker", "ドッカー", priority: 9);

            var result = dict.ApplyToText("");

            Assert.AreEqual("", result,
                "Empty text should return empty string");
        }

        [Test]
        public void ApplyToText_NullText_ReturnsNull()
        {
            var dict = new CustomDictionary(loadDefaults: false);
            dict.AddWord("Docker", "ドッカー", priority: 9);

            var result = dict.ApplyToText(null);

            Assert.IsNull(result, "Null text should return null");
        }

        [Test]
        public void ApplyToText_TextWithOnlyEmojis_ReturnsUnchanged()
        {
            var dict = new CustomDictionary(loadDefaults: false);
            dict.AddWord("Docker", "ドッカー", priority: 9);

            var emojiText = "\U0001F389\U0001F38A";
            var result = dict.ApplyToText(emojiText);

            Assert.AreEqual(emojiText, result,
                "Emoji-only text should pass through unchanged when no emoji entries exist");
        }

        [Test]
        public void ApplyToText_VeryLongText_DoesNotThrow()
        {
            var dict = new CustomDictionary(loadDefaults: false);
            dict.AddWord("Docker", "ドッカー", priority: 9);

            // Generate 50KB+ text
            var longText = string.Join(" ", new string[5000]);
            for (var i = 0; i < 50; i++)
            {
                longText += "Docker is great. ";
            }

            string result = null;
            Assert.DoesNotThrow(() =>
            {
                result = dict.ApplyToText(longText);
            }, "50KB+ text should not throw");

            Assert.IsNotNull(result, "Result should not be null for large text");
            Assert.Greater(result.Length, 0, "Result should not be empty for large text");
        }

        // ================================================================
        // SynthesisRequest edge cases
        // ================================================================

        [Test]
        public void FromPhonemes_MaxPhonemes_DoesNotThrow()
        {
            var phonemes = new string[10000];
            for (var i = 0; i < phonemes.Length; i++)
            {
                phonemes[i] = "a";
            }

            SynthesisRequest request = default;
            Assert.DoesNotThrow(() =>
            {
                request = SynthesisRequest.FromPhonemes(phonemes);
            }, "10000 phonemes should not throw");

            Assert.AreEqual(10000, request.Phonemes.Length,
                "Request should contain all 10000 phonemes");
            Assert.IsFalse(request.HasProsody,
                "Request without prosody should report HasProsody as false");
        }

        [Test]
        public void FromPhonemesWithProsody_MismatchedLengths_ThrowsArgumentException()
        {
            var phonemes = new[] { "a", "b", "c" }; // 3 phonemes
            // Correct length would be 3 * 3 = 9, but provide 5
            var badProsody = new[] { 1, 2, 3, 4, 5 };

            var ex = Assert.Throws<ArgumentException>(() =>
                SynthesisRequest.FromPhonemesWithProsody(phonemes, badProsody));

            Assert.AreEqual("prosodyFlat", ex.ParamName,
                "Exception should reference prosodyFlat parameter");
            StringAssert.Contains("9", ex.Message,
                "Exception message should mention expected length (9)");
        }
    }
}