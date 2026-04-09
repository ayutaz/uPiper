using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using uPiper.Core.Logging;
using uPiper.Core.Phonemizers.Multilingual.Handlers;

namespace uPiper.Core.Phonemizers.Multilingual
{
    /// <summary>
    /// Result of multilingual phonemization.
    /// </summary>
    public class MultilingualPhonemizeResult
    {
        /// <summary>Phoneme array (no BOS/EOS; PhonemeEncoder handles them).</summary>
        public string[] Phonemes { get; internal set; }

        /// <summary>Prosody A1 values per phoneme (0 for non-Japanese segments).</summary>
        public int[] ProsodyA1 { get; internal set; }

        /// <summary>Prosody A2 values per phoneme (0 for non-Japanese segments).</summary>
        public int[] ProsodyA2 { get; internal set; }

        /// <summary>Prosody A3 values per phoneme (0 for non-Japanese segments).</summary>
        public int[] ProsodyA3 { get; internal set; }

        /// <summary>Primary language detected for the text (e.g., "ja", "en").</summary>
        public string DetectedPrimaryLanguage { get; internal set; }
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

        private readonly Dictionary<string, HandlerEntry> _handlers = new();
        private readonly UnicodeLanguageDetector _detector;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private readonly IReadOnlyList<string> _languages;
        private readonly string _defaultLatinLanguage;
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

            // Load handlers from options (caller-provided, not owned)
            if (options.Handlers != null)
            {
                foreach (var kvp in options.Handlers)
                    _handlers[kvp.Key] = new HandlerEntry(kvp.Value, isOwned: false);
            }
        }

        /// <summary>
        /// Asynchronously initializes phonemizer backends for all configured languages.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the phonemizer has already been disposed.
        /// </exception>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MultilingualPhonemizer));

            if (_isInitialized)
                return;

            await _initLock.WaitAsync(cancellationToken);
            try
            {
                if (_isInitialized)
                    return; // double-check after lock

                // Create default handlers for languages that don't have one yet
                foreach (var lang in _languages)
                {
                    if (_handlers.ContainsKey(lang))
                        continue;

                    var handler = CreateDefaultHandler(lang);
                    if (handler != null)
                        _handlers[lang] = new HandlerEntry(handler, isOwned: true);
                }

                // Initialize all handlers
                foreach (var entry in _handlers.Values)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await entry.Handler.InitializeAsync(cancellationToken);
                }

                if (_handlers.Count == 0)
                {
                    PiperLogger.LogWarning(
                        "[MultilingualPhonemizer] Warning: No backends were successfully initialized");
                }

                _isInitialized = true;
                PiperLogger.LogInfo($"[MultilingualPhonemizer] Initialized for languages: [{string.Join(", ", _languages)}]");
            }
            finally
            {
                _initLock.Release();
            }
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
        /// <exception cref="ObjectDisposedException">
        /// Thrown if the phonemizer has already been disposed.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if <see cref="InitializeAsync"/> has not been called.
        /// </exception>
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

                if (_handlers.TryGetValue(lang, out var entry))
                {
                    if (entry.Handler == null)
                    {
                        PiperLogger.LogWarning(
                            $"[MultilingualPhonemizer] Null handler for '{lang}', " +
                            $"skipping segment.");
                        continue;
                    }
                    (segPhonemes, segA1, segA2, segA3) = entry.Handler.Process(segText);
                }
                else
                {
                    PiperLogger.LogWarning(
                        $"[MultilingualPhonemizer] Unsupported language '{lang}', skipping segment: \"{segText}\"");
                    continue;
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

            // Maintain async contract for future extensibility (e.g., async dictionary reload)
            await Task.CompletedTask;

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

            // Dispose order matters: handlers first, then _initLock last.
            // _initLock must outlive handler disposal because a concurrent InitializeAsync()
            // call could still hold the semaphore while awaiting handler initialization.
            // Disposing _initLock before handlers would cause ObjectDisposedException
            // if another thread releases the semaphore after it has been disposed.
            foreach (var entry in _handlers.Values)
            {
                if (entry.IsOwned)
                    entry.Handler.Dispose();
            }
            _handlers.Clear();
            _initLock.Dispose();
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static void PadToLength(List<int> list, int targetLength)
        {
            while (list.Count < targetLength)
                list.Add(0);
        }
    }
}
