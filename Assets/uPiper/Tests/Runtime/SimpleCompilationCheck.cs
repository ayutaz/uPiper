using UnityEngine;

namespace uPiper.Tests.Runtime
{
    public class SimpleCompilationCheck : MonoBehaviour
    {
        void Start()
        {
            // Test namespace resolution
            var baseType = typeof(uPiper.Core.Phonemizers.Backend.PhonemizerBackendBase);
            Debug.Log($"PhonemizerBackendBase type found: {baseType.FullName}");
            
            // Test phonemizer implementations
            var chinesePhonemizer = typeof(uPiper.Core.Phonemizers.Backend.ChinesePhonemizer);
            Debug.Log($"ChinesePhonemizer type found: {chinesePhonemizer.FullName}");
            
            var koreanPhonemizer = typeof(uPiper.Core.Phonemizers.Backend.KoreanPhonemizer);
            Debug.Log($"KoreanPhonemizer type found: {koreanPhonemizer.FullName}");
            
            var spanishPhonemizer = typeof(uPiper.Core.Phonemizers.Backend.SpanishPhonemizer);
            Debug.Log($"SpanishPhonemizer type found: {spanishPhonemizer.FullName}");
        }
    }
}