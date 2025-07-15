using System;

namespace uPiper.Core.Phonemizers
{
    /// <summary>
    /// Represents the result of text-to-phoneme conversion.
    /// </summary>
    [Serializable]
    public class PhonemeResult
    {
        /// <summary>
        /// Gets or sets the original input text.
        /// </summary>
        public string OriginalText { get; set; }

        /// <summary>
        /// Gets or sets the array of phoneme symbols.
        /// </summary>
        public string[] Phonemes { get; set; }

        /// <summary>
        /// Gets or sets the array of phoneme IDs for model input.
        /// </summary>
        public int[] PhonemeIds { get; set; }

        /// <summary>
        /// Gets or sets the duration for each phoneme in seconds.
        /// </summary>
        public float[] Durations { get; set; }

        /// <summary>
        /// Gets or sets the pitch values for each phoneme.
        /// </summary>
        public float[] Pitches { get; set; }

        /// <summary>
        /// Gets or sets the language code used for phonemization.
        /// </summary>
        public string Language { get; set; }

        /// <summary>
        /// Gets or sets the time taken to process the phonemization.
        /// </summary>
        public TimeSpan ProcessingTime { get; set; }

        /// <summary>
        /// Gets or sets whether this result was retrieved from cache.
        /// </summary>
        public bool FromCache { get; set; }

        /// <summary>
        /// Gets or sets additional metadata about the phonemization.
        /// </summary>
        public string Metadata { get; set; }

        /// <summary>
        /// Creates a new instance of PhonemeResult.
        /// </summary>
        public PhonemeResult()
        {
            Phonemes = Array.Empty<string>();
            PhonemeIds = Array.Empty<int>();
            Durations = Array.Empty<float>();
            Pitches = Array.Empty<float>();
        }

        /// <summary>
        /// Creates a copy of this PhonemeResult.
        /// </summary>
        /// <returns>A new PhonemeResult instance with copied values.</returns>
        public PhonemeResult Clone()
        {
            return new PhonemeResult
            {
                OriginalText = OriginalText,
                Phonemes = (string[])Phonemes?.Clone(),
                PhonemeIds = (int[])PhonemeIds?.Clone(),
                Durations = (float[])Durations?.Clone(),
                Pitches = (float[])Pitches?.Clone(),
                Language = Language,
                ProcessingTime = ProcessingTime,
                FromCache = FromCache,
                Metadata = Metadata
            };
        }

        /// <summary>
        /// Returns a string representation of the phoneme result.
        /// </summary>
        /// <returns>String representation.</returns>
        public override string ToString()
        {
            var phonemeString = Phonemes != null ? string.Join(" ", Phonemes) : "null";
            return $"PhonemeResult: \"{OriginalText}\" -> [{phonemeString}] ({Language}, {ProcessingTime.TotalMilliseconds:F1}ms{(FromCache ? ", cached" : "")})";
        }
    }
}