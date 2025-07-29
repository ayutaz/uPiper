#if UNITY_EDITOR
using System;
using System.Threading.Tasks;
using Unity.InferenceEngine;
using UnityEditor;
using UnityEngine;
using uPiper.Core;
using uPiper.Core.AudioGeneration;
using uPiper.Core.Logging;

namespace uPiper.Editor
{
    /// <summary>
    /// GPU推論機能のテストツール
    /// </summary>
    public class GPUInferenceTest : EditorWindow
    {
        private PiperConfig _config;
        private InferenceBackend _selectedBackend = InferenceBackend.Auto;
        private bool _allowFallback = true;
        private string _testResult = "";
        private bool _isTesting = false;
        private ModelAsset _testModel;

        [MenuItem("uPiper/Tools/GPU Inference Test")]
        public static void ShowWindow()
        {
            GetWindow<GPUInferenceTest>("GPU Inference Test");
        }

        private void OnEnable()
        {
            _config = PiperConfig.CreateDefault();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("GPU Inference Test Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Backend selection
            _selectedBackend = (InferenceBackend)EditorGUILayout.EnumPopup("Inference Backend", _selectedBackend);
            _allowFallback = EditorGUILayout.Toggle("Allow CPU Fallback", _allowFallback);

            EditorGUILayout.Space();

            // GPU Settings
            EditorGUILayout.LabelField("GPU Settings", EditorStyles.boldLabel);
            _config.GPUSettings.MaxBatchSize = EditorGUILayout.IntSlider("Max Batch Size", _config.GPUSettings.MaxBatchSize, 1, 16);
            _config.GPUSettings.UseFloat16 = EditorGUILayout.Toggle("Use Float16", _config.GPUSettings.UseFloat16);
            _config.GPUSettings.MaxMemoryMB = EditorGUILayout.IntSlider("Max Memory (MB)", _config.GPUSettings.MaxMemoryMB, 128, 2048);

            EditorGUILayout.Space();

            // Model selection
            _testModel = (ModelAsset)EditorGUILayout.ObjectField("Test Model", _testModel, typeof(ModelAsset), false);

            EditorGUILayout.Space();

            // Test buttons
            GUI.enabled = !_isTesting && _testModel != null;

            if (GUILayout.Button("Test Selected Backend", GUILayout.Height(30)))
            {
                RunBackendTest(_selectedBackend);
            }

            if (GUILayout.Button("Test All Backends", GUILayout.Height(30)))
            {
                RunAllBackendsTest();
            }

            GUI.enabled = true;

            // Results
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Test Results:", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(_testResult, EditorStyles.textArea, GUILayout.MinHeight(200));

            // System info
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("System Information:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Graphics Device: {SystemInfo.graphicsDeviceName}");
            EditorGUILayout.LabelField($"Graphics Device Type: {SystemInfo.graphicsDeviceType}");
            EditorGUILayout.LabelField($"Graphics Memory: {SystemInfo.graphicsMemorySize} MB");
            EditorGUILayout.LabelField($"Compute Shaders: {(SystemInfo.supportsComputeShaders ? "Supported" : "Not Supported")}");
        }

        private async void RunBackendTest(InferenceBackend backend)
        {
            _isTesting = true;
            _testResult = $"Testing {backend} backend...\n";
            Repaint();

            try
            {
                _config.Backend = backend;
                _config.AllowFallbackToCPU = _allowFallback;

                var generator = new InferenceAudioGenerator();
                var voiceConfig = new PiperVoiceConfig
                {
                    VoiceId = "test_voice",
                    Language = "ja",
                    SampleRate = 22050
                };

                var startTime = DateTime.Now;

                try
                {
                    await generator.InitializeAsync(_testModel, voiceConfig, _config);
                    var initTime = (DateTime.Now - startTime).TotalMilliseconds;

                    _testResult += $"✓ Initialization successful in {initTime:F1}ms\n";
                    _testResult += $"  Actual backend used: {GetActualBackendType(generator)}\n";

                    // Test inference
                    var testPhonemes = new int[] { 1, 2, 3, 4, 5, 0 }; // Simple test sequence
                    startTime = DateTime.Now;
                    var audio = await generator.GenerateAudioAsync(testPhonemes);
                    var inferenceTime = (DateTime.Now - startTime).TotalMilliseconds;

                    _testResult += $"✓ Inference successful in {inferenceTime:F1}ms\n";
                    _testResult += $"  Generated {audio.Length} audio samples\n";

                    generator.Dispose();
                }
                catch (Exception ex)
                {
                    _testResult += $"✗ Failed: {ex.Message}\n";
                    if (ex.InnerException != null)
                    {
                        _testResult += $"  Inner: {ex.InnerException.Message}\n";
                    }
                }
            }
            catch (Exception ex)
            {
                _testResult += $"✗ Test error: {ex.Message}\n";
            }
            finally
            {
                _isTesting = false;
                Repaint();
            }
        }

        private async void RunAllBackendsTest()
        {
            _isTesting = true;
            _testResult = "Testing all backends...\n\n";
            Repaint();

            var backends = new[] { InferenceBackend.CPU, InferenceBackend.GPUCompute, InferenceBackend.GPUPixel };

            foreach (var backend in backends)
            {
                _testResult += $"=== Testing {backend} ===\n";
                await Task.Run(() => RunBackendTest(backend));
                _testResult += "\n";
                await Task.Delay(100); // Small delay between tests
            }

            _testResult += "All tests completed.\n";
            _isTesting = false;
            Repaint();
        }

        private string GetActualBackendType(InferenceAudioGenerator generator)
        {
            return generator.ActualBackendType.ToString();
        }
    }
}
#endif