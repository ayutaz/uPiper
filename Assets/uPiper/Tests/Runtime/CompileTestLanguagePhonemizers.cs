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
        private void Start()
        {
            try
            {
                // Test that base class is accessible
                var baseType = typeof(PhonemizerBackendBase);
                Debug.Log($"PhonemizerBackendBase found: {baseType.FullName}");

                // Test proxy classes
                var chinese = new uPiper.Core.Phonemizers.Backend.ChinesePhonemizer();
                Debug.Log($"Chinese proxy created: {chinese.Name}");

                var korean = new uPiper.Core.Phonemizers.Backend.KoreanPhonemizer();
                Debug.Log($"Korean proxy created: {korean.Name}");

                var spanish = new uPiper.Core.Phonemizers.Backend.SpanishPhonemizer();
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