using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace uPiper.Core.Phonemizers.Backend.Chinese
{
    /// <summary>
    /// Processor for handling multi-tone characters in Chinese
    /// </summary>
    public class MultiToneProcessor
    {
        private readonly ChinesePinyinDictionary dictionary;
        private readonly Dictionary<char, MultiToneRule> multiToneRules;
        private readonly HashSet<char> multiToneCharacters;
        
        public MultiToneProcessor(ChinesePinyinDictionary dictionary)
        {
            this.dictionary = dictionary ?? throw new ArgumentNullException(nameof(dictionary));
            
            multiToneRules = new Dictionary<char, MultiToneRule>();
            multiToneCharacters = new HashSet<char>();
            
            InitializeMultiToneData();
            InitializeRules();
        }
        
        /// <summary>
        /// Initialize multi-tone character data from dictionary
        /// </summary>
        private void InitializeMultiToneData()
        {
            // Find all characters with multiple pronunciations
            foreach (var ch in GetAllCharacters())
            {
                if (dictionary.TryGetCharacterPinyin(ch, out var pinyinArray) && pinyinArray.Length > 1)
                {
                    multiToneCharacters.Add(ch);
                }
            }
            
            Debug.Log($"[MultiToneProcessor] Found {multiToneCharacters.Count} multi-tone characters");
        }
        
        /// <summary>
        /// Get all characters from dictionary
        /// </summary>
        private IEnumerable<char> GetAllCharacters()
        {
            return dictionary.GetAllCharacters();
        }
        
        /// <summary>
        /// Initialize tone sandhi and context rules
        /// </summary>
        private void InitializeRules()
        {
            // 不 - tone sandhi rule
            multiToneRules['不'] = new MultiToneRule
            {
                Character = '不',
                DefaultPinyin = "bu4",
                Rules = new List<ContextRule>
                {
                    new ContextRule
                    {
                        Description = "不 + 4th tone → bu2",
                        Condition = (context) => context.NextTone == 4,
                        ResultPinyin = "bu2"
                    }
                }
            };
            
            // 一 - complex tone sandhi
            multiToneRules['一'] = new MultiToneRule
            {
                Character = '一',
                DefaultPinyin = "yi1",
                Rules = new List<ContextRule>
                {
                    new ContextRule
                    {
                        Description = "一 + 4th tone → yi2",
                        Condition = (context) => context.NextTone == 4,
                        ResultPinyin = "yi2"
                    },
                    new ContextRule
                    {
                        Description = "一 + 1st/2nd/3rd tone → yi4",
                        Condition = (context) => context.NextTone >= 1 && context.NextTone <= 3,
                        ResultPinyin = "yi4"
                    }
                }
            };
            
            // 的 - most common multi-tone character
            multiToneRules['的'] = new MultiToneRule
            {
                Character = '的',
                DefaultPinyin = "de5", // Neutral tone by default (possessive)
                Rules = new List<ContextRule>
                {
                    new ContextRule
                    {
                        Description = "的确/的士 → di2",
                        Condition = (context) => context.NextChar == '确' || context.NextChar == '士',
                        ResultPinyin = "di2"
                    },
                    new ContextRule
                    {
                        Description = "目的 → di4",
                        Condition = (context) => context.PrevChar == '目',
                        ResultPinyin = "di4"
                    }
                }
            };
            
            // 了 - aspectual particle vs verb
            multiToneRules['了'] = new MultiToneRule
            {
                Character = '了',
                DefaultPinyin = "le5", // Neutral tone (aspectual particle)
                Rules = new List<ContextRule>
                {
                    new ContextRule
                    {
                        Description = "了解/了结 → liao3",
                        Condition = (context) => context.NextChar == '解' || context.NextChar == '结',
                        ResultPinyin = "liao3"
                    },
                    new ContextRule
                    {
                        Description = "为了 → liao3",
                        Condition = (context) => context.PrevChar == '为',
                        ResultPinyin = "liao3"
                    }
                }
            };
            
            // 着 - various uses
            multiToneRules['着'] = new MultiToneRule
            {
                Character = '着',
                DefaultPinyin = "zhe5", // Continuous aspect marker
                Rules = new List<ContextRule>
                {
                    new ContextRule
                    {
                        Description = "着急/着火 → zhao2",
                        Condition = (context) => context.NextChar == '急' || context.NextChar == '火',
                        ResultPinyin = "zhao2"
                    },
                    new ContextRule
                    {
                        Description = "着陆/着落 → zhuo2",
                        Condition = (context) => context.NextChar == '陆' || context.NextChar == '落',
                        ResultPinyin = "zhuo2"
                    }
                }
            };
            
            // 行 - xing2 vs hang2
            multiToneRules['行'] = new MultiToneRule
            {
                Character = '行',
                DefaultPinyin = "xing2", // "to walk, OK"
                Rules = new List<ContextRule>
                {
                    new ContextRule
                    {
                        Description = "银行/行业 → hang2",
                        Condition = (context) => context.PrevChar == '银' || context.NextChar == '业',
                        ResultPinyin = "hang2"
                    }
                }
            };
            
            // 长 - chang2 vs zhang3
            multiToneRules['长'] = new MultiToneRule
            {
                Character = '长',
                DefaultPinyin = "chang2", // "long"
                Rules = new List<ContextRule>
                {
                    new ContextRule
                    {
                        Description = "长大/成长/生长 → zhang3",
                        Condition = (context) => 
                            context.NextChar == '大' || 
                            context.PrevChar == '成' || 
                            context.PrevChar == '生',
                        ResultPinyin = "zhang3"
                    }
                }
            };
            
            // Add more rules as needed...
        }
        
        /// <summary>
        /// Get the best pronunciation for a character based on context
        /// </summary>
        public string GetBestPronunciation(char character, PronunciationContext context)
        {
            // Check if it's a multi-tone character with rules
            if (multiToneRules.TryGetValue(character, out var rule))
            {
                Debug.Log($"[MultiToneProcessor] Processing '{character}' with context: PrevChar='{context.PrevChar}', NextChar='{context.NextChar}', NextTone={context.NextTone}");
                
                // Apply rules in order
                foreach (var contextRule in rule.Rules)
                {
                    if (contextRule.Condition(context))
                    {
                        Debug.Log($"[MultiToneProcessor] Rule matched: {contextRule.Description} → {contextRule.ResultPinyin}");
                        return contextRule.ResultPinyin;
                    }
                }
                
                // Return default if no rule matches
                Debug.Log($"[MultiToneProcessor] No rule matched, using default: {rule.DefaultPinyin}");
                return rule.DefaultPinyin;
            }
            
            // For characters without specific rules, return first pronunciation
            if (dictionary.TryGetCharacterPinyin(character, out var pinyinArray))
            {
                return pinyinArray[0];
            }
            
            // Fallback
            return null;
        }
        
        /// <summary>
        /// Check if a character is multi-tone
        /// </summary>
        public bool IsMultiTone(char character)
        {
            return multiToneCharacters.Contains(character);
        }
        
        /// <summary>
        /// Get statistics about multi-tone characters
        /// </summary>
        public MultiToneStatistics GetStatistics()
        {
            return new MultiToneStatistics
            {
                TotalMultiToneCharacters = multiToneCharacters.Count,
                CharactersWithRules = multiToneRules.Count,
                TotalRules = multiToneRules.Values.Sum(r => r.Rules.Count)
            };
        }
    }
    
    /// <summary>
    /// Rule for a multi-tone character
    /// </summary>
    public class MultiToneRule
    {
        public char Character { get; set; }
        public string DefaultPinyin { get; set; }
        public List<ContextRule> Rules { get; set; } = new List<ContextRule>();
    }
    
    /// <summary>
    /// Context-based pronunciation rule
    /// </summary>
    public class ContextRule
    {
        public string Description { get; set; }
        public Func<PronunciationContext, bool> Condition { get; set; }
        public string ResultPinyin { get; set; }
    }
    
    /// <summary>
    /// Context information for pronunciation selection
    /// </summary>
    public class PronunciationContext
    {
        public char Character { get; set; }
        public char? PrevChar { get; set; }
        public char? NextChar { get; set; }
        public string PrevWord { get; set; }
        public string NextWord { get; set; }
        public int? PrevTone { get; set; }
        public int? NextTone { get; set; }
        public string Phrase { get; set; } // Full phrase if available
    }
    
    /// <summary>
    /// Statistics about multi-tone processing
    /// </summary>
    public class MultiToneStatistics
    {
        public int TotalMultiToneCharacters { get; set; }
        public int CharactersWithRules { get; set; }
        public int TotalRules { get; set; }
    }
}