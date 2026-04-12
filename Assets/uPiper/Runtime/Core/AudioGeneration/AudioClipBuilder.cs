using System;
using Unity.Collections;
using UnityEngine;
using uPiper.Core.Logging;

namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// 音声データからUnity AudioClipを構築するクラス
    /// </summary>
    public class AudioClipBuilder
    {

        /// <summary>
        /// float配列からAudioClipを作成する
        /// </summary>
        /// <param name="audioData">音声データ</param>
        /// <param name="sampleRate">サンプルレート</param>
        /// <param name="clipName">クリップ名（オプション）</param>
        /// <returns>作成されたAudioClip</returns>
        [Obsolete("Use BuildAudioClip(NativeArray<float>, int, string) to avoid managed marshalling.")]
        public AudioClip BuildAudioClip(float[] audioData, int sampleRate, string clipName = null)
        {
            if (audioData == null || audioData.Length == 0)
            {
                throw new ArgumentException("Audio data cannot be null or empty", nameof(audioData));
            }

            if (sampleRate <= 0)
            {
                throw new ArgumentException("Sample rate must be positive", nameof(sampleRate));
            }

            // AudioClipの名前を設定
            var name = string.IsNullOrEmpty(clipName) ? $"GeneratedAudio_{DateTime.Now:yyyyMMddHHmmss}" : clipName;

            // Unity AudioClipを作成
            var audioClip = AudioClip.Create(
                name: name,
                lengthSamples: audioData.Length,
                channels: 1, // モノラル
                frequency: sampleRate,
                stream: false
            );

            try
            {
                // データを設定
                if (!audioClip.SetData(audioData, 0))
                {
                    throw new InvalidOperationException("Failed to set audio data to AudioClip");
                }
            }
            catch
            {
                if (audioClip != null)
                {
                    if (Application.isPlaying)
                        UnityEngine.Object.Destroy(audioClip);
                    else
                        UnityEngine.Object.DestroyImmediate(audioClip);
                }
                throw;
            }

            PiperLogger.LogDebug($"Created AudioClip: {name}, {audioData.Length} samples, {sampleRate}Hz");
            return audioClip;
        }

        /// <summary>
        /// NativeArray&lt;float&gt;からAudioClipを作成する。managed marshallingを回避する。
        /// </summary>
        /// <param name="audioData">音声データ（NativeArray）</param>
        /// <param name="sampleRate">サンプルレート</param>
        /// <param name="clipName">クリップ名（オプション）</param>
        /// <returns>作成されたAudioClip</returns>
        /// <remarks>
        /// AudioClip.SetData は NativeArray の内容を内部バッファにコピーするため、
        /// 呼び出し後に NativeArray を Dispose しても安全。
        /// </remarks>
        public AudioClip BuildAudioClip(NativeArray<float> audioData, int sampleRate, string clipName = null)
        {
            if (!audioData.IsCreated || audioData.Length == 0)
            {
                throw new ArgumentException("Audio data cannot be empty or uninitialized", nameof(audioData));
            }

            if (sampleRate <= 0)
            {
                throw new ArgumentException("Sample rate must be positive", nameof(sampleRate));
            }

            // AudioClipの名前を設定
            var name = string.IsNullOrEmpty(clipName)
                ? $"GeneratedAudio_{DateTime.Now:yyyyMMddHHmmss}" : clipName;

            // Unity AudioClipを作成
            var audioClip = AudioClip.Create(
                name: name,
                lengthSamples: audioData.Length,
                channels: 1, // モノラル
                frequency: sampleRate,
                stream: false
            );

            try
            {
                // NativeArray版SetDataでmanaged marshallingを回避
                if (!audioClip.SetData(audioData, 0))
                {
                    throw new InvalidOperationException("Failed to set audio data to AudioClip");
                }
            }
            catch
            {
                if (audioClip != null)
                {
                    if (Application.isPlaying)
                        UnityEngine.Object.Destroy(audioClip);
                    else
                        UnityEngine.Object.DestroyImmediate(audioClip);
                }
                throw;
            }

            PiperLogger.LogDebug($"Created AudioClip: {name}, {audioData.Length} samples, {sampleRate}Hz");
            return audioClip;
        }
    }
}