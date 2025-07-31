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
        /// Gets the name of this phonemizer backend.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the version of this backend.
        /// </summary>
        public string Version { get; }

        /// <summary>
        /// Gets the license type of this backend (e.g., "MIT", "GPL v3").
        /// </summary>
        public string License { get; }

        /// <summary>
        /// Gets the list of supported language codes (ISO 639-1).
        /// </summary>
        public string[] SupportedLanguages { get; }

        /// <summary>
        /// Gets the priority of this backend (higher values are preferred).
        /// </summary>
        public int Priority { get; }

        /// <summary>
        /// Checks if this backend is currently available and initialized.
        /// </summary>
        public bool IsAvailable { get; }

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

        /// <summary>
        /// Checks if a specific language is supported.
        /// </summary>
        /// <param name="language">The language code to check.</param>
        /// <returns>True if the language is supported.</returns>
        public bool SupportsLanguage(string language);

        /// <summary>
        /// Gets the estimated memory usage of this backend in bytes.
        /// </summary>
        /// <returns>Estimated memory usage.</returns>
        public long GetMemoryUsage();

        /// <summary>
        /// Gets backend-specific capabilities.
        /// </summary>
        public BackendCapabilities GetCapabilities();
    }

    /// <summary>
    /// Options for backend initialization.
    /// </summary>
    public class PhonemizerBackendOptions
    {
        /// <summary>
        /// Path to data files (dictionaries, models, etc.).
        /// </summary>
        public string DataPath { get; set; }

        /// <summary>
        /// Maximum memory usage in bytes (0 = unlimited).
        /// </summary>
        public long MaxMemoryUsage { get; set; }

        /// <summary>
        /// Enable debug logging.
        /// </summary>
        public bool EnableDebugLogging { get; set; }

        /// <summary>
        /// Custom configuration parameters.
        /// </summary>
        public System.Collections.Generic.Dictionary<string, object> CustomParams { get; set; }
    }

    /// <summary>
    /// Describes the capabilities of a phonemizer backend.
    /// </summary>
    public class BackendCapabilities
    {
        /// <summary>
        /// Supports IPA (International Phonetic Alphabet) output.
        /// </summary>
        public bool SupportsIPA { get; set; }

        /// <summary>
        /// Supports stress markers in output.
        /// </summary>
        public bool SupportsStress { get; set; }

        /// <summary>
        /// Supports syllable boundaries.
        /// </summary>
        public bool SupportsSyllables { get; set; }

        /// <summary>
        /// Supports tone markers (for tonal languages).
        /// </summary>
        public bool SupportsTones { get; set; }

        /// <summary>
        /// Supports phoneme duration prediction.
        /// </summary>
        public bool SupportsDuration { get; set; }

        /// <summary>
        /// Can process multiple sentences in batch.
        /// </summary>
        public bool SupportsBatchProcessing { get; set; }

        /// <summary>
        /// Thread-safe for concurrent calls.
        /// </summary>
        public bool IsThreadSafe { get; set; }

        /// <summary>
        /// Requires network connection.
        /// </summary>
        public bool RequiresNetwork { get; set; }
    }
}