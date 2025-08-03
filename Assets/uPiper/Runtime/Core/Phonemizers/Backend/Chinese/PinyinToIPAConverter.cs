using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace uPiper.Core.Phonemizers.Backend.Chinese
{
    /// <summary>
    /// Converts Pinyin to IPA (International Phonetic Alphabet)
    /// </summary>
    public class PinyinToIPAConverter
    {
        private readonly ChinesePinyinDictionary dictionary;

        // IPA tone marks for Mandarin Chinese
        private readonly Dictionary<int, string> toneMarks = new()
        {
            [1] = "\u02e5",         // ˥ (55) - high level
            [2] = "\u02e7\u02e5",   // ˧˥ (35) - rising
            [3] = "\u02e8\u02e9\u02e6", // ˨˩˦ (214) - dipping
            [4] = "\u02e5\u02e9",   // ˥˩ (51) - falling
            [5] = ""                // neutral tone (no mark)
        };

        // eSpeak tone numbers for Mandarin Chinese
        private readonly Dictionary<int, string> eSpeakToneNumbers = new()
        {
            [1] = "1",  // high level
            [2] = "2",  // rising
            [3] = "3",  // dipping
            [4] = "4",  // falling
            [5] = "5"   // neutral
        };

        public PinyinToIPAConverter(ChinesePinyinDictionary dictionary)
        {
            this.dictionary = dictionary ?? throw new ArgumentNullException(nameof(dictionary));
        }

        /// <summary>
        /// Convert pinyin with tone number to IPA phonemes
        /// </summary>
        public string[] ConvertToIPA(string pinyinWithTone, bool useESpeakFormat = false)
        {
            if (string.IsNullOrEmpty(pinyinWithTone))
                return Array.Empty<string>();

            // Extract tone and base pinyin
            var (pinyin, tone) = ExtractTone(pinyinWithTone);

            // Get IPA mapping
            if (!dictionary.TryGetIPA(pinyin, out var ipaBase))
            {
                Debug.LogWarning($"No IPA mapping for pinyin: {pinyin}");
                return new[] { pinyin }; // Fallback
            }

            // Apply tone and split into phonemes
            return ApplyToneAndSplit(ipaBase, tone, useESpeakFormat);
        }

        /// <summary>
        /// Convert multiple pinyin syllables to IPA
        /// </summary>
        public string[] ConvertMultipleToIPA(string[] pinyinArray, bool useESpeakFormat = false)
        {
            var result = new List<string>();

            foreach (var pinyin in pinyinArray)
            {
                var ipaPhonemes = ConvertToIPA(pinyin, useESpeakFormat);
                result.AddRange(ipaPhonemes);
            }

            return result.ToArray();
        }

        private (string pinyin, int tone) ExtractTone(string pinyinWithTone)
        {
            if (string.IsNullOrEmpty(pinyinWithTone))
                return ("", 0);

            var lastChar = pinyinWithTone[pinyinWithTone.Length - 1];

            if (char.IsDigit(lastChar))
            {
                var tone = lastChar - '0';
                var pinyin = pinyinWithTone.Substring(0, pinyinWithTone.Length - 1);
                return (pinyin, tone);
            }

            // No tone number
            return (pinyinWithTone, 0);
        }

        private string[] ApplyToneAndSplit(string ipa, int tone, bool useESpeakFormat = false)
        {
            var phonemes = new List<string>();

            // Split IPA into individual phonemes
            var ipaPhonemes = SplitIPA(ipa, useESpeakFormat);
            phonemes.AddRange(ipaPhonemes);

            // Add tone mark if applicable
            if (tone >= 1 && tone <= 5)
            {
                if (useESpeakFormat && eSpeakToneNumbers.TryGetValue(tone, out var toneNumber))
                {
                    // For eSpeak format, add tone number
                    phonemes.Add(toneNumber);
                }
                else if (!useESpeakFormat && toneMarks.TryGetValue(tone, out var toneMark))
                {
                    // For IPA format, add tone mark
                    if (!string.IsNullOrEmpty(toneMark))
                    {
                        phonemes.Add(toneMark);
                    }
                }
            }

            return phonemes.ToArray();
        }

        private string[] SplitIPA(string ipa, bool useESpeakFormat = false)
        {
            // Advanced IPA splitting logic
            var phonemes = new List<string>();
            var current = new System.Text.StringBuilder();

            for (int i = 0; i < ipa.Length; i++)
            {
                var ch = ipa[i];

                // Check for multi-character phonemes
                if (i < ipa.Length - 1)
                {
                    var twoChar = ipa.Substring(i, 2);

                    // For eSpeak format, split diphthongs into individual vowels
                    if (useESpeakFormat && IsVowelDiphthong(twoChar))
                    {
                        if (current.Length > 0)
                        {
                            phonemes.Add(current.ToString());
                            current.Clear();
                        }
                        // Split diphthong into individual vowels for eSpeak
                        phonemes.Add(twoChar[0].ToString());
                        phonemes.Add(twoChar[1].ToString());
                        i++; // Skip next character
                        continue;
                    }

                    // Common two-character IPA symbols in Chinese
                    if (!useESpeakFormat && IsDigraph(twoChar))
                    {
                        if (current.Length > 0)
                        {
                            phonemes.Add(current.ToString());
                            current.Clear();
                        }
                        phonemes.Add(twoChar);
                        i++; // Skip next character
                        continue;
                    }
                }

                // Check for phoneme boundaries
                if (IsPhonemeBreak(ch, current.ToString()))
                {
                    if (current.Length > 0)
                    {
                        phonemes.Add(current.ToString());
                        current.Clear();
                    }
                }

                current.Append(ch);
            }

            // Add remaining
            if (current.Length > 0)
            {
                phonemes.Add(current.ToString());
            }

            return phonemes.ToArray();
        }

        private bool IsDigraph(string twoChar)
        {
            // Common digraphs in Chinese IPA
            var digraphs = new HashSet<string>
            {
                "tɕ", "tɕʰ", "ʈʂ", "ʈʂʰ", "ts", "tsʰ",
                "ai", "ei", "ao", "ou", "au", "eu", "iu",
                "an", "en", "in", "un", "yn",
                "aŋ", "əŋ", "iŋ", "uŋ", "yŋ"
            };

            return digraphs.Contains(twoChar);
        }

        private bool IsPhonemeBreak(char ch, string current)
        {
            // Determine if we should break at this character
            if (string.IsNullOrEmpty(current))
                return false;

            // Consonant-vowel boundary
            if (IsConsonant(current[current.Length - 1]) && IsVowel(ch))
                return true;

            // Vowel-consonant boundary (except for finals like 'n', 'ŋ')
            if (IsVowel(current[current.Length - 1]) && IsConsonant(ch) && ch != 'n' && ch != 'ŋ')
                return true;

            return false;
        }

        private bool IsConsonant(char ch)
        {
            var consonants = "bpmfdtnlgkhjqxʈʂʐɕtɕzsʰ";
            return consonants.Contains(ch);
        }

        private bool IsVowel(char ch)
        {
            var vowels = "aeiouəɚɤʅɿyæɛɑɔ";
            return vowels.Contains(ch);
        }

        private bool IsVowelDiphthong(string twoChar)
        {
            // Common diphthongs that need to be split for eSpeak
            var diphthongs = new HashSet<string>
            {
                "ai", "ei", "ao", "ou", "au", "eu", "iu"
            };
            return diphthongs.Contains(twoChar);
        }

        /// <summary>
        /// Get display-friendly IPA with tone marks
        /// </summary>
        public string GetDisplayIPA(string pinyinWithTone)
        {
            var phonemes = ConvertToIPA(pinyinWithTone);
            return string.Join("", phonemes);
        }
    }
}