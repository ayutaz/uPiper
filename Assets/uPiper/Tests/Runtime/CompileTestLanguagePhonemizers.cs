using System;
using UnityEngine;
using uPiper.Core.Phonemizers.Backend;

namespace uPiper.Tests.Runtime
{
    /// <summary>
    /// Test to verify language phonemizers compile correctly
    /// </summary>
    public class CompileTestLanguagePhonemizers : MonoBehaviour
    {
        void Start()
        {
            try
            {
                // Test that base class is accessible
                Type baseType = typeof(PhonemizerBackendBase);
                Debug.Log($"PhonemizerBackendBase found: {baseType.FullName}");
                
                // Test proxy classes
                var chinese = new ChinesePhonemizerProxy();
                Debug.Log($"Chinese proxy created: {chinese.Name}");
                
                var korean = new KoreanPhonemizerProxy();
                Debug.Log($"Korean proxy created: {korean.Name}");
                
                var spanish = new SpanishPhonemizerProxy();
                Debug.Log($"Spanish proxy created: {spanish.Name}");
                
                Debug.Log("All language phonemizers compiled successfully!");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Compilation test failed: {ex.Message}");
            }
        }
    }
}