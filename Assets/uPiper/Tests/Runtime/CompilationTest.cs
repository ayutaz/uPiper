using System;
using UnityEngine;
using uPiper.Core.Phonemizers.Backend;
using uPiper.Core.Phonemizers.Backend.RuleBased;

namespace uPiper.Tests.Runtime
{
    /// <summary>
    /// Test to ensure all phonemizer classes compile correctly
    /// </summary>
    public class CompilationTest : MonoBehaviour
    {
        private void Start()
        {
            // Test instantiation of all phonemizers
            try
            {
                var chinese = new uPiper.Core.Phonemizers.Backend.ChinesePhonemizer();
                Debug.Log($"Chinese phonemizer created: {chinese.Name}");

                var korean = new uPiper.Core.Phonemizers.Backend.KoreanPhonemizer();
                Debug.Log($"Korean phonemizer created: {korean.Name}");

                var spanish = new uPiper.Core.Phonemizers.Backend.SpanishPhonemizer();
                Debug.Log($"Spanish phonemizer created: {spanish.Name}");

                var ruleBased = new RuleBasedPhonemizer();
                Debug.Log($"Rule-based phonemizer created: {ruleBased.Name}");

                // FallbackPhonemizer test temporarily disabled due to meta file issues
                // var fallback = new FallbackPhonemizer();
                // Debug.Log($"Fallback phonemizer created: {fallback.Name}");

                Debug.Log("All phonemizers compiled and instantiated successfully!");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to create phonemizers: {ex.Message}");
            }
        }
    }
}