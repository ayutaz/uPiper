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

v1.3.x で即時修正済みの項目と、v1.4.0 実装済みの項目、および v2.0 で対応予定の項目を分類する。

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

## v1.4.0 実装済み

### ✅ ExecuteInference 3段階分離（P2-1）

**目的**: テスト可能性の向上 + Warmup 時の ReadbackAndClone 回避

`ExecuteInference` を以下の3段階プライベートメソッドに分離済み:

1. **PrepareInputs**: テンソル構築 + `_worker.SetInput()` — pure ロジック、テスト可能
2. **RunInference**: `_worker.Schedule()` — Warmup はここまでで終了
3. **ExtractResults**: `PeekOutput()` + `ReadbackAndClone()` + データコピー

Stopwatch タイミングログも同時追加（後述）。Worker 所有テンソルへの誤 Dispose を排除するリファクタリングも実施。

### ✅ ExtractAudioData 一括コピー化（P1-1）

**目的**: 推論ごとの最大ボトルネック候補の排除

`tensor[i]` 1要素ずつコピーから `ReadbackAndClone()` + インデクサ手動コピーに変更。Worker所有テンソルとクローンテンソルの所有権を明確に分離。

### ✅ IInferenceAudioGenerator に PiperConfig 対応 InitializeAsync 追加（P1-2）

**目的**: インターフェースの抽象化完全化

`PiperConfig` 付き `InitializeAsync` を `IInferenceAudioGenerator` インターフェースに追加（2オーバーロード対応）。テスト時の DI が可能になった。

### ✅ PiperConfig.ParsedPhonemeSilence 副作用排除（P1-3）

**目的**: 設定オブジェクトの責務適正化

`ParsedPhonemeSilence` を `PiperConfig` から除去し、`PiperTTS` 側に移管済み。設定オブジェクトが副作用なしのバリデーションのみ担当するようになった。

### ✅ PiperConfig サブオブジェクト分割 → ValidatedPiperConfig 方式（P4-1）

**目的**: 設定クラスの肥大化防止 + イミュータブルスナップショット導入

PiperConfig 自体のフィールド構造は維持しつつ、`ValidatedPiperConfig` 不変スナップショットを導入。`ParsedPhonemeSilence`（`IReadOnlyDictionary`）を内包し、`PiperConfig.ToValidated()` で取得する方式を採用。Unity の `[Serializable]` + Inspector 編集との相性を維持。

### ✅ IInferenceAudioGenerator メソッド統合（P3-2）

**目的**: インターフェースの責務明確化

3メソッド（GenerateAudioAsync, GenerateAudioWithProsodyAsync, GenerateAudioWithSilenceSplitAsync）を統合済み:

- GenerateAudioAsync と GenerateAudioWithProsodyAsync を nullable Prosody 引数で統合
- GenerateAudioWithSilenceSplitAsync をインターフェースから除去、オーケストレーション層に移動
- 結果: `IInferenceAudioGenerator = InitializeAsync + GenerateAudioAsync` の2メソッド構成

### ✅ SilenceSplit オーケストレーション層移動（P3-1）

**目的**: 推論エンジンの単一責任化

GenerateAudioWithSilenceSplitAsync の「分割→反復推論→結合」ロジックを専用の `SplitInferenceOrchestrator` クラスに移動済み。InferenceAudioGenerator は単一句の推論のみ担当するよう整理された。

### ✅ MultilingualPhonemizer Options オブジェクト化（P5-1）

**目的**: コンストラクタパラメータの整理

個別エンジンパラメータを `MultilingualPhonemizerOptions` クラスにまとめ、コンストラクタを簡素化済み。

### ✅ ILanguageG2PHandler 導入準備（P5-2）

**目的**: 言語追加時の拡張性確保

`MultilingualPhonemizer.PhonemizeWithProsodyAsync` の if-else チェーンを switch 文 + `ProcessXxx()` × 7メソッドに抽出済み。`ProcessFallbackAsync()` も分離。本格的な Strategy パターン（`Dictionary<string, ILanguageG2PHandler>`）への移行は v2.0 で対応。

---

## v1.4.0 設計レビュー指摘対応（同時実施）

v1.4.0 の実装レビュー時に指摘された追加改善項目を同時対応した。

### ✅ InferenceContext sealed class 導入

**内容**: `PrepareInputs` が返す7要素タプルを `private sealed class InferenceContext : IDisposable` に置換。

テンソルの Dispose 責務を `InferenceContext` に一元化し、using ブロックでの確実な解放を保証。タプル分解によるコードの可読性低下と Dispose 漏れリスクを解消した。

### ✅ TTSSynthesisOrchestrator 導入

**内容**: 音素列 → AudioClip 変換パイプライン（エンコード → 推論 → 句分割 → AudioClip 構築）を `TTSSynthesisOrchestrator` に集約。

`PiperTTS` に散在していた2メソッド分の重複ロジックを排除。単一責任の明確化と保守性が向上した。

### ✅ PeekOutput Worker 所有テンソルの Dispose バグ修正

**内容**: `ExtractResults()` のリファクタリングで、Worker 所有テンソルに対して誤って `Dispose()` を呼んでいた箇所を修正。

Worker 管理下のテンソルをユーザー側で Dispose すると二重解放になる問題を解消。テンソルの所有権モデルを整理した。

### ✅ Stopwatch 精度向上

**内容**: タイミングログを `ElapsedMilliseconds`（long, ミリ秒整数）から `Elapsed.TotalMilliseconds`（double）に変更し、`:F1` フォーマットで小数第1位まで出力するよう改善。

---

## v1.4.0 全チケット完了

### ✅ 統合テスト追加（P5-3）

**状況**: 実装完了

- StubInferenceAudioGenerator（パラメータ記録対応）
- TTSSynthesisOrchestratorTests（12テスト: 3パス検証、パラメータ伝播、エンコード結果検証、config=null等）
- SplitInferenceOrchestratorTests（5テスト: 単一句、複数句、Prosody付き句分割、空入力、nullガード）

### ✅ 設計レビュー第2回指摘対応

v1.4.0 実装に対するエージェントチームレビュー（6観点並行レビュー）の指摘を全件対応した。

#### Critical (2件)
- **IReadOnlyDictionary キャスト除去**: PhonemeSilenceProcessor.SplitAtPhonemeSilence のパラメータを `IReadOnlyDictionary` に変更。TTSSynthesisOrchestrator/SplitInferenceOrchestrator の `(Dictionary)` ダウンキャスト2箇所を除去
- **GPUSettings 参照リーク修正**: ValidatedPiperConfig コンストラクタで防御的コピー（`new GPUInferenceSettings { MaxMemoryMB = ... }`）

#### Major (4件)
- **SynthesisRequest 導入**: TTSSynthesisOrchestrator.SynthesizeAsync の12パラメータを `SynthesisRequest` readonly struct に集約。config/voiceConfig はコンストラクタ注入に昇格
- **Validate() 副作用ドキュメント化**: PiperConfig.Validate() と ToValidated() のXML docに副作用警告を追加
- **GetOutputTensor catch-all 除去**: 初期化時に出力名をキャッシュ（`_cachedOutputName`）、名前なしフォールバック削除
- **lock 目的コメント追加**: Generate-vs-Dispose 間のスレッド安全性保護であることを明記

#### Minor (4件)
- **SplitInferenceOrchestrator を internal 化**: public API面積最小化
- **デッドコード CreateProsodyTensor 削除**: Pooled版導入後の未使用メソッド
- **ArrayPool 閾値分岐廃止**: 常にArrayPool使用に簡素化
- **AudioClip命名 DateTime.Now → Guid.NewGuid()**: 同一秒内衝突防止

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