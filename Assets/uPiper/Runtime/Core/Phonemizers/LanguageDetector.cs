using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace uPiper.Core.Phonemizers
{
    /// <summary>
    /// Detects language segments in mixed text (Japanese/English).
    /// </summary>
    public class LanguageDetector
    {
        /// <summary>
        /// Language segment in text.
        /// </summary>
        public class LanguageSegment
        {
            public string Text { get; set; }
            public string Language { get; set; }
            public int StartIndex { get; set; }
            public int EndIndex { get; set; }
            public bool IsPunctuation { get; set; }

            public override string ToString()
            {
                return $"[{Language}] \"{Text}\" ({StartIndex}-{EndIndex})";
            }
        }

        // Unicode ranges for different scripts
        private static readonly Regex JapaneseRegex = new(
            @"[\u3040-\u309F\u30A0-\u30FF\u4E00-\u9FAF\u3400-\u4DBF]+",
            RegexOptions.Compiled
        );

        private static readonly Regex EnglishRegex = new(
            @"[a-zA-Z]+(?:['']?[a-zA-Z]+)*",
            RegexOptions.Compiled
        );

        private static readonly Regex NumberRegex = new(
            @"\d+(?:[.,]\d+)*",
            RegexOptions.Compiled
        );

        private static readonly Regex PunctuationRegex = new(
            @"[^\w\s\u3040-\u309F\u30A0-\u30FF\u4E00-\u9FAF\u3400-\u4DBF]+",
            RegexOptions.Compiled
        );

        /// <summary>
        /// Detects language segments in mixed text.
        /// </summary>
        public List<LanguageSegment> DetectSegments(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new List<LanguageSegment>();

            var segments = new List<LanguageSegment>();
            var processed = new bool[text.Length];

            // First pass: Find Japanese segments
            foreach (Match match in JapaneseRegex.Matches(text))
            {
                segments.Add(new LanguageSegment
                {
                    Text = match.Value,
                    Language = "ja",
                    StartIndex = match.Index,
                    EndIndex = match.Index + match.Length - 1,
                    IsPunctuation = false
                });
                MarkProcessed(processed, match.Index, match.Length);
            }

            // Second pass: Find English segments
            foreach (Match match in EnglishRegex.Matches(text))
            {
                if (!IsProcessed(processed, match.Index, match.Length))
                {
                    segments.Add(new LanguageSegment
                    {
                        Text = match.Value,
                        Language = "en",
                        StartIndex = match.Index,
                        EndIndex = match.Index + match.Length - 1,
                        IsPunctuation = false
                    });
                    MarkProcessed(processed, match.Index, match.Length);
                }
            }

            // Third pass: Find numbers
            foreach (Match match in NumberRegex.Matches(text))
            {
                if (!IsProcessed(processed, match.Index, match.Length))
                {
                    // Determine language based on context
                    var lang = DetermineNumberLanguage(text, match.Index, segments);
                    segments.Add(new LanguageSegment
                    {
                        Text = match.Value,
                        Language = lang,
                        StartIndex = match.Index,
                        EndIndex = match.Index + match.Length - 1,
                        IsPunctuation = false
                    });
                    MarkProcessed(processed, match.Index, match.Length);
                }
            }

            // Fourth pass: Handle remaining characters (punctuation, whitespace)
            for (var i = 0; i < text.Length; i++)
            {
                if (!processed[i])
                {
                    var ch = text[i];
                    if (char.IsWhiteSpace(ch))
                    {
                        // Skip whitespace for now
                        continue;
                    }

                    // Find consecutive unprocessed characters
                    var start = i;
                    while (i < text.Length && !processed[i] && !char.IsWhiteSpace(text[i]))
                    {
                        i++;
                    }
                    i--; // Back up one

                    var segment = text.Substring(start, i - start + 1);
                    var lang = DetermineSegmentLanguage(segment, start, segments);

                    segments.Add(new LanguageSegment
                    {
                        Text = segment,
                        Language = lang,
                        StartIndex = start,
                        EndIndex = i,
                        IsPunctuation = IsPunctuation(segment)
                    });
                }
            }

            // Sort by position and merge adjacent segments of same language
            segments.Sort((a, b) => a.StartIndex.CompareTo(b.StartIndex));
            return MergeAdjacentSegments(segments, text);
        }

        /// <summary>
        /// Detects the primary language of the entire text.
        /// </summary>
        public string DetectPrimaryLanguage(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "en";

            var japaneseChars = 0;
            var englishChars = 0;

            foreach (var ch in text)
            {
                if (IsJapaneseChar(ch))
                    japaneseChars++;
                else if (IsEnglishChar(ch))
                    englishChars++;
            }

            return japaneseChars > englishChars ? "ja" : "en";
        }

        private bool IsJapaneseChar(char ch)
        {
            return (ch >= '\u3040' && ch <= '\u309F') || // Hiragana
                   (ch >= '\u30A0' && ch <= '\u30FF') || // Katakana
                   (ch >= '\u4E00' && ch <= '\u9FAF') || // Kanji
                   (ch >= '\u3400' && ch <= '\u4DBF');   // Kanji extension
        }

        private bool IsEnglishChar(char ch)
        {
            return (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z');
        }

        private bool IsPunctuation(string text)
        {
            return text.All(ch => !char.IsLetterOrDigit(ch));
        }

        private void MarkProcessed(bool[] processed, int start, int length)
        {
            for (var i = 0; i < length && start + i < processed.Length; i++)
            {
                processed[start + i] = true;
            }
        }

        private bool IsProcessed(bool[] processed, int start, int length)
        {
            for (var i = 0; i < length && start + i < processed.Length; i++)
            {
                if (processed[start + i])
                    return true;
            }
            return false;
        }

        private string DetermineNumberLanguage(string text, int position, List<LanguageSegment> existingSegments)
        {
            // Look at surrounding segments
            var before = existingSegments
                .Where(s => s.EndIndex < position)
                .OrderByDescending(s => s.EndIndex)
                .FirstOrDefault();

            var after = existingSegments
                .Where(s => s.StartIndex > position)
                .OrderBy(s => s.StartIndex)
                .FirstOrDefault();

            // If surrounded by same language, use that
            if (before?.Language == after?.Language && before?.Language != null)
                return before.Language;

            // If only one side has context, use that
            if (before?.Language != null && !before.IsPunctuation)
                return before.Language;
            if (after?.Language != null && !after.IsPunctuation)
                return after.Language;

            // Default to primary language of the text
            return DetectPrimaryLanguage(text);
        }

        private string DetermineSegmentLanguage(string segment, int position, List<LanguageSegment> existingSegments)
        {
            // For punctuation, inherit from context
            if (IsPunctuation(segment))
            {
                var nearestSegment = existingSegments
                    .Where(s => !s.IsPunctuation)
                    .OrderBy(s => Math.Min(Math.Abs(s.EndIndex - position), Math.Abs(s.StartIndex - position)))
                    .FirstOrDefault();

                return nearestSegment?.Language ?? "neutral";
            }

            // Check if it contains any language-specific characters
            if (segment.Any(IsJapaneseChar))
                return "ja";
            if (segment.Any(IsEnglishChar))
                return "en";

            return "neutral";
        }

        private List<LanguageSegment> MergeAdjacentSegments(List<LanguageSegment> segments, string originalText)
        {
            if (segments.Count <= 1)
                return segments;

            var merged = new List<LanguageSegment>();
            var current = segments[0];

            for (var i = 1; i < segments.Count; i++)
            {
                var next = segments[i];

                // Check if we should merge
                var shouldMerge = current.Language == next.Language &&
                                  current.EndIndex + 1 >= next.StartIndex &&
                                  !current.IsPunctuation;

                // Check for whitespace between segments
                if (current.EndIndex + 1 < next.StartIndex)
                {
                    var between = originalText.Substring(current.EndIndex + 1, next.StartIndex - current.EndIndex - 1);
                    shouldMerge = shouldMerge && between.All(char.IsWhiteSpace);
                }

                if (shouldMerge)
                {
                    // Merge segments
                    current = new LanguageSegment
                    {
                        Text = originalText.Substring(current.StartIndex, next.EndIndex - current.StartIndex + 1),
                        Language = current.Language,
                        StartIndex = current.StartIndex,
                        EndIndex = next.EndIndex,
                        IsPunctuation = false
                    };
                }
                else
                {
                    merged.Add(current);
                    current = next;
                }
            }

            merged.Add(current);
            return merged;
        }
    }
}