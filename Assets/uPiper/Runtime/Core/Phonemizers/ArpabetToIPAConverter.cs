using System.Collections.Generic;

namespace uPiper.Core.Phonemizers
{
    /// <summary>
    /// Converts Arpabet (CMU) phonemes to IPA phonemes compatible with
    /// the multilingual Piper model's phoneme_id_map.
    /// Diphthongs are expanded to individual IPA characters.
    /// Multi-char consonants (CH, JH) are mapped to PUA characters via PuaTokenMapper.
    /// </summary>
    public static class ArpabetToIPAConverter
    {
        /// <summary>
        /// Maps ARPABET phonemes to one or more IPA phonemes.
        /// Diphthongs expand to multiple entries (e.g., AW → ["a", "ʊ"]).
        /// </summary>
        private static readonly Dictionary<string, string[]> ArpabetToIPA = new()
        {
            // Monophthong vowels (single IPA char)
            ["AA"] = new[] { "ɑ" },    // father
            ["AE"] = new[] { "æ" },    // cat
            ["AH"] = new[] { "ʌ" },    // cup
            ["AO"] = new[] { "ɔ" },    // caught
            ["EH"] = new[] { "ɛ" },    // bet
            ["ER"] = new[] { "ɚ" },    // bird
            ["IH"] = new[] { "ɪ" },    // bit
            ["IY"] = new[] { "i" },    // beat
            ["UH"] = new[] { "ʊ" },    // book
            ["UW"] = new[] { "u" },    // boot

            // Diphthongs (expanded to individual IPA chars for phoneme_id_map)
            ["AW"] = new[] { "a", "ʊ" },   // cow
            ["AY"] = new[] { "a", "ɪ" },   // bite
            ["EY"] = new[] { "e", "ɪ" },   // bait
            ["OW"] = new[] { "o", "ʊ" },   // boat
            ["OY"] = new[] { "ɔ", "ɪ" },   // boy

            // Consonants (single IPA char)
            ["B"] = new[] { "b" },
            ["D"] = new[] { "d" },
            ["DH"] = new[] { "ð" },    // this
            ["F"] = new[] { "f" },
            ["G"] = new[] { "ɡ" },
            ["HH"] = new[] { "h" },
            ["K"] = new[] { "k" },
            ["L"] = new[] { "l" },
            ["M"] = new[] { "m" },
            ["N"] = new[] { "n" },
            ["NG"] = new[] { "ŋ" },    // sing
            ["P"] = new[] { "p" },
            ["R"] = new[] { "ɹ" },
            ["S"] = new[] { "s" },
            ["SH"] = new[] { "ʃ" },    // ship
            ["T"] = new[] { "t" },
            ["TH"] = new[] { "θ" },    // think
            ["V"] = new[] { "v" },
            ["W"] = new[] { "w" },
            ["Y"] = new[] { "j" },
            ["Z"] = new[] { "z" },
            ["ZH"] = new[] { "ʒ" },    // vision

            // Multi-char affricates → PUA (matched via PuaTokenMapper in PhonemeEncoder)
            ["CH"] = new[] { "t", "ʃ" },   // church → t + ʃ
            ["JH"] = new[] { "d", "ʒ" },   // judge → d + ʒ

            // Pause/silence
            ["PAU"] = new[] { "_" },
            ["SIL"] = new[] { "_" },
            ["SP"] = new[] { "_" },
        };

        /// <summary>
        /// Convert a single Arpabet phoneme to one or more IPA phonemes.
        /// </summary>
        public static string[] ConvertToArray(string arpabetPhoneme)
        {
            if (string.IsNullOrEmpty(arpabetPhoneme))
                return System.Array.Empty<string>();

            var basePhoneme = arpabetPhoneme.TrimEnd('0', '1', '2');

            if (ArpabetToIPA.TryGetValue(basePhoneme.ToUpper(), out var ipa))
                return ipa;

            return new[] { arpabetPhoneme.ToLower() };
        }

        /// <summary>
        /// Convert Arpabet phoneme to IPA (returns first element for backward compatibility).
        /// </summary>
        public static string Convert(string arpabetPhoneme)
        {
            var result = ConvertToArray(arpabetPhoneme);
            return result.Length > 0 ? string.Join("", result) : "";
        }

        /// <summary>
        /// Convert array of Arpabet phonemes to IPA, expanding diphthongs
        /// into individual phonemes for the multilingual model.
        /// </summary>
        public static string[] ConvertAll(string[] arpabetPhonemes)
        {
            var result = new List<string>();
            foreach (var phoneme in arpabetPhonemes)
            {
                var converted = ConvertToArray(phoneme);
                result.AddRange(converted);
            }
            return result.ToArray();
        }
    }
}