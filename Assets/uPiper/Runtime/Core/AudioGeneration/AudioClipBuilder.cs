using System;
using System.Linq;
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

        /// <summary>
        /// 音声データにフェードイン/フェードアウトを適用する
        /// </summary>
        /// <param name="audioData">音声データ</param>
        /// <param name="fadeInSamples">フェードインサンプル数</param>
        /// <param name="fadeOutSamples">フェードアウトサンプル数</param>
        /// <returns>処理された音声データ</returns>
        public float[] ApplyFade(float[] audioData, int fadeInSamples = 0, int fadeOutSamples = 0)
        {
            if (audioData == null || audioData.Length == 0)
                return audioData;

            var processedData = (float[])audioData.Clone();

            // フェードイン
            if (fadeInSamples > 0)
            {
                var actualFadeIn = Mathf.Min(fadeInSamples, audioData.Length / 2);
                for (var i = 0; i < actualFadeIn; i++)
                {
                    var factor = (float)i / actualFadeIn;
                    processedData[i] *= factor;
                }
            }

            // フェードアウト
            if (fadeOutSamples > 0)
            {
                var actualFadeOut = Mathf.Min(fadeOutSamples, audioData.Length / 2);
                var startIndex = audioData.Length - actualFadeOut;
                for (var i = 0; i < actualFadeOut; i++)
                {
                    var factor = 1f - ((float)i / actualFadeOut);
                    processedData[startIndex + i] *= factor;
                }
            }

            return processedData;
        }

        /// <summary>
        /// 複数の音声データを結合する
        /// </summary>
        /// <param name="audioChunks">音声データの配列</param>
        /// <param name="gapSamples">チャンク間のギャップ（サンプル数）</param>
        /// <returns>結合された音声データ</returns>
        public float[] ConcatenateAudio(float[][] audioChunks, int gapSamples = 0)
        {
            if (audioChunks == null || audioChunks.Length == 0)
                return Array.Empty<float>();

            // 有効なチャンクのみをフィルタリング
            var validChunks = audioChunks.Where(chunk => chunk != null && chunk.Length > 0).ToArray();
            if (validChunks.Length == 0)
                return Array.Empty<float>();

            // 合計サンプル数を計算
            var totalSamples = validChunks.Sum(chunk => chunk.Length) + (validChunks.Length - 1) * Math.Max(0, gapSamples);
            var concatenated = new float[totalSamples];

            // データをコピー
            var currentIndex = 0;
            for (var i = 0; i < validChunks.Length; i++)
            {
                Array.Copy(validChunks[i], 0, concatenated, currentIndex, validChunks[i].Length);
                currentIndex += validChunks[i].Length;

                // ギャップを追加（最後のチャンク以外）
                if (i < validChunks.Length - 1 && gapSamples > 0)
                {
                    currentIndex += gapSamples; // float配列は既に0で初期化されている
                }
            }

            PiperLogger.LogDebug($"Concatenated {validChunks.Length} audio chunks into {totalSamples} samples");
            return concatenated;
        }
    }
}