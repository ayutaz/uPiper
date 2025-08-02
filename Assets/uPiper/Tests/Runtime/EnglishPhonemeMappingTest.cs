using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core;
using uPiper.Core.AudioGeneration;
using uPiper.Core.Logging;

namespace uPiper.Tests.Runtime
{
    /// <summary>
    /// Test English phoneme mapping and token processing
    /// </summary>
    public class EnglishPhonemeMappingTest
    {
        [Test]
        public void TestEnglishPhonemeMapping()
        {
            // Create test config with English phoneme mapping
            var config = new PiperVoiceConfig
            {
                VoiceId = "en_US-ljspeech-medium",
                PhonemeIdMap = new Dictionary<string, int>
                {
                    ["_"] = 0,  // PAD
                    ["^"] = 1,  // BOS
                    ["$"] = 2,  // EOS
                    [" "] = 3,  // Space
                    ["h"] = 20,
                    ["ɛ"] = 61,
                    ["l"] = 24,
                    ["o"] = 27,
                    ["w"] = 35,
                    ["ɚ"] = 60,
                    ["ɹ"] = 88,
                    ["d"] = 17
                }
            };

            var encoder = new PhonemeEncoder(config);

            // Test phonemes for "hello world"
            // Expected Arpabet: HH EH L OW _ W ER L D
            // Converted IPA: h ɛ l o _ w ɚ l d
            var testPhonemes = new[] { "h", "ɛ", "l", "o", " ", "w", "ɚ", "l", "d" };

            Debug.Log($"Input phonemes ({testPhonemes.Length}): {string.Join(" ", testPhonemes)}");

            var ids = encoder.Encode(testPhonemes);

            Debug.Log($"Output IDs ({ids.Length}): {string.Join(", ", ids)}");

            // For eSpeak models, we expect:
            // BOS + (phoneme + PAD)* + EOS
            // So for 9 phonemes, we should get: 1 BOS + 9*(phoneme+PAD) + 1 EOS = 20 IDs
            var expectedLength = 1 + (testPhonemes.Length * 2) + 1;

            Debug.Log($"Expected length: {expectedLength}, Actual length: {ids.Length}");

            // Verify structure
            Assert.AreEqual(1, ids[0], "First ID should be BOS (1)");
            Assert.AreEqual(2, ids[ids.Length - 1], "Last ID should be EOS (2)");

            // Verify each phoneme is followed by PAD
            var idIndex = 1; // Skip BOS
            foreach (var phoneme in testPhonemes)
            {
                if (config.PhonemeIdMap.TryGetValue(phoneme, out var expectedId))
                {
                    Assert.AreEqual(expectedId, ids[idIndex], $"Phoneme '{phoneme}' should map to ID {expectedId}");
                    Assert.AreEqual(0, ids[idIndex + 1], $"Phoneme '{phoneme}' should be followed by PAD (0)");
                    idIndex += 2;
                }
            }

            // Log the detailed mapping
            Debug.Log("Detailed ID mapping:");
            Debug.Log($"  BOS: {ids[0]}");
            for (int i = 0; i < testPhonemes.Length; i++)
            {
                var baseIndex = 1 + (i * 2);
                if (baseIndex < ids.Length - 1)
                {
                    Debug.Log($"  {testPhonemes[i]}: {ids[baseIndex]} + PAD: {ids[baseIndex + 1]}");
                }
            }
            Debug.Log($"  EOS: {ids[ids.Length - 1]}");
        }

        [Test]
        public void TestArpabetToIPAConversion()
        {
            // Test Arpabet to IPA conversion for "hello world"
            var arpabetPhonemes = new[] { "HH", "EH0", "L", "OW1", "W", "ER0", "L", "D" };

            // Expected IPA (with simplified diphthongs)
            var expectedIPA = new[] { "h", "ɛ", "l", "o", "w", "ɚ", "l", "d" };

            var result = ArpabetToIPAConverterTemp.ConvertAll(arpabetPhonemes);

            Debug.Log($"Arpabet: {string.Join(" ", arpabetPhonemes)}");
            Debug.Log($"IPA: {string.Join(" ", result)}");

            Assert.AreEqual(expectedIPA.Length, result.Length);
            for (int i = 0; i < expectedIPA.Length; i++)
            {
                Assert.AreEqual(expectedIPA[i], result[i], $"Mismatch at position {i}");
            }
        }
    }

    // Copy of the converter from InferenceEngineDemo for testing
    internal static class ArpabetToIPAConverterTemp
    {
        private static readonly Dictionary<string, string> ArpabetToIPA = new()
        {
            // Vowels
            ["AA"] = "ɑ",
            ["AE"] = "æ",
            ["AH"] = "ʌ",
            ["AO"] = "ɔ",
            ["AW"] = "a",
            ["AY"] = "a",
            ["EH"] = "ɛ",
            ["ER"] = "ɚ",
            ["EY"] = "e",
            ["IH"] = "ɪ",
            ["IY"] = "i",
            ["OW"] = "o",
            ["OY"] = "ɔ",
            ["UH"] = "ʊ",
            ["UW"] = "u",
            // Consonants
            ["B"] = "b",
            ["CH"] = "tʃ",
            ["D"] = "d",
            ["DH"] = "ð",
            ["F"] = "f",
            ["G"] = "ɡ",
            ["HH"] = "h",
            ["JH"] = "dʒ",
            ["K"] = "k",
            ["L"] = "l",
            ["M"] = "m",
            ["N"] = "n",
            ["NG"] = "ŋ",
            ["P"] = "p",
            ["R"] = "ɹ",
            ["S"] = "s",
            ["SH"] = "ʃ",
            ["T"] = "t",
            ["TH"] = "θ",
            ["V"] = "v",
            ["W"] = "w",
            ["Y"] = "j",
            ["Z"] = "z",
            ["ZH"] = "ʒ",
        };
        public static string[] ConvertAll(string[] arpabetPhonemes)
        {
            var result = new string[arpabetPhonemes.Length];
            for (int i = 0; i < arpabetPhonemes.Length; i++)
            {
                var basePhoneme = arpabetPhonemes[i].TrimEnd('0', '1', '2');
                result[i] = ArpabetToIPA.TryGetValue(basePhoneme.ToUpper(), out var ipa)
                    ? ipa : arpabetPhonemes[i].ToLower();
            }
            return result;
        }
    }
}