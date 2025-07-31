using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using uPiper.Core.Phonemizers;
using uPiper.Core.Phonemizers.Implementations;

namespace uPiper.Editor
{
    public static class OpenJTalkStatus
    {
        [MenuItem("uPiper/Debug/OpenJTalk/Show Status")]
        public static void ShowStatus()
        {
            Debug.Log("=== OpenJTalk Status Report ===");

            // Try to create phonemizer to check status
            OpenJTalkPhonemizer phonemizer = null;

            try
            {
                phonemizer = new OpenJTalkPhonemizer();
                Debug.Log($"Version: {phonemizer.Version}");
                Debug.Log($"Mode: Native Mode");
                Debug.Log($"Supported Languages: {string.Join(", ", phonemizer.SupportedLanguages)}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to initialize OpenJTalk: {ex.Message}");
                Debug.LogError("\nTo install OpenJTalk native library:");
                Debug.LogError("1. Navigate to: " + System.IO.Path.Combine(Application.dataPath, "../NativePlugins/OpenJTalk/"));
                Debug.LogError("2. Run build script:");
                Debug.LogError("   - macOS/Linux: ./build.sh");
                Debug.LogError("   - Windows: build.bat");
                Debug.LogError("3. Restart Unity Editor");
                return;
            }

            // Test various Japanese texts
            var testCases = new Dictionary<string, string>
            {
                { "こんにちは", "Simple hiragana (should work)" },
                { "今日はいい天気ですね", "Kanji + hiragana (currently broken)" },
                { "ありがとう", "Hiragana only" },
                { "東京", "Kanji only" },
                { "私は学生です", "Mixed kanji/hiragana" }
            };

            Debug.Log("\n=== Test Results ===");
            var workingCount = 0;
            var brokenCount = 0;

            foreach (var testCase in testCases)
            {
                try
                {
                    var result = phonemizer.PhonemizeAsync(testCase.Key, "ja").Result;
                    if (result != null && result.Phonemes != null)
                    {
                        var phonemes = result.Phonemes;

                        // Check for repeating patterns
                        var hasRepeatingPattern = phonemes
                            .GroupBy(p => p)
                            .Any(g => g.Count() > 3);

                        if (hasRepeatingPattern)
                        {
                            Debug.LogError($"✗ '{testCase.Key}' - BROKEN: Repeating pattern detected");
                            Debug.LogError($"  Output: {string.Join(" ", phonemes)}");
                            brokenCount++;
                        }
                        else if (phonemes.Length < 3 && testCase.Key.Length > 2)
                        {
                            Debug.LogWarning($"? '{testCase.Key}' - SUSPICIOUS: Too few phonemes");
                            Debug.LogWarning($"  Output: {string.Join(" ", phonemes)}");
                            brokenCount++;
                        }
                        else
                        {
                            Debug.Log($"✓ '{testCase.Key}' - OK");
                            Debug.Log($"  Output: {string.Join(" ", phonemes)}");
                            workingCount++;
                        }
                    }
                    else
                    {
                        Debug.LogError($"✗ '{testCase.Key}' - NO RESULT");
                        brokenCount++;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"✗ '{testCase.Key}' - EXCEPTION: {e.Message}");
                    brokenCount++;
                }
            }

            Debug.Log($"\n=== Summary ===");
            Debug.Log($"Working: {workingCount}/{testCases.Count}");
            Debug.Log($"Broken: {brokenCount}/{testCases.Count}");

            if (brokenCount > 0)
            {
                Debug.LogError("\n⚠️ OpenJTalk native library has issues with kanji processing!");
                Debug.LogError("The native library is returning incorrect phoneme sequences for text containing kanji.");
                Debug.LogError("This is a known issue in the current OpenJTalk wrapper implementation.");
            }

            phonemizer?.Dispose();
            Debug.Log("\n=== End of Status Report ===");
        }
    }
}