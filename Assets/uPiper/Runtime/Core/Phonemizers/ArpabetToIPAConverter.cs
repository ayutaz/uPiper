using System.Collections.Generic;

namespace uPiper.Core.Phonemizers
{
    /// <summary>
    /// Converts Arpabet (CMU) phonemes to IPA phonemes compatible with
    /// the multilingual Piper model's phoneme_id_map.
    ///
    /// Matches piper-plus EnglishPhonemizer output:
    /// - Stress markers (ˈ, ˌ) emitted as separate tokens before stressed vowels
    /// - Unstressed AH → ə (schwa)
    /// - Long vowels with ː (IY→iː, UW→uː, AO→ɔː)
    /// - Stressed ER → ɜː, Unstressed ER → ɚ
    /// - Diphthongs expanded to individual IPA chars
    /// - Word boundaries as space tokens
    /// </summary>
    public static class ArpabetToIPAConverter
    {
        /// <summary>
        /// Maps ARPABET base phonemes (without stress) to IPA phoneme arrays.
        /// Stress-dependent mappings are handled in ConvertToArray.
        /// </summary>
        private static readonly Dictionary<string, string[]> ArpabetToIPA = new()
        {
            // Vowels - stress-independent
            ["AA"] = new[] { "ɑ" },
            ["AE"] = new[] { "æ" },
            ["AO"] = new[] { "ɔ", "ː" },     // caught → ɔː (piper-plus: AO → ɔː)
            ["EH"] = new[] { "ɛ" },
            ["IH"] = new[] { "ɪ" },
            ["IY"] = new[] { "i", "ː" },      // beat → iː (piper-plus: IY → iː)
            ["UH"] = new[] { "ʊ" },
            ["UW"] = new[] { "u", "ː" },      // boot → uː (piper-plus: UW → uː)

            // Diphthongs (expanded to individual IPA chars)
            ["AW"] = new[] { "a", "ʊ" },
            ["AY"] = new[] { "a", "ɪ" },
            ["EY"] = new[] { "e", "ɪ" },
            ["OW"] = new[] { "o", "ʊ" },
            ["OY"] = new[] { "ɔ", "ɪ" },

            // Consonants
            ["B"] = new[] { "b" },
            ["D"] = new[] { "d" },
            ["DH"] = new[] { "ð" },
            ["F"] = new[] { "f" },
            ["G"] = new[] { "ɡ" },
            ["HH"] = new[] { "h" },
            ["K"] = new[] { "k" },
            ["L"] = new[] { "l" },
            ["M"] = new[] { "m" },
            ["N"] = new[] { "n" },
            ["NG"] = new[] { "ŋ" },
            ["P"] = new[] { "p" },
            ["R"] = new[] { "ɹ" },
            ["S"] = new[] { "s" },
            ["SH"] = new[] { "ʃ" },
            ["T"] = new[] { "t" },
            ["TH"] = new[] { "θ" },
            ["V"] = new[] { "v" },
            ["W"] = new[] { "w" },
            ["Y"] = new[] { "j" },
            ["Z"] = new[] { "z" },
            ["ZH"] = new[] { "ʒ" },

            // Affricates (expanded to components, matching piper-plus)
            ["CH"] = new[] { "t", "ʃ" },
            ["JH"] = new[] { "d", "ʒ" },
        };

        /// <summary>
        /// Convert a single Arpabet phoneme (with optional stress digit) to IPA phonemes.
        /// Handles stress-dependent mappings matching piper-plus EnglishPhonemizer.
        /// </summary>
        public static string[] ConvertToArray(string arpabetPhoneme)
        {
            if (string.IsNullOrEmpty(arpabetPhoneme))
                return System.Array.Empty<string>();

            // Handle pause/silence (case-insensitive)
            var upper = arpabetPhoneme.ToUpper();
            if (upper == "PAU" || upper == "SIL" || upper == "SP")
                return new[] { " " }; // Word boundary space (piper-plus uses space, not "_")

            // Extract stress digit (0=unstressed, 1=primary, 2=secondary)
            int stress = 0;
            var basePhoneme = arpabetPhoneme;
            if (basePhoneme.Length > 1 && char.IsDigit(basePhoneme[^1]))
            {
                stress = basePhoneme[^1] - '0';
                basePhoneme = basePhoneme[..^1];
            }
            var baseUpper = basePhoneme.ToUpper();

            // Stress-dependent special cases (matching piper-plus english.py)
            // AH: unstressed → ə (schwa), stressed → ʌ
            if (baseUpper == "AH")
            {
                var vowel = stress == 0 ? "ə" : "ʌ";
                return stress >= 1
                    ? new[] { stress == 1 ? "ˈ" : "ˌ", vowel }
                    : new[] { vowel };
            }

            // ER: stressed → ɜː, unstressed → ɚ
            if (baseUpper == "ER")
            {
                if (stress >= 1)
                    return new[] { stress == 1 ? "ˈ" : "ˌ", "ɜ", "ː" };
                return new[] { "ɚ" };
            }

            // Standard mapping
            if (ArpabetToIPA.TryGetValue(baseUpper, out var ipa))
            {
                // Add stress marker before vowels if stressed
                if (stress >= 1 && IsVowelPhoneme(baseUpper))
                {
                    var result = new List<string>();
                    result.Add(stress == 1 ? "ˈ" : "ˌ");
                    result.AddRange(ipa);
                    return result.ToArray();
                }
                return ipa;
            }

            return new[] { arpabetPhoneme.ToLower() };
        }

        /// <summary>
        /// Convert Arpabet phoneme to IPA (returns joined string for backward compatibility).
        /// </summary>
        public static string Convert(string arpabetPhoneme)
        {
            var result = ConvertToArray(arpabetPhoneme);
            return result.Length > 0 ? string.Join("", result) : "";
        }

        /// <summary>
        /// Convert array of Arpabet phonemes to IPA, expanding diphthongs
        /// and adding stress markers to match piper-plus output format.
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

        private static bool IsVowelPhoneme(string baseUpper)
        {
            return baseUpper == "AA" || baseUpper == "AE" || baseUpper == "AO" ||
                   baseUpper == "EH" || baseUpper == "IH" || baseUpper == "IY" ||
                   baseUpper == "UH" || baseUpper == "UW" ||
                   baseUpper == "AW" || baseUpper == "AY" || baseUpper == "EY" ||
                   baseUpper == "OW" || baseUpper == "OY";
        }
    }
}