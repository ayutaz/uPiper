using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace uPiper.Core.Phonemizers.Backend.Flite
{
    /// <summary>
    /// Flite lexicon implementation
    /// Manages pronunciation dictionaries for various languages
    /// </summary>
    public class FliteLexicon : IDisposable
    {
        private readonly Dictionary<string, Dictionary<string, List<string>>> lexicons;
        private readonly object syncLock = new();

        public FliteLexicon()
        {
            lexicons = new Dictionary<string, Dictionary<string, List<string>>>();
            InitializeBuiltInLexicons();
        }

        /// <summary>
        /// Look up pronunciation for a word
        /// </summary>
        public List<string> Lookup(string word, string language)
        {
            lock (syncLock)
            {
                if (lexicons.TryGetValue(language, out var lexicon))
                {
                    if (lexicon.TryGetValue(word.ToLower(), out var phonemes))
                    {
                        return new List<string>(phonemes);
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// Add a word to the lexicon
        /// </summary>
        public void AddWord(string word, List<string> phonemes, string language)
        {
            lock (syncLock)
            {
                if (!lexicons.ContainsKey(language))
                {
                    lexicons[language] = new Dictionary<string, List<string>>();
                }

                lexicons[language][word.ToLower()] = new List<string>(phonemes);
            }
        }

        /// <summary>
        /// Load lexicon from file
        /// </summary>
        public bool LoadLexicon(string filePath, string language)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Debug.LogWarning($"Lexicon file not found: {filePath}");
                    return false;
                }

                var lines = File.ReadAllLines(filePath);
                var lexicon = new Dictionary<string, List<string>>();

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        var word = parts[0].ToLower();
                        var phonemes = parts.Skip(1).ToList();
                        lexicon[word] = phonemes;
                    }
                }

                lock (syncLock)
                {
                    lexicons[language] = lexicon;
                }

                Debug.Log($"Loaded {lexicon.Count} words for language {language}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load lexicon: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Initialize built-in lexicons with common words
        /// </summary>
        private void InitializeBuiltInLexicons()
        {
            // Initialize US English lexicon with common words
            var usEnglish = new Dictionary<string, List<string>>
            {
                // Common words
                ["the"] = new List<string> { "dh", "ax" },
                ["a"] = new List<string> { "ax" },
                ["an"] = new List<string> { "ax", "n" },
                ["and"] = new List<string> { "ae", "n", "d" },
                ["of"] = new List<string> { "ax", "v" },
                ["to"] = new List<string> { "t", "uw" },
                ["in"] = new List<string> { "ih", "n" },
                ["is"] = new List<string> { "ih", "z" },
                ["it"] = new List<string> { "ih", "t" },
                ["that"] = new List<string> { "dh", "ae", "t" },
                ["for"] = new List<string> { "f", "ao", "r" },
                ["was"] = new List<string> { "w", "aa", "z" },
                ["with"] = new List<string> { "w", "ih", "dh" },
                ["be"] = new List<string> { "b", "iy" },
                ["by"] = new List<string> { "b", "ay" },
                ["have"] = new List<string> { "hh", "ae", "v" },
                ["from"] = new List<string> { "f", "r", "ah", "m" },
                ["or"] = new List<string> { "ao", "r" },
                ["as"] = new List<string> { "ae", "z" },
                ["what"] = new List<string> { "w", "ah", "t" },
                ["all"] = new List<string> { "ao", "l" },
                ["would"] = new List<string> { "w", "uh", "d" },
                ["there"] = new List<string> { "dh", "eh", "r" },
                ["their"] = new List<string> { "dh", "eh", "r" },

                // Numbers
                ["one"] = new List<string> { "w", "ah", "n" },
                ["two"] = new List<string> { "t", "uw" },
                ["three"] = new List<string> { "th", "r", "iy" },
                ["four"] = new List<string> { "f", "ao", "r" },
                ["five"] = new List<string> { "f", "ay", "v" },
                ["six"] = new List<string> { "s", "ih", "k", "s" },
                ["seven"] = new List<string> { "s", "eh", "v", "ax", "n" },
                ["eight"] = new List<string> { "ey", "t" },
                ["nine"] = new List<string> { "n", "ay", "n" },
                ["ten"] = new List<string> { "t", "eh", "n" },

                // Common verbs
                ["go"] = new List<string> { "g", "ow" },
                ["get"] = new List<string> { "g", "eh", "t" },
                ["make"] = new List<string> { "m", "ey", "k" },
                ["know"] = new List<string> { "n", "ow" },
                ["think"] = new List<string> { "th", "ih", "ng", "k" },
                ["take"] = new List<string> { "t", "ey", "k" },
                ["see"] = new List<string> { "s", "iy" },
                ["come"] = new List<string> { "k", "ah", "m" },
                ["want"] = new List<string> { "w", "aa", "n", "t" },
                ["use"] = new List<string> { "y", "uw", "z" },

                // Common nouns
                ["time"] = new List<string> { "t", "ay", "m" },
                ["person"] = new List<string> { "p", "er", "s", "ax", "n" },
                ["year"] = new List<string> { "y", "ih", "r" },
                ["way"] = new List<string> { "w", "ey" },
                ["day"] = new List<string> { "d", "ey" },
                ["man"] = new List<string> { "m", "ae", "n" },
                ["thing"] = new List<string> { "th", "ih", "ng" },
                ["woman"] = new List<string> { "w", "uh", "m", "ax", "n" },
                ["life"] = new List<string> { "l", "ay", "f" },
                ["child"] = new List<string> { "ch", "ay", "l", "d" },
                ["world"] = new List<string> { "w", "er", "l", "d" },

                // Technology terms
                ["computer"] = new List<string> { "k", "ax", "m", "p", "y", "uw", "t", "er" },
                ["software"] = new List<string> { "s", "ao", "f", "t", "w", "eh", "r" },
                ["hardware"] = new List<string> { "hh", "aa", "r", "d", "w", "eh", "r" },
                ["internet"] = new List<string> { "ih", "n", "t", "er", "n", "eh", "t" },
                ["email"] = new List<string> { "iy", "m", "ey", "l" },
                ["website"] = new List<string> { "w", "eh", "b", "s", "ay", "t" },
                ["data"] = new List<string> { "d", "ey", "t", "ax" },
                ["file"] = new List<string> { "f", "ay", "l" },
                ["system"] = new List<string> { "s", "ih", "s", "t", "ax", "m" }
            };

            lexicons["en-US"] = usEnglish;

            // Copy US English to other English variants with modifications
            lexicons["en-GB"] = new Dictionary<string, List<string>>(usEnglish);
            lexicons["en-IN"] = new Dictionary<string, List<string>>(usEnglish);

            // Add some British English specific pronunciations
            lexicons["en-GB"]["schedule"] = new List<string> { "sh", "eh", "d", "y", "uw", "l" };
            lexicons["en-GB"]["privacy"] = new List<string> { "p", "r", "ih", "v", "ax", "s", "iy" };
            lexicons["en-GB"]["aluminium"] = new List<string> { "ae", "l", "y", "uw", "m", "ih", "n", "iy", "ax", "m" };
        }

        /// <summary>
        /// Get lexicon size for a language
        /// </summary>
        public int GetLexiconSize(string language)
        {
            lock (syncLock)
            {
                return lexicons.TryGetValue(language, out var lexicon) ? lexicon.Count : 0;
            }
        }

        /// <summary>
        /// Check if a word exists in the lexicon
        /// </summary>
        public bool Contains(string word, string language)
        {
            lock (syncLock)
            {
                return lexicons.TryGetValue(language, out var lexicon) &&
                       lexicon.ContainsKey(word.ToLower());
            }
        }

        public void Dispose()
        {
            lock (syncLock)
            {
                lexicons.Clear();
            }
        }
    }
}