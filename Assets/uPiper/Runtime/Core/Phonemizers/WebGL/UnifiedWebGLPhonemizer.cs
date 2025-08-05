#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using uPiper.Core.Phonemizers.Text;

namespace uPiper.Core.Phonemizers.WebGL
{
    /// <summary>
    /// Unified phonemizer for WebGL that automatically selects the appropriate backend based on language
    /// </summary>
    public class UnifiedWebGLPhonemizer : BasePhonemizer
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

        public UnifiedWebGLPhonemizer() : base(new TextNormalizer(), cacheSize: 1000)
        {
            InitializeLanguageInfos();
            _ = InitializeAsync();
        }
        
        private void InitializeLanguageInfos()
        {
            foreach (var lang in SupportedLanguages)
            {
                _languageInfos[lang] = new LanguageInfo
                {
                    Code = lang,
                    Name = GetLanguageName(lang),
                    EnglishName = GetLanguageEnglishName(lang),
                    IsSupported = true
                };
            }
        }
        
        private string GetLanguageName(string code)
        {
            return code switch
            {
                "auto" => "Auto-detect",
                "ja" => "日本語",
                "ja-JP" => "日本語",
                "en" => "English",
                "en-US" => "English (US)",
                "en-GB" => "English (UK)",
                "zh" => "中文",
                "zh-CN" => "中文（简体）",
                "zh-TW" => "中文（繁體）",
                "ko" => "한국어",
                "ko-KR" => "한국어",
                "es" => "Español",
                "fr" => "Français",
                "de" => "Deutsch",
                "it" => "Italiano",
                "pt" => "Português",
                "ru" => "Русский",
                _ => code
            };
        }
        
        private string GetLanguageEnglishName(string code)
        {
            return code switch
            {
                "auto" => "Auto-detect",
                "ja" => "Japanese",
                "ja-JP" => "Japanese",
                "en" => "English",
                "en-US" => "English (US)",
                "en-GB" => "English (UK)",
                "zh" => "Chinese",
                "zh-CN" => "Chinese (Simplified)",
                "zh-TW" => "Chinese (Traditional)",
                "ko" => "Korean",
                "ko-KR" => "Korean",
                "es" => "Spanish",
                "fr" => "French",
                "de" => "German",
                "it" => "Italian",
                "pt" => "Portuguese",
                "ru" => "Russian",
                _ => code
            };
        }

        private async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
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
                tasks.Add(japanesePhonmizer.InitializeAsync(null, cancellationToken));

                // Initialize multilingual phonemizer
                multilingualPhonemizer = new WebGLESpeakPhonemizer();
                tasks.Add(multilingualPhonemizer.InitializeAsync(null, cancellationToken));

                // Wait for both to initialize
                var results = await Task.WhenAll(tasks);

                lock (initLock)
                {
                    isInitialized = results.All(r => r);
                }

                if (isInitialized)
                {
                    Debug.Log("[UnifiedWebGLPhonemizer] Unified phonemizer initialized successfully");
                }
                else
                {
                    Debug.LogError("[UnifiedWebGLPhonemizer] Failed to initialize one or more backends");
                }

                return isInitialized;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnifiedWebGLPhonemizer] Initialization error: {ex.Message}");
                return false;
            }
        }

        protected override async Task<PhonemeResult> InternalPhonemizeAsync(
            string text, 
            string language, 
            CancellationToken cancellationToken)
        {
            // Ensure initialization
            if (!isInitialized)
            {
                await InitializeAsync(cancellationToken);
                if (!isInitialized)
                {
                    throw new InvalidOperationException("Failed to initialize UnifiedWebGLPhonemizer");
                }
            }

            // Handle auto-detection
            if (language == "auto")
            {
                language = DetectLanguage(text);
                Debug.Log($"[UnifiedWebGLPhonemizer] Auto-detected language: {language}");
            }

            // Handle mixed language text
            if (ContainsMixedLanguages(text))
            {
                return await ProcessMixedLanguageTextAsync(text, cancellationToken);
            }

            // Select appropriate backend based on language
            if (ShouldUseJapanesePhonemizer(language))
            {
                return await japanesePhonmizer.PhonemizeAsync(text, language, cancellationToken);
            }
            else
            {
                return await multilingualPhonemizer.PhonemizeAsync(text, language, cancellationToken);
            }
        }

        private string DetectLanguage(string text)
        {
            // Count characters by script
            int japaneseCount = JapanesePattern.Matches(text).Count;
            int chineseCount = ChinesePattern.Matches(text).Count;
            int koreanCount = KoreanPattern.Matches(text).Count;
            int latinCount = LatinPattern.Matches(text).Count;

            // Japanese characters often overlap with Chinese, so check for kana first
            if (japaneseCount - chineseCount > 0 || 
                Regex.IsMatch(text, @"[\u3040-\u309F\u30A0-\u30FF]"))
            {
                return "ja";
            }

            // Check for predominant script
            var counts = new[]
            {
                (count: chineseCount, lang: "zh"),
                (count: koreanCount, lang: "ko"),
                (count: latinCount, lang: "en")
            };

            var dominant = counts.OrderByDescending(c => c.count).First();
            return dominant.count > 0 ? dominant.lang : "en"; // Default to English
        }

        private bool ContainsMixedLanguages(string text)
        {
            int scriptCount = 0;
            if (JapanesePattern.IsMatch(text)) scriptCount++;
            if (ChinesePattern.IsMatch(text) && !JapanesePattern.IsMatch(text)) scriptCount++;
            if (KoreanPattern.IsMatch(text)) scriptCount++;
            if (LatinPattern.IsMatch(text)) scriptCount++;

            return scriptCount > 1;
        }

        private async Task<PhonemeResult> ProcessMixedLanguageTextAsync(
            string text,
            CancellationToken cancellationToken)
        {
            Debug.Log("[UnifiedWebGLPhonemizer] Processing mixed language text");

            // Split text into segments by detected language
            var segments = SegmentTextByLanguage(text);
            var results = new List<PhonemeResult>();

            // Process each segment with appropriate backend
            foreach (var segment in segments)
            {
                if (string.IsNullOrWhiteSpace(segment.text))
                    continue;

                PhonemeResult result;
                if (ShouldUseJapanesePhonemizer(segment.language))
                {
                    result = await japanesePhonmizer.PhonemizeAsync(
                        segment.text, segment.language, cancellationToken);
                }
                else
                {
                    result = await multilingualPhonemizer.PhonemizeAsync(
                        segment.text, segment.language, cancellationToken);
                }

                results.Add(result);
            }

            // Combine results
            return CombinePhonemeResults(results);
        }

        private List<(string text, string language)> SegmentTextByLanguage(string text)
        {
            var segments = new List<(string text, string language)>();
            var currentSegment = new System.Text.StringBuilder();
            string currentLanguage = null;

            foreach (char c in text)
            {
                string charLanguage = DetectCharacterLanguage(c);

                if (currentLanguage == null)
                {
                    currentLanguage = charLanguage;
                }

                if (charLanguage != currentLanguage)
                {
                    // Save current segment
                    if (currentSegment.Length > 0)
                    {
                        segments.Add((currentSegment.ToString(), currentLanguage));
                        currentSegment.Clear();
                    }
                    currentLanguage = charLanguage;
                }

                currentSegment.Append(c);
            }

            // Add final segment
            if (currentSegment.Length > 0)
            {
                segments.Add((currentSegment.ToString(), currentLanguage));
            }

            return segments;
        }

        private string DetectCharacterLanguage(char c)
        {
            string s = c.ToString();
            
            if (Regex.IsMatch(s, @"[\u3040-\u309F\u30A0-\u30FF]"))
                return "ja"; // Kana = definitely Japanese
                
            if (JapanesePattern.IsMatch(s))
                return "ja"; // Kanji in Japanese context
                
            if (ChinesePattern.IsMatch(s))
                return "zh";
                
            if (KoreanPattern.IsMatch(s))
                return "ko";
                
            return "en"; // Default to English for Latin and other scripts
        }

        private PhonemeResult CombinePhonemeResults(List<PhonemeResult> results)
        {
            if (results.Count == 0)
            {
                return new PhonemeResult
                {
                    Success = false,
                    ErrorMessage = "No phoneme results to combine"
                };
            }

            var combinedPhonemes = new List<string>();
            var combinedDetails = new List<PhonemeDetail>();
            int offset = 0;

            foreach (var result in results)
            {
                if (!result.Success)
                    continue;

                combinedPhonemes.AddRange(result.Phonemes);

                if (result.Details != null)
                {
                    foreach (var detail in result.Details)
                    {
                        combinedDetails.Add(new PhonemeDetail
                        {
                            Phoneme = detail.Phoneme,
                            Position = detail.Position + offset,
                            Duration = detail.Duration
                        });
                    }
                    offset += result.Details.Count;
                }
            }

            return new PhonemeResult
            {
                Success = true,
                Phonemes = combinedPhonemes.ToArray(),
                PhonemeString = string.Join(" ", combinedPhonemes),
                Language = "mixed",
                Details = combinedDetails.ToArray(),
                ProcessingTimeMs = results.Sum(r => r.ProcessingTimeMs)
            };
        }

        private bool ShouldUseJapanesePhonemizer(string language)
        {
            return language == "ja" || language == "ja-JP";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                japanesePhonmizer?.Dispose();
                multilingualPhonemizer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
#endif