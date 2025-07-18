using UnityEngine;

namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// Interface for building Unity AudioClips from raw audio data
    /// </summary>
    public interface IAudioClipBuilder
    {
        /// <summary>
        /// Create an AudioClip from float array
        /// </summary>
        /// <param name="samples">Audio samples as float array</param>
        /// <param name="sampleRate">Sample rate in Hz</param>
        /// <param name="channels">Number of channels (1 for mono, 2 for stereo)</param>
        /// <param name="clipName">Optional name for the clip</param>
        /// <returns>Unity AudioClip</returns>
        AudioClip CreateAudioClip(
            float[] samples, 
            int sampleRate, 
            int channels = 1, 
            string clipName = "GeneratedAudio");

        /// <summary>
        /// Create an AudioClip from normalized float array (-1 to 1)
        /// </summary>
        /// <param name="normalizedSamples">Normalized audio samples</param>
        /// <param name="sampleRate">Sample rate in Hz</param>
        /// <param name="channels">Number of channels</param>
        /// <param name="clipName">Optional name for the clip</param>
        /// <returns>Unity AudioClip</returns>
        AudioClip CreateAudioClipNormalized(
            float[] normalizedSamples,
            int sampleRate,
            int channels = 1,
            string clipName = "GeneratedAudio");

        /// <summary>
        /// Normalize audio samples to -1 to 1 range
        /// </summary>
        /// <param name="samples">Raw audio samples</param>
        /// <returns>Normalized samples</returns>
        float[] NormalizeSamples(float[] samples);

        /// <summary>
        /// Apply post-processing to audio samples
        /// </summary>
        /// <param name="samples">Audio samples</param>
        /// <param name="options">Processing options</param>
        /// <returns>Processed samples</returns>
        float[] PostProcess(float[] samples, AudioProcessingOptions options);
    }

    /// <summary>
    /// Options for audio post-processing
    /// </summary>
    public class AudioProcessingOptions
    {
        /// <summary>
        /// Apply denoising
        /// </summary>
        public bool Denoise { get; set; } = false;

        /// <summary>
        /// Apply normalization
        /// </summary>
        public bool Normalize { get; set; } = true;

        /// <summary>
        /// Target peak level for normalization (0-1)
        /// </summary>
        public float TargetPeak { get; set; } = 0.95f;

        /// <summary>
        /// Apply fade in/out
        /// </summary>
        public bool ApplyFade { get; set; } = true;

        /// <summary>
        /// Fade in duration in seconds
        /// </summary>
        public float FadeInDuration { get; set; } = 0.01f;

        /// <summary>
        /// Fade out duration in seconds
        /// </summary>
        public float FadeOutDuration { get; set; } = 0.01f;

        /// <summary>
        /// Remove silence from beginning and end
        /// </summary>
        public bool TrimSilence { get; set; } = false;

        /// <summary>
        /// Silence threshold (0-1)
        /// </summary>
        public float SilenceThreshold { get; set; } = 0.01f;
    }
}