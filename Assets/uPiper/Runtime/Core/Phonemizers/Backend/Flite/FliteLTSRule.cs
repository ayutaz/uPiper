using System;

namespace uPiper.Core.Phonemizers.Backend.Flite
{
    /// <summary>
    /// Flite LTS rule structure representing a single WFST transition
    /// Based on cst_lts_rule from Flite's cst_lts.h
    /// </summary>
    [Serializable]
    public struct WFSTRule
    {
        /// <summary>
        /// Feature to test (index into feature array)
        /// </summary>
        public byte Feature;

        /// <summary>
        /// Value to compare against
        /// </summary>
        public byte Value;

        /// <summary>
        /// Next rule offset if test is true
        /// </summary>
        public ushort NextIfTrue;

        /// <summary>
        /// Next rule offset if test is false
        /// </summary>
        public ushort NextIfFalse;

        /// <summary>
        /// Create a new WFST rule
        /// </summary>
        public WFSTRule(byte feature, byte value, ushort nextIfTrue, ushort nextIfFalse)
        {
            Feature = feature;
            Value = value;
            NextIfTrue = nextIfTrue;
            NextIfFalse = nextIfFalse;
        }

        /// <summary>
        /// Check if this is a terminal rule (produces phoneme)
        /// </summary>
        public readonly bool IsTerminal => NextIfTrue == FliteLTSConstants.CST_LTS_EOR;

        /// <summary>
        /// Get the phoneme index if this is a terminal rule
        /// </summary>
        public int PhonemeIndex => IsTerminal ? Value : -1;
    }

    /// <summary>
    /// Constants used in Flite LTS system
    /// </summary>
    public static class FliteLTSConstants
    {
        /// <summary>
        /// End of rule marker
        /// </summary>
        public const ushort CST_LTS_EOR = 255;

        /// <summary>
        /// Feature index for current letter
        /// </summary>
        public const byte FEAT_CURRENT = 0;

        /// <summary>
        /// Feature indices for context letters (left)
        /// </summary>
        public const byte FEAT_L1 = 1;
        public const byte FEAT_L2 = 2;
        public const byte FEAT_L3 = 3;
        public const byte FEAT_L4 = 4;

        /// <summary>
        /// Feature indices for context letters (right)
        /// </summary>
        public const byte FEAT_R1 = 5;
        public const byte FEAT_R2 = 6;
        public const byte FEAT_R3 = 7;
        public const byte FEAT_R4 = 8;

        /// <summary>
        /// Total number of base features
        /// </summary>
        public const int BASE_FEATURES = 9;
    }
}