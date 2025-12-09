# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## プロジェクト概要

uPiperは[piper-plus](https://github.com/ayutaz/piper-plus)ベースの高品質ニューラルTTS（Text-to-Speech）Unityプラグイン。VITS（Variational Inference with adversarial learning for end-to-end Text-to-Speech）モデルを使用し、日本語（OpenJTalk）と英語（Flite LTS）の多言語音声合成に対応。

## ビルド・テストコマンド

### Unity テスト実行
```bash
# GitHub Actions経由（EditModeテストのみ）
# .github/workflows/unity-tests.yml 参照

# Unity Editor内
# Window > General > Test Runner > Run All
```

### OpenJTalkネイティブライブラリビルド
```bash
cd NativePlugins/OpenJTalk
./build_ci.sh  # Linux/macOS
# Windows: CMake + Visual Studio または MinGW
```

### コードフォーマットチェック
```bash
dotnet format --verify-no-changes
```

## アーキテクチャ

### データフロー
```
テキスト入力
    ↓
Phonemizer (テキスト→音素変換)
    • 日本語: OpenJTalk (ネイティブC++ライブラリ, P/Invoke)
    • 英語: Flite LTS (純粋C#実装)
    ↓
音素エンコーディング (Unicode PUAマッピング)
    • 複数文字音素（ky, ch, ts, sh等）→ Private Use Area文字
    ↓
VITS推論 (ONNX via Unity.InferenceEngine)
    • GPU: GPUPixel (推奨), GPUCompute (非推奨)
    • CPU: macOSデフォルト
    ↓
AudioClip出力 (22050Hz, float32)
```

### 主要コンポーネント

| コンポーネント | 場所 | 役割 |
|--------------|------|------|
| `IPiperTTS` / `PiperTTS` | `Runtime/Core/` | メインインターフェース |
| `IPhonemizerBackend` | `Runtime/Core/Phonemizers/Backend/` | 音素化バックエンド抽象 |
| `OpenJTalkPhonemizerBackend` | 同上 | 日本語音素化（P/Invoke） |
| `FliteLTSPhonemizerBackend` | 同上 | 英語音素化（C#） |
| `PiperConfig` | `Runtime/Core/` | 設定管理（GPU, キャッシュ, バックエンド選択） |
| `AudioChunkBuilder` | `Runtime/Core/AudioGeneration/` | 音声波形→AudioClip変換 |

### ディレクトリ構造
```
Assets/uPiper/
├── Runtime/Core/           # ランタイムコア
│   ├── AudioGeneration/    # AudioClip生成
│   ├── Phonemizers/        # 音素化システム
│   │   ├── Backend/        # バックエンド実装
│   │   ├── Native/         # P/Invoke定義
│   │   └── Threading/      # マルチスレッド処理
│   ├── IL2CPP/             # IL2CPP互換レイヤー
│   └── Platform/           # プラットフォーム固有コード
├── Editor/                 # エディタツール
├── Tests/                  # テスト
│   ├── Editor/             # EditModeテスト
│   └── Runtime/            # PlayModeテスト
├── Plugins/                # ネイティブライブラリ
└── Samples~/               # サンプルデータ

StreamingAssets/uPiper/     # 実行時データ（モデル, 辞書）
NativePlugins/OpenJTalk/    # OpenJTalk C/C++ソース
```

### Assembly Definitions
- `uPiper.Runtime.asmdef` - ランタイムコード
- `uPiper.Editor.asmdef` - エディタツール
- `uPiper.Tests.Editor.asmdef` - EditModeテスト
- `uPiper.Tests.Runtime.asmdef` - PlayModeテスト

## コーディング規約

### 命名規則（.editorconfig準拠）
- クラス、プロパティ、公開フィールド: PascalCase
- プライベートインスタンスフィールド: `_camelCase`（アンダースコアプレフィックス）
- プライベート静的フィールド: camelCase
- 定数: PascalCase
- ローカル変数、パラメータ: camelCase

### コードスタイル
- インデント: 4スペース
- 最大行長: 120文字
- using文: System.*を先頭に配置
- アクセス修飾子: 常に明示（`dotnet_style_require_accessibility_modifiers = always:error`）
- `var`の使用を推奨
- null合体演算子・null条件演算子の使用を推奨

### Unityアナライザー
- UNT0007/UNT0008: Unityオブジェクトにnull合体/null条件演算子を使用しない
- UNT0023: Unityオブジェクトにnull合体代入を使用しない

## プラットフォーム対応

| プラットフォーム | InferenceBackend.Auto |
|-----------------|----------------------|
| Windows/Linux | GPUPixel |
| macOS | CPU（Metal非対応） |
| iOS/Android | GPUPixel |
| WebGL | 未対応（調査中） |

## 定義シンボル

- `UPIPER_DEVELOPMENT` - セットアップウィザード無効化、開発メニュー有効化
- `ENABLE_IL2CPP_COMPATIBILITY` - IL2CPP固有コードパス

## バージョン管理

中央バージョン定数: `Assets/uPiper/Editor/uPiperSetup.cs`

## GitHub Actions ワークフロー

| ワークフロー | 目的 |
|------------|------|
| `unity-tests.yml` | Edit/Playモードテスト |
| `unity-build.yml` | マルチプラットフォームビルド |
| `dotnet-format.yml` | C#コードフォーマット |
| `build-openjtalk-native.yml` | ネイティブライブラリコンパイル |
