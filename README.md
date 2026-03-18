# uPiper

[English](README.en.md) | 日本語

[![openupm](https://img.shields.io/npm/v/com.ayutaz.upiper?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.ayutaz.upiper/)
[![Unity Tests](https://github.com/ayutaz/uPiper/actions/workflows/unity-tests.yml/badge.svg)](https://github.com/ayutaz/uPiper/actions/workflows/unity-tests.yml)
[![Unity Build](https://github.com/ayutaz/uPiper/actions/workflows/unity-build.yml/badge.svg)](https://github.com/ayutaz/uPiper/actions/workflows/unity-build.yml)

[piper-plus](https://github.com/ayutaz/piper-plus)のUnityプラグイン - 高品質なニューラル音声合成エンジン

## 目次

- [機能](#機能)
- [Requirements](#requirements)
- [インストール](#インストール)
  - [OpenUPM経由（推奨）](#openupm経由推奨)
  - [Git URL経由](#git-url経由)
  - [パッケージファイルからのインストール](#パッケージファイルからのインストール)
  - [トラブルシューティング](#トラブルシューティング)
- [サポートプラットフォーム](#サポートプラットフォーム)
- [GPU推論の使用](#gpu推論の使用)
- [詳細ドキュメント](#詳細ドキュメント)
- [ライセンス](#ライセンス)

## 機能

- 高品質な音声合成（piper-plusベース）
- 多言語対応（日本語、英語、中国語、スペイン語、フランス語、ポルトガル語）
- Unity AI Inference Engineによる高速推論
- dot-net-g2p（MeCab辞書）による高精度な日本語音素化（全プラットフォーム対応）
- GPU推論サポート（GPUCompute/GPUPixel）
- **Prosody（韻律）サポート**: より自然なイントネーションの音声合成
- **カスタム辞書**: 技術用語・固有名詞の読み変換

### 対応モデル

| モデル名 | 言語 | Prosody対応 | 説明 |
|---------|------|------------|------|
| multilingual-test-medium | 多言語(6言語: ja/en/zh/es/fr/pt) | Yes | 多言語対応モデル（Prosody対応） |

## Requirements
* Unity 6000.0.58f2
* Unity AI Inference Engine (com.unity.ai.inference) 2.2.2

## インストール

### OpenUPM経由（推奨）

#### openupm-cliを使用する場合

```bash
openupm add com.ayutaz.upiper
```

#### manifest.jsonを直接編集する場合

`Packages/manifest.json` に以下のscoped registryを追加してください：

```json
{
  "scopedRegistries": [
    {
      "name": "package.openupm.com",
      "url": "https://package.openupm.com",
      "scopes": [
        "com.ayutaz.upiper"
      ]
    }
  ],
  "dependencies": {
    "com.ayutaz.upiper": "1.3.0"
  }
}
```

### Git URL経由

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
   - **MeCab Dictionary Data** (必須) - 日本語音声合成用MeCab辞書
   - **CMU Pronouncing Dictionary** (必須) - 英語音声合成用辞書
   - **Voice Models** (推奨) - 高品質音声モデル
   - **Basic TTS Demo** (オプション) - デモシーン

#### ステップ3: データのセットアップ

サンプルをインポートした後：

1. **メニューから `uPiper > Setup > Install from Samples` を実行**
2. インストールダイアログで「Install」をクリック
3. セットアップが完了するまで待つ

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

#### UIボタンがクリックできない場合（Input Manager使用時）
- プロジェクト設定で「Active Input Handling」を確認
- Edit > Project Settings > Player > Active Input Handling
- 「Input Manager」に設定されている場合、EventSystemAutoSetupコンポーネントが自動的に対応
- 詳細は `Samples~/BasicTTSDemo/BasicTTSDemo_README.md` を参照

## サポートプラットフォーム

- ✅ Windows (x64)
- ✅ macOS (Apple Silicon/Intel)
- ✅ Linux (x64)
- ✅ Android (ARM64/ARMv7/x86/x86_64)
- ✅ iOS (ARM64, iOS 11.0+)
- ✅ WebGL (WebGPU / WebGL2)

> **WebGL**: WebGPU対応ブラウザではGPUComputeによる高速推論、WebGL2環境ではGPUPixelに自動フォールバックします。
> [デモページ](https://ayutaz.github.io/uPiper/)

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

### InferenceBackend.Auto の選択ロジック

`InferenceBackend.Auto`を指定した場合、プラットフォームに応じて最適なバックエンドが自動選択されます：

| プラットフォーム | 自動選択されるバックエンド | 理由 |
|-----------------|--------------------------|------|
| Windows/Linux | GPUPixel | VITSモデルとの互換性が良好 |
| macOS | CPU | MetalはUnity.InferenceEngineで問題があるため |
| iOS/Android | GPUPixel | モバイルGPUに最適化 |
| WebGL (WebGPU) | GPUCompute | WebGPU Compute Shaderによる高速推論 |
| WebGL (WebGL2) | GPUPixel | WebGL2フォールバック |

> **注意**: デスクトップ環境ではGPUComputeはVITSモデルで音声が正しく生成されない問題があるため、GPUPixelまたはCPUの使用を推奨します。WebGPU環境ではGPUComputeが正常に動作します。

詳細は[GPU推論ガイド](docs/features/gpu/gpu-inference.md)を参照してください。

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
