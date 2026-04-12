using System;
using System.Collections.Generic;
using uPiper.Core;
using uPiper.Core.AudioGeneration;

namespace uPiper.Tests.Editor.AudioGeneration
{
    /// <summary>
    /// 複数テストクラスで共有されるヘルパーメソッド・ユーティリティ。
    /// </summary>
    internal static class PhonemeTimingTestHelpers
    {
        /// <summary>
        /// PhonemeEncoder が動作する最小限の PhonemeIdMap を構築する。
        /// PAD=0, BOS=1, EOS=2 の特殊トークンに加え、基本音素を含む。
        /// </summary>
        internal static Dictionary<string, int[]> CreateMinimalPhonemeIdMap()
        {
            return new Dictionary<string, int[]>
            {
                ["_"] = new[] { 0 },  // PAD
                ["^"] = new[] { 1 },  // BOS
                ["$"] = new[] { 2 },  // EOS
                ["a"] = new[] { 3 },
                ["i"] = new[] { 4 },
                ["u"] = new[] { 5 },
                ["e"] = new[] { 6 },
                ["o"] = new[] { 7 },
                ["k"] = new[] { 8 },
                ["s"] = new[] { 9 },
                ["t"] = new[] { 10 },
                ["n"] = new[] { 11 },
                ["h"] = new[] { 12 },
                ["m"] = new[] { 13 },
                ["r"] = new[] { 14 },
                ["w"] = new[] { 15 },
                ["N"] = new[] { 16 },
                [" "] = new[] { 17 },
            };
        }

        /// <summary>
        /// テスト用 IPiperConfigReadOnly を生成する。
        /// ToValidated() で WorkerThreads=0 が自動検出されるため、手動で1以上を設定する。
        /// </summary>
        internal static IPiperConfigReadOnly CreateValidatedConfig(
            bool enableSilence, string silenceSpec = "_ 0.5")
        {
            var piperConfig = new PiperConfig
            {
                EnablePhonemeSilence = enableSilence,
                PhonemeSilenceSpec = silenceSpec,
                WorkerThreads = 1,
            };
            return piperConfig.ToValidated();
        }
    }

    /// <summary>
    /// テスト用の同期的 IProgress 実装。
    /// <see cref="Progress{T}"/> は SynchronizationContext.Post を使用するため、
    /// EditMode テストではコールバックの発火タイミングが不定。
    /// このクラスは Report() を同期的に呼び出す。
    /// </summary>
    internal sealed class SynchronousProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;

        public SynchronousProgress(Action<T> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public void Report(T value)
        {
            _handler(value);
        }
    }
}