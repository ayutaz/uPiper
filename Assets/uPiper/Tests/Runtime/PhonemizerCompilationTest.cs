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
    public class PhonemizerCompilationTest : MonoBehaviour
    {
        void Start()
        {
            try
            {
                // Test proxy classes
                var chinese = new ChinesePhonemizerProxy();
                Debug.Log($"Chinese proxy phonemizer created: {chinese.Name}");
                
                var korean = new KoreanPhonemizerProxy();
                Debug.Log($"Korean proxy phonemizer created: {korean.Name}");
                
                var spanish = new SpanishPhonemizerProxy();
                Debug.Log($"Spanish proxy phonemizer created: {spanish.Name}");
                
                // Test other phonemizers
                var ruleBased = new RuleBasedPhonemizer();
                Debug.Log($"Rule-based phonemizer created: {ruleBased.Name}");
                
                var fallback = new FallbackPhonemizer();
                Debug.Log($"Fallback phonemizer created: {fallback.Name}");
                
                // Test namespace access to component classes
                var chineseNormalizer = new uPiper.Core.Phonemizers.Backend.Chinese.ChineseTextNormalizer();
                Debug.Log("Chinese text normalizer created successfully");
                
                var koreanProcessor = new uPiper.Core.Phonemizers.Backend.Korean.HangulProcessor();
                Debug.Log("Korean Hangul processor created successfully");
                
                var spanishG2P = new uPiper.Core.Phonemizers.Backend.Spanish.SpanishG2P();
                Debug.Log("Spanish G2P engine created successfully");
                
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