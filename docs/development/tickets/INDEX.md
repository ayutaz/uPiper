# piper-plus v1.10.0 対応チケット一覧

**作成日**: 2026-04-06
**ベース文書**: [マイルストーン](../piper-plus-v1.10.0-milestones.md) | [移行調査](../piper-plus-v1.10.0-migration-analysis.md)

---

## 進捗サマリー

| チケット | タイトル | 優先度 | ステータス | 見積もり |
|---------|---------|--------|-----------|---------|
| [MS1-1](MS1-1_pua-mapping-fix.md) | PUA マッピング差分修正 | P0 | 未着手 | 2-3h |
| [MS2-1](MS2-1_warmup-implementation.md) | Warmup 実装 | P1 | 未着手 | 3-4h |
| [MS2-2](MS2-2_arraypool-optimization.md) | ArrayPool メモリ最適化 | P1 | 未着手 | 1.5日 |
| [MS3-1](MS3-1_swedish-language-support.md) | スウェーデン語 (SV) 対応 | P2 | ブロック中 | 1.5人日 |
| [MS3-2](MS3-2_phoneme-silence-processor.md) | 沈黙句分割 | P2 | 未着手 | 3人日 |

---

## マイルストーン別

### MS1: 互換性修正

| チケット | 依存 | 後続 |
|---------|------|------|
| [MS1-1: PUA マッピング差分修正](MS1-1_pua-mapping-fix.md) | なし | MS3-1 |

### MS2: パフォーマンス改善

| チケット | 依存 | 後続 |
|---------|------|------|
| [MS2-1: Warmup 実装](MS2-1_warmup-implementation.md) | なし | MS3-2 |
| [MS2-2: ArrayPool メモリ最適化](MS2-2_arraypool-optimization.md) | なし | MS3-2 |

### MS3: 新機能追加

| チケット | 依存 | 後続 |
|---------|------|------|
| [MS3-1: スウェーデン語対応](MS3-1_swedish-language-support.md) | MS1-1 + モデル更新 | なし |
| [MS3-2: 沈黙句分割](MS3-2_phoneme-silence-processor.md) | MS2-1, MS2-2 推奨 | なし |

---

## 依存関係グラフ

```
MS1-1: PUAマッピング修正 ──────────────────────────→ MS3-1: スウェーデン語対応
                                                      ↑ (モデル更新待ち)

MS2-1: Warmup実装 ─────────┐
                           ├── (推奨) ──→ MS3-2: 沈黙句分割
MS2-2: ArrayPool最適化 ────┘
```

---

## 特記事項

- **MS2-2 ArrayPool**: 調査の結果、`ExtractAudioData` の ArrayPool 化は public API 戻り値制約で安全でないことが判明。現実的な GC 削減は ~530KB → ~272KB（マイルストーン文書の目標も ~272KB に更新済み。~100KB 以下には NativeArray 統一等の破壊的変更が必要）
- **MS3-1 スウェーデン語**: DotNetG2P.Swedish v1.9.0 は開発完了済み。uPiper 側の統合作業のみ。ただしスウェーデン語対応モデルが利用可能になるまでブロック
- **MS1 と MS2 は並行着手可能**（相互依存なし）
