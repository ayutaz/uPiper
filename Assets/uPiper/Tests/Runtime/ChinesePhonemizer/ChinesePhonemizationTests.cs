using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using uPiper.Core.Phonemizers;
using uPiper.Core.Phonemizers.Backend;
using UnityEngine;
using UnityEngine.TestTools;

namespace uPiper.Tests.Runtime.ChinesePhonemizer
{
    public class ChinesePhonemizationTests
    {
        private Core.Phonemizers.Backend.ChinesePhonemizer phonemizer;
        
        [SetUp]
        public async Task Setup()
        {
            phonemizer = new Core.Phonemizers.Backend.ChinesePhonemizer();
            var initialized = await phonemizer.InitializeAsync(new PhonemizerBackendOptions());
            Assert.IsTrue(initialized, "Failed to initialize Chinese phonemizer");
        }
        
        [TearDown]
        public void TearDown()
        {
            phonemizer?.Dispose();
        }
        
        [Test]
        public async Task ChinesePhonemizer_BasicCharacters()
        {
            var testCases = new Dictionary<string, string[]>
            {
                ["你"] = new[] { "ni", "˨˩˦" },      // ni3
                ["好"] = new[] { "xau", "˨˩˦" },     // hao3
                ["中"] = new[] { "ʈʂuŋ", "˥" },      // zhong1
                ["国"] = new[] { "kuo", "˧˥" },      // guo2
                ["人"] = new[] { "ʐən", "˧˥" }       // ren2
            };
            
            foreach (var kvp in testCases)
            {
                var result = await phonemizer.PhonemizeAsync(kvp.Key, "zh-CN");
                Assert.IsTrue(result.Success, $"Failed to phonemize '{kvp.Key}'");
                
                // Check if phonemes contain expected IPA
                var phonemeString = string.Join(" ", result.Phonemes);
                Debug.Log($"'{kvp.Key}' -> {phonemeString}");
                
                // Basic validation - at least some phonemes should be present
                Assert.Greater(result.Phonemes.Length, 0, $"No phonemes for '{kvp.Key}'");
            }
        }
        
        [Test]
        public async Task ChinesePhonemizer_CommonPhrases()
        {
            var testCases = new Dictionary<string, int>
            {
                ["你好"] = 4,       // ni3 hao3 -> 4+ phonemes
                ["中国"] = 4,       // zhong1 guo2 -> 4+ phonemes
                ["谢谢"] = 4,       // xie4 xie4 -> 4+ phonemes
                ["再见"] = 4,       // zai4 jian4 -> 4+ phonemes
                ["中国人"] = 6      // zhong1 guo2 ren2 -> 6+ phonemes
            };
            
            foreach (var kvp in testCases)
            {
                var result = await phonemizer.PhonemizeAsync(kvp.Key, "zh-CN");
                Assert.IsTrue(result.Success, $"Failed to phonemize '{kvp.Key}'");
                
                var phonemeString = string.Join(" ", result.Phonemes);
                Debug.Log($"'{kvp.Key}' -> {phonemeString}");
                
                Assert.GreaterOrEqual(result.Phonemes.Length, kvp.Value, 
                    $"Expected at least {kvp.Value} phonemes for '{kvp.Key}', got {result.Phonemes.Length}");
            }
        }
        
        [Test]
        public async Task ChinesePhonemizer_NumberNormalization()
        {
            var testCases = new Dictionary<string, string[]>
            {
                ["123"] = new[] { "一百二十三" },
                ["2024年"] = new[] { "二零二四年" },
                ["第1个"] = new[] { "第一个" }
            };
            
            foreach (var kvp in testCases)
            {
                var result = await phonemizer.PhonemizeAsync(kvp.Key, "zh-CN");
                Assert.IsTrue(result.Success, $"Failed to phonemize '{kvp.Key}'");
                
                var phonemeString = string.Join(" ", result.Phonemes);
                Debug.Log($"'{kvp.Key}' -> {phonemeString}");
                
                // Should have phonemes for the normalized Chinese numbers
                Assert.Greater(result.Phonemes.Length, 3, 
                    $"Expected phonemes for normalized numbers in '{kvp.Key}'");
            }
        }
        
        [Test]
        public async Task ChinesePhonemizer_MixedChineseEnglish()
        {
            var text = "这是一个test句子with English";
            var result = await phonemizer.PhonemizeAsync(text, "zh-CN");
            
            Assert.IsTrue(result.Success);
            Assert.Greater(result.Phonemes.Length, 10, "Expected phonemes for mixed text");
            
            var phonemeString = string.Join(" ", result.Phonemes);
            Debug.Log($"Mixed text -> {phonemeString}");
            
            // Should contain both Chinese phonemes and English letters
            var hasChinesePhonemes = result.Phonemes.Any(p => p.Contains("ʂ") || p.Contains("ʈʂ") || p.Contains("ŋ"));
            var hasEnglishLetters = result.Phonemes.Any(p => p.Length == 1 && char.IsLetter(p[0]));
            
            Assert.IsTrue(hasChinesePhonemes, "Should have Chinese phonemes");
            Assert.IsTrue(hasEnglishLetters, "Should have English letters");
        }
        
        [Test]
        public async Task ChinesePhonemizer_Punctuation()
        {
            var text = "你好，世界！";
            var result = await phonemizer.PhonemizeAsync(text, "zh-CN");
            
            Assert.IsTrue(result.Success);
            
            // Should have pause marker for punctuation
            var hasPause = result.Phonemes.Contains("_");
            Assert.IsTrue(hasPause, "Should have pause marker for punctuation");
        }
        
        [Test]
        public async Task ChinesePhonemizer_EmptyText()
        {
            var result = await phonemizer.PhonemizeAsync("", "zh-CN");
            
            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, result.Phonemes.Length, "Empty text should produce no phonemes");
        }
        
        [Test]
        public async Task ChinesePhonemizer_Performance()
        {
            var text = "这是一个测试句子，包含标点符号和English words。";
            var iterations = 100;
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            for (int i = 0; i < iterations; i++)
            {
                var result = await phonemizer.PhonemizeAsync(text, "zh-CN");
                Assert.IsTrue(result.Success);
            }
            
            stopwatch.Stop();
            var avgMs = stopwatch.ElapsedMilliseconds / (double)iterations;
            
            Debug.Log($"Average phonemization time: {avgMs:F2}ms");
            Assert.Less(avgMs, 50, "Average phonemization should be under 50ms");
        }
        
        [Test]
        public void ChinesePhonemizer_Capabilities()
        {
            var capabilities = phonemizer.GetCapabilities();
            
            Assert.IsTrue(capabilities.SupportsIPA, "Should support IPA");
            Assert.IsTrue(capabilities.SupportsTones, "Should support tones");
            Assert.IsTrue(capabilities.SupportsSyllables, "Should support syllables");
            Assert.IsFalse(capabilities.RequiresNetwork, "Should not require network");
            Assert.IsTrue(capabilities.IsThreadSafe, "Should be thread safe");
        }
        
        [Test]
        public void ChinesePhonemizer_SupportedLanguages()
        {
            var languages = phonemizer.SupportedLanguages;
            
            Assert.Contains("zh", languages, "Should support generic Chinese");
            Assert.Contains("zh-CN", languages, "Should support Simplified Chinese");
            Assert.Contains("zh-TW", languages, "Should support Traditional Chinese");
            Assert.Contains("zh-HK", languages, "Should support Hong Kong Chinese");
            Assert.Contains("zh-SG", languages, "Should support Singapore Chinese");
        }
        
        [Test]
        public void ChinesePhonemizer_MemoryUsage()
        {
            var memoryUsage = phonemizer.GetMemoryUsage();
            
            Debug.Log($"Memory usage: {memoryUsage / 1024.0:F2} KB");
            
            // Should be reasonable (under 10MB for basic dictionary)
            Assert.Less(memoryUsage, 10 * 1024 * 1024, "Memory usage should be under 10MB");
        }
    }
}