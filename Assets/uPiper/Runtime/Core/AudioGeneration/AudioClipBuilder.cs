using System;
using System.Linq;
using UnityEngine;
using uPiper.Core.Logging;

namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// Builds Unity AudioClips from raw audio data
    /// </summary>
    public class AudioClipBuilder : IAudioClipBuilder
    {
        /// <summary>
        /// Create an AudioClip from float array
        /// </summary>
        public AudioClip CreateAudioClip(
            float[] samples, 
            int sampleRate, 
            int channels = 1, 
            string clipName = "GeneratedAudio")
        {
            if (samples == null || samples.Length == 0)
                throw new ArgumentNullException(nameof(samples));

            if (sampleRate <= 0)
                throw new ArgumentException("Sample rate must be positive", nameof(sampleRate));

            if (channels <= 0)
                throw new ArgumentException("Channel count must be positive", nameof(channels));

            // Calculate the number of samples per channel
            int samplesPerChannel = samples.Length / channels;
            
            // Create the audio clip
            var audioClip = AudioClip.Create(
                clipName,
                samplesPerChannel,
                channels,
                sampleRate,
                false // Don't stream
            );

            // Set the audio data
            if (!audioClip.SetData(samples, 0))
            {
                throw new PiperException("Failed to set audio data on AudioClip");
            }

            PiperLogger.LogDebug("Created AudioClip: {0} samples, {1}Hz, {2} channels", 
                samplesPerChannel, sampleRate, channels);

            return audioClip;
        }

        /// <summary>
        /// Create an AudioClip from normalized float array (-1 to 1)
        /// </summary>
        public AudioClip CreateAudioClipNormalized(
            float[] normalizedSamples,
            int sampleRate,
            int channels = 1,
            string clipName = "GeneratedAudio")
        {
            // Normalized samples can be used directly
            return CreateAudioClip(normalizedSamples, sampleRate, channels, clipName);
        }

        /// <summary>
        /// Normalize audio samples to -1 to 1 range
        /// </summary>
        public float[] NormalizeSamples(float[] samples)
        {
            if (samples == null || samples.Length == 0)
                return samples;

            // Find the maximum absolute value
            float maxAbsValue = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float absValue = Math.Abs(samples[i]);
                if (absValue > maxAbsValue)
                    maxAbsValue = absValue;
            }

            // If already normalized or silent, return as-is
            if (maxAbsValue <= 1f || maxAbsValue == 0f)
                return samples;

            // Normalize
            float scale = 1f / maxAbsValue;
            float[] normalized = new float[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                normalized[i] = samples[i] * scale;
            }

            PiperLogger.LogDebug("Normalized audio samples (max value was {0:F3})", maxAbsValue);
            return normalized;
        }

        /// <summary>
        /// Apply post-processing to audio samples
        /// </summary>
        public float[] PostProcess(float[] samples, AudioProcessingOptions options)
        {
            if (samples == null || samples.Length == 0)
                return samples;

            if (options == null)
                options = new AudioProcessingOptions();

            float[] processed = samples;

            // Trim silence
            if (options.TrimSilence)
            {
                processed = TrimSilence(processed, options.SilenceThreshold);
            }

            // Apply normalization
            if (options.Normalize)
            {
                processed = NormalizeToTarget(processed, options.TargetPeak);
            }

            // Apply fade in/out
            if (options.ApplyFade && processed.Length > 0)
            {
                int sampleRate = 22050; // Assume default, should be passed as parameter
                processed = ApplyFade(processed, sampleRate, 
                    options.FadeInDuration, options.FadeOutDuration);
            }

            // Apply denoising (simple moving average for now)
            if (options.Denoise)
            {
                processed = ApplySimpleDenoising(processed);
            }

            return processed;
        }

        /// <summary>
        /// Trim silence from beginning and end of audio
        /// </summary>
        private float[] TrimSilence(float[] samples, float threshold)
        {
            int startIndex = 0;
            int endIndex = samples.Length - 1;

            // Find first non-silent sample
            for (int i = 0; i < samples.Length; i++)
            {
                if (Math.Abs(samples[i]) > threshold)
                {
                    startIndex = i;
                    break;
                }
            }

            // Find last non-silent sample
            for (int i = samples.Length - 1; i >= 0; i--)
            {
                if (Math.Abs(samples[i]) > threshold)
                {
                    endIndex = i;
                    break;
                }
            }

            // If all silence, return empty array
            if (startIndex > endIndex)
                return new float[0];

            // Extract non-silent portion
            int length = endIndex - startIndex + 1;
            float[] trimmed = new float[length];
            Array.Copy(samples, startIndex, trimmed, 0, length);

            PiperLogger.LogDebug("Trimmed {0} samples from start, {1} from end", 
                startIndex, samples.Length - endIndex - 1);

            return trimmed;
        }

        /// <summary>
        /// Normalize samples to a target peak level
        /// </summary>
        private float[] NormalizeToTarget(float[] samples, float targetPeak)
        {
            if (targetPeak <= 0 || targetPeak > 1)
                targetPeak = 0.95f;

            // Find current peak
            float currentPeak = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float absValue = Math.Abs(samples[i]);
                if (absValue > currentPeak)
                    currentPeak = absValue;
            }

            // If silent or already at target, return as-is
            if (currentPeak == 0f || Math.Abs(currentPeak - targetPeak) < 0.001f)
                return samples;

            // Scale to target peak
            float scale = targetPeak / currentPeak;
            float[] normalized = new float[samples.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                normalized[i] = samples[i] * scale;
                // Ensure we don't exceed [-1, 1] range
                normalized[i] = Mathf.Clamp(normalized[i], -1f, 1f);
            }

            return normalized;
        }

        /// <summary>
        /// Apply fade in and fade out
        /// </summary>
        private float[] ApplyFade(float[] samples, int sampleRate, 
            float fadeInDuration, float fadeOutDuration)
        {
            float[] faded = (float[])samples.Clone();

            // Calculate fade lengths in samples
            int fadeInSamples = Mathf.Min(
                (int)(fadeInDuration * sampleRate), 
                samples.Length / 2);
            int fadeOutSamples = Mathf.Min(
                (int)(fadeOutDuration * sampleRate), 
                samples.Length / 2);

            // Apply fade in
            for (int i = 0; i < fadeInSamples; i++)
            {
                float fadeAmount = (float)i / fadeInSamples;
                faded[i] *= fadeAmount;
            }

            // Apply fade out
            int fadeOutStart = samples.Length - fadeOutSamples;
            for (int i = 0; i < fadeOutSamples; i++)
            {
                float fadeAmount = 1f - ((float)i / fadeOutSamples);
                faded[fadeOutStart + i] *= fadeAmount;
            }

            return faded;
        }

        /// <summary>
        /// Apply simple denoising using moving average
        /// </summary>
        private float[] ApplySimpleDenoising(float[] samples)
        {
            const int windowSize = 3;
            float[] denoised = new float[samples.Length];

            for (int i = 0; i < samples.Length; i++)
            {
                float sum = 0;
                int count = 0;

                // Calculate moving average
                for (int j = -windowSize / 2; j <= windowSize / 2; j++)
                {
                    int index = i + j;
                    if (index >= 0 && index < samples.Length)
                    {
                        sum += samples[index];
                        count++;
                    }
                }

                denoised[i] = sum / count;
            }

            return denoised;
        }

        /// <summary>
        /// Convert int16 samples to float samples
        /// </summary>
        public static float[] ConvertInt16ToFloat(short[] int16Samples)
        {
            if (int16Samples == null)
                return null;

            float[] floatSamples = new float[int16Samples.Length];
            const float scale = 1.0f / 32768.0f; // Convert from int16 range to [-1, 1]

            for (int i = 0; i < int16Samples.Length; i++)
            {
                floatSamples[i] = int16Samples[i] * scale;
            }

            return floatSamples;
        }

        /// <summary>
        /// Convert float samples to int16 samples
        /// </summary>
        public static short[] ConvertFloatToInt16(float[] floatSamples)
        {
            if (floatSamples == null)
                return null;

            short[] int16Samples = new short[floatSamples.Length];
            const float scale = 32767.0f; // Convert from [-1, 1] to int16 range

            for (int i = 0; i < floatSamples.Length; i++)
            {
                // Clamp to [-1, 1] range and scale
                float clamped = Mathf.Clamp(floatSamples[i], -1f, 1f);
                int16Samples[i] = (short)(clamped * scale);
            }

            return int16Samples;
        }
    }
}