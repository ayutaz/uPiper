# アーキテクチャ改善ロードマップ

**作成日**: 2026-04-06
**ベース**: MS1-MS2 設計レビュー結果
**関連文書**: [piper-plus-v1.10.0-milestones.md](piper-plus-v1.10.0-milestones.md)

---

## 概要

MS1-1（PUAマッピング修正）、MS2-1（Warmup）、MS2-2（ArrayPool最適化）の実装後、
3つの視点（データモデル設計 / 推論パイプライン / 横断的アーキテクチャ）からの設計レビューで
特定された改善項目をまとめる。

v1.3.x で即時修正済みの項目と、v1.4.0 / v2.0 で対応予定の項目を分類する。

---

## v1.3.x で対応済み

| 項目 | 優先度 | 内容 |
|------|--------|------|
| 出力テンソル finally パターン | P0 | outputTensor/readableTensor を finally で Dispose（リーク防止） |
| PhonemeEncoder PUA重複排除 | P1 | multiCharPhonemeMap/puaToPhonemeMap/puaToPhonemeMapMultilingual を削除、PuaTokenMapper に委譲（~84行削減） |
| PuaTokenMapper gap管理 | P2 | 0xE053 gapコメント追加、0xE01F gapテスト追加 |
| PuaTokenMapper テスト改善 | P2 | ResetForTesting()追加、ドメインリロード対応 |

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

### pua.json クロスバリデーションテスト

**目的**: piper-plus との PUA マッピング乖離の CI 自動検出

piper-plus の `pua.json` をテストフィクスチャとして配置し、
`PuaTokenMapper.FixedPuaMapping` と1エントリずつ照合するテストを追加。

### WebGL Warmup デフォルト無効化

**目的**: WebGL 環境での UI フリーズ回避

WebGL はシングルスレッドのため、Warmup の 2 回のダミー推論がブラウザのメインスレッドをブロックする。
`#if UNITY_WEBGL` で `EnableWarmup` を自動的に false に上書きするか、
ドキュメントで WebGL では `EnableWarmup = false` を推奨する旨を明記。

### IInferenceAudioGenerator に PiperConfig 対応 InitializeAsync 追加

**目的**: インターフェースの抽象化完全化

現在 `PiperConfig` 付き `InitializeAsync` は `InferenceAudioGenerator` クラスにのみ定義されており、
`IInferenceAudioGenerator` インターフェースには含まれていない。テスト時の DI に不便。

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

### PiperConfig サブオブジェクト分割

**目的**: 設定クラスの肥大化防止

MS3-2 の SentenceSilence 等が追加されると PiperConfig のフィールド数が増大。
各カテゴリ 5 フィールド超になった時点で以下のように分割:

- `GeneralSettings`（言語、デバッグ）
- `InferenceSettings`（Backend, Warmup, BatchSize + GPUSettings）
- `AudioSettings`（SampleRate, Normalization, SentenceSilence）
- `CacheSettings`（PhonemeCache, MaxSize）

### public API の戻り値型変更

**目的**: ArrayPool の恩恵を API 利用者に透過的に提供

`GenerateAudioAsync` の戻り値を `float[]` から `IMemoryOwner<float>` に変更。
ただし `AudioClip.SetData` が `float[]` を要求するため変換コストあり。費用対効果要検討。

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