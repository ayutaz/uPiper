using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace uPiper.Core.Phonemizers.Backend.Chinese
{
    /// <summary>
    /// Chinese word segmenter using maximum forward matching and dynamic programming
    /// Inspired by jieba segmentation algorithm
    /// </summary>
    public class ChineseWordSegmenter
    {
        private readonly ChinesePinyinDictionary dictionary;
        private readonly HashSet<string> wordSet;
        private readonly int maxWordLength;
        
        // Default word frequencies for unknown words
        private const float DEFAULT_WORD_FREQ = 1.0f;
        private const float SINGLE_CHAR_PENALTY = 0.5f; // Penalty for single character words
        
        public ChineseWordSegmenter(ChinesePinyinDictionary dictionary)
        {
            this.dictionary = dictionary ?? throw new ArgumentNullException(nameof(dictionary));
            
            // Build word set from phrase dictionary
            wordSet = new HashSet<string>();
            maxWordLength = 0;
            
            // Add all phrases to word set
            foreach (var phrase in dictionary.GetAllPhrases())
            {
                wordSet.Add(phrase);
                maxWordLength = Math.Max(maxWordLength, phrase.Length);
            }
            
            Debug.Log($"[ChineseWordSegmenter] Initialized with {wordSet.Count} words, max length: {maxWordLength}");
        }
        
        /// <summary>
        /// Segment Chinese text into words using dynamic programming
        /// </summary>
        public List<string> Segment(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new List<string>();
            
            // Dynamic programming approach
            var n = text.Length;
            var dp = new float[n + 1]; // dp[i] = best score for text[0..i)
            var prev = new int[n + 1]; // prev[i] = best split position for text[0..i)
            
            // Initialize
            dp[0] = 0;
            prev[0] = -1;
            
            // Fill DP table
            for (int i = 0; i < n; i++)
            {
                if (float.IsNegativeInfinity(dp[i]))
                    continue;
                
                // Try all possible word lengths
                for (int len = 1; len <= Math.Min(maxWordLength, n - i); len++)
                {
                    var word = text.Substring(i, len);
                    var score = GetWordScore(word);
                    var newScore = dp[i] + score;
                    
                    if (newScore > dp[i + len])
                    {
                        dp[i + len] = newScore;
                        prev[i + len] = i;
                    }
                }
            }
            
            // Backtrack to find the best segmentation
            var result = new List<string>();
            var pos = n;
            
            while (pos > 0)
            {
                var start = prev[pos];
                if (start < 0)
                {
                    // Fallback: single character
                    result.Add(text[pos - 1].ToString());
                    pos--;
                }
                else
                {
                    result.Add(text.Substring(start, pos - start));
                    pos = start;
                }
            }
            
            result.Reverse();
            return result;
        }
        
        /// <summary>
        /// Segment using maximum forward matching (simpler but less accurate)
        /// </summary>
        public List<string> SegmentMaxMatch(string text)
        {
            var result = new List<string>();
            var i = 0;
            
            while (i < text.Length)
            {
                var matched = false;
                
                // Try longest match first
                for (int len = Math.Min(maxWordLength, text.Length - i); len > 0; len--)
                {
                    var word = text.Substring(i, len);
                    
                    if (IsKnownWord(word))
                    {
                        result.Add(word);
                        i += len;
                        matched = true;
                        break;
                    }
                }
                
                // No match found, add single character
                if (!matched)
                {
                    result.Add(text[i].ToString());
                    i++;
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Get score for a word (higher is better)
        /// </summary>
        private float GetWordScore(string word)
        {
            // Known phrase
            if (wordSet.Contains(word))
            {
                // Get frequency if available
                if (dictionary.TryGetWordFrequency(word, out var freq))
                {
                    return (float)Math.Log(freq + 1);
                }
                return (float)Math.Log(DEFAULT_WORD_FREQ + 1);
            }
            
            // Single character
            if (word.Length == 1)
            {
                // Check if it's a known character
                if (dictionary.TryGetCharacterPinyin(word[0], out _))
                {
                    return (float)Math.Log(DEFAULT_WORD_FREQ * SINGLE_CHAR_PENALTY + 1);
                }
            }
            
            // Unknown word - heavily penalized
            return -word.Length * 0.5f;
        }
        
        /// <summary>
        /// Check if a word is known (either as phrase or valid character sequence)
        /// </summary>
        private bool IsKnownWord(string word)
        {
            // Check phrase dictionary
            if (wordSet.Contains(word))
                return true;
            
            // For single characters, check if it's in character dictionary
            if (word.Length == 1)
            {
                return dictionary.TryGetCharacterPinyin(word[0], out _);
            }
            
            // For multi-character words not in phrase dictionary,
            // check if all characters are known
            foreach (char ch in word)
            {
                if (!dictionary.TryGetCharacterPinyin(ch, out _))
                    return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Segment and get pinyin for each word
        /// </summary>
        public List<(string word, string[] pinyin)> SegmentWithPinyin(string text)
        {
            var segments = Segment(text);
            var result = new List<(string word, string[] pinyin)>();
            
            foreach (var word in segments)
            {
                string[] pinyin;
                
                // Try phrase first
                if (dictionary.TryGetPhrasePinyin(word, out var phrasePinyin))
                {
                    pinyin = phrasePinyin.Split(' ');
                }
                else
                {
                    // Get pinyin for each character
                    var pinyinList = new List<string>();
                    foreach (char ch in word)
                    {
                        if (dictionary.TryGetCharacterPinyin(ch, out var charPinyin))
                        {
                            // Use first pronunciation for now
                            // TODO: Use context to select best pronunciation
                            pinyinList.Add(charPinyin[0]);
                        }
                        else
                        {
                            pinyinList.Add($"u{(int)ch:x}"); // Unicode fallback
                        }
                    }
                    pinyin = pinyinList.ToArray();
                }
                
                result.Add((word, pinyin));
            }
            
            return result;
        }
    }
}