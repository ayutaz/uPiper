using System;
using System.Threading;
using System.Threading.Tasks;
using uPiper.Core.AudioGeneration;
using uPiper.Core.Logging;
using uPiper.Core.Phonemizers.Implementations;

namespace uPiper.Core.Phonemizers.Multilingual.Handlers
{
    /// <summary>
    /// Japanese G2P handler using DotNetG2PPhonemizer (dot-net-g2p / MeCab dictionary).
    /// Provides prosody-aware phonemization with A1/A2/A3 extraction.
    /// </summary>
    public sealed class JapaneseG2PHandler : ILanguageG2PHandler
    {
        private DotNetG2PPhonemizer _phonemizer;
        private bool _isInitialized;
        private bool _disposed;

        /// <inheritdoc/>
        public string LanguageCode => "ja";

        /// <inheritdoc/>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Creates a handler with an externally provided phonemizer.
        /// Ownership is managed by <see cref="HandlerEntry"/>.
        /// </summary>
        /// <remarks>
        /// The handler does NOT take ownership of the engine. When registered via
        /// <c>MultilingualPhonemizerOptions.Handlers</c>, the <c>HandlerEntry.IsOwned</c>
        /// flag is set to <c>false</c>, so <c>MultilingualPhonemizer.Dispose()</c>
        /// will NOT call this handler's Dispose. The caller retains responsibility
        /// for disposing the engine.
        /// </remarks>
        /// <param name="phonemizer">Pre-built Japanese phonemizer instance.</param>
        public JapaneseG2PHandler(DotNetG2PPhonemizer phonemizer)
        {
            _phonemizer = phonemizer ?? throw new ArgumentNullException(nameof(phonemizer));
            _isInitialized = true;
        }

        /// <summary>
        /// Creates a handler that will create its own phonemizer on initialization.
        /// </summary>
        public JapaneseG2PHandler()
        {
            _isInitialized = false;
        }

        /// <inheritdoc/>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_isInitialized)
                return;

#if UNITY_WEBGL && !UNITY_EDITOR
            _phonemizer = new DotNetG2PPhonemizer();
            await _phonemizer.InitializeAsync(cancellationToken);
#else
            _phonemizer = new DotNetG2PPhonemizer();
            await Task.CompletedTask;
#endif
            _isInitialized = true;
            PiperLogger.LogInfo("[JapaneseG2PHandler] Initialized");
        }

        /// <inheritdoc/>
        public (string[] Phonemes, int[] ProsodyFlat) Process(string text)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(JapaneseG2PHandler));
            if (!_isInitialized)
                throw new InvalidOperationException("Call InitializeAsync() before processing.");

            var result = _phonemizer.PhonemizeWithProsody(text);
            var phonemes = result.Phonemes ?? Array.Empty<string>();
            var a1 = result.ProsodyA1 ?? Array.Empty<int>();
            var a2 = result.ProsodyA2 ?? Array.Empty<int>();
            var a3 = result.ProsodyA3 ?? Array.Empty<int>();

            // Strip leading PAD ("_") from Japanese segments (added from "sil" conversion)
            if (phonemes.Length > 0 && phonemes[0] == "_")
            {
                phonemes = phonemes[1..];
                a1 = a1.Length > 1 ? a1[1..] : a1;
                a2 = a2.Length > 1 ? a2[1..] : a2;
                a3 = a3.Length > 1 ? a3[1..] : a3;
            }

            // Flatten A1/A2/A3 -> stride=3 flat array
            var flat = PhonemeEncoder.FlattenProsody(a1, a2, a3, phonemes.Length);
            return (phonemes, flat);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _phonemizer?.Dispose();
        }
    }
}