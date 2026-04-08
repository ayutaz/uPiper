# P1-6: Obsoleteコンストラクタ削除

**マイルストーン**: M2 - Phase 1 Completion (alpha)
**優先度**: P1
**見積もり**: 1 人日
**依存チケット**: P1-4, P1-5
**後続チケット**: なし（Phase 1 完了ゲート）
**ブランチ名**: `feature/v2.0-P1-6-obsolete-removal`

---

## 1. タスク目的とゴール

### なぜこのタスクが必要か

v1.4.0 で `[Obsolete]` を付与した旧 API が v2.0 リリースまでに残存している。これらは v2.0 の破壊的変更リリースで完全に削除すべきレガシーコードであり、以下の問題を引き起こしている:

1. **テストコードの `#pragma warning disable CS0618` 汚染**: テスト8ファイル + ランタイム3箇所 + デモ1箇所の計12箇所で Obsolete 警告を抑制しており、コード品質シグナルが劣化している。
2. **旧コンストラクタ（14引数）の存在がコードの理解を阻害**: `MultilingualPhonemizer` に2つのコンストラクタが併存し、新規開発者がどちらを使うべきか迷う。
3. **`CreateDummyAudioClip` が正規の音声生成パスを曖昧にする**: ダミー音声生成がフォールバックとして残存し、エラーハンドリングの設計意図が不明確。
4. **P1-4 で `[Obsolete]` 付与された `MultilingualPhonemizerOptions` の個別エンジンプロパティ**（`JaPhonemizer`, `EnEngine`, `EsEngine`, `FrEngine`, `PtEngine`, `ZhEngine`, `KoG2PEngine`）が残存。`Handlers` Dictionary への移行が完了した後もレガシープロパティが API サーフェスに露出している。

### 完了状態（Definition of Done）

- `MultilingualPhonemizer` の14引数 `[Obsolete]` コンストラクタが削除されている
- `MultilingualPhonemizerOptions` の `EnPhonemizer` / `KoPhonemizer` プロパティが削除されている（P1-5 と重複する場合は P1-5 完了を確認）
- `MultilingualPhonemizerOptions` の個別エンジンプロパティ（`JaPhonemizer`, `EnEngine` 等7つ）が削除されている（P1-4 で `[Obsolete]` 付与済み）
- `PiperTTS.CreateDummyAudioClip` が削除されている
- テスト7ファイル（35箇所）+ EOS テスト（11箇所）の旧コンストラクタが Options + Handlers 版に書き換え済み
- `InferenceEngineDemo.cs` が Options 版コンストラクタに書き換え済み
- 全 `#pragma warning disable CS0618` が除去済み
- 全テスト通過（EditMode + PlayMode）
- `dotnet format --verify-no-changes` 通過

---

## 2. 実装する内容の詳細

### Step 1: テスト書き換え -- 単純な旧コンストラクタ → Options + Handlers 版（35箇所）

P1-4 完了後、テストは `MultilingualPhonemizerOptions.Handlers` Dictionary 経由でハンドラを注入するパターンに移行済みのはずだが、旧コンストラクタ直接呼び出しが残存している箇所を Options 版に書き換える。

| ファイル | 旧コンストラクタ呼び出し数 | 備考 |
|---------|--------------------------|------|
| `Tests/Editor/MultilingualPhonemizerTests.cs` | 6箇所 | 基本テスト。全て言語リスト + defaultLatinLanguage |
| `Tests/Editor/Phonemizers/MultilingualAutoPromotionTests.cs` | 3箇所 | SupportedLanguages / DefaultLanguage 経由 |
| `Tests/Editor/Phonemizers/MultilingualPhonemizerDeepTests.cs` | 6箇所 | 言語検出・セグメント分割テスト |
| `Tests/Editor/Phonemizers/MultilingualPhonemizerPhase5Tests.cs` | 17箇所 | Phase 5 (7言語) テスト。全て言語リストのみ |
| `Tests/Editor/Phonemizers/ChinesePhonemizerTests.cs` | 1箇所 | 中国語テスト |
| `Tests/Runtime/MultilingualPipelineTests.cs` | 1箇所 | パイプライン統合テスト |
| `Tests/Runtime/MultilingualModelPipelineTests.cs` | 1箇所 | 多言語モデル統合テスト |

**書き換えパターン**:

```csharp
// Before (旧コンストラクタ)
var phonemizer = new MultilingualPhonemizer(
    new[] { "ja", "en" },
    defaultLatinLanguage: "en");

// After (Options + Handlers 版)
var phonemizer = new MultilingualPhonemizer(
    new MultilingualPhonemizerOptions
    {
        Languages = new[] { "ja", "en" },
        DefaultLatinLanguage = "en"
    });
```

P1-4 で `Handlers` Dictionary が導入されている場合、テストでハンドラを注入するケースは:

```csharp
// After (ハンドラ注入あり)
var phonemizer = new MultilingualPhonemizer(
    new MultilingualPhonemizerOptions
    {
        Languages = new[] { "ja", "en" },
        DefaultLatinLanguage = "en",
        Handlers = new Dictionary<string, ILanguageG2PHandler>
        {
            ["en"] = new StubG2PHandler("en", enPhonemes)
        }
    });
```

### Step 2: テスト書き換え -- EOS テストの `IPhonemizerBackend` スタブ移行（11箇所）

**ファイル**: `Tests/Editor/Phonemizers/MultilingualPhonemizerEosTests.cs`

このファイルは `StubPhonemizerBackend`（`IPhonemizerBackend` 実装）を定義し、8つのテストメソッドで英語音素結果を制御している。P1-4 で `StubG2PHandler`（`ILanguageG2PHandler` 実装）が作成されているため、スタブの差し替えを行う。

**書き換え方針**:
- `StubPhonemizerBackend` クラス定義を削除
- `StubG2PHandler`（P1-4 で作成済み）を使用するか、テストファイル内に `ILanguageG2PHandler` のローカルスタブを定義
- 旧コンストラクタ経由の `enPhonemizer: enStub` を `Options.Handlers = { ["en"] = enHandler }` に置換

**対象テストメソッド**:

| テストメソッド | StubPhonemizerBackend 使用 |
|--------------|--------------------------|
| `EosLikeTokens_ContainsPuaQuestionMarkers` | enStub (EN intermediate) |
| `IntermediateSegment_PuaEos_Stripped` | enStub (EN intermediate) |
| `FinalSegment_PuaEos_Preserved` | enStub (EN only) |
| `IntermediateSegment_StandardEos_Stripped` | enStub (EN intermediate) |
| `NonJapaneseSegment_NoPadStripping` | enStub (EN only) |
| `AllEosTokens_StrippedFromIntermediateSegment` | enStub (EN intermediate) |
| `IntermediateSegment_NoEos_PassesThrough` | enStub (EN intermediate) |
| `ProsodyArrays_AlignedAfterEosStrip` | enStub (EN intermediate) |

```csharp
// Before (IPhonemizerBackend スタブ)
var enStub = new StubPhonemizerBackend(enResult);
var mp = new MultilingualPhonemizer(
    new[] { "en", "ja" },
    enPhonemizer: enStub,
    jaPhonemizer: jaPhonemizer);

// After (ILanguageG2PHandler スタブ)
var enHandler = new StubG2PHandler("en", enPhonemes, enA1, enA2, enA3);
var mp = new MultilingualPhonemizer(
    new MultilingualPhonemizerOptions
    {
        Languages = new[] { "en", "ja" },
        Handlers = new Dictionary<string, ILanguageG2PHandler>
        {
            ["en"] = enHandler
        }
    });
```

### Step 3: デモコード書き換え

**ファイル**: `Runtime/Demo/InferenceEngineDemo.cs` (L227-230)

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
        Handlers = new Dictionary<string, ILanguageG2PHandler>
        {
            ["ja"] = new JapaneseG2PHandler(_japanesePhonemizer)
        }
    });
```

ファイル冒頭の `#pragma warning disable CS0618` (L18) も削除する。

### Step 4: 旧コンストラクタ削除

**ファイル**: `Runtime/Core/Phonemizers/Multilingual/MultilingualPhonemizer.cs`

L114-142 の14引数 `[Obsolete]` コンストラクタを物理削除する。Options 版コンストラクタ（L87-109）のみ残す。

### Step 5: `CreateDummyAudioClip` 削除

**ファイル**: `Runtime/Core/PiperTTS.cs`

1. L1323-1344 の `CreateDummyAudioClip` メソッド定義を削除
2. L734-736 のフォールバック呼び出し（catch ブロック）を修正:
   - ダミー AudioClip 返却を削除し、`null` を返却
   - 既存の `_onError` コールバックにエラー情報を委譲
3. L742-744 のフォールバック呼び出し（else ブロック）を修正:
   - 初期化未完了のエラーログ + `null` 返却

```csharp
// Before (catch ブロック)
#pragma warning disable CS0618
return CreateDummyAudioClip(text);
#pragma warning restore CS0618

// After
_onError?.Invoke(ex);
PiperLogger.LogError($"[PiperTTS] Audio generation failed: {ex.Message}");
return null;
```

### Step 6: `MultilingualPhonemizerOptions` のレガシープロパティ削除

**ファイル**: `Runtime/Core/Phonemizers/Multilingual/MultilingualPhonemizerOptions.cs`

以下を削除:

**P1-5 由来（`IPhonemizerBackend` 関連）**:
- `EnPhonemizer` プロパティ + `[Obsolete]` 属性 (L32-33)
- `KoPhonemizer` プロパティ + `[Obsolete]` 属性 (L48-49)

**P1-4 由来（個別エンジンプロパティ）**:
- `JaPhonemizer` プロパティ（`DotNetG2PPhonemizer` 型）
- `EnEngine` プロパティ（`EnglishG2PEngine` 型）
- `EsEngine` プロパティ（`SpanishG2PEngine` 型）
- `FrEngine` プロパティ（`FrenchG2PEngine` 型）
- `PtEngine` プロパティ（`PortugueseG2PEngine` 型）
- `ZhEngine` プロパティ（`ChineseG2PEngine` 型）
- `KoG2PEngine` プロパティ（`KoreanG2PEngine` 型）

削除後、`MultilingualPhonemizerOptions` には以下のプロパティのみ残る:
- `Languages`（`IReadOnlyList<string>`）
- `DefaultLatinLanguage`（`string`）
- `Handlers`（`Dictionary<string, ILanguageG2PHandler>`）-- P1-4 で追加

不要になった `using` 文（`uPiper.Core.Phonemizers.Backend` 等）も削除する。

### Step 7: `MultilingualPhonemizer` 内部フィールド・参照の削除

**ファイル**: `Runtime/Core/Phonemizers/Multilingual/MultilingualPhonemizer.cs`

P1-5 完了を前提に、以下の残存参照を削除:

| 対象 | 行番号 | 変更内容 |
|------|--------|---------|
| `_enPhonemizer` フィールド | L60 | 削除 |
| `_koPhonemizer` フィールド | L65 | 削除 |
| Options 経由の `_enPhonemizer`/`_koPhonemizer` 代入 | L100-103 | 削除（pragma 含む） |
| `InitializeAsync` 内の `_enPhonemizer == null` 条件 | L168 | 条件から除去 |
| `InitializeAsync` 内の `_koPhonemizer == null` 条件 | L271-273 | 条件から除去 |
| `Dispose` 内の `_enPhonemizer?.Dispose()` | L435 | 削除 |
| `Dispose` 内の `_koPhonemizer?.Dispose()` | L444 | 削除 |

**注意**: Step 7 の大部分は P1-5 と重複する。P1-5 が先にマージ済みの場合、これらの削除は P1-5 で完了しており、P1-6 では残存確認のみ行う。P1-5 が未完了の場合は P1-6 に含めて実施する。

### Step 8: 全 `#pragma warning disable CS0618` の除去

Step 1-7 完了後に残っている pragma を一括削除する。

**ランタイム（3箇所）**:

| ファイル | 行番号 | 理由 |
|---------|--------|------|
| `MultilingualPhonemizer.cs` | L100-103 | Options 内 `EnPhonemizer`/`KoPhonemizer` 読み取り -- Step 7 で削除済み |
| `PiperTTS.cs` | L734-736 | `CreateDummyAudioClip` 呼び出し -- Step 5 で削除済み |
| `PiperTTS.cs` | L742-744 | 同上 |

**デモ（1箇所）**:

| ファイル | 行番号 |
|---------|--------|
| `InferenceEngineDemo.cs` | L18 (ファイル冒頭) |

**テスト（8箇所）**:

| ファイル | 行番号 |
|---------|--------|
| `Tests/Editor/MultilingualPhonemizerTests.cs` | L5 |
| `Tests/Editor/Phonemizers/MultilingualAutoPromotionTests.cs` | L7 |
| `Tests/Editor/Phonemizers/MultilingualPhonemizerDeepTests.cs` | L13 |
| `Tests/Editor/Phonemizers/MultilingualPhonemizerPhase5Tests.cs` | L10 |
| `Tests/Editor/Phonemizers/ChinesePhonemizerTests.cs` | L8 |
| `Tests/Editor/Phonemizers/MultilingualPhonemizerEosTests.cs` | L9 |
| `Tests/Runtime/MultilingualPipelineTests.cs` | L10 |
| `Tests/Runtime/MultilingualModelPipelineTests.cs` | L10 |

---

## 3. エージェントチームの役割と人数

**1エージェント構成**（1 人日）

| 役割 | 担当内容 | 工数 |
|------|---------|------|
| 実装エージェント | Step 1-8 の全実装 | 1 人日 |

**理由**: 全変更が「削除」と「機械的な書き換え」が中心。新規ロジックの実装がほぼなく、複数エージェントに分割する利点がない。P1-4/P1-5 完了後の「残骸掃除」という性質上、コード理解コストも低い。

**推奨実施順序**: Step 1 → Step 2 → Step 3 → Step 4 → Step 5 → Step 6 → Step 7 → Step 8 → 全テスト実行 → `dotnet format` 確認

**P1-5 の完了状態による分岐**:
- P1-5 完了済みの場合: Step 6 の `EnPhonemizer`/`KoPhonemizer` 削除と Step 7 の `_enPhonemizer`/`_koPhonemizer` 削除は確認のみ
- P1-5 未完了の場合: Step 6-7 を P1-6 に含めて実施（P1-5 のスコープの一部を P1-6 が吸収）

---

## 4. 提供範囲とテスト項目

### 4.1 提供範囲

| カテゴリ | 範囲内 | 範囲外 |
|---------|-------|-------|
| コンストラクタ | 14引数 `[Obsolete]` コンストラクタの削除 | Options 版コンストラクタのロジック変更 |
| Options プロパティ | `EnPhonemizer`/`KoPhonemizer` + 個別エンジンプロパティ7つの削除 | `Handlers` Dictionary の設計変更（P1-3 スコープ） |
| CreateDummyAudioClip | メソッド削除 + フォールバックコードの null 返却化 | `PiperTTS` のエラーハンドリング全面再設計 |
| テスト | 旧コンストラクタ → Options 版書き換え（全46箇所） | テストケースの追加・テスト戦略の見直し |
| デモ | `InferenceEngineDemo.cs` の Options 版書き換え | デモ UI の機能変更 |
| pragma | 全 `#pragma warning disable CS0618` の除去 | 他の pragma warning の整理 |
| `IPhonemizerBackend` | フィールド参照の削除（P1-5 と重複） | インターフェース定義の削除（P1-5 スコープ） |
| ドキュメント | なし（API 削除のみのためドキュメント変更不要） | CHANGELOG.md への記載（リリース時に一括対応） |

### 4.2 Unit テスト

本チケットは新規テストの追加を行わない。全変更は既存テストの書き換え（旧 API → 新 API）であり、テストのカバレッジ自体は変化しない。

**書き換え対象テストの振る舞い不変確認**:

| テストファイル | テスト数 | 確認事項 |
|--------------|--------|---------|
| `MultilingualPhonemizerTests.cs` | 10 | Options 版書き換え後、同一結果 |
| `MultilingualAutoPromotionTests.cs` | 3+ | auto-promotion テストの振る舞い不変 |
| `MultilingualPhonemizerDeepTests.cs` | 20 | 言語検出・セグメント分割の振る舞い不変 |
| `MultilingualPhonemizerPhase5Tests.cs` | 18 | 7言語テストの振る舞い不変 |
| `ChinesePhonemizerTests.cs` | 全件 | 中国語テストの振る舞い不変 |
| `MultilingualPhonemizerEosTests.cs` | 10 | EOS トリム処理の振る舞い不変（スタブ移行後） |
| `MultilingualPipelineTests.cs` | 全件 | パイプライン統合テストの振る舞い不変 |
| `MultilingualModelPipelineTests.cs` | 全件 | 多言語モデル統合テストの振る舞い不変 |

### 4.3 E2E テスト

| テスト | 内容 |
|-------|------|
| `InferenceEngineDemo` 手動実行 | Options 版書き換え後、6言語ドロップダウンで音声生成が正常に動作することを確認 |
| CI `unity-tests.yml` 全通過 | EditMode + PlayMode テストの GREEN 確認 |
| `dotnet format --verify-no-changes` | フォーマットチェック通過 |

---

## 5. 懸念事項とレビュー項目

### 5.1 技術的懸念

| 懸念 | 深刻度 | 対策 |
|------|--------|------|
| **P1-5 との重複による二重作業** | 中 | `EnPhonemizer`/`KoPhonemizer` プロパティと `_enPhonemizer`/`_koPhonemizer` フィールドの削除は P1-5 と重複する。マイルストーン M2 の推奨マージ順序（P1-3 → P1-5 → P1-6）に従い、P1-5 完了後に P1-6 を実施する。P1-5 で削除済みの箇所は P1-6 では確認のみ |
| **EOS テストのスタブ移行の決定性低下** | 中 | `StubPhonemizerBackend` は固定の `PhonemeResult` を返すが、`StubG2PHandler` も同等の固定出力を返す設計とする。P1-4 で作成された `StubG2PHandler` のインターフェースを確認し、EOS テストに必要な出力制御（音素配列 + Prosody 配列の固定指定）が可能であることを事前検証する |
| **`CreateDummyAudioClip` 削除後のエラーパス** | 低 | 削除後は `null` 返却 + `_onError` コールバック。呼び出し元で `AudioClip` が `null` の場合のハンドリングが必要。既存の `PiperTTS` 利用者が `null` チェックをしていない場合、`NullReferenceException` が発生する。v2.0 は破壊的変更リリースのため許容範囲だが、CHANGELOG での明記が必要 |
| **P1-4 の `[Obsolete]` 付与確認** | 低 | P1-4 の Step 5 で `MultilingualPhonemizerOptions` の個別エンジンプロパティに `[Obsolete]` が付与されているはず。P1-6 実施前に `[Obsolete]` の存在を確認し、付与されていなければ先に付与してから削除する（1リリースの猶予なしの即削除は避けたい。ただし v2.0 = メジャーバージョンアップのため、SemVer 上は問題なし） |
| **テスト46箇所の機械的書き換えにおける漏れ** | 低 | 書き換え後に `new MultilingualPhonemizer(` で Grep し、Options 版以外の呼び出しが残っていないことを確認する。また、`CS0618` で Grep し、pragma が残っていないことを確認する |

### 5.2 レビューチェックリスト

- [ ] `MultilingualPhonemizer` に `[Obsolete]` コンストラクタが残存していないこと
- [ ] `MultilingualPhonemizerOptions` に `EnPhonemizer` / `KoPhonemizer` プロパティが残存していないこと
- [ ] `MultilingualPhonemizerOptions` に個別エンジンプロパティ（`JaPhonemizer`, `EnEngine` 等）が残存していないこと
- [ ] `PiperTTS.CreateDummyAudioClip` メソッドが削除されていること
- [ ] `CreateDummyAudioClip` の呼び出し箇所が null 返却 + エラーハンドリングに置換されていること
- [ ] テスト全46箇所が Options + Handlers 版に書き換えられていること
- [ ] `InferenceEngineDemo.cs` が Options 版コンストラクタを使用していること
- [ ] `#pragma warning disable CS0618` が全箇所で除去されていること（Grep で `CS0618` が0件であること）
- [ ] `_enPhonemizer` / `_koPhonemizer` フィールドが `MultilingualPhonemizer` に残存していないこと
- [ ] 不要な `using` 文が削除されていること（`uPiper.Core.Phonemizers.Backend` 等）
- [ ] 全テスト通過（EditMode + PlayMode）
- [ ] `dotnet format --verify-no-changes` パス

---

## 6. 一から作り直すとしたら

### 6.1 Deprecation ポリシーの設計

v1.4.0 で `[Obsolete]` を付与し v2.0 で削除するという現行のアプローチは、SemVer の原則に従った正統的な Deprecation サイクルである。しかし、もしゼロから Deprecation ポリシーを設計するなら、以下の改善が考えられる。

**現行の問題点**:
- `[Obsolete]` メッセージに「v2.0 で削除」と書かれているが、v2.0 がいつリリースされるかはメッセージからは不明。利用者はタイムラインを把握できない。
- `#pragma warning disable CS0618` でコンパイラ警告を抑制すると、Obsolete の警告効果が完全に無効化される。uPiper 自身のテストコードが12箇所で抑制しており、「内部コードですら旧 API を使い続けている」状態。
- Obsolete の「警告レベル」（`IsError = false`）と「エラーレベル」（`IsError = true`）の使い分けがない。全て警告レベルで付与されているため、利用者が移行を先送りしやすい。

**ゼロベースのポリシー案**:

```
v1.4.0: [Obsolete("...", error: false)] で警告付与 + 代替APIの提供
v1.5.0: [Obsolete("...", error: true)]  に昇格（コンパイルエラー化）
v2.0.0: メソッド/プロパティの物理削除
```

2段階の Deprecation サイクルにより、利用者は v1.5.0 でコンパイルエラーとして強制的に移行を促される。v2.0.0 での物理削除時には既に全利用者が新 API に移行済みとなる。

**不採用の理由**: uPiper の利用者層（Unity 開発者）は NuGet パッケージのバージョン管理に慣れておらず、Unity Package Manager 経由のアップデートで一気に v2.0 に移行するケースが多い。中間バージョン（v1.5.0）を挟む利点が薄い。また、v1.4.0 → v2.0 の間にマイナーリリースの計画がないため、2段階サイクルは実質的に機能しない。

### 6.2 `CreateDummyAudioClip` が存在した設計上の問題

`CreateDummyAudioClip` は「推論が失敗した場合でも AudioClip を返す」というフォールバック設計である。もしゼロから設計するなら、TTS API は以下のいずれかの方式を取るべきだった:

**案 A: Result 型パターン**
```csharp
public async Task<TTSResult> SynthesizeAsync(string text)
{
    // TTSResult.Success(audioClip) or TTSResult.Failure(error)
}
```

**案 B: nullable 返却 + エラーコールバック**
```csharp
public async Task<AudioClip?> SynthesizeAsync(string text)
{
    // 失敗時は null + _onError コールバック
}
```

v2.0 では案 B を採用する（既存の `_onError` コールバックとの整合性）。案 A は Phase 2 以降の public API 再設計（P2-4）で検討する価値がある。

### 6.3 Options パターンの肥大化

`MultilingualPhonemizerOptions` は v1.4.0 で導入されたが、P1-4 で `Handlers` Dictionary が追加され、さらに P1-6 で個別エンジンプロパティが削除されることで、プロパティ構成が大きく変わる:

| バージョン | プロパティ数 | 内容 |
|-----------|------------|------|
| v1.4.0 | 11 | Languages, DefaultLatinLanguage, JaPhonemizer, EnEngine, EnPhonemizer(Obsolete), EsEngine, FrEngine, PtEngine, ZhEngine, KoG2PEngine, KoPhonemizer(Obsolete) |
| P1-4 後 | 12 | 上記 + Handlers（個別エンジンプロパティは [Obsolete] に） |
| P1-6 後 | 3 | Languages, DefaultLatinLanguage, Handlers |

**もしゼロから設計するなら**: 最初から `Handlers` Dictionary のみを受け取る Options にし、個別エンジンプロパティを導入しない。`Handlers` が未指定の言語に対してはファクトリが自動生成するパターンは P1-4 の `CreateDefaultHandler` で既に実現されており、個別プロパティは「型安全な Builder パターン」としての価値はあるが、Dictionary ベースの柔軟性と二重に API サーフェスを持つコストの方が大きかった。

### 6.4 テスト書き換えの構造的コスト

46箇所のテスト書き換えは「機械的な作業」だが、これは v1.4.0 で旧コンストラクタを `[Obsolete]` にした時点で発生が確定していた技術的負債である。もしゼロからなら:

1. **テストヘルパーの早期導入**: テスト内に `CreatePhonemizer(params string[] languages)` のようなファクトリメソッドを用意し、コンストラクタ呼び出しを1箇所に集約する。API 変更時の書き換えが1箇所で済む。
2. **v1.4.0 リリース前にテストを Options 版に移行**: `[Obsolete]` 付与と同時にテストを新 API に移行していれば、`#pragma warning disable` が不要だった。v1.4.0 の開発スケジュールでこれを先送りしたことが、P1-6 の工数1人日の直接原因。

### 6.5 P1-4/P1-5/P1-6 の分割粒度

P1-4（ハンドラ分離）→ P1-5（IPhonemizerBackend 廃止）→ P1-6（Obsolete 削除）の3チケット分割は、`MultilingualPhonemizer.cs` への変更が3回に分散し、マージコンフリクトのリスクを高めている。

**ゼロベースの分割案**: P1-5 と P1-6 を統合し「レガシー API 一括削除」チケットとする。理由:
- P1-5 の `EnPhonemizer`/`KoPhonemizer` 削除と P1-6 の同プロパティ削除が完全に重複
- P1-5 の `_enPhonemizer`/`_koPhonemizer` フィールド削除と P1-6 の同フィールド削除が重複
- 両チケットとも P1-4 完了後の「掃除」であり、独立した設計判断がほぼない

**不採用の理由**: P1-5 は「非同期フォールバックパスの廃止 + 未対応言語の処理設計」という固有の設計判断を含む。P1-6 は「機械的な削除 + テスト書き換え」が主体。性質の異なるタスクを分離することで、レビュー時の焦点が明確になる。ただし、M2 の推奨マージ順序（P1-5 → P1-6）を遵守し、P1-5 完了後に即座に P1-6 を実施することで、中間状態の滞留期間を最小化すべき。

### 6.6 Phase 1 全体のゼロベース設計考察

P1-6 を Phase 1 全体（P1-1 ~ P1-6）の一部として統合的に評価する。Phase 1 の最終タスクとして、全体の完了像を定義する。（P1-1 セクション 6.6、P1-4 セクション 6.7、P1-5 セクション 6.5 と相互参照）

#### Phase 1 完了後の `MultilingualPhonemizer` の姿

P1-6 完了後（= Phase 1 完了後）の `MultilingualPhonemizer` は以下の状態になる:

```csharp
public sealed class MultilingualPhonemizer : IDisposable
{
    // P1-3: HandlerEntry レジストリ（所有権管理込み）
    private readonly Dictionary<string, HandlerEntry> _handlers;

    // P1-4: Options ベースコンストラクタのみ（P1-6 で旧コンストラクタ削除済み）
    public MultilingualPhonemizer(MultilingualPhonemizerOptions options) { ... }

    // P1-4: ハンドラ dispatch（P1-5 で ProcessFallbackAsync 廃止済み）
    public async Task<(string[], int[], int[], int[])> PhonemizeWithProsodyAsync(...)
    {
        if (_handlers.TryGetValue(lang, out var entry))
            return entry.Handler.Process(text);
        else
            PiperLogger.LogWarning(...);  // P1-5
    }

    // P1-3: IsOwned ベースの選択的 Dispose
    public void Dispose()
    {
        foreach (var entry in _handlers.Values)
            if (entry.IsOwned) entry.Handler?.Dispose();
    }
}
```

この姿は P1-4 セクション 6.7 の「P1-1 ~ P1-6 を統合した場合のアーキテクチャ」と合致する。P1-6 はこの最終形に到達するための最後のステップ。

#### `MultilingualPhonemizerOptions` の最終形

P1-6 完了後の Options は3プロパティに簡素化される（セクション 6.3 参照）:

```csharp
public class MultilingualPhonemizerOptions
{
    public IReadOnlyList<string> Languages { get; set; }
    public string DefaultLatinLanguage { get; set; } = "en";
    public Dictionary<string, ILanguageG2PHandler> Handlers { get; set; }
}
```

v1.4.0 の11プロパティから v2.0 の3プロパティへの削減は、Phase 1 の主要成果の一つ。この最終形は P1-4 セクション 6.6 の「Options パターンは依存が3つ以上、かつオプショナルなものが混在する場合に適している」という原則にちょうど合致する。`Languages` は必須、`DefaultLatinLanguage` はオプショナル（デフォルト "en"）、`Handlers` はオプショナル（未指定言語は `CreateDefaultHandler` で自動生成）。

**P1-3 との整合性に関する注意**: P1-3 セクション 2 Step 2 では `Handlers` の型が `Dictionary<string, ILanguageG2PHandler>` だが、P1-3 の `HandlerEntry` 導入後、Options 経由の注入では `ILanguageG2PHandler` を受け取り、`MultilingualPhonemizer` 内部で `new HandlerEntry(handler, isOwned: false)` にラップする設計。つまり **Options の公開型は `ILanguageG2PHandler` のまま**で、`HandlerEntry` は内部実装詳細。この区別が P1-3 と P1-6 の間で一貫していることを確認すること。

#### テスト書き換えの横断戦略

P1-6 の46箇所のテスト書き換えは Phase 1 最大のテスト修正量。Phase 1 全体のテスト書き換えを横断的に整理する:

| チケット | テスト書き換え量 | 主な内容 | テストヘルパー |
|---------|---------------|---------|-------------|
| P1-1 | 17箇所 | `PhonemeEncoder` コンストラクタ引数追加 | `new PuaTokenMapper()` |
| P1-2 | 1ファイル改修 | `PuaJsonCrossValidationTests` スナップショット廃止 | `TestPuaJsonHelper` |
| P1-3 | 8ファイル | Options + Handlers パターン移行 | `StubG2PHandler` (P1-4 由来) |
| P1-4 | 35箇所 | Options パターン移行 + ハンドラ単体テスト新規 | `StubG2PHandler` 導入 |
| P1-5 | 11箇所 | `StubPhonemizerBackend` → `StubG2PHandler` 移行 | `StubG2PHandler` |
| **P1-6** | **46箇所** | 旧コンストラクタ → Options 版 + pragma 除去 | `StubG2PHandler`, `CreatePhonemizer` |

**テストヘルパー共通化の優先度**: セクション 6.4 で指摘した `CreatePhonemizer(params string[] languages)` ファクトリは、P1-4/P1-6 の合計 81 箇所の書き換えを 2 箇所（ファクトリ定義 + 各テストでの呼び出し）に集約できる。一から設計するなら、P1-4 の時点でこのファクトリを導入し、P1-6 での書き換えコストを大幅に削減すべきだった。

推奨される `CreatePhonemizer` の配置先は P1-3 セクション 6.6 で示した `Tests/Editor/Helpers/` ディレクトリ。`StubG2PHandler` と共に配置することで、テストヘルパーの発見可能性を高める:

```
Tests/Editor/Helpers/
  StubG2PHandler.cs          // P1-4 で導入、P1-5/P1-6 で共用
  PhonemizerTestHelper.cs    // CreatePhonemizer ファクトリ
```

#### P1-5 + P1-6 統合の最終判断

P1-5 セクション 6.5 で統合の再検討を行い、「一から計画するなら統合が効率的」と結論した。P1-6 側からも同じ結論に至る:

- P1-5 の `EnPhonemizer`/`KoPhonemizer` 削除と P1-6 の個別エンジンプロパティ削除は、両方とも `MultilingualPhonemizerOptions.cs` の変更
- P1-5 の `_enPhonemizer`/`_koPhonemizer` フィールド削除と P1-6 の Obsolete コンストラクタ削除は、両方とも `MultilingualPhonemizer.cs` の変更
- 統合すれば `MultilingualPhonemizer.cs` への変更が1回で済み、リベースのコンフリクトリスクが消滅

**Phase 1 全体の最適な分割**: 一から設計するなら、以下の3タスク分割が最適:

| タスク | 内容 | 見積もり |
|-------|------|---------|
| P1-A: PuaTokenMapper 完全インスタンス化 + pua.json | P1-1 + P1-2 統合 | 2.5 人日 |
| P1-B: ILanguageG2PHandler + HandlerEntry Registry | P1-4 + P1-3 統合 | 7.5 人日 |
| P1-C: レガシー API 全廃 | P1-5 + P1-6 統合 | 1.5 人日 |

しかし P1-B の 7.5 人日は PR サイズが大きすぎるため、P1-4 と P1-3 の分離は維持が妥当。最終的には:

| タスク | 内容 | 見積もり |
|-------|------|---------|
| P1-A: PuaTokenMapper インスタンス化 | P1-1（現行通り） | 1.5 人日 |
| P1-B: pua.json ランタイム読み込み | P1-2（現行通り） | 1 人日 |
| P1-C: ILanguageG2PHandler 全面移行 | P1-4（現行通り） | 5.5 人日 |
| P1-D: Dictionary Registry 化 | P1-3（現行通り） | 2 人日 |
| **P1-E: レガシー API 全廃** | **P1-5 + P1-6 統合** | **1.5 人日** |

5タスク分割が最適解。ただし現計画の6タスク分割も、安全性を重視する判断として不合理ではない。

#### piper-plus 互換性の Phase 1 全体評価

P1-6 は piper-plus との互換性に直接関係しない（Obsolete コンストラクタは C# 固有の API）。Phase 1 全体の piper-plus 互換性は P1-2 セクション 6.5 で整理済みだが、P1-6 完了後の最終状態として改めて確認する:

Phase 1 完了後、uPiper は以下の点で piper-plus と設計思想が整合する:
- **データソース**: pua.json を single source of truth として共有（P1-2）
- **言語プラグイン**: `ILanguageG2PHandler` は piper-plus の `trait LanguageProcessor` に概念対応（P1-4）
- **同期 G2P**: piper-plus と同様に全言語がインプロセス同期処理（P1-5）

整合しない点:
- **所有権管理**: piper-plus は Rust の所有権システムで型安全に管理。uPiper は `HandlerEntry.IsOwned` フラグで実行時管理（P1-3、P1-4 セクション 6.4 参照）
- **DI パターン**: piper-plus はモジュールスコープのシングルトン。uPiper は Composition Root + Options パターン（C# + Unity 固有の要件）

#### Phase 1 完了後の理想 vs 現実

**理想**: P1-5 + P1-6 が統合され、レガシー API（`IPhonemizerBackend`, Obsolete コンストラクタ, 個別エンジンプロパティ, `CreateDummyAudioClip`）が一括削除。テストは全て Options + Handlers パターンに統一済み。`#pragma warning disable CS0618` はゼロ。`MultilingualPhonemizerOptions` は3プロパティのクリーンな構造。

**現実の妥協**: P1-5 → P1-6 の2段階削除により、P1-5 完了後に Obsolete コンストラクタと個別エンジンプロパティがまだ残存する中間状態が数日間存在。この期間にテストで `#pragma warning disable` が残り続ける。P1-6 完了で全て解消されるが、中間状態でのデバッグ時にレガシーコードの存在が混乱を招く可能性がある。

---

## 7. 後続タスクへの連絡事項

### Phase 1 完了ゲートとしての位置づけ

P1-6 は Phase 1 の最終タスクである。P1-6 完了をもって M2 の完了条件（Phase 1 完了 = alpha リリース候補）を満たす:

- `MultilingualPhonemizer` が `Dictionary<string, HandlerEntry>` レジストリで動作（P1-3）
- `IPhonemizerBackend` インターフェースが削除されている（P1-5）
- `[Obsolete]` コンストラクタ・プロパティが全て削除されている（**P1-6 = 本タスク**）
- `PuaTokenMapper` が pua.json からランタイム読み込み可能（P1-2）
- 全テスト通過 + `dotnet format` 通過

### Phase 2 以降への影響

P1-6 で `CreateDummyAudioClip` を削除し null 返却に変更することで、`PiperTTS.SynthesizeAsync` の戻り値型の null 許容性が変化する。Phase 2 の public API 再設計（P2-4）で、以下の検討が必要:

- `Task<AudioClip?>` の null 返却パターンを正式 API として採用するか
- Result 型パターン（`Task<TTSResult>`）に移行するか
- エラー時の挙動を `_onError` コールバックから `Exception` ベースに統一するか

### P1-3（Dictionary Registry 化）との連携

P1-6 で `MultilingualPhonemizerOptions` の個別エンジンプロパティを削除した後、`MultilingualPhonemizerOptions` は `Languages` + `DefaultLatinLanguage` + `Handlers` の3プロパティに簡素化される。P1-3 では `Handlers` の型が `Dictionary<string, ILanguageG2PHandler>` から `Dictionary<string, HandlerEntry>` に変更される可能性がある。マージ順序として P1-3 が P1-6 より先の場合、P1-6 は `HandlerEntry` ベースの Options に対して書き換えを行う必要がある。

### 変更ファイルサマリー

| ファイル | 変更種別 | Step |
|---------|---------|------|
| `Runtime/Core/Phonemizers/Multilingual/MultilingualPhonemizer.cs` | コンストラクタ削除 + フィールド削除 + pragma 除去 | 4, 7, 8 |
| `Runtime/Core/Phonemizers/Multilingual/MultilingualPhonemizerOptions.cs` | プロパティ削除 (9プロパティ) + using 削除 | 6 |
| `Runtime/Core/PiperTTS.cs` | メソッド削除 + フォールバック修正 + pragma 除去 | 5, 8 |
| `Runtime/Demo/InferenceEngineDemo.cs` | コンストラクタ書き換え + pragma 除去 | 3, 8 |
| `Tests/Editor/MultilingualPhonemizerTests.cs` | コンストラクタ書き換え (6箇所) + pragma 除去 | 1, 8 |
| `Tests/Editor/Phonemizers/MultilingualAutoPromotionTests.cs` | コンストラクタ書き換え (3箇所) + pragma 除去 | 1, 8 |
| `Tests/Editor/Phonemizers/MultilingualPhonemizerDeepTests.cs` | コンストラクタ書き換え (6箇所) + pragma 除去 | 1, 8 |
| `Tests/Editor/Phonemizers/MultilingualPhonemizerPhase5Tests.cs` | コンストラクタ書き換え (17箇所) + pragma 除去 | 1, 8 |
| `Tests/Editor/Phonemizers/ChinesePhonemizerTests.cs` | コンストラクタ書き換え (1箇所) + pragma 除去 | 1, 8 |
| `Tests/Editor/Phonemizers/MultilingualPhonemizerEosTests.cs` | スタブ移行 (11箇所) + pragma 除去 | 2, 8 |
| `Tests/Runtime/MultilingualPipelineTests.cs` | コンストラクタ書き換え (1箇所) + pragma 除去 | 1, 8 |
| `Tests/Runtime/MultilingualModelPipelineTests.cs` | コンストラクタ書き換え (1箇所) + pragma 除去 | 1, 8 |

**合計**: ランタイム 3ファイル + デモ 1ファイル + テスト 8ファイル = 12ファイル
