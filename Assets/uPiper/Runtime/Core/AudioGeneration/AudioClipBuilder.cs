using System;
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

            // データを設定
            if (!audioClip.SetData(audioData, 0))
            {
                throw new InvalidOperationException("Failed to set audio data to AudioClip");
            }

            PiperLogger.LogDebug($"Created AudioClip: {name}, {audioData.Length} samples, {sampleRate}Hz");
            return audioClip;
        }

        /// <summary>
        /// 音声データを正規化する
        /// </summary>
        /// <param name="audioData">音声データ</param>
        /// <param name="targetPeak">目標ピーク値（0-1）</param>
        /// <returns>正規化された音声データ</returns>
        public float[] NormalizeAudio(float[] audioData, float targetPeak = 0.95f)
        {
            if (audioData == null || audioData.Length == 0)
                return audioData;

            targetPeak = Mathf.Clamp01(targetPeak);

            // 最大振幅を見つける
            var maxAmplitude = 0f;
            for (var i = 0; i < audioData.Length; i++)
            {
                var absValue = Mathf.Abs(audioData[i]);
                if (absValue > maxAmplitude)
                {
                    maxAmplitude = absValue;
                }
            }

            // 既に正規化されている場合はそのまま返す
            if (maxAmplitude <= 0f || Mathf.Approximately(maxAmplitude, targetPeak))
            {
                return audioData;
            }

            // スケーリング係数を計算
            var scale = targetPeak / maxAmplitude;

            // 正規化
            var normalizedData = new float[audioData.Length];
            for (var i = 0; i < audioData.Length; i++)
            {
                normalizedData[i] = audioData[i] * scale;
            }

            PiperLogger.LogDebug($"Normalized audio: max amplitude {maxAmplitude:F3} -> {targetPeak:F3}");
            return normalizedData;
        }

    }
}