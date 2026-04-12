using System;
using System.Diagnostics;
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
    internal class InferenceAudioGenerator : IInferenceAudioGenerator, IModelCapabilities
    {
        /// <summary>
        /// Length of the dummy phoneme input used during warmup.
        /// Matches piper-plus ort-session-contract.toml: phoneme_length = 100
        /// </summary>
        private const int WarmupPhonemeLength = 100;

        /// <summary>BOS token ID per ort-session-contract.toml</summary>
        private const int WarmupBosToken = 1;

        /// <summary>EOS token ID per ort-session-contract.toml</summary>
        private const int WarmupEosToken = 2;

        /// <summary>Dummy phoneme ID for warmup filler per ort-session-contract.toml</summary>
        private const int WarmupDummyPhoneme = 8;

        /// <summary>Warmup noise scale per ort-session-contract.toml</summary>
        private const float WarmupNoiseScale = 0.667f;

        /// <summary>Warmup length scale per ort-session-contract.toml</summary>
        private const float WarmupLengthScale = 1.0f;

        /// <summary>Warmup noise W per ort-session-contract.toml</summary>
        private const float WarmupNoiseW = 0.8f;

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
        private bool _supportsMultiSpeaker;
        private bool _supportsLanguageId;
        private string _cachedOutputName;
        private string _cachedDurationsOutputName;
        private bool _supportsDurations;

        /// <inheritdoc/>
        public bool IsInitialized => _isInitialized;

        /// <inheritdoc/>
        public IModelCapabilities Capabilities => this;

        /// <inheritdoc/>
        public int SampleRate => _voiceConfig?.SampleRate ?? 22050;

        /// <summary>
        /// Get the actual backend type being used
        /// </summary>
        public BackendType ActualBackendType => _actualBackendType;

        /// <inheritdoc/>
        public bool SupportsProsody => _supportsProsody;

        /// <inheritdoc/>
        public bool SupportsMultiSpeaker => _supportsMultiSpeaker;

        /// <inheritdoc/>
        public bool SupportsLanguageId => _supportsLanguageId;

        /// <summary>
        /// モデルが durations 出力テンソルをサポートするかどうか。
        /// <c>model.outputs.Count &gt;= 2</c> の場合に <c>true</c>。
        /// </summary>
        public bool SupportsDurations => _supportsDurations;

        /// <inheritdoc/>
        public async Task InitializeAsync(ModelAsset modelAsset, PiperVoiceConfig config, CancellationToken cancellationToken = default)
        {
            // Use default PiperConfig for backward compatibility
            await InitializeAsync(modelAsset, config, PiperConfig.CreateDefault(), cancellationToken);
        }

        /// <inheritdoc/>
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
                        var platformInfo = PlatformInfo.FromCurrentEnvironment();
                        _actualBackendType = BackendSelector.Determine(
                            _piperConfig.Backend,
                            platformInfo,
                            _piperConfig.GPUSettings.MaxMemoryMB);

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

                        // Cache the output tensor name for use during inference
                        if (_model.outputs == null || _model.outputs.Count == 0)
                        {
                            throw new InvalidOperationException(
                                $"Model '{_modelAsset.name}' has no outputs. Cannot cache output tensor name.");
                        }
                        _cachedOutputName = _model.outputs[0].name;

                        if (_model.outputs.Count >= 2)
                        {
                            _cachedDurationsOutputName = _model.outputs[1].name;
                            _supportsDurations = true;
                            PiperLogger.LogInfo(
                                $"[InferenceAudioGenerator] Durations output detected: '{_cachedDurationsOutputName}'");
                        }
                        else
                        {
                            _supportsDurations = false;
                            PiperLogger.LogInfo(
                                "[InferenceAudioGenerator] Model has no durations output (single output model)");
                        }

                        // Check model capability inputs
                        _supportsProsody = _model.inputs.Any(input => input.name == "prosody_features");
                        _supportsMultiSpeaker = _model.inputs.Any(input => input.name == "sid");
                        _supportsLanguageId = _model.inputs.Any(input => input.name == "lid");
                        PiperLogger.LogInfo($"[InferenceAudioGenerator] Model prosody support: {_supportsProsody}");
                        PiperLogger.LogInfo($"[InferenceAudioGenerator] Model multi-speaker support: {_supportsMultiSpeaker}");
                        PiperLogger.LogInfo($"[InferenceAudioGenerator] Model language-id support: {_supportsLanguageId}");

                        // Validate prosody_features input type if model supports it
                        if (_supportsProsody)
                        {
                            foreach (var input in _model.inputs)
                            {
                                if (input.name == "prosody_features" && input.dataType != DataType.Int)
                                {
                                    PiperLogger.LogError($"[InferenceAudioGenerator] prosody_features expects {input.dataType}, " +
                                        "but implementation uses Int. This is a code/model mismatch!");
                                    throw new InvalidOperationException(
                                        $"Model prosody_features expects {input.dataType}, but implementation uses Int. " +
                                        "Please verify the model export settings or update the code to match.");
                                }
                            }
                        }

                        // Warmup: run dummy inference to eliminate first-call JIT overhead
                        // WebGL is single-threaded; warmup would block the browser UI thread
#if !UNITY_WEBGL
                        if (_piperConfig.EnableWarmup && _piperConfig.WarmupIterations > 0)
                        {
                            ExecuteWarmup(_piperConfig.WarmupIterations);
                        }
#else
                        if (_piperConfig.EnableWarmup)
                        {
                            PiperLogger.LogWarning("[InferenceAudioGenerator] Warmup is disabled on WebGL to prevent UI freeze. Set EnableWarmup = false to suppress this warning.");
                        }
#endif
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
        public async Task<InferenceOutput> GenerateAudioAsync(
            int[] phonemeIds,
            int[] prosodyFlat = null,
            float lengthScale = 1.0f,
            float noiseScale = 0.667f,
            float noiseW = 0.8f,
            int speakerId = 0,
            int languageId = 0,
            CancellationToken cancellationToken = default)
        {
            ValidateGenerationPrerequisites(phonemeIds);

            if (prosodyFlat != null && !_supportsProsody)
            {
                PiperLogger.LogWarning(
                    "[InferenceAudioGenerator] Prosody data provided but model does not support prosody. Ignoring prosody.");
                prosodyFlat = null;
            }

            return await UnityMainThreadDispatcher.RunOnMainThreadAsync(() =>
            {
                // lock protects against Dispose() being called from a background thread
                // while inference is in progress on the main thread.
                // MainThreadDispatcher serialises callbacks, so this lock is only needed
                // for Generate-vs-Dispose coordination across threads.
                lock (_lockObject)
                {
                    return ExecuteInference(
                        phonemeIds, prosodyFlat,
                        lengthScale, noiseScale, noiseW,
                        speakerId, languageId);
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
        }

        /// <summary>
        /// Warms up the inference engine by running dummy inferences.
        /// Eliminates JIT/kernel compilation overhead on the user's first real synthesis call.
        /// </summary>
        private void ExecuteWarmup(int iterations)
        {
            PiperLogger.LogInfo($"[InferenceAudioGenerator] Starting warmup ({iterations} iterations)...");

            try
            {
                // Build dummy phoneme IDs: BOS(1) + dummy(8) x 98 + EOS(2) = 100 tokens
                var dummyPhonemeIds = new int[WarmupPhonemeLength];
                dummyPhonemeIds[0] = WarmupBosToken;
                for (var i = 1; i < WarmupPhonemeLength - 1; i++)
                {
                    dummyPhonemeIds[i] = WarmupDummyPhoneme;
                }
                dummyPhonemeIds[WarmupPhonemeLength - 1] = WarmupEosToken;

                // Build dummy prosody flat array if model supports prosody
                int[] dummyProsodyFlat = null;
                if (_supportsProsody)
                {
                    dummyProsodyFlat = new int[WarmupPhonemeLength * PhonemeEncoder.ProsodyStride]; // zero-filled
                }

                for (var i = 0; i < iterations; i++)
                {
                    PiperLogger.LogDebug($"[InferenceAudioGenerator] Warmup iteration {i + 1}/{iterations}");

                    using var ctx = PrepareInputs(
                        dummyPhonemeIds, dummyProsodyFlat,
                        WarmupLengthScale, WarmupNoiseScale, WarmupNoiseW,
                        speakerId: 0, languageId: 0);

                    RunInference();

                    PiperLogger.LogDebug($"[InferenceAudioGenerator] Warmup iteration {i + 1} completed (no readback)");
                }

                PiperLogger.LogInfo($"[InferenceAudioGenerator] Warmup completed ({iterations} iterations)");
            }
            catch (Exception ex)
            {
                // Warmup failure must never prevent the application from starting
                PiperLogger.LogWarning($"[InferenceAudioGenerator] Warmup failed (non-fatal, inference will still work): {ex.Message}");
            }
        }

        /// <summary>
        /// Execute inference with the given inputs (facade for 3-stage pipeline).
        /// </summary>
        /// <remarks>Caller owns and must Dispose the returned <see cref="InferenceOutput"/>.</remarks>
        private InferenceOutput ExecuteInference(
            int[] phonemeIds,
            int[] prosodyFlat,
            float lengthScale,
            float noiseScale,
            float noiseW,
            int speakerId = 0,
            int languageId = 0)
        {
            var sw = Stopwatch.StartNew();
            using var ctx = PrepareInputs(phonemeIds, prosodyFlat,
                lengthScale, noiseScale, noiseW, speakerId, languageId);

            InferenceOutput output = null;
            try
            {
                var prepareMs = sw.Elapsed.TotalMilliseconds;

                sw.Restart();
                RunInference();
                var scheduleMs = sw.Elapsed.TotalMilliseconds;

                sw.Restart();
                output = ExtractResults();
                var extractMs = sw.Elapsed.TotalMilliseconds;

                var totalMs = prepareMs + scheduleMs + extractMs;
                PiperLogger.LogInfo(
                    $"[InferenceAudioGenerator] Inference took {totalMs:F1}ms " +
                    $"(prepare: {prepareMs:F1}ms, schedule: {scheduleMs:F1}ms, readback: {extractMs:F1}ms)" +
                    $"{(output.HasDurations ? " [durations: available]" : "")}");

                return output;
            }
            catch (Exception ex)
            {
                output?.Dispose();
                PiperLogger.LogError($"[InferenceAudioGenerator] Failed to execute inference: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 入力テンソルを構築し、ワーカーに設定する。
        /// 呼び出し元がテンソルの所有権を持ち、Disposeの責務を負う。
        /// </summary>
        private InferenceContext PrepareInputs(
            int[] phonemeIds,
            int[] prosodyFlat,
            float lengthScale, float noiseScale, float noiseW,
            int speakerId, int languageId)
        {
            var inputTensor = new Tensor<int>(new TensorShape(1, phonemeIds.Length), phonemeIds);
            var inputLengthsTensor = new Tensor<int>(new TensorShape(1), new[] { phonemeIds.Length });
            var scalesTensor = new Tensor<float>(new TensorShape(3), new[] { noiseScale, lengthScale, noiseW });
            Tensor<int> prosodyTensor = null;
            Tensor<int> sidTensor = null;
            Tensor<int> lidTensor = null;

            _worker.SetInput("input", inputTensor);
            _worker.SetInput("input_lengths", inputLengthsTensor);
            _worker.SetInput("scales", scalesTensor);

            if (_supportsMultiSpeaker)
            {
                sidTensor = new Tensor<int>(new TensorShape(1), new[] { speakerId });
                _worker.SetInput("sid", sidTensor);
            }

            if (_supportsLanguageId)
            {
                lidTensor = new Tensor<int>(new TensorShape(1), new[] { languageId });
                _worker.SetInput("lid", lidTensor);
            }

            if (_supportsProsody)
            {
                prosodyTensor = CreateProsodyTensor(phonemeIds.Length, prosodyFlat);
                _worker.SetInput("prosody_features", prosodyTensor);
            }

            return new InferenceContext(inputTensor, inputLengthsTensor, scalesTensor, prosodyTensor, sidTensor, lidTensor);
        }

        /// <summary>
        /// ワーカーの推論を実行する。事前にPrepareInputsで入力が設定されていること。
        /// </summary>
        private void RunInference()
        {
            _worker.Schedule();
        }

        /// <summary>
        /// 推論結果を取得し、InferenceOutput として返す。
        /// </summary>
        /// <remarks>Caller owns and must Dispose the returned InferenceOutput.</remarks>
        private InferenceOutput ExtractResults()
        {
            var audioData = ExtractAudioData();

            NativeArray<float> durations = default;
            if (_supportsDurations)
            {
                try
                {
                    durations = ExtractDurationsData();
                }
                catch (Exception ex)
                {
                    PiperLogger.LogWarning(
                        $"[InferenceAudioGenerator] Failed to read durations output: {ex.Message}. " +
                        "Continuing without timing data.");
                    if (durations.IsCreated)
                        durations.Dispose();
                    durations = default;
                }
            }

            return new InferenceOutput(audioData, durations);
        }

        /// <summary>
        /// Audio 出力テンソルから NativeArray&lt;float&gt; を抽出する。
        /// </summary>
        private NativeArray<float> ExtractAudioData()
        {
            var outputTensor = GetOutputTensor();
            var readableTensor = outputTensor.ReadbackAndClone();
            NativeArray<float> audioData = default;
            try
            {
                var audioLength = readableTensor.shape.length;
                audioData = new NativeArray<float>(audioLength, Allocator.Persistent);
                var src = readableTensor.DownloadToNativeArray();
                NativeArray<float>.Copy(src, audioData, audioLength);
                return audioData;
            }
            catch
            {
                if (audioData.IsCreated)
                    audioData.Dispose();
                throw;
            }
            finally
            {
                readableTensor.Dispose();
            }
        }

        /// <summary>
        /// Durations 出力テンソルから NativeArray&lt;float&gt; を抽出する。
        /// </summary>
        private NativeArray<float> ExtractDurationsData()
        {
            if (string.IsNullOrEmpty(_cachedDurationsOutputName))
                throw new InvalidOperationException(
                    "Durations output name not cached. Ensure InitializeAsync completed.");

            var durationsTensor = _worker.PeekOutput(_cachedDurationsOutputName) as Tensor<float>;
            if (durationsTensor == null)
                throw new InvalidOperationException(
                    $"Failed to get durations output '{_cachedDurationsOutputName}' from model");

            var readableTensor = durationsTensor.ReadbackAndClone();
            NativeArray<float> durations = default;
            try
            {
                var durLength = readableTensor.shape.length;
                durations = new NativeArray<float>(durLength, Allocator.Persistent);
                var src = readableTensor.DownloadToNativeArray();
                NativeArray<float>.Copy(src, durations, durLength);
                return durations;
            }
            catch
            {
                if (durations.IsCreated)
                    durations.Dispose();
                throw;
            }
            finally
            {
                readableTensor.Dispose();
            }
        }

        /// <summary>
        /// Create prosody tensor with exact-size array (no ArrayPool; Tensor requires exact length).
        /// </summary>
        private static Tensor<int> CreateProsodyTensor(
            int sequenceLength, int[] prosodyFlat)
        {
            var prosodySize = sequenceLength * PhonemeEncoder.ProsodyStride;
            var data = new int[prosodySize];

            if (prosodyFlat != null)
            {
                Array.Copy(prosodyFlat, data, Math.Min(prosodyFlat.Length, prosodySize));
            }

            return new Tensor<int>(new TensorShape(1, sequenceLength, PhonemeEncoder.ProsodyStride), data);
        }

        /// <summary>
        /// Get output tensor from worker using the cached output name.
        /// </summary>
        private Tensor<float> GetOutputTensor()
        {
            if (string.IsNullOrEmpty(_cachedOutputName))
                throw new InvalidOperationException("Output name not cached. Ensure InitializeAsync completed.");

            var outputTensor = _worker.PeekOutput(_cachedOutputName) as Tensor<float>;
            if (outputTensor == null)
                throw new InvalidOperationException($"Failed to get output '{_cachedOutputName}' from model");

            return outputTensor;
        }

        /// <summary>
        /// Holds all input tensors for a single inference call.
        /// Disposing this context releases all resources atomically.
        /// </summary>
        private sealed class InferenceContext : IDisposable
        {
            public Tensor<int> Input { get; }
            public Tensor<int> InputLengths { get; }
            public Tensor<float> Scales { get; }
            public Tensor<int> Prosody { get; }
            public Tensor<int> Sid { get; }
            public Tensor<int> Lid { get; }

            private bool _disposed;

            public InferenceContext(
                Tensor<int> input,
                Tensor<int> inputLengths,
                Tensor<float> scales,
                Tensor<int> prosody,
                Tensor<int> sid,
                Tensor<int> lid)
            {
                Input = input;
                InputLengths = inputLengths;
                Scales = scales;
                Prosody = prosody;
                Sid = sid;
                Lid = lid;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                Input?.Dispose();
                InputLengths?.Dispose();
                Scales?.Dispose();
                Sid?.Dispose();
                Lid?.Dispose();
                Prosody?.Dispose();
            }
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
                        // Model does not implement IDisposable (Unity.InferenceEngine.Model is a plain class).
                        // Setting to null releases the reference for GC.
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
                _cachedOutputName = null;
                _cachedDurationsOutputName = null;
                _supportsDurations = false;
                PiperLogger.LogDebug("InferenceAudioGenerator worker disposed");
            }
        }

    }
}