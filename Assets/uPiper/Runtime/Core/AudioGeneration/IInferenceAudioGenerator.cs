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
        /// PiperConfig指定で音声生成モデルを初期化する
        /// </summary>
        /// <param name="modelAsset">ONNXモデルアセット</param>
        /// <param name="config">音声設定</param>
        /// <param name="piperConfig">Piper全体設定（バックエンド選択等）</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>初期化タスク</returns>
        public Task InitializeAsync(ModelAsset modelAsset, PiperVoiceConfig config, PiperConfig piperConfig, CancellationToken cancellationToken = default);

        /// <summary>
        /// 音素から音声を生成する（Prosody対応統合版）。
        /// prosodyA1/A2/A3がnullの場合はProsodyなしで推論する。
        /// </summary>
        public Task<float[]> GenerateAudioAsync(
            int[] phonemeIds,
            int[] prosodyA1 = null,
            int[] prosodyA2 = null,
            int[] prosodyA3 = null,
            float lengthScale = 1.0f,
            float noiseScale = 0.667f,
            float noiseW = 0.8f,
            int speakerId = 0,
            int languageId = 0,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 初期化されているかどうか
        /// </summary>
        public bool IsInitialized { get; }

        /// <summary>
        /// 現在のモデルのサンプルレート
        /// </summary>
        public int SampleRate { get; }

        /// <summary>
        /// モデルがprosody_featuresをサポートするかどうか
        /// </summary>
        public bool SupportsProsody { get; }

        /// <summary>
        /// モデルがマルチスピーカー（sid入力）をサポートするかどうか
        /// </summary>
        public bool SupportsMultiSpeaker { get; }

        /// <summary>
        /// モデルが多言語（lid入力）をサポートするかどうか
        /// </summary>
        public bool SupportsLanguageId { get; }
    }
}