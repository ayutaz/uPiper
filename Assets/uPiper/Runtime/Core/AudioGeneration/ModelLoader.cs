using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using UnityEngine;
using Unity.InferenceEngine;
using uPiper.Core.Logging;

namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// Handles loading and management of ONNX models for Sentis
    /// </summary>
    public class ModelLoader : IDisposable
    {
        private Model _loadedModel;
        private ModelAsset _modelAsset;
        private bool _disposed;

        /// <summary>
        /// Currently loaded model
        /// </summary>
        public Model LoadedModel => _loadedModel;

        /// <summary>
        /// Whether a model is loaded
        /// </summary>
        public bool IsModelLoaded => _loadedModel != null;

        /// <summary>
        /// Load an ONNX model from file
        /// </summary>
        /// <param name="modelPath">Path to the ONNX model file</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Model information</returns>
        public async Task<ModelInfo> LoadModelAsync(string modelPath, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ModelLoader));

            if (string.IsNullOrEmpty(modelPath))
                throw new ArgumentNullException(nameof(modelPath));

            if (!File.Exists(modelPath))
                throw new FileNotFoundException($"Model file not found: {modelPath}");

            try
            {
                PiperLogger.LogInfo("Loading ONNX model from: {0}", modelPath);

                // Get file info
                var fileInfo = new FileInfo(modelPath);
                var modelSizeBytes = fileInfo.Length;

                // Load the model asynchronously
                await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // Load ONNX file as bytes
                    var modelBytes = File.ReadAllBytes(modelPath);
                    
                    // Create model from ONNX bytes
                    _modelAsset = ScriptableObject.CreateInstance<ModelAsset>();
                    _modelAsset.modelAssetData = new ModelAssetData { value = modelBytes };
                    
                    // Import the model
                    _loadedModel = Unity.InferenceEngine.ModelLoader.Load(_modelAsset);
                    
                }, cancellationToken);

                // Extract model information
                var modelInfo = ExtractModelInfo(modelPath, modelSizeBytes);
                
                PiperLogger.LogInfo("Model loaded successfully: {0}", modelInfo.Name);
                return modelInfo;
            }
            catch (OperationCanceledException)
            {
                PiperLogger.LogWarning("Model loading was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                PiperLogger.LogError("Failed to load model: {0}", ex.Message);
                throw new PiperException($"Failed to load ONNX model from {modelPath}", ex);
            }
        }

        /// <summary>
        /// Load a model from Unity ModelAsset
        /// </summary>
        /// <param name="modelAsset">Unity Sentis ModelAsset</param>
        /// <returns>Model information</returns>
        public ModelInfo LoadModel(ModelAsset modelAsset)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ModelLoader));

            if (modelAsset == null)
                throw new ArgumentNullException(nameof(modelAsset));

            try
            {
                PiperLogger.LogInfo("Loading model from ModelAsset");
                
                _modelAsset = modelAsset;
                _loadedModel = Unity.InferenceEngine.ModelLoader.Load(modelAsset);
                
                var modelInfo = ExtractModelInfo(modelAsset.name, 0);
                
                PiperLogger.LogInfo("Model loaded successfully: {0}", modelInfo.Name);
                return modelInfo;
            }
            catch (Exception ex)
            {
                PiperLogger.LogError("Failed to load model from asset: {0}", ex.Message);
                throw new PiperException("Failed to load model from ModelAsset", ex);
            }
        }

        /// <summary>
        /// Validate the loaded model for TTS compatibility
        /// </summary>
        /// <returns>Validation result</returns>
        public ValidationResult ValidateModel()
        {
            if (!IsModelLoaded)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "No model loaded"
                };
            }

            try
            {
                var result = new ValidationResult { IsValid = true };

                // Check for required inputs
                if (_loadedModel.inputs == null || _loadedModel.inputs.Count == 0)
                {
                    result.IsValid = false;
                    result.ErrorMessage = "Model has no inputs";
                    return result;
                }

                // Check for required outputs
                if (_loadedModel.outputs == null || _loadedModel.outputs.Count == 0)
                {
                    result.IsValid = false;
                    result.ErrorMessage = "Model has no outputs";
                    return result;
                }

                // Validate input shapes
                foreach (var input in _loadedModel.inputs)
                {
                    PiperLogger.LogDebug("Model input: {0}, shape: {1}", 
                        input.name, 
                        string.Join("x", input.shape.ToArray()));
                }

                // Validate output shapes
                foreach (var output in _loadedModel.outputs)
                {
                    PiperLogger.LogDebug("Model output: {0}", output.name);
                }

                return result;
            }
            catch (Exception ex)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"Validation error: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Get model metadata
        /// </summary>
        /// <returns>Model metadata dictionary</returns>
        public System.Collections.Generic.Dictionary<string, string> GetModelMetadata()
        {
            var metadata = new System.Collections.Generic.Dictionary<string, string>();

            if (!IsModelLoaded)
                return metadata;

            // Add basic model info
            metadata["InputCount"] = _loadedModel.inputs.Count.ToString();
            metadata["OutputCount"] = _loadedModel.outputs.Count.ToString();
            metadata["LayerCount"] = _loadedModel.layers.Count.ToString();

            // Add input info
            for (int i = 0; i < _loadedModel.inputs.Count; i++)
            {
                var input = _loadedModel.inputs[i];
                metadata[$"Input{i}_Name"] = input.name;
                metadata[$"Input{i}_Shape"] = string.Join("x", input.shape.ToArray());
            }

            // Add output info
            for (int i = 0; i < _loadedModel.outputs.Count; i++)
            {
                var output = _loadedModel.outputs[i];
                metadata[$"Output{i}_Name"] = output.name;
            }

            return metadata;
        }

        /// <summary>
        /// Unload the current model
        /// </summary>
        public void UnloadModel()
        {
            if (_loadedModel != null)
            {
                PiperLogger.LogInfo("Unloading model");
                _loadedModel = null;
            }

            if (_modelAsset != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(_modelAsset);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(_modelAsset);
                }
                _modelAsset = null;
            }
        }

        /// <summary>
        /// Extract model information
        /// </summary>
        private ModelInfo ExtractModelInfo(string modelPath, long modelSizeBytes)
        {
            var info = new ModelInfo
            {
                Name = Path.GetFileNameWithoutExtension(modelPath),
                ModelPath = modelPath,
                ModelSizeBytes = modelSizeBytes,
                Version = "1.0", // Default version
                SampleRate = 22050, // Default sample rate (will be overridden by model metadata)
                SpeakerCount = 1, // Default to single speaker
                MaxSequenceLength = 1024, // Default max sequence
                Backend = BackendType.GPUCompute // Default backend
            };

            // Try to load config from JSON file
            var jsonPath = modelPath + ".json";
            if (File.Exists(jsonPath))
            {
                try
                {
                    var jsonContent = File.ReadAllText(jsonPath);
                    var config = JsonUtility.FromJson<PiperModelConfig>(jsonContent);
                    
                    if (config != null && config.audio != null)
                    {
                        info.SampleRate = config.audio.sample_rate;
                    }
                    
                    PiperLogger.LogInfo("Loaded model config from {0}", jsonPath);
                }
                catch (Exception ex)
                {
                    PiperLogger.LogWarning("Failed to load model config: {0}", ex.Message);
                }
            }

            // Try to extract metadata from model
            if (_loadedModel != null)
            {
                // TODO: Extract actual metadata from model when Sentis supports it
                // For now, use defaults based on common Piper model configurations
            }

            return info;
        }

        /// <summary>
        /// Piper model configuration structure
        /// </summary>
        [Serializable]
        private class PiperModelConfig
        {
            public AudioConfig audio;
            public EspeakConfig espeak;
            public InferenceConfig inference;
            public string phoneme_type;
            
            [Serializable]
            public class AudioConfig
            {
                public int sample_rate;
                public string quality;
            }
            
            [Serializable]
            public class EspeakConfig
            {
                public string voice;
            }
            
            [Serializable]
            public class InferenceConfig
            {
                public float noise_scale;
                public float length_scale;
                public float noise_w;
            }
        }

        /// <summary>
        /// Dispose of resources
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            UnloadModel();
            _disposed = true;
        }

        /// <summary>
        /// Model validation result
        /// </summary>
        public class ValidationResult
        {
            public bool IsValid { get; set; }
            public string ErrorMessage { get; set; }
            public string[] Warnings { get; set; }
        }
    }
}