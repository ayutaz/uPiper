using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace uPiper.Core.Phonemizers.Backend.Korean
{
    /// <summary>
    /// Korean Grapheme-to-Phoneme (G2P) engine.
    /// Implements Korean phonological rules including sound changes.
    /// </summary>
    public class KoreanG2P
    {
        private readonly HangulProcessor hangulProcessor;
        private readonly Dictionary<string, string> initialPhonemeMap;
        private readonly Dictionary<string, string> medialPhonemeMap;
        private readonly Dictionary<string, string> finalPhonemeMap;
        
        public KoreanG2P()
        {
            hangulProcessor = new HangulProcessor();
            InitializePhonemeMapping();
        }
        
        /// <summary>
        /// Convert Jamo to IPA phonemes with context-dependent rules
        /// </summary>
        public List<string> JamoToPhonemes(HangulJamo jamo, char? prevChar, char? nextChar, bool isInitial)
        {
            var phonemes = new List<string>();
            
            // Get next syllable's initial if exists
            HangulJamo? nextJamo = null;
            if (nextChar.HasValue && hangulProcessor.IsHangul(nextChar.Value))
            {
                nextJamo = hangulProcessor.DecomposeHangul(nextChar.Value);
            }
            
            // Process initial consonant
            string initialPhoneme = GetInitialPhoneme(jamo.Initial, isInitial);
            if (!string.IsNullOrEmpty(initialPhoneme))
            {
                phonemes.Add(initialPhoneme);
            }
            
            // Process medial (vowel)
            var medialPhonemes = GetMedialPhonemes(jamo.Medial);
            phonemes.AddRange(medialPhonemes);
            
            // Process final consonant with liaison and assimilation rules
            if (jamo.HasFinal)
            {
                var finalPhonemes = GetFinalPhonemes(jamo.Final, nextJamo);
                phonemes.AddRange(finalPhonemes);
            }
            
            return phonemes;
        }
        
        private string GetInitialPhoneme(string initial, bool isWordInitial)
        {
            // Apply aspiration and tensification rules
            switch (initial)
            {
                case "":  // ㅇ as initial (no sound)
                    return "";
                    
                case "g":  // ㄱ
                    return isWordInitial ? "k" : "g";
                    
                case "d":  // ㄷ
                    return isWordInitial ? "t" : "d";
                    
                case "b":  // ㅂ
                    return isWordInitial ? "p" : "b";
                    
                case "j":  // ㅈ
                    return isWordInitial ? "tɕ" : "dʑ";
                    
                default:
                    return initialPhonemeMap.ContainsKey(initial) ? 
                           initialPhonemeMap[initial] : initial;
            }
        }
        
        private List<string> GetMedialPhonemes(string medial)
        {
            var phonemes = new List<string>();
            
            // Handle diphthongs
            switch (medial)
            {
                case "ya":   // ㅑ
                    phonemes.Add("j");
                    phonemes.Add("a");
                    break;
                    
                case "yeo":  // ㅕ
                    phonemes.Add("j");
                    phonemes.Add("ʌ");
                    break;
                    
                case "yo":   // ㅛ
                    phonemes.Add("j");
                    phonemes.Add("o");
                    break;
                    
                case "yu":   // ㅠ
                    phonemes.Add("j");
                    phonemes.Add("u");
                    break;
                    
                case "ye":   // ㅖ
                    phonemes.Add("j");
                    phonemes.Add("e");
                    break;
                    
                case "yae":  // ㅒ
                    phonemes.Add("j");
                    phonemes.Add("ɛ");
                    break;
                    
                case "wa":   // ㅘ
                    phonemes.Add("w");
                    phonemes.Add("a");
                    break;
                    
                case "wo":   // ㅝ
                    phonemes.Add("w");
                    phonemes.Add("ʌ");
                    break;
                    
                case "wae":  // ㅙ
                    phonemes.Add("w");
                    phonemes.Add("ɛ");
                    break;
                    
                case "we":   // ㅞ
                    phonemes.Add("w");
                    phonemes.Add("e");
                    break;
                    
                case "wi":   // ㅟ
                    phonemes.Add("w");
                    phonemes.Add("i");
                    break;
                    
                case "oe":   // ㅚ (can be [we] or [ø])
                    phonemes.Add("w");
                    phonemes.Add("e");
                    break;
                    
                case "ui":   // ㅢ
                    phonemes.Add("ɰ");
                    phonemes.Add("i");
                    break;
                    
                default:
                    // Simple vowels
                    if (medialPhonemeMap.ContainsKey(medial))
                    {
                        phonemes.Add(medialPhonemeMap[medial]);
                    }
                    else
                    {
                        phonemes.Add(medial);
                    }
                    break;
            }
            
            return phonemes;
        }
        
        private List<string> GetFinalPhonemes(string final, HangulJamo? nextJamo)
        {
            var phonemes = new List<string>();
            
            // Handle liaison (연음)
            if (nextJamo != null && string.IsNullOrEmpty(nextJamo.Value.Initial))
            {
                // Final consonant moves to next syllable's initial position
                switch (final)
                {
                    case "g":   // ㄱ
                    case "kk":  // ㄲ
                    case "gs":  // ㄳ
                        phonemes.Add("g");
                        break;
                        
                    case "n":   // ㄴ
                    case "nj":  // ㄵ
                    case "nh":  // ㄶ
                        phonemes.Add("n");
                        break;
                        
                    case "d":   // ㄷ
                    case "s":   // ㅅ
                    case "ss":  // ㅆ
                    case "j":   // ㅈ
                    case "ch":  // ㅊ
                    case "t":   // ㅌ
                    case "h":   // ㅎ
                        phonemes.Add("d");
                        break;
                        
                    case "l":   // ㄹ
                    case "lg":  // ㄺ
                    case "lm":  // ㄻ
                    case "lb":  // ㄼ
                    case "ls":  // ㄽ
                    case "lt":  // ㄾ
                    case "lp":  // ㄿ
                    case "lh":  // ㅀ
                        phonemes.Add("ɾ");
                        break;
                        
                    case "m":   // ㅁ
                        phonemes.Add("m");
                        break;
                        
                    case "b":   // ㅂ
                    case "bs":  // ㅄ
                    case "p":   // ㅍ
                        phonemes.Add("b");
                        break;
                        
                    case "ng":  // ㅇ
                        phonemes.Add("ŋ");
                        break;
                        
                    default:
                        phonemes.Add(final);
                        break;
                }
            }
            else
            {
                // Apply neutralization rules for syllable-final position
                switch (final)
                {
                    case "g":   // ㄱ
                    case "kk":  // ㄲ
                    case "k":   // ㅋ
                    case "gs":  // ㄳ
                        phonemes.Add("k̚");  // Unreleased k
                        break;
                        
                    case "n":   // ㄴ
                        phonemes.Add("n");
                        break;
                        
                    case "d":   // ㄷ
                    case "s":   // ㅅ
                    case "ss":  // ㅆ
                    case "j":   // ㅈ
                    case "ch":  // ㅊ
                    case "t":   // ㅌ
                    case "h":   // ㅎ
                        phonemes.Add("t̚");  // Unreleased t
                        break;
                        
                    case "l":   // ㄹ
                        phonemes.Add("l");
                        break;
                        
                    case "m":   // ㅁ
                        phonemes.Add("m");
                        break;
                        
                    case "b":   // ㅂ
                    case "p":   // ㅍ
                        phonemes.Add("p̚");  // Unreleased p
                        break;
                        
                    case "ng":  // ㅇ
                        phonemes.Add("ŋ");
                        break;
                        
                    // Consonant clusters
                    case "nj":  // ㄵ
                    case "nh":  // ㄶ
                        phonemes.Add("n");
                        break;
                        
                    case "lg":  // ㄺ
                    case "lm":  // ㄻ
                    case "lb":  // ㄼ
                    case "ls":  // ㄽ
                    case "lt":  // ㄾ
                    case "lp":  // ㄿ
                    case "lh":  // ㅀ
                        phonemes.Add("l");
                        break;
                        
                    case "bs":  // ㅄ
                        phonemes.Add("p̚");
                        break;
                        
                    default:
                        if (finalPhonemeMap.ContainsKey(final))
                        {
                            phonemes.Add(finalPhonemeMap[final]);
                        }
                        else
                        {
                            phonemes.Add(final);
                        }
                        break;
                }
            }
            
            return phonemes;
        }
        
        private void InitializePhonemeMapping()
        {
            // Initial consonant mapping
            initialPhonemeMap = new Dictionary<string, string>
            {
                ["g"] = "g",      // ㄱ
                ["kk"] = "k͈",    // ㄲ (tense)
                ["n"] = "n",      // ㄴ
                ["d"] = "d",      // ㄷ
                ["tt"] = "t͈",    // ㄸ (tense)
                ["r"] = "ɾ",      // ㄹ
                ["m"] = "m",      // ㅁ
                ["b"] = "b",      // ㅂ
                ["pp"] = "p͈",    // ㅃ (tense)
                ["s"] = "s",      // ㅅ
                ["ss"] = "s͈",    // ㅆ (tense)
                [""] = "",        // ㅇ (no sound)
                ["j"] = "dʑ",     // ㅈ
                ["jj"] = "t͈ɕ",   // ㅉ (tense)
                ["ch"] = "tɕʰ",   // ㅊ (aspirated)
                ["k"] = "kʰ",     // ㅋ (aspirated)
                ["t"] = "tʰ",     // ㅌ (aspirated)
                ["p"] = "pʰ",     // ㅍ (aspirated)
                ["h"] = "h"       // ㅎ
            };
            
            // Medial (vowel) mapping
            medialPhonemeMap = new Dictionary<string, string>
            {
                ["a"] = "a",      // ㅏ
                ["ae"] = "ɛ",     // ㅐ
                ["eo"] = "ʌ",     // ㅓ
                ["e"] = "e",      // ㅔ
                ["o"] = "o",      // ㅗ
                ["u"] = "u",      // ㅜ
                ["eu"] = "ɯ",     // ㅡ
                ["i"] = "i"       // ㅣ
            };
            
            // Final consonant mapping (basic)
            finalPhonemeMap = new Dictionary<string, string>
            {
                ["g"] = "k̚",
                ["n"] = "n",
                ["d"] = "t̚",
                ["l"] = "l",
                ["m"] = "m",
                ["b"] = "p̚",
                ["ng"] = "ŋ"
            };
        }
    }
}