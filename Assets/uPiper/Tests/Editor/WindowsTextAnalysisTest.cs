#if UNITY_EDITOR && UNITY_EDITOR_WIN
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using uPiper.Core;
using uPiper.Core.Logging;
using uPiper.Core.Phonemizers.Implementations;

namespace uPiper.Tests.Editor
{
    /// <summary>
    /// Windows環境での文章解析問題をテスト
    /// </summary>
    public class WindowsTextAnalysisTest
    {
        private OpenJTalkPhonemizer _phonemizer;

        [SetUp]
        public void Setup()
        {
            PiperLogger.SetMinimumLevel(PiperLogger.LogLevel.Debug);
        }

        [TearDown]
        public void TearDown()
        {
            _phonemizer?.Dispose();
        }

        [Test]
        public void TestProblematicSentences()
        {
            try
            {
                _phonemizer = new OpenJTalkPhonemizer();
                
                var testCases = new[]
                {
                    // 問題のある文
                    ("今日はいい天気ですね", "きょうはいいてんきですね"),
                    
                    // 比較用の文
                    ("今日は", "きょうは"),
                    ("いい", "いい"),
                    ("天気", "てんき"),
                    ("ですね", "ですね"),
                    ("いい天気", "いいてんき"),
                    ("天気ですね", "てんきですね"),
                    
                    // 別の文章パターン
                    ("今日はいい天気です", "きょうはいいてんきです"),
                    ("今日は天気がいいですね", "きょうはてんきがいいですね"),
                    ("いい天気ですね", "いいてんきですね")
                };
                
                foreach (var (text, reading) in testCases)
                {
                    Debug.Log($"\n=== Analyzing: {text} ({reading}) ===");
                    var result = _phonemizer.Phonemize(text, "ja");
                    
                    Debug.Log($"Input text: '{text}'");
                    Debug.Log($"Expected reading: {reading}");
                    Debug.Log($"Phoneme count: {result.Phonemes.Length}");
                    Debug.Log($"Phonemes: {string.Join(" ", result.Phonemes)}");
                    
                    // 期待されるフォネームの大まかな数をチェック
                    // 日本語の場合、通常1文字あたり1-2フォネーム
                    var minExpectedPhonemes = text.Length;
                    var maxExpectedPhonemes = text.Length * 3;
                    
                    Assert.GreaterOrEqual(result.Phonemes.Length, minExpectedPhonemes, 
                        $"Too few phonemes for '{text}'");
                    Assert.LessOrEqual(result.Phonemes.Length, maxExpectedPhonemes, 
                        $"Too many phonemes for '{text}'");
                    
                    // 特定のパターンチェック
                    var phonemeString = string.Join(" ", result.Phonemes);
                    
                    // 「今日」のチェック
                    if (text.StartsWith("今日"))
                    {
                        // "k y o:"または類似のパターンを期待
                        Assert.IsTrue(
                            phonemeString.Contains("k y o") || 
                            phonemeString.Contains("k i y o"),
                            $"'今日' not properly phonemized in '{text}'. Got: {phonemeString}"
                        );
                    }
                    
                    // 「天気」のチェック
                    if (text.Contains("天気"))
                    {
                        // "t e n k i"または類似のパターンを期待
                        Assert.IsTrue(
                            phonemeString.Contains("t e n k i") || 
                            phonemeString.Contains("t e N k i"),
                            $"'天気' not properly phonemized in '{text}'. Got: {phonemeString}"
                        );
                    }
                }
            }
            catch (PiperInitializationException)
            {
                Assert.Inconclusive("Native library not available");
            }
        }

        [Test]
        public void AnalyzeCharacterByCharacter()
        {
            try
            {
                _phonemizer = new OpenJTalkPhonemizer();
                
                var text = "今日はいい天気ですね";
                Debug.Log($"\n=== Character-by-character analysis of: {text} ===");
                
                // 各文字を個別に解析
                for (int i = 0; i < text.Length; i++)
                {
                    var singleChar = text[i].ToString();
                    var result = _phonemizer.Phonemize(singleChar, "ja");
                    Debug.Log($"'{singleChar}' -> {string.Join(" ", result.Phonemes)}");
                }
                
                // 段階的に文を構築して解析
                Debug.Log("\n=== Progressive analysis ===");
                for (int i = 1; i <= text.Length; i++)
                {
                    var partial = text.Substring(0, i);
                    var result = _phonemizer.Phonemize(partial, "ja");
                    Debug.Log($"'{partial}' -> {string.Join(" ", result.Phonemes)} ({result.Phonemes.Length} phonemes)");
                }
            }
            catch (PiperInitializationException)
            {
                Assert.Inconclusive("Native library not available");
            }
        }
    }
}
#endif