using UnityEngine;
using UnityEditor;
using System.Text;
using System.Linq;

namespace uPiper.Editor
{
    public static class TestTextEncoding
    {
        [MenuItem("uPiper/Debug/Test Text Encoding")]
        public static void TestEncodingIssues()
        {
            Debug.Log("[TestTextEncoding] Starting text encoding test...");
            
            var testTexts = new[]
            {
                "こんにちは",
                "今日はいい天気ですね",
                "Hello World",
                "123ABC",
                "漢字カナ混じり文"
            };
            
            foreach (var text in testTexts)
            {
                Debug.Log($"\n[TestTextEncoding] Testing: '{text}'");
                
                // Show byte representation
                var utf8Bytes = Encoding.UTF8.GetBytes(text);
                Debug.Log($"  UTF-8 bytes ({utf8Bytes.Length}): {string.Join(" ", utf8Bytes.Select(b => b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture)))}");
                
                // Show Unicode code points
                Debug.Log($"  Unicode code points: {string.Join(" ", text.Select(c => $"U+{((int)c).ToString("X4", System.Globalization.CultureInfo.InvariantCulture)}"))}");
                
                // Check for any special characters
                var hasKanji = text.Any(c => c >= 0x4E00 && c <= 0x9FFF);
                var hasHiragana = text.Any(c => c >= 0x3040 && c <= 0x309F);
                var hasKatakana = text.Any(c => c >= 0x30A0 && c <= 0x30FF);
                
                Debug.Log($"  Contains - Kanji: {hasKanji}, Hiragana: {hasHiragana}, Katakana: {hasKatakana}");
                
                // Test round-trip encoding
                var bytes = Encoding.UTF8.GetBytes(text);
                var roundTrip = Encoding.UTF8.GetString(bytes);
                var isIdentical = text == roundTrip;
                Debug.Log($"  UTF-8 round-trip test: {(isIdentical ? "PASS" : "FAIL")}");
                
                if (!isIdentical)
                {
                    Debug.LogError($"    Original: '{text}'");
                    Debug.LogError($"    Round-trip: '{roundTrip}'");
                }
            }
            
            Debug.Log("\n[TestTextEncoding] Test completed.");
        }
    }
}