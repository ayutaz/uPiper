# MultilingualPhonemizer 実装仕様（Phase 5）

## 概要

MultilingualPhonemizerは7言語（ja, en, es, fr, pt, zh, ko）をサポートする多言語音素化システム。
テキストをUnicode範囲で言語セグメントに分割し、各セグメントを対応する言語バックエンドに委譲する。

### クラス構成

```
MultilingualPhonemizer : IDisposable
  ├── _languages: IReadOnlyList<string>     # サポート言語リスト
  ├── _defaultLatinLanguage: string         # ラテン文字のデフォルト言語（"en"）
  ├── _detector: UnicodeLanguageDetector    # Unicode範囲言語検出
  ├── _jaPhonemizer: DotNetG2PPhonemizer    # 日本語（直接、IPhonemizerBackend非経由）
  ├── _enPhonemizer: IPhonemizerBackend     # 英語（Flite / RuleBased）
  ├── _esPhonemizer: IPhonemizerBackend     # スペイン語
  ├── _frPhonemizer: IPhonemizerBackend     # フランス語
  ├── _ptPhonemizer: IPhonemizerBackend     # ポルトガル語
  ├── _zhPhonemizer: IPhonemizerBackend     # 中国語
  ├── _koPhonemizer: IPhonemizerBackend     # 韓国語
  ├── _isInitialized: bool                  # 初期化済みフラグ
  └── _disposed: bool                       # 破棄済みフラグ
```

**注意**: 日本語はDotNetG2PPhonemizer（Prosody対応）を直接使用し、IPhonemizerBackend経由ではない。
他の6言語はすべてIPhonemizerBackendインターフェース経由で統一的に扱われる。

## コンストラクタ

```csharp
public MultilingualPhonemizer(
    IReadOnlyList<string> languages,         // 必須: サポート言語リスト（1つ以上）
    string defaultLatinLanguage = "en",      // ラテン文字のデフォルト言語
    DotNetG2PPhonemizer jaPhonemizer = null,  // 日本語（Prosody対応、直接使用）
    IPhonemizerBackend enPhonemizer = null,   // 英語
    IPhonemizerBackend esPhonemizer = null,   // スペイン語
    IPhonemizerBackend frPhonemizer = null,   // フランス語
    IPhonemizerBackend ptPhonemizer = null,   // ポルトガル語
    IPhonemizerBackend zhPhonemizer = null,   // 中国語
    IPhonemizerBackend koPhonemizer = null)   // 韓国語
```

**後方互換性**: 新規追加の5言語パラメータ（es, fr, pt, zh, ko）はすべてnullデフォルト。
既存の`MultilingualPhonemizer(languages, defaultLatinLanguage, jaPhonemizer, enPhonemizer)`呼び出しは
変更なしで動作する。

## InitializeAsync() 処理フロー

```
InitializeAsync(CancellationToken)
  ↓
1. 既に初期化済みなら即return
  ↓
2. 各言語について、languagesリストに含まれ かつ バックエンドがnullの場合のみ初期化:
   ├── ja: DotNetG2PPhonemizer()を生成
   │   ├── WebGL: InitializeAsync()で非同期初期化
   │   └── 非WebGL: コンストラクタで同期初期化
   ├── en: FlitePhonemizerBackend → 失敗時 RuleBasedPhonemizer にフォールバック
   ├── es: SpanishPhonemizerBackend
   ├── fr: FrenchPhonemizerBackend
   ├── pt: PortuguesePhonemizerBackend
   ├── zh: ChinesePhonemizerBackend
   └── ko: KoreanPhonemizerBackend
  ↓
3. _isInitialized = true
```

**備考**: コンストラクタで事前構築済みバックエンドを渡した場合、該当言語のInitializeはスキップされる。

## PhonemizeWithProsodyAsync() 処理フロー

```
入力テキスト
  ↓
1. _detector.SegmentText(text)
   ├── Unicode範囲で言語判定
   └── セグメントリスト: List<(string language, string text)>
  ↓
2. 文字数加重で主言語(DetectedPrimaryLanguage)を算出
  ↓
3. セグメント別音素化
   for each (lang, segText) in segments:
     ├── lang == "ja": _jaPhonemizer.PhonemizeWithProsody(segText) [直接呼び出し]
     └── それ以外: GetBackendForLanguage(lang).PhonemizeAsync(segText, lang, ...) [IPhonemizerBackend経由]
     ├── 中間セグメント: 末尾のEOS的トークン($, ?, ?!, ?., ?~)を除去
     └── 音素・Prosody配列を連結
  ↓
4. Prosody配列をPadToLength()で音素数に揃える
  ↓
5. 出力: MultilingualPhonemizeResult
   ├── Phonemes: string[]       （BOS/EOSなし。PhonemeEncoderが付与）
   ├── ProsodyA1/A2/A3: int[]   （非日本語セグメントは0埋め）
   └── DetectedPrimaryLanguage: string
```

## GetBackendForLanguage() ヘルパー

非日本語セグメントのバックエンド解決を担うswitch式。

```csharp
private IPhonemizerBackend GetBackendForLanguage(string lang)
{
    return lang switch
    {
        "en" => _enPhonemizer,
        "es" => _esPhonemizer,
        "fr" => _frPhonemizer,
        "pt" => _ptPhonemizer,
        "zh" => _zhPhonemizer,
        "ko" => _koPhonemizer,
        _ => _enPhonemizer  // 未知の言語は英語にフォールバック
    };
}
```

## EOS処理

中間セグメント末尾のEOS的トークンを除去し、最終セグメントのみEOSを保持する。
BOS/EOSのラッピングはPhonemeEncoder側の責務であり、MultilingualPhonemizerは付与しない。

```csharp
private static readonly HashSet<string> EosLikeTokens =
    new() { "$", "?", "?!", "?.", "?~" };
```

## C# クラス実装

```csharp
namespace uPiper.Core.Phonemizers.Multilingual
{
    public class MultilingualPhonemizeResult
    {
        public string[] Phonemes { get; set; }             // BOS/EOSなし
        public int[] ProsodyA1 { get; set; }               // 非日本語セグメントは0
        public int[] ProsodyA2 { get; set; }
        public int[] ProsodyA3 { get; set; }
        public string DetectedPrimaryLanguage { get; set; } // 文字数加重の主言語
    }

    public class MultilingualPhonemizer : IDisposable
    {
        // Public properties
        public bool IsInitialized { get; }
        public IReadOnlyList<string> Languages { get; }

        // Constructor (7言語対応、新規5言語はすべてoptional)
        public MultilingualPhonemizer(
            IReadOnlyList<string> languages,
            string defaultLatinLanguage = "en",
            DotNetG2PPhonemizer jaPhonemizer = null,
            IPhonemizerBackend enPhonemizer = null,
            IPhonemizerBackend esPhonemizer = null,
            IPhonemizerBackend frPhonemizer = null,
            IPhonemizerBackend ptPhonemizer = null,
            IPhonemizerBackend zhPhonemizer = null,
            IPhonemizerBackend koPhonemizer = null);

        // Initialization (all configured backends)
        public Task InitializeAsync(CancellationToken cancellationToken = default);

        // Phonemization with prosody
        public Task<MultilingualPhonemizeResult> PhonemizeWithProsodyAsync(
            string text, CancellationToken cancellationToken = default);

        // Backend routing
        private IPhonemizerBackend GetBackendForLanguage(string lang);

        // Dispose all backends
        public void Dispose();
    }
}
```

## 言語バックエンド対応表

| 言語コード | バックエンド型 | インターフェース | Prosody対応 |
|-----------|--------------|-----------------|------------|
| ja | DotNetG2PPhonemizer | 直接呼び出し | Yes（A1/A2/A3） |
| en | FlitePhonemizerBackend (→ RuleBasedPhonemizer fallback) | IPhonemizerBackend | No（0埋め） |
| es | SpanishPhonemizerBackend | IPhonemizerBackend | No（0埋め） |
| fr | FrenchPhonemizerBackend | IPhonemizerBackend | No（0埋め） |
| pt | PortuguesePhonemizerBackend | IPhonemizerBackend | No（0埋め） |
| zh | ChinesePhonemizerBackend | IPhonemizerBackend | No（0埋め） |
| ko | KoreanPhonemizerBackend | IPhonemizerBackend | No（0埋め） |

## Dispose

全7言語バックエンドのDisposeを呼び出す。二重Dispose防止済み（_disposedフラグ）。

## token_mapper の C# 対応

piper-plus の `token_mapper.py` は複数文字音素を1文字のPUA文字に変換する。

```csharp
public static class TokenMapper
{
    private static readonly Dictionary<string, char> Token2Char = new();
    private static readonly Dictionary<char, string> Char2Token = new();

    public static char Register(string token)
    {
        if (token.Length == 1) return token[0];
        if (Token2Char.TryGetValue(token, out var cached)) return cached;

        // 動的割り当て（0xE059以降）
        var ch = (char)(_nextDynamicCodepoint++);
        Token2Char[token] = ch;
        Char2Token[ch] = token;
        return ch;
    }
}
```

固定PUAマッピング範囲:
- 日本語: 0xE000-0xE01C
- 中国語: 0xE020-0xE04A
- 韓国語: 0xE04B-0xE053
- スペイン語/ポルトガル語: 0xE054-0xE055
- フランス語: 0xE056-0xE058
