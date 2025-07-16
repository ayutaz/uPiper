using System;
using System.Text;
using System.Text.RegularExpressions;
using uPiper.Core.Logging;

namespace uPiper.Core.Phonemizers.Text
{
    /// <summary>
    /// Default implementation of text normalizer.
    /// </summary>
    public class TextNormalizer : ITextNormalizer
    {
        // Regex patterns for normalization
        private static readonly Regex MultipleSpacesRegex = new Regex(@"\s+", RegexOptions.Compiled);
        private static readonly Regex LineBreaksRegex = new Regex(@"[\r\n]+", RegexOptions.Compiled);
        private static readonly Regex ControlCharsRegex = new Regex(@"[\x00-\x1F\x7F]", RegexOptions.Compiled);
        
        /// <summary>
        /// Normalizes text for phonemization.
        /// </summary>
        /// <param name="text">The text to normalize.</param>
        /// <param name="language">The language code for language-specific normalization.</param>
        /// <returns>The normalized text.</returns>
        public string Normalize(string text, string language)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var normalized = text;

            // Common normalization for all languages
            normalized = NormalizeCommon(normalized);

            // Language-specific normalization
            normalized = language?.ToLowerInvariant() switch
            {
                "ja" => NormalizeJapanese(normalized),
                "en" => NormalizeEnglish(normalized),
                "zh" => NormalizeChinese(normalized),
                "ko" => NormalizeKorean(normalized),
                _ => normalized
            };

            PiperLogger.LogDebug($"Text normalized: \"{text}\" -> \"{normalized}\" ({language})");
            return normalized;
        }

        /// <summary>
        /// Checks if the text needs normalization.
        /// </summary>
        /// <param name="text">The text to check.</param>
        /// <param name="language">The language code.</param>
        /// <returns>True if normalization is needed.</returns>
        public bool NeedsNormalization(string text, string language)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            // Check if text has leading/trailing spaces
            if (text != text.Trim())
            {
                return true;
            }

            // Check for common normalization needs
            if (MultipleSpacesRegex.IsMatch(text) || 
                LineBreaksRegex.IsMatch(text) || 
                ControlCharsRegex.IsMatch(text))
            {
                return true;
            }

            // Check for language-specific needs
            return language?.ToLowerInvariant() switch
            {
                "ja" => NeedsJapaneseNormalization(text),
                "en" => NeedsEnglishNormalization(text),
                _ => false
            };
        }

        /// <summary>
        /// Common normalization for all languages.
        /// </summary>
        private string NormalizeCommon(string text)
        {
            // Remove control characters
            text = ControlCharsRegex.Replace(text, " ");

            // Normalize line breaks to spaces
            text = LineBreaksRegex.Replace(text, " ");

            // Normalize multiple spaces to single space
            text = MultipleSpacesRegex.Replace(text, " ");

            // Trim
            return text.Trim();
        }

        /// <summary>
        /// Japanese-specific normalization.
        /// </summary>
        private string NormalizeJapanese(string text)
        {
            var sb = new StringBuilder(text.Length);

            foreach (char c in text)
            {
                // Convert full-width alphanumeric to half-width
                if (c >= '０' && c <= '９')
                {
                    sb.Append((char)(c - '０' + '0'));
                }
                else if (c >= 'Ａ' && c <= 'Ｚ')
                {
                    sb.Append((char)(c - 'Ａ' + 'A'));
                }
                else if (c >= 'ａ' && c <= 'ｚ')
                {
                    sb.Append((char)(c - 'ａ' + 'a'));
                }
                // Convert full-width space to half-width
                else if (c == '　')
                {
                    sb.Append(' ');
                }
                // Keep other characters as-is
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// English-specific normalization.
        /// </summary>
        private string NormalizeEnglish(string text)
        {
            // Convert to lowercase for consistency
            text = text.ToLowerInvariant();

            // Expand common contractions
            text = text.Replace("can't", "can not");
            text = text.Replace("won't", "will not");
            text = text.Replace("n't", " not");
            text = text.Replace("'s", " is");
            text = text.Replace("'re", " are");
            text = text.Replace("'ve", " have");
            text = text.Replace("'ll", " will");
            text = text.Replace("'d", " would");

            // Remove possessive apostrophes
            text = text.Replace("'", "");

            return text;
        }

        /// <summary>
        /// Chinese-specific normalization.
        /// </summary>
        private string NormalizeChinese(string text)
        {
            // Convert traditional to simplified (basic conversion)
            // In a real implementation, this would use a proper conversion library
            
            // For now, just handle full-width to half-width conversion
            var sb = new StringBuilder(text.Length);

            foreach (char c in text)
            {
                // Convert full-width punctuation
                switch (c)
                {
                    case '。': sb.Append('.'); break;
                    case '，': sb.Append(','); break;
                    case '？': sb.Append('?'); break;
                    case '！': sb.Append('!'); break;
                    case '、': sb.Append(','); break;
                    default: sb.Append(c); break;
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Korean-specific normalization.
        /// </summary>
        private string NormalizeKorean(string text)
        {
            // Basic Korean normalization
            // In a real implementation, this would handle Hangul normalization
            return text;
        }

        /// <summary>
        /// Checks if Japanese text needs normalization.
        /// </summary>
        private bool NeedsJapaneseNormalization(string text)
        {
            foreach (char c in text)
            {
                // Check for full-width characters
                if ((c >= '０' && c <= '９') || 
                    (c >= 'Ａ' && c <= 'Ｚ') || 
                    (c >= 'ａ' && c <= 'ｚ') ||
                    c == '　')
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Checks if English text needs normalization.
        /// </summary>
        private bool NeedsEnglishNormalization(string text)
        {
            // Check for contractions or uppercase letters
            return text.Contains("'") || text != text.ToLowerInvariant();
        }
    }
}