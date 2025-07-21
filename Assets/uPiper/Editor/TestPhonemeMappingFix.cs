using UnityEditor;
using UnityEngine;
using uPiper.Core.Phonemizers;
using System.Linq;

namespace uPiper.Editor
{
    public static class TestPhonemeMappingFix
    {
        [MenuItem("uPiper/Debug/Test Phoneme Mapping Fix")]
        public static void TestMapping()
        {
            Debug.Log("=== Testing phoneme mapping fix ===");
            
            // Test the mapping directly
            var testPhonemes = new[] { "pau", "ky", "o", "o", "pau" };
            Debug.Log($"Input phonemes: {string.Join(" ", testPhonemes)}");
            
            var mapped = OpenJTalkToPiperMapping.ConvertToPiperPhonemes(testPhonemes);
            Debug.Log($"Mapped phonemes: {string.Join(" ", mapped)}");
            
            // Check if ky was converted to PUA
            bool hasKyPUA = mapped.Any(p => p == "\ue006");
            Debug.Log($"Contains ky PUA character (\\ue006): {hasKyPUA}");
            
            // Display each phoneme
            for (int i = 0; i < mapped.Length; i++)
            {
                var phoneme = mapped[i];
                if (phoneme.Length == 1 && phoneme[0] >= '\ue000' && phoneme[0] <= '\uf8ff')
                {
                    Debug.Log($"  [{i}] PUA character U+{((int)phoneme[0]):X4}");
                }
                else
                {
                    Debug.Log($"  [{i}] '{phoneme}'");
                }
            }
        }
    }
}