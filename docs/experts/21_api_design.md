# 公開API拡張設計

> **Phase 5 完了** (2026-03-18): 7言語対応（ja, en, es, fr, pt, zh, ko）。MultilingualPhonemizer に5つの新バックエンドを追加。

## 現在の公開API（Phase 5 実装済み）

### IPiperTTS インターフェース

```csharp
public interface IPiperTTS : IDisposable
{
    // ── 初期化 ──
    Task InitializeAsync(CancellationToken = default);
    Task LoadVoiceAsync(PiperVoiceConfig voiceConfig, CancellationToken = default);
    IReadOnlyList<PiperVoiceConfig> GetAvailableVoices();

    // ── 音声生成（既存） ──
    Task<AudioClip> GenerateAudioAsync(string text, CancellationToken = default);
    Task<AudioClip> GenerateAudioAsync(string text, PiperVoiceConfig voiceConfig, CancellationToken = default);
    IAsyncEnumerable<AudioChunk> StreamAudioAsync(string text, CancellationToken = default);
    IAsyncEnumerable<AudioChunk> StreamAudioAsync(string text, PiperVoiceConfig voiceConfig, CancellationToken = default);

    // ── 音声生成（Phase 4 追加: 言語指定版） ──
    Task<AudioClip> GenerateAudioAsync(string text, string language, CancellationToken = default);

    // ── 言語情報（Phase 4 追加） ──
    string DetectLanguage(string text);
    IReadOnlyList<string> GetSupportedLanguages();

    // ── プロパティ ──
    PiperConfig Configuration { get; }
    bool IsInitialized { get; }
    PiperVoiceConfig CurrentVoice { get; }

    // ── キャッシュ ──
    void ClearCache();
    CacheStatistics GetCacheStatistics();

    // ── イベント ──
    event Action<bool> OnInitialized;
    event Action<PiperVoiceConfig> OnVoiceLoaded;
    event Action<PiperException> OnError;
    event Action<string> OnLanguageDetected;   // Phase 4 追加
}
```

**C#オーバーロード解決の注意**: `GenerateAudioAsync(string, string)` は位置引数で呼ぶと
他のオーバーロードと曖昧になる可能性がある。**必ず名前付き引数 `language:` を使用すること**を推奨。
```csharp
// 推奨: 名前付き引数
var audio = await piper.GenerateAudioAsync("Hello", language: "en");

// 非推奨: 位置引数（曖昧性リスク）
var audio = await piper.GenerateAudioAsync("Hello", "en");
```

### InferenceAudioGenerator

```csharp
Task InitializeAsync(ModelAsset, PiperVoiceConfig, CancellationToken);
Task<float[]> GenerateAudioAsync(int[] phonemeIds, float lengthScale, float noiseScale, float noiseW, CancellationToken);
Task<float[]> GenerateAudioWithProsodyAsync(int[] phonemeIds, int[] a1, int[] a2, int[] a3, ...);
bool SupportsProsody { get; }
```

### MultilingualPhonemizer（Phase 3 追加、Phase 5 拡張）

```csharp
public class MultilingualPhonemizer : IDisposable
{
    // ── コンストラクタ（Phase 5: 全バックエンドをオプショナル引数で受け取り） ──
    MultilingualPhonemizer(
        IReadOnlyList<string> languages,
        string defaultLatinLanguage = "en",
        DotNetG2PPhonemizer jaPhonemizer = null,   // DI 用
        IPhonemizerBackend enPhonemizer = null,     // DI 用
        IPhonemizerBackend esPhonemizer = null,     // Phase 5 追加
        IPhonemizerBackend frPhonemizer = null,     // Phase 5 追加
        IPhonemizerBackend ptPhonemizer = null,     // Phase 5 追加
        IPhonemizerBackend zhPhonemizer = null,     // Phase 5 追加
        IPhonemizerBackend koPhonemizer = null);    // Phase 5 追加

    // ── 初期化 ──
    Task InitializeAsync(CancellationToken = default);

    // ── 音素化 ──
    Task<MultilingualPhonemizeResult> PhonemizeWithProsodyAsync(
        string text, CancellationToken = default);

    // ── プロパティ ──
    bool IsInitialized { get; }
    IReadOnlyList<string> Languages { get; }
}
```

### MultilingualPhonemizeResult

```csharp
public class MultilingualPhonemizeResult
{
    string[] Phonemes { get; set; }           // 音素配列（BOS/EOS なし）
    int[] ProsodyA1 { get; set; }             // Prosody A1（非日本語セグメントは 0）
    int[] ProsodyA2 { get; set; }             // Prosody A2
    int[] ProsodyA3 { get; set; }             // Prosody A3
    string DetectedPrimaryLanguage { get; set; } // 主言語コード
}
```

### LanguageConstants（Phase 5 追加）

```csharp
public static class LanguageConstants
{
    // ── 言語ID定数（ONNXモデルの lid 入力テンソル用） ──
    const int LanguageIdJapanese = 0;    // ja
    const int LanguageIdEnglish = 1;     // en
    const int LanguageIdChinese = 2;     // zh
    const int LanguageIdSpanish = 3;     // es
    const int LanguageIdFrench = 4;      // fr
    const int LanguageIdPortuguese = 5;  // pt
    const int LanguageIdKorean = 6;      // ko

    // ── 言語コード定数（ISO 639-1） ──
    const string CodeJapanese = "ja";
    const string CodeEnglish = "en";
    const string CodeChinese = "zh";
    const string CodeSpanish = "es";
    const string CodeFrench = "fr";
    const string CodePortuguese = "pt";
    const string CodeKorean = "ko";

    // ── グループ定数 ──
    static readonly string[] AllLanguages;    // 7言語全て
    static readonly string[] LatinLanguages;  // en, es, fr, pt
    static readonly string[] CjkLanguages;    // ja, zh, ko

    // ── メソッド ──
    static int GetLanguageId(string languageCode);
    static string GetLanguageCode(int languageId);
    static bool IsLatinLanguage(string languageCode);
    static bool IsCjkLanguage(string languageCode);
    static bool IsSupportedLanguage(string languageCode);
}
```

## Phonemizerバックエンド構成（Phase 5）

### 継承階層

```
IPhonemizerBackend（インターフェース）
  ├── PhonemizerBackendBase（抽象クラス）
  │   ├── SpanishPhonemizerBackend     ← Phase 5 追加
  │   ├── FrenchPhonemizerBackend      ← Phase 5 追加
  │   ├── PortuguesePhonemizerBackend  ← Phase 5 追加
  │   ├── ChinesePhonemizerBackend     ← Phase 5 追加
  │   └── KoreanPhonemizerBackend      ← Phase 5 追加
  ├── FlitePhonemizerBackend（英語 Flite LTS）
  └── RuleBasedPhonemizer（英語フォールバック）

DotNetG2PPhonemizer（日本語、独自実装、Prosody対応）
```

### バックエンドルーティング（GetBackendForLanguage）

```csharp
// MultilingualPhonemizer 内部
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
        _ => _enPhonemizer   // フォールバック: 英語
    };
}
// 注: "ja" は DotNetG2PPhonemizer で直接処理（IPhonemizerBackend 経由ではない）
```

## PiperConfig 言語設定

```csharp
[Header("Language Settings")]

/// <summary>デフォルト言語コード</summary>
public string DefaultLanguage = "ja";

/// <summary>言語自動判定を有効化</summary>
public bool AutoDetectLanguage = false;

/// <summary>サポート言語リスト</summary>
public List<string> SupportedLanguages = new() { "ja", "en" };

/// <summary>混合言語テキストの処理モード</summary>
public MultiLanguageMode MixedLanguageMode = MultiLanguageMode.SegmentByLanguage;
```

```csharp
public enum MultiLanguageMode
{
    SegmentByLanguage = 0,  // 言語別にセグメント分割
    ForceDefault = 1,       // 全体をDefaultLanguageで処理
    AutoDetectWhole = 2,    // 全体を自動判定した単一言語で処理
    VoiceConfigPrimary = 3  // VoiceConfig.Language優先
}
```

### PiperVoiceConfig 言語マッピング

```csharp
/// <summary>モデルがサポートする言語リスト（多言語モデル用）</summary>
public string[] SupportedLanguages = new[] { "ja" };

/// <summary>言語ID→言語コード マッピング</summary>
[HideInInspector]
public Dictionary<string, int> LanguageIdMap;
```

## 後方互換性

### 動作保証

| 呼び出し | 動作 |
|---------|------|
| `GenerateAudioAsync("こんにちは")` | CurrentVoice.Language → "ja" |
| `GenerateAudioAsync("hello", enVoice)` | enVoice.Language → "en" |
| `GenerateAudioAsync("text", language: "ja")` | 明示的に "ja" |
| `GenerateAudioAsync("text", language: "auto")` | UnicodeLanguageDetector で自動判定 |
| `GenerateAudioAsync("Hola", language: "es")` | Phase 5: スペイン語 |

### 言語解決順序

```
1. メソッドパラメータの language
2. CurrentVoice.Language
3. PiperConfig.DefaultLanguage
4. フォールバック: "ja"
```

### MultilingualPhonemizer 後方互換性

| 変更点 | Phase 3 | Phase 5 | 互換性 |
|--------|---------|---------|--------|
| コンストラクタ引数 | languages, defaultLatinLanguage, jaPhonemizer?, enPhonemizer? | +esPhonemizer?, frPhonemizer?, ptPhonemizer?, zhPhonemizer?, koPhonemizer? | 完全互換（全新パラメータ = null デフォルト） |
| InitializeAsync() | ja, en のみ | 7言語全て | 完全互換（ContainsLanguage チェックで不要な言語はスキップ） |
| PhonemizeWithProsodyAsync() | ja/en ルーティング | 7言語ルーティング | 完全互換 |
| Dispose() | ja, en 解放 | 7バックエンド解放 | 完全互換 |

## ユーザー向け使用例

### 基本（後方互換）
```csharp
var piper = new PiperTTS(config);
await piper.InitializeAsync();
var audio = await piper.GenerateAudioAsync("こんにちは");  // 従来通り
```

### 言語指定
```csharp
var audio = await piper.GenerateAudioAsync("Hello", language: "en");
```

### Phase 5: 新言語
```csharp
var audio = await piper.GenerateAudioAsync("Hola mundo", language: "es");
var audio = await piper.GenerateAudioAsync("Bonjour le monde", language: "fr");
var audio = await piper.GenerateAudioAsync("Olá mundo", language: "pt");
var audio = await piper.GenerateAudioAsync("你好世界", language: "zh");
var audio = await piper.GenerateAudioAsync("안녕하세요", language: "ko");
```

### 自動判定
```csharp
config.AutoDetectLanguage = true;
var audio = await piper.GenerateAudioAsync("こんにちは Hello", language: "auto");
```

### 混合言語
```csharp
config.MixedLanguageMode = MultiLanguageMode.SegmentByLanguage;
var audio = await piper.GenerateAudioAsync("今日はgoodですね", language: "mixed");
```

### ストリーミング
```csharp
await foreach (var chunk in piper.StreamAudioAsync("長いテキスト", language: "ja"))
{
    PlayAudioChunk(chunk);
}
```

### MultilingualPhonemizer 直接利用
```csharp
// 全7言語対応
var phonemizer = new MultilingualPhonemizer(
    languages: LanguageConstants.AllLanguages,
    defaultLatinLanguage: "en");
await phonemizer.InitializeAsync();

var result = await phonemizer.PhonemizeWithProsodyAsync("今日はbuenosです");
// result.DetectedPrimaryLanguage → "ja"
// result.Phonemes → 日本語+スペイン語セグメント結合済み
```

### テスト用 DI パターン
```csharp
// モックバックエンドを注入
var phonemizer = new MultilingualPhonemizer(
    languages: new[] { "ja", "en", "es" },
    jaPhonemizer: mockJa,
    enPhonemizer: mockEn,
    esPhonemizer: mockEs);
// InitializeAsync() はインジェクト済みバックエンドをスキップ
await phonemizer.InitializeAsync();
```

## デモUI拡張

InferenceEngineDemo に追加:
- 言語ドロップダウン（"自動", "日本語", "英語", "スペイン語", "フランス語", "ポルトガル語", "中国語", "韓国語", "混合"）
- 自動判定トグル
- 検出言語表示テキスト
