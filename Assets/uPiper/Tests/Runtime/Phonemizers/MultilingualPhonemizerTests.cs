using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using uPiper.Core.Phonemizers.Multilingual;
using uPiper.Core.Phonemizers.Backend;

namespace uPiper.Tests.Phonemizers
{
    /// <summary>
    /// Tests for multilingual phonemization features
    /// </summary>
    [TestFixture]
    public class MultilingualPhonemizerTests
    {
        private MultilingualPhonemizerService service;
        private LanguageDetector detector;

        // Test phrases in different languages
        private readonly Dictionary<string, string> testPhrases = new Dictionary<string, string>
        {
            ["en-US"] = "The quick brown fox jumps over the lazy dog",
            ["en-GB"] = "Colour, honour, and favour are British spellings",
            ["en-IN"] = "Please do the needful and revert back",
            ["es-ES"] = "El rápido zorro marrón salta sobre el perro perezoso",
            ["fr-FR"] = "Le renard brun rapide saute par-dessus le chien paresseux",
            ["de-DE"] = "Der schnelle braune Fuchs springt über den faulen Hund",
            ["ja-JP"] = "素早い茶色のキツネが怠け者の犬を飛び越える",
            ["zh-CN"] = "敏捷的棕色狐狸跳过懒狗",
            ["ko-KR"] = "빠른 갈색 여우가 게으른 개를 뛰어넘는다"
        };

        [SetUp]
        public void SetUp()
        {
            service = new MultilingualPhonemizerService();
            detector = new LanguageDetector();
        }

        #region Language Detection Tests

        [Test]
        public void LanguageDetector_ShouldIdentifyCommonLanguages()
        {
            var testCases = new Dictionary<string, string>
            {
                ["en"] = "This is definitely an English sentence with common words.",
                ["es"] = "Esta es una oración en español con palabras comunes.",
                ["fr"] = "Ceci est une phrase en français avec des mots courants.",
                ["de"] = "Dies ist ein deutscher Satz mit häufigen Wörtern.",
                ["ja"] = "これは一般的な単語を含む日本語の文です。",
                ["zh"] = "这是一个包含常用词的中文句子。",
                ["ko"] = "이것은 일반적인 단어가 포함된 한국어 문장입니다."
            };

            foreach (var (expectedLang, text) in testCases)
            {
                var result = detector.DetectLanguage(text);
                
                Assert.IsTrue(result.DetectedLanguage.StartsWith(expectedLang) || 
                             expectedLang.StartsWith(result.DetectedLanguage.Substring(0, 2)),
                    $"Expected {expectedLang}, but detected {result.DetectedLanguage} for: {text}");
                
                Assert.Greater(result.Confidence, 0.5f, 
                    $"Low confidence ({result.Confidence}) for {expectedLang}");
                
                Debug.Log($"Detected {result.DetectedLanguage} with confidence {result.Confidence:F2} for {expectedLang}");
            }
        }

        [Test]
        public void LanguageDetector_ShouldHandleMixedScripts()
        {
            var mixedText = "Hello world! こんにちは世界！ Bonjour le monde! 你好世界！";
            var segments = detector.SegmentMixedLanguageText(mixedText);
            
            Assert.Greater(segments.Count, 2, "Should detect multiple language segments");
            
            var detectedLanguages = segments.Select(s => s.Language).Distinct().ToList();
            Assert.Greater(detectedLanguages.Count, 2, "Should detect at least 3 different languages");
            
            // Verify segment boundaries
            foreach (var segment in segments)
            {
                var extractedText = mixedText.Substring(segment.StartIndex, 
                    segment.EndIndex - segment.StartIndex + 1);
                Assert.AreEqual(segment.Text, extractedText, 
                    "Segment text should match extracted text");
            }
            
            Debug.Log($"Detected segments: {string.Join(", ", 
                segments.Select(s => $"{s.Language}:'{s.Text}'"))}");
        }

        [Test]
        public void LanguageDetector_ShouldHandleAmbiguousCases()
        {
            // Short text that could be multiple languages
            var ambiguousTexts = new[]
            {
                "No", // Could be English, Spanish, etc.
                "Si", // Could be Spanish, Italian, etc.
                "OK", // Universal
                "1234", // Numbers
                "!!!", // Punctuation only
            };

            foreach (var text in ambiguousTexts)
            {
                var result = detector.DetectLanguage(text);
                
                // Should still return a result, even if low confidence
                Assert.IsNotNull(result);
                Assert.IsNotNull(result.DetectedLanguage);
                
                Debug.Log($"Ambiguous '{text}' detected as {result.DetectedLanguage} " +
                         $"with confidence {result.Confidence:F2}");
            }
        }

        #endregion

        #region Multilingual Service Tests

        [Test]
        public async Task MultilingualService_ShouldSupportExpectedLanguages()
        {
            var supported = service.GetSupportedLanguages();
            
            // Check minimum expected languages
            var expectedLanguages = new[] { "en-US", "en-GB", "en-IN" };
            foreach (var lang in expectedLanguages)
            {
                Assert.IsTrue(supported.ContainsKey(lang), 
                    $"Should support {lang}");
                
                var capabilities = supported[lang];
                Assert.IsNotEmpty(capabilities.AvailableBackends,
                    $"{lang} should have available backends");
                Assert.Greater(capabilities.OverallQuality, 0,
                    $"{lang} should have quality score");
            }
            
            // Log all supported languages
            Debug.Log($"Supported languages: {string.Join(", ", 
                supported.Select(kvp => $"{kvp.Key} (Q:{kvp.Value.OverallQuality:F2})"))}");
        }

        [Test]
        public async Task MultilingualService_ShouldAutoDetectAndPhonemize()
        {
            var englishText = "Hello, this is a test of automatic language detection.";
            var result = await service.PhonemizeAutoDetectAsync(englishText);
            
            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result.Phonemes);
            Assert.IsTrue(result.DetectedLanguage.StartsWith("en"),
                $"Should detect English, but detected {result.DetectedLanguage}");
            Assert.Greater(result.LanguageConfidence, 0.5f,
                "Should have reasonable confidence");
            
            Debug.Log($"Auto-detected {result.DetectedLanguage} with confidence " +
                     $"{result.LanguageConfidence:F2}, used backend: {result.UsedBackend}");
            Debug.Log($"Phonemes: {string.Join(" ", result.Phonemes)}");
        }

        [Test]
        public async Task MultilingualService_ShouldHandleMultipleLanguagesInBatch()
        {
            var multilingualTexts = new Dictionary<string, string>
            {
                ["en-US"] = "Hello world",
                ["en-GB"] = "Hello world",
                ["en-IN"] = "Hello world"
            };
            
            var results = await service.PhonemizeMultilingualAsync(multilingualTexts);
            
            Assert.AreEqual(multilingualTexts.Count, results.Count);
            
            foreach (var (lang, result) in results)
            {
                Assert.IsNotEmpty(result.Phonemes,
                    $"Language {lang} should have phonemes");
                
                Debug.Log($"{lang}: {string.Join(" ", result.Phonemes)}");
            }
            
            // Check for dialect differences (if any)
            var usPhonemes = results["en-US"].Phonemes;
            var gbPhonemes = results["en-GB"].Phonemes;
            
            // They might be identical for "Hello world", but the system should handle both
            Debug.Log($"US vs GB phoneme difference: " +
                     $"{string.Join(",", usPhonemes.Except(gbPhonemes))}");
        }

        #endregion

        #region Fallback Mechanism Tests

        [Test]
        public void MultilingualService_ShouldUseFallbackForUnsupportedLanguage()
        {
            // Configure fallback chain
            service.SetLanguageFallbackChain("pt-BR", "pt-PT", "es-ES", "en-US");
            
            // Try to get backend for Brazilian Portuguese (might not be directly supported)
            var backend = service.GetBackendForLanguage("pt-BR");
            
            Assert.IsNotNull(backend, "Should find backend through fallback");
            
            // Check which languages the backend actually supports
            var supportedByBackend = backend.SupportedLanguages;
            Debug.Log($"Fallback backend supports: {string.Join(", ", supportedByBackend)}");
        }

        [Test]
        public async Task MultilingualService_ShouldReportFallbackUsage()
        {
            // Set up a fallback scenario
            service.SetLanguageFallbackChain("xx-XX", "en-US");
            
            // Create a simple test backend that we can track
            var testBackend = service.GetBackendForLanguage("en-US");
            Assert.IsNotNull(testBackend);
            
            // Try to phonemize with unsupported language
            var result = await service.PhonemizeAutoDetectAsync("Test fallback mechanism");
            
            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result.Phonemes);
            
            Debug.Log($"Used backend: {result.UsedBackend}, " +
                     $"Fallback: {result.UsedFallback}, " +
                     $"Reason: {result.FallbackReason}");
        }

        [Test]
        public void MultilingualService_ShouldPrioritizeSimilarLanguages()
        {
            // Test language group fallback
            var capabilities = service.GetSupportedLanguages();
            
            // Check Germanic language group
            if (capabilities.ContainsKey("en-US") && capabilities.ContainsKey("en-GB"))
            {
                var qualityUS = service.GetQualityScore("en-US", capabilities["en-US"].PreferredBackend);
                var qualityGB = service.GetQualityScore("en-GB", capabilities["en-GB"].PreferredBackend);
                
                Debug.Log($"Quality scores - en-US: {qualityUS:F2}, en-GB: {qualityGB:F2}");
                
                // Both should have reasonable quality
                Assert.Greater(qualityUS, 0.5f);
                Assert.Greater(qualityGB, 0.5f);
            }
        }

        #endregion

        #region Language-Specific Features Tests

        [Test]
        public void LanguageCapabilities_ShouldReflectLanguageFeatures()
        {
            var capabilities = service.GetSupportedLanguages();
            
            // Check English capabilities
            if (capabilities.TryGetValue("en-US", out var englishCap))
            {
                Assert.IsTrue(englishCap.SupportsStress, 
                    "English should support stress markers");
                Assert.IsFalse(englishCap.SupportsTone,
                    "English should not be tonal");
                Assert.AreEqual("Latin", englishCap.Script,
                    "English uses Latin script");
            }
            
            // Log all language capabilities
            foreach (var (lang, cap) in capabilities)
            {
                Debug.Log($"{lang}: Script={cap.Script}, " +
                         $"Stress={cap.SupportsStress}, " +
                         $"Tone={cap.SupportsTone}, " +
                         $"G2P={cap.SupportsG2P}");
            }
        }

        [Test]
        public async Task LanguageSpecific_EnglishDialectVariations()
        {
            var word = "schedule"; // Pronounced differently in US vs UK
            var dialects = new[] { "en-US", "en-GB" };
            var results = new Dictionary<string, PhonemeResult>();
            
            foreach (var dialect in dialects)
            {
                var backend = service.GetBackendForLanguage(dialect);
                if (backend != null)
                {
                    results[dialect] = await backend.PhonemizeAsync(word, dialect);
                }
            }
            
            // Log phoneme differences
            foreach (var (dialect, result) in results)
            {
                Debug.Log($"{dialect} '{word}': {string.Join(" ", result.Phonemes)}");
            }
            
            // Note: Actual phoneme differences depend on backend implementation
            // This test mainly verifies that the system can handle dialect-specific processing
        }

        #endregion

        #region Edge Cases and Stress Tests

        [Test]
        public async Task EdgeCase_EmptyAndWhitespaceText()
        {
            var edgeCases = new[] { "", " ", "\t", "\n", "   \t\n   " };
            
            foreach (var text in edgeCases)
            {
                var result = await service.PhonemizeAutoDetectAsync(text);
                
                Assert.IsNotNull(result, $"Should handle '{text.Replace("\n", "\\n").Replace("\t", "\\t")}'");
                // Empty text might result in empty phonemes or silence markers
                Debug.Log($"Edge case '{text}' resulted in {result.Phonemes.Count} phonemes");
            }
        }

        [Test]
        public async Task EdgeCase_SpecialCharactersAndEmoji()
        {
            var specialTexts = new[]
            {
                "Hello! @#$%^&*()",
                "Test 123 456",
                "😀 Hello 😎 World 🌍",
                "¿Cómo estás? ¡Bien!",
                "Test™ Product® Name©"
            };
            
            foreach (var text in specialTexts)
            {
                var result = await service.PhonemizeAutoDetectAsync(text);
                
                Assert.IsNotNull(result);
                Assert.IsNotEmpty(result.Phonemes, 
                    $"Should produce some phonemes for '{text}'");
                
                Debug.Log($"Special text '{text}' -> {string.Join(" ", result.Phonemes.Take(10))}...");
            }
        }

        [Test]
        public async Task StressTest_RapidLanguageSwitching()
        {
            const int iterations = 50;
            var languages = new[] { "en-US", "en-GB", "en-IN" };
            var errors = new List<string>();
            
            for (int i = 0; i < iterations; i++)
            {
                var lang = languages[i % languages.Length];
                var text = $"Test {i} in {lang}";
                
                try
                {
                    var backend = service.GetBackendForLanguage(lang);
                    Assert.IsNotNull(backend, $"Backend should exist for {lang}");
                    
                    var result = await backend.PhonemizeAsync(text, lang);
                    Assert.IsNotEmpty(result.Phonemes);
                }
                catch (System.Exception e)
                {
                    errors.Add($"Iteration {i} ({lang}): {e.Message}");
                }
            }
            
            Assert.IsEmpty(errors, $"Errors during rapid switching: {string.Join("\n", errors)}");
            Debug.Log($"Successfully completed {iterations} rapid language switches");
        }

        #endregion

        #region Performance with Multiple Languages

        [UnityTest]
        public IEnumerator Performance_MultilingualBatchProcessing()
        {
            var texts = new Dictionary<string, List<string>>();
            
            // Prepare texts in different languages
            foreach (var lang in new[] { "en-US", "en-GB", "en-IN" })
            {
                texts[lang] = Enumerable.Range(0, 10)
                    .Select(i => $"Test sentence number {i} in {lang}")
                    .ToList();
            }
            
            var startTime = Time.realtimeSinceStartup;
            var allTasks = new List<Task<PhonemeResult>>();
            
            // Process all texts
            foreach (var (lang, langTexts) in texts)
            {
                foreach (var text in langTexts)
                {
                    allTasks.Add(Task.Run(async () =>
                    {
                        var backend = service.GetBackendForLanguage(lang);
                        return await backend.PhonemizeAsync(text, lang);
                    }));
                }
            }
            
            // Wait for all to complete
            yield return new WaitUntil(() => allTasks.All(t => t.IsCompleted));
            
            var elapsedTime = Time.realtimeSinceStartup - startTime;
            var totalTexts = texts.Sum(kvp => kvp.Value.Count);
            var avgTimePerText = elapsedTime / totalTexts;
            
            Debug.Log($"Multilingual batch: {totalTexts} texts in {elapsedTime:F2}s " +
                     $"({avgTimePerText * 1000:F2}ms per text)");
            
            Assert.Less(avgTimePerText, 0.1f, "Should process each text in under 100ms");
            
            // Verify all completed successfully
            var failedCount = allTasks.Count(t => t.IsFaulted);
            Assert.AreEqual(0, failedCount, "All tasks should complete successfully");
        }

        #endregion
    }
}