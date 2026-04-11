using System;
using Unity.Collections;

namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// ONNX推論の生出力を保持する内部構造体。
    /// <c>Audio</c>（音声波形）と <c>Durations</c>（音素フレーム持続時間）の
    /// 2つの <see cref="NativeArray{T}"/> をまとめて管理し、
    /// <see cref="IDisposable"/> パターンで安全に破棄する。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Durations</c> はモデルが <c>durations</c> 出力テンソルを持つ場合のみ有効。
    /// 非対応モデルでは <c>Durations.IsCreated == false</c> となる。
    /// </para>
    /// <para>
    /// 両 <see cref="NativeArray{T}"/> は <c>Allocator.Persistent</c> で確保されることを想定。
    /// 呼び出し元は <c>using</c> ステートメントまたは明示的な <see cref="Dispose"/> で
    /// リソースを解放すること。
    /// </para>
    /// </remarks>
    internal readonly struct InferenceOutput : IDisposable
    {
        /// <summary>
        /// 音声波形データ（float32, モノラル）。
        /// <see cref="InferenceAudioGenerator.ExtractResults"/> で確保された
        /// <c>Allocator.Persistent</c> の <see cref="NativeArray{T}"/>。
        /// </summary>
        public NativeArray<float> Audio { get; }

        /// <summary>
        /// 音素ごとのフレーム持続時間。
        /// モデルの <c>durations</c> 出力テンソル（shape: <c>[1, phoneme_length]</c>）から読み取った値。
        /// <c>durations[i]</c> は <c>phonemeIds[i]</c> に対応するスペクトログラムフレーム数。
        /// </summary>
        /// <remarks>
        /// モデルが <c>durations</c> 出力を持たない場合は <c>IsCreated == false</c>。
        /// <see cref="HasDurations"/> で判定可能。
        /// </remarks>
        public NativeArray<float> Durations { get; }

        /// <summary>
        /// durations データが利用可能かどうか。
        /// </summary>
        public bool HasDurations => Durations.IsCreated;

        /// <summary>
        /// InferenceOutput を構築する。
        /// </summary>
        /// <param name="audio">
        /// 音声波形データ。<c>Allocator.Persistent</c> で確保された <see cref="NativeArray{T}"/>。
        /// </param>
        /// <param name="durations">
        /// 音素フレーム持続時間。モデル非対応時は <c>default</c> を渡す。
        /// </param>
        internal InferenceOutput(NativeArray<float> audio, NativeArray<float> durations)
        {
            Audio = audio;
            Durations = durations;
        }

        /// <summary>
        /// <see cref="Audio"/> と <see cref="Durations"/> の両 <see cref="NativeArray{T}"/> を
        /// 安全に破棄する。未作成（<c>IsCreated == false</c>）の配列は無視する。
        /// </summary>
        public void Dispose()
        {
            if (Audio.IsCreated)
                Audio.Dispose();
            if (Durations.IsCreated)
                Durations.Dispose();
        }
    }
}
