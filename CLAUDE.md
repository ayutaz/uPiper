# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## プロジェクト概要

uPiperは[piper-plus](https://github.com/ayutaz/piper-plus)ベースの高品質ニューラルTTS（Text-to-Speech）Unityプラグイン。VITS（Variational Inference with adversarial learning for end-to-end Text-to-Speech）モデルを使用。G2Pは7言語対応、現行モデルは6言語（ja/en/zh/es/fr/pt）に対応。

| 言語 | G2Pバックエンド |
|------|----------------|
| 日本語 | DotNetG2P.MeCab (dot-net-g2p / MeCab辞書) |
| 英語 | DotNetG2P.English (EnglishG2PEngine, CMU dict + LTS) |
| スペイン語 | DotNetG2P.Spanish (SpanishG2PEngine) |
| フランス語 | DotNetG2P.French (FrenchG2PEngine) |
| ポルトガル語 | DotNetG2P.Portuguese (PortugueseG2PEngine) |
| 中国語 | DotNetG2P.Chinese (ChineseG2PEngine, 44K文字辞書) |
| 韓国語 | DotNetG2P.Korean (KoreanG2PEngine) |

### 対応モデル

| モデル名 | 言語 | Prosody対応 | 説明 |
|---------|------|------------|------|
| multilingual-test-medium | 多言語(6言語) | Yes | 多言語対応モデル（ja/en/zh/es/fr/pt）、fp16、38MB、`phoneme_type: "multilingual"` |

## ビルド・テストコマンド

### Unity テスト実行
```bash
# GitHub Actions経由（EditMode + PlayModeテスト）
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
MultilingualPhonemizer
    • ILanguageDetector による言語検出:
      - HybridLanguageDetector (Unicode + Trigram複合検出)
      - UnicodeLanguageDetector (フォールバック)
    • ILanguageG2PHandler Strategy パターンで7言語ルーティング:
      ├─ ja: JapaneseG2PHandler (DotNetG2PPhonemizer, MeCab辞書)
      ├─ en: EnglishG2PHandler (EnglishG2PEngine, CMU dict + LTS)
      ├─ es: SpanishG2PHandler (SpanishG2PEngine)
      ├─ fr: FrenchG2PHandler (FrenchG2PEngine)
      ├─ pt: PortugueseG2PHandler (PortugueseG2PEngine)
      ├─ zh: ChineseG2PHandler (ChineseG2PEngine, 44K文字辞書)
      └─ ko: KoreanG2PHandler (KoreanG2PEngine)
    • 戻り値: MultilingualPhonemizeResult
      - Phonemes[], ProsodyFlat[] (stride=3), DetectedPrimaryLanguage
    ↓
PuaTokenMapper (インスタンスクラス、pua.json ランタイム読み込み対応)
    • 固定PUAマッピング + pua.json動的拡張
    • 全7言語の音素をPUA文字にマッピング
    ↓
PhonemeEncoder (ProsodyFlat stride=3 対応)
    • 音素→モデルID変換 (PhonemeIdMap: Dictionary<string, int[]>)
    • EncodeWithProsody: ProsodyFlatをBOS/EOS/PAD挿入に合わせて展開
    • FlattenProsody: 境界(BOS/EOS/PAD)に対応するProsody値を自動挿入
    ↓
┌──────────────────────────────────────────────────────┐
│ 多言語モデルの場合 (phoneme_type: "multilingual")     │
│   • PhonemeEncoder後にIntersperse PAD挿入:            │
│     [^, _, p1, _, p2, _, ..., $]                      │
│     （BOS後にもPADが入る）                             │
│   • PUA文字パススルー（IPA/PUA変換なし）              │
└──────────────────────────────────────────────────────┘
    ↓
TTSSynthesisOrchestrator (internal sealed class)
    → BackendSelector + PlatformInfo でバックエンド決定
    → IInferenceAudioGenerator (ONNX推論, NativeArray<float>出力)
    → SplitInferenceOrchestrator (沈黙句分割、オプション)
    ↓
AudioNormalizer (static class, NativeArray<float> in-place正規化)
    ↓
AudioClipBuilder (NativeArray<float>版 BuildAudioClip)
    • managed marshalling回避
    • AudioClip名: TTS_{Guid:N}
    ↓
AudioClip出力 (22050Hz, float32)
```

### 主要コンポーネント

#### コアAPI

| コンポーネント | 場所 | 役割 |
|--------------|------|------|
| `IPiperTTS` / `PiperTTS` | `Runtime/Core/` | メインインターフェース |
| `PiperConfig` | `Runtime/Core/` | 設定管理（GPU, キャッシュ, バックエンド選択） |
| `IPiperConfigReadOnly` | `Runtime/Core/` | バリデーション済み設定の読み取り専用インターフェース（6ネスト record struct プロパティ: Language, Performance, Inference, Audio, Silence, General） |
| `ValidatedPiperConfig` | `Runtime/Core/` | PiperConfig バリデーション後の不変スナップショット（IPiperConfigReadOnly実装、6 readonly record struct） |
| `SynthesisRequest` | `Runtime/Core/AudioGeneration/` | 音声合成リクエスト（public readonly struct）。音素・ProsodyFlat(stride=3)・合成パラメータを集約。ファクトリ: `FromPhonemes` / `FromPhonemesWithProsody` |
| `PhonemizeResult` | `Runtime/Core/AudioGeneration/` | PhonemizeAsync戻り値（public sealed class）。Phonemes[], ProsodyFlat[], DetectedLanguage, ResolvedLanguageId |

#### 音素化システム

| コンポーネント | 場所 | 役割 |
|--------------|------|------|
| `MultilingualPhonemizer` | `Runtime/Core/Phonemizers/Multilingual/` | 多言語テキスト分割・ILanguageG2PHandler Strategyで7言語ルーティング |
| `ILanguageG2PHandler` | `Runtime/Core/Phonemizers/Multilingual/Handlers/` | 言語別G2P Strategyインターフェース（public）。Process() → (Phonemes[], ProsodyFlat[]) |
| `JapaneseG2PHandler` | `Runtime/Core/Phonemizers/Multilingual/Handlers/` | 日本語G2P（DotNetG2PPhonemizer委譲、Prosody対応） |
| `EnglishG2PHandler` | `Runtime/Core/Phonemizers/Multilingual/Handlers/` | 英語G2P（EnglishG2PEngine委譲、CMU dict + LTS） |
| `SpanishG2PHandler` | `Runtime/Core/Phonemizers/Multilingual/Handlers/` | スペイン語G2P（SpanishG2PEngine委譲） |
| `FrenchG2PHandler` | `Runtime/Core/Phonemizers/Multilingual/Handlers/` | フランス語G2P（FrenchG2PEngine委譲） |
| `PortugueseG2PHandler` | `Runtime/Core/Phonemizers/Multilingual/Handlers/` | ポルトガル語G2P（PortugueseG2PEngine委譲） |
| `ChineseG2PHandler` | `Runtime/Core/Phonemizers/Multilingual/Handlers/` | 中国語G2P（ChineseG2PEngine委譲、44K文字辞書） |
| `KoreanG2PHandler` | `Runtime/Core/Phonemizers/Multilingual/Handlers/` | 韓国語G2P（KoreanG2PEngine委譲、Hangul分解 + 音韻規則） |
| `HandlerEntry` | `Runtime/Core/Phonemizers/Multilingual/` | ハンドラ + 所有権フラグの内部レジストリエントリ（internal readonly struct） |
| `G2PHandlerUtils` | `Runtime/Core/Phonemizers/Multilingual/Handlers/` | ハンドラ共通ユーティリティ（Prosody配列構築等） |
| `DotNetG2PPhonemizer` | `Runtime/Core/Phonemizers/Implementations/` | 日本語G2P実装（dot-net-g2p, Prosody対応） |
| `CustomDictionary` | `Runtime/Core/Phonemizers/` | カスタム辞書（技術用語・固有名詞の読み変換） |
| `PhonemeOptions` | `Runtime/Core/Phonemizers/Backend/` | 音素化リクエストオプション（共有型） |

#### 言語検出

| コンポーネント | 場所 | 役割 |
|--------------|------|------|
| `ILanguageDetector` | `Runtime/Core/Phonemizers/Multilingual/` | 言語検出インターフェース（public）。SegmentText() → (language, text)[] |
| `HybridLanguageDetector` | `Runtime/Core/Phonemizers/Multilingual/` | Unicode+Trigram複合言語検出（internal sealed）。Latin言語の曖昧さをTrigramで解消 |
| `UnicodeLanguageDetector` | `Runtime/Core/Phonemizers/Multilingual/` | Unicode文字範囲ベース言語検出（CJK, Hangul, Latin等） |
| `TrigramLanguageDetector` | `Runtime/Core/Phonemizers/Multilingual/` | Trigram頻度分析による言語検出（internal）。en/es/fr/pt区別 |
| `LatinSegmentRefiner` | `Runtime/Core/Phonemizers/Multilingual/` | Latin文字セグメントのTrigram精緻化（internal） |
| `LanguageConstants` | `Runtime/Core/Phonemizers/Multilingual/` | 言語ID/コード定数、IsLatinLanguage判定 |

#### 音素エンコーディング・PUAマッピング

| コンポーネント | 場所 | 役割 |
|--------------|------|------|
| `PuaTokenMapper` | `Runtime/Core/Phonemizers/Multilingual/` | PUA↔IPA双方向マッピング（インスタンスクラス）。FixedPuaMapping(96+固定エントリ) + pua.jsonランタイム読み込み対応。動的PUA割当機能あり |
| `PhonemeEncoder` | `Runtime/Core/AudioGeneration/` | 音素→モデルID変換（ProsodyFlat stride=3対応）。EncodeWithProsody / Encode / ProsodyStride=3定数 |

#### 音声生成パイプライン

| コンポーネント | 場所 | 役割 |
|--------------|------|------|
| `TTSSynthesisOrchestrator` | `Runtime/Core/AudioGeneration/` | 音素列→AudioClip変換パイプライン一元管理（internal sealed）。IPiperConfigReadOnly/PiperVoiceConfigをコンストラクタ注入 |
| `IInferenceAudioGenerator` | `Runtime/Core/AudioGeneration/` | ONNX推論インターフェース。GenerateAudioAsync(phonemeIds, prosodyFlat, ...) → NativeArray&lt;float&gt; |
| `InferenceAudioGenerator` | `Runtime/Core/AudioGeneration/` | ONNX推論実装（InferenceContext, ArrayPool, NativeArray出力） |
| `SplitInferenceOrchestrator` | `Runtime/Core/AudioGeneration/` | 沈黙句分割→反復推論→結合（internal class） |
| `AudioNormalizer` | `Runtime/Core/AudioGeneration/` | 音声正規化（public static class）。NativeArray&lt;float&gt; / float[] in-place正規化。GCアロケーションなし |
| `AudioClipBuilder` | `Runtime/Core/AudioGeneration/` | NativeArray&lt;float&gt;→AudioClip変換。float[]版は[Obsolete] |
| `BackendSelector` | `Runtime/Core/AudioGeneration/` | 推論バックエンド選択ロジック（public static class）。プリプロセッサフリー |
| `PlatformInfo` | `Runtime/Core/AudioGeneration/` | プラットフォーム依存情報カプセル化（public readonly struct）。FromCurrentEnvironment()ファクトリ |

#### DotNetG2Pパッケージ（外部）

| コンポーネント | パッケージ | 役割 |
|--------------|-----------|------|
| `EnglishG2PEngine` | DotNetG2P.English | 英語G2P（CMU dict + LTS + 同音異義語解決） |
| `SpanishG2PEngine` | DotNetG2P.Spanish | スペイン語G2P |
| `FrenchG2PEngine` | DotNetG2P.French | フランス語G2P |
| `PortugueseG2PEngine` | DotNetG2P.Portuguese | ポルトガル語G2P |
| `ChineseG2PEngine` | DotNetG2P.Chinese | 中国語G2P（44K文字辞書） |
| `KoreanG2PEngine` | DotNetG2P.Korean | 韓国語G2P（Hangul分解 + 音韻規則） |

#### デモ・その他

| コンポーネント | 場所 | 役割 |
|--------------|------|------|
| `InferenceEngineDemo` | `Runtime/Demo/` | テスト用デモUI（6言語ドロップダウン） |

### ディレクトリ構造
```
Assets/uPiper/
├── Runtime/
│   ├── Core/               # ランタイムコア
│   │   ├── AudioGeneration/    # AudioClip生成、ONNX推論、AudioNormalizer、BackendSelector
│   │   ├── Phonemizers/        # 音素化システム
│   │   │   ├── Backend/        # 共有型（PhonemeOptions）
│   │   │   ├── Implementations/# Prosody対応実装（DotNetG2PPhonemizer）
│   │   │   ├── Multilingual/   # 多言語共通(PuaTokenMapper, LanguageConstants, ILanguageDetector)
│   │   │   │   └── Handlers/   # ILanguageG2PHandler実装（7言語ハンドラ + G2PHandlerUtils）
│   │   │   └── (Backend/, Implementations/, Multilingual/ のみ)
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

### 言語バージョン
- C# 10.0（`Assets/csc.rsp` で `-langversion:10.0` を指定）
- `readonly record struct`、`global using`、`file-scoped namespace` 等が利用可能

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
| WebGL | GPUPixel / GPUCompute（Phase 1-4完了。WebGPU時はGPUCompute自動選択、WebGL2時はGPUPixel） |

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
Prosody対応モデル（multilingual-test-medium等）では、dot-net-g2p（MeCab辞書）から取得したアクセント情報を使用してより自然なイントネーションの音声を生成できる。

### Prosodyデータ形式
v2.0ではProsodyデータを **ProsodyFlat** (stride=3) 形式で統一管理する:
```
ProsodyFlat = [a1_0, a2_0, a3_0, a1_1, a2_1, a3_1, ...]
Length = Phonemes.Length * PhonemeEncoder.ProsodyStride (=3)
```

### Prosodyパラメータ
- **A1**: アクセント句内でのモーラ位置（0始まり）
- **A2**: アクセント句内のアクセント核位置（アクセント型）
- **A3**: 呼気段落（イントネーション句）内でのアクセント句位置

### 言語別Prosodyマッピング

| 言語 | A1 | A2 | A3 |
|------|----|----|-----|
| ja | モーラ位置 | アクセント核位置 | アクセント句位置 |
| en | 0 | 0 | 0 |
| zh | tone(1-5) | 音節位置 | 単語長 |
| ko | 0 | 0 | 音節数 |
| es/fr/pt | 0 | stress (0/2) | 語内音素数 |

### 使用方法（v2.0 API）
```csharp
// PhonemizeAsync → SynthesisRequest → SynthesizeAsync パイプライン
var result = await piperTTS.PhonemizeAsync("こんにちは");
// result.Phonemes: 音素配列
// result.ProsodyFlat: stride=3フラット配列 [a1_0,a2_0,a3_0, a1_1,a2_1,a3_1, ...]
// result.DetectedLanguage: "ja"
// result.ResolvedLanguageId: 0

// Prosody付きリクエスト構築
var request = SynthesisRequest.FromPhonemesWithProsody(
    result.Phonemes, result.ProsodyFlat, lengthScale: 0.8f);
var clip = await piperTTS.SynthesizeAsync(request);

// 音素直接入力（Prosodyなし）
var request2 = SynthesisRequest.FromPhonemes(
    new[] { "k", "o", "N_uvular", "n", "i", "ch", "w", "a" });
var clip2 = await piperTTS.SynthesizeAsync(request2);
```

### モデル判定
- `IInferenceAudioGenerator.SupportsProsody`: ONNXモデルがProsody入力をサポートしているかを返す
- Prosody対応モデルは `a1`, `a2`, `a3` 入力テンソルを持つ
- 統合API `GenerateAudioAsync(phonemeIds, prosodyFlat, ...)` でProsodyあり/なしを自動切替（prosodyFlat=nullでProsodyなし）

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

| モデルタイプ | 音素表現 | 例 |
|------------|---------|-----|
| **PUA (Private Use Area)** | Unicode私用領域文字 | `ch` → `\ue00e` (ID 39) |
| **IPA (International Phonetic Alphabet)** | 国際音声記号 | `ch` → `tɕ` (ID 32) |

### PhonemeEncoder の動作

`PhonemeEncoder`は初期化時にモデルの`phoneme_id_map`（`Dictionary<string, int[]>`型）を検査し、IPA文字（`ɕ`等）の有無で自動判定。`PuaTokenMapper`インスタンスをコンストラクタで受け取る。

```csharp
// コンストラクタ: PiperVoiceConfig + PuaTokenMapper
var encoder = new PhonemeEncoder(voiceConfig, puaTokenMapper);

// IPA判定: phoneme_id_mapに "ɕ" が含まれているか（multilingualモデルではスキップ）
_useIpaMapping = !_isMultilingualModel && _phonemeToId.ContainsKey("ɕ");
```

**ProsodyFlat stride=3 対応**:
- `EncodeWithProsody(phonemes, prosodyFlat)` → `ProsodyEncodingResult { PhonemeIds, ExpandedProsodyFlat }`
- BOS/EOS/PAD挿入に合わせてProsodyFlatを自動展開（境界にゼロ値を挿入）
- `ProsodyStride = 3` 定数を公開

**IPAモデルの場合**:
1. PUA文字を元の音素に逆変換（`\ue00e` → `ch`）
2. IPA音素に変換（`ch` → `tɕ`）
3. phoneme_id_mapでIDを取得

**PUAモデルの場合**:
1. PUA文字をそのまま使用
2. phoneme_id_mapでIDを取得

### PuaTokenMapper（多言語対応）

`PuaTokenMapper`はインスタンスクラスとして全7言語の音素に対する統一的なPUA↔IPAの双方向マッピングを提供する。

- **固定マッピング**: `FixedPuaMapping`（`IReadOnlyDictionary<string, int>`）に96+エントリをハードコード（0xE000〜0xE061）
- **pua.json ランタイム読み込み**: `InitializeAsync()` / `InitializeFromFile()` で `StreamingAssets/uPiper/pua.json` から動的ロード可能。copy-on-write で既存マッピングをアトミックに置換
- **動的PUA割当**: `Register(token)` で未登録の多文字トークンに新しいPUAコードポイントを自動割当（0xE062〜0xF8FF）
- **API**: `MapToken(token)`, `MapSequence(tokens)`, `UnmapChar(ch)`, `IsFixedPua(ch)`
- **スレッドセーフ**: `ConcurrentDictionary` + ロックベース動的割当

各DotNetG2Pエンジンの`ToPuaPhonemes()`メソッドが内部でPUA変換を行い、`ILanguageG2PHandler`はその結果をそのままモデルの`phoneme_id_map`と照合する。

### 主要な音素マッピング

| G2P出力 | PUA文字 | PUA ID | IPA音素 | IPA ID |
|--------------|---------|--------|---------|--------|
| `ch` (ち) | `\ue00e` | 39 | `tɕ` | 32 |
| `ts` (つ) | `\ue00f` | 40 | `ts` | 33 |
| `sh` (し) | `\ue010` | 42 | `ɕ` | 18 |
| `cl` (っ) | `\ue005` | 23 | `q` | 24 |
| `ky` (きゃ) | `\ue006` | 26 | `kʲ` | - |
| `N` (ん) | `N` | 22 | `ɴ` | 22 |

### 多言語モデルエンコーディング（phoneme_type: "multilingual"）

モデル設定の`phoneme_type`が`"multilingual"`の場合、PhonemeEncoderは以下の特殊動作を行う：

**Intersperse PAD挿入**:
多言語/espeakモデルではPhonemeEncoder処理後にPAD文字 (`_`, ID=0) を音素間に挿入する。BOS (`^`) の直後にもPADが入る：
```
通常モデル:   [^, phoneme1, phoneme2, ..., $]
多言語モデル: [^, _, phoneme1, _, phoneme2, _, ..., $]
```

**PUA文字パススルー**:
多言語モデルではPhonemeEncoderがPUA文字をIPA/PUA変換せずそのまま`phoneme_id_map`で検索する。PuaTokenMapperが事前に適切なPUA文字へ変換済みであるため、追加の変換は不要。

**N変種の保持**:
日本語の撥音「ん」は後続音素に応じて複数の異音を持つ。多言語モデルではこれらが区別されたIDを持つ：
- `N_m` — 唇音前の「ん」（例: さんぽ）
- `N_n` — 歯茎音前の「ん」（例: あんない）
- `N_ng` — 軟口蓋音前の「ん」（例: さんかく）
- `N_uvular` — その他の「ん」（語末等）

### デバッグ

PhonemeEncoderは初期化時に詳細なログを出力：
```
[PhonemeEncoder] PhonemeIdMap count: 58
[PhonemeEncoder] _useIpaMapping: True
[PhonemeEncoder] IPA key 'ɕ': found
[PhonemeEncoder] IPA key 'tɕ': found
```
