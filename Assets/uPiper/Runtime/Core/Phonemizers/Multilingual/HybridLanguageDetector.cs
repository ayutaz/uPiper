using System;
using System.Collections.Generic;

namespace uPiper.Core.Phonemizers.Multilingual
{
    /// <summary>
    /// Hybrid language detector that combines Unicode-based and trigram-based detection.
    /// CJK/Kana/Hangul segments are detected by Unicode range (fast, accurate).
    /// Latin-script segments are refined by trigram frequency analysis for en/es/fr/pt disambiguation.
    /// When trigram detection is unavailable, falls back to Unicode-only detection.
    /// </summary>
    internal sealed class HybridLanguageDetector : ILanguageDetector
    {
        private readonly UnicodeLanguageDetector _unicodeDetector;
        private readonly LatinSegmentRefiner _refiner;
        private readonly IReadOnlyList<string> _languages;
        private readonly string _defaultLatinLanguage;

        /// <inheritdoc/>
        public string DefaultLatinLanguage => _defaultLatinLanguage;

        /// <inheritdoc/>
        public IReadOnlyList<string> Languages => _languages;

        /// <summary>
        /// Creates a HybridLanguageDetector with Unicode and trigram backends.
        /// </summary>
        /// <param name="unicodeDetector">Unicode-based detector for initial segmentation.</param>
        /// <param name="trigramDetector">
        /// Trigram detector for Latin segment refinement. Can be null for Unicode-only mode.
        /// </param>
        /// <param name="languages">Supported language codes.</param>
        /// <param name="defaultLatinLanguage">Default language for Latin text fallback.</param>
        public HybridLanguageDetector(
            UnicodeLanguageDetector unicodeDetector,
            TrigramLanguageDetector trigramDetector,
            IReadOnlyList<string> languages,
            string defaultLatinLanguage = "en")
        {
            _unicodeDetector = unicodeDetector
                ?? throw new ArgumentNullException(nameof(unicodeDetector));
            _languages = languages
                ?? throw new ArgumentNullException(nameof(languages));
            _defaultLatinLanguage = defaultLatinLanguage ?? "en";

            // Create refiner only when trigram detector is available
            _refiner = trigramDetector != null
                ? new LatinSegmentRefiner(trigramDetector)
                : null;
        }

        /// <inheritdoc/>
        public IReadOnlyList<(string language, string text)> SegmentText(string text)
        {
            // Step 1: Unicode-based segmentation
            var segments = _unicodeDetector.SegmentText(text);
            if (segments.Count == 0)
                return segments;

            // Step 2: If no refiner (Unicode-only mode), return as-is
            if (_refiner == null)
                return segments;

            // Step 3: Refine Latin segments using trigram detection
            return _refiner.Refine(segments, _defaultLatinLanguage);
        }
    }
}
