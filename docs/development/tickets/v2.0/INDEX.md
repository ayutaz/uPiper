# v2.0 チケット INDEX

**全16チケット** | [マイルストーン定義](../../v2.0-milestones.md) | **合計見積もり**: 約30.5人日（チケット29 + M5ドキュメント1.5）  
**レビュー補正見積もり**: 約37人日（統合バッファ2.5 + エージェント補正3 + 個別修正2含む）  
**ステータス**: 全16チケット実装完了（2026-04-09）

---

## Milestone 1: 基盤リファクタリング起点

> 2つの独立チェーンの起点タスクを並行実施し、Phase 1 の土台を構築する。

| チケット | タスク名 | 優先度 | 見積もり | 依存 | ステータス |
|---------|---------|--------|---------|------|-----------|
| [P1-4](P1-4_ILanguageG2PHandler.md) | ILanguageG2PHandler 全面移行 | P0 | 5.5 人日 | なし | 完了 |
| [P1-1](P1-1_PuaTokenMapperInstance.md) | PuaTokenMapper インスタンスクラス化 | P1 | 1.5 人日 | なし | 完了 |

**並行可否**: P1-4 と P1-1 は完全独立。2エージェント同時着手可能。

---

## Milestone 2: Phase 1 完成 (alpha)

> M1 の成果物に基づいて Phase 1 の残りタスクを並行実施し、alpha リリース候補を完成する。

| チケット | タスク名 | 優先度 | 見積もり | 依存 | ステータス |
|---------|---------|--------|---------|------|-----------|
| [P1-3](P1-3_DictionaryRegistry.md) | MultilingualPhonemizer Dictionary Registry化 | P0 | 2 人日 | P1-4 | 完了 |
| [P1-5](P1-5_G2PSynchronization.md) | G2P全同期化（IPhonemizerBackend廃止） | P0 | 1 人日 | P1-4 | 完了 |
| [P1-6](P1-6_ObsoleteRemoval.md) | Obsoleteコンストラクタ削除 | P1 | 1 人日 | P1-4, P1-5 | 完了 |
| [P1-2](P1-2_PuaJsonRuntime.md) | pua.json ランタイム読み込み | P1 | 1 人日 | P1-1 | 完了 |

**推奨マージ順序**: P1-3 → P1-5 → P1-6（MultilingualPhonemizer.cs の同時変更回避）

---

## Milestone 3: データモデル + 設定整理

> piper-plus 互換のデータモデル変更と設定・API 整理を並行で進める。

| チケット | タスク名 | 優先度 | 見積もり | 依存 | ステータス |
|---------|---------|--------|---------|------|-----------|
| [P2-1](P2-1_PhonemeIdMapArray.md) | PhonemeIdMap int[] 型変更 | P0 | 1.5 人日 | M2完了 | 完了 |
| [P2-2](P2-2_ProsodyFlatArray.md) | Prosodyフラット配列化 | P0 | 3 人日 | P2-1, P1-4ハンドラ | 完了 |
| [P3-1](P3-1_ValidatedPiperConfigNested.md) | ValidatedPiperConfig ネスト構造化 | P1 | 0.5 人日 | M2完了 | 完了 |
| [P3-3](P3-3_ValidatePureFunction.md) | Validate() 純粋関数化 | P1 | 1 人日 | P3-1 | 完了 |
| [P3-2](P3-2_IPiperConfigReadOnly.md) | IPiperConfigReadOnly インターフェース | P2 | 0.5 人日 | P3-1, P3-3 | 完了 |
| [P3-5](P3-5_AudioNormalizer.md) | AudioNormalizer 切り出し | P2 | 0.5 人日 | M2完了 | 完了 |
| [P3-4](P3-4_BackendSelector.md) | BackendSelector 切り出し | P2 | 0.5 人日 | M2完了 (P3-5後推奨) | 完了 |

**並行グループ**: A (P2-1 → P2-2) / B (P3-1 → P3-3 → P3-2) / C (P3-5 → P3-4) は全て並行実行可能。

---

## Milestone 4: NativeArray + public API (beta)

> パフォーマンス最適化と条件付き public API 昇格を実施する。

| チケット | タスク名 | 優先度 | 見積もり | 依存 | ステータス |
|---------|---------|--------|---------|------|-----------|
| [P2-3](P2-3_NativeArrayUnification.md) | NativeArray統一 | P1 | 2 人日 | P2-2, P3-5 | 完了 |
| [P3-6](P3-6_SynthesisRequestPublic.md) | SynthesisRequest public API昇格 | P2 | 1 人日 | P2-2 | 完了 |

**並行可否**: P2-3 と P3-6 は並行実行可能。

---

## Milestone 5: 言語検出 + 仕上げ (RC)

> N-gram 言語検出を導入し、ドキュメントを完成させる。

| チケット | タスク名 | 優先度 | 見積もり | 依存 | ステータス |
|---------|---------|--------|---------|------|-----------|
| [P4-1](P4-1_NgramLanguageDetection.md) | N-gram言語検出 | P1 | 6.5 人日 | P1-3, P1-4 (M2完了後) | 完了 |

**備考**: M2完了後であれば M3/M4 と並行して開始可能。最終統合テストは M4 完了後。

---

## サマリー

| マイルストーン | チケット数 | 合計見積もり | リリース | ステータス |
|-------------|-----------|------------|---------|-----------|
| [M1: 基盤リファクタリング起点](../../v2.0-milestones.md#milestone-1-基盤リファクタリング起点) | 2 | 7 人日 | - | 完了 |
| [M2: Phase 1 完成](../../v2.0-milestones.md#milestone-2-phase-1-完成) | 4 | 5 人日 | alpha | 完了 |
| [M3: データモデル + 設定整理](../../v2.0-milestones.md#milestone-3-データモデル--設定整理) | 7 | 7.5 人日 | - | 完了 |
| [M4: NativeArray + public API](../../v2.0-milestones.md#milestone-4-nativearray-統一--public-api) | 2 | 3 人日 | beta | 完了 |
| [M5: 言語検出 + 仕上げ](../../v2.0-milestones.md#milestone-5-言語検出高度化--リリース準備) | 1 | 6.5 人日 | RC | 完了 |
| **合計** | **16** | **29 人日** | - | **全完了** |

**クリティカルパス**: P1-4 → (M2ゲート) → P2-1 → P2-2 → P2-3 (14人日)  
**準クリティカルパス**: P1-4 → (M2ゲート) → P4-1 (14人日)

**完了日**: 2026-04-09 -- 全16チケット実装完了。

---

## 凡例

| ステータス | 意味 |
|-----------|------|
| 未着手 | 作業開始前 |
| 進行中 | 実装中 |
| レビュー中 | PR作成・レビュー待ち |
| 完了 | マージ済み |