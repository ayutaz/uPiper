# P1-3: MultilingualPhonemizer Dictionary Registry 化 設計ドキュメント

**作成日**: 2026-04-08
**前提タスク**: P1-4 (ILanguageG2PHandler 定義)
**対象ファイル**: `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/MultilingualPhonemizer.cs`

---

## 1. 現状分析

### 1.1 個別フィールド一覧（7言語 + レガシー2つ = 9フィールド）

| フィールド | 型 | 用途 |
|-----------|------|------|
| `_jaPhonemizer` | `DotNetG2PPhonemizer` | 日本語G2P（dot-net-g2p, Prosody対応） |
| `_enEngine` | `EnglishG2PEngine` | 英語G2P（DotNetG2P.English） |
| `_enPhonemizer` | `IPhonemizerBackend` | 英語レガシー（テストスタブ注入用、`[Obsolete]`） |
| `_esEngine` | `SpanishG2PEngine` | スペイン語G2P |
| `_frEngine` | `FrenchG2PEngine` | フランス語G2P |
| `_ptEngine` | `PortugueseG2PEngine` | ポルトガル語G2P |
| `_zhEngine` | `ChineseG2PEngine` | 中国語G2P |
| `_koPhonemizer` | `IPhonemizerBackend` | 韓国語レガシー（`[Obsolete]`） |
| `_koG2PEngine` | `KoreanG2PEngine` | 韓国語G2P（DotNetG2P.Korean） |

### 1.2 所有権フラグ一覧（7フラグ）

| フラグ | 設定箇所 | 条件 |
|--------|---------|------|
| `_ownsJa` | `InitializeAsync` L163 | `ContainsLanguage("ja") && _jaPhonemizer == null` のとき内部生成 |
| `_ownsEn` | `InitializeAsync` L193 | `ContainsLanguage("en") && _enEngine == null && _enPhonemizer == null` のとき内部生成 |
| `_ownsEs` | `InitializeAsync` L205 | `ContainsLanguage("es") && _esEngine == null` のとき内部生成 |
| `_ownsFr` | `InitializeAsync` L214 | `ContainsLanguage("fr") && _frEngine == null` のとき内部生成 |
| `_ownsPt` | `InitializeAsync` L224 | `ContainsLanguage("pt") && _ptEngine == null` のとき内部生成 |
| `_ownsZh` | `InitializeAsync` L241 | `ContainsLanguage("zh") && _zhEngine == null` のとき内部生成 |
| `_ownsKo` | `InitializeAsync` L266 | `ContainsLanguage("ko") && _koG2PEngine == null` のとき内部生成 |

### 1.3 所有権管理の仕組み

**コンストラクタ注入時**: 外部から渡されたエンジンは `_ownsXxx = false`（デフォルト）のまま。Dispose時に破棄しない。呼び出し側が管理。

**内部生成時**: `InitializeAsync` 内で `new` して `_ownsXxx = true` に設定。Dispose時に内部で破棄。

**Disposeパターン（現状）**:
```csharp
public void Dispose()
{
    if (_disposed) return;
    _disposed = true;
    if (_ownsJa) _jaPhonemizer?.Dispose();
    if (_ownsEn) { _enEngine?.Dispose(); _enPhonemizer?.Dispose(); }
    if (_ownsEs) _esEngine?.Dispose();
    if (_ownsFr) _frEngine?.Dispose();
    if (_ownsPt) _ptEngine?.Dispose();
    if (_ownsZh) _zhEngine?.Dispose();
    if (_ownsKo) { _koG2PEngine?.Dispose(); _koPhonemizer?.Dispose(); }
}
```

### 1.4 InitializeAsync の構造

各言語が同一パターンの分岐で初期化される（7言語 x 同一パターン）:

```
if (ContainsLanguage("xx") && _xxEngine == null)
{
    _xxEngine = new XxxG2PEngine(...);
    _ownsXx = true;
}
```

例外: 英語と中国語はファイルベースの辞書読み込みが伴い、try-catchでラップされている。

### 1.5 PhonemizeWithProsodyAsync の switch文

```csharp
switch (lang)
{
    case "ja" when _jaPhonemizer != null:   ProcessJapanese(segText);
    case "en" when _enEngine != null:       ProcessEnglish(segText);
    case "es" when _esEngine != null:       ProcessSpanish(segText);
    case "fr" when _frEngine != null:       ProcessFrench(segText);
    case "pt" when _ptEngine != null:       ProcessPortuguese(segText);
    case "zh" when _zhEngine != null:       ProcessChinese(segText);
    case "ko" when _koG2PEngine != null:    ProcessKorean(segText);
    default:                                 ProcessFallbackAsync(lang, segText, ct);
}
```

### 1.6 現状の問題点

1. **スケーラビリティ**: 新言語追加に最低6箇所の変更が必要（フィールド, フラグ, Options, コンストラクタ, InitializeAsync, Dispose, switch文, ProcessXxx）
2. **型の不統一**: `DotNetG2PPhonemizer`, `EnglishG2PEngine`, `SpanishG2PEngine` 等すべて異なる型。共通インターフェースなし
3. **所有権管理の脆弱性**: フラグ設定忘れで二重破棄またはリーク
4. **レガシー分岐**: `_enPhonemizer`/`_koPhonemizer`（`IPhonemizerBackend`）がfallback用に残存
5. **フィールド数**: 9フィールド + 7フラグ + 1 disposed = 17フィールドが所有権管理に関与

---

## 2. Registry 設計

### 2.1 `ILanguageG2PHandler` インターフェース（P1-4で定義）

```csharp
/// <summary>
/// Language-specific G2P handler interface.
/// Each implementation wraps a DotNetG2P engine for a specific language.
/// </summary>
public interface ILanguageG2PHandler : IDisposable
{
    /// <summary>ISO 639-1 language code (e.g., "ja", "en").</summary>
    string LanguageCode { get; }

    /// <summary>Whether the handler has been initialized.</summary>
    bool IsInitialized { get; }

    /// <summary>Initializes the handler asynchronously (e.g., dictionary loading).</summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>Phonemizes text and returns phonemes with prosody.</summary>
    (string[] Phonemes, int[] A1, int[] A2, int[] A3) Process(string text);
}
```

### 2.2 RegistryEntry（所有権付きラッパー）

所有権を `Dictionary` の value 側に持たせる internal struct:

```csharp
/// <summary>
/// Registry entry that pairs a handler with ownership information.
/// </summary>
internal readonly struct HandlerEntry
{
    /// <summary>The language G2P handler.</summary>
    public ILanguageG2PHandler Handler { get; }

    /// <summary>
    /// True if MultilingualPhonemizer created this handler internally.
    /// False if injected externally (caller owns it).
    /// </summary>
    public bool IsOwned { get; }

    public HandlerEntry(ILanguageG2PHandler handler, bool isOwned)
    {
        Handler = handler;
        IsOwned = isOwned;
    }
}
```

### 2.3 MultilingualPhonemizer のフィールド変更

**Before (17フィールド)**:
```csharp
private DotNetG2PPhonemizer _jaPhonemizer;
private EnglishG2PEngine _enEngine;
private IPhonemizerBackend _enPhonemizer;
private SpanishG2PEngine _esEngine;
private FrenchG2PEngine _frEngine;
private PortugueseG2PEngine _ptEngine;
private ChineseG2PEngine _zhEngine;
private IPhonemizerBackend _koPhonemizer;
private KoreanG2PEngine _koG2PEngine;
private bool _ownsJa, _ownsEn, _ownsEs, _ownsFr, _ownsPt, _ownsZh, _ownsKo;
private bool _disposed;
```

**After (2フィールド)**:
```csharp
private readonly Dictionary<string, HandlerEntry> _handlers;
private bool _disposed;
```

---

## 3. 言語の登録・取得・破棄パターン

### 3.1 登録パターン

**外部注入（コンストラクタ経由）**: `IsOwned = false`

```csharp
// MultilingualPhonemizerOptions に ILanguageG2PHandler を指定
var options = new MultilingualPhonemizerOptions
{
    Languages = new[] { "ja", "en" },
    Handlers = new Dictionary<string, ILanguageG2PHandler>
    {
        ["ja"] = existingJaHandler,  // 外部で作成済み
    },
};
var mp = new MultilingualPhonemizer(options);
```

コンストラクタ内部:
```csharp
public MultilingualPhonemizer(MultilingualPhonemizerOptions options)
{
    // ...
    _handlers = new Dictionary<string, HandlerEntry>();

    // 外部注入ハンドラを登録（IsOwned = false）
    if (options.Handlers != null)
    {
        foreach (var (lang, handler) in options.Handlers)
        {
            _handlers[lang] = new HandlerEntry(handler, isOwned: false);
        }
    }
}
```

**内部生成（InitializeAsync）**: `IsOwned = true`

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

### 3.2 取得パターン

`PhonemizeWithProsodyAsync` の switch文をレジストリルックアップに置換:

**Before**:
```csharp
switch (lang)
{
    case "ja" when _jaPhonemizer != null:
        (segPhonemes, segA1, segA2, segA3) = ProcessJapanese(segText);
        break;
    case "en" when _enEngine != null:
        (segPhonemes, segA1, segA2, segA3) = ProcessEnglish(segText);
        break;
    // ... 5 more cases
    default:
        var fallbackResult = await ProcessFallbackAsync(lang, segText, ct);
        break;
}
```

**After**:
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

### 3.3 破棄パターン

**Before (7分岐)**:
```csharp
public void Dispose()
{
    if (_disposed) return;
    _disposed = true;
    if (_ownsJa) _jaPhonemizer?.Dispose();
    if (_ownsEn) { _enEngine?.Dispose(); _enPhonemizer?.Dispose(); }
    // ... 5 more
}
```

**After (ループ1つ)**:
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

---

## 4. MultilingualPhonemizerOptions との整合性

### 4.1 現状の Options

```csharp
public class MultilingualPhonemizerOptions
{
    public IReadOnlyList<string> Languages { get; set; }
    public string DefaultLatinLanguage { get; set; } = "en";
    public DotNetG2PPhonemizer JaPhonemizer { get; set; }
    public EnglishG2PEngine EnEngine { get; set; }
    [Obsolete] public IPhonemizerBackend EnPhonemizer { get; set; }
    public SpanishG2PEngine EsEngine { get; set; }
    public FrenchG2PEngine FrEngine { get; set; }
    public PortugueseG2PEngine PtEngine { get; set; }
    public ChineseG2PEngine ZhEngine { get; set; }
    [Obsolete] public IPhonemizerBackend KoPhonemizer { get; set; }
    public KoreanG2PEngine KoG2PEngine { get; set; }
}
```

### 4.2 Registry 化後の Options

```csharp
public class MultilingualPhonemizerOptions
{
    /// <summary>Languages to support (e.g., ["ja", "en"]).</summary>
    public IReadOnlyList<string> Languages { get; set; }

    /// <summary>Default language for Latin text (default: "en").</summary>
    public string DefaultLatinLanguage { get; set; } = "en";

    /// <summary>
    /// Pre-built language handlers. Key = language code, Value = handler instance.
    /// Handlers provided here are NOT owned by MultilingualPhonemizer
    /// (caller is responsible for disposal).
    /// Unspecified languages will be created internally during InitializeAsync.
    /// </summary>
    public Dictionary<string, ILanguageG2PHandler> Handlers { get; set; }

    public void Validate()
    {
        if (Languages == null || Languages.Count == 0)
            throw new ArgumentException(
                "At least one language must be specified.", nameof(Languages));
    }
}
```

### 4.3 移行の整合性

| 旧プロパティ | 移行先 |
|-------------|--------|
| `JaPhonemizer` | `Handlers["ja"] = new JapaneseG2PHandler(jaPhonemizer)` |
| `EnEngine` | `Handlers["en"] = new EnglishG2PHandler(enEngine)` |
| `EsEngine` | `Handlers["es"] = new SpanishG2PHandler(esEngine)` |
| `FrEngine` | `Handlers["fr"] = new FrenchG2PHandler(frEngine)` |
| `PtEngine` | `Handlers["pt"] = new PortugueseG2PHandler(ptEngine)` |
| `ZhEngine` | `Handlers["zh"] = new ChineseG2PHandler(zhEngine)` |
| `KoG2PEngine` | `Handlers["ko"] = new KoreanG2PHandler(koEngine)` |
| `EnPhonemizer` | 削除（P1-5で `IPhonemizerBackend` 廃止） |
| `KoPhonemizer` | 削除（P1-5で `IPhonemizerBackend` 廃止） |

---

## 5. デフォルトハンドラファクトリ

`InitializeAsync` で未登録言語に対してデフォルトハンドラを生成するファクトリメソッド:

```csharp
/// <summary>
/// Creates a default ILanguageG2PHandler for the given language code.
/// Returns null if the language is not supported.
/// </summary>
private static ILanguageG2PHandler CreateDefaultHandler(string lang)
{
    return lang switch
    {
        "ja" => new JapaneseG2PHandler(),
        "en" => new EnglishG2PHandler(),
        "es" => new SpanishG2PHandler(),
        "fr" => new FrenchG2PHandler(),
        "pt" => new PortugueseG2PHandler(),
        "zh" => new ChineseG2PHandler(),
        "ko" => new KoreanG2PHandler(),
        _ => null,
    };
}
```

このファクトリは `MultilingualPhonemizer` の private メソッドとする。将来的にDI化する場合は `ILanguageG2PHandlerFactory` インターフェースに抽出可能だが、現時点ではYAGNI。

---

## 6. ProcessXxx メソッドの移動先

現在 `MultilingualPhonemizer` の private メソッドである `ProcessJapanese`, `ProcessEnglish` 等のロジックは、P1-4 で作成する各 `ILanguageG2PHandler` 実装の `Process(string text)` メソッドに移動する。

| 現在のメソッド | 移動先クラス | 場所 |
|--------------|------------|------|
| `ProcessJapanese` | `JapaneseG2PHandler` | `Runtime/Core/Phonemizers/Multilingual/Handlers/` |
| `ProcessEnglish` | `EnglishG2PHandler` | 同上 |
| `ProcessSpanish` | `SpanishG2PHandler` | 同上 |
| `ProcessFrench` | `FrenchG2PHandler` | 同上 |
| `ProcessPortuguese` | `PortugueseG2PHandler` | 同上 |
| `ProcessChinese` | `ChineseG2PHandler` | 同上 |
| `ProcessKorean` | `KoreanG2PHandler` | 同上 |
| `ProcessFallbackAsync` | 削除（P1-5で廃止） | - |
| `GetBackendForLanguage` | 削除（レジストリルックアップに置換） | - |
| `ExtractProsodyArrays` | 共有ユーティリティまたはハンドラ基底クラスへ | 検討 |

---

## 7. PiperTTS.Inference.cs への影響

### 7.1 現在の Options 構築箇所

```csharp
// PiperTTS.Inference.cs L81-88
var phonemizerOptions = new MultilingualPhonemizerOptions
{
    Languages = supportedLanguages,
    DefaultLatinLanguage = _config.DefaultLanguage ?? "en",
    JaPhonemizer = _phonemizer as DotNetG2PPhonemizer
};
_multilingualPhonemizer = new MultilingualPhonemizer(phonemizerOptions);
```

### 7.2 Registry 化後の変更

```csharp
var handlers = new Dictionary<string, ILanguageG2PHandler>();

// 既存の日本語Phonemizerがあればハンドラとしてラップ
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

---

## 8. テストへの影響

### 8.1 影響を受けるテストファイル（8ファイル）

| ファイル | 影響内容 |
|---------|---------|
| `MultilingualPhonemizerTests.cs` | Obsoleteコンストラクタ使用 → Options に移行 |
| `MultilingualPhonemizerDeepTests.cs` | Obsoleteコンストラクタ使用 + 外部エンジン注入パターン → Handlers に移行 |
| `MultilingualPhonemizerPhase5Tests.cs` | Obsoleteコンストラクタ使用 + 外部 ChineseG2PEngine 注入 → Handlers に移行 |
| `MultilingualPhonemizerEosTests.cs` | Obsoleteコンストラクタ使用の可能性あり |
| `MultilingualAutoPromotionTests.cs` | `MultilingualPhonemizerOptions` のプロパティ参照 → Handlers パターンに変更 |
| `ChinesePhonemizerTests.cs` | MultilingualPhonemizer 経由テストの可能性 |
| `MultilingualPipelineTests.cs` | ランタイムテスト、Options構築パターンの変更 |
| `MultilingualModelPipelineTests.cs` | ランタイムテスト、Options構築パターンの変更 |

### 8.2 テスト移行例

**Before**（外部エンジン注入テスト）:
```csharp
var esEngine = new SpanishG2PEngine();
var phonemizer = new MultilingualPhonemizer(
    new[] { "ja", "en", "ko", "es" },
    defaultLatinLanguage: "en",
    jaPhonemizer: jaPhonemizer,
    koG2PEngine: koEngine,
    esEngine: esEngine);
```

**After**:
```csharp
var options = new MultilingualPhonemizerOptions
{
    Languages = new[] { "ja", "en", "ko", "es" },
    DefaultLatinLanguage = "en",
    Handlers = new Dictionary<string, ILanguageG2PHandler>
    {
        ["ja"] = new JapaneseG2PHandler(jaPhonemizer),
        ["ko"] = new KoreanG2PHandler(koEngine),
        ["es"] = new SpanishG2PHandler(esEngine),
    },
};
var phonemizer = new MultilingualPhonemizer(options);
```

### 8.3 Dispose テストの変更

現在のテスト `Dispose_DisposesAllBackends` は「外部注入されたバックエンドは Dispose されない」ことを検証している。Registry化後もこの挙動は `HandlerEntry.IsOwned = false` で維持される。テストは `ILanguageG2PHandler` のモック/スパイで所有権の正確性を検証可能になる。

---

## 9. 移行手順

### Step 1: P1-4 完了を確認
- `ILanguageG2PHandler` インターフェース定義
- 7言語ハンドラクラス（`JapaneseG2PHandler` ... `KoreanG2PHandler`）作成
- 各 `ProcessXxx` メソッドのロジックをハンドラに移動

### Step 2: HandlerEntry struct 追加
- `Runtime/Core/Phonemizers/Multilingual/HandlerEntry.cs` を新設
- `internal readonly struct HandlerEntry` を定義

### Step 3: MultilingualPhonemizerOptions の変更
- 個別エンジンプロパティ（7つ + Obsolete 2つ）を削除
- `Handlers` プロパティ（`Dictionary<string, ILanguageG2PHandler>`）を追加

### Step 4: MultilingualPhonemizer の内部フィールド置換
- 9個の個別フィールド + 7個のフラグを削除
- `Dictionary<string, HandlerEntry> _handlers` を追加
- コンストラクタで外部注入ハンドラを `IsOwned = false` で登録

### Step 5: InitializeAsync の簡素化
- 7言語の個別分岐をループ + `CreateDefaultHandler` に統一
- 未登録言語のみデフォルトハンドラを生成（`IsOwned = true`）

### Step 6: PhonemizeWithProsodyAsync の switch 文を置換
- `_handlers.TryGetValue(lang, out var entry)` + `entry.Handler.Process(segText)` に統一

### Step 7: Dispose の簡素化
- ループで `IsOwned == true` のハンドラのみ Dispose

### Step 8: PiperTTS.Inference.cs の Options 構築更新
- `JaPhonemizer = ...` → `Handlers = { ["ja"] = new JapaneseG2PHandler(...) }` に変更

### Step 9: テストの移行
- Obsoleteコンストラクタ使用箇所を Options + Handlers パターンに移行
- 外部エンジン注入テストをハンドララップパターンに更新
- Dispose所有権テストを `IsOwned` ベースに更新

### Step 10: InferenceEngineDemo.cs の更新
- MultilingualPhonemizerOptions 構築箇所を更新

---

## 10. P1-4 との依存関係

```
P1-4: ILanguageG2PHandler 定義・7ハンドラ作成
  │
  │  P1-4 が提供するもの:
  │  - ILanguageG2PHandler インターフェース
  │  - JapaneseG2PHandler (ProcessJapanese のロジック)
  │  - EnglishG2PHandler (ProcessEnglish のロジック)
  │  - SpanishG2PHandler (ProcessSpanish のロジック)
  │  - FrenchG2PHandler (ProcessFrench のロジック)
  │  - PortugueseG2PHandler (ProcessPortuguese のロジック)
  │  - ChineseG2PHandler (ProcessChinese のロジック)
  │  - KoreanG2PHandler (ProcessKorean のロジック)
  │
  ▼
P1-3: Dictionary Registry 化（本タスク）
  │
  │  P1-3 が実施するもの:
  │  - HandlerEntry struct 追加
  │  - MultilingualPhonemizerOptions の個別プロパティ → Handlers プロパティ
  │  - MultilingualPhonemizer の内部フィールド・フラグを Dictionary<string, HandlerEntry> に統一
  │  - InitializeAsync / Dispose / PhonemizeWithProsodyAsync の簡素化
  │
  ▼
P1-5: G2P 全同期化（IPhonemizerBackend 廃止）
  │  - ProcessFallbackAsync 削除
  │  - GetBackendForLanguage 削除
  │  - _enPhonemizer / _koPhonemizer 完全除去
  │
  ▼
P1-6: Obsolete コンストラクタ削除
```

**重要**: P1-3 と P1-4 は論理的には P1-4 が先行するが、実装は密結合しているため同一ブランチ/PRで同時実施が望ましい。

---

## 11. 変更サマリ

| カテゴリ | Before | After |
|---------|--------|-------|
| エンジンフィールド数 | 9 | 0（Dictionaryに統合） |
| 所有権フラグ数 | 7 | 0（HandlerEntry.IsOwnedに統合） |
| InitializeAsync 分岐数 | 7言語個別 | ループ1つ + ファクトリ |
| Dispose 分岐数 | 7言語個別 | ループ1つ |
| switch case 数 | 7 + default | TryGetValue 1行 |
| 新言語追加コスト | 6-8箇所変更 | ハンドラクラス1つ + ファクトリ1行 |
| Options プロパティ数 | 9（型バラバラ） | 1（`Handlers` Dictionary） |
