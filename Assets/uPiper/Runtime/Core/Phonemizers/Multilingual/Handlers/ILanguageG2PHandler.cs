using System;
using System.Threading;
using System.Threading.Tasks;

namespace uPiper.Core.Phonemizers.Multilingual.Handlers
{
    /// <summary>
    /// Strategy interface for per-language G2P (Grapheme-to-Phoneme) processing.
    /// Each language provides an implementation that handles text-to-phoneme conversion
    /// with prosody information.
    /// </summary>
    /// <remarks>
    /// All Process() implementations MUST be synchronous.
    /// InitializeAsync() is needed only for languages requiring dictionary loading (ja, en, zh).
    /// Do NOT use Task.Run inside Process() — WebGL prohibits background threads.
    /// Note: P2-2 (Prosody flat array) will change the return type to
    /// (string[] Phonemes, int[] ProsodyFlat) with stride=3.
    /// To create a custom handler, implement this interface and register it via
    /// <see cref="MultilingualPhonemizerOptions.Handlers"/>. See existing implementations
    /// (e.g., <see cref="JapaneseG2PHandler"/>, <see cref="EnglishG2PHandler"/>) for reference.
    /// </remarks>
    public interface ILanguageG2PHandler : IDisposable
    {
        /// <summary>ISO 639-1 language code (e.g., "ja", "en").</summary>
        string LanguageCode { get; }

        /// <summary>Whether the handler has been initialized (dictionaries loaded).</summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Initializes the handler asynchronously (loads dictionaries if needed).
        /// For languages without dictionaries (es, fr, pt, ko), returns Task.CompletedTask.
        /// </summary>
        Task InitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Processes text and returns phonemes with prosody arrays.
        /// All arrays must have the same length.
        /// </summary>
        /// <param name="text">Input text in the handler's language.</param>
        /// <returns>Tuple of (Phonemes, ProsodyA1, ProsodyA2, ProsodyA3).</returns>
        (string[] Phonemes, int[] A1, int[] A2, int[] A3) Process(string text);
    }
}
