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

namespace uPiper.Core.Phonemizers.Backend.Portuguese
{
    /// <summary>
    /// Rule-based Brazilian Portuguese phonemizer backend for Piper TTS.
    /// Converts Brazilian Portuguese text to IPA phonemes using grapheme-to-phoneme rules.
    /// No external G2P engine required - pure C# implementation.
    /// </summary>
    public class PortuguesePhonemizerBackend : PhonemizerBackendBase
    {
        // =====================================================================
        // PUA (Private Use Area) codepoints for multi-character phonemes
        // Must match token_mapper.py FIXED_PUA_MAPPING
        // =====================================================================

        /// <summary>PUA codepoint for voiceless postalveolar affricate (t&#x0283;).</summary>
        private const char PuaAffricateTsh = '\uE054';

        /// <summary>PUA codepoint for voiced postalveolar affricate (d&#x0292;).</summary>
        private const char PuaAffricateDzh = '\uE055';

        // =====================================================================
        // Character sets
        // =====================================================================

        /// <summary>Punctuation characters (attached to previous word, no space before).</summary>
        private static readonly HashSet<char> Punctuation = new()
        {
            ',', '.', ';', ':', '!', '?',
            '\u00a1', // inverted exclamation mark
            '\u00bf', // inverted question mark
            '\u2014', // em dash
            '\u2013', // en dash
            '\u2026'  // horizontal ellipsis
        };

        /// <summary>Vowel letters (for voicing/nasalization context checks).</summary>
        private static readonly HashSet<char> VowelChars = new()
        {
            'a', 'e', 'i', 'o', 'u',
            '\u00e1', // a-acute
            '\u00e0', // a-grave
            '\u00e2', // a-circumflex
            '\u00e3', // a-tilde
            '\u00e9', // e-acute
            '\u00ea', // e-circumflex
            '\u00ed', // i-acute
            '\u00f3', // o-acute
            '\u00f4', // o-circumflex
            '\u00f5', // o-tilde
            '\u00fa', // u-acute
            '\u00fc'  // u-diaeresis
        };

        /// <summary>Accent-to-base mapping for stress detection.</summary>
        private static readonly Dictionary<char, char> AccentedToBase = new()
        {
            { '\u00e1', 'a' }, // a-acute
            { '\u00e0', 'a' }, // a-grave
            { '\u00e2', 'a' }, // a-circumflex
            { '\u00e3', 'a' }, // a-tilde
            { '\u00e9', 'e' }, // e-acute
            { '\u00ea', 'e' }, // e-circumflex
            { '\u00ed', 'i' }, // i-acute
            { '\u00f3', 'o' }, // o-acute
            { '\u00f4', 'o' }, // o-circumflex
            { '\u00f5', 'o' }, // o-tilde
            { '\u00fa', 'u' }, // u-acute
            { '\u00fc', 'u' }  // u-diaeresis
        };

        /// <summary>Acute/grave accents indicate stressed open vowels.</summary>
        private static readonly HashSet<char> StressAccents = new()
        {
            '\u00e1', '\u00e9', '\u00ed', '\u00f3', '\u00fa'
        };

        /// <summary>Circumflex indicates stressed closed vowels.</summary>
        private static readonly HashSet<char> Circumflex = new()
        {
            '\u00e2', '\u00ea', '\u00f4'
        };

        /// <summary>Tilde indicates nasal vowels (also stressed when it's the only accent).</summary>
        private static readonly HashSet<char> Tilde = new()
        {
            '\u00e3', '\u00f5'
        };

        /// <summary>IPA oral vowel phonemes (for reduction checks in post-processing).</summary>
        private static readonly HashSet<string> IpaOralVowels = new()
        {
            "a", "e", "i", "o", "\u025b", "\u0254", "u"
        };

        /// <summary>IPA nasal vowel phonemes.</summary>
        private static readonly HashSet<string> IpaNasalVowels = new()
        {
            "\u00e3", "\u1ebd", "\u0129", "\u00f5", "\u0169"
        };

        /// <summary>IPA vowel phonemes (all = oral + nasal).</summary>
        private static readonly HashSet<string> IpaVowels;

        /// <summary>IPA consonant phonemes (for coda-l detection).</summary>
        private static readonly HashSet<string> IpaConsonants = new()
        {
            "b", "c", "d", "f", "\u0261", "h", "j", "k", "l", "m", "n",
            "p", "\u0272", "\u027e", "\u0281", "s", "\u0283", "t", "\u028e",
            "v", "w", "z", "\u0292"
        };

        /// <summary>Characters valid before "e/i" in soft-consonant contexts.</summary>
        private static readonly HashSet<char> SoftVowelTriggers = new()
        {
            'e', 'i', '\u00e9', '\u00ea', '\u00ed'
        };

        /// <summary>Nasal vowel map: base vowel -> nasal IPA vowel.</summary>
        private static readonly Dictionary<char, string> NasalVowelMap = new()
        {
            { 'a', "\u00e3" },
            { 'e', "\u1ebd" },
            { 'i', "\u0129" },
            { 'o', "\u00f5" },
            { 'u', "\u0169" }
        };

        /// <summary>Acute accent open vowel map: base vowel -> open IPA vowel.</summary>
        private static readonly Dictionary<char, string> OpenVowelMap = new()
        {
            { 'a', "a" },
            { 'e', "\u025b" },
            { 'i', "i" },
            { 'o', "\u0254" },
            { 'u', "u" }
        };

        /// <summary>Simple consonant mappings (identity).</summary>
        private static readonly HashSet<char> SimpleConsonants = new()
        {
            'b', 'f', 'k', 'l', 'm', 'n', 'p', 'v'
        };

        /// <summary>Regex for tokenizing text into words and punctuation.</summary>
        private static readonly Regex TokenizerRegex = new(
            @"[a-z\u00e1\u00e0\u00e2\u00e3\u00e9\u00ea\u00ed\u00f3\u00f4\u00f5\u00fa\u00fc\u00e7\u00f1]+" +
            @"|[,.;:!?\u00a1\u00bf\u2014\u2013\u2026]",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Regex for collapsing multiple spaces.</summary>
        private static readonly Regex MultiSpaceRegex = new(@"\s+", RegexOptions.Compiled);

        private readonly object _syncLock = new();

        // =====================================================================
        // Static constructor
        // =====================================================================

        static PortuguesePhonemizerBackend()
        {
            IpaVowels = new HashSet<string>(IpaOralVowels);
            foreach (var v in IpaNasalVowels)
            {
                IpaVowels.Add(v);
            }
        }

        // =====================================================================
        // PhonemizerBackendBase overrides
        // =====================================================================

        /// <inheritdoc/>
        public override string Name => "Portuguese";

        /// <inheritdoc/>
        public override string Version => "1.0.0";

        /// <inheritdoc/>
        public override string License => "MIT";

        /// <inheritdoc/>
        private static readonly string[] _supportedLanguages = { "pt", "pt-BR" };
        public override string[] SupportedLanguages => _supportedLanguages;

        /// <inheritdoc/>
        protected override Task<bool> InitializeInternalAsync(
            PhonemizerBackendOptions options,
            CancellationToken cancellationToken)
        {
            // Pure rule-based: no external data files required
            return Task.FromResult(true);
        }

        /// <inheritdoc/>
#pragma warning disable CS1998 // Async method lacks 'await' operators
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

            var sw = Stopwatch.StartNew();

#if UNITY_WEBGL && !UNITY_EDITOR
            lock (_syncLock)
            {
                var result = PhonemizeInternal(text, language);
                sw.Stop();
                result.ProcessingTimeMs = (float)sw.Elapsed.TotalMilliseconds;
                result.ProcessingTime = sw.Elapsed;
                return result;
            }
#else
            return await Task.Run(() =>
            {
                lock (_syncLock)
                {
                    var result = PhonemizeInternal(text, language);
                    sw.Stop();
                    result.ProcessingTimeMs = (float)sw.Elapsed.TotalMilliseconds;
                    result.ProcessingTime = sw.Elapsed;
                    return result;
                }
            }, cancellationToken);
#endif
        }
#pragma warning restore CS1998

        /// <inheritdoc/>
        public override long GetMemoryUsage()
        {
            // Pure rule-based, minimal memory footprint
            return 64 * 1024; // 64KB estimate
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
            // No resources to dispose
        }

        // =====================================================================
        // Core phonemization
        // =====================================================================

        /// <summary>
        /// Main phonemization entry point. Produces phonemes with prosody information.
        /// </summary>
        private PhonemeResult PhonemizeInternal(string text, string language)
        {
            var normalized = Normalize(text);
            var tokens = SplitWords(normalized);

            var phonemes = new List<string>();
            var prosodyA1 = new List<int>();
            var prosodyA2 = new List<int>();
            var prosodyA3 = new List<int>();
            var needSpace = false;

            foreach (var token in tokens)
            {
                bool isPunct = IsPunctuationToken(token);

                if (!isPunct && needSpace)
                {
                    phonemes.Add(" ");
                    prosodyA1.Add(0);
                    prosodyA2.Add(0);
                    prosodyA3.Add(0);
                }

                if (isPunct)
                {
                    foreach (var ch in token)
                    {
                        phonemes.Add(ch.ToString());
                        prosodyA1.Add(0);
                        prosodyA2.Add(0);
                        prosodyA3.Add(0);
                    }
                }
                else
                {
                    var (wordPhonemes, stressIdx) = ConvertWord(token);
                    int wordPhonemeCount = wordPhonemes.Count;

                    for (int j = 0; j < wordPhonemes.Count; j++)
                    {
                        int a2 = (j == stressIdx) ? 2 : 0;
                        phonemes.Add(wordPhonemes[j]);
                        prosodyA1.Add(0);
                        prosodyA2.Add(a2);
                        prosodyA3.Add(wordPhonemeCount);
                    }
                }

                needSpace = true;
            }

            // Map multi-character phonemes to PUA codepoints
            var mapped = MapToPua(phonemes);

            return new PhonemeResult
            {
                OriginalText = text,
                Phonemes = mapped.ToArray(),
                Language = language,
                Success = true,
                Backend = Name,
                ProsodyA1 = prosodyA1.ToArray(),
                ProsodyA2 = prosodyA2.ToArray(),
                ProsodyA3 = prosodyA3.ToArray(),
                Metadata = new Dictionary<string, object>
                {
                    ["backend"] = Name,
                    ["variant"] = "pt-BR"
                }
            };
        }

        // =====================================================================
        // Text normalization and tokenization
        // =====================================================================

        /// <summary>
        /// Normalize text: lowercase, NFC normalization, collapse whitespace.
        /// </summary>
        private static string Normalize(string text)
        {
            text = text.Trim();
            text = text.Normalize(NormalizationForm.FormC);
            text = text.ToLowerInvariant();
            text = MultiSpaceRegex.Replace(text, " ");
            return text;
        }

        /// <summary>
        /// Split text into words and punctuation tokens.
        /// </summary>
        private static List<string> SplitWords(string text)
        {
            var tokens = new List<string>();
            var matches = TokenizerRegex.Matches(text);
            foreach (Match match in matches)
            {
                tokens.Add(match.Value);
            }
            return tokens;
        }

        /// <summary>
        /// Check if every character in the token is punctuation.
        /// </summary>
        private static bool IsPunctuationToken(string token)
        {
            foreach (var ch in token)
            {
                if (!Punctuation.Contains(ch))
                    return false;
            }
            return true;
        }

        // =====================================================================
        // Vowel / context helpers
        // =====================================================================

        /// <summary>Check whether a character is a Portuguese vowel letter.</summary>
        private static bool IsVowelChar(char ch)
        {
            return VowelChars.Contains(ch);
        }

        /// <summary>
        /// Return true if position i in word has a vowel immediately before and after.
        /// Used for context-dependent r: intervocalic r -> tap, coda r -> uvular fricative.
        /// </summary>
        private static bool IsIntervocalic(int i, string word)
        {
            if (i <= 0 || i >= word.Length - 1)
                return false;
            return IsVowelChar(word[i - 1]) && IsVowelChar(word[i + 1]);
        }

        // =====================================================================
        // Stress detection
        // =====================================================================

        /// <summary>
        /// Count vowel groups in a word, properly handling digraphs.
        /// Digraphs like 'ou', 'qu' (before e/i), and 'gu' (before e/i) consume
        /// two characters but may count differently for vowel group tracking.
        /// </summary>
        private static int CountVowelGroups(string word)
        {
            int count = 0;
            int i = 0;
            int n = word.Length;

            while (i < n)
            {
                char ch = word[i];

                // Handle 'qu' digraph: u is silent before e/i, produces /kw/ before a/o
                if (ch == 'q' && i + 1 < n && word[i + 1] == 'u')
                {
                    if (i + 2 < n && SoftVowelTriggers.Contains(word[i + 2]))
                    {
                        // qu before e/i: u is silent, skip both q and u
                        i += 2;
                        continue;
                    }
                    else
                    {
                        // qu before a/o: u is pronounced as /w/ glide (consonant),
                        // not a vowel group; skip both q and u
                        i += 2;
                        continue;
                    }
                }

                // Handle 'gu' digraph: u is silent before e/i
                if (ch == 'g' && i + 1 < n && word[i + 1] == 'u')
                {
                    if (i + 2 < n && SoftVowelTriggers.Contains(word[i + 2]))
                    {
                        // gu before e/i: u is silent, skip both g and u
                        i += 2;
                        continue;
                    }
                }

                // Handle 'ou' diphthong: two vowel letters but one vowel group
                if (ch == 'o' && i + 1 < n && word[i + 1] == 'u')
                {
                    count++;
                    i += 2;
                    continue;
                }

                if (VowelChars.Contains(ch))
                {
                    count++;
                }

                i++;
            }

            return count;
        }

        /// <summary>
        /// Find the stressed syllable index (0-based from end).
        /// Returns the position of the stressed vowel group from the end of the word.
        /// Portuguese stress rules:
        /// - Words with acute/circumflex/tilde accent: stress on accented syllable
        /// - Words ending in a, e, o, am, em, en, ens: penultimate (paroxytone)
        /// - Words ending in consonant (except s), i, u: ultimate (oxytone)
        /// </summary>
        private static int FindStressPosition(string word)
        {
            int vowelGroupCount = CountVowelGroups(word);

            // Find accented vowel group position (digraph-aware)
            int accentGroup = -1;
            int currentGroup = 0;
            int i = 0;
            int n = word.Length;

            while (i < n)
            {
                char ch = word[i];

                // Skip digraphs the same way as CountVowelGroups
                if (ch == 'q' && i + 1 < n && word[i + 1] == 'u')
                {
                    if (i + 2 < n && SoftVowelTriggers.Contains(word[i + 2]))
                    {
                        i += 2;
                        continue;
                    }
                    else
                    {
                        // qu before a/o: u is /w/ glide, not a vowel group
                        i += 2;
                        continue;
                    }
                }

                if (ch == 'g' && i + 1 < n && word[i + 1] == 'u')
                {
                    if (i + 2 < n && SoftVowelTriggers.Contains(word[i + 2]))
                    {
                        i += 2;
                        continue;
                    }
                }

                if (ch == 'o' && i + 1 < n && word[i + 1] == 'u')
                {
                    // Check if either letter in 'ou' is accented
                    if (StressAccents.Contains(ch) || Circumflex.Contains(ch) || Tilde.Contains(ch))
                    {
                        accentGroup = currentGroup;
                    }
                    currentGroup++;
                    i += 2;
                    continue;
                }

                if (VowelChars.Contains(ch))
                {
                    if (StressAccents.Contains(ch) || Circumflex.Contains(ch) || Tilde.Contains(ch))
                    {
                        accentGroup = currentGroup;
                    }
                    currentGroup++;
                }

                i++;
            }

            if (vowelGroupCount == 0)
                return 0;

            if (accentGroup >= 0)
            {
                // Stress on accented syllable (convert to from-end index)
                return vowelGroupCount - 1 - accentGroup;
            }

            // Default rules based on ending
            string stripped = word.TrimEnd('s');

            if (stripped.EndsWith("am", StringComparison.Ordinal) ||
                stripped.EndsWith("em", StringComparison.Ordinal) ||
                stripped.EndsWith("en", StringComparison.Ordinal) ||
                stripped.EndsWith("a", StringComparison.Ordinal) ||
                stripped.EndsWith("e", StringComparison.Ordinal) ||
                stripped.EndsWith("o", StringComparison.Ordinal))
            {
                // Paroxytone: penultimate syllable
                return Math.Min(1, vowelGroupCount - 1);
            }
            else
            {
                // Oxytone: last syllable
                return 0;
            }
        }

        // =====================================================================
        // Word conversion (G2P rules)
        // =====================================================================

        /// <summary>
        /// Convert a Portuguese word to IPA phonemes.
        /// Returns (phonemes, stress_vowel_index) where stress_vowel_index is the
        /// index into phonemes of the primary stressed vowel.
        /// </summary>
        private static (List<string> phonemes, int stressIdx) ConvertWord(string word)
        {
            var phonemes = new List<string>();
            int stressIdx = -1;
            int i = 0;
            int n = word.Length;

            // Determine which vowel group gets stress (using digraph-aware counting)
            int stressFromEnd = FindStressPosition(word);
            int vowelGroupCount = CountVowelGroups(word);
            int stressVowelTarget = vowelGroupCount - 1 - stressFromEnd;
            int currentVowelGroup = 0;

            while (i < n)
            {
                char ch = word[i];

                // --- Multi-character sequences (check longest first) ---

                // "nh" -> palatal nasal
                if (ch == 'n' && i + 1 < n && word[i + 1] == 'h')
                {
                    phonemes.Add("\u0272"); // ɲ
                    i += 2;
                    continue;
                }

                // "lh" -> palatal lateral
                if (ch == 'l' && i + 1 < n && word[i + 1] == 'h')
                {
                    phonemes.Add("\u028e"); // ʎ
                    i += 2;
                    continue;
                }

                // "ch" -> voiceless postalveolar fricative
                if (ch == 'c' && i + 1 < n && word[i + 1] == 'h')
                {
                    phonemes.Add("\u0283"); // ʃ
                    i += 2;
                    continue;
                }

                // "rr" -> uvular fricative
                if (ch == 'r' && i + 1 < n && word[i + 1] == 'r')
                {
                    phonemes.Add("\u0281"); // ʁ
                    i += 2;
                    continue;
                }

                // "ss" -> voiceless alveolar fricative
                if (ch == 's' && i + 1 < n && word[i + 1] == 's')
                {
                    phonemes.Add("s");
                    i += 2;
                    continue;
                }

                // "sc" before e/i -> s (no geminate; like Spanish seseo)
                if (ch == 's' && i + 1 < n && word[i + 1] == 'c')
                {
                    if (i + 2 < n && SoftVowelTriggers.Contains(word[i + 2]))
                    {
                        phonemes.Add("s");
                        i += 2; // skip "sc", vowel handled next
                        continue;
                    }
                }

                // "qu" before e/i -> k (u is silent)
                // "qu" before a/o -> kw (u is pronounced)
                if (ch == 'q' && i + 1 < n && word[i + 1] == 'u')
                {
                    phonemes.Add("k");
                    if (i + 2 < n && SoftVowelTriggers.Contains(word[i + 2]))
                    {
                        // Silent u before e/i
                        i += 2;
                    }
                    else
                    {
                        // Pronounced u before a/o -> append w glide
                        phonemes.Add("w");
                        i += 2;
                    }
                    continue;
                }

                // "gu" before e/i -> voiced velar stop (u is silent)
                if (ch == 'g' && i + 1 < n && word[i + 1] == 'u')
                {
                    if (i + 2 < n && SoftVowelTriggers.Contains(word[i + 2]))
                    {
                        phonemes.Add("\u0261"); // ɡ
                        i += 2;
                        continue;
                    }
                }

                // "ou" -> o (common BR reduction, single vowel group)
                if (ch == 'o' && i + 1 < n && word[i + 1] == 'u')
                {
                    bool isStressed = currentVowelGroup == stressVowelTarget;
                    if (isStressed)
                    {
                        stressIdx = phonemes.Count;
                    }
                    phonemes.Add("o");
                    currentVowelGroup++;
                    i += 2;
                    continue;
                }

                // --- Consonants ---

                if (ch == 'r')
                {
                    // Intervocalic r (vowel before AND vowel after) -> alveolar tap
                    // All other positions (word-initial, word-final, after consonant,
                    // before consonant / coda) -> uvular fricative
                    if (IsIntervocalic(i, word))
                    {
                        phonemes.Add("\u027e"); // ɾ
                    }
                    else
                    {
                        phonemes.Add("\u0281"); // ʁ
                    }
                    i++;
                    continue;
                }

                if (ch == 's')
                {
                    // Intervocalic s -> z
                    if (i > 0 && i + 1 < n && IsVowelChar(word[i - 1]) && IsVowelChar(word[i + 1]))
                    {
                        phonemes.Add("z");
                    }
                    else
                    {
                        phonemes.Add("s");
                    }
                    i++;
                    continue;
                }

                if (ch == 'x')
                {
                    // Common x rules (simplified):
                    // Initial or after "en" -> voiceless postalveolar fricative,
                    // between vowels -> z or s
                    if (i == 0)
                    {
                        phonemes.Add("\u0283"); // ʃ
                    }
                    else if (i > 0 && IsVowelChar(word[i - 1]) && i + 1 < n && IsVowelChar(word[i + 1]))
                    {
                        phonemes.Add("z");
                    }
                    else
                    {
                        phonemes.Add("\u0283"); // ʃ
                    }
                    i++;
                    continue;
                }

                if (ch == 'c')
                {
                    // c before e/i -> s, otherwise -> k
                    if (i + 1 < n && SoftVowelTriggers.Contains(word[i + 1]))
                    {
                        phonemes.Add("s");
                    }
                    else
                    {
                        phonemes.Add("k");
                    }
                    i++;
                    continue;
                }

                if (ch == '\u00e7') // c-cedilla
                {
                    phonemes.Add("s");
                    i++;
                    continue;
                }

                if (ch == 'g')
                {
                    // g before e/i -> voiced postalveolar fricative, otherwise -> voiced velar stop
                    if (i + 1 < n && SoftVowelTriggers.Contains(word[i + 1]))
                    {
                        phonemes.Add("\u0292"); // ʒ
                    }
                    else
                    {
                        phonemes.Add("\u0261"); // ɡ
                    }
                    i++;
                    continue;
                }

                if (ch == 'j')
                {
                    phonemes.Add("\u0292"); // ʒ
                    i++;
                    continue;
                }

                if (ch == 't')
                {
                    // Brazilian Portuguese: t before i -> voiceless postalveolar affricate
                    // (palatalization before unstressed final -e is handled in post-processing)
                    if (i + 1 < n && (word[i + 1] == 'i' || word[i + 1] == '\u00ed'))
                    {
                        phonemes.Add("t\u0283"); // tʃ
                    }
                    else
                    {
                        phonemes.Add("t");
                    }
                    i++;
                    continue;
                }

                if (ch == 'd')
                {
                    // Brazilian Portuguese: d before i -> voiced postalveolar affricate
                    // (palatalization before unstressed final -e is handled in post-processing)
                    if (i + 1 < n && (word[i + 1] == 'i' || word[i + 1] == '\u00ed'))
                    {
                        phonemes.Add("d\u0292"); // dʒ
                    }
                    else
                    {
                        phonemes.Add("d");
                    }
                    i++;
                    continue;
                }

                if (ch == 'h')
                {
                    // h is silent in Portuguese (except in digraphs already handled)
                    i++;
                    continue;
                }

                // Simple consonant mappings
                if (SimpleConsonants.Contains(ch))
                {
                    phonemes.Add(ch.ToString());
                    i++;
                    continue;
                }

                if (ch == 'z')
                {
                    phonemes.Add("z");
                    i++;
                    continue;
                }

                if (ch == 'w')
                {
                    phonemes.Add("w");
                    i++;
                    continue;
                }

                // --- Vowels ---

                if (VowelChars.Contains(ch))
                {
                    bool isStressed = currentVowelGroup == stressVowelTarget;
                    char baseVowel = AccentedToBase.TryGetValue(ch, out var mapped) ? mapped : ch;

                    // Check for nasalization: tilde or vowel before n/m + consonant/end
                    // Exception: vowel before "nh" digraph is NOT nasal (nh = palatal nasal)
                    bool isNasal = false;
                    bool nasalAbsorbed = false; // True when n/m is absorbed into nasalization

                    if (Tilde.Contains(ch))
                    {
                        isNasal = true;
                    }
                    else if (i + 1 < n && (word[i + 1] == 'n' || word[i + 1] == 'm'))
                    {
                        // Check for "nh" digraph -- do NOT nasalize before nh
                        if (word[i + 1] == 'n' && i + 2 < n && word[i + 2] == 'h')
                        {
                            isNasal = false;
                        }
                        else if (i + 2 >= n)
                        {
                            // Nasal: n/m at end of word -- absorb the nasal consonant
                            isNasal = true;
                            nasalAbsorbed = true;
                        }
                        else if (!IsVowelChar(word[i + 2]))
                        {
                            // Nasal: n/m followed by consonant -- absorb the nasal coda
                            isNasal = true;
                            nasalAbsorbed = true;
                        }
                    }

                    string phoneme;

                    if (isNasal)
                    {
                        phoneme = NasalVowelMap.ContainsKey(baseVowel)
                            ? NasalVowelMap[baseVowel]
                            : baseVowel.ToString();
                    }
                    else if (StressAccents.Contains(ch))
                    {
                        // Acute accent = open vowel
                        phoneme = OpenVowelMap.ContainsKey(baseVowel)
                            ? OpenVowelMap[baseVowel]
                            : baseVowel.ToString();
                    }
                    else if (Circumflex.Contains(ch))
                    {
                        // Circumflex = closed vowel
                        phoneme = baseVowel.ToString();
                    }
                    else
                    {
                        phoneme = baseVowel.ToString();
                    }

                    if (isStressed)
                    {
                        stressIdx = phonemes.Count;
                    }
                    phonemes.Add(phoneme);
                    currentVowelGroup++;

                    // Advance past the absorbed nasal consonant (n/m already encoded
                    // in the nasal vowel; skip it to avoid redundant coda)
                    if (nasalAbsorbed)
                    {
                        i += 2; // skip vowel + nasal consonant
                    }
                    else
                    {
                        i++;
                    }
                    continue;
                }

                // Punctuation or unknown: pass through
                if (Punctuation.Contains(ch))
                {
                    phonemes.Add(ch.ToString());
                    i++;
                    continue;
                }

                // Skip unknown characters
                i++;
            }

            // Apply BR Portuguese post-processing
            stressIdx = RemoveDuplicateNasalCoda(phonemes, stressIdx);
            phonemes = ApplyCodaLVocalization(phonemes);
            phonemes = ApplyBrPostprocessing(phonemes, stressIdx);

            return (phonemes, stressIdx);
        }

        // =====================================================================
        // Post-processing: duplicate nasal coda removal
        // =====================================================================

        /// <summary>
        /// Remove duplicate nasal consonant after nasal vowel at word end.
        /// When a word ends in a nasal vowel + nasal consonant (n or m), the nasal
        /// consonant is redundant because the nasality is already encoded in the vowel.
        /// Example: "bom" might produce [b, nasal-o, m] -> [b, nasal-o]
        /// </summary>
        private static int RemoveDuplicateNasalCoda(List<string> result, int stressIdx)
        {
            // Process from end, looking for patterns: nasal_vowel + n/m at word boundary
            int i = result.Count - 1;
            while (i >= 1)
            {
                // Check for nasal vowel followed by n/m
                if ((result[i] == "n" || result[i] == "m") && IpaNasalVowels.Contains(result[i - 1]))
                {
                    // Check this is at word end (next is space, punctuation, or end)
                    bool atBoundary = (i == result.Count - 1) ||
                                      result[i + 1] == " " ||
                                      (result[i + 1].Length == 1 && Punctuation.Contains(result[i + 1][0]));
                    if (atBoundary)
                    {
                        result.RemoveAt(i);
                        if (i < stressIdx)
                        {
                            stressIdx--;
                        }
                    }
                }
                i--;
            }

            return stressIdx;
        }

        // =====================================================================
        // Post-processing: coda-l vocalization
        // =====================================================================

        /// <summary>
        /// Vocalize l in syllable coda position to [w] (BR Portuguese).
        /// In Brazilian Portuguese, /l/ becomes [w] when in coda position
        /// (before a consonant or at word end).
        /// Examples: "Brasil" -> [w] not [l], "alto" -> [w] not [l]
        /// </summary>
        private static List<string> ApplyCodaLVocalization(List<string> phonemes)
        {
            var result = new List<string>(phonemes);

            for (int i = 0; i < result.Count; i++)
            {
                if (result[i] != "l")
                    continue;

                // l at end of phoneme list -> coda
                if (i == result.Count - 1)
                {
                    result[i] = "w";
                    continue;
                }

                string nextPh = result[i + 1];

                // l before space or punctuation -> coda (word-final)
                if (nextPh == " " || (nextPh.Length == 1 && Punctuation.Contains(nextPh[0])))
                {
                    result[i] = "w";
                    continue;
                }

                // l before a consonant -> coda
                // Check first character for multi-char phonemes like tsh, dzh
                bool nextIsConsonant = IpaConsonants.Contains(nextPh) ||
                                       (nextPh.Length > 1 && IpaConsonants.Contains(nextPh[0].ToString()));
                bool nextIsVowel = IpaVowels.Contains(nextPh);

                if (nextIsConsonant && !nextIsVowel)
                {
                    result[i] = "w";
                }
            }

            return result;
        }

        // =====================================================================
        // Post-processing: BR palatalization and vowel reduction
        // =====================================================================

        /// <summary>
        /// Apply Brazilian Portuguese phonological rules as post-processing.
        /// 1. t/d palatalization before unstressed final -e:
        ///    - te# (unstressed) -> tsh + i
        ///    - de# (unstressed) -> dzh + i
        /// 2. Unstressed final vowel reduction:
        ///    - Unstressed final e -> i
        ///    - Unstressed final o -> u
        /// </summary>
        private static List<string> ApplyBrPostprocessing(List<string> phonemes, int stressIdx)
        {
            var result = new List<string>(phonemes);

            // Find word boundaries in the phoneme list
            var wordRanges = FindWordRanges(result);

            foreach (var (start, end) in wordRanges)
            {
                // Check for t/d + e at word end (before punctuation or end)
                // end is exclusive index
                if (end - start < 2)
                    continue;

                int lastPhonemeIdx = end - 1;

                // Skip trailing punctuation within this word range
                while (lastPhonemeIdx >= start &&
                       result[lastPhonemeIdx].Length == 1 &&
                       Punctuation.Contains(result[lastPhonemeIdx][0]))
                {
                    lastPhonemeIdx--;
                }

                if (lastPhonemeIdx < start)
                    continue;

                // Check if last phoneme is unstressed 'e'
                if (result[lastPhonemeIdx] == "e" && lastPhonemeIdx != stressIdx)
                {
                    // Check if preceded by 't'
                    if (lastPhonemeIdx >= start + 1 && result[lastPhonemeIdx - 1] == "t")
                    {
                        // t + unstressed final e -> tsh i (single affricate token)
                        result[lastPhonemeIdx - 1] = "t\u0283"; // tʃ
                        result[lastPhonemeIdx] = "i";
                        continue;
                    }

                    // Check if preceded by 'd'
                    if (lastPhonemeIdx >= start + 1 && result[lastPhonemeIdx - 1] == "d")
                    {
                        // d + unstressed final e -> dzh i (single affricate token)
                        result[lastPhonemeIdx - 1] = "d\u0292"; // dʒ
                        result[lastPhonemeIdx] = "i";
                        continue;
                    }

                    // Unstressed final e -> i (general reduction)
                    result[lastPhonemeIdx] = "i";
                }
                else if (result[lastPhonemeIdx] == "o" && lastPhonemeIdx != stressIdx)
                {
                    // Unstressed final o -> u
                    result[lastPhonemeIdx] = "u";
                }
            }

            return result;
        }

        /// <summary>
        /// Find (start, end) ranges for each word in the phoneme list.
        /// Words are delimited by space phonemes.
        /// </summary>
        private static List<(int start, int end)> FindWordRanges(List<string> phonemes)
        {
            var ranges = new List<(int start, int end)>();
            int start = 0;

            for (int i = 0; i < phonemes.Count; i++)
            {
                if (phonemes[i] == " ")
                {
                    if (i > start)
                    {
                        ranges.Add((start, i));
                    }
                    start = i + 1;
                }
            }

            if (start < phonemes.Count)
            {
                ranges.Add((start, phonemes.Count));
            }

            return ranges;
        }

        // =====================================================================
        // PUA mapping
        // =====================================================================

        /// <summary>
        /// Map multi-character phoneme tokens to single PUA codepoints.
        /// Only "tsh" and "dzh" require PUA mapping for Portuguese.
        /// Single-character phonemes are passed through unchanged.
        /// </summary>
        private static List<string> MapToPua(List<string> phonemes)
        {
            var result = new List<string>(phonemes.Count);

            foreach (var ph in phonemes)
            {
                if (ph == "t\u0283") // tʃ
                {
                    result.Add(PuaAffricateTsh.ToString());
                }
                else if (ph == "d\u0292") // dʒ
                {
                    result.Add(PuaAffricateDzh.ToString());
                }
                else
                {
                    result.Add(ph);
                }
            }

            return result;
        }
    }
}