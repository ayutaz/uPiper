namespace uPiper.Core.Phonemizers.Text
{
    /// <summary>
    /// Interface for text normalization before phonemization.
    /// </summary>
    public interface ITextNormalizer
    {
        /// <summary>
        /// Normalizes text for phonemization.
        /// </summary>
        /// <param name="text">The text to normalize.</param>
        /// <param name="language">The language code for language-specific normalization.</param>
        /// <returns>The normalized text.</returns>
        public string Normalize(string text, string language);

        /// <summary>
        /// Checks if the text needs normalization.
        /// </summary>
        /// <param name="text">The text to check.</param>
        /// <param name="language">The language code.</param>
        /// <returns>True if normalization is needed.</returns>
        public bool NeedsNormalization(string text, string language);
    }
}