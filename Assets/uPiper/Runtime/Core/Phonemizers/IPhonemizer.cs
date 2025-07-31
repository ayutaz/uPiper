using System;
using System.Threading;
using System.Threading.Tasks;
using uPiper.Core.Phonemizers.Backend;

namespace uPiper.Core.Phonemizers
{
    /// <summary>
    /// Interface for phonemizers that convert text to phonemes.
    /// </summary>
    public interface IPhonemizer : IDisposable
    {
        /// <summary>
        /// Gets the name of the phonemizer.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the version of the phonemizer.
        /// </summary>
        public string Version { get; }

        /// <summary>
        /// Gets the list of supported language codes (ISO 639-1).
        /// </summary>
        public string[] SupportedLanguages { get; }

        /// <summary>
        /// Gets or sets whether to use caching for phonemization results.
        /// </summary>
        public bool UseCache { get; set; }

        /// <summary>
        /// Converts text to phonemes asynchronously.
        /// </summary>
        /// <param name="text">The text to phonemize.</param>
        /// <param name="language">The language code (default: "ja").</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The phonemization result.</returns>
        public Task<PhonemeResult> PhonemizeAsync(string text, string language = "ja", CancellationToken cancellationToken = default);

        /// <summary>
        /// Converts text to phonemes synchronously.
        /// </summary>
        /// <param name="text">The text to phonemize.</param>
        /// <param name="language">The language code (default: "ja").</param>
        /// <returns>The phonemization result.</returns>
        public PhonemeResult Phonemize(string text, string language = "ja");

        /// <summary>
        /// Converts multiple texts to phonemes asynchronously.
        /// </summary>
        /// <param name="texts">The texts to phonemize.</param>
        /// <param name="language">The language code (default: "ja").</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Array of phonemization results.</returns>
        public Task<PhonemeResult[]> PhonemizeBatchAsync(string[] texts, string language = "ja", CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears the phonemization cache.
        /// </summary>
        public void ClearCache();

        /// <summary>
        /// Gets cache statistics.
        /// </summary>
        /// <returns>Cache statistics.</returns>
        public CacheStatistics GetCacheStatistics();

        /// <summary>
        /// Checks if a language is supported.
        /// </summary>
        /// <param name="language">The language code to check.</param>
        /// <returns>True if the language is supported.</returns>
        public bool IsLanguageSupported(string language);

        /// <summary>
        /// Gets detailed information about a supported language.
        /// </summary>
        /// <param name="language">The language code.</param>
        /// <returns>Language information or null if not supported.</returns>
        public LanguageInfo GetLanguageInfo(string language);
    }
}