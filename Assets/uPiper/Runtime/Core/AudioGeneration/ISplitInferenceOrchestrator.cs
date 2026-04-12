using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// 沈黙句分割付き音声生成のインターフェース。
    /// </summary>
    internal interface ISplitInferenceOrchestrator
    {
        /// <summary>
        /// 沈黙句分割付きで音声を生成する。
        /// 句ごとの推論結果（Audio + Durations）を結合した <see cref="InferenceOutput"/> を返す。
        /// </summary>
        /// <param name="phonemeIds">音素ID配列</param>
        /// <param name="prosodyFlat">Prosodyフラット配列 (stride=3), or null</param>
        /// <param name="phonemeSilence">音素→沈黙秒数マッピング</param>
        /// <param name="phonemeIdMap">音素→ID配列マッピング</param>
        /// <param name="sampleRate">サンプルレート (Hz)</param>
        /// <param name="lengthScale">話速スケール</param>
        /// <param name="noiseScale">ノイズスケール</param>
        /// <param name="noiseW">ノイズ幅</param>
        /// <param name="speakerId">スピーカーID</param>
        /// <param name="languageId">言語ID</param>
        /// <param name="progress">句単位の進捗コールバック (0.0〜1.0)。null許容。</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <remarks>
        /// Caller owns and must Dispose the returned <see cref="InferenceOutput"/>.
        /// Durations は句ごとの durations を結合した配列（句間無音は含まない）。
        /// 1句でも durations があれば HasDurations == true。
        /// </remarks>
        Task<InferenceOutput> GenerateWithSilenceSplitAsync(
            int[] phonemeIds,
            int[] prosodyFlat,
            IReadOnlyDictionary<string, float> phonemeSilence,
            IReadOnlyDictionary<string, int[]> phonemeIdMap,
            int sampleRate,
            float lengthScale = 1.0f,
            float noiseScale = 0.667f,
            float noiseW = 0.8f,
            int speakerId = 0,
            int languageId = 0,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default);
    }
}