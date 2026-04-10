using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace uPiper.Core
{
    using Entry = InitializationValidationResult.Entry;

    /// <summary>
    /// Runs up-front validation at the start of InitializeAsync / InitializeWithInferenceAsync.
    /// </summary>
    internal static class InitializationValidator
    {
        /// <summary>
        /// Validation for the lightweight InitializeAsync path (no model specified).
        /// </summary>
        internal static InitializationValidationResult ValidateForInitialize(
            PiperConfig config)
        {
            var entries = new List<Entry>();

            ValidateRuntimeEnvironment(entries);
            ValidateStreamingAssetsBase(entries);
            ValidateDictionaryFiles(entries);
            ValidatePlatformSpecific(entries);

            return new InitializationValidationResult(entries);
        }

        /// <summary>
        /// Validation for InitializeWithInferenceAsync (full initialization with model).
        /// </summary>
        internal static InitializationValidationResult ValidateForInference(
            PiperConfig config,
            object modelAsset,
            PiperVoiceConfig voiceConfig)
        {
            var entries = new List<Entry>();

            ValidateRuntimeEnvironment(entries);
            ValidateModelAsset(entries, modelAsset);
            ValidateVoiceConfig(entries, voiceConfig);
            ValidatePhonemeIdMap(entries, voiceConfig);
            ValidateStreamingAssetsBase(entries);
            ValidateDictionaryFiles(entries);
            ValidatePlatformSpecific(entries);

            return new InitializationValidationResult(entries);
        }

        private static void ValidateRuntimeEnvironment(List<Entry> entries)
        {
            try
            {
                var unityVersion = Application.unityVersion;
                if (!string.IsNullOrEmpty(unityVersion) && unityVersion.CompareTo("2022.3") < 0)
                {
                    entries.Add(new Entry(
                        ValidationCategory.RuntimeEnvironment,
                        ValidationSeverity.Warning,
                        $"Unity {unityVersion} detected. uPiper is tested with Unity 2022.3+.",
                        "Upgrade to Unity 2022.3 LTS or later for full compatibility."));
                }
            }
            catch (Exception ex)
            {
                entries.Add(new Entry(
                    ValidationCategory.RuntimeEnvironment,
                    ValidationSeverity.Warning,
                    $"Could not verify runtime environment: {ex.Message}",
                    "This is typically harmless in test environments."));
            }
        }

        private static void ValidateModelAsset(List<Entry> entries, object modelAsset)
        {
            if (modelAsset == null)
            {
                entries.Add(new Entry(
                    ValidationCategory.Model,
                    ValidationSeverity.Error,
                    "Model asset is null.",
                    "Pass a valid ModelAsset to InitializeWithInferenceAsync(). " +
                    "Load it via Resources.Load<ModelAsset>(\"Models/your-model\")."));
            }
        }

        private static void ValidateVoiceConfig(List<Entry> entries, PiperVoiceConfig voiceConfig)
        {
            if (voiceConfig == null)
            {
                entries.Add(new Entry(
                    ValidationCategory.VoiceConfig,
                    ValidationSeverity.Error,
                    "Voice configuration is null.",
                    "Create a PiperVoiceConfig with VoiceId, ModelPath, and Language set."));
                return;
            }

            if (string.IsNullOrEmpty(voiceConfig.VoiceId))
            {
                entries.Add(new Entry(
                    ValidationCategory.VoiceConfig,
                    ValidationSeverity.Warning,
                    "VoiceId is empty.",
                    "Set PiperVoiceConfig.VoiceId to a unique identifier."));
            }

            if (string.IsNullOrEmpty(voiceConfig.Language))
            {
                entries.Add(new Entry(
                    ValidationCategory.VoiceConfig,
                    ValidationSeverity.Warning,
                    "Language is not set in VoiceConfig.",
                    "Set PiperVoiceConfig.Language to a valid language code (e.g., 'ja', 'en')."));
            }
        }

        private static void ValidatePhonemeIdMap(List<Entry> entries, PiperVoiceConfig voiceConfig)
        {
            if (voiceConfig?.PhonemeIdMap == null)
            {
                entries.Add(new Entry(
                    ValidationCategory.PhonemeIdMap,
                    ValidationSeverity.Error,
                    "PhonemeIdMap is null. Phoneme encoding will fail.",
                    "Load the model's .onnx.json config and set PiperVoiceConfig.PhonemeIdMap."));
                return;
            }

            var map = voiceConfig.PhonemeIdMap;

            var requiredTokens = new[] { "_", "^", "$" };
            var missing = new List<string>();
            foreach (var t in requiredTokens)
            {
                if (!map.ContainsKey(t))
                    missing.Add(t);
            }

            if (missing.Count > 0)
            {
                entries.Add(new Entry(
                    ValidationCategory.PhonemeIdMap,
                    ValidationSeverity.Error,
                    $"PhonemeIdMap is missing required tokens: {string.Join(", ", missing)}.",
                    "Ensure '_' (PAD), '^' (BOS), and '$' (EOS) are in the phoneme_id_map."));
            }

            if (map.Count < 10)
            {
                entries.Add(new Entry(
                    ValidationCategory.PhonemeIdMap,
                    ValidationSeverity.Warning,
                    $"PhonemeIdMap has only {map.Count} entries.",
                    "A typical model has 50+ phoneme mappings. " +
                    "Verify the model config JSON was loaded correctly."));
            }
        }

        private static void ValidateStreamingAssetsBase(List<Entry> entries)
        {
            try
            {
                var path = Path.Combine(Application.streamingAssetsPath, "uPiper");
                if (!Directory.Exists(path))
                {
                    entries.Add(new Entry(
                        ValidationCategory.StreamingAssets,
                        ValidationSeverity.Warning,
                        $"StreamingAssets/uPiper directory not found at: {path}",
                        "Ensure uPiper StreamingAssets are imported. " +
                        "Dictionary and language profile files should be in StreamingAssets/uPiper/."));
                }
            }
            catch (Exception)
            {
                // WebGL etc. may not support Directory.Exists
            }
        }

        private static void ValidateDictionaryFiles(List<Entry> entries)
        {
            try
            {
                var dictPath = Path.Combine(
                    Application.streamingAssetsPath, "uPiper", "Dictionaries");
                if (Directory.Exists(dictPath))
                {
                    var files = Directory.GetFiles(dictPath, "*.json");
                    if (files.Length == 0)
                    {
                        entries.Add(new Entry(
                            ValidationCategory.Dictionary,
                            ValidationSeverity.Warning,
                            "No dictionary JSON files found in StreamingAssets/uPiper/Dictionaries/.",
                            "Custom dictionary will not be available. " +
                            "This is fine if you don't need pronunciation overrides."));
                    }
                }
            }
            catch (Exception)
            {
                // WebGL etc.
            }
        }

        private static void ValidatePlatformSpecific(List<Entry> entries)
        {
#if UNITY_IOS && !UNITY_EDITOR
            entries.Add(new Entry(
                ValidationCategory.Platform,
                ValidationSeverity.Warning,
                "iOS platform detected. AVAudioSession will be auto-initialized.",
                "If audio is silent, check IOSAudioSessionHelper initialization logs."));
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
            entries.Add(new Entry(
                ValidationCategory.Platform,
                ValidationSeverity.Warning,
                "WebGL platform detected. AudioContext requires user interaction.",
                "Ensure WebGLInteractionGate is used before audio playback."));
#endif
        }
    }
}