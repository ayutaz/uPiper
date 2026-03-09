# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## プロジェクト概要

uPiperは[piper-plus](https://github.com/ayutaz/piper-plus)ベースの高品質ニューラルTTS（Text-to-Speech）Unityプラグイン。VITS（Variational Inference with adversarial learning for end-to-end Text-to-Speech）モデルを使用し、日本語（dot-net-g2p / MeCab辞書）と英語（Flite LTS）の多言語音声合成に対応。

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

### コードフォーマットチェック
```bash
dotnet format --verify-no-changes
```

## アーキテクチャ

### データフロー
```
テキスト入力
    ↓
カスタム辞書による前処理 (CustomDictionary)
    • 技術用語・固有名詞の読み変換
    • 例: "Docker" → "ドッカー", "GitHub" → "ギットハブ"
    ↓
Phonemizer (テキスト→音素変換)
    • 日本語: dot-net-g2p (純粋C#実装, MeCab辞書)
    • 英語: Flite LTS (純粋C#実装)
    ↓
音素エンコーディング (Unicode PUAマッピング)
    • 複数文字音素（ky, ch, ts, sh等）→ Private Use Area文字
    ↓
┌─────────────────────────────────────────────────┐
│ Prosody対応モデルの場合（tsukuyomi-chan等）     │
│   Prosody情報取得 (DotNetG2PPhonemizer PhonemizeWithProsody)│
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
| `FliteLTSPhonemizerBackend` | 同上 | 英語音素化（C#） |
| `DotNetG2PPhonemizer` | `Runtime/Core/Phonemizers/Implementations/` | 日本語G2P（dot-net-g2p, Prosody対応） |
| `CustomDictionary` | `Runtime/Core/Phonemizers/` | カスタム辞書（技術用語・固有名詞の読み変換） |
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
│   │       ├── WebGLStreamingAssetsLoader.cs  # WebGL非同期ファイルローダー
│   │       ├── IndexedDBCache.cs              # IndexedDBキャッシュC#ラッパー
│   │       └── WebGLLoadingPanel.cs           # ローディング進捗UI
│   └── Demo/               # デモ・テストUI
├── Resources/Models/       # ONNXモデルファイル（*.onnx, *.onnx.json）
├── Editor/                 # エディタツール
│   └── WebGL/                 # WebGLビルドツール
│       ├── WebGLSplitDataProcessor.cs  # 大容量ファイル自動分割
│       ├── split-file-loader.js        # 分割ファイル結合ローダー
│       └── github-pages-adapter.js     # GitHub Pagesパス解決
├── Tests/                  # テスト
│   ├── Editor/             # EditModeテスト
│   └── Runtime/            # PlayModeテスト
├── Plugins/                # プラットフォーム固有プラグイン（Android Sentis等）
│   └── WebGL/                 # WebGLネイティブプラグイン
│       └── IndexedDBCache.jslib        # IndexedDB JS interop
└── Samples~/               # サンプルデータ

StreamingAssets/uPiper/     # 実行時データ（辞書）
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
| WebGL | GPUPixel（Phase 1-3完了、Phase 2/4未実装） |

## 定義シンボル

- `UPIPER_DEVELOPMENT` - セットアップウィザード無効化、開発メニュー有効化
- `ENABLE_IL2CPP_COMPATIBILITY` - IL2CPP固有コードパス
- `UNITY_WEBGL` - WebGLプラットフォーム判定（Task.Run除去、ファイルI/O代替）

## バージョン管理

中央バージョン定数: `Assets/uPiper/Editor/uPiperSetup.cs`

## GitHub Actions ワークフロー

| ワークフロー | 目的 |
|------------|------|
| `unity-tests.yml` | Edit/Playモードテスト |
| `unity-build.yml` | マルチプラットフォームビルド |
| `dotnet-format.yml` | C#コードフォーマット |
| `deploy-webgl.yml` | WebGLビルド・GitHub Pagesデプロイ |

## Prosody（韻律）機能

### 概要
Prosody対応モデル（tsukuyomi-chan等）では、dot-net-g2p（MeCab辞書）から取得したアクセント情報を使用してより自然なイントネーションの音声を生成できる。

### Prosodyパラメータ
- **A1 (ProsodyA1)**: アクセント句内でのモーラ位置（0始まり）
- **A2 (ProsodyA2)**: アクセント句内のアクセント核位置（アクセント型）
- **A3 (ProsodyA3)**: 呼気段落（イントネーション句）内でのアクセント句位置

### 使用方法
```csharp
// Prosody情報付き音素化
var phonemizer = new DotNetG2PPhonemizer();
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

## カスタム辞書機能

### 概要
技術用語や固有名詞（英単語・アルファベット）を日本語の読みに変換する前処理機能。piper-plusのPython実装と互換性のあるJSON形式をサポート。

### 辞書ファイル
辞書は `StreamingAssets/uPiper/Dictionaries/` に配置（ファイル名順に読み込み）:

| ファイル | 内容 |
|---------|------|
| `additional_tech_dict.json` | AI/LLM関連用語 |
| `default_common_dict.json` | IT/ビジネス用語 |
| `default_tech_dict.json` | 技術用語（プログラミング言語、開発ツール等） |
| `user_custom_dict.json` | ユーザー定義辞書（テンプレート） |

### JSON形式
```json
{
  "version": "2.0",
  "entries": {
    "Docker": {"pronunciation": "ドッカー", "priority": 9},
    "GitHub": {"pronunciation": "ギットハブ", "priority": 9}
  }
}
```

### 使用方法
`DotNetG2PPhonemizer`が自動的にカスタム辞書を読み込み、テキスト前処理を行う。
```csharp
// 辞書は自動読み込み
var phonemizer = new DotNetG2PPhonemizer();

// "DockerとGitHubを使った開発" → "ドッカーとギットハブを使った開発" に変換後、音素化
var result = phonemizer.PhonemizeWithProsody("DockerとGitHubを使った開発");
```

### 動的追加
```csharp
var dict = new CustomDictionary();
dict.AddWord("MyTerm", "マイターム", priority: 10);
```

## 音素エンコーディング

### IPA vs PUA モデル

Piperモデルには2種類の音素表現がある：

| モデルタイプ | 音素表現 | 例 | 対応モデル |
|------------|---------|-----|-----------|
| **PUA (Private Use Area)** | Unicode私用領域文字 | `ch` → `\ue00e` (ID 39) | ja_JP-test-medium |
| **IPA (International Phonetic Alphabet)** | 国際音声記号 | `ch` → `tɕ` (ID 32) | tsukuyomi-chan |

### PhonemeEncoder の動作

`PhonemeEncoder`は初期化時にモデルの`phoneme_id_map`を検査し、IPA文字（`ɕ`等）の有無で自動判定：

```csharp
// IPA判定: phoneme_id_mapに "ɕ" が含まれているか
_useIpaMapping = _phonemeToId.ContainsKey("ɕ");
```

**IPAモデルの場合**:
1. PUA文字を元の音素に逆変換（`\ue00e` → `ch`）
2. IPA音素に変換（`ch` → `tɕ`）
3. phoneme_id_mapでIDを取得

**PUAモデルの場合**:
1. PUA文字をそのまま使用
2. phoneme_id_mapでIDを取得

### 主要な音素マッピング

| G2P出力 | PUA文字 | PUA ID | IPA音素 | IPA ID |
|--------------|---------|--------|---------|--------|
| `ch` (ち) | `\ue00e` | 39 | `tɕ` | 32 |
| `ts` (つ) | `\ue00f` | 40 | `ts` | 33 |
| `sh` (し) | `\ue010` | 42 | `ɕ` | 18 |
| `cl` (っ) | `\ue005` | 23 | `q` | 24 |
| `ky` (きゃ) | `\ue006` | 26 | `kʲ` | - |
| `N` (ん) | `N` | 22 | `ɴ` | 22 |

### デバッグ

PhonemeEncoderは初期化時に詳細なログを出力：
```
[PhonemeEncoder] PhonemeIdMap count: 58
[PhonemeEncoder] _useIpaMapping: True
[PhonemeEncoder] IPA key 'ɕ': found
[PhonemeEncoder] IPA key 'tɕ': found
```
