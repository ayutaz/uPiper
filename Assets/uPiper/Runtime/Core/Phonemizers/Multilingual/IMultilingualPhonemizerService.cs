using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using uPiper.Core.Phonemizers.Backend;

namespace uPiper.Core.Phonemizers.Multilingual
{
    /// <summary>
    /// Interface for multilingual phonemization services
    /// </summary>
    public interface IMultilingualPhonemizerService
    {
        /// <summary>
        /// Get supported languages with their capabilities
        /// </summary>
        public Dictionary<string, LanguageCapabilities> GetSupportedLanguages();

        /// <summary>
        /// Get the preferred backend for a language
        /// </summary>
        public IPhonemizerBackend GetPreferredBackend(string language);

        /// <summary>
        /// Phonemize text with fallback support
        /// </summary>
        public Task<PhonemeResult> PhonemizeWithFallbackAsync(
            string text,
            string language,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Register a phonemizer backend for a language
        /// </summary>
        public void RegisterBackend(string language, IPhonemizerBackend backend);

        /// <summary>
        /// Set language fallback chain
        /// </summary>
        public void SetLanguageFallback(string language, string[] fallbackLanguages);

        /// <summary>
        /// Get all registered backends
        /// </summary>
        public Dictionary<string, List<IPhonemizerBackend>> GetAllBackends();

        /// <summary>
        /// Phonemize text in multiple languages
        /// </summary>
        public Task<Dictionary<string, PhonemeResult>> PhonemizeBatchAsync(
            Dictionary<string, string> textsByLanguage,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Dispose all backends
        /// </summary>
        public void Dispose();
    }

    /// <summary>
    /// Language capabilities information
    /// </summary>
    public class LanguageCapabilities
    {
        public string Language { get; set; }
        public string LanguageCode { get; set; }
        public string DisplayName { get; set; }
        public string NativeName { get; set; }
        public List<string> SupportedBackends { get; set; } = new List<string>();
        public List<string> AvailableBackends { get; set; } = new List<string>();
        public string PreferredBackend { get; set; }
        public bool SupportsIPA { get; set; }
        public bool SupportsStress { get; set; }
        public bool SupportsSyllables { get; set; }
        public bool SupportsTones { get; set; }
        public bool SupportsTone { get; set; }
        public bool SupportsG2P { get; set; }
        public bool RequiresNormalization { get; set; }
        public string Script { get; set; } // Latin, Cyrillic, Arabic, etc.
        public float OverallQuality { get; set; } // 0-1 quality score
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Result from multilingual phonemization
    /// </summary>
    public class MultilingualPhonemeResult : PhonemeResult
    {
        public string DetectedLanguage { get; set; }
        public float LanguageConfidence { get; set; }
        public Dictionary<string, float> LanguageScores { get; set; }
        public string UsedBackend { get; set; }
        public bool UsedFallback { get; set; }
        public string FallbackReason { get; set; }
    }

    /// <summary>
    /// Language detection result
    /// </summary>
    public class LanguageDetectionResult
    {
        public string DetectedLanguage { get; set; }
        public float Confidence { get; set; }
        public Dictionary<string, float> LanguageScores { get; set; }
        public bool IsReliable { get; set; }
    }

    /// <summary>
    /// Language-specific phoneme mapping
    /// </summary>
    public interface IPhonemeMapper
    {
        /// <summary>
        /// Map phonemes from one system to another
        /// </summary>
        public string MapPhoneme(string phoneme, string fromSystem, string toSystem);

        /// <summary>
        /// Check if mapping is available
        /// </summary>
        public bool CanMap(string fromSystem, string toSystem);

        /// <summary>
        /// Get available phoneme systems
        /// </summary>
        public string[] GetPhonemeSystems();
    }

    /// <summary>
    /// Language group information for better fallback
    /// </summary>
    public class LanguageGroup
    {
        public string GroupName { get; set; }
        public List<string> Languages { get; set; } = new List<string>();
        public string CommonScript { get; set; }
        public bool SharePhonemeSet { get; set; }

        // Language similarity matrix (0-1)
        public Dictionary<(string, string), float> SimilarityScores { get; set; }
            = new Dictionary<(string, string), float>();
    }

    /// <summary>
    /// Multilingual text preprocessor
    /// </summary>
    public interface IMultilingualTextPreprocessor
    {
        /// <summary>
        /// Preprocess text for a specific language
        /// </summary>
        public string PreprocessText(string text, string languageCode);

        /// <summary>
        /// Normalize text across languages
        /// </summary>
        public string NormalizeMultilingualText(string text);

        /// <summary>
        /// Split mixed-language text into segments
        /// </summary>
        public List<TextSegment> SegmentMixedLanguageText(string text);
    }

    /// <summary>
    /// Text segment with language information
    /// </summary>
    public class TextSegment
    {
        public string Text { get; set; }
        public string Language { get; set; }
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
        public float Confidence { get; set; }
    }
}