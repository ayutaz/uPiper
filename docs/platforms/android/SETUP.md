# Android セットアップガイド

## 前提条件

- Unity 2022.3 LTS 以降
- Android SDK / NDK（Unity Hub 経由で自動インストール推奨）
- Android 7.0 (API 24) 以降のターゲットデバイス
- Sentis パッケージ（Unity.InferenceEngine）がインストール済み

## 推論バックエンド

Android では ComputeShader のサポート状況に応じてバックエンドが自動選択されます。

| 条件 | 自動選択バックエンド | 備考 |
|------|-------------------|------|
| ComputeShader 対応デバイス | GPUPixel | VITS モデルとの互換性が良好 |
| ComputeShader 非対応デバイス | CPU | ローエンドデバイス向けフォールバック |

```csharp
// 推奨: Auto で自動選択
var config = new PiperConfig
{
    Backend = InferenceBackend.Auto
};
```

> GPUCompute を明示的に指定した場合、VITSモデルとの互換性問題のため GPUPixel に自動切替されます。

## セットアップ手順

### 1. ビルドターゲットの切り替え

1. **File > Build Settings** を開く
2. **Android** を選択
3. **Switch Platform** をクリック

### 2. Player Settings の設定

**Edit > Project Settings > Player > Android** で以下を設定します。

#### Other Settings

| 設定項目 | 推奨値 | 備考 |
|---------|--------|------|
| Scripting Backend | IL2CPP | パフォーマンスと互換性のため必須 |
| Api Compatibility Level | .NET Standard 2.1 | |
| Target Architectures | ARM64 | 64-bit 必須。ARMv7 は任意 |
| Minimum API Level | Android 7.0 (API 24) | |
| Target API Level | 最新の安定版 | Google Play 要件に準拠 |

#### Configuration

```
Scripting Backend: IL2CPP
Api Compatibility Level: .NET Standard 2.1
Target Architectures:
  - ARM64 (必須)
  - ARMv7 (任意、古いデバイスサポート時)
```

### 3. AndroidManifest.xml

uPiper は `Assets/uPiper/Plugins/Android/AndroidManifest.xml` にカスタムマニフェストを含んでいます。Unity のビルドプロセスで自動的にマージされます。

含まれるパーミッション:
- `WRITE_EXTERNAL_STORAGE`（API 28 以下のみ、辞書ファイル展開用）
- API 29 以降は Scoped Storage を使用するためパーミッション不要

追加のパーミッションは不要です。

### 4. StreamingAssets の取り扱い

Android では StreamingAssets は APK 内に `jar:file://` プロトコルで格納されるため、通常のファイル I/O（`File.ReadAllBytes` 等）でアクセスできません。uPiper は内部で `WebGLStreamingAssetsLoader` を使用して `UnityWebRequest` 経由の非同期読み込みを自動的に行います。

```csharp
// uPiper 内部で自動的に UnityWebRequest 経由で読み込まれるため、
// ユーザーコードでの特別な対応は不要
var tts = new PiperTTS(config);
await tts.InitializeAsync(); // 辞書データは自動的に APK 内から読み込まれる
```

StreamingAssets のディレクトリ構造:

```
StreamingAssets/
└── uPiper/
    ├── Dictionaries/        # カスタム辞書（JSON）
    │   ├── additional_tech_dict.json
    │   ├── default_common_dict.json
    │   ├── default_tech_dict.json
    │   └── user_custom_dict.json
    ├── LanguageProfiles/    # Trigram言語プロファイル
    │   └── trigram_profiles.json
    └── pua.json             # PUAマッピング
```

### 5. Sentis プラグイン

Android 向けの Sentis プラグインは Unity パッケージに含まれており、追加の設定は不要です。

### 6. ビルドと実行

#### APK ビルド

1. **File > Build Settings** を開く
2. **Build** をクリック
3. 出力先を指定して APK を生成

#### 実機でのテスト

```bash
# ADB 経由でインストール
adb install -r your_app.apk

# ログの確認
adb logcat -s Unity

# uPiper 関連のログのみフィルタリング
adb logcat -s Unity | grep -E "\[Piper|\[BackendSelector|\[PhonemeEncoder"
```

## コード例

```csharp
using uPiper.Core;
using UnityEngine;

public class AndroidTTSExample : MonoBehaviour
{
    private PiperTTS _tts;

    private async void Start()
    {
        var config = new PiperConfig
        {
            Backend = InferenceBackend.Auto
        };

        _tts = new PiperTTS(config);
        await _tts.InitializeAsync();

        // 多言語音声合成
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

| デバイス種別 | バックエンド | 推論速度（目安） |
|------------|------------|----------------|
| ハイエンド（Snapdragon 8 Gen 2 等） | GPUPixel | 高速 |
| ミドルレンジ（Snapdragon 7 Gen 1 等） | GPUPixel | 良好 |
| ローエンド（ComputeShader 非対応） | CPU | やや遅い |

multilingual-test-medium モデル（38MB, fp16）は、ミドルレンジ以上のデバイスでリアルタイム音声合成に十分な速度で動作します。

### メモリに関する注意

モバイルデバイスはメモリが限られているため、以下を考慮してください:

```csharp
var config = new PiperConfig
{
    Backend = InferenceBackend.Auto,
    GPUSettings = new GPUInferenceSettings
    {
        MaxMemoryMB = 256  // モバイル向けにメモリ制限を下げる
    }
};
```

## CI/CD 用の設定

### コマンドラインビルド

```bash
Unity -batchmode -quit \
  -projectPath . \
  -buildTarget Android \
  -executeMethod BuildScript.BuildAndroid
```

### ビルドスクリプト例

```csharp
public static void BuildAndroid()
{
    var buildPlayerOptions = new BuildPlayerOptions
    {
        scenes = new[] { "Assets/Scenes/SampleScene.unity" },
        locationPathName = "Build/Android/uPiper.apk",
        target = BuildTarget.Android,
        options = BuildOptions.None
    };

    var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
    if (report.summary.result == BuildResult.Succeeded)
    {
        Debug.Log("ビルド成功: " + report.summary.totalSize + " バイト");
    }
    else
    {
        Debug.Log("ビルド失敗");
        EditorApplication.Exit(1);
    }
}
```

## よくある問題

### 音素化が動作しない

**原因**: MeCab 辞書が StreamingAssets に含まれていない、または読み込みに失敗している。

**解決策**:
1. `adb logcat -s Unity` でエラーログを確認
2. `StreamingAssets/uPiper/Dictionaries/` にファイルが存在するか確認
3. Scripting Backend が `IL2CPP` であることを確認

### APK 内の StreamingAssets にアクセスできない

**原因**: `File.ReadAllBytes` など同期ファイル I/O を使用している。

**解決策**: uPiper は内部で `UnityWebRequest` 経由の非同期読み込みを自動的に使用します。カスタムコードで StreamingAssets にアクセスする場合は `WebGLStreamingAssetsLoader` を使用してください:

```csharp
var data = await WebGLStreamingAssetsLoader.LoadBytesAsync(
    "uPiper/your_custom_file.json");
```

### GPU バックエンドで音声が破損する

**原因**: デバイスの GPU ドライバとの互換性問題。

**解決策**:
```csharp
// CPU バックエンドに明示的に切り替え
config.Backend = InferenceBackend.CPU;

// または CPU フォールバックを有効化
config.AllowFallbackToCPU = true;
```

### ビルドサイズが大きい

**解決策**:
- IL2CPP Code Generation: Faster (smaller) builds を選択
- 不要なアーキテクチャを除外（ARM64 のみで十分な場合が多い）
- Managed Stripping Level を High に設定
- multilingual-test-medium モデル（38MB）がアプリサイズに含まれることを考慮

### ProGuard / R8 による問題

**原因**: コード縮小ツールが必要なクラスを除去。

**解決策**: `proguard-user.txt` に uPiper の除外ルールを追加:
```
-keep class uPiper.** { *; }
```