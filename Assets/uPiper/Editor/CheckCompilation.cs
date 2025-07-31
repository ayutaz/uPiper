using UnityEditor;
using UnityEngine;

namespace uPiper.Editor
{
    public static class CheckCompilation
    {
        [MenuItem("uPiper/Debug/Check Compilation")]
        public static void CheckIfCompiles()
        {
            Debug.Log("======== Compilation Check ========");

            try
            {
                // Test Backend namespace
                var backendType = typeof(uPiper.Core.Phonemizers.Backend.PhonemizerBackendBase);
                Debug.Log($"✓ PhonemizerBackendBase found: {backendType.FullName}");

                // Test PhonemeResult
                var resultType = typeof(uPiper.Core.Phonemizers.Backend.PhonemeResult);
                Debug.Log($"✓ PhonemeResult found: {resultType.FullName}");

                // Test phonemizer implementations
                var chineseType = typeof(uPiper.Core.Phonemizers.Backend.ChinesePhonemizer);
                Debug.Log($"✓ ChinesePhonemizer found: {chineseType.FullName}");

                var koreanType = typeof(uPiper.Core.Phonemizers.Backend.KoreanPhonemizer);
                Debug.Log($"✓ KoreanPhonemizer found: {koreanType.FullName}");

                var spanishType = typeof(uPiper.Core.Phonemizers.Backend.SpanishPhonemizer);
                Debug.Log($"✓ SpanishPhonemizer found: {spanishType.FullName}");

                // Test instantiation
                var result = new uPiper.Core.Phonemizers.Backend.PhonemeResult
                {
                    OriginalText = "test",
                    Phonemes = new string[] { "t", "e", "s", "t" },
                    PhonemeIds = new int[] { 1, 2, 3, 4 },
                    Language = "en",
                    Success = true,
                    ProcessingTimeMs = 10.5f,
                    FromCache = false,
                    Metadata = new System.Collections.Generic.Dictionary<string, object>()
                };
                Debug.Log($"✓ PhonemeResult instantiated successfully");

                var clone = result.Clone();
                Debug.Log($"✓ PhonemeResult.Clone() works");

                Debug.Log("======== All checks passed! ========");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"✗ Compilation check failed: {ex.Message}");
                Debug.LogError(ex.StackTrace);
            }
        }
    }
}