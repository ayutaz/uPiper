# アーキテクチャ改善ロードマップ

**作成日**: 2026-04-06
**ベース**: MS1-MS3 設計レビュー結果
**関連文書**: [piper-plus-v1.10.0-completed.md](piper-plus-v1.10.0-completed.md)

---

## 概要

MS1-1（PUAマッピング修正）、MS2-1（Warmup）、MS2-2（ArrayPool最適化）の実装後、
3つの視点（データモデル設計 / 推論パイプライン / 横断的アーキテクチャ）からの設計レビューで
特定された改善項目をまとめる。MS3-2（沈黙句分割）の実装完了、および MS3-1（スウェーデン語対応、ブロック中）の
レビュー結果を反映済み。

v1.3.x で即時修正済みの項目と、v1.4.0 / v2.0 で対応予定の項目を分類する。

---

## v1.3.x で対応済み

| 項目 | 優先度 | 内容 |
|------|--------|------|
| 出力テンソル finally パターン | P0 | outputTensor/readableTensor を finally で Dispose（リーク防止） |
| PhonemeEncoder PUA重複排除 | P1 | multiCharPhonemeMap/puaToPhonemeMap/puaToPhonemeMapMultilingual を削除、PuaTokenMapper に委譲（~84行削減） |
| PuaTokenMapper gap管理 | P2 | 0xE053 gapコメント追加、0xE01F gapテスト追加 |
| PuaTokenMapper テスト改善 | P2 | ResetForTesting()追加、ドメインリロード対応 |
| MS3-2 沈黙句分割 | P2 | PhonemeSilenceProcessor, パイプライン統合, 37テスト |
| pua.json クロスバリデーション | P1 | piper-plus pua.json v1 との双方向96エントリ照合テスト（CI自動検出） |
| WebGL Warmup 無効化 | P2 | `#if !UNITY_WEBGL` でスキップ、UIフリーズ防止 |
| PiperConfig バリデーションテスト | P2 | Warmup/PhonemeSilence の Validate() テスト11件 |

---

## v1.4.0 計画

### ExecuteInference 3段階分離

**目的**: テスト可能性の向上 + Warmup 時の ReadbackAndClone 回避

現在の `ExecuteInference` は4つの責務（テンソル構築・推論実行・結果読み戻し・データ抽出）が1メソッドに凝縮。
以下の3段階に分離する:

1. **PrepareInputs**: テンソル構築 + `_worker.SetInput()` — pure ロジック、テスト可能
2. **RunInference**: `_worker.Schedule()` — Warmup はここまでで終了
3. **ExtractResults**: `PeekOutput()` + `ReadbackAndClone()` + データコピー

**効果**:
- Warmup時に不要な GPU→CPU読み戻し（数百KB）+ float[]アロケーションを回避
- テンソルデータ準備ロジックを EditMode テストで検証可能に

### ExtractAudioData 一括コピー化

**目的**: 推論ごとの最大ボトルネック候補の排除

現在 `tensor[i]` で1要素ずつコピー（毎回境界チェック+オフセット計算）。
Sentis の `Tensor<T>.ToArray()` または同等のバルクコピー API が利用可能か確認し、置換する。

### 推論タイミングログ

**目的**: パフォーマンス計測基盤

`ExecuteInference` に `Stopwatch` ベースのタイミングログを追加:
`[InferenceAudioGenerator] Inference took Xms (schedule: Xms, readback: Xms, extract: Xms)`

### IInferenceAudioGenerator に PiperConfig 対応 InitializeAsync 追加

**目的**: インターフェースの抽象化完全化

現在 `PiperConfig` 付き `InitializeAsync` は `InferenceAudioGenerator` クラスにのみ定義されており、
`IInferenceAudioGenerator` インターフェースには含まれていない。テスト時の DI に不便。

### PiperConfig サブオブジェクト分割（v2.0 から前倒し）

**目的**: 設定クラスの肥大化防止

MS2-1 で Warmup 2フィールド、MS3-2 で Silence 3フィールドが追加され、合計22フィールドに到達。
Advanced カテゴリは6フィールドで超過。以下の4分割を推奨:

- `GeneralSettings`（言語、デバッグ: 5フィールド）
- `InferenceSettings`（Backend, Warmup, BatchSize, Threads + GPUSettings: 7フィールド）
- `AudioSettings`（SampleRate, Normalization, SentenceSilence: 6フィールド）
- `CacheSettings`（Cache: 2フィールド）

PiperConfig 自体はファサードとして残し、内部でサブオブジェクトに委譲。
Unity の `[Serializable]` + Inspector 編集との相性を維持。

### IInferenceAudioGenerator メソッド統合

**目的**: インターフェースの責務明確化

現在の3メソッド（GenerateAudioAsync, GenerateAudioWithProsodyAsync, GenerateAudioWithSilenceSplitAsync）を統合:

- GenerateAudioAsync と GenerateAudioWithProsodyAsync を統合（Prosody引数をnullable化）
- GenerateAudioWithSilenceSplitAsync をインターフェースから除去、オーケストレーション層に移動
- 結果: IInferenceAudioGenerator = InitializeAsync + GenerateAudioAsync の2メソッド

### SilenceSplit オーケストレーション層移動

**目的**: 推論エンジンの単一責任化

GenerateAudioWithSilenceSplitAsync の「分割→反復推論→結合」ロジックは推論エンジンの責務ではない。
PiperTTS.Inference.cs か専用の SplitInferenceOrchestrator に移動。
InferenceAudioGenerator は単一句の推論のみ担当。

### MultilingualPhonemizer Options オブジェクト化（MS3-1 と同時）

**目的**: コンストラクタパラメータの整理

コンストラクタが10パラメータ（SV追加で11）に達する。
個別エンジンパラメータを `MultilingualPhonemizerOptions` にまとめ、DI を簡素化。

### ILanguageG2PHandler 導入準備（MS3-1 と同時）

**目的**: 言語追加時の拡張性確保

8言語目（スウェーデン語）の追加がリファクタリング契機。
MultilingualPhonemizer の if-else チェーンから各言語の処理を private メソッドに抽出する中間リファクタリング。
本格的な Strategy パターンは10言語超で。

### PiperConfig.ParsedPhonemeSilence 副作用排除

**目的**: 設定オブジェクトの責務適正化

Validate() の副作用として `ParsedPhonemeSilence` がパースされる現設計は設定オブジェクトの責務超過。
呼び出し側で `PhonemeSilenceProcessor.Parse` を使う形に整理するか、
`ParsedConfig` イミュータブルオブジェクトに分離。

### 統合テスト追加

**目的**: 分岐パスの網羅的検証

- PiperTTS.Inference.cs の3分岐パス（通常/Prosody/Silence分割）のモックベース統合テスト

---

## v2.0 計画

### PuaTokenMapper インスタンスクラス化

**目的**: テスト間状態リークの根本解決 + DI 対応

現在の static class を通常のクラスに変更。固定マッピングは `static readonly` のまま残し、
動的割り当て部分をインスタンスごとに持つ。

**影響**: public API 変更（breaking change）。MultilingualPhonemizer, PhonemeEncoder の参照を更新。

### pua.json ランタイム読み込み

**目的**: piper-plus との仕様同期の自動化

`pua.json` を StreamingAssets に配置し、`PuaTokenMapper.InitializeAsync(string json)` で読み込み。
PuaTokenMapper を static class からインスタンスクラスに変更する必要がある（上記と同時実施）。

**WebGL 制約**: StreamingAssets は非同期読み込み必須。Resources.Load なら同期可だが配布パスが複雑。

### NativeArray 統一

**目的**: ExtractAudioData → NormalizeAudio → AudioClip.SetData の中間コピー完全排除

`AudioClip.SetData(NativeArray<float>, int)` は Unity 2023.1+ で利用可能。
`Tensor<float>` から NativeArray を直接取得できれば、float[] 中間バッファが不要になる。

**制約**: Unity 2023.1+ が最低要件。それ以前は `#if` 分岐が必要で保守コスト高。

### public API の戻り値型変更

**目的**: ArrayPool の恩恵を API 利用者に透過的に提供

`GenerateAudioAsync` の戻り値を `float[]` から `IMemoryOwner<float>` に変更。
ただし `AudioClip.SetData` が `float[]` を要求するため変換コストあり。費用対効果要検討。

### ILanguageG2PHandler 全面移行

**目的**: 言語追加のスケーラビリティ確保

10言語超で MultilingualPhonemizer の if-else チェーンを `Dictionary<string, ILanguageG2PHandler>` に完全移行。
各言語の G2P 処理を個別 Handler 実装に閉じ込める。

### PhonemeIdMap `int` → `int[]` 型変更

**目的**: piper-plus との完全互換

`Dictionary<string, int>` を `Dictionary<string, int[]>` に変更。
15+ファイルに波及する破壊的変更。現時点では全モデルが1要素配列のため実害なし。

### Prosody フラット配列化

**目的**: piper-plus との推論インターフェース統一

A1/A2/A3 別配列を piper-plus と同じフラット `int[]` に統一。
ストリーミング対応時に同時実施が効率的。

### N-gram 言語検出

**目的**: ラテン文字多言語の自動検出

10言語超のラテン文字言語に対応するため、CJK は Unicode 範囲検出（高速・確実）、
ラテン文字間は trigram 頻度分布検出（精度重視）のハイブリッド方式を導入。

---

## 設計原則

### piper-plus との距離感

| レイヤー | 方針 | 例 |
|---------|------|-----|
| 仕様レイヤー | 厳密に追従 | PUAマッピング、Warmupパラメータ、Intersperse PAD |
| 実装レイヤー | Unity独自設計 | BackendType選択、Metal回避、WebGL分岐 |
| 検証レイヤー | CI自動検出 | pua.json差分テスト |

### 不採用とした提案

| 提案 | 理由 |
|------|------|
| IWarmupStrategy インターフェース | YAGNI。現実的なバリエーションがダミー入力パラメータ違いのみ |
| AudioBufferPool (TTS専用プール) | ArrayPool.Shared で十分。TTS呼び出し頻度はリアルタイムレンダリングほどではない |
| Span/Memory ベース public API | Unity API が float[] を要求するため変換コストが発生 |
| Builder パターン (PiperConfig) | Unity の [Serializable] + Inspector 編集と相性が悪い |
| ISynthesisPipeline 抽象 | piper-plus も不採用。YAGNI。ストリーミング対応時に再評価 |
| 言語ヒント API `[sv]` タグ | defaultLatinLanguage 切り替えで代替可能 |
| Phoneme ID `int` → `long` 統一 | Sentis が int32 テンソル要求。C# 側で long を使うメリットなし |