using UnityEngine;
using UnityEditor;
using uPiper.Core.Phonemizers.Implementations;
using uPiper.Core.Phonemizers;
using System.Linq;

namespace uPiper.Editor
{
    public static class TestOpenJTalkKanji
    {
        [MenuItem("uPiper/Debug/Test OpenJTalk Kanji Parsing")]
        public static void TestKanjiParsing()
        {
            Debug.Log("[TestOpenJTalkKanji] Starting kanji parsing test...");
            
            // Test different Japanese texts
            var testTexts = new[]
            {
                ("こんにちは", "Simple hiragana"),
                ("今日", "Kanji only"),
                ("今日は", "Kanji + hiragana"),
                ("いい天気", "Hiragana + kanji"),
                ("天気です", "Kanji + hiragana"),
                ("今日はいい天気ですね", "Full sentence with kanji"),
                ("私は学生です", "Another kanji sentence"),
                ("元気ですか", "Mixed text"),
                ("ありがとう", "Hiragana only"),
                ("東京", "Place name kanji")
            };
            
            // Create phonemizer
            var phonemizer = new OpenJTalkPhonemizer();
            
            Debug.Log($"[TestOpenJTalkKanji] OpenJTalk version: {phonemizer.Version}");
            Debug.Log($"[TestOpenJTalkKanji] Mock mode: {OpenJTalkPhonemizer.MockMode}");
            
            foreach (var (text, description) in testTexts)
            {
                Debug.Log($"\n[TestOpenJTalkKanji] Testing: '{text}' ({description})");
                
                try
                {
                    // Get phonemes synchronously
                    var result = phonemizer.PhonemizeAsync(text, "ja").Result;
                    
                    if (result != null && result.Phonemes != null)
                    {
                        Debug.Log($"[TestOpenJTalkKanji] Raw phonemes ({result.Phonemes.Length}): {string.Join(" ", result.Phonemes)}");
                        
                        // Convert to Piper phonemes
                        var piperPhonemes = OpenJTalkToPiperMapping.ConvertToPiperPhonemes(result.Phonemes);
                        Debug.Log($"[TestOpenJTalkKanji] Piper phonemes ({piperPhonemes.Length}): {string.Join(" ", piperPhonemes)}");
                        
                        // Check for suspicious patterns
                        if (result.Phonemes.Length > 5)
                        {
                            // Check for repeating patterns
                            var groups = result.Phonemes
                                .Select((p, i) => new { Phoneme = p, Index = i })
                                .GroupBy(x => x.Phoneme)
                                .Where(g => g.Count() > 2)
                                .ToList();
                            
                            if (groups.Any())
                            {
                                Debug.LogWarning($"[TestOpenJTalkKanji] Suspicious repeating patterns detected:");
                                foreach (var group in groups)
                                {
                                    Debug.LogWarning($"  - '{group.Key}' appears {group.Count()} times");
                                }
                            }
                        }
                        
                        // Validate output makes sense
                        var uniquePhonemes = result.Phonemes.Distinct().Count();
                        var totalPhonemes = result.Phonemes.Length;
                        var repetitionRatio = 1.0f - (float)uniquePhonemes / totalPhonemes;
                        
                        if (repetitionRatio > 0.5f)
                        {
                            Debug.LogError($"[TestOpenJTalkKanji] High repetition ratio ({repetitionRatio:P}) - possible parsing error!");
                        }
                    }
                    else
                    {
                        Debug.LogError($"[TestOpenJTalkKanji] No result returned for '{text}'");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[TestOpenJTalkKanji] Error processing '{text}': {e.Message}");
                }
            }
            
            // Test raw native call if not in mock mode
            if (!OpenJTalkPhonemizer.MockMode)
            {
                Debug.Log("\n[TestOpenJTalkKanji] Testing native library directly...");
                TestNativeLibraryDirectly();
            }
            
            phonemizer.Dispose();
            Debug.Log("\n[TestOpenJTalkKanji] Test completed.");
        }
        
        private static void TestNativeLibraryDirectly()
        {
            // This would require direct P/Invoke access which is internal
            // For now, just log that we're using the native library
            Debug.Log("[TestOpenJTalkKanji] Native library is being used (not mock mode)");
        }
    }
}