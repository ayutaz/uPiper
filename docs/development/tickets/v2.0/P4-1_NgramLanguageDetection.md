# P4-1: N-gram言語検出

**マイルストーン**: M5 - 言語検出高度化 + リリース準備 (RC)
**優先度**: P1
**見積もり**: 6.5 人日
**依存チケット**: Phase 1完了（M2ゲート）— 特にP1-3 (Dictionary Registry化), P1-4 (ILanguageG2PHandler)
**後続チケット**: なし（v2.0最終タスク）
**ブランチ名**: `feature/v2.0-P4-1-ngram-language-detection`
**設計ドキュメント**: [P4-1_NgramLanguageDetection.md](../../v2.0-design/P4-1_NgramLanguageDetection.md)

---

## 1. タスク目的とゴール

現行の `UnicodeLanguageDetector` はラテン文字を全て `defaultLatinLanguage`（デフォルト: "en"）に一律マッピングしており、en/es/fr/pt の4言語間で区別ができない。Trigram頻度分析に基づくN-gram言語検出を導入し、ラテン文字セグメントの言語識別精度を向上させる。

**解決する問題**:

1. **ラテン文字言語の誤判定**: "Bonjour le monde" が "en" と判定される。"Hola mundo", "Bom dia mundo" も同様に全て "en" になる。
2. **`defaultLatinLanguage` フォールバックの限界**: Unicode文字範囲では en/es/fr/pt の区別が原理的に不可能。統計的手法（Trigram）が必要。
3. **piper-plus との互換性**: piper-plus はスウェーデン語向けにワードレベル後処理（`refineLatinSegmentsForSwedish`）を実装済みだが、uPiper は未移植。本タスクの `LatinSegmentRefiner` はこのパターンを Trigram ベースに一般化する。

**完了状態（Definition of Done）**:

- `ILanguageDetector` インターフェースが抽出され、`UnicodeLanguageDetector` が実装している
- `MultilingualPhonemizer` の検出器依存が `ILanguageDetector` に変更されている（DI可能）
- `HybridLanguageDetector`（Unicode + Trigram統合）が `ILanguageDetector` として動作する
- 50文字以上のラテンテキストで en vs 他 > 98%、es/fr/pt 間 > 80% の検出精度（キーワード辞書併用導入後に88%を目標）
- 短文（15文字未満）は `defaultLatinLanguage` にフォールバックする
- Trigramプロファイル未配置時は Unicode のみモードに自動フォールバック
- 全テスト通過（EditMode + PlayMode）
- `dotnet format --verify-no-changes` 通過

---

## 2. 実装する内容の詳細

### 2.1 ハイブリッド設計

CJK/かな/ハングルは既存の Unicode 範囲チェックで一意に決まるため変更しない。ラテン文字セグメントに対してのみ Trigram 検出を適用する二段構成:

```
入力テキスト
    ↓
[1] UnicodeLanguageDetector.SegmentText()  -- 既存ロジック（変更なし）
    ↓ (language, text) タプルリスト
[2] LatinSegmentRefiner.Refine()  -- 新規：ラテンセグメント再分類
    │ (a) セグメントが短すぎる (< 15文字) → フォールバック維持
    │ (b) TrigramLanguageDetector.Detect(segmentText)
    │ (c) スコア > 0.65 かつ 2位との差 > 0.05 → 言語再分類
    │ (d) それ以外 → defaultLatinLanguage 維持
    ↓
[3] 再分類済みセグメントリスト → MultilingualPhonemizer へ
```

**LatinSegmentRefiner キーワード辞書併用**: LatinSegmentRefinerに30-50語の言語固有キーワード辞書（"bonjour","merci"等）を組み込み、trigram検出と併用する。これにより短文の精度を補完する。

**MinCharsForDetection 引き下げ**: MinCharsForDetectionを15→10に引き下げ。10-15文字帯はconfidence閾値を0.75に引き上げることで誤検出を抑制する。

### 2.2 Trigramプロファイル

Trigramは連続3文字のシーケンス。テキスト正規化（小文字化、アクセント除去、非アルファベット→スペース、語境界スペース追加）後に抽出する。

**プロファイルサイズ**: 300 trigrams/言語（4言語合計1,200エントリ）。精度 ~93% とメモリ ~9.6KB のバランスが良い。

**データフォーマット**: `StreamingAssets/uPiper/LanguageProfiles/trigram_profiles.json` に配置。配列内の順序がランク（インデックス0が最頻出）。

**スコアリング**: Cavnar & Trenkle (1994) の Out-of-Place 距離法。入力 trigram のランクとプロファイル内ランクの差の絶対値を合計し、正規化して 0.0-1.0 のスコアに変換。信頼度閾値 0.65、最高スコアと2位の差 0.05 未満の場合もフォールバック。

### 2.3 新規クラス群

| クラス/インターフェース | 種別 | 配置先 | 役割 |
|----------------------|------|--------|------|
| `ILanguageDetector` | interface | `Multilingual/` | 言語検出抽象。`SegmentText()`, `DefaultLatinLanguage`, `Languages` |
| `HybridLanguageDetector` | public sealed class | `Multilingual/` | Unicode + Trigram 統合検出器。`ILanguageDetector` 実装 |
| `TrigramLanguageDetector` | internal sealed class | `Multilingual/` | Trigram頻度ベース検出器。`MinCharsForDetection = 15`, `DefaultConfidenceThreshold = 0.65f` |
| `TrigramProfile` | internal sealed class | `Multilingual/` | 言語別 trigram 頻度テーブル。`ComputeSimilarity()` メソッド |
| `TrigramProfileLoader` | internal class | `Multilingual/` | プロファイル JSON 読み込み（同期 + 非同期、WebGL対応） |
| `LatinSegmentRefiner` | internal sealed class | `Multilingual/` | ラテンセグメント再分類。piper-plus の `refineLatinSegmentsForSwedish` パターンを一般化 |

### 2.4 既存クラスの変更

| ファイル | 変更内容 |
|---------|---------|
| `UnicodeLanguageDetector.cs` | `ILanguageDetector` インターフェース実装を追加 |
| `MultilingualPhonemizer.cs` | `_detector` フィールドの型を `UnicodeLanguageDetector` → `ILanguageDetector` に変更 |
| `MultilingualPhonemizerOptions.cs` | `LanguageDetector` プロパティ（`ILanguageDetector`）と `EnableTrigramDetection` プロパティ（`bool`, default: true）を追加 |

### 2.5 初期化フロー

`MultilingualPhonemizer.InitializeAsync()` 内で以下の分岐:
- `options.LanguageDetector != null` → カスタム検出器を使用
- `EnableTrigramDetection = true` かつラテン言語が2つ以上 → プロファイルを読み込み `HybridLanguageDetector` を生成
- プロファイル読み込み失敗またはラテン言語が1つのみ → `UnicodeLanguageDetector` にフォールバック

### 2.6 実装ステップと見積もり

| ステップ | 内容 | 見積もり |
|---------|------|---------|
| 1 | `ILanguageDetector` インターフェース抽出 | 0.5日 |
| 2 | `UnicodeLanguageDetector` に `ILanguageDetector` 実装 | 0.5日 |
| 3 | `MultilingualPhonemizer` の依存型変更 | 0.5日 |
| 4 | `TrigramProfile` + `TrigramLanguageDetector` 実装 | 1日 |
| 5 | Trigram プロファイル生成スクリプト（Python/uv） + データ生成 | 1日 |
| 6 | `TrigramProfileLoader` 実装（WebGL対応含む） | 0.5日 |
| 7 | `LatinSegmentRefiner` + `HybridLanguageDetector` 実装 | 1日 |
| 8 | 単体テスト + 統合テスト | 1日 |
| 9 | パフォーマンス検証 + 閾値チューニング | 0.5日 |
| **合計** | | **6.5日** |

### 2.7 ファイル配置

**新規ファイル**:

| ファイルパス | 種別 |
|------------|------|
| `Runtime/Core/Phonemizers/Multilingual/ILanguageDetector.cs` | interface |
| `Runtime/Core/Phonemizers/Multilingual/TrigramLanguageDetector.cs` | class |
| `Runtime/Core/Phonemizers/Multilingual/TrigramProfile.cs` | class |
| `Runtime/Core/Phonemizers/Multilingual/TrigramProfileLoader.cs` | class |
| `Runtime/Core/Phonemizers/Multilingual/HybridLanguageDetector.cs` | class |
| `Runtime/Core/Phonemizers/Multilingual/LatinSegmentRefiner.cs` | class |
| `StreamingAssets/uPiper/LanguageProfiles/trigram_profiles.json` | data |
| `Tests/Editor/TrigramLanguageDetectorTests.cs` | test |
| `Tests/Editor/HybridLanguageDetectorTests.cs` | test |

### 2.8 Trigramプロファイル生成

Python スクリプトで UDHR + Wikipedia サブセットコーパス（各言語100K文、Public Domain / CC-BY-SA）から言語別300 trigram のプロファイルを事前生成する。

**正規化ルール**: 小文字化 → NFD分解後 Combining Mark 除去（アクセント除去）→ 非アルファベット→スペース → 連続スペース圧縮 → 語境界スペース追加

**プロファイル検証基準**:
1. 自言語テスト（100文）で自言語が最高スコア
2. 交差検証で誤検出率を測定
3. 言語間上位100 trigram の重複率が50%未満

---

## 3. エージェントチームの役割と人数

**1エージェント構成**（6.5人日）

| 役割 | 担当内容 | 工数 |
|------|---------|------|
| 実装エージェント | Step 1-9 の全実装（インターフェース抽出、Trigram 検出器、プロファイル生成、統合、テスト、チューニング） | 6.5 人日 |

**理由**: 変更対象が `Multilingual/` ディレクトリに集中しており、ILanguageDetector 抽出 → Trigram 実装 → HybridLanguageDetector 統合という直列の依存関係がある。複数エージェントに分割するとインターフェース定義の齟齬やマージコストが工数を上回る。

**推奨実施順序**: Step 1-3（インターフェース抽出・型変更）→ Step 4（Trigram検出器）→ Step 5（プロファイル生成）→ Step 6（ローダー）→ Step 7（統合）→ Step 8-9（テスト・チューニング）

**M2完了後の並行開始**: P4-1 は M2 ゲート通過後に M3/M4 と並行で開始可能。ただし最終統合テストは M4 完了後に実施する。

---

## 4. 提供範囲とテスト項目

### 4.1 提供範囲

| カテゴリ | 範囲内 | 範囲外 |
|---------|-------|-------|
| 言語検出 | ILanguageDetector 抽出、HybridLanguageDetector 実装 | ラテン文字間コードスイッチング検出（文中言語切替） |
| Trigram | TrigramProfile、TrigramLanguageDetector、プロファイル生成スクリプト | ML ベースの言語検出モデル |
| 対象言語 | en, es, fr, pt（LanguageConstants.LatinLanguages の4言語） | スウェーデン語（sv）等の新規言語追加 |
| プロファイルデータ | trigram_profiles.json 生成・配置 | コーパスの継続的更新パイプライン |
| WebGL | TrigramProfileLoader の非同期読み込み対応 | WebGL 固有の最適化 |
| MultilingualPhonemizer | 検出器依存の ILanguageDetector 化 | G2P ロジック自体の変更 |
| テスト | 単体テスト + 統合テスト + パフォーマンステスト | 全言語の網羅的精度ベンチマーク |
| ドキュメント | CLAUDE.md / CHANGELOG.md の最小更新 | ARCHITECTURE ドキュメントの全面書き換え（M5 ドキュメントタスクで対応） |

### 4.2 Unitテスト

**TrigramLanguageDetectorTests**:

| テストメソッド | 内容 |
|--------------|------|
| `ExtractTrigrams_SimpleText_ReturnsCorrectTrigrams` | "hello" から " he", "hel", "ell", "llo", "lo " が抽出されること |
| `ExtractTrigrams_EmptyText_ReturnsEmpty` | 空文字列で空の辞書が返ること |
| `ExtractTrigrams_AccentedText_NormalizesAccents` | "cafe"（アクセント付き）が "cafe" と同じ trigram を生成すること |
| `Detect_EnglishText_ReturnsEn` | "The quick brown fox jumps over the lazy dog" → en（信頼度 > 0.65） |
| `Detect_SpanishText_ReturnsEs` | "El zorro marron rapido salta sobre el perro perezoso" → es |
| `Detect_FrenchText_ReturnsFr` | "Le renard brun rapide saute par dessus le chien paresseux" → fr |
| `Detect_PortugueseText_ReturnsPt` | "A raposa marrom rapida salta sobre o cachorro preguicoso" → pt |
| `Detect_ShortText_ReturnsFallback` | "Hi"（MinCharsForDetection未満）→ null（フォールバック） |
| `Detect_AmbiguousText_ReturnsFallback` | "de la de la de la"（es/fr/pt 拮抗）→ null |

**HybridLanguageDetectorTests**:

| テストメソッド | 内容 |
|--------------|------|
| `SegmentText_JapaneseAndFrench_CorrectSegments` | "こんにちは Bonjour le monde et bienvenue" → [ja, fr] |
| `SegmentText_ChineseAndSpanish_CorrectSegments` | "你好世界 Buenos dias como estas" → [zh, es] |
| `SegmentText_LatinOnly_SingleLanguageList_SkipsTrigram` | ["ja", "en"] で "Bonjour le monde" → en（フォールバック維持） |
| `SegmentText_NoProfiles_FallsBackToUnicode` | trigramDetector=null で "Bonjour le monde" → en（Unicode のみ） |

**TrigramProfileTests**:

| テストメソッド | 内容 |
|--------------|------|
| `ComputeSimilarity_IdenticalProfile_ReturnsHigh` | 同一プロファイル同士で高スコア |
| `ComputeSimilarity_DifferentProfile_ReturnsLow` | 異なる言語プロファイルで低スコア |
| `ComputeSimilarity_EmptyInput_ReturnsZero` | 空入力で0を返す |

**LatinSegmentRefinerTests**:

| テストメソッド | 内容 |
|--------------|------|
| `Refine_NonLatinSegment_PassesThrough` | ja/zh/ko セグメントが変更されないこと |
| `Refine_ShortLatinSegment_KeepsDefault` | 15文字未満のラテンセグメントがフォールバック維持 |
| `Refine_LongLatinSegment_Reclassifies` | 十分な長さのフランス語テキストが fr に再分類 |

**追加テスト**:

| テストメソッド | 内容 |
|--------------|------|
| `Detect_TextWithAccentedBorrowings_StillDetectsCorrectly` | cafe, resume等のアクセント付き借用語が混在するテキストで正しい言語が検出されること |
| `SegmentText_CJKAndLatinMixed_CorrectlySplits` | 既存UnicodeLanguageDetector.SegmentTextがCJK+ラテン混在文で正しく分割することの明示的テスト |

### 4.3 E2Eテスト

**混合テキストの統合テスト**:

| テスト | 入力 | 期待セグメント |
|-------|------|--------------|
| ja + en | "こんにちは The weather is great" | [ja: "こんにちは ", en: "The weather is great"] |
| ja + fr | "今日は Bonjour comment allez vous" | [ja: "今日は ", fr: "Bonjour comment allez vous"] |
| zh + es | "你好世界 Buenos dias como estas" | [zh: "你好世界 ", es: "Buenos dias como estas"] |
| ja + en + ja | "おはよう Good morning ですね" | [ja, en, ja]（既存テスト互換） |

**言語別精度テスト**:

| 言語 | テストケース | 文字数 | 期待精度 |
|------|------------|--------|---------|
| en | "The weather is beautiful today and we should go outside" | 55 | > 95% |
| en | "Hello" | 5 | フォールバック |
| es | "Buenos dias como estas hoy espero que bien" | 43 | > 90% |
| es | "Hola" | 4 | フォールバック |
| fr | "Bonjour comment allez vous aujourd hui" | 39 | > 90% |
| fr | "Merci" | 5 | フォールバック |
| pt | "Bom dia como voce esta hoje" | 28 | > 85% |
| pt | "Ola" | 3 | フォールバック |

**パフォーマンステスト**:

| テスト | 内容 |
|-------|------|
| `SegmentText_Performance_HybridVsUnicode` | 100文字ラテンテキスト x 1000回。HybridLanguageDetector が UnicodeLanguageDetector の2倍以内で完了すること |

**CI テスト**:

| テスト | 内容 |
|-------|------|
| CI `unity-tests.yml` 全通過 | EditMode + PlayMode テストの GREEN 確認 |
| `InferenceEngineDemo` 手動実行 | 6言語ドロップダウンで音声生成が正常に動作することを確認 |

---

## 5. 懸念事項とレビュー項目

### 5.1 技術的懸念

| 懸念 | 深刻度 | 対策 |
|------|--------|------|
| **ロマンス言語間の混同** | 高 | es/fr/pt は言語的類似性が高い。上位 trigram の重複が大きい（" de", "que", "ent" が3言語共通）。300 trigram プロファイルと信頼度閾値 0.65 + マージン 0.05 で緩和。50文字以上で > 80% を目標（キーワード辞書併用導入後に88%を目標） |
| **短文検出の限界** | 中 | 15文字未満は原理的にフォールバック。"Bonjour", "Gracias" 等の単語レベルは検出不可。将来的に辞書ベース補完（piper-plus スウェーデン語パターン）を `LatinSegmentRefiner` に追加可能 |
| **プロファイルデータの品質** | 中 | コーパス選択が検出精度に直結。UDHR + Wikipedia サブセットを使用。自言語テスト + 交差検証 + 重複率チェックで品質担保 |
| **WebGL での非同期読み込み** | 中 | StreamingAssets の同期 File I/O が不可。`TrigramProfileLoader.LoadAsync()` を `MultilingualPhonemizer.InitializeAsync()` 内で呼び出し。ファイルサイズ小（~12KB）のためネットワーク遅延は最小限 |
| **MultilingualPhonemizer への影響範囲** | 中 | `_detector` の型変更のみで、G2P ルーティングロジックは変更なし。`SegmentText()` の戻り値型も同一のため、下流への影響は限定的 |
| **パフォーマンスオーバーヘッド** | 低 | Trigram 追加処理は ~0.08ms（100文字）。TTS 推論の数十〜数百 ms に対して無視可能。ランタイムメモリは ~62KB |
| **固有名詞・借用語の誤検出** | 低 | "Tokyo", "restaurant" 等は言語固有の trigram パターンを持たないが、文脈全体の分布で判定するため単語単位の借用語は影響限定的 |
| **プロファイルの陳腐化** | 低 | 静的プロファイルだが、TTS 入力は標準的な書き言葉が中心のため実用上問題なし |
| **Trigramプロファイル生成のコーパスライセンス** | 中 | Wikipedia(CC BY-SA 3.0)の帰属表示義務あり。THIRD-PARTY-NOTICESファイルを追加し、プロファイルJSONにsourceメタデータを記録する。UDHR(Public Domain)のみでプロファイル生成可能か検証し、可能ならWikipedia依存を排除 |

**LanguageDetectionディレクトリ**: `Runtime/Core/Phonemizers/LanguageDetection/`サブディレクトリを新設し、HybridLanguageDetector/TrigramLanguageDetector/TrigramProfileLoader/LatinSegmentRefinerを配置する。

### 5.2 レビューチェックリスト

- [ ] `ILanguageDetector` が `SegmentText`, `DefaultLatinLanguage`, `Languages` を公開しているか
- [ ] `UnicodeLanguageDetector` が `ILanguageDetector` を実装し、既存動作を維持しているか
- [ ] `MultilingualPhonemizer._detector` の型が `ILanguageDetector` に変更されているか
- [ ] `HybridLanguageDetector` が trigram プロファイル未配置時に Unicode のみモードにフォールバックするか
- [ ] `TrigramLanguageDetector.MinCharsForDetection` が 15 に設定されているか
- [ ] `TrigramLanguageDetector.DefaultConfidenceThreshold` が 0.65f に設定されているか
- [ ] 信頼度スコアの2位マージンチェック（差 < 0.05 でフォールバック）が実装されているか
- [ ] `LatinSegmentRefiner` が非ラテンセグメント（ja, zh, ko）をパススルーしているか
- [ ] `TrigramProfileLoader` が WebGL 非同期読み込みに対応しているか
- [ ] `MultilingualPhonemizerOptions.EnableTrigramDetection` のデフォルトが `true` か
- [ ] ラテン言語が1つのみの場合に trigram 検出がスキップされるか
- [ ] プロファイル生成スクリプトの正規化ルールと `TrigramLanguageDetector.ExtractTrigrams` の正規化ルールが一致しているか
- [ ] `trigram_profiles.json` が `StreamingAssets/uPiper/LanguageProfiles/` に配置されているか
- [ ] 各言語プロファイルが 300 エントリであるか
- [ ] パフォーマンステストで Hybrid が Unicode のみの2倍以内であるか
- [ ] `dotnet format --verify-no-changes` が通過するか

---

## 6. 一から作り直すとしたら

### 6.1 言語検出そのものの設計思想: ルールベース vs 統計ベース vs ML

本タスクは Trigram 頻度分析（統計ベース）を採用するが、一から言語検出を設計するなら以下の3アプローチを比較検討すべきである。

**案A: ルールベース（辞書 + 特殊文字）**

piper-plus のスウェーデン語検出（`refineLatinSegmentsForSwedish`）と同じパターン。各言語の特徴語リスト（20-30語）+ 言語固有文字（n, c 等）でセグメントを再分類する。

- メリット: 実装が極めて単純。メモリフットプリントがほぼゼロ。短い単語でも検出可能（"Bonjour" 自体が特徴語リストに含まれていれば一発）。
- デメリット: 辞書に含まれない単語は検出不可。言語ペアごとに手動で辞書を構築・メンテナンスする必要がある。スケーラビリティが低い（新言語追加時に専門知識が必要）。
- TTS 用途での評価: 入力テキストが短い（1-2文）ことが多い TTS では、実は特徴語辞書の方が Trigram より実用的な精度を出せる可能性がある。特に15文字未満のテキストで Trigram がフォールバックする領域をカバーできる。

**案B: Trigram 頻度分析（本タスクで採用）**

Cavnar & Trenkle (1994) の Out-of-Place 距離法。事前生成した言語別 Trigram プロファイルとの類似度で判定。

- メリット: 言語知識不要でコーパスからプロファイルを自動生成できる。新言語の追加がプロファイル追加のみで可能。学術的に十分な実績がある。
- デメリット: 短文（15文字未満）で信頼性が低い。ロマンス言語間の弁別力に限界がある。プロファイルデータの配布が必要（~12KB）。
- 正直な評価: TTS の入力テキストは一般的に1-3文程度であり、Trigram が有効に機能する50文字以上のテキストばかりではない。20-50文字帯での精度が実用上最も重要だが、ここでの es/fr/pt 間の精度は 75-80% 程度に留まる。

**案C: 軽量 ML モデル（fastText 等）**

事前学習済みの言語判定モデルを使用する。fastText の lid.176 モデル（~1MB）や、ONNX に変換した小規模分類器等。

- メリット: 短文でも高精度。ロマンス言語間の弁別力が Trigram を大幅に上回る。
- デメリット: 追加の ML モデル読み込みが必要（サイズ、初期化時間）。ONNX ランタイムとの競合（Sentis は TTS 推論で使用中）。Unity への統合コストが高い。WebGL でのモデル配布サイズが問題。
- TTS 用途での評価: 精度は最高だが、TTS パイプラインに既に ONNX 推論エンジンがある状態で追加の ML モデルを導入するのはアーキテクチャ的に過剰。

**採用判断**: 本タスクでは案Bを採用する。案Aの辞書ベース補完は将来の `LatinSegmentRefiner` 拡張として残し、短文精度の向上に活用する設計余地を確保している。案Cは v2.0 のスコープに対して過剰であり、uPiper のサイズ・パフォーマンス要件に合わない。

### 6.2 Trigram の代替手法

Trigram（3文字）以外の N-gram サイズも検討に値する:

- **Bigram（2文字）**: プロファイルサイズが小さくなるが、弁別力が大幅に低下。ロマンス言語間では実用的な精度が出ない。
- **4-gram / 5-gram**: 弁別力は向上するが、プロファイルサイズが指数的に増大（4-gram は ~100KB/言語）。短文で有効な N-gram 数が減少し、かえって精度が下がる場合がある。
- **可変長 N-gram（2-5 の混合）**: 最も高い精度が期待できるが、実装・プロファイル構造の複雑度が上がる。

Trigram は N-gram 言語検出の「スイートスポット」として広く認知されており（Cavnar & Trenkle 1994 以降の多数の追試で確認）、変更する強い動機はない。

### 6.3 ILanguageDetector インターフェースの設計

本タスクでは最小限のインターフェース（`SegmentText`, `DefaultLatinLanguage`, `Languages`）を定義するが、一から設計するなら以下を検討すべきである:

- **信頼度スコアの公開**: `SegmentText` の戻り値に各セグメントの信頼度を含める（`List<(string language, string text, float confidence)>`）。呼び出し元が信頼度に基づいてフォールバック戦略を選択可能になる。
- **非同期 API**: `SegmentTextAsync` を追加し、プロファイルの遅延読み込みやリモート言語検出サービスへの拡張を可能にする。
- **検出コンテキスト**: セッション内の前回検出結果を `DefaultLatinLanguage` ヒントとして使用する適応的検出（設計ドキュメント 13.3 参照）。

ただし、v2.0 では既存の `SegmentText` シグネチャとの互換性を優先し、最小インターフェースを採用する。信頼度スコアや適応的検出は将来の拡張として残す。

### 6.4 piper-plus との設計乖離

uPiper の P4-1 は piper-plus に存在しない機能（N-gram 言語検出）を独自に追加する。piper-plus は Unicode 範囲ベース + スウェーデン語特化の後処理に留まっており、汎用的なラテン言語検出は未実装である。

この設計乖離は意図的であり、uPiper が piper-plus のフォークではなく独立したプロジェクトとして進化する方向性を示している。ただし、`ILanguageDetector` インターフェースの設計は piper-plus の `SegmentText` 関数シグネチャとの互換性を維持し、将来的にアルゴリズムを共有する余地を残している。

### 6.5 現設計の正直な弱点

1. **15文字の閾値が TTS 用途で厳しい**: TTS への入力は短いフレーズが多く、"Bonjour" (7文字), "Buenos dias" (11文字) 等が検出不可。辞書ベース補完（設計ドキュメント 13.2）を早期に導入すべきだった可能性がある。
2. **プロファイル生成がオフライン前提**: Python スクリプトで事前生成する設計のため、ユーザーがカスタムコーパスから独自プロファイルを生成する手段がランタイムに存在しない。
3. **ラテン文字内コードスイッチング非対応**: "I went to the bibliotheque" のようなラテン文字間の言語混合は検出不可。サブセグメント分割のスライディングウィンドウ方式は v2.0 スコープ外。
4. **Trigram プロファイルの StreamingAssets 配置がプラットフォーム間で非統一**: デスクトップでは同期ファイル読み込み、WebGL では `UnityWebRequest` 非同期読み込みと、`TrigramProfileLoader` に2パスが必要。P1-2（pua.json ランタイム読み込み）と同一の WebGL 制約パターンだが、プロファイルファイルが増えるたびにローダーの複雑度が上がる。StreamingAssets ファイル読み込みの統一ヘルパーを Phase 1 で導入しておくべきだった可能性がある。
5. **精度目標の非対称性**: 50文字以上で en vs 他 > 98% は達成可能だが、es/fr/pt 間 > 88% はロマンス言語の言語学的類似性から楽観的な目標である。実運用で 80-85% 程度に留まる場合、ユーザーが `defaultLatinLanguage` を手動設定した方が結果が良いケースが生じ、N-gram 検出が「賢いが不安定」という評価になるリスクがある。

### 6.6 v2.0 全体俯瞰レビュー

本セクションは P4-1 を v2.0 全16タスクの文脈で俯瞰し、タスク間の整合性と設計一貫性を検証する。

#### P4-1 の独立性: Phase 1-3 との接続点の検証

P4-1 は M5 に配置されているが、M2 完了後（Phase 1 完了後）であれば M3/M4 と並行で開始可能とされている。この独立性の主張を検証する。

**Phase 1 との接続点**:

| P1 チケット | P4-1 との接続 | 独立性への影響 |
|------------|-------------|-------------|
| P1-3 (Dictionary Registry) | `MultilingualPhonemizer` が `Dictionary<string, HandlerEntry>` で言語ハンドラを管理。P4-1 の `ILanguageDetector` は言語検出のみを担当し、ハンドラ呼び出しは `MultilingualPhonemizer` が引き続き行う | 独立。検出結果（language code 文字列）をキーにハンドラを lookup するだけ。Registry の内部構造に依存しない |
| P1-4 (ILanguageG2PHandler) | ハンドラの `Process(text)` メソッドは言語コードで dispatch される。P4-1 はこの dispatch に渡す言語コードの精度を向上させるだけ | 独立。ハンドラインターフェースの変更は不要。`ILanguageG2PHandler.LanguageCode` と `ILanguageDetector` の出力する言語コードが一致していれば動作する |
| P1-5 (G2P 全同期化) | `IPhonemizerBackend` 廃止後、未対応言語は警告ログのみ。P4-1 で新たに検出される言語が `_handlers` に登録されていない場合も同じ警告パス | 独立。ただし P4-1 が es/fr/pt を正しく検出した結果、対応するハンドラが未登録の場合のエラーメッセージが適切か確認が必要 |

**Phase 2/3 との接続点**:

| チケット | P4-1 との接続 | 独立性への影響 |
|---------|-------------|-------------|
| P2-2 (Prosody フラット配列) | `ILanguageG2PHandler.Process()` の戻り値型が変更されるが、P4-1 はハンドラを呼び出さない（検出のみ） | 完全に独立 |
| P2-3 (NativeArray) | 推論パイプラインの内部変更。言語検出とは無関係 | 完全に独立 |
| P3-6 (SynthesisRequest public) | 外部ユーザーが `LanguageId` を手動指定する低レベル API。P4-1 の自動検出とは別パス | 完全に独立 |

**結論**: P4-1 は Phase 1-3 の変更と真に独立している。唯一の接続点は `MultilingualPhonemizer._detector` フィールドの型変更（`UnicodeLanguageDetector` → `ILanguageDetector`）だが、これは P4-1 自身が行う変更であり、他チケットとの競合はない。M3/M4 と並行開始しても問題ない。

ただし、**最終統合テスト**では以下の組み合わせを検証する必要がある:
- P1-3 の Registry + P4-1 の `HybridLanguageDetector` が連携して、検出された言語コードに対応するハンドラが正しく lookup されること
- P4-1 で新たに es/fr/pt が検出されるようになった場合、それらの言語ハンドラが `InitializeAsync` で正しく初期化されていること

#### ILanguageG2PHandler の言語ルーティングとの接続

P4-1 の `ILanguageDetector.SegmentText()` は `(language, text)` タプルリストを返す。この `language` 文字列が `ILanguageG2PHandler.LanguageCode` と一致している必要がある。現行では両方とも `LanguageConstants` の定数（"ja", "en", "es", "fr", "pt", "zh", "ko"）を使用するため問題ないが、以下の暗黙的結合に注意:

1. **`LanguageConstants.LatinLanguages`**: P4-1 の Trigram 検出対象言語リストは `LatinLanguages` に基づく。新しいラテン言語（例: sv）を追加する場合、`LatinLanguages` への追加 + Trigram プロファイルの追加 + `ILanguageG2PHandler` 実装の追加が必要。この3点セットが `LanguageConstants` で暗黙的に結合されている。
2. **`MultilingualPhonemizerOptions.Languages`**: 初期化時に指定される言語リスト。`HybridLanguageDetector` はこのリストを参照してラテン言語が2つ以上かどうかを判定する。`ILanguageG2PHandler` のハンドラ登録リストとも一致している必要がある。

この暗黙的結合は v2.0 の16タスクを通して `LanguageConstants` に集約される設計方針と一致しているが、明示的な整合性チェック（例: 初期化時に `detector.Languages` と `handlers.Keys` の差集合を警告ログで出力する）があると堅牢性が向上する。

#### v2.0 全体の設計一貫性

P4-1 の設計思想を他チケットと照合する:

| 設計原則 | P4-1 での適用 | 他チケットとの一貫性 |
|---------|-------------|-------------------|
| **インターフェース抽出による DI 可能化** | `ILanguageDetector` 抽出、`MultilingualPhonemizer` への DI | P1-4（`ILanguageG2PHandler`）、P3-2（`IPiperConfigReadOnly`）と同一パターン。v2.0 では「必要になった時点でインターフェースを抽出する」YAGNI 方針で一貫 |
| **ハイブリッド方式（既存 + 新規の組み合わせ）** | Unicode 検出（高速・確実）+ Trigram 検出（ラテン文字のみ） | P1-2（ハードコード固定マッピング + pua.json ランタイム読み込みのフォールバック設計）と同一パターン |
| **フォールバック安全設計** | プロファイル未配置時は Unicode のみモード、短文は `defaultLatinLanguage` | P1-2（pua.json 未配置時はハードコードフォールバック）と同一パターン。v2.0 全体で「新機能が利用不可でも既存動作を維持する」方針が一貫 |
| **StreamingAssets データファイルの配置** | `StreamingAssets/uPiper/LanguageProfiles/trigram_profiles.json` | P1-2（`StreamingAssets/uPiper/pua.json`）、既存辞書（`StreamingAssets/uPiper/Dictionaries/`）と同一の配置規約 |

#### beta リリース判定への影響

P4-1 は M5（RC リリース）に配置されており、M4（beta）の判定基準には含まれない。ただし、P4-1 の実装が M3/M4 と並行して進む場合、以下の点に注意:

- P4-1 の `MultilingualPhonemizer._detector` 型変更は、M3/M4 で `MultilingualPhonemizer` に加えられる他の変更（P2-2 のハンドラ戻り値型変更等）とコンフリクトする可能性がある。P4-1 のブランチは M4 完了後にリベースして最終統合する運用が安全。
- P4-1 が RC に間に合わない場合でも、`EnableTrigramDetection = false`（デフォルト true だが設定可能）で無効化して RC リリースは可能。ただしこの場合、v2.0 GA までに P4-1 を完了させるか、v2.1 に延期するかの判断が必要。

---

## 7. 後続タスクへの連絡事項

### v2.0 リリースに向けたドキュメント整備

P4-1 完了後、M5 の残りタスクとして以下のドキュメント整備が必要:

1. **CLAUDE.md 更新**: 主要コンポーネント表に `ILanguageDetector`, `HybridLanguageDetector`, `TrigramLanguageDetector` を追加。データフロー図の言語検出部分を更新。
2. **ARCHITECTURE_ja.md / ARCHITECTURE_en.md 更新**: 言語検出サブシステムのアーキテクチャ図・説明を追加。
3. **CHANGELOG.md**: 破壊的変更として `UnicodeLanguageDetector` → `ILanguageDetector` 化を記載。新機能として Trigram 言語検出を記載。
4. **マイグレーションガイド**: `MultilingualPhonemizerOptions` に `LanguageDetector` / `EnableTrigramDetection` プロパティが追加された旨を記載。カスタム検出器の注入方法を例示。

### 将来の拡張ポイント

P4-1 の設計は以下の拡張を想定して余地を残している（v2.0 スコープ外）:

- **スウェーデン語 (sv) 対応**: `trigram_profiles.json` に sv プロファイルを追加するだけで対応可能。`LanguageConstants.LatinLanguages` への追加も必要。
- **辞書ベース補完**: `LatinSegmentRefiner` に言語別特徴語辞書を追加し、15文字未満の短文精度を向上。
- **コンテキスト累積**: 同一セッション内で前回の検出結果を次回の `defaultLatinLanguage` ヒントとして使用する適応的検出。
- **piper-plus辞書ベースrefine連携**: piper-plusが将来ラテン言語の辞書ベースrefineを追加した場合、LatinSegmentRefinerの第一段として辞書チェックを組み込みTrigramをフォールバックとする二段構成を検討する。