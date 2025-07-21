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
                
                // PUA文字を含む音素を正しく表示するための処理
                var phonemeDisplay = result.Phonemes.Select(p => {
                    if (p.Length == 1 && p[0] >= '\ue000' && p[0] <= '\uf8ff')
                    {
                        return $"PUA(U+{((int)p[0]):X4})";
                    }
                    return p;
                }).ToArray();
                
                var phonemeString = string.Join(" ", phonemeDisplay);
                
                PiperLogger.LogInfo($"Phonemes: {phonemeString}");
                PiperLogger.LogInfo($"Phoneme count: {result.Phonemes.Length}");
                
                // 期待される音素パターンのチェック - PUA文字も考慮
                bool hasKyo = false;
                if (text.Contains("今日"))
                {
                    // "ky"がPUA文字U+E006に変換されているか確認
                    for (int i = 0; i < result.Phonemes.Length - 1; i++)
                    {
                        var phoneme = result.Phonemes[i];
                        if (phoneme.Length == 1 && phoneme[0] == '\ue006') // "ky"のPUA文字
                        {
                            if (i + 1 < result.Phonemes.Length && result.Phonemes[i + 1] == "o")
                            {
                                hasKyo = true;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    hasKyo = true; // 「今日」を含まないテキストは常にOK
                }
                
                if (!hasKyo && text.Contains("今日"))
                {
                    PiperLogger.LogError($"'今日' not found in phonemes!");
                    
                    // 詳細分析
                    PiperLogger.LogInfo("Detailed phoneme analysis:");
                    for (int i = 0; i < result.Phonemes.Length; i++)
                    {
                        var phoneme = result.Phonemes[i];
                        if (phoneme.Length == 1 && phoneme[0] >= '\ue000' && phoneme[0] <= '\uf8ff')
                        {
                            PiperLogger.LogInfo($"  [{i}] PUA character U+{((int)phoneme[0]):X4}");
                        }
                        else
                        {
                            PiperLogger.LogInfo($"  [{i}] '{phoneme}' (length: {phoneme.Length})");
                        }
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