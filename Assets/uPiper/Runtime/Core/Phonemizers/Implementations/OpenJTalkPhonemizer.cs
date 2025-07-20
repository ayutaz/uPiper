#if !UNITY_WEBGL

// Define a constant to control P/Invoke usage
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
#define ENABLE_PINVOKE
#endif

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using uPiper.Core.Logging;
using uPiper.Core.Platform;

namespace uPiper.Core.Phonemizers.Implementations
{
    /// <summary>
    /// OpenJTalk-based phonemizer for Japanese text processing.
    /// Uses P/Invoke to call native OpenJTalk library.
    /// </summary>
    public class OpenJTalkPhonemizer : BasePhonemizer
    {
        #region Mock Mode Support

        private static bool mockMode = false;
        private static bool forceUseMock = false;

        /// <summary>
        /// Enable mock mode for testing without native library
        /// </summary>
        public static bool MockMode
        {
            get => mockMode || forceUseMock;
            set => forceUseMock = value;
        }

        #endregion

        #region Native Structures

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePhonemeResult
        {
            public IntPtr phonemes;      // char* - space-separated phoneme string (UTF-8)
            public IntPtr phoneme_ids;   // int*
            public int phoneme_count;
            public IntPtr durations;     // float*
            public float total_duration;
        }

        #endregion

        #region P/Invoke Declarations

        private const string LIBRARY_NAME = "openjtalk_wrapper";

#if ENABLE_PINVOKE
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr openjtalk_create([MarshalAs(UnmanagedType.LPStr)] string dict_path);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void openjtalk_destroy(IntPtr handle);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr openjtalk_phonemize(IntPtr handle, string text);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void openjtalk_free_result(IntPtr result);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr openjtalk_get_version();

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int openjtalk_get_last_error(IntPtr handle);

        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr openjtalk_get_error_string(int error_code);
#else
        // Stub implementations for when P/Invoke is disabled
        private static IntPtr openjtalk_create(string dict_path) => IntPtr.Zero;
        private static void openjtalk_destroy(IntPtr handle) { }
        private static IntPtr openjtalk_phonemize(IntPtr handle, string text) => IntPtr.Zero;
        private static void openjtalk_free_result(IntPtr result) { }
        private static IntPtr openjtalk_get_version() => IntPtr.Zero;
        private static int openjtalk_get_last_error(IntPtr handle) => -1;
        private static IntPtr openjtalk_get_error_string(int error_code) => IntPtr.Zero;
#endif

        #endregion

        #region Fields

        private IntPtr _handle = IntPtr.Zero;
        private readonly object _handleLock = new object();
        private bool _disposed;
        private readonly string _dictionaryPath;

        // Phoneme mapping from OpenJTalk to Piper format
        private static readonly Dictionary<string, int> phonemeToId = new Dictionary<string, int>
        {
            // Japanese phonemes (example mapping - needs to be completed based on actual Piper model)
            {"pau", 0}, {"sil", 0}, // Silence
            {"a", 1}, {"i", 2}, {"u", 3}, {"e", 4}, {"o", 5},
            {"ka", 6}, {"ki", 7}, {"ku", 8}, {"ke", 9}, {"ko", 10},
            {"ga", 11}, {"gi", 12}, {"gu", 13}, {"ge", 14}, {"go", 15},
            {"sa", 16}, {"shi", 17}, {"su", 18}, {"se", 19}, {"so", 20},
            {"za", 21}, {"ji", 22}, {"zu", 23}, {"ze", 24}, {"zo", 25},
            {"ta", 26}, {"chi", 27}, {"tsu", 28}, {"te", 29}, {"to", 30},
            {"da", 31}, {"di", 32}, {"du", 33}, {"de", 34}, {"do", 35},
            {"na", 36}, {"ni", 37}, {"nu", 38}, {"ne", 39}, {"no", 40},
            {"ha", 41}, {"hi", 42}, {"fu", 43}, {"he", 44}, {"ho", 45},
            {"ba", 46}, {"bi", 47}, {"bu", 48}, {"be", 49}, {"bo", 50},
            {"pa", 51}, {"pi", 52}, {"pu", 53}, {"pe", 54}, {"po", 55},
            {"ma", 56}, {"mi", 57}, {"mu", 58}, {"me", 59}, {"mo", 60},
            {"ya", 61}, {"yu", 62}, {"yo", 63},
            {"ra", 64}, {"ri", 65}, {"ru", 66}, {"re", 67}, {"ro", 68},
            {"wa", 69}, {"wo", 70}, {"n", 71},
            // Add more mappings as needed
        };

        #endregion

        #region Properties

        public override string Name => "OpenJTalk";
        public override string Version => GetVersionString();
        public override string[] SupportedLanguages => new[] { "ja" };

        #endregion

        #region Constructor and Destructor

        public OpenJTalkPhonemizer(int cacheCapacity = 1000, string dictionaryPath = null, bool forceMockMode = false)
            : base(cacheCapacity)
        {
            // Allow explicit control over mock mode
            if (forceMockMode)
            {
                mockMode = true;
                Debug.Log("[OpenJTalkPhonemizer] Mock mode forced.");
            }
            else if (IsInTestRunner() && !IsNativeTestContext())
            {
                // Auto-enable mock mode in test runner, except for native tests
                mockMode = true;
                Debug.Log("[OpenJTalkPhonemizer] Test runner detected. Using mock mode.");
            }

            _dictionaryPath = dictionaryPath ?? GetDefaultDictionaryPath();
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

                // Check if we should use mock mode
                if (MockMode)
                {
                    mockMode = true;
                    _handle = new IntPtr(1); // Fake handle for mock mode
                    Debug.Log("[OpenJTalkPhonemizer] Running in mock mode (explicitly requested)");
                    return;
                }
                
                if (!IsNativeLibraryAvailable())
                {
                    var expectedPath = GetExpectedLibraryPath();
                    Debug.LogError($"[OpenJTalkPhonemizer] Native library not found at: {expectedPath}");
                    Debug.LogError("[OpenJTalkPhonemizer] Please ensure the OpenJTalk native library is properly installed.");
                    Debug.LogError("[OpenJTalkPhonemizer] Expected locations:");
                    Debug.LogError($"  - macOS: {Path.Combine(Application.dataPath, "uPiper/Plugins/macOS/libopenjtalk_wrapper.dylib")}");
                    Debug.LogError($"  - Windows: {Path.Combine(Application.dataPath, "uPiper/Plugins/Windows/x86_64/openjtalk_wrapper.dll")}");
                    Debug.LogError($"  - Linux: {Path.Combine(Application.dataPath, "uPiper/Plugins/Linux/x86_64/libopenjtalk_wrapper.so")}");
                    
                    mockMode = true;
                    _handle = new IntPtr(1); // Fake handle for mock mode
                    Debug.LogWarning("[OpenJTalkPhonemizer] Falling back to mock mode. Text-to-phoneme conversion will be simulated.");
                    return;
                }

                if (!Directory.Exists(_dictionaryPath))
                {
                    throw new PiperConfigurationException(
                        $"Dictionary directory not found: {_dictionaryPath}");
                }

                try
                {
#if ENABLE_PINVOKE
                    Debug.Log($"[OpenJTalkPhonemizer] Attempting to create OpenJTalk instance with dictionary: {_dictionaryPath}");
                    _handle = openjtalk_create(_dictionaryPath);
                    if (_handle == IntPtr.Zero)
                    {
                        throw new PiperInitializationException(
                            "Failed to create OpenJTalk instance");
                    }

                    // Log version info
                    var version = GetVersionString();
                    Debug.Log($"[OpenJTalkPhonemizer] Successfully initialized OpenJTalk");
                    Debug.Log($"[OpenJTalkPhonemizer] Version: {version}");
                    Debug.Log($"[OpenJTalkPhonemizer] Dictionary path: {_dictionaryPath}");
                    Debug.Log($"[OpenJTalkPhonemizer] Native handle: 0x{_handle.ToInt64():X}");
#else
                    // P/Invoke is disabled, use mock mode
                    mockMode = true;
                    _handle = new IntPtr(1); // Fake handle for mock mode
                    Debug.LogWarning($"[OpenJTalkPhonemizer] P/Invoke disabled. Running in mock mode.");
#endif
                }
                catch (Exception ex)
                {
                    mockMode = true;
                    _handle = new IntPtr(1); // Fake handle for mock mode
                    Debug.LogWarning($"[OpenJTalkPhonemizer] Failed to initialize: {ex.Message}. Running in mock mode.");
                }
            }
        }

        private string GetVersionString()
        {
            if (MockMode)
            {
                return "1.0.0-mock";
            }

#if ENABLE_PINVOKE
            try
            {
                var versionPtr = openjtalk_get_version();
                if (versionPtr != IntPtr.Zero)
                {
                    return Marshal.PtrToStringAnsi(versionPtr) ?? "1.0.0";
                }
            }
            catch
            {
                // Ignore errors during version retrieval
            }
#endif
            return "1.0.0";
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

            lock (_handleLock)
            {
                if (_handle == IntPtr.Zero)
                    throw new PiperInitializationException("OpenJTalk not initialized");

                // Use mock implementation if in mock mode
                if (MockMode)
                {
                    return GenerateMockPhonemes(text);
                }
                
                // Log input text
                PiperLogger.LogDebug($"[OpenJTalkPhonemizer] Processing text: '{text}' (length: {text.Length})");

                IntPtr resultPtr = IntPtr.Zero;
                try
                {
#if ENABLE_PINVOKE
                    // Call native phonemize function (Unity marshals as UTF-8 by default)
                    resultPtr = openjtalk_phonemize(_handle, text);
                    if (resultPtr == IntPtr.Zero)
                    {
                        var errorCode = openjtalk_get_last_error(_handle);
                        var errorMsgPtr = openjtalk_get_error_string(errorCode);
                        var errorMsg = errorMsgPtr != IntPtr.Zero 
                            ? Marshal.PtrToStringAnsi(errorMsgPtr) 
                            : "Unknown error";
                        
                        throw new PiperPhonemizationException(text, "ja",
                            $"OpenJTalk phonemization failed: {errorMsg}");
                    }
#else
                    // P/Invoke disabled
                    throw new PiperInitializationException("P/Invoke is disabled");
#endif

                    // Marshal the result
                    var nativeResult = Marshal.PtrToStructure<NativePhonemeResult>(resultPtr);
                    
                    // Log native result info
                    PiperLogger.LogDebug($"[OpenJTalkPhonemizer] Native result - phoneme_count: {nativeResult.phoneme_count}, total_duration: {nativeResult.total_duration:F3}");

                    // Convert to PhonemeResult
                    return ConvertToPhonemeResult(nativeResult);
                }
                finally
                {
#if ENABLE_PINVOKE
                    if (resultPtr != IntPtr.Zero)
                        openjtalk_free_result(resultPtr);
#endif
                }
            }
        }

        #endregion

        #region Conversion Methods

        private PhonemeResult ConvertToPhonemeResult(NativePhonemeResult nativeResult)
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
                PiperLogger.LogDebug($"[OpenJTalkPhonemizer] Native phoneme string: '{phonemeString}'");
                PiperLogger.LogDebug($"[OpenJTalkPhonemizer] Native phoneme count: {nativeResult.phoneme_count}");
                
                if (!string.IsNullOrEmpty(phonemeString))
                {
                    var phonemeList = phonemeString.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var unknownPhonemes = new System.Collections.Generic.HashSet<string>();
                    
                    // Log if phoneme count mismatch
                    if (phonemeList.Length != nativeResult.phoneme_count)
                    {
                        PiperLogger.LogWarning($"[OpenJTalkPhonemizer] Phoneme count mismatch: expected {nativeResult.phoneme_count}, got {phonemeList.Length}");
                    }

                    for (int i = 0; i < Math.Min(phonemeList.Length, nativeResult.phoneme_count); i++)
                    {
                        phonemes[i] = phonemeList[i];

                        // Map phoneme to ID
                        if (phonemeToId.TryGetValue(phonemes[i], out var id))
                        {
                            phonemeIds[i] = id;
                        }
                        else
                        {
                            // Default to unknown phoneme ID
                            phonemeIds[i] = 0;
                            unknownPhonemes.Add(phonemes[i]);
                        }
                    }

                    // Log unknown phonemes once per conversion
                    if (unknownPhonemes.Count > 0)
                    {
                        Debug.LogWarning($"[OpenJTalkPhonemizer] Unknown phonemes: {string.Join(", ", unknownPhonemes)}");
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
            for (int i = 0; i < nativeResult.phoneme_count; i++)
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
                Metadata = $"TotalDuration:{nativeResult.total_duration:F3}"
            };
        }

        #endregion

        #region Helper Methods

        private static string GetDefaultDictionaryPath()
        {
            // Look for dictionary in various locations
            var possiblePaths = new[]
            {
                // NAIST dictionary path in StreamingAssets (priority)
                Path.Combine(Application.streamingAssetsPath, "uPiper", "OpenJTalk", "naist_jdic", "open_jtalk_dic_utf_8-1.11"),
                // Legacy dictionary paths
                Path.Combine(Application.streamingAssetsPath, "uPiper", "OpenJTalk", "dictionary"),
                Path.Combine(Application.dataPath, "StreamingAssets", "uPiper", "OpenJTalk", "dictionary"),
                Path.Combine(PlatformHelper.GetNativeLibraryDirectory(), "OpenJTalk", "dictionary"),
            };

            foreach (var path in possiblePaths)
            {
                if (Directory.Exists(path))
                {
                    // Verify it contains required files
                    bool allFilesExist = true;
                    foreach (var file in OpenJTalkConstants.RequiredDictionaryFiles)
                    {
                        if (!File.Exists(Path.Combine(path, file)))
                        {
                            allFilesExist = false;
                            break;
                        }
                    }

                    if (allFilesExist)
                    {
                        Debug.Log($"[OpenJTalkPhonemizer] Found complete dictionary at: {path}");
                        return path;
                    }
                    else
                    {
                        Debug.LogWarning($"[OpenJTalkPhonemizer] Dictionary at {path} is incomplete");
                    }
                }
            }

            Debug.LogWarning($"[OpenJTalkPhonemizer] Dictionary not found in any of the expected locations");
            // Return first path as default (will fail later if not found)
            return possiblePaths[0];
        }

        #endregion

        #region Mock Implementation

        private PhonemeResult GenerateMockPhonemes(string text)
        {
            // Simple mock implementation for testing
            var mockPhonemes = new List<string>();
            var mockIds = new List<int>();
            var mockDurations = new List<float>();
            var mockPitches = new List<float>();

            // Generate simple mock phonemes based on text length
            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c)) continue;

                // Add a mock phoneme
                mockPhonemes.Add("mock_" + c);
                mockIds.Add(mockPhonemes.Count);
                mockDurations.Add(0.1f);
                mockPitches.Add(1.0f);
            }

            if (mockPhonemes.Count == 0)
            {
                mockPhonemes.Add("sil");
                mockIds.Add(0);
                mockDurations.Add(0.1f);
                mockPitches.Add(1.0f);
            }

            return new PhonemeResult
            {
                OriginalText = text,
                Phonemes = mockPhonemes.ToArray(),
                PhonemeIds = mockIds.ToArray(),
                Durations = mockDurations.ToArray(),
                Pitches = mockPitches.ToArray(),
                Language = "ja",
                ProcessingTime = TimeSpan.FromMilliseconds(10),
                Metadata = "TotalDuration:" + (mockDurations.Count * 0.1f)
            };
        }

        private bool IsNativeLibraryAvailable()
        {
            try
            {
                // Always use mock mode in test runner or when P/Invoke is disabled
#if !ENABLE_PINVOKE
                Debug.LogWarning("[OpenJTalkPhonemizer] P/Invoke is disabled for this platform");
                return false;
#endif

                // First check if we're in a test environment
                // Check environment variable first for explicit test mode
                if (Environment.GetEnvironmentVariable("UPIPER_TEST_MODE") == "true" ||
                    Environment.GetEnvironmentVariable("IS_TEST_ENVIRONMENT") == "true")
                {
                    Debug.Log("[OpenJTalkPhonemizer] Test environment detected via environment variable");
                    return false;
                }

                // Fallback to Unity application state check
                if (Application.isEditor && !Application.isPlaying)
                {
                    // In test runner, always use mock mode to avoid crashes
                    Debug.Log("[OpenJTalkPhonemizer] Unity Test Runner detected");
                    return false;
                }

                // Check platform-specific library file existence
                var libraryPath = GetExpectedLibraryPath();
                if (string.IsNullOrEmpty(libraryPath))
                {
                    Debug.LogError("[OpenJTalkPhonemizer] No library path defined for current platform");
                    return false;
                }
                
                if (!File.Exists(libraryPath))
                {
                    Debug.LogError($"[OpenJTalkPhonemizer] Library file not found: {libraryPath}");
                    Debug.LogError($"[OpenJTalkPhonemizer] Current working directory: {Directory.GetCurrentDirectory()}");
                    Debug.LogError($"[OpenJTalkPhonemizer] Application.dataPath: {Application.dataPath}");
                    return false;
                }
                
                Debug.Log($"[OpenJTalkPhonemizer] Native library found at: {libraryPath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[OpenJTalkPhonemizer] Error checking library availability: {ex.Message}");
                return false;
            }
        }

        private string GetExpectedLibraryPath()
        {
            // First try uPiper/Plugins path
            var uPiperPluginsPath = Path.Combine(Application.dataPath, "uPiper", "Plugins");
            
            if (PlatformHelper.IsWindows)
            {
                var windowsPath = Path.Combine(uPiperPluginsPath, "Windows", "x86_64", "openjtalk_wrapper.dll");
                if (File.Exists(windowsPath)) return windowsPath;
                
                // Fallback to old path
                return Path.Combine(Application.dataPath, "Plugins", "x86_64", "openjtalk_wrapper.dll");
            }
            else if (PlatformHelper.IsMacOS)
            {
                var macPath = Path.Combine(uPiperPluginsPath, "macOS", "libopenjtalk_wrapper.dylib");
                if (File.Exists(macPath)) return macPath;
                
                // Fallback to old path
                return Path.Combine(Application.dataPath, "Plugins", "macOS", "libopenjtalk_wrapper.dylib");
            }
            else if (PlatformHelper.IsLinux)
            {
                var linuxPath = Path.Combine(uPiperPluginsPath, "Linux", "x86_64", "libopenjtalk_wrapper.so");
                if (File.Exists(linuxPath)) return linuxPath;
                
                // Fallback to old path
                return Path.Combine(Application.dataPath, "Plugins", "x86_64", "libopenjtalk_wrapper.so");
            }

            return null;
        }

        private bool IsInTestRunner()
        {
            // Check if we're running in Unity Test Runner
            try
            {
                // Test runner has specific execution context
                var stackTrace = new System.Diagnostics.StackTrace();
                var frames = stackTrace.GetFrames();

                foreach (var frame in frames)
                {
                    var method = frame.GetMethod();
                    if (method != null)
                    {
                        var typeName = method.DeclaringType?.FullName ?? "";
                        if (typeName.Contains("NUnit") ||
                            typeName.Contains("UnityEngine.TestRunner") ||
                            typeName.Contains("UnityEditor.TestRunner"))
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // Ignore any errors in detection
            }

            return false;
        }

        private bool IsNativeTestContext()
        {
            // Check if we're being called from native tests
            try
            {
                var stackTrace = new System.Diagnostics.StackTrace();
                var frames = stackTrace.GetFrames();

                foreach (var frame in frames)
                {
                    var method = frame.GetMethod();
                    if (method != null)
                    {
                        var typeName = method.DeclaringType?.FullName ?? "";
                        if (typeName.Contains("OpenJTalkNativeTest"))
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // Ignore any errors in detection
            }

            return false;
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
                            if (!MockMode)
                            {
                                openjtalk_destroy(_handle);
                            }
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
    }
}
#endif