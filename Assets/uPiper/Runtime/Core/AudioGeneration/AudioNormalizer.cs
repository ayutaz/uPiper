using System;
using Unity.Collections;
using UnityEngine;
using uPiper.Core.Logging;

namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// 音声データの正規化を行うユーティリティクラス
    /// </summary>
    public static class AudioNormalizer
    {
        /// <summary>
        /// 音声データをin-placeで正規化する。元の配列が直接変更される。
        /// </summary>
        /// <param name="audioData">音声データ（変更される）</param>
        /// <param name="targetPeak">目標ピーク値（0-1）</param>
        public static void NormalizeInPlace(float[] audioData, float targetPeak = 0.95f)
        {
            if (audioData == null || audioData.Length == 0)
                return;

            targetPeak = Mathf.Clamp01(targetPeak);

            // 最大振幅を見つける
            var maxAmplitude = 0f;
            for (var i = 0; i < audioData.Length; i++)
            {
                var absValue = Mathf.Abs(audioData[i]);
                if (absValue > maxAmplitude)
                    maxAmplitude = absValue;
            }

            // 既に正規化されている場合はスキップ
            if (maxAmplitude <= 0f || Mathf.Approximately(maxAmplitude, targetPeak))
                return;

            var scale = targetPeak / maxAmplitude;
            for (var i = 0; i < audioData.Length; i++)
            {
                audioData[i] *= scale;
            }

            PiperLogger.LogDebug($"Normalized audio in-place: max amplitude {maxAmplitude:F3} -> {targetPeak:F3}");
        }

        /// <summary>
        /// NativeArray&lt;float&gt;音声データをin-placeで正規化する。元のデータが直接変更される。
        /// GCアロケーションなし。
        /// </summary>
        /// <param name="audioData">音声データ（変更される）</param>
        /// <param name="targetPeak">目標ピーク値（0-1）</param>
        public static void NormalizeInPlace(NativeArray<float> audioData, float targetPeak = 0.95f)
        {
            if (!audioData.IsCreated || audioData.Length == 0)
                return;

            targetPeak = Mathf.Clamp01(targetPeak);

            // 最大振幅を見つける
            var maxAmplitude = 0f;
            for (var i = 0; i < audioData.Length; i++)
            {
                var absValue = Mathf.Abs(audioData[i]);
                if (absValue > maxAmplitude)
                    maxAmplitude = absValue;
            }

            // 既に正規化されている場合はスキップ
            if (maxAmplitude <= 0f || Mathf.Approximately(maxAmplitude, targetPeak))
                return;

            var scale = targetPeak / maxAmplitude;
            for (var i = 0; i < audioData.Length; i++)
            {
                audioData[i] *= scale;
            }

            PiperLogger.LogDebug(
                $"Normalized audio (NativeArray) in-place: max amplitude {maxAmplitude:F3} -> {targetPeak:F3}");
        }

        /// <summary>
        /// 音声データを正規化し、新しい配列で返す。元のデータは変更しない。
        /// </summary>
        /// <param name="audioData">音声データ</param>
        /// <param name="targetPeak">目標ピーク値（0-1）</param>
        /// <returns>正規化された音声データ</returns>
        public static float[] Normalize(float[] audioData, float targetPeak = 0.95f)
        {
            if (audioData == null)
                return Array.Empty<float>();
            if (audioData.Length == 0)
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
