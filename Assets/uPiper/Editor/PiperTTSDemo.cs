using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using uPiper.Core;
using uPiper.Core.Logging;

namespace uPiper.Editor
{
    /// <summary>
    /// Editor window for testing PiperTTS functionality
    /// </summary>
    public class PiperTTSDemo : EditorWindow
    {
        private PiperTTS _piperTTS;
        private string _testText = "こんにちは、世界！";
        private AudioClip _generatedClip;
        private bool _isProcessing = false;
        private string _statusMessage = "Not initialized";
        private CacheStatistics _cacheStats;
        
        [MenuItem("uPiper/Demo/PiperTTS Test Window")]
        public static void ShowWindow()
        {
            var window = GetWindow<PiperTTSDemo>("PiperTTS Demo");
            window.minSize = new Vector2(400, 500);
        }
        
        private void OnEnable()
        {
            PiperLogger.SetMinimumLevel(PiperLogger.LogLevel.Debug);
        }
        
        private void OnDisable()
        {
            _piperTTS?.Dispose();
        }
        
        private void OnGUI()
        {
            EditorGUILayout.LabelField("PiperTTS Demo - Phase 1.8 Test (with OpenJTalk)", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            // Initialization section
            EditorGUILayout.LabelField("1. Initialization", EditorStyles.boldLabel);
            
            using (new EditorGUI.DisabledScope(_piperTTS != null && _piperTTS.IsInitialized))
            {
                if (GUILayout.Button("Initialize PiperTTS"))
                {
                    _ = InitializePiperTTSAsync();
                }
            }
            
            EditorGUILayout.LabelField("Status:", _statusMessage);
            EditorGUILayout.Space();
            
            // Voice loading section
            using (new EditorGUI.DisabledScope(_piperTTS == null || !_piperTTS.IsInitialized))
            {
                EditorGUILayout.LabelField("2. Voice Management", EditorStyles.boldLabel);
                
                if (GUILayout.Button("Load Test Voice"))
                {
                    _ = LoadTestVoiceAsync();
                }
                
                if (_piperTTS != null && _piperTTS.CurrentVoice != null)
                {
                    EditorGUILayout.LabelField($"Current Voice: {_piperTTS.CurrentVoice.VoiceId}");
                    EditorGUILayout.LabelField($"Sample Rate: {_piperTTS.CurrentVoice.SampleRate}Hz");
                }
                
                EditorGUILayout.Space();
                
                // Audio generation section
                EditorGUILayout.LabelField("3. Audio Generation (Stub)", EditorStyles.boldLabel);
                
                _testText = EditorGUILayout.TextField("Test Text:", _testText);
                
                using (new EditorGUI.DisabledScope(_isProcessing || _piperTTS.CurrentVoice == null))
                {
                    if (GUILayout.Button("Generate Audio"))
                    {
                        _ = GenerateAudioAsync();
                    }
                    
                    if (GUILayout.Button("Stream Audio"))
                    {
                        _ = StreamAudioAsync();
                    }
                }
                
                if (_generatedClip != null)
                {
                    EditorGUILayout.LabelField($"Generated Clip: {_generatedClip.name}");
                    EditorGUILayout.LabelField($"Duration: {_generatedClip.length:F2} seconds");
                    EditorGUILayout.LabelField($"Samples: {_generatedClip.samples}");
                    
                    if (GUILayout.Button("Play Audio"))
                    {
                        PlayClip(_generatedClip);
                    }
                }
                
                EditorGUILayout.Space();
                
                // Cache section
                EditorGUILayout.LabelField("4. Cache Management", EditorStyles.boldLabel);
                
                if (GUILayout.Button("Preload Text"))
                {
                    _ = PreloadTextAsync();
                }
                
                if (GUILayout.Button("Get Cache Stats"))
                {
                    GetCacheStats();
                }
                
                if (_cacheStats != null)
                {
                    EditorGUILayout.LabelField($"Cache Entries: {_cacheStats.EntryCount}");
                    EditorGUILayout.LabelField($"Cache Size: {_cacheStats.TotalSizeMB:F2}MB / {_cacheStats.MaxSizeMB:F2}MB");
                    EditorGUILayout.LabelField($"Usage: {_cacheStats.UsagePercentage:P0}");
                }
                
                if (GUILayout.Button("Clear Cache"))
                {
                    ClearCache();
                }
            }
            
            // Disposal section
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("5. Cleanup", EditorStyles.boldLabel);
            
            using (new EditorGUI.DisabledScope(_piperTTS == null))
            {
                if (GUILayout.Button("Dispose"))
                {
                    DisposePiperTTS();
                }
            }
        }
        
        private async Task InitializePiperTTSAsync()
        {
            try
            {
                _statusMessage = "Initializing...";
                
                var config = PiperConfig.CreateDefault();
                config.EnableDebugLogging = true;
                config.EnablePhonemeCache = true;
                config.MaxCacheSizeMB = 50;
                config.DefaultLanguage = "ja"; // Set Japanese as default for OpenJTalk
                
                _piperTTS = new PiperTTS(config);
                
                _piperTTS.OnInitialized += (success) =>
                {
                    Debug.Log($"PiperTTS initialized: {success}");
                };
                
                _piperTTS.OnVoiceLoaded += (voice) =>
                {
                    Debug.Log($"Voice loaded: {voice.VoiceId}");
                };
                
                _piperTTS.OnError += (error) =>
                {
                    Debug.LogError($"PiperTTS error: {error.Message}");
                };
                
                _piperTTS.OnProcessingProgress += (progress) =>
                {
                    Debug.Log($"Processing progress: {progress:P0}");
                };
                
                await _piperTTS.InitializeAsync();
                _statusMessage = "Initialized successfully";
                Repaint();
            }
            catch (System.Exception ex)
            {
                _statusMessage = $"Initialization failed: {ex.Message}";
                Debug.LogError(ex);
            }
        }
        
        private async Task LoadTestVoiceAsync()
        {
            try
            {
                _statusMessage = "Loading voice...";
                
                var voice = new PiperVoiceConfig
                {
                    VoiceId = "test-voice-ja",
                    ModelPath = "test_model.onnx",
                    Language = "ja",
                    SampleRate = 22050
                };
                
                await _piperTTS.LoadVoiceAsync(voice);
                _statusMessage = "Voice loaded successfully";
                Repaint();
            }
            catch (System.Exception ex)
            {
                _statusMessage = $"Voice loading failed: {ex.Message}";
                Debug.LogError(ex);
            }
        }
        
        private async Task GenerateAudioAsync()
        {
            try
            {
                _isProcessing = true;
                _statusMessage = "Generating audio...";
                Repaint();
                
                _generatedClip = await _piperTTS.GenerateAudioAsync(_testText);
                
                _statusMessage = "Audio generated successfully";
                _isProcessing = false;
                Repaint();
            }
            catch (System.Exception ex)
            {
                _statusMessage = $"Audio generation failed: {ex.Message}";
                _isProcessing = false;
                Debug.LogError(ex);
            }
        }
        
        private async Task StreamAudioAsync()
        {
            try
            {
                _isProcessing = true;
                _statusMessage = "Streaming audio...";
                Repaint();
                
                int chunkCount = 0;
                await foreach (var chunk in _piperTTS.StreamAudioAsync(_testText))
                {
                    chunkCount++;
                    Debug.Log($"Received chunk {chunk.ChunkIndex}: {chunk.Samples.Length} samples, Final: {chunk.IsFinal}");
                }
                
                _statusMessage = $"Streaming completed: {chunkCount} chunks";
                _isProcessing = false;
                Repaint();
            }
            catch (System.Exception ex)
            {
                _statusMessage = $"Streaming failed: {ex.Message}";
                _isProcessing = false;
                Debug.LogError(ex);
            }
        }
        
        private async Task PreloadTextAsync()
        {
            try
            {
                _statusMessage = "Preloading text...";
                await _piperTTS.PreloadTextAsync(_testText);
                _statusMessage = "Text preloaded to cache";
                Repaint();
            }
            catch (System.Exception ex)
            {
                _statusMessage = $"Preload failed: {ex.Message}";
                Debug.LogError(ex);
            }
        }
        
        private void GetCacheStats()
        {
            try
            {
                _cacheStats = _piperTTS.GetCacheStatistics();
                _cacheStats.LogStatistics();
                Repaint();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to get cache stats: {ex}");
            }
        }
        
        private void ClearCache()
        {
            try
            {
                _piperTTS.ClearCache();
                _statusMessage = "Cache cleared";
                _cacheStats = null;
                Repaint();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to clear cache: {ex}");
            }
        }
        
        private void DisposePiperTTS()
        {
            _piperTTS?.Dispose();
            _piperTTS = null;
            _statusMessage = "Disposed";
            _generatedClip = null;
            _cacheStats = null;
            Repaint();
        }
        
        private void PlayClip(AudioClip clip)
        {
            if (clip == null) return;
            
            // Unity Editor内でAudioClipを再生
            var gameObject = new GameObject("TempAudioPlayer");
            var audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.clip = clip;
            audioSource.Play();
            
            // 再生終了後に自動削除
            EditorApplication.delayCall += () =>
            {
                Task.Delay((int)(clip.length * 1000) + 100).ContinueWith(_ =>
                {
                    EditorApplication.delayCall += () =>
                    {
                        if (gameObject != null)
                            DestroyImmediate(gameObject);
                    };
                });
            };
        }
    }
}