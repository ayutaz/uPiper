using System;
using System.Threading;
using System.Threading.Tasks;
using DotNetG2P.Spanish;
using uPiper.Core.Logging;

namespace uPiper.Core.Phonemizers.Multilingual.Handlers
{
    /// <summary>
    /// Spanish G2P handler using DotNetG2P.Spanish.
    /// Provides PUA phonemes with prosody A1/A2/A3 extraction.
    /// </summary>
    public sealed class SpanishG2PHandler : ILanguageG2PHandler
    {
        private SpanishG2PEngine _engine;
        private bool _ownsEngine;
        private bool _isInitialized;
        private bool _disposed;

        /// <inheritdoc/>
        public string LanguageCode => "es";

        /// <inheritdoc/>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Creates a handler with an externally provided engine (caller retains ownership).
        /// </summary>
        /// <param name="engine">Pre-built Spanish G2P engine instance.</param>
        public SpanishG2PHandler(SpanishG2PEngine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _ownsEngine = false;
            _isInitialized = true;
        }

        /// <summary>
        /// Creates a handler that will create its own engine on initialization.
        /// </summary>
        public SpanishG2PHandler()
        {
            _ownsEngine = false;
            _isInitialized = false;
        }

        /// <inheritdoc/>
        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_isInitialized)
                return Task.CompletedTask;

            _engine = new SpanishG2PEngine();
            _ownsEngine = true;
            _isInitialized = true;
            PiperLogger.LogInfo("[SpanishG2PHandler] Initialized: DotNetG2P.Spanish");
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public (string[] Phonemes, int[] A1, int[] A2, int[] A3) Process(string text)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SpanishG2PHandler));
            if (!_isInitialized || _engine == null)
                throw new InvalidOperationException("Call InitializeAsync() before processing.");

            var result = _engine.ToIpaWithProsody(text);
            var phonemes = result.Phonemes ?? Array.Empty<string>();
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
