using System;
using System.Collections.Generic;
using DotNetG2P.Chinese;
using DotNetG2P.English;
using DotNetG2P.French;
using DotNetG2P.Korean;
using DotNetG2P.Portuguese;
using DotNetG2P.Spanish;
using uPiper.Core.Phonemizers.Backend;
using uPiper.Core.Phonemizers.Implementations;
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

        /// <summary>Optional pre-built Japanese phonemizer; one is created if null.</summary>
        [Obsolete("Use Handlers dictionary instead. Will be removed in P1-6.")]
        public DotNetG2PPhonemizer JaPhonemizer { get; set; }

        /// <summary>Optional pre-built English G2P engine (DotNetG2P.English).</summary>
        [Obsolete("Use Handlers dictionary instead. Will be removed in P1-6.")]
        public EnglishG2PEngine EnEngine { get; set; }

        /// <summary>Optional pre-built English phonemizer backend (legacy, for test stubs).</summary>
        [Obsolete("Use EnEngine instead. This property will be removed in v2.0.")]
        public IPhonemizerBackend EnPhonemizer { get; set; }

        /// <summary>Optional pre-built Spanish G2P engine.</summary>
        [Obsolete("Use Handlers dictionary instead. Will be removed in P1-6.")]
        public SpanishG2PEngine EsEngine { get; set; }

        /// <summary>Optional pre-built French G2P engine.</summary>
        [Obsolete("Use Handlers dictionary instead. Will be removed in P1-6.")]
        public FrenchG2PEngine FrEngine { get; set; }

        /// <summary>Optional pre-built Portuguese G2P engine.</summary>
        [Obsolete("Use Handlers dictionary instead. Will be removed in P1-6.")]
        public PortugueseG2PEngine PtEngine { get; set; }

        /// <summary>Optional pre-built Chinese G2P engine (DotNetG2P.Chinese).</summary>
        [Obsolete("Use Handlers dictionary instead. Will be removed in P1-6.")]
        public ChineseG2PEngine ZhEngine { get; set; }

        /// <summary>Optional pre-built Korean phonemizer backend (legacy, prefer KoG2PEngine).</summary>
        [Obsolete("Use KoG2PEngine instead. This property will be removed in v2.0.")]
        public IPhonemizerBackend KoPhonemizer { get; set; }

        /// <summary>Optional pre-built Korean G2P engine (DotNetG2P.Korean).</summary>
        [Obsolete("Use Handlers dictionary instead. Will be removed in P1-6.")]
        public KoreanG2PEngine KoG2PEngine { get; set; }

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