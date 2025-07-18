using System;
using System.Collections.Generic;
using System.Linq;
using uPiper.Core.Logging;
using uPiper.Core.Phonemizers;

namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// Encodes phonemes to model-compatible ID arrays
    /// </summary>
    public class PhonemeEncoder : IPhonemeEncoder
    {
        private readonly Dictionary<string, int> _phonemeToId;
        private readonly Dictionary<int, string> _idToPhoneme;
        private readonly int _padTokenId;
        private readonly int _unknownTokenId;
        private readonly int _startTokenId;
        private readonly int _endTokenId;

        /// <summary>
        /// Get the phoneme vocabulary size
        /// </summary>
        public int VocabularySize => _phonemeToId.Count;

        /// <summary>
        /// Get the padding token ID
        /// </summary>
        public int PadTokenId => _padTokenId;

        /// <summary>
        /// Get the unknown token ID
        /// </summary>
        public int UnknownTokenId => _unknownTokenId;

        /// <summary>
        /// Create a new phoneme encoder with default Piper phoneme mappings
        /// </summary>
        public PhonemeEncoder() : this(GetDefaultPhonemeMapping())
        {
        }

        /// <summary>
        /// Create a new phoneme encoder with custom phoneme mappings
        /// </summary>
        /// <param name="phonemeToId">Phoneme to ID mapping dictionary</param>
        /// <param name="padTokenId">ID for padding token</param>
        /// <param name="unknownTokenId">ID for unknown phonemes</param>
        /// <param name="startTokenId">ID for start token (optional)</param>
        /// <param name="endTokenId">ID for end token (optional)</param>
        public PhonemeEncoder(
            Dictionary<string, int> phonemeToId,
            int padTokenId = 0,
            int unknownTokenId = 1,
            int startTokenId = -1,
            int endTokenId = -1)
        {
            _phonemeToId = phonemeToId ?? throw new ArgumentNullException(nameof(phonemeToId));
            _padTokenId = padTokenId;
            _unknownTokenId = unknownTokenId;
            _startTokenId = startTokenId;
            _endTokenId = endTokenId;

            // Create reverse mapping
            _idToPhoneme = new Dictionary<int, string>();
            foreach (var kvp in _phonemeToId)
            {
                _idToPhoneme[kvp.Value] = kvp.Key;
            }

            PiperLogger.LogInfo("PhonemeEncoder initialized with {0} phonemes", _phonemeToId.Count);
        }

        /// <summary>
        /// Encode phoneme strings to ID array
        /// </summary>
        public int[] EncodePhonemes(string[] phonemes)
        {
            if (phonemes == null || phonemes.Length == 0)
            {
                return new int[0];
            }

            var ids = new List<int>(phonemes.Length);
            var unknownPhonemes = new HashSet<string>();

            foreach (var phoneme in phonemes)
            {
                if (string.IsNullOrEmpty(phoneme))
                    continue;

                if (_phonemeToId.TryGetValue(phoneme, out var id))
                {
                    ids.Add(id);
                }
                else
                {
                    ids.Add(_unknownTokenId);
                    unknownPhonemes.Add(phoneme);
                }
            }

            if (unknownPhonemes.Count > 0)
            {
                PiperLogger.LogWarning("Unknown phonemes encountered: {0}", 
                    string.Join(", ", unknownPhonemes));
            }

            return ids.ToArray();
        }

        /// <summary>
        /// Encode phoneme result to ID array
        /// </summary>
        public int[] EncodePhonemes(PhonemeResult phonemeResult)
        {
            if (phonemeResult?.Phonemes == null)
            {
                return new int[0];
            }

            // If the phoneme result already has IDs, validate and use them
            if (phonemeResult.PhonemeIds != null && 
                phonemeResult.PhonemeIds.Length == phonemeResult.Phonemes.Length)
            {
                // Validate IDs are within vocabulary range
                var validatedIds = new int[phonemeResult.PhonemeIds.Length];
                for (int i = 0; i < phonemeResult.PhonemeIds.Length; i++)
                {
                    var id = phonemeResult.PhonemeIds[i];
                    if (id >= 0 && id < VocabularySize)
                    {
                        validatedIds[i] = id;
                    }
                    else
                    {
                        // Fall back to encoding the phoneme string
                        if (_phonemeToId.TryGetValue(phonemeResult.Phonemes[i], out var mappedId))
                        {
                            validatedIds[i] = mappedId;
                        }
                        else
                        {
                            validatedIds[i] = _unknownTokenId;
                        }
                    }
                }
                return validatedIds;
            }

            // Otherwise, encode from phoneme strings
            return EncodePhonemes(phonemeResult.Phonemes);
        }

        /// <summary>
        /// Add padding to phoneme ID sequence
        /// </summary>
        public int[] AddPadding(int[] phonemeIds, int targetLength, int padId = -1)
        {
            if (phonemeIds == null)
                throw new ArgumentNullException(nameof(phonemeIds));

            if (targetLength <= 0)
                throw new ArgumentException("Target length must be positive", nameof(targetLength));

            // Use default pad ID if not specified
            if (padId < 0)
                padId = _padTokenId;

            if (phonemeIds.Length >= targetLength)
            {
                // Truncate if too long
                return phonemeIds.Take(targetLength).ToArray();
            }

            // Pad with specified ID
            var padded = new int[targetLength];
            Array.Copy(phonemeIds, padded, phonemeIds.Length);
            for (int i = phonemeIds.Length; i < targetLength; i++)
            {
                padded[i] = padId;
            }

            return padded;
        }

        /// <summary>
        /// Add special tokens (start/end) to sequence
        /// </summary>
        public int[] AddSpecialTokens(int[] phonemeIds, int? startToken = null, int? endToken = null)
        {
            if (phonemeIds == null)
                throw new ArgumentNullException(nameof(phonemeIds));

            var result = new List<int>(phonemeIds.Length + 2);

            // Add start token if specified
            if (startToken.HasValue && startToken.Value >= 0)
            {
                result.Add(startToken.Value);
            }
            else if (_startTokenId >= 0)
            {
                result.Add(_startTokenId);
            }

            // Add phoneme IDs
            result.AddRange(phonemeIds);

            // Add end token if specified
            if (endToken.HasValue && endToken.Value >= 0)
            {
                result.Add(endToken.Value);
            }
            else if (_endTokenId >= 0)
            {
                result.Add(_endTokenId);
            }

            return result.ToArray();
        }

        /// <summary>
        /// Decode ID array back to phoneme strings
        /// </summary>
        public string[] DecodeIds(int[] ids)
        {
            if (ids == null || ids.Length == 0)
                return new string[0];

            var phonemes = new string[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                if (_idToPhoneme.TryGetValue(ids[i], out var phoneme))
                {
                    phonemes[i] = phoneme;
                }
                else
                {
                    phonemes[i] = "<UNK>";
                }
            }

            return phonemes;
        }

        /// <summary>
        /// Get default phoneme mapping for Piper TTS
        /// </summary>
        private static Dictionary<string, int> GetDefaultPhonemeMapping()
        {
            // This is a simplified phoneme set combining common phonemes across languages
            // In production, this should be loaded from the model's vocabulary file
            var mapping = new Dictionary<string, int>
            {
                // Special tokens
                {"<PAD>", 0},
                {"<UNK>", 1},
                {"<BOS>", 2},
                {"<EOS>", 3},
                
                // Silence and pauses
                {"sil", 4},
                {"pau", 5},
                {"sp", 6},
                
                // English phonemes (simplified set)
                {"AA", 10}, {"AE", 11}, {"AH", 12}, {"AO", 13}, {"AW", 14},
                {"AY", 15}, {"EH", 16}, {"ER", 17}, {"EY", 18}, {"IH", 19},
                {"IY", 20}, {"OW", 21}, {"OY", 22}, {"UH", 23}, {"UW", 24},
                {"B", 25}, {"CH", 26}, {"D", 27}, {"DH", 28}, {"F", 29},
                {"G", 30}, {"HH", 31}, {"JH", 32}, {"K", 33}, {"L", 34},
                {"M", 35}, {"N", 36}, {"NG", 37}, {"P", 38}, {"R", 39},
                {"S", 40}, {"SH", 41}, {"T", 42}, {"TH", 43}, {"V", 44},
                {"W", 45}, {"Y", 46}, {"Z", 47}, {"ZH", 48},
                
                // Japanese phonemes (hiragana representation)
                {"a", 50}, {"i", 51}, {"u", 52}, {"e", 53}, {"o", 54},
                {"ka", 55}, {"ki", 56}, {"ku", 57}, {"ke", 58}, {"ko", 59},
                {"ga", 60}, {"gi", 61}, {"gu", 62}, {"ge", 63}, {"go", 64},
                {"sa", 65}, {"shi", 66}, {"su", 67}, {"se", 68}, {"so", 69},
                {"za", 70}, {"ji", 71}, {"zu", 72}, {"ze", 73}, {"zo", 74},
                {"ta", 75}, {"chi", 76}, {"tsu", 77}, {"te", 78}, {"to", 79},
                {"da", 80}, {"de", 81}, {"do", 82},
                {"na", 83}, {"ni", 84}, {"nu", 85}, {"ne", 86}, {"no", 87},
                {"ha", 88}, {"hi", 89}, {"fu", 90}, {"he", 91}, {"ho", 92},
                {"ba", 93}, {"bi", 94}, {"bu", 95}, {"be", 96}, {"bo", 97},
                {"pa", 98}, {"pi", 99}, {"pu", 100}, {"pe", 101}, {"po", 102},
                {"ma", 103}, {"mi", 104}, {"mu", 105}, {"me", 106}, {"mo", 107},
                {"ya", 108}, {"yu", 109}, {"yo", 110},
                {"ra", 111}, {"ri", 112}, {"ru", 113}, {"re", 114}, {"ro", 115},
                {"wa", 116}, {"wo", 117}, {"n", 118},
                
                // Additional symbols
                {".", 120}, {",", 121}, {"?", 122}, {"!", 123},
                {"-", 124}, {"_", 125}
            };

            return mapping;
        }

        /// <summary>
        /// Load phoneme mapping from file
        /// </summary>
        public static Dictionary<string, int> LoadPhonemeMapping(string filePath)
        {
            var mapping = new Dictionary<string, int>();

            try
            {
                var lines = System.IO.File.ReadAllLines(filePath);
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    var parts = line.Split('\t', ' ');
                    if (parts.Length >= 2)
                    {
                        var phoneme = parts[0].Trim();
                        if (int.TryParse(parts[1], out var id))
                        {
                            mapping[phoneme] = id;
                        }
                    }
                }

                PiperLogger.LogInfo("Loaded {0} phonemes from {1}", mapping.Count, filePath);
            }
            catch (Exception ex)
            {
                PiperLogger.LogError("Failed to load phoneme mapping: {0}", ex.Message);
                throw;
            }

            return mapping;
        }

        /// <summary>
        /// Load phoneme mapping from Piper model JSON config
        /// </summary>
        public static Dictionary<string, int> LoadPhonemeMappingFromJson(string jsonPath)
        {
            var mapping = new Dictionary<string, int>();

            try
            {
                var jsonContent = System.IO.File.ReadAllText(jsonPath);
                
                // Find the phoneme_id_map section
                var idMapStart = jsonContent.IndexOf("\"phoneme_id_map\"");
                if (idMapStart > 0)
                {
                    // Find the opening brace
                    var braceStart = jsonContent.IndexOf("{", idMapStart);
                    if (braceStart > 0)
                    {
                        // Find the closing brace (simple approach - count braces)
                        var braceCount = 1;
                        var braceEnd = braceStart + 1;
                        while (braceCount > 0 && braceEnd < jsonContent.Length)
                        {
                            if (jsonContent[braceEnd] == '{') braceCount++;
                            else if (jsonContent[braceEnd] == '}') braceCount--;
                            braceEnd++;
                        }
                        
                        // Extract the phoneme_id_map content
                        var mapContent = jsonContent.Substring(braceStart + 1, braceEnd - braceStart - 2);
                        
                        // Parse each line
                        var lines = mapContent.Split(new[] { '\n', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines)
                        {
                            // Format: "phoneme": [id]
                            var colonIndex = line.IndexOf(':');
                            if (colonIndex > 0)
                            {
                                var phonemePart = line.Substring(0, colonIndex).Trim().Trim('"');
                                var idPart = line.Substring(colonIndex + 1).Trim();
                                
                                // Extract ID from array notation [id]
                                var bracketStart = idPart.IndexOf('[');
                                var bracketEnd = idPart.IndexOf(']');
                                if (bracketStart >= 0 && bracketEnd > bracketStart)
                                {
                                    var idStr = idPart.Substring(bracketStart + 1, bracketEnd - bracketStart - 1).Trim();
                                    if (int.TryParse(idStr, out var id))
                                    {
                                        mapping[phonemePart] = id;
                                    }
                                }
                            }
                        }
                    }
                }

                PiperLogger.LogInfo("Loaded {0} phonemes from JSON config", mapping.Count);
            }
            catch (Exception ex)
            {
                PiperLogger.LogWarning("Failed to load phoneme mapping from JSON: {0}", ex.Message);
                // Return empty mapping instead of throwing
            }

            return mapping;
        }
    }
}