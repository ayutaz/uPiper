namespace uPiper.Core
{
    /// <summary>
    /// 読み取り専用の設定インターフェース。
    /// バリデーション済みの不変設定にアクセスするための抽象。
    /// </summary>
    public interface IPiperConfigReadOnly
    {
        /// <summary>言語関連の設定（デフォルト言語、自動検出、対応言語リスト）</summary>
        LanguageSettings Language { get; }

        /// <summary>パフォーマンス関連の設定（キャッシュ、ワーカースレッド数、バッチサイズ）</summary>
        PerformanceSettings Performance { get; }

        /// <summary>推論バックエンド関連の設定（CPU/GPU選択、ウォームアップ、フォールバック）</summary>
        InferenceSettings Inference { get; }

        /// <summary>音声出力関連の設定（サンプルレート、正規化、RMSレベル）</summary>
        PiperAudioSettings Audio { get; }

        /// <summary>沈黙句分割関連の設定（有効化フラグ、仕様文字列、パース済み辞書）</summary>
        SilenceSettings Silence { get; }

        /// <summary>汎用設定（デバッグログ、タイムアウト）</summary>
        GeneralSettings General { get; }
    }
}