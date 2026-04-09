namespace uPiper.Core
{
    /// <summary>
    /// 読み取り専用の設定インターフェース。
    /// バリデーション済みの不変設定にアクセスするための抽象。
    /// </summary>
    public interface IPiperConfigReadOnly
    {
        LanguageSettings Language { get; }
        PerformanceSettings Performance { get; }
        InferenceSettings Inference { get; }
        PiperAudioSettings Audio { get; }
        SilenceSettings Silence { get; }
        GeneralSettings General { get; }
    }
}