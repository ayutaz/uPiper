using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using uPiper.Core.Phonemizers;

namespace uPiper.Core.Phonemizers.Backend
{
    /// <summary>
    /// Spanish phonemizer implementation
    /// </summary>
    public class SpanishPhonemizer : PhonemizerBackendBase
    {
        private Dictionary<string, string[]> spanishDict;
        private readonly object dictLock = new();

        public override string Name => "Spanish";
        public override string Version => "1.0.0";
        public override string License => "MIT";
        public override string[] SupportedLanguages => new[]
        {
            "es", "es-ES", "es-MX", "es-AR", "es-CO", "es-CL", "es-PE", "es-VE", "es-EC", "es-BO", "es-UY", "es-PY"
        };

        protected override async Task<bool> InitializeInternalAsync(
            PhonemizerBackendOptions options,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                try
                {
                    spanishDict = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to initialize Spanish phonemizer: {ex.Message}");
                    return false;
                }
            }, cancellationToken);
        }

        public override async Task<PhonemeResult> PhonemizeAsync(
            string text,
            string language,
            PhonemeOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                if (string.IsNullOrEmpty(text))
                {
                    return new PhonemeResult { Phonemes = new string[0] };
                }

                try
                {
                    var normalized = NormalizeText(text);
                    var words = TokenizeSpanish(normalized);
                    var phonemes = new List<string>();

                    foreach (var word in words)
                    {
                        if (string.IsNullOrWhiteSpace(word))
                        {
                            phonemes.Add("_");
                            continue;
                        }

                        string[] wordPhonemes = null;

                        lock (dictLock)
                        {
                            if (spanishDict.TryGetValue(word.ToUpper(), out var dictPhonemes))
                            {
                                wordPhonemes = dictPhonemes;
                            }
                        }

                        wordPhonemes ??= ProcessSpanishWord(word);

                        phonemes.AddRange(wordPhonemes);
                    }

                    return new PhonemeResult
                    {
                        Phonemes = phonemes.ToArray(),
                        Language = language,
                        Success = true
                    };
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error in Spanish phonemization: {ex.Message}");
                    throw;
                }
            }, cancellationToken);
        }

        private string NormalizeText(string text)
        {
            return text.Trim();
        }

        private List<string> TokenizeSpanish(string text)
        {
            var words = new List<string>();
            var currentWord = "";

            foreach (var c in text)
            {
                if (char.IsLetter(c) || c == '\'' || c == '-' || c == 'ñ' || c == 'Ñ')
                {
                    currentWord += c;
                }
                else
                {
                    if (!string.IsNullOrEmpty(currentWord))
                    {
                        words.Add(currentWord);
                        currentWord = "";
                    }

                    if (char.IsPunctuation(c) && c != '\'' && c != '-')
                    {
                        words.Add(c.ToString());
                    }
                }
            }

            if (!string.IsNullOrEmpty(currentWord))
            {
                words.Add(currentWord);
            }

            return words;
        }

        private string[] ProcessSpanishWord(string word)
        {
            // Simple phoneme generation for Spanish
            var phonemes = new List<string>();
            var lowerWord = word.ToLower();

            for (var i = 0; i < lowerWord.Length; i++)
            {
                var ch = lowerWord[i];

                // Check for digraphs first
                if (i < lowerWord.Length - 1)
                {
                    var digraph = lowerWord.Substring(i, 2);
                    if (digraph == "ll")
                    {
                        phonemes.Add("ʎ");
                        i++; // Skip next character
                        continue;
                    }
                    else if (digraph == "rr")
                    {
                        phonemes.Add("r");
                        i++; // Skip next character
                        continue;
                    }
                    else if (digraph == "ch")
                    {
                        phonemes.Add("tʃ");
                        i++; // Skip next character
                        continue;
                    }
                }

                // Basic Spanish G2P rules for single characters
                switch (ch)
                {
                    case 'a': phonemes.Add("a"); break;
                    case 'e': phonemes.Add("e"); break;
                    case 'i': phonemes.Add("i"); break;
                    case 'o': phonemes.Add("o"); break;
                    case 'u': phonemes.Add("u"); break;
                    case 'ñ': phonemes.Add("ɲ"); break;
                    case 'j': phonemes.Add("x"); break;
                    case 'r': phonemes.Add("ɾ"); break;
                    case 'v': phonemes.Add("b"); break;
                    case 'b': phonemes.Add("b"); break;
                    case 'l': phonemes.Add("l"); break;
                    case 'c': phonemes.Add("k"); break;
                    case 's': phonemes.Add("s"); break;
                    case 'n': phonemes.Add("n"); break;
                    case 'm': phonemes.Add("m"); break;
                    case 'p': phonemes.Add("p"); break;
                    case 't': phonemes.Add("t"); break;
                    case 'd': phonemes.Add("d"); break;
                    case 'f': phonemes.Add("f"); break;
                    case 'g': phonemes.Add("g"); break;
                    case 'h': break; // Silent in Spanish
                    case 'k': phonemes.Add("k"); break;
                    case 'w': phonemes.Add("w"); break;
                    case 'x': phonemes.Add("ks"); break;
                    case 'y': phonemes.Add("j"); break;
                    case 'z': phonemes.Add("θ"); break; // Spain Spanish
                    default:
                        if (char.IsLetter(ch))
                            phonemes.Add(ch.ToString());
                        break;
                }
            }
            return phonemes.ToArray();
        }

        public override long GetMemoryUsage()
        {
            return spanishDict?.Count * 80 ?? 0;
        }

        public override BackendCapabilities GetCapabilities()
        {
            return new BackendCapabilities
            {
                SupportsIPA = true,
                SupportsStress = true,
                SupportsSyllables = true,
                SupportsTones = false,
                SupportsDuration = false,
                SupportsBatchProcessing = false,
                IsThreadSafe = true,
                RequiresNetwork = false
            };
        }

        protected override void DisposeInternal()
        {
            spanishDict?.Clear();
        }
    }
}