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
        /// Converts text to phonemes asynchronously.
        /// </summary>
        /// <param name="text">The text to phonemize.</param>
        /// <param name="language">The language code (default: "ja").</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The phonemization result.</returns>
        public Task<PhonemeResult> PhonemizeAsync(string text, string language = "ja", CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears the phonemization cache.
        /// </summary>
        public void ClearCache();

        /// <summary>
        /// Gets cache statistics.
        /// </summary>
        /// <returns>Cache statistics.</returns>
        public CacheStatistics GetCacheStatistics();
    }
}