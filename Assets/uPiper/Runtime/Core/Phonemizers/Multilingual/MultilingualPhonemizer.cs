using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using uPiper.Core.Logging;
using uPiper.Core.Phonemizers.Backend;
using uPiper.Core.Phonemizers.Implementations;
using uPiper.Core.Phonemizers.Multilingual.Handlers;

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

        private readonly Dictionary<string, ILanguageG2PHandler> _handlers = new();
        private readonly UnicodeLanguageDetector _detector;
        private readonly IReadOnlyList<string> _languages;
        private readonly string _defaultLatinLanguage;
        private IPhonemizerBackend _enPhonemizer;        // English legacy (for test stub injection, kept for P1-5)
        private IPhonemizerBackend _koPhonemizer;        // Korean (legacy backend, kept for P1-5)
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
            _defaultLatinLanguage = options.DefaultLatinLanguage ?? "en";
            _detector = new UnicodeLanguageDetector(options.Languages, _defaultLatinLanguage);

            // Load handlers from options
            if (options.Handlers != null)
            {
                foreach (var kvp in options.Handlers)
                    _handlers[kvp.Key] = kvp.Value;
            }

            // Backward compatibility: wrap individual engine properties into handlers
#pragma warning disable CS0618
            if (options.JaPhonemizer != null && !_handlers.ContainsKey("ja"))
                _handlers["ja"] = new JapaneseG2PHandler(options.JaPhonemizer);
            if (options.EnEngine != null && !_handlers.ContainsKey("en"))
                _handlers["en"] = new EnglishG2PHandler(options.EnEngine);
            _enPhonemizer = options.EnPhonemizer;
            if (options.EsEngine != null && !_handlers.ContainsKey("es"))
                _handlers["es"] = new SpanishG2PHandler(options.EsEngine);
            if (options.FrEngine != null && !_handlers.ContainsKey("fr"))
                _handlers["fr"] = new FrenchG2PHandler(options.FrEngine);
            if (options.PtEngine != null && !_handlers.ContainsKey("pt"))
                _handlers["pt"] = new PortugueseG2PHandler(options.PtEngine);
            if (options.ZhEngine != null && !_handlers.ContainsKey("zh"))
                _handlers["zh"] = new ChineseG2PHandler(options.ZhEngine);
            _koPhonemizer = options.KoPhonemizer;
            if (options.KoG2PEngine != null && !_handlers.ContainsKey("ko"))
                _handlers["ko"] = new KoreanG2PHandler(options.KoG2PEngine);
#pragma warning restore CS0618
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
            DotNetG2P.Spanish.SpanishG2PEngine esEngine = null,
            DotNetG2P.French.FrenchG2PEngine frEngine = null,
            DotNetG2P.Portuguese.PortugueseG2PEngine ptEngine = null,
            DotNetG2P.Chinese.ChineseG2PEngine zhEngine = null,
            IPhonemizerBackend koPhonemizer = null,
            DotNetG2P.Korean.KoreanG2PEngine koG2PEngine = null)
        {
            if (languages == null || languages.Count == 0)
                throw new ArgumentException("At least one language must be specified.", nameof(languages));

            _languages = languages;
            _defaultLatinLanguage = defaultLatinLanguage;
            _detector = new UnicodeLanguageDetector(languages, defaultLatinLanguage);

            // Wrap individual engines into handlers for backward compatibility
            if (jaPhonemizer != null)
                _handlers["ja"] = new JapaneseG2PHandler(jaPhonemizer);
            _enPhonemizer = enPhonemizer;
            if (esEngine != null)
                _handlers["es"] = new SpanishG2PHandler(esEngine);
            if (frEngine != null)
                _handlers["fr"] = new FrenchG2PHandler(frEngine);
            if (ptEngine != null)
                _handlers["pt"] = new PortugueseG2PHandler(ptEngine);
            if (zhEngine != null)
                _handlers["zh"] = new ChineseG2PHandler(zhEngine);
            _koPhonemizer = koPhonemizer;
            if (koG2PEngine != null)
                _handlers["ko"] = new KoreanG2PHandler(koG2PEngine);
        }

        /// <summary>
        /// Asynchronously initializes phonemizer backends for all configured languages.
        /// </summary>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_isInitialized)
                return;

            // Create default handlers for languages that don't have one yet
            foreach (var lang in _languages)
            {
                if (_handlers.ContainsKey(lang))
                    continue;

                // Skip languages that have legacy backend fallback (en/ko via _enPhonemizer/_koPhonemizer)
                if (lang == "en" && _enPhonemizer != null)
                    continue;
                if (lang == "ko" && _koPhonemizer != null)
                    continue;

                var handler = CreateDefaultHandler(lang);
                if (handler != null)
                    _handlers[lang] = handler;
            }

            // Initialize all handlers
            foreach (var handler in _handlers.Values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await handler.InitializeAsync(cancellationToken);
            }

            if (_handlers.Count == 0 && _enPhonemizer == null && _koPhonemizer == null)
            {
                PiperLogger.LogWarning(
                    "[MultilingualPhonemizer] Warning: No backends were successfully initialized");
            }

            _isInitialized = true;
            PiperLogger.LogInfo($"[MultilingualPhonemizer] Initialized for languages: [{string.Join(", ", _languages)}]");
        }

        private static ILanguageG2PHandler CreateDefaultHandler(string lang)
        {
            return lang switch
            {
                "ja" => new JapaneseG2PHandler(),
                "en" => new EnglishG2PHandler(),
                "es" => new SpanishG2PHandler(),
                "fr" => new FrenchG2PHandler(),
                "pt" => new PortugueseG2PHandler(),
                "zh" => new ChineseG2PHandler(),
                "ko" => new KoreanG2PHandler(),
                _ => null
            };
        }

        /// <summary>
        /// Phonemizes mixed-language text and returns phonemes with optional prosody.
        /// </summary>
        public async Task<MultilingualPhonemizeResult> PhonemizeWithProsodyAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MultilingualPhonemizer));
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

                if (_handlers.TryGetValue(lang, out var handler))
                {
                    (segPhonemes, segA1, segA2, segA3) = handler.Process(segText);
                }
                else
                {
                    var fallbackResult = await ProcessFallbackAsync(lang, segText, cancellationToken);
                    if (fallbackResult.phonemes == null)
                        continue;
                    (segPhonemes, segA1, segA2, segA3) = fallbackResult;
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

            foreach (var handler in _handlers.Values)
                handler.Dispose();
            _handlers.Clear();

            // Legacy backends (kept for P1-5)
            _enPhonemizer?.Dispose();
            _koPhonemizer?.Dispose();
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

        private static void PadToLength(List<int> list, int targetLength)
        {
            while (list.Count < targetLength)
                list.Add(0);
        }
    }
}