# uPiper

[English](README.en.md) | 日本語

[![Unity Tests](https://github.com/ayutaz/uPiper/actions/workflows/unity-tests.yml/badge.svg)](https://github.com/ayutaz/uPiper/actions/workflows/unity-tests.yml)
[![Unity Build](https://github.com/ayutaz/uPiper/actions/workflows/unity-build.yml/badge.svg)](https://github.com/ayutaz/uPiper/actions/workflows/unity-build.yml)
[![codecov](https://codecov.io/github/ayutaz/uPiper/graph/badge.svg?token=348eb741-4320-4368-89fa-3eee5188bd3f)](https://codecov.io/github/ayutaz/uPiper)

[piper-plus](https://github.com/ayutaz/piper-plus)のUnityプラグイン - 高品質なニューラル音声合成エンジン

## 目次

- [機能](#機能)
- [Requirements](#requirements)
- [ビルド要件](#ビルド要件)
- [インストール](#インストール)
  - [Unity Package Manager経由（推奨）](#unity-package-manager経由推奨)
  - [パッケージファイルからのインストール](#パッケージファイルからのインストール)
  - [サンプルのインポート](#サンプルのインポート)
- [サポートプラットフォーム](#サポートプラットフォーム)
- [ビルドとパッケージ作成](#ビルドとパッケージ作成)
- [GPU推論の使用](#gpu推論の使用)
- [詳細ドキュメント](#詳細ドキュメント)
- [ライセンス](#ライセンス)

## 機能

- 🎤 高品質な音声合成（piper-plusベース）
- 🌍 多言語対応（日本語、英語、中国語、韓国語など）
- 🚀 Unity AI Inference Engineによる高速推論
- 📱 マルチプラットフォーム対応
- 🔧 OpenJTalkによる高精度な日本語音素化（Windows/macOS/Linux/Android）
- 🇨🇳 中国語音声合成対応（eSpeak-NG音素化、ピンイン変換）※音素精度に改善の余地あり
- ⚡ GPU推論サポート（GPUCompute/GPUPixel）
- 🎭 高度なサンプル（ストリーミング、複数音声、リアルタイム）
- 🌐 **WebGL対応**（実験的）- WebAssemblyによる日本語・英語音声合成

## Requirements
* Unity 6000.0.35f1
* Unity AI Interface (Inference Engine) 2.2.x

## ビルド要件

- **Windows**: Visual Studio 2019以降
- **macOS**: Xcode 14以降
- **Linux**: GCC 9以降
- **Android**: NDK r21以降

## インストール

### Unity Package Manager経由（推奨）
1. Unity Editorで `Window > Package Manager` を開く
2. `+` ボタンから `Add package from git URL...` を選択
3. 以下のURLを入力：
   ```
   https://github.com/ayutaz/uPiper.git?path=Assets/uPiper
   ```

### パッケージファイルからのインストール
[Releases](https://github.com/ayutaz/uPiper/releases)から最新のパッケージファイルをダウンロード：
- **Unity Package (.unitypackage)**: レガシー形式、全てのUnityバージョンで使用可能
- **UPM Package (.tgz)**: Unity Package Manager用、Unity 2019.3以降

### サンプルのインポート
1. Package Managerで uPiper を選択
2. `Samples` タブを開く
3. 利用可能なサンプル：
   - `Basic TTS Demo` - 基本的な音声生成
   - `Streaming TTS Demo` - リアルタイムストリーミング音声生成
   - `Multi-Voice Demo` - 複数音声の同時処理
   - `Realtime TTS Demo` - 低レイテンシ音声生成
4. インポートしたいサンプルの `Import` をクリック

## サポートプラットフォーム

### 現在サポート中
- ✅ Windows (x64)
- ✅ macOS (Apple Silicon/Intel)
- ✅ Linux (x64)
- ✅ Android (ARM64)
- ✅ WebGL（実験的）- 日本語（OpenJTalk）・英語（eSpeak-ng）対応

### 未対応
- ❌ iOS - Phase 5で対応予定

## ビルドとパッケージ作成

### 自動ビルド（GitHub Actions）
- mainブランチへのプッシュ時に自動的に全プラットフォーム向けのビルドが実行されます
- リリースタグ（v*）をプッシュすると、自動的にリリースとパッケージが作成されます

### パッケージエクスポート（開発者向け）
Unity Editorから手動でパッケージを作成：
1. `uPiper/Package/Export Unity Package (.unitypackage)` - レガシー形式
2. `uPiper/Package/Export UPM Package (.tgz)` - Unity Package Manager形式
3. `uPiper/Package/Export Both Formats` - 両形式を同時にエクスポート

## GPU推論の使用

uPiperはGPU推論をサポートしており、より高速な音声生成が可能です：

```csharp
var config = new PiperConfig
{
    Backend = InferenceBackend.Auto,  // 自動選択
    AllowFallbackToCPU = true,        // GPU失敗時にCPUフォールバック
    GPUSettings = new GPUInferenceSettings
    {
        MaxBatchSize = 4,
        UseFloat16 = true,
        MaxMemoryMB = 512
    }
};
```

詳細は[GPU推論ガイド](docs/ja/guides/technical/gpu-inference.md)を参照してください。

## WebGL対応（実験的）

uPiperはWebGLプラットフォームでの音声合成をサポートしています：

### 主な機能
- 🌐 ブラウザ上での高品質音声合成
- 🇯🇵 OpenJTalk WebAssemblyによる日本語音素化
- 🇺🇸 eSpeak-ng WebAssemblyによる英語音素化
- 💾 IndexedDBによる音素化結果のキャッシュ
- 🚀 GitHub Pagesへの自動デプロイ

### ビルド方法
```bash
# Unity Editorから
Menu: uPiper/Build/Build WebGL

# コマンドラインから
Unity -batchmode -quit -projectPath . -buildTarget WebGL -executeMethod PiperBuildProcessor.BuildWebGL
```

### デモサイト
GitHub Actionsによる自動デプロイ後、以下のURLでアクセス可能：
```
https://[username].github.io/uPiper/
```

### 制限事項
- 初回ロード時間が長い（WebAssembly + ONNXモデル）
- メモリ使用量が多い（Unity WebGL heap 1GB）
- 中国語音素化は基本実装のみ

詳細は[WebGL対応ガイド](docs/ja/guides/implementation/webgl-support/README.md)を参照してください。

## 詳細ドキュメント

- [アーキテクチャ](docs/ja/ARCHITECTURE.md) - 設計と技術的な詳細
- [開発ログ](docs/DEVELOPMENT_LOG.md) - 開発進捗と変更履歴
- [技術ドキュメント](docs/ja/guides/technical/) - 詳細な技術情報

## ライセンス

このプロジェクトは Apache License 2.0 の下でライセンスされています。詳細は [LICENSE](LICENSE) ファイルを参照してください。

### サードパーティライセンス

#### フォント
- **Noto Sans Japanese**: SIL Open Font License, Version 1.1
  - Copyright 2014-2021 Adobe (http://www.adobe.com/)
  - TextMeshProでの日本語表示に使用
  - 詳細は `Assets/Fonts/LICENSE.txt` を参照
