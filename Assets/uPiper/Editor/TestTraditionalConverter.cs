using UnityEngine;
using UnityEditor;
using uPiper.Core.Phonemizers.Backend.Chinese;

namespace uPiper.Editor
{
    public static class TestTraditionalConverter
    {
        [MenuItem("uPiper/Tests/Test Traditional Converter")]
        public static void TestConverter()
        {
            var converter = new TraditionalChineseConverter();
            
            // Test the mixed text case that was freezing
            var testCases = new[]
            {
                ("Hello 世界！", "Hello 世界！"),
                ("學習English", "学习English"),
                ("我love臺灣", "我love台湾"),
                ("123書本456", "123书本456"),
                ("ABC語言XYZ", "ABC语言XYZ"),
            };
            
            Debug.Log("[TestConverter] Starting mixed text conversion tests...");
            
            foreach (var (input, expected) in testCases)
            {
                var result = converter.ConvertToSimplified(input);
                var passed = result == expected;
                Debug.Log($"[TestConverter] '{input}' → '{result}' | Expected: '{expected}' | {(passed ? "PASS" : "FAIL")}");
            }
            
            Debug.Log("[TestConverter] Tests completed!");
        }
    }
}