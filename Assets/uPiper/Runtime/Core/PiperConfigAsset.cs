using System.Collections.Generic;
using UnityEngine;

namespace uPiper.Core
{
    /// <summary>
    /// PiperConfig の ScriptableObject ラッパー。
    /// プロジェクトアセットとして永続化し、Inspector で編集可能にする。
    /// </summary>
    [CreateAssetMenu(
        fileName = "PiperConfigAsset",
        menuName = "uPiper/Config Asset",
        order = 100)]
    public sealed class PiperConfigAsset : ScriptableObject
    {
        [SerializeField]
        private PiperConfig _config = new();

        /// <summary>内部 PiperConfig への読み取り専用アクセス。</summary>
        public PiperConfig Config => _config;

        /// <summary>バリデーション済み不変スナップショットを返す。</summary>
        public ValidatedPiperConfig ToValidated()
        {
            return _config.ToValidated();
        }

        /// <summary>ランタイム用ディープコピーを返す。</summary>
        public PiperConfig CreateRuntimeCopy()
        {
            var copy = new PiperConfig
            {
                // General Settings
                EnableDebugLogging = _config.EnableDebugLogging,
                DefaultLanguage = _config.DefaultLanguage,
                AutoDetectLanguage = _config.AutoDetectLanguage,

                // Fallback Settings
                FallbackLanguage = _config.FallbackLanguage,

                // Multilingual Settings
                SupportedLanguages = _config.SupportedLanguages != null
                    ? new List<string>(_config.SupportedLanguages)
                    : new List<string>(),
                MixedLanguageMode = _config.MixedLanguageMode,

                // Performance Settings
                MaxCacheSizeMB = _config.MaxCacheSizeMB,
                EnablePhonemeCache = _config.EnablePhonemeCache,
                WorkerThreads = _config.WorkerThreads,
                Backend = _config.Backend,

                // Sentence Silence Settings
                EnablePhonemeSilence = _config.EnablePhonemeSilence,
                PhonemeSilenceSpec = _config.PhonemeSilenceSpec,

                // Audio Settings
                SampleRate = _config.SampleRate,
                NormalizeAudio = _config.NormalizeAudio,
                TargetRMSLevel = _config.TargetRMSLevel,

                // Advanced Settings
                EnableWarmup = _config.EnableWarmup,
                WarmupIterations = _config.WarmupIterations,
                TimeoutMs = _config.TimeoutMs,
                EnableMultiThreadedInference = _config.EnableMultiThreadedInference,
                InferenceBatchSize = _config.InferenceBatchSize,
                GPUSettings = new GPUInferenceSettings
                {
                    MaxMemoryMB = _config.GPUSettings != null
                        ? _config.GPUSettings.MaxMemoryMB
                        : 512
                },
                AllowFallbackToCPU = _config.AllowFallbackToCPU
            };

            return copy;
        }

        private void Reset()
        {
            _config = PiperConfig.CreateDefault();
        }
    }
}