using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace uPiper.Core.Phonemizers.Multilingual
{
    /// <summary>
    /// Language detection system for automatic language identification
    /// </summary>
    public class LanguageDetector
    {
        private readonly Dictionary<string, LanguageProfile> languageProfiles;
        private readonly Dictionary<string, Regex> scriptDetectors;
        private readonly int ngramSize = 3;
        private readonly float minConfidence = 0.7f;

        public LanguageDetector()
        {
            languageProfiles = new Dictionary<string, LanguageProfile>();
            scriptDetectors = new Dictionary<string, Regex>();
            InitializeProfiles();
            InitializeScriptDetectors();
        }

        /// <summary>
        /// Detect language from text
        /// </summary>
        public LanguageDetectionResult DetectLanguage(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new LanguageDetectionResult
                {
                    DetectedLanguage = "unknown",
                    Confidence = 0f,
                    IsReliable = false,
                    LanguageScores = new Dictionary<string, float>()
                };
            }

            // First, try script-based detection
            var scriptBasedResult = DetectByScript(text);
            if (scriptBasedResult != null && scriptBasedResult.Confidence > 0.9f)
            {
                return scriptBasedResult;
            }

            // Then use n-gram based detection
            var ngramBasedResult = DetectByNGrams(text);

            // Combine results if both are available
            if (scriptBasedResult != null && ngramBasedResult != null)
            {
                return CombineResults(scriptBasedResult, ngramBasedResult);
            }

            return ngramBasedResult ?? scriptBasedResult ?? new LanguageDetectionResult
            {
                DetectedLanguage = "unknown",
                Confidence = 0f,
                IsReliable = false,
                LanguageScores = new Dictionary<string, float>()
            };
        }

        /// <summary>
        /// Detect language based on script/character set
        /// </summary>
        private LanguageDetectionResult DetectByScript(string text)
        {
            var scores = new Dictionary<string, float>();

            foreach (var (script, regex) in scriptDetectors)
            {
                var matches = regex.Matches(text);
                if (matches.Count > 0)
                {
                    var coverage = (float)matches.Cast<Match>().Sum(m => m.Length) / text.Length;

                    // Map script to languages
                    var languages = GetLanguagesForScript(script);
                    foreach (var lang in languages)
                    {
                        scores[lang] = coverage;
                    }
                }
            }

            if (scores.Count == 0)
                return null;

            var topLanguage = scores.OrderByDescending(kvp => kvp.Value).First();

            return new LanguageDetectionResult
            {
                DetectedLanguage = topLanguage.Key,
                Confidence = topLanguage.Value,
                LanguageScores = scores,
                IsReliable = topLanguage.Value > minConfidence
            };
        }

        /// <summary>
        /// Detect language based on n-gram analysis
        /// </summary>
        private LanguageDetectionResult DetectByNGrams(string text)
        {
            var textNGrams = ExtractNGrams(text.ToLower(), ngramSize);
            if (textNGrams.Count == 0)
                return null;

            var scores = new Dictionary<string, float>();

            foreach (var (language, profile) in languageProfiles)
            {
                var score = CalculateSimilarity(textNGrams, profile.NGrams);
                scores[language] = score;
            }

            var topLanguages = scores.OrderByDescending(kvp => kvp.Value).ToList();
            if (topLanguages.Count == 0)
                return null;

            var topLanguage = topLanguages[0];
            var confidence = topLanguage.Value;

            // Adjust confidence based on score distribution
            if (topLanguages.Count > 1)
            {
                var secondBest = topLanguages[1].Value;
                var margin = confidence - secondBest;
                confidence = Math.Min(confidence, margin * 2); // Reduce confidence if close scores
            }

            return new LanguageDetectionResult
            {
                DetectedLanguage = topLanguage.Key,
                Confidence = confidence,
                LanguageScores = scores,
                IsReliable = confidence > minConfidence
            };
        }

        /// <summary>
        /// Extract n-grams from text
        /// </summary>
        private Dictionary<string, float> ExtractNGrams(string text, int n)
        {
            var ngrams = new Dictionary<string, int>();

            // Add word boundary markers
            text = " " + text + " ";

            for (var i = 0; i <= text.Length - n; i++)
            {
                var ngram = text.Substring(i, n);

                // Skip n-grams with only spaces
                if (string.IsNullOrWhiteSpace(ngram))
                    continue;

                if (ngrams.ContainsKey(ngram))
                    ngrams[ngram]++;
                else
                    ngrams[ngram] = 1;
            }

            // Normalize to frequencies
            var total = ngrams.Values.Sum();
            var frequencies = new Dictionary<string, float>();

            foreach (var (ngram, count) in ngrams)
            {
                frequencies[ngram] = (float)count / total;
            }

            return frequencies;
        }

        /// <summary>
        /// Calculate similarity between two n-gram profiles
        /// </summary>
        private float CalculateSimilarity(Dictionary<string, float> profile1, Dictionary<string, float> profile2)
        {
            var allNGrams = new HashSet<string>(profile1.Keys);
            allNGrams.UnionWith(profile2.Keys);

            var similarity = 0f;

            foreach (var ngram in allNGrams)
            {
                var freq1 = profile1.TryGetValue(ngram, out var f1) ? f1 : 0f;
                var freq2 = profile2.TryGetValue(ngram, out var f2) ? f2 : 0f;

                // Use cosine similarity
                similarity += freq1 * freq2;
            }

            // Normalize
            var norm1 = (float)Math.Sqrt(profile1.Values.Sum(f => f * f));
            var norm2 = (float)Math.Sqrt(profile2.Values.Sum(f => f * f));

            if (norm1 > 0 && norm2 > 0)
                similarity /= (norm1 * norm2);

            return similarity;
        }

        /// <summary>
        /// Combine script-based and n-gram based results
        /// </summary>
        private LanguageDetectionResult CombineResults(
            LanguageDetectionResult scriptResult,
            LanguageDetectionResult ngramResult)
        {
            var combinedScores = new Dictionary<string, float>();

            // Weight: 60% n-gram, 40% script (script is more definitive but less precise)
            var ngramWeight = 0.6f;
            var scriptWeight = 0.4f;

            // Add n-gram scores
            foreach (var (lang, score) in ngramResult.LanguageScores)
            {
                combinedScores[lang] = score * ngramWeight;
            }

            // Add script scores
            foreach (var (lang, score) in scriptResult.LanguageScores)
            {
                if (combinedScores.ContainsKey(lang))
                    combinedScores[lang] += score * scriptWeight;
                else
                    combinedScores[lang] = score * scriptWeight;
            }

            var topLanguage = combinedScores.OrderByDescending(kvp => kvp.Value).First();

            return new LanguageDetectionResult
            {
                DetectedLanguage = topLanguage.Key,
                Confidence = topLanguage.Value,
                LanguageScores = combinedScores,
                IsReliable = topLanguage.Value > minConfidence
            };
        }

        /// <summary>
        /// Initialize language profiles with common n-grams
        /// </summary>
        private void InitializeProfiles()
        {
            // English profile
            var englishProfile = new LanguageProfile
            {
                Language = "en-US",
                NGrams = new Dictionary<string, float>
                {
                    // Common English trigrams
                    [" th"] = 0.05f,
                    ["the"] = 0.04f,
                    ["he "] = 0.03f,
                    ["ing"] = 0.025f,
                    ["and"] = 0.02f,
                    ["nd "] = 0.018f,
                    ["ion"] = 0.017f,
                    ["tio"] = 0.016f,
                    ["ent"] = 0.015f,
                    ["ati"] = 0.014f,
                    ["for"] = 0.013f,
                    ["her"] = 0.012f,
                    ["ter"] = 0.011f,
                    ["hat"] = 0.01f,
                    ["tha"] = 0.009f,
                    ["ere"] = 0.008f,
                    ["ate"] = 0.007f,
                    ["his"] = 0.006f,
                    ["con"] = 0.005f,
                    ["res"] = 0.004f,
                    ["ver"] = 0.003f,
                    ["all"] = 0.002f,
                    ["ons"] = 0.001f
                }
            };
            languageProfiles["en-US"] = englishProfile;
            languageProfiles["en-GB"] = englishProfile; // Share profile
            languageProfiles["en-IN"] = englishProfile; // Share profile

            // Japanese profile (romanized for simplicity)
            var japaneseProfile = new LanguageProfile
            {
                Language = "ja-JP",
                NGrams = new Dictionary<string, float>
                {
                    ["no "] = 0.04f,
                    ["shi"] = 0.035f,
                    ["ka "] = 0.03f,
                    ["ni "] = 0.028f,
                    ["de "] = 0.025f,
                    ["wa "] = 0.023f,
                    ["to "] = 0.02f,
                    ["ga "] = 0.018f,
                    ["tte"] = 0.015f,
                    ["iru"] = 0.013f,
                    ["koto"] = 0.011f,
                    ["oto"] = 0.01f,
                    ["aru"] = 0.009f,
                    ["ita"] = 0.008f,
                    ["desu"] = 0.007f,
                    ["masu"] = 0.006f,
                    ["suru"] = 0.005f,
                    ["kara"] = 0.004f
                }
            };
            languageProfiles["ja-JP"] = japaneseProfile;

            // Spanish profile
            var spanishProfile = new LanguageProfile
            {
                Language = "es-ES",
                NGrams = new Dictionary<string, float>
                {
                    [" de"] = 0.05f,
                    ["de "] = 0.04f,
                    [" la"] = 0.035f,
                    ["la "] = 0.03f,
                    ["que"] = 0.028f,
                    ["ue "] = 0.025f,
                    ["ion"] = 0.02f,
                    ["cion"] = 0.018f,
                    [" el"] = 0.015f,
                    ["el "] = 0.013f,
                    ["ent"] = 0.011f,
                    ["nte"] = 0.01f,
                    ["con"] = 0.009f,
                    ["est"] = 0.008f,
                    ["los"] = 0.007f,
                    ["las"] = 0.006f,
                    ["par"] = 0.005f,
                    ["ara"] = 0.004f
                }
            };
            languageProfiles["es-ES"] = spanishProfile;

            // German profile
            var germanProfile = new LanguageProfile
            {
                Language = "de-DE",
                NGrams = new Dictionary<string, float>
                {
                    ["der"] = 0.04f,
                    ["die"] = 0.035f,
                    ["und"] = 0.03f,
                    ["den"] = 0.025f,
                    ["das"] = 0.022f,
                    ["ist"] = 0.02f,
                    ["ein"] = 0.018f,
                    ["ich"] = 0.016f,
                    ["cht"] = 0.014f,
                    ["sch"] = 0.012f,
                    ["che"] = 0.01f,
                    ["gen"] = 0.009f,
                    ["ung"] = 0.008f,
                    ["ver"] = 0.007f,
                    ["ter"] = 0.006f,
                    ["eit"] = 0.005f,
                    ["ber"] = 0.004f,
                    ["mit"] = 0.003f
                }
            };
            languageProfiles["de-DE"] = germanProfile;

            // French profile
            var frenchProfile = new LanguageProfile
            {
                Language = "fr-FR",
                NGrams = new Dictionary<string, float>
                {
                    [" de"] = 0.04f,
                    ["de "] = 0.035f,
                    [" le"] = 0.03f,
                    ["le "] = 0.025f,
                    ["ent"] = 0.022f,
                    ["que"] = 0.02f,
                    ["les"] = 0.018f,
                    [" la"] = 0.016f,
                    ["la "] = 0.014f,
                    ["tion"] = 0.012f,
                    ["ment"] = 0.01f,
                    ["pour"] = 0.009f,
                    ["dans"] = 0.008f,
                    ["des"] = 0.007f,
                    ["est"] = 0.006f,
                    ["vous"] = 0.005f,
                    ["avec"] = 0.004f,
                    ["plus"] = 0.003f
                }
            };
            languageProfiles["fr-FR"] = frenchProfile;
        }

        /// <summary>
        /// Initialize script detectors for various writing systems
        /// </summary>
        private void InitializeScriptDetectors()
        {
            // Latin script
            scriptDetectors["Latin"] = new Regex(@"[a-zA-Z]", RegexOptions.Compiled);

            // Japanese scripts
            scriptDetectors["Hiragana"] = new Regex(@"[\u3040-\u309F]", RegexOptions.Compiled);
            scriptDetectors["Katakana"] = new Regex(@"[\u30A0-\u30FF]", RegexOptions.Compiled);
            scriptDetectors["Kanji"] = new Regex(@"[\u4E00-\u9FAF]", RegexOptions.Compiled);

            // Chinese script (simplified range)
            scriptDetectors["Chinese"] = new Regex(@"[\u4E00-\u9FFF]", RegexOptions.Compiled);

            // Korean script
            scriptDetectors["Hangul"] = new Regex(@"[\uAC00-\uD7AF]", RegexOptions.Compiled);

            // Cyrillic script
            scriptDetectors["Cyrillic"] = new Regex(@"[\u0400-\u04FF]", RegexOptions.Compiled);

            // Arabic script
            scriptDetectors["Arabic"] = new Regex(@"[\u0600-\u06FF]", RegexOptions.Compiled);

            // Devanagari script (Hindi, etc.)
            scriptDetectors["Devanagari"] = new Regex(@"[\u0900-\u097F]", RegexOptions.Compiled);
        }

        /// <summary>
        /// Map scripts to potential languages
        /// </summary>
        private List<string> GetLanguagesForScript(string script)
        {
            return script switch
            {
                "Latin" => new List<string> { "en-US", "es-ES", "fr-FR", "de-DE" },
                "Hiragana" or "Katakana" or "Kanji" => new List<string> { "ja-JP" },
                "Chinese" => new List<string> { "zh-CN", "zh-TW" },
                "Hangul" => new List<string> { "ko-KR" },
                "Cyrillic" => new List<string> { "ru-RU" },
                "Arabic" => new List<string> { "ar-SA" },
                "Devanagari" => new List<string> { "hi-IN" },
                _ => new List<string>()
            };
        }

        /// <summary>
        /// Segment text into language-specific parts
        /// </summary>
        public List<TextSegment> SegmentMixedLanguageText(string text)
        {
            var segments = new List<TextSegment>();
            var currentSegment = new System.Text.StringBuilder();
            string currentLanguage = null;
            var segmentStart = 0;

            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                var charLanguage = DetectCharacterLanguage(c);

                if (currentLanguage == null)
                {
                    currentLanguage = charLanguage;
                    segmentStart = i;
                }

                if (charLanguage != currentLanguage && charLanguage != "common")
                {
                    // Language change detected
                    if (currentSegment.Length > 0)
                    {
                        var segmentText = currentSegment.ToString();
                        var detectionResult = DetectLanguage(segmentText);

                        segments.Add(new TextSegment
                        {
                            Text = segmentText,
                            Language = detectionResult.DetectedLanguage,
                            StartIndex = segmentStart,
                            EndIndex = i - 1,
                            Confidence = detectionResult.Confidence
                        });
                    }

                    currentSegment.Clear();
                    currentLanguage = charLanguage;
                    segmentStart = i;
                }

                currentSegment.Append(c);
            }

            // Add final segment
            if (currentSegment.Length > 0)
            {
                var segmentText = currentSegment.ToString();
                var detectionResult = DetectLanguage(segmentText);

                segments.Add(new TextSegment
                {
                    Text = segmentText,
                    Language = detectionResult.DetectedLanguage,
                    StartIndex = segmentStart,
                    EndIndex = text.Length - 1,
                    Confidence = detectionResult.Confidence
                });
            }

            return segments;
        }

        /// <summary>
        /// Detect language based on a single character
        /// </summary>
        private string DetectCharacterLanguage(char c)
        {
            if (char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsDigit(c))
                return "common";

            foreach (var (script, regex) in scriptDetectors)
            {
                if (regex.IsMatch(c.ToString()))
                {
                    var languages = GetLanguagesForScript(script);
                    return languages.FirstOrDefault() ?? "unknown";
                }
            }

            return "unknown";
        }

        /// <summary>
        /// Language profile for n-gram analysis
        /// </summary>
        private class LanguageProfile
        {
            public string Language { get; set; }
            public Dictionary<string, float> NGrams { get; set; } = new Dictionary<string, float>();
        }
    }

    // LanguageDetectionResult and TextSegment are defined in IMultilingualPhonemizerService.cs
}