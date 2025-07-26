using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace uPiper.Core.Phonemizers.Backend.Chinese
{
    /// <summary>
    /// Maps Pinyin romanization to IPA phonemes for Mandarin Chinese.
    /// </summary>
    public class PinyinToPhonemeMapper
    {
        private readonly Dictionary<string, string> initialMap;
        private readonly Dictionary<string, string> finalMap;
        private readonly Dictionary<string, string> toneMap;
        
        public PinyinToPhonemeMapper()
        {
            InitializeMappings();
        }
        
        /// <summary>
        /// Convert pinyin syllable to IPA phonemes
        /// </summary>
        public List<string> PinyinToIPA(string pinyin)
        {
            var phonemes = new List<string>();
            
            if (string.IsNullOrEmpty(pinyin))
                return phonemes;
                
            // Extract tone number
            var toneMatch = Regex.Match(pinyin, @"(\d)$");
            string tone = toneMatch.Success ? toneMatch.Groups[1].Value : "5"; // Default to neutral tone
            string syllable = toneMatch.Success ? pinyin.Substring(0, pinyin.Length - 1) : pinyin;
            
            // Parse initial and final
            var (initial, final) = ParsePinyinSyllable(syllable);
            
            // Add initial consonant
            if (!string.IsNullOrEmpty(initial) && initialMap.ContainsKey(initial))
            {
                phonemes.Add(initialMap[initial]);
            }
            
            // Add final (vowel + optional ending)
            if (!string.IsNullOrEmpty(final))
            {
                string mappedFinal = GetMappedFinal(final, string.IsNullOrEmpty(initial));
                if (!string.IsNullOrEmpty(mappedFinal))
                {
                    // Split complex finals into individual phonemes
                    foreach (var phoneme in mappedFinal.Split(' '))
                    {
                        if (!string.IsNullOrEmpty(phoneme))
                        {
                            phonemes.Add(phoneme);
                        }
                    }
                }
            }
            
            // Add tone marker (simplified - in practice, tone affects pitch contour)
            if (toneMap.ContainsKey(tone))
            {
                phonemes.Add(toneMap[tone]);
            }
            
            return phonemes;
        }
        
        private (string initial, string final) ParsePinyinSyllable(string syllable)
        {
            // Handle special cases first
            if (syllable == "er") return ("", "er");
            
            // Try to match longest possible initial
            string[] possibleInitials = { "zh", "ch", "sh", "b", "p", "m", "f", "d", "t", "n", "l", 
                                         "g", "k", "h", "j", "q", "x", "z", "c", "s", "r", "y", "w" };
            
            foreach (var initial in possibleInitials)
            {
                if (syllable.StartsWith(initial))
                {
                    string final = syllable.Substring(initial.Length);
                    return (initial, final);
                }
            }
            
            // No initial consonant
            return ("", syllable);
        }
        
        private string GetMappedFinal(string final, bool noInitial)
        {
            // Handle special transformations
            if (noInitial)
            {
                // When no initial, some finals change
                switch (final)
                {
                    case "i": return "i";
                    case "in": return "i n";
                    case "ing": return "i ŋ";
                    case "u": return "u";
                    case "un": return "u ən"; // Actually "wen"
                    case "uan": return "u a n";
                    case "uang": return "u a ŋ";
                    case "ueng": return "u ə ŋ";
                    case "ong": return "u ŋ";
                    case "yu": return "y";
                    case "yue": return "y e";
                    case "yuan": return "y ɛ n";
                    case "yun": return "y n";
                }
            }
            
            // Standard finals mapping
            if (finalMap.ContainsKey(final))
            {
                return finalMap[final];
            }
            
            // Handle v/ü substitution
            string finalWithU = final.Replace("v", "ü");
            if (finalMap.ContainsKey(finalWithU))
            {
                return finalMap[finalWithU];
            }
            
            Debug.LogWarning($"Unknown pinyin final: {final}");
            return final; // Return as-is if not found
        }
        
        private void InitializeMappings()
        {
            // Initial consonants (声母)
            initialMap = new Dictionary<string, string>
            {
                ["b"] = "p",      // unaspirated
                ["p"] = "pʰ",     // aspirated
                ["m"] = "m",
                ["f"] = "f",
                ["d"] = "t",      // unaspirated
                ["t"] = "tʰ",     // aspirated
                ["n"] = "n",
                ["l"] = "l",
                ["g"] = "k",      // unaspirated
                ["k"] = "kʰ",     // aspirated
                ["h"] = "x",      // velar fricative
                ["j"] = "tɕ",     // alveolo-palatal
                ["q"] = "tɕʰ",    // aspirated alveolo-palatal
                ["x"] = "ɕ",      // alveolo-palatal fricative
                ["zh"] = "ʈʂ",    // retroflex
                ["ch"] = "ʈʂʰ",   // aspirated retroflex
                ["sh"] = "ʂ",     // retroflex fricative
                ["r"] = "ʐ",      // retroflex approximant
                ["z"] = "ts",     // alveolar affricate
                ["c"] = "tsʰ",    // aspirated alveolar affricate
                ["s"] = "s",
                ["y"] = "j",      // palatal approximant
                ["w"] = "w"
            };
            
            // Finals (韵母)
            finalMap = new Dictionary<string, string>
            {
                // Simple vowels
                ["a"] = "a",
                ["o"] = "o",
                ["e"] = "ɤ",
                ["i"] = "i",
                ["u"] = "u",
                ["ü"] = "y",
                ["er"] = "ɚ",
                
                // Complex finals
                ["ai"] = "a i",
                ["ei"] = "e i",
                ["ui"] = "u e i",
                ["ao"] = "a u",
                ["ou"] = "o u",
                ["iu"] = "i o u",
                ["ie"] = "i e",
                ["üe"] = "y e",
                ["ue"] = "u e",
                
                // Nasal finals
                ["an"] = "a n",
                ["en"] = "ə n",
                ["in"] = "i n",
                ["un"] = "u ə n",
                ["ün"] = "y n",
                
                ["ang"] = "a ŋ",
                ["eng"] = "ə ŋ",
                ["ing"] = "i ŋ",
                ["ong"] = "u ŋ",
                
                ["ian"] = "i ɛ n",
                ["uan"] = "u a n",
                ["üan"] = "y ɛ n",
                
                ["iang"] = "i a ŋ",
                ["uang"] = "u a ŋ",
                
                ["iong"] = "i u ŋ",
                ["ueng"] = "u ə ŋ",
                
                // Special cases
                ["ia"] = "i a",
                ["ua"] = "u a",
                ["uo"] = "u o",
                ["uai"] = "u a i",
                ["uei"] = "u e i",
                
                // Syllabic consonants
                ["zi"] = "ɿ",
                ["ci"] = "ɿ",
                ["si"] = "ɿ",
                ["zhi"] = "ʅ",
                ["chi"] = "ʅ",
                ["shi"] = "ʅ",
                ["ri"] = "ʅ"
            };
            
            // Tone markers (simplified - actual implementation would use pitch contours)
            toneMap = new Dictionary<string, string>
            {
                ["1"] = "˥",    // High level (55)
                ["2"] = "˧˥",   // Rising (35)
                ["3"] = "˨˩˦",  // Dipping (214)
                ["4"] = "˥˩",   // Falling (51)
                ["5"] = ""      // Neutral (no tone marker)
            };
        }
    }
}