# Phoneme Timing Feature - Ticket Index

**作成日**: 2026-04-12
**マイルストーン**: [phoneme-timing-milestones.md](../phoneme-timing-milestones.md)
**要件定義**: [phoneme-timing-requirements.md](../phoneme-timing-requirements.md)
**技術調査**: [phoneme-timing-investigation.md](../phoneme-timing-investigation.md)
**仕様書**: [phoneme-timing-spec.md](../phoneme-timing-spec.md)

---

## Phase 1: 基盤型定義

> **目標**: タイミング計算に必要な全データ型と変換ロジックを定義し、単体テストで正確性を保証する。

| ID | タイトル | 種別 | 状態 | 依存 | チケット |
|----|---------|------|------|------|---------|
| P1-1 | PhonemeTimingEntry 定義 | impl | todo | - | [P1-1.md](./P1-1.md) |
| P1-2 | InferenceOutput 定義 | impl | todo | - | [P1-2.md](./P1-2.md) |
| P1-3 | SynthesisWithTimingResult 定義 | impl | todo | P1-1 | [P1-3.md](./P1-3.md) |
| P1-4 | TimingCalculator 実装 | impl | todo | P1-1 | [P1-4.md](./P1-4.md) |
| T1-1 | TimingCalculator 基本計算テスト | test | todo | P1-4 | [T1-1.md](./T1-1.md) |
| T1-2 | 特殊トークンスキップテスト | test | todo | P1-4 | [T1-2.md](./T1-2.md) |
| T1-3 | PUA 逆引きテスト | test | todo | P1-4 | [T1-3.md](./T1-3.md) |
| T1-4 | エッジケーステスト | test | todo | P1-4 | [T1-4.md](./T1-4.md) |
| T1-5 | PhonemeTimingEntry 構造テスト | test | todo | P1-1 | [T1-5.md](./T1-5.md) |

## Phase 2: 推論レイヤー

> **目標**: `IInferenceAudioGenerator` の戻り値を `InferenceOutput` に拡張し、durations テンソル読み取りと短テキストデコレータ追従を完了する。

| ID | タイトル | 種別 | 状態 | 依存 | チケット |
|----|---------|------|------|------|---------|
| P2-1 | IInferenceAudioGenerator 戻り値型変更 | impl | todo | P1-2 | [P2-1.md](./P2-1.md) |
| P2-2 | InferenceAudioGenerator durations 読み取り | impl | todo | P2-1 | [P2-2.md](./P2-2.md) |
| P2-3 | ShortTextMitigatingGenerator 追従 | impl | todo | P2-1, P2-2 | [P2-3.md](./P2-3.md) |
| P2-4 | StubInferenceAudioGenerator durations 対応 | impl | todo | P2-1 | [P2-4.md](./P2-4.md) |
| T2-1 | ShortTextMitigatingGenerator PAD 除去テスト | test | todo | P2-3, P2-4 | [T2-1.md](./T2-1.md) |
| T2-2 | InferenceOutput Dispose テスト | test | todo | P1-2 | [T2-2.md](./T2-2.md) |

## Phase 3: オーケストレーション

> **目標**: `InferenceOutput`（durations）をパイプライン全体に伝搬し、`TimingCalculator` で秒タイミングに変換してキャッシュに格納する。

| ID | タイトル | 種別 | 状態 | 依存 | チケット |
|----|---------|------|------|------|---------|
| P3-1 | SplitInferenceOrchestrator durations 結合 | impl | todo | P2-1, P2-3 | [P3-1.md](./P3-1.md) |
| P3-2 | TTSSynthesisOrchestrator タイミング計算統合 | impl | todo | P1-4, P3-1 | [P3-2.md](./P3-2.md) |
| P3-3 | AudioSynthesisCache タイミング格納拡張 | impl | todo | P1-1 | [P3-3.md](./P3-3.md) |
| T3-1 | SplitInferenceOrchestrator 句分割タイミングテスト | test | todo | P3-1, P2-4 | [T3-1.md](./T3-1.md) |
| T3-2 | AudioSynthesisCache タイミング格納テスト | test | todo | P3-3 | [T3-2.md](./T3-2.md) |
| T3-3 | durations 非対応モデルテスト | test | todo | P3-2, P2-4 | [T3-3.md](./T3-3.md) |

## Phase 4: 公開API

> **目標**: `IPiperTTS.SynthesizeWithTimingAsync` を公開し、エンドツーエンドでタイミング情報を取得可能にする。

| ID | タイトル | 種別 | 状態 | 依存 | チケット |
|----|---------|------|------|------|---------|
| P4-1 | IPiperTTS インターフェース拡張 | impl | todo | P1-3 | [P4-1.md](./P4-1.md) |
| P4-2 | PiperTTS.Inference.cs 実装 | impl | todo | P4-1, P3-2 | [P4-2.md](./P4-2.md) |
| T4-1 | 統合テスト: タイミング付き合成 | test | todo | P4-2, P2-4 | [T4-1.md](./T4-1.md) |
| T4-2 | 統合テスト: タイミング精度 | test | todo | P4-2 | [T4-2.md](./T4-2.md) |
| T4-3 | 統合テスト: キャッシュ整合 | test | todo | P4-2, P3-3 | [T4-3.md](./T4-3.md) |
| T4-4 | 統合テスト: durations 非対応 | test | todo | P4-2 | [T4-4.md](./T4-4.md) |
| T4-5 | リグレッションテスト | test | todo | P4-2 | [T4-5.md](./T4-5.md) |

---

## 進捗サマリー

| Phase | 実装 | テスト | 完了率 |
|-------|------|--------|--------|
| Phase 1 | 0/4 | 0/5 | 0% |
| Phase 2 | 0/4 | 0/2 | 0% |
| Phase 3 | 0/3 | 0/3 | 0% |
| Phase 4 | 0/2 | 0/5 | 0% |
| **合計** | **0/13** | **0/15** | **0%** |
