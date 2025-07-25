using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace uPiper.Phonemizers.Backend.Flite
{
    /// <summary>
    /// Flite Letter-to-Sound (LTS) rules implementation
    /// Generates phonemes for out-of-vocabulary words
    /// </summary>
    public class FliteLetterToSound : IDisposable
    {
        private readonly Dictionary<string, List<LTSRule>> rulesByLanguage;
        private readonly Dictionary<string, PhonemeInventory> phonemeInventories;

        public FliteLetterToSound()
        {
            rulesByLanguage = new Dictionary<string, List<LTSRule>>();
            phonemeInventories = new Dictionary<string, PhonemeInventory>();
            InitializeRules();
        }

        /// <summary>
        /// Generate phonemes for a word using LTS rules
        /// </summary>
        public List<string> GeneratePhonemes(string word, string language)
        {
            if (!rulesByLanguage.TryGetValue(language, out var rules))
            {
                // Default to basic English rules
                rules = rulesByLanguage["en-US"];
            }

            var normalizedWord = word.ToLower();
            var phonemes = new List<string>();
            var context = new LTSContext(normalizedWord);

            while (context.Position < normalizedWord.Length)
            {
                bool ruleApplied = false;

                // Try to apply rules in order
                foreach (var rule in rules)
                {
                    if (rule.Matches(context))
                    {
                        var output = rule.Apply(context);
                        if (!string.IsNullOrEmpty(output))
                        {
                            phonemes.AddRange(output.Split(' '));
                        }
                        ruleApplied = true;
                        break;
                    }
                }

                if (!ruleApplied)
                {
                    // No rule matched, use default mapping
                    var phoneme = GetDefaultPhoneme(context.CurrentChar, language);
                    if (!string.IsNullOrEmpty(phoneme))
                    {
                        phonemes.Add(phoneme);
                    }
                    context.Advance();
                }
            }

            return PostProcessPhonemes(phonemes, language);
        }

        /// <summary>
        /// Initialize LTS rules for supported languages
        /// </summary>
        private void InitializeRules()
        {
            // Initialize English LTS rules
            var englishRules = new List<LTSRule>();

            // Consonant clusters
            englishRules.Add(new LTSRule("ch", "ch", null, null));
            englishRules.Add(new LTSRule("sh", "sh", null, null));
            englishRules.Add(new LTSRule("th", "th", null, null)); // simplified - should distinguish voiced/unvoiced
            englishRules.Add(new LTSRule("ph", "f", null, null));
            englishRules.Add(new LTSRule("gh", "", null, null)); // often silent
            englishRules.Add(new LTSRule("ck", "k", null, null));
            englishRules.Add(new LTSRule("qu", "k w", null, null));
            englishRules.Add(new LTSRule("wh", "w", null, null));
            englishRules.Add(new LTSRule("wr", "r", null, null));
            englishRules.Add(new LTSRule("kn", "n", null, null));
            englishRules.Add(new LTSRule("gn", "n", null, null));
            englishRules.Add(new LTSRule("ps", "s", null, null));
            englishRules.Add(new LTSRule("ng", "ng", null, null));

            // Vowel digraphs
            englishRules.Add(new LTSRule("ee", "iy", null, null));
            englishRules.Add(new LTSRule("ea", "iy", null, null));
            englishRules.Add(new LTSRule("ie", "iy", null, null));
            englishRules.Add(new LTSRule("ei", "ey", null, null));
            englishRules.Add(new LTSRule("ay", "ey", null, null));
            englishRules.Add(new LTSRule("ai", "ey", null, null));
            englishRules.Add(new LTSRule("oa", "ow", null, null));
            englishRules.Add(new LTSRule("ow", "ow", null, null));
            englishRules.Add(new LTSRule("ou", "aw", null, null));
            englishRules.Add(new LTSRule("oo", "uw", null, null));
            englishRules.Add(new LTSRule("oi", "oy", null, null));
            englishRules.Add(new LTSRule("oy", "oy", null, null));
            englishRules.Add(new LTSRule("au", "ao", null, null));
            englishRules.Add(new LTSRule("aw", "ao", null, null));

            // Context-dependent rules
            englishRules.Add(new LTSRule("c", "s", null, "e|i|y"));  // c -> s before e, i, y
            englishRules.Add(new LTSRule("c", "k", null, null));     // c -> k otherwise
            englishRules.Add(new LTSRule("g", "jh", null, "e|i|y")); // g -> j before e, i, y (simplified)
            englishRules.Add(new LTSRule("g", "g", null, null));     // g -> g otherwise
            englishRules.Add(new LTSRule("x", "k s", null, null));   // x -> ks
            englishRules.Add(new LTSRule("y", "ay", "\\b", null));   // y -> ay at word start
            englishRules.Add(new LTSRule("y", "iy", null, "\\b"));   // y -> iy at word end
            englishRules.Add(new LTSRule("y", "ih", null, null));    // y -> ih otherwise

            // Silent e rules
            englishRules.Add(new LTSRule("e", "", "[a-z]", "\\b"));  // silent e at end after consonant

            // Single consonants
            englishRules.Add(new LTSRule("b", "b", null, null));
            englishRules.Add(new LTSRule("d", "d", null, null));
            englishRules.Add(new LTSRule("f", "f", null, null));
            englishRules.Add(new LTSRule("h", "hh", null, null));
            englishRules.Add(new LTSRule("j", "jh", null, null));
            englishRules.Add(new LTSRule("k", "k", null, null));
            englishRules.Add(new LTSRule("l", "l", null, null));
            englishRules.Add(new LTSRule("m", "m", null, null));
            englishRules.Add(new LTSRule("n", "n", null, null));
            englishRules.Add(new LTSRule("p", "p", null, null));
            englishRules.Add(new LTSRule("r", "r", null, null));
            englishRules.Add(new LTSRule("s", "s", null, null));
            englishRules.Add(new LTSRule("t", "t", null, null));
            englishRules.Add(new LTSRule("v", "v", null, null));
            englishRules.Add(new LTSRule("w", "w", null, null));
            englishRules.Add(new LTSRule("z", "z", null, null));

            // Single vowels (default rules)
            englishRules.Add(new LTSRule("a", "ae", null, null));
            englishRules.Add(new LTSRule("e", "eh", null, null));
            englishRules.Add(new LTSRule("i", "ih", null, null));
            englishRules.Add(new LTSRule("o", "aa", null, null));
            englishRules.Add(new LTSRule("u", "ah", null, null));

            // Sort rules by length (longer patterns first)
            englishRules.Sort((a, b) => b.Pattern.Length.CompareTo(a.Pattern.Length));

            rulesByLanguage["en-US"] = englishRules;
            rulesByLanguage["en-GB"] = new List<LTSRule>(englishRules);
            rulesByLanguage["en-IN"] = new List<LTSRule>(englishRules);

            // Initialize phoneme inventories
            InitializePhonemeInventories();
        }

        /// <summary>
        /// Initialize phoneme inventories for each language
        /// </summary>
        private void InitializePhonemeInventories()
        {
            var englishInventory = new PhonemeInventory
            {
                Vowels = new HashSet<string> { "aa", "ae", "ah", "ao", "aw", "ax", "ay", 
                                              "eh", "er", "ey", "ih", "iy", "ow", "oy", 
                                              "uh", "uw", "ux" },
                Consonants = new HashSet<string> { "b", "ch", "d", "dh", "f", "g", "hh", 
                                                  "jh", "k", "l", "m", "n", "ng", "p", 
                                                  "r", "s", "sh", "t", "th", "v", "w", 
                                                  "y", "z", "zh" },
                Silence = new HashSet<string> { "pau", "sil" }
            };

            phonemeInventories["en-US"] = englishInventory;
            phonemeInventories["en-GB"] = englishInventory;
            phonemeInventories["en-IN"] = englishInventory;
        }

        /// <summary>
        /// Get default phoneme mapping for a character
        /// </summary>
        private string GetDefaultPhoneme(char c, string language)
        {
            // Basic fallback mapping
            return c switch
            {
                'a' => "ae",
                'e' => "eh",
                'i' => "ih",
                'o' => "aa",
                'u' => "ah",
                'b' => "b",
                'c' => "k",
                'd' => "d",
                'f' => "f",
                'g' => "g",
                'h' => "hh",
                'j' => "jh",
                'k' => "k",
                'l' => "l",
                'm' => "m",
                'n' => "n",
                'p' => "p",
                'r' => "r",
                's' => "s",
                't' => "t",
                'v' => "v",
                'w' => "w",
                'x' => "k s",
                'y' => "y",
                'z' => "z",
                _ => ""
            };
        }

        /// <summary>
        /// Post-process generated phonemes
        /// </summary>
        private List<string> PostProcessPhonemes(List<string> phonemes, string language)
        {
            var processed = new List<string>();

            for (int i = 0; i < phonemes.Count; i++)
            {
                var phoneme = phonemes[i];

                // Skip empty phonemes
                if (string.IsNullOrEmpty(phoneme))
                    continue;

                // Validate phoneme
                if (IsValidPhoneme(phoneme, language))
                {
                    processed.Add(phoneme);
                }
                else
                {
                    // Try to map to closest valid phoneme
                    var mapped = MapToValidPhoneme(phoneme, language);
                    if (!string.IsNullOrEmpty(mapped))
                    {
                        processed.Add(mapped);
                    }
                }
            }

            return processed;
        }

        /// <summary>
        /// Check if a phoneme is valid for the language
        /// </summary>
        private bool IsValidPhoneme(string phoneme, string language)
        {
            if (phonemeInventories.TryGetValue(language, out var inventory))
            {
                return inventory.IsValid(phoneme);
            }
            return true; // Accept all if no inventory defined
        }

        /// <summary>
        /// Map an invalid phoneme to the closest valid one
        /// </summary>
        private string MapToValidPhoneme(string phoneme, string language)
        {
            // Simple mapping strategy - could be improved
            return phoneme;
        }

        public void Dispose()
        {
            rulesByLanguage.Clear();
            phonemeInventories.Clear();
        }

        /// <summary>
        /// LTS rule definition
        /// </summary>
        private class LTSRule
        {
            public string Pattern { get; }
            public string Output { get; }
            public string LeftContext { get; }
            public string RightContext { get; }

            public LTSRule(string pattern, string output, string leftContext, string rightContext)
            {
                Pattern = pattern;
                Output = output;
                LeftContext = leftContext;
                RightContext = rightContext;
            }

            public bool Matches(LTSContext context)
            {
                // Check if pattern matches at current position
                if (!context.MatchesAt(Pattern))
                    return false;

                // Check left context if specified
                if (!string.IsNullOrEmpty(LeftContext))
                {
                    var leftPart = context.GetLeft(1);
                    if (!Regex.IsMatch(leftPart, LeftContext))
                        return false;
                }

                // Check right context if specified
                if (!string.IsNullOrEmpty(RightContext))
                {
                    var rightPart = context.GetRight(Pattern.Length, 1);
                    if (!Regex.IsMatch(rightPart, RightContext))
                        return false;
                }

                return true;
            }

            public string Apply(LTSContext context)
            {
                context.Advance(Pattern.Length);
                return Output;
            }
        }

        /// <summary>
        /// Context for LTS processing
        /// </summary>
        private class LTSContext
        {
            private readonly string word;
            public int Position { get; private set; }

            public LTSContext(string word)
            {
                this.word = word;
                Position = 0;
            }

            public char CurrentChar => Position < word.Length ? word[Position] : '\0';

            public bool MatchesAt(string pattern)
            {
                if (Position + pattern.Length > word.Length)
                    return false;

                return word.Substring(Position, pattern.Length) == pattern;
            }

            public string GetLeft(int length)
            {
                int start = Math.Max(0, Position - length);
                int len = Position - start;
                return len > 0 ? word.Substring(start, len) : "\\b";
            }

            public string GetRight(int skip, int length)
            {
                int start = Position + skip;
                if (start >= word.Length)
                    return "\\b";

                int len = Math.Min(length, word.Length - start);
                return word.Substring(start, len);
            }

            public void Advance(int count = 1)
            {
                Position = Math.Min(Position + count, word.Length);
            }
        }

        /// <summary>
        /// Phoneme inventory for a language
        /// </summary>
        private class PhonemeInventory
        {
            public HashSet<string> Vowels { get; set; }
            public HashSet<string> Consonants { get; set; }
            public HashSet<string> Silence { get; set; }

            public bool IsValid(string phoneme)
            {
                return Vowels.Contains(phoneme) || 
                       Consonants.Contains(phoneme) || 
                       Silence.Contains(phoneme);
            }
        }
    }
}