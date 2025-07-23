# uPiper

[![Unity Tests](https://github.com/ayutaz/uPiper/actions/workflows/unity-tests.yml/badge.svg)](https://github.com/ayutaz/uPiper/actions/workflows/unity-tests.yml)
[![Unity Build](https://github.com/ayutaz/uPiper/actions/workflows/unity-build.yml/badge.svg)](https://github.com/ayutaz/uPiper/actions/workflows/unity-build.yml)
[![codecov](https://codecov.io/github/ayutaz/uPiper/graph/badge.svg?token=348eb741-4320-4368-89fa-3eee5188bd3f)](https://codecov.io/github/ayutaz/uPiper)

[piper-plus]()のUnityプラグイン

## 機能

- 🎤 高品質な音声合成（Piper TTSベース）
- 🌍 多言語対応（日本語、英語、中国語、韓国語など）
- 🚀 Unity AI Inference Engineによる高速推論
- 📱 マルチプラットフォーム対応
- 🔧 OpenJTalkによる高精度な日本語音素化（Windows/macOS/Linux）
- ⚡ GPU推論サポート（GPUCompute/GPUPixel）
- 🎭 高度なサンプル（ストリーミング、複数音声、リアルタイム）

## Requirements
* Unity 6000.0.35f1
* Unity AI Interface (Inference Engine) 2.2.x

## インストール

### Unity Package Manager経由（推奨）
1. Unity Editorで `Window > Package Manager` を開く
2. `+` ボタンから `Add package from git URL...` を選択
3. 以下のURLを入力：
   ```
   https://github.com/ayutaz/uPiper.git?path=Assets/uPiper
   ```

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

### 未対応
- ❌ WebGL - Unity.InferenceEngineがWebGLをサポートしていないため
- ❌ iOS/Android - モバイル対応は今後検討予定

## ビルド

### 自動ビルド（GitHub Actions）
- mainブランチへのプッシュ時に自動的に全プラットフォーム向けのビルドが実行されます
- リリースタグ（v*）をプッシュすると、自動的にリリースが作成されます

### 手動ビルド
1. Unity Editorで `uPiper/Build/Configure Build Settings` を実行
2. `uPiper/Build/Build All Platforms` で全プラットフォームをビルド

## アーキテクチャと設計判断

### 音声合成パイプライン
uPiperは、ニューラルネットワークベースの音声合成（VITS）を採用しています：

```
テキスト → 音素化（OpenJTalk/eSpeak-NG） → VITSモデル → 音声
```

### 重要な設計判断（Phase 1.10）

#### 1. 音素タイミングの簡略化
- **現状**: OpenJTalkから出力される音素の継続時間は全て50ms固定
- **理由**: VITSモデル内のDuration Predictorが自動的に適切なタイミングを再計算するため
- **影響**: 音声品質への影響なし（ニューラルモデルが補正）

#### 2. PUA（Private Use Area）文字の使用
- **目的**: 複数文字の音素（"ky", "ch", "ts"など）を単一文字として表現
- **理由**: Piperモデルは1音素=1文字を期待するため
- **実装**: Unicode PUA領域（U+E000-U+F8FF）を使用

#### 3. HTS Engine非使用
- **決定**: OpenJTalkのHTS Engine音声合成機能は使用しない
- **理由**: Piperはニューラル音声合成（VITS）を使用するため、HMMベースの合成は不要
- **利点**: 依存関係の削減、ビルドサイズの縮小

詳細な技術情報は[ドキュメント](docs/)を参照してください。

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

詳細は[GPU推論ガイド](docs/technical/GPU-INFERENCE-GUIDE.md)を参照してください。

## 実装進捗

### Phase 1: 基盤実装
- ✅ 1.1-1.9: Core API、音素化システム、OpenJTalkライブラリ実装
- ✅ 1.10: OpenJTalk統合による日本語発音改善（2025年1月完了）
- ✅ 1.11: Unity基本統合 - Package Manager対応、デモシーン作成（2025年1月完了）
- ✅ 1.12: IL2CPPサポート - Mono/IL2CPP両対応、CI/CD統合（2025年1月完了）
- ✅ 1.13: GPU推論サポートと高度なサンプル実装（2025年1月完了）

詳細な進捗は[ロードマップ](docs/ROADMAP.md)を参照してください。

## ライセンス

### フォント
- **Noto Sans Japanese**: SIL Open Font License, Version 1.1
  - Copyright 2014-2021 Adobe (http://www.adobe.com/)
  - TextMeshProでの日本語表示に使用
  - 詳細は `Assets/Fonts/LICENSE.txt` を参照
