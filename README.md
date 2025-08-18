# uPiper

[English](README.en.md) | 日本語

[![Unity Tests](https://github.com/ayutaz/uPiper/actions/workflows/unity-tests.yml/badge.svg)](https://github.com/ayutaz/uPiper/actions/workflows/unity-tests.yml)
[![Unity Build](https://github.com/ayutaz/uPiper/actions/workflows/unity-build.yml/badge.svg)](https://github.com/ayutaz/uPiper/actions/workflows/unity-build.yml)

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
- 🌍 多言語対応（日本語、英語）
- 🚀 Unity AI Inference Engineによる高速推論
- 📱 マルチプラットフォーム対応
- 🔧 OpenJTalkによる高精度な日本語音素化（Windows/macOS/Linux/Android）
- ⚡ GPU推論サポート（GPUCompute/GPUPixel）
- 🎭 高度なサンプル（ストリーミング、複数音声、リアルタイム）

## Requirements
* Unity 6000.0.55f1
* Unity AI Interface (Inference Engine) 2.2.x

## ビルド要件

- **Windows**: Visual Studio 2022以降
- **macOS**: Xcode 14以降
- **Linux**: GCC 9以降
- **Android**: NDK r21以降

## インストール

### Unity Package Manager経由（推奨）

#### ステップ1: パッケージのインストール
1. Unity Editorで `Window > Package Manager` を開く
2. `+` ボタンから `Add package from git URL...` を選択
3. 以下のURLを入力：
   ```
   https://github.com/ayutaz/uPiper.git?path=Assets/uPiper
   ```

#### ステップ2: 必要なデータのインポート

Package Managerからインストール後、**必ず以下の手順でデータをインポートしてください**：

1. **Package Managerで「In Project」を選択**
2. **「uPiper」パッケージを選択**
3. **「Samples」セクションを展開**
4. **以下のサンプルをインポート**：
   - 📚 **OpenJTalk Dictionary Data** (必須) - 日本語音声合成用辞書
   - 📚 **CMU Pronouncing Dictionary** (必須) - 英語音声合成用辞書
   - 🎤 **Voice Models** (推奨) - 高品質音声モデル
   - 🎮 **Basic TTS Demo** (オプション) - デモシーン

#### ステップ3: データのセットアップ

サンプルをインポートした後：

1. **メニューから `uPiper > Setup > Install from Samples` を実行**
2. インストールダイアログで「Install」をクリック
3. セットアップが完了するまで待つ

> 💡 **注**: 以前の「Run Initial Setup」メニューは削除されました。「Install from Samples」が唯一のセットアップ方法です。
> 
> 📁 **モデルファイルの保存場所**: 音声モデルは `Assets/Resources/uPiper/Models/` に保存されます（パッケージの外側）。

#### ステップ4: 動作確認

1. **メニューから `uPiper > Setup > Check Setup Status` を実行**
2. すべての項目が「✓ Installed」になっていることを確認
3. Basic TTS Demoシーンを開いて動作確認

> ⚠️ **重要**: 辞書データをインポートしないとTTS機能は動作しません

### パッケージファイルからのインストール
[Releases](https://github.com/ayutaz/uPiper/releases)から最新のパッケージファイルをダウンロード：
- **Unity Package (.unitypackage)**: レガシー形式、全てのUnityバージョンで使用可能
- **UPM Package (.tgz)**: Unity Package Manager用、Unity 2019.3以降

### トラブルシューティング

#### Samplesが1つしか表示されない場合
- Unity Editorを再起動
- Package Managerで「Refresh」ボタンをクリック

#### 辞書ファイルが見つからないエラー
- `uPiper > Setup > Install from Samples` を実行したか確認
- `uPiper > Setup > Check Setup Status` で状態を確認

#### 日本語が文字化けする場合
- Basic TTS Demoに含まれるNotoSansJP-Regular SDFフォントを使用

## サポートプラットフォーム

### 現在サポート中
- ✅ Windows (x64)
- ✅ macOS (Apple Silicon/Intel)
- ✅ Linux (x64)
- ✅ Android (ARM64)

### 未対応
- ❌ WebGL - 技術調査中（piper-plus連携により将来対応予定）
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

詳細は[GPU推論ガイド](docs/features/gpu/gpu-inference.md)を参照してください。

## 開発者向け情報

### プロジェクト構造

開発環境では以下の構造で辞書データを管理しています：

```
Assets/
├── uPiper/
│   ├── Samples~/                         # Package Manager配布用データ（Unity Editorからは非表示）
│   │   ├── OpenJTalk Dictionary Data/    # 日本語音声合成用辞書（約50MB）
│   │   │   └── naist_jdic/              # OpenJTalk辞書本体
│   │   │       └── open_jtalk_dic_utf_8-1.11/
│   │   ├── CMU Pronouncing Dictionary/   # 英語音声合成用辞書（約3MB）
│   │   │   └── cmudict-0.7b.txt         # CMU辞書本体
│   │   └── Voice Models/                 # 音声モデル（ONNX形式）
│   │       ├── ja_JP-test-medium.onnx   # 日本語音声モデル
│   │       └── en_US-ljspeech-medium.onnx # 英語音声モデル
│   ├── Runtime/                          # ランタイムコード
│   ├── Editor/                           # エディタ拡張
│   └── Plugins/                          # ネイティブプラグイン
└── StreamingAssets/                      # 実行時データ（Package Manager版のみ）
```

#### 注意事項

- **Samples~フォルダ**: Unity Editorからは見えません（Unityの仕様）
- **開発環境判定**: `UPIPER_DEVELOPMENT`プリプロセッサディレクティブが定義されています
- **辞書の読み込み**: 開発環境では`Samples~`から直接読み込み、Package Manager版では`StreamingAssets`から読み込みます
- **セットアップ不要**: 開発環境では初回セットアップは不要で、クローン後すぐに動作します

### Package Manager配布時の動作

Package Manager経由で配布される場合：
1. ユーザーがPackage Managerから辞書サンプルをインポート
2. `uPiper/Setup/Install from Samples`メニューを実行
3. 辞書データが`StreamingAssets/uPiper/`にコピーされる

## 詳細ドキュメント

- [アーキテクチャ](docs/ARCHITECTURE_ja.md) - 設計と技術的な詳細
- [開発ログ](docs/DEVELOPMENT_LOG.md) - 開発進捗と変更履歴
- [ドキュメント一覧](docs/) - 技術ドキュメント、ガイド、仕様書

## ライセンス

このプロジェクトは Apache License 2.0 の下でライセンスされています。詳細は [LICENSE](LICENSE) ファイルを参照してください。

### サードパーティライセンス

#### フォント
- **Noto Sans Japanese**: SIL Open Font License, Version 1.1
  - Copyright 2014-2021 Adobe (http://www.adobe.com/)
  - TextMeshProでの日本語表示に使用
  - 詳細は `Assets/Fonts/LICENSE.txt` を参照
