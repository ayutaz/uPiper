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
            
            // Test Chinese components
            var chineseNormalizer = typeof(uPiper.Core.Phonemizers.Backend.Chinese.ChineseTextNormalizer);
            Debug.Log($"ChineseTextNormalizer type found: {chineseNormalizer.FullName}");
            
            var chineseSegmenter = typeof(uPiper.Core.Phonemizers.Backend.Chinese.ChineseTextSegmenter);
            Debug.Log($"ChineseTextSegmenter type found: {chineseSegmenter.FullName}");
            
            var pinyinMapper = typeof(uPiper.Core.Phonemizers.Backend.Chinese.PinyinToPhonemeMapper);
            Debug.Log($"PinyinToPhonemeMapper type found: {pinyinMapper.FullName}");
        }
    }
}