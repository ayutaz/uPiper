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
        private readonly object syncLock = new object();

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
            var usEnglish = new Dictionary<string, List<string>>();
            
            // Common words
            usEnglish["the"] = new List<string> { "dh", "ax" };
            usEnglish["a"] = new List<string> { "ax" };
            usEnglish["an"] = new List<string> { "ax", "n" };
            usEnglish["and"] = new List<string> { "ae", "n", "d" };
            usEnglish["of"] = new List<string> { "ax", "v" };
            usEnglish["to"] = new List<string> { "t", "uw" };
            usEnglish["in"] = new List<string> { "ih", "n" };
            usEnglish["is"] = new List<string> { "ih", "z" };
            usEnglish["it"] = new List<string> { "ih", "t" };
            usEnglish["that"] = new List<string> { "dh", "ae", "t" };
            usEnglish["for"] = new List<string> { "f", "ao", "r" };
            usEnglish["was"] = new List<string> { "w", "aa", "z" };
            usEnglish["with"] = new List<string> { "w", "ih", "dh" };
            usEnglish["be"] = new List<string> { "b", "iy" };
            usEnglish["by"] = new List<string> { "b", "ay" };
            usEnglish["have"] = new List<string> { "hh", "ae", "v" };
            usEnglish["from"] = new List<string> { "f", "r", "ah", "m" };
            usEnglish["or"] = new List<string> { "ao", "r" };
            usEnglish["as"] = new List<string> { "ae", "z" };
            usEnglish["what"] = new List<string> { "w", "ah", "t" };
            usEnglish["all"] = new List<string> { "ao", "l" };
            usEnglish["would"] = new List<string> { "w", "uh", "d" };
            usEnglish["there"] = new List<string> { "dh", "eh", "r" };
            usEnglish["their"] = new List<string> { "dh", "eh", "r" };
            
            // Numbers
            usEnglish["one"] = new List<string> { "w", "ah", "n" };
            usEnglish["two"] = new List<string> { "t", "uw" };
            usEnglish["three"] = new List<string> { "th", "r", "iy" };
            usEnglish["four"] = new List<string> { "f", "ao", "r" };
            usEnglish["five"] = new List<string> { "f", "ay", "v" };
            usEnglish["six"] = new List<string> { "s", "ih", "k", "s" };
            usEnglish["seven"] = new List<string> { "s", "eh", "v", "ax", "n" };
            usEnglish["eight"] = new List<string> { "ey", "t" };
            usEnglish["nine"] = new List<string> { "n", "ay", "n" };
            usEnglish["ten"] = new List<string> { "t", "eh", "n" };
            
            // Common verbs
            usEnglish["go"] = new List<string> { "g", "ow" };
            usEnglish["get"] = new List<string> { "g", "eh", "t" };
            usEnglish["make"] = new List<string> { "m", "ey", "k" };
            usEnglish["know"] = new List<string> { "n", "ow" };
            usEnglish["think"] = new List<string> { "th", "ih", "ng", "k" };
            usEnglish["take"] = new List<string> { "t", "ey", "k" };
            usEnglish["see"] = new List<string> { "s", "iy" };
            usEnglish["come"] = new List<string> { "k", "ah", "m" };
            usEnglish["want"] = new List<string> { "w", "aa", "n", "t" };
            usEnglish["use"] = new List<string> { "y", "uw", "z" };
            
            // Common nouns
            usEnglish["time"] = new List<string> { "t", "ay", "m" };
            usEnglish["person"] = new List<string> { "p", "er", "s", "ax", "n" };
            usEnglish["year"] = new List<string> { "y", "ih", "r" };
            usEnglish["way"] = new List<string> { "w", "ey" };
            usEnglish["day"] = new List<string> { "d", "ey" };
            usEnglish["man"] = new List<string> { "m", "ae", "n" };
            usEnglish["thing"] = new List<string> { "th", "ih", "ng" };
            usEnglish["woman"] = new List<string> { "w", "uh", "m", "ax", "n" };
            usEnglish["life"] = new List<string> { "l", "ay", "f" };
            usEnglish["child"] = new List<string> { "ch", "ay", "l", "d" };
            usEnglish["world"] = new List<string> { "w", "er", "l", "d" };
            
            // Technology terms
            usEnglish["computer"] = new List<string> { "k", "ax", "m", "p", "y", "uw", "t", "er" };
            usEnglish["software"] = new List<string> { "s", "ao", "f", "t", "w", "eh", "r" };
            usEnglish["hardware"] = new List<string> { "hh", "aa", "r", "d", "w", "eh", "r" };
            usEnglish["internet"] = new List<string> { "ih", "n", "t", "er", "n", "eh", "t" };
            usEnglish["email"] = new List<string> { "iy", "m", "ey", "l" };
            usEnglish["website"] = new List<string> { "w", "eh", "b", "s", "ay", "t" };
            usEnglish["data"] = new List<string> { "d", "ey", "t", "ax" };
            usEnglish["file"] = new List<string> { "f", "ay", "l" };
            usEnglish["system"] = new List<string> { "s", "ih", "s", "t", "ax", "m" };
            
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