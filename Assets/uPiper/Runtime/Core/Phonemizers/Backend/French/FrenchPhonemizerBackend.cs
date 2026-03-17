using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace uPiper.Core.Phonemizers.Backend.French
{
    /// <summary>
    /// Rule-based French phonemizer backend for Piper TTS.
    /// Converts French text to IPA phonemes using grapheme-to-phoneme rules.
    /// No external G2P engine required - pure C# implementation.
    ///
    /// Handles: nasal vowels, vowel/consonant digraphs, silent letters, e muet,
    /// liaison rules, stress placement, -er verb endings, ille/il exceptions,
    /// context-dependent x handling, semi-vowels, and all major French pronunciation rules.
    /// </summary>
    public class FrenchPhonemizerBackend : PhonemizerBackendBase
    {
        // ---------------------------------------------------------------
        // PUA (Private Use Area) codepoints for multi-character phonemes
        // These must match token_mapper.py FIXED_PUA_MAPPING
        // ---------------------------------------------------------------
        private const char PuaNasalEpsilon = '\uE056'; // ɛ̃ (vin, pain)
        private const char PuaNasalAlpha = '\uE057';   // ɑ̃ (France, temps)
        private const char PuaNasalOpenO = '\uE058';   // ɔ̃ (bon, nom)
        private const char PuaYVowel = '\uE01E';       // y_vowel (close front rounded [y])

        // ---------------------------------------------------------------
        // Character sets
        // ---------------------------------------------------------------

        /// <summary>
        /// French vowel letters (for context checks in grapheme rules).
        /// </summary>
        private static readonly HashSet<char> Vowels = new HashSet<char>
        {
            'a', 'e', 'i', 'o', 'u', 'y',
            '\u00E0', // à
            '\u00E2', // â
            '\u00E6', // æ
            '\u00E9', // é
            '\u00E8', // è
            '\u00EA', // ê
            '\u00EB', // ë
            '\u00EE', // î
            '\u00EF', // ï
            '\u00F4', // ô
            '\u00F9', // ù
            '\u00FB', // û
            '\u00FC', // ü
            '\u0153'  // œ
        };

        /// <summary>
        /// French consonant letters.
        /// </summary>
        private static readonly HashSet<char> Consonants = new HashSet<char>
        {
            'b', 'c', 'd', 'f', 'g', 'h', 'j', 'k', 'l', 'm',
            'n', 'p', 'q', 'r', 's', 't', 'v', 'w', 'x', 'z'
        };

        /// <summary>
        /// Common silent final consonants in French.
        /// </summary>
        private static readonly HashSet<char> SilentFinal = new HashSet<char>
        {
            'd', 'g', 'h', 'm', 'n', 'p', 's', 't', 'x', 'z'
        };

        /// <summary>
        /// Punctuation characters recognized by the phonemizer.
        /// </summary>
        private static readonly HashSet<char> Punctuation = new HashSet<char>
        {
            ',', '.', ';', ':', '!', '?',
            '\u00A1', // ¡
            '\u00BF', // ¿
            '\u2014', // —
            '\u2013', // –
            '\u2026', // …
            '\u00AB', // «
            '\u00BB'  // »
        };

        /// <summary>
        /// Words where "ille" is pronounced /il/ not /ij/.
        /// </summary>
        private static readonly HashSet<string> IlleAsIl = new HashSet<string>(StringComparer.Ordinal)
        {
            "ville", "mille", "tranquille"
        };

        /// <summary>
        /// Polysyllabic words ending in -er that are pronounced /ɛʁ/ (not /e/).
        /// Exceptions to the verb infinitive -er -> /e/ rule.
        /// </summary>
        private static readonly HashSet<string> ErAsEhr = new HashSet<string>(StringComparer.Ordinal)
        {
            "hiver", "enfer", "amer", "cancer", "super", "laser",
            "hamster", "master", "poster", "cluster", "starter",
            "leader", "transfer", "fer"
        };

        /// <summary>
        /// IPA vowel phonemes (used for stress assignment).
        /// </summary>
        private static readonly HashSet<string> VowelPhonemes = new HashSet<string>(StringComparer.Ordinal)
        {
            "a", "e", "\u025B", "i", "o", "\u0254", "u",
            "y_vowel", "\u0259", "\u00F8", "\u0153",
            "\u025B\u0303", "\u0251\u0303", "\u0254\u0303"
        };

        /// <summary>
        /// Simple consonant letter to IPA phoneme mapping.
        /// </summary>
        private static readonly Dictionary<char, string> SimpleConsonants =
            new Dictionary<char, string>
            {
                { 'b', "b" },
                { 'd', "d" },
                { 'f', "f" },
                { 'k', "k" },
                { 'l', "l" },
                { 'm', "m" },
                { 'n', "n" },
                { 'p', "p" },
                { 's', "s" },
                { 't', "t" },
                { 'v', "v" },
                { 'w', "w" },
                { 'z', "z" }
            };

        /// <summary>
        /// Characters that soften 'c' and 'g' (c/g before these -> s/ʒ).
        /// </summary>
        private static readonly HashSet<char> SoftVowels = new HashSet<char>
        {
            'e', 'i', 'y',
            '\u00E9', // é
            '\u00E8', // è
            '\u00EA', // ê
            '\u00EB'  // ë
        };

        /// <summary>
        /// Characters that make 'u' silent after 'g' (gu + these -> /ɡ/).
        /// </summary>
        private static readonly HashSet<char> GuSilentVowels = new HashSet<char>
        {
            'e', 'i',
            '\u00E9', // é
            '\u00E8', // è
            '\u00EA', // ê
            '\u00EB', // ë
            '\u00EE', // î
            '\u00EF'  // ï
        };

        /// <summary>
        /// Regex for tokenizing French text into words and punctuation.
        /// Apostrophes act as word boundaries (l'ami -> "l", "ami").
        /// </summary>
        private static readonly Regex TokenRegex = new Regex(
            @"[a-z\u00E0\u00E2\u00E6\u00E9\u00E8\u00EA\u00EB\u00EE\u00EF\u00F4\u00F9\u00FB\u00FC\u0153\u00E7\u00F1]+|[,\.;:!\?\u00A1\u00BF\u2014\u2013\u2026\u00AB\u00BB]",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Regex for collapsing whitespace.
        /// </summary>
        private static readonly Regex WhitespaceRegex = new Regex(@"\s+", RegexOptions.Compiled);

        private readonly object _syncLock = new object();

        // ---------------------------------------------------------------
        // PhonemizerBackendBase overrides
        // ---------------------------------------------------------------

        /// <inheritdoc/>
        public override string Name => "FrenchRuleBased";

        /// <inheritdoc/>
        public override string Version => "1.0.0";

        /// <inheritdoc/>
        public override string License => "MIT";

        /// <inheritdoc/>
        public override string[] SupportedLanguages => new[] { "fr", "fr-FR" };

        /// <inheritdoc/>
        protected override Task<bool> InitializeInternalAsync(
            PhonemizerBackendOptions options,
            CancellationToken cancellationToken)
        {
            // Pure rule-based: no external data files required
            return Task.FromResult(true);
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators
        /// <inheritdoc/>
        public override async Task<PhonemeResult> PhonemizeAsync(
            string text,
            string language,
            PhonemeOptions options = null,
            CancellationToken cancellationToken = default)
        {
            EnsureInitialized();

            if (!ValidateInput(text, language, out var error))
            {
                return CreateErrorResult(error, language);
            }

            var opts = options ?? new PhonemeOptions();

#if UNITY_WEBGL && !UNITY_EDITOR
            lock (_syncLock)
            {
                return PhonemizeInternal(text, language, opts);
            }
#else
            return await Task.Run(() =>
            {
                lock (_syncLock)
                {
                    return PhonemizeInternal(text, language, opts);
                }
            }, cancellationToken);
#endif
        }
#pragma warning restore CS1998

        /// <inheritdoc/>
        public override long GetMemoryUsage()
        {
            // Rule-based: minimal memory footprint
            return 64 * 1024; // 64 KB estimate
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        protected override void DisposeInternal()
        {
            // No resources to release
        }

        // ---------------------------------------------------------------
        // Core phonemization logic
        // ---------------------------------------------------------------

        /// <summary>
        /// Internal phonemization entry point producing a full PhonemeResult.
        /// </summary>
        private PhonemeResult PhonemizeInternal(string text, string language, PhonemeOptions options)
        {
            var sw = Stopwatch.StartNew();

            var (phonemes, prosodyA1, prosodyA2, prosodyA3) = PhonemizeWithProsody(text);

            sw.Stop();

            return new PhonemeResult
            {
                OriginalText = text,
                Phonemes = phonemes,
                Language = language,
                Success = true,
                Backend = Name,
                ProcessingTimeMs = (float)sw.Elapsed.TotalMilliseconds,
                ProcessingTime = sw.Elapsed,
                ProsodyA1 = prosodyA1,
                ProsodyA2 = prosodyA2,
                ProsodyA3 = prosodyA3,
                Metadata = new Dictionary<string, object>
                {
                    ["backend"] = Name,
                    ["language"] = language
                }
            };
        }

        // ---------------------------------------------------------------
        // Text normalisation
        // ---------------------------------------------------------------

        /// <summary>
        /// Normalize text: lowercase, NFC normalize, collapse whitespace.
        /// </summary>
        private static string Normalize(string text)
        {
            text = text.Trim();
            text = text.Normalize(NormalizationForm.FormC);
            text = text.ToLowerInvariant();
            text = WhitespaceRegex.Replace(text, " ");
            return text;
        }

        // ---------------------------------------------------------------
        // Tokenization
        // ---------------------------------------------------------------

        /// <summary>
        /// Split text into words and punctuation tokens.
        /// Apostrophes (straight and curly) act as word boundaries in French elision
        /// (l'ami -> ["l", "ami"]).
        /// </summary>
        private static List<string> SplitWords(string text)
        {
            // Normalize curly/typographic apostrophes then drop them
            text = text.Replace('\u2019', '\'').Replace('\u2018', '\'');

            var matches = TokenRegex.Matches(text);
            var tokens = new List<string>(matches.Count);
            foreach (Match m in matches)
            {
                tokens.Add(m.Value);
            }
            return tokens;
        }

        // ---------------------------------------------------------------
        // Character classification helpers
        // ---------------------------------------------------------------

        private static bool IsVowelChar(char ch)
        {
            return Vowels.Contains(ch);
        }

        private static bool IsConsonantChar(char ch)
        {
            return Consonants.Contains(ch);
        }

        // ---------------------------------------------------------------
        // Word-level G2P conversion
        // ---------------------------------------------------------------

        /// <summary>
        /// Convert a single French word (lowercase) to a list of IPA phoneme strings.
        ///
        /// Handles all major French grapheme-to-phoneme rules including:
        /// - Nasal vowels (an/am, en/em, in/im, on/om, un/um, ain/aim, ein/eim, oin, ien)
        /// - Vowel digraphs (ou, au, eau, ai, ei, eu, oi, etc.)
        /// - Silent letters and final consonants
        /// - Consonant digraphs (ch, gn, ph, th, qu, gu)
        /// - Intervocalic s voicing (s between vowels -> z)
        /// - -er verb endings vs -er exception list
        /// - Context-dependent x handling
        /// - Semi-vowel ɥ (u before i after consonant)
        /// - -aille/-eille/-ouille/-ille patterns
        /// - e muet (silent final e)
        /// - tion -> /sjɔ̃/ vs stion -> /stjɔ̃/
        /// </summary>
        private static List<string> ConvertWord(string word)
        {
            var phonemes = new List<string>();
            int i = 0;
            int n = word.Length;

            while (i < n)
            {
                char ch = word[i];

                // =============================================================
                // Multi-character sequences (longest match first)
                // =============================================================

                // -er word-final: verb infinitive ending -> /e/
                // Only apply to polysyllabic words (parler, manger); monosyllabic words
                // like mer, fer, ver keep /ɛʁ/ pronunciation.
                // Exception list (ErAsEhr): polysyllabic words like "hiver", "enfer"
                // that keep /ɛʁ/ pronunciation.
                if (ch == 'e' && i + 1 == n - 1 && word[i + 1] == 'r')
                {
                    int vowelCount = CountVowels(word);
                    if (vowelCount >= 2 && !ErAsEhr.Contains(word))
                    {
                        // Word ends in "er" (polysyllabic)
                        // -ier/-yer: the 'i'/'y' already produced 'j' (or 'i'),
                        // just produce /e/ for 'er' and skip 'r'
                        phonemes.Add("e");
                        i += 2;
                        continue;
                    }
                    // else: monosyllabic -er (mer, fer) — fall through to normal e handling
                }

                // "eau" -> o
                if (ch == 'e' && i + 2 < n && Substring(word, i + 1, 2) == "au")
                {
                    phonemes.Add("o");
                    i += 3;
                    continue;
                }

                // "ouille" -> /uj/ (before end or consonant)
                if (ch == 'o'
                    && i + 5 <= n
                    && Substring(word, i + 1, 5) == "uille"
                    && (i + 6 >= n || !IsVowelChar(word[i + 6])))
                {
                    phonemes.Add("u");
                    phonemes.Add("j");
                    i += 6;
                    continue;
                }

                // "aille" -> /aj/
                if (ch == 'a'
                    && i + 4 <= n
                    && Substring(word, i + 1, 4) == "ille"
                    && (i + 5 >= n || !IsVowelChar(word[i + 5])))
                {
                    phonemes.Add("a");
                    phonemes.Add("j");
                    i += 5;
                    continue;
                }

                // "euille" -> /œj/ (feuille, écureuil)
                if (ch == 'e'
                    && i + 5 <= n
                    && Substring(word, i + 1, 5) == "uille"
                    && i + 6 >= n)
                {
                    phonemes.Add("\u0153"); // œ
                    phonemes.Add("j");
                    i += 6;
                    continue;
                }

                // "eil" at word end -> /ɛj/ (soleil, réveil)
                if (ch == 'e'
                    && i + 2 < n
                    && Substring(word, i + 1, 2) == "il"
                    && i + 3 >= n)
                {
                    phonemes.Add("\u025B"); // ɛ
                    phonemes.Add("j");
                    i += 3;
                    continue;
                }

                // "eille" -> /ɛj/
                if (ch == 'e'
                    && i + 4 <= n
                    && Substring(word, i + 1, 4) == "ille"
                    && (i + 5 >= n || !IsVowelChar(word[i + 5])))
                {
                    phonemes.Add("\u025B"); // ɛ
                    phonemes.Add("j");
                    i += 5;
                    continue;
                }

                // "ain", "aim" -> ɛ̃ (before consonant or end)
                if (ch == 'a' && i + 2 < n && word[i + 1] == 'i' && (word[i + 2] == 'n' || word[i + 2] == 'm'))
                {
                    if (i + 3 >= n || !IsVowelChar(word[i + 3]))
                    {
                        phonemes.Add("\u025B\u0303"); // ɛ̃
                        i += 3;
                        continue;
                    }
                }

                // "ein", "eim" -> ɛ̃
                if (ch == 'e' && i + 2 < n && word[i + 1] == 'i' && (word[i + 2] == 'n' || word[i + 2] == 'm'))
                {
                    if (i + 3 >= n || !IsVowelChar(word[i + 3]))
                    {
                        phonemes.Add("\u025B\u0303"); // ɛ̃
                        i += 3;
                        continue;
                    }
                }

                // "oin" -> wɛ̃
                if (ch == 'o' && i + 2 < n && Substring(word, i + 1, 2) == "in")
                {
                    if (i + 3 >= n || !IsVowelChar(word[i + 3]))
                    {
                        phonemes.Add("w");
                        phonemes.Add("\u025B\u0303"); // ɛ̃
                        i += 3;
                        continue;
                    }
                }

                // "ien" -> jɛ̃
                if (ch == 'i' && i + 2 < n && Substring(word, i + 1, 2) == "en")
                {
                    if (i + 3 >= n || !IsVowelChar(word[i + 3]))
                    {
                        phonemes.Add("j");
                        phonemes.Add("\u025B\u0303"); // ɛ̃
                        i += 3;
                        continue;
                    }
                }

                // "stion" -> /stjɔ̃/ (NOT /ssjɔ̃/)
                // "tion" -> /sjɔ̃/ (only when NOT preceded by 's')
                if (ch == 't' && i + 3 < n && Substring(word, i + 1, 3) == "ion")
                {
                    if (i + 4 >= n || !IsVowelChar(word[i + 4]))
                    {
                        // Check if preceded by 's' — if so, produce /tjɔ̃/
                        // (the 's' already produced /s/, so we just need /t/)
                        if (i > 0 && word[i - 1] == 's')
                        {
                            phonemes.Add("t");
                        }
                        else
                        {
                            phonemes.Add("s");
                        }
                        phonemes.Add("j");
                        phonemes.Add("\u0254\u0303"); // ɔ̃
                        i += 4;
                        continue;
                    }
                }

                // "ille" -> /ij/ by default, but /il/ for exceptions (ville, mille, etc.)
                if (ch == 'i'
                    && i + 3 < n
                    && Substring(word, i + 1, 3) == "lle"
                    && (i + 4 >= n || !IsVowelChar(word[i + 4])))
                {
                    if (IlleAsIl.Contains(word))
                    {
                        phonemes.Add("i");
                        phonemes.Add("l");
                    }
                    else
                    {
                        phonemes.Add("i");
                        phonemes.Add("j");
                    }
                    i += 4;
                    continue;
                }

                // "gn" -> ɲ
                if (ch == 'g' && i + 1 < n && word[i + 1] == 'n')
                {
                    phonemes.Add("\u0272"); // ɲ
                    i += 2;
                    continue;
                }

                // "ph" -> f
                if (ch == 'p' && i + 1 < n && word[i + 1] == 'h')
                {
                    phonemes.Add("f");
                    i += 2;
                    continue;
                }

                // "th" -> t
                if (ch == 't' && i + 1 < n && word[i + 1] == 'h')
                {
                    phonemes.Add("t");
                    i += 2;
                    continue;
                }

                // "ch" -> ʃ
                if (ch == 'c' && i + 1 < n && word[i + 1] == 'h')
                {
                    phonemes.Add("\u0283"); // ʃ
                    i += 2;
                    continue;
                }

                // "qu" -> k
                if (ch == 'q' && i + 1 < n && word[i + 1] == 'u')
                {
                    phonemes.Add("k");
                    i += 2;
                    continue;
                }

                // "gu" before e/i -> ɡ (u silent)
                if (ch == 'g' && i + 1 < n && word[i + 1] == 'u')
                {
                    if (i + 2 < n && GuSilentVowels.Contains(word[i + 2]))
                    {
                        phonemes.Add("\u0261"); // ɡ
                        i += 2;
                        continue;
                    }
                }

                // =============================================================
                // Nasal vowels: vowel + n/m before consonant or end
                // =============================================================

                // "an", "am", "en", "em" -> ɑ̃
                if ((ch == 'a' || ch == 'e') && i + 1 < n && (word[i + 1] == 'n' || word[i + 1] == 'm'))
                {
                    if (i + 2 >= n)
                    {
                        phonemes.Add("\u0251\u0303"); // ɑ̃
                        i += 2;
                        continue;
                    }
                    if (!IsVowelChar(word[i + 2]) && word[i + 2] != word[i + 1])
                    {
                        phonemes.Add("\u0251\u0303"); // ɑ̃
                        i += 2;
                        continue;
                    }
                }

                // "in", "im" -> ɛ̃
                if (ch == 'i' && i + 1 < n && (word[i + 1] == 'n' || word[i + 1] == 'm'))
                {
                    if (i + 2 >= n)
                    {
                        phonemes.Add("\u025B\u0303"); // ɛ̃
                        i += 2;
                        continue;
                    }
                    if (!IsVowelChar(word[i + 2]) && word[i + 2] != word[i + 1])
                    {
                        phonemes.Add("\u025B\u0303"); // ɛ̃
                        i += 2;
                        continue;
                    }
                }

                // "on", "om" -> ɔ̃
                if (ch == 'o' && i + 1 < n && (word[i + 1] == 'n' || word[i + 1] == 'm'))
                {
                    if (i + 2 >= n)
                    {
                        phonemes.Add("\u0254\u0303"); // ɔ̃
                        i += 2;
                        continue;
                    }
                    if (!IsVowelChar(word[i + 2]) && word[i + 2] != word[i + 1])
                    {
                        phonemes.Add("\u0254\u0303"); // ɔ̃
                        i += 2;
                        continue;
                    }
                }

                // "un", "um" -> ɛ̃ (modern French merger)
                if (ch == 'u' && i + 1 < n && (word[i + 1] == 'n' || word[i + 1] == 'm'))
                {
                    if (i + 2 >= n)
                    {
                        phonemes.Add("\u025B\u0303"); // ɛ̃
                        i += 2;
                        continue;
                    }
                    if (!IsVowelChar(word[i + 2]) && word[i + 2] != word[i + 1])
                    {
                        phonemes.Add("\u025B\u0303"); // ɛ̃
                        i += 2;
                        continue;
                    }
                }

                // "yn", "ym" before consonant -> ɛ̃ (syndicat, symbole)
                if (ch == 'y' && i + 1 < n && (word[i + 1] == 'n' || word[i + 1] == 'm'))
                {
                    if (i + 2 >= n)
                    {
                        phonemes.Add("\u025B\u0303"); // ɛ̃
                        i += 2;
                        continue;
                    }
                    if (!IsVowelChar(word[i + 2]) && word[i + 2] != word[i + 1])
                    {
                        phonemes.Add("\u025B\u0303"); // ɛ̃
                        i += 2;
                        continue;
                    }
                }

                // =============================================================
                // Vowel digraphs
                // =============================================================

                // "ou" -> u
                if (ch == 'o' && i + 1 < n && word[i + 1] == 'u')
                {
                    phonemes.Add("u");
                    i += 2;
                    continue;
                }

                // "au" -> o
                if (ch == 'a' && i + 1 < n && word[i + 1] == 'u')
                {
                    phonemes.Add("o");
                    i += 2;
                    continue;
                }

                // "oi" -> wa
                if (ch == 'o' && i + 1 < n && word[i + 1] == 'i')
                {
                    phonemes.Add("w");
                    phonemes.Add("a");
                    i += 2;
                    continue;
                }

                // "ai" -> ɛ
                if (ch == 'a' && i + 1 < n && word[i + 1] == 'i')
                {
                    phonemes.Add("\u025B"); // ɛ
                    i += 2;
                    continue;
                }

                // "ei" -> ɛ
                if (ch == 'e' && i + 1 < n && word[i + 1] == 'i')
                {
                    phonemes.Add("\u025B"); // ɛ
                    i += 2;
                    continue;
                }

                // "eu", "œu" -> ø (closed) or œ (open, before pronounced consonant)
                if ((ch == 'e' && i + 1 < n && word[i + 1] == 'u')
                    || (ch == '\u0153' && i + 1 < n && word[i + 1] == 'u'))
                {
                    // Open before pronounced consonant in same syllable
                    if (i + 2 < n
                        && IsConsonantChar(word[i + 2])
                        && !SilentFinal.Contains(word[i + 2]))
                    {
                        phonemes.Add("\u0153"); // œ
                    }
                    else
                    {
                        phonemes.Add("\u00F8"); // ø
                    }
                    i += 2;
                    continue;
                }

                // =============================================================
                // Single vowels
                // =============================================================

                // é -> /e/
                if (ch == '\u00E9')
                {
                    phonemes.Add("e");
                    i += 1;
                    continue;
                }

                // è, ê -> /ɛ/
                if (ch == '\u00E8' || ch == '\u00EA')
                {
                    phonemes.Add("\u025B"); // ɛ
                    i += 1;
                    continue;
                }

                // ë -> /ɛ/
                if (ch == '\u00EB')
                {
                    phonemes.Add("\u025B"); // ɛ
                    i += 1;
                    continue;
                }

                // à, â -> /a/
                if (ch == '\u00E0' || ch == '\u00E2')
                {
                    phonemes.Add("a");
                    i += 1;
                    continue;
                }

                // a -> /a/
                if (ch == 'a')
                {
                    phonemes.Add("a");
                    i += 1;
                    continue;
                }

                // î, ï -> /i/
                if (ch == '\u00EE' || ch == '\u00EF')
                {
                    phonemes.Add("i");
                    i += 1;
                    continue;
                }

                // i
                if (ch == 'i')
                {
                    // "i" before vowel -> j (semi-vowel), EXCEPT before word-final
                    // silent 'e' (vie->/vi/, amie->/ami/, not */vj/, */amj/)
                    if (i + 1 < n && IsVowelChar(word[i + 1]))
                    {
                        // Don't glide before word-final silent 'e'
                        if (i + 1 == n - 1 && word[i + 1] == 'e')
                        {
                            phonemes.Add("i");
                        }
                        else
                        {
                            phonemes.Add("j");
                        }
                    }
                    else
                    {
                        phonemes.Add("i");
                    }
                    i += 1;
                    continue;
                }

                // ô -> /o/
                if (ch == '\u00F4')
                {
                    phonemes.Add("o");
                    i += 1;
                    continue;
                }

                // o: open /ɔ/ before pronounced consonant at word end, closed /o/ elsewhere
                if (ch == 'o')
                {
                    var remaining = word.Substring(i + 1);
                    var effective = remaining;
                    if (effective.EndsWith("es", StringComparison.Ordinal))
                    {
                        effective = effective.Substring(0, effective.Length - 2);
                    }
                    else if (effective.EndsWith("e", StringComparison.Ordinal))
                    {
                        effective = effective.Substring(0, effective.Length - 1);
                    }

                    if (effective.Length > 0
                        && AllConsonants(effective)
                        && AnyPronouncedConsonant(effective))
                    {
                        phonemes.Add("\u0254"); // ɔ
                    }
                    else
                    {
                        phonemes.Add("o");
                    }
                    i += 1;
                    continue;
                }

                // ù, û -> y_vowel
                if (ch == '\u00F9' || ch == '\u00FB')
                {
                    phonemes.Add("y_vowel");
                    i += 1;
                    continue;
                }

                // ü -> y_vowel
                if (ch == '\u00FC')
                {
                    phonemes.Add("y_vowel");
                    i += 1;
                    continue;
                }

                // u
                if (ch == 'u')
                {
                    // Semi-vowel ɥ: u before i (after consonant) -> ɥi
                    if (i + 1 < n && word[i + 1] == 'i')
                    {
                        phonemes.Add("\u0265"); // ɥ
                        phonemes.Add("i");
                        i += 2;
                        continue;
                    }
                    // "u" after g/q already handled; standalone u -> y_vowel
                    phonemes.Add("y_vowel");
                    i += 1;
                    continue;
                }

                // y
                if (ch == 'y')
                {
                    // 'y' in French usually acts as 'i'
                    if (i + 1 < n && IsVowelChar(word[i + 1]))
                    {
                        phonemes.Add("j");
                    }
                    else
                    {
                        phonemes.Add("i");
                    }
                    i += 1;
                    continue;
                }

                // œ (standalone, not part of digraph)
                if (ch == '\u0153')
                {
                    phonemes.Add("\u0153"); // œ
                    i += 1;
                    continue;
                }

                // æ -> /e/
                if (ch == '\u00E6')
                {
                    phonemes.Add("e");
                    i += 1;
                    continue;
                }

                // "e" context-dependent
                if (ch == 'e')
                {
                    // Final silent e (e muet) -- skip at word end
                    if (i == n - 1)
                    {
                        i += 1;
                        continue;
                    }

                    var remaining = word.Substring(i + 1);
                    // ɛ in closed syllable:
                    //   (a) before 2+ leading consonants (merci, service, berceau)
                    //   (b) before only consonant(s) with at least one pronounced final one
                    if (remaining.Length > 0)
                    {
                        int consonantCount = 0;
                        foreach (char c in remaining)
                        {
                            if (Consonants.Contains(c))
                            {
                                consonantCount++;
                            }
                            else
                            {
                                break;
                            }
                        }

                        if (consonantCount >= 2)
                        {
                            phonemes.Add("\u025B"); // ɛ
                        }
                        else if (AllConsonants(remaining) && AnyPronouncedConsonant(remaining))
                        {
                            phonemes.Add("\u025B"); // ɛ
                        }
                        else
                        {
                            phonemes.Add("\u0259"); // ə
                        }
                    }
                    else
                    {
                        phonemes.Add("\u0259"); // ə
                    }
                    i += 1;
                    continue;
                }

                // =============================================================
                // Consonants
                // =============================================================

                // c: before e, i, y -> s; otherwise -> k
                if (ch == 'c')
                {
                    if (i + 1 < n && SoftVowels.Contains(word[i + 1]))
                    {
                        phonemes.Add("s");
                    }
                    else
                    {
                        phonemes.Add("k");
                    }
                    i += 1;
                    continue;
                }

                // ç -> s
                if (ch == '\u00E7')
                {
                    phonemes.Add("s");
                    i += 1;
                    continue;
                }

                // g: before e, i, y -> ʒ; otherwise -> ɡ
                if (ch == 'g')
                {
                    if (i + 1 < n && SoftVowels.Contains(word[i + 1]))
                    {
                        phonemes.Add("\u0292"); // ʒ
                    }
                    else
                    {
                        phonemes.Add("\u0261"); // ɡ
                    }
                    i += 1;
                    continue;
                }

                // j -> ʒ
                if (ch == 'j')
                {
                    phonemes.Add("\u0292"); // ʒ
                    i += 1;
                    continue;
                }

                // r -> ʁ (skip doubled r)
                if (ch == 'r')
                {
                    phonemes.Add("\u0281"); // ʁ
                    if (i + 1 < n && word[i + 1] == 'r')
                    {
                        i += 2;
                    }
                    else
                    {
                        i += 1;
                    }
                    continue;
                }

                // x: context-dependent handling
                if (ch == 'x')
                {
                    // Word-final x is usually silent
                    if (i == n - 1)
                    {
                        i += 1;
                        continue;
                    }
                    // Also silent before final silent 'e'/'es'
                    var remainingAfter = word.Substring(i + 1);
                    if (remainingAfter == "e" || remainingAfter == "es")
                    {
                        i += 1;
                        continue;
                    }
                    // "ex" + vowel -> /ɛgz/ (x is after e, next is vowel)
                    if (i > 0
                        && word[i - 1] == 'e'
                        && i + 1 < n
                        && IsVowelChar(word[i + 1]))
                    {
                        phonemes.Add("\u0261"); // ɡ
                        phonemes.Add("z");
                        i += 1;
                        continue;
                    }
                    // Default: x -> /ks/
                    phonemes.Add("k");
                    phonemes.Add("s");
                    i += 1;
                    continue;
                }

                // h is always silent in French
                if (ch == 'h')
                {
                    i += 1;
                    continue;
                }

                // Double consonants -> single (fall through to simple mapping below)
                if (i + 1 < n && word[i + 1] == ch && Consonants.Contains(ch))
                {
                    // Just note it; fall through to simple mapping
                }

                // Simple consonant mappings
                if (SimpleConsonants.TryGetValue(ch, out var ipaConsonant))
                {
                    // Handle final silent consonants
                    bool isWordFinal = i == n - 1;
                    bool isBeforeFinalS = i == n - 2 && word[n - 1] == 's';
                    bool isFinal = isWordFinal || isBeforeFinalS;

                    if (isFinal && SilentFinal.Contains(ch))
                    {
                        i += 1;
                        continue;
                    }

                    // Intervocalic s voicing: single 's' between two vowels -> /z/
                    if (ch == 's')
                    {
                        bool prevIsVowel = i > 0 && IsVowelChar(word[i - 1]);
                        bool nextIsVowel = i + 1 < n && IsVowelChar(word[i + 1]);
                        bool isSingle = !(i + 1 < n && word[i + 1] == 's');
                        if (prevIsVowel && nextIsVowel && isSingle)
                        {
                            phonemes.Add("z");
                            i += 1;
                            continue;
                        }
                    }

                    phonemes.Add(ipaConsonant);
                    // Skip doubled consonant
                    if (i + 1 < n && word[i + 1] == ch)
                    {
                        i += 2;
                    }
                    else
                    {
                        i += 1;
                    }
                    continue;
                }

                // Punctuation
                if (Punctuation.Contains(ch))
                {
                    phonemes.Add(ch.ToString());
                    i += 1;
                    continue;
                }

                // Skip unknown characters
                i += 1;
            }

            return phonemes;
        }

        // ---------------------------------------------------------------
        // PUA mapping
        // ---------------------------------------------------------------

        /// <summary>
        /// Map multi-character phoneme tokens to PUA single codepoints.
        /// Single-character phonemes are left unchanged.
        /// </summary>
        private static readonly Dictionary<string, string> PuaMap =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "\u025B\u0303", PuaNasalEpsilon.ToString() }, // ɛ̃
                { "\u0251\u0303", PuaNasalAlpha.ToString() },   // ɑ̃
                { "\u0254\u0303", PuaNasalOpenO.ToString() },   // ɔ̃
                { "y_vowel", PuaYVowel.ToString() }
            };

        /// <summary>
        /// Replace multi-character phoneme tokens with PUA-mapped single characters.
        /// </summary>
        private static string[] MapToPua(List<string> phonemes)
        {
            var result = new string[phonemes.Count];
            for (int i = 0; i < phonemes.Count; i++)
            {
                result[i] = PuaMap.TryGetValue(phonemes[i], out var mapped) ? mapped : phonemes[i];
            }
            return result;
        }

        // ---------------------------------------------------------------
        // Prosody computation
        // ---------------------------------------------------------------

        /// <summary>
        /// Phonemize text and compute prosody arrays.
        /// French has fixed stress on the last syllable of each word/phrase.
        ///
        /// Prosody conventions:
        ///   a1 = 0 (unused in French)
        ///   a2 = stress level: 2 for last vowel phoneme in each word, 0 otherwise
        ///   a3 = total phoneme count of the word
        /// </summary>
        private (string[] phonemes, int[] prosodyA1, int[] prosodyA2, int[] prosodyA3) PhonemizeWithProsody(
            string text)
        {
            text = Normalize(text);
            var tokens = SplitWords(text);

            var phonemeList = new List<string>();
            var a1List = new List<int>();
            var a2List = new List<int>();
            var a3List = new List<int>();
            bool needSpace = false;

            foreach (var token in tokens)
            {
                bool isPunct = IsPunctuationToken(token);

                if (!isPunct && needSpace)
                {
                    phonemeList.Add(" ");
                    a1List.Add(0);
                    a2List.Add(0);
                    a3List.Add(0);
                }

                if (isPunct)
                {
                    foreach (char c in token)
                    {
                        phonemeList.Add(c.ToString());
                        a1List.Add(0);
                        a2List.Add(0);
                        a3List.Add(0);
                    }
                }
                else
                {
                    var wordPhonemes = ConvertWord(token);
                    int wordPhonemeCount = wordPhonemes.Count;

                    // French: stress always on last syllable (last vowel phoneme)
                    int lastVowelIdx = -1;
                    for (int j = wordPhonemes.Count - 1; j >= 0; j--)
                    {
                        if (VowelPhonemes.Contains(wordPhonemes[j]))
                        {
                            lastVowelIdx = j;
                            break;
                        }
                    }

                    for (int j = 0; j < wordPhonemes.Count; j++)
                    {
                        int a2 = j == lastVowelIdx ? 2 : 0;
                        phonemeList.Add(wordPhonemes[j]);
                        a1List.Add(0);
                        a2List.Add(a2);
                        a3List.Add(wordPhonemeCount);
                    }
                }

                needSpace = true;
            }

            // Map multi-character tokens to PUA codepoints
            var mapped = MapToPua(phonemeList);

            return (mapped, a1List.ToArray(), a2List.ToArray(), a3List.ToArray());
        }

        // ---------------------------------------------------------------
        // Utility helpers
        // ---------------------------------------------------------------

        /// <summary>
        /// Safe substring extraction that never throws on out-of-range.
        /// Returns up to <paramref name="length"/> characters starting at <paramref name="startIndex"/>.
        /// </summary>
        private static string Substring(string s, int startIndex, int length)
        {
            if (startIndex >= s.Length)
                return string.Empty;
            int available = s.Length - startIndex;
            return s.Substring(startIndex, Math.Min(length, available));
        }

        /// <summary>
        /// Count the number of vowel letters in a word.
        /// </summary>
        private static int CountVowels(string word)
        {
            int count = 0;
            foreach (char c in word)
            {
                if (Vowels.Contains(c))
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Check if all characters in a string are consonants.
        /// </summary>
        private static bool AllConsonants(string s)
        {
            foreach (char c in s)
            {
                if (!Consonants.Contains(c))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Check if at least one character in a string is a pronounced (non-silent) consonant.
        /// </summary>
        private static bool AnyPronouncedConsonant(string s)
        {
            foreach (char c in s)
            {
                if (!SilentFinal.Contains(c))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Check if a token is entirely punctuation.
        /// </summary>
        private static bool IsPunctuationToken(string token)
        {
            foreach (char c in token)
            {
                if (!Punctuation.Contains(c))
                    return false;
            }
            return true;
        }
    }
}