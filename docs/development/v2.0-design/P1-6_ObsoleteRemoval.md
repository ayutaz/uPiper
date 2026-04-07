# P1-6: Obsolete コンストラクタ削除 — 技術設計書

## 概要

v1.4.0 で `[Obsolete]` 指定済みの旧 API を v2.0 で削除する。対象は `MultilingualPhonemizer` の14引数コンストラクタと、`MultilingualPhonemizerOptions` の2つのレガシープロパティ。

---

## 1. 削除対象の完全リスト

### 1.1 `MultilingualPhonemizer` — 旧コンストラクタ（14引数）

| 項目 | 内容 |
|------|------|
| ファイル | `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/MultilingualPhonemizer.cs` |
| 行番号 | L114-142 |
| メンバー | `public MultilingualPhonemizer(IReadOnlyList<string>, string, DotNetG2PPhonemizer, IPhonemizerBackend, SpanishG2PEngine, FrenchG2PEngine, PortugueseG2PEngine, ChineseG2PEngine, IPhonemizerBackend, KoreanG2PEngine)` |
| Obsolete メッセージ | `"Use the constructor that takes MultilingualPhonemizerOptions instead. This constructor will be removed in v2.0."` |
| 代替 API | `MultilingualPhonemizer(MultilingualPhonemizerOptions options)` (L87-109) |

### 1.2 `MultilingualPhonemizerOptions.EnPhonemizer` — レガシー英語バックエンド

| 項目 | 内容 |
|------|------|
| ファイル | `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/MultilingualPhonemizerOptions.cs` |
| 行番号 | L32-33 |
| メンバー | `public IPhonemizerBackend EnPhonemizer { get; set; }` |
| Obsolete メッセージ | `"Use EnEngine instead. This property will be removed in v2.0."` |
| 代替 API | `public EnglishG2PEngine EnEngine { get; set; }` (L29) |

### 1.3 `MultilingualPhonemizerOptions.KoPhonemizer` — レガシー韓国語バックエンド

| 項目 | 内容 |
|------|------|
| ファイル | `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/MultilingualPhonemizerOptions.cs` |
| 行番号 | L48-49 |
| メンバー | `public IPhonemizerBackend KoPhonemizer { get; set; }` |
| Obsolete メッセージ | `"Use KoG2PEngine instead. This property will be removed in v2.0."` |
| 代替 API | `public KoreanG2PEngine KoG2PEngine { get; set; }` (L52) |

### 1.4 `PiperTTS.CreateDummyAudioClip` — ダミー音声生成

| 項目 | 内容 |
|------|------|
| ファイル | `Assets/uPiper/Runtime/Core/PiperTTS.cs` |
| 行番号 | L1323-1344 |
| メンバー | `private AudioClip CreateDummyAudioClip(string text)` |
| Obsolete メッセージ | `"This is a fallback method. Use InferenceAudioGenerator for actual TTS."` |
| 代替 API | `InferenceAudioGenerator`（正規の音声生成パス） |

---

## 2. 関連する内部フィールド・メソッドの削除

`[Obsolete]` プロパティ削除に伴い、以下の内部実装も削除対象となる。

### 2.1 `MultilingualPhonemizer` 内部フィールド

| フィールド | 行番号 | 説明 |
|-----------|--------|------|
| `_enPhonemizer` (IPhonemizerBackend) | L60 | `EnPhonemizer` が削除されれば不要 |
| `_koPhonemizer` (IPhonemizerBackend) | L65 | `KoPhonemizer` が削除されれば不要 |

### 2.2 `MultilingualPhonemizer` 内部メソッド

| メソッド | 行番号 | 説明 |
|---------|--------|------|
| `ProcessFallbackAsync(string, string, CancellationToken)` | L647-664 | `IPhonemizerBackend` 経由の非同期フォールバック。P1-5 で削除予定 |
| `GetBackendForLanguage(string)` | L668-678 | `_enPhonemizer`/`_koPhonemizer` を返すスイッチ。P1-5 で削除予定 |

### 2.3 Dispose 内の参照

| 箇所 | 行番号 | 変更内容 |
|------|--------|---------|
| `_enPhonemizer?.Dispose()` | L435 | 削除 |
| `_koPhonemizer?.Dispose()` | L444 | 削除 |

### 2.4 InitializeAsync 内の参照

| 箇所 | 行番号 | 変更内容 |
|------|--------|---------|
| `&& _enPhonemizer == null` (英語初期化条件) | L168 | 条件から除去 |
| `&& _koPhonemizer == null` (全バックエンド null チェック) | L271-273 | 条件から除去 |

---

## 3. `#pragma warning disable CS0618` 除去箇所

### 3.1 ランタイムコード（3箇所）

| ファイル | 行番号 | 理由 | 対応 |
|---------|--------|------|------|
| `MultilingualPhonemizer.cs` | L100-103 | Options コンストラクタ内で `EnPhonemizer`/`KoPhonemizer` を読み取り | プロパティ削除後に pragma と共に該当行を削除 |
| `PiperTTS.cs` | L734-736 | `CreateDummyAudioClip` 呼び出し | メソッド削除後に pragma とフォールバックコードを削除 |
| `PiperTTS.cs` | L742-744 | 同上（2箇所目） | 同上 |

### 3.2 デモコード（1箇所）

| ファイル | 行番号 | 理由 | 対応 |
|---------|--------|------|------|
| `InferenceEngineDemo.cs` | L18 (ファイル冒頭) | 旧コンストラクタ使用 | Options版コンストラクタに書き換え後、pragma 削除 |

### 3.3 テストコード（8箇所）

| ファイル | 行番号 | 対応 |
|---------|--------|------|
| `Tests/Editor/MultilingualPhonemizerTests.cs` | L5 | Options 版に書き換え後、pragma 削除 |
| `Tests/Editor/Phonemizers/MultilingualAutoPromotionTests.cs` | L7 | 同上 |
| `Tests/Editor/Phonemizers/MultilingualPhonemizerDeepTests.cs` | L13 | 同上 |
| `Tests/Editor/Phonemizers/MultilingualPhonemizerPhase5Tests.cs` | L10 | 同上 |
| `Tests/Editor/Phonemizers/ChinesePhonemizerTests.cs` | L8 | 同上 |
| `Tests/Editor/Phonemizers/MultilingualPhonemizerEosTests.cs` | L9 | **要注意**: `IPhonemizerBackend` スタブ依存あり（後述） |
| `Tests/Runtime/MultilingualPipelineTests.cs` | L10 | Options 版に書き換え後、pragma 削除 |
| `Tests/Runtime/MultilingualModelPipelineTests.cs` | L10 | 同上 |

---

## 4. テスト更新が必要な箇所

### 4.1 単純な書き換え（旧コンストラクタ → Options 版）

以下のテストファイルは、旧コンストラクタを `MultilingualPhonemizerOptions` に機械的に書き換えるだけで対応可能。

| ファイル | 旧コンストラクタ呼び出し数 | 備考 |
|---------|--------------------------|------|
| `MultilingualPhonemizerTests.cs` | 6箇所 | 基本テスト。全て単純な言語リスト+defaultLatinLanguage |
| `MultilingualAutoPromotionTests.cs` | 3箇所 | SupportedLanguages/DefaultLanguage 経由 |
| `MultilingualPhonemizerDeepTests.cs` | 6箇所 | 言語検出・セグメント分割テスト |
| `MultilingualPhonemizerPhase5Tests.cs` | 17箇所 | Phase 5 (7言語) テスト。全て言語リストのみ |
| `ChinesePhonemizerTests.cs` | 1箇所 | 中国語テスト |
| `MultilingualPipelineTests.cs` | 1箇所 | パイプライン統合テスト |
| `MultilingualModelPipelineTests.cs` | 1箇所 | 多言語モデル統合テスト |

**書き換えパターン**:
```csharp
// Before (旧コンストラクタ)
var phonemizer = new MultilingualPhonemizer(
    new[] { "ja", "en" },
    defaultLatinLanguage: "en");

// After (Options 版)
var phonemizer = new MultilingualPhonemizer(
    new MultilingualPhonemizerOptions
    {
        Languages = new[] { "ja", "en" },
        DefaultLatinLanguage = "en"
    });
```

### 4.2 要注意: `IPhonemizerBackend` スタブ依存テスト

| ファイル | 呼び出し数 | スタブ使用 |
|---------|-----------|-----------|
| `MultilingualPhonemizerEosTests.cs` | 11箇所 | **8箇所で `enPhonemizer: enStub` を使用** |

このファイルは `StubPhonemizerBackend` (L24-46) を定義し、`IPhonemizerBackend` 経由で英語音素結果を制御している。P1-5 で `IPhonemizerBackend` が廃止されるため、テスト戦略の見直しが必要。

**対応選択肢**:

**(A) P1-4 の `ILanguageG2PHandler` に移行**:
- `StubPhonemizerBackend` を `ILanguageG2PHandler` のスタブに書き換え
- Options に `ILanguageG2PHandler` のカスタムハンドラ注入口を用意
- P1-4 と P1-6 を同時に実施する場合に最適

**(B) `EnglishG2PEngine` の実インスタンスを使用**:
- EOS テストの目的は「中間セグメントの EOS トークン除去」の検証
- 実際の `EnglishG2PEngine` 出力でも同じ検証は可能だが、出力内容が固定でなくなる
- テストの決定性が低下するため非推奨

**推奨**: 選択肢 (A) — P1-4 と同時実施

### 4.3 `InferenceEngineDemo.cs` の書き換え

| ファイル | 行番号 | 変更内容 |
|---------|--------|---------|
| `InferenceEngineDemo.cs` | L227-230 | Options 版コンストラクタに書き換え |

```csharp
// Before
_multilingualPhonemizer = new MultilingualPhonemizer(
    SupportedLanguages,
    defaultLatinLanguage: defaultLatinLanguage,
    jaPhonemizer: _japanesePhonemizer);

// After
_multilingualPhonemizer = new MultilingualPhonemizer(
    new MultilingualPhonemizerOptions
    {
        Languages = SupportedLanguages,
        DefaultLatinLanguage = defaultLatinLanguage,
        JaPhonemizer = _japanesePhonemizer
    });
```

---

## 5. `IPhonemizerBackend` インターフェース自体の削除

`EnPhonemizer`/`KoPhonemizer` の削除に伴い、`IPhonemizerBackend` を参照する箇所がなくなる場合はインターフェース自体も削除可能。

### 5.1 `IPhonemizerBackend` の参照箇所（全体）

| ファイル | 参照種別 |
|---------|---------|
| `Backend/IPhonemizerBackend.cs` | 定義 |
| `MultilingualPhonemizerOptions.cs` L33, L49 | プロパティ型 (Obsolete) |
| `MultilingualPhonemizer.cs` L60, L65, L119, L124, L668 | フィールド・パラメータ・メソッド戻り値 |
| `MultilingualPhonemizerEosTests.cs` L22, L25 | テストスタブ |

P1-6 で Obsolete プロパティとコンストラクタを削除し、P1-5 で `ProcessFallbackAsync`/`GetBackendForLanguage` を削除すれば、`IPhonemizerBackend` の参照は完全に消える。

### 5.2 `Backend/` ディレクトリの扱い

| ファイル | P1-6 後の状態 | 最終的な扱い |
|---------|--------------|-------------|
| `IPhonemizerBackend.cs` | `MultilingualPhonemizer` 内部フィールドのみ参照 | P1-5 完了後に削除可能 |
| `PhonemeOptions.cs` | `PhonemeResult`, `PhonemeOptions` 等を定義 | `PhonemeResult` は他で使用されている可能性あり。要調査 |

**注意**: `PhonemeResult` は `IPhonemizerBackend.PhonemizeAsync` の戻り値型だが、`MultilingualPhonemizer.ProcessFallbackAsync` 内でも使用されている。P1-5 完了後に `PhonemeResult` の他の参照がなければ、`PhonemeOptions.cs` ごと削除可能。

---

## 6. `PiperTTS.CreateDummyAudioClip` 削除の影響

### 6.1 呼び出し箇所

| ファイル | 行番号 | 状況 |
|---------|--------|------|
| `PiperTTS.cs` L735 | 推論失敗時のフォールバック | catch ブロック内 |
| `PiperTTS.cs` L743 | InferenceGenerator 未初期化時 | else ブロック内 |

### 6.2 削除後の対応

`CreateDummyAudioClip` は無音 AudioClip を返すダミー実装。削除後は:
- catch ブロック: 例外を上位に伝播するか、null を返してエラーハンドリングを呼び出し元に委譲
- else ブロック: 初期化未完了の場合のエラーログ + null 返却 or 例外スロー

推奨: エラー時は `null` を返し、呼び出し元でハンドリング（既に `_onError` コールバックが存在するため）。

---

## 7. 削除順序（依存関係を考慮）

```
Step 1: テスト書き換え（単純な旧コンストラクタ → Options 版）
    ├── MultilingualPhonemizerTests.cs
    ├── MultilingualAutoPromotionTests.cs
    ├── MultilingualPhonemizerDeepTests.cs
    ├── MultilingualPhonemizerPhase5Tests.cs
    ├── ChinesePhonemizerTests.cs
    ├── MultilingualPipelineTests.cs
    └── MultilingualModelPipelineTests.cs

Step 2: デモコード書き換え
    └── InferenceEngineDemo.cs （旧コンストラクタ → Options 版）

Step 3: 旧コンストラクタ削除
    └── MultilingualPhonemizer.cs L114-142 を削除

Step 4: CreateDummyAudioClip 削除
    ├── PiperTTS.cs L1323-1344 メソッド削除
    └── PiperTTS.cs L734-736, L742-744 フォールバックコード修正

Step 5: MultilingualPhonemizerOptions の Obsolete プロパティ削除
    ├── EnPhonemizer プロパティ削除
    ├── KoPhonemizer プロパティ削除
    └── Options コンストラクタ内の pragma warning disable/restore 除去
        + _enPhonemizer, _koPhonemizer フィールド代入行の削除

Step 6: MultilingualPhonemizer 内部フィールド削除
    ├── _enPhonemizer フィールド (L60)
    ├── _koPhonemizer フィールド (L65)
    ├── InitializeAsync 内の _enPhonemizer == null 条件 (L168)
    ├── InitializeAsync 内の _koPhonemizer == null 条件 (L273)
    ├── Dispose 内の _enPhonemizer?.Dispose() (L435)
    └── Dispose 内の _koPhonemizer?.Dispose() (L444)

Step 7: 全 #pragma warning disable CS0618 の除去
    （Step 1-6 完了後に残っている pragma を一括削除）
```

**注意**: Step 5-6 は P1-5 (IPhonemizerBackend 廃止) と重複する。P1-5 が先行する場合は Step 5-6 は P1-5 に含まれ、P1-6 では Step 1-4 + Step 7 のみ実施。

---

## 8. P1-4, P1-5 との依存関係

```
P1-4 (ILanguageG2PHandler 全面移行)
  │
  ├─→ P1-5 (IPhonemizerBackend 廃止)
  │     │
  │     ├─→ ProcessFallbackAsync 削除
  │     ├─→ GetBackendForLanguage 削除
  │     ├─→ IPhonemizerBackend インターフェース削除
  │     └─→ MultilingualPhonemizerOptions.EnPhonemizer / KoPhonemizer 削除  ← P1-6 と重複
  │
  └─→ P1-6 (Obsolete コンストラクタ削除) ← 本タスク
        │
        ├─→ 旧コンストラクタ削除（P1-4 が先行していれば安全）
        ├─→ EnPhonemizer / KoPhonemizer 削除（P1-5 と重複）
        ├─→ CreateDummyAudioClip 削除（独立して実施可能）
        └─→ テスト書き換え
```

### 推奨実施順序

1. **P1-4 → P1-5 → P1-6** が理想的な順序
2. P1-6 の一部（旧コンストラクタ削除、CreateDummyAudioClip 削除、単純テスト書き換え）は P1-4/P1-5 と並行して実施可能
3. `MultilingualPhonemizerEosTests.cs` のスタブ書き換えは P1-4 完了後に実施（`ILanguageG2PHandler` スタブに移行するため）
4. `EnPhonemizer`/`KoPhonemizer` プロパティおよび `_enPhonemizer`/`_koPhonemizer` フィールドの削除は P1-5 と同時または P1-5 完了後に実施

### P1-6 単独で先行実施する場合の最小スコープ

P1-4/P1-5 に先行して P1-6 を実施する場合、以下の最小スコープで実施可能:

- 旧コンストラクタ (14引数) の削除
- テストの Options 版書き換え（EOS テスト以外の35箇所）
- `InferenceEngineDemo.cs` の Options 版書き換え
- `CreateDummyAudioClip` の削除
- **除外**: `EnPhonemizer`/`KoPhonemizer` プロパティ削除（P1-5 に委譲）
- **除外**: `MultilingualPhonemizerEosTests.cs` の `IPhonemizerBackend` スタブ書き換え（P1-4 に委譲）

---

## 9. 変更ファイルサマリー

| ファイル | 変更種別 | Step |
|---------|---------|------|
| `Runtime/Core/Phonemizers/Multilingual/MultilingualPhonemizer.cs` | コンストラクタ削除 + フィールド削除 | 3, 5, 6 |
| `Runtime/Core/Phonemizers/Multilingual/MultilingualPhonemizerOptions.cs` | プロパティ削除 | 5 |
| `Runtime/Core/PiperTTS.cs` | メソッド削除 + フォールバック修正 | 4 |
| `Runtime/Demo/InferenceEngineDemo.cs` | コンストラクタ書き換え | 2 |
| `Tests/Editor/MultilingualPhonemizerTests.cs` | コンストラクタ書き換え (6箇所) | 1 |
| `Tests/Editor/Phonemizers/MultilingualAutoPromotionTests.cs` | コンストラクタ書き換え (3箇所) | 1 |
| `Tests/Editor/Phonemizers/MultilingualPhonemizerDeepTests.cs` | コンストラクタ書き換え (6箇所) | 1 |
| `Tests/Editor/Phonemizers/MultilingualPhonemizerPhase5Tests.cs` | コンストラクタ書き換え (17箇所) | 1 |
| `Tests/Editor/Phonemizers/ChinesePhonemizerTests.cs` | コンストラクタ書き換え (1箇所) | 1 |
| `Tests/Editor/Phonemizers/MultilingualPhonemizerEosTests.cs` | スタブ移行 (P1-4 依存) | P1-4後 |
| `Tests/Runtime/MultilingualPipelineTests.cs` | コンストラクタ書き換え (1箇所) | 1 |
| `Tests/Runtime/MultilingualModelPipelineTests.cs` | コンストラクタ書き換え (1箇所) | 1 |
| `Runtime/Core/Phonemizers/Backend/IPhonemizerBackend.cs` | 削除 (P1-5 後) | P1-5後 |
| `Runtime/Core/Phonemizers/Backend/PhonemeOptions.cs` | 要調査 (PhonemeResult の他参照) | P1-5後 |

**合計**: ランタイム 4ファイル + テスト 8ファイル + デモ 1ファイル = 13ファイル
