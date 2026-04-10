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

        /// <summary>
        /// Flat prosody array (stride=3): [a1_0, a2_0, a3_0, a1_1, a2_1, a3_1, ...].
        /// Length = Phonemes.Length * 3. Null when prosody is not available.
        /// </summary>
        public int[] ProsodyFlat { get; internal set; }

        /// <summary>Primary language detected for the text (e.g., "ja", "en").</summary>
        public string DetectedPrimaryLanguage { get; internal set; }

        /// <summary>Prosody data is available.</summary>
        public bool HasProsody => ProsodyFlat != null;
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
        private ILanguageDetector _detector;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private readonly IReadOnlyList<string> _languages;
        private readonly string _defaultLatinLanguage;
        private readonly bool _enableTrigramDetection;
        private readonly ILanguageDetector _customDetector;
        private readonly string _fallbackLanguage;
        private volatile bool _isInitialized;
        private volatile bool _isTrigramDetectionActive;
        private bool _disposed;

        /// <summary>Whether the phonemizer has been initialized.</summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Whether trigram-based language detection is currently active.
        /// Returns true only after <see cref="InitializeAsync"/> completes successfully
        /// and trigram profiles were loaded. When false, Latin-script language detection
        /// falls back to the default language (typically "en").
        /// </summary>
        public bool IsTrigramDetectionActive => _isTrigramDetectionActive;

        /// <summary>Supported language codes.</summary>
        public IReadOnlyList<string> Languages => _languages;

        /// <summary>
        /// Callback invoked when an unsupported language is detected during phonemization.
        /// Set by PiperTTS to bubble the event up to the public API.
        /// </summary>
        internal Action<UnsupportedLanguageEventArgs> OnUnsupportedLanguage { get; set; }

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
            _enableTrigramDetection = options.EnableTrigramDetection;
            _customDetector = options.LanguageDetector;
            _fallbackLanguage = options.FallbackLanguage;

            // Detector will be resolved in InitializeAsync().
            // Use the custom detector if provided, otherwise default to UnicodeLanguageDetector.
            _detector = _customDetector
                ?? new UnicodeLanguageDetector(options.Languages, _defaultLatinLanguage);

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

                // Upgrade detector to HybridLanguageDetector when trigram detection is enabled,
                // no custom detector was provided, and multiple Latin languages are configured.
                if (_customDetector == null && _enableTrigramDetection)
                {
                    await TryUpgradeToHybridDetectorAsync(cancellationToken);
                }

                _isInitialized = true;
                var detectorType = _isTrigramDetectionActive ? "Hybrid (Unicode + Trigram)" : "Unicode-only";
                PiperLogger.LogInfo(
                    $"[MultilingualPhonemizer] Initialized for languages: [{string.Join(", ", _languages)}], " +
                    $"detector: {detectorType}");
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
                    ProsodyFlat = Array.Empty<int>(),
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
                    ProsodyFlat = Array.Empty<int>(),
                    DetectedPrimaryLanguage = _defaultLatinLanguage
                };
            }

            var allPhonemes = new List<string>();
            var allProsodyFlat = new List<int>();

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
                int[] segProsodyFlat = null;

                if (_handlers.TryGetValue(lang, out var entry) && entry.Handler != null)
                {
                    (segPhonemes, segProsodyFlat) = entry.Handler.Process(segText);
                    segProsodyFlat ??= Array.Empty<int>();
                }
                else
                {
                    var fallbackUsed = false;
                    if (_fallbackLanguage != null
                        && _fallbackLanguage != lang
                        && _handlers.TryGetValue(_fallbackLanguage, out var fallbackEntry)
                        && fallbackEntry.Handler != null)
                    {
                        PiperLogger.LogWarning(
                            $"[MultilingualPhonemizer] Unsupported language '{lang}', " +
                            $"using fallback language '{_fallbackLanguage}' for segment: \"{segText}\"");
                        (segPhonemes, segProsodyFlat) = fallbackEntry.Handler.Process(segText);
                        segProsodyFlat ??= Array.Empty<int>();
                        fallbackUsed = true;
                    }
                    else
                    {
                        PiperLogger.LogWarning(
                            $"[MultilingualPhonemizer] Unsupported language '{lang}', " +
                            $"skipping segment: \"{segText}\"");
                    }

                    OnUnsupportedLanguage?.Invoke(
                        new UnsupportedLanguageEventArgs(
                            lang, segText, _languages,
                            fallbackUsed ? _fallbackLanguage : null));

                    if (!fallbackUsed)
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
                        if (segProsodyFlat.Length >= AudioGeneration.PhonemeEncoder.ProsodyStride)
                            segProsodyFlat = segProsodyFlat[..^AudioGeneration.PhonemeEncoder.ProsodyStride];
                    }
                }

                allPhonemes.AddRange(segPhonemes);
                allProsodyFlat.AddRange(segProsodyFlat);
            }

            // Ensure prosody flat array is aligned (phonemeCount * ProsodyStride)
            var targetFlatLen = allPhonemes.Count * AudioGeneration.PhonemeEncoder.ProsodyStride;
            while (allProsodyFlat.Count < targetFlatLen)
            {
                allProsodyFlat.Add(0); // a1
                allProsodyFlat.Add(0); // a2
                allProsodyFlat.Add(0); // a3
            }

            PiperLogger.LogDebug(
                $"[MultilingualPhonemizer] '{text}' \u2192 {allPhonemes.Count} phonemes, primary lang: {primaryLang}");

            // Maintain async contract for future extensibility (e.g., async dictionary reload)
            await Task.CompletedTask;

            return new MultilingualPhonemizeResult
            {
                Phonemes = allPhonemes.ToArray(),
                ProsodyFlat = allProsodyFlat.ToArray(),
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
            _isTrigramDetectionActive = false;

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

        // ── Private helpers ─────────────────────────────────────────────────

        /// <summary>
        /// Attempts to upgrade the detector to a HybridLanguageDetector by loading trigram profiles.
        /// Falls back to the existing UnicodeLanguageDetector if profiles are unavailable
        /// or fewer than 2 Latin languages are configured.
        /// </summary>
        private async Task TryUpgradeToHybridDetectorAsync(CancellationToken cancellationToken)
        {
            // Count Latin languages in the configured language list
            var latinCount = 0;
            for (var i = 0; i < _languages.Count; i++)
            {
                if (LanguageConstants.IsLatinLanguage(_languages[i]))
                    latinCount++;
            }

            if (latinCount < 2)
            {
                PiperLogger.LogInfo(
                    "[MultilingualPhonemizer] Trigram detection not needed: " +
                    $"only {latinCount} Latin language(s) configured " +
                    "(requires 2+ for disambiguation). Using Unicode-only detection.");
                return;
            }

            try
            {
                var profiles = await TrigramProfileLoader.LoadAsync(cancellationToken);
                if (profiles == null || profiles.Count == 0)
                {
                    PiperLogger.LogWarning(
                        "[MultilingualPhonemizer] Trigram profiles not found at " +
                        "StreamingAssets/uPiper/LanguageProfiles/trigram_profiles.json. " +
                        "Falling back to Unicode-only detection. " +
                        "All Latin-script text will be treated as the default language. " +
                        "To enable Latin language disambiguation, ensure the trigram profiles file exists.");
                    return;
                }

                var trigramDetector = new TrigramLanguageDetector(profiles);
                var unicodeDetector = _detector as UnicodeLanguageDetector
                    ?? new UnicodeLanguageDetector(_languages, _defaultLatinLanguage);

                _detector = new HybridLanguageDetector(
                    unicodeDetector, trigramDetector, _languages, _defaultLatinLanguage);

                _isTrigramDetectionActive = true;
                PiperLogger.LogInfo(
                    "[MultilingualPhonemizer] Upgraded to HybridLanguageDetector " +
                    $"with {profiles.Count} language profile(s): " +
                    $"[{string.Join(", ", profiles.Keys)}]. " +
                    "Latin-script language disambiguation is now active.");
            }
            catch (Exception ex)
            {
                PiperLogger.LogWarning(
                    "[MultilingualPhonemizer] Failed to load trigram profiles: " +
                    $"{ex.Message}. Falling back to Unicode-only detection. " +
                    "Latin-script language disambiguation will not be available.");
            }
        }
    }
}