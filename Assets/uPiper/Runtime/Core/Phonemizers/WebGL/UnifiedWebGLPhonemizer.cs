#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using uPiper.Core.Phonemizers.Backend;

namespace uPiper.Core.Phonemizers.WebGL
{
    /// <summary>
    /// Unified phonemizer for WebGL that automatically selects the appropriate backend based on language
    /// </summary>
    public class UnifiedWebGLPhonemizer : PhonemizerBackendBase
    {
        private WebGLOpenJTalkPhonemizer japanesePhonmizer;
        private WebGLESpeakPhonemizer multilingualPhonemizer;
        private readonly object initLock = new object();
        private bool isInitialized = false;

        // Language detection patterns
        private static readonly Regex JapanesePattern = new Regex(@"[\u3040-\u309F\u30A0-\u30FF\u4E00-\u9FAF]", RegexOptions.Compiled);
        private static readonly Regex ChinesePattern = new Regex(@"[\u4E00-\u9FFF\u3400-\u4DBF]", RegexOptions.Compiled);
        private static readonly Regex KoreanPattern = new Regex(@"[\uAC00-\uD7AF\u1100-\u11FF]", RegexOptions.Compiled);
        private static readonly Regex LatinPattern = new Regex(@"[a-zA-Z]", RegexOptions.Compiled);

        public override string Name => "Unified WebGL Phonemizer";
        public override string Version => "1.0.0";
        public override string License => "Mixed (BSD-3-Clause for OpenJTalk, GPL-3.0 for eSpeak-ng)";
        
        // Combine supported languages from both backends
        public override string[] SupportedLanguages => new[] 
        { 
            "auto", // Auto-detect language
            "ja", "ja-JP", // Japanese
            "en", "en-US", "en-GB", // English
            "zh", "zh-CN", "zh-TW", // Chinese
            "ko", "ko-KR", // Korean (via eSpeak-ng)
            "es", "fr", "de", "it", "pt", "ru" // Other languages via eSpeak-ng
        };

        protected override async Task<bool> InitializeInternalAsync(
            PhonemizerBackendOptions options,
            CancellationToken cancellationToken)
        {
            return await Task.Run(async () =>
            {
                lock (initLock)
                {
                    if (isInitialized)
                    {
                        return true;
                    }
                }

                try
                {
                    Debug.Log("[UnifiedWebGLPhonemizer] Initializing unified phonemizer...");

                    // Initialize both backends in parallel
                    var tasks = new List<Task<bool>>();

                    // Initialize Japanese phonemizer
                    japanesePhonmizer = new WebGLOpenJTalkPhonemizer();
                    tasks.Add(japanesePhonmizer.InitializeAsync(options, cancellationToken));

                    // Initialize multilingual phonemizer
                    multilingualPhonemizer = new WebGLESpeakPhonemizer();
                    tasks.Add(multilingualPhonemizer.InitializeAsync(options, cancellationToken));

                    // Wait for both to complete
                    var results = await Task.WhenAll(tasks);

                    lock (initLock)
                    {
                        isInitialized = results.All(r => r);
                        if (isInitialized)
                        {
                            Debug.Log("[UnifiedWebGLPhonemizer] All backends initialized successfully");
                        }
                        else
                        {
                            Debug.LogError("[UnifiedWebGLPhonemizer] One or more backends failed to initialize");
                        }
                        return isInitialized;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[UnifiedWebGLPhonemizer] Initialization error: {e.Message}");
                    return false;
                }
            });
        }

        public override async Task<PhonemeResult> PhonemizeAsync(
            string text,
            string language,
            PhonemeOptions options = null,
            CancellationToken cancellationToken = default)
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("UnifiedWebGLPhonemizer is not initialized");
            }

            if (string.IsNullOrEmpty(text))
            {
                return new PhonemeResult
                {
                    Phonemes = Array.Empty<string>(),
                    Language = language,
                    Success = true
                };
            }

            // Determine the actual language to use
            var detectedLanguage = language.ToLower() == "auto" 
                ? DetectLanguage(text) 
                : language;

            Debug.Log($"[UnifiedWebGLPhonemizer] Processing text with language: {detectedLanguage} (requested: {language})");

            try
            {
                // For mixed text, process by segments
                if (language.ToLower() == "auto" && ContainsMixedLanguages(text))
                {
                    return await ProcessMixedLanguageText(text, options, cancellationToken);
                }

                // Route to appropriate backend
                var backend = SelectBackend(detectedLanguage);
                if (backend == null)
                {
                    throw new NotSupportedException($"Language '{detectedLanguage}' is not supported");
                }

                return await backend.PhonemizeAsync(text, detectedLanguage, options, cancellationToken);
            }
            catch (Exception e)
            {
                // Fallback handling
                Debug.LogWarning($"[UnifiedWebGLPhonemizer] Primary phonemization failed: {e.Message}");
                return await HandleFallback(text, detectedLanguage, options, cancellationToken);
            }
        }

        private PhonemizerBackendBase SelectBackend(string language)
        {
            var normalizedLang = language.ToLower();
            
            // Japanese always uses OpenJTalk
            if (normalizedLang.StartsWith("ja"))
            {
                return japanesePhonmizer;
            }

            // All other languages use eSpeak-ng
            return multilingualPhonemizer;
        }

        private string DetectLanguage(string text)
        {
            // Count characters by script
            var scriptCounts = new Dictionary<string, int>
            {
                ["ja"] = JapanesePattern.Matches(text).Count,
                ["zh"] = ChinesePattern.Matches(text).Count,
                ["ko"] = KoreanPattern.Matches(text).Count,
                ["en"] = LatinPattern.Matches(text).Count
            };

            // Japanese detection (prioritize if contains kana)
            if (scriptCounts["ja"] > 0 && text.Any(c => 
                (c >= '\u3040' && c <= '\u309F') || // Hiragana
                (c >= '\u30A0' && c <= '\u30FF')))   // Katakana
            {
                return "ja";
            }

            // Find the script with the most characters
            var dominantScript = scriptCounts.OrderByDescending(kvp => kvp.Value).First();
            
            // If no specific script dominates, default to English
            if (dominantScript.Value == 0)
            {
                return "en";
            }

            return dominantScript.Key;
        }

        private bool ContainsMixedLanguages(string text)
        {
            int scriptTypeCount = 0;
            
            if (JapanesePattern.IsMatch(text)) scriptTypeCount++;
            if (ChinesePattern.IsMatch(text) && !JapanesePattern.IsMatch(text)) scriptTypeCount++;
            if (KoreanPattern.IsMatch(text)) scriptTypeCount++;
            if (LatinPattern.IsMatch(text)) scriptTypeCount++;

            return scriptTypeCount > 1;
        }

        private async Task<PhonemeResult> ProcessMixedLanguageText(
            string text,
            PhonemeOptions options,
            CancellationToken cancellationToken)
        {
            Debug.Log("[UnifiedWebGLPhonemizer] Processing mixed language text");
            
            var segments = SegmentTextByLanguage(text);
            var allPhonemes = new List<string>();

            foreach (var segment in segments)
            {
                try
                {
                    var backend = SelectBackend(segment.Language);
                    if (backend != null)
                    {
                        var result = await backend.PhonemizeAsync(
                            segment.Text, 
                            segment.Language, 
                            options, 
                            cancellationToken);
                        
                        if (result.Success)
                        {
                            // Remove start/end markers from intermediate segments
                            var phonemes = result.Phonemes.ToList();
                            if (allPhonemes.Count > 0 && phonemes.Count > 0 && phonemes[0] == "^")
                            {
                                phonemes.RemoveAt(0);
                            }
                            if (phonemes.Count > 0 && phonemes[phonemes.Count - 1] == "$")
                            {
                                phonemes.RemoveAt(phonemes.Count - 1);
                            }
                            
                            allPhonemes.AddRange(phonemes);
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[UnifiedWebGLPhonemizer] Failed to process segment '{segment.Text}': {e.Message}");
                    // Add a pause for failed segments
                    allPhonemes.Add("_");
                }
            }

            // Add start and end markers
            if (allPhonemes.Count > 0)
            {
                allPhonemes.Insert(0, "^");
                allPhonemes.Add("$");
            }

            return new PhonemeResult
            {
                Phonemes = allPhonemes.ToArray(),
                Language = "mixed",
                Success = true
            };
        }

        private List<TextSegment> SegmentTextByLanguage(string text)
        {
            var segments = new List<TextSegment>();
            var currentSegment = new System.Text.StringBuilder();
            string currentLanguage = null;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                string charLanguage = DetectCharacterLanguage(c);

                if (currentLanguage == null)
                {
                    currentLanguage = charLanguage;
                }

                if (charLanguage != currentLanguage && currentSegment.Length > 0)
                {
                    // Language changed, save current segment
                    segments.Add(new TextSegment 
                    { 
                        Text = currentSegment.ToString(), 
                        Language = currentLanguage 
                    });
                    
                    currentSegment.Clear();
                    currentLanguage = charLanguage;
                }

                currentSegment.Append(c);
            }

            // Add the last segment
            if (currentSegment.Length > 0)
            {
                segments.Add(new TextSegment 
                { 
                    Text = currentSegment.ToString(), 
                    Language = currentLanguage 
                });
            }

            return segments;
        }

        private string DetectCharacterLanguage(char c)
        {
            // Japanese (Hiragana, Katakana)
            if ((c >= '\u3040' && c <= '\u309F') || (c >= '\u30A0' && c <= '\u30FF'))
            {
                return "ja";
            }

            // CJK Unified Ideographs (could be Chinese or Japanese)
            if (c >= '\u4E00' && c <= '\u9FAF')
            {
                // This is simplified - in reality, we'd need context
                return "zh"; // Default to Chinese for now
            }

            // Korean
            if ((c >= '\uAC00' && c <= '\uD7AF') || (c >= '\u1100' && c <= '\u11FF'))
            {
                return "ko";
            }

            // Default to English for Latin characters and others
            return "en";
        }

        private async Task<PhonemeResult> HandleFallback(
            string text,
            string language,
            PhonemeOptions options,
            CancellationToken cancellationToken)
        {
            Debug.Log($"[UnifiedWebGLPhonemizer] Attempting fallback for language: {language}");

            // Try with a different backend
            PhonemizerBackendBase fallbackBackend = null;

            if (language.StartsWith("ja"))
            {
                // If Japanese failed, try eSpeak-ng (won't be good but better than nothing)
                fallbackBackend = multilingualPhonemizer;
            }
            else
            {
                // For other languages, we could try Japanese phonemizer for romaji
                // but that's probably not useful
                fallbackBackend = null;
            }

            if (fallbackBackend != null)
            {
                try
                {
                    var result = await fallbackBackend.PhonemizeAsync(text, "en", options, cancellationToken);
                    result.Language = language; // Keep original language tag
                    Debug.LogWarning($"[UnifiedWebGLPhonemizer] Fallback successful using {fallbackBackend.Name}");
                    return result;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[UnifiedWebGLPhonemizer] Fallback also failed: {e.Message}");
                }
            }

            // Last resort: return empty phonemes with error
            return new PhonemeResult
            {
                Phonemes = new[] { "^", "_", "$" }, // Just silence
                Language = language,
                Success = false,
                Error = "Phonemization failed for all backends"
            };
        }

        public override long GetMemoryUsage()
        {
            long total = 0;
            if (japanesePhonmizer != null) total += japanesePhonmizer.GetMemoryUsage();
            if (multilingualPhonemizer != null) total += multilingualPhonemizer.GetMemoryUsage();
            return total;
        }

        public override BackendCapabilities GetCapabilities()
        {
            // Combined capabilities
            return new BackendCapabilities
            {
                SupportsIPA = true, // eSpeak-ng supports IPA
                SupportsStress = true, // eSpeak-ng supports stress
                SupportsSyllables = false,
                SupportsTones = true, // eSpeak-ng supports tones
                SupportsDuration = false,
                SupportsBatchProcessing = false,
                IsThreadSafe = false,
                RequiresNetwork = false
            };
        }

        protected override void DisposeInternal()
        {
            japanesePhonmizer?.Dispose();
            multilingualPhonemizer?.Dispose();
            japanesePhonmizer = null;
            multilingualPhonemizer = null;
            isInitialized = false;
        }

        private class TextSegment
        {
            public string Text { get; set; }
            public string Language { get; set; }
        }
    }
}
#endif