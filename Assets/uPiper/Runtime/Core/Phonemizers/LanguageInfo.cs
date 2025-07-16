using System;

namespace uPiper.Core.Phonemizers
{
    /// <summary>
    /// Contains information about a supported language.
    /// </summary>
    [Serializable]
    public class LanguageInfo
    {
        /// <summary>
        /// Gets or sets the ISO 639-1 language code (e.g., "ja", "en").
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// Gets or sets the English name of the language.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the native name of the language.
        /// </summary>
        public string NativeName { get; set; }

        /// <summary>
        /// Gets or sets whether this language requires special preprocessing.
        /// </summary>
        public bool RequiresPreprocessing { get; set; }

        /// <summary>
        /// Gets or sets the list of available voice IDs for this language.
        /// </summary>
        public string[] AvailableVoices { get; set; }

        /// <summary>
        /// Gets or sets the default voice ID for this language.
        /// </summary>
        public string DefaultVoice { get; set; }

        /// <summary>
        /// Gets or sets the phoneme set type used for this language.
        /// </summary>
        public string PhonemeSetType { get; set; }

        /// <summary>
        /// Gets or sets whether this language supports accent/pitch information.
        /// </summary>
        public bool SupportsAccent { get; set; }

        /// <summary>
        /// Gets or sets the text direction for this language.
        /// </summary>
        public TextDirection Direction { get; set; } = TextDirection.LeftToRight;

        /// <summary>
        /// Creates a new instance of LanguageInfo.
        /// </summary>
        public LanguageInfo()
        {
            AvailableVoices = Array.Empty<string>();
        }

        /// <summary>
        /// Creates a LanguageInfo instance with basic information.
        /// </summary>
        /// <param name="code">Language code.</param>
        /// <param name="name">English name.</param>
        /// <param name="nativeName">Native name.</param>
        /// <returns>A new LanguageInfo instance.</returns>
        public static LanguageInfo Create(string code, string name, string nativeName)
        {
            return new LanguageInfo
            {
                Code = code,
                Name = name,
                NativeName = nativeName
            };
        }

        /// <summary>
        /// Returns a string representation of the language info.
        /// </summary>
        /// <returns>String representation.</returns>
        public override string ToString()
        {
            return $"{Name} ({Code}) - {NativeName}";
        }
    }

    /// <summary>
    /// Text direction enumeration.
    /// </summary>
    public enum TextDirection
    {
        /// <summary>
        /// Left to right text direction.
        /// </summary>
        LeftToRight,

        /// <summary>
        /// Right to left text direction.
        /// </summary>
        RightToLeft
    }
}