using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace uPiper.Core.Phonemizers.Backend.Korean
{
    /// <summary>
    /// Korean text normalizer for preprocessing text before phonemization.
    /// Handles numbers, punctuation, and mixed Korean-English text.
    /// </summary>
    public class KoreanTextNormalizer
    {
        private readonly Dictionary<char, string> digitMap;
        private readonly Dictionary<string, string> abbreviations;
        
        public KoreanTextNormalizer()
        {
            InitializeMappings();
        }
        
        public string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
                
            // 1. Expand abbreviations
            text = ExpandAbbreviations(text);
            
            // 2. Convert numbers to Korean
            text = NormalizeNumbers(text);
            
            // 3. Handle special characters
            text = HandleSpecialCharacters(text);
            
            // 4. Normalize punctuation
            text = NormalizePunctuation(text);
            
            // 5. Normalize whitespace
            text = Regex.Replace(text, @"\s+", " ").Trim();
            
            return text;
        }
        
        private string ExpandAbbreviations(string text)
        {
            foreach (var abbr in abbreviations)
            {
                text = text.Replace(abbr.Key, abbr.Value);
            }
            return text;
        }
        
        private string NormalizeNumbers(string text)
        {
            // Convert Arabic numerals to Korean
            text = Regex.Replace(text, @"\d+", match =>
            {
                string number = match.Value;
                return ConvertNumberToKorean(number);
            });
            
            return text;
        }
        
        private string ConvertNumberToKorean(string numberStr)
        {
            if (!long.TryParse(numberStr, out long number))
                return numberStr;
                
            if (number == 0)
                return "영";
                
            // Handle negative numbers
            if (number < 0)
                return "마이너스 " + ConvertNumberToKorean((-number).ToString());
                
            var result = new StringBuilder();
            
            // Korean number system uses 만 (10,000) as base
            if (number >= 100000000) // 억 (100 million)
            {
                long eok = number / 100000000;
                result.Append(ConvertNumberBelow10000(eok) + "억");
                number %= 100000000;
                if (number > 0) result.Append(" ");
            }
            
            if (number >= 10000) // 만 (10,000)
            {
                long man = number / 10000;
                result.Append(ConvertNumberBelow10000(man) + "만");
                number %= 10000;
                if (number > 0) result.Append(" ");
            }
            
            if (number > 0)
            {
                result.Append(ConvertNumberBelow10000(number));
            }
            
            return result.ToString();
        }
        
        private string ConvertNumberBelow10000(long number)
        {
            if (number == 0)
                return "";
                
            var result = new StringBuilder();
            
            if (number >= 1000)
            {
                result.Append(digitMap[(char)('0' + number / 1000)] + "천");
                number %= 1000;
                if (number > 0) result.Append(" ");
            }
            
            if (number >= 100)
            {
                result.Append(digitMap[(char)('0' + number / 100)] + "백");
                number %= 100;
                if (number > 0) result.Append(" ");
            }
            
            if (number >= 10)
            {
                result.Append(digitMap[(char)('0' + number / 10)] + "십");
                number %= 10;
                if (number > 0) result.Append(" ");
            }
            
            if (number > 0)
            {
                result.Append(digitMap[(char)('0' + number)]);
            }
            
            // Remove redundant "일" (one) in certain positions
            string resultStr = result.ToString();
            resultStr = Regex.Replace(resultStr, @"일천", "천");
            resultStr = Regex.Replace(resultStr, @"일백", "백");
            resultStr = Regex.Replace(resultStr, @"일십", "십");
            
            return resultStr;
        }
        
        private string HandleSpecialCharacters(string text)
        {
            // Currency
            text = text.Replace("$", "달러");
            text = text.Replace("₩", "원");
            text = text.Replace("￦", "원");
            text = text.Replace("€", "유로");
            text = text.Replace("£", "파운드");
            text = text.Replace("¥", "엔");
            
            // Common symbols
            text = text.Replace("%", "퍼센트");
            text = text.Replace("&", "앤드");
            text = text.Replace("+", "플러스");
            text = text.Replace("-", "마이너스");
            text = text.Replace("*", "곱하기");
            text = text.Replace("/", "나누기");
            text = text.Replace("=", "는");
            text = text.Replace("@", "골뱅이");
            text = text.Replace("#", "샵");
            
            return text;
        }
        
        private string NormalizePunctuation(string text)
        {
            // Korean uses different quotation marks
            text = text.Replace(""", "\"");
            text = text.Replace(""", "\"");
            text = text.Replace("'", "'");
            text = text.Replace("'", "'");
            text = text.Replace("『", "\"");
            text = text.Replace("』", "\"");
            text = text.Replace("「", "'");
            text = text.Replace("」", "'");
            
            // Normalize other punctuation
            text = text.Replace("。", ".");
            text = text.Replace("、", ",");
            text = text.Replace("·", ",");
            text = text.Replace("〜", "~");
            text = text.Replace("～", "~");
            
            return text;
        }
        
        private void InitializeMappings()
        {
            digitMap = new Dictionary<char, string>
            {
                ['0'] = "영",
                ['1'] = "일",
                ['2'] = "이",
                ['3'] = "삼",
                ['4'] = "사",
                ['5'] = "오",
                ['6'] = "육",
                ['7'] = "칠",
                ['8'] = "팔",
                ['9'] = "구"
            };
            
            abbreviations = new Dictionary<string, string>
            {
                // Common English abbreviations used in Korean
                ["OK"] = "오케이",
                ["PC"] = "피씨",
                ["TV"] = "티비",
                ["CD"] = "시디",
                ["DVD"] = "디비디",
                ["USB"] = "유에스비",
                ["IT"] = "아이티",
                ["AI"] = "에이아이",
                ["CEO"] = "최고경영자",
                ["SNS"] = "에스엔에스",
                ["GPS"] = "지피에스",
                ["WiFi"] = "와이파이",
                ["LTE"] = "엘티이",
                ["5G"] = "오지",
                
                // Korean abbreviations
                ["서울대"] = "서울대학교",
                ["고대"] = "고려대학교",
                ["연대"] = "연세대학교",
                ["국민대"] = "국민대학교",
                ["한대"] = "한양대학교",
                
                // Common terms
                ["vs"] = "대",
                ["VS"] = "대",
                ["No."] = "넘버",
                ["no."] = "넘버",
                
                // Units
                ["km"] = "킬로미터",
                ["m"] = "미터",
                ["cm"] = "센티미터",
                ["kg"] = "킬로그램",
                ["g"] = "그램"
            };
        }
    }
}