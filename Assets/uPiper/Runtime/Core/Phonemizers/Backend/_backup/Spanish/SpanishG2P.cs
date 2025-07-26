using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace uPiper.Core.Phonemizers.Backend.Spanish
{
    /// <summary>
    /// Spanish Grapheme-to-Phoneme (G2P) engine.
    /// Converts Spanish text to phonemes using rule-based approach.
    /// </summary>
    public class SpanishG2P
    {
        private readonly Dictionary<string, string> digraphs;
        private readonly Dictionary<char, string> basicMappings;
        private readonly HashSet<char> vowels;
        
        public SpanishG2P()
        {
            vowels = new HashSet<char> { 'a', 'e', 'i', 'o', 'u', 'á', 'é', 'í', 'ó', 'ú' };
            
            // Digraphs and special combinations (checked first)
            digraphs = new Dictionary<string, string>
            {
                // Double letters
                ["ll"] = "ʎ",  // or "j" in some dialects
                ["rr"] = "r",  // rolled r
                ["ch"] = "tʃ",
                
                // Letter combinations
                ["qu"] = "k",
                ["gu"] = "g",  // before e,i becomes "g" (guerra)
                ["gü"] = "gw", // (pingüino)
                
                // C combinations
                ["ce"] = "θ",  // Spain Spanish (Latin America uses "s")
                ["ci"] = "θ",  // Spain Spanish
                ["ca"] = "ka",
                ["co"] = "ko",
                ["cu"] = "ku",
                
                // G combinations  
                ["ge"] = "x",  // Spanish j sound
                ["gi"] = "x",
                ["ga"] = "ga",
                ["go"] = "go",
                ["gu"] = "gu"
            };
            
            // Basic letter mappings
            basicMappings = new Dictionary<char, string>
            {
                // Vowels
                ['a'] = "a", ['á'] = "a",
                ['e'] = "e", ['é'] = "e", 
                ['i'] = "i", ['í'] = "i",
                ['o'] = "o", ['ó'] = "o",
                ['u'] = "u", ['ú'] = "u",
                
                // Consonants
                ['b'] = "b",
                ['c'] = "k",  // default, but context matters
                ['d'] = "d",
                ['f'] = "f",
                ['g'] = "g",  // default, but context matters
                ['h'] = "",   // silent
                ['j'] = "x",
                ['k'] = "k",
                ['l'] = "l",
                ['m'] = "m",
                ['n'] = "n",
                ['ñ'] = "ɲ",
                ['p'] = "p",
                ['q'] = "k",
                ['r'] = "ɾ",  // single r (tap)
                ['s'] = "s",
                ['t'] = "t",
                ['v'] = "b",  // same as b in Spanish
                ['w'] = "w",
                ['x'] = "ks",
                ['y'] = "j",  // as consonant
                ['z'] = "θ"   // Spain Spanish (Latin America uses "s")
            };
        }
        
        public string[] Grapheme2Phoneme(string word)
        {
            if (string.IsNullOrEmpty(word))
                return new string[0];
                
            var phonemes = new List<string>();
            word = word.ToLower();
            int i = 0;
            
            while (i < word.Length)
            {
                bool matched = false;
                
                // Check for digraphs and special combinations
                if (i < word.Length - 1)
                {
                    string twoChar = word.Substring(i, 2);
                    
                    // Special handling for specific contexts
                    if (twoChar == "gu" && i < word.Length - 2 && 
                        (word[i + 2] == 'e' || word[i + 2] == 'i'))
                    {
                        // "gue", "gui" -> g (not "gw")
                        phonemes.Add("g");
                        i += 2;
                        matched = true;
                    }
                    else if (digraphs.ContainsKey(twoChar))
                    {
                        phonemes.Add(digraphs[twoChar]);
                        i += 2;
                        matched = true;
                    }
                }
                
                // Check for single character
                if (!matched)
                {
                    char c = word[i];
                    
                    // Context-dependent rules
                    if (c == 'c')
                    {
                        if (i < word.Length - 1 && (word[i + 1] == 'e' || word[i + 1] == 'i'))
                        {
                            phonemes.Add("θ"); // or "s" for Latin America
                        }
                        else
                        {
                            phonemes.Add("k");
                        }
                    }
                    else if (c == 'g')
                    {
                        if (i < word.Length - 1 && (word[i + 1] == 'e' || word[i + 1] == 'i'))
                        {
                            phonemes.Add("x"); // j sound
                        }
                        else
                        {
                            phonemes.Add("g");
                        }
                    }
                    else if (c == 'r')
                    {
                        // Initial r or r after l,n,s is rolled
                        if (i == 0 || (i > 0 && "lns".Contains(word[i - 1])))
                        {
                            phonemes.Add("r"); // rolled
                        }
                        else
                        {
                            phonemes.Add("ɾ"); // tap
                        }
                    }
                    else if (c == 'y')
                    {
                        // Y as vowel at end of word
                        if (i == word.Length - 1 || (i < word.Length - 1 && !vowels.Contains(word[i + 1])))
                        {
                            phonemes.Add("i");
                        }
                        else
                        {
                            phonemes.Add("j");
                        }
                    }
                    else if (c == 'b' || c == 'v')
                    {
                        // Intervocalic b/v becomes fricative β
                        if (i > 0 && i < word.Length - 1 && 
                            vowels.Contains(word[i - 1]) && vowels.Contains(word[i + 1]))
                        {
                            phonemes.Add("β");
                        }
                        else
                        {
                            phonemes.Add("b");
                        }
                    }
                    else if (c == 'd')
                    {
                        // Intervocalic d becomes fricative ð
                        if (i > 0 && i < word.Length - 1 && 
                            vowels.Contains(word[i - 1]) && vowels.Contains(word[i + 1]))
                        {
                            phonemes.Add("ð");
                        }
                        else
                        {
                            phonemes.Add("d");
                        }
                    }
                    else if (c == 'g')
                    {
                        // Intervocalic g becomes fricative ɣ
                        if (i > 0 && i < word.Length - 1 && 
                            vowels.Contains(word[i - 1]) && vowels.Contains(word[i + 1]))
                        {
                            phonemes.Add("ɣ");
                        }
                        else
                        {
                            phonemes.Add("g");
                        }
                    }
                    else if (basicMappings.ContainsKey(c))
                    {
                        string phoneme = basicMappings[c];
                        if (!string.IsNullOrEmpty(phoneme))
                        {
                            phonemes.Add(phoneme);
                        }
                    }
                    else
                    {
                        // Unknown character - keep as is
                        Debug.LogWarning($"Unknown character in Spanish G2P: {c}");
                        phonemes.Add(c.ToString());
                    }
                    
                    i++;
                }
            }
            
            // Apply stress rules (simplified)
            ApplyStressRules(phonemes, word);
            
            return phonemes.ToArray();
        }
        
        private void ApplyStressRules(List<string> phonemes, string word)
        {
            // Spanish stress rules (simplified):
            // 1. Words ending in vowel, -n, or -s: stress on penultimate syllable
            // 2. Words ending in consonant (except -n, -s): stress on last syllable
            // 3. Written accent marks override rules
            
            // This is a simplified implementation
            // Full implementation would need syllable detection
            
            // Check for written accents
            bool hasAccent = word.Any(c => "áéíóú".Contains(c));
            if (hasAccent)
            {
                // Accent already marked in phonemes
                return;
            }
            
            // Add default stress marking (simplified)
            // In a full implementation, we would:
            // 1. Detect syllables
            // 2. Apply stress rules
            // 3. Mark stressed vowel with stress marker
        }
        
        /// <summary>
        /// Set dialect-specific variations
        /// </summary>
        public void SetDialect(string dialect)
        {
            switch (dialect.ToLower())
            {
                case "es-mx":
                case "es-ar":
                case "es-co":
                    // Latin American Spanish
                    // c before e,i -> s (not θ)
                    // z -> s (not θ)
                    digraphs["ce"] = "s";
                    digraphs["ci"] = "s";
                    basicMappings['z'] = "s";
                    
                    // ll -> j (yeísmo)
                    digraphs["ll"] = "j";
                    break;
                    
                case "es-ar":
                    // Argentinian Spanish
                    // ll, y -> ʃ (sh sound)
                    digraphs["ll"] = "ʃ";
                    basicMappings['y'] = "ʃ";
                    break;
                    
                default:
                    // Spain Spanish (default)
                    break;
            }
        }
    }
}