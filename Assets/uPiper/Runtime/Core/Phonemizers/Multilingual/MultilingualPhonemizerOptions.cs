using System;
using System.Collections.Generic;
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
        /// Validates the options, throwing if required properties are missing or invalid.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when Languages is null or empty.</exception>
        public void Validate()
        {
            if (Languages == null || Languages.Count == 0)
                throw new ArgumentException(
                    "At least one language must be specified.", nameof(Languages));
        }
    }
}
