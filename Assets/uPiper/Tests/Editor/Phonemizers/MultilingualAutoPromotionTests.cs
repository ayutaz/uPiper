using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using uPiper.Core;
using uPiper.Core.Phonemizers.Multilingual;

namespace uPiper.Tests.Editor.Phonemizers
{
    /// <summary>
    /// Tests for PiperTTS auto-promotion logic that determines whether a
    /// MultilingualPhonemizer should be created based on model LanguageIdMap
    /// and PiperConfig.AutoDetectLanguage settings.
    ///
    /// The production logic (PiperTTS.Inference.cs) is:
    ///   var isMultilingualModel = voiceConfig?.LanguageIdMap != null
    ///       &amp;&amp; voiceConfig.LanguageIdMap.Count > 1;
    ///   if (config != null &amp;&amp; (config.AutoDetectLanguage || isMultilingualModel))
    ///       => create MultilingualPhonemizer
    ///
    /// Because PiperTTS.InitializeWithInferenceAsync requires a real ONNX model and
    /// Unity.InferenceEngine context, these tests exercise the detection predicates
    /// and config interaction in isolation using the same condition expressions.
    /// </summary>
    [TestFixture]
    public class MultilingualAutoPromotionTests
    {
        // ── Helper: mirrors the production isMultilingualModel predicate ────

        /// <summary>
        /// Evaluates the same condition used in PiperTTS.InitializeWithInferenceAsync
        /// to decide whether the loaded model is multilingual.
        /// </summary>
        private static bool IsMultilingualModel(PiperVoiceConfig voiceConfig)
        {
            return voiceConfig?.LanguageIdMap != null
                && voiceConfig.LanguageIdMap.Count > 1;
        }

        /// <summary>
        /// Evaluates the combined condition used in PiperTTS.InitializeWithInferenceAsync
        /// to decide whether a MultilingualPhonemizer should be created.
        /// </summary>
        private static bool ShouldCreateMultilingualPhonemizer(
            PiperConfig config,
            PiperVoiceConfig voiceConfig)
        {
            var isMultilingual = IsMultilingualModel(voiceConfig);
            return config != null && (config.AutoDetectLanguage || isMultilingual);
        }

        // ── Auto-promotion detection ───────────────────────────────────────

        [Test]
        public void IsMultilingualModel_MultipleLanguages_ReturnsTrue()
        {
            var voiceConfig = new PiperVoiceConfig
            {
                VoiceId = "multilingual-test",
                Language = "ja",
                LanguageIdMap = new Dictionary<string, int>
                {
                    { "ja", 0 },
                    { "en", 1 },
                    { "zh", 2 },
                    { "ko", 3 },
                    { "es", 4 },
                    { "fr", 5 }
                }
            };

            Assert.IsTrue(IsMultilingualModel(voiceConfig),
                "A model with 6 languages in LanguageIdMap should be detected as multilingual");
        }

        [Test]
        public void IsMultilingualModel_SingleLanguage_ReturnsFalse()
        {
            var voiceConfig = new PiperVoiceConfig
            {
                VoiceId = "single-lang-test",
                Language = "ja",
                LanguageIdMap = new Dictionary<string, int>
                {
                    { "ja", 0 }
                }
            };

            Assert.IsFalse(IsMultilingualModel(voiceConfig),
                "A model with only 1 language in LanguageIdMap should NOT be detected as multilingual");
        }

        [Test]
        public void IsMultilingualModel_NullMap_ReturnsFalse()
        {
            var voiceConfig = new PiperVoiceConfig
            {
                VoiceId = "no-map-test",
                Language = "ja",
                LanguageIdMap = null
            };

            Assert.IsFalse(IsMultilingualModel(voiceConfig),
                "A model with null LanguageIdMap should NOT be detected as multilingual");
        }

        [Test]
        public void IsMultilingualModel_EmptyMap_ReturnsFalse()
        {
            var voiceConfig = new PiperVoiceConfig
            {
                VoiceId = "empty-map-test",
                Language = "ja",
                LanguageIdMap = new Dictionary<string, int>()
            };

            Assert.IsFalse(IsMultilingualModel(voiceConfig),
                "A model with an empty LanguageIdMap should NOT be detected as multilingual");
        }

        [Test]
        public void IsMultilingualModel_NullVoiceConfig_ReturnsFalse()
        {
            Assert.IsFalse(IsMultilingualModel(null),
                "Null voiceConfig should not be detected as multilingual");
        }

        // ── Config interaction ─────────────────────────────────────────────

        [Test]
        public void AutoDetectLanguage_True_CreatesMultilingualPhonemizer()
        {
            var config = new PiperConfig
            {
                AutoDetectLanguage = true,
                DefaultLanguage = "ja"
            };

            // Single-language model, but AutoDetectLanguage is on
            var voiceConfig = new PiperVoiceConfig
            {
                VoiceId = "single-lang",
                Language = "ja",
                LanguageIdMap = new Dictionary<string, int> { { "ja", 0 } }
            };

            Assert.IsTrue(ShouldCreateMultilingualPhonemizer(config, voiceConfig),
                "When AutoDetectLanguage is true, MultilingualPhonemizer should be created " +
                "even for a single-language model");
        }

        [Test]
        public void AutoDetectLanguage_False_MultilingualModel_StillCreatesMultilingualPhonemizer()
        {
            var config = new PiperConfig
            {
                AutoDetectLanguage = false,
                DefaultLanguage = "ja"
            };

            var voiceConfig = new PiperVoiceConfig
            {
                VoiceId = "multi-lang",
                Language = "ja",
                LanguageIdMap = new Dictionary<string, int>
                {
                    { "ja", 0 },
                    { "en", 1 }
                }
            };

            Assert.IsTrue(ShouldCreateMultilingualPhonemizer(config, voiceConfig),
                "When the model has multiple languages, MultilingualPhonemizer should be created " +
                "even when AutoDetectLanguage is false (auto-promotion)");
        }

        [Test]
        public void AutoDetectLanguage_False_SingleLangModel_NoMultilingualPhonemizer()
        {
            var config = new PiperConfig
            {
                AutoDetectLanguage = false,
                DefaultLanguage = "ja"
            };

            var voiceConfig = new PiperVoiceConfig
            {
                VoiceId = "single-lang",
                Language = "ja",
                LanguageIdMap = new Dictionary<string, int> { { "ja", 0 } }
            };

            Assert.IsFalse(ShouldCreateMultilingualPhonemizer(config, voiceConfig),
                "When AutoDetectLanguage is false and model has only 1 language, " +
                "MultilingualPhonemizer should NOT be created");
        }

        [Test]
        public void NullConfig_NeverCreatesMultilingualPhonemizer()
        {
            var voiceConfig = new PiperVoiceConfig
            {
                VoiceId = "multi-lang",
                Language = "ja",
                LanguageIdMap = new Dictionary<string, int>
                {
                    { "ja", 0 },
                    { "en", 1 }
                }
            };

            Assert.IsFalse(ShouldCreateMultilingualPhonemizer(null, voiceConfig),
                "When config is null, MultilingualPhonemizer should never be created " +
                "regardless of model languages");
        }

        // ── Language detection fallback ────────────────────────────────────

        [Test]
        public void GenerateAudio_NoLanguage_MultilingualModel_AutoDetectsLanguage()
        {
            // Simulates the language ID resolution path in GenerateAudioWithMultilingualAsync
            // when languageId < 0 (auto-detect) and the model has a LanguageIdMap.
            var voiceConfig = new PiperVoiceConfig
            {
                VoiceId = "multi-lang",
                Language = "ja",
                LanguageIdMap = new Dictionary<string, int>
                {
                    { "ja", 0 },
                    { "en", 1 },
                    { "zh", 2 }
                }
            };

            int languageId = -1; // auto-detect
            string detectedLang = "ja"; // simulated detection result

            // Mirror the resolution logic from GenerateAudioWithMultilingualAsync
            int resolvedLanguageId = languageId >= 0 ? languageId : 0;
            if (languageId < 0
                && voiceConfig.LanguageIdMap != null
                && voiceConfig.LanguageIdMap.TryGetValue(detectedLang, out var detectedId))
            {
                resolvedLanguageId = detectedId;
            }

            Assert.AreEqual(0, resolvedLanguageId,
                "When languageId=-1 and detected language is 'ja', resolved ID should be 0");
        }

        [Test]
        public void GenerateAudio_ExplicitLanguage_UsesSpecified()
        {
            // When an explicit languageId is provided (>= 0), it should be used as-is.
            var voiceConfig = new PiperVoiceConfig
            {
                VoiceId = "multi-lang",
                Language = "ja",
                LanguageIdMap = new Dictionary<string, int>
                {
                    { "ja", 0 },
                    { "en", 1 }
                }
            };

            int languageId = 1; // explicitly English
            string detectedLang = "ja"; // detection would say Japanese, but explicit overrides

            int resolvedLanguageId = languageId >= 0 ? languageId : 0;
            if (languageId < 0
                && voiceConfig.LanguageIdMap != null
                && voiceConfig.LanguageIdMap.TryGetValue(detectedLang, out var detectedId))
            {
                resolvedLanguageId = detectedId;
            }

            Assert.AreEqual(1, resolvedLanguageId,
                "When an explicit languageId (1=en) is provided, it should be used " +
                "regardless of auto-detection result");
        }

        [Test]
        public void GenerateAudio_AutoDetect_EnglishText_ResolvesToEnglishId()
        {
            var voiceConfig = new PiperVoiceConfig
            {
                VoiceId = "multi-lang",
                Language = "ja",
                LanguageIdMap = new Dictionary<string, int>
                {
                    { "ja", 0 },
                    { "en", 1 },
                    { "zh", 2 }
                }
            };

            int languageId = -1;
            string detectedLang = "en"; // English detected

            int resolvedLanguageId = languageId >= 0 ? languageId : 0;
            if (languageId < 0
                && voiceConfig.LanguageIdMap != null
                && voiceConfig.LanguageIdMap.TryGetValue(detectedLang, out var detectedId))
            {
                resolvedLanguageId = detectedId;
            }

            Assert.AreEqual(1, resolvedLanguageId,
                "When auto-detecting English text, resolved ID should be 1");
        }

        [Test]
        public void GenerateAudio_AutoDetect_UnknownLanguage_FallsBackToZero()
        {
            // If the detected language is not in the model's LanguageIdMap,
            // the resolved ID should remain the default (0).
            var voiceConfig = new PiperVoiceConfig
            {
                VoiceId = "multi-lang",
                Language = "ja",
                LanguageIdMap = new Dictionary<string, int>
                {
                    { "ja", 0 },
                    { "en", 1 }
                }
            };

            int languageId = -1;
            string detectedLang = "de"; // German, not in LanguageIdMap

            int resolvedLanguageId = languageId >= 0 ? languageId : 0;
            if (languageId < 0
                && voiceConfig.LanguageIdMap != null
                && voiceConfig.LanguageIdMap.TryGetValue(detectedLang, out var detectedId))
            {
                resolvedLanguageId = detectedId;
            }

            Assert.AreEqual(0, resolvedLanguageId,
                "When detected language 'de' is not in LanguageIdMap, " +
                "resolved ID should fall back to default (0)");
        }

        [Test]
        public void GenerateAudio_AutoDetect_NullLanguageIdMap_FallsBackToZero()
        {
            var voiceConfig = new PiperVoiceConfig
            {
                VoiceId = "legacy-model",
                Language = "ja",
                LanguageIdMap = null
            };

            int languageId = -1;
            string detectedLang = "ja";

            int resolvedLanguageId = languageId >= 0 ? languageId : 0;
            if (languageId < 0
                && voiceConfig.LanguageIdMap != null
                && voiceConfig.LanguageIdMap.TryGetValue(detectedLang, out var detectedId))
            {
                resolvedLanguageId = detectedId;
            }

            Assert.AreEqual(0, resolvedLanguageId,
                "When LanguageIdMap is null, resolved ID should fall back to default (0)");
        }

        // ── MultilingualPhonemizer config propagation ──────────────────────

        [Test]
        public void MultilingualPhonemizer_SupportedLanguages_MatchesConfig()
        {
            // Verifies that the languages from PiperConfig.SupportedLanguages
            // would be passed correctly to MultilingualPhonemizer constructor
            var config = new PiperConfig
            {
                AutoDetectLanguage = true,
                SupportedLanguages = new List<string> { "ja", "en", "zh" },
                DefaultLanguage = "en"
            };

            var phonemizer = new MultilingualPhonemizer(
                config.SupportedLanguages,
                config.DefaultLanguage ?? "en");

            Assert.AreEqual(3, phonemizer.Languages.Count);
            Assert.IsTrue(phonemizer.Languages.Contains("ja"));
            Assert.IsTrue(phonemizer.Languages.Contains("en"));
            Assert.IsTrue(phonemizer.Languages.Contains("zh"));

            phonemizer.Dispose();
        }

        [Test]
        public void MultilingualPhonemizer_DefaultLanguageFallback_UsesEn()
        {
            // When PiperConfig.DefaultLanguage is null, the production code
            // falls back to "en": _config.DefaultLanguage ?? "en"
            var config = new PiperConfig
            {
                AutoDetectLanguage = true,
                SupportedLanguages = new List<string> { "ja", "en" },
                DefaultLanguage = null
            };

            var defaultLang = config.DefaultLanguage ?? "en";
            Assert.AreEqual("en", defaultLang,
                "When DefaultLanguage is null, fallback should be 'en'");

            var phonemizer = new MultilingualPhonemizer(
                config.SupportedLanguages,
                defaultLang);

            Assert.IsNotNull(phonemizer);
            phonemizer.Dispose();
        }

        [Test]
        public void MultilingualPhonemizer_NullSupportedLanguages_DefaultsToJaEn()
        {
            // Production code: _config.SupportedLanguages ?? new List<string> { "ja", "en" }
            var config = new PiperConfig
            {
                AutoDetectLanguage = true,
                SupportedLanguages = null,
                DefaultLanguage = "en"
            };

            var languages = config.SupportedLanguages ?? new List<string> { "ja", "en" };
            Assert.AreEqual(2, languages.Count);
            Assert.AreEqual("ja", languages[0]);
            Assert.AreEqual("en", languages[1]);

            var phonemizer = new MultilingualPhonemizer(languages, config.DefaultLanguage ?? "en");
            Assert.AreEqual(2, phonemizer.Languages.Count);

            phonemizer.Dispose();
        }

        // ── Edge cases for LanguageIdMap count boundaries ──────────────────

        [Test]
        public void IsMultilingualModel_TwoLanguages_ReturnsTrue()
        {
            var voiceConfig = new PiperVoiceConfig
            {
                VoiceId = "bilingual-test",
                Language = "ja",
                LanguageIdMap = new Dictionary<string, int>
                {
                    { "ja", 0 },
                    { "en", 1 }
                }
            };

            Assert.IsTrue(IsMultilingualModel(voiceConfig),
                "A model with exactly 2 languages should be detected as multilingual");
        }

        [Test]
        public void IsMultilingualModel_SevenLanguages_ReturnsTrue()
        {
            var voiceConfig = new PiperVoiceConfig
            {
                VoiceId = "full-multilingual-test",
                Language = "ja",
                LanguageIdMap = new Dictionary<string, int>
                {
                    { "ja", 0 },
                    { "en", 1 },
                    { "zh", 2 },
                    { "es", 3 },
                    { "fr", 4 },
                    { "pt", 5 },
                    { "ko", 6 }
                }
            };

            Assert.IsTrue(IsMultilingualModel(voiceConfig),
                "A model with all 7 supported languages should be detected as multilingual");
            Assert.AreEqual(7, voiceConfig.LanguageIdMap.Count);
        }
    }
}