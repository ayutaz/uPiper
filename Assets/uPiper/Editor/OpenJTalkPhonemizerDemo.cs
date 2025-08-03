using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using uPiper.Core.Phonemizers;
using uPiper.Core.Phonemizers.Backend;
#if !UNITY_WEBGL
using uPiper.Core.Phonemizers.Implementations;
#endif

namespace uPiper.Editor
{
#if UNITY_WEBGL
    /// <summary>
    /// Editor window for testing OpenJTalkPhonemizer functionality (not available on WebGL)
    /// </summary>
    public class OpenJTalkPhonemizerDemo : EditorWindow
    {
        [MenuItem("uPiper/Tools/OpenJTalk Phonemizer Test")]
        public static void ShowWindow()
        {
            EditorUtility.DisplayDialog("Not Available", 
                "OpenJTalk Phonemizer Test is not available on WebGL platform.\n\n" +
                "WebGL uses WebAssembly-based phonemization instead.", 
                "OK");
        }
    }
#else
    /// <summary>
    /// Editor window for testing OpenJTalkPhonemizer functionality
    /// </summary>
    public class OpenJTalkPhonemizerDemo : EditorWindow
    {
        private IPhonemizer _phonemizer;
        private string _testText = "こんにちは、世界！今日は良い天気ですね。";
        private string _statusMessage = "Not initialized";
        private bool _isProcessing = false;

        // Results
        private PhonemeResult _lastResult;
        private Vector2 _scrollPosition;

        [MenuItem("uPiper/Tools/OpenJTalk Phonemizer Test")]
        public static void ShowWindow()
        {
            var window = GetWindow<OpenJTalkPhonemizerDemo>("OpenJTalk Phonemizer");
            window.minSize = new Vector2(500, 600);
        }

        private void OnDisable()
        {
            _phonemizer?.Dispose();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("OpenJTalk Phonemizer Demo", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Platform check
#if UNITY_WEBGL
            EditorGUILayout.HelpBox("OpenJTalk is not supported on WebGL platform.", MessageType.Warning);
            return;
#endif

            // Initialization section
            EditorGUILayout.LabelField("1. Initialization", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(_phonemizer != null))
            {
                if (GUILayout.Button("Initialize OpenJTalkPhonemizer"))
                {
                    InitializePhonemizer();
                }
            }

            EditorGUILayout.LabelField("Status:", _statusMessage);

            if (_phonemizer != null)
            {
                EditorGUILayout.LabelField($"Name: {_phonemizer.Name}");
                EditorGUILayout.LabelField($"Version: {_phonemizer.Version}");
                EditorGUILayout.LabelField($"Languages: {string.Join(", ", _phonemizer.SupportedLanguages)}");
            }

            EditorGUILayout.Space();

            // Phonemization section
            using (new EditorGUI.DisabledScope(_phonemizer == null))
            {
                EditorGUILayout.LabelField("2. Phonemization Test", EditorStyles.boldLabel);

                EditorGUILayout.LabelField("Test Text:");
                _testText = EditorGUILayout.TextArea(_testText, GUILayout.Height(60));

                using (new EditorGUI.DisabledScope(_isProcessing))
                {
                    if (GUILayout.Button("Phonemize Text"))
                    {
                        _ = PhonemizeTextAsync();
                    }

                    if (GUILayout.Button("Phonemize (Sync)"))
                    {
                        PhonemizeTextSync();
                    }
                }

                EditorGUILayout.Space();

                // Results section
                if (_lastResult != null && (_lastResult.Phonemes?.Length ?? 0) > 0)
                {
                    EditorGUILayout.LabelField("3. Results", EditorStyles.boldLabel);

                    EditorGUILayout.LabelField($"Phoneme Count: {(_lastResult.Phonemes?.Length ?? 0)}");
                    // Total duration can be extracted from metadata if available
                    var totalDuration = 0f;
                    if (_lastResult.Metadata != null && _lastResult.Metadata.ContainsKey("TotalDuration"))
                    {
                        if (_lastResult.Metadata["TotalDuration"] is float duration)
                        {
                            totalDuration = duration;
                        }
                    }
                    EditorGUILayout.LabelField($"Total Duration: {totalDuration:F3} seconds");
                    EditorGUILayout.LabelField($"Language: {_lastResult.Language}");
                    EditorGUILayout.LabelField($"Processing Time: {_lastResult.ProcessingTimeMs:F3} ms");
                    EditorGUILayout.LabelField($"From Cache: {_lastResult.FromCache}");

                    EditorGUILayout.Space();

                    // Phoneme details
                    EditorGUILayout.LabelField("Phoneme Details:", EditorStyles.boldLabel);

                    _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(200));

                    for (var i = 0; i < (_lastResult.Phonemes?.Length ?? 0); i++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField($"{i + 1}:", GUILayout.Width(40));
                        EditorGUILayout.LabelField(_lastResult.Phonemes[i], GUILayout.Width(80));
                        EditorGUILayout.LabelField($"ID: {_lastResult.PhonemeIds[i]}", GUILayout.Width(60));
                        EditorGUILayout.LabelField($"Duration: {_lastResult.Durations[i]:F3}s", GUILayout.Width(100));
                        EditorGUILayout.LabelField($"Pitch: {_lastResult.Pitches[i]:F2}", GUILayout.Width(80));
                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUILayout.EndScrollView();

                    EditorGUILayout.Space();

                    // Full phoneme string
                    EditorGUILayout.LabelField("Phoneme String:");
                    EditorGUILayout.TextArea(string.Join(" ", _lastResult.Phonemes), GUILayout.Height(40));
                }

                EditorGUILayout.Space();

                // Cache management
                EditorGUILayout.LabelField("4. Cache Management", EditorStyles.boldLabel);

                if (_phonemizer != null)
                {
                    // Statistics display - would need to be implemented
                    EditorGUILayout.LabelField("Cache statistics not available in current implementation");
                }

                if (GUILayout.Button("Clear Cache"))
                {
                    _phonemizer?.ClearCache();
                    _statusMessage = "Cache cleared";
                }

                EditorGUILayout.Space();

                // Test cases
                EditorGUILayout.LabelField("5. Quick Test Cases", EditorStyles.boldLabel);

                if (GUILayout.Button("Test: Hiragana"))
                {
                    _testText = "おはようございます";
                }

                if (GUILayout.Button("Test: Katakana"))
                {
                    _testText = "コンピューター";
                }

                if (GUILayout.Button("Test: Kanji"))
                {
                    _testText = "私は日本語を勉強しています";
                }

                if (GUILayout.Button("Test: Mixed"))
                {
                    _testText = "今日はAIについて学びます";
                }

                if (GUILayout.Button("Test: Numbers"))
                {
                    _testText = "2024年1月17日です";
                }
            }

            // Disposal
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("6. Cleanup", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(_phonemizer == null))
            {
                if (GUILayout.Button("Dispose Phonemizer"))
                {
                    DisposePhonemizer();
                }
            }
        }

        private void InitializePhonemizer()
        {
            try
            {
                _statusMessage = "Initializing...";

#if !UNITY_WEBGL
                _phonemizer = new OpenJTalkPhonemizer();
                _statusMessage = "OpenJTalkPhonemizer initialized successfully (Native Mode)";
#else
                _phonemizer = null;
                _statusMessage = "OpenJTalk not available on WebGL platform. Build for WebGL to use WebAssembly-based phonemization.";
#endif

                Repaint();
            }
            catch (Exception ex)
            {
                _statusMessage = $"Initialization failed: {ex.Message}";
                Debug.LogError($"Failed to initialize phonemizer: {ex}");
                Debug.LogError("\n=== OpenJTalk Installation Guide ===");
                Debug.LogError("To install OpenJTalk native library:");
                Debug.LogError($"1. Navigate to: {System.IO.Path.Combine(Application.dataPath, "../NativePlugins/OpenJTalk/")}");
                Debug.LogError("2. Run build script:");
                Debug.LogError("   - macOS/Linux: ./build.sh");
                Debug.LogError("   - Windows: build.bat");
                Debug.LogError("3. Restart Unity Editor");
                Debug.LogError("====================================");
            }
        }

        private async Task PhonemizeTextAsync()
        {
            try
            {
                _isProcessing = true;
                _statusMessage = "Phonemizing...";
                Repaint();

                _lastResult = await _phonemizer.PhonemizeAsync(_testText);

                _statusMessage = "Phonemization completed";
                _isProcessing = false;
                Repaint();
            }
            catch (Exception ex)
            {
                _statusMessage = $"Phonemization failed: {ex.Message}";
                _isProcessing = false;
                Debug.LogError($"Phonemization error: {ex}");
            }
        }

        private void PhonemizeTextSync()
        {
            try
            {
                _statusMessage = "Phonemizing (sync)...";

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                _lastResult = _phonemizer.Phonemize(_testText);
                stopwatch.Stop();

                _statusMessage = $"Phonemization completed in {stopwatch.ElapsedMilliseconds}ms";
                Repaint();
            }
            catch (Exception ex)
            {
                _statusMessage = $"Phonemization failed: {ex.Message}";
                Debug.LogError($"Phonemization error: {ex}");
            }
        }

        private void DisposePhonemizer()
        {
            _phonemizer?.Dispose();
            _phonemizer = null;
            _lastResult = null;
            _statusMessage = "Disposed";
            Repaint();
        }
    }
#endif
}