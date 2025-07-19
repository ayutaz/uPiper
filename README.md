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

## ビルド

### 自動ビルド（GitHub Actions）
- mainブランチへのプッシュ時に自動的に全プラットフォーム向けのビルドが実行されます
- WebGLビルドはGitHub Pagesに自動デプロイされます
- リリースタグ（v*）をプッシュすると、自動的にリリースが作成されます

### 手動ビルド
1. Unity Editorで `uPiper/Build/Configure Build Settings` を実行
2. `uPiper/Build/Build All Platforms` で全プラットフォームをビルド

### サポートプラットフォーム
- Windows (x64)
- macOS
- Linux (x64)

※ WebGLサポートについては[Issue #17](https://github.com/ayutaz/uPiper/issues/17)を参照してください

## ライセンス

### フォント
- **Noto Sans Japanese**: SIL Open Font License, Version 1.1
  - Copyright 2014-2021 Adobe (http://www.adobe.com/)
  - TextMeshProでの日本語表示に使用
  - 詳細は `Assets/Fonts/LICENSE.txt` を参照
