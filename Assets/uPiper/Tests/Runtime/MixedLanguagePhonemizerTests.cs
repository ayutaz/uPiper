using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using uPiper.Core.Phonemizers;
using Debug = UnityEngine.Debug;

namespace uPiper.Tests.Runtime
{
    [TestFixture]
    [Timeout(30000)] // 30 second timeout for the entire test class
    // [Ignore("Temporarily disabled due to CMUDictionary loading issues")] // Re-enabled with proper timeout handling
    public class MixedLanguagePhonemizerTests
    {
        private UnifiedPhonemizer unifiedPhonemizer;
        private MixedLanguagePhonemizer mixedPhonemizer;
        private LanguageDetector languageDetector;

        [SetUp]
        [Timeout(30000)] // 30 second timeout - initialization with multiple backends can be slow
        public async Task Setup()
        {
            // Initialize unified phonemizer
            unifiedPhonemizer = new UnifiedPhonemizer();
            var unifiedResult = await unifiedPhonemizer.InitializeAsync();
            Assert.IsTrue(unifiedResult, "Failed to initialize UnifiedPhonemizer");

            // Initialize mixed language phonemizer
            mixedPhonemizer = new MixedLanguagePhonemizer();
            var mixedResult = await mixedPhonemizer.InitializeAsync();
            Assert.IsTrue(mixedResult, "Failed to initialize MixedLanguagePhonemizer");

            // Initialize language detector
            languageDetector = new LanguageDetector();
        }

        [TearDown]
        public void TearDown()
        {
            unifiedPhonemizer?.Dispose();
            mixedPhonemizer?.Dispose();
        }

        [Test]
        public void TestLanguageDetection()
        {
            var testCases = new[]
            {
                ("こんにちは", "ja"),
                ("Hello world", "en"),
                ("今日はmeeting at 3pmです", "mixed"),
                ("Unity エンジンで開発", "mixed"),
                ("123456", "en"), // Numbers default to English
                ("！？。、", "ja"), // Japanese punctuation context
            };

            foreach (var (text, expected) in testCases)
            {
                var detected = languageDetector.DetectPrimaryLanguage(text);
                Debug.Log($"Text: '{text}' - Expected: {expected}, Detected: {detected}");

                if (expected == "mixed")
                {
                    var segments = languageDetector.DetectSegments(text);
                    Assert.Greater(segments.Count, 1, $"Mixed text should have multiple segments: {text}");
                    var languages = segments.Select(s => s.Language).Distinct().Count();
                    Assert.Greater(languages, 1, $"Mixed text should have multiple languages: {text}");
                }
            }
        }

        [Test]
        public void TestLanguageSegmentation()
        {
            var text = "今日はmeeting at 3pmです。Tomorrow is 明日。";
            var segments = languageDetector.DetectSegments(text);

            Debug.Log($"Total segments: {segments.Count}");
            foreach (var segment in segments)
            {
                Debug.Log($"  {segment}");
            }

            // Verify we have both Japanese and English segments
            Assert.IsTrue(segments.Any(s => s.Language == "ja"), "Should have Japanese segments");
            Assert.IsTrue(segments.Any(s => s.Language == "en"), "Should have English segments");
        }

        [Test]
        public async Task TestMixedTextPhonemization()
        {
            var testCases = new[]
            {
                "今日はnice weatherですね",
                "Please call me at 午後3時",
                "UnityでTTSを実装する",
                "AI技術は素晴らしい",
                "これはtest messageです"
            };

            foreach (var text in testCases)
            {
                var result = await mixedPhonemizer.PhonemizeAsync(text);

                Assert.IsTrue(result.Success, $"Failed to phonemize: {text}");
                Assert.IsNotEmpty(result.Phonemes, $"Empty phonemes for: {text}");

                Debug.Log($"\nMixed text: '{text}'");
                Debug.Log($"Phonemes ({result.Phonemes.Length}): {string.Join(" ", result.Phonemes)}");

                if (result.Metadata != null && result.Metadata.ContainsKey("backends_used"))
                {
                    var backends = result.Metadata["backends_used"] as string[];
                    Debug.Log($"Backends used: {string.Join(", ", backends ?? new[] { "none" })}");
                }
            }
        }

        [Test]
        public async Task TestUnifiedPhonemizerAutoDetection()
        {
            var testCases = new[]
            {
                ("こんにちは世界", "ja"),
                ("Hello world", "en"),
                ("今日はgood dayです", "mixed")
            };

            foreach (var (text, expectedType) in testCases)
            {
                // Test with auto detection
                var autoResult = await unifiedPhonemizer.PhonemizeAsync(text, "auto");
                Assert.IsTrue(autoResult.Success, $"Failed with auto detection: {text}");

                // Test with explicit language
                var explicitResult = await unifiedPhonemizer.PhonemizeAsync(text, expectedType);
                Assert.IsTrue(explicitResult.Success, $"Failed with explicit language: {text}");

                Debug.Log($"\nText: '{text}'");
                Debug.Log($"Auto phonemes: {string.Join(" ", autoResult.Phonemes)}");
                Debug.Log($"Language: {autoResult.Language}");
            }
        }

        [Test]
        public async Task TestPunctuationHandling()
        {
            var testCases = new[]
            {
                "Hello, world!",
                "こんにちは、世界！",
                "What's your name?",
                "質問があります。答えてください。"
            };

            foreach (var text in testCases)
            {
                var result = await unifiedPhonemizer.PhonemizeAsync(text, "auto");
                Assert.IsTrue(result.Success);

                // Check for silence markers at punctuation
                var silenceCount = result.Phonemes.Count(p => p == "_");
                Assert.Greater(silenceCount, 0, $"Should have silence markers for: {text}");

                Debug.Log($"Text: '{text}' - Silences: {silenceCount}");
            }
        }

        [Test]
        public async Task TestComplexMixedText()
        {
            var complexText = @"
uPiperは、Unity環境でPiper TTSを使用するための
high-qualityな音声合成pluginです。
OpenJTalkによる日本語音素化と、
SimpleLTSによるEnglish phonemizationを
サポートしています。";

            var result = await unifiedPhonemizer.PhonemizeAsync(complexText, "auto");

            Assert.IsTrue(result.Success);
            Assert.IsNotEmpty(result.Phonemes);

            Debug.Log($"Complex text phoneme count: {result.Phonemes.Length}");

            // Analyze the text
            var stats = mixedPhonemizer.AnalyzeText(complexText);
            Debug.Log($"Text analysis: {string.Join(", ", stats.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
        }

        [Test]
        public async Task TestPerformanceComparison()
        {
            const string jaText = "これは日本語のテストテキストです。音素化の性能を測定しています。";
            const string enText = "This is an English test text. We are measuring phonemization performance.";
            const string mixedText = "これはmixed language textです。Performance測定中。";

            const int iterations = 10;

            // Japanese performance
            var jaWatch = Stopwatch.StartNew();
            for (var i = 0; i < iterations; i++)
            {
                await unifiedPhonemizer.PhonemizeAsync(jaText, "ja");
            }
            jaWatch.Stop();
            var jaAvg = jaWatch.ElapsedMilliseconds / (float)iterations;

            // English performance
            var enWatch = Stopwatch.StartNew();
            for (var i = 0; i < iterations; i++)
            {
                await unifiedPhonemizer.PhonemizeAsync(enText, "en");
            }
            enWatch.Stop();
            var enAvg = enWatch.ElapsedMilliseconds / (float)iterations;

            // Mixed performance
            var mixedWatch = Stopwatch.StartNew();
            for (var i = 0; i < iterations; i++)
            {
                await unifiedPhonemizer.PhonemizeAsync(mixedText, "mixed");
            }
            mixedWatch.Stop();
            var mixedAvg = mixedWatch.ElapsedMilliseconds / (float)iterations;

            Debug.Log($"Performance comparison (avg over {iterations} iterations):");
            Debug.Log($"  Japanese: {jaAvg:F2}ms");
            Debug.Log($"  English: {enAvg:F2}ms");
            Debug.Log($"  Mixed: {mixedAvg:F2}ms");

            // Mixed processing is expected to be slower due to language detection and switching
            // Allow up to 50x slower than Japanese (which has hardware acceleration)
            Assert.Less(mixedAvg, Math.Max(jaAvg * 50f, 300f), "Mixed processing should not be too slow");
        }

        [Test]
        public async Task TestEdgeCases()
        {
            var edgeCases = new[]
            {
                "",                    // Empty string
                "   ",                // Only whitespace
                "123",                // Only numbers
                "!@#$%",             // Only symbols
                "あ",                 // Single Japanese character
                "a",                  // Single English character
                "あa",                // Minimal mixed
                string.Join("", Enumerable.Repeat("あいうえお", 100)), // Very long Japanese
                string.Join(" ", Enumerable.Repeat("test", 100)),      // Very long English
            };

            foreach (var text in edgeCases)
            {
                var result = await unifiedPhonemizer.PhonemizeAsync(text, "auto");
                Assert.IsTrue(result.Success, $"Should handle edge case: '{text[..System.Math.Min(20, text.Length)]}'...");

                if (!string.IsNullOrWhiteSpace(text))
                {
                    Debug.Log($"Edge case handled: '{text[..System.Math.Min(20, text.Length)]}'... -> {result.Phonemes?.Length ?? 0} phonemes");
                }
            }
        }

        [Test]
        [Timeout(30000)] // 30 second timeout - backend initialization can be slow
        public void TestBackendAvailability()
        {
            var backends = unifiedPhonemizer.GetAvailableBackends();

            Assert.IsTrue(backends.ContainsKey("ja"), "Should have Japanese backend");
            Assert.IsTrue(backends.ContainsKey("en"), "Should have English backend");

            Debug.Log("Available backends:");
            foreach (var (language, backendList) in backends)
            {
                Debug.Log($"  {language}: {string.Join(", ", backendList)}");
            }
        }

        [Test]
        public async Task TestRealWorldExamples()
        {
            var examples = new[]
            {
                "お疲れ様でした。See you tomorrow!",
                "新しいfeatureを実装しました",
                "バグfixが必要です",
                "今日のmeetingは3pmからです",
                "AIとmachine learningについて勉強中",
                "Unity 2022.3 LTSを使っています",
                "このAPIはdeprecatedになりました",
                "パフォーマンスを50%改善しました"
            };

            foreach (var text in examples)
            {
                var result = await unifiedPhonemizer.PhonemizeAsync(text, "auto");
                Assert.IsTrue(result.Success);
                Debug.Log($"\nReal-world example: '{text}'");
                Debug.Log($"Detected as: {result.Language}");
                Debug.Log($"Phoneme count: {result.Phonemes.Length}");
            }
        }
    }
}