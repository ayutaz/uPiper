using System;
using System.Collections.Generic;
using UnityEngine;

namespace uPiper.Core
{
    /// <summary>
    /// Main configuration for Piper TTS
    /// </summary>
    [Serializable]
    public class PiperConfig
    {
        #region Constants

        // Constants are now defined in ValidatedPiperConfig.
        // These aliases exist for backward compatibility within this class.
        private const int MinSampleRate = ValidatedPiperConfig.MinSampleRate;
        private const int MaxSampleRate = ValidatedPiperConfig.MaxSampleRate;

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
        /// Optional fallback language for unsupported language segments.
        /// When a detected language has no registered G2P handler, the segment
        /// will be processed using this language's handler instead of being skipped.
        /// Set to null (default) to skip unsupported segments.
        /// </summary>
        [Header("Fallback Settings")]
        [Tooltip("非対応言語セグメントのフォールバック言語（null = スキップ）\n" +
            "検出された言語に対応するG2Pハンドラがない場合、この言語で処理する\n" +
            "--- Fallback language for unsupported segments (null = skip) ---")]
        public string FallbackLanguage;

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
        /// Enable audio synthesis result caching.
        /// When enabled, repeated synthesis with the same text/parameters skips ONNX inference.
        /// </summary>
        [Tooltip("音声合成結果をキャッシュし、同一テキスト・パラメータでの再合成時にONNX推論をスキップする\n" +
            "--- Cache synthesis results to skip inference for repeated text ---")]
        public bool EnableAudioCache = true;

        /// <summary>
        /// Maximum number of audio synthesis cache entries.
        /// Each entry stores float[] audio samples. Memory usage depends on audio length.
        /// </summary>
        [Tooltip("音声合成キャッシュの最大エントリ数（各エントリはfloat[]音声データを保持）\n" +
            "--- Max audio cache entries (each stores float[] audio data) ---")]
        [Range(1, 200)]
        public int MaxAudioCacheEntries = 50;

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
        [Tooltip("沈黙トークンで音素列を分割し、句間に無音を挿入する\n" +
            "有効にすると長文の自然さが向上（句切りで息継ぎ風の間を挿入）\n" +
            "--- Split phoneme sequences at silence tokens ---\n" +
            "Inserts pauses between phrases for more natural long-text speech")]
        public bool EnablePhonemeSilence = false;

        /// <summary>
        /// 沈黙トークンと沈黙秒数の設定文字列
        /// 形式: "phoneme seconds" (カンマ区切りで複数指定可)
        /// 例: "_ 0.5" または "_ 0.5,# 0.3"
        /// </summary>
        [Tooltip("沈黙トークンと無音秒数の指定\n" +
            "形式: \"<音素> <秒数>\" カンマ区切りで複数指定可\n" +
            "例: \"_ 0.5\" (読点で0.5秒) / \"_ 0.5,# 0.3\" (読点0.5秒+句点0.3秒)\n" +
            "--- Silence specification: '<phoneme> <seconds>' (comma-separated) ---\n" +
            "Example: '_ 0.5' or '_ 0.5,# 0.3'")]
        public string PhonemeSilenceSpec = "_ 0.5";

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
        /// ディープコピーを作成する。
        /// ScriptableObject のオリジナルデータを保護するためのランタイムコピー用。
        /// </summary>
        /// <remarks>
        /// MemberwiseClone() によりプリミティブ/string/enum フィールドは自動コピーされる。
        /// フィールド追加時も漏れが生じない。参照型のみ明示的にディープコピーする。
        /// </remarks>
        public PiperConfig Clone()
        {
            var copy = (PiperConfig)MemberwiseClone();
            // 参照型フィールドのディープコピー
            copy.SupportedLanguages = new List<string>(SupportedLanguages ?? new List<string>());
            if (GPUSettings != null)
                copy.GPUSettings = new GPUInferenceSettings { MaxMemoryMB = GPUSettings.MaxMemoryMB };
            return copy;
        }

        /// <summary>
        /// 設定値を検証する（例外スローのみ）。フィールドは一切変更しない。
        /// <para>
        /// クランプ・正規化・自動検出のロジックは <see cref="ValidatedPiperConfig"/> コンストラクタに移動済み。
        /// バリデーション済みの不変設定を取得するには <see cref="ToValidated()"/> を使用すること。
        /// </para>
        /// </summary>
        [Obsolete("Use ToValidated() instead. Validate() no longer modifies fields. Will be removed in v3.0.")]
        public void Validate()
        {
            ValidateThrowOnly();
        }

        /// <summary>
        /// 例外チェックのみ実施する内部メソッド。フィールドは一切変更しない。
        /// </summary>
        private void ValidateThrowOnly()
        {
            if (string.IsNullOrWhiteSpace(DefaultLanguage))
                throw new PiperException("DefaultLanguage cannot be null or empty");

            if (SampleRate < MinSampleRate || SampleRate > MaxSampleRate)
            {
                throw new PiperException(
                    $"Invalid sample rate: {SampleRate}Hz. Must be between {MinSampleRate}-{MaxSampleRate}Hz");
            }

            if (WorkerThreads < 0)
                throw new PiperException($"Invalid WorkerThreads: {WorkerThreads}. Must be >= 0");

            if (TimeoutMs < 0)
                throw new PiperException($"Invalid TimeoutMs: {TimeoutMs}. Must be >= 0");

            if (EnablePhonemeSilence)
            {
                try
                {
                    AudioGeneration.PhonemeSilenceProcessor.Parse(PhonemeSilenceSpec);
                }
                catch (ArgumentException ex)
                {
                    throw new PiperException($"Invalid PhonemeSilenceSpec: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// この PiperConfig を検証し、不変スナップショットとして返す。
        /// PiperConfig のフィールドは一切変更しない（純粋関数）。
        /// バリデーション・クランプ・正規化は ValidatedPiperConfig コンストラクタで実行される。
        /// </summary>
        /// <returns>バリデーション済みの不変設定オブジェクト</returns>
        /// <exception cref="PiperException">設定値が不正な場合</exception>
        public ValidatedPiperConfig ToValidated()
        {
            return new ValidatedPiperConfig(this);
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