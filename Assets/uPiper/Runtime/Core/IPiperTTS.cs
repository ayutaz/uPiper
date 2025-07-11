using System;
using System.Threading.Tasks;
using UnityEngine;

namespace uPiper.Core
{
    public interface IPiperTTS : IDisposable
    {
        /// <summary>
        /// Initializes the TTS engine with the specified configuration
        /// </summary>
        /// <param name="config">Configuration settings for the TTS engine</param>
        /// <returns>Task that completes when initialization is finished</returns>
        Task InitializeAsync(PiperConfig config);

        /// <summary>
        /// Generates speech audio from the provided text
        /// </summary>
        /// <param name="text">The text to convert to speech</param>
        /// <param name="language">Language code (e.g., "ja" for Japanese)</param>
        /// <returns>AudioClip containing the generated speech</returns>
        Task<AudioClip> GenerateSpeechAsync(string text, string language = "ja");

        /// <summary>
        /// Gets whether the TTS engine is initialized and ready
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Gets the current configuration
        /// </summary>
        PiperConfig CurrentConfig { get; }

        /// <summary>
        /// Event raised when initialization state changes
        /// </summary>
        event Action<bool> OnInitializationStateChanged;

        /// <summary>
        /// Event raised when an error occurs
        /// </summary>
        event Action<string> OnError;
    }
}