# uPiper

[![Unity Tests](https://github.com/ayutaz/uPiper/actions/workflows/unity-tests.yml/badge.svg)](https://github.com/ayutaz/uPiper/actions/workflows/unity-tests.yml)
[![Unity Build](https://github.com/ayutaz/uPiper/actions/workflows/unity-build.yml/badge.svg)](https://github.com/ayutaz/uPiper/actions/workflows/unity-build.yml)
[![codecov](https://codecov.io/github/ayutaz/uPiper/graph/badge.svg?token=348eb741-4320-4368-89fa-3eee5188bd3f)](https://codecov.io/github/ayutaz/uPiper)

[piper-plus]()のUnityプラグイン

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
- WebGL
