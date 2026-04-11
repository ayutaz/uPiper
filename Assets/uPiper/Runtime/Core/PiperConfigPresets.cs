namespace uPiper.Core
{
    /// <summary>
    /// よく使う設定のプリセットを提供する。
    /// <para>
    /// 音声合成パラメータ（LengthScale / NoiseScale / NoiseW）は <see cref="PiperVoiceConfig"/> 側で設定する。
    /// このクラスはシステムレベルの <see cref="PiperConfig"/> 設定のみを扱う。
    /// </para>
    /// </summary>
    public static class PiperConfigPresets
    {
        /// <summary>
        /// 低レイテンシ・高速合成向け。リアルタイム応答に最適。
        /// <para>
        /// AudioCache・Warmup を有効化して繰り返し合成と初回レイテンシを最適化する。
        /// 音声パラメータは <see cref="PiperVoiceConfig"/> で LengthScale=0.8, NoiseScale=0.4, NoiseW=0.5 を推奨。
        /// </para>
        /// </summary>
        public static PiperConfig Fast()
        {
            var config = PiperConfig.CreateDefault();
            config.EnableAudioCache = true;
            config.EnableWarmup = true;
            return config;
        }

        /// <summary>
        /// 品質と速度のバランス型。デフォルト設定と同等。
        /// <para>
        /// 音声パラメータは <see cref="PiperVoiceConfig"/> のデフォルト値
        /// （LengthScale=1.0, NoiseScale=0.667, NoiseW=0.8）を推奨。
        /// </para>
        /// </summary>
        public static PiperConfig Natural()
        {
            return PiperConfig.CreateDefault();
        }

        /// <summary>
        /// 高品質・ナレーション向け。処理時間は長くなる。
        /// <para>
        /// 音声正規化を有効化し、出力音量を安定させる。
        /// 音声パラメータは <see cref="PiperVoiceConfig"/> で LengthScale=1.1, NoiseScale=0.8, NoiseW=1.0 を推奨。
        /// </para>
        /// </summary>
        public static PiperConfig HighQuality()
        {
            var config = PiperConfig.CreateDefault();
            config.NormalizeAudio = true;
            return config;
        }
    }
}