#if !UNITY_WEBGL

// Define a constant to control P/Invoke usage
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX || UNITY_ANDROID
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

        // Unity uses UTF-8 by default, remove CharSet to let Unity handle the marshalling
        [DllImport(LIBRARY_NAME, CallingConvention = CallingConvention.Cdecl)]
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

        // Note: Phoneme to ID mapping is now handled by PhonemeEncoder based on the model's config
        // The phonemizer only returns the phoneme strings, not IDs

        #endregion

        #region Properties

        public override string Name => "OpenJTalk";
        public override string Version => GetVersionString();
        public override string[] SupportedLanguages => new[] { "ja" };

        #endregion

        #region Constructor and Destructor

        public OpenJTalkPhonemizer(int cacheCapacity = 1000, string dictionaryPath = null)
            : base(cacheCapacity)
        {
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

                if (!IsNativeLibraryAvailable())
                {
                    var expectedPath = GetExpectedLibraryPath();
                    var nativePluginsPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../NativePlugins/OpenJTalk/"));

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

        private string GetVersionString()
        {
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

                // Log input text with detailed encoding information
                PiperLogger.LogDebug($"[OpenJTalkPhonemizer] Processing text: '{text}' (length: {text.Length})");

                // Debug text encoding to identify Windows-specific issues
                var textBytes = System.Text.Encoding.UTF8.GetBytes(text);
                var hexText = string.Join(" ", textBytes.Select(b => b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture)));
                PiperLogger.LogDebug($"[OpenJTalkPhonemizer] Input text UTF-8 bytes: {hexText}");

                // Additional character analysis for Windows debugging
                PiperLogger.LogDebug($"[OpenJTalkPhonemizer] Character analysis:");
                for (int i = 0; i < Math.Min(text.Length, 10); i++)
                {
                    var ch = text[i];
                    var unicode = ((int)ch).ToString("X4", System.Globalization.CultureInfo.InvariantCulture);
                    PiperLogger.LogDebug($"  [{i}] '{ch}' = U+{unicode}");
                }

                // Enable stderr output for debugging
                if (Application.isEditor)
                {
                    // In Unity Editor, stderr should be visible in Console
                    Debug.Log("[OpenJTalkPhonemizer] Note: Debug logs from native library will appear in stderr/console");
                }

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
                Debug.Log($"[OpenJTalkPhonemizer] Native phoneme string: '{phonemeString}'");
                Debug.Log($"[OpenJTalkPhonemizer] Native phoneme count: {nativeResult.phoneme_count}");

                // Calculate checksum for comparison
                uint checksum = 0;
                foreach (char c in phonemeString)
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
                        for (int i = 0; i < Math.Min(10, parts.Length); i++)
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

                    // Debug log the mapping result
                    PiperLogger.LogDebug($"[OpenJTalkPhonemizer] Phoneme mapping: {string.Join(" ", phonemeList)} -> {string.Join(" ", piperPhonemes.Select(p => p.Length == 1 && p[0] >= '\ue000' && p[0] <= '\uf8ff' ? $"PUA(U+{((int)p[0]):X4})" : $"'{p}'"))}");

                    for (int i = 0; i < Math.Min(piperPhonemes.Length, nativeResult.phoneme_count); i++)
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

        private bool IsNativeLibraryAvailable()
        {
            try
            {
#if UNITY_EDITOR
                // In Editor, check if the library file exists
                var libraryPath = GetExpectedLibraryPath();
                if (string.IsNullOrEmpty(libraryPath))
                {
                    Debug.LogError("[OpenJTalkPhonemizer] No library path defined for current platform");
                    return false;
                }
                
                // For bundle format on macOS, check if directory exists
                bool libraryExists = false;
                if (PlatformHelper.IsMacOS && libraryPath.EndsWith(".bundle"))
                {
                    libraryExists = Directory.Exists(libraryPath);
                    if (libraryExists)
                    {
                        // Verify the actual binary exists inside the bundle
                        var binaryPath = Path.Combine(libraryPath, "Contents", "MacOS", "openjtalk_wrapper");
                        if (!File.Exists(binaryPath))
                        {
                            Debug.LogError($"[OpenJTalkPhonemizer] Bundle exists but binary not found: {binaryPath}");
                            libraryExists = false;
                        }
                    }
                }
                else
                {
                    libraryExists = File.Exists(libraryPath);
                }
                
                if (!libraryExists)
                {
                    Debug.LogError($"[OpenJTalkPhonemizer] Library not found: {libraryPath}");
                    Debug.LogError($"[OpenJTalkPhonemizer] Current working directory: {Directory.GetCurrentDirectory()}");
                    Debug.LogError($"[OpenJTalkPhonemizer] Application.dataPath: {Application.dataPath}");
                    
                    // List contents of plugin directory for debugging
                    var pluginDir = Path.GetDirectoryName(libraryPath);
                    if (Directory.Exists(pluginDir))
                    {
                        Debug.LogError($"[OpenJTalkPhonemizer] Contents of {pluginDir}:");
                        foreach (var item in Directory.GetFileSystemEntries(pluginDir))
                        {
                            Debug.LogError($"  - {Path.GetFileName(item)}");
                        }
                    }
                    
                    return false;
                }
                
                Debug.Log($"[OpenJTalkPhonemizer] Native library found at: {libraryPath}");
                return true;
#else
                // In built application, try to call a simple function to check if library is loaded
                try
                {
#if ENABLE_PINVOKE
                    // Try to get version as a simple test
                    var version = Marshal.PtrToStringAnsi(openjtalk_get_version());
                    if (!string.IsNullOrEmpty(version))
                    {
                        Debug.Log($"[OpenJTalkPhonemizer] Native library loaded successfully (version: {version})");
                        return true;
                    }
#endif
                    return false;
                }
                catch (DllNotFoundException)
                {
                    Debug.LogError("[OpenJTalkPhonemizer] Native library not found. Please ensure the plugin is properly included in the build.");
                    Debug.LogError("[OpenJTalkPhonemizer] Expected library name: " + GetExpectedLibraryPath());
                    return false;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[OpenJTalkPhonemizer] Error loading native library: {ex.Message}");
                    return false;
                }
#endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"[OpenJTalkPhonemizer] Error checking library availability: {ex.Message}");
                return false;
            }
        }

        private string GetExpectedLibraryPath()
        {
            // In Unity Editor
#if UNITY_EDITOR
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
                // Check for bundle format first (Unity's preferred format for macOS)
                var bundlePath = Path.Combine(uPiperPluginsPath, "macOS", "openjtalk_wrapper.bundle");
                if (Directory.Exists(bundlePath)) return bundlePath;
                
                // Check for dylib format
                var dylibPath = Path.Combine(uPiperPluginsPath, "macOS", "libopenjtalk_wrapper.dylib");
                if (File.Exists(dylibPath)) return dylibPath;
                
                // Fallback to old paths
                var oldBundlePath = Path.Combine(Application.dataPath, "Plugins", "macOS", "openjtalk_wrapper.bundle");
                if (Directory.Exists(oldBundlePath)) return oldBundlePath;
                
                return Path.Combine(Application.dataPath, "Plugins", "macOS", "libopenjtalk_wrapper.dylib");
            }
            else if (PlatformHelper.IsLinux)
            {
                var linuxPath = Path.Combine(uPiperPluginsPath, "Linux", "x86_64", "libopenjtalk_wrapper.so");
                if (File.Exists(linuxPath)) return linuxPath;
                
                // Fallback to old path
                return Path.Combine(Application.dataPath, "Plugins", "x86_64", "libopenjtalk_wrapper.so");
            }
#else
            // In built application, Unity automatically loads native plugins
            // We just need to verify if the library was loaded successfully
            // The actual path checking is not necessary as Unity handles plugin loading

            // Return a dummy path that indicates the library should be loaded by Unity
            if (PlatformHelper.IsWindows)
                return "openjtalk_wrapper.dll";
            else if (PlatformHelper.IsMacOS)
                return "libopenjtalk_wrapper.dylib";
            else if (PlatformHelper.IsLinux)
                return "libopenjtalk_wrapper.so";
#endif

            return null;
        }


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
                            openjtalk_destroy(_handle);
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
            for (int i = 0; i < phonemes.Length - 6; i++)
            {
                // Check for 3+ character sequences that repeat
                for (int len = 3; len <= Math.Min(6, (phonemes.Length - i) / 2); len++)
                {
                    bool isRepeated = true;
                    for (int j = 0; j < len && i + len + j < phonemes.Length; j++)
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

            for (int i = 0; i < phonemes.Length; i++)
            {
                if (i <= skipUntil) continue;

                // Look for the start of a repeated pattern
                for (int len = 3; len <= Math.Min(8, (phonemes.Length - i) / 2); len++)
                {
                    if (i + len * 2 > phonemes.Length) break;

                    bool isRepeated = true;
                    for (int j = 0; j < len; j++)
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
                        for (int k = 0; k < len; k++)
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