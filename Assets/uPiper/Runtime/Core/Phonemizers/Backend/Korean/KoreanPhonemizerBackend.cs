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

namespace uPiper.Core.Phonemizers.Backend.Korean
{
    /// <summary>
    /// Korean phonemizer backend using Hangul decomposition and IPA mapping.
    /// Implements phonological rules (liaison, nasalization, aspiration, tensification)
    /// without external dependencies (g2pk2/MeCab).
    /// </summary>
    public class KoreanPhonemizerBackend : PhonemizerBackendBase
    {
        // =====================================================================
        // Constants
        // =====================================================================

        public override string Name => "Korean";
        public override string Version => "1.0.0";
        public override string License => "MIT";
        public override string[] SupportedLanguages => new[] { "ko", "ko-KR" };

        // Hangul syllable block range (U+AC00 .. U+D7A3)
        private const int HangulStart = 0xAC00;
        private const int HangulEnd = 0xD7A3;

        // Decomposition constants
        private const int NMedials = 21;
        private const int NFinals = 28;

        // =====================================================================
        // PUA (Private Use Area) mappings
        // Shared with Chinese: aspirated/affricate consonants
        // =====================================================================

        // Shared with Chinese
        private const char PuaAspiratedP = '\uE020';   // ph (aspirated bilabial)
        private const char PuaAspiratedT = '\uE021';   // th (aspirated alveolar)
        private const char PuaAspiratedK = '\uE022';   // kh (aspirated velar)
        private const char PuaAffricateJ = '\uE023';    // tc (alveolo-palatal affricate)
        private const char PuaAffricateJh = '\uE024';   // tch (aspirated alveolo-palatal)

        // Korean tense consonants (fortis)
        private const char PuaTenseP = '\uE04B';   // p_tense (tense bilabial)
        private const char PuaTenseT = '\uE04C';   // t_tense (tense alveolar)
        private const char PuaTenseK = '\uE04D';   // k_tense (tense velar)
        private const char PuaTenseS = '\uE04E';   // s_tense (tense sibilant)
        private const char PuaTenseTc = '\uE04F';  // tc_tense (tense alveolo-palatal)

        // Korean unreleased finals
        private const char PuaUnreleasedK = '\uE050'; // k_unreleased
        private const char PuaUnreleasedT = '\uE051'; // t_unreleased
        private const char PuaUnreleasedP = '\uE052'; // p_unreleased

        // =====================================================================
        // IPA token strings (multi-character IPA used for PUA mapping)
        // =====================================================================

        // Aspirated (shared with Chinese)
        private const string IpaAspiratedP = "p\u02B0";  // ph
        private const string IpaAspiratedT = "t\u02B0";  // th
        private const string IpaAspiratedK = "k\u02B0";  // kh
        private const string IpaAffricateJ = "t\u0255";   // tc (alveolo-palatal)
        private const string IpaAffricateJh = "t\u0255\u02B0"; // tch

        // Tense
        private const string IpaTenseP = "p\u0348";       // p_tense
        private const string IpaTenseT = "t\u0348";       // t_tense
        private const string IpaTenseK = "k\u0348";       // k_tense
        private const string IpaTenseS = "s\u0348";       // s_tense
        private const string IpaTenseTc = "t\u0348\u0255"; // tc_tense

        // Unreleased
        private const string IpaUnreleasedK = "k\u031A"; // k_unreleased
        private const string IpaUnreleasedT = "t\u031A"; // t_unreleased
        private const string IpaUnreleasedP = "p\u031A"; // p_unreleased

        // =====================================================================
        // Initial consonants (chosung) - 19 entries, index -> IPA token list
        // =====================================================================

        private static readonly string[][] InitialToIpa =
        {
            new[] { "k" },              // 0: g (lax velar)
            new[] { IpaTenseK },        // 1: gg (tense velar)
            new[] { "n" },              // 2: n
            new[] { "t" },              // 3: d (lax alveolar)
            new[] { IpaTenseT },        // 4: dd (tense alveolar)
            new[] { "\u027E" },         // 5: r/l (alveolar tap)
            new[] { "m" },              // 6: m
            new[] { "p" },              // 7: b (lax bilabial)
            new[] { IpaTenseP },        // 8: bb (tense bilabial)
            new[] { "s" },              // 9: s
            new[] { IpaTenseS },        // 10: ss (tense sibilant)
            Array.Empty<string>(),      // 11: ieung (silent in initial position)
            new[] { IpaAffricateJ },    // 12: j (alveolo-palatal affricate)
            new[] { IpaTenseTc },       // 13: jj (tense affricate)
            new[] { IpaAffricateJh },   // 14: ch (aspirated affricate)
            new[] { IpaAspiratedK },    // 15: k (aspirated velar)
            new[] { IpaAspiratedT },    // 16: t (aspirated alveolar)
            new[] { IpaAspiratedP },    // 17: p (aspirated bilabial)
            new[] { "h" },              // 18: h
        };

        // =====================================================================
        // Medial vowels (jungsung) - 21 entries, index -> IPA token list
        // Diphthongs are decomposed into glide + vowel sequences.
        // =====================================================================

        private static readonly string[][] MedialToIpa =
        {
            new[] { "a" },             // 0: a
            new[] { "\u025B" },        // 1: ae (open-mid front)
            new[] { "j", "a" },        // 2: ya
            new[] { "j", "\u025B" },   // 3: yae
            new[] { "\u028C" },        // 4: eo (open-mid back unrounded)
            new[] { "e" },             // 5: e
            new[] { "j", "\u028C" },   // 6: yeo
            new[] { "j", "e" },        // 7: ye
            new[] { "o" },             // 8: o
            new[] { "w", "a" },        // 9: wa
            new[] { "w", "\u025B" },   // 10: wae
            new[] { "w", "e" },        // 11: oe (modern Seoul: diphthong [we])
            new[] { "j", "o" },        // 12: yo
            new[] { "u" },             // 13: u
            new[] { "w", "\u028C" },   // 14: wo
            new[] { "w", "e" },        // 15: we
            new[] { "w", "i" },        // 16: wi
            new[] { "j", "u" },        // 17: yu
            new[] { "\u026F" },        // 18: eu (close back unrounded)
            new[] { "\u0270", "i" },   // 19: ui (velar approximant + i)
            new[] { "i" },             // 20: i
        };

        // =====================================================================
        // Final consonants (jongsung) - 28 entries, index -> IPA token list
        // Index 0 = no final consonant.
        // Complex finals are simplified to their representative sound.
        // =====================================================================

        private static readonly string[][] FinalToIpa =
        {
            Array.Empty<string>(),        // 0: (none)
            new[] { IpaUnreleasedK },     // 1: g -> k_unreleased
            new[] { IpaUnreleasedK },     // 2: gg -> k_unreleased
            new[] { IpaUnreleasedK },     // 3: gs (g+s) -> k_unreleased
            new[] { "n" },                // 4: n
            new[] { "n" },                // 5: nj (n+j) -> n
            new[] { "n" },                // 6: nh (n+h) -> n
            new[] { IpaUnreleasedT },     // 7: d -> t_unreleased
            new[] { "l" },                // 8: l
            new[] { IpaUnreleasedK },     // 9: lg (l+g) -> k_unreleased
            new[] { "m" },                // 10: lm (l+m) -> m
            new[] { "l" },                // 11: lb (l+b) -> l
            new[] { "l" },                // 12: ls (l+s) -> l
            new[] { "l" },                // 13: lt (l+t) -> l
            new[] { "l" },                // 14: lp (l+p) -> l
            new[] { "l" },                // 15: lh (l+h) -> l
            new[] { "m" },                // 16: m
            new[] { IpaUnreleasedP },     // 17: b -> p_unreleased
            new[] { IpaUnreleasedP },     // 18: bs (b+s) -> p_unreleased
            new[] { IpaUnreleasedT },     // 19: s -> t_unreleased
            new[] { IpaUnreleasedT },     // 20: ss -> t_unreleased
            new[] { "\u014B" },           // 21: ng (velar nasal)
            new[] { IpaUnreleasedT },     // 22: j -> t_unreleased
            new[] { IpaUnreleasedT },     // 23: ch -> t_unreleased
            new[] { IpaUnreleasedK },     // 24: k -> k_unreleased
            new[] { IpaUnreleasedT },     // 25: t -> t_unreleased
            new[] { IpaUnreleasedP },     // 26: p -> p_unreleased
            new[] { IpaUnreleasedT },     // 27: h -> t_unreleased
        };

        // =====================================================================
        // Phonological rule tables
        // =====================================================================

        // Jongsung index -> initial consonant index for liaison (yeoneum-hwa)
        // Maps the final consonant to the initial it becomes when followed by ieung
        private static readonly int[] FinalToInitialForLiaison =
        {
            -1,  // 0: none
            0,   // 1: g -> g
            0,   // 2: gg -> g
            9,   // 3: gs -> s (second element)
            2,   // 4: n -> n
            12,  // 5: nj -> j (second element)
            -1,  // 6: nh -> h is absorbed
            3,   // 7: d -> d
            5,   // 8: l -> r
            0,   // 9: lg -> g (second element)
            6,   // 10: lm -> m (second element)
            7,   // 11: lb -> b (second element)
            9,   // 12: ls -> s (second element)
            16,  // 13: lt -> t (second element)
            17,  // 14: lp -> p (second element)
            -1,  // 15: lh -> h is absorbed
            6,   // 16: m -> m
            7,   // 17: b -> b
            9,   // 18: bs -> s (second element)
            9,   // 19: s -> s
            10,  // 20: ss -> ss
            -1,  // 21: ng -> stays as ng (not liaison candidate)
            12,  // 22: j -> j
            14,  // 23: ch -> ch
            15,  // 24: k -> k
            16,  // 25: t -> t
            17,  // 26: p -> p
            -1,  // 27: h -> absorbed
        };

        // Complex final first element index (for complex finals that split)
        // Maps compound jongsung -> remaining jongsung after liaison takes second element
        private static readonly int[] ComplexFinalFirstElement =
        {
            -1, -1, -1,
            1,   // 3: gs -> g remains (index 1)
            -1,
            4,   // 5: nj -> n remains (index 4)
            4,   // 6: nh -> n remains (index 4)
            -1,
            -1,
            8,   // 9: lg -> l remains (index 8)
            8,   // 10: lm -> l remains (index 8)
            8,   // 11: lb -> l remains (index 8)
            8,   // 12: ls -> l remains (index 8)
            8,   // 13: lt -> l remains (index 8)
            8,   // 14: lp -> l remains (index 8)
            8,   // 15: lh -> l remains (index 8)
            -1, -1,
            17,  // 18: bs -> b remains (index 17)
            -1, -1, -1, -1, -1, -1, -1, -1, -1,
        };

        // Nasalization: obstruent final + nasal initial -> nasal final
        // Maps jongsung representative sound class to nasalized jongsung IPA
        // k-class finals (1,2,3,9,24) before n/m -> ng
        // t-class finals (7,19,20,22,23,25,27) before n/m -> n
        // p-class finals (17,18,26) before n/m -> m
        private static readonly Dictionary<int, string> FinalNasalizationMap = new()
        {
            // k-class -> ng
            { 1, "\u014B" }, { 2, "\u014B" }, { 3, "\u014B" },
            { 9, "\u014B" }, { 24, "\u014B" },
            // t-class -> n
            { 7, "n" }, { 19, "n" }, { 20, "n" },
            { 22, "n" }, { 23, "n" }, { 25, "n" }, { 27, "n" },
            // p-class -> m
            { 17, "m" }, { 18, "m" }, { 26, "m" },
        };

        // Aspiration: h + lax consonant or lax final + h
        // initial index -> aspirated initial index
        private static readonly Dictionary<int, int> LaxToAspiratedInitial = new()
        {
            { 0, 15 },  // g -> k
            { 3, 16 },  // d -> t
            { 7, 17 },  // b -> p
            { 12, 14 }, // j -> ch
        };

        // Tensification: obstruent final + lax initial -> tense initial
        private static readonly Dictionary<int, int> LaxToTenseInitial = new()
        {
            { 0, 1 },   // g -> gg
            { 3, 4 },   // d -> dd
            { 7, 8 },   // b -> bb
            { 9, 10 },  // s -> ss
            { 12, 13 }, // j -> jj
        };

        // Set of obstruent final indices (for tensification trigger)
        private static readonly HashSet<int> ObstruentFinals = new()
        {
            1, 2, 3, 7, 9, 17, 18, 19, 20, 22, 23, 24, 25, 26
        };

        // Nasal initial indices (for nasalization trigger)
        private static readonly HashSet<int> NasalInitials = new()
        {
            2, // n
            6, // m
        };

        // Punctuation characters (passed through as-is)
        private static readonly HashSet<char> Punctuation = new()
        {
            ',', '.', ';', ':', '!', '?',
            '\u3002', '\uFF0C', '\uFF01', '\uFF1F', '\u3001'
        };

        // Regex to split text into word-tokens and whitespace
        private static readonly Regex WordSplitRegex = new(@"(\s+)", RegexOptions.Compiled);

        // PUA mapping: multi-character IPA string -> single PUA character
        private static readonly Dictionary<string, char> IpaToPua = new()
        {
            // Shared with Chinese
            { IpaAspiratedP, PuaAspiratedP },
            { IpaAspiratedT, PuaAspiratedT },
            { IpaAspiratedK, PuaAspiratedK },
            { IpaAffricateJ, PuaAffricateJ },
            { IpaAffricateJh, PuaAffricateJh },
            // Korean tense
            { IpaTenseP, PuaTenseP },
            { IpaTenseT, PuaTenseT },
            { IpaTenseK, PuaTenseK },
            { IpaTenseS, PuaTenseS },
            { IpaTenseTc, PuaTenseTc },
            // Korean unreleased
            { IpaUnreleasedK, PuaUnreleasedK },
            { IpaUnreleasedT, PuaUnreleasedT },
            { IpaUnreleasedP, PuaUnreleasedP },
        };

        private readonly object _syncLock = new();

        // =====================================================================
        // PhonemizerBackendBase overrides
        // =====================================================================

        protected override Task<bool> InitializeInternalAsync(
            PhonemizerBackendOptions options,
            CancellationToken cancellationToken)
        {
            // Pure algorithmic backend - no external data files required
            return Task.FromResult(true);
        }

#pragma warning disable CS1998
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

#if UNITY_WEBGL && !UNITY_EDITOR
            lock (_syncLock)
            {
                return PhonemizeInternal(text, language);
            }
#else
            return await Task.Run(() =>
            {
                lock (_syncLock)
                {
                    return PhonemizeInternal(text, language);
                }
            }, cancellationToken);
#endif
        }
#pragma warning restore CS1998

        public override long GetMemoryUsage()
        {
            // Pure algorithmic - minimal memory footprint
            return 64 * 1024; // ~64KB estimate for static tables
        }

        public override BackendCapabilities GetCapabilities()
        {
            return new BackendCapabilities
            {
                SupportsIPA = true,
                SupportsStress = false,
                SupportsSyllables = true,
                SupportsTones = false,
                SupportsDuration = false,
                SupportsBatchProcessing = false,
                IsThreadSafe = true,
                RequiresNetwork = false,
            };
        }

        protected override void DisposeInternal()
        {
            // No managed resources to release
        }

        // =====================================================================
        // Core phonemization
        // =====================================================================

        private PhonemeResult PhonemizeInternal(string text, string language)
        {
            var stopwatch = Stopwatch.StartNew();

            // Normalize to NFC to handle NFD-decomposed Hangul jamo
            text = text.Normalize(NormalizationForm.FormC);

            // Apply phonological rules
            var syllables = DecomposeText(text);
            ApplyPhonologicalRules(syllables);

            // Convert to IPA with prosody
            var phonemes = new List<string>();
            var prosodyA1 = new List<int>();
            var prosodyA2 = new List<int>();
            var prosodyA3 = new List<int>();

            var needSpace = false;

            foreach (var word in syllables)
            {
                if (word.IsWhitespace)
                {
                    needSpace = true;
                    continue;
                }

                // Insert word-boundary space token
                if (needSpace && phonemes.Count > 0)
                {
                    phonemes.Add(" ");
                    prosodyA1.Add(0);
                    prosodyA2.Add(0);
                    prosodyA3.Add(0);
                }

                int syllableCount = CountHangulSyllables(word);
                int a3Value = Math.Max(syllableCount, 1);

                foreach (var unit in word.Units)
                {
                    if (unit.IsPunctuation)
                    {
                        phonemes.Add(unit.OriginalChar.ToString());
                        prosodyA1.Add(0);
                        prosodyA2.Add(0);
                        prosodyA3.Add(0);
                    }
                    else if (unit.IsHangul)
                    {
                        var ipaTokens = SyllableToIpa(unit);
                        foreach (var token in ipaTokens)
                        {
                            phonemes.Add(token);
                            prosodyA1.Add(0);
                            prosodyA2.Add(0);
                            prosodyA3.Add(a3Value);
                        }
                    }
                    else if (char.IsLetter(unit.OriginalChar))
                    {
                        // Non-Hangul alphabetic characters -- pass through
                        phonemes.Add(unit.OriginalChar.ToString());
                        prosodyA1.Add(0);
                        prosodyA2.Add(0);
                        prosodyA3.Add(a3Value);
                    }
                    // Digits and other characters are skipped
                }

                needSpace = true;
            }

            // Map multi-character IPA tokens to PUA codepoints
            var mapped = MapToPua(phonemes);

            stopwatch.Stop();

            return new PhonemeResult
            {
                OriginalText = text,
                Phonemes = mapped.ToArray(),
                Language = language,
                Success = true,
                Backend = Name,
                ProcessingTimeMs = (float)stopwatch.Elapsed.TotalMilliseconds,
                ProcessingTime = stopwatch.Elapsed,
                ProsodyA1 = prosodyA1.ToArray(),
                ProsodyA2 = prosodyA2.ToArray(),
                ProsodyA3 = prosodyA3.ToArray(),
                Metadata = new Dictionary<string, object>
                {
                    ["backend"] = Name,
                    ["phonologicalRules"] = "liaison,nasalization,aspiration,tensification",
                },
            };
        }

        // =====================================================================
        // Hangul decomposition
        // =====================================================================

        /// <summary>
        /// Checks if a character is a composed Hangul syllable (U+AC00..U+D7A3).
        /// </summary>
        private static bool IsHangulSyllable(char ch)
        {
            int code = ch;
            return code >= HangulStart && code <= HangulEnd;
        }

        /// <summary>
        /// Decomposes a Hangul syllable into (initial, medial, final) indices.
        /// </summary>
        private static (int initial, int medial, int final_) DecomposeSyllable(char ch)
        {
            int code = ch - HangulStart;
            int initial = code / (NMedials * NFinals);
            int medial = (code % (NMedials * NFinals)) / NFinals;
            int final_ = code % NFinals;
            return (initial, medial, final_);
        }

        /// <summary>
        /// Converts a decomposed syllable unit to a list of IPA tokens.
        /// Uses the (possibly modified) initial/medial/final indices.
        /// </summary>
        private static List<string> SyllableToIpa(SyllableUnit unit)
        {
            var phonemes = new List<string>();

            if (unit.InitialIndex >= 0 && unit.InitialIndex < InitialToIpa.Length)
            {
                phonemes.AddRange(InitialToIpa[unit.InitialIndex]);
            }

            if (unit.MedialIndex >= 0 && unit.MedialIndex < MedialToIpa.Length)
            {
                phonemes.AddRange(MedialToIpa[unit.MedialIndex]);
            }

            if (unit.FinalIndex >= 0 && unit.FinalIndex < FinalToIpa.Length)
            {
                phonemes.AddRange(FinalToIpa[unit.FinalIndex]);
            }

            return phonemes;
        }

        // =====================================================================
        // Text decomposition into structured word/syllable data
        // =====================================================================

        /// <summary>
        /// Decomposes full text into a list of WordData containing SyllableUnits.
        /// </summary>
        private static List<WordData> DecomposeText(string text)
        {
            var words = new List<WordData>();
            var parts = WordSplitRegex.Split(text);

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part))
                    continue;

                if (IsWhitespace(part))
                {
                    words.Add(new WordData { IsWhitespace = true });
                    continue;
                }

                var word = new WordData();
                foreach (var ch in part)
                {
                    if (IsHangulSyllable(ch))
                    {
                        var (initial, medial, final_) = DecomposeSyllable(ch);
                        word.Units.Add(new SyllableUnit
                        {
                            OriginalChar = ch,
                            IsHangul = true,
                            InitialIndex = initial,
                            MedialIndex = medial,
                            FinalIndex = final_,
                        });
                    }
                    else if (Punctuation.Contains(ch))
                    {
                        word.Units.Add(new SyllableUnit
                        {
                            OriginalChar = ch,
                            IsPunctuation = true,
                            InitialIndex = -1,
                            MedialIndex = -1,
                            FinalIndex = -1,
                        });
                    }
                    else
                    {
                        word.Units.Add(new SyllableUnit
                        {
                            OriginalChar = ch,
                            InitialIndex = -1,
                            MedialIndex = -1,
                            FinalIndex = -1,
                        });
                    }
                }

                words.Add(word);
            }

            return words;
        }

        private static bool IsWhitespace(string s)
        {
            foreach (var ch in s)
            {
                if (!char.IsWhiteSpace(ch))
                    return false;
            }

            return s.Length > 0;
        }

        private static int CountHangulSyllables(WordData word)
        {
            int count = 0;
            foreach (var unit in word.Units)
            {
                if (unit.IsHangul)
                    count++;
            }

            return count;
        }

        // =====================================================================
        // Phonological rules
        // =====================================================================

        /// <summary>
        /// Applies Korean phonological rules across all words.
        /// Rules are applied in order: liaison, nasalization, aspiration, tensification.
        /// These operate on adjacent syllable pairs within each word.
        /// </summary>
        private static void ApplyPhonologicalRules(List<WordData> words)
        {
            foreach (var word in words)
            {
                if (word.IsWhitespace)
                    continue;

                // Collect Hangul syllable indices for pairwise processing
                var hangulIndices = new List<int>();
                for (int i = 0; i < word.Units.Count; i++)
                {
                    if (word.Units[i].IsHangul)
                        hangulIndices.Add(i);
                }

                // Process each pair of adjacent Hangul syllables
                for (int p = 0; p < hangulIndices.Count - 1; p++)
                {
                    int curIdx = hangulIndices[p];
                    int nextIdx = hangulIndices[p + 1];

                    var cur = word.Units[curIdx];
                    var next = word.Units[nextIdx];

                    // Skip if current syllable has no final consonant
                    if (cur.FinalIndex == 0)
                        continue;

                    bool applied = false;

                    // Rule 1: Aspiration (gyeogeum-hwa)
                    // h + lax initial or lax final + h -> aspirated
                    applied = TryApplyAspiration(ref cur, ref next);

                    if (!applied)
                    {
                        // Rule 2: Liaison (yeoneum-hwa)
                        // Final consonant + initial ieung -> move final to initial
                        applied = TryApplyLiaison(ref cur, ref next);
                    }

                    if (!applied)
                    {
                        // Rule 3: Nasalization (bieumhwa)
                        // Obstruent final + nasal initial -> nasalized final
                        applied = TryApplyNasalization(ref cur, ref next);
                    }

                    if (!applied)
                    {
                        // Rule 4: Tensification (gyeongeumhwa)
                        // Obstruent final + lax initial -> tense initial
                        TryApplyTensification(ref cur, ref next);
                    }

                    word.Units[curIdx] = cur;
                    word.Units[nextIdx] = next;
                }
            }
        }

        /// <summary>
        /// Liaison: Final consonant + initial ieung (silent) -> pronounce final as initial.
        /// e.g., "han-in" -> "ha-nin"
        /// </summary>
        private static bool TryApplyLiaison(ref SyllableUnit cur, ref SyllableUnit next)
        {
            // Only applies when next syllable starts with ieung (silent, index 11)
            if (next.InitialIndex != 11)
                return false;

            int finalIdx = cur.FinalIndex;
            if (finalIdx <= 0 || finalIdx >= FinalToInitialForLiaison.Length)
                return false;

            int liaisonInitial = FinalToInitialForLiaison[finalIdx];
            if (liaisonInitial < 0)
            {
                // h-type finals: h is absorbed, ieung remains silent
                // nh (6), lh (15), h (27) -> final becomes simplified, next stays ieung
                if (finalIdx == 6)
                {
                    cur.FinalIndex = 4; // nh -> n remains
                    return true;
                }

                if (finalIdx == 15)
                {
                    cur.FinalIndex = 8; // lh -> l remains
                    return true;
                }

                if (finalIdx == 27)
                {
                    cur.FinalIndex = 0; // h -> removed
                    return true;
                }

                if (finalIdx == 21)
                {
                    // ng stays as final, next stays as ieung (no change)
                    return false;
                }

                return false;
            }

            // Check if this is a complex final (compound jongsung)
            int firstElement = ComplexFinalFirstElement[finalIdx];
            if (firstElement >= 0)
            {
                // Complex final: first element stays as final, second moves to initial
                cur.FinalIndex = firstElement;
                next.InitialIndex = liaisonInitial;
            }
            else
            {
                // Simple final: final is removed, becomes next initial
                cur.FinalIndex = 0;
                next.InitialIndex = liaisonInitial;
            }

            return true;
        }

        /// <summary>
        /// Nasalization: Obstruent final before nasal initial -> nasal final.
        /// e.g., "hanguk-mal" -> "hangung-mal" (k -> ng before m)
        /// Also handles liquid nasalization: l final before nasal -> n.
        /// </summary>
        private static bool TryApplyNasalization(ref SyllableUnit cur, ref SyllableUnit next)
        {
            if (!NasalInitials.Contains(next.InitialIndex))
                return false;

            int finalIdx = cur.FinalIndex;

            // Obstruent finals nasalize before nasal initials
            if (FinalNasalizationMap.TryGetValue(finalIdx, out _))
            {
                // Replace the final with its nasalized counterpart
                // k-class -> ng (index 21), t-class -> n (index 4), p-class -> m (index 16)
                if (finalIdx == 1 || finalIdx == 2 || finalIdx == 3
                    || finalIdx == 9 || finalIdx == 24)
                {
                    cur.FinalIndex = 21; // ng
                }
                else if (finalIdx == 7 || finalIdx == 19 || finalIdx == 20
                         || finalIdx == 22 || finalIdx == 23 || finalIdx == 25
                         || finalIdx == 27)
                {
                    cur.FinalIndex = 4; // n
                }
                else if (finalIdx == 17 || finalIdx == 18 || finalIdx == 26)
                {
                    cur.FinalIndex = 16; // m
                }

                return true;
            }

            // Liquid nasalization: l (8) before nasal -> n
            if (finalIdx == 8 && NasalInitials.Contains(next.InitialIndex))
            {
                cur.FinalIndex = 4; // l -> n
                return true;
            }

            return false;
        }

        /// <summary>
        /// Aspiration: h combined with lax consonant -> aspirated consonant.
        /// Pattern 1: lax final + h initial -> aspirated (final removed)
        /// Pattern 2: h final + lax initial -> aspirated (final removed)
        /// </summary>
        private static bool TryApplyAspiration(ref SyllableUnit cur, ref SyllableUnit next)
        {
            int finalIdx = cur.FinalIndex;
            int nextInitial = next.InitialIndex;

            // Pattern 1: h final (27) or nh (6) or lh (15) + lax initial -> aspirated
            bool isFinalH = finalIdx == 27;
            bool isFinalNh = finalIdx == 6;
            bool isFinalLh = finalIdx == 15;

            if (isFinalH || isFinalNh || isFinalLh)
            {
                if (LaxToAspiratedInitial.TryGetValue(nextInitial, out int aspirated))
                {
                    next.InitialIndex = aspirated;

                    if (isFinalH)
                    {
                        cur.FinalIndex = 0; // remove h
                    }
                    else if (isFinalNh)
                    {
                        cur.FinalIndex = 4; // nh -> n remains
                    }
                    else if (isFinalLh)
                    {
                        cur.FinalIndex = 8; // lh -> l remains
                    }

                    return true;
                }
            }

            // Pattern 2: lax obstruent final + h initial (18) -> aspirated
            if (nextInitial == 18) // h initial
            {
                // Map the final's representative to aspirated form
                // k-class finals -> kh
                if (finalIdx == 1 || finalIdx == 2 || finalIdx == 3
                    || finalIdx == 9 || finalIdx == 24)
                {
                    next.InitialIndex = 15; // kh
                    cur.FinalIndex = 0;
                    return true;
                }

                // t-class finals -> th
                if (finalIdx == 7 || finalIdx == 19 || finalIdx == 20
                    || finalIdx == 22 || finalIdx == 23 || finalIdx == 25)
                {
                    next.InitialIndex = 16; // th
                    cur.FinalIndex = 0;
                    return true;
                }

                // p-class finals -> ph
                if (finalIdx == 17 || finalIdx == 18 || finalIdx == 26)
                {
                    next.InitialIndex = 17; // ph
                    cur.FinalIndex = 0;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Tensification: Obstruent final + lax initial -> tense initial.
        /// e.g., "hakkyo" -> "hak-ggyo" (k + g -> k + gg)
        /// </summary>
        private static bool TryApplyTensification(ref SyllableUnit cur, ref SyllableUnit next)
        {
            if (!ObstruentFinals.Contains(cur.FinalIndex))
                return false;

            if (LaxToTenseInitial.TryGetValue(next.InitialIndex, out int tenseInitial))
            {
                next.InitialIndex = tenseInitial;
                return true;
            }

            return false;
        }

        // =====================================================================
        // PUA mapping
        // =====================================================================

        /// <summary>
        /// Maps multi-character IPA tokens to single PUA codepoints.
        /// Single-character tokens pass through unchanged.
        /// </summary>
        private static List<string> MapToPua(List<string> phonemes)
        {
            var result = new List<string>(phonemes.Count);
            foreach (var token in phonemes)
            {
                if (token.Length <= 1)
                {
                    result.Add(token);
                }
                else if (IpaToPua.TryGetValue(token, out char pua))
                {
                    result.Add(pua.ToString());
                }
                else
                {
                    // Unknown multi-character token -- pass through as-is
                    result.Add(token);
                }
            }

            return result;
        }

        // =====================================================================
        // Internal data structures
        // =====================================================================

        /// <summary>
        /// Represents a word (or whitespace) in the decomposed text.
        /// </summary>
        private class WordData
        {
            public bool IsWhitespace;
            public List<SyllableUnit> Units = new();
        }

        /// <summary>
        /// Represents a single character unit in a word.
        /// For Hangul syllables, stores the decomposed jamo indices which can be
        /// modified by phonological rules before IPA conversion.
        /// </summary>
        private struct SyllableUnit
        {
            public char OriginalChar;
            public bool IsHangul;
            public bool IsPunctuation;
            public int InitialIndex;   // Chosung index (0-18), -1 if not Hangul
            public int MedialIndex;    // Jungsung index (0-20), -1 if not Hangul
            public int FinalIndex;     // Jongsung index (0-27), -1 if not Hangul
        }
    }
}