using System;
using UnityEngine;
using Unity.InferenceEngine;
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
        /// Unity ModelAsset reference (alternative to ModelPath)
        /// </summary>
        [Tooltip("Unity ModelAsset reference for Sentis")]
        public ModelAsset ModelAsset;

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
        /// Voice synthesis parameters
        /// </summary>
        [Header("Synthesis Parameters")]
        
        /// <summary>
        /// Speech rate multiplier (0.5 = half speed, 2.0 = double speed)
        /// </summary>
        [Range(0.5f, 2.0f)]
        [Tooltip("Speech rate multiplier")]
        public float? SpeechRate;
        
        /// <summary>
        /// Pitch scale multiplier (0.5 = lower pitch, 2.0 = higher pitch)
        /// </summary>
        [Range(0.5f, 2.0f)]
        [Tooltip("Pitch scale multiplier")]
        public float? PitchScale;
        
        /// <summary>
        /// Volume scale multiplier (0.0 = silent, 1.0 = normal)
        /// </summary>
        [Range(0.0f, 2.0f)]
        [Tooltip("Volume scale multiplier")]
        public float? VolumeScale;

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