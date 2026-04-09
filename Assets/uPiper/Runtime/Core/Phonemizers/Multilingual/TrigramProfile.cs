using System.Collections.Generic;

namespace uPiper.Core.Phonemizers.Multilingual
{
    /// <summary>
    /// Represents a language's trigram frequency profile for language detection.
    /// Trigrams are ranked by frequency (index 0 = most frequent).
    /// Similarity is computed using the Out-of-Place distance method (Cavnar &amp; Trenkle 1994).
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
        /// Uses the Out-of-Place distance method: for each input trigram, the absolute difference
        /// between its rank in the input and its rank in this profile is summed.
        /// The result is normalized to a 0.0-1.0 similarity score (1.0 = identical).
        /// </summary>
        /// <param name="inputTrigrams">
        /// Dictionary mapping trigrams to their frequency counts in the input text.
        /// </param>
        /// <returns>Similarity score between 0.0 and 1.0.</returns>
        public float ComputeSimilarity(Dictionary<string, int> inputTrigrams)
        {
            if (inputTrigrams == null || inputTrigrams.Count == 0 || _trigramRanks.Count == 0)
                return 0f;

            // Build ranked list from input frequencies (sorted descending by count)
            var inputRanked = new List<KeyValuePair<string, int>>(inputTrigrams);
            inputRanked.Sort((a, b) => b.Value.CompareTo(a.Value));

            var maxDistance = _trigramRanks.Count;
            long totalDistance = 0;

            for (var inputRank = 0; inputRank < inputRanked.Count; inputRank++)
            {
                var trigram = inputRanked[inputRank].Key;
                if (_trigramRanks.TryGetValue(trigram, out var profileRank))
                {
                    totalDistance += System.Math.Abs(inputRank - profileRank);
                }
                else
                {
                    // Trigram not in profile: use maximum distance penalty
                    totalDistance += maxDistance;
                }
            }

            // Normalize: worst case is inputCount * maxDistance
            var worstCase = (long)inputRanked.Count * maxDistance;
            if (worstCase == 0)
                return 0f;

            return 1f - (float)totalDistance / worstCase;
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
