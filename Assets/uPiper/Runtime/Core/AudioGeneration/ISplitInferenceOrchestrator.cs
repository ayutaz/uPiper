using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;

namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// 沈黙句分割付き音声生成のインターフェース。
    /// </summary>
    internal interface ISplitInferenceOrchestrator
    {
        /// <summary>
        /// 沈黙句分割付きで音声を生成する。
        /// </summary>
        /// <remarks>Caller owns and must Dispose the returned NativeArray.</remarks>
        Task<NativeArray<float>> GenerateWithSilenceSplitAsync(
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
            CancellationToken cancellationToken = default);
    }
}