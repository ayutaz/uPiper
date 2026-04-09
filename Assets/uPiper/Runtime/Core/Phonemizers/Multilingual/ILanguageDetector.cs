using System.Collections.Generic;

namespace uPiper.Core.Phonemizers.Multilingual
{
    /// <summary>
    /// Abstraction for language detection in multilingual TTS.
    /// Implementations segment mixed-language text into per-language chunks.
    /// </summary>
    public interface ILanguageDetector
    {
        /// <summary>
        /// Segments text into language-specific chunks.
        /// </summary>
        /// <param name="text">Input text (may contain mixed languages).</param>
        /// <returns>
        /// List of (languageCode, segmentText) tuples in order of appearance.
        /// Returns empty list for null/empty input.
        /// </returns>
        IReadOnlyList<(string language, string text)> SegmentText(string text);

        /// <summary>
        /// Gets the default language code for Latin-script text (e.g., "en").
        /// </summary>
        string DefaultLatinLanguage { get; }

        /// <summary>
        /// Gets the list of supported language codes.
        /// </summary>
        IReadOnlyList<string> Languages { get; }
    }
}