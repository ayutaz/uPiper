# アーキテクチャ改善ロードマップ

**作成日**: 2026-04-06
**ベース**: MS1-MS3 設計レビュー結果 + piper-plus v1.10.0 対応

---

## v1.3.x で対応済み

| 項目 | 優先度 | 概要 |
|------|--------|------|
| 出力テンソル finally パターン | P0 | outputTensor/readableTensor の Dispose リーク防止 |
| PhonemeEncoder PUA重複排除 | P1 | 3辞書削除 (~84行削減)、PuaTokenMapper に委譲 |
| pua.json クロスバリデーション | P1 | piper-plus pua.json v1 との双方向96エントリ照合テスト |
| PuaTokenMapper gap管理・テスト改善 | P2 | gap コメント/テスト追加、ResetForTesting()、ドメインリロード対応 |
| MS3-2 沈黙句分割 | P2 | PhonemeSilenceProcessor 実装、37テスト |
| WebGL Warmup 無効化 | P2 | `#if !UNITY_WEBGL` でスキップ、UIフリーズ防止 |
| PiperConfig バリデーションテスト | P2 | Warmup/PhonemeSilence の Validate() テスト11件 |

---

## v1.4.0 実装済み

| 項目 | ID | 成果 |
|------|-----|------|
| ExecuteInference 3段階分離 | P2-1 | PrepareInputs/RunInference/ExtractResults に分離。Warmup 時の ReadbackAndClone 回避 |
| ExtractAudioData 一括コピー化 | P1-1 | ReadbackAndClone + インデクサ手動コピーに変更。Worker/クローンの所有権分離 |
| IInferenceAudioGenerator に PiperConfig 対応 InitializeAsync 追加 | P1-2 | 2オーバーロード対応。テスト時 DI 可能に |
| PiperConfig.ParsedPhonemeSilence 副作用排除 | P1-3 | PiperTTS 側に移管。設定オブジェクトがバリデーション専任に |
| ValidatedPiperConfig 不変スナップショット | P4-1 | PiperConfig.ToValidated() で取得。ParsedPhonemeSilence 内包、Inspector 編集互換維持 |
| IInferenceAudioGenerator メソッド統合 | P3-2 | 3メソッド → InitializeAsync + GenerateAudioAsync の2メソッド構成に |
| SilenceSplit オーケストレーション層移動 | P3-1 | SplitInferenceOrchestrator に分離。推論エンジンの単一責任化 |
| MultilingualPhonemizer Options オブジェクト化 | P5-1 | MultilingualPhonemizerOptions でコンストラクタ簡素化 |
| ILanguageG2PHandler 導入準備 | P5-2 | if-else → switch + ProcessXxx() × 7メソッド抽出。v2.0 で Strategy パターン化予定 |

---

## v1.4.0 設計レビュー指摘対応

| 重要度 | 項目 | 概要 |
|--------|------|------|
| — | InferenceContext sealed class 導入 | 7要素タプルを IDisposable クラスに置換、テンソル Dispose 一元化 |
| — | TTSSynthesisOrchestrator 導入 | 音素列→AudioClip パイプラインを集約、PiperTTS の重複ロジック排除 |
| — | PeekOutput Worker 所有テンソル Dispose バグ修正 | Worker 管理テンソルの二重解放を解消 |
| — | Stopwatch 精度向上 | ElapsedMilliseconds → Elapsed.TotalMilliseconds (:F1) |
| — | 統合テスト追加 (P5-3) | TTSSynthesisOrchestratorTests 12件、SplitInferenceOrchestratorTests 5件 |
| Critical | IReadOnlyDictionary キャスト除去 | Dictionary ダウンキャスト2箇所を除去 |
| Critical | GPUSettings 参照リーク修正 | ValidatedPiperConfig コンストラクタで防御的コピー |
| Major | SynthesisRequest 導入 | 12パラメータ → readonly struct 集約、config/voiceConfig をコンストラクタ注入 |
| Major | Validate() 副作用ドキュメント化 | XML doc に副作用警告追加 |
| Major | GetOutputTensor catch-all 除去 | 初期化時に出力名キャッシュ、名前なしフォールバック削除 |
| Major | lock 目的コメント追加 | Generate-vs-Dispose 間のスレッド安全性を明記 |
| Minor | SplitInferenceOrchestrator を internal 化 | public API 面積最小化 |
| Minor | デッドコード CreateProsodyTensor 削除 | Pooled 版導入後の未使用メソッド |
| Minor | ArrayPool 閾値分岐廃止 | 常に ArrayPool 使用に簡素化 |
| Minor | AudioClip 命名 DateTime.Now → Guid | 同一秒内衝突防止 |

---

## piper-plus v1.10.0 対応不要と判定した項目

| 項目 | 理由 |
|------|------|
| Prosodyテンソル型 (int32 vs int64) | Sentis が自動キャスト |
| 日本語音素化キャッシュ | LRU 5000エントリ実装済み |
| ORT SessionOptions | Unity.InferenceEngine が非公開 |
| ORT設定準拠 (ort-session-contract.toml) | Unity.InferenceEngine が非公開 |
| 環境変数制御 | PiperConfig で代替 |

---

## ブロック中

| チケット | 内容 | ブロッカー |
|---------|------|-----------|
| [MS3-1](tickets/MS3-1_swedish-language-support.md) | スウェーデン語 (SV) 対応 | SV対応ONNXモデル + DotNetG2P.Swedish SwedishPuaMapper 長母音PUA追加 |

---

## v2.0 計画

公開APIの破壊的変更を伴う改善を集約。piper-plus完全互換・DI対応・ゼロコピーパイプラインが主目標。

詳細は [v2.0-plan.md](v2.0-plan.md) と [v2.0チケット](tickets/v2.0/INDEX.md) を参照。

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