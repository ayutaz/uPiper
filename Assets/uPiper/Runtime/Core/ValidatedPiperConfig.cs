using System.Collections.Generic;

namespace uPiper.Core
{
    /// <summary>
    /// PiperConfigをValidate()後にスナップショットした不変の設定オブジェクト。
    /// PiperConfig.ToValidated()で取得する。
    /// バリデーション済みの値のみを保持するため、読み取り専用プロパティのみを持つ。
    /// </summary>
    public sealed class ValidatedPiperConfig
    {
        // Language settings
        public string DefaultLanguage { get; }
        public bool AutoDetectLanguage { get; }
        public IReadOnlyList<string> SupportedLanguages { get; }
        public MultiLanguageMode MixedLanguageMode { get; }

        // Performance settings
        public int MaxCacheSizeMB { get; }
        public bool EnablePhonemeCache { get; }
        public int WorkerThreads { get; }
        public bool EnableMultiThreadedInference { get; }
        public int InferenceBatchSize { get; }

        // Inference settings
        public InferenceBackend Backend { get; }
        public bool EnableWarmup { get; }
        public int WarmupIterations { get; }
        public bool AllowFallbackToCPU { get; }
        public GPUInferenceSettings GPUSettings { get; }

        // Audio settings
        public int SampleRate { get; }
        public bool NormalizeAudio { get; }
        public float TargetRMSLevel { get; }

        // Silence settings
        public bool EnablePhonemeSilence { get; }
        public string PhonemeSilenceSpec { get; }
        /// <summary>
        /// EnablePhonemeSilenceがtrueのときのパース済み音素→秒数マップ。
        /// EnablePhonemeSilenceがfalseのときはnull。
        /// PiperTTS._parsedPhonemeSilenceを置き換える。
        /// </summary>
        public IReadOnlyDictionary<string, float> ParsedPhonemeSilence { get; }

        // General settings
        public bool EnableDebugLogging { get; }
        public int TimeoutMs { get; }

        /// <summary>
        /// PiperConfig.ToValidated()からのみ呼ぶ。
        /// Validate()済みのconfigを渡すこと。
        /// </summary>
        internal ValidatedPiperConfig(PiperConfig source)
        {
            DefaultLanguage = source.DefaultLanguage;
            AutoDetectLanguage = source.AutoDetectLanguage;
            SupportedLanguages = source.SupportedLanguages != null
                ? new List<string>(source.SupportedLanguages)
                : (IReadOnlyList<string>)System.Array.Empty<string>();
            MixedLanguageMode = source.MixedLanguageMode;

            MaxCacheSizeMB = source.MaxCacheSizeMB;
            EnablePhonemeCache = source.EnablePhonemeCache;
            WorkerThreads = source.WorkerThreads;
            EnableMultiThreadedInference = source.EnableMultiThreadedInference;
            InferenceBatchSize = source.InferenceBatchSize;

            Backend = source.Backend;
            EnableWarmup = source.EnableWarmup;
            WarmupIterations = source.WarmupIterations;
            AllowFallbackToCPU = source.AllowFallbackToCPU;
            GPUSettings = new GPUInferenceSettings
            {
                MaxMemoryMB = source.GPUSettings != null ? source.GPUSettings.MaxMemoryMB : 512
            };

            SampleRate = source.SampleRate;
            NormalizeAudio = source.NormalizeAudio;
            TargetRMSLevel = source.TargetRMSLevel;

            EnablePhonemeSilence = source.EnablePhonemeSilence;
            PhonemeSilenceSpec = source.PhonemeSilenceSpec;
            ParsedPhonemeSilence = source.EnablePhonemeSilence
                ? AudioGeneration.PhonemeSilenceProcessor.Parse(source.PhonemeSilenceSpec)
                : null;

            EnableDebugLogging = source.EnableDebugLogging;
            TimeoutMs = source.TimeoutMs;
        }
    }
}