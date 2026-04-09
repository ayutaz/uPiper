using System;
using System.Collections.Generic;
using UnityEngine;
using uPiper.Core.Logging;

namespace uPiper.Core
{
    // ── Language ──────────────────────────────────────────
    /// <summary>
    /// 言語関連の設定をグループ化した不変構造体。
    /// </summary>
    /// <remarks>
    /// Do not use default constructor. Always create via ValidatedPiperConfig constructor
    /// which ensures proper validation and clamping.
    /// </remarks>
    public readonly record struct LanguageSettings(
        string DefaultLanguage,
        bool AutoDetectLanguage,
        IReadOnlyList<string> SupportedLanguages,
        MultiLanguageMode MixedLanguageMode);

    // ── Performance ──────────────────────────────────────
    /// <summary>
    /// パフォーマンス関連の設定をグループ化した不変構造体。
    /// </summary>
    /// <remarks>
    /// Do not use default constructor. Always create via ValidatedPiperConfig constructor
    /// which ensures proper validation and clamping.
    /// </remarks>
    public readonly record struct PerformanceSettings(
        int MaxCacheSizeMB,
        bool EnablePhonemeCache,
        int WorkerThreads,
        bool EnableMultiThreadedInference,
        int InferenceBatchSize);

    // ── Inference ────────────────────────────────────────
    /// <summary>
    /// 推論バックエンド関連の設定をグループ化した不変構造体。
    /// </summary>
    /// <remarks>
    /// Do not use default constructor. Always create via ValidatedPiperConfig constructor
    /// which ensures proper validation and clamping.
    /// </remarks>
    public readonly record struct InferenceSettings(
        InferenceBackend Backend,
        bool EnableWarmup,
        int WarmupIterations,
        bool AllowFallbackToCPU,
        GPUInferenceSettings GPUSettings);

    // ── Audio ────────────────────────────────────────────
    /// <summary>
    /// 音声出力関連の設定をグループ化した不変構造体。
    /// </summary>
    /// <remarks>
    /// Do not use default constructor. Always create via ValidatedPiperConfig constructor
    /// which ensures proper validation and clamping.
    /// </remarks>
    public readonly record struct PiperAudioSettings(
        int SampleRate,
        bool NormalizeAudio,
        float TargetRMSLevel);

    // ── Silence ──────────────────────────────────────────
    /// <summary>
    /// 沈黙句分割関連の設定をグループ化した不変構造体。
    /// </summary>
    /// <remarks>
    /// Do not use default constructor. Always create via ValidatedPiperConfig constructor
    /// which ensures proper validation and clamping.
    /// </remarks>
    public readonly record struct SilenceSettings(
        bool EnablePhonemeSilence,
        string PhonemeSilenceSpec,
        IReadOnlyDictionary<string, float> ParsedPhonemeSilence);

    // ── General ──────────────────────────────────────────
    /// <summary>
    /// 汎用設定をグループ化した不変構造体。
    /// </summary>
    /// <remarks>
    /// Do not use default constructor. Always create via ValidatedPiperConfig constructor
    /// which ensures proper validation and clamping.
    /// </remarks>
    public readonly record struct GeneralSettings(
        bool EnableDebugLogging,
        int TimeoutMs);

    /// <summary>
    /// PiperConfigを検証・クランプ・正規化した不変の設定オブジェクト。
    /// PiperConfig.ToValidated()で取得する。
    /// バリデーション済みの値のみを保持するため、読み取り専用プロパティのみを持つ。
    /// 6つのネスト record struct（Language, Performance, Inference, Audio, Silence, General）に分類。
    /// PiperConfig のフィールドは一切変更しない（純粋関数）。
    /// </summary>
    public sealed class ValidatedPiperConfig : IPiperConfigReadOnly
    {
        #region Constants

        // Cache size limits
        internal const int MinCacheSizeMB = 10;
        internal const int MaxCacheSizeMBThreshold = 500;

        // Sample rate bounds
        internal const int MinSampleRate = 8000;
        internal const int MaxSampleRate = 48000;

        // Worker thread limits
        internal const int MaxWorkerThreads = 16;

        // Timeout limits
        internal const int MinRecommendedTimeoutMs = 1000;

        // Batch size limits
        internal const int MinBatchSize = 1;
        internal const int MaxBatchSize = 32;

        // RMS level limits
        internal const float MaxRMSLevel = 0f;
        internal const float MinRMSLevel = -40f;

        // GPU memory limits
        internal const int MinGPUMemoryMB = 128;
        internal const int MaxGPUMemoryMB = 2048;

        #endregion

        /// <summary>言語関連の設定</summary>
        public LanguageSettings Language { get; }

        /// <summary>パフォーマンス関連の設定</summary>
        public PerformanceSettings Performance { get; }

        /// <summary>推論バックエンド関連の設定</summary>
        public InferenceSettings Inference { get; }

        /// <summary>音声出力関連の設定</summary>
        public PiperAudioSettings Audio { get; }

        /// <summary>沈黙句分割関連の設定</summary>
        public SilenceSettings Silence { get; }

        /// <summary>汎用設定</summary>
        public GeneralSettings General { get; }

        /// <summary>
        /// PiperConfig.ToValidated()からのみ呼ぶ。
        /// バリデーション（例外スロー）+ クランプ・正規化を一括実行する。
        /// source の PiperConfig フィールドは一切変更しない。
        /// </summary>
        internal ValidatedPiperConfig(PiperConfig source)
        {
            // ── バリデーション (例外スロー) ──────────────────
            if (string.IsNullOrWhiteSpace(source.DefaultLanguage))
                throw new PiperException("DefaultLanguage cannot be null or empty");

            if (source.SampleRate < MinSampleRate || source.SampleRate > MaxSampleRate)
            {
                throw new PiperException(
                    $"Invalid sample rate: {source.SampleRate}Hz. Must be between {MinSampleRate}-{MaxSampleRate}Hz");
            }

            if (source.WorkerThreads < 0)
                throw new PiperException($"Invalid WorkerThreads: {source.WorkerThreads}. Must be >= 0");

            if (source.TimeoutMs < 0)
                throw new PiperException($"Invalid TimeoutMs: {source.TimeoutMs}. Must be >= 0");

            IReadOnlyDictionary<string, float> parsedPhonemeSilence = null;
            if (source.EnablePhonemeSilence)
            {
                try
                {
                    parsedPhonemeSilence =
                        AudioGeneration.PhonemeSilenceProcessor.Parse(source.PhonemeSilenceSpec);
                }
                catch (ArgumentException ex)
                {
                    throw new PiperException($"Invalid PhonemeSilenceSpec: {ex.Message}", ex);
                }
            }

            // ── ネスト record 構築（正規化・クランプ込み） ──

            Language = new LanguageSettings(
                DefaultLanguage: source.DefaultLanguage.ToLowerInvariant().Trim(),
                AutoDetectLanguage: source.AutoDetectLanguage,
                SupportedLanguages: source.SupportedLanguages != null
                    ? new List<string>(source.SupportedLanguages)
                    : (IReadOnlyList<string>)Array.Empty<string>(),
                MixedLanguageMode: source.MixedLanguageMode);

            Performance = new PerformanceSettings(
                MaxCacheSizeMB: Mathf.Clamp(source.MaxCacheSizeMB, MinCacheSizeMB, MaxCacheSizeMBThreshold),
                EnablePhonemeCache: source.EnablePhonemeCache,
                WorkerThreads: source.WorkerThreads == 0
                    ? Mathf.Max(1, SystemInfo.processorCount - 1)
                    : Mathf.Clamp(source.WorkerThreads, 1, MaxWorkerThreads),
                EnableMultiThreadedInference: source.EnableMultiThreadedInference,
                InferenceBatchSize: Mathf.Clamp(source.InferenceBatchSize, MinBatchSize, MaxBatchSize));

            Inference = new InferenceSettings(
                Backend: source.Backend,
                EnableWarmup: source.EnableWarmup,
                WarmupIterations: source.EnableWarmup && source.WarmupIterations < 1
                    ? 1
                    : source.WarmupIterations,
                AllowFallbackToCPU: source.AllowFallbackToCPU,
                GPUSettings: new GPUInferenceSettings
                {
                    MaxMemoryMB = source.GPUSettings != null
                        ? Mathf.Clamp(source.GPUSettings.MaxMemoryMB, MinGPUMemoryMB, MaxGPUMemoryMB)
                        : 512
                });

            Audio = new PiperAudioSettings(
                SampleRate: source.SampleRate,
                NormalizeAudio: source.NormalizeAudio,
                TargetRMSLevel: source.NormalizeAudio
                    ? Mathf.Clamp(source.TargetRMSLevel, MinRMSLevel, MaxRMSLevel)
                    : source.TargetRMSLevel);

            Silence = new SilenceSettings(
                EnablePhonemeSilence: source.EnablePhonemeSilence,
                PhonemeSilenceSpec: source.PhonemeSilenceSpec,
                ParsedPhonemeSilence: parsedPhonemeSilence);

            General = new GeneralSettings(
                EnableDebugLogging: source.EnableDebugLogging,
                TimeoutMs: source.TimeoutMs);

            // ── 警告ログ (source を変更せず、クランプ発動時にログ出力) ──

            if (source.MaxCacheSizeMB < MinCacheSizeMB)
            {
                PiperLogger.LogWarning(
                    "MaxCacheSizeMB too small ({0}MB), clamped to minimum {1}MB",
                    source.MaxCacheSizeMB, MinCacheSizeMB);
            }
            else if (source.MaxCacheSizeMB > MaxCacheSizeMBThreshold)
            {
                PiperLogger.LogWarning(
                    "MaxCacheSizeMB too large ({0}MB), clamped to maximum {1}MB",
                    source.MaxCacheSizeMB, MaxCacheSizeMBThreshold);
            }

            if (source.SampleRate != 16000 && source.SampleRate != 22050
                && source.SampleRate != 44100 && source.SampleRate != 48000)
            {
                PiperLogger.LogWarning(
                    "Non-standard sample rate {0}Hz. Recommended: 22050Hz or 16000Hz", source.SampleRate);
            }

            if (source.WorkerThreads == 0)
            {
                PiperLogger.LogInfo(
                    "Auto-detected {0} worker threads", Performance.WorkerThreads);
            }
            else if (source.WorkerThreads > MaxWorkerThreads)
            {
                PiperLogger.LogWarning(
                    "WorkerThreads ({0}) exceeds recommended maximum of {1}",
                    source.WorkerThreads, MaxWorkerThreads);
            }

            var normalizedLang = source.DefaultLanguage.ToLowerInvariant().Trim();
            if (normalizedLang.Length != 2 && normalizedLang.Length != 5)
            {
                PiperLogger.LogWarning(
                    "Unusual language code format: '{0}'. Expected format: 'ja' or 'ja-JP'",
                    normalizedLang);
            }

            if (source.TimeoutMs > 0 && source.TimeoutMs < MinRecommendedTimeoutMs)
            {
                PiperLogger.LogWarning(
                    "TimeoutMs ({0}ms) is very short. Recommended minimum: {1}ms",
                    source.TimeoutMs, MinRecommendedTimeoutMs);
            }

            if (source.InferenceBatchSize < MinBatchSize)
            {
                PiperLogger.LogWarning(
                    "InferenceBatchSize too small ({0}), clamped to {1}",
                    source.InferenceBatchSize, MinBatchSize);
            }
            else if (source.InferenceBatchSize > MaxBatchSize)
            {
                PiperLogger.LogWarning(
                    "InferenceBatchSize too large ({0}), clamped to {1}",
                    source.InferenceBatchSize, MaxBatchSize);
            }

            if (source.NormalizeAudio)
            {
                if (source.TargetRMSLevel > MaxRMSLevel)
                {
                    PiperLogger.LogWarning(
                        "TargetRMSLevel ({0}dB) is positive, clamped to {1}dB",
                        source.TargetRMSLevel, MaxRMSLevel);
                }
                else if (source.TargetRMSLevel < MinRMSLevel)
                {
                    PiperLogger.LogWarning(
                        "TargetRMSLevel ({0}dB) is too low, clamped to {1}dB",
                        source.TargetRMSLevel, MinRMSLevel);
                }
            }

            if (source.EnableWarmup && source.WarmupIterations < 1)
            {
                PiperLogger.LogWarning(
                    "WarmupIterations ({0}) is less than 1, clamped to 1",
                    source.WarmupIterations);
            }

            PiperLogger.LogInfo("PiperConfig validated successfully");
        }
    }
}