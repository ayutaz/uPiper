using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace uPiper.Core.Phonemizers.Backend.RuleBased
{
    /// <summary>
    /// Grapheme-to-Phoneme (G2P) engine for handling out-of-vocabulary words.
    /// Uses rule-based approach with English phonetic patterns.
    /// </summary>
    public class G2PEngine : IDisposable
    {
        private Dictionary<string, string[]> letterToPhoneme;
        private Dictionary<string, string[]> digraphRules;
        private Dictionary<string, string[]> suffixRules;
        private Dictionary<string, string[]> prefixRules;
        private List<PhoneticRule> contextRules;

        /// <summary>
        /// Initializes the G2P engine with rules.
        /// </summary>
        public void Initialize()
        {
            InitializeBasicMappings();
            InitializeDigraphRules();
            InitializeAffixRules();
            InitializeContextRules();
        }

        /// <summary>
        /// Predicts phonemes for a word using G2P rules.
        /// </summary>
        /// <param name="word">The word to convert.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Array of ARPABET phonemes.</returns>
        public Task<string[]> PredictAsync(string word, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Predict(word), cancellationToken);
        }

        /// <summary>
        /// Synchronous prediction.
        /// </summary>
        public string[] Predict(string word)
        {
            if (string.IsNullOrEmpty(word))
                return Array.Empty<string>();

            word = word.ToUpper();
            var result = new List<string>();

            // Apply prefix rules
            var (prefix, remainingWord) = ExtractPrefix(word);
            if (!string.IsNullOrEmpty(prefix))
            {
                result.AddRange(prefixRules[prefix]);
                word = remainingWord;
            }

            // Apply suffix rules
            var (wordWithoutSuffix, suffix) = ExtractSuffix(word);
            if (!string.IsNullOrEmpty(suffix))
            {
                word = wordWithoutSuffix;
            }

            // Process main word body
            var i = 0;
            while (i < word.Length)
            {
                var matched = false;

                // Try context rules first
                foreach (var rule in contextRules)
                {
                    if (rule.Matches(word, i))
                    {
                        result.AddRange(rule.Phonemes);
                        i += rule.GraphemeLength;
                        matched = true;
                        break;
                    }
                }

                if (matched) continue;

                // Try digraphs (2-letter combinations)
                if (i < word.Length - 1)
                {
                    var digraph = word.Substring(i, 2);
                    if (digraphRules.TryGetValue(digraph, out var phonemes))
                    {
                        result.AddRange(phonemes);
                        i += 2;
                        continue;
                    }
                }

                // Fall back to single letter
                var letter = word[i].ToString();
                if (letterToPhoneme.TryGetValue(letter, out var defaultPhonemes))
                {
                    // Apply simple context rules
                    var phoneme = ChoosePhonemeByContext(letter, word, i, defaultPhonemes);
                    result.Add(phoneme);
                }

                i++;
            }

            // Add suffix phonemes
            if (!string.IsNullOrEmpty(suffix) && suffixRules.TryGetValue(suffix, out var suffixPhonemes))
            {
                result.AddRange(suffixPhonemes);
            }

            // Apply stress patterns
            ApplyStressPatterns(result);

            return result.ToArray();
        }

        /// <summary>
        /// Gets estimated memory usage.
        /// </summary>
        public long GetMemoryUsage()
        {
            // Rough estimation
            return 100 * 1024; // ~100KB for rules
        }

        private void InitializeBasicMappings()
        {
            // Basic letter to phoneme mappings (most common pronunciations)
            letterToPhoneme = new Dictionary<string, string[]>
            {
                // Vowels
                ["A"] = new[] { "AE", "EY", "AH" },
                ["E"] = new[] { "EH", "IY" },
                ["I"] = new[] { "IH", "AY" },
                ["O"] = new[] { "AA", "OW", "AH" },
                ["U"] = new[] { "AH", "UW", "YUW" },
                ["Y"] = new[] { "IY", "AY", "Y" },

                // Consonants
                ["B"] = new[] { "B" },
                ["C"] = new[] { "K", "S" },
                ["D"] = new[] { "D" },
                ["F"] = new[] { "F" },
                ["G"] = new[] { "G", "JH" },
                ["H"] = new[] { "HH" },
                ["J"] = new[] { "JH" },
                ["K"] = new[] { "K" },
                ["L"] = new[] { "L" },
                ["M"] = new[] { "M" },
                ["N"] = new[] { "N" },
                ["P"] = new[] { "P" },
                ["Q"] = new[] { "K" },
                ["R"] = new[] { "R" },
                ["S"] = new[] { "S", "Z" },
                ["T"] = new[] { "T" },
                ["V"] = new[] { "V" },
                ["W"] = new[] { "W" },
                ["X"] = new[] { "K", "S" },
                ["Z"] = new[] { "Z" }
            };
        }

        private void InitializeDigraphRules()
        {
            digraphRules = new Dictionary<string, string[]>
            {
                // Common digraphs
                ["CH"] = new[] { "CH" },
                ["SH"] = new[] { "SH" },
                ["TH"] = new[] { "TH", "DH" },
                ["PH"] = new[] { "F" },
                ["GH"] = new[] { "G", "" }, // Sometimes silent
                ["WH"] = new[] { "W" },
                ["CK"] = new[] { "K" },
                ["NG"] = new[] { "NG" },

                // Vowel digraphs
                ["AI"] = new[] { "EY" },
                ["AY"] = new[] { "EY" },
                ["EA"] = new[] { "IY", "EH" },
                ["EE"] = new[] { "IY" },
                ["EI"] = new[] { "EY", "IY" },
                ["EY"] = new[] { "EY" },
                ["IE"] = new[] { "IY", "AY" },
                ["OA"] = new[] { "OW" },
                ["OE"] = new[] { "OW", "UW" },
                ["OI"] = new[] { "OY" },
                ["OO"] = new[] { "UW", "UH" },
                ["OU"] = new[] { "AW", "OW" },
                ["OW"] = new[] { "OW", "AW" },
                ["OY"] = new[] { "OY" },
                ["UE"] = new[] { "UW", "YUW" },
                ["UI"] = new[] { "UW", "IH" }
            };
        }

        private void InitializeAffixRules()
        {
            // Common prefixes
            prefixRules = new Dictionary<string, string[]>
            {
                ["UN"] = new[] { "AH", "N" },
                ["RE"] = new[] { "R", "IY" },
                ["IN"] = new[] { "IH", "N" },
                ["DIS"] = new[] { "D", "IH", "S" },
                ["MIS"] = new[] { "M", "IH", "S" },
                ["PRE"] = new[] { "P", "R", "IY" },
                ["POST"] = new[] { "P", "OW", "S", "T" }
            };

            // Common suffixes
            suffixRules = new Dictionary<string, string[]>
            {
                ["ING"] = new[] { "IH", "NG" },
                ["ED"] = new[] { "D" }, // or "T" or "IH", "D"
                ["ER"] = new[] { "ER" },
                ["EST"] = new[] { "IH", "S", "T" },
                ["LY"] = new[] { "L", "IY" },
                ["NESS"] = new[] { "N", "AH", "S" },
                ["MENT"] = new[] { "M", "AH", "N", "T" },
                ["TION"] = new[] { "SH", "AH", "N" },
                ["SION"] = new[] { "ZH", "AH", "N" },
                ["ABLE"] = new[] { "AH", "B", "AH", "L" },
                ["IBLE"] = new[] { "IH", "B", "AH", "L" },
                ["FUL"] = new[] { "F", "AH", "L" },
                ["LESS"] = new[] { "L", "AH", "S" }
            };
        }

        private void InitializeContextRules()
        {
            contextRules = new List<PhoneticRule>
            {
                // C before E, I, Y usually sounds like S
                new("C", new[] { "S" }, context: (word, pos) =>
                    pos < word.Length - 1 && "EIY".Contains(word[pos + 1])),
                
                // G before E, I, Y often sounds like J
                new("G", new[] { "JH" }, context: (word, pos) =>
                    pos < word.Length - 1 && "EIY".Contains(word[pos + 1])),
                
                // Silent E at end of word
                new("E", new string[0], context: (word, pos) =>
                    pos == word.Length - 1 && word.Length > 2),
                
                // QU combination
                new("QU", new[] { "K", "W" }, graphemeLength: 2)
            };
        }

        private string ChoosePhonemeByContext(string letter, string word, int position, string[] options)
        {
            // Simple heuristics for choosing among phoneme options
            if (options.Length == 1)
                return options[0];

            // For C: use K sound unless before E, I, Y
            if (letter == "C")
            {
                if (position < word.Length - 1 && "EIY".Contains(word[position + 1]))
                    return "S";
                return "K";
            }

            // For G: use G sound unless before E, I, Y
            if (letter == "G")
            {
                if (position < word.Length - 1 && "EIY".Contains(word[position + 1]))
                    return "JH";
                return "G";
            }

            // Default to first option
            return options[0];
        }

        private (string prefix, string remaining) ExtractPrefix(string word)
        {
            foreach (var prefix in prefixRules.Keys.OrderByDescending(p => p.Length))
            {
                if (word.StartsWith(prefix) && word.Length > prefix.Length)
                {
                    return (prefix, word[prefix.Length..]);
                }
            }
            return (null, word);
        }

        private (string word, string suffix) ExtractSuffix(string word)
        {
            foreach (var suffix in suffixRules.Keys.OrderByDescending(s => s.Length))
            {
                if (word.EndsWith(suffix) && word.Length > suffix.Length)
                {
                    return (word[..^suffix.Length], suffix);
                }
            }
            return (word, null);
        }

        private void ApplyStressPatterns(List<string> phonemes)
        {
            // Simple stress pattern: stress first vowel
            var stressApplied = false;
            for (var i = 0; i < phonemes.Count; i++)
            {
                if (IsVowel(phonemes[i]) && !stressApplied)
                {
                    phonemes[i] = phonemes[i] + "1"; // Primary stress
                    stressApplied = true;
                }
                else if (IsVowel(phonemes[i]))
                {
                    phonemes[i] = phonemes[i] + "0"; // No stress
                }
            }
        }

        private bool IsVowel(string phoneme)
        {
            var vowelPhonemes = new HashSet<string>
            {
                "AA", "AE", "AH", "AO", "AW", "AY", "EH", "ER",
                "EY", "IH", "IY", "OW", "OY", "UH", "UW"
            };
            return vowelPhonemes.Contains(phoneme);
        }

        public void Dispose()
        {
            letterToPhoneme?.Clear();
            digraphRules?.Clear();
            suffixRules?.Clear();
            prefixRules?.Clear();
            contextRules?.Clear();
        }
    }

    /// <summary>
    /// Represents a phonetic rule with context.
    /// </summary>
    internal class PhoneticRule
    {
        public string Grapheme { get; }
        public string[] Phonemes { get; }
        public int GraphemeLength { get; }
        public Func<string, int, bool> Context { get; }

        public PhoneticRule(string grapheme, string[] phonemes,
            int graphemeLength = 1, Func<string, int, bool> context = null)
        {
            Grapheme = grapheme;
            Phonemes = phonemes;
            GraphemeLength = graphemeLength;
            Context = context;
        }

        public bool Matches(string word, int position)
        {
            if (position + GraphemeLength > word.Length)
                return false;

            var substr = word.Substring(position, GraphemeLength);
            if (substr != Grapheme)
                return false;

            return Context == null || Context(word, position);
        }
    }
}