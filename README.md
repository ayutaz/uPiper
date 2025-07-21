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

## Requirements
* Unity 6000.0.35f1
* Unity AI Interface (Inference Engine) 2.2.x

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

## ライセンス

### フォント
- **Noto Sans Japanese**: SIL Open Font License, Version 1.1
  - Copyright 2014-2021 Adobe (http://www.adobe.com/)
  - TextMeshProでの日本語表示に使用
  - 詳細は `Assets/Fonts/LICENSE.txt` を参照
