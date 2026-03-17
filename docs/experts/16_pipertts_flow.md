# PiperTTS 初期化・音声生成フロー

> **Phase 5 完了** (2026-03-18): MultilingualPhonemizer が7言語（ja, en, es, fr, pt, zh, ko）に対応。

## 初期化シーケンス

```
InitializeAsync()
  ├── ValidateRuntimeEnvironment()
  ├── InitializeInferenceEngineAsync()
  │   └── Backend選択: Auto/CPU/GPUCompute/GPUPixel
  ├── InitializeCacheSystem()
  ├── InitializePhonemizerAsync()
  │   ├── DefaultLanguage == "ja"/"jp"/"japanese"
  │   │   ├── WebGL → DotNetG2PPhonemizer.InitializeAsync()
  │   │   └── 非WebGL → DotNetG2PPhonemizer() 同期
  │   └── その他 → _phonemizer = null（フォールバック）
  │
  │   ※ MultilingualPhonemizer は GenerateAudioAsync(text, language) 経由で
  │     必要時に PiperTTS 内部で生成・初期化される
  └── _isInitialized = true
```

### MultilingualPhonemizer 初期化詳細

```
MultilingualPhonemizer(languages, defaultLatinLanguage,
                       jaPhonemizer?, enPhonemizer?,
                       esPhonemizer?, frPhonemizer?,
                       ptPhonemizer?, zhPhonemizer?,
                       koPhonemizer?)
  │  ※ コンストラクタは全バックエンドをオプショナル引数で受け取り（DI対応）
  │  ※ null の場合は InitializeAsync() で自動生成
  │
  └── InitializeAsync()
      ├── "ja" → DotNetG2PPhonemizer()
      │   ├── WebGL → InitializeAsync() (非同期)
      │   └── 非WebGL → コンストラクタで同期初期化
      ├── "en" → FlitePhonemizerBackend (Flite LTS)
      │   └── フォールバック → RuleBasedPhonemizer
      ├── "es" → SpanishPhonemizerBackend
      ├── "fr" → FrenchPhonemizerBackend
      ├── "pt" → PortuguesePhonemizerBackend
      ├── "zh" → ChinesePhonemizerBackend
      ├── "ko" → KoreanPhonemizerBackend
      └── _isInitialized = true
```

**バックエンド一覧（Phase 5）**:

| 言語 | コード | バックエンド | ベースクラス |
|------|--------|-------------|-------------|
| 日本語 | ja | DotNetG2PPhonemizer | 独自実装（Prosody対応） |
| 英語 | en | FlitePhonemizerBackend | IPhonemizerBackend |
| スペイン語 | es | SpanishPhonemizerBackend | PhonemizerBackendBase |
| フランス語 | fr | FrenchPhonemizerBackend | PhonemizerBackendBase |
| ポルトガル語 | pt | PortuguesePhonemizerBackend | PhonemizerBackendBase |
| 中国語 | zh | ChinesePhonemizerBackend | PhonemizerBackendBase |
| 韓国語 | ko | KoreanPhonemizerBackend | PhonemizerBackendBase |

## 音声生成フロー

### 基本フロー（言語指定なし）

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

### 言語指定フロー（Phase 4/5）

```
GenerateAudioAsync(text, language)
  ├── 言語解決
  │   ├── language == "auto" || null → DetectLanguage(text)
  │   │   └── UnicodeLanguageDetector.SegmentText() → 文字数加重で主言語決定
  │   └── 明示的言語コード → そのまま使用
  ├── LanguageIdMap から languageId 取得
  └── GenerateAudioWithMultilingualAsync(text, languageId)
      └── 内部で MultilingualPhonemizer.PhonemizeWithProsodyAsync() を使用
```

### 多言語音素化フロー（MultilingualPhonemizer）

```
PhonemizeWithProsodyAsync(text)
  ├── UnicodeLanguageDetector.SegmentText(text)
  │   └── テキストを言語別セグメントに分割
  │       例: "今日はgoodですね" → [("ja","今日は"), ("en","good"), ("ja","ですね")]
  ├── 主言語判定（文字数加重）
  ├── セグメントごとに処理:
  │   ├── lang == "ja" → DotNetG2PPhonemizer.PhonemizeWithProsody()
  │   │   └── Prosody情報（A1, A2, A3）付き音素を返す
  │   └── その他 → GetBackendForLanguage(lang)
  │       ├── "en" → _enPhonemizer.PhonemizeAsync()
  │       ├── "es" → _esPhonemizer.PhonemizeAsync()
  │       ├── "fr" → _frPhonemizer.PhonemizeAsync()
  │       ├── "pt" → _ptPhonemizer.PhonemizeAsync()
  │       ├── "zh" → _zhPhonemizer.PhonemizeAsync()
  │       ├── "ko" → _koPhonemizer.PhonemizeAsync()
  │       └── 不明 → _enPhonemizer（英語フォールバック）
  ├── 中間セグメントのEOS除去（"$", "?", "?!" 等）
  ├── Prosody配列のパディング（非日本語セグメントは 0 埋め）
  └── MultilingualPhonemizeResult
      ├── Phonemes: 全セグメント結合済み音素配列
      ├── ProsodyA1, ProsodyA2, ProsodyA3: Prosody値配列
      └── DetectedPrimaryLanguage: 主言語コード
```

## 言語コード・ID マッピング（LanguageConstants）

`LanguageConstants` クラスが言語コードと ONNX モデルの `lid` 入力テンソル用 ID の双方向マッピングを提供。

| 言語 | コード | ID | グループ |
|------|--------|----|---------|
| 日本語 | ja | 0 | CJK |
| 英語 | en | 1 | Latin |
| 中国語 | zh | 2 | CJK |
| スペイン語 | es | 3 | Latin |
| フランス語 | fr | 4 | Latin |
| ポルトガル語 | pt | 5 | Latin |
| 韓国語 | ko | 6 | CJK |

```csharp
// コード → ID
int id = LanguageConstants.GetLanguageId("ja");  // → 0

// ID → コード
string code = LanguageConstants.GetLanguageCode(0);  // → "ja"

// グループ判定
LanguageConstants.IsLatinLanguage("es");  // → true (en, es, fr, pt)
LanguageConstants.IsCjkLanguage("ko");    // → true (ja, zh, ko)
LanguageConstants.IsSupportedLanguage("de");  // → false
```

### VoiceConfig 言語マッピング

`model.onnx.json` から言語情報を読み取り:
- `num_languages`: 対応言語数
- `language_id_map`: 言語コード→ID マッピング

```json
{
  "num_languages": 7,
  "language_id_map": {
    "ja": 0, "en": 1, "zh": 2, "es": 3, "fr": 4, "pt": 5, "ko": 6
  }
}
```

## 後方互換性戦略

| 変更対象 | 既存動作 | 新動作 | 互換性 |
|---------|---------|--------|--------|
| `GenerateAudioAsync(text)` | DefaultLanguage使用 | Voice言語優先 | 完全互換 |
| `GenerateAudioAsync(text, language)` | Phase 4 追加 | 7言語対応 | 完全互換 |
| `InitializeAsync()` | 日本語初期化 | 複数言語対応 | フォールバック |
| `PiperVoiceConfig.Language` | 既存フィールド | 言語キー使用 | 既に使用可能 |
| `MultilingualPhonemizer()` | Phase 3: ja, en | Phase 5: +es,fr,pt,zh,ko | 完全互換（新パラメータはすべてオプショナル） |

**マイグレーションパス**:
```csharp
// 旧コード（そのまま動作）
var audio = await piper.GenerateAudioAsync("こんにちは");

// 新コード（明示的言語指定）
var audio = await piper.GenerateAudioAsync("こんにちは", language: "ja");

// 新コード（自動判定）
var audio = await piper.GenerateAudioAsync("Hello こんにちは", language: "auto");

// Phase 5: 新言語
var audio = await piper.GenerateAudioAsync("Hola mundo", language: "es");
var audio = await piper.GenerateAudioAsync("Bonjour le monde", language: "fr");
```

### MultilingualPhonemizer DI パターン

```csharp
// デフォルト: 全バックエンドを自動生成
var phonemizer = new MultilingualPhonemizer(
    languages: new[] { "ja", "en", "es", "fr", "pt", "zh", "ko" },
    defaultLatinLanguage: "en");
await phonemizer.InitializeAsync();

// テスト/カスタム: 事前構築済みバックエンドを注入
var phonemizer = new MultilingualPhonemizer(
    languages: new[] { "ja", "en" },
    defaultLatinLanguage: "en",
    jaPhonemizer: mockJaPhonemizer,
    enPhonemizer: mockEnPhonemizer);
```
