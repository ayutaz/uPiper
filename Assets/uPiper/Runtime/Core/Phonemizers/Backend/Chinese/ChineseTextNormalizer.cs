using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace uPiper.Core.Phonemizers.Backend.Chinese
{
    /// <summary>
    /// Normalizes Chinese text for phonemization
    /// </summary>
    public class ChineseTextNormalizer
    {
        // Chinese number characters
        private readonly Dictionary<char, string> digitMap = new()
        {
            ['0'] = "零", ['1'] = "一", ['2'] = "二", ['3'] = "三", ['4'] = "四",
            ['5'] = "五", ['6'] = "六", ['7'] = "七", ['8'] = "八", ['9'] = "九"
        };
        
        // Number units
        private readonly string[] units = { "", "十", "百", "千" };
        private readonly string[] bigUnits = { "", "万", "亿", "万亿" };
        
        // Punctuation normalization
        private readonly Dictionary<char, char> punctuationMap = new()
        {
            ['，'] = ',', ['。'] = '.', ['！'] = '!', ['？'] = '?',
            ['；'] = ';', ['：'] = ':', ['（'] = '(', ['）'] = ')',
            ['【'] = '[', ['】'] = ']', ['《'] = '<', ['》'] = '>',
            ['、'] = ',', ['·'] = '·', ['"'] = '"', ['"'] = '"',
            ['\u2018'] = '\'', ['\u2019'] = '\'', ['…'] = '.'
        };
        
        public enum NumberFormat
        {
            Individual,  // 123 -> 一二三
            Formal      // 123 -> 一百二十三
        }
        
        /// <summary>
        /// Normalize Chinese text
        /// </summary>
        public string Normalize(string text, NumberFormat numberFormat = NumberFormat.Formal)
        {
            if (string.IsNullOrEmpty(text))
                return text;
                
            // Step 1: Normalize whitespace
            text = NormalizeWhitespace(text);
            
            // Step 2: Normalize punctuation
            text = NormalizePunctuation(text);
            
            // Step 3: Convert numbers
            text = NormalizeNumbers(text, numberFormat);
            
            // Step 4: Handle special characters
            text = NormalizeSpecialCharacters(text);
            
            return text;
        }
        
        private string NormalizeWhitespace(string text)
        {
            // Replace multiple spaces with single space
            text = Regex.Replace(text, @"\s+", " ");
            
            // Remove spaces around Chinese punctuation
            text = Regex.Replace(text, @"\s*([，。！？；：、])\s*", "$1");
            
            return text.Trim();
        }
        
        public string NormalizePunctuation(string text)
        {
            var result = new StringBuilder();
            
            foreach (var ch in text)
            {
                if (punctuationMap.TryGetValue(ch, out var normalized))
                {
                    result.Append(normalized);
                }
                else
                {
                    result.Append(ch);
                }
            }
            
            return result.ToString();
        }
        
        public string NormalizeNumbers(string text, NumberFormat format)
        {
            // Match sequences of digits
            var pattern = @"\d+";
            
            return Regex.Replace(text, pattern, match =>
            {
                var number = match.Value;
                
                if (format == NumberFormat.Individual)
                {
                    return ConvertToIndividualChinese(number);
                }
                else
                {
                    return ConvertToFormalChinese(number);
                }
            });
        }
        
        private string ConvertToIndividualChinese(string number)
        {
            var result = new StringBuilder();
            
            foreach (var digit in number)
            {
                if (digitMap.TryGetValue(digit, out var chinese))
                {
                    result.Append(chinese);
                }
                else
                {
                    result.Append(digit);
                }
            }
            
            return result.ToString();
        }
        
        private string ConvertToFormalChinese(string number)
        {
            if (number == "0")
                return "零";
                
            var value = long.Parse(number);
            
            if (value < 0)
            {
                return "负" + ConvertToFormalChinese((-value).ToString());
            }
            
            return ConvertPositiveToFormalChinese(value);
        }
        
        private string ConvertPositiveToFormalChinese(long value)
        {
            if (value == 0)
                return "";
                
            var result = new StringBuilder();
            var groups = new long[4]; // 个、万、亿、万亿
            
            // Split number into groups of 4 digits
            for (int i = 0; i < 4 && value > 0; i++)
            {
                groups[i] = value % 10000;
                value /= 10000;
            }
            
            // Find the highest non-zero group
            int highestGroup = 3;
            while (highestGroup >= 0 && groups[highestGroup] == 0)
            {
                highestGroup--;
            }
            
            // Track if we've seen any groups (for zero insertion)
            bool hasSeenNonZeroGroup = false;
            bool needZero = false;
            
            for (int i = highestGroup; i >= 0; i--)
            {
                if (groups[i] == 0)
                {
                    // Mark that we might need a zero if we see another group
                    if (hasSeenNonZeroGroup)
                    {
                        needZero = true;
                    }
                }
                else
                {
                    // We have a non-zero group
                    if (needZero && groups[i] < 1000)
                    {
                        result.Append("零");
                    }
                    
                    // Convert the group
                    result.Append(ConvertSegmentToChinese(groups[i]));
                    
                    // Add unit (万、亿、万亿)
                    if (i > 0)
                    {
                        result.Append(bigUnits[i]);
                    }
                    
                    hasSeenNonZeroGroup = true;
                    needZero = false;
                }
            }
            
            // Handle special cases like "一十" -> "十"
            var resultStr = result.ToString();
            if (resultStr.StartsWith("一十"))
            {
                resultStr = resultStr.Substring(1);
            }
            
            return resultStr;
        }
        
        private string ConvertSegmentToChinese(long segment)
        {
            if (segment == 0)
                return "";
                
            var result = new StringBuilder();
            var digitPos = 0;
            var hasZero = false;
            
            while (segment > 0)
            {
                var digit = segment % 10;
                
                if (digit == 0)
                {
                    hasZero = true;
                }
                else
                {
                    var digitStr = digitMap[(char)('0' + digit)];
                    
                    if (digitPos > 0)
                    {
                        digitStr += units[digitPos];
                    }
                    
                    if (hasZero && result.Length > 0)
                    {
                        result.Insert(0, "零");
                        hasZero = false;
                    }
                    
                    result.Insert(0, digitStr);
                }
                
                segment /= 10;
                digitPos++;
            }
            
            return result.ToString();
        }
        
        private string NormalizeSpecialCharacters(string text)
        {
            // Handle common abbreviations and special cases
            var replacements = new Dictionary<string, string>
            {
                ["etc."] = "等等",
                ["vs."] = "对",
                ["Mr."] = "先生",
                ["Mrs."] = "夫人",
                ["Ms."] = "女士",
                ["Dr."] = "博士"
            };
            
            foreach (var kvp in replacements)
            {
                text = text.Replace(kvp.Key, kvp.Value);
            }
            
            return text;
        }
        
        /// <summary>
        /// Split mixed Chinese-English text
        /// </summary>
        public (string chinese, string english)[] SplitMixedText(string text)
        {
            var segments = new List<(string chinese, string english)>();
            var pattern = @"([a-zA-Z]+[\s\-']*)+";
            
            var matches = Regex.Matches(text, pattern);
            var lastEnd = 0;
            
            foreach (Match match in matches)
            {
                // Chinese text before English
                if (match.Index > lastEnd)
                {
                    var chinese = text.Substring(lastEnd, match.Index - lastEnd);
                    segments.Add((chinese.Trim(), ""));
                }
                
                // English text
                segments.Add(("", match.Value.Trim()));
                lastEnd = match.Index + match.Length;
            }
            
            // Remaining Chinese text
            if (lastEnd < text.Length)
            {
                var chinese = text.Substring(lastEnd);
                segments.Add((chinese.Trim(), ""));
            }
            
            return segments.ToArray();
        }
        
        /// <summary>
        /// Check if character is Chinese
        /// </summary>
        public static bool IsChinese(char ch)
        {
            return (ch >= 0x4E00 && ch <= 0x9FFF) ||   // CJK Unified Ideographs
                   (ch >= 0x3400 && ch <= 0x4DBF) ||   // CJK Extension A
                   (ch >= 0xF900 && ch <= 0xFAFF);     // CJK Compatibility Ideographs
        }
    }
}