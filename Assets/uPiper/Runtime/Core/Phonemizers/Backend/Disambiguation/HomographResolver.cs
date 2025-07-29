using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace uPiper.Core.Phonemizers.Backend.Disambiguation
{
    /// <summary>
    /// Resolves homographs (words with same spelling but different pronunciations)
    /// based on context and part-of-speech tagging.
    /// </summary>
    public class HomographResolver
    {
        private readonly Dictionary<string, HomographEntry> homographs;

        public HomographResolver()
        {
            homographs = InitializeHomographs();
        }

        /// <summary>
        /// Attempts to resolve a homograph based on context.
        /// </summary>
        public bool TryResolve(string word, string sentence, out string[] phonemes)
        {
            phonemes = null;
            var lowerWord = word.ToLower();

            if (!homographs.ContainsKey(lowerWord))
                return false;

            var entry = homographs[lowerWord];
            var pos = EstimatePartOfSpeech(word, sentence);

            phonemes = SelectPronunciation(entry, pos, sentence);
            return phonemes != null;
        }

        private PartOfSpeech EstimatePartOfSpeech(string word, string sentence)
        {
            // Simple heuristic-based POS tagging
            var words = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var wordIndex = Array.FindIndex(words, w =>
                w.Equals(word, StringComparison.OrdinalIgnoreCase) ||
                w.TrimEnd('.', ',', '!', '?').Equals(word, StringComparison.OrdinalIgnoreCase));

            if (wordIndex < 0)
                return PartOfSpeech.Unknown;

            // Check previous word for determiners/auxiliaries
            if (wordIndex > 0)
            {
                var prevWord = words[wordIndex - 1].ToLower();

                // Determiners suggest noun
                if (IsDeTerminer(prevWord))
                    return PartOfSpeech.Noun;

                // Modal/auxiliary verbs suggest verb
                if (IsAuxiliary(prevWord))
                    return PartOfSpeech.Verb;

                // "to" often precedes verbs
                if (prevWord == "to")
                    return PartOfSpeech.Verb;
            }

            // Check following word
            if (wordIndex < words.Length - 1)
            {
                var nextWord = words[wordIndex + 1].ToLower();

                // Followed by noun suggests adjective/determiner
                if (IsLikelyNoun(nextWord))
                    return PartOfSpeech.Adjective;
            }

            // Check word endings
            var lowerWord = word.ToLower();
            if (lowerWord.EndsWith("ing") || lowerWord.EndsWith("ed"))
                return PartOfSpeech.Verb;

            if (lowerWord.EndsWith("ly"))
                return PartOfSpeech.Adverb;

            // Default based on position
            if (wordIndex == 0)
                return PartOfSpeech.Noun; // Sentence start often noun/subject

            return PartOfSpeech.Unknown;
        }

        private bool IsDeTerminer(string word)
        {
            var determiners = new HashSet<string>
            {
                "the", "a", "an", "this", "that", "these", "those",
                "my", "your", "his", "her", "its", "our", "their",
                "some", "many", "few", "all", "no"
            };
            return determiners.Contains(word);
        }

        private bool IsAuxiliary(string word)
        {
            var auxiliaries = new HashSet<string>
            {
                "will", "would", "can", "could", "may", "might",
                "shall", "should", "must", "ought", "do", "does",
                "did", "have", "has", "had", "am", "is", "are",
                "was", "were", "been", "being"
            };
            return auxiliaries.Contains(word);
        }

        private bool IsLikelyNoun(string word)
        {
            // Simple heuristic - words ending in common noun suffixes
            return word.EndsWith("tion") || word.EndsWith("ment") ||
                   word.EndsWith("ness") || word.EndsWith("ity");
        }

        private string[] SelectPronunciation(HomographEntry entry, PartOfSpeech pos, string sentence)
        {
            // Check specific rules first
            if (entry.Rules != null)
            {
                foreach (var rule in entry.Rules)
                {
                    if (rule.Matches(sentence))
                        return rule.Phonemes;
                }
            }

            // Then check POS-based pronunciations
            return pos switch
            {
                PartOfSpeech.Noun => entry.NounPhonemes ?? entry.DefaultPhonemes,
                PartOfSpeech.Verb => entry.VerbPhonemes ?? entry.DefaultPhonemes,
                PartOfSpeech.Adjective => entry.AdjectivePhonemes ?? entry.DefaultPhonemes,
                PartOfSpeech.Adverb => entry.AdverbPhonemes ?? entry.DefaultPhonemes,
                _ => entry.DefaultPhonemes,
            };
        }

        private Dictionary<string, HomographEntry> InitializeHomographs()
        {
            return new Dictionary<string, HomographEntry>
            {
                ["read"] = new HomographEntry
                {
                    DefaultPhonemes = new[] { "R", "IY1", "D" },
                    VerbPhonemes = new[] { "R", "IY1", "D" }, // present
                    Rules = new[]
                    {
                        new ContextRule
                        {
                            Pattern = @"\b(have|has|had|having)\s+read\b",
                            Phonemes = new[] { "R", "EH1", "D" } // past participle
                        },
                        new ContextRule
                        {
                            Pattern = @"\bread\s+(the|a|an)\b",
                            Phonemes = new[] { "R", "EH1", "D" } // past tense
                        }
                    }
                },

                ["lead"] = new HomographEntry
                {
                    NounPhonemes = new[] { "L", "EH1", "D" }, // metal
                    VerbPhonemes = new[] { "L", "IY1", "D" }, // to guide
                    DefaultPhonemes = new[] { "L", "IY1", "D" }
                },

                ["tear"] = new HomographEntry
                {
                    NounPhonemes = new[] { "T", "IH1", "R" }, // drop from eye
                    VerbPhonemes = new[] { "T", "EH1", "R" }, // to rip
                    DefaultPhonemes = new[] { "T", "IH1", "R" }
                },

                ["bow"] = new HomographEntry
                {
                    NounPhonemes = new[] { "B", "OW1" }, // weapon/ribbon
                    VerbPhonemes = new[] { "B", "AW1" }, // to bend
                    DefaultPhonemes = new[] { "B", "OW1" }
                },

                ["live"] = new HomographEntry
                {
                    VerbPhonemes = new[] { "L", "IH1", "V" }, // to exist
                    AdjectivePhonemes = new[] { "L", "AY1", "V" }, // not dead
                    DefaultPhonemes = new[] { "L", "IH1", "V" }
                },

                ["wind"] = new HomographEntry
                {
                    NounPhonemes = new[] { "W", "IH1", "N", "D" }, // air movement
                    VerbPhonemes = new[] { "W", "AY1", "N", "D" }, // to coil
                    DefaultPhonemes = new[] { "W", "IH1", "N", "D" }
                },

                ["close"] = new HomographEntry
                {
                    VerbPhonemes = new[] { "K", "L", "OW1", "Z" }, // to shut
                    AdjectivePhonemes = new[] { "K", "L", "OW1", "S" }, // nearby
                    DefaultPhonemes = new[] { "K", "L", "OW1", "Z" }
                },

                ["present"] = new HomographEntry
                {
                    NounPhonemes = new[] { "P", "R", "EH1", "Z", "AH0", "N", "T" }, // gift
                    VerbPhonemes = new[] { "P", "R", "IH0", "Z", "EH1", "N", "T" }, // to show
                    AdjectivePhonemes = new[] { "P", "R", "EH1", "Z", "AH0", "N", "T" }, // current
                    DefaultPhonemes = new[] { "P", "R", "EH1", "Z", "AH0", "N", "T" }
                },

                ["object"] = new HomographEntry
                {
                    NounPhonemes = new[] { "AA1", "B", "JH", "EH0", "K", "T" }, // thing
                    VerbPhonemes = new[] { "AH0", "B", "JH", "EH1", "K", "T" }, // to protest
                    DefaultPhonemes = new[] { "AA1", "B", "JH", "EH0", "K", "T" }
                },

                ["refuse"] = new HomographEntry
                {
                    NounPhonemes = new[] { "R", "EH1", "F", "Y", "UW2", "S" }, // garbage
                    VerbPhonemes = new[] { "R", "IH0", "F", "Y", "UW1", "Z" }, // to decline
                    DefaultPhonemes = new[] { "R", "IH0", "F", "Y", "UW1", "Z" }
                },

                ["record"] = new HomographEntry
                {
                    NounPhonemes = new[] { "R", "EH1", "K", "ER0", "D" }, // album/data
                    VerbPhonemes = new[] { "R", "IH0", "K", "AO1", "R", "D" }, // to capture
                    DefaultPhonemes = new[] { "R", "EH1", "K", "ER0", "D" }
                },

                ["produce"] = new HomographEntry
                {
                    NounPhonemes = new[] { "P", "R", "OW1", "D", "UW0", "S" }, // vegetables
                    VerbPhonemes = new[] { "P", "R", "AH0", "D", "UW1", "S" }, // to create
                    DefaultPhonemes = new[] { "P", "R", "AH0", "D", "UW1", "S" }
                },

                ["minute"] = new HomographEntry
                {
                    NounPhonemes = new[] { "M", "IH1", "N", "AH0", "T" }, // 60 seconds
                    AdjectivePhonemes = new[] { "M", "AY0", "N", "UW1", "T" }, // tiny
                    DefaultPhonemes = new[] { "M", "IH1", "N", "AH0", "T" }
                },

                ["content"] = new HomographEntry
                {
                    NounPhonemes = new[] { "K", "AA1", "N", "T", "EH0", "N", "T" }, // material
                    AdjectivePhonemes = new[] { "K", "AH0", "N", "T", "EH1", "N", "T" }, // satisfied
                    DefaultPhonemes = new[] { "K", "AA1", "N", "T", "EH0", "N", "T" }
                },

                ["desert"] = new HomographEntry
                {
                    NounPhonemes = new[] { "D", "EH1", "Z", "ER0", "T" }, // dry land
                    VerbPhonemes = new[] { "D", "IH0", "Z", "ER1", "T" }, // to abandon
                    DefaultPhonemes = new[] { "D", "EH1", "Z", "ER0", "T" }
                }
            };
        }

        private class HomographEntry
        {
            public string[] DefaultPhonemes { get; set; }
            public string[] NounPhonemes { get; set; }
            public string[] VerbPhonemes { get; set; }
            public string[] AdjectivePhonemes { get; set; }
            public string[] AdverbPhonemes { get; set; }
            public ContextRule[] Rules { get; set; }
        }

        private class ContextRule
        {
            public string Pattern { get; set; }
            public string[] Phonemes { get; set; }

            public bool Matches(string sentence)
            {
                return Regex.IsMatch(sentence, Pattern, RegexOptions.IgnoreCase);
            }
        }

        private enum PartOfSpeech
        {
            Unknown,
            Noun,
            Verb,
            Adjective,
            Adverb
        }
    }
}