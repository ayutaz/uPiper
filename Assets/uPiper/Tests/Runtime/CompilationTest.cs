using System;
using UnityEngine;
using uPiper.Core.Phonemizers.Backend;
using uPiper.Core.Phonemizers.Backend.RuleBased;
using uPiper.Core.Phonemizers.ErrorHandling;

namespace uPiper.Tests.Runtime
{
    /// <summary>
    /// Test to ensure all phonemizer classes compile correctly
    /// </summary>
    public class CompilationTest : MonoBehaviour
    {
        void Start()
        {
            // Test instantiation of all phonemizers
            try
            {
                var chinese = new ChinesePhonemizer();
                Debug.Log($"Chinese phonemizer created: {chinese.Name}");
                
                var korean = new KoreanPhonemizer();
                Debug.Log($"Korean phonemizer created: {korean.Name}");
                
                var spanish = new SpanishPhonemizer();
                Debug.Log($"Spanish phonemizer created: {spanish.Name}");
                
                var ruleBased = new RuleBasedPhonemizer();
                Debug.Log($"Rule-based phonemizer created: {ruleBased.Name}");
                
                var fallback = new FallbackPhonemizer();
                Debug.Log($"Fallback phonemizer created: {fallback.Name}");
                
                Debug.Log("All phonemizers compiled and instantiated successfully!");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to create phonemizers: {ex.Message}");
            }
        }
    }
}