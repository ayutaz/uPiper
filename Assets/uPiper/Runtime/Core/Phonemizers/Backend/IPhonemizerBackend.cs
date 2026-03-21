using System;
using System.Threading;
using System.Threading.Tasks;

namespace uPiper.Core.Phonemizers.Backend
{
    /// <summary>
    /// Interface for phonemizer backend implementations.
    /// This allows for pluggable phonemization engines with different licenses and capabilities.
    /// </summary>
    public interface IPhonemizerBackend : IDisposable
    {
        /// <summary>
        /// Initializes the backend asynchronously.
        /// </summary>
        /// <param name="options">Initialization options.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if initialization succeeded.</returns>
        public Task<bool> InitializeAsync(
            PhonemizerBackendOptions options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Converts text to phonemes asynchronously.
        /// </summary>
        /// <param name="text">The text to phonemize.</param>
        /// <param name="language">The language code.</param>
        /// <param name="options">Phonemization options.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The phonemization result.</returns>
        public Task<PhonemeResult> PhonemizeAsync(
            string text,
            string language,
            PhonemeOptions options = null,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Options for backend initialization.
    /// </summary>
    public class PhonemizerBackendOptions
    {
        /// <summary>
        /// Enable debug logging.
        /// </summary>
        public bool EnableDebugLogging { get; set; }
    }
}