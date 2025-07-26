using System;
using UnityEngine;

namespace uPiper.Core.Phonemizers.Backend.Korean
{
    /// <summary>
    /// Processes Hangul characters by decomposing them into Jamo components.
    /// Based on Unicode Hangul syllable composition formula.
    /// </summary>
    public class HangulProcessor
    {
        // Hangul Unicode constants
        private const int HANGUL_BASE = 0xAC00;
        private const int HANGUL_END = 0xD7A3;
        private const int INITIAL_COUNT = 19;
        private const int MEDIAL_COUNT = 21;
        private const int FINAL_COUNT = 28;
        private const int SYLLABLE_COUNT = INITIAL_COUNT * MEDIAL_COUNT * FINAL_COUNT;
        
        // Jamo arrays
        private readonly string[] initials = 
        { 
            "g", "kk", "n", "d", "tt", "r", "m", "b", "pp", 
            "s", "ss", "", "j", "jj", "ch", "k", "t", "p", "h" 
        };
        
        private readonly string[] medials = 
        { 
            "a", "ae", "ya", "yae", "eo", "e", "yeo", "ye", "o", 
            "wa", "wae", "oe", "yo", "u", "wo", "we", "wi", "yu", 
            "eu", "ui", "i" 
        };
        
        private readonly string[] finals = 
        { 
            "", "g", "kk", "gs", "n", "nj", "nh", "d", "l", "lg", 
            "lm", "lb", "ls", "lt", "lp", "lh", "m", "b", "bs", 
            "s", "ss", "ng", "j", "ch", "k", "t", "p", "h" 
        };
        
        /// <summary>
        /// Check if a character is Hangul
        /// </summary>
        public bool IsHangul(char ch)
        {
            return ch >= HANGUL_BASE && ch <= HANGUL_END;
        }
        
        /// <summary>
        /// Decompose a Hangul syllable into its Jamo components
        /// </summary>
        public HangulJamo DecomposeHangul(char syllable)
        {
            if (!IsHangul(syllable))
            {
                throw new ArgumentException($"Character '{syllable}' is not a Hangul syllable");
            }
            
            int syllableIndex = syllable - HANGUL_BASE;
            
            int initialIndex = syllableIndex / (MEDIAL_COUNT * FINAL_COUNT);
            int medialIndex = (syllableIndex % (MEDIAL_COUNT * FINAL_COUNT)) / FINAL_COUNT;
            int finalIndex = syllableIndex % FINAL_COUNT;
            
            return new HangulJamo
            {
                Initial = initials[initialIndex],
                InitialIndex = initialIndex,
                Medial = medials[medialIndex],
                MedialIndex = medialIndex,
                Final = finals[finalIndex],
                FinalIndex = finalIndex,
                HasFinal = finalIndex > 0
            };
        }
        
        /// <summary>
        /// Compose Jamo components back into a Hangul syllable
        /// </summary>
        public char ComposeHangul(int initialIndex, int medialIndex, int finalIndex)
        {
            if (initialIndex < 0 || initialIndex >= INITIAL_COUNT ||
                medialIndex < 0 || medialIndex >= MEDIAL_COUNT ||
                finalIndex < 0 || finalIndex >= FINAL_COUNT)
            {
                throw new ArgumentException("Invalid Jamo indices");
            }
            
            int syllableIndex = initialIndex * MEDIAL_COUNT * FINAL_COUNT +
                               medialIndex * FINAL_COUNT +
                               finalIndex;
            
            return (char)(HANGUL_BASE + syllableIndex);
        }
        
        /// <summary>
        /// Get the romanized form of Jamo
        /// </summary>
        public string GetRomanization(HangulJamo jamo)
        {
            var result = jamo.Initial + jamo.Medial;
            if (jamo.HasFinal)
            {
                result += "-" + jamo.Final;
            }
            return result;
        }
        
        /// <summary>
        /// Check if a Jamo is a consonant cluster
        /// </summary>
        public bool IsConsonantCluster(string jamo)
        {
            return jamo == "gs" || jamo == "nj" || jamo == "nh" || 
                   jamo == "lg" || jamo == "lm" || jamo == "lb" || 
                   jamo == "ls" || jamo == "lt" || jamo == "lp" || 
                   jamo == "lh" || jamo == "bs";
        }
        
        /// <summary>
        /// Check if a medial (vowel) is a diphthong
        /// </summary>
        public bool IsDiphthong(string medial)
        {
            return medial == "wa" || medial == "wae" || medial == "oe" || 
                   medial == "wo" || medial == "we" || medial == "wi" || 
                   medial == "ui" || medial == "ya" || medial == "yae" || 
                   medial == "yeo" || medial == "ye" || medial == "yo" || 
                   medial == "yu";
        }
    }
    
    /// <summary>
    /// Represents decomposed Hangul Jamo
    /// </summary>
    public class HangulJamo
    {
        public string Initial { get; set; }
        public int InitialIndex { get; set; }
        public string Medial { get; set; }
        public int MedialIndex { get; set; }
        public string Final { get; set; }
        public int FinalIndex { get; set; }
        public bool HasFinal { get; set; }
        
        public override string ToString()
        {
            return $"{Initial}+{Medial}" + (HasFinal ? $"+{Final}" : "");
        }
    }
}