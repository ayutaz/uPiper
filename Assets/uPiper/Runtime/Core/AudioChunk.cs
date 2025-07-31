using System;
using UnityEngine;

namespace uPiper.Core
{
    /// <summary>
    /// Represents a chunk of audio data for streaming
    /// </summary>
    public class AudioChunk
    {
        /// <summary>
        /// Audio data samples
        /// </summary>
        public float[] Samples { get; }

        /// <summary>
        /// Sample rate of the audio
        /// </summary>
        public int SampleRate { get; }

        /// <summary>
        /// Number of channels (1 for mono, 2 for stereo)
        /// </summary>
        public int Channels { get; }

        /// <summary>
        /// Chunk index in the stream
        /// </summary>
        public int ChunkIndex { get; }

        /// <summary>
        /// Whether this is the final chunk
        /// </summary>
        public bool IsFinal { get; }

        /// <summary>
        /// Text portion that generated this audio
        /// </summary>
        public string TextSegment { get; }

        /// <summary>
        /// Start time in seconds from the beginning of the full audio
        /// </summary>
        public float StartTime { get; }

        /// <summary>
        /// Duration of this chunk in seconds
        /// </summary>
        public float Duration => Samples.Length / (float)(SampleRate * Channels);

        /// <summary>
        /// Create a new audio chunk
        /// </summary>
        public AudioChunk(
            float[] samples,
            int sampleRate,
            int channels,
            int chunkIndex,
            bool isFinal,
            string textSegment = null,
            float startTime = 0f)
        {
            if (sampleRate <= 0)
                throw new ArgumentException("Sample rate must be positive", nameof(sampleRate));
            if (channels <= 0)
                throw new ArgumentException("Channel count must be positive", nameof(channels));
            if (chunkIndex < 0)
                throw new ArgumentException("Chunk index must be non-negative", nameof(chunkIndex));

            Samples = samples ?? throw new ArgumentNullException(nameof(samples));
            SampleRate = sampleRate;
            Channels = channels;
            ChunkIndex = chunkIndex;
            IsFinal = isFinal;
            TextSegment = textSegment;
            StartTime = startTime;
        }

        /// <summary>
        /// Convert to Unity AudioClip
        /// </summary>
        public AudioClip ToAudioClip(string name = null)
        {
            if (string.IsNullOrEmpty(name))
                name = $"AudioChunk_{ChunkIndex}";

            var clip = AudioClip.Create(
                name,
                Samples.Length / Channels,
                Channels,
                SampleRate,
                false);

            clip.SetData(Samples, 0);
            return clip;
        }

        /// <summary>
        /// Combine multiple chunks into a single AudioClip
        /// </summary>
        public static AudioClip CombineChunks(AudioChunk[] chunks, string name = "CombinedAudio")
        {
            if (chunks == null || chunks.Length == 0)
                throw new ArgumentException("No chunks to combine", nameof(chunks));

            // Validate all chunks have same format
            var firstChunk = chunks[0];
            var totalSamples = 0;

            foreach (var chunk in chunks)
            {
                if (chunk.SampleRate != firstChunk.SampleRate)
                    throw new ArgumentException("All chunks must have the same sample rate");
                if (chunk.Channels != firstChunk.Channels)
                    throw new ArgumentException("All chunks must have the same channel count");

                totalSamples += chunk.Samples.Length;
            }

            // Combine samples
            var combinedSamples = new float[totalSamples];
            var currentPosition = 0;

            foreach (var chunk in chunks)
            {
                Array.Copy(chunk.Samples, 0, combinedSamples, currentPosition, chunk.Samples.Length);
                currentPosition += chunk.Samples.Length;
            }

            // Create combined AudioClip
            var clip = AudioClip.Create(
                name,
                totalSamples / firstChunk.Channels,
                firstChunk.Channels,
                firstChunk.SampleRate,
                false);

            clip.SetData(combinedSamples, 0);
            return clip;
        }
    }
}