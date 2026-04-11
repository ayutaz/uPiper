# iOS セットアップガイド

## 前提条件

- Unity 6000.0 (Unity 6) 以降
- Xcode 15 以降
- iOS 11.0 以降のターゲットデバイス
- Apple Developer アカウント（実機テスト時）
- Sentis パッケージ（Unity.InferenceEngine）がインストール済み

## 推論バックエンド

iOS では **CPU バックエンド**のみがサポートされています。

| バックエンド | 対応状況 | 備考 |
|------------|---------|------|
| CPU | 対応 | 唯一の安定バックエンド |
| GPUPixel | 非対応 | Metal シェーダーコンパイル問題 |
| GPUCompute | 非対応 | Metal シェーダーコンパイル問題 |

`InferenceBackend.Auto` を指定した場合、`BackendSelector` が Metal デバイスを検出し、自動的に CPU バックエンドを選択します。GPU バックエンドを明示的に指定した場合も、警告ログとともに CPU に強制切替されます。

```csharp
// 推奨設定（Auto で自動的に CPU が選択される）
var config = new PiperConfig
{
    Backend = InferenceBackend.Auto
};
```

### Metal が使えない理由

Unity.InferenceEngine（Sentis）の Metal バックエンドには、VITS モデルのシェーダーコンパイルで既知の問題があります。`'metal_stdlib' file not found` エラーが発生し、GPU 推論で破損した音声が出力されます。この問題は Unity / Sentis 側の制限であり、uPiper 側では対応できません。

## セットアップ手順

### 1. ビルドターゲットの切り替え

1. **File > Build Settings** を開く
2. **iOS** を選択
3. **Switch Platform** をクリック

### 2. Player Settings の設定

**Edit > Project Settings > Player > iOS** で以下を設定します。

#### Other Settings

| 設定項目 | 推奨値 | 備考 |
|---------|--------|------|
| Scripting Backend | IL2CPP | iOS では必須 |
| Api Compatibility Level | .NET Standard 2.1 | |
| Target minimum iOS Version | 11.0 | |
| Architecture | ARM64 | 現行 iOS デバイスはすべて ARM64 |

#### Configuration

```
Scripting Backend: IL2CPP
Api Compatibility Level: .NET Standard 2.1
```

### 3. AVAudioSession の設定

uPiper は `PiperTTS.InitializeAsync()` 内で自動的に iOS の AVAudioSession を Playback カテゴリで初期化します。追加の手動設定は不要です。

```csharp
// InitializeAsync() 内で自動的に以下が実行される:
// IOSAudioSessionHelper.Initialize()
// → AVAudioSession を Playback カテゴリに設定
// → サイレントスイッチを無視して音声再生可能に
var tts = new PiperTTS(config);
await tts.InitializeAsync(); // iOS AudioSession 自動初期化
```

自動初期化が失敗した場合（警告ログが出力される）、手動で初期化できます:

```csharp
#if UNITY_IOS && !UNITY_EDITOR
uPiper.Core.Platform.IOSAudioSessionHelper.Initialize();
#endif
```

#### IOSAudioSessionHelper の機能

| メソッド | 説明 |
|---------|------|
| `Initialize()` | AVAudioSession を Playback カテゴリで初期化（サイレントスイッチ無視） |
| `EnsureActive()` | セッションがアクティブか確認し、必要に応じて再初期化 |
| `GetCategoryName()` | 現在の AudioSession カテゴリ名を取得 |
| `GetVolume()` | ハードウェアボリューム（0.0〜1.0）を取得 |
| `Deactivate()` | AudioSession を無効化（他アプリの音声再開） |
| `LogStatus()` | AudioSession 状態のデバッグログ出力 |

### 4. StreamingAssets の配置

辞書データは `StreamingAssets/uPiper/` 以下に配置します。iOS ビルドでは Unity が自動的にアプリバンドル内にコピーします。

```
StreamingAssets/
└── uPiper/
    ├── Dictionaries/        # カスタム辞書（JSON）
    ├── LanguageProfiles/    # Trigram言語プロファイル
    └── pua.json             # PUAマッピング
```

### 5. ビルドと実行

1. **File > Build Settings > Build** をクリック
2. Xcode プロジェクトが生成される
3. Xcode でプロジェクトを開く
4. 署名設定（Signing & Capabilities）でチームを選択
5. 実機またはシミュレータでビルド・実行

## コード例

```csharp
using uPiper.Core;

public class IOSTTSExample : MonoBehaviour
{
    private PiperTTS _tts;

    private async void Start()
    {
        var config = new PiperConfig
        {
            Backend = InferenceBackend.Auto  // iOS では自動的に CPU が選択される
        };

        _tts = new PiperTTS(config);
        await _tts.InitializeAsync();

        // 音声合成
        var result = await _tts.PhonemizeAsync("こんにちは");
        var request = SynthesisRequest.FromPhonemesWithProsody(
            result.Phonemes, result.ProsodyFlat);
        var clip = await _tts.SynthesizeAsync(request);

        var audioSource = GetComponent<AudioSource>();
        audioSource.clip = clip;
        audioSource.Play();
    }

    private void OnDestroy()
    {
        _tts?.Dispose();
    }
}
```

## パフォーマンス

iOS では CPU バックエンドのみ使用可能です。

| デバイス | 推論速度（目安） | 備考 |
|---------|----------------|------|
| iPhone 15 Pro (A17 Pro) | 高速 | Neural Engine は未使用（CPU のみ） |
| iPhone 13 (A15) | 良好 | |
| iPhone SE 3rd gen (A15) | 良好 | |
| iPad Air (M1) | 高速 | |

CPU バックエンドでも、multilingual-test-medium モデル（38MB, fp16）はリアルタイム音声合成に十分な速度で動作します。

## よくある問題

### 音声が再生されない（無音）

**原因**: AVAudioSession が正しく初期化されていない。

**解決策**:
1. `PiperTTS.InitializeAsync()` が正常に完了しているか確認
2. Xcode コンソールで `[IOSAudioSession]` ログを確認
3. 手動初期化を試す:

```csharp
#if UNITY_IOS && !UNITY_EDITOR
uPiper.Core.Platform.IOSAudioSessionHelper.LogStatus();
uPiper.Core.Platform.IOSAudioSessionHelper.Initialize();
#endif
```

### サイレントスイッチで音が消える

**原因**: AVAudioSession カテゴリが `Playback` に設定されていない。

**解決策**: `IOSAudioSessionHelper.Initialize()` が `Playback` カテゴリを設定します。カテゴリを確認:

```csharp
#if UNITY_IOS && !UNITY_EDITOR
var category = uPiper.Core.Platform.IOSAudioSessionHelper.GetCategoryName();
Debug.Log($"AudioSession Category: {category}"); // "AVAudioSessionCategoryPlayback" であること
#endif
```

### バックグラウンドで音声が停止する

**原因**: iOS はバックグラウンドでの音声再生にはバックグラウンドモードの設定が必要です。

**解決策**:
1. Xcode で **Signing & Capabilities** を開く
2. **+ Capability** で **Background Modes** を追加
3. **Audio, AirPlay, and Picture in Picture** にチェックを入れる

### 他のアプリの音声が停止する

**原因**: `Playback` カテゴリは他のアプリの音声を中断します。

**解決策**: TTS 使用後に AudioSession を無効化して他アプリの音声を復帰:

```csharp
#if UNITY_IOS && !UNITY_EDITOR
uPiper.Core.Platform.IOSAudioSessionHelper.Deactivate();
#endif
```

### Metal シェーダーエラー

**症状**: ログに `'metal_stdlib' file not found` や GPU 推論関連のエラーが表示される。

**解決策**: `InferenceBackend.Auto` を使用していれば自動的に CPU にフォールバックされます。手動で GPU バックエンドを指定している場合は `Auto` または `CPU` に変更してください。

### ビルドサイズが大きい

**解決策**:
- **Il2CPP Code Generation**: Faster (smaller) builds を選択
- 不要なアセットを除外
- multilingual-test-medium モデル（38MB）がアプリサイズに含まれることを考慮