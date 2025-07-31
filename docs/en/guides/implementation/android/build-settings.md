# Android ビルド設定ガイド

## Unity Android ビルド設定

### 1. Build Settings

1. File → Build Settings を開く
2. Platform: Android を選択
3. Switch Platform をクリック

### 2. Player Settings

Edit → Project Settings → Player → Android settings

#### Other Settings
- **Configuration**
  - Scripting Backend: `IL2CPP`
  - Api Compatibility Level: `.NET Standard 2.1`
  - Target Architectures: 
    - ✅ ARMv7
    - ✅ ARM64
    - ✅ x86 (Chrome OS and the emulator)

#### Publishing Settings
- **Build**
  - Custom Main Gradle Template: ✅ (必要に応じて)
  - Custom Gradle Properties Template: ✅ (必要に応じて)

### 3. Android SDK/NDK 設定

Edit → Preferences → External Tools

- Android SDK: Unityが自動的に設定
- Android NDK: Unityが自動的に設定
- JDK: Unityが自動的に設定

## ネイティブライブラリの配置

すべてのAndroidネイティブライブラリは以下の構造で配置されています：

```
Assets/
└── uPiper/
    └── Plugins/
        └── Android/
            └── libs/
                ├── arm64-v8a/
                │   └── libopenjtalk_wrapper.so
                ├── armeabi-v7a/
                │   └── libopenjtalk_wrapper.so
                ├── x86/
                │   └── libopenjtalk_wrapper.so
                └── x86_64/
                    └── libopenjtalk_wrapper.so
```

## ビルドとテスト

### 1. APKビルド
1. File → Build Settings
2. Build → Build APK
3. 生成されたAPKをデバイスにインストール

### 2. 実機テスト
```bash
# ADB経由でインストール
adb install -r your_app.apk

# ログの確認
adb logcat -s Unity
```

### 3. トラブルシューティング

#### ライブラリが読み込まれない場合
- Player Settings → Other Settings → Configuration → Scripting Backend が `IL2CPP` になっているか確認
- すべてのTarget Architecturesが有効になっているか確認

#### 音素化が動作しない場合
- adb logcatでエラーログを確認
- OpenJTalk辞書がStreamingAssetsに含まれているか確認

## CI/CD用の設定

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
    BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
    buildPlayerOptions.scenes = new[] { "Assets/Scenes/SampleScene.unity" };
    buildPlayerOptions.locationPathName = "Build/Android/uPiper.apk";
    buildPlayerOptions.target = BuildTarget.Android;
    buildPlayerOptions.options = BuildOptions.None;

    BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
    if (report.summary.result == BuildResult.Succeeded)
    {
        Debug.Log("Build succeeded: " + report.summary.totalSize + " bytes");
    }
    else
    {
        Debug.Log("Build failed");
    }
}
```