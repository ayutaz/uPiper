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
        private PiperVoiceConfig _config;
        private bool _isInitialized;
        private readonly object _lockObject = new object();
        private bool _disposed;

        /// <inheritdoc/>
        public bool IsInitialized => _isInitialized;

        /// <inheritdoc/>
        public int SampleRate => _config?.SampleRate ?? 22050;

        /// <inheritdoc/>
        public async Task InitializeAsync(ModelAsset modelAsset, PiperVoiceConfig config, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(InferenceAudioGenerator));

            if (modelAsset == null)
                throw new ArgumentNullException(nameof(modelAsset));

            if (config == null)
                throw new ArgumentNullException(nameof(config));

            // Unity APIはメインスレッドからのみ呼び出し可能
            await UnityMainThreadDispatcher.RunOnMainThreadAsync(() =>
            {
                lock (_lockObject)
                {
                    // 既存のワーカーがあれば破棄
                    DisposeWorker();

                    _modelAsset = modelAsset;
                    _config = config;

                    try
                    {
                        // Unity.InferenceEngineワーカーを作成
                        _model = ModelLoader.Load(_modelAsset);
                        _worker = new Worker(_model, BackendType.GPUCompute);
                        _isInitialized = true;

                        // モデルの入力/出力情報をログ出力（デバッグ用）
                        PiperLogger.LogDebug($"InferenceAudioGenerator initialized with model: {_modelAsset.name}");
                        PiperLogger.LogDebug($"Model inputs: {_model.inputs.Count}");
                        for (int i = 0; i < _model.inputs.Count; i++)
                        {
                            var input = _model.inputs[i];
                            PiperLogger.LogDebug($"  Input[{i}]: name='{input.name}', shape={string.Join("x", input.shape)}");
                        }
                        PiperLogger.LogDebug($"Model outputs: {_model.outputs.Count}");
                        for (int i = 0; i < _model.outputs.Count; i++)
                        {
                            var output = _model.outputs[i];
                            PiperLogger.LogDebug($"  Output[{i}]: name='{output.name}'");
                        }
                    }
                    catch (Exception ex)
                    {
                        PiperLogger.LogError($"Failed to initialize InferenceAudioGenerator: {ex.Message}");
                        throw;
                    }
                }
            }, cancellationToken);
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
                        // 入力テンソルを作成
                        var inputTensor = new Tensor<int>(new TensorShape(1, phonemeIds.Length), phonemeIds);
                        var scalesTensor = new Tensor<float>(new TensorShape(3), new[] { noiseScale, lengthScale, noiseW });

                        // モデルに入力を設定
                        // Piper TTSモデルの入力名は様々なので、複数のパターンを試す
                        try
                        {
                            // パターン1: "input" と "scales"
                            _worker.SetInput("input", inputTensor);
                            _worker.SetInput("scales", scalesTensor);
                        }
                        catch
                        {
                            try
                            {
                                // パターン2: "input_ids" と "scales"
                                _worker.SetInput("input_ids", inputTensor);
                                _worker.SetInput("scales", scalesTensor);
                            }
                            catch
                            {
                                // パターン3: インデックスで設定
                                _worker.SetInput(0, inputTensor);
                                if (_model.inputs.Count > 1)
                                {
                                    _worker.SetInput(1, scalesTensor);
                                }
                            }
                        }
                        
                        // 推論を実行
                        _worker.Schedule();
                        
                        // 出力を取得（Piper TTSモデルの出力は通常 "output" または最初の出力）
                        var outputTensor = _worker.PeekOutput() as Tensor<float>;
                        if (outputTensor == null)
                        {
                            throw new InvalidOperationException("Failed to get output from model");
                        }

                        // 出力データをコピー
                        // Schedule()は同期的に実行されるため、追加の待機は不要
                        var audioLength = outputTensor.shape[0] * outputTensor.shape[1];
                        var audioData = new float[audioLength];
                        for (int i = 0; i < audioLength; i++)
                        {
                            audioData[i] = outputTensor[i];
                        }

                        // テンソルを破棄
                        inputTensor.Dispose();
                        scalesTensor.Dispose();
                        outputTensor.Dispose();

                        PiperLogger.LogDebug($"Generated audio: {audioData.Length} samples");
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
                        _config = null;
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
    }
}