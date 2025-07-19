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
    /// NOTE: Worker.Execute() APIは実際のUnity.InferenceEngine APIに合わせて更新が必要
    /// </summary>
    public class InferenceAudioGenerator : IInferenceAudioGenerator
    {
        private Worker _worker;
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
                        var model = ModelLoader.Load(_modelAsset);
                        _worker = new Worker(model, BackendType.GPUCompute);
                        _isInitialized = true;

                        PiperLogger.LogDebug($"InferenceAudioGenerator initialized with model: {_modelAsset.name}");
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

            return await Task.Run(async () =>
            {
                lock (_lockObject)
                {
                    try
                    {
                        // 入力テンソルを作成
                        var inputTensor = new Tensor<int>(new TensorShape(1, phonemeIds.Length), phonemeIds);
                        var scalesTensor = new Tensor<float>(new TensorShape(3), new[] { noiseScale, lengthScale, noiseW });

                        // モデルに入力を設定して実行
                        // Unity.InferenceEngineでは、Workerの実行方法が異なる可能性がある
                        // TODO: 実際のAPIに合わせて修正が必要
                        PiperLogger.LogWarning("Worker execution API needs to be updated for Unity.InferenceEngine");
                        
                        // 仮の出力データを返す（実際のモデル実行が実装されるまで）
                        var outputTensor = new Tensor<float>(new TensorShape(1, _sampleRate * 2), new float[_sampleRate * 2]);
                        if (outputTensor == null)
                        {
                            throw new InvalidOperationException("Failed to get output from model");
                        }

                        // 出力データをコピー
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
            }, cancellationToken);
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