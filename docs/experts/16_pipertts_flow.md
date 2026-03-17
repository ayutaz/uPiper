# PiperTTS 初期化・音声生成フロー

## 初期化シーケンス

```
InitializeAsync()
  ├── ValidateRuntimeEnvironment()
  ├── InitializeInferenceEngineAsync()
  │   └── Backend選択: Auto/CPU/GPUCompute/GPUPixel
  ├── InitializeCacheSystem()
  ├── InitializePhonemizerAsync()       ← 変更対象
  │   ├── Language == "ja"/"jp"/"japanese"
  │   │   ├── WebGL → DotNetG2PPhonemizer.InitializeAsync()
  │   │   └── 非WebGL → DotNetG2PPhonemizer() 同期
  │   └── その他 → _phonemizer = null   ← 問題箇所
  └── _isInitialized = true
```

## 音声生成フロー

```
GenerateAudioAsync(text)
  ├── PhonemizeAsync(text)
  │   ├── キャッシュチェック
  │   ├── TextNormalizer.Normalize()
  │   └── DotNetG2PPhonemizer.PhonemizeInternalAsync()
  │       ├── CustomDictionary.ApplyToText()
  │       ├── G2PEngine.ToPhonemes() / ToProsodyFeatures()
  │       ├── OpenJTalkToPiperMapping変換
  │       ├── N音素コンテキスト依存処理
  │       └── 疑問形判定
  ├── Prosody対応判定
  │   ├── _inferenceGenerator.SupportsProsody
  │   └── _phonemizer is DotNetG2PPhonemizer
  ├── PhonemeEncoder.Encode[WithProsody]()
  └── InferenceAudioGenerator.GenerateAudio[WithProsody]Async()
      └── ExecuteInference() → AudioClip
```

## 変更が必要な箇所

### 1. InitializePhonemizerAsync()（行1050-1086）

**現在**: 日本語のみハードコード

```csharp
if (_config.DefaultLanguage == "ja" || ...)
    _phonemizer = new DotNetG2PPhonemizer();
else
    _phonemizer = null;  // 他言語未対応
```

**改修案**: PhonemizerFactory パターン

```csharp
private async Task InitializePhonemizerAsync()
{
    _phonemizer = PhonemizerFactory.Create(_config.DefaultLanguage);
    if (_phonemizer != null)
        await _phonemizer.InitializeAsync();

    // 多言語モデルの場合、MultilingualPhonemizer を使用
    if (_config.AutoDetectLanguage || _config.MixedLanguageMode == MultiLanguageMode.SegmentByLanguage)
    {
        _multilingualPhonemizer = new MultilingualPhonemizer(
            _config.SupportedLanguages,
            _config.DefaultLanguage);
    }
}
```

### 2. GenerateAudioAsync()（行478, 521, 862）

**現在**: 言語パラメータなし

**改修案**: 言語指定オーバーロード追加

```csharp
public async Task<AudioClip> GenerateAudioAsync(
    string text,
    string language = null,  // null → CurrentVoice.Language → DefaultLanguage
    CancellationToken cancellationToken = default)
{
    var effectiveLanguage = language
        ?? CurrentVoice?.Language
        ?? _config.DefaultLanguage;

    // 言語に応じた音素化
    var phonemeResult = await PhonemizeByLanguage(text, effectiveLanguage);

    // 言語IDの取得
    var languageId = GetLanguageId(effectiveLanguage);

    // 推論
    return await GenerateAudioFromPhonemes(phonemeResult, languageId);
}
```

### 3. PiperConfig 拡張

```csharp
[Header("Language Settings")]
public string DefaultLanguage = "ja";
public bool AutoDetectLanguage = false;
public List<string> SupportedLanguages = new() { "ja", "en" };
public MultiLanguageMode MixedLanguageMode = MultiLanguageMode.SegmentByLanguage;
```

### 4. SplitIntoSentences()（行1195-1232）

**現在**: 英語・日本語の簡易分文

**改修案**: 言語パラメータ追加

### 5. VoiceConfig 言語マッピング

`model.onnx.json` から言語情報を読み取り:
- `num_languages`: 対応言語数
- `language_id_map`: 言語コード→ID マッピング

```json
{
  "num_languages": 6,
  "language_id_map": {
    "ja": 0, "en": 1, "zh": 2, "es": 3, "fr": 4, "pt": 5
  }
}
```

## 後方互換性戦略

| 変更対象 | 既存動作 | 新動作 | 互換性 |
|---------|---------|--------|--------|
| `GenerateAudioAsync(text)` | DefaultLanguage使用 | Voice言語優先 | 完全互換 |
| `InitializeAsync()` | 日本語初期化 | 複数言語対応 | フォールバック |
| `PiperVoiceConfig.Language` | 既存フィールド | 言語キー使用 | 既に使用可能 |

**マイグレーションパス**:
```csharp
// 旧コード（そのまま動作）
var audio = await piper.GenerateAudioAsync("こんにちは");

// 新コード（明示的言語指定）
var audio = await piper.GenerateAudioAsync("こんにちは", language: "ja");

// 新コード（自動判定）
var audio = await piper.GenerateAudioAsync("Hello こんにちは", language: "auto");
```
