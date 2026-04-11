using System;
using System.Collections.Generic;
using UnityEngine;

namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// タイミング情報付き音声合成結果。
    /// <c>IPiperTTS.SynthesizeWithTimingAsync</c> の戻り値。
    /// AudioClip と各音素の開始・終了時刻を保持する。
    /// </summary>
    /// <remarks>
    /// AudioClip の Destroy 責任は呼び出し元にある。
    /// このクラスは AudioClip を借用するのみ。
    /// </remarks>
    public sealed class SynthesisWithTimingResult
    {
        /// <summary>生成された AudioClip (22050Hz, float32)。</summary>
        public AudioClip AudioClip { get; }

        /// <summary>
        /// 各音素のタイミング情報。PAD/BOS/EOS を除く実音素のみ。
        /// モデルが durations テンソルを出力しない場合は null。
        /// </summary>
        public IReadOnlyList<PhonemeTimingEntry> Timings { get; }

        /// <summary>音声全体の長さ（秒）。AudioClip.length と等価。</summary>
        public float TotalDurationSeconds { get; }

        /// <summary>タイミング情報が利用可能かどうか。</summary>
        public bool HasTimings => Timings != null;

        /// <summary>
        /// SynthesisWithTimingResult を構築する。
        /// </summary>
        /// <param name="audioClip">生成された AudioClip。null 不可。</param>
        /// <param name="timings">
        /// 音素タイミングリスト。防御的コピーされる。
        /// モデル非対応時は null を渡す。
        /// </param>
        /// <param name="totalDurationSeconds">音声全体の長さ（秒）。</param>
        /// <exception cref="ArgumentNullException"><paramref name="audioClip"/> が null の場合。</exception>
        internal SynthesisWithTimingResult(
            AudioClip audioClip,
            IReadOnlyList<PhonemeTimingEntry> timings,
            float totalDurationSeconds)
        {
            AudioClip = audioClip
                ?? throw new ArgumentNullException(nameof(audioClip));
            Timings = timings != null
                ? new List<PhonemeTimingEntry>(timings).AsReadOnly()
                : null;
            TotalDurationSeconds = totalDurationSeconds;
        }
    }
}
