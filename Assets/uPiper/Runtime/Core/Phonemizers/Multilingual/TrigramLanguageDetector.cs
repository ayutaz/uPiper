using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace uPiper.Core.Phonemizers.Multilingual
{
    /// <summary>
    /// Result of trigram-based language detection.
    /// </summary>
    internal readonly struct TrigramDetectionResult
    {
        /// <summary>Detected language code, or null if detection failed.</summary>
        public string Language { get; }

        /// <summary>Similarity score of the best match (0.0-1.0).</summary>
        public float Score { get; }

        /// <summary>Similarity score of the second-best match (0.0-1.0).</summary>
        public float SecondScore { get; }

        /// <summary>Whether detection succeeded (score above threshold and sufficient margin).</summary>
        public bool IsConfident { get; }

        public TrigramDetectionResult(string language, float score, float secondScore, bool isConfident)
        {
            Language = language;
            Score = score;
            SecondScore = secondScore;
            IsConfident = isConfident;
        }
    }

    /// <summary>
    /// Trigram frequency-based language detector for Latin-script languages.
    /// Uses the Out-of-Place distance method (Cavnar &amp; Trenkle 1994) to compare
    /// input text trigram distributions against precomputed language profiles.
    /// </summary>
    internal sealed class TrigramLanguageDetector
    {
        /// <summary>Minimum character count for trigram detection to be attempted.</summary>
        public const int MinCharsForDetection = 10;

        /// <summary>Default confidence threshold for detection.</summary>
        public const float DefaultConfidenceThreshold = 0.65f;

        /// <summary>
        /// Higher confidence threshold for short texts (between MinCharsForDetection and 15 chars).
        /// </summary>
        public const float ShortTextConfidenceThreshold = 0.75f;

        /// <summary>Short text length boundary (texts below this use higher threshold).</summary>
        public const int ShortTextBoundary = 15;

        /// <summary>Minimum margin between top-1 and top-2 scores for confident detection.</summary>
        public const float MinMargin = 0.05f;

        private readonly Dictionary<string, TrigramProfile> _profiles;

        /// <summary>
        /// Creates a TrigramLanguageDetector with precomputed language profiles.
        /// </summary>
        /// <param name="profiles">Dictionary mapping language codes to their trigram profiles.</param>
        public TrigramLanguageDetector(Dictionary<string, TrigramProfile> profiles)
        {
            _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        }

        /// <summary>
        /// Detects the language of the given Latin-script text.
        /// </summary>
        /// <param name="text">Input text to analyze.</param>
        /// <returns>Detection result with language, scores, and confidence.</returns>
        public TrigramDetectionResult Detect(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new TrigramDetectionResult(null, 0f, 0f, false);

            var normalized = NormalizeText(text);
            if (normalized.Length < MinCharsForDetection)
                return new TrigramDetectionResult(null, 0f, 0f, false);

            var trigrams = ExtractTrigrams(normalized);
            if (trigrams.Count == 0)
                return new TrigramDetectionResult(null, 0f, 0f, false);

            // Score against all profiles
            string bestLang = null;
            var bestScore = 0f;
            var secondScore = 0f;

            foreach (var kvp in _profiles)
            {
                var score = kvp.Value.ComputeSimilarity(trigrams);
                if (score > bestScore)
                {
                    secondScore = bestScore;
                    bestScore = score;
                    bestLang = kvp.Key;
                }
                else if (score > secondScore)
                {
                    secondScore = score;
                }
            }

            // Determine confidence threshold based on text length
            var threshold = normalized.Length < ShortTextBoundary
                ? ShortTextConfidenceThreshold
                : DefaultConfidenceThreshold;

            var isConfident = bestScore >= threshold && (bestScore - secondScore) >= MinMargin;

            return new TrigramDetectionResult(bestLang, bestScore, secondScore, isConfident);
        }

        /// <summary>
        /// Normalizes text for trigram extraction:
        /// 1. Lowercase
        /// 2. NFD decomposition, remove combining marks (accent removal)
        /// 3. Non-alphabetic characters to space
        /// 4. Collapse consecutive spaces
        /// 5. Add word boundary spaces (leading/trailing)
        /// </summary>
        /// <param name="text">Raw input text.</param>
        /// <returns>Normalized text ready for trigram extraction.</returns>
        internal static string NormalizeText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // Step 1: Lowercase
            var lower = text.ToLowerInvariant();

            // Step 2: NFD decomposition + remove combining marks
            var nfd = lower.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(nfd.Length);
            for (var i = 0; i < nfd.Length; i++)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(nfd[i]);
                if (category != UnicodeCategory.NonSpacingMark
                    && category != UnicodeCategory.SpacingCombiningMark
                    && category != UnicodeCategory.EnclosingMark)
                {
                    sb.Append(nfd[i]);
                }
            }

            // Step 3: Non-alphabetic → space, Step 4: collapse consecutive spaces
            var cleaned = new StringBuilder(sb.Length + 2);
            var lastWasSpace = true; // Treat start as space to skip leading spaces

            for (var i = 0; i < sb.Length; i++)
            {
                var ch = sb[i];
                if (ch >= 'a' && ch <= 'z')
                {
                    cleaned.Append(ch);
                    lastWasSpace = false;
                }
                else
                {
                    if (!lastWasSpace)
                    {
                        cleaned.Append(' ');
                        lastWasSpace = true;
                    }
                }
            }

            // Step 5: Add word boundary spaces
            // Remove trailing space if present, then wrap with spaces
            if (cleaned.Length > 0 && cleaned[cleaned.Length - 1] == ' ')
                cleaned.Remove(cleaned.Length - 1, 1);

            if (cleaned.Length == 0)
                return string.Empty;

            cleaned.Insert(0, ' ');
            cleaned.Append(' ');

            return cleaned.ToString();
        }

        /// <summary>
        /// Extracts trigrams (3-character sequences) from normalized text.
        /// </summary>
        /// <param name="normalizedText">Text that has been normalized via NormalizeText.</param>
        /// <returns>Dictionary mapping each trigram to its frequency count.</returns>
        internal static Dictionary<string, int> ExtractTrigrams(string normalizedText)
        {
            var trigrams = new Dictionary<string, int>();
            if (string.IsNullOrEmpty(normalizedText) || normalizedText.Length < 3)
                return trigrams;

            for (var i = 0; i <= normalizedText.Length - 3; i++)
            {
                var trigram = normalizedText.Substring(i, 3);
                if (trigrams.TryGetValue(trigram, out var count))
                    trigrams[trigram] = count + 1;
                else
                    trigrams[trigram] = 1;
            }

            return trigrams;
        }
    }
}