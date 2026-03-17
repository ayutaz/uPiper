# 多言語対応テスト戦略

## 既存テスト構造

### テスト統計
- テストファイル数: 45個
- テスト数: 455個
- カバレッジ基準: 50%ライン（`uPiper.Runtime`対象）

### 主要な既存テスト

| カテゴリ | ファイル | 内容 |
|---------|---------|------|
| 音素化(ja) | `DotNetG2PPhonemizerTest.cs` | 日本語G2P |
| 音素化(en) | `FliteLTSPhonemizerTests.cs` | 英語Flite LTS |
| 音素エンコード | `PhonemeEncoderTests.cs` | PUA/IPA両対応 |
| IPA | `PhonemeEncoderIPATests.cs` | IPAマッピング |
| eSpeak | `PhonemeEncoderESpeakTests.cs` | eSpeak PAD |
| Prosody | `ProsodyInferenceIntegrationTests.cs` | Prosody統合 |
| 言語検出 | `MixedLanguagePhonemizerTests.cs` | 混在言語 |
| 推論 | `InferenceAudioGeneratorTests.cs` | ONNX推論 |

## 追加が必要なテスト一覧

### 1. LanguageDetector テスト

**ファイル**: `Tests/Runtime/Core/Phonemizers/UnicodeLanguageDetectorTests.cs`

```
Script Detection:
  - Hiragana/Katakana → "ja"
  - CJK + kana context → "ja"
  - CJK only → "zh"
  - Hangul → "ko"
  - Latin → "en" (default)
  - Latin with accent (é, ñ) → ラテン言語

CJK Disambiguation:
  - "漢字とかな" → ja (kanaあり)
  - "漢字" → zh (kanaなし, ja+zh対応時)
  - "漢字" → ja (jaのみ対応時)

Segment Detection:
  - "今日はgoodですね" → [(ja,"今日は"),(en,"good"),(ja,"ですね")]
  - "Hello World" → [(en,"Hello World")]
  - "" → []
  - "123" → フォールバック言語

Edge Cases:
  - 全角ラテン (Ａ-Ｚ)
  - CJK句読点 (。、！)
  - 絵文字混在
```

### 2. PhonemeEncoder 多言語テスト

**ファイル**: `Tests/Runtime/AudioGeneration/PhonemeEncoderMultilingualTests.cs`

```
Multilingual PhonemeIdMap:
  - 173音素マップのロード
  - 各言語の音素がIDに変換されること
  - 共有音素が同一IDであること
  - 言語固有音素が異なるIDであること

IPA Detection:
  - 多言語モデル → _useIpaMapping = true
  - 日本語PUAモデル → _useIpaMapping = false

Encoding:
  - 日本語音素のエンコード
  - 英語音素のエンコード
  - BOS/EOS/PADトークンの正確な挿入
  - EOS-likeトークン(?!, ?., ?~)の処理
```

### 3. InferenceAudioGenerator 多言語テスト

**ファイル**: `Tests/Runtime/AudioGeneration/InferenceAudioGeneratorMultilingualTests.cs`

```
Model Capability Detection:
  - "lid" 入力あり → SupportsMultilingual = true
  - "sid" 入力あり → SupportsMultiSpeaker = true
  - 入力なし → false

Language ID Tensor:
  - lid=0 (ja) でのテンソル生成
  - lid=1 (en) でのテンソル生成
  - Dispose処理の確認

Backward Compatibility:
  - 単一言語モデルで lid なし → 正常動作
  - Prosody + lid の同時使用
```

### 4. MultilingualPhonemizer テスト

**ファイル**: `Tests/Runtime/Core/Phonemizers/MultilingualPhonemizerTests.cs`

```
Segmentation:
  - 日本語のみ → 1セグメント
  - 英語のみ → 1セグメント
  - 混合 → 複数セグメント

Phonemization:
  - セグメント別の正しい音素化
  - BOS/EOS除去と再付与
  - Prosody情報のマージ

PostProcessIds:
  - Inter-phoneme パディング
  - BOS/EOS ラッピング
  - _last_eos の正しい伝播
```

### 5. 統合テスト

**ファイル**: `Tests/Runtime/MultilingualTTSIntegrationTests.cs`

```
Full Pipeline:
  - "こんにちは" → 音素 → ID → 推論 → AudioClip
  - "hello" → 音素 → ID → 推論 → AudioClip
  - "こんにちは hello" → セグメント → 音素 → ID → 推論 → AudioClip

Error Handling:
  - 未サポート言語 → 例外
  - 空テキスト → 空AudioClip
  - null → ArgumentException

Backward Compatibility:
  - 既存API（言語パラメータなし）が動作すること
```

## テストデータ

### テスト文字列セット

```csharp
var testTexts = new Dictionary<string, string[]>
{
    ["ja"] = new[] { "こんにちは", "今日はいい天気ですね", "アルゴリズム" },
    ["en"] = new[] { "hello world", "how are you", "cooperation" },
    ["mixed"] = new[] { "今日はgoodですね", "これはtestです" },
    ["zh"] = new[] { "你好世界", "今天天气很好" },
    ["ko"] = new[] { "안녕하세요", "오늘 날씨가 좋습니다" },
    ["es"] = new[] { "hola mundo", "buenos días" },
    ["fr"] = new[] { "bonjour le monde", "comment allez-vous" },
    ["pt"] = new[] { "olá mundo", "bom dia" },
    ["edge"] = new[] { "", "   ", "123", "!?@#$%", "😊こんにちは😊" }
};
```

### テストモデル

- 既存: `ja_JP-test-medium.onnx`, `tsukuyomi-chan.onnx`
- 新規: 多言語テストモデル（小型版があれば理想）
- Mock: ダミーONNXモデル（構造確認用）

## CI/CDへの影響

### unity-tests.yml 変更

```yaml
# テストタイムアウト調整
testTimeout: 120  # 60→120秒

# カバレッジ対象追加
pathFilters:
  +**/Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/**
```

### 予測実行時間

| テストカテゴリ | 予測時間 |
|--------------|---------|
| LanguageDetector | ~30秒 |
| PhonemeEncoder多言語 | ~20秒 |
| MultilingualPhonemizer統合 | ~45秒 |
| InferenceAudioGenerator多言語 | ~60秒 |
| 統合テスト | ~30秒 |
| **合計追加分** | **~3分** |
