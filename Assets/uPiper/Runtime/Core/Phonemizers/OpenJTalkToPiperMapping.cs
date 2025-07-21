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
            // NOTE: Palatalized consonants (ky, gy, ny, etc.) are NOT mapped here
            // They are handled by PUA mapping to preserve the palatalization
            { "ry", "r" },   // りゃ行 - simplified to r (less common)
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
        /// 
        /// IMPORTANT: These values are hardcoded to match pre-trained Piper models
        /// =====================================================================
        /// DO NOT CHANGE these values unless you're using a different model.
        /// Each Piper model has its own phoneme_id_map defined in the model JSON.
        /// These default values work with standard Japanese Piper models.
        /// 
        /// If using a custom model, override these values by loading from model JSON.
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
        /// 
        /// IMPORTANT: Why we use PUA characters
        /// ====================================
        /// Piper's VITS model expects one character per phoneme token.
        /// Japanese has multi-character phonemes like "ky" (きゃ), "ch" (ち), "ts" (つ).
        /// 
        /// Solution: Map multi-char phonemes to single Unicode PUA characters (U+E000-U+F8FF).
        /// These are guaranteed to never conflict with real text characters.
        /// 
        /// Example flow:
        /// 1. OpenJTalk outputs: ["k", "y", "o", "u"] for "きょう"
        /// 2. We combine "ky" → "\ue006" 
        /// 3. Send to Piper: ["\ue006", "o", "u"]
        /// 4. Piper model recognizes "\ue006" as the "ky" phoneme
        /// 
        /// This mapping MUST match the phoneme_id_map in the Piper model JSON.
        /// If you change these values, the model won't recognize the phonemes correctly.
        /// 
        /// Reference: This approach is used by pyopenjtalk and Japanese Piper models.
        /// </summary>
        private static readonly Dictionary<string, string> PhonemeToPUA = new Dictionary<string, string>
        {
            // Palatalized consonants (拗音 - youon)
            { "ky", "\ue006" },  // きゃ、きゅ、きょ
            { "gy", "\ue008" },  // ぎゃ、ぎゅ、ぎょ
            { "sy", "\ue010" },  // しゃ、しゅ、しょ (= sh)
            { "zy", "\ue011" },  // じゃ、じゅ、じょ
            { "ty", "\ue00a" },  // ちゃ、ちゅ、ちょ
            { "dy", "\ue00b" },  // でゃ、でゅ、でょ (rare)
            { "ny", "\ue013" },  // にゃ、にゅ、にょ
            { "hy", "\ue012" },  // ひゃ、ひゅ、ひょ
            { "by", "\ue00d" },  // びゃ、びゅ、びょ
            { "py", "\ue00c" },  // ぴゃ、ぴゅ、ぴょ
            { "my", "\ue014" },  // みゃ、みゅ、みょ
            { "ry", "\ue015" },  // りゃ、りゅ、りょ
            
            // Other multi-character phonemes
            { "ch", "\ue00e" },  // ち、ちゃ、ちゅ、ちょ
            { "ts", "\ue00f" },  // つ
            { "sh", "\ue010" }   // し、しゃ、しゅ、しょ (same as "sy")
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
                // This should be checked BEFORE the simple mapping to ensure "ky" -> PUA, not "ky" -> "k"
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