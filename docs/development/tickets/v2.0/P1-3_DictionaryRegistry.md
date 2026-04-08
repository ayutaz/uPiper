# P1-3: MultilingualPhonemizer Dictionary Registry化

**マイルストーン**: M2 - Phase 1 完了 (alpha)
**優先度**: P0
**見積もり**: 2 人日
**依存チケット**: P1-4（ILanguageG2PHandler定義が前提）
**後続チケット**: なし（直接の後続なし。Phase 2へのゲート）
**ブランチ名**: `feature/v2.0-P1-3-dictionary-registry`
**設計ドキュメント**: [P1-3_DictionaryRegistry.md](../../v2.0-design/P1-3_DictionaryRegistry.md)

---

## 1. タスク目的とゴール

### なぜこのタスクが必要か

P1-4 完了時点で `MultilingualPhonemizer` は `Dictionary<string, ILanguageG2PHandler> _handlers` を保持し、`PhonemizeWithProsodyAsync` の switch 文は `_handlers` Dictionary lookup に置換済みである。しかし以下の問題が残存している:

1. **所有権管理が二層に分散**: 各ハンドラ内部の `_ownsEngine` フラグと、`MultilingualPhonemizer` 側の「どのハンドラを内部生成したか」の暗黙的な知識が並存している。ハンドラの Dispose 責任が曖昧であり、外部注入ハンドラを誤って Dispose するリスクがある。
2. **MultilingualPhonemizerOptions の個別エンジンプロパティが残存**: `JaPhonemizer`, `EnEngine`, `EsEngine` 等の9プロパティが `[Obsolete]` 付きで残り、`Handlers` Dictionary と二重の注入パスが存在する。新規利用者が旧APIを使用するリスクがある。
3. **フィールド数の肥大**: P1-4 でハンドラは Dictionary 化されたが、`_ownsXxx` フラグ7つは構造的に解消されていない。`HandlerEntry` struct による所有権の集約が未実施。

`HandlerEntry` struct を導入し、所有権情報を Dictionary の value 側に統合することで、`MultilingualPhonemizer` のフィールド数を 17 -> 2 に削減し、所有権管理を単一の仕組みに一元化する。これにより Phase 2 以降の拡張（新言語追加、データモデル変更等）が安全に行える基盤を確立する。

### 完了の定義

- `HandlerEntry` internal struct が `Runtime/Core/Phonemizers/Multilingual/HandlerEntry.cs` に定義されている
- `MultilingualPhonemizer` のエンジンフィールド9個 + 所有権フラグ7個が `Dictionary<string, HandlerEntry> _handlers` に統合されている
- `MultilingualPhonemizerOptions` の個別エンジンプロパティ9個が `Handlers` Dictionary 1個に置換されている
- `InitializeAsync` がループ + `CreateDefaultHandler` ファクトリで動作
- `Dispose` がループ1つで `IsOwned == true` のハンドラのみ破棄
- `PiperTTS.Inference.cs` の Options 構築が `Handlers` パターンに更新されている
- テスト8ファイルが `Options + Handlers` パターンに移行済み
- 全テスト通過（EditMode + PlayMode）
- `dotnet format --verify-no-changes` パス

---

## 2. 実装する内容の詳細

### P1-4 連絡事項の前提

P1-4 のセクション7連絡事項を取り込む:

- P1-4 完了時点で `MultilingualPhonemizer` は既に `Dictionary<string, ILanguageG2PHandler> _handlers` を保持している
- P1-3 の主な残作業は `HandlerEntry` struct 導入と `MultilingualPhonemizerOptions` の個別エンジンプロパティ削除
- **重要**: P1-4 では各ハンドラ内部の `_ownsEngine` フラグで所有権を管理している。P1-3 で `HandlerEntry.IsOwned` に移行する際、ハンドラの `Dispose` ロジック内の `_ownsEngine` チェックを `HandlerEntry` 側に移す必要がある
- P1-4 の `CreateDefaultHandler` ファクトリメソッドは P1-3 でそのまま再利用可能

### Step 1: HandlerEntry struct 追加（0.25 人日）

**新規ファイル**: `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/HandlerEntry.cs`

```csharp
namespace uPiper.Core.Phonemizers.Multilingual
{
    /// <summary>
    /// Registry entry that pairs a handler with ownership information.
    /// </summary>
    internal readonly struct HandlerEntry
    {
        public ILanguageG2PHandler Handler { get; }
        public bool IsOwned { get; }

        public HandlerEntry(ILanguageG2PHandler handler, bool isOwned)
        {
            Handler = handler;
            IsOwned = isOwned;
        }
    }
}
```

設計判断:
- `internal readonly struct` とする。公開APIに露出する必要なし。struct は GC 圧を回避。
- `IsOwned = true` は「`MultilingualPhonemizer` が `InitializeAsync` 内で生成したハンドラ。Dispose 時に破棄する」を意味する。
- `IsOwned = false` は「外部から `Options.Handlers` 経由で注入されたハンドラ。呼び出し側がライフサイクル管理する」を意味する。

### Step 2: MultilingualPhonemizerOptions の変更（0.25 人日）

**対象ファイル**: `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/MultilingualPhonemizerOptions.cs`

- 個別エンジンプロパティ9個（`JaPhonemizer`, `EnEngine`, `EnPhonemizer`(Obsolete), `EsEngine`, `FrEngine`, `PtEngine`, `ZhEngine`, `KoPhonemizer`(Obsolete), `KoG2PEngine`）を削除
- `Handlers` プロパティ（`Dictionary<string, ILanguageG2PHandler>`）を追加
- `Validate()` メソッドを追加（`Languages` の null/空チェック）

**After**:
```csharp
public class MultilingualPhonemizerOptions
{
    public IReadOnlyList<string> Languages { get; set; }
    public string DefaultLatinLanguage { get; set; } = "en";
    public Dictionary<string, ILanguageG2PHandler> Handlers { get; set; }

    public void Validate()
    {
        if (Languages == null || Languages.Count == 0)
            throw new ArgumentException(
                "At least one language must be specified.", nameof(Languages));
    }
}
```

### Step 3: MultilingualPhonemizer の内部フィールド置換（0.5 人日）

**対象ファイル**: `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/MultilingualPhonemizer.cs`

**削除するフィールド**:
- エンジンフィールド9個: `_jaPhonemizer`, `_enEngine`, `_enPhonemizer`, `_esEngine`, `_frEngine`, `_ptEngine`, `_zhEngine`, `_koPhonemizer`, `_koG2PEngine`
- 所有権フラグ7個: `_ownsJa`, `_ownsEn`, `_ownsEs`, `_ownsFr`, `_ownsPt`, `_ownsZh`, `_ownsKo`

**追加するフィールド**:
```csharp
private readonly Dictionary<string, HandlerEntry> _handlers;
private bool _disposed;
```

**コンストラクタ変更**:
```csharp
public MultilingualPhonemizer(MultilingualPhonemizerOptions options)
{
    // ...
    _handlers = new Dictionary<string, HandlerEntry>();

    if (options.Handlers != null)
    {
        foreach (var (lang, handler) in options.Handlers)
        {
            _handlers[lang] = new HandlerEntry(handler, isOwned: false);
        }
    }
}
```

### Step 4: 各ハンドラの所有権ロジック移行（0.25 人日）

P1-4 で各ハンドラ内部に `_ownsEngine` フラグが実装されている。P1-3 では:

1. 各ハンドラクラス（7ファイル）から `_ownsEngine` フラグと Dispose 内の条件分岐を削除
2. 全ハンドラの `Dispose()` を「常にエンジンを Dispose する」に変更
3. 所有権の判定は `HandlerEntry.IsOwned` に一元化し、`MultilingualPhonemizer.Dispose()` が `IsOwned == true` のエントリのみ `Handler.Dispose()` を呼ぶ

これにより、ハンドラは「自分が所有権を持つかどうか」を意識せず、純粋な処理ロジックに専念できる。

### Step 5: InitializeAsync の簡素化（0.25 人日）

```csharp
public async Task InitializeAsync(CancellationToken cancellationToken = default)
{
    if (_isInitialized) return;

    foreach (var lang in _languages)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_handlers.ContainsKey(lang))
            continue;  // 既に外部注入済み

        var handler = CreateDefaultHandler(lang);
        if (handler != null)
        {
            await handler.InitializeAsync(cancellationToken);
            _handlers[lang] = new HandlerEntry(handler, isOwned: true);
        }
    }

    _isInitialized = true;
}
```

P1-4 の `CreateDefaultHandler` ファクトリメソッドをそのまま再利用。7言語の個別分岐がループ1つに統一される。

### Step 6: PhonemizeWithProsodyAsync の switch 文確認（0.1 人日）

P1-4 で switch 文は既に `_handlers.TryGetValue` に置換済み。P1-3 では `HandlerEntry` の間接参照に変更:

```csharp
if (_handlers.TryGetValue(lang, out var entry))
{
    (segPhonemes, segA1, segA2, segA3) = entry.Handler.Process(segText);
}
else
{
    PiperLogger.LogWarning(
        $"[MultilingualPhonemizer] No handler for '{lang}', skipping segment.");
    continue;
}
```

### Step 7: Dispose の簡素化（0.1 人日）

```csharp
public void Dispose()
{
    if (_disposed) return;
    _disposed = true;

    foreach (var entry in _handlers.Values)
    {
        if (entry.IsOwned)
        {
            entry.Handler?.Dispose();
        }
    }

    _handlers.Clear();
}
```

7言語個別の Dispose 分岐がループ1つに統一。`IsOwned == false`（外部注入）のハンドラは Dispose しない。

### Step 8: PiperTTS.Inference.cs の Options 構築更新（0.1 人日）

**対象ファイル**: `Assets/uPiper/Runtime/Core/PiperTTS.Inference.cs`

```csharp
var handlers = new Dictionary<string, ILanguageG2PHandler>();
if (_phonemizer is DotNetG2PPhonemizer jaPhon)
{
    handlers["ja"] = new JapaneseG2PHandler(jaPhon);
}

var phonemizerOptions = new MultilingualPhonemizerOptions
{
    Languages = supportedLanguages,
    DefaultLatinLanguage = _config.DefaultLanguage ?? "en",
    Handlers = handlers,
};
_multilingualPhonemizer = new MultilingualPhonemizer(phonemizerOptions);
```

### Step 9: テストの移行（0.15 人日）

**影響テストファイル8個**:

| ファイル | 移行内容 |
|---------|---------|
| `MultilingualPhonemizerTests.cs` | Obsolete コンストラクタ -> Options + Handlers |
| `MultilingualPhonemizerDeepTests.cs` | 外部エンジン注入 -> `Handlers` Dictionary |
| `MultilingualPhonemizerPhase5Tests.cs` | 個別エンジン注入 -> `Handlers` Dictionary |
| `MultilingualPhonemizerEosTests.cs` | Obsolete コンストラクタ使用箇所の更新 |
| `MultilingualAutoPromotionTests.cs` | `MultilingualPhonemizerOptions` のプロパティ参照変更 |
| `ChinesePhonemizerTests.cs` | `MultilingualPhonemizer` 経由テストの更新 |
| `MultilingualPipelineTests.cs` | ランタイムテスト、Options 構築パターン変更 |
| `MultilingualModelPipelineTests.cs` | ランタイムテスト、Options 構築パターン変更 |

Dispose 所有権テスト（`Dispose_DisposesAllBackends` 等）は `HandlerEntry.IsOwned` ベースに更新。`ILanguageG2PHandler` のモック/スパイで所有権の正確性を検証。

### Step 10: InferenceEngineDemo.cs の更新（0.05 人日）

`Assets/uPiper/Runtime/Demo/InferenceEngineDemo.cs` の `MultilingualPhonemizerOptions` 構築箇所を `Handlers` パターンに更新。

---

## 3. エージェントチームの役割と人数

### 推奨構成: エージェント1名

P1-3 は P1-4 の成果物に対する「薄い追加レイヤー」であり、変更の大半は機械的な置換である。M2 のマイルストーン計画では P1-3 -> P1-5 -> P1-6 を同一エージェントが連続実施する想定（`MultilingualPhonemizer.cs` の連続変更によるコンフリクト回避のため）。

| エージェント | 担当 | 工数 |
|------------|------|------|
| **Agent 1** | Step 1-10 全て | 2 人日 |

### 作業順序

```
Time ────────────────────────────────────────────>

Agent 1: [Step1-2: struct+Options 0.5d] → [Step3-4: フィールド+所有権 0.75d] → [Step5-8: 簡素化 0.55d] → [Step9-10: テスト 0.2d]
```

Step 1-2 はデータ構造の定義、Step 3-4 は `MultilingualPhonemizer` 本体の書き換え、Step 5-8 は `InitializeAsync`/`Dispose`/呼び出し元の更新、Step 9-10 はテスト移行と周辺更新。全て順次実施。

### M2 内での位置づけ

M2 では P1-3 を最優先でマージし、その後 P1-5（IPhonemizerBackend 廃止）-> P1-6（Obsolete 削除）の順で同一エージェントが実施する。P1-2（pua.json ランタイム読み込み）は別エージェントが並行で実施可能。

---

## 4. 提供範囲とテスト項目

### 4.1 提供範囲

#### スコープ内

- `HandlerEntry` internal readonly struct の新設
- `MultilingualPhonemizerOptions` の個別エンジンプロパティ9個 -> `Handlers` Dictionary 1個への置換
- `MultilingualPhonemizer` のエンジンフィールド9個 + 所有権フラグ7個 -> `Dictionary<string, HandlerEntry>` への統合
- 各ハンドラ（7クラス）からの `_ownsEngine` フラグ削除・Dispose ロジック簡素化
- `InitializeAsync` のループ + ファクトリ化
- `Dispose` のループ化
- `PiperTTS.Inference.cs` の Options 構築更新
- `InferenceEngineDemo.cs` の Options 構築更新
- テスト8ファイルの `Options + Handlers` パターン移行

#### スコープ外（後続タスクで対応）

- `IPhonemizerBackend` インターフェース削除 -> **P1-5**
- `ProcessFallbackAsync` / `GetBackendForLanguage` 削除 -> **P1-5**
- `_enPhonemizer` / `_koPhonemizer` フィールド削除 -> **P1-5**
- `[Obsolete]` 14引数コンストラクタの削除 -> **P1-6**
- `#pragma warning disable CS0618` の完全除去 -> **P1-6**（P1-5 完了後に実施が安全）

### 4.2 Unitテスト

#### 4.2.1 HandlerEntry テスト

| テストクラス | テストメソッド | 検証内容 |
|------------|--------------|---------|
| `HandlerEntryTests` | `Constructor_SetsHandlerAndIsOwned` | コンストラクタ引数が正しくプロパティに設定される |
| 同上 | `IsOwned_True_ForInternallyCreatedHandler` | `isOwned: true` で構築した場合の検証 |
| 同上 | `IsOwned_False_ForExternallyInjectedHandler` | `isOwned: false` で構築した場合の検証 |

#### 4.2.2 MultilingualPhonemizer 所有権テスト

| テストクラス | テストメソッド | 検証内容 |
|------------|--------------|---------|
| `MultilingualPhonemizerTests` | `Dispose_OnlyDisposesOwnedHandlers` | `IsOwned == true` のハンドラのみ Dispose される |
| 同上 | `Dispose_ExternalHandler_NotDisposed` | `Options.Handlers` 経由で注入したハンドラが Dispose されない |
| 同上 | `Dispose_CalledTwice_DoesNotThrow` | 二重 Dispose で例外なし |
| 同上 | `InitializeAsync_CreatesDefaultForUnregistered` | `Handlers` 未指定の言語にデフォルトハンドラが生成される（`IsOwned == true`） |
| 同上 | `InitializeAsync_SkipsAlreadyRegistered` | `Handlers` 指定済みの言語はスキップされる |

#### 4.2.3 Options テスト

| テストクラス | テストメソッド | 検証内容 |
|------------|--------------|---------|
| `MultilingualPhonemizerOptionsTests` | `Validate_NullLanguages_Throws` | `Languages = null` で `ArgumentException` |
| 同上 | `Validate_EmptyLanguages_Throws` | `Languages` 空で `ArgumentException` |
| 同上 | `Handlers_Null_DefaultsCreatedInInitialize` | `Handlers = null` でも `InitializeAsync` でデフォルト生成 |

### 4.3 E2Eテスト

#### 4.3.1 既存テストの振る舞い不変確認

以下の既存テストファイルが **移行後に全てパス** することを確認:

| テストファイル | 確認事項 |
|--------------|---------|
| `MultilingualPhonemizerTests.cs` | Options + Handlers 経由に書き換え後、同一結果 |
| `MultilingualPhonemizerDeepTests.cs` | 3言語以上混在テストの振る舞い不変 |
| `MultilingualPhonemizerEosTests.cs` | EOS処理の振る舞い不変 |
| `MultilingualPhonemizerPhase5Tests.cs` | 各言語テストの振る舞い不変 |
| `MultilingualAutoPromotionTests.cs` | auto-promotion テストが影響を受けないこと |
| `ChinesePhonemizerTests.cs` | 中国語固有テストが影響を受けないこと |
| `MultilingualPipelineTests.cs` | ランタイム統合テストの振る舞い不変 |
| `MultilingualModelPipelineTests.cs` | モデルパイプラインテストの振る舞い不変 |

#### 4.3.2 追加統合テスト

| テストクラス | テストメソッド | 検証内容 |
|------------|--------------|---------|
| `MultilingualPhonemizerRegistryTests` | `Registry_ExternalAndDefault_CoexistCorrectly` | 一部外部注入 + 一部デフォルト生成が正しく共存 |
| 同上 | `Registry_Dispose_OwnershipRespected` | 混在環境で所有権が正しく尊重される |
| 同上 | `Registry_AllLanguages_Phonemize_SameAsV1` | 全6言語のテキストが v1.4.0 と同一の音素出力を返す |

---

## 5. 懸念事項とレビュー項目

### 5.1 技術的懸念

| 懸念 | リスク | 緩和策 |
|------|--------|--------|
| **ハンドラ内 `_ownsEngine` 削除の影響** | P1-4 でハンドラ内部に `_ownsEngine` を実装済み。これを `HandlerEntry.IsOwned` に移行する際、各ハンドラの Dispose が「常に Dispose する」に変わるため、外部注入パスで二重 Dispose が発生するリスク | `MultilingualPhonemizer.Dispose` が `IsOwned == false` のエントリでは `Handler.Dispose()` を呼ばないことをテストで検証。外部注入ハンドラは呼び出し側が Dispose 責任を持つ規約をドキュメント化 |
| **MultilingualPhonemizerOptions のプロパティ削除** | 旧プロパティ（`JaPhonemizer`, `EnEngine` 等）を削除すると、P1-5/P1-6 で予定していたクリーンアップと範囲が重複する可能性 | P1-5 は `EnPhonemizer`/`KoPhonemizer`（`IPhonemizerBackend` 型）の削除がメイン。P1-3 では `ILanguageG2PHandler` 型以外の個別エンジンプロパティを削除し、P1-5 はレガシー `IPhonemizerBackend` プロパティの削除に専念する |
| **テスト8ファイルの一括書き換え** | 機械的な書き換えだが、テスト数が多い（合計50+テスト）ため見落としリスク | 書き換え後に `dotnet format --verify-no-changes` + 全テスト実行で網羅的に検証 |
| **P1-3 と P1-5/P1-6 のマージ順序** | P1-3 を先にマージしないと P1-5/P1-6 でコンフリクト多発 | M2 の推奨マージ順序（P1-3 -> P1-5 -> P1-6）を厳守。同一エージェントが連続実施 |

### 5.2 レビューチェックリスト

- [ ] `HandlerEntry` が `internal readonly struct` であること
- [ ] `MultilingualPhonemizerOptions` から個別エンジンプロパティ9個が削除されていること
- [ ] `MultilingualPhonemizer` のエンジンフィールド9個 + 所有権フラグ7個が削除されていること
- [ ] `_handlers` が `Dictionary<string, HandlerEntry>` 型であること
- [ ] コンストラクタで外部注入ハンドラが `IsOwned = false` で登録されること
- [ ] `InitializeAsync` でデフォルト生成ハンドラが `IsOwned = true` で登録されること
- [ ] `Dispose` が `IsOwned == true` のハンドラのみ Dispose すること
- [ ] 各ハンドラクラスから `_ownsEngine` フラグが削除されていること
- [ ] 各ハンドラの `Dispose` が無条件でエンジンを Dispose すること（所有権判定は `HandlerEntry` に委譲済み）
- [ ] `PiperTTS.Inference.cs` が `Handlers` Dictionary 経由で日本語ハンドラを注入していること
- [ ] `InferenceEngineDemo.cs` が `Handlers` パターンに更新されていること
- [ ] `ProcessFallbackAsync` / `GetBackendForLanguage` / `_enPhonemizer` / `_koPhonemizer` が **残存している** こと（P1-5 のスコープ）
- [ ] `[Obsolete]` 14引数コンストラクタが **残存している** こと（P1-6 のスコープ）
- [ ] `dotnet format --verify-no-changes` パス
- [ ] 全テスト（EditMode + PlayMode）パス

---

## 6. 一から作り直すとしたら

### 6.1 現設計が抱える構造的妥協

本チケットの設計は「P1-4 の成果物に対する追加レイヤー」として `HandlerEntry` struct を導入する漸進的アプローチをとっている。この前提が持つ構造的妥協を整理する。

1. **所有権の責務移動が中途半端になるリスク**: P1-4 では各ハンドラが `_ownsEngine` で所有権を管理し、P1-3 で `HandlerEntry.IsOwned` に移行する。しかし、この移行の過渡期に「ハンドラは常に Dispose する」「`HandlerEntry` 側で Dispose 呼び出しを制御する」という二つの規約が正しく適用されないと、リソースリークまたは二重 Dispose が発生する。P1-4 と P1-3 を同一 PR で実施できれば過渡期が消えるが、PR サイズ（7.0 人日相当）がレビュー限界を超える。

2. **`MultilingualPhonemizerOptions` の破壊的変更**: 個別エンジンプロパティ9個の削除は public API の破壊的変更である。v1.4.0 のユーザーが `JaPhonemizer = ...` で注入している場合、コンパイルエラーになる。ただし v2.0 は semver major bump のため破壊的変更は許容される。

3. **`HandlerEntry` struct の外部不可視性**: `internal` のため、パッケージ利用者はレジストリの所有権管理を直接テストできない。テストアセンブリからは `[assembly: InternalsVisibleTo("uPiper.Tests.Editor")]` で可視化が必要。

### 6.2 代替パターン: Service Locator

`Dictionary<string, HandlerEntry>` を `MultilingualPhonemizer` の内部フィールドとして持つ代わりに、独立した `LanguageHandlerServiceLocator` クラスに切り出すパターン:

```csharp
public sealed class LanguageHandlerServiceLocator : IDisposable
{
    private readonly Dictionary<string, HandlerEntry> _entries = new();

    public void Register(string lang, ILanguageG2PHandler handler, bool ownsLifecycle)
        => _entries[lang] = new HandlerEntry(handler, ownsLifecycle);

    public ILanguageG2PHandler Resolve(string lang)
        => _entries.TryGetValue(lang, out var e) ? e.Handler : null;

    public void Dispose()
    {
        foreach (var e in _entries.Values)
            if (e.IsOwned) e.Handler?.Dispose();
        _entries.Clear();
    }
}
```

| 観点 | Service Locator | 現設計（Dictionary フィールド） |
|------|----------------|-------------------------------|
| テスタビリティ | Locator をモック注入可能 | Dictionary は内部フィールドで直接テスト不可 |
| 複雑度 | クラス1つ追加 | struct 1つ追加 |
| `MultilingualPhonemizer` への影響 | コンストラクタに `ILanguageHandlerLocator` を注入 | 内部フィールドの型変更のみ |
| Unity 制約 | インターフェース追加 = asmdef 依存増 | 追加なし |

**不採用理由**: `MultilingualPhonemizer` 以外にレジストリを参照するクラスが存在しないため、Locator を独立クラスにする実益がない。「ルックアップ対象が1クラスだけの Service Locator」はアンチパターン。Dictionary フィールド + `HandlerEntry` struct の方がシンプル。

### 6.3 代替パターン: Composition Root 一元管理

`PiperTTS`（Composition Root）が全ハンドラのライフサイクルを管理し、`MultilingualPhonemizer` にはハンドラの「参照のみ」を渡すパターン:

```csharp
// PiperTTS.cs
private readonly List<IDisposable> _ownedResources = new();

private async Task InitializeMultilingualAsync()
{
    var handlers = new Dictionary<string, ILanguageG2PHandler>();
    foreach (var lang in supportedLanguages)
    {
        var handler = CreateHandler(lang);
        await handler.InitializeAsync();
        handlers[lang] = handler;
        _ownedResources.Add(handler);  // PiperTTS が全ハンドラの Dispose を管理
    }

    _multilingualPhonemizer = new MultilingualPhonemizer(
        new MultilingualPhonemizerOptions { Languages = supportedLanguages, Handlers = handlers });
    // MultilingualPhonemizer は Dispose 責任を持たない
}
```

| 観点 | Composition Root | 現設計（`HandlerEntry.IsOwned`） |
|------|-----------------|-------------------------------|
| 所有権の明確さ | 全リソースが `PiperTTS` に集約 | `IsOwned` フラグで分散管理 |
| `MultilingualPhonemizer` の単純さ | Dispose 不要（参照のみ） | Dispose ロジックあり |
| テスト時 | `PiperTTS` 全体の初期化が必要 | `MultilingualPhonemizer` 単体テスト可能 |
| 外部注入の柔軟性 | `PiperTTS` が全てを知る必要あり | ハンドラ単位で外部/内部を混在可能 |

**不採用理由**: `PiperTTS` に全ハンドラのライフサイクルを集約すると、テスト時に `PiperTTS` 全体の初期化が必要になり、テスト単位が大きくなる。現設計では `MultilingualPhonemizer` に `Options.Handlers` でスタブを注入するだけで単体テストが可能であり、テスタビリティの観点で優位。また、`PiperTTS` はモデルロード・推論エンジン管理等の責務を既に持っており、ハンドラのライフサイクル管理まで負わせると Single Responsibility Principle に反する。

### 6.4 代替パターン: IDisposable を外部注入ハンドラに委ねる（現行のハイブリッド案）

P1-4 の設計をそのまま維持し、`HandlerEntry` を導入しない案:

- 各ハンドラ内部の `_ownsEngine` フラグをそのまま残す
- `MultilingualPhonemizer.Dispose` は全ハンドラの `Dispose()` を無条件に呼ぶ
- 外部注入ハンドラは `_ownsEngine = false` で構築されており、`Dispose()` 呼び出し時にエンジンを解放しない

| 観点 | ハイブリッド案 | 現設計（`HandlerEntry`） |
|------|--------------|------------------------|
| フィールド数 | `_handlers` Dictionary のみ（フラグはハンドラ内部） | `_handlers` Dictionary のみ（フラグは `HandlerEntry` 内部） |
| 所有権の一元性 | ハンドラ内部に分散 | `HandlerEntry` に集約 |
| ハンドラのテスト | 所有権フラグの設定がテストに必要 | ハンドラ自体は所有権非依存 |
| 変更コスト | P1-4 のまま。追加変更なし | 7ハンドラの `_ownsEngine` 削除 + `HandlerEntry` 新設 |

**不採用理由**: 所有権管理がハンドラ内部に分散したままだと、「外部注入ハンドラは `_ownsEngine = false` で構築すること」という暗黙の規約が残り、テストスタブの作成時に見落としやすい。`HandlerEntry` に所有権を集約することで、登録時点で `isOwned` が明示的に決定され、ハンドラ実装者は所有権を意識する必要がなくなる。設計ドキュメント（P1-3_DictionaryRegistry.md）の設計意図に合致する。

### 6.5 ゼロベースで最もクリーンな設計

もし v1.x の互換性を一切考慮せず、P1-1 ~ P1-6 を一括で実施するなら:

```csharp
// ハンドラは処理ロジックのみ。IDisposable なし。
public interface ILanguageG2PHandler
{
    string LanguageCode { get; }
    (string[] Phonemes, int[] A1, int[] A2, int[] A3) Process(string text);
}

// 初期化可能なハンドラは拡張インターフェース
public interface IAsyncInitializable
{
    Task InitializeAsync(CancellationToken ct = default);
}

// ライフサイクル管理は専用レジストリ
public sealed class LanguageHandlerRegistry : IDisposable
{
    private readonly record struct Entry(ILanguageG2PHandler Handler, IDisposable Resource);
    private readonly Dictionary<string, Entry> _entries = new();

    public void Register(ILanguageG2PHandler handler, IDisposable resource = null) { ... }
    public ILanguageG2PHandler Get(string lang) => _entries[lang].Handler;
    public void Dispose() { foreach (var e in _entries.Values) e.Resource?.Dispose(); }
}
```

**利点**: ハンドラが完全にステートレス（テスト容易）。所有権管理が `LanguageHandlerRegistry` に完全に分離。`IDisposable` がハンドラのインターフェースを汚染しない。

**不採用理由**: 現実的には日本語/英語/中国語のハンドラは内部にエンジンインスタンス（`DotNetG2PPhonemizer`, `EnglishG2PEngine`, `ChineseG2PEngine`）を保持する必要があり、完全なステートレス化は困難。エンジンインスタンスとハンドラを分離して `Resource` として渡すのは、ハンドラの `Process()` メソッド内でエンジンを参照する必要があるため、逆に結合が増える。現設計の `HandlerEntry`（ハンドラ + 所有権フラグ）は、この現実的制約に対する pragmatic な解である。

### 6.6 Phase 1 全体のゼロベース設計考察

P1-3 を Phase 1 全体（P1-1 ~ P1-6）の一部として統合的に評価する。（P1-1 セクション 6.6、P1-4 セクション 6.7 と相互参照）

#### P1-4 との分割粒度の妥当性

P1-3 と P1-4 は「ハンドラ分離」と「レジストリ化」という、本来は一つの設計変更の前半・後半である。P1-4 がハンドラクラスを分離し `Dictionary<string, ILanguageG2PHandler>` を導入した後、P1-3 が `HandlerEntry` struct で所有権管理を改善する。この分割の結果、P1-4 完了時点で「各ハンドラが `_ownsEngine` を持つ」中間状態が生まれる。

**統合した場合のメリット**: P1-4 + P1-3 を同一 PR で実施すれば、所有権管理の中間状態が消え、最初から `HandlerEntry` ベースの設計になる。P1-4 のハンドラ実装時に `_ownsEngine` フラグを入れる必要がなく、ハンドラは処理ロジックに専念できる。

**統合しない理由（現設計の判断）**: PR サイズが 5.5 + 2.0 = 7.5 人日相当となり、レビュー可能な限界（P1-4 セクション 6.6 参照）を超える。また、P1-4 の7ハンドラ実装は単体テストで品質を担保する必要があり、P1-3 のレジストリ変更と混在するとテスト失敗時の原因切り分けが困難になる。

**妥協の現実的影響**: P1-4 完了 → P1-3 完了の間（推定数日〜1週間）、各ハンドラの `_ownsEngine` フラグと `MultilingualPhonemizer` 側の所有権知識が二重管理になる。この期間にバグが発生した場合、所有権関連のリソースリーク/二重 Dispose の原因切り分けが必要。P1-3 を P1-4 直後に着手し、中間状態の滞留期間を最小化することが重要。

#### DI 戦略の統一性: P1-1 コンストラクタ注入 vs P1-3 Options Dictionary 注入

P1-1 は `PhonemeEncoder(config, tokenMapper)` のコンストラクタ注入、P1-3 は `MultilingualPhonemizerOptions.Handlers` Dictionary 注入という異なる DI パターンを採用する。この非対称性について:

- **P1-1 のコンストラクタ注入**: 依存が `PiperVoiceConfig` と `PuaTokenMapper` の2つのみ。パラメータ数が少なく、コンストラクタが自然。null 時は即座に `ArgumentNullException`。
- **P1-3 の Options Dictionary 注入**: 依存が7言語分のハンドラ + 所有権フラグの計14要素。コンストラクタパラメータにすると引数が爆発する（v1.x の14引数コンストラクタの教訓）。Dictionary + `HandlerEntry` が自然。

**統一するとしたら**: 両方を Options パターンに揃える案（P1-1 セクション 6.6 の `PhonemeEncoderOptions` 案）は、`PhonemeEncoder` の依存が2つしかないため冗長。逆に両方をコンストラクタ注入にする案は、ハンドラ7つを個別パラメータにすると v1.x の失敗を繰り返す。**現設計の非対称性は、依存の数と性質の違いに基づく合理的な判断**であり、統一する必然性はない。

#### 所有権管理の Phase 1 全体での一貫性

Phase 1 で所有権管理が関わるコンポーネントを横断的に整理する:

| コンポーネント | リソース | 所有権管理 | 管理者 |
|-------------|---------|-----------|-------|
| `PuaTokenMapper` (P1-1) | ConcurrentDictionary | インスタンスのライフサイクル | `PiperTTS` (Composition Root) |
| `PhonemeEncoder` (P1-1) | `PuaTokenMapper` 参照 | 参照のみ（所有しない） | `PiperTTS` が TokenMapper を別途管理 |
| ハンドラ (P1-4) | DotNetG2P エンジン等 | `_ownsEngine` フラグ → P1-3 で `HandlerEntry.IsOwned` に移行 | `MultilingualPhonemizer` |
| `MultilingualPhonemizer` (P1-3) | `Dictionary<string, HandlerEntry>` | `HandlerEntry.IsOwned` で Dispose 判断 | 自身（`IDisposable`） |

この表が示す通り、Phase 1 完了後の所有権管理は2つのパターンに収束する:
1. **Composition Root 管理**: `PuaTokenMapper` は `PiperTTS` が生成し、ライフサイクルを管理
2. **Registry 管理**: ハンドラは `HandlerEntry.IsOwned` フラグにより `MultilingualPhonemizer` が選択的に Dispose

この2パターンの共存は、コンポーネントの性質の違い（単一の共有リソース vs 複数の言語別リソース）に基づくものであり、一貫性は保たれている。

#### テスト戦略: HandlerEntry のテスタビリティ

`HandlerEntry` は `internal readonly struct` であり、テストアセンブリからアクセスするには `[assembly: InternalsVisibleTo]` が必要。P1-1 ではこの属性を削除する方針（P1-1 セクション 2 Step 1-7）だが、P1-3 で再追加が必要になる。

**Phase 1 全体のテストヘルパー共通化**: P1-4 で導入される `StubG2PHandler` は P1-3 / P1-5 / P1-6 のテストでも共用される。テストヘルパーの配置先を統一すべき:

| テストヘルパー | 導入チケット | 使用チケット | 推奨配置 |
|-------------|-----------|-----------|---------|
| `StubG2PHandler` | P1-4 | P1-3, P1-5, P1-6 | `Tests/Editor/Helpers/StubG2PHandler.cs` |
| `TestPuaJsonHelper` (pua.json パス解決) | P1-2 | P1-2 のみ | テストファイル内 (ローカル) |

`StubG2PHandler` は4チケットにまたがって使用されるため、テストヘルパーディレクトリへの配置が望ましい。P1-6 セクション 6.4 で指摘されている `CreatePhonemizer` ファクトリも同ディレクトリに配置することで、テストコードの DRY 原則が守られる。

#### Phase 1 完了後の理想 vs 現実

**理想**: P1-3 + P1-4 が統合され、ハンドラは最初から `HandlerEntry` ベースで登録。各ハンドラに `_ownsEngine` フラグが存在しない。所有権管理は `MultilingualPhonemizer` の `HandlerEntry` レジストリに完全集約。P1-5/P1-6 のレガシー掃除も同時に完了し、`MultilingualPhonemizer` は「ハンドラ Dictionary + 所有権管理」のみのクリーンな状態。

**現実の妥協**: P1-4 → P1-3 の段階的移行により、`_ownsEngine` フラグの中間状態が一時的に存在。P1-3 完了後も P1-5/P1-6 のレガシーコード（`IPhonemizerBackend`, Obsolete コンストラクタ）が残存し、`MultilingualPhonemizer` は「新旧混在」の状態が M2 完了まで続く。この妥協は PR サイズとレビュー負荷のバランスに起因する実務的制約。

---

## 7. 後続タスクへの連絡事項

### P1-5（G2P 全同期化 / IPhonemizerBackend 廃止）への連絡

- P1-3 完了後、`MultilingualPhonemizer` のエンジンフィールド・所有権フラグは全て `Dictionary<string, HandlerEntry>` に統合済み
- ただし以下のレガシーコードは **意図的に残存** している:
  - `_enPhonemizer` フィールド（`IPhonemizerBackend`）
  - `_koPhonemizer` フィールド（`IPhonemizerBackend`）
  - `ProcessFallbackAsync` メソッド
  - `GetBackendForLanguage` メソッド
  - `PhonemizeWithProsodyAsync` 内の `default` ケースでの `ProcessFallbackAsync` 呼び出し
- P1-5 ではこれらを全て削除し、`default` ケースを「警告ログ + `continue`」に置換すること
- `MultilingualPhonemizerOptions` の `EnPhonemizer` / `KoPhonemizer` プロパティ（`IPhonemizerBackend` 型、P1-3 では削除対象外）は P1-5 で削除
- **注意**: P1-3 で `_enPhonemizer` / `_koPhonemizer` は `HandlerEntry` レジストリに統合 **していない**。これらは `IPhonemizerBackend` 型であり `ILanguageG2PHandler` 型ではないため、レジストリの型制約に合致しない。P1-5 でインターフェース自体を廃止する際に同時に削除すること

### P1-6（Obsolete コンストラクタ削除）への連絡

- P1-3 完了後、`MultilingualPhonemizerOptions` の個別エンジンプロパティ（`JaPhonemizer`, `EnEngine`, `EsEngine`, `FrEngine`, `PtEngine`, `ZhEngine`, `KoG2PEngine`）は **削除済み**
- P1-6 で残る作業は `[Obsolete]` 14引数コンストラクタの削除と、それに伴うテスト内の `#pragma warning disable CS0618` の除去
- `InferenceEngineDemo.cs` は P1-3 で `Handlers` パターンに更新済み。P1-6 での追加変更は不要のはず
- P1-6 はマージ順序上 P1-5 の後に実施するのが安全（`_enPhonemizer` / `_koPhonemizer` 参照の掃除が P1-5 で完了するため）
