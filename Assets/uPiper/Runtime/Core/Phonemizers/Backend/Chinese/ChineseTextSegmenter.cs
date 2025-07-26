using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace uPiper.Core.Phonemizers.Backend.Chinese
{
    /// <summary>
    /// Simple Chinese text segmenter using character-based approach.
    /// For production use, consider integrating a proper segmentation library.
    /// </summary>
    public class ChineseTextSegmenter
    {
        private HashSet<string> commonWords;
        
        public ChineseTextSegmenter()
        {
            InitializeCommonWords();
        }
        
        /// <summary>
        /// Segment Chinese text into words/characters
        /// </summary>
        public List<string> Segment(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new List<string>();
                
            var segments = new List<string>();
            var currentSegment = new StringBuilder();
            bool inNonChinese = false;
            
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                
                if (IsChinese(ch))
                {
                    // If we were processing non-Chinese, add it as segment
                    if (inNonChinese && currentSegment.Length > 0)
                    {
                        segments.Add(currentSegment.ToString());
                        currentSegment.Clear();
                        inNonChinese = false;
                    }
                    
                    // Try to match common words using forward maximum matching
                    string matched = TryMatchWord(text, i);
                    if (matched != null)
                    {
                        segments.Add(matched);
                        i += matched.Length - 1; // Skip matched characters
                    }
                    else
                    {
                        // Single character as segment
                        segments.Add(ch.ToString());
                    }
                }
                else if (char.IsWhiteSpace(ch))
                {
                    // Add accumulated segment if any
                    if (currentSegment.Length > 0)
                    {
                        segments.Add(currentSegment.ToString());
                        currentSegment.Clear();
                    }
                    inNonChinese = false;
                    
                    // Add space as separate segment
                    segments.Add(" ");
                }
                else
                {
                    // Non-Chinese character (English, numbers, punctuation)
                    inNonChinese = true;
                    currentSegment.Append(ch);
                }
            }
            
            // Add final segment if any
            if (currentSegment.Length > 0)
            {
                segments.Add(currentSegment.ToString());
            }
            
            return segments;
        }
        
        private string TryMatchWord(string text, int startIndex)
        {
            // Simple forward maximum matching
            // Try to match longest possible word from common words
            int maxLength = Math.Min(4, text.Length - startIndex); // Most Chinese words are 2-4 characters
            
            for (int len = maxLength; len >= 2; len--)
            {
                if (startIndex + len <= text.Length)
                {
                    string candidate = text.Substring(startIndex, len);
                    if (commonWords.Contains(candidate))
                    {
                        return candidate;
                    }
                }
            }
            
            return null; // No match found
        }
        
        private bool IsChinese(char ch)
        {
            return (ch >= 0x4E00 && ch <= 0x9FFF) ||     // CJK Unified Ideographs
                   (ch >= 0x3400 && ch <= 0x4DBF) ||     // CJK Extension A
                   (ch >= 0xF900 && ch <= 0xFAFF);       // CJK Compatibility Ideographs
        }
        
        private void InitializeCommonWords()
        {
            // Initialize with common Chinese words
            // This is a simplified approach - a real implementation would load from dictionary
            commonWords = new HashSet<string>
            {
                // Common two-character words
                "你好", "中国", "我们", "他们", "大家", "现在", "时间", "今天", "明天", "昨天",
                "可以", "不是", "没有", "知道", "什么", "怎么", "为什么", "这个", "那个", "哪个",
                "很好", "非常", "已经", "还是", "但是", "因为", "所以", "如果", "虽然", "或者",
                "工作", "学习", "生活", "朋友", "家人", "公司", "学校", "医院", "商店", "饭店",
                "电话", "电脑", "手机", "汽车", "飞机", "火车", "地铁", "公交", "出租", "自行",
                "早上", "中午", "下午", "晚上", "星期", "月份", "年份", "小时", "分钟", "秒钟",
                
                // Common three-character words
                "星期一", "星期二", "星期三", "星期四", "星期五", "星期六", "星期天",
                "怎么样", "为什么", "不知道", "没关系", "对不起", "谢谢你", "不客气",
                "火车站", "飞机场", "汽车站", "地铁站", "公交站", "出租车",
                
                // Common four-character words/phrases
                "欢迎光临", "恭喜发财", "身体健康", "万事如意", "心想事成", "一路平安",
                "努力工作", "认真学习", "天天向上", "身体力行", "实事求是", "与时俱进",
                
                // Common function words that should stay together
                "可是", "但是", "虽然", "因为", "所以", "如果", "那么", "或者", "而且", "并且",
                "不但", "而且", "除了", "另外", "首先", "其次", "最后", "总之", "比如", "例如"
            };
        }
    }
}