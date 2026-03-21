using System;
using System.Collections.Generic;
using System.IO;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.IO.Compression;
#endif
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotNetG2P;
using DotNetG2P.MeCab;
#if UNITY_WEBGL && !UNITY_EDITOR
using DotNetG2P.MeCab.Dictionary;
#endif
using DotNetG2P.Models;
using UnityEngine;
using UnityEngine.Scripting;
using uPiper.Core.Logging;
using uPiper.Core.Phonemizers.Backend;
#if UNITY_WEBGL && !UNITY_EDITOR
using uPiper.Core.Platform;
#endif

namespace uPiper.Core.Phonemizers.Implementations
{
    /// <summary>
    /// Pure C# Japanese phonemizer using dot-net-g2p library.
    /// Provides the same interface as OpenJTalkPhonemizer without native plugin dependency.
    /// </summary>
    [Preserve]
    public class DotNetG2PPhonemizer : IPhonemizer
    {
        #region Nested Types

        /// <summary>
        /// Result of prosody-aware phonemization
        /// </summary>
        public class ProsodyResult
        {
            public string[] Phonemes { get; set; }
            public int[] ProsodyA1 { get; set; }
            public int[] ProsodyA2 { get; set; }
            public int[] ProsodyA3 { get; set; }
            public int PhonemeCount { get; set; }
        }

        #endregion

        #region Fields

        private G2PEngine _engine;
        private MeCabTokenizer _tokenizer;
        private readonly CustomDictionary _customDictionary;
        private readonly object _engineLock = new();
        private bool _disposed;
        private readonly string _dictionaryPath;

        #endregion

        #region Constructor and Destructor

        public DotNetG2PPhonemizer(string dictionaryPath = null,
            bool loadCustomDictionary = true)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL: skip synchronous initialization, use InitializeAsync() instead
            _dictionaryPath = null;
            _customDictionary = new CustomDictionary(false); // WebGL: async load in InitializeAsync()
#else
            _dictionaryPath = dictionaryPath ?? GetDefaultDictionaryPath();
            _customDictionary = new CustomDictionary(loadCustomDictionary);
            Initialize();
#endif
        }

        ~DotNetG2PPhonemizer()
        {
            Dispose(false);
        }

        #endregion

        #region Initialization

#if !UNITY_WEBGL || UNITY_EDITOR
        private void Initialize()
        {
            if (!Directory.Exists(_dictionaryPath))
            {
                throw new PiperConfigurationException(
                    $"Dictionary directory not found: {_dictionaryPath}");
            }

            try
            {
                _tokenizer = new MeCabTokenizer(_dictionaryPath);
                _engine = new G2PEngine(_tokenizer);
                Debug.Log($"[DotNetG2PPhonemizer] Initialized with dictionary: {_dictionaryPath}");
            }
            catch (Exception ex)
            {
                throw new PiperInitializationException(
                    $"Failed to initialize DotNetG2P: {ex.Message}", ex);
            }
        }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
        /// <summary>
        /// Asynchronously initialize the phonemizer for WebGL.
        /// Downloads the MeCab dictionary ZIP from StreamingAssets and loads it via byte[] API.
        /// </summary>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                Debug.Log("[DotNetG2PPhonemizer] WebGL: Downloading dictionary ZIP...");

                var zipData = await WebGLStreamingAssetsLoader.LoadBytesAsync(
                    "uPiper/OpenJTalk/naist_jdic.zip", cancellationToken);

                Debug.Log($"[DotNetG2PPhonemizer] WebGL: ZIP downloaded ({zipData.Length} bytes), extracting...");

                byte[] sysDic = null, matrix = null, charBin = null, unkDic = null;

                using (var zipStream = new MemoryStream(zipData))
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
                {
                    foreach (var entry in archive.Entries)
                    {
                        var name = Path.GetFileName(entry.FullName);
                        if (string.IsNullOrEmpty(name))
                            continue;

                        using var entryStream = entry.Open();
                        using var ms = new MemoryStream();
                        entryStream.CopyTo(ms);
                        var data = ms.ToArray();

                        switch (name)
                        {
                            case "sys.dic":
                                sysDic = data;
                                break;
                            case "matrix.bin":
                                matrix = data;
                                break;
                            case "char.bin":
                                charBin = data;
                                break;
                            case "unk.dic":
                                unkDic = data;
                                break;
                        }
                    }
                }

                if (sysDic == null || matrix == null || charBin == null || unkDic == null)
                {
                    throw new PiperInitializationException(
                        "Failed to extract MeCab dictionary from ZIP: missing required files " +
                        $"(sys.dic={sysDic != null}, matrix.bin={matrix != null}, " +
                        $"char.bin={charBin != null}, unk.dic={unkDic != null})");
                }

                var bundle = DictionaryBundle.Load(sysDic, matrix, charBin, unkDic);
                _tokenizer = new MeCabTokenizer(bundle);
                _engine = new G2PEngine(_tokenizer);

                Debug.Log("[DotNetG2PPhonemizer] WebGL: Dictionary initialized from ZIP");

                // Load custom dictionaries asynchronously (WebGL cannot use synchronous file I/O)
                await _customDictionary.LoadDefaultDictionariesAsync(cancellationToken);
            }
            catch (PiperInitializationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new PiperInitializationException(
                    $"Failed to initialize DotNetG2P on WebGL: {ex.Message}", ex);
            }
        }
#endif

        #endregion

        #region IPhonemizer Implementation

#pragma warning disable CS1998 // Async method lacks 'await' operators
        public async Task<PhonemeResult> PhonemizeAsync(
            string text,
            string language = "ja",
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new PhonemeResult
                {
                    OriginalText = text ?? string.Empty,
                    Language = language,
                    Phonemes = Array.Empty<string>(),
                    PhonemeIds = Array.Empty<int>(),
                    Durations = Array.Empty<float>(),
                    Pitches = Array.Empty<float>(),
                    ProcessingTime = TimeSpan.Zero,
                    FromCache = false
                };
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            return PhonemizeInternal(text);
#else
            return await Task.Run(() => PhonemizeInternal(text), cancellationToken);
#endif
        }
#pragma warning restore CS1998

        public void ClearCache()
        {
            // No-op: caching handled by PhonemeCache.Instance
        }

        public CacheStatistics GetCacheStatistics()
        {
            return new CacheStatistics();
        }
#pragma warning restore CS1998

        private PhonemeResult PhonemizeInternal(string text)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DotNetG2PPhonemizer));

            if (string.IsNullOrEmpty(text))
            {
                return new PhonemeResult
                {
                    OriginalText = text ?? string.Empty,
                    Language = "ja",
                    ProcessingTime = TimeSpan.Zero,
                    FromCache = false
                };
            }

            var processedText = ApplyCustomDictionaryWithLogging(text);

            string phonemeString;
            lock (_engineLock)
            {
                phonemeString = _engine.ToPhonemes(processedText);
            }

            PiperLogger.LogDebug($"[DotNetG2PPhonemizer] Raw phonemes: '{phonemeString}'");

            if (string.IsNullOrEmpty(phonemeString))
            {
                return new PhonemeResult
                {
                    OriginalText = text,
                    Language = "ja",
                    ProcessingTime = TimeSpan.Zero,
                    FromCache = false
                };
            }

            // Split and convert
            var phonemeArray = phonemeString.Split(
                new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            var piperPhonemes = ConvertAndFinalizePhonemes(phonemeArray, text);

            PiperLogger.LogDebug(
                $"[DotNetG2PPhonemizer] Final phonemes: {string.Join(" ", piperPhonemes.Select(p => p.Length == 1 && p[0] >= '\ue000' && p[0] <= '\uf8ff' ? $"PUA(U+{((int)p[0]):X4})" : $"'{p}'"))}");

            // Build PhonemeResult
            var phonemes = new string[piperPhonemes.Length];
            var phonemeIds = new int[piperPhonemes.Length];
            var durations = new float[piperPhonemes.Length];
            var pitches = new float[piperPhonemes.Length];

            for (var i = 0; i < piperPhonemes.Length; i++)
            {
                phonemes[i] = piperPhonemes[i];
                phonemeIds[i] = 0; // Placeholder - set by PhonemeEncoder based on model
                pitches[i] = 1.0f; // Default pitch
            }

            return new PhonemeResult
            {
                Phonemes = phonemes,
                PhonemeIds = phonemeIds,
                Durations = durations,
                Pitches = pitches,
                Language = "ja",
                ProcessingTime = TimeSpan.Zero, // Placeholder; not measured here
                Metadata = new Dictionary<string, object>
                {
                    ["TotalDuration"] = 0.0f
                }
            };
        }

        #endregion

        #region Prosody API

        /// <summary>
        /// Phonemize text with prosody features (A1/A2/A3 extraction via dot-net-g2p)
        /// </summary>
        /// <param name="text">Japanese text to phonemize</param>
        /// <returns>ProsodyResult containing phonemes and prosody values</returns>
        public ProsodyResult PhonemizeWithProsody(string text)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DotNetG2PPhonemizer));

            if (string.IsNullOrEmpty(text))
            {
                return new ProsodyResult
                {
                    Phonemes = Array.Empty<string>(),
                    ProsodyA1 = Array.Empty<int>(),
                    ProsodyA2 = Array.Empty<int>(),
                    ProsodyA3 = Array.Empty<int>(),
                    PhonemeCount = 0
                };
            }

            var processedText = ApplyCustomDictionaryWithLogging(text);

            ProsodyFeatures features;
            lock (_engineLock)
            {
                features = _engine.ToProsodyFeatures(processedText);
            }

            PiperLogger.LogDebug(
                $"[DotNetG2PPhonemizer] ProsodyFeatures: {features.Count} phonemes");

            if (features.Count == 0)
            {
                return new ProsodyResult
                {
                    Phonemes = Array.Empty<string>(),
                    ProsodyA1 = Array.Empty<int>(),
                    ProsodyA2 = Array.Empty<int>(),
                    ProsodyA3 = Array.Empty<int>(),
                    PhonemeCount = 0
                };
            }

            // Convert phonemes from ProsodyFeatures
            var rawPhonemes = features.Phonemes.ToArray();
            var rawA1 = features.A1.ToArray();
            var rawA2 = features.A2.ToArray();
            var rawA3 = features.A3.ToArray();

            var piperPhonemes = ConvertAndFinalizePhonemes(rawPhonemes, text);

            // Extend prosody arrays to match phoneme count (question marker gets 0)
            var totalCount = piperPhonemes.Length;
            var newA1 = new int[totalCount];
            var newA2 = new int[totalCount];
            var newA3 = new int[totalCount];

            // Copy original prosody values (up to the raw phoneme count)
            var copyCount = Math.Min(rawA1.Length, totalCount - 1);
            Array.Copy(rawA1, newA1, copyCount);
            Array.Copy(rawA2, newA2, copyCount);
            Array.Copy(rawA3, newA3, copyCount);
            // Question marker position remains 0

            PiperLogger.LogDebug(
                $"[DotNetG2PPhonemizer] Prosody extraction complete: {totalCount} phonemes (including question marker)");

            return new ProsodyResult
            {
                Phonemes = piperPhonemes,
                ProsodyA1 = newA1,
                ProsodyA2 = newA2,
                ProsodyA3 = newA3,
                PhonemeCount = totalCount
            };
        }

        #endregion

        #region Extended Question Markers and N Phoneme Variants (piper-plus #210)

        /// <summary>
        /// Tokens that should be skipped when looking for the next actual phoneme
        /// </summary>
        private static readonly HashSet<string> SkipTokens =
            new() { "_", "#", "[", "]", "^", "$", "?", "?!", "?.", "?~" };

        /// <summary>
        /// Bilabial consonants for N phoneme variant detection (N before m/b/p)
        /// </summary>
        private static readonly HashSet<string> BilabialConsonants =
            new() { "m", "my", "b", "by", "p", "py" };

        /// <summary>
        /// Alveolar consonants for N phoneme variant detection (N before n/t/d/ts/ch)
        /// </summary>
        private static readonly HashSet<string> AlveolarConsonants =
            new() { "n", "ny", "t", "ty", "d", "dy", "ts", "ch" };

        /// <summary>
        /// Velar consonants for N phoneme variant detection (N before k/g)
        /// </summary>
        private static readonly HashSet<string> VelarConsonants =
            new() { "k", "ky", "kw", "g", "gy", "gw" };

        /// <summary>
        /// Detect the question type from the input text for extended question markers
        /// </summary>
        /// <param name="text">Input text</param>
        /// <returns>Question type marker</returns>
        private static string GetQuestionType(string text)
        {
            var stripped = text.Trim();

            // Emphatic question: !? or ！？ or ？！
            if (stripped.EndsWith("?!") || stripped.EndsWith("!?") ||
                stripped.EndsWith("！？") || stripped.EndsWith("？！"))
                return "?!";

            // Declarative question: ?. or 。？ or ？。
            if (stripped.EndsWith("?.") || stripped.EndsWith(".?") ||
                stripped.EndsWith("。？") || stripped.EndsWith("？。"))
                return "?.";

            // Confirmatory question: ?~ or ～？ or ？～
            if (stripped.EndsWith("?~") || stripped.EndsWith("~?") ||
                stripped.EndsWith("～？") || stripped.EndsWith("？～"))
                return "?~";

            // Normal question
            if (stripped.EndsWith("?") || stripped.EndsWith("？"))
                return "?";

            // Not a question
            return "$";
        }

        /// <summary>
        /// Apply context-dependent N phoneme rules based on the following consonant
        /// </summary>
        /// <param name="phonemes">Input phoneme array</param>
        /// <returns>Phoneme array with N replaced by context-specific variants</returns>
        private static string[] ApplyNPhonemeRules(string[] phonemes)
        {
            var result = new List<string>(phonemes.Length);

            for (var i = 0; i < phonemes.Length; i++)
            {
                if (phonemes[i] != "N")
                {
                    result.Add(phonemes[i]);
                    continue;
                }

                // Look ahead for the next actual phoneme (skipping special tokens)
                string nextPhoneme = null;
                for (var j = i + 1; j < phonemes.Length; j++)
                {
                    if (!SkipTokens.Contains(phonemes[j]))
                    {
                        nextPhoneme = phonemes[j];
                        break;
                    }
                }

                // Determine N variant based on following consonant
                if (nextPhoneme == null)
                {
                    // End of phrase: uvular N
                    result.Add("N_uvular");
                }
                else if (BilabialConsonants.Contains(nextPhoneme))
                {
                    // Before m, my, b, by, p, py: bilabial assimilation
                    result.Add("N_m");
                }
                else if (AlveolarConsonants.Contains(nextPhoneme))
                {
                    // Before n, ny, t, ty, d, dy, ts, ch: alveolar assimilation
                    result.Add("N_n");
                }
                else if (VelarConsonants.Contains(nextPhoneme))
                {
                    // Before k, ky, kw, g, gy, gw: velar assimilation
                    result.Add("N_ng");
                }
                else
                {
                    // Before vowels or other consonants: uvular N
                    result.Add("N_uvular");
                }
            }

            return result.ToArray();
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Apply custom dictionary replacements and log if any substitution was made.
        /// </summary>
        private string ApplyCustomDictionaryWithLogging(string text)
        {
            var processedText = _customDictionary.ApplyToText(text);
            if (processedText != text)
            {
                PiperLogger.LogInfo(
                    $"[DotNetG2PPhonemizer] Custom dictionary applied: '{text}' -> '{processedText}'");
            }

            return processedText;
        }

        /// <summary>
        /// Convert raw OpenJTalk phonemes to finalized Piper phonemes:
        /// mapping conversion, N-variant rules, and question marker appending.
        /// </summary>
        /// <param name="rawPhonemes">OpenJTalk phoneme array</param>
        /// <param name="originalText">Original input text (used for question detection)</param>
        /// <returns>Finalized Piper phoneme array including trailing question marker</returns>
        private static string[] ConvertAndFinalizePhonemes(string[] rawPhonemes, string originalText)
        {
            // Convert OpenJTalk phonemes to Piper phonemes using the mapping
            var piperPhonemes = OpenJTalkToPiperMapping.ConvertToPiperPhonemes(rawPhonemes);

            // Apply context-dependent N phoneme rules (piper-plus #210)
            piperPhonemes = ApplyNPhonemeRules(piperPhonemes);

            // Append question marker based on original text (piper-plus #210)
            var questionType = GetQuestionType(originalText);
            var piperPhonemesList = new List<string>(piperPhonemes);
            piperPhonemesList.Add(questionType);

            PiperLogger.LogDebug(
                $"[DotNetG2PPhonemizer] Question type detected: '{questionType}' for text: '{originalText}'");

            return piperPhonemesList.ToArray();
        }

        private static string GetDefaultDictionaryPath()
        {
#if UNITY_EDITOR && UPIPER_DEVELOPMENT
            var developmentPath = uPiperPaths.GetDevelopmentOpenJTalkPath();
            if (Directory.Exists(developmentPath))
            {
                Debug.Log(
                    $"[DotNetG2PPhonemizer] Development mode: Loading from Samples~: {developmentPath}");
                return developmentPath;
            }
            else
            {
                Debug.LogError(
                    $"[DotNetG2PPhonemizer] Development mode: Dictionary not found at: {developmentPath}");
                return developmentPath;
            }
#elif UNITY_ANDROID && !UNITY_EDITOR
            // On Android, StreamingAssets is inside APK. Dictionary must be extracted to persistentDataPath.
            var extractedPath = Path.Combine(
                Application.persistentDataPath, "uPiper", "OpenJTalk",
                "naist_jdic", uPiperPaths.OPENJTALK_DICT_NAME);
            if (Directory.Exists(extractedPath))
            {
                Debug.Log($"[DotNetG2PPhonemizer] Found dictionary at: {extractedPath}");
                return extractedPath;
            }

            Debug.LogError("[DotNetG2PPhonemizer] Dictionary not found. Please extract naist_jdic from StreamingAssets.");
            return extractedPath;
#else
            var primaryPath = uPiperPaths.GetRuntimeOpenJTalkPath();
            if (Directory.Exists(primaryPath))
            {
                Debug.Log($"[DotNetG2PPhonemizer] Found dictionary at: {primaryPath}");
                return primaryPath;
            }

            var legacyPath = Path.Combine(
                Application.streamingAssetsPath, "uPiper", "OpenJTalk", "dictionary");
            if (Directory.Exists(legacyPath))
            {
                Debug.Log(
                    $"[DotNetG2PPhonemizer] Found dictionary at legacy path: {legacyPath}");
                return legacyPath;
            }

            Debug.LogError("[DotNetG2PPhonemizer] Dictionary not found.");
            return primaryPath;
#endif
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _engine?.Dispose();
                    _tokenizer?.Dispose();
                }

                _disposed = true;
            }
        }

        #endregion
    }
}