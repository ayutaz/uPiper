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
            this.cache = new Dictionary<string, string[]>(maxCacheSize);
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
            for (int i = 0; i < word.Length; i++)
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
                WindowSize = DEFAULT_CONTEXT_WINDOW
            };
            
            // Extract context window
            context.LeftContext = new char[DEFAULT_CONTEXT_WINDOW];
            context.RightContext = new char[DEFAULT_CONTEXT_WINDOW];
            
            for (int i = 0; i < DEFAULT_CONTEXT_WINDOW; i++)
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
            int currentRule = offset;
            int iterations = 0;
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
                bool matches = EvaluateFeature(rule.Feature, rule.Value, context);
                
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
            switch (char.ToLower(letter))
            {
                case 'a': return new[] { "ae1" };
                case 'b': return new[] { "b" };
                case 'c': return new[] { "k" };
                case 'd': return new[] { "d" };
                case 'e': return new[] { "eh1" };
                case 'f': return new[] { "f" };
                case 'g': return new[] { "g" };
                case 'h': return new[] { "hh" };
                case 'i': return new[] { "ih1" };
                case 'j': return new[] { "jh" };
                case 'k': return new[] { "k" };
                case 'l': return new[] { "l" };
                case 'm': return new[] { "m" };
                case 'n': return new[] { "n" };
                case 'o': return new[] { "aa1" };
                case 'p': return new[] { "p" };
                case 'q': return new[] { "k", "w" };
                case 'r': return new[] { "r" };
                case 's': return new[] { "s" };
                case 't': return new[] { "t" };
                case 'u': return new[] { "ah1" };
                case 'v': return new[] { "v" };
                case 'w': return new[] { "w" };
                case 'x': return new[] { "k", "s" };
                case 'y': return new[] { "y" };
                case 'z': return new[] { "z" };
                default: return null;
            }
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
            char targetChar = (char)value;
            
            switch (feature)
            {
                case FliteLTSConstants.FEAT_CURRENT:
                    return context.Word[context.Position] == targetChar;
                    
                case FliteLTSConstants.FEAT_L1:
                    return context.LeftContext[0] == targetChar;
                case FliteLTSConstants.FEAT_L2:
                    return context.LeftContext[1] == targetChar;
                case FliteLTSConstants.FEAT_L3:
                    return context.LeftContext[2] == targetChar;
                case FliteLTSConstants.FEAT_L4:
                    return context.LeftContext[3] == targetChar;
                    
                case FliteLTSConstants.FEAT_R1:
                    return context.RightContext[0] == targetChar;
                case FliteLTSConstants.FEAT_R2:
                    return context.RightContext[1] == targetChar;
                case FliteLTSConstants.FEAT_R3:
                    return context.RightContext[2] == targetChar;
                case FliteLTSConstants.FEAT_R4:
                    return context.RightContext[3] == targetChar;
                    
                default:
                    return false;
            }
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