using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DotNetG2P.English;
using UnityEngine;
using uPiper.Core.Logging;

namespace uPiper.Core.Phonemizers.Multilingual.Handlers
{
    /// <summary>
    /// English G2P handler using DotNetG2P.English (CMU dict + LTS + homograph resolution).
    /// Provides PUA phonemes with prosody A1/A2/A3 extraction.
    /// </summary>
    public sealed class EnglishG2PHandler : ILanguageG2PHandler
    {
        private EnglishG2PEngine _engine;
        private bool _isInitialized;
        private bool _disposed;

        /// <inheritdoc/>
        public string LanguageCode => "en";

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
        /// <param name="engine">Pre-built English G2P engine instance.</param>
        public EnglishG2PHandler(EnglishG2PEngine engine)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _isInitialized = true;
        }

        /// <summary>
        /// Creates a handler that will create its own engine on initialization.
        /// </summary>
        public EnglishG2PHandler()
        {
            _isInitialized = false;
        }

        /// <inheritdoc/>
        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_isInitialized)
                return Task.CompletedTask;

            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                // WebGL: File I/O unavailable, use embedded dictionary
                _engine = new EnglishG2PEngine();
                PiperLogger.LogInfo(
                    "[EnglishG2PHandler] Initialized: DotNetG2P.English (embedded, WebGL)");
#elif UNITY_ANDROID && !UNITY_EDITOR
                // Android APK内のStreamingAssetsはFile APIで読めないため、embedded辞書にフォールバック
                _engine = new EnglishG2PEngine();
                PiperLogger.LogInfo(
                    "[EnglishG2PHandler] Initialized: DotNetG2P.English (embedded, Android)");
#else
                var dictPath = Path.Combine(
                    Application.streamingAssetsPath,
                    "uPiper",
                    "Phonemizers",
                    "cmudict-0.7b.txt");

                if (File.Exists(dictPath))
                {
                    _engine = new EnglishG2PEngine(dictPath);
                    PiperLogger.LogInfo(
                        "[EnglishG2PHandler] Initialized: DotNetG2P.English (external CMU dict)");
                }
                else
                {
                    // Fallback to embedded dictionary
                    _engine = new EnglishG2PEngine();
                    PiperLogger.LogInfo(
                        "[EnglishG2PHandler] Initialized: DotNetG2P.English (embedded CMU dict)");
                }
#endif

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                PiperLogger.LogWarning(
                    $"[EnglishG2PHandler] Failed to initialize: {ex.Message}");
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public (string[] Phonemes, int[] A1, int[] A2, int[] A3) Process(string text)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(EnglishG2PHandler));
            if (!_isInitialized || _engine == null)
                throw new InvalidOperationException("Call InitializeAsync() before processing.");

            // Known limitation: ToPuaPhonemes and ToIpaWithProsody run G2P independently.
            // A unified DotNetG2P API (ToPuaWithProsody) would eliminate this double processing.
            // Prosody array length may differ from phonemes; loop uses Math.Min guard.
            var prosodyResult = _engine.ToIpaWithProsody(text);
            var phonemes = _engine.ToPuaPhonemes(text);
            var prosody = prosodyResult.Prosody;
            if (phonemes.Length != prosody.Length)
            {
                PiperLogger.LogWarning(
                    $"[EnglishG2PHandler] Prosody length ({prosody.Length}) differs from " +
                    $"phonemes length ({phonemes.Length}). Using min length.");
            }

            var a1 = new int[phonemes.Length];
            var a2 = new int[phonemes.Length];
            var a3 = new int[phonemes.Length];
            for (var i = 0; i < phonemes.Length && i < prosody.Length; i++)
            {
                a1[i] = prosody[i].A1;
                a2[i] = prosody[i].A2;
                a3[i] = prosody[i].A3;
            }

            return (phonemes, a1, a2, a3);
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
