using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using uPiper.Core.AudioGeneration;
using uPiper.Core.Platform;

namespace uPiper.Core.Debug
{
    /// <summary>
    /// Debug utility for uPiper TTS system
    /// </summary>
    public class PiperDebugger : MonoBehaviour
    {
        [Header("Debug Settings")]
        [SerializeField] private bool showDebugInfo = true;
        [SerializeField] private bool showPerformanceMetrics = true;
        [SerializeField] private bool showCacheInfo = true;
        [SerializeField] private bool showPhonemeInfo = false;
        [SerializeField] private KeyCode toggleKey = KeyCode.F12;
        
        [Header("Display Settings")]
        [SerializeField] private int fontSize = 14;
        [SerializeField] private Color backgroundColor = new Color(0, 0, 0, 0.8f);
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private Color warningColor = Color.yellow;
        [SerializeField] private Color errorColor = Color.red;
        
        private PiperTTS _piperTTS;
        private bool _isVisible = true;
        private GUIStyle _boxStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _warningStyle;
        private GUIStyle _errorStyle;
        
        private Queue<LogEntry> _logEntries = new Queue<LogEntry>();
        private const int MaxLogEntries = 20;
        
        private float _lastUpdateTime;
        private const float UpdateInterval = 0.5f; // Update every 500ms
        
        // Performance tracking
        private float _fps;
        private float _deltaTime;
        private long _memoryUsage;
        
        // Audio generation tracking
        private int _totalGenerations;
        private float _lastGenerationTime;
        private float _averageGenerationTime;
        
        private class LogEntry
        {
            public string Message { get; set; }
            public LogType Type { get; set; }
            public DateTime Timestamp { get; set; }
        }

        private void Awake()
        {
            // Subscribe to Unity log messages
            Application.logMessageReceived += HandleLog;
        }

        private void Start()
        {
            InitializeStyles();
            
            // PiperTTS is not a MonoBehaviour, it needs to be created or assigned
            if (_piperTTS == null)
            {
                // Create a default instance for debugging
                var config = new PiperConfig();
                _piperTTS = new PiperTTS(config);
                Debug.Log("[PiperDebugger] Created new PiperTTS instance for debugging");
            }
            
            // Subscribe to PiperTTS events
            if (_piperTTS != null)
            {
                _piperTTS.OnProcessingProgress += OnProcessingProgress;
                _piperTTS.OnError += OnError;
            }
        }

        private void Update()
        {
            // Toggle visibility
            if (Input.GetKeyDown(toggleKey))
            {
                _isVisible = !_isVisible;
            }
            
            // Update FPS
            _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
            _fps = 1.0f / _deltaTime;
            
            // Update memory usage periodically
            if (Time.time - _lastUpdateTime > UpdateInterval)
            {
                _lastUpdateTime = Time.time;
                _memoryUsage = GC.GetTotalMemory(false);
            }
        }

        private void OnGUI()
        {
            if (!_isVisible || !showDebugInfo)
                return;

            // Initialize styles if needed
            if (_boxStyle == null)
                InitializeStyles();

            // Calculate window size
            float windowWidth = Screen.width * 0.4f;
            float windowHeight = Screen.height * 0.8f;
            float x = Screen.width - windowWidth - 10;
            float y = 10;
            
            // Draw background
            GUI.Box(new Rect(x, y, windowWidth, windowHeight), "", _boxStyle);
            
            // Draw content
            GUILayout.BeginArea(new Rect(x + 10, y + 10, windowWidth - 20, windowHeight - 20));
            
            // Title
            GUILayout.Label("uPiper TTS Debugger", _labelStyle);
            GUILayout.Space(10);
            
            // System Info
            DrawSystemInfo();
            GUILayout.Space(10);
            
            // Performance Metrics
            if (showPerformanceMetrics)
            {
                DrawPerformanceMetrics();
                GUILayout.Space(10);
            }
            
            // TTS Info
            if (_piperTTS != null)
            {
                DrawTTSInfo();
                GUILayout.Space(10);
                
                // Cache Info
                if (showCacheInfo)
                {
                    DrawCacheInfo();
                    GUILayout.Space(10);
                }
                
                // Sentis Info
                DrawSentisInfo();
                GUILayout.Space(10);
            }
            
            // Log entries
            DrawLogEntries();
            
            GUILayout.EndArea();
        }

        private void InitializeStyles()
        {
            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTexture(2, 2, backgroundColor) }
            };
            
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                normal = { textColor = textColor }
            };
            
            _warningStyle = new GUIStyle(_labelStyle)
            {
                normal = { textColor = warningColor }
            };
            
            _errorStyle = new GUIStyle(_labelStyle)
            {
                normal = { textColor = errorColor }
            };
        }

        private Texture2D MakeTexture(int width, int height, Color color)
        {
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;
            
            var texture = new Texture2D(width, height);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private void DrawSystemInfo()
        {
            GUILayout.Label("=== System Info ===", _labelStyle);
            GUILayout.Label($"Platform: {PlatformHelper.Platform}", _labelStyle);
            GUILayout.Label($"Unity Version: {Application.unityVersion}", _labelStyle);
            GUILayout.Label($"System Memory: {SystemInfo.systemMemorySize} MB", _labelStyle);
            GUILayout.Label($"Processor: {SystemInfo.processorType}", _labelStyle);
            GUILayout.Label($"Processor Count: {SystemInfo.processorCount}", _labelStyle);
        }

        private void DrawPerformanceMetrics()
        {
            GUILayout.Label("=== Performance ===", _labelStyle);
            GUILayout.Label($"FPS: {_fps:F1}", _labelStyle);
            GUILayout.Label($"Memory: {_memoryUsage / 1024f / 1024f:F2} MB", _labelStyle);
            GUILayout.Label($"GC Allocations: {GC.CollectionCount(0)}", _labelStyle);
            
            if (_totalGenerations > 0)
            {
                GUILayout.Label($"Total Generations: {_totalGenerations}", _labelStyle);
                GUILayout.Label($"Avg Generation Time: {_averageGenerationTime:F2}ms", _labelStyle);
            }
        }

        private void DrawTTSInfo()
        {
            GUILayout.Label("=== TTS Status ===", _labelStyle);
            GUILayout.Label($"Initialized: {_piperTTS.IsInitialized}", _labelStyle);
            GUILayout.Label($"Processing: {_piperTTS.IsProcessing}", _labelStyle);
            
            if (_piperTTS.CurrentVoice != null)
            {
                GUILayout.Label($"Current Voice: {_piperTTS.CurrentVoice.VoiceId}", _labelStyle);
                GUILayout.Label($"Language: {_piperTTS.CurrentVoice.Language}", _labelStyle);
            }
            
            GUILayout.Label($"Available Voices: {_piperTTS.AvailableVoices.Count}", _labelStyle);
        }

        private void DrawCacheInfo()
        {
            var cacheStats = _piperTTS.GetCacheStatistics();
            
            GUILayout.Label("=== Cache Info ===", _labelStyle);
            GUILayout.Label($"Entries: {cacheStats.EntryCount}", _labelStyle);
            GUILayout.Label($"Size: {cacheStats.TotalSizeBytes / 1024f / 1024f:F2} MB", _labelStyle);
            GUILayout.Label($"Hits: {cacheStats.HitCount}", _labelStyle);
            GUILayout.Label($"Misses: {cacheStats.MissCount}", _labelStyle);
            
            if (cacheStats.HitCount + cacheStats.MissCount > 0)
            {
                float hitRate = (float)cacheStats.HitCount / (cacheStats.HitCount + cacheStats.MissCount) * 100f;
                GUILayout.Label($"Hit Rate: {hitRate:F1}%", _labelStyle);
            }
            
            GUILayout.Label($"Evictions: {cacheStats.EvictionCount}", _labelStyle);
        }

        private void DrawSentisInfo()
        {
            GUILayout.Label("=== Sentis Info ===", _labelStyle);
            
            // This would show Sentis-specific info if available
            GUILayout.Label($"Backend: {_piperTTS.Configuration.Backend}", _labelStyle);
            GUILayout.Label($"Multi-threaded: {_piperTTS.Configuration.EnableMultiThreadedInference}", _labelStyle);
            
            if (_piperTTS.Configuration.EnableMultiThreadedInference)
            {
                GUILayout.Label($"Worker Threads: {_piperTTS.Configuration.WorkerThreads}", _labelStyle);
            }
        }

        private void DrawLogEntries()
        {
            GUILayout.Label("=== Recent Logs ===", _labelStyle);
            
            foreach (var entry in _logEntries)
            {
                var style = _labelStyle;
                
                switch (entry.Type)
                {
                    case LogType.Warning:
                        style = _warningStyle;
                        break;
                    case LogType.Error:
                    case LogType.Exception:
                        style = _errorStyle;
                        break;
                }
                
                var timeStr = entry.Timestamp.ToString("HH:mm:ss");
                GUILayout.Label($"[{timeStr}] {entry.Message}", style);
            }
        }

        private void HandleLog(string logString, string stackTrace, LogType type)
        {
            // Filter for uPiper-related messages
            if (!logString.Contains("Piper") && !logString.Contains("TTS") && 
                !logString.Contains("Sentis") && !logString.Contains("Phonem"))
                return;
            
            var entry = new LogEntry
            {
                Message = logString,
                Type = type,
                Timestamp = DateTime.Now
            };
            
            _logEntries.Enqueue(entry);
            
            while (_logEntries.Count > MaxLogEntries)
                _logEntries.Dequeue();
        }

        private void OnProcessingProgress(float progress)
        {
            // Could show progress bar in debug view
        }

        private void OnError(PiperException error)
        {
            HandleLog($"PiperTTS Error: {error.Message}", error.StackTrace, LogType.Error);
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= HandleLog;
            
            if (_piperTTS != null)
            {
                _piperTTS.OnProcessingProgress -= OnProcessingProgress;
                _piperTTS.OnError -= OnError;
            }
        }

        /// <summary>
        /// Log phoneme information
        /// </summary>
        public void LogPhonemes(string text, string[] phonemes, int[] phonemeIds)
        {
            if (!showPhonemeInfo)
                return;
            
            var sb = new StringBuilder();
            sb.AppendLine($"Phonemes for: \"{text}\"");
            
            for (int i = 0; i < phonemes.Length && i < phonemeIds.Length; i++)
            {
                sb.AppendLine($"  [{i}] {phonemes[i]} -> {phonemeIds[i]}");
            }
            
            UnityEngine.Debug.Log(sb.ToString());
        }

        /// <summary>
        /// Track audio generation
        /// </summary>
        public void TrackGeneration(float generationTime)
        {
            _totalGenerations++;
            _lastGenerationTime = generationTime;
            
            // Update running average
            _averageGenerationTime = ((_averageGenerationTime * (_totalGenerations - 1)) + generationTime) / _totalGenerations;
        }
    }
}