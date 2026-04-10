using System;
using System.Collections.Generic;
using UnityEngine;
using uPiper.Core.Logging;

namespace uPiper.Core
{
    /// <summary>
    /// Configuration for a specific voice model
    /// </summary>
    [Serializable]
    public class PiperVoiceConfig
    {
        /// <summary>
        /// Unique identifier for the voice
        /// </summary>
        [Tooltip("Unique voice identifier")]
        public string VoiceId;

        /// <summary>
        /// Display name of the voice
        /// </summary>
        [Tooltip("Human-readable voice name")]
        public string DisplayName;

        /// <summary>
        /// Language code (ISO 639-1)
        /// </summary>
        [Tooltip("Language code (e.g., 'ja', 'en')")]
        public string Language;

        /// <summary>
        /// Path to the ONNX model file
        /// </summary>
        [Tooltip("Path to the ONNX model file")]
        public string ModelPath;

        /// <summary>
        /// Path to the model configuration JSON
        /// </summary>
        [Tooltip("Path to the model configuration JSON")]
        public string ConfigPath;

        /// <summary>
        /// Sample rate of the model
        /// </summary>
        [Tooltip("Model's native sample rate")]
        public int SampleRate = 22050;

        /// <summary>
        /// Voice characteristics
        /// </summary>
        [Header("Voice Characteristics")]

        /// <summary>
        /// Gender of the voice
        /// </summary>
        [Tooltip("Voice gender")]
        public VoiceGender Gender = VoiceGender.Neutral;

        /// <summary>
        /// Age group of the voice
        /// </summary>
        [Tooltip("Voice age group")]
        public VoiceAge AgeGroup = VoiceAge.Adult;

        /// <summary>
        /// Speaking style
        /// </summary>
        [Tooltip("Default speaking style")]
        public SpeakingStyle Style = SpeakingStyle.Normal;

        /// <summary>
        /// Model quality level
        /// </summary>
        [Tooltip("Model quality/size")]
        public ModelQuality Quality = ModelQuality.Medium;

        /// <summary>
        /// Additional metadata
        /// </summary>
        [Header("Metadata")]

        /// <summary>
        /// Model version
        /// </summary>
        [Tooltip("Model version string")]
        public string Version;

        /// <summary>
        /// Model size in MB
        /// </summary>
        [Tooltip("Approximate model size in MB")]
        public float ModelSizeMB;

        /// <summary>
        /// Whether this voice supports streaming
        /// </summary>
        [Tooltip("Whether streaming is supported")]
        public bool SupportsStreaming = true;

        /// <summary>
        /// Number of speakers in the model
        /// </summary>
        [Tooltip("Number of speakers supported by the model")]
        public int NumSpeakers = 1;

        /// <summary>
        /// Number of languages supported by the model
        /// </summary>
        [Tooltip("Number of languages supported by the model")]
        public int NumLanguages = 1;

        /// <summary>
        /// Phoneme to ID mapping dictionary.
        /// Each phoneme maps to an array of integer IDs (piper-plus compatible).
        /// PhonemeEncoder extracts ids[0] for internal encoding;
        /// PhonemeSilenceProcessor uses ids[^1] as the silence trigger (piper-plus convention).
        /// </summary>
        [HideInInspector]
        public Dictionary<string, int[]> PhonemeIdMap;

        /// <summary>
        /// Language code to ID mapping (e.g., {"ja": 0, "en": 1})
        /// </summary>
        [HideInInspector]
        public Dictionary<string, int> LanguageIdMap;

        /// <summary>
        /// Speaker name to ID mapping
        /// </summary>
        [HideInInspector]
        public Dictionary<string, int> SpeakerIdMap;

        /// <summary>
        /// Phoneme type for encoding (e.g., "espeak", "openjtalk")
        /// Determines whether PAD tokens are inserted between phonemes
        /// </summary>
        [HideInInspector]
        public string PhonemeType;

        /// <summary>
        /// Inference parameters
        /// </summary>
        [Header("Inference Parameters")]

        /// <summary>
        /// Noise scale for inference (VITS noise_scale).
        /// Controls phoneme-level variation in pitch and timbre.
        /// Higher values produce more expressive but less stable speech.
        /// </summary>
        [Tooltip("音素レベルの声質・ピッチ変動 (VITS noise_scale)\n" +
            "0.0 = 変動なし（単調・ロボット的）、0.667 = デフォルト（自然な変動）、1.0+ = 表現豊か（不安定になりうる）\n" +
            "--- Phoneme-level pitch/timbre variation ---\n" +
            "0.0 = no variation (flat/robotic), 0.667 = default (natural), 1.0+ = expressive (may be unstable)")]
        [Range(0.0f, 2.0f)]
        public float NoiseScale = 0.667f;

        /// <summary>
        /// Length scale for inference (VITS length_scale).
        /// Controls overall speaking speed by scaling phoneme durations.
        /// </summary>
        [Tooltip("話速スケール (VITS length_scale)\n" +
            "1.0 = 標準速度、0.5 = 2倍速、2.0 = 半分の速度\n" +
            "推奨範囲: 0.7〜1.3（極端な値は音質劣化の原因）\n" +
            "--- Speaking speed (phoneme duration scale) ---\n" +
            "1.0 = normal, <1.0 = faster, >1.0 = slower. Recommended: 0.7-1.3")]
        [Range(0.1f, 2.0f)]
        public float LengthScale = 1.0f;

        /// <summary>
        /// Noise W parameter for inference (VITS noise_w).
        /// Controls duration-level variation (phoneme length randomness).
        /// Unlike NoiseScale (pitch/timbre), this affects timing/rhythm.
        /// </summary>
        [Tooltip("音素の発話時間の揺らぎ (VITS noise_w)\n" +
            "0.0 = 均一なリズム（機械的）、0.8 = デフォルト（自然なリズム）、1.0+ = リズム変動大\n" +
            "※ NoiseScale（声質変動）とは独立。リズム・間の自然さを制御\n" +
            "--- Phoneme duration variation (rhythm/timing) ---\n" +
            "0.0 = uniform rhythm, 0.8 = default (natural), 1.0+ = more rhythmic variation.\n" +
            "Independent of NoiseScale (pitch/timbre); controls timing naturalness")]
        [Range(0.0f, 2.0f)]
        public float NoiseW = 0.8f;

        /// <summary>
        /// Key for voice identification (alias for VoiceId)
        /// </summary>
        public string Key => VoiceId;

        /// <summary>
        /// Create a voice configuration from model paths
        /// </summary>
        public static PiperVoiceConfig FromModelPath(string modelPath, string configPath)
        {
            var config = new PiperVoiceConfig
            {
                ModelPath = modelPath,
                ConfigPath = configPath
            };

            // Extract voice ID from filename
            var fileName = System.IO.Path.GetFileNameWithoutExtension(modelPath);
            config.VoiceId = fileName;

            // Parse language from filename (e.g., "multilingual-test-medium" -> "multilingual")
            if (fileName.Contains("_") || fileName.Contains("-"))
            {
                var parts = fileName.Split(new[] { '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    config.Language = parts[0].ToLower();
                }
            }

            // Set display name
            config.DisplayName = fileName.Replace("_", " ").Replace("-", " ");

            return config;
        }

        /// <summary>
        /// Validate the configuration
        /// </summary>
        public bool Validate()
        {
            if (string.IsNullOrEmpty(VoiceId))
            {
                PiperLogger.LogError("Voice ID is required");
                return false;
            }

            if (string.IsNullOrEmpty(ModelPath))
            {
                PiperLogger.LogError("Model path is required");
                return false;
            }

            if (string.IsNullOrEmpty(Language))
            {
                PiperLogger.LogError("Language is required");
                return false;
            }

            if (PhonemeIdMap != null)
            {
                foreach (var kvp in PhonemeIdMap)
                {
                    if (kvp.Value == null || kvp.Value.Length == 0)
                    {
                        PiperLogger.LogWarning(
                            $"PhonemeIdMap entry '{kvp.Key}' has empty ID array. " +
                            "It will be skipped during encoding.");
                    }
                }
            }

            return true;
        }

        public override string ToString()
        {
            return $"{DisplayName} ({Language})";
        }
    }

    /// <summary>
    /// Voice gender
    /// </summary>
    public enum VoiceGender
    {
        Neutral,
        Male,
        Female
    }

    /// <summary>
    /// Voice age group
    /// </summary>
    public enum VoiceAge
    {
        Child,
        Teen,
        Adult,
        Senior
    }

    /// <summary>
    /// Speaking style
    /// </summary>
    public enum SpeakingStyle
    {
        Normal,
        Happy,
        Sad,
        Angry,
        Fearful,
        Surprised,
        Disgusted,
        Neutral
    }

    /// <summary>
    /// Model quality level
    /// </summary>
    public enum ModelQuality
    {
        Low,      // ~10MB, faster but lower quality
        Medium,   // ~50MB, balanced
        High,     // ~100MB, high quality
        Ultra     // ~200MB+, highest quality
    }
}