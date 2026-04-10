using System;
using System.Collections.Generic;

namespace uPiper.Core
{
    /// <summary>
    /// Event arguments for unsupported language detection.
    /// </summary>
    public sealed class UnsupportedLanguageEventArgs
    {
        /// <summary>Maximum text length stored in <see cref="SkippedText"/>.</summary>
        internal const int MaxTextLength = 200;

        /// <summary>Detected language code that is not supported (e.g., "ko").</summary>
        public string LanguageCode { get; }

        /// <summary>
        /// The text segment that was skipped or processed by fallback.
        /// Truncated to <see cref="MaxTextLength"/> characters when the original text is longer.
        /// </summary>
        public string SkippedText { get; }

        /// <summary>Language codes supported by the current model.</summary>
        public IReadOnlyList<string> SupportedLanguages { get; }

        /// <summary>
        /// The fallback language that was used to process the segment,
        /// or <c>null</c> if the segment was skipped entirely.
        /// </summary>
        public string FallbackLanguageUsed { get; }

        /// <summary>
        /// Whether the segment was processed using a fallback language handler
        /// instead of being skipped.
        /// </summary>
        public bool WasProcessedByFallback => FallbackLanguageUsed != null;

        /// <summary>
        /// Initializes a new instance of <see cref="UnsupportedLanguageEventArgs"/>.
        /// </summary>
        /// <param name="languageCode">Detected unsupported language code.</param>
        /// <param name="skippedText">The text segment that was skipped or processed by fallback.</param>
        /// <param name="supportedLanguages">Language codes supported by the current model.</param>
        /// <param name="fallbackLanguageUsed">
        /// The fallback language used, or <c>null</c> if the segment was skipped.
        /// </param>
        public UnsupportedLanguageEventArgs(
            string languageCode,
            string skippedText,
            IReadOnlyList<string> supportedLanguages,
            string fallbackLanguageUsed = null)
        {
            LanguageCode = languageCode ?? throw new ArgumentNullException(nameof(languageCode));
            SkippedText = skippedText != null && skippedText.Length > MaxTextLength
                ? skippedText[..MaxTextLength] + "..."
                : skippedText ?? string.Empty;
            SupportedLanguages = supportedLanguages
                ?? throw new ArgumentNullException(nameof(supportedLanguages));
            FallbackLanguageUsed = fallbackLanguageUsed;
        }
    }
}