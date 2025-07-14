using System;
using UnityEngine;
using uPiper.Core.Logging;

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
            // Cache size validation
            if (MaxCacheSizeMB < 10)
            {
                PiperLogger.LogWarning("MaxCacheSizeMB too small ({0}MB), setting to minimum 10MB", MaxCacheSizeMB);
                MaxCacheSizeMB = 10;
            }
            else if (MaxCacheSizeMB > 500)
            {
                PiperLogger.LogWarning("MaxCacheSizeMB too large ({0}MB), setting to maximum 500MB", MaxCacheSizeMB);
                MaxCacheSizeMB = 500;
            }

            // Sample rate validation
            if (SampleRate < 8000 || SampleRate > 48000)
            {
                throw new PiperException($"Invalid sample rate: {SampleRate}Hz. Must be between 8000-48000Hz");
            }
            
            if (SampleRate != 16000 && SampleRate != 22050 && SampleRate != 44100 && SampleRate != 48000)
            {
                PiperLogger.LogWarning("Non-standard sample rate {0}Hz. Recommended: 22050Hz or 16000Hz", SampleRate);
            }

            // Worker threads validation
            if (WorkerThreads < 0)
            {
                throw new PiperException($"Invalid WorkerThreads: {WorkerThreads}. Must be >= 0");
            }
            
            if (WorkerThreads == 0)
            {
                WorkerThreads = Mathf.Max(1, SystemInfo.processorCount - 1);
                PiperLogger.LogInfo("Auto-detected {0} worker threads", WorkerThreads);
            }
            else if (WorkerThreads > 16)
            {
                PiperLogger.LogWarning("WorkerThreads ({0}) exceeds recommended maximum of 16", WorkerThreads);
            }

            // Language validation
            if (string.IsNullOrWhiteSpace(DefaultLanguage))
            {
                throw new PiperException("DefaultLanguage cannot be null or empty");
            }
            
            DefaultLanguage = DefaultLanguage.ToLowerInvariant().Trim();
            if (DefaultLanguage.Length != 2 && DefaultLanguage.Length != 5) // e.g., "ja" or "ja-JP"
            {
                PiperLogger.LogWarning("Unusual language code format: '{0}'. Expected format: 'ja' or 'ja-JP'", DefaultLanguage);
            }

            // Timeout validation
            if (TimeoutMs < 0)
            {
                throw new PiperException($"Invalid TimeoutMs: {TimeoutMs}. Must be >= 0");
            }
            
            if (TimeoutMs > 0 && TimeoutMs < 1000)
            {
                PiperLogger.LogWarning("TimeoutMs ({0}ms) is very short. Recommended minimum: 1000ms", TimeoutMs);
            }

            // Batch size validation
            if (InferenceBatchSize < 1)
            {
                PiperLogger.LogWarning("InferenceBatchSize too small ({0}), setting to 1", InferenceBatchSize);
                InferenceBatchSize = 1;
            }
            else if (InferenceBatchSize > 32)
            {
                PiperLogger.LogWarning("InferenceBatchSize too large ({0}), setting to 32", InferenceBatchSize);
                InferenceBatchSize = 32;
            }

            // RMS level validation
            if (NormalizeAudio)
            {
                if (TargetRMSLevel > 0)
                {
                    PiperLogger.LogWarning("TargetRMSLevel ({0}dB) is positive, setting to 0dB", TargetRMSLevel);
                    TargetRMSLevel = 0f;
                }
                else if (TargetRMSLevel < -40f)
                {
                    PiperLogger.LogWarning("TargetRMSLevel ({0}dB) is too low, setting to -40dB", TargetRMSLevel);
                    TargetRMSLevel = -40f;
                }
            }

            PiperLogger.LogInfo("PiperConfig validated successfully");
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