# piper-plus v1.10.0 対応チケット

**完了サマリー**: [piper-plus-v1.10.0-completed.md](../piper-plus-v1.10.0-completed.md)

---

## 未完了チケット

| チケット | タイトル | 優先度 | ステータス | ブロッカー |
|---------|---------|--------|-----------|-----------|
| [MS3-1](MS3-1_swedish-language-support.md) | スウェーデン語 (SV) 対応 | P2 | 対応可能 | ~~DotNetG2P.Swedish PuaMapper更新~~ 解決済み。SV対応ONNXモデルのみ未提供（E2Eテストに必要、G2P統合作業はモデルなしで着手可能） |

## 完了済みチケット (4件)

MS1-1, MS2-1, MS2-2, MS3-2 は全て実装完了。詳細は[完了サマリー](../piper-plus-v1.10.0-completed.md)を参照。

---

## v1.4.0 チケット (全完了)

| チケット | タイトル | 優先度 | ステータス |
|---------|---------|--------|-----------|
| [P2-1](v1.4.0-P2-1_execute-inference-separation-and-timing.md) | ExecuteInference 3段階分離 + タイミングログ | P2 | ✅ 完了 |
| [P3-1](v1.4.0-P3-1_silence-split-orchestration.md) | SilenceSplit オーケストレーション層移動 | P3 | ✅ 完了 |
| [P3-2](v1.4.0-P3-2_inference-method-consolidation.md) | IInferenceAudioGenerator メソッド統合 | P3 | ✅ 完了 |
| [P4-1](v1.4.0-P4-1_piperconfig-subobject-split.md) | PiperConfig サブオブジェクト分割 → ValidatedPiperConfig | P4 | ✅ 完了 |
| [P5-1](v1.4.0-P5-1_multilingual-phonemizer-options.md) | MultilingualPhonemizer Options オブジェクト化 | P5 | ✅ 完了 |
| [P5-2](v1.4.0-P5-2_language-g2p-handler-preparation.md) | ILanguageG2PHandler 導入準備（switch + ProcessXxx） | P5 | ✅ 完了 |
| [P5-3](v1.4.0-P5-3_integration-tests.md) | 統合テスト追加 | P5 | ✅ 完了 |