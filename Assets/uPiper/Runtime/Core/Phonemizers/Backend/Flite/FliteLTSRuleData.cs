using System;
using System.Collections.Generic;

namespace uPiper.Core.Phonemizers.Backend.Flite
{
    /// <summary>
    /// Flite LTS rule data for Phase 2 implementation
    /// This contains a simplified rule set for initial testing
    /// </summary>
    public static class FliteLTSRuleData
    {
        /// <summary>
        /// Simplified LTS rules for common English patterns
        /// Format: (feature, value, nextIfTrue, nextIfFalse)
        /// </summary>
        public static readonly WFSTRule[] SimplifiedRules = new WFSTRule[]
        {
            // Rules for letter 'a' at offset 0
            // Check right context for 'r'
            new(FliteLTSConstants.FEAT_R1, (byte)'r', 2, 1),
            // Terminal: produce 'ae1' 
            new(255, 10, FliteLTSConstants.CST_LTS_EOR, 0),
            // Check for 'ar' pattern -> 'aa1 r'
            new(255, 2, FliteLTSConstants.CST_LTS_EOR, 0),
            
            // Rules for letter 'b' at offset 3
            // Simple 'b' -> 'b'
            new(255, 25, FliteLTSConstants.CST_LTS_EOR, 0),
            
            // Rules for letter 'c' at offset 4  
            // Check for 'ch'
            new(FliteLTSConstants.FEAT_R1, (byte)'h', 6, 5),
            // 'c' -> 'k'
            new(255, 27, FliteLTSConstants.CST_LTS_EOR, 0),
            // 'ch' -> 'ch'
            new(255, 26, FliteLTSConstants.CST_LTS_EOR, 0),
            
            // Rules for letter 'd' at offset 7
            // Simple 'd' -> 'd'
            new(255, 31, FliteLTSConstants.CST_LTS_EOR, 0),
            
            // Rules for letter 'e' at offset 8
            // Check for end of word
            new(FliteLTSConstants.FEAT_R1, (byte)'#', 10, 9),
            // 'e' -> 'eh1'
            new(255, 1, FliteLTSConstants.CST_LTS_EOR, 0),
            // Final 'e' is often silent
            new(255, 0, FliteLTSConstants.CST_LTS_EOR, 0),
            
            // Rules for letter 'f' at offset 11
            // Simple 'f' -> 'f'
            new(255, 42, FliteLTSConstants.CST_LTS_EOR, 0),
            
            // Rules for letter 'g' at offset 12
            // Simple 'g' -> 'g'
            new(255, 43, FliteLTSConstants.CST_LTS_EOR, 0),
            
            // Rules for letter 'h' at offset 13
            // Simple 'h' -> 'hh'
            new(255, 45, FliteLTSConstants.CST_LTS_EOR, 0),
            
            // Rules for letter 'i' at offset 14
            // Check for 'ing'
            new(FliteLTSConstants.FEAT_R1, (byte)'n', 16, 15),
            // 'i' -> 'ih1'
            new(255, 11, FliteLTSConstants.CST_LTS_EOR, 0),
            // Check for 'g' after 'n'
            new(FliteLTSConstants.FEAT_R2, (byte)'g', 17, 15),
            // 'ing' -> 'ih0 ng'
            new(255, 17, FliteLTSConstants.CST_LTS_EOR, 0),
            
            // Continue with more rules...
        };

        /// <summary>
        /// Letter to rule offset mapping
        /// Maps each letter to its starting rule index
        /// </summary>
        public static readonly Dictionary<char, int> LetterOffsets = new()
        {
            { 'a', 0 },
            { 'b', 3 },
            { 'c', 4 },
            { 'd', 7 },
            { 'e', 8 },
            { 'f', 11 },
            { 'g', 12 },
            { 'h', 13 },
            { 'i', 14 },
            { 'j', 18 },
            { 'k', 18 },
            { 'l', 18 },
            { 'm', 18 },
            { 'n', 18 },
            { 'o', 18 },
            { 'p', 18 },
            { 'q', 18 },
            { 'r', 18 },
            { 's', 18 },
            { 't', 18 },
            { 'u', 18 },
            { 'v', 18 },
            { 'w', 18 },
            { 'x', 18 },
            { 'y', 18 },
            { 'z', 18 }
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