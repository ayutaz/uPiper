using System;
using UnityEngine;
using uPiper.Core.Phonemizers.Backend;
using uPiper.Core.Phonemizers.Backend.RuleBased;

namespace uPiper.Tests.Runtime
{
    /// <summary>
    /// Test to ensure all phonemizer classes compile correctly
    /// </summary>
    public class PhonemizerCompilationTest : MonoBehaviour
    {
        void Start()
        {
            try
            {
                // Test phonemizer implementations
                var chinese = new ChinesePhonemizer();
                Debug.Log($"Chinese phonemizer created: {chinese.Name}");
                
                var korean = new KoreanPhonemizer();
                Debug.Log($"Korean phonemizer created: {korean.Name}");
                
                var spanish = new SpanishPhonemizer();
                Debug.Log($"Spanish phonemizer created: {spanish.Name}");
                
                // Test other phonemizers
                var ruleBased = new RuleBasedPhonemizer();
                Debug.Log($"Rule-based phonemizer created: {ruleBased.Name}");
                
                var fallback = new FallbackPhonemizer();
                Debug.Log($"Fallback phonemizer created: {fallback.Name}");
                
                // Test that we can access PhonemeResult
                var result = new PhonemeResult();
                result.OriginalText = "test";
                result.Phonemes = new string[] { "t", "e", "s", "t" };
                Debug.Log($"PhonemeResult created with text: {result.OriginalText}");
                
                Debug.Log("All phonemizers compiled and instantiated successfully!");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to create phonemizers: {ex.Message}");
                Debug.LogError($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}