using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace uPiper.Core
{
    /// <summary>
    /// Main interface for Piper TTS functionality
    /// </summary>
    public interface IPiperTTS : IDisposable
    {
        /// <summary>
        /// Current configuration
        /// </summary>
        public PiperConfig Configuration { get; }

        /// <summary>
        /// Whether the TTS engine is initialized and ready
        /// </summary>
        public bool IsInitialized { get; }

        /// <summary>
        /// Currently loaded voice configuration
        /// </summary>
        public PiperVoiceConfig CurrentVoice { get; }

        /// <summary>
        /// Initialize the TTS engine
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task that completes when initialization is done</returns>
        public Task InitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate audio from text (asynchronous)
        /// </summary>
        /// <param name="text">Input text</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task that returns generated audio clip</returns>
        public Task<AudioClip> GenerateAudioAsync(string text, CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate audio from text with specific voice configuration (asynchronous)
        /// </summary>
        /// <param name="text">Input text</param>
        /// <param name="voiceConfig">Voice configuration to use</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task that returns generated audio clip</returns>
        public Task<AudioClip> GenerateAudioAsync(string text, PiperVoiceConfig voiceConfig, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stream audio generation for long text
        /// </summary>
        /// <param name="text">Input text</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Async enumerable of audio chunks</returns>
        public IAsyncEnumerable<AudioChunk> StreamAudioAsync(string text, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stream audio generation with specific voice configuration
        /// </summary>
        /// <param name="text">Input text</param>
        /// <param name="voiceConfig">Voice configuration to use</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Async enumerable of audio chunks</returns>
        public IAsyncEnumerable<AudioChunk> StreamAudioAsync(string text, PiperVoiceConfig voiceConfig, CancellationToken cancellationToken = default);

        /// <summary>
        /// Load a voice model
        /// </summary>
        /// <param name="voiceConfig">Voice configuration to load</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task that completes when voice is loaded</returns>
        public Task LoadVoiceAsync(PiperVoiceConfig voiceConfig, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get available voices
        /// </summary>
        /// <returns>List of available voice configurations</returns>
        public IReadOnlyList<PiperVoiceConfig> GetAvailableVoices();

        /// <summary>
        /// Clear phoneme cache
        /// </summary>
        public void ClearCache();

        /// <summary>
        /// Get cache statistics
        /// </summary>
        /// <returns>Cache statistics</returns>
        public CacheStatistics GetCacheStatistics();

        /// <summary>
        /// Generate audio from text with explicit language specification.
        /// </summary>
        /// <param name="text">Input text</param>
        /// <param name="language">Language code (e.g., "ja", "en", "auto" for auto-detect)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Generated audio clip</returns>
        public Task<AudioClip> GenerateAudioAsync(string text, string language, CancellationToken cancellationToken = default);

        /// <summary>
        /// Detect the primary language of the given text using Unicode character analysis.
        /// </summary>
        /// <param name="text">Input text to analyze</param>
        /// <returns>Detected language code (e.g., "ja", "en"), or the default language if undetermined</returns>
        public string DetectLanguage(string text);

        /// <summary>
        /// Get the list of languages supported by the current model.
        /// </summary>
        /// <returns>Read-only list of language codes</returns>
        public IReadOnlyList<string> GetSupportedLanguages();

        /// <summary>
        /// Event raised when initialization completes
        /// </summary>
        public event Action<bool> OnInitialized;

        /// <summary>
        /// Event raised when voice is loaded
        /// </summary>
        public event Action<PiperVoiceConfig> OnVoiceLoaded;

        /// <summary>
        /// Event raised on error
        /// </summary>
        public event Action<PiperException> OnError;

        /// <summary>
        /// Event raised when a language is detected in the input text.
        /// The string argument is the detected language code.
        /// </summary>
        public event Action<string> OnLanguageDetected;
    }
}