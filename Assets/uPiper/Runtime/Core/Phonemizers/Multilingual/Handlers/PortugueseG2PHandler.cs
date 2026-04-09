using System;
using System.Threading;
using System.Threading.Tasks;
using DotNetG2P.Portuguese;
using uPiper.Core.Logging;

namespace uPiper.Core.Phonemizers.Multilingual.Handlers
{
    /// <summary>
    /// Portuguese G2P handler using DotNetG2P.Portuguese.
    /// Uses ToPuaPhonemes for phonemes and ToIpaWithProsody for prosody only.
    /// </summary>
    public sealed class PortugueseG2PHandler : ILanguageG2PHandler
    {
        private PortugueseG2PEngine _engine;
        private bool _ownsEngine;
        private bool _isInitialized;
        private bool _disposed;

        /// <inheritdoc/>
        public string LanguageCode => "pt";

        /// <inheritdoc/>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Creates a handler with an externally provided engine (caller retains ownership).
        /// </summary>
        /// <param name="engine">Pre-built Portuguese G2P engine instance.</param>
        public PortugueseG2PHandler(PortugueseG2PEngine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _ownsEngine = false;
            _isInitialized = true;
        }

        /// <summary>
        /// Creates a handler that will create its own engine on initialization.
        /// </summary>
        public PortugueseG2PHandler()
        {
            _ownsEngine = false;
            _isInitialized = false;
        }

        /// <inheritdoc/>
        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_isInitialized)
                return Task.CompletedTask;

            _engine = new PortugueseG2PEngine();
            _ownsEngine = true;
            _isInitialized = true;
            PiperLogger.LogInfo("[PortugueseG2PHandler] Initialized: DotNetG2P.Portuguese");
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public (string[] Phonemes, int[] A1, int[] A2, int[] A3) Process(string text)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PortugueseG2PHandler));
            if (!_isInitialized || _engine == null)
                throw new InvalidOperationException("Call InitializeAsync() before processing.");

            var phonemes = _engine.ToPuaPhonemes(text);
            var result = _engine.ToIpaWithProsody(text);
            var (a1, a2, a3) = G2PHandlerUtils.ExtractProsodyArrays(
                result.Prosody, p => (p.A1, p.A2, p.A3), phonemes.Length);
            return (phonemes, a1, a2, a3);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (_ownsEngine)
                _engine?.Dispose();
        }
    }
}
