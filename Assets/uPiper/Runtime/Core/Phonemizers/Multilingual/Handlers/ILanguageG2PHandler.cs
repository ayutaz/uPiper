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
    /// To create a custom handler, implement this interface and register it via
    /// <see cref="MultilingualPhonemizerOptions.Handlers"/>. See existing implementations
    /// (e.g., <see cref="JapaneseG2PHandler"/>, <see cref="EnglishG2PHandler"/>) for reference.
    ///
    /// Future consideration: If additional return data is needed (confidence, syllable boundaries),
    /// consider migrating from ValueTuple to a dedicated PhonemeProcessResult type.
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
        /// Processes text and returns phonemes with a prosody flat array (stride=3).
        /// ProsodyFlat layout: [a1_0, a2_0, a3_0, a1_1, a2_1, a3_1, ...].
        /// ProsodyFlat.Length must equal Phonemes.Length * 3.
        /// </summary>
        /// <param name="text">Input text in the handler's language.</param>
        /// <returns>Tuple of (Phonemes, ProsodyFlat).</returns>
        (string[] Phonemes, int[] ProsodyFlat) Process(string text);
    }
}