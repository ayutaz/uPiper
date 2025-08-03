using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace uPiper.Core.Phonemizers.Backend.Chinese
{
    /// <summary>
    /// Converts Chinese text to Pinyin using dictionary lookups
    /// </summary>
    public class PinyinConverter
    {
        private readonly ChinesePinyinDictionary dictionary;

        public PinyinConverter(ChinesePinyinDictionary dictionary)
        {
            this.dictionary = dictionary ?? throw new ArgumentNullException(nameof(dictionary));
        }

        /// <summary>
        /// Convert Chinese text to pinyin with phrase matching
        /// </summary>
        public string[] GetPinyin(string text, bool usePhrase = true)
        {
            if (string.IsNullOrEmpty(text))
                return Array.Empty<string>();

            var result = new List<string>();

            if (usePhrase)
            {
                // Try phrase matching first
                result = GetPinyinWithPhraseMatching(text);
            }
            else
            {
                // Simple character-by-character conversion
                result = GetPinyinPerCharacter(text);
            }

            return result.ToArray();
        }

        private List<string> GetPinyinWithPhraseMatching(string text)
        {
            var result = new List<string>();
            var i = 0;

            while (i < text.Length)
            {
                // Try to match longest phrase first
                var matched = false;

                // Try phrases of decreasing length (max 4 characters)
                for (int len = Math.Min(4, text.Length - i); len >= 2; len--)
                {
                    if (i + len > text.Length)
                        continue;

                    var phrase = text.Substring(i, len);

                    // Check if all characters are Chinese
                    if (!phrase.All(ch => IsChinese(ch)))
                        continue;

                    if (dictionary.TryGetPhrasePinyin(phrase, out var phrasePinyin))
                    {
                        // Split phrase pinyin by spaces
                        var pinyinArray = phrasePinyin.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        result.AddRange(pinyinArray);
                        i += len;
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                {
                    // No phrase match, try single character
                    var ch = text[i];
                    if (IsChinese(ch))
                    {
                        if (dictionary.TryGetCharacterPinyin(ch, out var pinyinOptions))
                        {
                            // Use first option for now
                            // TODO: Implement context-aware selection
                            result.Add(pinyinOptions[0]);
                        }
                        else
                        {
                            // Unknown character - fallback to the character itself
                            // This prevents IPA converter from receiving invalid pinyin
                            Debug.LogWarning($"[PinyinConverter] No pinyin found for character: {ch} (U+{(int)ch:X4})");
                            result.Add(ch.ToString());
                        }
                    }
                    else
                    {
                        // Non-Chinese character
                        result.Add(ch.ToString());
                    }
                    i++;
                }
            }

            return result;
        }

        private List<string> GetPinyinPerCharacter(string text)
        {
            var result = new List<string>();

            foreach (var ch in text)
            {
                if (IsChinese(ch))
                {
                    if (dictionary.TryGetCharacterPinyin(ch, out var pinyinOptions))
                    {
                        // Use first option
                        result.Add(pinyinOptions[0]);
                    }
                    else
                    {
                        // Unknown character
                        // Unknown character - fallback to the character itself
                        Debug.LogWarning($"[PinyinConverter] No pinyin found for character in phrase mode: {ch} (U+{(int)ch:X4})");
                        result.Add(ch.ToString());
                    }
                }
                else
                {
                    // Non-Chinese character
                    result.Add(ch.ToString());
                }
            }

            return result;
        }

        /// <summary>
        /// Select appropriate pinyin based on context (multi-tone character resolution)
        /// </summary>
        public string[] SelectPinyinByContext(string[] words, string[][] pinyinOptions)
        {
            if (words == null || pinyinOptions == null || words.Length != pinyinOptions.Length)
                throw new ArgumentException("Words and pinyin options must have the same length");

            var result = new string[words.Length];

            for (int i = 0; i < words.Length; i++)
            {
                if (pinyinOptions[i] == null || pinyinOptions[i].Length == 0)
                {
                    result[i] = words[i]; // Fallback to original
                    continue;
                }

                if (pinyinOptions[i].Length == 1)
                {
                    result[i] = pinyinOptions[i][0]; // Only one option
                    continue;
                }

                // Multiple options - apply context rules
                result[i] = SelectBestPinyin(words, i, pinyinOptions[i]);
            }

            return result;
        }

        private string SelectBestPinyin(string[] words, int index, string[] options)
        {
            var word = words[index];

            // Special rules for common multi-tone characters
            switch (word)
            {
                case "不":
                    // "不" is pronounced "bu2" before 4th tone
                    if (index < words.Length - 1)
                    {
                        var nextPinyin = GetFirstPinyinOption(words[index + 1]);
                        if (nextPinyin != null && nextPinyin.EndsWith("4"))
                        {
                            return "bu2";
                        }
                    }
                    return "bu4";

                case "一":
                    // "一" tone changes based on context
                    if (index < words.Length - 1)
                    {
                        var nextPinyin = GetFirstPinyinOption(words[index + 1]);
                        if (nextPinyin != null)
                        {
                            if (nextPinyin.EndsWith("4"))
                                return "yi2"; // 2nd tone before 4th tone
                            else
                                return "yi4"; // 4th tone before other tones
                        }
                    }
                    return "yi1";

                case "了":
                    // Context-based selection for "了"
                    if (index > 0 && index == words.Length - 1)
                    {
                        return "le5"; // Particle at end of sentence
                    }
                    return "liao3"; // Verb

                default:
                    // Default to first option
                    return options[0];
            }
        }

        private string GetFirstPinyinOption(string word)
        {
            if (word.Length == 1 && IsChinese(word[0]))
            {
                if (dictionary.TryGetCharacterPinyin(word[0], out var options))
                {
                    return options[0];
                }
            }
            return null;
        }

        private bool IsChinese(char ch)
        {
            return (ch >= 0x4E00 && ch <= 0x9FFF) ||   // CJK Unified Ideographs
                   (ch >= 0x3400 && ch <= 0x4DBF) ||   // CJK Extension A
                   (ch >= 0xF900 && ch <= 0xFAFF);     // CJK Compatibility Ideographs
        }
    }
}