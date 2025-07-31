using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using uPiper.Core.Phonemizers.Backend;

namespace uPiper.Core.Phonemizers.Backend.Flite
{
    /// <summary>
    /// Flite-based phonemizer backend (MIT/BSD licensed)
    /// Provides lightweight phonemization for English and other languages
    /// </summary>
    public class FlitePhonemizerBackend : uPiper.Core.Phonemizers.Backend.PhonemizerBackendBase
    {
        private readonly FliteLexicon lexicon;
        private readonly FliteLetterToSound lts;
        private readonly Dictionary<string, IFliteVoice> voices;
        private readonly object syncLock = new();

        public override string Name => "Flite";
        public override string Version => "1.0.0";
        public override string License => "MIT/BSD";
        public override string[] SupportedLanguages => new[] { "en-US", "en-GB", "en-IN" };

        public FlitePhonemizerBackend()
        {
            lexicon = new FliteLexicon();
            lts = new FliteLetterToSound();
            voices = new Dictionary<string, IFliteVoice>
            {
                ["en-US"] = new FliteUSEnglishVoice(),
                ["en-GB"] = new FliteBritishEnglishVoice(),
                ["en-IN"] = new FliteIndianEnglishVoice()
            };
        }

        public override async Task<PhonemeResult> PhonemizeAsync(
            string text,
            string language,
            PhonemeOptions options = null,
            CancellationToken cancellationToken = default)
        {
            if (!SupportsLanguage(language))
            {
                throw new NotSupportedException($"Language {language} is not supported by Flite backend");
            }

            return await Task.Run(() =>
            {
                lock (syncLock)
                {
                    return PhonemizeInternal(text, language, options ?? new PhonemeOptions());
                }
            }, cancellationToken);
        }

        private PhonemeResult PhonemizeInternal(string text, string language, PhonemeOptions options)
        {
            var voice = voices[language];
            var result = new PhonemeResult
            {
                Language = language,
                Success = true
            };

            // Normalize and tokenize text
            var normalizedText = voice.NormalizeText(text);
            var tokens = TokenizeText(normalizedText);

            var phonemeList = new List<string>();
            var stressList = options.IncludeStress ? new List<int>() : null;
            var durationList = options.IncludeDurations ? new List<float>() : null;
            var wordBoundaryList = options.IncludeWordBoundaries ? new List<int>() : null;

            var phonemeIndex = 0;
            foreach (var token in tokens)
            {

                if (IsWord(token))
                {
                    // Try lexicon lookup first
                    var phonemes = lexicon.Lookup(token.ToLower(), language);

                    if (phonemes == null || phonemes.Count == 0)
                    {
                        // Fall back to letter-to-sound rules
                        phonemes = lts.GeneratePhonemes(token, language);
                    }

                    // Apply voice-specific modifications
                    phonemes = voice.ModifyPhonemes(phonemes);

                    // Add word boundary if requested
                    if (options.IncludeWordBoundaries && phonemeList.Count > 0)
                    {
                        wordBoundaryList.Add(phonemeIndex);
                    }

                    // Process each phoneme
                    foreach (var phoneme in phonemes)
                    {
                        // Extract stress if present
                        var (cleanPhoneme, stress) = ExtractStress(phoneme);

                        phonemeList.Add(cleanPhoneme);

                        if (options.IncludeStress)
                        {
                            stressList.Add(stress);
                        }

                        if (options.IncludeDurations)
                        {
                            // Estimate duration based on phoneme type
                            var duration = EstimatePhonemeDuration(cleanPhoneme, stress);
                            durationList.Add(duration);
                        }

                        phonemeIndex++;
                    }
                }
                else if (IsPunctuation(token))
                {
                    // Handle punctuation as silence
                    phonemeList.Add("pau");

                    if (options.IncludeStress)
                    {
                        stressList.Add(0);
                    }

                    if (options.IncludeDurations)
                    {
                        var pauseDuration = GetPauseDuration(token);
                        durationList.Add(pauseDuration);
                    }

                    phonemeIndex++;
                }
            }

            // Set arrays on result
            result.Phonemes = phonemeList.ToArray();
            if (stressList != null) result.Stresses = stressList.ToArray();
            if (durationList != null) result.Durations = durationList.ToArray();
            if (wordBoundaryList != null) result.WordBoundaries = wordBoundaryList.ToArray();

            result.Backend = Name;
            result.Metadata = new Dictionary<string, object>
            {
                ["backend"] = Name,
                ["voice"] = voice.Name
            };

            return result;
        }

        private List<string> TokenizeText(string text)
        {
            // Simple tokenizer that preserves punctuation
            var tokens = new List<string>();
            var pattern = @"(\w+|[^\w\s])";
            var matches = Regex.Matches(text, pattern);

            foreach (Match match in matches)
            {
                tokens.Add(match.Value);
            }

            return tokens;
        }

        private bool IsWord(string token)
        {
            return Regex.IsMatch(token, @"^\w+$");
        }

        private bool IsPunctuation(string token)
        {
            return Regex.IsMatch(token, @"^[.,!?;:]$");
        }

        private (string phoneme, int stress) ExtractStress(string phonemeWithStress)
        {
            // Flite uses numbers after vowels to indicate stress (0, 1, 2)
            var match = Regex.Match(phonemeWithStress, @"^(.+?)(\d?)$");
            if (match.Success && !string.IsNullOrEmpty(match.Groups[2].Value))
            {
                return (match.Groups[1].Value, int.Parse(match.Groups[2].Value));
            }
            return (phonemeWithStress, 0);
        }

        private float EstimatePhonemeDuration(string phoneme, int stress)
        {
            // Basic duration estimation in seconds
            var baseDuration = 0.08f;

            // Vowels are longer
            if (IsVowel(phoneme))
            {
                baseDuration = 0.12f;
                // Stressed vowels are even longer
                if (stress == 1) baseDuration *= 1.3f;
                else if (stress == 2) baseDuration *= 1.1f;
            }
            // Stops are shorter
            else if (IsStop(phoneme))
            {
                baseDuration = 0.05f;
            }
            // Fricatives
            else if (IsFricative(phoneme))
            {
                baseDuration = 0.1f;
            }

            return baseDuration;
        }

        private float GetPauseDuration(string punctuation)
        {
            return punctuation switch
            {
                "." => 0.3f,
                "!" => 0.3f,
                "?" => 0.3f,
                "," => 0.15f,
                ";" => 0.2f,
                ":" => 0.2f,
                _ => 0.1f
            };
        }

        private bool IsVowel(string phoneme)
        {
            var vowels = new HashSet<string> { "aa", "ae", "ah", "ao", "aw", "ax", "ay",
                                               "eh", "er", "ey", "ih", "iy", "ow", "oy",
                                               "uh", "uw", "ux" };
            return vowels.Contains(phoneme.ToLower());
        }

        private bool IsStop(string phoneme)
        {
            var stops = new HashSet<string> { "p", "b", "t", "d", "k", "g" };
            return stops.Contains(phoneme.ToLower());
        }

        private bool IsFricative(string phoneme)
        {
            var fricatives = new HashSet<string> { "f", "v", "th", "dh", "s", "z", "sh", "zh", "h" };
            return fricatives.Contains(phoneme.ToLower());
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lexicon?.Dispose();
                lts?.Dispose();

                foreach (var voice in voices.Values)
                {
                    voice?.Dispose();
                }
                voices.Clear();
            }

            base.Dispose(disposing);
        }

        protected override Task<bool> InitializeInternalAsync(PhonemizerBackendOptions options, CancellationToken cancellationToken)
        {
            // Flite is self-contained and doesn't require external data files
            return Task.FromResult(true);
        }

        // ValidateAsync is not an abstract method in the base class

        protected override void DisposeInternal()
        {
            lexicon?.Dispose();
            lts?.Dispose();
            foreach (var voice in voices.Values)
            {
                voice.Dispose();
            }
            voices.Clear();
        }

        public override long GetMemoryUsage()
        {
            // Estimate memory usage
            return 1024 * 1024; // 1MB estimate
        }

        public override BackendCapabilities GetCapabilities()
        {
            return new BackendCapabilities
            {
                SupportsIPA = true,
                SupportsStress = true,
                SupportsSyllables = false,
                SupportsTones = false,
                SupportsDuration = false,
                SupportsBatchProcessing = false,
                IsThreadSafe = true,
                RequiresNetwork = false
            };
        }
    }

    /// <summary>
    /// Interface for Flite voice implementations
    /// </summary>
    public interface IFliteVoice : IDisposable
    {
        public string Name { get; }
        public string Language { get; }
        public string NormalizeText(string text);
        public List<string> ModifyPhonemes(List<string> phonemes);
    }

    /// <summary>
    /// US English voice implementation
    /// </summary>
    public class FliteUSEnglishVoice : IFliteVoice
    {
        public string Name => "cmu_us_generic";
        public string Language => "en-US";

        public string NormalizeText(string text)
        {
            // Basic text normalization for US English
            text = text.ToLower();
            text = Regex.Replace(text, @"\s+", " ");
            text = text.Trim();
            return text;
        }

        public List<string> ModifyPhonemes(List<string> phonemes)
        {
            // US English specific phoneme modifications
            return phonemes;
        }

        public void Dispose()
        {
            // Cleanup if needed
        }
    }

    /// <summary>
    /// British English voice implementation
    /// </summary>
    public class FliteBritishEnglishVoice : IFliteVoice
    {
        public string Name => "cmu_gb_generic";
        public string Language => "en-GB";

        public string NormalizeText(string text)
        {
            text = text.ToLower();
            text = Regex.Replace(text, @"\s+", " ");
            text = text.Trim();
            return text;
        }

        public List<string> ModifyPhonemes(List<string> phonemes)
        {
            // British English specific modifications
            // For example, modify 'r' sounds
            var modified = new List<string>();
            foreach (var phoneme in phonemes)
            {
                if (phoneme == "er" || phoneme == "r")
                {
                    // British English often drops 'r' in certain contexts
                    // This is simplified - real implementation would be more complex
                    modified.Add("ah");
                }
                else
                {
                    modified.Add(phoneme);
                }
            }
            return modified;
        }

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// Indian English voice implementation
    /// </summary>
    public class FliteIndianEnglishVoice : IFliteVoice
    {
        public string Name => "cmu_in_generic";
        public string Language => "en-IN";

        public string NormalizeText(string text)
        {
            text = text.ToLower();
            text = Regex.Replace(text, @"\s+", " ");
            text = text.Trim();
            return text;
        }

        public List<string> ModifyPhonemes(List<string> phonemes)
        {
            // Indian English specific modifications
            return phonemes;
        }

        public void Dispose()
        {
        }
    }
}