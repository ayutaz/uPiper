using Unity.Sentis;
using UnityEngine;

namespace uPiper.Core
{
    [System.Serializable]
    public class PiperConfig
    {
        /// <summary>
        /// Default language for speech synthesis
        /// </summary>
        public string Language { get; set; } = "ja";

        /// <summary>
        /// Path to the ONNX model file
        /// </summary>
        public string ModelPath { get; set; }

        /// <summary>
        /// Whether to use caching for phonemization results
        /// </summary>
        public bool UseCache { get; set; } = true;

        /// <summary>
        /// Maximum cache size in MB
        /// </summary>
        public int MaxCacheSizeMB { get; set; } = 100;

        /// <summary>
        /// Sentis backend type for inference
        /// </summary>
        public BackendType SentisBackend { get; set; } = BackendType.GPUCompute;

        /// <summary>
        /// Audio sample rate
        /// </summary>
        public int SampleRate { get; set; } = 22050;

        /// <summary>
        /// Number of audio channels (1 for mono, 2 for stereo)
        /// </summary>
        public int Channels { get; set; } = 1;

        /// <summary>
        /// Whether to log debug information
        /// </summary>
        public bool EnableDebugLogging { get; set; } = false;

        /// <summary>
        /// Timeout for operations in milliseconds
        /// </summary>
        public int TimeoutMs { get; set; } = 5000;

        /// <summary>
        /// Validates the configuration
        /// </summary>
        /// <returns>True if valid, false otherwise</returns>
        public bool Validate(out string errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrEmpty(ModelPath))
            {
                errorMessage = "Model path is required";
                return false;
            }

            if (SampleRate <= 0)
            {
                errorMessage = "Sample rate must be positive";
                return false;
            }

            if (Channels != 1 && Channels != 2)
            {
                errorMessage = "Channels must be 1 (mono) or 2 (stereo)";
                return false;
            }

            if (MaxCacheSizeMB <= 0)
            {
                errorMessage = "Cache size must be positive";
                return false;
            }

            if (TimeoutMs <= 0)
            {
                errorMessage = "Timeout must be positive";
                return false;
            }

            return true;
        }
    }
}