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
        private static readonly Dictionary<string, string> OpenJTalkToPiperPhoneme = new()
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
        private static readonly Dictionary<string, int> DefaultPiperPhonemeToId = new()
        {
            // Special tokens (from ja_JP-test-medium.onnx.json)
            { "_", 0 },      // Pad token
            { "^", 1 },      // Start token
            { "$", 2 },      // End token
            { "?", 3 },      // Question
            { "#", 4 },      // Number sign
            { "[", 5 },      // Left bracket
            { "]", 6 },      // Right bracket
            
            // Basic vowels (from model JSON)
            { "a", 7 },
            { "i", 8 },
            { "u", 9 },
            { "e", 10 },
            { "o", 11 },
            { "A", 12 },     // Large A
            { "I", 13 },     // Large I
            { "U", 14 },     // Large U
            { "E", 15 },     // Large E
            { "O", 16 },     // Large O
            
            // PUA mappings for long vowels and multi-char phonemes (from model JSON)
            { "\ue000", 17 },  // a:
            { "\ue001", 18 },  // i:
            { "\ue002", 19 },  // u:
            { "\ue003", 20 },  // e:
            { "\ue004", 21 },  // o:
            { "N", 22 },       // ん
            { "\ue005", 23 },  // cl
            { "q", 24 },       // glottal stop
            { "k", 25 },
            { "\ue006", 26 },  // ky
            { "\ue007", 27 },  // kw
            { "g", 28 },
            { "\ue008", 29 },  // gy
            { "\ue009", 30 },  // gw
            { "t", 31 },
            { "\ue00a", 32 },  // ty (and ch!)
            { "d", 33 },
            { "\ue00b", 34 },  // dy
            { "p", 35 },
            { "\ue00c", 36 },  // py
            { "b", 37 },
            { "\ue00d", 38 },  // by
            { "\ue00e", 39 },  // ch (mapped to same ID as ty)
            { "\ue00f", 40 },  // ts
            { "s", 41 },
            { "\ue010", 42 },  // sh (and sy)
            { "z", 43 },
            { "j", 44 },
            { "\ue011", 45 },  // zy
            { "f", 46 },
            { "h", 47 },
            { "\ue012", 48 },  // hy
            { "v", 49 },
            { "n", 50 },
            { "\ue013", 51 },  // ny
            { "m", 52 },
            { "\ue014", 53 },  // my
            { "r", 54 },
            { "\ue015", 55 },  // ry
            { "w", 56 },
            { "y", 57 }
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
        private static readonly Dictionary<string, string> PhonemeToPUA = new()
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
            { "ch", "\ue00a" },  // ち、ちゃ、ちゅ、ちょ (maps to same as ty in model)
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

            for (var i = 0; i < openJTalkPhonemes.Length; i++)
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
                    var isChiSound = true;

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