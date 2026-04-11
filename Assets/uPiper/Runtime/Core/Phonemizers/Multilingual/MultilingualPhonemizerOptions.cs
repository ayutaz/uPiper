using System;
using System.Collections.Generic;
using uPiper.Core.Logging;
using uPiper.Core.Phonemizers.Multilingual.Handlers;

namespace uPiper.Core.Phonemizers.Multilingual
{
    /// <summary>
    /// Configuration options for <see cref="MultilingualPhonemizer"/>.
    /// </summary>
    public class MultilingualPhonemizerOptions
    {
        /// <summary>Languages to support (e.g., ["ja", "en"]).</summary>
        public IReadOnlyList<string> Languages { get; set; }

        /// <summary>Default language for Latin text (default: "en").</summary>
        public string DefaultLatinLanguage { get; set; } = "en";

        /// <summary>Optional pre-built handlers keyed by language code.</summary>
        public Dictionary<string, ILanguageG2PHandler> Handlers { get; set; }

        /// <summary>
        /// Optional custom language detector. When set, this detector is used instead of
        /// the default Unicode-based detector or the hybrid trigram detector.
        /// </summary>
        public ILanguageDetector LanguageDetector { get; set; }

        /// <summary>
        /// Whether to enable trigram-based language detection for Latin-script languages.
        /// When true and multiple Latin languages are configured, a
        /// <see cref="HybridLanguageDetector"/> is created automatically.
        /// Default: true.
        /// </summary>
        public bool EnableTrigramDetection { get; set; } = true;

        /// <summary>
        /// Optional fallback language code for unsupported language segments.
        /// Set to null (default) to skip unsupported segments (backward-compatible).
        /// </summary>
        public string FallbackLanguage { get; set; }

        /// <summary>
        /// Validates the options, throwing if required properties are missing or invalid.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when Languages is null or empty.</exception>
        public void Validate()
        {
            if (Languages == null || Languages.Count == 0)
                throw new ArgumentException(
                    "At least one language must be specified.", nameof(Languages));

            // Handlers のキーが Languages に含まれることを検証
            if (Handlers != null && Languages != null)
            {
                foreach (var key in Handlers.Keys)
                {
                    if (!ListContains(Languages, key))
                    {
                        PiperLogger.LogWarning(
                            $"[MultilingualPhonemizerOptions] Handler for '{key}' registered " +
                            $"but '{key}' is not in Languages list. This handler will be unused.");
                    }
                }
            }

            if (DefaultLatinLanguage != null && Languages != null
                && !ListContains(Languages, DefaultLatinLanguage))
            {
                PiperLogger.LogWarning(
                    $"[MultilingualPhonemizerOptions] DefaultLatinLanguage " +
                    $"'{DefaultLatinLanguage}' " +
                    $"is not in Languages list. It may not be detected correctly.");
            }

            if (!string.IsNullOrWhiteSpace(FallbackLanguage) && Languages != null
                && !ListContains(Languages, FallbackLanguage))
            {
                PiperLogger.LogWarning(
                    $"[MultilingualPhonemizerOptions] FallbackLanguage '{FallbackLanguage}' " +
                    "is not in Languages list. Fallback will not work.");
            }
        }

        private static bool ListContains(IReadOnlyList<string> list, string value)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] == value)
                    return true;
            }
            return false;
        }
    }
}