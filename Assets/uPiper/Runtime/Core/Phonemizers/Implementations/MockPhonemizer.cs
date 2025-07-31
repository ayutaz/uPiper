using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;
using uPiper.Core.Phonemizers.Backend;

namespace uPiper.Core.Phonemizers.Implementations
{
    /// <summary>
    /// Mock phonemizer for platforms without native library support.
    /// Provides basic phoneme generation for testing and demos.
    /// </summary>
    [Preserve]
    public class MockPhonemizer : BasePhonemizer
    {
        private static readonly Dictionary<string, string[]> SimplePhonemeMap = new()
        {
            // Japanese hiragana
            { "こ", new[] { "k", "o" } },
            { "ん", new[] { "N" } },
            { "に", new[] { "n", "i" } },
            { "ち", new[] { "ch", "i" } },
            { "は", new[] { "h", "a" } },
            { "世", new[] { "s", "e" } },
            { "界", new[] { "k", "a", "i" } },
            { "こんにちは", new[] { "k", "o", "N", "n", "i", "ch", "i", "w", "a" } },
            { "世界", new[] { "s", "e", "k", "a", "i" } },
            { "テスト", new[] { "t", "e", "s", "u", "t", "o" } },
            { "です", new[] { "d", "e", "s", "u" } },
            
            // English basic words
            { "hello", new[] { "h", "e", "l", "o" } },
            { "world", new[] { "w", "o", "r", "l", "d" } },
            { "test", new[] { "t", "e", "s", "t" } },
        };

        public override string Name => "MockPhonemizer";
        public override string Version => "1.0.0";
        public override string[] SupportedLanguages => new[] { "ja_JP", "en_US" };

        public MockPhonemizer() : base()
        {
        }

        protected override void InitializeLanguages()
        {
            // Initialize supported languages
            _languageInfos["ja_JP"] = new LanguageInfo
            {
                Code = "ja_JP",
                Name = "Japanese",
                NativeName = "日本語",
                RequiresPreprocessing = true,
                SupportsAccent = true
            };

            _languageInfos["en_US"] = new LanguageInfo
            {
                Code = "en_US",
                Name = "English",
                NativeName = "English",
                RequiresPreprocessing = false,
                SupportsAccent = false
            };
        }

        protected override async Task<PhonemeResult> PhonemizeInternalAsync(
            string text,
            string language,
            CancellationToken cancellationToken)
        {
            await Task.Yield(); // Make it truly async

            // Simple character-based phoneme generation
            var phonemes = new List<string>();
            var phonemeIds = new List<int>();
            var durations = new List<float>();

            // Try to match known phrases first
            var lowerText = text.ToLower();
            if (SimplePhonemeMap.ContainsKey(lowerText))
            {
                phonemes.AddRange(SimplePhonemeMap[lowerText]);
            }
            else if (SimplePhonemeMap.ContainsKey(text))
            {
                phonemes.AddRange(SimplePhonemeMap[text]);
            }
            else
            {
                // Fall back to character-based generation
                foreach (var c in text)
                {
                    if (char.IsWhiteSpace(c))
                    {
                        phonemes.Add("pau"); // pause
                    }
                    else if (char.IsLetter(c))
                    {
                        phonemes.Add(c.ToString().ToLower());
                    }
                }
            }

            // Generate simple IDs and durations
            for (var i = 0; i < phonemes.Count; i++)
            {
                phonemeIds.Add(i + 1); // Simple sequential IDs
                durations.Add(0.1f); // Fixed duration for simplicity
            }

            var result = new PhonemeResult
            {
                OriginalText = text,
                Language = language,
                Phonemes = phonemes.ToArray(),
                PhonemeIds = phonemeIds.ToArray(),
                Durations = durations.ToArray(),
                ProcessingTime = TimeSpan.FromMilliseconds(1),
                FromCache = false
            };

            Debug.Log($"[MockPhonemizer] Generated {phonemes.Count} phonemes for text: {text}");
            Debug.Log($"[MockPhonemizer] Phonemes: {string.Join(" ", phonemes)}");

            return result;
        }

    }
}