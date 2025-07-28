using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core.Phonemizers.Backend;

namespace uPiper.Tests.Phonemizers
{
    /// <summary>
    /// Test suite for Korean phonemizer implementation
    /// </summary>
    [TestFixture]
    public class KoreanPhonemizerTests
    {
        private KoreanPhonemizer phonemizer;
        // Note: Component classes are now internal to the proxy
        // private HangulProcessor hangulProcessor;
        // private KoreanG2P g2pEngine;
        // private KoreanTextNormalizer normalizer;
        
        [SetUp]
        public async Task SetUp()
        {
            phonemizer = new KoreanPhonemizer();
            await phonemizer.InitializeAsync(null);
            
            // Component classes are now internal to the proxy
            // hangulProcessor = new HangulProcessor();
            // g2pEngine = new KoreanG2P();
            // normalizer = new KoreanTextNormalizer();
        }
        
        [TearDown]
        public void TearDown()
        {
            phonemizer?.Dispose();
        }
        
        #region Basic Functionality Tests
        
        [Test]
        public async Task Korean_ShouldInitializeSuccessfully()
        {
            Assert.IsTrue(phonemizer.IsInitialized);
            Assert.Contains("ko", phonemizer.SupportedLanguages);
            Assert.Contains("ko-KR", phonemizer.SupportedLanguages);
        }
        
        [Test]
        public async Task Korean_ShouldPhonemizeBasicSyllables()
        {
            var tests = new Dictionary<string, int>
            {
                ["가"] = 2,  // g + a
                ["나"] = 2,  // n + a
                ["다"] = 2,  // d + a
                ["라"] = 2,  // r + a
                ["마"] = 2,  // m + a
                ["바"] = 2,  // b + a
                ["사"] = 2,  // s + a
            };
            
            foreach (var (syllable, expectedCount) in tests)
            {
                var result = await phonemizer.PhonemizeAsync(syllable, "ko-KR");
                
                Assert.IsNotNull(result);
                Assert.IsNotEmpty(result.Phonemes);
                
                var phonemes = result.Phonemes.Where(p => p != "_").ToList();
                Debug.Log($"'{syllable}' -> [{string.Join(" ", phonemes)}]");
                
                Assert.GreaterOrEqual(phonemes.Count, expectedCount);
            }
        }
        
        [Test]
        public async Task Korean_ShouldHandleCommonWords()
        {
            var words = new[]
            {
                "안녕",      // Hello
                "감사",      // Thanks
                "사랑",      // Love
                "한국",      // Korea
                "학교"       // School
            };
            
            foreach (var word in words)
            {
                var result = await phonemizer.PhonemizeAsync(word, "ko-KR");
                
                Assert.IsNotNull(result);
                Assert.IsNotEmpty(result.Phonemes);
                Assert.IsTrue(result.Success);
                
                Debug.Log($"'{word}' -> {string.Join(" ", result.Phonemes)}");
            }
        }
        
        #endregion
        
        #region Hangul Processor Tests
        
        [Test]
        public void HangulProcessor_ShouldDecomposeCorrectly()
        {
            var tests = new Dictionary<char, HangulJamo>
            {
                ['가'] = new HangulJamo { Initial = "g", Medial = "a", Final = "", HasFinal = false },
                ['각'] = new HangulJamo { Initial = "g", Medial = "a", Final = "g", HasFinal = true },
                ['한'] = new HangulJamo { Initial = "h", Medial = "a", Final = "n", HasFinal = true },
                ['글'] = new HangulJamo { Initial = "g", Medial = "eu", Final = "l", HasFinal = true },
                ['왜'] = new HangulJamo { Initial = "", Medial = "wae", Final = "", HasFinal = false }
            };
            
            foreach (var (syllable, expected) in tests)
            {
                var result = hangulProcessor.DecomposeHangul(syllable);
                
                Assert.AreEqual(expected.Initial, result.Initial);
                Assert.AreEqual(expected.Medial, result.Medial);
                Assert.AreEqual(expected.Final, result.Final);
                Assert.AreEqual(expected.HasFinal, result.HasFinal);
                
                Debug.Log($"'{syllable}' -> {result}");
            }
        }
        
        [Test]
        public void HangulProcessor_ShouldIdentifyHangul()
        {
            Assert.IsTrue(hangulProcessor.IsHangul('가'));
            Assert.IsTrue(hangulProcessor.IsHangul('힣'));
            Assert.IsFalse(hangulProcessor.IsHangul('A'));
            Assert.IsFalse(hangulProcessor.IsHangul('1'));
            Assert.IsFalse(hangulProcessor.IsHangul('。'));
        }
        
        #endregion
        
        #region G2P Engine Tests
        
        [Test]
        public void G2P_ShouldHandleInitialConsonants()
        {
            var jamo = new HangulJamo { Initial = "g", Medial = "a", Final = "", HasFinal = false };
            var phonemes = g2pEngine.JamoToPhonemes(jamo, null, null, true);
            
            // Word-initial ㄱ should be [k]
            Assert.Contains("k", phonemes);
            Assert.Contains("a", phonemes);
        }
        
        [Test]
        public void G2P_ShouldHandleFinalConsonants()
        {
            var jamo = new HangulJamo { Initial = "g", Medial = "a", Final = "g", HasFinal = true };
            var phonemes = g2pEngine.JamoToPhonemes(jamo, null, null, false);
            
            // Final ㄱ should be unreleased [k̚]
            Assert.IsTrue(phonemes.Any(p => p.Contains("k")));
        }
        
        [Test]
        public void G2P_ShouldHandleLiaison()
        {
            // Test 연음 (liaison) - "한국어" where ㄱ moves to next syllable
            var jamo1 = new HangulJamo { Initial = "g", Medial = "u", Final = "g", HasFinal = true };
            var jamo2 = new HangulJamo { Initial = "", Medial = "eo", Final = "", HasFinal = false };
            
            // Simulate "국어" where ㄱ should move to ㅇ position
            var phonemes = g2pEngine.JamoToPhonemes(jamo1, null, '어', false);
            
            Assert.IsNotEmpty(phonemes);
            Debug.Log($"Liaison test: {string.Join(" ", phonemes)}");
        }
        
        #endregion
        
        #region Text Normalizer Tests
        
        [Test]
        public void Normalizer_ShouldConvertNumbers()
        {
            var tests = new Dictionary<string, string>
            {
                ["1개"] = "일개",
                ["10명"] = "십명",
                ["100원"] = "백원",
                ["2023년"] = "이천 이십 삼년",
                ["5만원"] = "오만원"
            };
            
            foreach (var (input, expected) in tests)
            {
                var result = normalizer.Normalize(input);
                Assert.AreEqual(expected, result,
                    $"Failed to normalize '{input}'");
            }
        }
        
        [Test]
        public void Normalizer_ShouldExpandAbbreviations()
        {
            var tests = new Dictionary<string, string>
            {
                ["OK입니다"] = "오케이입니다",
                ["PC방"] = "피씨방",
                ["5G 네트워크"] = "오지 네트워크",
                ["AI 기술"] = "에이아이 기술"
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
        public async Task Korean_ShouldPhonemizeSentences()
        {
            var sentences = new[]
            {
                "안녕하세요.",
                "오늘 날씨가 좋네요.",
                "한국어를 공부합니다.",
                "감사합니다!",
                "내일 만나요."
            };
            
            foreach (var sentence in sentences)
            {
                var result = await phonemizer.PhonemizeAsync(sentence, "ko-KR");
                
                Assert.IsNotNull(result);
                Assert.IsNotEmpty(result.Phonemes);
                Assert.Greater(result.Phonemes.Count, 5, 
                    $"Too few phonemes for sentence: {sentence}");
                
                Debug.Log($"Sentence: {sentence}");
                Debug.Log($"Phonemes ({result.Phonemes.Count}): {string.Join(" ", result.Phonemes)}");
            }
        }
        
        [Test]
        public async Task Korean_ShouldHandleMixedKoreanEnglish()
        {
            var text = "나는 iPhone을 좋아해요";
            var result = await phonemizer.PhonemizeAsync(text, "ko-KR");
            
            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result.Phonemes);
            
            // Should contain both Korean phonemes and English letters
            var phonemeStr = string.Join(" ", result.Phonemes);
            Debug.Log($"Mixed text phonemes: {phonemeStr}");
        }
        
        [Test]
        public async Task Korean_ShouldProvideTimingInfo()
        {
            var text = "안녕하세요";
            var result = await phonemizer.PhonemizeAsync(text, "ko-KR");
            
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
        public async Task Korean_PerformanceBenchmark()
        {
            var testText = "이것은 한국어 음성 합성 시스템의 성능을 테스트하는 문장입니다.";
            
            // Warm up
            await phonemizer.PhonemizeAsync("테스트", "ko-KR");
            
            // Measure
            var sw = System.Diagnostics.Stopwatch.StartNew();
            const int iterations = 100;
            
            for (int i = 0; i < iterations; i++)
            {
                await phonemizer.PhonemizeAsync(testText, "ko-KR");
            }
            
            sw.Stop();
            double avgMs = sw.ElapsedMilliseconds / (double)iterations;
            
            Debug.Log($"Korean phonemization average time: {avgMs:F2} ms");
            Assert.Less(avgMs, 10, "Should process text in under 10ms average");
        }
        
        #endregion
        
        #region Edge Cases
        
        [Test]
        public async Task Korean_ShouldHandleConsonantClusters()
        {
            // Words with complex final consonants
            var words = new[]
            {
                "닭",   // lg
                "삶",   // lm
                "읊다", // lp
                "값"    // bs
            };
            
            foreach (var word in words)
            {
                var result = await phonemizer.PhonemizeAsync(word, "ko-KR");
                
                Assert.IsNotNull(result);
                Assert.IsNotEmpty(result.Phonemes);
                
                Debug.Log($"Complex final: '{word}' -> {string.Join(" ", result.Phonemes)}");
            }
        }
        
        [Test]
        public async Task Korean_ShouldHandleEmptyText()
        {
            var result = await phonemizer.PhonemizeAsync("", "ko-KR");
            
            Assert.IsNotNull(result);
            Assert.IsEmpty(result.Phonemes);
        }
        
        [Test]
        public async Task Korean_ShouldHandlePunctuationOnly()
        {
            var result = await phonemizer.PhonemizeAsync("。、！？", "ko-KR");
            
            Assert.IsNotNull(result);
            // Should have pauses for punctuation
            Assert.IsNotEmpty(result.Phonemes);
            Assert.IsTrue(result.Phonemes.All(p => p == "_"));
        }
        
        #endregion
    }
}