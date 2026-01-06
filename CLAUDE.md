# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## プロジェクト概要

uPiperは[piper-plus](https://github.com/ayutaz/piper-plus)ベースの高品質ニューラルTTS（Text-to-Speech）Unityプラグイン。VITS（Variational Inference with adversarial learning for end-to-end Text-to-Speech）モデルを使用し、日本語（OpenJTalk）と英語（Flite LTS）の多言語音声合成に対応。

### 対応モデル

| モデル名 | 言語 | Prosody対応 | 説明 |
|---------|------|------------|------|
| ja_JP-test-medium | 日本語 | No | 標準日本語モデル |
| en_US-ljspeech-medium | 英語 | No | 標準英語モデル |
| tsukuyomi-chan | 日本語 | Yes | Prosody対応日本語モデル（より自然なイントネーション） |

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
┌─────────────────────────────────────────────────┐
│ Prosody対応モデルの場合（tsukuyomi-chan等）     │
│   Prosody情報取得 (OpenJTalk PhonemizeWithProsody)│
│     • A1: アクセント句内モーラ位置              │
│     • A2: アクセント句内アクセント位置          │
│     • A3: 呼気段落内アクセント句位置            │
└─────────────────────────────────────────────────┘
    ↓
VITS推論 (ONNX via Unity.InferenceEngine)
    • GPU: GPUPixel (推奨), GPUCompute (非推奨)
    • CPU: macOSデフォルト
    • Prosody対応: GenerateAudioWithProsodyAsync
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
| `OpenJTalkPhonemizer` | `Runtime/Core/Phonemizers/Implementations/` | Prosody情報付き音素化 |
| `PiperConfig` | `Runtime/Core/` | 設定管理（GPU, キャッシュ, バックエンド選択） |
| `AudioChunkBuilder` | `Runtime/Core/AudioGeneration/` | 音声波形→AudioClip変換 |
| `InferenceAudioGenerator` | `Runtime/Core/AudioGeneration/` | ONNX直接推論（Prosody対応） |
| `InferenceEngineDemo` | `Runtime/Demo/` | テスト用デモUI |

### ディレクトリ構造
```
Assets/uPiper/
├── Runtime/
│   ├── Core/               # ランタイムコア
│   │   ├── AudioGeneration/    # AudioClip生成、ONNX推論
│   │   ├── Phonemizers/        # 音素化システム
│   │   │   ├── Backend/        # バックエンド実装
│   │   │   ├── Implementations/# Prosody対応実装
│   │   │   ├── Native/         # P/Invoke定義
│   │   │   └── Threading/      # マルチスレッド処理
│   │   ├── IL2CPP/             # IL2CPP互換レイヤー
│   │   └── Platform/           # プラットフォーム固有コード
│   └── Demo/               # デモ・テストUI
├── Resources/Models/       # ONNXモデルファイル（*.onnx, *.onnx.json）
├── Editor/                 # エディタツール
├── Tests/                  # テスト
│   ├── Editor/             # EditModeテスト
│   └── Runtime/            # PlayModeテスト
├── Plugins/                # ネイティブライブラリ
└── Samples~/               # サンプルデータ

StreamingAssets/uPiper/     # 実行時データ（辞書）
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

## Prosody（韻律）機能

### 概要
Prosody対応モデル（tsukuyomi-chan等）では、OpenJTalkから取得したアクセント情報を使用してより自然なイントネーションの音声を生成できる。

### Prosodyパラメータ
- **A1 (ProsodyA1)**: アクセント句内でのモーラ位置（0始まり）
- **A2 (ProsodyA2)**: アクセント句内のアクセント核位置（アクセント型）
- **A3 (ProsodyA3)**: 呼気段落（イントネーション句）内でのアクセント句位置

### 使用方法
```csharp
// Prosody情報付き音素化
var phonemizer = new OpenJTalkPhonemizer();
var result = phonemizer.PhonemizeWithProsody("こんにちは");
// result.Phonemes: 音素配列
// result.ProsodyA1, ProsodyA2, ProsodyA3: 各音素に対応するProsody値

// Prosody対応音声生成
var generator = new InferenceAudioGenerator();
await generator.InitializeAsync(modelAsset, voiceConfig);
if (generator.SupportsProsody)
{
    var audio = await generator.GenerateAudioWithProsodyAsync(
        phonemeIds, prosodyA1, prosodyA2, prosodyA3);
}
```

### モデル判定
- `InferenceAudioGenerator.SupportsProsody`: ONNXモデルがProsody入力をサポートしているかを返す
- Prosody対応モデルは `a1`, `a2`, `a3` 入力テンソルを持つ
