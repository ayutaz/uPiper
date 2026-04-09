using System.Collections.Generic;

namespace uPiper.Core.Phonemizers.Multilingual
{
    /// <summary>
    /// Represents a language's trigram frequency profile for language detection.
    /// Trigrams are stored as a set of known trigrams for the language.
    /// Similarity is computed using frequency-weighted overlap between input trigrams and the profile.
    /// </summary>
    internal sealed class TrigramProfile
    {
        private readonly Dictionary<string, int> _trigramRanks;

        /// <summary>Language code (e.g., "en", "es", "fr", "pt").</summary>
        public string Language { get; }

        /// <summary>Number of trigrams in this profile.</summary>
        public int Count => _trigramRanks.Count;

        /// <summary>
        /// Creates a trigram profile from a ranked list of trigrams.
        /// </summary>
        /// <param name="language">ISO 639-1 language code.</param>
        /// <param name="rankedTrigrams">
        /// Trigrams ordered by frequency (index 0 = most frequent).
        /// </param>
        public TrigramProfile(string language, IReadOnlyList<string> rankedTrigrams)
        {
            Language = language;
            _trigramRanks = new Dictionary<string, int>(rankedTrigrams.Count);
            for (var i = 0; i < rankedTrigrams.Count; i++)
            {
                // First occurrence wins if there are duplicates
                if (!_trigramRanks.ContainsKey(rankedTrigrams[i]))
                    _trigramRanks[rankedTrigrams[i]] = i;
            }
        }

        /// <summary>
        /// Computes similarity between this profile and an input trigram frequency distribution.
        /// Uses frequency-weighted overlap: for each input trigram occurrence, checks whether
        /// it exists in the profile. High-frequency trigrams contribute proportionally more,
        /// giving robust scores even for short texts.
        /// The result is a 0.0-1.0 similarity score (1.0 = all input trigrams found in profile).
        /// </summary>
        /// <param name="inputTrigrams">
        /// Dictionary mapping trigrams to their frequency counts in the input text.
        /// </param>
        /// <returns>Similarity score between 0.0 and 1.0.</returns>
        public float ComputeSimilarity(Dictionary<string, int> inputTrigrams)
        {
            if (inputTrigrams == null || inputTrigrams.Count == 0 || _trigramRanks.Count == 0)
                return 0f;

            long matchedWeight = 0;
            long totalWeight = 0;

            foreach (var kvp in inputTrigrams)
            {
                totalWeight += kvp.Value;
                if (_trigramRanks.ContainsKey(kvp.Key))
                {
                    matchedWeight += kvp.Value;
                }
            }

            if (totalWeight == 0)
                return 0f;

            return (float)matchedWeight / totalWeight;
        }

        /// <summary>
        /// Returns true if this profile contains the specified trigram.
        /// </summary>
        public bool Contains(string trigram)
        {
            return _trigramRanks.ContainsKey(trigram);
        }
    }
}