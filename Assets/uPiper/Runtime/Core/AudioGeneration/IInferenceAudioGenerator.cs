using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.InferenceEngine;
using UnityEngine;

namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// Unity.InferenceEngineを使用した音声生成のインターフェース
    /// </summary>
    public interface IInferenceAudioGenerator : IDisposable
    {
        /// <summary>
        /// 音声生成モデルを初期化する
        /// </summary>
        /// <param name="modelAsset">ONNXモデルアセット</param>
        /// <param name="config">音声設定</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>初期化タスク</returns>
        public Task InitializeAsync(ModelAsset modelAsset, PiperVoiceConfig config, CancellationToken cancellationToken = default);

        /// <summary>
        /// 音素から音声を生成する
        /// </summary>
        /// <param name="phonemeIds">音素ID配列</param>
        /// <param name="lengthScale">長さスケール</param>
        /// <param name="noiseScale">ノイズスケール</param>
        /// <param name="noiseW">ノイズ幅</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>生成された音声データ</returns>
        public Task<float[]> GenerateAudioAsync(
            int[] phonemeIds,
            float lengthScale = 1.0f,
            float noiseScale = 0.667f,
            float noiseW = 0.8f,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 初期化されているかどうか
        /// </summary>
        public bool IsInitialized { get; }

        /// <summary>
        /// 現在のモデルのサンプルレート
        /// </summary>
        public int SampleRate { get; }
    }
}