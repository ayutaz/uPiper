using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using uPiper.Core.AudioGeneration;
using uPiper.Core.Phonemizers;

namespace uPiper.Editor
{
    /// <summary>
    /// Phase 1.10 デバッグ用 - 音素マッピングの問題を診断するためのツール
    /// </summary>
    public static class DebugPhonemeMapping
    {
        [MenuItem("uPiper/Debug/Test Phoneme Mapping for こんにちは")]
        public static void TestKonnichiwaMapping()
        {
            Debug.Log("=== Testing Phoneme Mapping for こんにちは ===");
            
            // Test simple mapping
            var simplePhonemes = ConvertToPhonemes("こんにちは", "ja");
            Debug.Log($"Simple phonemes: {string.Join(" ", simplePhonemes)}");
            
            // Test OpenJTalk to Piper mapping
            var openJTalkPhonemes = new[] { "k", "o", "N", "n", "i", "ch", "i", "w", "a" };
            Debug.Log($"OpenJTalk phonemes (expected): {string.Join(" ", openJTalkPhonemes)}");
            
            var piperPhonemes = OpenJTalkToPiperMapping.ConvertToPiperPhonemes(openJTalkPhonemes);
            Debug.Log($"Piper phonemes: {string.Join(" ", piperPhonemes)}");
            
            // Test with a dummy config
            var config = new PiperVoiceConfig
            {
                VoiceId = "test",
                DisplayName = "test",
                Language = "ja",
                SampleRate = 22050,
                PhonemeIdMap = GetTestPhonemeIdMap()
            };
            
            var encoder = new PhonemeEncoder(config);
            var ids = encoder.Encode(piperPhonemes);
            Debug.Log($"Encoded IDs: {string.Join(", ", ids)}");
            
            // Decode back to verify
            var decoded = encoder.Decode(ids);
            Debug.Log($"Decoded phonemes: {string.Join(" ", decoded)}");
            
            // Specifically test "chi" sound
            Debug.Log("\n=== Testing 'chi' sound specifically ===");
            TestSpecificPhoneme("ch", config, encoder);
            TestSpecificPhoneme("ty", config, encoder);
            
            // Test individual mappings
            Debug.Log("\n=== Individual phoneme tests ===");
            string[] testPhonemes = { "ch", "i", "u", "ch i", "ch u", "ty i", "ty u" };
            foreach (var phoneme in testPhonemes)
            {
                var testArray = phoneme.Split(' ');
                var testIds = encoder.Encode(testArray);
                Debug.Log($"'{phoneme}' -> IDs: {string.Join(", ", testIds)}");
            }
        }
        
        private static void TestSpecificPhoneme(string phoneme, PiperVoiceConfig config, PhonemeEncoder encoder)
        {
            Debug.Log($"\nTesting phoneme: {phoneme}");
            
            // Check if it's in the config
            if (config.PhonemeIdMap.TryGetValue(phoneme, out var id))
            {
                Debug.Log($"  Found in config as ID: {id}");
            }
            else
            {
                Debug.Log($"  NOT found in config directly");
            }
            
            // Test encoding
            var encoded = encoder.Encode(new[] { phoneme });
            Debug.Log($"  Encoded as: {string.Join(", ", encoded)}");
            
            // Test with vowels
            string[] vowels = { "i", "u" };
            foreach (var vowel in vowels)
            {
                var combined = encoder.Encode(new[] { phoneme, vowel });
                Debug.Log($"  '{phoneme} {vowel}' encoded as: {string.Join(", ", combined)}");
            }
        }
        
        private static string[] ConvertToPhonemes(string text, string language)
        {
            var phonemeMap = new Dictionary<string, string[]>
            {
                { "こ", new[] { "k", "o" } },
                { "ん", new[] { "N" } },
                { "に", new[] { "n", "i" } },
                { "ち", new[] { "ch", "i" } },
                { "は", new[] { "w", "a" } }
            };

            var phonemes = new List<string>();
            foreach (char c in text)
            {
                var key = c.ToString();
                if (phonemeMap.TryGetValue(key, out var ph))
                {
                    phonemes.AddRange(ph);
                }
            }
            return phonemes.ToArray();
        }
        
        private static Dictionary<string, int> GetTestPhonemeIdMap()
        {
            // Based on ja_JP-test-medium.onnx.json
            return new Dictionary<string, int>
            {
                { "_", 0 }, { "^", 1 }, { "$", 2 }, { "?", 3 },
                { "#", 4 }, { "[", 5 }, { "]", 6 },
                { "a", 7 }, { "i", 8 }, { "u", 9 }, { "e", 10 }, { "o", 11 },
                { "A", 12 }, { "I", 13 }, { "U", 14 }, { "E", 15 }, { "O", 16 },
                { "\ue000", 17 }, { "\ue001", 18 }, { "\ue002", 19 }, { "\ue003", 20 }, { "\ue004", 21 },
                { "N", 22 }, { "\ue005", 23 }, { "q", 24 }, { "k", 25 },
                { "\ue006", 26 }, { "\ue007", 27 }, { "g", 28 }, { "\ue008", 29 }, { "\ue009", 30 },
                { "t", 31 }, { "\ue00a", 32 }, { "d", 33 }, { "\ue00b", 34 },
                { "p", 35 }, { "\ue00c", 36 }, { "b", 37 }, { "\ue00d", 38 },
                { "\ue00e", 39 }, { "\ue00f", 40 },
                { "s", 41 }, { "\ue010", 42 }, { "z", 43 }, { "j", 44 }, { "\ue011", 45 },
                { "f", 46 }, { "h", 47 }, { "\ue012", 48 }, { "v", 49 },
                { "n", 50 }, { "\ue013", 51 }, { "m", 52 }, { "\ue014", 53 },
                { "r", 54 }, { "\ue015", 55 }, { "w", 56 }, { "y", 57 }
            };
        }
    }
}