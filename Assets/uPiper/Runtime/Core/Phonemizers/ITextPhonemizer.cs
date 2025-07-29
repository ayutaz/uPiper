using System.Threading;
using System.Threading.Tasks;
using uPiper.Core.Phonemizers.Backend;

namespace uPiper.Core.Phonemizers
{
    /// <summary>
    /// Interface for text-to-phoneme conversion.
    /// Provides a simplified API for integrating different phonemizers with audio generation.
    /// </summary>
    public interface ITextPhonemizer
    {
        /// <summary>
        /// Gets the name of the phonemizer.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the supported language codes (e.g., "ja", "en").
        /// </summary>
        public string[] SupportedLanguages { get; }

        /// <summary>
        /// Converts text to phonemes asynchronously.
        /// </summary>
        /// <param name="text">The input text to convert.</param>
        /// <param name="language">The language code (e.g., "ja", "en").</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task that represents the asynchronous operation, containing the phoneme result.</returns>
        public Task<PhonemeResult> PhonemizeAsync(string text, string language, CancellationToken cancellationToken = default);

        /// <summary>
        /// Converts text to phonemes synchronously.
        /// </summary>
        /// <param name="text">The input text to convert.</param>
        /// <param name="language">The language code (e.g., "ja", "en").</param>
        /// <returns>The phoneme result.</returns>
        public PhonemeResult Phonemize(string text, string language);
    }
}