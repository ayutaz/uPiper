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
        /// Phoneme to ID mapping dictionary
        /// </summary>
        [HideInInspector]
        public Dictionary<string, int> PhonemeIdMap;

        /// <summary>
        /// Inference parameters
        /// </summary>
        [Header("Inference Parameters")]

        /// <summary>
        /// Noise scale for inference
        /// </summary>
        [Tooltip("Controls the randomness/variation in the generated speech")]
        [Range(0.0f, 2.0f)]
        public float NoiseScale = 0.667f;

        /// <summary>
        /// Length scale for inference
        /// </summary>
        [Tooltip("Controls the speaking speed (1.0 = normal, <1.0 = faster, >1.0 = slower)")]
        [Range(0.1f, 2.0f)]
        public float LengthScale = 1.0f;

        /// <summary>
        /// Noise W parameter for inference
        /// </summary>
        [Tooltip("Additional noise parameter for variation")]
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

            // Parse language from filename (e.g., "ja_JP-test-medium" -> "ja")
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