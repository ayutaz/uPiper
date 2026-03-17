# uPiper API/DX設計改善レポート - 2026年3月版

## 最優先改善

### 1. Fluent Builder API
```csharp
var tts = await PiperTTS.CreateBuilder()
    .WithLanguage("ja")
    .WithBackend(InferenceBackend.Auto)
    .WithVoice("ja_JP-test-medium")
    .BuildAsync();

var audio = await tts.Speak("こんにちは")
    .WithSpeed(1.2f)
    .GenerateAsync();
```
- **難易度**: 中 / **インパクト**: 高

### 2. エラーコード体系
```csharp
public enum PiperErrorCode
{
    InitializationFailed = 1000,
    PhonemizerInitializationFailed = 1001,
    ModelLoadFailed = 1002,
    AudioGenerationFailed = 2000,
    InvalidConfiguration = 3000,
}
```
- **難易度**: 低 / **インパクト**: 高

### 3. ScriptableObject設定
```csharp
[CreateAssetMenu(fileName = "PiperSettings", menuName = "uPiper/Settings")]
public class PiperSettingsAsset : ScriptableObject
{
    public PlatformSettings Windows;
    public PlatformSettings macOS;
    public PlatformSettings iOS;
    public PlatformSettings Android;
    public PlatformSettings WebGL;
}
```
- **難易度**: 低 / **インパクト**: 高

## エディタ統合

### カスタムインスペクタ（UI Toolkit）
- バックエンド選択 + プラットフォーム別推奨表示
- リアルタイムバリデーション
- **難易度**: 中 / **インパクト**: 中

### 音声プレビュー機能
- エディタ拡張でプレビュー再生
- **難易度**: 中 / **インパクト**: 高

## Observable統合（R3/UniRx）
- `Observable<GenerationProgress>` で進捗報告
- GenerationStage列挙型（LoadingModel, Phonemizing, Encoding, Synthesizing等）
- **難易度**: 低 / **インパクト**: 中

## ドキュメント自動生成
- DocFX統合でXMLコメントからAPI Reference自動生成
- GitHub Pagesへの自動デプロイ
- **難易度**: 低 / **インパクト**: 高

## Graceful Degradation（フォールバック戦略）
- 複数バックエンド（GPUPixel→CPU）の自動フォールバック
- ユーザーフレンドリーなエラーメッセージ + helpUrl
- **難易度**: 中 / **インパクト**: 高

## ロードマップ

### Phase 1: 基盤強化（1-2週間）
- エラーコード体系
- ScriptableObject Configuration
- API Reference自動生成

### Phase 2: DX向上（2-4週間）
- Fluent Builder API
- Graceful Degradation
- 音声プレビュー

### Phase 3: 高度機能（4-6週間）
- Observable統合（R3）
- カスタムインスペクタ
- プリセット管理

## Sources

- [Unity ScriptableObject architecture e-book](https://unity.com/resources/create-modular-game-architecture-scriptableobjects-unity-6)
- [Cysharp/R3 GitHub](https://github.com/Cysharp/R3)
- [Unity UIToolkit as inspector editor tool](https://prographers.com/blog/unity-uitoolkit-as-the-best-inspector-editor-tool)
