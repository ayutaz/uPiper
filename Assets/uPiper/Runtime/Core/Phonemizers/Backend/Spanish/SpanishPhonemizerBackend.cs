using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace uPiper.Core.Phonemizers.Backend.Spanish
{
    /// <summary>
    /// Rule-based Spanish G2P (grapheme-to-phoneme) phonemizer backend.
    /// Converts Spanish text to IPA phonemes using orthographic rules.
    /// Uses Latin American Spanish pronunciation by default (seseo: c/z -> s).
    /// Ported from piper-plus Python implementation (spanish.py).
    /// </summary>
    public class SpanishPhonemizerBackend : PhonemizerBackendBase
    {
        // -----------------------------------------------------------------------
        // PUA (Private Use Area) mappings for multi-character phonemes
        // -----------------------------------------------------------------------

        /// <summary>PUA codepoint for Spanish trill "rr".</summary>
        private const char PuaRr = '\uE01D';

        /// <summary>PUA codepoint for voiceless postalveolar affricate "t&#x0283;" (ch).</summary>
        private const char PuaTsh = '\uE054';

        // -----------------------------------------------------------------------
        // Constants and static data
        // -----------------------------------------------------------------------

        /// <summary>Punctuation characters passed through as-is.</summary>
        private static readonly HashSet<char> Punctuation = new()
        {
            '.', ',', ';', ':', '!', '?', '\u00A1', '\u00BF'
        };

        /// <summary>Vowels for context checks.</summary>
        private static readonly HashSet<char> Vowels = new() { 'a', 'e', 'i', 'o', 'u' };

        /// <summary>Accented vowel to base vowel mapping.</summary>
        private static readonly Dictionary<char, char> AccentMap = new()
        {
            { '\u00E1', 'a' }, // a
            { '\u00E9', 'e' }, // e
            { '\u00ED', 'i' }, // i
            { '\u00F3', 'o' }, // o
            { '\u00FA', 'u' }, // u
            { '\u00FC', 'u' }  // u (diaeresis)
        };

        /// <summary>Stress-indicating accent characters (not including diaeresis).</summary>
        private static readonly HashSet<char> StressAccents = new()
        {
            '\u00E1', '\u00E9', '\u00ED', '\u00F3', '\u00FA'
        };

        /// <summary>
        /// Letters that are exceptions to the final-syllable stress rule.
        /// Words ending in consonant other than n/s get final-syllable stress.
        /// </summary>
        private static readonly HashSet<char> StressFinalExceptions = new() { 'n', 's' };

        /// <summary>Strong vowels for hiatus detection.</summary>
        private static readonly HashSet<char> StrongVowels = new() { 'a', 'e', 'o' };

        /// <summary>Weak vowels for diphthong detection.</summary>
        private static readonly HashSet<char> WeakVowels = new() { 'i', 'u' };

        /// <summary>Inseparable onset clusters for syllabification.</summary>
        private static readonly HashSet<string> InseparableClusters = new()
        {
            "bl", "br", "cl", "cr", "dr", "fl", "fr",
            "gl", "gr", "pl", "pr", "tr", "tl"
        };

        /// <summary>
        /// Regex: split text into word tokens and punctuation.
        /// Matches sequences of Spanish word characters or punctuation characters.
        /// </summary>
        private static readonly Regex ReToken = new(
            @"([a-z\u00E1\u00E9\u00ED\u00F3\u00FA\u00FC\u00F1]+|[,.;:!?\u00A1\u00BF]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Common monosyllabic function words that are phonologically unstressed
        /// in connected speech and should not receive the primary stress marker.
        /// </summary>
        private static readonly HashSet<string> UnstressedFunctionWords = new()
        {
            "el", "la", "los", "las", "un", "una",
            "de", "del", "al", "a", "en", "con", "por",
            "y", "o", "que", "se", "me", "te", "le",
            "lo", "nos", "su", "mi", "tu", "es", "no", "si"
        };

        private readonly object _syncLock = new();

        // -----------------------------------------------------------------------
        // PhonemizerBackendBase overrides
        // -----------------------------------------------------------------------

        /// <inheritdoc/>
        public override string Name => "SpanishG2P";

        /// <inheritdoc/>
        public override string Version => "1.0.0";

        /// <inheritdoc/>
        public override string License => "MIT";

        /// <inheritdoc/>
        public override string[] SupportedLanguages => new[] { "es", "es-ES", "es-MX" };

        /// <inheritdoc/>
        protected override Task<bool> InitializeInternalAsync(
            PhonemizerBackendOptions options,
            CancellationToken cancellationToken)
        {
            // Rule-based -- no external data files required.
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

        /// <inheritdoc/>
        public override long GetMemoryUsage()
        {
            // Lightweight rule-based engine -- negligible memory.
            return 64 * 1024; // 64 KB estimate
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        protected override void DisposeInternal()
        {
            // No unmanaged resources.
        }

        // -----------------------------------------------------------------------
        // Internal phonemization entry point
        // -----------------------------------------------------------------------

        private PhonemeResult PhonemizeInternal(string text, string language)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

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
                    ["dialect"] = "Latin American (seseo)"
                }
            };
        }

        // ===================================================================
        // Public API — phonemization with prosody
        // ===================================================================

        /// <summary>
        /// Convert Spanish text to phoneme list and prosody features.
        /// Returns (phonemes, prosodyA1, prosodyA2, prosodyA3).
        /// Prosody: a1=0, a2=stress-based (0 or 2), a3=word phoneme count.
        /// </summary>
        public (string[] Phonemes, int[] ProsodyA1, int[] ProsodyA2, int[] ProsodyA3)
            PhonemizeWithProsody(string text)
        {
            text = Normalize(text);
            var matches = ReToken.Matches(text);

            var phonemeList = new List<string>();
            var a1List = new List<int>();
            var a2List = new List<int>();
            var a3List = new List<int>();
            var needSpace = false;

            foreach (Match match in matches)
            {
                var token = match.Value;

                // Check if pure punctuation
                if (IsPurelyPunctuation(token))
                {
                    foreach (var c in token)
                    {
                        phonemeList.Add(c.ToString());
                        a1List.Add(0);
                        a2List.Add(0);
                        a3List.Add(0);
                    }
                    continue;
                }

                // Regular word — insert space separator between words
                if (needSpace)
                {
                    phonemeList.Add(" ");
                    a1List.Add(0);
                    a2List.Add(0);
                    a3List.Add(0);
                }

                var (wordPhonemes, stressedSyl, units, boundaries) = G2PWord(token);

                // Skip stress marker for common unstressed function words
                List<string> wordWithStress;
                if (UnstressedFunctionWords.Contains(token))
                {
                    wordWithStress = wordPhonemes;
                }
                else
                {
                    wordWithStress = InsertStressMarker(
                        wordPhonemes, token,
                        units, boundaries, stressedSyl);
                }

                var wordPhonemeCount = wordPhonemes.Count; // count without stress marker

                for (int pi = 0; pi < wordWithStress.Count; pi++)
                {
                    var ph = wordWithStress[pi];
                    if (ph == "\u02C8") // stress marker
                    {
                        phonemeList.Add("\u02C8");
                        a1List.Add(0);
                        a2List.Add(2);
                        a3List.Add(wordPhonemeCount);
                    }
                    else
                    {
                        var isStressedVowel = false;
                        // Check if this phoneme is right after a stress marker
                        if (phonemeList.Count > 0
                            && phonemeList[phonemeList.Count - 1] == "\u02C8"
                            && Vowels.Contains(ph.Length == 1 ? ph[0] : '\0'))
                        {
                            isStressedVowel = true;
                        }

                        var a2 = isStressedVowel ? 2 : 0;
                        phonemeList.Add(ph);
                        a1List.Add(0);
                        a2List.Add(a2);
                        a3List.Add(wordPhonemeCount);
                    }
                }

                needSpace = true;
            }

            // Map multi-character phonemes to PUA codepoints
            var mapped = MapToPua(phonemeList);

            return (
                mapped.ToArray(),
                a1List.ToArray(),
                a2List.ToArray(),
                a3List.ToArray()
            );
        }

        // ===================================================================
        // Text normalization
        // ===================================================================

        /// <summary>Lowercase and NFC-normalize the input text.</summary>
        private static string Normalize(string text)
        {
            text = text.ToLowerInvariant();
            text = text.Normalize(NormalizationForm.FormC);
            return text;
        }

        // ===================================================================
        // Grapheme segmentation
        // ===================================================================

        /// <summary>
        /// A grapheme unit produced by segmentation.
        /// </summary>
        private readonly struct GraphemeUnit
        {
            /// <summary>The original grapheme string (may include accented chars).</summary>
            public readonly string Grapheme;

            /// <summary>True if this unit is a vowel.</summary>
            public readonly bool IsVowel;

            /// <summary>True if this unit produces no phoneme (e.g. silent h).</summary>
            public readonly bool IsSilent;

            public GraphemeUnit(string grapheme, bool isVowel, bool isSilent)
            {
                Grapheme = grapheme;
                IsVowel = isVowel;
                IsSilent = isSilent;
            }
        }

        /// <summary>
        /// Split a word into grapheme units respecting Spanish digraphs.
        /// Multi-character graphemes (ch, ll, rr, qu, gu, gu, sc before e/i)
        /// are kept as single units so that syllabification and the
        /// char-to-phoneme walker never tear them apart.
        /// </summary>
        private static List<GraphemeUnit> SegmentGraphemes(string word)
        {
            // Build base-form word for consonant context checks
            var baseWord = new StringBuilder(word.Length);
            foreach (var ch in word)
            {
                baseWord.Append(AccentMap.TryGetValue(ch, out var mapped) ? mapped : ch);
            }

            var units = new List<GraphemeUnit>();
            int n = word.Length;
            int i = 0;

            while (i < n)
            {
                char bch = baseWord[i];

                // --- Multi-character graphemes (longest match first) ---

                // "qu" (u is silent; the following vowel is separate)
                if (bch == 'q' && i + 1 < n && baseWord[i + 1] == 'u')
                {
                    units.Add(new GraphemeUnit(word.Substring(i, 2), false, false));
                    i += 2;
                    continue;
                }

                // "gu" before e/i with diaeresis -- u is pronounced (/gw/)
                if (bch == 'g' && i + 1 < n && word[i + 1] == '\u00FC'
                    && i + 2 < n && (baseWord[i + 2] == 'e' || baseWord[i + 2] == 'i'))
                {
                    units.Add(new GraphemeUnit(word.Substring(i, 2), false, false));
                    i += 2;
                    continue;
                }

                // "gu" before e/i -- u is silent
                if (bch == 'g' && i + 1 < n && baseWord[i + 1] == 'u'
                    && i + 2 < n && (baseWord[i + 2] == 'e' || baseWord[i + 2] == 'i'))
                {
                    units.Add(new GraphemeUnit(word.Substring(i, 2), false, false));
                    i += 2;
                    continue;
                }

                // "ch" -- single consonant unit
                if (bch == 'c' && i + 1 < n && baseWord[i + 1] == 'h')
                {
                    units.Add(new GraphemeUnit(word.Substring(i, 2), false, false));
                    i += 2;
                    continue;
                }

                // "ll" -- single consonant unit
                if (bch == 'l' && i + 1 < n && baseWord[i + 1] == 'l')
                {
                    units.Add(new GraphemeUnit(word.Substring(i, 2), false, false));
                    i += 2;
                    continue;
                }

                // "rr" -- single consonant unit
                if (bch == 'r' && i + 1 < n && baseWord[i + 1] == 'r')
                {
                    units.Add(new GraphemeUnit(word.Substring(i, 2), false, false));
                    i += 2;
                    continue;
                }

                // "sc" before e/i -- single consonant unit (seseo: /s/, no geminate)
                if (bch == 's' && i + 1 < n && baseWord[i + 1] == 'c'
                    && i + 2 < n && (baseWord[i + 2] == 'e' || baseWord[i + 2] == 'i'))
                {
                    units.Add(new GraphemeUnit(word.Substring(i, 2), false, false));
                    i += 2;
                    continue;
                }

                // "xc" before e/i -- single consonant unit (/ks/, c is absorbed)
                if (bch == 'x' && i + 1 < n && baseWord[i + 1] == 'c'
                    && i + 2 < n && (baseWord[i + 2] == 'e' || baseWord[i + 2] == 'i'))
                {
                    units.Add(new GraphemeUnit(word.Substring(i, 2), false, false));
                    i += 2;
                    continue;
                }

                // --- Single characters ---

                // Silent "h"
                if (bch == 'h')
                {
                    units.Add(new GraphemeUnit(word[i].ToString(), false, true));
                    i += 1;
                    continue;
                }

                // Vowels (including accented)
                if (Vowels.Contains(bch))
                {
                    units.Add(new GraphemeUnit(word[i].ToString(), true, false));
                    i += 1;
                    continue;
                }

                // All other consonants
                units.Add(new GraphemeUnit(word[i].ToString(), false, false));
                i += 1;
            }

            return units;
        }

        // ===================================================================
        // Syllabification
        // ===================================================================

        /// <summary>
        /// Return list of grapheme-unit indices where each syllable starts.
        /// Operates on grapheme units so that digraphs are treated as single
        /// consonant units and are never split across syllables.
        /// </summary>
        private static List<int> FindSyllableBoundaries(
            string word, List<GraphemeUnit> units = null)
        {
            if (units == null)
                units = SegmentGraphemes(word);

            // Build vowel/consonant mask, skipping silent units.
            var nonSilentIdx = new List<int>();
            var isVowelNs = new List<bool>();

            for (int idx = 0; idx < units.Count; idx++)
            {
                if (units[idx].IsSilent)
                    continue;
                nonSilentIdx.Add(idx);
                isVowelNs.Add(units[idx].IsVowel);
            }

            int nsN = nonSilentIdx.Count;
            if (nsN == 0)
                return new List<int> { 0 };

            var nsBoundaries = new List<int> { 0 };
            int ni = 1;

            while (ni < nsN)
            {
                if (isVowelNs[ni])
                {
                    if (ni > 0 && isVowelNs[ni - 1])
                    {
                        // Check hiatus vs diphthong: strong+strong = hiatus
                        var prevGrapheme = units[nonSilentIdx[ni - 1]].Grapheme;
                        var currGrapheme = units[nonSilentIdx[ni]].Grapheme;
                        char prevChar = prevGrapheme[prevGrapheme.Length - 1];
                        char currChar = currGrapheme[currGrapheme.Length - 1];
                        char prevBase = GetBaseVowel(prevChar);
                        char currBase = GetBaseVowel(currChar);

                        if (StrongVowels.Contains(prevBase) && StrongVowels.Contains(currBase))
                        {
                            nsBoundaries.Add(ni);
                        }
                        else
                        {
                            // Accented weak vowel forces hiatus (diphthong breaking)
                            if (WeakVowels.Contains(currBase) && HasAccentOnChar(currChar))
                            {
                                nsBoundaries.Add(ni);
                            }
                            else if (WeakVowels.Contains(prevBase)
                                     && HasAccentOnChar(prevChar))
                            {
                                nsBoundaries.Add(ni);
                            }
                        }
                    }
                    ni++;
                }
                else
                {
                    // Consonant cluster before next vowel
                    int consStart = ni;
                    while (ni < nsN && !isVowelNs[ni])
                    {
                        ni++;
                    }
                    int consCount = ni - consStart;

                    if (ni < nsN) // vowel follows
                    {
                        if (consCount == 1)
                        {
                            // V.CV
                            nsBoundaries.Add(consStart);
                        }
                        else if (consCount >= 2)
                        {
                            if (consCount == 2)
                            {
                                var pair = BaseConsFromNsIdx(units, nonSilentIdx, consStart)
                                           + BaseConsFromNsIdx(units, nonSilentIdx, consStart + 1);
                                if (InseparableClusters.Contains(pair))
                                {
                                    nsBoundaries.Add(consStart);
                                }
                                else
                                {
                                    nsBoundaries.Add(consStart + 1);
                                }
                            }
                            else
                            {
                                // 3+ consonants -- split before last 2 if they form
                                // an inseparable cluster, else before last 1.
                                var last2 = BaseConsFromNsIdx(units, nonSilentIdx, ni - 2)
                                            + BaseConsFromNsIdx(units, nonSilentIdx, ni - 1);
                                if (InseparableClusters.Contains(last2))
                                {
                                    nsBoundaries.Add(ni - 2);
                                }
                                else
                                {
                                    nsBoundaries.Add(ni - 1);
                                }
                            }
                        }
                    }
                }
            }

            // Map non-silent indices back to grapheme-unit indices
            var result = new List<int>(nsBoundaries.Count);
            foreach (var b in nsBoundaries)
            {
                result.Add(nonSilentIdx[b]);
            }
            return result;
        }

        /// <summary>
        /// Return the base consonant letter for a non-silent index.
        /// </summary>
        private static string BaseConsFromNsIdx(
            List<GraphemeUnit> units, List<int> nonSilentIdx, int nsIdx)
        {
            var g = units[nonSilentIdx[nsIdx]].Grapheme;
            char lastChar = g[g.Length - 1];
            return (AccentMap.TryGetValue(lastChar, out var mapped) ? mapped : lastChar)
                .ToString();
        }

        // ===================================================================
        // Stress detection
        // ===================================================================

        /// <summary>
        /// Return the 0-based syllable index that receives stress.
        /// Spanish stress rules:
        /// 1. If accent mark -- stressed syllable contains that vowel
        /// 2. Words ending in vowel, n, s -- penultimate syllable
        /// 3. Words ending in other consonant -- final syllable
        /// </summary>
        private static int GetStressedSyllable(
            string word,
            List<GraphemeUnit> units = null,
            List<int> boundaries = null)
        {
            if (units == null)
                units = SegmentGraphemes(word);
            if (boundaries == null)
                boundaries = FindSyllableBoundaries(word, units);

            int numSyllables = boundaries.Count;
            if (numSyllables == 0)
                return 0;

            // Check for explicit accent mark
            int? accentIdx = HasAccent(word);
            if (accentIdx.HasValue)
            {
                // Find which grapheme-unit contains this character index
                int charOffset = 0;
                int accentUnitIdx = 0;
                for (int uid = 0; uid < units.Count; uid++)
                {
                    int gLen = units[uid].Grapheme.Length;
                    if (charOffset <= accentIdx.Value && accentIdx.Value < charOffset + gLen)
                    {
                        accentUnitIdx = uid;
                        break;
                    }
                    charOffset += gLen;
                }

                // Find which syllable contains this unit index
                for (int sylIdx = boundaries.Count - 1; sylIdx >= 0; sylIdx--)
                {
                    if (boundaries[sylIdx] <= accentUnitIdx)
                        return sylIdx;
                }
                return 0;
            }

            if (numSyllables == 1)
                return 0;

            // Get base form of last character
            char baseLast = GetBaseVowel(word[word.Length - 1]);

            if (Vowels.Contains(baseLast) || StressFinalExceptions.Contains(baseLast))
            {
                return Math.Max(0, numSyllables - 2);
            }
            else
            {
                return numSyllables - 1;
            }
        }

        /// <summary>
        /// Return the index of the accented vowel in word, or null.
        /// Only stress-indicating accents are considered (not diaeresis).
        /// </summary>
        private static int? HasAccent(string word)
        {
            for (int i = 0; i < word.Length; i++)
            {
                if (StressAccents.Contains(word[i]))
                    return i;
            }
            return null;
        }

        /// <summary>Return true if the character is an accented vowel.</summary>
        private static bool HasAccentOnChar(char ch)
        {
            return StressAccents.Contains(ch);
        }

        /// <summary>Check if character is a Spanish vowel (including accented).</summary>
        private static bool IsVowelChar(char ch)
        {
            return Vowels.Contains(ch) || AccentMap.ContainsKey(ch);
        }

        /// <summary>Get base vowel from potentially accented character.</summary>
        private static char GetBaseVowel(char ch)
        {
            return AccentMap.TryGetValue(ch, out var mapped) ? mapped : ch;
        }

        // ===================================================================
        // G2P -- grapheme to phoneme conversion
        // ===================================================================

        /// <summary>
        /// Convert a Spanish word to IPA phonemes.
        /// Returns (phonemes, stressedSyllableIndex, graphemeUnits, syllableBoundaries).
        /// </summary>
        private static (List<string> Phonemes, int StressedSyl,
            List<GraphemeUnit> Units, List<int> Boundaries)
            G2PWord(string word)
        {
            var phonemes = new List<string>();
            int n = word.Length;
            int i = 0;

            // Base form for consonant context checks
            var baseWord = new StringBuilder(n);
            foreach (var ch in word)
            {
                baseWord.Append(AccentMap.TryGetValue(ch, out var mapped) ? mapped : ch);
            }

            while (i < n)
            {
                char ch = word[i];
                char baseCh = AccentMap.TryGetValue(ch, out var bv) ? bv : ch;

                // --- Vowels ---
                if (Vowels.Contains(baseCh))
                {
                    phonemes.Add(baseCh.ToString());
                    i += 1;
                    continue;
                }

                // --- Multi-character sequences (check longest first) ---

                // "qu" before e/i -> k
                if (baseCh == 'q' && i + 1 < n && baseWord[i + 1] == 'u')
                {
                    phonemes.Add("k");
                    i += 2; // skip "qu", vowel handled next iteration
                    continue;
                }

                // "ch" -> tsh
                if (baseCh == 'c' && i + 1 < n && baseWord[i + 1] == 'h')
                {
                    phonemes.Add("t\u0283"); // t + esh
                    i += 2;
                    continue;
                }

                // "ll" -> voiced palatal fricative (yeismo)
                if (baseCh == 'l' && i + 1 < n && baseWord[i + 1] == 'l')
                {
                    phonemes.Add("\u029D"); // ydot below (voiced palatal fricative)
                    i += 2;
                    continue;
                }

                // "rr" -> trill
                if (baseCh == 'r' && i + 1 < n && baseWord[i + 1] == 'r')
                {
                    phonemes.Add("rr");
                    i += 2;
                    continue;
                }

                // "gu" before e/i with diaeresis -> g + w
                if (baseCh == 'g' && i + 1 < n && word[i + 1] == '\u00FC'
                    && i + 2 < n
                    && (baseWord[i + 2] == 'e' || baseWord[i + 2] == 'i'))
                {
                    phonemes.Add("\u0261"); // voiced velar stop
                    phonemes.Add("w");
                    i += 2; // skip "gu", vowel handled next
                    continue;
                }

                // "gu" before e/i -> g (u is silent)
                if (baseCh == 'g' && i + 1 < n && baseWord[i + 1] == 'u'
                    && i + 2 < n
                    && (baseWord[i + 2] == 'e' || baseWord[i + 2] == 'i'))
                {
                    if (PrevIsVowel(word, i) && !IsAfterNasal(baseWord, i))
                    {
                        phonemes.Add("\u0263"); // voiced velar fricative
                    }
                    else
                    {
                        phonemes.Add("\u0261"); // voiced velar stop
                    }
                    i += 2; // skip "gu"
                    continue;
                }

                // "sc" before e/i -> s (seseo: avoid geminate ss)
                if (baseCh == 's' && i + 1 < n && baseWord[i + 1] == 'c'
                    && i + 2 < n
                    && (baseWord[i + 2] == 'e' || baseWord[i + 2] == 'i'))
                {
                    phonemes.Add("s");
                    i += 2; // skip "sc", vowel handled next
                    continue;
                }

                // --- Single character rules ---

                if (baseCh == 'b' || baseCh == 'v')
                {
                    if (IsWordInitial(i)
                        || IsAfterNasal(baseWord, i)
                        || (i > 0 && baseWord[i - 1] == 'l'))
                    {
                        phonemes.Add("b");
                    }
                    else
                    {
                        phonemes.Add("\u03B2"); // beta -- fricative in all other positions
                    }
                    i += 1;
                    continue;
                }

                if (baseCh == 'c')
                {
                    if (i + 1 < n && (baseWord[i + 1] == 'e' || baseWord[i + 1] == 'i'))
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

                if (baseCh == 'd')
                {
                    if (IsWordInitial(i)
                        || IsAfterNasal(baseWord, i)
                        || (i > 0 && baseWord[i - 1] == 'l'))
                    {
                        phonemes.Add("d");
                    }
                    else
                    {
                        phonemes.Add("\u00F0"); // eth -- fricative in all other positions
                    }
                    i += 1;
                    continue;
                }

                if (baseCh == 'f')
                {
                    phonemes.Add("f");
                    i += 1;
                    continue;
                }

                if (baseCh == 'g')
                {
                    if (i + 1 < n && (baseWord[i + 1] == 'e' || baseWord[i + 1] == 'i'))
                    {
                        phonemes.Add("x");
                    }
                    else if (IsWordInitial(i)
                             || IsAfterNasal(baseWord, i)
                             || (i > 0 && baseWord[i - 1] == 'l'))
                    {
                        phonemes.Add("\u0261"); // voiced velar stop
                    }
                    else
                    {
                        phonemes.Add("\u0263"); // voiced velar fricative
                    }
                    i += 1;
                    continue;
                }

                if (baseCh == 'h')
                {
                    // h is silent in Spanish
                    i += 1;
                    continue;
                }

                if (baseCh == 'j')
                {
                    phonemes.Add("x");
                    i += 1;
                    continue;
                }

                if (baseCh == 'k')
                {
                    phonemes.Add("k");
                    i += 1;
                    continue;
                }

                if (baseCh == 'l')
                {
                    phonemes.Add("l");
                    i += 1;
                    continue;
                }

                if (baseCh == 'm')
                {
                    phonemes.Add("m");
                    i += 1;
                    continue;
                }

                if (baseCh == 'n')
                {
                    phonemes.Add("n");
                    i += 1;
                    continue;
                }

                if (baseCh == '\u00F1') // n-tilde
                {
                    phonemes.Add("\u0272"); // palatal nasal
                    i += 1;
                    continue;
                }

                if (baseCh == 'p')
                {
                    phonemes.Add("p");
                    i += 1;
                    continue;
                }

                if (baseCh == 'r')
                {
                    if (IsWordInitial(i))
                    {
                        phonemes.Add("rr");
                    }
                    else if (i > 0
                             && (baseWord[i - 1] == 'l'
                                 || baseWord[i - 1] == 'n'
                                 || baseWord[i - 1] == 's'))
                    {
                        phonemes.Add("rr");
                    }
                    else
                    {
                        phonemes.Add("\u027E"); // alveolar tap
                    }
                    i += 1;
                    continue;
                }

                if (baseCh == 's')
                {
                    phonemes.Add("s");
                    i += 1;
                    continue;
                }

                if (baseCh == 't')
                {
                    phonemes.Add("t");
                    i += 1;
                    continue;
                }

                if (baseCh == 'w')
                {
                    phonemes.Add("w");
                    i += 1;
                    continue;
                }

                if (baseCh == 'x')
                {
                    // Check for xc+e/i: the following c is silent (x already provides /ks/)
                    if (i + 1 < n && baseWord[i + 1] == 'c'
                        && i + 2 < n
                        && (baseWord[i + 2] == 'e' || baseWord[i + 2] == 'i'))
                    {
                        phonemes.Add("k");
                        phonemes.Add("s");
                        i += 2; // skip both x and c
                        continue;
                    }
                    // Normal x -> /ks/
                    phonemes.Add("k");
                    phonemes.Add("s");
                    i += 1;
                    continue;
                }

                if (baseCh == 'y')
                {
                    if (i == n - 1)
                    {
                        phonemes.Add("i");
                    }
                    else
                    {
                        phonemes.Add("\u029D"); // voiced palatal fricative
                    }
                    i += 1;
                    continue;
                }

                if (baseCh == 'z')
                {
                    phonemes.Add("s");
                    i += 1;
                    continue;
                }

                // Unknown character -- skip
                i += 1;
            }

            var units = SegmentGraphemes(word);
            var boundaries = FindSyllableBoundaries(word, units);
            int stressedSyl = GetStressedSyllable(word, units, boundaries);
            return (phonemes, stressedSyl, units, boundaries);
        }

        // ===================================================================
        // G2P helpers -- context checks
        // ===================================================================

        private static bool PrevIsVowel(string word, int i)
        {
            return i > 0 && IsVowelChar(word[i - 1]);
        }

        private static bool IsAfterNasal(StringBuilder baseWord, int i)
        {
            return i > 0 && (baseWord[i - 1] == 'm' || baseWord[i - 1] == 'n');
        }

        private static bool IsWordInitial(int i)
        {
            return i == 0;
        }

        // ===================================================================
        // Phoneme count per grapheme unit -- used by the stress-marker walker
        // ===================================================================

        /// <summary>
        /// Return the number of phonemes produced by a single grapheme unit.
        /// Most units produce exactly 1 phoneme.  Exceptions:
        /// - gu before e/i -> 2 phonemes (g + w)
        /// - x -> 2 phonemes (k + s)
        /// - silent h -> 0 phonemes
        /// </summary>
        private static int PhonemeCountForUnit(GraphemeUnit unit)
        {
            var grapheme = unit.Grapheme;

            // Build base form
            var baseSb = new StringBuilder(grapheme.Length);
            foreach (var ch in grapheme)
            {
                baseSb.Append(AccentMap.TryGetValue(ch, out var mapped) ? mapped : ch);
            }
            var baseStr = baseSb.ToString();

            // Silent h
            if (baseStr == "h")
                return 0;

            // gu digraph with diaeresis -> 2 phonemes
            if (baseStr.Length == 2 && baseStr[0] == 'g' && grapheme[1] == '\u00FC')
                return 2;

            // x -> ks (2 phonemes)
            if (baseStr == "x")
                return 2;

            // Everything else (single chars, ch, ll, rr, qu, gu) -> 1
            return 1;
        }

        // ===================================================================
        // Stress marker insertion
        // ===================================================================

        /// <summary>
        /// Insert stress marker (U+02C8) before the stressed syllable's first vowel.
        /// Uses grapheme segmentation for reliable char-to-phoneme mapping.
        /// </summary>
        private static List<string> InsertStressMarker(
            List<string> phonemes,
            string word,
            List<GraphemeUnit> units = null,
            List<int> boundaries = null,
            int? stressedSyl = null)
        {
            if (phonemes == null || phonemes.Count == 0)
                return phonemes;

            if (units == null)
                units = SegmentGraphemes(word);
            if (boundaries == null)
                boundaries = FindSyllableBoundaries(word, units);
            if (!stressedSyl.HasValue)
                stressedSyl = GetStressedSyllable(word, units, boundaries);

            if (boundaries.Count == 0)
                return phonemes;

            int numUnits = units.Count;

            if (stressedSyl.Value >= boundaries.Count)
                return phonemes;

            int sylStart = boundaries[stressedSyl.Value];
            int sylEnd = stressedSyl.Value + 1 < boundaries.Count
                ? boundaries[stressedSyl.Value + 1]
                : numUnits;

            // Find first vowel grapheme-unit in the stressed syllable
            int? stressedUnitIdx = null;
            for (int uid = sylStart; uid < sylEnd; uid++)
            {
                if (uid < numUnits && units[uid].IsVowel)
                {
                    stressedUnitIdx = uid;
                    break;
                }
            }

            if (!stressedUnitIdx.HasValue)
                return phonemes;

            // Walk grapheme units and accumulate phoneme count to map
            // the stressed unit index to a phoneme index.
            int phI = 0;
            for (int uid = 0; uid < numUnits; uid++)
            {
                if (uid == stressedUnitIdx.Value)
                {
                    // phI now points to the phoneme for this vowel
                    var result = new List<string>(phonemes.Count + 1);
                    for (int j = 0; j < phI; j++)
                        result.Add(phonemes[j]);
                    result.Add("\u02C8"); // stress marker
                    for (int j = phI; j < phonemes.Count; j++)
                        result.Add(phonemes[j]);
                    return result;
                }
                int count = PhonemeCountForUnit(units[uid]);
                phI += count;
            }

            return phonemes;
        }

        // ===================================================================
        // PUA token mapping
        // ===================================================================

        /// <summary>
        /// Map multi-character phonemes to PUA single codepoints.
        /// Only "rr" and "t&#x0283;" are mapped for Spanish.
        /// Single-character phonemes pass through unchanged.
        /// </summary>
        private static List<string> MapToPua(List<string> phonemes)
        {
            var result = new List<string>(phonemes.Count);
            foreach (var ph in phonemes)
            {
                if (ph == "rr")
                {
                    result.Add(PuaRr.ToString());
                }
                else if (ph == "t\u0283") // tesh
                {
                    result.Add(PuaTsh.ToString());
                }
                else
                {
                    result.Add(ph);
                }
            }
            return result;
        }

        // ===================================================================
        // Utility
        // ===================================================================

        /// <summary>Check whether every character in token is punctuation.</summary>
        private static bool IsPurelyPunctuation(string token)
        {
            foreach (var c in token)
            {
                if (!Punctuation.Contains(c))
                    return false;
            }
            return true;
        }
    }
}
