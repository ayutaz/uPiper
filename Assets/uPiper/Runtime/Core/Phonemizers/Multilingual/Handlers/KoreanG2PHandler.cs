using System;
using System.Threading;
using System.Threading.Tasks;
using DotNetG2P.Korean;
using uPiper.Core.Logging;

namespace uPiper.Core.Phonemizers.Multilingual.Handlers
{
    /// <summary>
    /// Korean G2P handler using DotNetG2P.Korean (Hangul decomposition + phonological rules).
    /// Provides PUA phonemes with prosody A1/A2/A3 extraction.
    /// </summary>
    public sealed class KoreanG2PHandler : ILanguageG2PHandler
    {
        private KoreanG2PEngine _engine;
        private bool _isInitialized;
        private bool _disposed;

        /// <inheritdoc/>
        public string LanguageCode => "ko";

        /// <inheritdoc/>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Creates a handler with an externally provided engine.
        /// Ownership is managed by <see cref="HandlerEntry"/>.
        /// </summary>
        /// <remarks>
        /// The handler does NOT take ownership of the engine. When registered via
        /// <c>MultilingualPhonemizerOptions.Handlers</c>, the <c>HandlerEntry.IsOwned</c>
        /// flag is set to <c>false</c>, so <c>MultilingualPhonemizer.Dispose()</c>
        /// will NOT call this handler's Dispose. The caller retains responsibility
        /// for disposing the engine.
        /// </remarks>
        /// <param name="engine">Pre-built Korean G2P engine instance.</param>
        public KoreanG2PHandler(KoreanG2PEngine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _isInitialized = true;
        }

        /// <summary>
        /// Creates a handler that will create its own engine on initialization.
        /// </summary>
        public KoreanG2PHandler()
        {
            _isInitialized = false;
        }

        /// <inheritdoc/>
        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_isInitialized)
                return Task.CompletedTask;

            _engine = new KoreanG2PEngine();
            _isInitialized = true;
            PiperLogger.LogInfo("[KoreanG2PHandler] Initialized: DotNetG2P.Korean");
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public (string[] Phonemes, int[] A1, int[] A2, int[] A3) Process(string text)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(KoreanG2PHandler));
            if (!_isInitialized || _engine == null)
                throw new InvalidOperationException("Call InitializeAsync() before processing.");

            // Use DotNetG2P.Korean directly for Korean text
            var puaPhonemes = _engine.ToPuaPhonemes(text);
            var prosodyResult = _engine.ToIpaWithProsody(text);

            var phonemes = puaPhonemes ?? Array.Empty<string>();

            int[] segA1, segA2, segA3;

            // Map prosody from IPA-based result to PUA phonemes.
            // PUA phonemes and IPA phonemes should have the same count
            // (PuaMapper only replaces multi-char IPA with single PUA chars,
            // preserving the 1:1 phoneme correspondence).
            if (prosodyResult.Prosody.Length == phonemes.Length)
            {
                segA1 = new int[phonemes.Length];
                segA2 = new int[phonemes.Length];
                segA3 = new int[phonemes.Length];
                for (var i = 0; i < prosodyResult.Prosody.Length; i++)
                {
                    segA1[i] = prosodyResult.Prosody[i].A1;
                    segA2[i] = prosodyResult.Prosody[i].A2;
                    segA3[i] = prosodyResult.Prosody[i].A3;
                }
            }
            else
            {
                // Fallback: if lengths differ, use zeros for A1/A2 and
                // derive A3 from prosody result when possible
                segA1 = new int[phonemes.Length];
                segA2 = new int[phonemes.Length];
                segA3 = new int[phonemes.Length];
                if (prosodyResult.Prosody.Length > 0)
                {
                    var defaultA3 = prosodyResult.Prosody[0].A3;
                    for (var i = 0; i < phonemes.Length; i++)
                    {
                        segA3[i] = i < prosodyResult.Prosody.Length
                            ? prosodyResult.Prosody[i].A3
                            : defaultA3;
                    }
                }

                PiperLogger.LogWarning(
                    $"[KoreanG2PHandler] PUA/prosody length mismatch: " +
                    $"PUA={phonemes.Length}, Prosody={prosodyResult.Prosody.Length}");
            }

            return (phonemes, segA1, segA2, segA3);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _engine?.Dispose();
        }
    }
}
