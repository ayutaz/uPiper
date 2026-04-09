using System;
using Unity.Collections;
using UnityEngine;

namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// 短いテキスト合成の品質低下を軽減するユーティリティ。
    /// <para>
    /// VITSモデルでは音素ID列が短い（約40トークン未満）場合、
    /// Duration Predictorが不安定になりゼロ長やノイズの多い音声を生成する。
    /// 2つの補完的な戦略で対処する：
    /// <list type="bullet">
    ///   <item><b>Strategy A</b> (<see cref="PadPhonemeIds"/>/<see cref="TrimSilence"/>):
    ///     短い音素列をPAD (ID=0) で <see cref="MinPhonemeIds"/> まで充填し、
    ///     推論後に無音マージンをトリムする。</item>
    ///   <item><b>Strategy B</b> (<see cref="AdjustScales"/>):
    ///     短い列に対してnoiseScale/noiseWを低減し、
    ///     確率的Duration Predictorを安定化する。</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <remarks>
    /// piper-plus ShortTextProcessor (short-text-contract.toml) のC# Unity移植。
    /// int[] 音素ID / NativeArray&lt;float&gt; オーディオに対応。
    /// </remarks>
    internal static class ShortTextProcessor
    {
        // ------------------------------------------------------------------
        // Constants — piper-plus short-text-contract.toml 準拠
        // ------------------------------------------------------------------

        /// <summary>
        /// パディングが適用される音素ID数の下限。
        /// </summary>
        internal const int MinPhonemeIds = 40;

        /// <summary>
        /// 無音判定のRMS閾値（Strategy A トリム）。
        /// </summary>
        internal const float TrimThresholdRms = 0.01f;

        /// <summary>
        /// トリム後に保持する最小サンプル数（22050 Hz × 0.1 s）。
        /// </summary>
        internal const int TrimMinSamples = 2205;

        /// <summary>
        /// RMS無音検出に使用するウィンドウサイズ（サンプル数）。
        /// </summary>
        internal const int TrimWindowSize = 256;

        /// <summary>
        /// パディングに使用するPADトークンID。PhonemeEncoder の '_' トークンに対応。
        /// </summary>
        internal const int PadId = 0;

        /// <summary>noiseScaleの最小縮小比率。piper-plus short-text-contract.toml: noise_scale_min_ratio</summary>
        internal const float NoiseScaleMinRatio = 0.5f;

        /// <summary>noiseWの最小縮小比率。piper-plus short-text-contract.toml: noise_w_min_ratio</summary>
        internal const float NoiseWMinRatio = 0.4f;

        // ------------------------------------------------------------------
        // Strategy A: Silence Padding
        // ------------------------------------------------------------------

        /// <summary>
        /// 音素ID列がパディングを必要とするほど短いかを判定する。
        /// </summary>
        internal static bool NeedsPadding(int[] phonemeIds)
        {
            if (phonemeIds == null) throw new ArgumentNullException(nameof(phonemeIds));
            return phonemeIds.Length < MinPhonemeIds;
        }

        /// <summary>
        /// 短い音素ID列をPAD (ID=0) で充填する。
        /// BOS直後とEOS直前に均等にPADを分配し、
        /// 長さが <see cref="MinPhonemeIds"/> 以上になるようにする。
        /// Prosody配列がある場合はゼロ値で拡張する。
        /// </summary>
        /// <param name="phonemeIds">元の音素ID列。</param>
        /// <param name="prosodyFlat">
        /// フラットProsody配列（長さ = phonemeIds.Length * 3）。nullの場合はProsodyなし。
        /// </param>
        /// <returns>パディングされた音素IDと（オプションで）パディングされたProsody配列。</returns>
        internal static (int[] PaddedIds, int[] PaddedProsody) PadPhonemeIds(
            int[] phonemeIds, int[] prosodyFlat)
        {
            if (phonemeIds == null) throw new ArgumentNullException(nameof(phonemeIds));
            if (phonemeIds.Length < 2)
                return (phonemeIds, prosodyFlat); // BOS+EOS未満は安全にスキップ
            if (phonemeIds.Length >= MinPhonemeIds)
                return (phonemeIds, prosodyFlat);

            var deficit = MinPhonemeIds - phonemeIds.Length;
            var afterBos = deficit / 2;
            var beforeEos = deficit - afterBos;

            var newLength = phonemeIds.Length + deficit;
            var padded = new int[newLength];

            // BOS
            padded[0] = phonemeIds[0];

            // BOS 直後にPADを挿入
            for (var i = 1; i <= afterBos; i++)
                padded[i] = PadId;

            // Body（BOS〜EOS間）
            var bodyStart = 1;
            var bodyEnd = phonemeIds.Length - 1;
            var bodyLength = bodyEnd - bodyStart;
            Array.Copy(phonemeIds, bodyStart, padded, 1 + afterBos, bodyLength);

            // EOS 直前にPADを挿入
            var eosInsertStart = 1 + afterBos + bodyLength;
            for (var i = 0; i < beforeEos; i++)
                padded[eosInsertStart + i] = PadId;

            // EOS
            padded[newLength - 1] = phonemeIds[phonemeIds.Length - 1];

            // Prosody を拡張（PAD位置はゼロ値で埋める）
            int[] paddedProsody = null;
            if (prosodyFlat is not null && prosodyFlat.Length == phonemeIds.Length * 3)
            {
                paddedProsody = new int[newLength * 3];

                // BOS prosody
                paddedProsody[0] = prosodyFlat[0];
                paddedProsody[1] = prosodyFlat[1];
                paddedProsody[2] = prosodyFlat[2];

                // Body prosody
                var bodyProsodyOffset = (1 + afterBos) * 3;
                Array.Copy(prosodyFlat, bodyStart * 3, paddedProsody, bodyProsodyOffset, bodyLength * 3);

                // EOS prosody
                var eosSrcOffset = (phonemeIds.Length - 1) * 3;
                var eosDstOffset = (newLength - 1) * 3;
                paddedProsody[eosDstOffset] = prosodyFlat[eosSrcOffset];
                paddedProsody[eosDstOffset + 1] = prosodyFlat[eosSrcOffset + 1];
                paddedProsody[eosDstOffset + 2] = prosodyFlat[eosSrcOffset + 2];
            }

            return (padded, paddedProsody);
        }

        // ------------------------------------------------------------------
        // Strategy A: Post-inference silence trimming
        // ------------------------------------------------------------------

        /// <summary>
        /// NativeArray&lt;float&gt; オーディオの先頭・末尾の無音をRMSウィンドウでトリムする。
        /// 少なくとも <see cref="TrimMinSamples"/> を保持する。
        /// トリムが必要な場合は新しいNativeArrayを返し、元のNativeArrayをDisposeする。
        /// トリム不要の場合は元のNativeArrayをそのまま返す。
        /// </summary>
        /// <param name="audio">生の float32 オーディオサンプル。</param>
        /// <returns>トリムされたオーディオ。呼び出し元がDisposeを管理する。</returns>
        internal static NativeArray<float> TrimSilence(NativeArray<float> audio)
        {
            if (!audio.IsCreated) return audio;
            if (audio.Length <= TrimMinSamples)
                return audio;

            var totalWindows = audio.Length / TrimWindowSize;
            if (totalWindows < 2)
                return audio;

            // 先頭から最初の非無音ウィンドウを探す
            var firstNonSilent = 0;
            var foundFront = false;
            for (var w = 0; w < totalWindows; w++)
            {
                if (WindowRms(audio, w * TrimWindowSize) > TrimThresholdRms)
                {
                    firstNonSilent = w * TrimWindowSize;
                    foundFront = true;
                    break;
                }
            }

            if (!foundFront)
            {
                firstNonSilent = 0;
            }

            // 末尾から最後の非無音ウィンドウを探す
            var lastNonSilentEnd = audio.Length;
            var foundBack = false;

            var tailStart = totalWindows * TrimWindowSize;
            if (tailStart < audio.Length && WindowRms(audio, tailStart) > TrimThresholdRms)
            {
                lastNonSilentEnd = audio.Length;
                foundBack = true;
            }

            if (!foundBack)
            {
                for (var w = totalWindows - 1; w >= 0; w--)
                {
                    if (WindowRms(audio, w * TrimWindowSize) > TrimThresholdRms)
                    {
                        lastNonSilentEnd = Math.Min(audio.Length, (w + 1) * TrimWindowSize);
                        foundBack = true;
                        break;
                    }
                }
            }

            if (!foundBack)
            {
                lastNonSilentEnd = firstNonSilent;
            }

            // 最小長を確保
            var trimmedLength = lastNonSilentEnd - firstNonSilent;
            if (trimmedLength < TrimMinSamples)
            {
                var midpoint = (firstNonSilent + lastNonSilentEnd) / 2;
                firstNonSilent = Math.Max(0, midpoint - TrimMinSamples / 2);
                lastNonSilentEnd = Math.Min(audio.Length, firstNonSilent + TrimMinSamples);
                firstNonSilent = Math.Max(0, lastNonSilentEnd - TrimMinSamples);
            }

            if (firstNonSilent == 0 && lastNonSilentEnd == audio.Length)
                return audio;

            // トリムされた新しい NativeArray を作成
            var resultLength = lastNonSilentEnd - firstNonSilent;
            var trimmed = new NativeArray<float>(resultLength, Allocator.Persistent);
            try
            {
                NativeArray<float>.Copy(audio, firstNonSilent, trimmed, 0, resultLength);
                audio.Dispose();
                return trimmed;
            }
            catch
            {
                trimmed.Dispose();
                throw;
            }
        }

        /// <summary>
        /// <paramref name="offset"/> から <see cref="TrimWindowSize"/> サンプル分のRMSを計算する。
        /// </summary>
        private static float WindowRms(NativeArray<float> audio, int offset)
        {
            var end = Math.Min(offset + TrimWindowSize, audio.Length);
            var count = end - offset;
            if (count <= 0)
                return 0f;

            var sumSq = 0f;
            for (var i = offset; i < end; i++)
                sumSq += audio[i] * audio[i];

            return Mathf.Sqrt(sumSq / count);
        }

        // ------------------------------------------------------------------
        // Strategy B: Dynamic Scales Adjustment
        // ------------------------------------------------------------------

        /// <summary>
        /// 短い音素列に対してnoiseScaleとnoiseWを動的に調整する。
        /// 元の値は変更されず、調整されたコピーを返す。
        /// </summary>
        /// <param name="phonemeIdCount">音素ID列の長さ。</param>
        /// <param name="noiseScale">元のnoiseScale。</param>
        /// <param name="noiseW">元のnoiseW。</param>
        /// <returns>調整された (noiseScale, noiseW) タプル。</returns>
        internal static (float NoiseScale, float NoiseW) AdjustScales(
            int phonemeIdCount, float noiseScale, float noiseW)
        {
            if (phonemeIdCount >= MinPhonemeIds)
                return (noiseScale, noiseW);

            var ratio = Mathf.Clamp01((float)phonemeIdCount / MinPhonemeIds);
            var adjustedNoiseScale = noiseScale * Mathf.Max(NoiseScaleMinRatio, ratio);
            var adjustedNoiseW = noiseW * Mathf.Max(NoiseWMinRatio, ratio);

            return (adjustedNoiseScale, adjustedNoiseW);
        }
    }
}
