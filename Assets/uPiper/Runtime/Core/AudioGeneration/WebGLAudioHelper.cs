#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
using uPiper.Core.Logging;

namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// WebGL 専用の音声処理ヘルパークラス
    /// 特定のパターンで問題が発生する文章を検出し、追加の調整を行う
    /// </summary>
    public static class WebGLAudioHelper
    {
        /// <summary>
        /// 特別な処理が必要なテキストかどうかを判定
        /// </summary>
        public static bool NeedsSpecialProcessing(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
            
            // 「ち」を含む文章は特別な処理が必要
            // これらの文字が含まれると推論精度に問題が発生しやすい
            var problematicPatterns = new[] 
            { 
                "ち", "ちゃ", "ちゅ", "ちょ", "ちぇ",  // ち行
                "つ", "つぁ", "つぃ", "つぇ", "つぉ"   // つ行（同様の問題がある可能性）
            };
            
            foreach (var pattern in problematicPatterns)
            {
                if (text.Contains(pattern))
                {
                    PiperLogger.LogInfo($"[WebGLAudioHelper] Detected problematic pattern '{pattern}' in text");
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// 特別な処理が必要な場合のパラメータ調整係数を取得
        /// </summary>
        public static (float noiseScaleFactor, float lengthScaleFactor, float noiseWFactor) GetSpecialProcessingFactors(string text)
        {
            if (!NeedsSpecialProcessing(text))
            {
                return (1.0f, 1.0f, 1.0f);
            }
            
            // 「こんにちは」の場合は特に慎重な調整
            if (text == "こんにちは")
            {
                PiperLogger.LogInfo("[WebGLAudioHelper] Applying special adjustment for 'こんにちは'");
                return (0.6f, 0.9f, 0.6f);
            }
            
            // その他の「ち」を含む文章
            if (text.Contains("ち"))
            {
                PiperLogger.LogInfo("[WebGLAudioHelper] Applying adjustment for text containing 'ち'");
                return (0.7f, 0.92f, 0.7f);
            }
            
            // デフォルトの調整
            return (0.8f, 0.95f, 0.8f);
        }
        
        /// <summary>
        /// 音素IDの前処理
        /// WebGL での精度問題を軽減するため、パディングを追加
        /// </summary>
        public static int[] PreprocessPhonemeIds(int[] phonemeIds)
        {
            // 短すぎる入力の場合はパディングを追加
            if (phonemeIds.Length < 5)
            {
                PiperLogger.LogInfo($"[WebGLAudioHelper] Adding padding to short input (length: {phonemeIds.Length})");
                var paddedIds = new int[phonemeIds.Length + 2];
                paddedIds[0] = 0; // PAD
                Array.Copy(phonemeIds, 0, paddedIds, 1, phonemeIds.Length);
                paddedIds[paddedIds.Length - 1] = 0; // PAD
                return paddedIds;
            }
            
            return phonemeIds;
        }
        
        /// <summary>
        /// 音声データの後処理
        /// 特定パターンで発生する異常を検出して修正
        /// </summary>
        public static float[] PostprocessAudioData(float[] audioData, string originalText)
        {
            if (audioData == null || audioData.Length == 0)
                return audioData;
            
            // 「ち」を含む文章の場合、追加のフィルタリング
            if (NeedsSpecialProcessing(originalText))
            {
                PiperLogger.LogInfo("[WebGLAudioHelper] Applying special post-processing for problematic pattern");
                
                // スパイクノイズの除去
                audioData = RemoveSpikes(audioData);
                
                // 平滑化フィルタ
                audioData = ApplySmoothingFilter(audioData);
            }
            
            return audioData;
        }
        
        /// <summary>
        /// スパイクノイズを除去
        /// </summary>
        private static float[] RemoveSpikes(float[] audioData)
        {
            const float spikeThreshold = 3.0f; // 標準偏差の3倍を超える値をスパイクとする
            
            // 標準偏差を計算
            float mean = audioData.Average();
            float stdDev = (float)Math.Sqrt(audioData.Select(x => Math.Pow(x - mean, 2)).Average());
            
            float upperBound = mean + spikeThreshold * stdDev;
            float lowerBound = mean - spikeThreshold * stdDev;
            
            int spikeCount = 0;
            for (int i = 0; i < audioData.Length; i++)
            {
                if (audioData[i] > upperBound || audioData[i] < lowerBound)
                {
                    // スパイクを前後の値の平均で置換
                    float replacement = mean;
                    if (i > 0 && i < audioData.Length - 1)
                    {
                        replacement = (audioData[i - 1] + audioData[i + 1]) / 2f;
                    }
                    else if (i > 0)
                    {
                        replacement = audioData[i - 1];
                    }
                    else if (i < audioData.Length - 1)
                    {
                        replacement = audioData[i + 1];
                    }
                    
                    audioData[i] = replacement;
                    spikeCount++;
                }
            }
            
            if (spikeCount > 0)
            {
                PiperLogger.LogInfo($"[WebGLAudioHelper] Removed {spikeCount} spike(s) from audio");
            }
            
            return audioData;
        }
        
        /// <summary>
        /// 平滑化フィルタを適用
        /// </summary>
        private static float[] ApplySmoothingFilter(float[] audioData)
        {
            const int windowSize = 3; // 移動平均のウィンドウサイズ
            
            float[] smoothed = new float[audioData.Length];
            
            for (int i = 0; i < audioData.Length; i++)
            {
                int start = Math.Max(0, i - windowSize / 2);
                int end = Math.Min(audioData.Length - 1, i + windowSize / 2);
                
                float sum = 0;
                int count = 0;
                for (int j = start; j <= end; j++)
                {
                    sum += audioData[j];
                    count++;
                }
                
                smoothed[i] = sum / count;
            }
            
            PiperLogger.LogDebug("[WebGLAudioHelper] Applied smoothing filter");
            return smoothed;
        }
    }
}
#endif