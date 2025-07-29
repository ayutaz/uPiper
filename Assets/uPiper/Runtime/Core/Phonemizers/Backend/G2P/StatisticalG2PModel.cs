using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace uPiper.Core.Phonemizers.Backend.G2P
{
    /// <summary>
    /// Statistical Grapheme-to-Phoneme model trained on CMU dictionary.
    /// Uses n-gram probabilities to predict phonemes for unknown words.
    /// </summary>
    public class StatisticalG2PModel
    {
        // N-gram models for different context sizes
        private Dictionary<string, Dictionary<string, float>> trigramModel;
        private Dictionary<string, Dictionary<string, float>> bigramModel;
        private Dictionary<string, Dictionary<string, float>> unigramModel;

        // Letter-to-phoneme alignment data from training
        private Dictionary<string, List<AlignmentExample>> alignmentExamples;

        // Phoneme transition probabilities
        private Dictionary<string, Dictionary<string, float>> phonemeTransitions;

        private bool isInitialized;
        private readonly object lockObject = new();

        public bool IsInitialized => isInitialized;

        /// <summary>
        /// Trains the model from CMU dictionary data.
        /// </summary>
        public async Task TrainFromDictionary(Dictionary<string, string[]> dictionary, CancellationToken cancellationToken = default)
        {
            await Task.Run(() =>
            {
                trigramModel = new Dictionary<string, Dictionary<string, float>>();
                bigramModel = new Dictionary<string, Dictionary<string, float>>();
                unigramModel = new Dictionary<string, Dictionary<string, float>>();
                alignmentExamples = new Dictionary<string, List<AlignmentExample>>();
                phonemeTransitions = new Dictionary<string, Dictionary<string, float>>();

                var alignments = new List<WordAlignment>();

                // Step 1: Create letter-phoneme alignments
                foreach (var entry in dictionary)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var word = entry.Key.ToLower();
                    var phonemes = entry.Value;

                    var alignment = AlignLettersToPhonemes(word, phonemes);
                    if (alignment != null)
                    {
                        alignments.Add(alignment);

                        // Store alignment examples
                        for (var i = 0; i < alignment.Letters.Length; i++)
                        {
                            var letter = alignment.Letters[i];
                            var phoneme = alignment.Phonemes[i];
                            var context = GetAlignmentContext(alignment, i);

                            if (!alignmentExamples.ContainsKey(letter))
                                alignmentExamples[letter] = new List<AlignmentExample>();

                            alignmentExamples[letter].Add(new AlignmentExample
                            {
                                Letter = letter,
                                Phoneme = phoneme,
                                Context = context,
                                Word = word
                            });
                        }
                    }
                }

                // Step 2: Build n-gram models
                BuildNGramModels(alignments, cancellationToken);

                // Step 3: Build phoneme transition model
                BuildPhonemeTransitions(alignments, cancellationToken);

                lock (lockObject)
                {
                    isInitialized = true;
                }

                Debug.Log($"G2P model trained with {alignments.Count} words");
            }, cancellationToken);
        }

        /// <summary>
        /// Predicts phonemes for an unknown word using Viterbi algorithm.
        /// </summary>
        public string[] PredictPhonemes(string word)
        {
            if (!isInitialized)
                throw new InvalidOperationException("Model not initialized");

            word = word.ToLower();

            // Use Viterbi algorithm to find most likely phoneme sequence
            var result = ViterbiDecode(word);

            // Post-process to clean up results
            return PostProcessPhonemes(result, word);
        }

        private WordAlignment AlignLettersToPhonemes(string word, string[] phonemes)
        {
            // Simple alignment heuristic - can be improved with EM algorithm
            var alignment = new WordAlignment
            {
                Word = word,
                OriginalPhonemes = phonemes
            };

            // Special cases for common patterns
            var letterGroups = new List<string>();
            var alignedPhonemes = new List<string>();

            var i = 0;
            var p = 0;

            while (i < word.Length && p < phonemes.Length)
            {
                // Check for digraphs/trigraphs
                string letterGroup = null;
                string matchedPhoneme = null;

                // Try trigraphs
                if (i + 2 < word.Length)
                {
                    var trigraph = word.Substring(i, 3);
                    if (IsKnownTrigraph(trigraph))
                    {
                        letterGroup = trigraph;
                        matchedPhoneme = MapTrigraphToPhoneme(trigraph, phonemes[p]);
                        i += 3;
                    }
                }

                // Try digraphs
                if (letterGroup == null && i + 1 < word.Length)
                {
                    var digraph = word.Substring(i, 2);
                    if (IsKnownDigraph(digraph))
                    {
                        letterGroup = digraph;
                        matchedPhoneme = MapDigraphToPhoneme(digraph, phonemes[p]);
                        i += 2;
                    }
                }

                // Single letter
                if (letterGroup == null)
                {
                    letterGroup = word[i].ToString();
                    matchedPhoneme = phonemes[p];
                    i++;
                }

                letterGroups.Add(letterGroup);
                alignedPhonemes.Add(matchedPhoneme);
                p++;

                // Handle cases where one letter maps to multiple phonemes
                if (p < phonemes.Length && ShouldConsumeNextPhoneme(letterGroup, matchedPhoneme, phonemes[p]))
                {
                    alignedPhonemes[^1] += "+" + phonemes[p];
                    p++;
                }
            }

            // Handle remaining letters (usually silent)
            while (i < word.Length)
            {
                letterGroups.Add(word[i].ToString());
                alignedPhonemes.Add("_"); // Silent
                i++;
            }

            alignment.Letters = letterGroups.ToArray();
            alignment.Phonemes = alignedPhonemes.ToArray();

            return alignment;
        }

        private void BuildNGramModels(List<WordAlignment> alignments, CancellationToken cancellationToken)
        {
            // Count occurrences
            var trigramCounts = new Dictionary<string, Dictionary<string, int>>();
            var bigramCounts = new Dictionary<string, Dictionary<string, int>>();
            var unigramCounts = new Dictionary<string, int>();

            foreach (var alignment in alignments)
            {
                cancellationToken.ThrowIfCancellationRequested();

                for (var i = 0; i < alignment.Letters.Length; i++)
                {
                    var letter = alignment.Letters[i];
                    var phoneme = alignment.Phonemes[i];

                    // Unigram
                    if (!unigramCounts.ContainsKey(letter))
                        unigramCounts[letter] = 0;
                    unigramCounts[letter]++;

                    // Bigram context
                    if (i > 0)
                    {
                        var prevLetter = alignment.Letters[i - 1];
                        var bigramKey = $"{prevLetter}|{letter}";

                        if (!bigramCounts.ContainsKey(bigramKey))
                            bigramCounts[bigramKey] = new Dictionary<string, int>();

                        if (!bigramCounts[bigramKey].ContainsKey(phoneme))
                            bigramCounts[bigramKey][phoneme] = 0;
                        bigramCounts[bigramKey][phoneme]++;
                    }

                    // Trigram context
                    if (i > 1)
                    {
                        var prevLetter1 = alignment.Letters[i - 2];
                        var prevLetter2 = alignment.Letters[i - 1];
                        var trigramKey = $"{prevLetter1}|{prevLetter2}|{letter}";

                        if (!trigramCounts.ContainsKey(trigramKey))
                            trigramCounts[trigramKey] = new Dictionary<string, int>();

                        if (!trigramCounts[trigramKey].ContainsKey(phoneme))
                            trigramCounts[trigramKey][phoneme] = 0;
                        trigramCounts[trigramKey][phoneme]++;
                    }
                }
            }

            // Convert counts to probabilities
            foreach (var trigram in trigramCounts)
            {
                var total = trigram.Value.Values.Sum();
                trigramModel[trigram.Key] = new Dictionary<string, float>();

                foreach (var phoneme in trigram.Value)
                {
                    trigramModel[trigram.Key][phoneme.Key] = (float)phoneme.Value / total;
                }
            }

            foreach (var bigram in bigramCounts)
            {
                var total = bigram.Value.Values.Sum();
                bigramModel[bigram.Key] = new Dictionary<string, float>();

                foreach (var phoneme in bigram.Value)
                {
                    bigramModel[bigram.Key][phoneme.Key] = (float)phoneme.Value / total;
                }
            }

            // Build unigram model from alignment examples
            foreach (var letterExamples in alignmentExamples)
            {
                var phoneemCounts = letterExamples.Value
                    .GroupBy(e => e.Phoneme)
                    .ToDictionary(g => g.Key, g => g.Count());

                var total = phoneemCounts.Values.Sum();
                unigramModel[letterExamples.Key] = new Dictionary<string, float>();

                foreach (var phoneme in phoneemCounts)
                {
                    unigramModel[letterExamples.Key][phoneme.Key] = (float)phoneme.Value / total;
                }
            }
        }

        private void BuildPhonemeTransitions(List<WordAlignment> alignments, CancellationToken cancellationToken)
        {
            var transitionCounts = new Dictionary<string, Dictionary<string, int>>();

            foreach (var alignment in alignments)
            {
                cancellationToken.ThrowIfCancellationRequested();

                for (var i = 1; i < alignment.Phonemes.Length; i++)
                {
                    var prev = alignment.Phonemes[i - 1];
                    var curr = alignment.Phonemes[i];

                    if (!transitionCounts.ContainsKey(prev))
                        transitionCounts[prev] = new Dictionary<string, int>();

                    if (!transitionCounts[prev].ContainsKey(curr))
                        transitionCounts[prev][curr] = 0;

                    transitionCounts[prev][curr]++;
                }
            }

            // Convert to probabilities
            foreach (var transition in transitionCounts)
            {
                var total = transition.Value.Values.Sum();
                phonemeTransitions[transition.Key] = new Dictionary<string, float>();

                foreach (var next in transition.Value)
                {
                    phonemeTransitions[transition.Key][next.Key] = (float)next.Value / total;
                }
            }
        }

        private string[] ViterbiDecode(string word)
        {
            // Preprocess word into letter groups
            var letterGroups = PreprocessWord(word);

            if (letterGroups.Count == 0)
                return new string[0];

            // Initialize Viterbi tables
            var viterbi = new Dictionary<int, Dictionary<string, ViterbiCell>>();
            var states = GetPossiblePhonemes();

            // Initialize first position
            viterbi[0] = new Dictionary<string, ViterbiCell>();
            var firstLetter = letterGroups[0];

            foreach (var phoneme in GetPhonemesForLetter(firstLetter))
            {
                var prob = GetUnigramProbability(firstLetter, phoneme);
                viterbi[0][phoneme] = new ViterbiCell
                {
                    Probability = prob,
                    BackPointer = null,
                    Phoneme = phoneme
                };
            }

            // Forward pass
            for (var t = 1; t < letterGroups.Count; t++)
            {
                viterbi[t] = new Dictionary<string, ViterbiCell>();
                var currentLetter = letterGroups[t];

                foreach (var phoneme in GetPhonemesForLetter(currentLetter))
                {
                    var bestProb = 0.0f;
                    string bestPrev = null;

                    // Find best previous state
                    foreach (var prevState in viterbi[t - 1])
                    {
                        var prevPhoneme = prevState.Key;
                        var prevProb = prevState.Value.Probability;

                        // Calculate probability
                        var emissionProb = GetEmissionProbability(letterGroups, t, phoneme);
                        var transitionProb = GetTransitionProbability(prevPhoneme, phoneme);

                        var prob = prevProb * emissionProb * transitionProb;

                        if (prob > bestProb)
                        {
                            bestProb = prob;
                            bestPrev = prevPhoneme;
                        }
                    }

                    if (bestPrev != null)
                    {
                        viterbi[t][phoneme] = new ViterbiCell
                        {
                            Probability = bestProb,
                            BackPointer = bestPrev,
                            Phoneme = phoneme
                        };
                    }
                }
            }

            // Backward pass - find best path
            var result = new List<string>();

            // Find best final state
            var lastPosition = letterGroups.Count - 1;
            var bestFinalProb = 0.0f;
            string bestFinalState = null;

            foreach (var state in viterbi[lastPosition])
            {
                if (state.Value.Probability > bestFinalProb)
                {
                    bestFinalProb = state.Value.Probability;
                    bestFinalState = state.Key;
                }
            }

            // Trace back
            if (bestFinalState != null)
            {
                var currentState = bestFinalState;

                for (var t = lastPosition; t >= 0; t--)
                {
                    result.Add(currentState);

                    if (t > 0 && viterbi[t].ContainsKey(currentState))
                    {
                        currentState = viterbi[t][currentState].BackPointer;
                    }
                }

                result.Reverse();
            }

            return result.ToArray();
        }

        private List<string> PreprocessWord(string word)
        {
            var groups = new List<string>();
            var i = 0;

            while (i < word.Length)
            {
                // Check for known letter groups
                string group = null;

                // Try 4-letter groups (e.g., "ough")
                if (i + 3 < word.Length)
                {
                    var quad = word.Substring(i, 4);
                    if (IsKnownQuadgraph(quad))
                    {
                        group = quad;
                        i += 4;
                    }
                }

                // Try trigraphs
                if (group == null && i + 2 < word.Length)
                {
                    var tri = word.Substring(i, 3);
                    if (IsKnownTrigraph(tri))
                    {
                        group = tri;
                        i += 3;
                    }
                }

                // Try digraphs
                if (group == null && i + 1 < word.Length)
                {
                    var di = word.Substring(i, 2);
                    if (IsKnownDigraph(di))
                    {
                        group = di;
                        i += 2;
                    }
                }

                // Single letter
                if (group == null)
                {
                    group = word[i].ToString();
                    i++;
                }

                groups.Add(group);
            }

            return groups;
        }

        private float GetEmissionProbability(List<string> letterGroups, int position, string phoneme)
        {
            var letter = letterGroups[position];

            // Try trigram first
            if (position >= 2)
            {
                var trigramKey = $"{letterGroups[position - 2]}|{letterGroups[position - 1]}|{letter}";
                if (trigramModel.ContainsKey(trigramKey) &&
                    trigramModel[trigramKey].ContainsKey(phoneme))
                {
                    return trigramModel[trigramKey][phoneme];
                }
            }

            // Try bigram
            if (position >= 1)
            {
                var bigramKey = $"{letterGroups[position - 1]}|{letter}";
                if (bigramModel.ContainsKey(bigramKey) &&
                    bigramModel[bigramKey].ContainsKey(phoneme))
                {
                    return bigramModel[bigramKey][phoneme] * 0.8f; // Slight penalty for less context
                }
            }

            // Fall back to unigram
            return GetUnigramProbability(letter, phoneme) * 0.6f; // More penalty
        }

        private float GetUnigramProbability(string letter, string phoneme)
        {
            if (unigramModel.ContainsKey(letter) &&
                unigramModel[letter].ContainsKey(phoneme))
            {
                return unigramModel[letter][phoneme];
            }

            // Smoothing for unseen combinations
            return 0.001f;
        }

        private float GetTransitionProbability(string prevPhoneme, string currPhoneme)
        {
            if (phonemeTransitions.ContainsKey(prevPhoneme) &&
                phonemeTransitions[prevPhoneme].ContainsKey(currPhoneme))
            {
                return phonemeTransitions[prevPhoneme][currPhoneme];
            }

            // Smoothing
            return 0.01f;
        }

        private HashSet<string> GetPossiblePhonemes()
        {
            var phonemes = new HashSet<string>();

            foreach (var transitions in phonemeTransitions.Values)
            {
                foreach (var phoneme in transitions.Keys)
                {
                    phonemes.Add(phoneme);
                }
            }

            return phonemes;
        }

        private List<string> GetPhonemesForLetter(string letter)
        {
            if (unigramModel.ContainsKey(letter))
            {
                return unigramModel[letter].Keys.ToList();
            }

            // Default phonemes for unknown letters
            return GetDefaultPhonemesForLetter(letter);
        }

        private List<string> GetDefaultPhonemesForLetter(string letter)
        {
            // Basic letter-to-phoneme mappings
            var defaults = new Dictionary<string, List<string>>
            {
                ["a"] = new List<string> { "AE1", "EY1", "AH0" },
                ["e"] = new List<string> { "EH1", "IY1", "AH0" },
                ["i"] = new List<string> { "IH1", "AY1" },
                ["o"] = new List<string> { "AA1", "OW1", "AH0" },
                ["u"] = new List<string> { "AH1", "UW1" },
                ["b"] = new List<string> { "B" },
                ["c"] = new List<string> { "K", "S" },
                ["d"] = new List<string> { "D" },
                ["f"] = new List<string> { "F" },
                ["g"] = new List<string> { "G", "JH" },
                ["h"] = new List<string> { "HH" },
                ["j"] = new List<string> { "JH" },
                ["k"] = new List<string> { "K" },
                ["l"] = new List<string> { "L" },
                ["m"] = new List<string> { "M" },
                ["n"] = new List<string> { "N" },
                ["p"] = new List<string> { "P" },
                ["q"] = new List<string> { "K" },
                ["r"] = new List<string> { "R" },
                ["s"] = new List<string> { "S", "Z" },
                ["t"] = new List<string> { "T" },
                ["v"] = new List<string> { "V" },
                ["w"] = new List<string> { "W" },
                ["x"] = new List<string> { "K S" },
                ["y"] = new List<string> { "Y", "IY0" },
                ["z"] = new List<string> { "Z" }
            };

            if (defaults.ContainsKey(letter.ToLower()))
                return defaults[letter.ToLower()];

            return new List<string> { "_" }; // Silent
        }

        private bool IsKnownDigraph(string letters)
        {
            var digraphs = new HashSet<string>
            {
                "ch", "sh", "th", "ph", "wh", "gh", "ng", "ck",
                "qu", "gu", "kn", "wr", "gn", "ps", "pn", "mb"
            };
            return digraphs.Contains(letters.ToLower());
        }

        private bool IsKnownTrigraph(string letters)
        {
            var trigraphs = new HashSet<string>
            {
                "tch", "dge", "igh", "ght", "shr", "thr", "squ", "str"
            };
            return trigraphs.Contains(letters.ToLower());
        }

        private bool IsKnownQuadgraph(string letters)
        {
            var quadgraphs = new HashSet<string>
            {
                "ough", "augh", "eigh"
            };
            return quadgraphs.Contains(letters.ToLower());
        }

        private string MapDigraphToPhoneme(string digraph, string defaultPhoneme)
        {
            var mappings = new Dictionary<string, string>
            {
                ["ch"] = "CH",
                ["sh"] = "SH",
                ["th"] = "TH", // or DH
                ["ph"] = "F",
                ["ng"] = "NG",
                ["ck"] = "K"
            };

            return mappings.ContainsKey(digraph.ToLower())
                ? mappings[digraph.ToLower()]
                : defaultPhoneme;
        }

        private string MapTrigraphToPhoneme(string trigraph, string defaultPhoneme)
        {
            var mappings = new Dictionary<string, string>
            {
                ["tch"] = "CH",
                ["dge"] = "JH",
                ["igh"] = "AY1"
            };

            return mappings.ContainsKey(trigraph.ToLower())
                ? mappings[trigraph.ToLower()]
                : defaultPhoneme;
        }

        private bool ShouldConsumeNextPhoneme(string letter, string currentPhoneme, string nextPhoneme)
        {
            // Some letters commonly map to multiple phonemes
            if (letter == "x" && currentPhoneme == "K" && nextPhoneme == "S")
                return true;

            return false;
        }

        private AlignmentContext GetAlignmentContext(WordAlignment alignment, int position)
        {
            return new AlignmentContext
            {
                PrevLetter = position > 0 ? alignment.Letters[position - 1] : "",
                NextLetter = position < alignment.Letters.Length - 1 ? alignment.Letters[position + 1] : "",
                Position = position,
                WordLength = alignment.Letters.Length,
                IsInitial = position == 0,
                IsFinal = position == alignment.Letters.Length - 1
            };
        }

        private string[] PostProcessPhonemes(string[] phonemes, string word)
        {
            var result = new List<string>();

            foreach (var phoneme in phonemes)
            {
                // Skip silent markers
                if (phoneme == "_")
                    continue;

                // Split compound phonemes
                if (phoneme.Contains("+"))
                {
                    result.AddRange(phoneme.Split('+'));
                }
                else if (phoneme.Contains(" "))
                {
                    result.AddRange(phoneme.Split(' '));
                }
                else
                {
                    result.Add(phoneme);
                }
            }

            return result.ToArray();
        }

        // Helper classes
        private class WordAlignment
        {
            public string Word { get; set; }
            public string[] Letters { get; set; }
            public string[] Phonemes { get; set; }
            public string[] OriginalPhonemes { get; set; }
        }

        private class AlignmentExample
        {
            public string Letter { get; set; }
            public string Phoneme { get; set; }
            public AlignmentContext Context { get; set; }
            public string Word { get; set; }
        }

        private class AlignmentContext
        {
            public string PrevLetter { get; set; }
            public string NextLetter { get; set; }
            public int Position { get; set; }
            public int WordLength { get; set; }
            public bool IsInitial { get; set; }
            public bool IsFinal { get; set; }
        }

        private class ViterbiCell
        {
            public float Probability { get; set; }
            public string BackPointer { get; set; }
            public string Phoneme { get; set; }
        }
    }
}