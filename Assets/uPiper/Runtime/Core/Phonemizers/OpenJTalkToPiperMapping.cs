using System.Collections.Generic;

namespace uPiper.Core.Phonemizers
{
    /// <summary>
    /// Provides mapping between OpenJTalk phonemes and Piper model phoneme IDs.
    /// This class handles the conversion between different phoneme representations.
    /// </summary>
    public static class OpenJTalkToPiperMapping
    {
        /// <summary>
        /// Maps OpenJTalk phonemes to simplified Piper phonemes.
        /// OpenJTalk uses more detailed phoneme representations that need to be mapped to Piper's simpler set.
        /// </summary>
        private static readonly Dictionary<string, string> OpenJTalkToPiperPhoneme = new Dictionary<string, string>
        {
            // Silence and pauses
            { "pau", "_" },  // Pause to pad token
            { "sil", "_" },  // Silence to pad token
            { "sp", "_" },   // Short pause to pad token
            
            // Vowels
            { "a", "a" },
            { "i", "i" },
            { "u", "u" },
            { "e", "e" },
            { "o", "o" },
            
            // Consonants - Basic
            { "k", "k" },
            { "g", "g" },
            { "s", "s" },
            { "z", "z" },
            { "t", "t" },
            { "d", "d" },
            { "n", "n" },
            { "h", "h" },
            { "b", "b" },
            { "p", "p" },
            { "m", "m" },
            { "y", "y" },
            { "r", "r" },
            { "w", "w" },
            
            // Special consonants
            { "N", "N" },    // ん
            { "cl", "t" },   // Closure for っ (small tsu) - map to t
            { "ts", "ts" },  // つ -> Will be mapped to PUA by PhonemeEncoder
            { "ch", "ch" },  // ち -> Will be mapped to PUA by PhonemeEncoder
            { "sh", "sh" },  // し -> Will be mapped to PUA by PhonemeEncoder
            { "j", "j" },    // じ
            { "f", "f" },    // ふ
            { "ry", "r" },   // りゃ行 - simplified to r
            { "ky", "k" },   // きゃ行 - simplified to k
            { "gy", "g" },   // ぎゃ行 - simplified to g
            { "ny", "n" },   // にゃ行 - simplified to n
            { "hy", "h" },   // ひゃ行 - simplified to h
            { "by", "b" },   // びゃ行 - simplified to b
            { "py", "p" },   // ぴゃ行 - simplified to p
            { "my", "m" },   // みゃ行 - simplified to m
            { "ty", "ch" },  // てぃ -> ち (OpenJTalk may output ty for ち)
            
            // Long vowels (map to regular vowels for simplicity)
            { "a:", "a" },
            { "i:", "i" },
            { "u:", "u" },
            { "e:", "e" },
            { "o:", "o" },
            
            // Question mark (rising intonation)
            { "?", "_" }
        };

        /// <summary>
        /// Default Piper phoneme to ID mapping for Japanese.
        /// This should match the phoneme_id_map in the Piper model's JSON configuration.
        /// </summary>
        private static readonly Dictionary<string, int> DefaultPiperPhonemeToId = new Dictionary<string, int>
        {
            // Special tokens
            { "_", 0 },      // Pad token
            { "^", 1 },      // Start token
            { "$", 2 },      // End token
            
            // Basic vowels
            { "a", 4 },
            { "i", 5 },
            { "u", 6 },
            { "U", 6 },      // 無声化された「う」
            { "e", 7 },
            { "o", 8 },
            
            // Consonants
            { "k", 9 },
            { "g", 10 },
            { "s", 11 },
            { "z", 12 },
            { "t", 13 },
            { "d", 14 },
            { "n", 15 },
            { "h", 16 },
            { "b", 17 },
            { "p", 18 },
            { "m", 19 },
            { "y", 20 },
            { "r", 21 },
            { "w", 22 },
            { "N", 23 },     // ん
            { "ts", 24 },    // つ
            { "ch", 25 },    // ち
            { "sh", 26 },    // し
            { "j", 27 },     // じ
            { "f", 28 },     // ふ
            
            // 拗音 (Palatalized consonants)
            { "ky", 25 },    // きゃ、きゅ、きょ
            { "gy", 12 },    // ぎゃ、ぎゅ、ぎょ
            { "sy", 26 },    // しゃ、しゅ、しょ (= sh)
            { "zy", 27 },    // じゃ、じゅ、じょ (= j)
            { "ty", 25 },    // ちゃ、ちゅ、ちょ (= ch)
            { "dy", 33 },    // でゃ、でゅ、でょ
            { "ny", 32 },    // にゃ、にゅ、にょ
            { "hy", 16 },    // ひゃ、ひゅ、ひょ
            { "by", 17 },    // びゃ、びゅ、びょ
            { "py", 18 },    // ぴゃ、ぴゅ、ぴょ
            { "my", 19 },    // みゃ、みゅ、みょ
            { "ry", 21 },    // りゃ、りゅ、りょ
            
            // Space (word boundary)
            { " ", 3 }
        };

        /// <summary>
        /// Multi-character phonemes to PUA (Private Use Area) mapping
        /// </summary>
        private static readonly Dictionary<string, string> PhonemeToPUA = new Dictionary<string, string>
        {
            // Palatalized consonants
            { "ky", "\ue006" },
            { "gy", "\ue008" },
            { "sy", "\ue010" },  // sh
            { "zy", "\ue011" },
            { "ty", "\ue00a" },
            { "dy", "\ue00b" },
            { "ny", "\ue013" },
            { "hy", "\ue012" },
            { "by", "\ue00d" },
            { "py", "\ue00c" },
            { "my", "\ue014" },
            { "ry", "\ue015" },
            // Other multi-character phonemes
            { "ch", "\ue00e" },
            { "ts", "\ue00f" },
            { "sh", "\ue010" }
        };

        /// <summary>
        /// Converts OpenJTalk phonemes to Piper phonemes.
        /// </summary>
        /// <param name="openJTalkPhonemes">Array of OpenJTalk phonemes.</param>
        /// <returns>Array of Piper-compatible phonemes.</returns>
        public static string[] ConvertToPiperPhonemes(string[] openJTalkPhonemes)
        {
            var result = new List<string>();
            
            for (int i = 0; i < openJTalkPhonemes.Length; i++)
            {
                var phoneme = openJTalkPhonemes[i];
                if (string.IsNullOrEmpty(phoneme))
                    continue;
                
                // Check if this is a multi-character phoneme that needs PUA conversion
                if (PhonemeToPUA.ContainsKey(phoneme.ToLower()))
                {
                    result.Add(PhonemeToPUA[phoneme.ToLower()]);
                    continue;
                }
                
                // Special handling for "t i" sequence -> "ch i" (for ち)
                if (phoneme.ToLower() == "t" && i + 1 < openJTalkPhonemes.Length && openJTalkPhonemes[i + 1].ToLower() == "i")
                {
                    // Check if this is actually "ち" sound
                    // Look at the previous phoneme to determine context
                    bool isChiSound = true;
                    
                    // If preceded by "t" (like in "tti"), it's not "chi"
                    if (i > 0 && openJTalkPhonemes[i - 1].ToLower() == "t")
                    {
                        isChiSound = false;
                    }
                    
                    if (isChiSound)
                    {
                        result.Add("\ue00e"); // PUA for "ch"
                        continue;
                    }
                }
                
                // Handle pause/silence
                if (phoneme.ToLower() == "pau")
                {
                    result.Add("_");
                    continue;
                }
                
                // Try to map the phoneme
                if (OpenJTalkToPiperPhoneme.TryGetValue(phoneme.ToLower(), out var piperPhoneme))
                {
                    result.Add(piperPhoneme);
                }
                else
                {
                    // If no mapping exists, use the original phoneme
                    // IMPORTANT: Preserve case for unvoiced vowels (U, I, E, O, A)
                    if (phoneme.Length == 1 && "UIEOA".Contains(phoneme))
                    {
                        result.Add(phoneme); // Keep uppercase for unvoiced vowels
                    }
                    else
                    {
                        result.Add(phoneme.ToLower());
                    }
                }
            }
            
            return result.ToArray();
        }

        /// <summary>
        /// Gets the default Piper phoneme to ID mapping.
        /// This can be overridden by the actual model's phoneme_id_map.
        /// </summary>
        /// <returns>Dictionary mapping phonemes to IDs.</returns>
        public static Dictionary<string, int> GetDefaultPhonemeToIdMapping()
        {
            return new Dictionary<string, int>(DefaultPiperPhonemeToId);
        }

        /// <summary>
        /// Converts OpenJTalk phonemes directly to Piper phoneme IDs.
        /// </summary>
        /// <param name="openJTalkPhonemes">Array of OpenJTalk phonemes.</param>
        /// <param name="phonemeIdMap">The Piper model's phoneme to ID mapping.</param>
        /// <returns>Array of phoneme IDs.</returns>
        public static int[] ConvertToPhonemeIds(string[] openJTalkPhonemes, Dictionary<string, int> phonemeIdMap)
        {
            var piperPhonemes = ConvertToPiperPhonemes(openJTalkPhonemes);
            var result = new List<int>();
            
            foreach (var phoneme in piperPhonemes)
            {
                if (phonemeIdMap.TryGetValue(phoneme, out var id))
                {
                    result.Add(id);
                }
                else
                {
                    // Default to pad token if phoneme not found
                    result.Add(0);
                }
            }
            
            return result.ToArray();
        }
    }
}