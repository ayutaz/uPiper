# 公開API拡張設計

## 現在の公開API

### IPiperTTS インターフェース

```csharp
// 初期化
Task InitializeAsync(CancellationToken = default);
Task LoadVoiceAsync(PiperVoiceConfig voiceConfig, CancellationToken = default);
IReadOnlyList<PiperVoiceConfig> GetAvailableVoices();

// 音声生成
Task<AudioClip> GenerateAudioAsync(string text, CancellationToken = default);
Task<AudioClip> GenerateAudioAsync(string text, PiperVoiceConfig voiceConfig, CancellationToken = default);
IAsyncEnumerable<AudioChunk> StreamAudioAsync(string text, CancellationToken = default);

// プロパティ
PiperConfig Configuration { get; }
bool IsInitialized { get; }
PiperVoiceConfig CurrentVoice { get; }

// イベント
event Action<bool> OnInitialized;
event Action<PiperVoiceConfig> OnVoiceLoaded;
event Action<PiperException> OnError;
```

### InferenceAudioGenerator

```csharp
Task InitializeAsync(ModelAsset, PiperVoiceConfig, CancellationToken);
Task<float[]> GenerateAudioAsync(int[] phonemeIds, float lengthScale, float noiseScale, float noiseW, CancellationToken);
Task<float[]> GenerateAudioWithProsodyAsync(int[] phonemeIds, int[] a1, int[] a2, int[] a3, ...);
bool SupportsProsody { get; }
```

## 推奨API拡張

### 方針: 既存メソッドにオプショナル言語パラメータ追加

後方互換性を完全に保ちつつ、言語指定機能を追加。

### IPiperTTS 拡張

```csharp
public interface IPiperTTS : IDisposable
{
    // === 既存API（変更なし） ===
    Task InitializeAsync(CancellationToken = default);
    Task LoadVoiceAsync(PiperVoiceConfig voiceConfig, CancellationToken = default);
    Task<AudioClip> GenerateAudioAsync(string text, CancellationToken = default);
    Task<AudioClip> GenerateAudioAsync(string text, PiperVoiceConfig voiceConfig, CancellationToken = default);

    // === 新規: 言語指定版 ===

    /// <summary>
    /// 明示的な言語指定で音声生成
    /// </summary>
    /// <param name="language">"ja", "en", "zh", "es", "fr", "pt", "auto", "mixed"</param>
    Task<AudioClip> GenerateAudioAsync(
        string text,
        string language,
        CancellationToken cancellationToken = default);

    Task<AudioClip> GenerateAudioAsync(
        string text,
        PiperVoiceConfig voiceConfig,
        string language,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<AudioChunk> StreamAudioAsync(
        string text,
        string language,
        CancellationToken cancellationToken = default);

    // === 新規: 言語情報 ===

    /// <summary>テキストの言語を検出</summary>
    string DetectLanguage(string text);

    /// <summary>サポート言語一覧</summary>
    IReadOnlyList<string> GetSupportedLanguages();

    // === 新規: イベント ===
    event Action<string> OnLanguageDetected;
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

### InferenceAudioGenerator 拡張

```csharp
// 新規メソッド（既存メソッドは維持）
Task<float[]> GenerateAudioAsync(
    int[] phonemeIds,
    int languageId = 0,     // 新規
    int speakerId = 0,      // 新規
    float lengthScale = 1.0f,
    float noiseScale = 0.667f,
    float noiseW = 0.8f,
    CancellationToken cancellationToken = default);

Task<float[]> GenerateAudioWithProsodyAsync(
    int[] phonemeIds,
    int[] prosodyA1, int[] prosodyA2, int[] prosodyA3,
    int languageId = 0,     // 新規
    int speakerId = 0,      // 新規
    float lengthScale = 1.0f,
    float noiseScale = 0.667f,
    float noiseW = 0.8f,
    CancellationToken cancellationToken = default);

// 新規プロパティ
bool SupportsMultilingual { get; }
bool SupportsMultiSpeaker { get; }
```

### PiperConfig 拡張

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

### PiperVoiceConfig 拡張

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
| `GenerateAudioAsync("text", "ja")` | 明示的に "ja" |
| `GenerateAudioAsync("text", "auto")` | LanguageDetector で自動判定 |

### 言語解決順序

```
1. メソッドパラメータの language
2. CurrentVoice.Language
3. PiperConfig.DefaultLanguage
4. フォールバック: "ja"
```

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

## デモUI拡張

InferenceEngineDemo に追加:
- 言語ドロップダウン（"自動", "日本語", "英語", "混合"...）
- 自動判定トグル
- 検出言語表示テキスト
