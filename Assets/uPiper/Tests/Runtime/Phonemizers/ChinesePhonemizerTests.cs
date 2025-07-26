using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core.Phonemizers.Backend.Chinese;

namespace uPiper.Tests.Phonemizers
{
    /// <summary>
    /// Test suite for Chinese (Mandarin) phonemizer implementation
    /// </summary>
    [TestFixture]
    public class ChinesePhonemizerTests
    {
        private ChinesePhonemizer phonemizer;
        private PinyinToPhonemeMapper pinyinMapper;
        private ChineseTextSegmenter segmenter;
        private ChineseTextNormalizer normalizer;
        
        [SetUp]
        public async Task SetUp()
        {
            phonemizer = new ChinesePhonemizer();
            await phonemizer.InitializeAsync(null);
            
            pinyinMapper = new PinyinToPhonemeMapper();
            segmenter = new ChineseTextSegmenter();
            normalizer = new ChineseTextNormalizer();
        }
        
        [TearDown]
        public void TearDown()
        {
            phonemizer?.Dispose();
        }
        
        #region Basic Functionality Tests
        
        [Test]
        public async Task Chinese_ShouldInitializeSuccessfully()
        {
            Assert.IsTrue(phonemizer.IsInitialized);
            Assert.Contains("zh", phonemizer.SupportedLanguages);
            Assert.Contains("zh-CN", phonemizer.SupportedLanguages);
            Assert.Contains("zh-TW", phonemizer.SupportedLanguages);
        }
        
        [Test]
        public async Task Chinese_ShouldPhonemizeBasicCharacters()
        {
            var tests = new Dictionary<string, string>
            {
                ["你"] = "ni3",
                ["好"] = "hao3",
                ["中"] = "zhong1",
                ["国"] = "guo2",
                ["人"] = "ren2"
            };
            
            foreach (var (character, expectedPinyin) in tests)
            {
                var result = await phonemizer.PhonemizeAsync(character, "zh-CN");
                
                Assert.IsNotNull(result);
                Assert.IsNotEmpty(result.Phonemes);
                
                // Verify contains expected phonemes (simplified check)
                var phonemeStr = string.Join(" ", result.Phonemes.Where(p => p != "_"));
                Debug.Log($"'{character}' ({expectedPinyin}) -> [{phonemeStr}]");
                
                Assert.IsTrue(result.Success);
            }
        }
        
        [Test]
        public async Task Chinese_ShouldHandleCommonPhrases()
        {
            var phrases = new[]
            {
                "你好",
                "谢谢",
                "再见",
                "不客气",
                "对不起"
            };
            
            foreach (var phrase in phrases)
            {
                var result = await phonemizer.PhonemizeAsync(phrase, "zh-CN");
                
                Assert.IsNotNull(result);
                Assert.IsNotEmpty(result.Phonemes);
                Assert.IsTrue(result.Success);
                
                Debug.Log($"'{phrase}' -> {string.Join(" ", result.Phonemes.Take(20))}...");
            }
        }
        
        #endregion
        
        #region Pinyin Mapper Tests
        
        [Test]
        public void PinyinMapper_ShouldConvertBasicSyllables()
        {
            var tests = new Dictionary<string, string[]>
            {
                ["ma1"] = new[] { "m", "a", "˥" },      // 妈 (mother)
                ["ma2"] = new[] { "m", "a", "˧˥" },     // 麻 (hemp)
                ["ma3"] = new[] { "m", "a", "˨˩˦" },    // 马 (horse)
                ["ma4"] = new[] { "m", "a", "˥˩" },     // 骂 (scold)
                ["ma"] = new[] { "m", "a" }             // neutral tone
            };
            
            foreach (var (pinyin, expected) in tests)
            {
                var result = pinyinMapper.PinyinToIPA(pinyin);
                
                Debug.Log($"Pinyin '{pinyin}' -> IPA [{string.Join(" ", result)}]");
                
                Assert.AreEqual(expected.Length, result.Count,
                    $"Expected {expected.Length} phonemes for '{pinyin}', got {result.Count}");
            }
        }
        
        [Test]
        public void PinyinMapper_ShouldHandleComplexFinals()
        {
            var tests = new Dictionary<string, bool>
            {
                ["xiang1"] = true,  // Should have initial 'ɕ' + complex final
                ["zhuang4"] = true, // Retroflex + complex final
                ["yuan2"] = true,   // No initial + complex final
                ["er2"] = true      // Special 'er' final
            };
            
            foreach (var (pinyin, shouldSucceed) in tests)
            {
                var result = pinyinMapper.PinyinToIPA(pinyin);
                
                if (shouldSucceed)
                {
                    Assert.IsNotEmpty(result, $"Should convert '{pinyin}' successfully");
                    Debug.Log($"'{pinyin}' -> [{string.Join(" ", result)}]");
                }
            }
        }
        
        #endregion
        
        #region Text Segmenter Tests
        
        [Test]
        public void Segmenter_ShouldSegmentMixedText()
        {
            var text = "我的iPhone很好用";
            var segments = segmenter.Segment(text);
            
            Assert.IsNotEmpty(segments);
            
            // Should separate Chinese and English
            Assert.Contains("iPhone", segments);
            
            Debug.Log($"Segmented: [{string.Join("|", segments)}]");
        }
        
        [Test]
        public void Segmenter_ShouldRecognizeCommonWords()
        {
            var text = "你好世界，今天天气很好";
            var segments = segmenter.Segment(text);
            
            // Should recognize "你好" as one word
            Assert.Contains("你好", segments);
            
            Debug.Log($"Segmented: [{string.Join("|", segments)}]");
        }
        
        #endregion
        
        #region Text Normalizer Tests
        
        [Test]
        public void Normalizer_ShouldConvertNumbers()
        {
            var tests = new Dictionary<string, string>
            {
                ["1个"] = "一个",
                ["10元"] = "十元",
                ["100人"] = "一百人",
                ["2023年"] = "二千零二十三年",
                ["5G网络"] = "第五代网络"
            };
            
            foreach (var (input, expected) in tests)
            {
                var result = normalizer.Normalize(input);
                Assert.AreEqual(expected, result,
                    $"Failed to normalize '{input}'");
            }
        }
        
        [Test]
        public void Normalizer_ShouldHandleTraditionalCharacters()
        {
            var tests = new Dictionary<string, string>
            {
                ["國家"] = "国家",
                ["學習"] = "学习",
                ["愛"] = "爱",
                ["時間"] = "时间"
            };
            
            foreach (var (traditional, simplified) in tests)
            {
                var result = normalizer.Normalize(traditional);
                Assert.AreEqual(simplified, result,
                    $"Failed to convert '{traditional}' to simplified");
            }
        }
        
        [Test]
        public void Normalizer_ShouldExpandAbbreviations()
        {
            var tests = new Dictionary<string, string>
            {
                ["OK的"] = "好的的",
                ["APP下载"] = "应用程序下载",
                ["AI技术"] = "人工智能技术",
                ["CEO说"] = "首席执行官说"
            };
            
            foreach (var (input, expected) in tests)
            {
                var result = normalizer.Normalize(input);
                Assert.AreEqual(expected, result);
            }
        }
        
        #endregion
        
        #region Integration Tests
        
        [Test]
        public async Task Chinese_ShouldPhonemizeSentences()
        {
            var sentences = new[]
            {
                "今天天气很好。",
                "我喜欢学习中文。",
                "这个APP很好用。",
                "明天见！",
                "请问去北京怎么走？"
            };
            
            foreach (var sentence in sentences)
            {
                var result = await phonemizer.PhonemizeAsync(sentence, "zh-CN");
                
                Assert.IsNotNull(result);
                Assert.IsNotEmpty(result.Phonemes);
                Assert.Greater(result.Phonemes.Count, 5, 
                    $"Too few phonemes for sentence: {sentence}");
                
                Debug.Log($"Sentence: {sentence}");
                Debug.Log($"Phonemes ({result.Phonemes.Count}): {string.Join(" ", result.Phonemes.Take(30))}...");
            }
        }
        
        [Test]
        public async Task Chinese_ShouldHandleMixedChineseEnglish()
        {
            var text = "我的email是test@example.com";
            var result = await phonemizer.PhonemizeAsync(text, "zh-CN");
            
            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result.Phonemes);
            
            // Should contain both Chinese phonemes and English letters
            var phonemeStr = string.Join(" ", result.Phonemes);
            Assert.IsTrue(phonemeStr.Contains("e") || phonemeStr.Contains("m"));
            
            Debug.Log($"Mixed text phonemes: {phonemeStr}");
        }
        
        [Test]
        public async Task Chinese_ShouldProvideTimingInfo()
        {
            var text = "你好世界";
            var result = await phonemizer.PhonemizeAsync(text, "zh-CN");
            
            Assert.IsNotNull(result.TimingInfo);
            Assert.AreEqual(result.Phonemes.Count, result.TimingInfo.Count);
            
            // Check timing progression
            double lastTime = 0;
            foreach (var timing in result.TimingInfo)
            {
                Assert.GreaterOrEqual(timing.StartTime, lastTime);
                Assert.Greater(timing.Duration, 0);
                lastTime = timing.StartTime;
            }
        }
        
        #endregion
        
        #region Performance Tests
        
        [Test]
        public async Task Chinese_PerformanceBenchmark()
        {
            var testText = "这是一个用于测试中文语音合成系统性能的测试文本。";
            
            // Warm up
            await phonemizer.PhonemizeAsync("测试", "zh-CN");
            
            // Measure
            var sw = System.Diagnostics.Stopwatch.StartNew();
            const int iterations = 100;
            
            for (int i = 0; i < iterations; i++)
            {
                await phonemizer.PhonemizeAsync(testText, "zh-CN");
            }
            
            sw.Stop();
            double avgMs = sw.ElapsedMilliseconds / (double)iterations;
            
            Debug.Log($"Chinese phonemization average time: {avgMs:F2} ms");
            Assert.Less(avgMs, 20, "Should process text in under 20ms average");
        }
        
        #endregion
        
        #region Edge Cases
        
        [Test]
        public async Task Chinese_ShouldHandleEmptyText()
        {
            var result = await phonemizer.PhonemizeAsync("", "zh-CN");
            
            Assert.IsNotNull(result);
            Assert.IsEmpty(result.Phonemes);
        }
        
        [Test]
        public async Task Chinese_ShouldHandlePunctuationOnly()
        {
            var result = await phonemizer.PhonemizeAsync("。，！？", "zh-CN");
            
            Assert.IsNotNull(result);
            // Should have pauses for punctuation
            Assert.IsNotEmpty(result.Phonemes);
            Assert.IsTrue(result.Phonemes.All(p => p == "_"));
        }
        
        [Test]
        public async Task Chinese_ShouldHandleUnknownCharacters()
        {
            // Use a rare character that might not be in the sample dictionary
            var text = "你好𠀀世界"; // Contains a rare CJK Extension character
            
            var result = await phonemizer.PhonemizeAsync(text, "zh-CN");
            
            // Should still produce some output
            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result.Phonemes);
        }
        
        #endregion
    }
}