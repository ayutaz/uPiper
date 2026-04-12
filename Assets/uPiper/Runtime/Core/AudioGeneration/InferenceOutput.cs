using System;
using Unity.Collections;

namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// ONNX推論の生出力を保持する内部クラス。
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
    /// <para>
    /// <see cref="DetachAudio"/> / <see cref="DetachDurations"/> で所有権を呼び出し元に
    /// 移転できる。移転後のフィールドは <c>default</c> となり、<see cref="Dispose"/> では解放されない。
    /// </para>
    /// </remarks>
    internal sealed class InferenceOutput : IDisposable
    {
        private bool _disposed;
        private NativeArray<float> _audio;
        private NativeArray<float> _durations;

        /// <summary>
        /// 音声波形データ（float32, モノラル）。
        /// <see cref="InferenceAudioGenerator.ExtractResults"/> で確保された
        /// <c>Allocator.Persistent</c> の <see cref="NativeArray{T}"/>。
        /// <see cref="DetachAudio"/> 呼び出し後は <c>default</c> を返す。
        /// </summary>
        public NativeArray<float> Audio => _audio;

        /// <summary>
        /// 音素ごとのフレーム持続時間。
        /// モデルの <c>durations</c> 出力テンソル（shape: <c>[1, phoneme_length]</c>）から読み取った値。
        /// <c>durations[i]</c> は <c>phonemeIds[i]</c> に対応するスペクトログラムフレーム数。
        /// <see cref="DetachDurations"/> 呼び出し後は <c>default</c> を返す。
        /// </summary>
        /// <remarks>
        /// モデルが <c>durations</c> 出力を持たない場合は <c>IsCreated == false</c>。
        /// <see cref="HasDurations"/> で判定可能。
        /// </remarks>
        public NativeArray<float> Durations => _durations;

        /// <summary>
        /// durations データが利用可能かどうか。
        /// </summary>
        public bool HasDurations => _durations.IsCreated;

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
            _audio = audio;
            _durations = durations;
        }

        /// <summary>
        /// Audio の所有権を呼び出し元に移転する。
        /// 移転後、<see cref="Audio"/> は <c>default</c> を返し、
        /// <see cref="Dispose"/> は Audio を解放しない。
        /// </summary>
        internal NativeArray<float> DetachAudio()
        {
            var a = _audio;
            _audio = default;
            return a;
        }

        /// <summary>
        /// Durations の所有権を呼び出し元に移転する。
        /// 移転後、<see cref="Durations"/> は <c>default</c> を返し、
        /// <see cref="Dispose"/> は Durations を解放しない。
        /// </summary>
        internal NativeArray<float> DetachDurations()
        {
            var d = _durations;
            _durations = default;
            return d;
        }

        /// <summary>
        /// <see cref="Audio"/> と <see cref="Durations"/> の両 <see cref="NativeArray{T}"/> を
        /// 安全に破棄する。未作成（<c>IsCreated == false</c>）の配列は無視する。
        /// <see cref="DetachAudio"/> / <see cref="DetachDurations"/> で移転済みのフィールドは
        /// <c>default</c> のため破棄されない。
        /// 二重 Dispose は安全に無視される。
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_audio.IsCreated) _audio.Dispose();
            if (_durations.IsCreated) _durations.Dispose();
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        ~InferenceOutput()
        {
            if (_audio.IsCreated || _durations.IsCreated)
            {
                UnityEngine.Debug.LogWarning(
                    "[InferenceOutput] Dispose was not called. NativeArray may have leaked.");
            }
        }
#endif
    }
}