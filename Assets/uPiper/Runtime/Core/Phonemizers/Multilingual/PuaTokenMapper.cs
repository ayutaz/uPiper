using System.Collections.Concurrent;
using System.Collections.Generic;

namespace uPiper.Core.Phonemizers.Multilingual
{
    /// <summary>
    /// Maps multi-character phoneme tokens to single Unicode Private Use Area (PUA) codepoints.
    /// Ported from piper-plus Python implementation (token_mapper.py).
    /// <para>
    /// This mapping must match the C++ implementation in openjtalk_phonemize.cpp
    /// and all language-specific C++ phonemizers (chinese_phonemize.cpp, etc.).
    /// </para>
    /// <para>
    /// CRITICAL: Every PUA codepoint hardcoded in C++ MUST appear here.
    /// Do NOT change assigned codepoints -- they are baked into trained models.
    /// </para>
    /// </summary>
    public static class PuaTokenMapper
    {
        // ── Fixed PUA mapping table ─────────────────────────────────────────────
        // Ensures consistency between Python, C++, and C# implementations.

        /// <summary>
        /// Fixed mapping from multi-character phoneme tokens to PUA Unicode codepoints.
        /// These assignments are permanent and must not be changed.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, int> FixedPuaMapping =
            new Dictionary<string, int>
            {
                // =================================================================
                // Japanese (JA) -- openjtalk_phonemize_utils.cpp
                // =================================================================
                // Long vowels
                { "a:", 0xE000 },
                { "i:", 0xE001 },
                { "u:", 0xE002 },
                { "e:", 0xE003 },
                { "o:", 0xE004 },
                // Special consonants
                { "cl", 0xE005 },
                // Palatalized consonants
                { "ky", 0xE006 },
                { "kw", 0xE007 },
                { "gy", 0xE008 },
                { "gw", 0xE009 },
                { "ty", 0xE00A },
                { "dy", 0xE00B },
                { "py", 0xE00C },
                { "by", 0xE00D },
                // Affricates and special sounds
                { "ch", 0xE00E },
                { "ts", 0xE00F },
                { "sh", 0xE010 },
                { "zy", 0xE011 },
                { "hy", 0xE012 },
                // Palatalized nasals/liquids
                { "ny", 0xE013 },
                { "my", 0xE014 },
                { "ry", 0xE015 },
                // Question type markers (Issue #204)
                { "?!", 0xE016 },  // Emphatic question
                { "?.", 0xE017 },  // Neutral/rhetorical question
                { "?~", 0xE018 },  // Tag question
                // N phoneme variants (Issue #207)
                { "N_m", 0xE019 },      // before m/b/p (bilabial)
                { "N_n", 0xE01A },      // before n/t/d/ts/ch (alveolar)
                { "N_ng", 0xE01B },     // before k/g (velar)
                { "N_uvular", 0xE01C }, // at end or before vowels

                // =================================================================
                // Multilingual shared
                // =================================================================
                { "rr", 0xE01D },      // Spanish trill r
                { "y_vowel", 0xE01E }, // Close front rounded vowel [y] (ZH pinyin u-umlaut, FR lune)
                // 0xE01F reserved (unused gap)

                // =================================================================
                // Chinese (ZH) -- chinese_phonemize.cpp
                // =================================================================
                // --- Initials (aspirated/affricate) ---
                { "p\u02B0", 0xE020 },         // ph  aspirated bilabial (pinyin p)
                { "t\u02B0", 0xE021 },         // th  aspirated alveolar (pinyin t)
                { "k\u02B0", 0xE022 },         // kh  aspirated velar (pinyin k)
                { "t\u0255", 0xE023 },         // tc  alveolo-palatal affricate (pinyin j)
                { "t\u0255\u02B0", 0xE024 },   // tch aspirated alveolo-palatal affricate (pinyin q)
                // (U+0255 is a single codepoint -- no PUA needed)
                { "t\u0282", 0xE025 },         // ts  retroflex affricate (pinyin zh)
                { "t\u0282\u02B0", 0xE026 },   // tsh aspirated retroflex affricate (pinyin ch)
                // (U+0282, U+027B are single codepoints -- no PUA needed)
                { "ts\u02B0", 0xE027 },        // tsh aspirated alveolar affricate (pinyin c)
                // --- Diphthongs ---
                { "a\u026A", 0xE028 },         // ai (pinyin ai)
                { "e\u026A", 0xE029 },         // ei (pinyin ei)
                { "a\u028A", 0xE02A },         // au (pinyin ao)
                { "o\u028A", 0xE02B },         // ou (pinyin ou)
                // --- Nasal finals ---
                { "an", 0xE02C },              // an (pinyin an)
                { "\u0259n", 0xE02D },         // en (pinyin en)
                { "a\u014B", 0xE02E },         // ang (pinyin ang)
                { "\u0259\u014B", 0xE02F },    // eng (pinyin eng)
                { "u\u014B", 0xE030 },         // ung (pinyin ong)
                // --- i-compound finals ---
                { "ia", 0xE031 },              // ia (pinyin ia/ya)
                { "i\u025B", 0xE032 },         // ie (pinyin ie/ye)
                { "iou", 0xE033 },             // iou (pinyin iu/you)
                { "ia\u028A", 0xE034 },        // iau (pinyin iao/yao)
                { "i\u025Bn", 0xE035 },        // ien (pinyin ian/yan)
                { "in", 0xE036 },              // in (pinyin in/yin)
                { "ia\u014B", 0xE037 },        // iang (pinyin iang/yang)
                { "i\u014B", 0xE038 },         // ing (pinyin ing/ying)
                { "iu\u014B", 0xE039 },        // iung (pinyin iong/yong)
                // --- u-compound finals ---
                { "ua", 0xE03A },              // ua (pinyin ua/wa)
                { "uo", 0xE03B },              // uo (pinyin uo/wo)
                { "ua\u026A", 0xE03C },        // uai (pinyin uai/wai)
                { "ue\u026A", 0xE03D },        // uei (pinyin ui/wei)
                { "uan", 0xE03E },             // uan (pinyin uan/wan)
                { "u\u0259n", 0xE03F },        // uen (pinyin un/wen)
                { "ua\u014B", 0xE040 },        // uang (pinyin uang/wang)
                { "u\u0259\u014B", 0xE041 },   // ueng (pinyin ueng/weng)
                // --- u-umlaut-compound finals ---
                { "y\u025B", 0xE042 },         // ye (pinyin ue/yue)
                { "y\u025Bn", 0xE043 },        // yen (pinyin uan/yuan)
                { "yn", 0xE044 },              // yn (pinyin un/yun)
                // --- Syllabic consonants ---
                { "\u027B\u0329", 0xE045 },    // syllabic retroflex (zhi/chi/shi/ri)
                // (U+0268 is a single codepoint -- no PUA needed)
                // --- Tone markers ---
                { "tone1", 0xE046 },           // high level
                { "tone2", 0xE047 },           // rising
                { "tone3", 0xE048 },           // dipping
                { "tone4", 0xE049 },           // falling
                { "tone5", 0xE04A },           // neutral

                // =================================================================
                // Korean (KO) -- korean_phonemize.cpp
                // =================================================================
                // Note: ph/th/kh/tc/tch are shared with ZH (same PUA codepoints above)
                // --- Tense consonants (fortis) ---
                { "p\u0348", 0xE04B },         // tense bilabial
                { "t\u0348", 0xE04C },         // tense alveolar
                { "k\u0348", 0xE04D },         // tense velar
                { "s\u0348", 0xE04E },         // tense sibilant
                { "t\u0348\u0255", 0xE04F },   // tense alveolo-palatal affricate
                // --- Unreleased finals ---
                { "k\u031A", 0xE050 },         // unreleased velar
                { "t\u031A", 0xE051 },         // unreleased alveolar
                { "p\u031A", 0xE052 },         // unreleased bilabial
                // 0xE053 reserved (unused gap)

                // =================================================================
                // Spanish (ES) / Portuguese (PT)
                // =================================================================
                { "t\u0283", 0xE054 },         // voiceless postalveolar affricate
                { "d\u0292", 0xE055 },         // voiced postalveolar affricate

                // =================================================================
                // French (FR) -- french_phonemize.cpp
                // =================================================================
                // --- Nasal vowels ---
                { "\u025B\u0303", 0xE056 },    // nasal open-mid front unrounded (vin, pain)
                { "\u0251\u0303", 0xE057 },    // nasal open back unrounded (France, temps)
                { "\u0254\u0303", 0xE058 },    // nasal open-mid back rounded (bon, nom)
            };

        /// <summary>
        /// Last fixed PUA codepoint. Dynamic allocation starts after this.
        /// </summary>
        private const int LastFixedCodepoint = 0xE058;

        /// <summary>
        /// First codepoint available for dynamic allocation.
        /// </summary>
        private const int DynamicPuaStart = 0xE059;

        // ── Bidirectional lookup dictionaries ───────────────────────────────────

        /// <summary>
        /// Token string to PUA char mapping. Includes both fixed and dynamically registered entries.
        /// Thread-safe for concurrent reads and writes.
        /// </summary>
        public static readonly ConcurrentDictionary<string, char> Token2Char = new();

        /// <summary>
        /// PUA char to token string mapping. Includes both fixed and dynamically registered entries.
        /// Thread-safe for concurrent reads and writes.
        /// </summary>
        public static readonly ConcurrentDictionary<char, string> Char2Token = new();

        /// <summary>
        /// Next available codepoint for dynamic allocation. Access protected by <see cref="_dynamicLock"/>.
        /// </summary>
        private static int _nextDynamic = DynamicPuaStart;

        /// <summary>
        /// Lock for dynamic codepoint allocation to ensure thread-safe sequential assignment.
        /// </summary>
        private static readonly object _dynamicLock = new();

        // ── Static constructor ──────────────────────────────────────────────────

        static PuaTokenMapper()
        {
            // Initialize bidirectional mappings from the fixed table
            foreach (var kvp in FixedPuaMapping)
            {
                var ch = (char)kvp.Value;
                Token2Char[kvp.Key] = ch;
                Char2Token[ch] = kvp.Key;
            }
        }

        // ── Public API ──────────────────────────────────────────────────────────

        /// <summary>
        /// Registers a token and returns its single-character PUA replacement.
        /// If the token is already registered, returns the existing mapping.
        /// If the token is a single character, it maps to itself (no PUA needed).
        /// Otherwise, a new PUA codepoint is dynamically allocated.
        /// Thread-safe.
        /// </summary>
        /// <param name="token">The phoneme token string to register.</param>
        /// <returns>The single PUA character representing this token.</returns>
        public static char Register(string token)
        {
            // Fast path: already registered
            if (Token2Char.TryGetValue(token, out var existing))
                return existing;

            // Single-character tokens map to themselves
            if (token.Length == 1)
            {
                Token2Char[token] = token[0];
                Char2Token[token[0]] = token;
                return token[0];
            }

            // Dynamic allocation (must be serialized to ensure sequential codepoints)
            lock (_dynamicLock)
            {
                // Double-check after acquiring lock
                if (Token2Char.TryGetValue(token, out existing))
                    return existing;

                var ch = (char)_nextDynamic;
                _nextDynamic++;

                Token2Char[token] = ch;
                Char2Token[ch] = token;
                return ch;
            }
        }

        /// <summary>
        /// Converts a list of phoneme tokens into a list of single PUA characters.
        /// Each multi-character token is replaced with its PUA codepoint;
        /// single-character tokens pass through unchanged.
        /// Matches the Python <c>map_sequence()</c> function.
        /// </summary>
        /// <param name="tokens">List of phoneme token strings.</param>
        /// <returns>List of single characters (one per token).</returns>
        public static List<char> MapSequence(IList<string> tokens)
        {
            var result = new List<char>(tokens.Count);
            for (var i = 0; i < tokens.Count; i++)
            {
                result.Add(Register(tokens[i]));
            }
            return result;
        }

        /// <summary>
        /// Converts a single phoneme token to its PUA character.
        /// Registers the token if not already known.
        /// </summary>
        /// <param name="token">The phoneme token string.</param>
        /// <returns>The single PUA character representing this token.</returns>
        public static char MapToken(string token)
        {
            return Register(token);
        }

        /// <summary>
        /// Converts a PUA character back to its original token string.
        /// </summary>
        /// <param name="ch">The PUA character to look up.</param>
        /// <returns>The original token string, or <c>null</c> if not found.</returns>
        public static string UnmapChar(char ch)
        {
            return Char2Token.TryGetValue(ch, out var token) ? token : null;
        }

        /// <summary>
        /// Checks whether a character falls within the fixed PUA range (0xE000 .. 0xE058).
        /// </summary>
        /// <param name="ch">The character to test.</param>
        /// <returns><c>true</c> if the character is a fixed PUA mapping; otherwise <c>false</c>.</returns>
        public static bool IsFixedPua(char ch)
        {
            return ch >= '\uE000' && ch <= (char)LastFixedCodepoint;
        }
    }
}