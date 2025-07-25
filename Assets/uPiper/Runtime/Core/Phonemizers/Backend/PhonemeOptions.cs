using System.Collections.Generic;

namespace uPiper.Core.Phonemizers.Backend
{
    /// <summary>
    /// Options for phonemization requests.
    /// </summary>
    public class PhonemeOptions
    {
        /// <summary>
        /// Output format preference.
        /// </summary>
        public PhonemeFormat Format { get; set; } = PhonemeFormat.IPA;

        /// <summary>
        /// Include stress markers in the output.
        /// </summary>
        public bool IncludeStress { get; set; } = false;

        /// <summary>
        /// Include syllable boundaries.
        /// </summary>
        public bool IncludeSyllables { get; set; } = false;

        /// <summary>
        /// Include tone information (for tonal languages).
        /// </summary>
        public bool IncludeTones { get; set; } = false;

        /// <summary>
        /// Normalize the text before phonemization.
        /// </summary>
        public bool NormalizeText { get; set; } = true;

        /// <summary>
        /// Handle out-of-vocabulary words using G2P.
        /// </summary>
        public bool UseG2PFallback { get; set; } = true;

        /// <summary>
        /// Custom parameters for specific backends.
        /// </summary>
        public Dictionary<string, object> CustomParams { get; set; }

        /// <summary>
        /// Creates default options.
        /// </summary>
        public static PhonemeOptions Default => new PhonemeOptions();

        /// <summary>
        /// Creates options for detailed phonetic analysis.
        /// </summary>
        public static PhonemeOptions Detailed => new PhonemeOptions
        {
            IncludeStress = true,
            IncludeSyllables = true,
            IncludeTones = true
        };

        /// <summary>
        /// Creates options for simple TTS usage.
        /// </summary>
        public static PhonemeOptions SimpleTTS => new PhonemeOptions
        {
            Format = PhonemeFormat.Piper,
            IncludeStress = false,
            IncludeSyllables = false
        };
    }

    /// <summary>
    /// Phoneme output format.
    /// </summary>
    public enum PhonemeFormat
    {
        /// <summary>
        /// International Phonetic Alphabet.
        /// </summary>
        IPA,

        /// <summary>
        /// ARPABET (CMU style).
        /// </summary>
        ARPABET,

        /// <summary>
        /// X-SAMPA.
        /// </summary>
        XSAMPA,

        /// <summary>
        /// Piper-specific format.
        /// </summary>
        Piper,

        /// <summary>
        /// Backend native format.
        /// </summary>
        Native
    }
}