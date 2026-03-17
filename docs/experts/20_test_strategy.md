# 多言語対応テスト戦略

## テスト構造

### テスト統計
- テストファイル数: 51個（既存45 + Phase 5で6個追加）
- テスト数: 685個（既存455 + Phase 5で230個追加）
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

### Phase 5 追加テスト（完了）

全テストは `Tests/Editor/Phonemizers/` ディレクトリに配置。EditModeテスト（純粋C#音素化バックエンドのためPlayModeテスト不要）。NUnitパターンでOneTimeSetUp/OneTimeTearDownを使用。

| ファイル | テスト数 | カバレッジ領域 |
|---------|---------|--------------|
| `SpanishPhonemizerTests.cs` | 36 | 基本語、ストレス検出、二重字（ch/ll/rr）、異音規則（intervocalic b/d/g）、音節化、句読点、seseo、Prosody出力、バックエンドプロパティ |
| `FrenchPhonemizerTests.cs` | 38 | 基本語、鼻母音（an/in/on）、無声子音、二重字（ch/gn/ou/eau/oi）、e muet、-er語尾例外、-ille語例外、intervocalic s有声化、uvular r、Prosody出力 |
| `PortuguesePhonemizerTests.cs` | 41 | 基本語、鼻母音（チルダ/n前/語末m）、子音クラスタ（nh/lh/ch/rr）、後処理4規則（coda-l母音化、t/d口蓋化、語末母音弱化e→i/o→u）、ストレス検出、cedilla、r分布 |
| `ChinesePhonemizerTests.cs` | 38 | プロパティ・メタデータ、言語サポート、ピンイン→IPA変換（声母・韻母）、声調マーカー、第三声声調変化、中国語句読点、Prosody出力（A1=声調値）、PUAトークンマッピング、Dispose |
| `KoreanPhonemizerTests.cs` | 48 | プロパティ・メタデータ、ハングル分解（初声/中声/終声）、初声IPA（平音/激音/濃音）、中声IPA（単母音/二重母音）、終声IPA（内破音/鼻音/流音）、音韻規則4種（連音化/鼻音化/激音化/濃音化）、非ハングル処理、Prosody出力（A1=A2=0, A3=音節数）、PUAマッピング、NFC正規化 |
| `MultilingualPhonemizerPhase5Tests.cs` | 29 | コンストラクタ（zh/ko/全7言語/プレビルドバックエンド）、言語定数、セグメント処理（es/zh/ko/fr/pt）、混合言語テキスト（CJK曖昧性解消/韓英/日西）、UnicodeDetector新言語対応、全言語初期化、Prosody伝播、Dispose、エラーハンドリング |

## 既存テスト計画（未実装）

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
  +**/Assets/uPiper/Runtime/Core/Phonemizers/Backend/Spanish/**
  +**/Assets/uPiper/Runtime/Core/Phonemizers/Backend/French/**
  +**/Assets/uPiper/Runtime/Core/Phonemizers/Backend/Portuguese/**
  +**/Assets/uPiper/Runtime/Core/Phonemizers/Backend/Chinese/**
  +**/Assets/uPiper/Runtime/Core/Phonemizers/Backend/Korean/**
```

### 予測実行時間

| テストカテゴリ | テスト数 | 予測時間 | 状態 |
|--------------|---------|---------|------|
| SpanishPhonemizerTests | 36 | ~20秒 | 完了 |
| FrenchPhonemizerTests | 38 | ~20秒 | 完了 |
| PortuguesePhonemizerTests | 41 | ~20秒 | 完了 |
| ChinesePhonemizerTests | 38 | ~25秒 | 完了 |
| KoreanPhonemizerTests | 48 | ~25秒 | 完了 |
| MultilingualPhonemizerPhase5Tests | 29 | ~30秒 | 完了 |
| LanguageDetector | - | ~30秒 | 未実装 |
| PhonemeEncoder多言語 | - | ~20秒 | 未実装 |
| MultilingualPhonemizer統合 | - | ~45秒 | 未実装 |
| InferenceAudioGenerator多言語 | - | ~60秒 | 未実装 |
| 統合テスト | - | ~30秒 | 未実装 |
| **Phase 5 合計** | **230** | **~2.5分** | **完了** |
