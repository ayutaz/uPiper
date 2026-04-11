# uPiper Tests

uPiper の品質を担保するテストスイートです。EditMode テストと PlayMode テストで構成されています。

## テスト構成

### Editor Tests（EditMode）

Unity の EditMode で実行されるテストです。モック/スタブを使用し、実際のONNXモデルなしで動作します。

| カテゴリ | 主要テストファイル | 内容 |
|---------|-----------------|------|
| **コアAPI** | `CreateAsyncTests`, `PiperTTSLifecycleTests`, `PiperTTSSynthesizeTests` | PiperTTS の生成・初期化・合成パイプライン |
| **ライフサイクル** | `AsyncLifecycleTests`, `CancellationTests` | 非同期ライフサイクル管理・キャンセル処理 |
| **音声生成** | `TTSSynthesisOrchestratorTests`, `SplitInferenceOrchestratorTests`, `ShortTextProcessorTests`, `InterspersePaddingTests` | 合成オーケストレーション・短テキスト緩和・PAD挿入 |
| **音素エンコーディング** | `PhonemeEncoderTests`, `PuaTokenMapperTests`, `PuaTokenMapperJsonTests`, `PuaJsonCrossValidationTests` | 音素→ID変換・PUAマッピング・pua.json検証 |
| **音素化** | `MultilingualPhonemizerTests`, `MultilingualPhonemizerDeepTests`, `MultilingualPhonemizerEosTests`, `ChinesePhonemizerTests`, `CrossLanguagePhonemeTests` | 多言語音素化・言語別ハンドラ |
| **言語検出** | `UnicodeLanguageDetectorTests`, `HybridLanguageDetectorTests`, `TrigramLanguageDetectorTests`, `MultilingualPhonemizerTrigramTests` | Unicode/Trigram/Hybrid言語検出 |
| **G2Pハンドラ** | `HandlerCommonTests`, `HandlerDispatchTests`, `G2PHandlerUtilsTests` | ILanguageG2PHandler共通テスト・ルーティング |
| **辞書** | `CustomDictionarySecurityTests`, `CustomDictionaryDetailTests`, `DictionaryJsonEditorTests` | カスタム辞書セキュリティ・JSON編集 |
| **設定・初期化** | `InitializationValidationTests`, `PiperConfigAssetTests`, `PlatformInitLoggingTests` | バリデーション・設定アセット・プラットフォームログ |
| **バックエンド選択** | `BackendSelectorTests` | 推論バックエンド自動選択ロジック |
| **その他** | `SynthesisRequestTests`, `PhonemizeResultTests`, `AudioSynthesisCacheTests`, `UnsupportedLanguageEventTests` | データ型・キャッシュ・イベント |

### Runtime Tests（PlayMode）

Unity の PlayMode で実行されるテスト、および `includePlatforms: ["Editor"]` により EditMode で実行されるランタイムテストです。

| カテゴリ | 主要テストファイル | 内容 |
|---------|-----------------|------|
| **音声生成** | `PhonemeEncoderTests`, `PhonemeEncoderIPATests`, `PhonemeEncoderMultilingualTests`, `PhonemeEncoderESpeakTests` | 各モデルタイプの音素エンコーディング |
| **AudioClip** | `AudioClipBuilderTests`, `AudioNormalizerTests` | AudioClip構築・音声正規化 |
| **推論** | `InferenceAudioGeneratorTests`, `InferenceAudioGeneratorMultilingualTests`, `ProsodyInferenceIntegrationTests` | ONNX推論・Prosody統合 |
| **コア** | `PiperConfigTest`, `PiperVoiceConfigTest`, `PiperTTSSimpleTest`, `PiperTTSFunctionTest` | 設定・ボイス設定・基本機能 |
| **プラットフォーム** | `BackendSelectorTests`, `IOSPlatformTest`, `AndroidIntegrationTest` | プラットフォーム別動作 |
| **パイプライン** | `MultilingualModelPipelineTests`, `MultilingualPipelineTests` | 多言語パイプライン統合 |
| **パフォーマンス** | `IL2CPPPerformanceTest` | IL2CPP環境パフォーマンス |

### テストヘルパー

| ファイル | 内容 |
|---------|------|
| `Editor/TestHelpers/TestPhonemeIdMapFactory` | テスト用PhonemeIdMap生成 |
| `Editor/TestHelpers/TestVoiceConfigFactory` | テスト用VoiceConfig生成 |
| `Editor/AudioGeneration/StubInferenceAudioGenerator` | IInferenceAudioGeneratorスタブ |
| `Editor/Phonemizers/Handlers/StubG2PHandler` | ILanguageG2PHandlerスタブ |
| `Runtime/Helpers/SyncTestHelpers` | 同期テストユーティリティ |

## テスト実行方法

### Unity Test Runner（推奨）

1. Unity Editor で `Window > General > Test Runner` を開く
2. **EditMode** または **PlayMode** タブを選択
3. `Run All` でテストを実行

### GitHub Actions

CI では以下のワークフローで自動実行されます：

- `.github/workflows/unity-tests.yml` — EditMode + PlayMode テスト

### コマンドライン

```bash
# フォーマットチェック
dotnet format --verify-no-changes
```

## 注意事項

- `uPiper.Tests.Runtime.asmdef` は `includePlatforms: ["Editor"]` のため、実質 EditMode で実行されます
- 多くのテストはモック/スタブを使用し、実際のONNXモデルなしで動作します
- 統合テスト（`InferenceAudioGeneratorTests` 等）は実際のモデルファイルが必要な場合があります
- GPU が利用できない環境では CPU バックエンドが使用されます