using System;
using System.Collections.Generic;

namespace uPiper.Core.Phonemizers.Backend.Flite
{
    /// <summary>
    /// Extended Flite LTS rules for Phase 3 implementation
    /// This contains more comprehensive rule patterns for English
    /// </summary>
    public static class FliteLTSExtendedRules
    {
        /// <summary>
        /// Extended LTS rules covering common English patterns
        /// Based on Flite's CMU LTS rule structure
        /// </summary>
        public static readonly WFSTRule[] Rules = new WFSTRule[]
        {
            // Rules for letter 'a' (offset 0)
            // Check right context
            new(5, (byte)'r', 2, 1),   // R1='r' -> check more
            new(255, 10, 255, 0),      // Default 'a' -> 'ae1'
            // 'ar' patterns
            new(6, (byte)'e', 4, 3),   // R2='e' -> 'are'
            new(255, 2, 255, 0),       // 'ar' -> 'aa1'
            new(255, 61, 255, 0),      // 'are' -> 'er0'
            
            // Check for 'ai'
            new(5, (byte)'i', 6, 5),   // R1='i'
            new(255, 10, 255, 0),      // Default
            new(255, 3, 255, 0),       // 'ai' -> 'ey1'
            
            // Check for 'au'
            new(5, (byte)'u', 9, 8),   // R1='u'
            new(255, 10, 255, 0),      // Default
            new(255, 4, 255, 0),       // 'au' -> 'aw1'
            
            // Check for 'aw'
            new(5, (byte)'w', 12, 11), // R1='w'
            new(255, 10, 255, 0),      // Default
            new(255, 6, 255, 0),       // 'aw' -> 'ao1'
            
            // Check for 'ay'
            new(5, (byte)'y', 15, 14), // R1='y'
            new(255, 10, 255, 0),      // Default
            new(255, 22, 255, 0),      // 'ay' -> 'ay1'
            
            // Check for final 'a'
            new(5, (byte)'#', 18, 17), // R1='#' (end)
            new(255, 10, 255, 0),      // Default
            new(255, 23, 255, 0),      // Final 'a' -> 'ah0'
            
            // Rules for letter 'b' (offset 19)
            new(255, 25, 255, 0),      // 'b' -> 'b'
            
            // Rules for letter 'c' (offset 20)
            // Check for 'ch'
            new(5, (byte)'h', 22, 21), // R1='h'
            // Check for 'ce', 'ci', 'cy' (soft c)
            new(5, (byte)'e', 24, 23), // R1='e'
            new(255, 26, 255, 0),      // 'ch' -> 'ch'
            new(5, (byte)'i', 24, 25), // R1='i'
            new(255, 28, 255, 0),      // Soft 'c' -> 's'
            new(5, (byte)'y', 24, 26), // R1='y'
            new(255, 27, 255, 0),      // Hard 'c' -> 'k'
            
            // Rules for letter 'd' (offset 27)
            new(255, 31, 255, 0),      // 'd' -> 'd'
            
            // Rules for letter 'e' (offset 28)
            // Check for silent final 'e'
            new(5, (byte)'#', 30, 29), // R1='#'
            new(255, 1, 255, 0),       // 'e' -> 'eh1'
            new(255, 0, 255, 0),       // Silent 'e' -> epsilon
            
            // Check for 'ea'
            new(5, (byte)'a', 33, 32), // R1='a'
            new(255, 1, 255, 0),       // Default
            new(255, 34, 255, 0),      // 'ea' -> 'iy1'
            
            // Check for 'ee'
            new(5, (byte)'e', 36, 35), // R1='e'
            new(255, 1, 255, 0),       // Default
            new(255, 34, 255, 0),      // 'ee' -> 'iy1'
            
            // Check for 'ei'
            new(5, (byte)'i', 39, 38), // R1='i'
            new(255, 1, 255, 0),       // Default
            new(255, 3, 255, 0),       // 'ei' -> 'ey1'
            
            // Check for 'ew'
            new(5, (byte)'w', 42, 41), // R1='w'
            new(255, 1, 255, 0),       // Default
            new(255, 36, 255, 0),      // 'ew' -> 'uw1'
            
            // Check for 'er'
            new(5, (byte)'r', 45, 44), // R1='r'
            new(255, 1, 255, 0),       // Default
            new(255, 60, 255, 0),      // 'er' -> 'er1'
            
            // Rules for letter 'f' (offset 47)
            new(255, 42, 255, 0),      // 'f' -> 'f'
            
            // Rules for letter 'g' (offset 48)
            // Check for soft 'g' before e, i, y
            new(5, (byte)'e', 50, 49), // R1='e'
            new(255, 43, 255, 0),      // Hard 'g' -> 'g'
            new(255, 33, 255, 0),      // Soft 'g' -> 'jh'
            new(5, (byte)'i', 50, 52), // R1='i'
            new(5, (byte)'y', 50, 53), // R1='y'
            new(255, 43, 255, 0),      // Default 'g' -> 'g'
            
            // Rules for letter 'h' (offset 54)
            new(255, 45, 255, 0),      // 'h' -> 'hh'
            
            // Rules for letter 'i' (offset 55)
            // Check for 'ing' suffix
            new(5, (byte)'n', 57, 56), // R1='n'
            new(255, 11, 255, 0),      // 'i' -> 'ih1'
            new(6, (byte)'g', 59, 58), // R2='g'
            new(255, 11, 255, 0),      // Default
            new(7, (byte)'#', 61, 60), // R3='#'
            new(255, 11, 255, 0),      // Default
            new(255, 17, 255, 0),      // 'ing' -> 'ih0'
            
            // Check for 'ie'
            new(5, (byte)'e', 64, 63), // R1='e'
            new(255, 11, 255, 0),      // Default
            new(255, 34, 255, 0),      // 'ie' -> 'iy1'
            
            // Check for 'igh'
            new(5, (byte)'g', 67, 66), // R1='g'
            new(255, 11, 255, 0),      // Default
            new(6, (byte)'h', 69, 68), // R2='h'
            new(255, 11, 255, 0),      // Default
            new(255, 7, 255, 0),       // 'igh' -> 'ay0'
            
            // Rules for letter 'j' (offset 70)
            new(255, 33, 255, 0),      // 'j' -> 'jh'
            
            // Rules for letter 'k' (offset 71)
            // Check for silent 'k' before 'n'
            new(5, (byte)'n', 73, 72), // R1='n'
            new(255, 27, 255, 0),      // 'k' -> 'k'
            new(255, 0, 255, 0),       // Silent 'k' -> epsilon
            
            // Rules for letter 'l' (offset 74)
            new(255, 47, 255, 0),      // 'l' -> 'l'
            
            // Rules for letter 'm' (offset 75)
            new(255, 49, 255, 0),      // 'm' -> 'm'
            
            // Rules for letter 'n' (offset 76)
            // Check for 'ng'
            new(5, (byte)'g', 78, 77), // R1='g'
            new(255, 54, 255, 0),      // 'n' -> 'n'
            new(255, 53, 255, 0),      // 'ng' -> 'ng'
            
            // Rules for letter 'o' (offset 79)
            // Check for 'oo'
            new(5, (byte)'o', 81, 80), // R1='o'
            new(255, 6, 255, 0),       // 'o' -> 'ao1'
            new(255, 36, 255, 0),      // 'oo' -> 'uw1'
            
            // Check for 'ou'
            new(5, (byte)'u', 84, 83), // R1='u'
            new(255, 6, 255, 0),       // Default
            new(6, (byte)'g', 86, 85), // R2='g'
            new(255, 4, 255, 0),       // 'ou' -> 'aw1'
            new(6, (byte)'h', 86, 87), // R2='h'
            new(255, 23, 255, 0),      // 'ough' -> 'ah0'
            
            // Check for 'ow'
            new(5, (byte)'w', 90, 89), // R1='w'
            new(255, 6, 255, 0),       // Default
            new(255, 15, 255, 0),      // 'ow' -> 'ow1'
            
            // Check for 'oy'
            new(5, (byte)'y', 93, 92), // R1='y'
            new(255, 6, 255, 0),       // Default
            new(255, 38, 255, 0),      // 'oy' -> 'oy1'
            
            // Rules for letter 'p' (offset 94)
            // Check for 'ph'
            new(5, (byte)'h', 96, 95), // R1='h'
            new(255, 61, 255, 0),      // 'p' -> 'p'
            new(255, 42, 255, 0),      // 'ph' -> 'f'
            
            // Rules for letter 'q' (offset 97)
            // 'q' is always followed by 'u'
            new(5, (byte)'u', 99, 98), // R1='u'
            new(255, 27, 255, 0),      // Fallback 'q' -> 'k'
            new(255, 27, 255, 0),      // 'qu' -> 'k' (w handled by 'u')
            
            // Rules for letter 'r' (offset 100)
            new(255, 62, 255, 0),      // 'r' -> 'r'
            
            // Rules for letter 's' (offset 101)
            // Check for 'sh'
            new(5, (byte)'h', 103, 102), // R1='h'
            new(255, 28, 255, 0),      // 's' -> 's'
            new(255, 30, 255, 0),      // 'sh' -> 'sh'
            
            // Rules for letter 't' (offset 104)
            // Check for 'th'
            new(5, (byte)'h', 106, 105), // R1='h'
            new(255, 32, 255, 0),      // 't' -> 't'
            // Check if voiced or unvoiced 'th'
            new(4, (byte)'e', 108, 107), // L1='e' (often voiced)
            new(255, 65, 255, 0),      // 'th' -> 'th' (unvoiced)
            new(255, 66, 255, 0),      // 'th' -> 'dh' (voiced)
            
            // Check for 'tion'
            new(5, (byte)'i', 111, 110), // R1='i'
            new(255, 32, 255, 0),      // Default
            new(6, (byte)'o', 113, 112), // R2='o'
            new(255, 32, 255, 0),      // Default
            new(7, (byte)'n', 115, 114), // R3='n'
            new(255, 32, 255, 0),      // Default
            new(255, 30, 255, 0),      // 'tion' -> 'sh'
            
            // Rules for letter 'u' (offset 116)
            new(255, 23, 255, 0),      // 'u' -> 'ah1'
            
            // Rules for letter 'v' (offset 117)
            new(255, 71, 255, 0),      // 'v' -> 'v'
            
            // Rules for letter 'w' (offset 118)
            // Check for 'wh'
            new(5, (byte)'h', 120, 119), // R1='h'
            new(255, 58, 255, 0),      // 'w' -> 'w'
            new(255, 58, 255, 0),      // 'wh' -> 'w' (h is silent)
            
            // Rules for letter 'x' (offset 121)
            new(255, 72, 255, 0),      // 'x' -> 'k-s'
            
            // Rules for letter 'y' (offset 122)
            // Check if consonant or vowel usage
            new(4, (byte)'#', 124, 123), // L1='#' (start)
            new(255, 11, 255, 0),      // Vowel 'y' -> 'ih1'
            new(255, 46, 255, 0),      // Consonant 'y' -> 'y'
            
            // Rules for letter 'z' (offset 125)
            new(255, 64, 255, 0),      // 'z' -> 'z'
        };

        /// <summary>
        /// Letter to rule offset mapping
        /// </summary>
        public static readonly Dictionary<char, int> LetterOffsets = new()
        {
            { 'a', 0 },
            { 'b', 19 },
            { 'c', 20 },
            { 'd', 27 },
            { 'e', 28 },
            { 'f', 47 },
            { 'g', 48 },
            { 'h', 54 },
            { 'i', 55 },
            { 'j', 70 },
            { 'k', 71 },
            { 'l', 74 },
            { 'm', 75 },
            { 'n', 76 },
            { 'o', 79 },
            { 'p', 94 },
            { 'q', 97 },
            { 'r', 100 },
            { 's', 101 },
            { 't', 104 },
            { 'u', 116 },
            { 'v', 117 },
            { 'w', 118 },
            { 'x', 121 },
            { 'y', 122 },
            { 'z', 125 }
        };

        /// <summary>
        /// Get rule offset for a letter
        /// </summary>
        public static int GetLetterRuleOffset(char letter)
        {
            letter = char.ToLower(letter);
            return LetterOffsets.TryGetValue(letter, out var offset) ? offset : -1;
        }
    }
}