using System;
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
        /// Include duration information.
        /// </summary>
        public bool IncludeDurations { get; set; } = false;

        /// <summary>
        /// Include word boundaries.
        /// </summary>
        public bool IncludeWordBoundaries { get; set; } = false;

        /// <summary>
        /// Custom parameters for specific backends.
        /// </summary>
        public Dictionary<string, object> CustomParams { get; set; }

        /// <summary>
        /// Creates default options.
        /// </summary>
        public static PhonemeOptions Default => new();

        /// <summary>
        /// Creates options for detailed phonetic analysis.
        /// </summary>
        public static PhonemeOptions Detailed => new()
        {
            IncludeStress = true,
            IncludeSyllables = true,
            IncludeTones = true
        };

        /// <summary>
        /// Creates options for simple TTS usage.
        /// </summary>
        public static PhonemeOptions SimpleTTS => new()
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

    /// <summary>
    /// Result of phonemization.
    /// </summary>
    public class PhonemeResult
    {
        /// <summary>
        /// Default constructor that initializes arrays.
        /// </summary>
        public PhonemeResult()
        {
            Phonemes = new string[0];
            PhonemeIds = new int[0];
            Durations = new float[0];
            Pitches = new float[0];
            Stresses = new int[0];
            WordBoundaries = new int[0];
            Success = true;
            Metadata = new Dictionary<string, object>();
        }
        /// <summary>
        /// The original input text.
        /// </summary>
        public string OriginalText { get; set; }

        /// <summary>
        /// The phonemes extracted from the text.
        /// </summary>
        public string[] Phonemes { get; set; }

        /// <summary>
        /// The phoneme IDs for model input.
        /// </summary>
        public int[] PhonemeIds { get; set; }

        /// <summary>
        /// The language used for phonemization.
        /// </summary>
        public string Language { get; set; }

        /// <summary>
        /// Whether the phonemization was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if phonemization failed.
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// Error message (alias for Error).
        /// </summary>
        public string ErrorMessage { get => Error; set => Error = value; }

        /// <summary>
        /// Stress markers for each phoneme.
        /// </summary>
        public int[] Stresses { get; set; }

        /// <summary>
        /// Duration in milliseconds for each phoneme.
        /// </summary>
        public float[] Durations { get; set; }

        /// <summary>
        /// Pitch values for each phoneme.
        /// </summary>
        public float[] Pitches { get; set; }

        /// <summary>
        /// Word boundary indices.
        /// </summary>
        public int[] WordBoundaries { get; set; }

        /// <summary>
        /// Backend used for phonemization.
        /// </summary>
        public string Backend { get; set; }

        /// <summary>
        /// Processing time.
        /// </summary>
        public TimeSpan ProcessingTime { get; set; }

        /// <summary>
        /// Processing time in milliseconds.
        /// </summary>
        public float ProcessingTimeMs { get; set; }

        /// <summary>
        /// Whether this result was retrieved from cache.
        /// </summary>
        public bool FromCache { get; set; }

        /// <summary>
        /// Additional metadata about the phonemization.
        /// 
        /// Migration note: Changed from Dictionary&lt;string, string&gt; to Dictionary&lt;string, object&gt;
        /// to support richer metadata types. String values are still supported and will work
        /// as before. For backward compatibility, cast object values to string when needed:
        /// string value = result.Metadata["key"]?.ToString();
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; }

        /// <summary>
        /// Creates a copy of this PhonemeResult.
        /// 
        /// Note: This creates a shallow copy where arrays are shared until modified.
        /// For a deep copy with independent arrays, use DeepClone().
        /// </summary>
        public PhonemeResult Clone()
        {
            return new PhonemeResult
            {
                OriginalText = OriginalText,
                Phonemes = Phonemes, // Shallow copy - arrays are immutable in practice
                PhonemeIds = PhonemeIds,
                Language = Language,
                Success = Success,
                Error = Error,
                Stresses = Stresses,
                Durations = Durations,
                Pitches = Pitches,
                WordBoundaries = WordBoundaries,
                Backend = Backend,
                ProcessingTime = ProcessingTime,
                ProcessingTimeMs = ProcessingTimeMs,
                FromCache = FromCache,
                Metadata = Metadata != null ? new Dictionary<string, object>(Metadata) : null
            };
        }

        /// <summary>
        /// Creates a deep copy of this PhonemeResult with independent arrays.
        /// Use this when you need to modify the arrays after cloning.
        /// </summary>
        public PhonemeResult DeepClone()
        {
            return new PhonemeResult
            {
                OriginalText = OriginalText,
                Phonemes = (string[])Phonemes?.Clone(),
                PhonemeIds = (int[])PhonemeIds?.Clone(),
                Language = Language,
                Success = Success,
                Error = Error,
                Stresses = (int[])Stresses?.Clone(),
                Durations = (float[])Durations?.Clone(),
                Pitches = (float[])Pitches?.Clone(),
                WordBoundaries = (int[])WordBoundaries?.Clone(),
                Backend = Backend,
                ProcessingTime = ProcessingTime,
                ProcessingTimeMs = ProcessingTimeMs,
                FromCache = FromCache,
                Metadata = Metadata != null ? new Dictionary<string, object>(Metadata) : null
            };
        }

        /// <summary>
        /// Returns a string representation of this PhonemeResult.
        /// </summary>
        public override string ToString()
        {
            var phonemeStr = Phonemes == null ? "[null]" :
                            Phonemes.Length == 0 ? "[]" :
                            $"[{string.Join(" ", Phonemes)}]";

            var cacheStr = FromCache ? ", cached" : "";

            return $"PhonemeResult: \"{OriginalText}\" -> {phonemeStr} ({Language}, {ProcessingTime.TotalMilliseconds:F1}ms{cacheStr})";
        }
    }
}