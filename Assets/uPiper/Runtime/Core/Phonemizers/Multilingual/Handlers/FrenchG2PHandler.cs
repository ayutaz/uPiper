using System;
using System.Threading;
using System.Threading.Tasks;
using DotNetG2P.French;
using uPiper.Core.Logging;

namespace uPiper.Core.Phonemizers.Multilingual.Handlers
{
    /// <summary>
    /// French G2P handler using DotNetG2P.French.
    /// Uses ToPuaPhonemes for phonemes and ToIpaWithProsody for prosody only.
    /// </summary>
    public sealed class FrenchG2PHandler : ILanguageG2PHandler
    {
        private FrenchG2PEngine _engine;
        private bool _isInitialized;
        private bool _disposed;

        /// <inheritdoc/>
        public string LanguageCode => "fr";

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
        /// <param name="engine">Pre-built French G2P engine instance.</param>
        public FrenchG2PHandler(FrenchG2PEngine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _isInitialized = true;
        }

        /// <summary>
        /// Creates a handler that will create its own engine on initialization.
        /// </summary>
        public FrenchG2PHandler()
        {
            _isInitialized = false;
        }

        /// <inheritdoc/>
        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_isInitialized)
                return Task.CompletedTask;

            _engine = new FrenchG2PEngine();
            _isInitialized = true;
            PiperLogger.LogInfo("[FrenchG2PHandler] Initialized: DotNetG2P.French");
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public (string[] Phonemes, int[] ProsodyFlat) Process(string text)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FrenchG2PHandler));
            if (!_isInitialized || _engine == null)
                throw new InvalidOperationException("Call InitializeAsync() before processing.");

            // Known limitation: ToPuaPhonemes and ToIpaWithProsody run G2P independently.
            // A unified DotNetG2P API (ToPuaWithProsody) would eliminate this double processing.
            // Prosody array length may differ from phonemes; loop uses Math.Min guard.
            var phonemes = _engine.ToPuaPhonemes(text);
            var result = _engine.ToIpaWithProsody(text);
            if (phonemes.Length != result.Prosody.Length)
            {
                PiperLogger.LogWarning(
                    $"[FrenchG2PHandler] Prosody length ({result.Prosody.Length}) differs from " +
                    $"phonemes length ({phonemes.Length}). Using min length.");
            }

            var flat = G2PHandlerUtils.ExtractProsodyFlat(
                result.Prosody, p => (p.A1, p.A2, p.A3), phonemes.Length);
            return (phonemes, flat);
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