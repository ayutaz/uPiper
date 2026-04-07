# P1-4: ILanguageG2PHandler 全面移行 設計ドキュメント

**作成日**: 2026-04-08
**ベース**: v2.0-plan.md P1-4 セクション + v1.4.0-P5-2 完了済みリファクタリング
**前提**: v1.4.0 で `switch` + `ProcessXxx()` 抽出が完了済み（develop ブランチ）

---

## 1. 現状分析

### 1.1 MultilingualPhonemizer の構造（v1.4.0 時点）

**ファイル**: `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/MultilingualPhonemizer.cs`（708行）

v1.4.0 で `PhonemizeWithProsodyAsync` 内の if-else チェーンが `switch` 文 + `ProcessXxx()` private メソッドに抽出済み。現在の言語別処理メソッドは以下のとおり。

#### 各 ProcessXxx メソッドの詳細

| メソッド | 行数 | 依存エンジン | 主要ロジック | 複雑度 |
|---------|------|-------------|------------|--------|
| `ProcessJapanese` (L450-468) | 18行 | `DotNetG2PPhonemizer` | `PhonemizeWithProsody()` 呼び出し + 先頭PAD除去 | 低 |
| `ProcessEnglish` (L470-486) | 16行 | `EnglishG2PEngine` | `ToPuaPhonemes()` + `ToIpaWithProsody()` + Prosody展開ループ | 低 |
| `ProcessSpanish` (L488-495) | 7行 | `SpanishG2PEngine` | `ToIpaWithProsody()` + `ExtractProsodyArrays` ヘルパー | 低 |
| `ProcessFrench` (L497-504) | 7行 | `FrenchG2PEngine` | `ToPuaPhonemes()` + `ToIpaWithProsody()` + `ExtractProsodyArrays` | 低 |
| `ProcessPortuguese` (L506-513) | 7行 | `PortugueseG2PEngine` | FR と同一パターン | 低 |
| `ProcessChinese` (L515-593) | 78行 | `ChineseG2PEngine` | トーンPUA挿入、音節分配、残余音素処理、配列リサイズ | **高** |
| `ProcessKorean` (L595-645) | 50行 | `KoreanG2PEngine` | PUA/Prosody長不一致フォールバック、警告ログ | 中 |

#### 共通パターン分析

**全言語共通**:
- 戻り値型: `(string[] segPhonemes, int[] a1, int[] a2, int[] a3)`
- 入力: `string text`（テキストセグメント）
- 同期処理（async なし）

**パターン A（ES/FR/PT）**: `ToPuaPhonemes()` + `ToIpaWithProsody()` + `ExtractProsodyArrays` ヘルパー呼び出し。3言語でほぼ同一のコード構造。

**パターン B（EN）**: `ToPuaPhonemes()` + `ToIpaWithProsody()` + 手動ループでProsody展開。パターンAと類似だが、Prosodyオブジェクトの型が異なる（`EnglishG2PEngine` 固有の `Prosody` 型）。

**パターン C（JA）**: `DotNetG2PPhonemizer.PhonemizeWithProsody()` を呼び出し、結果をそのまま使用。先頭PAD除去の追加処理あり。唯一 `DotNetG2PPhonemizer` 型に直接依存。

**パターン D（ZH）**: 最も複雑。音節分配ロジック、トーンPUAマーカー挿入、残余音素処理。`TonePuaChars` static readonly フィールドへの参照。

**パターン E（KO）**: `ToPuaPhonemes()` + `ToIpaWithProsody()` + PUA/Prosody長一致チェック。不一致時のフォールバックロジックとログ出力。

### 1.2 エンジンフィールドと所有権管理

`MultilingualPhonemizer` は7言語分のエンジンフィールド + 所有権フラグを個別に保持している。

```
_jaPhonemizer  + _ownsJa   (DotNetG2PPhonemizer)
_enEngine      + _ownsEn   (EnglishG2PEngine)
_enPhonemizer             (IPhonemizerBackend, [Obsolete])
_esEngine      + _ownsEs   (SpanishG2PEngine)
_frEngine      + _ownsFr   (FrenchG2PEngine)
_ptEngine      + _ownsPt   (PortugueseG2PEngine)
_zhEngine      + _ownsZh   (ChineseG2PEngine)
_koG2PEngine   + _ownsKo   (KoreanG2PEngine)
_koPhonemizer             (IPhonemizerBackend, [Obsolete])
```

問題点:
- 言語追加のたびにフィールド2つ + `InitializeAsync` 分岐 + `Dispose` 分岐を追加する必要がある
- 各エンジン型が異なるため（`DotNetG2PPhonemizer`, `EnglishG2PEngine`, `SpanishG2PEngine` 等）、統一的な扱いができない
- `IPhonemizerBackend` のレガシーフィールドが2つ残存（`_enPhonemizer`, `_koPhonemizer`、いずれも `[Obsolete]`）

### 1.3 InitializeAsync の現状

`InitializeAsync` (L147-281) は各言語のエンジン初期化を逐次実行する。言語ごとに異なる初期化パターンを持つ。

| 言語 | 初期化パターン | 外部依存 |
|------|-------------|---------|
| ja | `new DotNetG2PPhonemizer()` (WebGL: `InitializeAsync`) | MeCab辞書 (StreamingAssets) |
| en | `new EnglishG2PEngine(dictPath)` or `new EnglishG2PEngine()` | CMUdict (StreamingAssets, optional) |
| es | `new SpanishG2PEngine()` | なし |
| fr | `new FrenchG2PEngine()` | なし |
| pt | `new PortugueseG2PEngine()` | なし |
| zh | `new ChineseG2PEngine(charPath, phrasePath)` | pinyin辞書 (StreamingAssets) |
| ko | `new KoreanG2PEngine()` | なし |

### 1.4 PiperTTS.Inference.cs からの利用

`PiperTTS.Inference.cs` は以下のように `MultilingualPhonemizer` を使用している。

```csharp
// 生成時
var phonemizerOptions = new MultilingualPhonemizerOptions
{
    Languages = supportedLanguages,
    DefaultLatinLanguage = _config.DefaultLanguage ?? "en",
    JaPhonemizer = _phonemizer as DotNetG2PPhonemizer
};
_multilingualPhonemizer = new MultilingualPhonemizer(phonemizerOptions);
await _multilingualPhonemizer.InitializeAsync(cancellationToken);

// 音素化時
var multiResult = await _multilingualPhonemizer.PhonemizeWithProsodyAsync(text, cancellationToken);

// 破棄時
_multilingualPhonemizer?.Dispose();
```

注目点: `JaPhonemizer` を外部から注入している（`PiperTTS` が別途所有する `_phonemizer` をキャスト）。この場合 `_ownsJa = false` となり、`MultilingualPhonemizer.Dispose()` では破棄されない。

### 1.5 既存テストの分析

| テストファイル | テスト数 | 主な検証内容 |
|--------------|--------|------------|
| `MultilingualPhonemizerTests.cs` | 10 | 基本的なコンストラクタ、言語検出、初期化 |
| `MultilingualPhonemizerDeepTests.cs` | 20 | 3言語以上混在、Prosody整合、EOS処理、エッジケース |
| `MultilingualPhonemizerEosTests.cs` | 10 | EOS/PUA トークン除去、先頭PAD除去、Prosody整合 |
| `MultilingualPhonemizerPhase5Tests.cs` | 18 | 各言語の個別テスト、CJK区別、Dispose |

テストの特徴:
- `StubPhonemizerBackend`（`IPhonemizerBackend` 実装）を使用したスタブテストが存在（`EosTests`）
- v2.0 で `IPhonemizerBackend` を廃止するため、スタブの仕組みを `ILanguageG2PHandler` ベースに移行する必要がある
- 大半のテストは `[Obsolete]` コンストラクタ経由で `MultilingualPhonemizer` を生成している（`#pragma warning disable CS0618`）
- 初期化の重い統合テスト（MeCab辞書ロード等）が多い

---

## 2. ILanguageG2PHandler インターフェース設計

### 2.1 インターフェース定義

```csharp
namespace uPiper.Core.Phonemizers.Multilingual
{
    /// <summary>
    /// Language-specific G2P handler interface.
    /// Each supported language implements this interface to provide phonemization
    /// with prosody information.
    /// </summary>
    public interface ILanguageG2PHandler : IDisposable
    {
        /// <summary>ISO 639-1 language code (e.g., "ja", "en", "zh").</summary>
        string LanguageCode { get; }

        /// <summary>Whether this handler has been initialized and is ready to process text.</summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Asynchronously initializes the handler (dictionary loading, etc.).
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task that completes when initialization is done.</returns>
        Task InitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Converts text to phonemes with prosody information.
        /// </summary>
        /// <param name="text">Text segment in this handler's language.</param>
        /// <returns>
        /// Tuple of (phonemes, prosodyA1, prosodyA2, prosodyA3).
        /// Arrays must be aligned (same length).
        /// </returns>
        (string[] Phonemes, int[] A1, int[] A2, int[] A3) Process(string text);
    }
}
```

### 2.2 設計判断

**Q: `Process` を同期メソッドにする理由は?**
A: 現在の7言語の `ProcessXxx` は全て同期処理。非同期パスは `ProcessFallbackAsync`（`IPhonemizerBackend` 経由）のみだが、P1-5 で廃止予定。WebGL の `DotNetG2PPhonemizer.InitializeAsync()` は `InitializeAsync` メソッドで対応済み。`Process` 自体は同期で問題ない。

**Q: `InitializeAsync` を必須にする理由は?**
A: ja/en/zh は辞書ロードが必要。WebGL 環境では全言語で非同期初期化が必要になる可能性がある。es/fr/pt/ko は `InitializeAsync` が no-op（`Task.CompletedTask` 返却）でよい。

**Q: `IDisposable` を継承する理由は?**
A: ja（MeCab トークナイザ）と en（CMUdict リソース）はネイティブリソースを保持する。全ハンドラに統一的に `Dispose` を呼べるようにする。es/fr/pt/ko は `Dispose` が no-op でよい。

**Q: `ExtractProsodyArrays` ヘルパーの扱いは?**
A: ES/FR/PT の3言語で共通利用されている。以下の選択肢がある:
1. `ILanguageG2PHandler` に static ヘルパーとして同梱 → インターフェースが実装詳細を持つため不適切
2. 基底抽象クラス `LanguageG2PHandlerBase` に配置 → 適切
3. 独立した static ユーティリティクラスに配置 → 最も疎結合

推奨: 選択肢 3（`G2PHandlerUtils` static クラス）。ES/FR/PT ハンドラから参照する。基底クラスは YAGNI（現時点で共通ロジックがヘルパー1つのみ）。

**Q: `TonePuaChars` static フィールドの扱いは?**
A: 現在 `MultilingualPhonemizer` の `private static readonly` フィールド。中国語ハンドラに移動する。

---

## 3. 7つのハンドラクラス設計

### 3.1 配置先

```
Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/Handlers/
    ILanguageG2PHandler.cs
    G2PHandlerUtils.cs
    JapaneseG2PHandler.cs
    EnglishG2PHandler.cs
    SpanishG2PHandler.cs
    FrenchG2PHandler.cs
    PortugueseG2PHandler.cs
    ChineseG2PHandler.cs
    KoreanG2PHandler.cs
```

`Handlers/` サブディレクトリに配置。`ILanguageG2PHandler.cs` と `G2PHandlerUtils.cs` も同ディレクトリに配置して関連性を明確にする。

### 3.2 各ハンドラクラスの設計

#### JapaneseG2PHandler

```csharp
public class JapaneseG2PHandler : ILanguageG2PHandler
{
    public string LanguageCode => "ja";
    public bool IsInitialized => _phonemizer != null;

    private DotNetG2PPhonemizer _phonemizer;
    private readonly bool _ownsEngine;

    // コンストラクタ1: 外部注入（PiperTTS.Inference.cs から使用）
    public JapaneseG2PHandler(DotNetG2PPhonemizer phonemizer)
    {
        _phonemizer = phonemizer ?? throw new ArgumentNullException(nameof(phonemizer));
        _ownsEngine = false;
    }

    // コンストラクタ2: 自動生成
    public JapaneseG2PHandler() { _ownsEngine = true; }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_phonemizer != null) return;
#if UNITY_WEBGL && !UNITY_EDITOR
        _phonemizer = new DotNetG2PPhonemizer();
        await _phonemizer.InitializeAsync(ct);
#else
        _phonemizer = new DotNetG2PPhonemizer();
        await Task.CompletedTask;
#endif
    }

    public (string[] Phonemes, int[] A1, int[] A2, int[] A3) Process(string text)
    {
        // ProcessJapanese のロジックをそのまま移植
        // - PhonemizeWithProsody 呼び出し
        // - 先頭PAD除去
    }

    public void Dispose()
    {
        if (_ownsEngine) _phonemizer?.Dispose();
    }
}
```

**依存**: `DotNetG2PPhonemizer`（dot-net-g2p）
**特記事項**: `PiperTTS.Inference.cs` が `DotNetG2PPhonemizer` を外部注入するパターンを維持するため、2つのコンストラクタを提供。`_ownsEngine` フラグで所有権を管理。

#### EnglishG2PHandler

```csharp
public class EnglishG2PHandler : ILanguageG2PHandler
{
    public string LanguageCode => "en";
    public bool IsInitialized => _engine != null;

    private EnglishG2PEngine _engine;
    private readonly bool _ownsEngine;

    public EnglishG2PHandler(EnglishG2PEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _ownsEngine = false;
    }

    public EnglishG2PHandler() { _ownsEngine = true; }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_engine != null) return;
        // CMUdict パス検索 + EnglishG2PEngine 生成ロジック
        // （現在の MultilingualPhonemizer.InitializeAsync L168-199 を移植）
        await Task.CompletedTask;
    }

    public (string[] Phonemes, int[] A1, int[] A2, int[] A3) Process(string text)
    {
        // ProcessEnglish のロジックを移植
    }

    public void Dispose()
    {
        if (_ownsEngine) _engine?.Dispose();
    }
}
```

**依存**: `EnglishG2PEngine`（DotNetG2P.English）
**特記事項**: CMUdict パス検索ロジック（`Application.streamingAssetsPath` 参照）を `InitializeAsync` に含む。

#### SpanishG2PHandler / FrenchG2PHandler / PortugueseG2PHandler

3言語は同一パターン。ES を例示する。

```csharp
public class SpanishG2PHandler : ILanguageG2PHandler
{
    public string LanguageCode => "es";
    public bool IsInitialized => _engine != null;

    private SpanishG2PEngine _engine;
    private readonly bool _ownsEngine;

    public SpanishG2PHandler(SpanishG2PEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _ownsEngine = false;
    }

    public SpanishG2PHandler() { _ownsEngine = true; }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_engine != null) return;
        _engine = new SpanishG2PEngine();
        await Task.CompletedTask;
    }

    public (string[] Phonemes, int[] A1, int[] A2, int[] A3) Process(string text)
    {
        // ProcessSpanish のロジックを移植
        // G2PHandlerUtils.ExtractProsodyArrays() を使用
    }

    public void Dispose()
    {
        if (_ownsEngine) _engine?.Dispose();
    }
}
```

**FR/PT の差分**:
- `ProcessFrench` は `ToPuaPhonemes()` + `ToIpaWithProsody()` で音素取得（`ToPuaPhonemes` の結果を使用）
- `ProcessSpanish` は `ToIpaWithProsody()` の `Phonemes` プロパティを使用
- `ProcessPortuguese` は `ProcessFrench` と同一パターン

これらの差分は各ハンドラの `Process` メソッド内で吸収される。

#### ChineseG2PHandler

```csharp
public class ChineseG2PHandler : ILanguageG2PHandler
{
    public string LanguageCode => "zh";
    public bool IsInitialized => _engine != null;

    // TonePuaChars を MultilingualPhonemizer から移動
    private static readonly char[] TonePuaChars =
        { '\0', '\ue046', '\ue047', '\ue048', '\ue049', '\ue04a' };

    private ChineseG2PEngine _engine;
    private readonly bool _ownsEngine;

    public ChineseG2PHandler(ChineseG2PEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _ownsEngine = false;
    }

    public ChineseG2PHandler() { _ownsEngine = true; }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_engine != null) return;
        // pinyin辞書パス検索 + ChineseG2PEngine 生成ロジック
        // （現在の MultilingualPhonemizer.InitializeAsync L229-260 を移植）
        await Task.CompletedTask;
    }

    public (string[] Phonemes, int[] A1, int[] A2, int[] A3) Process(string text)
    {
        // ProcessChinese のロジック（78行）をそのまま移植
        // TonePuaChars はクラス内 static フィールドとして利用
    }

    public void Dispose()
    {
        if (_ownsEngine) _engine?.Dispose();
    }
}
```

**依存**: `ChineseG2PEngine`（DotNetG2P.Chinese）
**特記事項**: 最も複雑な `Process` メソッド。音節分配ロジック、トーンPUAマーカー挿入、配列リサイズを含む。`TonePuaChars` を `MultilingualPhonemizer` から本クラスに移動。

#### KoreanG2PHandler

```csharp
public class KoreanG2PHandler : ILanguageG2PHandler
{
    public string LanguageCode => "ko";
    public bool IsInitialized => _engine != null;

    private KoreanG2PEngine _engine;
    private readonly bool _ownsEngine;

    public KoreanG2PHandler(KoreanG2PEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _ownsEngine = false;
    }

    public KoreanG2PHandler() { _ownsEngine = true; }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_engine != null) return;
        _engine = new KoreanG2PEngine();
        await Task.CompletedTask;
    }

    public (string[] Phonemes, int[] A1, int[] A2, int[] A3) Process(string text)
    {
        // ProcessKorean のロジックを移植
        // PUA/Prosody 長不一致フォールバック + 警告ログ
    }

    public void Dispose()
    {
        if (_ownsEngine) _engine?.Dispose();
    }
}
```

**依存**: `KoreanG2PEngine`（DotNetG2P.Korean）
**特記事項**: PUA/Prosody 長不一致時の `PiperLogger.LogWarning` を維持。

### 3.3 G2PHandlerUtils

```csharp
namespace uPiper.Core.Phonemizers.Multilingual.Handlers
{
    /// <summary>
    /// Shared utility methods for ILanguageG2PHandler implementations.
    /// </summary>
    internal static class G2PHandlerUtils
    {
        /// <summary>
        /// Extracts prosody A1/A2/A3 arrays from a prosody info array.
        /// Used by ES/FR/PT handlers to avoid duplicated extraction loops.
        /// </summary>
        internal static (int[] a1, int[] a2, int[] a3) ExtractProsodyArrays<T>(
            T[] prosody, Func<T, (int a1, int a2, int a3)> accessor, int phonemeCount)
        {
            // MultilingualPhonemizer.ExtractProsodyArrays のロジックをそのまま移動
        }
    }
}
```

---

## 4. MultilingualPhonemizer 移行後の姿

### 4.1 移行後のクラス構造

```csharp
public class MultilingualPhonemizer : IDisposable
{
    private static readonly HashSet<string> EosLikeTokens = ...;

    private readonly UnicodeLanguageDetector _detector;
    private readonly IReadOnlyList<string> _languages;
    private readonly HashSet<string> _languageSet;
    private readonly string _defaultLatinLanguage;

    // 全言語ハンドラを統一的に管理
    private readonly Dictionary<string, ILanguageG2PHandler> _handlers;
    // 所有権は各ハンドラ内部で管理 (_ownsEngine フラグ)

    private volatile bool _isInitialized;
    private bool _disposed;

    // コンストラクタ（Options 経由のみ、Obsolete コンストラクタは P1-6 で削除）
    public MultilingualPhonemizer(MultilingualPhonemizerOptions options)
    {
        // options.Handlers があればそれを使用
        // なければ空の Dictionary を初期化
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        // 未登録の言語に対してデフォルトファクトリからハンドラを生成
        foreach (var lang in _languages)
        {
            if (!_handlers.ContainsKey(lang))
            {
                var handler = CreateDefaultHandler(lang);
                if (handler != null)
                    _handlers[lang] = handler;
            }
        }

        // 全ハンドラの InitializeAsync を呼び出し
        foreach (var handler in _handlers.Values)
        {
            if (!handler.IsInitialized)
                await handler.InitializeAsync(ct);
        }

        _isInitialized = true;
    }

    public async Task<MultilingualPhonemizeResult> PhonemizeWithProsodyAsync(
        string text, CancellationToken ct = default)
    {
        // ... (セグメント分割、言語検出は変更なし)

        for (var si = 0; si < segments.Count; si++)
        {
            var (lang, segText) = segments[si];

            // switch 文が Dictionary lookup に置換される
            if (_handlers.TryGetValue(lang, out var handler))
            {
                (segPhonemes, segA1, segA2, segA3) = handler.Process(segText);
            }
            else
            {
                PiperLogger.LogWarning(
                    $"[MultilingualPhonemizer] No handler for '{lang}', skipping.");
                continue;
            }

            // ... (EOS処理は変更なし)
        }

        // ... (Prosody整合、結果構築は変更なし)
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var handler in _handlers.Values)
            handler.Dispose();
    }

    // デフォルトハンドラファクトリ
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
            _ => null
        };
    }
}
```

### 4.2 MultilingualPhonemizerOptions 移行後

```csharp
public class MultilingualPhonemizerOptions
{
    public IReadOnlyList<string> Languages { get; set; }
    public string DefaultLatinLanguage { get; set; } = "en";

    /// <summary>
    /// Pre-built language handlers. Keys are language codes (e.g., "ja", "en").
    /// If a language is in Languages but not in Handlers,
    /// MultilingualPhonemizer will create a default handler during InitializeAsync.
    /// </summary>
    public Dictionary<string, ILanguageG2PHandler> Handlers { get; set; }

    // 以下は全て廃止（P1-5/P1-6 で削除）
    // JaPhonemizer, EnEngine, EnPhonemizer, EsEngine, FrEngine, PtEngine,
    // ZhEngine, KoPhonemizer, KoG2PEngine
}
```

### 4.3 削除される要素

| 要素 | 現在の場所 | 削除理由 |
|------|-----------|---------|
| `_jaPhonemizer` フィールド | MultilingualPhonemizer | ハンドラ内に移動 |
| `_enEngine` フィールド | MultilingualPhonemizer | ハンドラ内に移動 |
| `_esEngine` フィールド | MultilingualPhonemizer | ハンドラ内に移動 |
| `_frEngine` フィールド | MultilingualPhonemizer | ハンドラ内に移動 |
| `_ptEngine` フィールド | MultilingualPhonemizer | ハンドラ内に移動 |
| `_zhEngine` フィールド | MultilingualPhonemizer | ハンドラ内に移動 |
| `_koG2PEngine` フィールド | MultilingualPhonemizer | ハンドラ内に移動 |
| `_enPhonemizer` フィールド | MultilingualPhonemizer | P1-5 で IPhonemizerBackend 廃止 |
| `_koPhonemizer` フィールド | MultilingualPhonemizer | P1-5 で IPhonemizerBackend 廃止 |
| `_ownsXxx` フラグ x7 | MultilingualPhonemizer | ハンドラ内 `_ownsEngine` に移動 |
| `ProcessJapanese` メソッド | MultilingualPhonemizer | JapaneseG2PHandler.Process に移動 |
| `ProcessEnglish` メソッド | MultilingualPhonemizer | EnglishG2PHandler.Process に移動 |
| `ProcessSpanish` メソッド | MultilingualPhonemizer | SpanishG2PHandler.Process に移動 |
| `ProcessFrench` メソッド | MultilingualPhonemizer | FrenchG2PHandler.Process に移動 |
| `ProcessPortuguese` メソッド | MultilingualPhonemizer | PortugueseG2PHandler.Process に移動 |
| `ProcessChinese` メソッド | MultilingualPhonemizer | ChineseG2PHandler.Process に移動 |
| `ProcessKorean` メソッド | MultilingualPhonemizer | KoreanG2PHandler.Process に移動 |
| `ProcessFallbackAsync` メソッド | MultilingualPhonemizer | P1-5 で廃止 |
| `GetBackendForLanguage` メソッド | MultilingualPhonemizer | P1-5 で廃止 |
| `ExtractProsodyArrays` メソッド | MultilingualPhonemizer | G2PHandlerUtils に移動 |
| `TonePuaChars` フィールド | MultilingualPhonemizer | ChineseG2PHandler に移動 |
| `ContainsLanguage` メソッド | MultilingualPhonemizer | `_handlers.ContainsKey` に置換 |

---

## 5. 移行手順

### 5.1 段階的移行（推奨）

P1-4 は「一括移行」ではなく「段階的移行」を推奨する。理由:
- 各ステップでテストを実行し、振る舞い不変を確認できる
- レビュー単位が小さくなり、レビュアーの負担が軽減される
- P1-3（Dictionary Registry 化）と P1-5（IPhonemizerBackend 廃止）との依存関係を段階的に解決できる

#### Step 1: ILanguageG2PHandler インターフェース + G2PHandlerUtils 定義

**スコープ**: `ILanguageG2PHandler.cs`, `G2PHandlerUtils.cs` の新規作成
**テスト影響**: なし（新規ファイルのみ）
**所要時間**: 0.5 人日

1. `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/Handlers/` ディレクトリ作成
2. `ILanguageG2PHandler.cs` 作成
3. `G2PHandlerUtils.cs` 作成（`ExtractProsodyArrays` を `MultilingualPhonemizer` からコピー）
4. `.asmdef` の参照確認（`uPiper.Runtime.asmdef` 内なので追加不要）

#### Step 2: 7つのハンドラクラス実装

**スコープ**: 7ハンドラクラスの新規作成。`MultilingualPhonemizer` はまだ変更しない。
**テスト影響**: なし（新規ファイルのみ。各ハンドラの単体テストを追加）
**所要時間**: 2 人日

1. `JapaneseG2PHandler.cs` 〜 `KoreanG2PHandler.cs` を作成
2. 各ハンドラの `Process` メソッドに `MultilingualPhonemizer` の対応する `ProcessXxx` のロジックをコピー
3. 各ハンドラの `InitializeAsync` に `MultilingualPhonemizer.InitializeAsync` の対応部分をコピー
4. 各ハンドラの単体テストを作成（`Tests/Editor/Phonemizers/Handlers/` に配置）

#### Step 3: MultilingualPhonemizer を Dictionary 化

**スコープ**: `MultilingualPhonemizer` の内部を `Dictionary<string, ILanguageG2PHandler>` ベースに書き換え。`MultilingualPhonemizerOptions` にも `Handlers` プロパティ追加。
**テスト影響**: 既存テストが通ることを確認。一部テストは `Handlers` 経由の生成に書き換え。
**所要時間**: 1.5 人日

1. `MultilingualPhonemizerOptions` に `Handlers` プロパティを追加
2. `MultilingualPhonemizer` コンストラクタで `Handlers` を受け取り `_handlers` Dictionary に格納
3. `InitializeAsync` をハンドラベースに書き換え
4. `PhonemizeWithProsodyAsync` の switch 文を `_handlers[lang].Process()` に置換
5. `Dispose` をハンドラベースに書き換え
6. `ProcessXxx` メソッド群、個別エンジンフィールド群、`_ownsXxx` フラグ群を削除
7. `TonePuaChars` を削除（`ChineseG2PHandler` に移動済み）
8. `ExtractProsodyArrays` を削除（`G2PHandlerUtils` に移動済み）

#### Step 4: 既存テストの移行

**スコープ**: テストファイル群の更新。
**所要時間**: 1 人日

1. `StubPhonemizerBackend` を `StubG2PHandler : ILanguageG2PHandler` に置き換え
2. `[Obsolete]` コンストラクタ経由の生成を `MultilingualPhonemizerOptions.Handlers` 経由に書き換え
3. 各ハンドラの単体テスト追加
4. 全テスト通過を確認

#### Step 5: レガシー API クリーンアップ（P1-5, P1-6 と同時）

**スコープ**: `IPhonemizerBackend` 参照の削除、`[Obsolete]` コンストラクタの削除。
**所要時間**: 0.5 人日

1. `MultilingualPhonemizerOptions` から `[Obsolete]` プロパティ群を削除
2. `MultilingualPhonemizer` の `[Obsolete]` コンストラクタを削除
3. `ProcessFallbackAsync`, `GetBackendForLanguage` を削除

### 5.2 合計見積もり

| Step | 内容 | 見積もり |
|------|------|---------|
| Step 1 | インターフェース + ユーティリティ | 0.5 人日 |
| Step 2 | 7ハンドラクラス実装 + 単体テスト | 2 人日 |
| Step 3 | MultilingualPhonemizer 書き換え | 1.5 人日 |
| Step 4 | 既存テスト移行 | 1 人日 |
| Step 5 | レガシー API クリーンアップ | 0.5 人日 |
| **合計** | | **5.5 人日** |

---

## 6. 影響範囲

### 6.1 変更が必要なファイル一覧

#### 新規作成

| ファイル | 内容 |
|---------|------|
| `Runtime/Core/Phonemizers/Multilingual/Handlers/ILanguageG2PHandler.cs` | インターフェース定義 |
| `Runtime/Core/Phonemizers/Multilingual/Handlers/G2PHandlerUtils.cs` | 共通ユーティリティ |
| `Runtime/Core/Phonemizers/Multilingual/Handlers/JapaneseG2PHandler.cs` | 日本語ハンドラ |
| `Runtime/Core/Phonemizers/Multilingual/Handlers/EnglishG2PHandler.cs` | 英語ハンドラ |
| `Runtime/Core/Phonemizers/Multilingual/Handlers/SpanishG2PHandler.cs` | スペイン語ハンドラ |
| `Runtime/Core/Phonemizers/Multilingual/Handlers/FrenchG2PHandler.cs` | フランス語ハンドラ |
| `Runtime/Core/Phonemizers/Multilingual/Handlers/PortugueseG2PHandler.cs` | ポルトガル語ハンドラ |
| `Runtime/Core/Phonemizers/Multilingual/Handlers/ChineseG2PHandler.cs` | 中国語ハンドラ |
| `Runtime/Core/Phonemizers/Multilingual/Handlers/KoreanG2PHandler.cs` | 韓国語ハンドラ |
| `Tests/Editor/Phonemizers/Handlers/*G2PHandlerTests.cs` | 各ハンドラの単体テスト |

#### 変更

| ファイル | 変更内容 |
|---------|---------|
| `Runtime/Core/Phonemizers/Multilingual/MultilingualPhonemizer.cs` | switch 文 -> Dictionary lookup、個別フィールド削除 |
| `Runtime/Core/Phonemizers/Multilingual/MultilingualPhonemizerOptions.cs` | `Handlers` プロパティ追加、個別エンジンプロパティ削除 |
| `Runtime/Core/PiperTTS.Inference.cs` | Options 生成を Handlers 経由に変更 |
| `Tests/Editor/MultilingualPhonemizerTests.cs` | コンストラクタ呼び出し変更 |
| `Tests/Editor/Phonemizers/MultilingualPhonemizerDeepTests.cs` | Options 経由に変更 |
| `Tests/Editor/Phonemizers/MultilingualPhonemizerEosTests.cs` | StubPhonemizerBackend -> StubG2PHandler |
| `Tests/Editor/Phonemizers/MultilingualPhonemizerPhase5Tests.cs` | Options 経由に変更 |

#### 削除候補（P1-5 で実施）

| ファイル | 削除理由 |
|---------|---------|
| `Runtime/Core/Phonemizers/Backend/IPhonemizerBackend.cs` | レガシーインターフェース廃止 |
| `Runtime/Core/Phonemizers/Backend/PhonemizerBackendOptions.cs` | IPhonemizerBackend と共に廃止 |

### 6.2 Assembly Definition への影響

`uPiper.Runtime.asmdef` の参照先は変更なし。新規ファイルは全て `Runtime/Core/Phonemizers/Multilingual/Handlers/` 配下で、既存の asmdef 範囲内。

---

## 7. テスト戦略

### 7.1 単体テスト（新規追加）

各ハンドラに対して以下のテストを追加する。

```
Tests/Editor/Phonemizers/Handlers/
    JapaneseG2PHandlerTests.cs
    EnglishG2PHandlerTests.cs
    SpanishG2PHandlerTests.cs
    FrenchG2PHandlerTests.cs
    PortugueseG2PHandlerTests.cs
    ChineseG2PHandlerTests.cs
    KoreanG2PHandlerTests.cs
```

各ハンドラテストの共通テストケース:

| テストケース | 検証内容 |
|------------|---------|
| `LanguageCode_ReturnsCorrectCode` | `LanguageCode` プロパティが正しい言語コードを返す |
| `IsInitialized_BeforeInit_ReturnsFalse` | 初期化前は `false` |
| `InitializeAsync_SetsIsInitialized` | 初期化後は `true` |
| `Process_ValidText_ReturnsAlignedArrays` | 戻り値の4配列が同一長 |
| `Process_EmptyText_ReturnsEmptyArrays` | 空文字列の処理 |
| `Dispose_CalledTwice_DoesNotThrow` | 二重 Dispose 安全性 |
| `Dispose_ExternalEngine_DoesNotDisposeEngine` | 外部注入時の所有権 |

言語固有テストケース:

| 言語 | 追加テストケース |
|------|---------------|
| ja | `Process_LeadingPadStripped` (先頭PAD除去) |
| en | `Process_ProsodyAllZeroA1` (A1=0) |
| zh | `Process_TonePuaInserted` (トーンPUA挿入確認) |
| ko | `Process_ProsodyLengthMismatch_LogsWarning` (長不一致ログ) |
| es/fr/pt | `Process_ProsodyExtracted` (Prosody抽出) |

### 7.2 統合テスト（既存テストの移行）

既存の4テストファイルは以下の方針で移行する:

1. **`MultilingualPhonemizerTests.cs`**: `[Obsolete]` コンストラクタから Options + Handlers 経由に変更。振る舞い不変を確認。
2. **`MultilingualPhonemizerDeepTests.cs`**: `CreateInitialized` ヘルパーを Handlers 経由に変更。3言語以上混在テストの振る舞い不変を確認。
3. **`MultilingualPhonemizerEosTests.cs`**: `StubPhonemizerBackend` を `StubG2PHandler : ILanguageG2PHandler` に置き換え。EOS処理の振る舞い不変を確認。
4. **`MultilingualPhonemizerPhase5Tests.cs`**: 個別エンジン注入を Handlers 経由に変更。

### 7.3 テストスタブの移行

現在の `StubPhonemizerBackend`（`IPhonemizerBackend` 実装）を以下に置き換え:

```csharp
/// <summary>
/// Minimal ILanguageG2PHandler stub for unit tests.
/// </summary>
internal sealed class StubG2PHandler : ILanguageG2PHandler
{
    private readonly (string[] Phonemes, int[] A1, int[] A2, int[] A3) _result;

    public string LanguageCode { get; }
    public bool IsInitialized => true;

    public StubG2PHandler(
        string languageCode,
        string[] phonemes,
        int[] a1 = null, int[] a2 = null, int[] a3 = null)
    {
        LanguageCode = languageCode;
        var len = phonemes?.Length ?? 0;
        _result = (
            phonemes ?? Array.Empty<string>(),
            a1 ?? new int[len],
            a2 ?? new int[len],
            a3 ?? new int[len]
        );
    }

    public Task InitializeAsync(CancellationToken ct = default)
        => Task.CompletedTask;

    public (string[] Phonemes, int[] A1, int[] A2, int[] A3) Process(string text)
        => _result;

    public void Dispose() { }
}
```

### 7.4 回帰テスト基準

移行完了時に以下の全テストが変更なしで（またはスタブ置換のみで）パスすること:

- `dotnet format --verify-no-changes` パス
- EditMode テスト全パス
- PlayMode テスト全パス
- CI（GitHub Actions `unity-tests.yml`）全パス

---

## 8. 破壊的変更

### 8.1 P1-4 単体での破壊的変更

| 変更 | 影響 | 緩和策 |
|------|------|--------|
| `MultilingualPhonemizerOptions` に `Handlers` プロパティ追加 | 既存コードに影響なし（追加のみ） | なし（後方互換） |
| 個別エンジンプロパティ（`JaPhonemizer`, `EnEngine` 等）の廃止 | **破壊的** | Step 3 で `[Obsolete]` 指定し、Step 5（P1-5/P1-6）で削除 |

### 8.2 P1-4 + P1-5 + P1-6 合計での破壊的変更

| 変更 | 影響 |
|------|------|
| `MultilingualPhonemizerOptions` の個別エンジンプロパティ削除 | `JaPhonemizer`, `EnEngine`, `EsEngine`, `FrEngine`, `PtEngine`, `ZhEngine`, `KoG2PEngine` が使用不可 |
| `MultilingualPhonemizerOptions.EnPhonemizer` / `KoPhonemizer` 削除 | `IPhonemizerBackend` ベースのスタブ注入が不可 |
| `MultilingualPhonemizer` の `[Obsolete]` コンストラクタ削除 | 14引数コンストラクタが使用不可 |
| `IPhonemizerBackend` インターフェース削除 | 実装クラスがコンパイル不可 |

### 8.3 マイグレーションパス

```csharp
// v1.4.0 (現在)
var options = new MultilingualPhonemizerOptions
{
    Languages = new[] { "ja", "en", "es" },
    DefaultLatinLanguage = "en",
    JaPhonemizer = myJaPhonemizer,
    EsEngine = myEsEngine,
};

// v2.0 (移行後)
var options = new MultilingualPhonemizerOptions
{
    Languages = new[] { "ja", "en", "es" },
    DefaultLatinLanguage = "en",
    Handlers = new Dictionary<string, ILanguageG2PHandler>
    {
        ["ja"] = new JapaneseG2PHandler(myJaPhonemizer),
        ["es"] = new SpanishG2PHandler(myEsEngine),
        // "en" は Handlers に含めない -> InitializeAsync で自動生成
    },
};
```

---

## 9. P1-3 との関係

P1-3（Dictionary Registry 化）と P1-4（ILanguageG2PHandler 全面移行）は密接に関連する。v2.0-plan.md の依存関係では P1-4 が先（P1-4 → P1-3）だが、実質的に Step 3 で P1-3 の内容（`Dictionary<string, ILanguageG2PHandler>` レジストリ）も同時に実現される。

**推奨**: P1-3 と P1-4 を1つの PR で実施する。Step 3 が両方のスコープをカバーするため、分離する意味が薄い。

---

## 10. 未解決事項

| 項目 | 現状 | 対応方針 |
|------|------|---------|
| `PiperTTS.Inference.cs` の `DotNetG2PPhonemizer` 直接参照 | `_phonemizer as DotNetG2PPhonemizer` でキャストして `JaPhonemizer` に注入 | `JapaneseG2PHandler(phonemizer)` コンストラクタで対応 |
| WebGL の `InitializeAsync` | `DotNetG2PPhonemizer.InitializeAsync()` が WebGL 専用 | `JapaneseG2PHandler.InitializeAsync` 内で `#if UNITY_WEBGL` 分岐を維持 |
| `EosLikeTokens` の配置 | 現在 `MultilingualPhonemizer` の static フィールド | `MultilingualPhonemizer` に残す（EOS処理はセグメント結合時のロジックであり、個別ハンドラの責務ではない） |
| `PadToLength` ヘルパー | 現在 `MultilingualPhonemizer` の private static メソッド | `MultilingualPhonemizer` に残す（Prosody配列整合はセグメント結合後の責務） |
