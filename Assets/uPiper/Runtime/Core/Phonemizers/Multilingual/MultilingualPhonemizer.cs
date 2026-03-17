using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
    /// Supports Japanese (DotNetG2PPhonemizer) and English (IPhonemizerBackend) out of the box.
    /// Additional languages (zh, ko, es, fr, pt) will be added in Phase 5.
    /// </summary>
    public class MultilingualPhonemizer : IDisposable
    {
        // EOS-like tokens that act as sentence terminators
        private static readonly HashSet<string> EosLikeTokens =
            new() { "$", "?", "?!", "?.", "?~" };

        private readonly UnicodeLanguageDetector _detector;
        private readonly IReadOnlyList<string> _languages;
        private readonly string _defaultLatinLanguage;
        private DotNetG2PPhonemizer _jaPhonemizer;
        private IPhonemizerBackend _enPhonemizer;
        private bool _isInitialized;
        private bool _disposed;

        /// <summary>Whether the phonemizer has been initialized.</summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>Supported language codes.</summary>
        public IReadOnlyList<string> Languages => _languages;

        /// <summary>
        /// Creates a MultilingualPhonemizer for the specified language list.
        /// </summary>
        /// <param name="languages">Languages to support (e.g., ["ja", "en"]).</param>
        /// <param name="defaultLatinLanguage">Default language for Latin text (default: "en").</param>
        /// <param name="jaPhonemizer">Optional pre-built Japanese phonemizer; one is created if null.</param>
        /// <param name="enPhonemizer">Optional pre-built English phonemizer backend.</param>
        public MultilingualPhonemizer(
            IReadOnlyList<string> languages,
            string defaultLatinLanguage = "en",
            DotNetG2PPhonemizer jaPhonemizer = null,
            IPhonemizerBackend enPhonemizer = null)
        {
            if (languages == null || languages.Count == 0)
                throw new ArgumentException("At least one language must be specified.", nameof(languages));

            _languages = languages;
            _defaultLatinLanguage = defaultLatinLanguage;
            _detector = new UnicodeLanguageDetector(languages, defaultLatinLanguage);
            _jaPhonemizer = jaPhonemizer;
            _enPhonemizer = enPhonemizer;
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
            }

            // Initialize English phonemizer if needed
            if (ContainsLanguage("en") && _enPhonemizer == null)
            {
                var flite = new Backend.Flite.FlitePhonemizerBackend();
                if (await flite.InitializeAsync(new PhonemizerBackendOptions(), cancellationToken))
                {
                    _enPhonemizer = flite;
                    PiperLogger.LogInfo("[MultilingualPhonemizer] English backend initialized: Flite");
                }
                else
                {
                    var ruleBased = new Backend.RuleBased.RuleBasedPhonemizer();
                    if (await ruleBased.InitializeAsync(new PhonemizerBackendOptions(), cancellationToken))
                    {
                        _enPhonemizer = ruleBased;
                        PiperLogger.LogInfo("[MultilingualPhonemizer] English backend initialized: RuleBased");
                    }
                    else
                    {
                        PiperLogger.LogWarning("[MultilingualPhonemizer] Failed to initialize English backend");
                    }
                }
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

                string[] segPhonemes;
                int[] segA1;
                int[] segA2;
                int[] segA3;

                // Phonemize each segment
                if (lang == "ja" && _jaPhonemizer != null)
                {
                    var result = _jaPhonemizer.PhonemizeWithProsody(segText);
                    segPhonemes = result.Phonemes ?? Array.Empty<string>();
                    segA1 = result.ProsodyA1 ?? Array.Empty<int>();
                    segA2 = result.ProsodyA2 ?? Array.Empty<int>();
                    segA3 = result.ProsodyA3 ?? Array.Empty<int>();
                }
                else if (_enPhonemizer != null)
                {
                    // English and fallback: use IPhonemizerBackend
                    var result = await _enPhonemizer.PhonemizeAsync(segText, lang, null, cancellationToken);
                    segPhonemes = result?.Phonemes ?? Array.Empty<string>();
                    segA1 = new int[segPhonemes.Length];
                    segA2 = new int[segPhonemes.Length];
                    segA3 = new int[segPhonemes.Length];
                }
                else
                {
                    PiperLogger.LogWarning($"[MultilingualPhonemizer] No backend for language '{lang}', skipping segment.");
                    continue;
                }

                if (segPhonemes.Length == 0)
                    continue;

                // For intermediate segments: replace EOS-like markers with neutral "$"
                // but trim them so the final text only has one EOS marker
                if (!isLast && segPhonemes.Length > 0)
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
                $"[MultilingualPhonemizer] '{text}' → {allPhonemes.Count} phonemes, primary lang: {primaryLang}");

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
            _jaPhonemizer?.Dispose();
            _enPhonemizer?.Dispose();
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private bool ContainsLanguage(string lang)
        {
            for (var i = 0; i < _languages.Count; i++)
                if (_languages[i] == lang) return true;
            return false;
        }

        private static void PadToLength(List<int> list, int targetLength)
        {
            while (list.Count < targetLength)
                list.Add(0);
        }
    }
}