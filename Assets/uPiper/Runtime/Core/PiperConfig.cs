using System;
using System.Collections.Generic;
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
        #region Constants

        // Cache size limits
        private const int MinCacheSizeMB = 10;
        private const int MaxCacheSizeMBThreshold = 500;

        // Sample rate bounds
        private const int MinSampleRate = 8000;
        private const int MaxSampleRate = 48000;

        // Worker thread limits
        private const int MaxWorkerThreads = 16;

        // Timeout limits
        private const int MinRecommendedTimeoutMs = 1000;

        // Batch size limits
        private const int MinBatchSize = 1;
        private const int MaxBatchSize = 32;

        // RMS level limits
        private const float MaxRMSLevel = 0f;
        private const float MinRMSLevel = -40f;

        #endregion
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

        /// <summary>
        /// Automatically detect language from input text.
        /// When enabled, text language is detected and the appropriate phonemizer is used.
        /// </summary>
        [Tooltip("Automatically detect language from text (requires multilingual model)")]
        public bool AutoDetectLanguage = false;

        /// <summary>
        /// Supported languages for multilingual mode.
        /// Used when AutoDetectLanguage is true.
        /// </summary>
        [Tooltip("Languages supported by the loaded model (e.g., [\"ja\", \"en\"])")]
        public List<string> SupportedLanguages = new() { "ja", "en" };

        /// <summary>
        /// How to handle mixed-language input text.
        /// </summary>
        [Tooltip("Strategy for handling text that contains multiple languages")]
        public MultiLanguageMode MixedLanguageMode = MultiLanguageMode.SegmentByLanguage;

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

        [Header("Sentence Silence Settings")]

        /// <summary>
        /// 沈黙トークンによる句分割を有効にする
        /// </summary>
        [Tooltip("Split phoneme sequences at silence tokens and insert silence between phrases")]
        public bool EnablePhonemeSilence = false;

        /// <summary>
        /// 沈黙トークンと沈黙秒数の設定文字列
        /// 形式: "phoneme seconds" (カンマ区切りで複数指定可)
        /// 例: "_ 0.5" または "_ 0.5,# 0.3"
        /// </summary>
        [Tooltip("Phoneme silence specification: '<phoneme> <seconds>' (comma-separated)")]
        public string PhonemeSilenceSpec = "_ 0.5";

        /// <summary>
        /// パース済みの沈黙トークンマップ（Validate後に利用可能）
        /// </summary>
        [NonSerialized]
        public Dictionary<string, float> ParsedPhonemeSilence;

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
        /// Enable warmup inference after model initialization.
        /// Reduces first inference latency by ~500-800ms.
        /// </summary>
        [Tooltip("Run dummy inference after initialization to reduce first call latency")]
        public bool EnableWarmup = false;

        /// <summary>
        /// Number of warmup inference iterations.
        /// ORT JIT cache stabilises in 1-2 runs; 2 provides a safety margin.
        /// </summary>
        [Tooltip("Number of warmup iterations (piper-plus default: 2)")]
        [Range(1, 5)]
        public int WarmupIterations = 2;

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
        /// GPU-specific settings
        /// </summary>
        [Tooltip("Settings specific to GPU inference")]
        public GPUInferenceSettings GPUSettings = new();

        /// <summary>
        /// Allow fallback to CPU if GPU fails
        /// </summary>
        [Tooltip("Automatically fallback to CPU if GPU initialization fails")]
        public bool AllowFallbackToCPU = true;

        /// <summary>
        /// Create default configuration
        /// </summary>
        public static PiperConfig CreateDefault()
        {
            var config = new PiperConfig();

            // Apply IL2CPP-specific optimizations
            if (IL2CPP.IL2CPPCompatibility.PlatformSettings.IsIL2CPP)
            {
                config.WorkerThreads = IL2CPP.IL2CPPCompatibility.PlatformSettings.GetRecommendedWorkerThreads();
                config.MaxCacheSizeMB = IL2CPP.IL2CPPCompatibility.PlatformSettings.GetRecommendedCacheSizeMB();
            }

            return config;
        }

        /// <summary>
        /// Validate configuration
        /// </summary>
        public void Validate()
        {
            // Cache size validation
            if (MaxCacheSizeMB < MinCacheSizeMB)
            {
                PiperLogger.LogWarning("MaxCacheSizeMB too small ({0}MB), setting to minimum {1}MB", MaxCacheSizeMB, MinCacheSizeMB);
                MaxCacheSizeMB = MinCacheSizeMB;
            }
            else if (MaxCacheSizeMB > MaxCacheSizeMBThreshold)
            {
                PiperLogger.LogWarning("MaxCacheSizeMB too large ({0}MB), setting to maximum {1}MB", MaxCacheSizeMB, MaxCacheSizeMBThreshold);
                MaxCacheSizeMB = MaxCacheSizeMBThreshold;
            }

            // Sample rate validation
            if (SampleRate < MinSampleRate || SampleRate > MaxSampleRate)
            {
                throw new PiperException($"Invalid sample rate: {SampleRate}Hz. Must be between {MinSampleRate}-{MaxSampleRate}Hz");
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
            else if (WorkerThreads > MaxWorkerThreads)
            {
                PiperLogger.LogWarning("WorkerThreads ({0}) exceeds recommended maximum of {1}", WorkerThreads, MaxWorkerThreads);
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

            if (TimeoutMs > 0 && TimeoutMs < MinRecommendedTimeoutMs)
            {
                PiperLogger.LogWarning("TimeoutMs ({0}ms) is very short. Recommended minimum: {1}ms", TimeoutMs, MinRecommendedTimeoutMs);
            }

            // Batch size validation
            if (InferenceBatchSize < MinBatchSize)
            {
                PiperLogger.LogWarning("InferenceBatchSize too small ({0}), setting to {1}", InferenceBatchSize, MinBatchSize);
                InferenceBatchSize = MinBatchSize;
            }
            else if (InferenceBatchSize > MaxBatchSize)
            {
                PiperLogger.LogWarning("InferenceBatchSize too large ({0}), setting to {1}", InferenceBatchSize, MaxBatchSize);
                InferenceBatchSize = MaxBatchSize;
            }

            // RMS level validation
            if (NormalizeAudio)
            {
                if (TargetRMSLevel > MaxRMSLevel)
                {
                    PiperLogger.LogWarning("TargetRMSLevel ({0}dB) is positive, setting to {1}dB", TargetRMSLevel, MaxRMSLevel);
                    TargetRMSLevel = MaxRMSLevel;
                }
                else if (TargetRMSLevel < MinRMSLevel)
                {
                    PiperLogger.LogWarning("TargetRMSLevel ({0}dB) is too low, setting to {1}dB", TargetRMSLevel, MinRMSLevel);
                    TargetRMSLevel = MinRMSLevel;
                }
            }

            // Warmup iterations validation
            if (EnableWarmup && WarmupIterations < 1)
            {
                PiperLogger.LogWarning("WarmupIterations ({0}) is less than 1, setting to 1", WarmupIterations);
                WarmupIterations = 1;
            }

            // Phoneme silence validation
            if (EnablePhonemeSilence)
            {
                try
                {
                    ParsedPhonemeSilence = AudioGeneration.PhonemeSilenceProcessor.Parse(PhonemeSilenceSpec);
                }
                catch (ArgumentException ex)
                {
                    throw new PiperException($"Invalid PhonemeSilenceSpec: {ex.Message}", ex);
                }
            }
            else
            {
                ParsedPhonemeSilence = null;
            }

            // GPU settings validation
            GPUSettings?.Validate();

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

    /// <summary>
    /// Strategy for handling mixed-language text input.
    /// </summary>
    public enum MultiLanguageMode
    {
        /// <summary>
        /// Automatically segment text by detected language and process each segment separately.
        /// </summary>
        SegmentByLanguage = 0,

        /// <summary>
        /// Force all text to be processed as DefaultLanguage, ignoring language detection.
        /// </summary>
        ForceDefault = 1,

        /// <summary>
        /// Detect the dominant language of the entire text and process it as a single language.
        /// </summary>
        AutoDetectWhole = 2,

        /// <summary>
        /// Use the language specified in the current VoiceConfig as the primary language.
        /// </summary>
        VoiceConfigPrimary = 3
    }
}