using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Unity.InferenceEngine;



namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// Interface for Sentis-based audio generation from phoneme sequences
    /// </summary>
    public interface ISentisAudioGenerator : IDisposable
    {
        /// <summary>
        /// Whether the generator is initialized and ready
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Current ONNX model information
        /// </summary>
        ModelInfo CurrentModel { get; }

        /// <summary>
        /// Initialize the generator with a model
        /// </summary>
        /// <param name="modelPath">Path to the ONNX model file</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task InitializeAsync(string modelPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate audio from phoneme IDs
        /// </summary>
        /// <param name="phonemeIds">Array of phoneme IDs</param>
        /// <param name="speakerId">Optional speaker ID for multi-speaker models</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Generated audio clip</returns>
        Task<AudioClip> GenerateAudioAsync(
            int[] phonemeIds, 
            int speakerId = 0,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate audio from phoneme result
        /// </summary>
        /// <param name="phonemeResult">Phoneme result from phonemizer</param>
        /// <param name="speakerId">Optional speaker ID for multi-speaker models</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Generated audio clip</returns>
        Task<AudioClip> GenerateAudioAsync(
            Phonemizers.PhonemeResult phonemeResult,
            int speakerId = 0,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stream audio generation in chunks
        /// </summary>
        /// <param name="phonemeIds">Array of phoneme IDs</param>
        /// <param name="speakerId">Optional speaker ID</param>
        /// <param name="chunkSize">Size of each audio chunk in samples</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task that yields audio chunks</returns>
        Task<IEnumerable<AudioChunk>> StreamAudioAsync(
            int[] phonemeIds,
            int speakerId = 0,
            int chunkSize = 8192,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Get generation statistics
        /// </summary>
        GenerationStatistics GetStatistics();

        /// <summary>
        /// Event fired when generation progress updates
        /// </summary>
        event Action<float> OnProgress;

        /// <summary>
        /// Event fired when an error occurs
        /// </summary>
        event Action<Exception> OnError;
    }

    /// <summary>
    /// Information about the loaded ONNX model
    /// </summary>
    public class ModelInfo
    {
        /// <summary>
        /// Model name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Model version
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Sample rate the model was trained with
        /// </summary>
        public int SampleRate { get; set; }

        /// <summary>
        /// Number of speakers (for multi-speaker models)
        /// </summary>
        public int SpeakerCount { get; set; }

        /// <summary>
        /// Maximum phoneme sequence length
        /// </summary>
        public int MaxSequenceLength { get; set; }

        /// <summary>
        /// Model file path
        /// </summary>
        public string ModelPath { get; set; }

        /// <summary>
        /// Model size in bytes
        /// </summary>
        public long ModelSizeBytes { get; set; }

        /// <summary>
        /// Backend type being used
        /// </summary>
        public BackendType Backend { get; set; }
    }

    /// <summary>
    /// Statistics about audio generation performance
    /// </summary>
    public class GenerationStatistics
    {
        /// <summary>
        /// Total number of generations performed
        /// </summary>
        public int TotalGenerations { get; set; }

        /// <summary>
        /// Average generation time in milliseconds
        /// </summary>
        public float AverageGenerationTimeMs { get; set; }

        /// <summary>
        /// Average real-time factor (audio duration / generation time)
        /// </summary>
        public float AverageRealTimeFactor { get; set; }

        /// <summary>
        /// Total audio duration generated in seconds
        /// </summary>
        public float TotalAudioDurationSeconds { get; set; }

        /// <summary>
        /// Peak memory usage in bytes
        /// </summary>
        public long PeakMemoryUsageBytes { get; set; }

        /// <summary>
        /// Number of errors encountered
        /// </summary>
        public int ErrorCount { get; set; }
    }
}