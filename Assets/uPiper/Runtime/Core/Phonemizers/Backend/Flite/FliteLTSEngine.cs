using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace uPiper.Core.Phonemizers.Backend.Flite
{
    /// <summary>
    /// Flite Letter-to-Sound (LTS) engine for converting unknown words to phonemes
    /// Based on Flite's WFST (Weighted Finite State Transducer) implementation
    /// </summary>
    public class FliteLTSEngine
    {
        // Constants from Flite
        private const int CST_LTS_EOR = 255;  // End of rule marker
        private const int DEFAULT_CONTEXT_WINDOW = 4;  // Default context window size

        private readonly FliteLTSRuleSet ruleSet;
        private readonly Dictionary<string, string[]> cache;
        private readonly int maxCacheSize;

        public FliteLTSEngine(FliteLTSRuleSet ruleSet, int maxCacheSize = 5000)
        {
            this.ruleSet = ruleSet ?? throw new ArgumentNullException(nameof(ruleSet));
            this.maxCacheSize = maxCacheSize;
            cache = new Dictionary<string, string[]>(maxCacheSize);
        }

        /// <summary>
        /// Apply LTS rules to convert a word to phonemes
        /// </summary>
        public string[] ApplyLTS(string word)
        {
            if (string.IsNullOrEmpty(word))
                return new string[0];

            // Normalize word
            word = word.ToLower().Trim();

            // Check cache
            if (cache.TryGetValue(word, out var cachedResult))
                return cachedResult;

            // Apply LTS rules
            var phonemes = ApplyLTSInternal(word);

            // Cache result
            CacheResult(word, phonemes);

            return phonemes;
        }

        private string[] ApplyLTSInternal(string word)
        {
            var phonemes = new List<string>();

            // Add word boundaries for context
            var paddedWord = PadWord(word);

            // Process each letter
            for (var i = 0; i < word.Length; i++)
            {
                var letterPhonemes = GetPhonemesForLetter(paddedWord, i + DEFAULT_CONTEXT_WINDOW);
                if (letterPhonemes != null && letterPhonemes.Length > 0)
                {
                    phonemes.AddRange(letterPhonemes);
                }
            }

            return phonemes.ToArray();
        }

        private string PadWord(string word)
        {
            // Add context padding
            var padding = new string('#', DEFAULT_CONTEXT_WINDOW);
            return padding + word + padding;
        }

        private string[] GetPhonemesForLetter(string paddedWord, int position)
        {
            var letter = paddedWord[position];

            // Get rule offset for this letter
            var offset = ruleSet.UseExtendedRules
                ? FliteLTSExtendedRules.GetLetterRuleOffset(letter)
                : FliteLTSRuleData.GetLetterRuleOffset(letter);
            if (offset < 0)
                return null;

            // Build context features
            var context = BuildContext(paddedWord, position);

            // Apply rules starting from letter's offset
            return ApplyRulesAtOffset(offset, context);
        }

        private LTSContext BuildContext(string paddedWord, int position)
        {
            var context = new LTSContext
            {
                Word = paddedWord,
                Position = position,
                WindowSize = DEFAULT_CONTEXT_WINDOW,
                // Extract context window
                LeftContext = new char[DEFAULT_CONTEXT_WINDOW],
                RightContext = new char[DEFAULT_CONTEXT_WINDOW]
            };

            for (var i = 0; i < DEFAULT_CONTEXT_WINDOW; i++)
            {
                // Left context (preceding letters)
                if (position - i - 1 >= 0)
                    context.LeftContext[i] = paddedWord[position - i - 1];
                else
                    context.LeftContext[i] = '#';

                // Right context (following letters)
                if (position + i + 1 < paddedWord.Length)
                    context.RightContext[i] = paddedWord[position + i + 1];
                else
                    context.RightContext[i] = '#';
            }

            return context;
        }

        private string[] ApplyRulesAtOffset(int offset, LTSContext context)
        {
            var phonemes = new List<string>();
            var rules = ruleSet.UseExtendedRules ? FliteLTSExtendedRules.Rules : FliteLTSRuleData.SimplifiedRules;

            // Start at the given offset
            var currentRule = offset;
            var iterations = 0;
            const int maxIterations = 100; // Prevent infinite loops

            while (currentRule < rules.Length && iterations < maxIterations)
            {
                iterations++;
                var rule = rules[currentRule];

                // Check if this is a terminal rule
                if (rule.Feature == 255) // Terminal marker
                {
                    if (rule.Value != 0) // 0 = epsilon (no phoneme)
                    {
                        var phoneme = FliteLTSData.GetPhoneByIndex(rule.Value);
                        if (!string.IsNullOrEmpty(phoneme))
                            phonemes.Add(phoneme);
                    }
                    break;
                }

                // Evaluate feature
                var matches = EvaluateFeature(rule.Feature, rule.Value, context);

                // Follow the appropriate branch
                if (matches)
                {
                    currentRule = rule.NextIfTrue;
                }
                else
                {
                    currentRule = rule.NextIfFalse;
                }

                // Safety check to prevent infinite loops
                if (currentRule == FliteLTSConstants.CST_LTS_EOR || currentRule >= rules.Length)
                    break;
            }

            // Fallback if no rules matched
            if (phonemes.Count == 0)
            {
                var letter = context.Word[context.Position];
                var defaultPhonemes = GetDefaultPhonemesForLetter(letter);
                if (defaultPhonemes != null)
                    phonemes.AddRange(defaultPhonemes);
            }

            return phonemes.ToArray();
        }

        private string[] GetDefaultPhonemesForLetter(char letter)
        {
            // Default phoneme mappings (simplified)
            return char.ToLower(letter) switch
            {
                'a' => new[] { "ae1" },
                'b' => new[] { "b" },
                'c' => new[] { "k" },
                'd' => new[] { "d" },
                'e' => new[] { "eh1" },
                'f' => new[] { "f" },
                'g' => new[] { "g" },
                'h' => new[] { "hh" },
                'i' => new[] { "ih1" },
                'j' => new[] { "jh" },
                'k' => new[] { "k" },
                'l' => new[] { "l" },
                'm' => new[] { "m" },
                'n' => new[] { "n" },
                'o' => new[] { "aa1" },
                'p' => new[] { "p" },
                'q' => new[] { "k", "w" },
                'r' => new[] { "r" },
                's' => new[] { "s" },
                't' => new[] { "t" },
                'u' => new[] { "ah1" },
                'v' => new[] { "v" },
                'w' => new[] { "w" },
                'x' => new[] { "k", "s" },
                'y' => new[] { "y" },
                'z' => new[] { "z" },
                _ => null,
            };
        }

        private void CacheResult(string word, string[] phonemes)
        {
            if (cache.Count >= maxCacheSize)
            {
                // Simple eviction - remove first entry
                var firstKey = cache.Keys.First();
                cache.Remove(firstKey);
            }

            cache[word] = phonemes;
        }

        /// <summary>
        /// Clear the LTS cache
        /// </summary>
        public void ClearCache()
        {
            cache.Clear();
        }

        /// <summary>
        /// Get current cache size
        /// </summary>
        public int CacheSize => cache.Count;

        /// <summary>
        /// Get memory usage estimate
        /// </summary>
        public long GetMemoryUsage()
        {
            long total = 0;

            // Cache memory
            foreach (var kvp in cache)
            {
                total += kvp.Key.Length * 2; // Unicode chars
                total += kvp.Value.Sum(p => p.Length * 2);
                total += 32; // Overhead
            }

            return total;
        }

        /// <summary>
        /// Context information for LTS rule application
        /// </summary>
        private class LTSContext
        {
            public string Word { get; set; }
            public int Position { get; set; }
            public int WindowSize { get; set; }
            public char[] LeftContext { get; set; }
            public char[] RightContext { get; set; }
        }

        /// <summary>
        /// Evaluate a feature against the context
        /// </summary>
        private bool EvaluateFeature(byte feature, byte value, LTSContext context)
        {
            var targetChar = (char)value;

            return feature switch
            {
                FliteLTSConstants.FEAT_CURRENT => context.Word[context.Position] == targetChar,
                FliteLTSConstants.FEAT_L1 => context.LeftContext[0] == targetChar,
                FliteLTSConstants.FEAT_L2 => context.LeftContext[1] == targetChar,
                FliteLTSConstants.FEAT_L3 => context.LeftContext[2] == targetChar,
                FliteLTSConstants.FEAT_L4 => context.LeftContext[3] == targetChar,
                FliteLTSConstants.FEAT_R1 => context.RightContext[0] == targetChar,
                FliteLTSConstants.FEAT_R2 => context.RightContext[1] == targetChar,
                FliteLTSConstants.FEAT_R3 => context.RightContext[2] == targetChar,
                FliteLTSConstants.FEAT_R4 => context.RightContext[3] == targetChar,
                _ => false,
            };
        }
    }

    /// <summary>
    /// LTS rule set containing rule data
    /// </summary>
    public class FliteLTSRuleSet
    {
        public string Name { get; set; }
        public byte[] RuleData { get; set; }
        public int[] LetterIndex { get; set; }
        public string[] PhoneTable { get; set; }
        public string[] LetterTable { get; set; }
        public int ContextWindowSize { get; set; } = 4;
        public bool UseExtendedRules { get; set; } = true;

        /// <summary>
        /// Create default rule set from Flite data
        /// </summary>
        public static FliteLTSRuleSet CreateDefault()
        {
            return new FliteLTSRuleSet
            {
                Name = "CMU LTS Rules",
                LetterIndex = FliteLTSData.LetterIndex,
                PhoneTable = FliteLTSData.PhoneTable,
                LetterTable = FliteLTSData.LetterTable,
                ContextWindowSize = 4,
                UseExtendedRules = true
            };
        }
    }
}