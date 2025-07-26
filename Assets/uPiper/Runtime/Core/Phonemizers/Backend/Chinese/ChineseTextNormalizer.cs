using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace uPiper.Core.Phonemizers.Backend.Chinese
{
    /// <summary>
    /// Chinese text normalizer for preprocessing text before phonemization.
    /// Handles numbers, punctuation, and mixed Chinese-English text.
    /// </summary>
    public class ChineseTextNormalizer
    {
        private readonly Dictionary<char, string> digitMap;
        private readonly Dictionary<string, string> abbreviations;
        
        public ChineseTextNormalizer()
        {
            InitializeMappings();
        }
        
        public string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
                
            // 1. Convert traditional to simplified (basic mappings)
            text = ConvertTraditionalToSimplified(text);
            
            // 2. Expand abbreviations
            text = ExpandAbbreviations(text);
            
            // 3. Convert numbers to Chinese
            text = NormalizeNumbers(text);
            
            // 4. Handle special characters and punctuation
            text = HandleSpecialCharacters(text);
            
            // 5. Normalize whitespace
            text = Regex.Replace(text, @"\s+", " ").Trim();
            
            return text;
        }
        
        private string ConvertTraditionalToSimplified(string text)
        {
            // Basic traditional to simplified mappings
            // In production, use a comprehensive conversion library
            var mappings = new Dictionary<char, char>
            {
                ['國'] = '国', ['學'] = '学', ['愛'] = '爱', ['時'] = '时',
                ['會'] = '会', ['來'] = '来', ['為'] = '为', ['東'] = '东',
                ['車'] = '车', ['馬'] = '马', ['開'] = '开', ['關'] = '关',
                ['門'] = '门', ['們'] = '们', ['個'] = '个', ['書'] = '书',
                ['長'] = '长', ['萬'] = '万', ['與'] = '与', ['這'] = '这',
                ['進'] = '进', ['對'] = '对', ['將'] = '将', ['點'] = '点',
                ['應'] = '应', ['從'] = '从', ['動'] = '动', ['兩'] = '两',
                ['雖'] = '虽', ['無'] = '无', ['於'] = '于', ['話'] = '话',
                ['過'] = '过', ['見'] = '见', ['錢'] = '钱', ['飛'] = '飞',
                ['聽'] = '听', ['覺'] = '觉', ['還'] = '还', ['幾'] = '几'
            };
            
            var result = new StringBuilder();
            foreach (char ch in text)
            {
                result.Append(mappings.ContainsKey(ch) ? mappings[ch] : ch);
            }
            return result.ToString();
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
            // Convert Arabic numerals to Chinese
            text = Regex.Replace(text, @"\d+", match =>
            {
                string number = match.Value;
                return ConvertNumberToChinese(number);
            });
            
            return text;
        }
        
        private string ConvertNumberToChinese(string numberStr)
        {
            if (!long.TryParse(numberStr, out long number))
                return numberStr;
                
            if (number == 0)
                return "零";
                
            // Handle negative numbers
            if (number < 0)
                return "负" + ConvertNumberToChinese((-number).ToString());
                
            var result = new StringBuilder();
            
            // Handle numbers up to 9999亿
            if (number >= 100000000)
            {
                long yi = number / 100000000;
                result.Append(ConvertNumberBelow10000(yi) + "亿");
                number %= 100000000;
            }
            
            if (number >= 10000)
            {
                long wan = number / 10000;
                result.Append(ConvertNumberBelow10000(wan) + "万");
                number %= 10000;
            }
            
            if (number > 0)
            {
                if (result.Length > 0 && number < 1000)
                {
                    result.Append("零");
                }
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
                result.Append(digitMap[(char)('0' + number / 1000)] + "千");
                number %= 1000;
            }
            
            if (number >= 100)
            {
                result.Append(digitMap[(char)('0' + number / 100)] + "百");
                number %= 100;
            }
            else if (result.Length > 0 && number > 0)
            {
                result.Append("零");
            }
            
            if (number >= 10)
            {
                if (number >= 20)
                {
                    result.Append(digitMap[(char)('0' + number / 10)]);
                }
                result.Append("十");
                number %= 10;
            }
            else if (result.Length > 0 && number > 0 && !result.ToString().EndsWith("零"))
            {
                result.Append("零");
            }
            
            if (number > 0)
            {
                result.Append(digitMap[(char)('0' + number)]);
            }
            
            return result.ToString();
        }
        
        private string HandleSpecialCharacters(string text)
        {
            // Currency
            text = text.Replace("$", "美元");
            text = text.Replace("¥", "元");
            text = text.Replace("￥", "元");
            text = text.Replace("€", "欧元");
            text = text.Replace("£", "英镑");
            
            // Common symbols
            text = text.Replace("%", "百分之");
            text = text.Replace("&", "和");
            text = text.Replace("+", "加");
            text = text.Replace("-", "减");
            text = text.Replace("*", "乘");
            text = text.Replace("/", "除以");
            text = text.Replace("=", "等于");
            text = text.Replace("@", "at");
            text = text.Replace("#", "井号");
            
            // Punctuation normalization
            text = text.Replace("！", "!");
            text = text.Replace("？", "?");
            text = text.Replace("。", ".");
            text = text.Replace("，", ",");
            text = text.Replace("、", ",");
            text = text.Replace("；", ";");
            text = text.Replace("：", ":");
            text = text.Replace(""", "\"");
            text = text.Replace(""", "\"");
            text = text.Replace("'", "'");
            text = text.Replace("'", "'");
            text = text.Replace("（", "(");
            text = text.Replace("）", ")");
            text = text.Replace("【", "[");
            text = text.Replace("】", "]");
            text = text.Replace("《", "<");
            text = text.Replace("》", ">");
            
            return text;
        }
        
        private void InitializeMappings()
        {
            digitMap = new Dictionary<char, string>
            {
                ['0'] = "零",
                ['1'] = "一",
                ['2'] = "二",
                ['3'] = "三",
                ['4'] = "四",
                ['5'] = "五",
                ['6'] = "六",
                ['7'] = "七",
                ['8'] = "八",
                ['9'] = "九"
            };
            
            abbreviations = new Dictionary<string, string>
            {
                // Common abbreviations
                ["VS"] = "对",
                ["vs"] = "对",
                ["PK"] = "对决",
                ["pk"] = "对决",
                ["OK"] = "好的",
                ["ok"] = "好的",
                ["APP"] = "应用程序",
                ["app"] = "应用程序",
                ["AI"] = "人工智能",
                ["VR"] = "虚拟现实",
                ["AR"] = "增强现实",
                ["PC"] = "个人电脑",
                ["TV"] = "电视",
                ["DVD"] = "数字视频光盘",
                ["CEO"] = "首席执行官",
                ["GDP"] = "国内生产总值",
                ["NBA"] = "美国职业篮球联赛",
                ["IT"] = "信息技术",
                ["3D"] = "三维",
                ["4G"] = "第四代",
                ["5G"] = "第五代",
                ["WiFi"] = "无线网络",
                ["GPS"] = "全球定位系统",
                ["ATM"] = "自动取款机",
                ["VIP"] = "贵宾",
                ["DIY"] = "自己动手做",
                ["FAQ"] = "常见问题",
                ["PM2.5"] = "细颗粒物",
                ["KTV"] = "卡拉OK"
            };
        }
    }
}