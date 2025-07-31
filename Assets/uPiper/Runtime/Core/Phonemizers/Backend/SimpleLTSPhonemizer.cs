using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using uPiper.Core.Phonemizers.Backend.RuleBased;
using Debug = UnityEngine.Debug;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace uPiper.Core.Phonemizers.Backend
{
    /// <summary>
    /// Simple Letter-to-Sound phonemizer for English.
    /// Provides basic G2P rules without external dependencies.
    /// </summary>
    public class SimpleLTSPhonemizer : PhonemizerBackendBase
    {
        private CMUDictionary cmuDictionary;
        private readonly Dictionary<string, string[]> ltsCache = new();
        private readonly object lockObject = new();

        public override string Name => "SimpleLTS";
        public override string Version => "1.0.0";
        public override string License => "MIT";
        public override string[] SupportedLanguages => new[] { "en", "en-US", "en-GB" };

        protected override async Task<bool> InitializeInternalAsync(
            PhonemizerBackendOptions options,
            CancellationToken cancellationToken)
        {
            try
            {
                // Load CMU dictionary
                cmuDictionary = new CMUDictionary();
                var dictPath = options?.DataPath ?? GetDefaultDictionaryPath();
                await cmuDictionary.LoadAsync(dictPath, cancellationToken);

                Priority = 120; // Higher than RuleBased, lower than Flite
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize SimpleLTSPhonemizer: {ex.Message}");
                return false;
            }
        }

        public override async Task<PhonemeResult> PhonemizeAsync(
            string text,
            string language,
            PhonemeOptions options = null,
            CancellationToken cancellationToken = default)
        {
            // Run synchronous code on a background thread to avoid blocking
            return await Task.Run(() =>
            {
                var stopwatch = Stopwatch.StartNew();

                try
                {
                    EnsureInitialized();

                    // Handle empty text as a special case - return empty result, not error
                    if (string.IsNullOrEmpty(text))
                    {
                        stopwatch.Stop();
                        return new PhonemeResult
                        {
                            OriginalText = text,
                            Phonemes = new string[0],
                            PhonemeIds = new int[0],
                            Language = language,
                            Success = true,
                            Backend = Name,
                            ProcessingTimeMs = (float)stopwatch.ElapsedMilliseconds,
                            ProcessingTime = stopwatch.Elapsed
                        };
                    }

                    if (!ValidateInput(text, language, out var error))
                    {
                        return CreateErrorResult(error, language);
                    }

                    options ??= PhonemeOptions.Default;

                    var phonemes = new List<string>();
                    var words = TokenizeText(text);

                    foreach (var word in words)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (IsPunctuation(word))
                        {
                            phonemes.Add("_"); // Silence
                            continue;
                        }

                        var cleanWord = CleanWord(word);
                        if (string.IsNullOrEmpty(cleanWord))
                            continue;

                        // Try dictionary first
                        if (cmuDictionary != null && cmuDictionary.TryGetPronunciation(cleanWord, out var dictPhonemes))
                        {
                            phonemes.AddRange(ProcessPhonemes(dictPhonemes, options));
                        }
                        else
                        {
                            // Apply simple LTS rules
                            var ltsPhonemes = ApplyLTSRules(cleanWord);
                            phonemes.AddRange(ProcessPhonemes(ltsPhonemes, options));
                        }
                    }

                    stopwatch.Stop();

                    return new PhonemeResult
                    {
                        OriginalText = text,
                        Phonemes = phonemes.ToArray(),
                        PhonemeIds = ConvertToPhonemeIds(phonemes),
                        Language = language,
                        Success = true,
                        Backend = Name,
                        ProcessingTimeMs = (float)stopwatch.ElapsedMilliseconds
                    };
                }
                catch (Exception ex)
                {
                    return CreateErrorResult($"Phonemization failed: {ex.Message}", language);
                }
            }, cancellationToken);
        }

        private string[] ApplyLTSRules(string word)
        {
            // Check cache
            lock (lockObject)
            {
                if (ltsCache.TryGetValue(word, out var cached))
                    return cached;
            }

            var phonemes = new List<string>();
            var lowWord = word.ToLower();
            var i = 0;

            while (i < lowWord.Length)
            {
                // Multi-character patterns
                if (i < lowWord.Length - 1)
                {
                    var twoChar = lowWord.Substring(i, Math.Min(2, lowWord.Length - i));
                    var threeChar = i < lowWord.Length - 2 ? lowWord.Substring(i, Math.Min(3, lowWord.Length - i)) : "";

                    // Common trigraphs
                    if (threeChar == "igh" && IsVowelContext(lowWord, i + 3))
                    {
                        phonemes.Add("AY");
                        i += 3;
                        continue;
                    }

                    // Common digraphs
                    switch (twoChar)
                    {
                        case "ch":
                            phonemes.Add("CH");
                            i += 2;
                            continue;
                        case "sh":
                            phonemes.Add("SH");
                            i += 2;
                            continue;
                        case "th":
                            // Simplified: use voiced TH in common words
                            if (IsCommonVoicedTH(lowWord))
                                phonemes.Add("DH");
                            else
                                phonemes.Add("TH");
                            i += 2;
                            continue;
                        case "ph":
                            phonemes.Add("F");
                            i += 2;
                            continue;
                        case "gh":
                            // Usually silent
                            i += 2;
                            continue;
                        case "ck":
                            phonemes.Add("K");
                            i += 2;
                            continue;
                        case "ng":
                            phonemes.Add("NG");
                            i += 2;
                            continue;
                        case "qu":
                            phonemes.Add("K");
                            phonemes.Add("W");
                            i += 2;
                            continue;
                    }
                }

                // Single character rules
                var ch = lowWord[i];
                var context = GetContext(lowWord, i);

                switch (ch)
                {
                    // Vowels
                    case 'a':
                        if (context.NextChar == 'r')
                            phonemes.Add("AA");
                        else if (context.IsEndOfWord || (context.NextChar == 'e' && context.NextNextChar == '\0'))
                            phonemes.Add("EY");
                        else
                            phonemes.Add("AE");
                        break;

                    case 'e':
                        if (context.IsEndOfWord && i > 0)
                        {
                            // Silent E at end
                            if (i > 1 && IsConsonant(lowWord[i - 1]) && IsVowel(lowWord[i - 2]))
                            {
                                // Magic E pattern (make -> "meyk")
                                // Already handled by previous vowel
                            }
                        }
                        else if (context.NextChar == 'r')
                            phonemes.Add("ER");
                        else
                            phonemes.Add("EH");
                        break;

                    case 'i':
                        if (context.NextChar == 'r')
                            phonemes.Add("ER");
                        else if (context.IsEndOfWord || IsLongIContext(lowWord, i))
                            phonemes.Add("AY");
                        else
                            phonemes.Add("IH");
                        break;

                    case 'o':
                        if (context.NextChar == 'r')
                            phonemes.Add("AO");
                        else if (context.NextChar == 'w')
                        {
                            phonemes.Add("OW");
                            i++; // Skip 'w'
                        }
                        else if (context.IsEndOfWord || IsLongOContext(lowWord, i))
                            phonemes.Add("OW");
                        else
                            phonemes.Add("AA");
                        break;

                    case 'u':
                        if (context.NextChar == 'r')
                            phonemes.Add("ER");
                        else if (IsLongUContext(lowWord, i))
                            phonemes.Add("UW");
                        else
                            phonemes.Add("AH");
                        break;

                    case 'y':
                        if (i == 0)
                            phonemes.Add("Y");
                        else if (context.IsEndOfWord)
                            phonemes.Add("IY");
                        else if (IsVowel(context.PrevChar))
                            phonemes.Add("Y");
                        else
                            phonemes.Add("IH");
                        break;

                    // Consonants
                    case 'b': phonemes.Add("B"); break;
                    case 'c':
                        if (context.NextChar == 'e' || context.NextChar == 'i' || context.NextChar == 'y')
                            phonemes.Add("S");
                        else
                            phonemes.Add("K");
                        break;
                    case 'd': phonemes.Add("D"); break;
                    case 'f': phonemes.Add("F"); break;
                    case 'g':
                        if (context.NextChar == 'e' || context.NextChar == 'i' || context.NextChar == 'y')
                            phonemes.Add("JH");
                        else
                            phonemes.Add("G");
                        break;
                    case 'h': phonemes.Add("HH"); break;
                    case 'j': phonemes.Add("JH"); break;
                    case 'k': phonemes.Add("K"); break;
                    case 'l': phonemes.Add("L"); break;
                    case 'm': phonemes.Add("M"); break;
                    case 'n': phonemes.Add("N"); break;
                    case 'p': phonemes.Add("P"); break;
                    case 'r': phonemes.Add("R"); break;
                    case 's':
                        if (context.PrevChar != '\0' && IsVowel(context.PrevChar) &&
                            context.NextChar != '\0' && IsVowel(context.NextChar))
                            phonemes.Add("Z");
                        else
                            phonemes.Add("S");
                        break;
                    case 't': phonemes.Add("T"); break;
                    case 'v': phonemes.Add("V"); break;
                    case 'w': phonemes.Add("W"); break;
                    case 'x':
                        if (i == 0)
                            phonemes.Add("Z");
                        else
                        {
                            phonemes.Add("K");
                            phonemes.Add("S");
                        }
                        break;
                    case 'z': phonemes.Add("Z"); break;
                }

                i++;
            }

            // Cache result
            lock (lockObject)
            {
                if (ltsCache.Count > 10000)
                    ltsCache.Clear(); // Simple cache eviction
                ltsCache[word] = phonemes.ToArray();
            }

            return phonemes.ToArray();
        }

        private struct CharContext
        {
            public char PrevChar;
            public char NextChar;
            public char NextNextChar;
            public bool IsEndOfWord;
        }

        private CharContext GetContext(string word, int index)
        {
            return new CharContext
            {
                PrevChar = index > 0 ? word[index - 1] : '\0',
                NextChar = index < word.Length - 1 ? word[index + 1] : '\0',
                NextNextChar = index < word.Length - 2 ? word[index + 2] : '\0',
                IsEndOfWord = index == word.Length - 1
            };
        }

        private bool IsVowel(char c)
        {
            return "aeiouAEIOU".Contains(c);
        }

        private bool IsConsonant(char c)
        {
            return char.IsLetter(c) && !IsVowel(c);
        }

        private bool IsVowelContext(string word, int index)
        {
            return index < word.Length && IsVowel(word[index]);
        }

        private bool IsLongIContext(string word, int i)
        {
            // Simple heuristic for long I
            if (i == word.Length - 1) return true;
            if (i < word.Length - 2 && word[i + 1] == 'n' && word[i + 2] == 'd') return true;
            if (i < word.Length - 2 && word[i + 1] == 'g' && word[i + 2] == 'h') return true;
            return false;
        }

        private bool IsLongOContext(string word, int i)
        {
            // Simple heuristic for long O
            if (i == word.Length - 1) return true;
            if (i < word.Length - 2 && word[i + 1] == 'l' && word[i + 2] == 'd') return true;
            if (i < word.Length - 1 && word[i + 1] == 'w') return true;
            return false;
        }

        private bool IsLongUContext(string word, int i)
        {
            // Simple heuristic for long U
            if (i < word.Length - 1 && word[i + 1] == 'e') return true;
            if (i < word.Length - 2 && word.Substring(i, 3) == "ute") return true;
            return false;
        }

        private bool IsCommonVoicedTH(string word)
        {
            // Common words with voiced TH
            var voicedWords = new HashSet<string>
            {
                "the", "this", "that", "these", "those", "them", "they",
                "their", "there", "then", "than", "thus", "though"
            };
            return voicedWords.Contains(word);
        }

        private string[] ProcessPhonemes(string[] phonemes, PhonemeOptions options)
        {
            if (options.Format == PhonemeFormat.IPA)
            {
                return phonemes.Select(p => ConvertToIPA(p)).ToArray();
            }
            return phonemes;
        }

        private string ConvertToIPA(string arpabet)
        {
            // Remove stress markers for conversion
            var basePhoneme = Regex.Replace(arpabet, @"\d+$", "");

            return ArpabetToIpaMap.TryGetValue(basePhoneme, out var ipa) ? ipa : arpabet.ToLower();
        }

        private string[] TokenizeText(string text)
        {
            var pattern = @"(\w+|[^\w\s])";
            var matches = Regex.Matches(text, pattern);
            return matches.Cast<Match>().Select(m => m.Value).ToArray();
        }

        private bool IsPunctuation(string token)
        {
            return token.Length == 1 && char.IsPunctuation(token[0]);
        }

        private string CleanWord(string word)
        {
            return Regex.Replace(word, @"[^a-zA-Z]", "").ToUpper();
        }

        private int[] ConvertToPhonemeIds(List<string> phonemes)
        {
            return phonemes.Select(p => PhonemeToIdMap.TryGetValue(p, out var id) ? id : 0).ToArray();
        }

        private string GetDefaultDictionaryPath()
        {
            return System.IO.Path.Combine(
                Application.streamingAssetsPath,
                "uPiper",
                "Phonemizers",
                "cmudict-0.7b.txt"
            );
        }

        public override long GetMemoryUsage()
        {
            long total = 0;
            if (cmuDictionary != null)
                total += cmuDictionary.GetMemoryUsage();

            lock (lockObject)
            {
                foreach (var kvp in ltsCache)
                {
                    total += kvp.Key.Length * 2;
                    total += kvp.Value.Sum(p => p.Length * 2);
                }
            }
            return total;
        }

        public override BackendCapabilities GetCapabilities()
        {
            return new BackendCapabilities
            {
                SupportsIPA = true,
                SupportsStress = false,
                SupportsSyllables = false,
                SupportsTones = false,
                SupportsDuration = false,
                SupportsBatchProcessing = true,
                IsThreadSafe = true,
                RequiresNetwork = false
            };
        }

        protected override void DisposeInternal()
        {
            cmuDictionary?.Dispose();
            ltsCache?.Clear();
        }

        // ARPABET to IPA mapping
        private static readonly Dictionary<string, string> ArpabetToIpaMap = new()
        {
            ["AA"] = "ɑ",
            ["AE"] = "æ",
            ["AH"] = "ʌ",
            ["AO"] = "ɔ",
            ["AW"] = "aʊ",
            ["AY"] = "aɪ",
            ["EH"] = "ɛ",
            ["ER"] = "ɝ",
            ["EY"] = "eɪ",
            ["IH"] = "ɪ",
            ["IY"] = "i",
            ["OW"] = "oʊ",
            ["OY"] = "ɔɪ",
            ["UH"] = "ʊ",
            ["UW"] = "u",
            ["B"] = "b",
            ["CH"] = "tʃ",
            ["D"] = "d",
            ["DH"] = "ð",
            ["F"] = "f",
            ["G"] = "g",
            ["HH"] = "h",
            ["JH"] = "dʒ",
            ["K"] = "k",
            ["L"] = "l",
            ["M"] = "m",
            ["N"] = "n",
            ["NG"] = "ŋ",
            ["P"] = "p",
            ["R"] = "r",
            ["S"] = "s",
            ["SH"] = "ʃ",
            ["T"] = "t",
            ["TH"] = "θ",
            ["V"] = "v",
            ["W"] = "w",
            ["Y"] = "j",
            ["Z"] = "z",
            ["ZH"] = "ʒ",
            ["_"] = "_" // Silence
        };

        private static readonly Dictionary<string, int> PhonemeToIdMap = new()
        {
            ["_"] = 0,
            ["ɑ"] = 1,
            ["æ"] = 2,
            ["ʌ"] = 3,
            ["ɔ"] = 4,
            ["aʊ"] = 5,
            ["aɪ"] = 6,
            ["ɛ"] = 7,
            ["ɝ"] = 8,
            ["eɪ"] = 9,
            ["ɪ"] = 10,
            ["i"] = 11,
            ["oʊ"] = 12,
            ["ɔɪ"] = 13,
            ["ʊ"] = 14,
            ["u"] = 15,
            ["b"] = 16,
            ["tʃ"] = 17,
            ["d"] = 18,
            ["ð"] = 19,
            ["f"] = 20,
            ["g"] = 21,
            ["h"] = 22,
            ["dʒ"] = 23,
            ["k"] = 24,
            ["l"] = 25,
            ["m"] = 26,
            ["n"] = 27,
            ["ŋ"] = 28,
            ["p"] = 29,
            ["r"] = 30,
            ["s"] = 31,
            ["ʃ"] = 32,
            ["t"] = 33,
            ["θ"] = 34,
            ["v"] = 35,
            ["w"] = 36,
            ["j"] = 37,
            ["z"] = 38,
            ["ʒ"] = 39
        };
    }
}