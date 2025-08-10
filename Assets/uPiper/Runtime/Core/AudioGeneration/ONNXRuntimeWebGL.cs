#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Unity.InferenceEngine;
using uPiper.Core.Logging;

namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// WebGL環境でONNX Runtime Webを使用した音声生成の実装
    /// </summary>
    public class ONNXRuntimeWebGL : IInferenceAudioGenerator
    {
        #region P/Invoke Declarations
        
        [DllImport("__Internal")]
        private static extern void ONNXRuntime_LoadWrapper(Action<int> callback);
        
        [DllImport("__Internal")]
        private static extern void ONNXRuntime_Initialize(string modelPath, string configPath, Action<int> callback);
        
        [DllImport("__Internal")]
        private static extern void ONNXRuntime_Synthesize(int[] phonemeIds, int length, Action<int, IntPtr, int> callback);
        
        [DllImport("__Internal")]
        private static extern void ONNXRuntime_FreeMemory(IntPtr ptr);
        
        [DllImport("__Internal")]
        private static extern void ONNXRuntime_Dispose();
        
        [DllImport("__Internal")]
        private static extern void ONNXRuntime_SetDebugMode(int enabled);
        
        [DllImport("__Internal")]
        private static extern int ONNXRuntime_IsInitialized();
        
        #endregion
        
        private bool _isInitialized;
        private PiperVoiceConfig _voiceConfig;
        private bool _disposed;
        private readonly object _lockObject = new object();
        
        /// <inheritdoc/>
        public bool IsInitialized => _isInitialized;
        
        /// <inheritdoc/>
        public int SampleRate => _voiceConfig?.SampleRate ?? 22050;
        
        /// <summary>
        /// Initialize with ONNX Runtime Web using PiperConfig
        /// </summary>
        public async Task InitializeAsync(string modelName, PiperVoiceConfig voiceConfig, PiperConfig piperConfig, CancellationToken cancellationToken = default)
        {
            PiperLogger.LogInfo("[ONNXRuntimeWebGL] Starting initialization...");
            
            if (_disposed)
                throw new ObjectDisposedException(nameof(ONNXRuntimeWebGL));
            
            if (string.IsNullOrEmpty(modelName))
                throw new ArgumentNullException(nameof(modelName));
            
            if (voiceConfig == null)
                throw new ArgumentNullException(nameof(voiceConfig));
            
            _voiceConfig = voiceConfig;
            
            try
            {
                // Step 1: Load the wrapper script
                PiperLogger.LogInfo("[ONNXRuntimeWebGL] Loading ONNX Runtime wrapper...");
                bool wrapperLoaded = await LoadWrapperAsync(cancellationToken);
                if (!wrapperLoaded)
                {
                    throw new InvalidOperationException("Failed to load ONNX Runtime wrapper");
                }
                
                // Step 2: Initialize ONNX Runtime with model
                string modelPath = GetStreamingAssetsPath(modelName + ".onnx");
                string configPath = GetStreamingAssetsPath(modelName + ".onnx.json");
                
                PiperLogger.LogInfo($"[ONNXRuntimeWebGL] Initializing with model: {modelPath}");
                
                bool initialized = await InitializeONNXAsync(modelPath, configPath, cancellationToken);
                if (!initialized)
                {
                    throw new InvalidOperationException("Failed to initialize ONNX Runtime");
                }
                
                _isInitialized = true;
                PiperLogger.LogInfo("[ONNXRuntimeWebGL] Initialization complete");
                
                // Enable debug mode in development
                #if DEVELOPMENT_BUILD || UNITY_EDITOR
                ONNXRuntime_SetDebugMode(1);
                #endif
            }
            catch (Exception ex)
            {
                PiperLogger.LogError($"[ONNXRuntimeWebGL] Initialization failed: {ex.Message}");
                _isInitialized = false;
                throw;
            }
        }
        
        /// <inheritdoc/>
        public async Task InitializeAsync(ModelAsset modelAsset, PiperVoiceConfig voiceConfig, CancellationToken cancellationToken = default)
        {
            // Extract model name from ModelAsset
            string modelName = modelAsset?.name ?? throw new ArgumentNullException(nameof(modelAsset));
            await InitializeAsync(modelName, voiceConfig, PiperConfig.CreateDefault(), cancellationToken);
        }
        
        /// <summary>
        /// Load the ONNX Runtime wrapper script
        /// </summary>
        private Task<bool> LoadWrapperAsync(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            
            Action<int> callback = (success) =>
            {
                PiperLogger.LogInfo($"[ONNXRuntimeWebGL] Wrapper load callback: {success}");
                tcs.TrySetResult(success != 0);
            };
            
            // Register cancellation
            cancellationToken.Register(() => tcs.TrySetCanceled());
            
            // Call JavaScript
            ONNXRuntime_LoadWrapper(callback);
            
            return tcs.Task;
        }
        
        /// <summary>
        /// Initialize ONNX Runtime with model
        /// </summary>
        private Task<bool> InitializeONNXAsync(string modelPath, string configPath, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            
            Action<int> callback = (success) =>
            {
                PiperLogger.LogInfo($"[ONNXRuntimeWebGL] Initialize callback: {success}");
                tcs.TrySetResult(success != 0);
            };
            
            // Register cancellation
            cancellationToken.Register(() => tcs.TrySetCanceled());
            
            // Call JavaScript
            ONNXRuntime_Initialize(modelPath, configPath, callback);
            
            return tcs.Task;
        }
        
        /// <inheritdoc/>
        public async Task<float[]> GenerateAudioAsync(
            int[] phonemeIds,
            float lengthScale = 1.0f,
            float noiseScale = 0.667f,
            float noiseW = 0.8f,
            CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ONNXRuntimeWebGL));
            
            if (!_isInitialized)
                throw new InvalidOperationException("Generator is not initialized. Call InitializeAsync first.");
            
            if (phonemeIds == null || phonemeIds.Length == 0)
                throw new ArgumentException("Phoneme IDs cannot be null or empty.", nameof(phonemeIds));
            
            PiperLogger.LogInfo($"[ONNXRuntimeWebGL] Generating audio for {phonemeIds.Length} phoneme IDs");
            PiperLogger.LogInfo($"[ONNXRuntimeWebGL] Phoneme IDs: {string.Join(", ", phonemeIds)}");
            
            // Note: lengthScale, noiseScale, noiseW are currently hardcoded in JavaScript
            // TODO: Pass these parameters to JavaScript for dynamic control
            
            var tcs = new TaskCompletionSource<float[]>();
            
            Action<int, IntPtr, int> callback = (success, dataPtr, dataLength) =>
            {
                try
                {
                    if (success != 0 && dataPtr != IntPtr.Zero && dataLength > 0)
                    {
                        PiperLogger.LogInfo($"[ONNXRuntimeWebGL] Synthesis successful, received {dataLength} samples");
                        
                        // Copy data from JavaScript memory
                        float[] audioData = new float[dataLength];
                        Marshal.Copy(dataPtr, audioData, 0, dataLength);
                        
                        // Free JavaScript memory
                        ONNXRuntime_FreeMemory(dataPtr);
                        
                        // Log audio statistics
                        LogAudioStats(audioData);
                        
                        tcs.TrySetResult(audioData);
                    }
                    else
                    {
                        PiperLogger.LogError("[ONNXRuntimeWebGL] Synthesis failed");
                        tcs.TrySetException(new InvalidOperationException("Audio synthesis failed"));
                    }
                }
                catch (Exception ex)
                {
                    PiperLogger.LogError($"[ONNXRuntimeWebGL] Callback error: {ex.Message}");
                    tcs.TrySetException(ex);
                }
            };
            
            // Register cancellation
            cancellationToken.Register(() => tcs.TrySetCanceled());
            
            // Call JavaScript
            ONNXRuntime_Synthesize(phonemeIds, phonemeIds.Length, callback);
            
            return await tcs.Task;
        }
        
        /// <summary>
        /// Log audio statistics for debugging
        /// </summary>
        private void LogAudioStats(float[] audioData)
        {
            if (audioData == null || audioData.Length == 0) return;
            
            float min = float.MaxValue;
            float max = float.MinValue;
            float sum = 0;
            float absSum = 0;
            
            foreach (float sample in audioData)
            {
                min = Math.Min(min, sample);
                max = Math.Max(max, sample);
                sum += sample;
                absSum += Math.Abs(sample);
            }
            
            float avg = sum / audioData.Length;
            float absAvg = absSum / audioData.Length;
            
            PiperLogger.LogInfo($"[ONNXRuntimeWebGL] Audio stats - Samples: {audioData.Length}, Min: {min:F4}, Max: {max:F4}, Avg: {avg:F4}, AbsAvg: {absAvg:F4}");
            
            // Duration in seconds (assuming 22050 Hz)
            float duration = audioData.Length / 22050.0f;
            PiperLogger.LogInfo($"[ONNXRuntimeWebGL] Audio duration: {duration:F2} seconds");
        }
        
        /// <summary>
        /// Get StreamingAssets path for WebGL
        /// </summary>
        private string GetStreamingAssetsPath(string fileName)
        {
            // In WebGL, StreamingAssets is relative to the build root
            return $"StreamingAssets/{fileName}";
        }
        
        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    lock (_lockObject)
                    {
                        if (_isInitialized)
                        {
                            PiperLogger.LogInfo("[ONNXRuntimeWebGL] Disposing resources...");
                            ONNXRuntime_Dispose();
                            _isInitialized = false;
                        }
                        
                        _voiceConfig = null;
                    }
                }
                _disposed = true;
            }
        }
    }
}
#endif