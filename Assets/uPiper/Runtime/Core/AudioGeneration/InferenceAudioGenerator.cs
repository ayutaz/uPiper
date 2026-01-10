using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.InferenceEngine;
using UnityEngine;
using uPiper.Core.Logging;

namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// Unity.InferenceEngineを使用した音声生成の実装
    /// </summary>
    public class InferenceAudioGenerator : IInferenceAudioGenerator
    {
        private Worker _worker;
        private Model _model;
        private ModelAsset _modelAsset;
        private PiperVoiceConfig _voiceConfig;
        private PiperConfig _piperConfig;
        private bool _isInitialized;
        private readonly object _lockObject = new();
        private bool _disposed;
        private BackendType _actualBackendType;
        private bool _supportsProsody;

        /// <inheritdoc/>
        public bool IsInitialized => _isInitialized;

        /// <inheritdoc/>
        public int SampleRate => _voiceConfig?.SampleRate ?? 22050;

        /// <summary>
        /// Get the actual backend type being used
        /// </summary>
        public BackendType ActualBackendType => _actualBackendType;

        /// <inheritdoc/>
        public bool SupportsProsody => _supportsProsody;

        /// <inheritdoc/>
        public async Task InitializeAsync(ModelAsset modelAsset, PiperVoiceConfig config, CancellationToken cancellationToken = default)
        {
            // Use default PiperConfig for backward compatibility
            await InitializeAsync(modelAsset, config, PiperConfig.CreateDefault(), cancellationToken);
        }

        /// <summary>
        /// Initialize with PiperConfig for backend selection
        /// </summary>
        public async Task InitializeAsync(ModelAsset modelAsset, PiperVoiceConfig voiceConfig, PiperConfig piperConfig, CancellationToken cancellationToken = default)
        {
            PiperLogger.LogDebug("[InferenceAudioGenerator] InitializeAsync started");

            if (_disposed)
                throw new ObjectDisposedException(nameof(InferenceAudioGenerator));

            if (modelAsset == null)
                throw new ArgumentNullException(nameof(modelAsset));

            if (voiceConfig == null)
                throw new ArgumentNullException(nameof(voiceConfig));

            if (piperConfig == null)
                throw new ArgumentNullException(nameof(piperConfig));

            PiperLogger.LogDebug("[InferenceAudioGenerator] Dispatching to main thread...");

            // Unity APIはメインスレッドからのみ呼び出し可能
            await UnityMainThreadDispatcher.RunOnMainThreadAsync(() =>
            {
                PiperLogger.LogDebug("[InferenceAudioGenerator] Now on main thread");

                lock (_lockObject)
                {
                    // 既存のワーカーがあれば破棄
                    DisposeWorker();

                    _modelAsset = modelAsset;
                    _voiceConfig = voiceConfig;
                    _piperConfig = piperConfig ?? PiperConfig.CreateDefault();

                    try
                    {
                        PiperLogger.LogDebug($"[InferenceAudioGenerator] Loading model: {_modelAsset.name}");

                        // Unity.InferenceEngineワーカーを作成
                        _model = ModelLoader.Load(_modelAsset);

                        if (_model == null)
                        {
                            throw new InvalidOperationException("ModelLoader.Load returned null");
                        }

                        PiperLogger.LogDebug("[InferenceAudioGenerator] Model loaded, creating worker...");

                        // Select backend based on configuration
                        _actualBackendType = DetermineBackendType(_piperConfig);

                        try
                        {
                            _worker = new Worker(_model, _actualBackendType);
                            _isInitialized = true;
                            PiperLogger.LogInfo($"[InferenceAudioGenerator] Successfully initialized with backend: {_actualBackendType}");
                        }
                        catch (Exception gpuEx)
                        {
                            PiperLogger.LogWarning($"[InferenceAudioGenerator] Failed to initialize with {_actualBackendType}: {gpuEx.Message}");

                            if (_piperConfig.AllowFallbackToCPU && _actualBackendType != BackendType.CPU)
                            {
                                PiperLogger.LogInfo("[InferenceAudioGenerator] Falling back to CPU backend...");
                                _actualBackendType = BackendType.CPU;
                                _worker = new Worker(_model, BackendType.CPU);
                                _isInitialized = true;
                                PiperLogger.LogInfo("[InferenceAudioGenerator] Successfully initialized with CPU backend (fallback)");
                            }
                            else
                            {
                                throw;
                            }
                        }

                        // モデルの入力/出力情報をログ出力（デバッグ用）
                        PiperLogger.LogInfo($"[InferenceAudioGenerator] Successfully initialized with model: {_modelAsset.name}");
                        PiperLogger.LogInfo($"[InferenceAudioGenerator] Model inputs: {_model.inputs.Count}");
                        for (var i = 0; i < _model.inputs.Count; i++)
                        {
                            var input = _model.inputs[i];
                            PiperLogger.LogInfo($"  Input[{i}]: name='{input.name}', shape={string.Join("x", input.shape)}, dataType={input.dataType}");
                        }
                        PiperLogger.LogInfo($"[InferenceAudioGenerator] Model outputs: {_model.outputs.Count}");
                        for (var i = 0; i < _model.outputs.Count; i++)
                        {
                            var output = _model.outputs[i];
                            PiperLogger.LogInfo($"  Output[{i}]: name='{output.name}'");
                        }

                        // Check if model supports prosody_features input
                        _supportsProsody = _model.inputs.Any(input => input.name == "prosody_features");
                        PiperLogger.LogInfo($"[InferenceAudioGenerator] Model prosody support: {_supportsProsody}");

                        // Validate prosody_features input type if model supports it
                        if (_supportsProsody)
                        {
                            foreach (var input in _model.inputs)
                            {
                                if (input.name == "prosody_features" && input.dataType != DataType.Float)
                                {
                                    PiperLogger.LogError($"[InferenceAudioGenerator] prosody_features expects {input.dataType}, " +
                                        "but implementation uses Float. This is a code/model mismatch!");
                                    throw new InvalidOperationException(
                                        $"Model prosody_features expects {input.dataType}, but implementation uses Float. " +
                                        "Please verify the model export settings or update the code to match.");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        PiperLogger.LogError($"[InferenceAudioGenerator] Failed to initialize: {ex.Message}\n{ex.StackTrace}");
                        throw;
                    }
                }
            }, cancellationToken);

            PiperLogger.LogDebug("[InferenceAudioGenerator] InitializeAsync completed");
        }

        /// <inheritdoc/>
        public async Task<float[]> GenerateAudioAsync(
            int[] phonemeIds,
            float lengthScale = 1.0f,
            float noiseScale = 0.667f,
            float noiseW = 0.8f,
            CancellationToken cancellationToken = default)
        {
            ValidateGenerationPrerequisites(phonemeIds);

            // Unity.InferenceEngineの操作はメインスレッドで実行する必要がある
            return await UnityMainThreadDispatcher.RunOnMainThreadAsync(() =>
            {
                lock (_lockObject)
                {
                    // デフォルトのprosody配列（すべてゼロ）を使用
                    return ExecuteInference(phonemeIds, null, null, null, lengthScale, noiseScale, noiseW);
                }
            });
        }

        /// <inheritdoc/>
        public async Task<float[]> GenerateAudioWithProsodyAsync(
            int[] phonemeIds,
            int[] prosodyA1,
            int[] prosodyA2,
            int[] prosodyA3,
            float lengthScale = 1.0f,
            float noiseScale = 0.667f,
            float noiseW = 0.8f,
            CancellationToken cancellationToken = default)
        {
            ValidateGenerationPrerequisites(phonemeIds);

            // Unity.InferenceEngineの操作はメインスレッドで実行する必要がある
            return await UnityMainThreadDispatcher.RunOnMainThreadAsync(() =>
            {
                lock (_lockObject)
                {
                    return ExecuteInference(phonemeIds, prosodyA1, prosodyA2, prosodyA3, lengthScale, noiseScale, noiseW);
                }
            });
        }

        /// <summary>
        /// Validate common prerequisites for audio generation
        /// </summary>
        private void ValidateGenerationPrerequisites(int[] phonemeIds)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(InferenceAudioGenerator));

            if (!_isInitialized)
                throw new InvalidOperationException("Generator is not initialized. Call InitializeAsync first.");

            if (phonemeIds == null || phonemeIds.Length == 0)
                throw new ArgumentException("Phoneme IDs cannot be null or empty.", nameof(phonemeIds));

            // Prosody非対応モデルはサポートしない
            if (!_supportsProsody)
                throw new InvalidOperationException("This model does not support prosody features. Only prosody-enabled models are supported.");
        }

        /// <summary>
        /// Execute inference with the given inputs (common logic for both methods)
        /// </summary>
        private float[] ExecuteInference(
            int[] phonemeIds,
            int[] prosodyA1,
            int[] prosodyA2,
            int[] prosodyA3,
            float lengthScale,
            float noiseScale,
            float noiseW)
        {
            var hasAnyProsodyInput = prosodyA1 != null || prosodyA2 != null || prosodyA3 != null;
            PiperLogger.LogInfo($"[InferenceAudioGenerator] Preparing model inputs{(hasAnyProsodyInput ? " with prosody" : "")}...");
            PiperLogger.LogInfo($"  Phoneme IDs length: {phonemeIds.Length}");

            // 入力テンソルを作成
            var inputTensor = new Tensor<int>(new TensorShape(1, phonemeIds.Length), phonemeIds);
            var inputLengthsTensor = new Tensor<int>(new TensorShape(1), new[] { phonemeIds.Length });
            var scalesTensor = new Tensor<float>(new TensorShape(3), new[] { noiseScale, lengthScale, noiseW });
            Tensor<float> prosodyTensor = null;

            try
            {
                // 基本の3入力を設定
                if (_model.inputs.Count < 3)
                {
                    throw new InvalidOperationException($"Model has {_model.inputs.Count} inputs, but Piper models require at least 3 inputs");
                }

                var inputName = _model.inputs[0].name;
                _worker.SetInput(inputName, inputTensor);

                var lengthsName = _model.inputs[1].name;
                _worker.SetInput(lengthsName, inputLengthsTensor);

                var scalesName = _model.inputs[2].name;
                _worker.SetInput(scalesName, scalesTensor);

                // prosody_featuresテンソルを設定（モデルがサポートする場合）
                if (_supportsProsody)
                {
                    prosodyTensor = CreateProsodyTensor(phonemeIds.Length, prosodyA1, prosodyA2, prosodyA3);
                    _worker.SetInput("prosody_features", prosodyTensor);
                    PiperLogger.LogInfo($"[InferenceAudioGenerator] Set prosody_features tensor with shape (1, {phonemeIds.Length}, 3)");
                }

                // 推論を実行
                PiperLogger.LogInfo($"[InferenceAudioGenerator] Running inference with backend: {_actualBackendType}...");
                _worker.Schedule();
                PiperLogger.LogInfo("[InferenceAudioGenerator] Inference completed");

                // 出力を取得
                var outputTensor = GetOutputTensor();

                // GPUからCPUにデータを読み戻す
                var readableTensor = outputTensor.ReadbackAndClone();
                var audioData = ExtractAudioData(readableTensor);

                PiperLogger.LogInfo($"[InferenceAudioGenerator] Generated {audioData.Length} samples{(hasAnyProsodyInput ? " with prosody" : "")}");

                // テンソルを破棄
                readableTensor.Dispose();
                outputTensor.Dispose();

                return audioData;
            }
            catch (Exception ex)
            {
                PiperLogger.LogError($"[InferenceAudioGenerator] Failed to set model inputs: {ex.Message}");
                throw;
            }
            finally
            {
                // テンソルをクリーンアップ
                inputTensor?.Dispose();
                inputLengthsTensor?.Dispose();
                scalesTensor?.Dispose();
                prosodyTensor?.Dispose();
            }
        }

        /// <summary>
        /// Create prosody tensor from A1/A2/A3 arrays
        /// </summary>
        private Tensor<float> CreateProsodyTensor(int sequenceLength, int[] prosodyA1, int[] prosodyA2, int[] prosodyA3)
        {
            // Shape: (1, sequence_length, 3)
            // Note: ONNX model expects Float, not Int (despite Python using int64)
            var prosodyData = new float[sequenceLength * 3];
            for (var i = 0; i < sequenceLength; i++)
            {
                prosodyData[i * 3 + 0] = prosodyA1 != null && i < prosodyA1.Length ? prosodyA1[i] : 0f;
                prosodyData[i * 3 + 1] = prosodyA2 != null && i < prosodyA2.Length ? prosodyA2[i] : 0f;
                prosodyData[i * 3 + 2] = prosodyA3 != null && i < prosodyA3.Length ? prosodyA3[i] : 0f;
            }

            return new Tensor<float>(new TensorShape(1, sequenceLength, 3), prosodyData);
        }

        /// <summary>
        /// Get output tensor from worker
        /// </summary>
        private Tensor<float> GetOutputTensor()
        {
            Tensor<float> outputTensor = null;

            if (_model.outputs.Count > 0)
            {
                var outputName = _model.outputs[0].name;
                try
                {
                    outputTensor = _worker.PeekOutput(outputName) as Tensor<float>;
                }
                catch
                {
                    outputTensor = _worker.PeekOutput() as Tensor<float>;
                }
            }
            else
            {
                outputTensor = _worker.PeekOutput() as Tensor<float>;
            }

            if (outputTensor == null)
            {
                throw new InvalidOperationException("Failed to get output from model");
            }

            return outputTensor;
        }

        /// <summary>
        /// Extract audio data from tensor
        /// </summary>
        private float[] ExtractAudioData(Tensor<float> tensor)
        {
            var audioLength = tensor.shape.length;
            var audioData = new float[audioLength];

            for (var i = 0; i < audioLength; i++)
            {
                audioData[i] = tensor[i];
            }

            return audioData;
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
                        DisposeWorker();
                        _model = null;
                        _modelAsset = null;
                        _voiceConfig = null;
                        _piperConfig = null;
                    }
                }
                _disposed = true;
            }
        }

        private void DisposeWorker()
        {
            if (_worker != null)
            {
                _worker.Dispose();
                _worker = null;
                _isInitialized = false;
                PiperLogger.LogDebug("InferenceAudioGenerator worker disposed");
            }
        }

        /// <summary>
        /// Determine the best backend type based on configuration and platform
        /// </summary>
        private BackendType DetermineBackendType(PiperConfig config)
        {
            // Check for Metal first - it has known issues with GPU backends
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Metal)
            {
                if (config.Backend == InferenceBackend.GPUCompute || config.Backend == InferenceBackend.GPUPixel)
                {
                    PiperLogger.LogWarning($"[InferenceAudioGenerator] {config.Backend} requested on Metal, but Metal has known issues with GPU inference. Using CPU backend instead.");
                    PiperLogger.LogWarning("[InferenceAudioGenerator] This is a known issue with Unity.InferenceEngine on macOS. GPU inference may produce corrupted audio.");
                    return BackendType.CPU;
                }
            }

            // GPU Compute has known issues with VITS models producing silent/corrupted audio
            // Force GPU Pixel or CPU for better compatibility
            if (config.Backend == InferenceBackend.GPUCompute)
            {
                PiperLogger.LogWarning("[InferenceAudioGenerator] GPU Compute backend has known issues with VITS audio models.");
                PiperLogger.LogWarning("[InferenceAudioGenerator] Switching to GPU Pixel backend for better compatibility.");
                PiperLogger.LogWarning("[InferenceAudioGenerator] If issues persist, please use CPU backend explicitly.");
                return BackendType.GPUPixel;
            }

            if (config.Backend == InferenceBackend.CPU)
            {
                return BackendType.CPU;
            }

            if (config.Backend == InferenceBackend.GPUCompute)
            {
                return BackendType.GPUCompute;
            }

            if (config.Backend == InferenceBackend.GPUPixel)
            {
                return BackendType.GPUPixel;
            }

            // Auto selection based on platform
            if (config.Backend == InferenceBackend.Auto)
            {
#if UNITY_WEBGL
                // WebGL typically works better with GPUPixel
                PiperLogger.LogInfo("[InferenceAudioGenerator] Auto-selecting GPUPixel backend for WebGL");
                return BackendType.GPUPixel;
#elif UNITY_IOS || UNITY_ANDROID
                // Mobile platforms often have GPU support but may have compatibility issues
                if (SystemInfo.supportsComputeShaders)
                {
                    PiperLogger.LogInfo("[InferenceAudioGenerator] Auto-selecting GPUCompute backend for mobile");
                    return BackendType.GPUCompute;
                }
                else
                {
                    PiperLogger.LogInfo("[InferenceAudioGenerator] Auto-selecting CPU backend for mobile (no compute shader support)");
                    return BackendType.CPU;
                }
#else
                // Desktop platforms
                if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Metal)
                {
                    // Metal currently has issues with shader compilation
                    PiperLogger.LogWarning("[InferenceAudioGenerator] Metal detected - using CPU backend due to known shader compilation issues");
                    return BackendType.CPU;
                }
                else if (SystemInfo.supportsComputeShaders && SystemInfo.graphicsMemorySize >= config.GPUSettings.MaxMemoryMB)
                {
                    // GPU Pixel is more stable than GPU Compute for VITS models
                    PiperLogger.LogInfo("[InferenceAudioGenerator] Auto-selecting GPUPixel backend for desktop (better VITS compatibility)");
                    return BackendType.GPUPixel;
                }
                else
                {
                    PiperLogger.LogInfo("[InferenceAudioGenerator] Auto-selecting CPU backend for desktop");
                    return BackendType.CPU;
                }
#endif
            }

            // Default to CPU if unknown
            return BackendType.CPU;
        }
    }
}