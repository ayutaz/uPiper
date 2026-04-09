using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using uPiper.Core.Logging;
using uPiper.Core.Platform;

namespace uPiper.Core.Phonemizers.Multilingual
{
    // ── DTO classes for pua.json deserialization ─────────────────────────────

    /// <summary>
    /// Root structure of pua.json. Compatible with <see cref="JsonUtility"/>.
    /// </summary>
    [Serializable]
    internal class PuaJsonData
    {
        public int version;
        public string description;
        public PuaJsonEntry[] entries;
    }

    /// <summary>
    /// Single entry in the pua.json entries array.
    /// </summary>
    [Serializable]
    internal class PuaJsonEntry
    {
        public string token;
        public string codepoint; // "0xE000" hex string
        public string language;
        public string description;
    }
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
    public sealed class PuaTokenMapper
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
                // 0xE053 reserved (unused gap — was ɔɪ in early training, removed for piper-plus pua.json compat)
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

                // =================================================================
                // Swedish (SV) -- swedish_phonemize.cpp
                // =================================================================
                // --- Long vowels (Complementary Quantity) ---
                { "i\u02D0", 0xE059 },          // iː  close front unrounded long
                { "y\u02D0", 0xE05A },          // yː  close front rounded long
                { "e\u02D0", 0xE05B },          // eː  close-mid front unrounded long
                { "\u025B\u02D0", 0xE05C },     // ɛː  open-mid front unrounded long
                { "\u00F8\u02D0", 0xE05D },     // øː  close-mid front rounded long
                { "\u0251\u02D0", 0xE05E },     // ɑː  open back unrounded long
                { "o\u02D0", 0xE05F },          // oː  close-mid back rounded long
                { "u\u02D0", 0xE060 },          // uː  close back rounded long
                { "\u0289\u02D0", 0xE061 },     // ʉː  close central rounded long
            };

        /// <summary>
        /// Last fixed PUA codepoint. Dynamic allocation starts after this.
        /// </summary>
        private const int LastFixedCodepoint = 0xE061;

        /// <summary>
        /// First codepoint available for dynamic allocation.
        /// </summary>
        private const int DynamicPuaStart = 0xE062;

        // ── Bidirectional lookup dictionaries ───────────────────────────────────

        /// <summary>
        /// Token string to PUA char mapping. Includes both fixed and dynamically registered entries.
        /// Thread-safe for concurrent reads and writes.
        /// </summary>
        private readonly ConcurrentDictionary<string, char> _token2Char;

        /// <summary>
        /// PUA char to token string mapping. Includes both fixed and dynamically registered entries.
        /// Thread-safe for concurrent reads and writes.
        /// </summary>
        private readonly ConcurrentDictionary<char, string> _char2Token;

        /// <summary>
        /// Next available codepoint for dynamic allocation. Access protected by <see cref="_dynamicLock"/>.
        /// </summary>
        private int _nextDynamic;

        /// <summary>
        /// Lock for dynamic codepoint allocation to ensure thread-safe sequential assignment.
        /// </summary>
        private readonly object _dynamicLock = new();

        /// <summary>
        /// Read-only view of the token-to-char mapping.
        /// </summary>
        public IReadOnlyDictionary<string, char> Token2Char => _token2Char;

        /// <summary>
        /// Read-only view of the char-to-token mapping.
        /// </summary>
        public IReadOnlyDictionary<char, string> Char2Token => _char2Token;

        // ── Constructor ─────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a new instance with the fixed PUA mapping table pre-loaded.
        /// </summary>
        public PuaTokenMapper()
        {
            _token2Char = new ConcurrentDictionary<string, char>();
            _char2Token = new ConcurrentDictionary<char, string>();
            _nextDynamic = DynamicPuaStart;

            // Initialize bidirectional mappings from the fixed table
            foreach (var kvp in FixedPuaMapping)
            {
                var ch = (char)kvp.Value;
                _token2Char[kvp.Key] = ch;
                _char2Token[ch] = kvp.Key;
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
        public char Register(string token)
        {
            // Fast path: already registered
            if (_token2Char.TryGetValue(token, out var existing))
                return existing;

            // Single-character tokens map to themselves
            if (token.Length == 1)
            {
                _token2Char[token] = token[0];
                _char2Token[token[0]] = token;
                return token[0];
            }

            // Dynamic allocation (must be serialized to ensure sequential codepoints)
            lock (_dynamicLock)
            {
                // Double-check after acquiring lock
                if (_token2Char.TryGetValue(token, out existing))
                    return existing;

                if (_nextDynamic > 0xF8FF)
                    throw new InvalidOperationException("PUA codepoint space exhausted");

                var ch = (char)_nextDynamic;
                _nextDynamic++;

                _token2Char[token] = ch;
                _char2Token[ch] = token;
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
        public List<char> MapSequence(IList<string> tokens)
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
        public char MapToken(string token)
        {
            return Register(token);
        }

        /// <summary>
        /// Converts a PUA character back to its original token string.
        /// </summary>
        /// <param name="ch">The PUA character to look up.</param>
        /// <returns>The original token string, or <c>null</c> if not found.</returns>
        public string UnmapChar(char ch)
        {
            return _char2Token.TryGetValue(ch, out var token) ? token : null;
        }

        /// <summary>
        /// Checks whether a character falls within the fixed PUA range (0xE000 .. 0xE061).
        /// </summary>
        /// <param name="ch">The character to test.</param>
        /// <returns><c>true</c> if the character is a fixed PUA mapping; otherwise <c>false</c>.</returns>
        public static bool IsFixedPua(char ch)
        {
            return ch >= '\uE000' && ch <= (char)LastFixedCodepoint;
        }

        // ── pua.json runtime loading ────────────────────────────────────────

        /// <summary>
        /// Maximum number of entries allowed in pua.json as a safety limit.
        /// </summary>
        internal const int MaxEntries = 500;

        /// <summary>
        /// Relative path to pua.json within StreamingAssets.
        /// </summary>
        private const string PuaJsonRelativePath = "uPiper/pua.json";

        /// <summary>
        /// Whether this mapper has been initialized from a pua.json file.
        /// </summary>
        public bool IsLoadedFromJson { get; private set; }

        /// <summary>
        /// Parses pua.json content and populates the mapper.
        /// On success, clears existing mappings and replaces them with the JSON entries.
        /// On failure, keeps existing hardcoded mapping intact and returns false.
        /// </summary>
        /// <param name="json">Raw JSON string from pua.json.</param>
        /// <returns><c>true</c> if parsing succeeded; <c>false</c> otherwise.</returns>
        public bool LoadFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                PiperLogger.LogError("[PuaTokenMapper] LoadFromJson: JSON string is null or empty");
                return false;
            }

            PuaJsonData data;
            try
            {
                data = JsonUtility.FromJson<PuaJsonData>(json);
            }
            catch (Exception ex)
            {
                PiperLogger.LogError(
                    $"[PuaTokenMapper] LoadFromJson: Failed to parse JSON: {ex.Message}");
                return false;
            }

            if (data == null)
            {
                PiperLogger.LogError("[PuaTokenMapper] LoadFromJson: Deserialized data is null");
                return false;
            }

            // Validate version (forward compatible: accept >= 1)
            if (data.version < 1)
            {
                PiperLogger.LogError(
                    $"[PuaTokenMapper] LoadFromJson: Invalid version {data.version} (expected >= 1)");
                return false;
            }

            // null entries is treated as empty (valid but no overrides)
            if (data.entries == null || data.entries.Length == 0)
            {
                PiperLogger.LogDebug("[PuaTokenMapper] LoadFromJson: Empty entries array");
                IsLoadedFromJson = true;
                return true;
            }

            // Safety limit
            if (data.entries.Length > MaxEntries)
            {
                PiperLogger.LogError(
                    $"[PuaTokenMapper] LoadFromJson: Too many entries ({data.entries.Length} > {MaxEntries})");
                return false;
            }

            // Parse entries into temporary collections before modifying state
            var newToken2Char = new Dictionary<string, char>();
            var newChar2Token = new Dictionary<char, string>();
            var maxCodepoint = LastFixedCodepoint;

            for (var i = 0; i < data.entries.Length; i++)
            {
                var entry = data.entries[i];

                // Validate token
                if (string.IsNullOrEmpty(entry.token))
                {
                    PiperLogger.LogWarning(
                        $"[PuaTokenMapper] LoadFromJson: Skipping entry[{i}] with empty token");
                    continue;
                }

                // Parse codepoint hex string
                if (!TryParseHexCodepoint(entry.codepoint, out var codepoint))
                {
                    PiperLogger.LogWarning(
                        $"[PuaTokenMapper] LoadFromJson: Skipping entry[{i}] " +
                        $"token=\"{entry.token}\": invalid codepoint \"{entry.codepoint}\"");
                    continue;
                }

                // Validate PUA range
                if (codepoint < 0xE000 || codepoint > 0xF8FF)
                {
                    PiperLogger.LogWarning(
                        $"[PuaTokenMapper] LoadFromJson: Skipping entry[{i}] " +
                        $"token=\"{entry.token}\": codepoint 0x{codepoint:X4} " +
                        $"outside PUA range (0xE000-0xF8FF)");
                    continue;
                }

                var ch = (char)codepoint;

                // Detect duplicate tokens (last wins)
                if (newToken2Char.ContainsKey(entry.token))
                {
                    PiperLogger.LogWarning(
                        $"[PuaTokenMapper] LoadFromJson: Duplicate token \"{entry.token}\" " +
                        $"at entry[{i}], overwriting previous mapping");
                    // Remove old char mapping for this token
                    var oldChar = newToken2Char[entry.token];
                    newChar2Token.Remove(oldChar);
                }

                // Detect duplicate codepoints (last wins)
                if (newChar2Token.ContainsKey(ch))
                {
                    var oldToken = newChar2Token[ch];
                    PiperLogger.LogWarning(
                        $"[PuaTokenMapper] LoadFromJson: Duplicate codepoint 0x{codepoint:X4} " +
                        $"at entry[{i}] (token=\"{entry.token}\"), " +
                        $"overwriting previous token \"{oldToken}\"");
                    newToken2Char.Remove(oldToken);
                }

                newToken2Char[entry.token] = ch;
                newChar2Token[ch] = entry.token;

                if (codepoint > maxCodepoint)
                    maxCodepoint = codepoint;
            }

            // Apply the parsed mappings to the instance
            _token2Char.Clear();
            _char2Token.Clear();

            foreach (var kvp in newToken2Char)
            {
                _token2Char[kvp.Key] = kvp.Value;
            }

            foreach (var kvp in newChar2Token)
            {
                _char2Token[kvp.Key] = kvp.Value;
            }

            // Update dynamic allocation start to be after the highest loaded codepoint
            lock (_dynamicLock)
            {
                _nextDynamic = maxCodepoint + 1;
            }

            IsLoadedFromJson = true;
            PiperLogger.LogInfo(
                $"[PuaTokenMapper] Loaded {newToken2Char.Count} entries from pua.json (v{data.version})");
            return true;
        }

        /// <summary>
        /// Synchronously loads pua.json from StreamingAssets.
        /// Not available on WebGL (use <see cref="InitializeAsync"/> instead).
        /// </summary>
        /// <returns><c>true</c> if the file was found and loaded successfully; <c>false</c> otherwise.</returns>
        public bool InitializeFromFile()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            PiperLogger.LogWarning(
                "[PuaTokenMapper] InitializeFromFile: Not supported on WebGL. Use InitializeAsync().");
            return false;
#else
            var path = Path.Combine(Application.streamingAssetsPath, PuaJsonRelativePath);
            if (!File.Exists(path))
            {
                PiperLogger.LogDebug(
                    $"[PuaTokenMapper] InitializeFromFile: pua.json not found at {path}");
                return false;
            }

            try
            {
                var json = File.ReadAllText(path);
                return LoadFromJson(json);
            }
            catch (Exception ex)
            {
                PiperLogger.LogError(
                    $"[PuaTokenMapper] InitializeFromFile: Failed to read file: {ex.Message}");
                return false;
            }
#endif
        }

        /// <summary>
        /// Asynchronously loads pua.json from StreamingAssets.
        /// Works on all platforms including WebGL via <see cref="WebGLStreamingAssetsLoader"/>.
        /// Falls back to hardcoded mapping on failure.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>true</c> if the file was found and loaded successfully; <c>false</c> otherwise.</returns>
        public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var json = await WebGLStreamingAssetsLoader.LoadTextAsync(
                    PuaJsonRelativePath, cancellationToken);
                return LoadFromJson(json);
            }
            catch (FileNotFoundException)
            {
                PiperLogger.LogDebug(
                    "[PuaTokenMapper] InitializeAsync: pua.json not found in StreamingAssets");
                return false;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                PiperLogger.LogError(
                    $"[PuaTokenMapper] InitializeAsync: Failed to load pua.json: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Tries to parse a hex string like "0xE000" or "0xe000" to an integer.
        /// </summary>
        private static bool TryParseHexCodepoint(string hexString, out int codepoint)
        {
            codepoint = 0;
            if (string.IsNullOrEmpty(hexString))
                return false;

            // Strip "0x" or "0X" prefix
            var toParse = hexString;
            if (toParse.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                toParse = toParse.Substring(2);

            return int.TryParse(toParse, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out codepoint);
        }
    }
}