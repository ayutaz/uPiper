#if !UNITY_WEBGL

// Define a constant to control P/Invoke usage
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX || UNITY_ANDROID || UNITY_IOS
#define ENABLE_PINVOKE
#endif

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;
using uPiper.Core;
using uPiper.Core.Logging;
using uPiper.Core.Phonemizers.Backend;
using uPiper.Core.Phonemizers.Native;
using uPiper.Core.Platform;

namespace uPiper.Core.Phonemizers.Implementations
{
    /// <summary>
    /// OpenJTalk-based phonemizer for Japanese text processing.
    /// Uses P/Invoke to call native OpenJTalk library.
    /// </summary>
    [Preserve]
    public class OpenJTalkPhonemizer : BasePhonemizer
    {
        #region Fields

        private IntPtr _handle = IntPtr.Zero;
        private readonly object _handleLock = new();
        private bool _disposed;
        private readonly string _dictionaryPath;
        private readonly CustomDictionary _customDictionary;

        // Note: Phoneme to ID mapping is now handled by PhonemeEncoder based on the model's config
        // The phonemizer only returns the phoneme strings, not IDs

        #endregion

        #region Properties

        public override string Name => "OpenJTalk";
        public override string Version => OpenJTalkNative.GetVersion();
        public override string[] SupportedLanguages => new[] { "ja" };

        #endregion

        #region Constructor and Destructor

        public OpenJTalkPhonemizer(int cacheCapacity = 1000, string dictionaryPath = null, bool loadCustomDictionary = true)
            : base(cacheCapacity)
        {
            _dictionaryPath = dictionaryPath ?? GetDefaultDictionaryPath();
            _customDictionary = new CustomDictionary(loadCustomDictionary);
            Initialize();
        }

        ~OpenJTalkPhonemizer()
        {
            Dispose(false);
        }

        #endregion

        #region Initialization

        private void Initialize()
        {
            lock (_handleLock)
            {
                if (_handle != IntPtr.Zero)
                    return;

                // Try to set DLL directory on Windows in CI environment
#if ENABLE_PINVOKE && UNITY_EDITOR_WIN
                if (NativeLibraryResolver.IsCIEnvironment)
                {
                    Debug.Log("[OpenJTalkPhonemizer] CI environment detected, setting DLL search paths...");

                    // Try multiple DLL directories
                    var dllPaths = NativeLibraryResolver.GetAlternativeLibraryPaths();
                    foreach (var dllPath in dllPaths)
                    {
                        var dirPath = Path.GetDirectoryName(dllPath);
                        if (Directory.Exists(dirPath) && File.Exists(dllPath))
                        {
                            Debug.Log($"[OpenJTalkPhonemizer] Found DLL at: {dllPath}, setting DLL directory to: {dirPath}");
                            OpenJTalkNative.SetDllDirectory(dirPath);
                            break;
                        }
                    }
                }
#endif

                if (!NativeLibraryResolver.IsNativeLibraryAvailable())
                {
                    var expectedPath = NativeLibraryResolver.GetExpectedLibraryPath();
                    var nativePluginsPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../NativePlugins/OpenJTalk/"));

                    // In CI environment, provide more detailed information
                    if (NativeLibraryResolver.IsCIEnvironment)
                    {
                        Debug.LogError("[OpenJTalkPhonemizer] CI Environment detected. Library search failed.");
                        NativeLibraryResolver.LogEnvironmentInfo();
                        NativeLibraryResolver.LogPluginDirectoryContents();
                    }

                    throw new PiperInitializationException(
                        $"OpenJTalk native library not found at: {expectedPath}\n\n" +
                        "To install the OpenJTalk native library:\n" +
                        $"1. Navigate to: {nativePluginsPath}\n" +
                        "2. Run the build script:\n" +
                        "   - macOS/Linux: ./build.sh\n" +
                        "   - Windows: build.bat\n" +
                        "3. Restart Unity Editor to reload native plugins\n\n" +
                        "Expected library locations after build:\n" +
                        $"  - macOS: {Path.Combine(Application.dataPath, "uPiper/Plugins/macOS/libopenjtalk_wrapper.dylib")}\n" +
                        $"  - Windows: {Path.Combine(Application.dataPath, "uPiper/Plugins/Windows/x86_64/openjtalk_wrapper.dll")}\n" +
                        $"  - Linux: {Path.Combine(Application.dataPath, "uPiper/Plugins/Linux/x86_64/libopenjtalk_wrapper.so")}");
                }

                if (!Directory.Exists(_dictionaryPath))
                {
                    throw new PiperConfigurationException(
                        $"Dictionary directory not found: {_dictionaryPath}");
                }

                // Add detailed logging for CI debugging
                NativeLibraryResolver.LogEnvironmentInfo();

                try
                {
#if ENABLE_PINVOKE
                    Debug.Log($"[OpenJTalkPhonemizer] Attempting to create OpenJTalk instance with dictionary: {_dictionaryPath}");
                    _handle = OpenJTalkNative.openjtalk_create(_dictionaryPath);
                    if (_handle == IntPtr.Zero)
                    {
                        throw new PiperInitializationException(
                            "Failed to create OpenJTalk instance");
                    }

                    // Log version info
                    var version = OpenJTalkNative.GetVersion();
                    Debug.Log($"[OpenJTalkPhonemizer] Successfully initialized OpenJTalk");
                    Debug.Log($"[OpenJTalkPhonemizer] Version: {version}");
                    Debug.Log($"[OpenJTalkPhonemizer] Dictionary path: {_dictionaryPath}");
                    Debug.Log($"[OpenJTalkPhonemizer] Native handle: 0x{_handle.ToInt64():X}");
#else
                    throw new PiperInitializationException(
                        "OpenJTalk is not supported on this platform. P/Invoke is disabled.");
#endif
                }
                catch (Exception ex)
                {
                    throw new PiperInitializationException(
                        $"Failed to initialize OpenJTalk: {ex.Message}", ex);
                }
            }
        }

        #endregion

        #region BasePhonemizer Implementation

        protected override async Task<PhonemeResult> PhonemizeInternalAsync(
            string normalizedText,
            string language,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() => PhonemizeInternal(normalizedText), cancellationToken);
        }

        private PhonemeResult PhonemizeInternal(string text)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(OpenJTalkPhonemizer));

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

            // Apply custom dictionary replacements before phonemization
            var processedText = _customDictionary.ApplyToText(text);
            if (processedText != text)
            {
                PiperLogger.LogInfo($"[OpenJTalkPhonemizer] Custom dictionary applied: '{text}' -> '{processedText}'");
            }

            lock (_handleLock)
            {
                if (_handle == IntPtr.Zero)
                    throw new PiperInitializationException("OpenJTalk not initialized");

                // Log input text with detailed encoding information
                PiperLogger.LogDebug($"[OpenJTalkPhonemizer] Processing text: '{processedText}' (length: {processedText.Length})");

                // Debug text encoding to identify Windows-specific issues
                var textBytes = System.Text.Encoding.UTF8.GetBytes(processedText);
                var hexText = string.Join(" ", textBytes.Select(b => b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture)));
                PiperLogger.LogDebug($"[OpenJTalkPhonemizer] Input text UTF-8 bytes: {hexText}");

                // Additional character analysis for Windows debugging
                PiperLogger.LogDebug($"[OpenJTalkPhonemizer] Character analysis:");
                for (var i = 0; i < Math.Min(processedText.Length, 10); i++)
                {
                    var ch = processedText[i];
                    var unicode = ((int)ch).ToString("X4", System.Globalization.CultureInfo.InvariantCulture);
                    PiperLogger.LogDebug($"  [{i}] '{ch}' = U+{unicode}");
                }

                // Enable stderr output for debugging
                if (Application.isEditor)
                {
                    // In Unity Editor, stderr should be visible in Console
                    Debug.Log("[OpenJTalkPhonemizer] Note: Debug logs from native library will appear in stderr/console");
                }

                var resultPtr = IntPtr.Zero;
                try
                {
#if ENABLE_PINVOKE
                    // Call native phonemize function (Unity marshals as UTF-8 by default)
                    resultPtr = OpenJTalkNative.openjtalk_phonemize(_handle, processedText);
                    if (resultPtr == IntPtr.Zero)
                    {
                        var errorCode = OpenJTalkNative.openjtalk_get_last_error(_handle);
                        var errorMsg = OpenJTalkNative.GetErrorMessage(errorCode);

                        throw new PiperPhonemizationException(processedText, "ja",
                            $"OpenJTalk phonemization failed: {errorMsg}");
                    }
#else
                    // P/Invoke disabled
                    throw new PiperInitializationException("P/Invoke is disabled");
#endif

                    // Marshal the result
                    var nativeResult = Marshal.PtrToStructure<OpenJTalkNative.NativePhonemeResult>(resultPtr);

                    // Log native result info
                    PiperLogger.LogDebug($"[OpenJTalkPhonemizer] Native result - phoneme_count: {nativeResult.phoneme_count}, total_duration: {nativeResult.total_duration:F3}");

                    // Convert to PhonemeResult
                    return ConvertToPhonemeResult(nativeResult);
                }
                finally
                {
#if ENABLE_PINVOKE
                    if (resultPtr != IntPtr.Zero)
                        OpenJTalkNative.openjtalk_free_result(resultPtr);
#endif
                }
            }
        }

        #endregion

        #region Conversion Methods

        private PhonemeResult ConvertToPhonemeResult(OpenJTalkNative.NativePhonemeResult nativeResult)
        {
            if (nativeResult.phoneme_count <= 0)
            {
                return new PhonemeResult
                {
                    OriginalText = string.Empty,
                    Language = "ja",
                    ProcessingTime = TimeSpan.Zero,
                    FromCache = false
                };
            }

            var phonemes = new string[nativeResult.phoneme_count];
            var phonemeIds = new int[nativeResult.phoneme_count];
            var durations = new float[nativeResult.phoneme_count];
            var pitches = new float[nativeResult.phoneme_count];

            // Parse space-separated phoneme string
            if (nativeResult.phonemes != IntPtr.Zero)
            {
                var phonemeString = Marshal.PtrToStringAnsi(nativeResult.phonemes);

                // Debug log the raw phoneme string from native library
                Debug.Log($"[OpenJTalkPhonemizer] Native phoneme string: '{phonemeString}'");
                Debug.Log($"[OpenJTalkPhonemizer] Native phoneme count: {nativeResult.phoneme_count}");

                // Calculate checksum for comparison
                uint checksum = 0;
                foreach (var c in phonemeString)
                {
                    checksum = checksum * 31 + (uint)c;
                }
                Debug.Log($"[OpenJTalkPhonemizer] C# checksum: {checksum}");

                // Log raw bytes for debugging
                if (phonemeString.Length > 0 && phonemeString.Length < 200)
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(phonemeString);
                    var hexString = string.Join(" ", bytes.Select(b => b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture)));
                    Debug.Log($"[OpenJTalkPhonemizer] Raw bytes (hex): {hexString}");

                    // Check for specific patterns
                    var firstSpace = phonemeString.IndexOf(' ');
                    var lastSpace = phonemeString.LastIndexOf(' ');
                    Debug.Log($"[OpenJTalkPhonemizer] First space at: {firstSpace}, Last space at: {lastSpace}");

                    // Log first few phonemes individually
                    var parts = phonemeString.Split(' ');
                    if (parts.Length > 0)
                    {
                        Debug.Log($"[OpenJTalkPhonemizer] First 10 phonemes:");
                        for (var i = 0; i < Math.Min(10, parts.Length); i++)
                        {
                            Debug.Log($"  [{i}] '{parts[i]}'");
                        }
                    }
                }

                if (!string.IsNullOrEmpty(phonemeString))
                {
                    var phonemeList = phonemeString.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    // Log if phoneme count mismatch
                    if (phonemeList.Length != nativeResult.phoneme_count)
                    {
                        PiperLogger.LogWarning($"[OpenJTalkPhonemizer] Phoneme count mismatch: expected {nativeResult.phoneme_count}, got {phonemeList.Length}");
                    }

                    // Check for suspicious repetitive patterns (Windows-specific issue)
                    if (DetectRepeatedPatterns(phonemeList))
                    {
                        PiperLogger.LogWarning($"[OpenJTalkPhonemizer] Detected repeated phoneme patterns, attempting to clean up");
                        phonemeList = CleanRepeatedPhonemes(phonemeList);
                    }

                    // Convert OpenJTalk phonemes to Piper phonemes using the mapping
                    var piperPhonemes = OpenJTalkToPiperMapping.ConvertToPiperPhonemes(phonemeList);

                    // Apply context-dependent N phoneme rules (piper-plus #210)
                    piperPhonemes = ApplyNPhonemeRules(piperPhonemes);

                    // Debug log the mapping result
                    PiperLogger.LogDebug($"[OpenJTalkPhonemizer] Phoneme mapping: {string.Join(" ", phonemeList)} -> {string.Join(" ", piperPhonemes.Select(p => p.Length == 1 && p[0] >= '\ue000' && p[0] <= '\uf8ff' ? $"PUA(U+{((int)p[0]):X4})" : $"'{p}'"))}");

                    for (var i = 0; i < Math.Min(piperPhonemes.Length, nativeResult.phoneme_count); i++)
                    {
                        phonemes[i] = piperPhonemes[i];
                        // PhonemeIds will be set by PhonemeEncoder based on the model's phoneme mapping
                        phonemeIds[i] = 0; // Placeholder - not used anymore
                    }
                }
                else
                {
                    PiperLogger.LogError("[OpenJTalkPhonemizer] Phoneme string is empty!");
                }
            }
            else
            {
                PiperLogger.LogError("[OpenJTalkPhonemizer] Native phonemes pointer is null!");
            }

            // Marshal phoneme IDs (if provided by native)
            if (nativeResult.phoneme_ids != IntPtr.Zero)
            {
                Marshal.Copy(nativeResult.phoneme_ids, phonemeIds, 0, nativeResult.phoneme_count);
            }

            // Marshal durations
            if (nativeResult.durations != IntPtr.Zero)
            {
                Marshal.Copy(nativeResult.durations, durations, 0, nativeResult.phoneme_count);
            }

            // Pitches are not provided by the current native implementation
            // Initialize with default values
            for (var i = 0; i < nativeResult.phoneme_count; i++)
            {
                pitches[i] = 1.0f; // Default pitch
            }

            return new PhonemeResult
            {
                Phonemes = phonemes,
                PhonemeIds = phonemeIds,
                Durations = durations,
                Pitches = pitches,
                Language = "ja",
                ProcessingTime = TimeSpan.Zero, // Will be set by BasePhonemizer
                Metadata = new Dictionary<string, object>
                {
                    ["TotalDuration"] = nativeResult.total_duration
                }
            };
        }

        #endregion

        #region Prosody API

        /// <summary>
        /// Result structure for phoneme conversion with prosody features (A1/A2/A3)
        /// </summary>
        public struct ProsodyResult
        {
            /// <summary>Phoneme strings</summary>
            public string[] Phonemes;
            /// <summary>A1: relative position from accent nucleus (can be negative)</summary>
            public int[] ProsodyA1;
            /// <summary>A2: position in accent phrase (1-based)</summary>
            public int[] ProsodyA2;
            /// <summary>A3: total morae in accent phrase</summary>
            public int[] ProsodyA3;
            /// <summary>Number of phonemes</summary>
            public int PhonemeCount;
        }

        /// <summary>
        /// Phonemize text with prosody features (A1/A2/A3 extraction from OpenJTalk labels)
        /// </summary>
        /// <param name="text">Japanese text to phonemize</param>
        /// <returns>ProsodyResult containing phonemes and prosody values</returns>
        public ProsodyResult PhonemizeWithProsody(string text)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(OpenJTalkPhonemizer));

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

            // Apply custom dictionary replacements before phonemization
            var processedText = _customDictionary.ApplyToText(text);
            if (processedText != text)
            {
                PiperLogger.LogInfo($"[OpenJTalkPhonemizer] Custom dictionary applied: '{text}' -> '{processedText}'");
            }

            lock (_handleLock)
            {
                if (_handle == IntPtr.Zero)
                    throw new PiperInitializationException("OpenJTalk not initialized");

                PiperLogger.LogDebug($"[OpenJTalkPhonemizer] Processing text with prosody: '{processedText}'");

                var resultPtr = IntPtr.Zero;
                try
                {
#if ENABLE_PINVOKE
                    resultPtr = OpenJTalkNative.openjtalk_phonemize_with_prosody(_handle, processedText);
                    if (resultPtr == IntPtr.Zero)
                    {
                        var errorCode = OpenJTalkNative.openjtalk_get_last_error(_handle);
                        var errorMsg = OpenJTalkNative.GetErrorMessage(errorCode);

                        throw new PiperPhonemizationException(processedText, "ja",
                            $"OpenJTalk phonemization with prosody failed: {errorMsg}");
                    }
#else
                    throw new PiperInitializationException("P/Invoke is disabled");
#endif

                    // Marshal the result
                    var nativeResult = Marshal.PtrToStructure<OpenJTalkNative.NativeProsodyPhonemeResult>(resultPtr);
                    return ConvertToProsodyResult(nativeResult);
                }
                finally
                {
#if ENABLE_PINVOKE
                    if (resultPtr != IntPtr.Zero)
                        OpenJTalkNative.openjtalk_free_prosody_result(resultPtr);
#endif
                }
            }
        }

        private ProsodyResult ConvertToProsodyResult(OpenJTalkNative.NativeProsodyPhonemeResult nativeResult)
        {
            if (nativeResult.phoneme_count <= 0)
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

            var phonemes = new string[nativeResult.phoneme_count];
            var prosodyA1 = new int[nativeResult.phoneme_count];
            var prosodyA2 = new int[nativeResult.phoneme_count];
            var prosodyA3 = new int[nativeResult.phoneme_count];

            // Parse space-separated phoneme string
            if (nativeResult.phonemes != IntPtr.Zero)
            {
                var phonemeString = Marshal.PtrToStringAnsi(nativeResult.phonemes);

                // DEBUG: Log raw native phoneme string BEFORE any conversion
                PiperLogger.LogInfo($"[DEBUG] Native raw phoneme string (BEFORE conversion): '{phonemeString}'");

                if (!string.IsNullOrEmpty(phonemeString))
                {
                    var phonemeList = phonemeString.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    // DEBUG: Log phoneme list after splitting
                    PiperLogger.LogInfo($"[DEBUG] Phoneme list after split ({phonemeList.Length}): [{string.Join(", ", phonemeList.Select(p => $"'{p}'"))}]");

                    // Convert OpenJTalk phonemes to Piper phonemes using the mapping
                    var piperPhonemes = OpenJTalkToPiperMapping.ConvertToPiperPhonemes(phonemeList);

                    // Apply context-dependent N phoneme rules (piper-plus #210)
                    piperPhonemes = ApplyNPhonemeRules(piperPhonemes);

                    for (var i = 0; i < Math.Min(piperPhonemes.Length, nativeResult.phoneme_count); i++)
                    {
                        phonemes[i] = piperPhonemes[i];
                    }
                }
            }

            // Marshal prosody arrays
            if (nativeResult.prosody_a1 != IntPtr.Zero)
            {
                Marshal.Copy(nativeResult.prosody_a1, prosodyA1, 0, nativeResult.phoneme_count);
            }
            if (nativeResult.prosody_a2 != IntPtr.Zero)
            {
                Marshal.Copy(nativeResult.prosody_a2, prosodyA2, 0, nativeResult.phoneme_count);
            }
            if (nativeResult.prosody_a3 != IntPtr.Zero)
            {
                Marshal.Copy(nativeResult.prosody_a3, prosodyA3, 0, nativeResult.phoneme_count);
            }

            PiperLogger.LogDebug($"[OpenJTalkPhonemizer] Prosody extraction complete: {nativeResult.phoneme_count} phonemes");

            return new ProsodyResult
            {
                Phonemes = phonemes,
                ProsodyA1 = prosodyA1,
                ProsodyA2 = prosodyA2,
                ProsodyA3 = prosodyA3,
                PhonemeCount = nativeResult.phoneme_count
            };
        }

        #endregion

        #region Extended Question Markers and N Phoneme Variants (piper-plus #210)

        /// <summary>
        /// Tokens that should be skipped when looking for the next actual phoneme
        /// </summary>
        private static readonly HashSet<string> SkipTokens = new() { "_", "#", "[", "]", "^", "$", "?", "?!", "?.", "?~" };

        /// <summary>
        /// Bilabial consonants for N phoneme variant detection (N before m/b/p)
        /// </summary>
        private static readonly HashSet<string> BilabialConsonants = new() { "m", "my", "b", "by", "p", "py" };

        /// <summary>
        /// Alveolar consonants for N phoneme variant detection (N before n/t/d/ts/ch)
        /// </summary>
        private static readonly HashSet<string> AlveolarConsonants = new() { "n", "ny", "t", "ty", "d", "dy", "ts", "ch" };

        /// <summary>
        /// Velar consonants for N phoneme variant detection (N before k/g)
        /// </summary>
        private static readonly HashSet<string> VelarConsonants = new() { "k", "ky", "kw", "g", "gy", "gw" };

        /// <summary>
        /// Detect the question type from the input text for extended question markers
        /// </summary>
        /// <param name="text">Input text</param>
        /// <returns>Question type marker: "?!" for emphatic, "?." for declarative, "?~" for confirmatory, "?" for normal, "$" for non-question</returns>
        private static string GetQuestionType(string text)
        {
            var stripped = text.Trim();

            // Emphatic question (強調疑問): !? or ！？ or ？！
            if (stripped.EndsWith("?!") || stripped.EndsWith("!?") ||
                stripped.EndsWith("！？") || stripped.EndsWith("？！"))
                return "?!";

            // Declarative question (平叙疑問): ?. or 。？ or ？。
            if (stripped.EndsWith("?.") || stripped.EndsWith(".?") ||
                stripped.EndsWith("。？") || stripped.EndsWith("？。"))
                return "?.";

            // Confirmatory question (確認疑問): ?~ or ～？ or ？～
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

        private static string GetDefaultDictionaryPath()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // On Android, use the persistent data path where we extract the dictionary
            return AndroidPathResolver.GetOpenJTalkDictionaryPath();
#elif UNITY_EDITOR && UPIPER_DEVELOPMENT
            // Development environment: Load directly from Samples~
            var developmentPath = uPiperPaths.GetDevelopmentOpenJTalkPath();
            if (Directory.Exists(developmentPath))
            {
                Debug.Log($"[OpenJTalkPhonemizer] Development mode: Loading from Samples~: {developmentPath}");
                return developmentPath;
            }
            else
            {
                Debug.LogError($"[OpenJTalkPhonemizer] Development mode: Dictionary not found at: {developmentPath}");
                return developmentPath; // Return expected path for error messages
            }
#elif UNITY_IOS && !UNITY_EDITOR
            // On iOS, use the IOSPathResolver for proper path handling
            return IOSPathResolver.GetOpenJTalkDictionaryPath();
#else
            // Use uPiperPaths for consistent path handling
            var primaryPath = uPiperPaths.GetRuntimeOpenJTalkPath();

            // Check primary path first
            if (Directory.Exists(primaryPath))
            {
                // Verify it contains required files
                var allFilesExist = true;
                foreach (var file in OpenJTalkConstants.RequiredDictionaryFiles)
                {
                    if (!File.Exists(Path.Combine(primaryPath, file)))
                    {
                        allFilesExist = false;
                        break;
                    }
                }

                if (allFilesExist)
                {
                    Debug.Log($"[OpenJTalkPhonemizer] Found dictionary at: {primaryPath}");
                    return primaryPath;
                }
            }

            // Fallback to legacy path
            var legacyPath = Path.Combine(Application.streamingAssetsPath, "uPiper", "OpenJTalk", "dictionary");
            if (Directory.Exists(legacyPath))
            {
                Debug.Log($"[OpenJTalkPhonemizer] Found dictionary at legacy path: {legacyPath}");
                return legacyPath;
            }

            Debug.LogError("[OpenJTalkPhonemizer] Dictionary not found. Please run 'uPiper/Setup/Run Initial Setup' from the menu.");
            return primaryPath; // Return expected path for error messages
#endif
        }

        #endregion

        #region IDisposable Implementation

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                lock (_handleLock)
                {
                    if (_handle != IntPtr.Zero)
                    {
                        try
                        {
#if ENABLE_PINVOKE
                            OpenJTalkNative.openjtalk_destroy(_handle);
#endif
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[OpenJTalkPhonemizer] Error during disposal: {ex.Message}");
                        }
                        finally
                        {
                            _handle = IntPtr.Zero;
                        }
                    }
                }

                _disposed = true;
            }

            base.Dispose(disposing);
        }

        #endregion

        #region Windows Bug Workarounds

        /// <summary>
        /// Detect repeated phoneme patterns that indicate a Windows-specific OpenJTalk bug
        /// </summary>
        private static bool DetectRepeatedPatterns(string[] phonemes)
        {
            if (phonemes.Length < 6) return false;

            // Look for patterns where the same phoneme sequence appears multiple times consecutively
            for (var i = 0; i < phonemes.Length - 6; i++)
            {
                // Check for 3+ character sequences that repeat
                for (var len = 3; len <= Math.Min(6, (phonemes.Length - i) / 2); len++)
                {
                    var isRepeated = true;
                    for (var j = 0; j < len && i + len + j < phonemes.Length; j++)
                    {
                        if (phonemes[i + j] != phonemes[i + len + j])
                        {
                            isRepeated = false;
                            break;
                        }
                    }

                    if (isRepeated)
                    {
                        PiperLogger.LogWarning($"[OpenJTalkPhonemizer] Found repeated pattern of length {len} starting at position {i}: {string.Join(" ", phonemes.Skip(i).Take(len))}");
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Attempt to clean repeated phonemes by removing duplicate patterns
        /// </summary>
        private static string[] CleanRepeatedPhonemes(string[] phonemes)
        {
            var cleaned = new List<string>();
            var skipUntil = -1;

            for (var i = 0; i < phonemes.Length; i++)
            {
                if (i <= skipUntil) continue;

                // Look for the start of a repeated pattern
                for (var len = 3; len <= Math.Min(8, (phonemes.Length - i) / 2); len++)
                {
                    if (i + len * 2 > phonemes.Length) break;

                    var isRepeated = true;
                    for (var j = 0; j < len; j++)
                    {
                        if (phonemes[i + j] != phonemes[i + len + j])
                        {
                            isRepeated = false;
                            break;
                        }
                    }

                    if (isRepeated)
                    {
                        // Add only the first occurrence
                        for (var k = 0; k < len; k++)
                        {
                            cleaned.Add(phonemes[i + k]);
                        }

                        // Skip the repeated part
                        skipUntil = i + len * 2 - 1;
                        PiperLogger.LogInfo($"[OpenJTalkPhonemizer] Removed repeated pattern: {string.Join(" ", phonemes.Skip(i + len).Take(len))}");
                        break;
                    }
                }

                // If no repetition found at this position, add the phoneme normally
                if (skipUntil < i)
                {
                    cleaned.Add(phonemes[i]);
                }
            }

            PiperLogger.LogInfo($"[OpenJTalkPhonemizer] Cleaned phonemes: {phonemes.Length} -> {cleaned.Count}");
            return cleaned.ToArray();
        }

        #endregion
    }
}
#endif