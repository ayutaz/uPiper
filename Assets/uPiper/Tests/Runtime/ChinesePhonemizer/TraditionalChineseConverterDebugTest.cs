using System;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using uPiper.Core.Phonemizers.Backend.Chinese;
using Debug = UnityEngine.Debug;

namespace uPiper.Tests.Runtime.ChinesePhonemizer
{
    /// <summary>
    /// Debug test to identify freeze issue
    /// </summary>
    public class TraditionalChineseConverterDebugTest
    {
        [Test]
        public void DebugTest_ConverterCreation()
        {
            Debug.Log("[DebugTest] Starting converter creation test");
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                var converter = new TraditionalChineseConverter();
                stopwatch.Stop();
                Debug.Log($"[DebugTest] Converter created in {stopwatch.ElapsedMilliseconds}ms");
                
                Assert.IsNotNull(converter);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DebugTest] Exception during creation: {ex}");
                throw;
            }
        }
        
        [Test]
        public void DebugTest_SimpleConversion()
        {
            Debug.Log("[DebugTest] Starting simple conversion test");
            
            try
            {
                var converter = new TraditionalChineseConverter();
                
                // Test single character
                var result1 = converter.ConvertToSimplified("學");
                Debug.Log($"[DebugTest] Single char: '學' → '{result1}'");
                Assert.AreEqual("学", result1);
                
                // Test simple text
                var result2 = converter.ConvertToSimplified("學習");
                Debug.Log($"[DebugTest] Simple text: '學習' → '{result2}'");
                Assert.AreEqual("学习", result2);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DebugTest] Exception during conversion: {ex}");
                throw;
            }
        }
        
        [Test]
        public void DebugTest_ProblematicMixedText()
        {
            Debug.Log("[DebugTest] Starting problematic mixed text test");
            
            try
            {
                var converter = new TraditionalChineseConverter();
                
                // Test the exact same strings that are causing issues
                var testCases = new[]
                {
                    "Hello 世界！",
                    "學習English",
                    "我love臺灣",
                    "123書本456",
                    "ABC語言XYZ"
                };
                
                foreach (var input in testCases)
                {
                    Debug.Log($"[DebugTest] About to convert: '{input}'");
                    var stopwatch = Stopwatch.StartNew();
                    
                    var result = converter.ConvertToSimplified(input);
                    
                    stopwatch.Stop();
                    Debug.Log($"[DebugTest] Converted in {stopwatch.ElapsedMilliseconds}ms: '{input}' → '{result}'");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DebugTest] Exception during mixed text conversion: {ex}");
                throw;
            }
        }
    }
}