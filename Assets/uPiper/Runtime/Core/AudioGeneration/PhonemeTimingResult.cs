namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// 1音素のタイミング情報。
    /// ONNX モデルの <c>durations</c> 出力テンソルから算出される。
    /// </summary>
    /// <remarks>
    /// piper-plus <c>TimingWriter.PhonemeTimingEntry</c> と同等のデータを保持する。
    /// 時刻は発話先頭からの秒数。ミリ秒変換はユーザー側の責務。
    /// </remarks>
    public readonly struct PhonemeTimingEntry
    {
        /// <summary>人間可読な音素文字列（"a:", "k", "N_m" 等）。</summary>
        public string Phoneme { get; }

        /// <summary>開始時刻（秒）。発話先頭からの経過時間。</summary>
        public float StartSeconds { get; }

        /// <summary>終了時刻（秒）。発話先頭からの経過時間。</summary>
        public float EndSeconds { get; }

        /// <summary>
        /// 持続時間（秒）。<c>EndSeconds - StartSeconds</c> と等価な算出プロパティ。
        /// API利便性のため冗長に保持する。
        /// </summary>
        public float DurationSeconds => EndSeconds - StartSeconds;

        /// <summary>
        /// PhonemeTimingEntry を構築する。
        /// </summary>
        /// <param name="phoneme">人間可読な音素文字列。</param>
        /// <param name="startSeconds">開始時刻（秒）。</param>
        /// <param name="endSeconds">終了時刻（秒）。</param>
        internal PhonemeTimingEntry(string phoneme, float startSeconds, float endSeconds)
        {
            Phoneme = phoneme;
            StartSeconds = startSeconds;
            EndSeconds = endSeconds;
        }
    }
}