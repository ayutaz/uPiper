using UnityEditor;
using UnityEngine;
using uPiper.Core.Phonemizers.Implementations;
using uPiper.Core.Logging;
using System.Text;
using System.Linq;

namespace uPiper.Editor
{
    public static class TestKyouPhonemization
    {
        [MenuItem("uPiper/Debug/Test '今日' Phonemization")]
        public static async void TestKyou()
        {
            PiperLogger.LogInfo("=== Testing '今日' phonemization on Windows ===");
            
            var phonemizer = new OpenJTalkPhonemizer();
            
            // テストケース
            var testCases = new[]
            {
                ("今日", "きょう"),
                ("きょう", "きょう"),
                ("キョウ", "きょう"),
                ("今日は", "きょうは"),
                ("今日はいい天気", "きょうはいいてんき")
            };
            
            foreach (var (text, reading) in testCases)
            {
                PiperLogger.LogInfo($"\nTesting: {text} (expected: {reading})");
                
                // UTF-8バイトを表示
                var bytes = Encoding.UTF8.GetBytes(text);
                var hexString = string.Join(" ", bytes.Select(b => b.ToString("X2")));
                PiperLogger.LogInfo($"UTF-8 bytes: {hexString}");
                
                // 音素化
                var result = await phonemizer.PhonemizeAsync(text);
                var phonemeString = string.Join(" ", result.Phonemes);
                
                PiperLogger.LogInfo($"Phonemes: {phonemeString}");
                PiperLogger.LogInfo($"Phoneme count: {result.Phonemes.Length}");
                
                // 期待される音素パターンのチェック
                bool hasKyo = phonemeString.Contains("k y o") || 
                             phonemeString.Contains("k i y o") ||
                             phonemeString.Contains("ky o");
                
                if (!hasKyo && text.Contains("今日"))
                {
                    PiperLogger.LogError($"'今日' not found in phonemes!");
                    
                    // 詳細分析
                    PiperLogger.LogInfo("Detailed phoneme analysis:");
                    for (int i = 0; i < result.Phonemes.Length; i++)
                    {
                        var phoneme = result.Phonemes[i];
                        PiperLogger.LogInfo($"  [{i}] '{phoneme}' (length: {phoneme.Length})");
                    }
                }
                else
                {
                    PiperLogger.LogInfo("✓ Phonemization successful");
                }
            }
            
            phonemizer.Dispose();
            PiperLogger.LogInfo("\n=== Test completed ===");
        }
        
        [MenuItem("uPiper/Debug/Test Simple Phonemization")]
        public static async void TestSimple()
        {
            PiperLogger.LogInfo("=== Testing simple phonemization ===");
            
            var phonemizer = new OpenJTalkPhonemizer();
            
            // シンプルなテスト
            var simpleTests = new[] { "あ", "か", "さ", "た", "な", "は", "ま", "や", "ら", "わ" };
            
            foreach (var text in simpleTests)
            {
                var result = await phonemizer.PhonemizeAsync(text);
                var phonemeString = string.Join(" ", result.Phonemes);
                PiperLogger.LogInfo($"{text} -> {phonemeString}");
            }
            
            phonemizer.Dispose();
        }
    }
}