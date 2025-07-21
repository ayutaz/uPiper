using UnityEngine;
using UnityEditor;
using uPiper.Core.Phonemizers.Implementations;
using System;

namespace uPiper.Editor
{
    public static class TestMeCabOutput
    {
        [MenuItem("uPiper/Debug/Test MeCab Native Output")]
        public static void TestNativeOutput()
        {
            Debug.Log("=== Testing MeCab Native Output ===");
            
            // Set environment variable to enable debug output
            Environment.SetEnvironmentVariable("DEBUG_MECAB", "1");
            Environment.SetEnvironmentVariable("UPIPER_DEBUG", "1");
            
            var phonemizer = new OpenJTalkPhonemizer();
            
            var testTexts = new[]
            {
                "こんにちは",
                "今日",
                "天気",
                "今日はいい天気ですね"
            };
            
            foreach (var text in testTexts)
            {
                Debug.Log($"\n--- Testing: '{text}' ---");
                
                try
                {
                    var result = phonemizer.PhonemizeAsync(text, "ja").Result;
                    
                    if (result != null && result.Phonemes != null)
                    {
                        Debug.Log($"Phoneme count: {result.Phonemes.Length}");
                        Debug.Log($"Phonemes: {string.Join(" ", result.Phonemes)}");
                        
                        // Check first few phonemes for patterns
                        if (result.Phonemes.Length > 10)
                        {
                            Debug.Log("First 10 phonemes:");
                            for (int i = 0; i < 10 && i < result.Phonemes.Length; i++)
                            {
                                Debug.Log($"  [{i}]: '{result.Phonemes[i]}'");
                            }
                        }
                    }
                    else
                    {
                        Debug.LogError("No result returned");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Exception: {e.Message}");
                }
            }
            
            // Clear environment variables
            Environment.SetEnvironmentVariable("DEBUG_MECAB", null);
            Environment.SetEnvironmentVariable("UPIPER_DEBUG", null);
            
            phonemizer.Dispose();
            Debug.Log("\n=== Test Complete ===");
        }
    }
}