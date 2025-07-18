using UnityEngine;
using UnityEditor;
using uPiper.Core;
using System.Linq;

namespace uPiper.Editor
{
    /// <summary>
    /// Custom inspector for PiperTTS configuration
    /// </summary>
    [CustomEditor(typeof(PiperConfig))]
    public class PiperConfigInspector : UnityEditor.Editor
    {
        private SerializedProperty _defaultLanguage;
        private SerializedProperty _sampleRate;
        private SerializedProperty _backend;
        private SerializedProperty _enableMultiThreadedInference;
        private SerializedProperty _workerThreads;
        private SerializedProperty _enablePhonemeCache;
        private SerializedProperty _maxCacheSizeMB;
        private SerializedProperty _timeoutMs;
        
        private void OnEnable()
        {
            _defaultLanguage = serializedObject.FindProperty("DefaultLanguage");
            _sampleRate = serializedObject.FindProperty("SampleRate");
            _backend = serializedObject.FindProperty("Backend");
            _enableMultiThreadedInference = serializedObject.FindProperty("EnableMultiThreadedInference");
            _workerThreads = serializedObject.FindProperty("WorkerThreads");
            _enablePhonemeCache = serializedObject.FindProperty("EnablePhonemeCache");
            _maxCacheSizeMB = serializedObject.FindProperty("MaxCacheSizeMB");
            _timeoutMs = serializedObject.FindProperty("TimeoutMs");
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Piper TTS Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            // Basic Settings
            EditorGUILayout.LabelField("Basic Settings", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            EditorGUILayout.PropertyField(_defaultLanguage);
            EditorGUILayout.PropertyField(_sampleRate);
            EditorGUILayout.PropertyField(_backend);
            
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
            
            // Threading Settings
            EditorGUILayout.LabelField("Threading Settings", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            EditorGUILayout.PropertyField(_enableMultiThreadedInference);
            if (_enableMultiThreadedInference.boolValue)
            {
                EditorGUILayout.PropertyField(_workerThreads);
                _workerThreads.intValue = Mathf.Clamp(_workerThreads.intValue, 1, SystemInfo.processorCount);
            }
            
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
            
            // Cache Settings
            EditorGUILayout.LabelField("Cache Settings", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            EditorGUILayout.PropertyField(_enablePhonemeCache);
            if (_enablePhonemeCache.boolValue)
            {
                EditorGUILayout.PropertyField(_maxCacheSizeMB);
                _maxCacheSizeMB.intValue = Mathf.Clamp(_maxCacheSizeMB.intValue, 1, 1024);
            }
            
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
            
            // Advanced Settings
            EditorGUILayout.LabelField("Advanced Settings", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            
            EditorGUILayout.PropertyField(_timeoutMs);
            _timeoutMs.intValue = Mathf.Max(0, _timeoutMs.intValue);
            
            EditorGUI.indentLevel--;
            
            serializedObject.ApplyModifiedProperties();
        }
    }
    
    /// <summary>
    /// Editor window for testing TTS
    /// </summary>
    public class PiperTTSTestWindow : EditorWindow
    {
        private string _testText = "こんにちは、これはテストです。";
        private string _language = "ja";
        private PiperTTS _piperTTS;
        private AudioClip _generatedClip;
        private string _status = "Not initialized";
        private bool _isProcessing;
        
        [MenuItem("Window/uPiper/TTS Test Window")]
        public static void ShowWindow()
        {
            GetWindow<PiperTTSTestWindow>("Piper TTS Test");
        }
        
        private void OnEnable()
        {
            InitializeTTS();
        }
        
        private void OnDisable()
        {
            _piperTTS?.Dispose();
        }
        
        private async void InitializeTTS()
        {
            var config = new PiperConfig
            {
                DefaultLanguage = _language,
                SampleRate = 22050,
                EnablePhonemeCache = true
            };
            
            _piperTTS = new PiperTTS(config);
            _status = "Initializing...";
            
            try
            {
                await _piperTTS.InitializeAsync();
                _status = "Ready";
            }
            catch (System.Exception ex)
            {
                _status = $"Error: {ex.Message}";
                Debug.LogError($"Failed to initialize TTS: {ex}");
            }
            
            Repaint();
        }
        
        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Piper TTS Test", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            // Status
            EditorGUILayout.LabelField("Status:", _status);
            EditorGUILayout.Space();
            
            // Language selection
            _language = EditorGUILayout.TextField("Language:", _language);
            
            // Text input
            EditorGUILayout.LabelField("Text to synthesize:");
            _testText = EditorGUILayout.TextArea(_testText, GUILayout.Height(60));
            
            EditorGUILayout.Space();
            
            // Generate button
            EditorGUI.BeginDisabledGroup(_isProcessing || _piperTTS == null || !_piperTTS.IsInitialized);
            if (GUILayout.Button("Generate Audio", GUILayout.Height(30)))
            {
                GenerateAudio();
            }
            EditorGUI.EndDisabledGroup();
            
            // Play button
            if (_generatedClip != null)
            {
                EditorGUILayout.Space();
                
                if (GUILayout.Button("Play Audio"))
                {
                    PlayClip(_generatedClip);
                }
                
                EditorGUILayout.LabelField($"Duration: {_generatedClip.length:F2}s");
                EditorGUILayout.LabelField($"Sample Rate: {_generatedClip.frequency}Hz");
            }
            
            // Cache statistics
            if (_piperTTS != null && _piperTTS.IsInitialized)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Cache Statistics", EditorStyles.boldLabel);
                
                var stats = _piperTTS.GetCacheStatistics();
                EditorGUILayout.LabelField($"Entries: {stats.EntryCount}");
                EditorGUILayout.LabelField($"Size: {stats.TotalSizeBytes / 1024f / 1024f:F2} MB");
                EditorGUILayout.LabelField($"Hits: {stats.HitCount}");
                EditorGUILayout.LabelField($"Misses: {stats.MissCount}");
                
                if (GUILayout.Button("Clear Cache"))
                {
                    _piperTTS.ClearCache();
                }
            }
        }
        
        private async void GenerateAudio()
        {
            _isProcessing = true;
            _status = "Generating...";
            Repaint();
            
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                _generatedClip = await _piperTTS.GenerateAudioAsync(_testText);
                stopwatch.Stop();
                
                _status = $"Generated in {stopwatch.ElapsedMilliseconds}ms";
            }
            catch (System.Exception ex)
            {
                _status = $"Error: {ex.Message}";
                Debug.LogError($"Failed to generate audio: {ex}");
            }
            finally
            {
                _isProcessing = false;
                Repaint();
            }
        }
        
        private void PlayClip(AudioClip clip)
        {
            if (clip == null) return;
            
            // Use reflection to play preview
            var unityEditorAssembly = typeof(AudioImporter).Assembly;
            var audioUtilClass = unityEditorAssembly.GetType("UnityEditor.AudioUtil");
            var playClipMethod = audioUtilClass.GetMethod(
                "PlayPreviewClip",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public,
                null,
                new System.Type[] { typeof(AudioClip), typeof(int), typeof(bool) },
                null
            );
            
            playClipMethod.Invoke(null, new object[] { clip, 0, false });
        }
    }
}