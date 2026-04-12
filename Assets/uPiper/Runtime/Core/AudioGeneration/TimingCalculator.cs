using System;
using System.Collections.Generic;
using uPiper.Core.Logging;
using uPiper.Core.Phonemizers.Multilingual;

namespace uPiper.Core.AudioGeneration
{
    /// <summary>
    /// ONNX モデルの durations テンソルから各音素の開始・終了時刻を算出する。
    /// piper-plus TimingWriter.CalculateTiming の Unity 向け移植。
    /// </summary>
    public static class TimingCalculator
    {
        // 特殊トークン ID (PhonemeEncoder と一致)
        private const int PadId = 0;
        private const int BosId = 1;
        private const int EosId = 2;

        // ASCII フォールバック用キャッシュ (piper-plus s_asciiStrings と同等)
        // ResolvePhonemeString で未知 ID のフォールバック時に毎回 ToString() を避ける
        private static readonly string[] AsciiStrings = InitAsciiStrings();

        private static string[] InitAsciiStrings()
        {
            var arr = new string[128];
            for (int i = 0; i < 128; i++)
                arr[i] = ((char)i).ToString();
            return arr;
        }

        /// <summary>
        /// 音素 ID 配列と durations 配列からタイミング情報を算出する。
        /// </summary>
        /// <param name="phonemeIds">
        /// モデルに入力した音素 ID 配列。PhonemeEncoder.Encode() の出力と同一順序。
        /// </param>
        /// <param name="durations">
        /// ONNX モデルの durations 出力テンソル。
        /// durations[i] は phonemeIds[i] のスペクトログラムフレーム数。
        /// </param>
        /// <param name="phonemeIdMap">
        /// モデル設定 (onnx.json) 由来の phoneme_id_map。
        /// キー: 音素文字列、値: int[] (先頭要素が ID)。
        /// </param>
        /// <param name="puaTokenMapper">
        /// PUA 逆引き用マッパー。PUA 文字を多文字トークンに変換する。
        /// null の場合は PUA 逆引きをスキップする。
        /// </param>
        /// <param name="sampleRate">
        /// オーディオサンプルレート (Hz)。通常 22050。
        /// </param>
        /// <param name="hopSize">
        /// スペクトログラムのホップサイズ (サンプル数)。VITS デフォルト: 256。
        /// </param>
        /// <returns>
        /// PAD/BOS/EOS を除く実音素のタイミングリスト。
        /// 空入力の場合は空リストを返す。
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// phonemeIds, durations, phonemeIdMap のいずれかが null の場合。
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// sampleRate または hopSize が 0 以下の場合。
        /// </exception>
        public static List<PhonemeTimingEntry> Calculate(
            int[] phonemeIds,
            float[] durations,
            Dictionary<string, int[]> phonemeIdMap,
            PuaTokenMapper puaTokenMapper,
            int sampleRate,
            int hopSize = 256)
        {
            // --- バリデーション ---
            if (phonemeIds == null)
                throw new ArgumentNullException(nameof(phonemeIds));
            if (durations == null)
                throw new ArgumentNullException(nameof(durations));
            if (phonemeIdMap == null)
                throw new ArgumentNullException(nameof(phonemeIdMap));
            if (sampleRate <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(sampleRate),
                    sampleRate,
                    "Sample rate must be positive.");
            if (hopSize <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(hopSize),
                    hopSize,
                    "Hop size must be positive.");

            // 空入力 → 空リスト
            if (phonemeIds.Length == 0 || durations.Length == 0)
                return new List<PhonemeTimingEntry>();

            // --- 逆引き辞書構築 ---
            var reverseIdMap = BuildReverseIdMap(phonemeIdMap, puaTokenMapper);

            // --- タイミング計算 ---
            float frameLength = (float)hopSize / sampleRate;
            int count = Math.Min(phonemeIds.Length, durations.Length);
            var entries = new List<PhonemeTimingEntry>(count);
            float currentTime = 0f;

            if (phonemeIds.Length != durations.Length)
            {
                PiperLogger.LogWarning(
                    $"[TimingCalculator] phonemeIds.Length ({phonemeIds.Length}) != " +
                    $"durations.Length ({durations.Length}). " +
                    $"Processing min({phonemeIds.Length}, {durations.Length}) = {count} elements.");
            }

            for (int i = 0; i < count; i++)
            {
                int id = phonemeIds[i];
                float frameDuration = durations[i];

                // 負値・NaN・Infinity はクランプ（ONNX テンソル不正時の全エントリ NaN 汚染を防止）
                if (frameDuration < 0f || float.IsNaN(frameDuration) || float.IsInfinity(frameDuration))
                {
                    PiperLogger.LogWarning(
                        $"[TimingCalculator] Invalid duration ({frameDuration}) " +
                        $"at index {i} (phonemeId={id}). Clamping to 0.");
                    frameDuration = 0f;
                }

                // PAD(0) / BOS(1) / EOS(2) はスキップ（時間カーソルは進行）
                if (id is PadId or BosId or EosId)
                {
                    currentTime += frameDuration * frameLength;
                    continue;
                }

                // 通常音素: タイミングエントリ生成
                float startTime = currentTime;
                currentTime += frameDuration * frameLength;
                float endTime = currentTime;

                string phonemeStr = ResolvePhonemeString(id, reverseIdMap);

                entries.Add(new PhonemeTimingEntry(
                    phonemeStr,
                    startTime,
                    endTime));
            }

            return entries;
        }

        /// <summary>
        /// phonemeIdMap から逆引き辞書 (ID → 表示用音素文字列) を構築する。
        /// PUA 文字キーは puaTokenMapper で多文字トークンに変換する。
        /// </summary>
        internal static Dictionary<int, string> BuildReverseIdMap(
            Dictionary<string, int[]> phonemeIdMap,
            PuaTokenMapper puaTokenMapper)
        {
            var reverse = new Dictionary<int, string>(phonemeIdMap.Count);
            foreach (var (phonemeStr, ids) in phonemeIdMap)
            {
                if (ids is not { Length: > 0 })
                    continue;

                string display = phonemeStr;

                // PUA 単文字キーを多文字トークンに逆引き
                // PUA コードポイント (U+E000-U+F8FF) は常に BMP 内なので
                // Length==1 で正しく PUA 文字を判別できる（サロゲートペア不要）
                if (phonemeStr.Length == 1 && puaTokenMapper != null)
                {
                    var token = puaTokenMapper.UnmapChar(phonemeStr[0]);
                    if (token != null)
                    {
                        display = token;
                    }
                }

                reverse.TryAdd(ids[0], display);
            }

            return reverse;
        }

        /// <summary>
        /// 音素 ID を表示用文字列に解決する。
        /// 未知の ID は "?" を返す (piper-plus の UNKNOWN_PHONEME と同等)。
        /// </summary>
        private static string ResolvePhonemeString(
            int id,
            Dictionary<int, string> idToString)
        {
            if (idToString.TryGetValue(id, out var str))
                return str;

            // フォールバック: ASCII 範囲 (3-127) ならキャッシュ済み文字列を返す
            if (id is > 2 and < 128)
                return AsciiStrings[id];

            return "?";
        }
    }
}