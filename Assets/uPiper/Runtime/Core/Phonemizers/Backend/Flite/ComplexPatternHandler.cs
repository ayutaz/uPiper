using System;
using System.Collections.Generic;
using System.Linq;

namespace uPiper.Core.Phonemizers.Backend.Flite
{
    /// <summary>
    /// Handles complex grapheme patterns that standard LTS rules may miss.
    /// This improves accuracy for multi-phoneme suffixes and irregular patterns.
    ///
    /// Implementation for Issue #69 - English phonemization quality improvements
    /// </summary>
    public class ComplexPatternHandler
    {
        /// <summary>
        /// Complex suffix patterns that produce multiple phonemes
        /// Key: suffix pattern, Value: phoneme sequence
        /// </summary>
        private static readonly Dictionary<string, string[]> ComplexSuffixes = new()
        {
            // Common multi-phoneme suffixes
            ["tion"] = new[] { "sh", "ah0", "n" },      // nation, action, cooperation
            ["sion"] = new[] { "zh", "ah0", "n" },      // vision, decision, explosion
            ["cian"] = new[] { "sh", "ah0", "n" },      // musician, magician, physician
            ["ssion"] = new[] { "sh", "ah0", "n" },     // mission, session, passion

            // -ture and -sure patterns
            ["ture"] = new[] { "ch", "er0" },           // nature, picture, future
            ["sure"] = new[] { "zh", "er0" },           // measure, pleasure, treasure

            // -age patterns (final)
            ["age"] = new[] { "ih0", "jh" },            // package, message, advantage

            // -ous patterns
            ["ious"] = new[] { "iy0", "ah0", "s" },     // curious, various, obvious
            ["eous"] = new[] { "iy0", "ah0", "s" },     // gorgeous, courageous
            ["ous"] = new[] { "ah0", "s" },             // famous, nervous, dangerous
        };

        /// <summary>
        /// Cached sorted suffixes for performance (longest to shortest)
        /// </summary>
        private static readonly KeyValuePair<string, string[]>[] SortedSuffixes =
            ComplexSuffixes.OrderByDescending(kvp => kvp.Key.Length).ToArray();

        /// <summary>
        /// Context-dependent "ough" patterns
        /// Different pronunciations based on the preceding letters
        /// </summary>
        private static readonly Dictionary<string, string[]> OughPatterns = new()
        {
            ["through"] = new[] { "th", "r", "uw1" },           // through
            ["though"] = new[] { "dh", "ow1" },                 // though, although
            ["thought"] = new[] { "th", "ao1", "t" },           // thought, bought
            ["tough"] = new[] { "t", "ah1", "f" },              // tough, rough, enough
            ["cough"] = new[] { "k", "ao1", "f" },              // cough
            ["bough"] = new[] { "b", "aw1" },                   // bough
            ["ought"] = new[] { "ao1", "t" },                   // ought, bought, fought
        };

        /// <summary>
        /// Silent letter patterns
        /// Key: pattern, Value: null (indicates letters should be removed)
        /// </summary>
        private static readonly HashSet<string> SilentPatterns = new()
        {
            "kn",  // knight, know, knot
            "gn",  // gnome, sign, foreign (initial gn)
            "wr",  // write, wrong, wrist
            "ps",  // psychology, psalm, pseudonym (initial ps)
            "pn",  // pneumonia, pneumatic (initial pn)
        };

        /// <summary>
        /// Try to apply complex pattern matching before LTS fallback
        /// Returns null if no pattern matches
        /// </summary>
        public string[] TryApplyComplexPattern(string word)
        {
            if (string.IsNullOrEmpty(word))
                return null;

            word = word.ToLower();

            // 1. Check for "ough" patterns first (most specific)
            var oughMatch = TryMatchOugh(word);
            if (oughMatch != null)
                return oughMatch;

            // 2. Check for complex suffixes
            var suffixMatch = TryMatchSuffix(word);
            if (suffixMatch != null)
                return suffixMatch;

            // No complex pattern matched
            return null;
        }

        /// <summary>
        /// Try to match word-specific "ough" patterns
        /// </summary>
        private string[] TryMatchOugh(string word)
        {
            if (!word.Contains("ough"))
                return null;

            // Try exact word matches first
            foreach (var pattern in OughPatterns)
            {
                if (word == pattern.Key)
                    return pattern.Value;

                // Also check if word ends with the pattern (e.g., "although" contains "though")
                if (word.EndsWith(pattern.Key))
                {
                    // For compound words, we might need to handle prefix separately
                    // For now, just return the pattern match
                    return pattern.Value;
                }
            }

            // Default "ough" -> "ah0 f" (like "rough", "tough", "enough")
            if (word.EndsWith("ough"))
                return new[] { "ah0", "f" };

            return null;
        }

        /// <summary>
        /// Try to match complex suffix patterns
        /// Returns phonemes for the entire word if pattern matches at the end
        /// </summary>
        private string[] TryMatchSuffix(string word)
        {
            // Check each suffix pattern from longest to shortest (using cached sorted list)
            foreach (var pattern in SortedSuffixes)
            {
                if (word.EndsWith(pattern.Key))
                {
                    // Get the part before the suffix
                    var prefix = word.Substring(0, word.Length - pattern.Key.Length);

                    // If prefix is too short, skip (probably not a real suffix)
                    if (prefix.Length < 2)
                        continue;

                    // Return indicator that this suffix was found
                    // The caller will need to process the prefix separately
                    return pattern.Value;
                }
            }

            return null;
        }

        /// <summary>
        /// Check if word starts with a silent letter pattern
        /// </summary>
        public bool StartsWithSilentPattern(string word, out string silentPattern)
        {
            silentPattern = null;
            if (string.IsNullOrEmpty(word) || word.Length < 2)
                return false;

            var prefix = word.Substring(0, 2).ToLower();
            if (SilentPatterns.Contains(prefix))
            {
                silentPattern = prefix;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Split word into prefix and complex suffix if applicable
        /// Returns (prefix, suffix, suffixPhonemes) or null if no match
        /// </summary>
        public (string prefix, string suffix, string[] suffixPhonemes)? SplitComplexSuffix(string word)
        {
            if (string.IsNullOrEmpty(word))
                return null;

            word = word.ToLower();

            // Check each suffix pattern from longest to shortest (using cached sorted list)
            foreach (var pattern in SortedSuffixes)
            {
                if (word.EndsWith(pattern.Key))
                {
                    var prefix = word.Substring(0, word.Length - pattern.Key.Length);

                    // Prefix must be substantial
                    if (prefix.Length >= 2)
                    {
                        return (prefix, pattern.Key, pattern.Value);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Get all registered complex suffix patterns (for testing/debugging)
        /// </summary>
        public IReadOnlyDictionary<string, string[]> GetComplexSuffixes()
        {
            return ComplexSuffixes;
        }

        /// <summary>
        /// Get all registered "ough" patterns (for testing/debugging)
        /// </summary>
        public IReadOnlyDictionary<string, string[]> GetOughPatterns()
        {
            return OughPatterns;
        }
    }
}