using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core;
using uPiper.Core.AudioGeneration;
using uPiper.Core.Phonemizers.Backend.Flite;

namespace uPiper.Tests.Runtime
{
    /// <summary>
    /// End-to-end tests for English audio generation
    /// </summary>
    [TestFixture]
    public class EnglishAudioGenerationTests
    {
        private FliteLTSPhonemizer _phonemizer;
        private PhonemeEncoder _encoder;
        private PiperVoiceConfig _config;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Create English voice config
            _config = new PiperVoiceConfig
            {
                VoiceId = "en_US-ljspeech-medium",
                Language = "en",
                SampleRate = 22050,
                PhonemeIdMap = CreateEnglishPhonemeIdMap()
            };
        }

        [SetUp]
        public void SetUp()
        {
            _phonemizer = new FliteLTSPhonemizer();
            _encoder = new PhonemeEncoder(_config);
        }

        [TearDown]
        public void TearDown()
        {
            _phonemizer?.Dispose();
        }

        private Dictionary<string, int> CreateEnglishPhonemeIdMap()
        {
            // Minimal phoneme ID map for testing
            var map = new Dictionary<string, int>
            {
                ["_"] = 0,  // PAD
                ["^"] = 1,  // BOS
                ["$"] = 2,  // EOS
                [" "] = 3,  // Space
                // Common IPA phonemes
                ["h"] = 20,
                ["ɛ"] = 61,
                ["l"] = 24,
                ["o"] = 27,
                ["w"] = 35,
                ["ɚ"] = 60,
                ["ɹ"] = 88,
                ["d"] = 17,
                ["ð"] = 41,
                ["ɪ"] = 74,
                ["s"] = 31,
                ["z"] = 38,
                ["ə"] = 59,
                ["t"] = 32,
                ["k"] = 23,
                ["u"] = 33,
                ["b"] = 15,
                ["a"] = 14,
                ["n"] = 26,
                ["f"] = 19,
                ["ɔ"] = 54,
                ["ks"] = 36, // x
                ["dʒ"] = 22, // j
                ["ʌ"] = 102,
                ["m"] = 25,
                ["p"] = 28,
                ["v"] = 34,
                ["i"] = 21,
                ["e"] = 18,
                ["g"] = 66,
                ["j"] = 37  // y
            };
            return map;
        }

        [UnityTest]
        public IEnumerator TestFliteLTSInitialization()
        {
            var initTask = _phonemizer.InitializeAsync(new uPiper.Core.Phonemizers.Backend.PhonemizerBackendOptions());

            // Wait for initialization
            while (!initTask.IsCompleted)
            {
                yield return null;
            }

            Assert.IsTrue(initTask.Result, "FliteLTS should initialize successfully");
        }

        [UnityTest]
        public IEnumerator TestHelloWorldPhonemization()
        {
            // Initialize phonemizer
            var initTask = _phonemizer.InitializeAsync(new uPiper.Core.Phonemizers.Backend.PhonemizerBackendOptions());
            while (!initTask.IsCompleted) yield return null;
            Assert.IsTrue(initTask.Result);

            // Phonemize "Hello world"
            var phonemizeTask = _phonemizer.PhonemizeAsync("Hello world", "en");
            while (!phonemizeTask.IsCompleted) yield return null;

            var result = phonemizeTask.Result;
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Phonemes);
            Assert.Greater(result.Phonemes.Length, 0);

            Debug.Log($"[Test] Arpabet phonemes for 'Hello world': {string.Join(" ", result.Phonemes)}");

            // Expected Arpabet: HH EH L OW _ W ER L D (or similar)
            Assert.IsTrue(result.Phonemes.Any(p => p.StartsWith("HH")), "Should contain 'H' sound");
            Assert.IsTrue(result.Phonemes.Any(p => p.StartsWith("L")), "Should contain 'L' sound");
            Assert.IsTrue(result.Phonemes.Any(p => p.StartsWith("W")), "Should contain 'W' sound");
        }

        [Test]
        public void TestArpabetToIPAConversion()
        {
            // Test the conversion used in InferenceEngineDemo
            var arpabetPhonemes = new[] { "HH", "EH0", "L", "OW1", "W", "ER0", "L", "D" };
            var ipaPhonemes = ConvertArpabetToIPA(arpabetPhonemes);

            Debug.Log($"[Test] Arpabet: {string.Join(" ", arpabetPhonemes)}");
            Debug.Log($"[Test] IPA: {string.Join(" ", ipaPhonemes)}");

            Assert.AreEqual(arpabetPhonemes.Length, ipaPhonemes.Length);
            Assert.AreEqual("h", ipaPhonemes[0]);
            Assert.AreEqual("ɛ", ipaPhonemes[1]);
            Assert.AreEqual("l", ipaPhonemes[2]);
            Assert.AreEqual("o", ipaPhonemes[3]); // Simplified diphthong
            Assert.AreEqual("w", ipaPhonemes[4]);
            Assert.AreEqual("ɚ", ipaPhonemes[5]);
            Assert.AreEqual("l", ipaPhonemes[6]);
            Assert.AreEqual("d", ipaPhonemes[7]);
        }

        [Test]
        public void TestEnglishPhonemeEncoding()
        {
            // Test encoding IPA phonemes for "hello world"
            var ipaPhonemes = new[] { "h", "ɛ", "l", "o", " ", "w", "ɚ", "l", "d" };
            var ids = _encoder.Encode(ipaPhonemes);

            Debug.Log($"[Test] Input phonemes ({ipaPhonemes.Length}): {string.Join(" ", ipaPhonemes)}");
            Debug.Log($"[Test] Output IDs ({ids.Length}): {string.Join(", ", ids)}");

            // For eSpeak models: BOS + (phoneme + PAD)* + EOS
            var expectedLength = 1 + (ipaPhonemes.Length * 2) + 1;
            Assert.AreEqual(expectedLength, ids.Length, "Should have correct number of IDs");

            // Check structure
            Assert.AreEqual(1, ids[0], "First ID should be BOS");
            Assert.AreEqual(2, ids[ids.Length - 1], "Last ID should be EOS");

            // Check that each phoneme is followed by PAD
            for (int i = 0; i < ipaPhonemes.Length; i++)
            {
                var phonemeIndex = 1 + (i * 2);
                var padIndex = phonemeIndex + 1;

                if (padIndex < ids.Length - 1)
                {
                    Assert.AreEqual(0, ids[padIndex], $"Phoneme at position {i} should be followed by PAD");
                }
            }
        }

        [UnityTest]
        public IEnumerator TestFullEnglishPipeline()
        {
            // Initialize phonemizer
            var initTask = _phonemizer.InitializeAsync(new uPiper.Core.Phonemizers.Backend.PhonemizerBackendOptions());
            while (!initTask.IsCompleted) yield return null;
            Assert.IsTrue(initTask.Result);

            // Test text
            var text = "Hello world";

            // Step 1: Phonemize
            var phonemizeTask = _phonemizer.PhonemizeAsync(text, "en");
            while (!phonemizeTask.IsCompleted) yield return null;

            var phonemeResult = phonemizeTask.Result;
            Assert.IsNotNull(phonemeResult);

            var arpabetPhonemes = phonemeResult.Phonemes;
            Debug.Log($"[Test] Step 1 - Arpabet ({arpabetPhonemes.Length}): {string.Join(" ", arpabetPhonemes)}");

            // Step 2: Convert to IPA
            var ipaPhonemes = ConvertArpabetToIPA(arpabetPhonemes);
            Debug.Log($"[Test] Step 2 - IPA ({ipaPhonemes.Length}): {string.Join(" ", ipaPhonemes)}");

            // Step 3: Encode
            var ids = _encoder.Encode(ipaPhonemes);
            Debug.Log($"[Test] Step 3 - IDs ({ids.Length}): {string.Join(", ", ids)}");

            // Verify the pipeline produces reasonable output
            Assert.Greater(arpabetPhonemes.Length, 0, "Should have Arpabet phonemes");
            Assert.Greater(ipaPhonemes.Length, 0, "Should have IPA phonemes");
            Assert.Greater(ids.Length, 0, "Should have encoded IDs");

            // For "Hello world", we expect around 8-10 phonemes
            Assert.GreaterOrEqual(arpabetPhonemes.Length, 6, "Should have at least 6 phonemes");
            Assert.LessOrEqual(arpabetPhonemes.Length, 12, "Should have at most 12 phonemes");
        }

        private string[] ConvertArpabetToIPA(string[] arpabetPhonemes)
        {
            var arpabetToIPA = new Dictionary<string, string>
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

            var result = new string[arpabetPhonemes.Length];
            for (int i = 0; i < arpabetPhonemes.Length; i++)
            {
                var basePhoneme = arpabetPhonemes[i].TrimEnd('0', '1', '2');
                result[i] = arpabetToIPA.TryGetValue(basePhoneme.ToUpper(), out var ipa)
                    ? ipa : arpabetPhonemes[i].ToLower();
            }
            return result;
        }
    }
}