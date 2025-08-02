using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        /// <inheritdoc/>
        public bool IsInitialized => _isInitialized;

        /// <inheritdoc/>
        public int SampleRate => _voiceConfig?.SampleRate ?? 22050;

        /// <summary>
        /// Get the actual backend type being used
        /// </summary>
        public BackendType ActualBackendType => _actualBackendType;

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
            if (_disposed)
                throw new ObjectDisposedException(nameof(InferenceAudioGenerator));

            if (!_isInitialized)
                throw new InvalidOperationException("Generator is not initialized. Call InitializeAsync first.");

            if (phonemeIds == null || phonemeIds.Length == 0)
                throw new ArgumentException("Phoneme IDs cannot be null or empty.", nameof(phonemeIds));

            // Unity.InferenceEngineの操作はメインスレッドで実行する必要がある
            return await UnityMainThreadDispatcher.RunOnMainThreadAsync(() =>
            {
                lock (_lockObject)
                {
                    try
                    {
                        // Piperモデルは3つの入力を必要とする:
                        // 1. input: 音素ID (shape: [batch_size, sequence_length])
                        // 2. input_lengths: 入力の長さ (shape: [batch_size])
                        // 3. scales: ノイズとレングススケール (shape: [3])

                        PiperLogger.LogInfo($"[InferenceAudioGenerator] Preparing model inputs...");
                        PiperLogger.LogInfo($"  Phoneme IDs: {string.Join(", ", phonemeIds.Take(Math.Min(10, phonemeIds.Length)))}... (length: {phonemeIds.Length})");
                        PiperLogger.LogInfo($"  Length scale: {lengthScale}, Noise scale: {noiseScale}, Noise W: {noiseW}");

                        // 入力テンソルを作成
                        var inputTensor = new Tensor<int>(new TensorShape(1, phonemeIds.Length), phonemeIds);
                        var inputLengthsTensor = new Tensor<int>(new TensorShape(1), new[] { phonemeIds.Length });
                        var scalesTensor = new Tensor<float>(new TensorShape(3), new[] { noiseScale, lengthScale, noiseW });

                        // デバッグ: 入力データの詳細を表示
                        PiperLogger.LogInfo($"[InferenceAudioGenerator] Input tensor shape: {inputTensor.shape}");
                        PiperLogger.LogInfo($"[InferenceAudioGenerator] Input data: [{string.Join(", ", phonemeIds)}]");
                        PiperLogger.LogInfo($"[InferenceAudioGenerator] Input lengths tensor: {phonemeIds.Length}");
                        PiperLogger.LogInfo($"[InferenceAudioGenerator] Scales: noise={noiseScale}, length={lengthScale}, noise_w={noiseW}");

                        try
                        {
                            // モデルの入力名を確認
                            PiperLogger.LogInfo($"[InferenceAudioGenerator] Model expects {_model.inputs.Count} inputs:");
                            for (var i = 0; i < _model.inputs.Count; i++)
                            {
                                var modelInput = _model.inputs[i];
                                PiperLogger.LogInfo($"  Input[{i}]: name='{modelInput.name}', shape=({string.Join(", ", modelInput.shape)}), dataType={modelInput.dataType}");
                            }

                            // 各入力を名前で設定
                            if (_model.inputs.Count >= 3)
                            {
                                // input (音素ID)
                                var inputName = _model.inputs[0].name;
                                _worker.SetInput(inputName, inputTensor);
                                PiperLogger.LogInfo($"[InferenceAudioGenerator] Set '{inputName}' with phoneme IDs tensor");

                                // input_lengths (入力長)
                                var lengthsName = _model.inputs[1].name;
                                _worker.SetInput(lengthsName, inputLengthsTensor);
                                PiperLogger.LogInfo($"[InferenceAudioGenerator] Set '{lengthsName}' with length tensor");

                                // scales (スケールパラメータ)
                                var scalesName = _model.inputs[2].name;
                                _worker.SetInput(scalesName, scalesTensor);
                                PiperLogger.LogInfo($"[InferenceAudioGenerator] Set '{scalesName}' with scales tensor");
                            }
                            else
                            {
                                throw new InvalidOperationException($"Model has {_model.inputs.Count} inputs, but Piper models require 3 inputs");
                            }
                        }
                        catch (Exception ex)
                        {
                            PiperLogger.LogError($"[InferenceAudioGenerator] Failed to set model inputs: {ex.Message}");
                            // テンソルをクリーンアップ
                            inputTensor?.Dispose();
                            inputLengthsTensor?.Dispose();
                            scalesTensor?.Dispose();
                            throw;
                        }

                        // 推論を実行
                        PiperLogger.LogInfo($"[InferenceAudioGenerator] Running inference with backend: {_actualBackendType}...");
                        _worker.Schedule();

                        // GPU Computeバックエンドの場合は特別な処理が必要な場合がある
                        if (_actualBackendType == BackendType.GPUCompute)
                        {
                            PiperLogger.LogInfo("[InferenceAudioGenerator] GPU Compute backend - applying special handling");
                            // Note: Unity.InferenceEngine handles synchronization internally
                            // The output retrieval will block until computation is complete
                        }

                        PiperLogger.LogInfo("[InferenceAudioGenerator] Inference completed");

                        // 出力を取得
                        Tensor<float> outputTensor = null;

                        // 出力名を確認
                        if (_model.outputs.Count > 0)
                        {
                            var outputName = _model.outputs[0].name;
                            PiperLogger.LogInfo($"[InferenceAudioGenerator] Getting output with name: '{outputName}'");

                            try
                            {
                                outputTensor = _worker.PeekOutput(outputName) as Tensor<float>;
                            }
                            catch
                            {
                                // 名前で失敗した場合は、デフォルトの方法を試す
                                PiperLogger.LogInfo("[InferenceAudioGenerator] Failed to get output by name, trying default method");
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

                        // GPUからCPUにデータを読み戻すためにReadbackAndClone()を使用
                        PiperLogger.LogInfo("[InferenceAudioGenerator] Reading back tensor data from GPU...");
                        var readableTensor = outputTensor.ReadbackAndClone();

                        // 出力データをコピー
                        var shape = readableTensor.shape;
                        PiperLogger.LogInfo($"[InferenceAudioGenerator] Output shape: {string.Join("x", shape.ToArray())}");
                        PiperLogger.LogInfo($"[InferenceAudioGenerator] Shape dimensions: {shape.rank}D, total elements: {shape.length}");

                        // Piperモデルの出力は通常1次元の音声データ
                        // ただし、バッチ次元などが含まれている場合があるため、フラット化する
                        var audioLength = shape.length;
                        var audioData = new float[audioLength];

                        // テンソルデータをコピー
                        for (var i = 0; i < audioLength; i++)
                        {
                            audioData[i] = readableTensor[i];
                        }

                        PiperLogger.LogInfo($"[InferenceAudioGenerator] Copied {audioData.Length} samples");

                        // デバッグ用：最初の10サンプルの値を表示
                        var sampleDebug = string.Join(", ", audioData.Take(10).Select(x => x.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)));
                        PiperLogger.LogDebug($"[InferenceAudioGenerator] First 10 samples: {sampleDebug}");

                        // 音声データの統計情報を表示
                        var min = audioData.Min();
                        var max = audioData.Max();
                        var avg = audioData.Average();
                        var absAvg = audioData.Select(Math.Abs).Average();
                        PiperLogger.LogInfo($"[InferenceAudioGenerator] Audio stats - Min: {min:F4}, Max: {max:F4}, Avg: {avg:F4}, AbsAvg: {absAvg:F4}");

                        // GPU Computeバックエンドでのデバッグ情報
                        if (_actualBackendType == BackendType.GPUCompute)
                        {
                            PiperLogger.LogInfo($"[InferenceAudioGenerator] GPU Compute backend debug info:");
                            PiperLogger.LogInfo($"  - Graphics device: {SystemInfo.graphicsDeviceType}");
                            PiperLogger.LogInfo($"  - Compute shader support: {SystemInfo.supportsComputeShaders}");
                            PiperLogger.LogInfo($"  - Graphics memory: {SystemInfo.graphicsMemorySize} MB");

                            // データの妥当性を確認
                            if (absAvg < 0.001f)
                            {
                                PiperLogger.LogWarning("[InferenceAudioGenerator] WARNING: Audio output appears to be near silent - this may indicate a GPU Compute issue");
                            }
                        }

                        // 読み戻し用のテンソルを破棄
                        readableTensor.Dispose();

                        // すべてのテンソルを破棄
                        inputTensor.Dispose();
                        inputLengthsTensor.Dispose();
                        scalesTensor.Dispose();
                        outputTensor.Dispose();

                        PiperLogger.LogDebug($"Generated audio: {audioData.Length} samples");

                        // 音声の長さを秒単位で計算（22050 Hzの場合）
                        var durationSeconds = audioData.Length / 22050.0f;
                        PiperLogger.LogInfo($"[InferenceAudioGenerator] Audio duration: {durationSeconds:F2} seconds");

                        // 入力音素数との比較
                        PiperLogger.LogInfo($"[InferenceAudioGenerator] Input phoneme IDs: {phonemeIds.Length}, Output samples: {audioData.Length}");

                        return audioData;
                    }
                    catch (Exception ex)
                    {
                        PiperLogger.LogError($"Failed to generate audio: {ex.Message}");
                        throw;
                    }
                }
            });
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