using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Sentis;
using UnityEngine;

namespace uPiper.Sentis
{
    /// <summary>
    /// Handles audio generation using Unity Sentis
    /// </summary>
    public class SentisAudioGenerator : IDisposable
    {
        private Model _model;
        private Worker _worker;
        private readonly BackendType _backendType;
        private bool _isInitialized;

        public bool IsInitialized => _isInitialized;

        public SentisAudioGenerator(BackendType backendType = BackendType.GPUCompute)
        {
            _backendType = backendType;
        }

        /// <summary>
        /// Loads the ONNX model for audio generation
        /// </summary>
        public async Task LoadModelAsync(string modelPath)
        {
            if (_isInitialized)
            {
                throw new InvalidOperationException("Model is already loaded");
            }

            await Task.Run(() =>
            {
                try
                {
                    // Load the ONNX model
                    _model = ModelLoader.Load(modelPath);
                    
                    // Create worker with specified backend
                    _worker = new Worker(_model, _backendType);
                    
                    _isInitialized = true;
                    Debug.Log($"[uPiper] Loaded model from: {modelPath}");
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to load model from {modelPath}: {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// Generates audio from phoneme input
        /// </summary>
        public async Task<float[]> GenerateAudioAsync(int[] phonemeIds, int speakerId = 0, float lengthScale = 1.0f)
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("Model is not loaded");
            }

            return await Task.Run(() =>
            {
                try
                {
                    // Create input tensors
                    var phonemeInput = TensorInt.AllocNoData(new TensorShape(1, phonemeIds.Length));
                    phonemeInput.CompleteAllocation(phonemeIds);
                    
                    var speakerInput = TensorInt.AllocNoData(new TensorShape(1));
                    speakerInput.CompleteAllocation(new[] { speakerId });
                    
                    var lengthScaleInput = TensorFloat.AllocNoData(new TensorShape(1));
                    lengthScaleInput.CompleteAllocation(new[] { lengthScale });

                    // Set inputs
                    _worker.SetInput("phoneme_ids", phonemeInput);
                    _worker.SetInput("speaker_id", speakerInput);
                    _worker.SetInput("length_scale", lengthScaleInput);
                    
                    // Execute the model
                    _worker.Schedule();

                    // Get output audio
                    var output = _worker.PeekOutput("audio") as TensorFloat;
                    if (output == null)
                    {
                        throw new Exception("Failed to get audio output from model");
                    }

                    // Convert tensor to float array
                    var audioData = output.ToReadOnlyArray();
                    var result = new float[audioData.Length];
                    audioData.CopyTo(result, 0);

                    // Dispose tensors
                    phonemeInput.Dispose();
                    speakerInput.Dispose();
                    lengthScaleInput.Dispose();
                    output.Dispose();

                    return result;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to generate audio: {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// Gets model metadata
        /// </summary>
        public ModelMetadata GetModelMetadata()
        {
            if (!_isInitialized || _model == null)
            {
                throw new InvalidOperationException("Model is not loaded");
            }

            var metadata = new ModelMetadata
            {
                InputNames = new List<string>(),
                OutputNames = new List<string>(),
                SampleRate = 22050 // Default, should be read from model metadata
            };

            // Get input/output names from the model
            foreach (var input in _model.inputs)
            {
                metadata.InputNames.Add(input.name);
            }

            foreach (var output in _model.outputs)
            {
                metadata.OutputNames.Add(output.name);
            }

            return metadata;
        }

        public void Dispose()
        {
            _worker?.Dispose();
            _worker = null;
            _model = null;
            _isInitialized = false;
        }
    }

    /// <summary>
    /// Model metadata information
    /// </summary>
    public class ModelMetadata
    {
        public List<string> InputNames { get; set; }
        public List<string> OutputNames { get; set; }
        public int SampleRate { get; set; }
        public int MaxPhonemeLength { get; set; }
        public int NumSpeakers { get; set; }
    }
}