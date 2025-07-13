using System;
using UnityEngine;

namespace uPiper.Core
{
    /// <summary>
    /// Main configuration for Piper TTS
    /// </summary>
    [Serializable]
    public class PiperConfig
    {
        [Header("General Settings")]
        
        /// <summary>
        /// Enable debug logging
        /// </summary>
        [Tooltip("Enable detailed debug logging")]
        public bool EnableDebugLogging = false;

        /// <summary>
        /// Default language for text processing
        /// </summary>
        [Tooltip("Default language code (e.g., 'ja' for Japanese, 'en' for English)")]
        public string DefaultLanguage = "ja";

        [Header("Performance Settings")]
        
        /// <summary>
        /// Maximum cache size in MB
        /// </summary>
        [Tooltip("Maximum size of phoneme cache in MB")]
        [Range(10, 500)]
        public int MaxCacheSizeMB = 100;

        /// <summary>
        /// Enable phoneme caching
        /// </summary>
        [Tooltip("Cache phoneme results to improve performance")]
        public bool EnablePhonemeCache = true;

        /// <summary>
        /// Number of worker threads for parallel processing
        /// </summary>
        [Tooltip("Number of worker threads (0 = auto-detect)")]
        [Range(0, 16)]
        public int WorkerThreads = 0;

        /// <summary>
        /// Inference backend type
        /// </summary>
        [Tooltip("Backend to use for neural network inference")]
        public InferenceBackend Backend = InferenceBackend.Auto;

        [Header("Audio Settings")]
        
        /// <summary>
        /// Output sample rate
        /// </summary>
        [Tooltip("Audio output sample rate in Hz")]
        public int SampleRate = 22050;

        /// <summary>
        /// Audio normalization
        /// </summary>
        [Tooltip("Normalize audio output volume")]
        public bool NormalizeAudio = true;

        /// <summary>
        /// Target RMS level for normalization
        /// </summary>
        [Tooltip("Target RMS level for audio normalization (dB)")]
        [Range(-40f, 0f)]
        public float TargetRMSLevel = -20f;

        [Header("Advanced Settings")]
        
        /// <summary>
        /// Timeout for operations in milliseconds
        /// </summary>
        [Tooltip("Operation timeout in milliseconds (0 = no timeout)")]
        public int TimeoutMs = 30000;

        /// <summary>
        /// Enable multi-threaded inference
        /// </summary>
        [Tooltip("Use multiple threads for inference (experimental)")]
        public bool EnableMultiThreadedInference = false;

        /// <summary>
        /// Batch size for inference
        /// </summary>
        [Tooltip("Batch size for neural network inference")]
        [Range(1, 32)]
        public int InferenceBatchSize = 1;

        /// <summary>
        /// Create default configuration
        /// </summary>
        public static PiperConfig CreateDefault()
        {
            return new PiperConfig();
        }

        /// <summary>
        /// Validate configuration
        /// </summary>
        public void Validate()
        {
            if (MaxCacheSizeMB < 10)
                MaxCacheSizeMB = 10;

            if (SampleRate != 16000 && SampleRate != 22050 && SampleRate != 44100 && SampleRate != 48000)
            {
                Debug.LogWarning($"Non-standard sample rate {SampleRate}Hz. Recommended: 22050Hz or 16000Hz");
            }

            if (WorkerThreads == 0)
            {
                WorkerThreads = Mathf.Max(1, SystemInfo.processorCount - 1);
            }
        }
    }

    /// <summary>
    /// Inference backend type
    /// </summary>
    public enum InferenceBackend
    {
        /// <summary>
        /// Automatically select best backend
        /// </summary>
        Auto,

        /// <summary>
        /// CPU backend
        /// </summary>
        CPU,

        /// <summary>
        /// GPU Compute backend
        /// </summary>
        GPUCompute,

        /// <summary>
        /// GPU Pixel backend
        /// </summary>
        GPUPixel
    }
}