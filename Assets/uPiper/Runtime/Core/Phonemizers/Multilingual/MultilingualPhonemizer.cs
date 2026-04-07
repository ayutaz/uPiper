using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DotNetG2P.Chinese;
using DotNetG2P.English;
using DotNetG2P.French;
using DotNetG2P.Korean;
using DotNetG2P.Portuguese;
using DotNetG2P.Spanish;
using UnityEngine;
using uPiper.Core.Logging;
using uPiper.Core.Phonemizers.Backend;
using uPiper.Core.Phonemizers.Implementations;

namespace uPiper.Core.Phonemizers.Multilingual
{
    /// <summary>
    /// Result of multilingual phonemization.
    /// </summary>
    public class MultilingualPhonemizeResult
    {
        /// <summary>Phoneme array (no BOS/EOS; PhonemeEncoder handles them).</summary>
        public string[] Phonemes { get; set; }

        /// <summary>Prosody A1 values per phoneme (0 for non-Japanese segments).</summary>
        public int[] ProsodyA1 { get; set; }

        /// <summary>Prosody A2 values per phoneme (0 for non-Japanese segments).</summary>
        public int[] ProsodyA2 { get; set; }

        /// <summary>Prosody A3 values per phoneme (0 for non-Japanese segments).</summary>
        public int[] ProsodyA3 { get; set; }

        /// <summary>Primary language detected for the text (e.g., "ja", "en").</summary>
        public string DetectedPrimaryLanguage { get; set; }
    }

    /// <summary>
    /// Multilingual phonemizer that segments text by language and delegates to per-language backends.
    /// Supports Japanese (DotNetG2PPhonemizer) and English (EnglishG2PEngine) out of the box.
    /// Supports 7 languages: ja, en, es, fr, pt, zh, ko.
    /// </summary>
    public class MultilingualPhonemizer : IDisposable
    {
        // EOS-like tokens that act as sentence terminators
        private static readonly HashSet<string> EosLikeTokens =
            new() { "$", "?", "?!", "?.", "?~", "\ue016", "\ue017", "\ue018" };

        // Tone number -> PUA character mapping (tone1=E046 ... tone5=E04A)
        private static readonly char[] TonePuaChars = { '\0', '\ue046', '\ue047', '\ue048', '\ue049', '\ue04a' };

        private readonly UnicodeLanguageDetector _detector;
        private readonly IReadOnlyList<string> _languages;
        private readonly HashSet<string> _languageSet;
        private readonly string _defaultLatinLanguage;
        private DotNetG2PPhonemizer _jaPhonemizer;
        private EnglishG2PEngine _enEngine;              // English (DotNetG2P)
        private IPhonemizerBackend _enPhonemizer;        // English legacy (for test stub injection)
        private SpanishG2PEngine _esEngine;              // Spanish (DotNetG2P)
        private FrenchG2PEngine _frEngine;               // French (DotNetG2P)
        private PortugueseG2PEngine _ptEngine;           // Portuguese (DotNetG2P)
        private ChineseG2PEngine _zhEngine;              // Chinese (DotNetG2P)
        private IPhonemizerBackend _koPhonemizer;        // Korean (legacy backend, kept for backward compatibility)
        private KoreanG2PEngine _koG2PEngine;            // Korean (DotNetG2P)
        private bool _ownsJa;
        private bool _ownsEn;
        private bool _ownsEs;
        private bool _ownsFr;
        private bool _ownsPt;
        private bool _ownsZh;
        private bool _ownsKo;
        private volatile bool _isInitialized;
        private bool _disposed;

        /// <summary>Whether the phonemizer has been initialized.</summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>Supported language codes.</summary>
        public IReadOnlyList<string> Languages => _languages;

        /// <summary>
        /// Creates a MultilingualPhonemizer using an options object.
        /// </summary>
        /// <param name="options">Configuration options.</param>
        public MultilingualPhonemizer(MultilingualPhonemizerOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            options.Validate();

            _languages = options.Languages;
            _languageSet = new HashSet<string>(options.Languages);
            _defaultLatinLanguage = options.DefaultLatinLanguage ?? "en";
            _detector = new UnicodeLanguageDetector(options.Languages, _defaultLatinLanguage);
            _jaPhonemizer = options.JaPhonemizer;
            _enEngine = options.EnEngine;
#pragma warning disable CS0618
            _enPhonemizer = options.EnPhonemizer;
            _koPhonemizer = options.KoPhonemizer;
#pragma warning restore CS0618
            _esEngine = options.EsEngine;
            _frEngine = options.FrEngine;
            _ptEngine = options.PtEngine;
            _zhEngine = options.ZhEngine;
            _koG2PEngine = options.KoG2PEngine;
        }

        /// <summary>
        /// Creates a MultilingualPhonemizer for the specified language list.
        /// </summary>
        [Obsolete("Use the constructor that takes MultilingualPhonemizerOptions instead. This constructor will be removed in v2.0.")]
        public MultilingualPhonemizer(
            IReadOnlyList<string> languages,
            string defaultLatinLanguage = "en",
            DotNetG2PPhonemizer jaPhonemizer = null,
            IPhonemizerBackend enPhonemizer = null,
            SpanishG2PEngine esEngine = null,
            FrenchG2PEngine frEngine = null,
            PortugueseG2PEngine ptEngine = null,
            ChineseG2PEngine zhEngine = null,
            IPhonemizerBackend koPhonemizer = null,
            KoreanG2PEngine koG2PEngine = null)
        {
            if (languages == null || languages.Count == 0)
                throw new ArgumentException("At least one language must be specified.", nameof(languages));

            _languages = languages;
            _languageSet = new HashSet<string>(languages);
            _defaultLatinLanguage = defaultLatinLanguage;
            _detector = new UnicodeLanguageDetector(languages, defaultLatinLanguage);
            _jaPhonemizer = jaPhonemizer;
            _enPhonemizer = enPhonemizer;
            _esEngine = esEngine;
            _frEngine = frEngine;
            _ptEngine = ptEngine;
            _zhEngine = zhEngine;
            _koPhonemizer = koPhonemizer;
            _koG2PEngine = koG2PEngine;
        }

        /// <summary>
        /// Asynchronously initializes phonemizer backends for all configured languages.
        /// </summary>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_isInitialized)
                return;

            // Initialize Japanese phonemizer if needed
            if (ContainsLanguage("ja") && _jaPhonemizer == null)
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                _jaPhonemizer = new DotNetG2PPhonemizer();
                await _jaPhonemizer.InitializeAsync(cancellationToken);
#else
                _jaPhonemizer = new DotNetG2PPhonemizer();
                // Non-WebGL: synchronous initialization in constructor
                await Task.CompletedTask;
#endif
                _ownsJa = true;
            }

            // Initialize English G2P engine if needed
            // Use DotNetG2P.English (CMU dict + LTS + homograph resolution) for piper-plus compatible IPA output
            if (ContainsLanguage("en") && _enEngine == null && _enPhonemizer == null)
            {
                try
                {
                    var dictPath = Path.Combine(
                        Application.streamingAssetsPath,
                        "uPiper",
                        "Phonemizers",
                        "cmudict-0.7b.txt");

                    if (File.Exists(dictPath))
                    {
                        _enEngine = new EnglishG2PEngine(dictPath);
                        PiperLogger.LogInfo(
                            "[MultilingualPhonemizer] English backend initialized: DotNetG2P.English (external CMU dict)");
                    }
                    else
                    {
                        // Fallback to embedded dictionary
                        _enEngine = new EnglishG2PEngine();
                        PiperLogger.LogInfo(
                            "[MultilingualPhonemizer] English backend initialized: DotNetG2P.English (embedded CMU dict)");
                    }

                    _ownsEn = true;
                }
                catch (Exception ex)
                {
                    PiperLogger.LogWarning(
                        $"[MultilingualPhonemizer] Failed to initialize English backend: {ex.Message}");
                }
            }

            // Initialize Spanish G2P engine if needed
            if (ContainsLanguage("es") && _esEngine == null)
            {
                _esEngine = new SpanishG2PEngine();
                _ownsEs = true;
                PiperLogger.LogInfo("[MultilingualPhonemizer] Spanish backend initialized: DotNetG2P.Spanish");
                await Task.CompletedTask;
            }

            // Initialize French G2P engine if needed
            if (ContainsLanguage("fr") && _frEngine == null)
            {
                _frEngine = new FrenchG2PEngine();
                _ownsFr = true;
                PiperLogger.LogInfo("[MultilingualPhonemizer] French backend initialized: DotNetG2P.French");
                await Task.CompletedTask;
            }

            // Initialize Portuguese G2P engine if needed
            if (ContainsLanguage("pt") && _ptEngine == null)
            {
                _ptEngine = new PortugueseG2PEngine();
                _ownsPt = true;
                PiperLogger.LogInfo("[MultilingualPhonemizer] Portuguese backend initialized: DotNetG2P.Portuguese");
                await Task.CompletedTask;
            }

            // Initialize Chinese G2P engine if needed
            if (ContainsLanguage("zh") && _zhEngine == null)
            {
                try
                {
                    var charPath = Path.Combine(
                        Application.streamingAssetsPath, "uPiper", "Chinese", "pinyin_char.txt");
                    var phrasePath = Path.Combine(
                        Application.streamingAssetsPath, "uPiper", "Chinese", "pinyin_phrase.txt");

                    if (File.Exists(charPath))
                    {
                        _zhEngine = File.Exists(phrasePath)
                            ? new ChineseG2PEngine(charPath, phrasePath)
                            : new ChineseG2PEngine(charPath);
                        _ownsZh = true;
                        PiperLogger.LogInfo(
                            "[MultilingualPhonemizer] Chinese backend initialized: DotNetG2P.Chinese");
                    }
                    else
                    {
                        PiperLogger.LogWarning(
                            $"[MultilingualPhonemizer] Chinese dictionary not found at {charPath}");
                    }
                }
                catch (Exception ex)
                {
                    PiperLogger.LogWarning(
                        $"[MultilingualPhonemizer] Failed to initialize Chinese backend: {ex.Message}");
                }

                await Task.CompletedTask;
            }

            // Initialize Korean G2P engine if needed (prefer DotNetG2P.Korean over legacy backend)
            if (ContainsLanguage("ko") && _koG2PEngine == null)
            {
                _koG2PEngine = new KoreanG2PEngine();
                _ownsKo = true;
                PiperLogger.LogInfo("[MultilingualPhonemizer] Korean backend initialized: DotNetG2P.Korean");
                await Task.CompletedTask;
            }

            if (_jaPhonemizer == null && _enEngine == null && _enPhonemizer == null &&
                _esEngine == null && _frEngine == null && _ptEngine == null &&
                _zhEngine == null && _koG2PEngine == null && _koPhonemizer == null)
            {
                PiperLogger.LogWarning(
                    "[MultilingualPhonemizer] Warning: No backends were successfully initialized");
            }

            _isInitialized = true;
            PiperLogger.LogInfo($"[MultilingualPhonemizer] Initialized for languages: [{string.Join(", ", _languages)}]");
        }

        /// <summary>
        /// Phonemizes mixed-language text and returns phonemes with optional prosody.
        /// </summary>
        public async Task<MultilingualPhonemizeResult> PhonemizeWithProsodyAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Call InitializeAsync() before phonemizing.");

            if (string.IsNullOrWhiteSpace(text))
            {
                return new MultilingualPhonemizeResult
                {
                    Phonemes = Array.Empty<string>(),
                    ProsodyA1 = Array.Empty<int>(),
                    ProsodyA2 = Array.Empty<int>(),
                    ProsodyA3 = Array.Empty<int>(),
                    DetectedPrimaryLanguage = _defaultLatinLanguage
                };
            }

            // Segment text by language
            var segments = _detector.SegmentText(text);
            if (segments.Count == 0)
            {
                return new MultilingualPhonemizeResult
                {
                    Phonemes = Array.Empty<string>(),
                    ProsodyA1 = Array.Empty<int>(),
                    ProsodyA2 = Array.Empty<int>(),
                    ProsodyA3 = Array.Empty<int>(),
                    DetectedPrimaryLanguage = _defaultLatinLanguage
                };
            }

            var allPhonemes = new List<string>();
            var allA1 = new List<int>();
            var allA2 = new List<int>();
            var allA3 = new List<int>();

            string primaryLang = segments[0].language;
            // Count character-weighted primary language
            var langCharCounts = new Dictionary<string, int>();
            foreach (var (lang, seg) in segments)
            {
                if (!langCharCounts.ContainsKey(lang))
                    langCharCounts[lang] = 0;
                langCharCounts[lang] += seg.Length;
                if (langCharCounts[lang] > langCharCounts.GetValueOrDefault(primaryLang, 0))
                    primaryLang = lang;
            }

            for (var si = 0; si < segments.Count; si++)
            {
                var (lang, segText) = segments[si];
                var isLast = (si == segments.Count - 1);

                cancellationToken.ThrowIfCancellationRequested();

                // Phonemize each segment
                string[] segPhonemes = null;
                int[] segA1 = null;
                int[] segA2 = null;
                int[] segA3 = null;

                switch (lang)
                {
                    case "ja" when _jaPhonemizer != null:
                        (segPhonemes, segA1, segA2, segA3) = ProcessJapanese(segText);
                        break;
                    case "en" when _enEngine != null:
                        (segPhonemes, segA1, segA2, segA3) = ProcessEnglish(segText);
                        break;
                    case "es" when _esEngine != null:
                        (segPhonemes, segA1, segA2, segA3) = ProcessSpanish(segText);
                        break;
                    case "fr" when _frEngine != null:
                        (segPhonemes, segA1, segA2, segA3) = ProcessFrench(segText);
                        break;
                    case "pt" when _ptEngine != null:
                        (segPhonemes, segA1, segA2, segA3) = ProcessPortuguese(segText);
                        break;
                    case "zh" when _zhEngine != null:
                        (segPhonemes, segA1, segA2, segA3) = ProcessChinese(segText);
                        break;
                    case "ko" when _koG2PEngine != null:
                        (segPhonemes, segA1, segA2, segA3) = ProcessKorean(segText);
                        break;
                    default:
                        var fallbackResult = await ProcessFallbackAsync(lang, segText, cancellationToken);
                        if (fallbackResult.phonemes == null)
                            continue;
                        (segPhonemes, segA1, segA2, segA3) = fallbackResult;
                        break;
                }

                if (segPhonemes.Length == 0)
                    continue;

                // For intermediate segments: replace EOS-like markers with neutral "$"
                // but trim them so the final text only has one EOS marker
                if (!isLast)
                {
                    var lastToken = segPhonemes[^1];
                    if (EosLikeTokens.Contains(lastToken))
                    {
                        // Remove the EOS-like marker from intermediate segments
                        segPhonemes = segPhonemes[..^1];
                        segA1 = segA1.Length > 0 ? segA1[..^1] : segA1;
                        segA2 = segA2.Length > 0 ? segA2[..^1] : segA2;
                        segA3 = segA3.Length > 0 ? segA3[..^1] : segA3;
                    }
                }

                allPhonemes.AddRange(segPhonemes);
                allA1.AddRange(segA1);
                allA2.AddRange(segA2);
                allA3.AddRange(segA3);
            }

            // Ensure arrays are aligned
            var maxLen = allPhonemes.Count;
            PadToLength(allA1, maxLen);
            PadToLength(allA2, maxLen);
            PadToLength(allA3, maxLen);

            PiperLogger.LogDebug(
                $"[MultilingualPhonemizer] '{text}' \u2192 {allPhonemes.Count} phonemes, primary lang: {primaryLang}");

            return new MultilingualPhonemizeResult
            {
                Phonemes = allPhonemes.ToArray(),
                ProsodyA1 = allA1.ToArray(),
                ProsodyA2 = allA2.ToArray(),
                ProsodyA3 = allA3.ToArray(),
                DetectedPrimaryLanguage = primaryLang
            };
        }

        // ── IDisposable ────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (_ownsJa) _jaPhonemizer?.Dispose();
            if (_ownsEn)
            {
                _enEngine?.Dispose();
                _enPhonemizer?.Dispose();
            }
            if (_ownsEs) _esEngine?.Dispose();
            if (_ownsFr) _frEngine?.Dispose();
            if (_ownsPt) _ptEngine?.Dispose();
            if (_ownsZh) _zhEngine?.Dispose();
            if (_ownsKo)
            {
                _koG2PEngine?.Dispose();
                _koPhonemizer?.Dispose();
            }
        }

        // ── Language-specific processing methods ──────────────────────────────

        private (string[] segPhonemes, int[] a1, int[] a2, int[] a3) ProcessJapanese(string text)
        {
            var result = _jaPhonemizer.PhonemizeWithProsody(text);
            var phonemes = result.Phonemes ?? Array.Empty<string>();
            var a1 = result.ProsodyA1 ?? Array.Empty<int>();
            var a2 = result.ProsodyA2 ?? Array.Empty<int>();
            var a3 = result.ProsodyA3 ?? Array.Empty<int>();

            // Strip leading PAD ("_") from Japanese segments (added from "sil" conversion)
            if (phonemes.Length > 0 && phonemes[0] == "_")
            {
                phonemes = phonemes[1..];
                a1 = a1.Length > 1 ? a1[1..] : a1;
                a2 = a2.Length > 1 ? a2[1..] : a2;
                a3 = a3.Length > 1 ? a3[1..] : a3;
            }

            return (phonemes, a1, a2, a3);
        }

        private (string[] segPhonemes, int[] a1, int[] a2, int[] a3) ProcessEnglish(string text)
        {
            var prosodyResult = _enEngine.ToIpaWithProsody(text);
            var phonemes = _enEngine.ToPuaPhonemes(text);
            var prosody = prosodyResult.Prosody;
            var a1 = new int[phonemes.Length];
            var a2 = new int[phonemes.Length];
            var a3 = new int[phonemes.Length];
            for (var i = 0; i < phonemes.Length && i < prosody.Length; i++)
            {
                a1[i] = prosody[i].A1;
                a2[i] = prosody[i].A2;
                a3[i] = prosody[i].A3;
            }

            return (phonemes, a1, a2, a3);
        }

        private (string[] segPhonemes, int[] a1, int[] a2, int[] a3) ProcessSpanish(string text)
        {
            var result = _esEngine.ToIpaWithProsody(text);
            var phonemes = result.Phonemes ?? Array.Empty<string>();
            var (a1, a2, a3) = ExtractProsodyArrays(
                result.Prosody, p => (p.A1, p.A2, p.A3), phonemes.Length);
            return (phonemes, a1, a2, a3);
        }

        private (string[] segPhonemes, int[] a1, int[] a2, int[] a3) ProcessFrench(string text)
        {
            var phonemes = _frEngine.ToPuaPhonemes(text);
            var result = _frEngine.ToIpaWithProsody(text);
            var (a1, a2, a3) = ExtractProsodyArrays(
                result.Prosody, p => (p.A1, p.A2, p.A3), phonemes.Length);
            return (phonemes, a1, a2, a3);
        }

        private (string[] segPhonemes, int[] a1, int[] a2, int[] a3) ProcessPortuguese(string text)
        {
            var phonemes = _ptEngine.ToPuaPhonemes(text);
            var result = _ptEngine.ToIpaWithProsody(text);
            var (a1, a2, a3) = ExtractProsodyArrays(
                result.Prosody, p => (p.A1, p.A2, p.A3), phonemes.Length);
            return (phonemes, a1, a2, a3);
        }

        private (string[] segPhonemes, int[] a1, int[] a2, int[] a3) ProcessChinese(string text)
        {
            // Use DotNetG2P.Chinese directly for Chinese text
            var puaPhonemes = _zhEngine.ToPuaPhonemes(text);
            var prosodyResult = _zhEngine.ToIpaWithProsody(text);

            var phonemeList = new List<string>();
            var prosodyA1List = new List<int>();
            var prosodyA2List = new List<int>();
            var prosodyA3List = new List<int>();

            int totalSyllables = prosodyResult.Prosody.Count;
            if (totalSyllables == 0)
            {
                // No syllables (all non-Chinese text)
                foreach (var p in puaPhonemes)
                    phonemeList.Add(p);
            }
            else
            {
                // Distribute PUA phonemes across syllables and insert tone markers
                int phonemesPerSyllable = puaPhonemes.Length / totalSyllables;
                int remainder = puaPhonemes.Length % totalSyllables;
                int puaIdx = 0;

                for (int syl = 0; syl < totalSyllables; syl++)
                {
                    int count = phonemesPerSyllable + (syl < remainder ? 1 : 0);
                    int toneVal = syl < prosodyResult.Prosody.Count
                        ? prosodyResult.Prosody[syl].A1 : 5;
                    int sylPos = syl < prosodyResult.Prosody.Count
                        ? prosodyResult.Prosody[syl].A2 : 1;
                    int wordLen = syl < prosodyResult.Prosody.Count
                        ? prosodyResult.Prosody[syl].A3 : 1;

                    // Add initial + final phonemes for this syllable
                    for (int j = 0; j < count && puaIdx < puaPhonemes.Length; j++, puaIdx++)
                    {
                        phonemeList.Add(puaPhonemes[puaIdx]);
                        prosodyA1List.Add(toneVal);
                        prosodyA2List.Add(sylPos);
                        prosodyA3List.Add(wordLen);
                    }

                    // Append tone marker PUA (tone1=E046 ... tone5=E04A)
                    if (toneVal >= 1 && toneVal <= 5)
                    {
                        phonemeList.Add(TonePuaChars[toneVal].ToString());
                        prosodyA1List.Add(toneVal);
                        prosodyA2List.Add(sylPos);
                        prosodyA3List.Add(wordLen);
                    }
                }

                // Add any remaining phonemes (non-Chinese tokens)
                for (; puaIdx < puaPhonemes.Length; puaIdx++)
                {
                    phonemeList.Add(puaPhonemes[puaIdx]);
                    prosodyA1List.Add(0);
                    prosodyA2List.Add(0);
                    prosodyA3List.Add(0);
                }
            }

            var segPhonemes = phonemeList.ToArray();
            var segA1 = prosodyA1List.ToArray();
            var segA2 = prosodyA2List.ToArray();
            var segA3 = prosodyA3List.ToArray();

            // Ensure prosody arrays are aligned with phoneme count
            if (segA1.Length < segPhonemes.Length)
            {
                Array.Resize(ref segA1, segPhonemes.Length);
                Array.Resize(ref segA2, segPhonemes.Length);
                Array.Resize(ref segA3, segPhonemes.Length);
            }

            return (segPhonemes, segA1, segA2, segA3);
        }

        private (string[] segPhonemes, int[] a1, int[] a2, int[] a3) ProcessKorean(string text)
        {
            // Use DotNetG2P.Korean directly for Korean text
            var puaPhonemes = _koG2PEngine.ToPuaPhonemes(text);
            var prosodyResult = _koG2PEngine.ToIpaWithProsody(text);

            var phonemes = puaPhonemes ?? Array.Empty<string>();

            int[] segA1, segA2, segA3;

            // Map prosody from IPA-based result to PUA phonemes.
            // PUA phonemes and IPA phonemes should have the same count
            // (PuaMapper only replaces multi-char IPA with single PUA chars,
            // preserving the 1:1 phoneme correspondence).
            if (prosodyResult.Prosody.Length == phonemes.Length)
            {
                segA1 = new int[phonemes.Length];
                segA2 = new int[phonemes.Length];
                segA3 = new int[phonemes.Length];
                for (var i = 0; i < prosodyResult.Prosody.Length; i++)
                {
                    segA1[i] = prosodyResult.Prosody[i].A1;
                    segA2[i] = prosodyResult.Prosody[i].A2;
                    segA3[i] = prosodyResult.Prosody[i].A3;
                }
            }
            else
            {
                // Fallback: if lengths differ, use zeros for A1/A2 and
                // derive A3 from prosody result when possible
                segA1 = new int[phonemes.Length];
                segA2 = new int[phonemes.Length];
                segA3 = new int[phonemes.Length];
                if (prosodyResult.Prosody.Length > 0)
                {
                    var defaultA3 = prosodyResult.Prosody[0].A3;
                    for (var i = 0; i < phonemes.Length; i++)
                    {
                        segA3[i] = i < prosodyResult.Prosody.Length
                            ? prosodyResult.Prosody[i].A3
                            : defaultA3;
                    }
                }

                PiperLogger.LogWarning(
                    $"[MultilingualPhonemizer] Korean PUA/prosody length mismatch: " +
                    $"PUA={phonemes.Length}, Prosody={prosodyResult.Prosody.Length}");
            }

            return (phonemes, segA1, segA2, segA3);
        }

        private async Task<(string[] phonemes, int[] a1, int[] a2, int[] a3)> ProcessFallbackAsync(
            string lang, string text, CancellationToken cancellationToken)
        {
            var backend = GetBackendForLanguage(lang);
            if (backend != null)
            {
                var result = await backend.PhonemizeAsync(text, lang, null, cancellationToken);
                var phonemes = result?.Phonemes ?? Array.Empty<string>();
                var a1 = result?.ProsodyA1 ?? new int[phonemes.Length];
                var a2 = result?.ProsodyA2 ?? new int[phonemes.Length];
                var a3 = result?.ProsodyA3 ?? new int[phonemes.Length];
                return (phonemes, a1, a2, a3);
            }

            PiperLogger.LogWarning(
                $"[MultilingualPhonemizer] No backend for '{lang}', skipping segment.");
            return (null, null, null, null);
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private IPhonemizerBackend GetBackendForLanguage(string lang)
        {
            // Note: English normally goes through _enEngine path (DotNetG2P.English).
            // _enPhonemizer is only used as legacy fallback when _enEngine is null (e.g., test stubs).
            return lang switch
            {
                "en" => _enPhonemizer,
                "ko" => _koPhonemizer,
                _ => _enPhonemizer // fallback to English legacy backend
            };
        }

        private bool ContainsLanguage(string lang) => _languageSet.Contains(lang);

        /// <summary>
        /// Extracts prosody A1/A2/A3 arrays from a prosody info array using the given accessor.
        /// Used by ES/FR/PT branches to avoid duplicated extraction loops.
        /// </summary>
        private static (int[] a1, int[] a2, int[] a3) ExtractProsodyArrays<T>(
            T[] prosody, Func<T, (int a1, int a2, int a3)> accessor, int phonemeCount)
        {
            var a1 = new int[phonemeCount];
            var a2 = new int[phonemeCount];
            var a3 = new int[phonemeCount];
            for (var i = 0; i < Math.Min(phonemeCount, prosody.Length); i++)
            {
                var (pa1, pa2, pa3) = accessor(prosody[i]);
                a1[i] = pa1;
                a2[i] = pa2;
                a3[i] = pa3;
            }

            return (a1, a2, a3);
        }

        private static void PadToLength(List<int> list, int targetLength)
        {
            while (list.Count < targetLength)
                list.Add(0);
        }
    }
}