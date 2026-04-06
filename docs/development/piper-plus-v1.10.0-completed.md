# piper-plus v1.10.0 対応 — 完了サマリー

**実施期間**: 2026-04-06
**ブランチ**: `docs/piper-plus-v1.10.0-migration`

---

## 対応結果

piper-plus v1.10.0 の変更点11項目を調査し、5項目は対応不要（Unity制約/実装済み）、5項目を実装完了、1項目がブロック中。

### 実装完了 (4チケット)

| チケット | 内容 | 主な変更ファイル |
|---------|------|----------------|
| MS1-1 | PUAマッピング差分修正 (88→96エントリ) | `PuaTokenMapper.cs`, `PuaTokenMapperTests.cs` |
| MS2-1 | Warmup実装 (初回レイテンシ500-800ms削減) | `InferenceAudioGenerator.cs`, `PiperConfig.cs` |
| MS2-2 | ArrayPool最適化 (GC ~530KB→~272KB/推論) | `InferenceAudioGenerator.cs`, `AudioClipBuilder.cs`, `PiperTTS.Inference.cs` |
| MS3-2 | 沈黙句分割 (PhonemeSilenceProcessor) | `PhonemeSilenceProcessor.cs` (新規), `InferenceAudioGenerator.cs`, `PiperTTS.Inference.cs` |

### 設計レビューで追加修正

| 修正 | 内容 |
|------|------|
| P0 テンソルリーク修正 | outputTensor/readableTensor を finally で Dispose |
| P1 PhonemeEncoder重複排除 | PUA辞書3つ削除 (~100行削減)、PuaTokenMapper に委譲 |
| P2 PuaTokenMapper改善 | gap管理、ResetForTesting、ドメインリロード対応 |

### ブロック中 (1チケット)

| チケット | 内容 | ブロッカー |
|---------|------|-----------|
| [MS3-1](tickets/MS3-1_swedish-language-support.md) | スウェーデン語 (SV) 対応 | SV対応ONNXモデル + DotNetG2P.Swedish SwedishPuaMapper 長母音PUA追加 |

### 対応不要と判定 (5項目)

| 項目 | 理由 |
|------|------|
| Prosodyテンソル型 (int32 vs int64) | Sentis が自動キャスト |
| 日本語音素化キャッシュ | LRU 5000エントリ実装済み |
| ORT SessionOptions | Unity.InferenceEngine が非公開 |
| ORT設定準拠 (ort-session-contract.toml) | Unity.InferenceEngine が非公開 |
| 環境変数制御 | PiperConfig で代替 |

---

## コミット履歴

```
8e77fc4 docs: MS3レビュー結果をロードマップに反映 + テスト欠落2件追加
4933a8d feat: MS3-2 沈黙句分割 (PhonemeSilenceProcessor) 実装
3ce1200 refactor: 設計レビュー指摘の即時修正 + 将来ロードマップ作成
009fcc1 feat: MS1-1/MS2-1/MS2-2 実装 — PUAマッピング修正・Warmup・ArrayPool最適化
4e5f323 docs: piper-plus v1.10.0 移行調査・マイルストーン・チケット作成
```

## 関連ドキュメント

- [アーキテクチャ改善ロードマップ](architecture-improvement-roadmap.md) — v1.4.0/v2.0 の将来計画
- [MS3-1 スウェーデン語対応チケット](tickets/MS3-1_swedish-language-support.md) — ブロック中