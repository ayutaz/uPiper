# dot-net-g2p 統合調査ドキュメント

> **Issue**: [#111 - dot-net-g2pの統合をする](https://github.com/ayutaz/uPiper/issues/111)
> **調査日**: 2026-03-05
> **ブランチ**: `feature/111-integrate-dot-net-g2p`

---

## 目次

1. [エグゼクティブサマリー](#1-エグゼクティブサマリー)
2. [dot-net-g2p ライブラリ概要](#2-dot-net-g2p-ライブラリ概要)
3. [uPiper現行アーキテクチャの分析](#3-upiper現行アーキテクチャの分析)
4. [統合の技術的分析](#4-統合の技術的分析)
5. [統合設計方針](#5-統合設計方針)
6. [段階的実装計画](#6-段階的実装計画)
7. [削除可能なコンポーネント](#7-削除可能なコンポーネント)
8. [リスク・課題](#8-リスク課題)
9. [dot-net-g2pライブラリ側の問題点・改善提案](#9-dot-net-g2pライブラリ側の問題点改善提案)
10. [まとめ](#10-まとめ)

---

## 1. エグゼクティブサマリー

### 統合の目的

[dot-net-g2p](https://github.com/ayutaz/dot-net-g2p) を uPiper に統合することで、**OpenJTalkネイティブプラグイン（C/C++ DLL/SO/DYLIB）への依存を排除**し、日本語G2P処理を**純粋C#実装**に置き換える。

### 統合の実現可能性: **高い**

| 項目 | 評価 |
|------|------|
| 音素互換性 | **完全互換** - OpenJTalkと同一の音素セットを出力 |
| Prosody対応 | **対応可能** - HTSフルコンテキストラベルからA1/A2/A3を抽出可能 |
| Unity互換性 | **対応済み** - .NET Standard 2.1、.asmdef完備、UnityEngine非依存 |
| 辞書互換性 | **完全互換** - uPiperの既存naist-jdic辞書をそのまま利用可能 |
| ライセンス | **問題なし** - Apache-2.0 |
| 英語G2P | **対象外** - dot-net-g2pは日本語専用。英語は既存Flite LTSを継続利用 |

### 主要な影響

- **ネイティブライブラリ（6プラットフォーム分）の配布・ビルドが不要**になる
- **ビルドサイズ削減**: ネイティブバイナリ分（プラットフォーム毎 数MB）が削除
- **CI/CD簡素化**: ネイティブビルドワークフロー（`build-openjtalk-native.yml`）が不要に
- **プラットフォーム対応の簡素化**: P/Invoke、NativeLibraryResolver、プラットフォーム別辞書パス解決が不要に

---

## 2. dot-net-g2p ライブラリ概要

### 基本情報

| 項目 | 内容 |
|------|------|
| リポジトリ | https://github.com/ayutaz/dot-net-g2p |
| バージョン | v1.1.0 |
| ライセンス | Apache-2.0 |
| ターゲット | .NET Standard 2.1 (Unity 2021.2+互換) |
| 外部依存 | **なし**（完全自己完結） |
| パッケージ | NuGet: `DotNetG2P` (89.66 KB) + `DotNetG2P.MeCab` (41.53 KB) |
| Unity対応 | .asmdef完備、`noEngineReferences: true` |

### パッケージ構成

```
DotNetG2P.Core (DotNetG2P namespace)
  ├── G2PEngine          - メインエントリーポイント
  ├── G2POptions          - 処理オプション
  ├── Models/             - AccentPhrase, Mora, NjdNode, POS等
  ├── NJD/                - 6段階NJD処理パイプライン
  ├── JPCommon/           - HTSフルコンテキストラベル生成
  ├── PhonemeConverter/   - 音素変換・Prosody抽出
  ├── TextNormalization/  - テキスト正規化
  └── Tokenizer/          - ITokenizer抽象インターフェース

DotNetG2P.MeCab (DotNetG2P.MeCab namespace)
  ├── MeCabTokenizer     - ITokenizer実装（純C# MeCabエンジン）
  ├── Dictionary/         - sys.dic, matrix.bin, char.bin, unk.dic読み込み
  ├── Lattice/            - ViterbiDecoder, LatticeBuilder
  └── Trie/               - DoubleArrayTrie
```

### 公開API

```csharp
// 初期化
using var tokenizer = new MeCabTokenizer(dictionaryPath);
using var engine = new G2PEngine(tokenizer);
// または
using var engine = new G2PEngine(tokenizer, new G2POptions(
    enableTextNormalization: true,
    enableUnvoicedVowel: true,
    enableDigitProcessing: true,
    enableAccentPhrase: true,
    enableAccentType: true,
    expandLongVowels: true
));

// 音素変換
string phonemes = engine.ToPhonemes("こんにちは");
// → "k o N n i ch i w a"

// カタカナ読み
string kana = engine.ToKana("東京都");
// → "トーキョート"

// Prosody (ESPnet韻律記号)
string prosody = engine.ToProsody("こんにちは");
// → "^ k o [ N n i ch i w a $"

// VOICEVOX互換AccentPhrase
IReadOnlyList<AccentPhrase> phrases = engine.ToAccentPhrases("こんにちは");

// HTSフルコンテキストラベル
IReadOnlyList<string> labels = engine.ToFullContextLabels("こんにちは");

// Prosody特徴量（A1/A2/A3）の直接取得 (v1.1.0で追加)
ProsodyFeatures features = engine.ToProsodyFeatures("こんにちは");
// features.Phonemes: IReadOnlyList<string> - 音素配列 ("sil", "k", "o", ...)
// features.A1: IReadOnlyList<int> - アクセント核からの相対位置
// features.A2: IReadOnlyList<int> - アクセント句内位置 (1始まり)
// features.A3: IReadOnlyList<int> - 後方からのモーラ位置

// NJDノード（高度なAPI）
IReadOnlyList<NjdNode> nodes = engine.Analyze("こんにちは");

// バッチ処理 (v1.1.0で追加)
IReadOnlyList<ProsodyFeatures> batch = engine.ToProsodyFeaturesBatch(texts);
```

**重要**: 全APIが**同期のみ**。これは正しい設計判断（全処理が100%CPUバウンドのため）。

### 処理パイプライン

```
テキスト入力
  → TextNormalizer.Normalize()         半角→全角、濁点結合等
  → MeCabTokenizer.Tokenize()          形態素解析（DoubleArrayTrie + Viterbi）
  → NjdNode.FromTokens()               中間表現構築
  → SetPronunciation.Process()          発音生成（辞書読み→カタカナ→モーラ）
  → DigitSequenceProcessor.Process()    数字列検出
  → SetDigit.Process()                  数字発音補正（助数詞音便等）
  → SetAccentPhrase.Process()           アクセント句結合（18ルール）
  → SetAccentType.Process()             アクセント結合型決定（C1-C5等）
  → SetUnvoicedVowel.Process()          無声音化（6ルール）
  → MoraMapping.MorasToPhonemeString()  最終音素文字列生成
```

### テスト・品質

- テストフレームワーク: xunit 2.5.3
- テストファイル: 27ファイル以上、8カテゴリ
- pyopenjtalk互換テスト（18ケース）: G2Pロジック自体は同等精度。差異は出力フォーマットの違い（無声母音の大文字表記 `U` vs `u`、促音 `cl` vs `q` 等）であり、正規化（大文字→小文字統一、長音記号→母音繰り返し変換）適用後に75%以上の一致を品質ゲートとして要求
- エッジケーステスト: 記号のみ、英語テキスト、空白のみ、500字超長文等
- パフォーマンステスト: 短文100回<5秒、標準文100回<10秒
- **ProsodyFeaturesテスト（v1.1.0追加）**: 7テスト - A値計算の正確性、HTSラベルとの一貫性、複数アクセント句の境界判定
- CI: GitHub Actions (ubuntu-latest, .NET 9.0.x)

---

## 3. uPiper現行アーキテクチャの分析

### 音素化インターフェース階層

uPiperには**2つの独立した音素化インターフェース階層**が存在する。

#### 階層A: IPhonemizer（PiperTTSから使用される主要インターフェース）

```
IPhonemizer
  └── BasePhonemizer (LRUキャッシュ、テキスト正規化内蔵)
        └── OpenJTalkPhonemizer (日本語、P/Invoke)
```

- `PiperTTS._phonemizer` フィールドの型
- **現在、日本語のみこの階層で初期化**（英語は `_phonemizer = null`）

#### 階層B: IPhonemizerBackend（プラグイン可能なバックエンド）

```
IPhonemizerBackend
  └── PhonemizerBackendBase
        └── FliteLTSPhonemizer (英語、純C#)
```

- **PiperTTSからは直接使用されていない**（テスト経由のみ）
- 英語TTSパスは**完全に未接続**

### OpenJTalkネイティブ統合の構成

| コンポーネント | ファイル | 役割 |
|--------------|---------|------|
| P/Invoke定義 | `Runtime/Core/Phonemizers/Native/OpenJTalkNative.cs` | ネイティブ関数宣言 |
| ネイティブバイナリ | `Plugins/Windows/x86_64/openjtalk_wrapper.dll` 他 | 6プラットフォーム分 |
| メインPhonemizer | `Runtime/Core/Phonemizers/Implementations/OpenJTalkPhonemizer.cs` | BasePhonemizer継承 |
| 音素マッピング | `Runtime/Core/Phonemizers/OpenJTalkToPiperMapping.cs` | OpenJTalk→Piper変換 |
| ライブラリ解決 | `Runtime/Core/Platform/NativeLibraryResolver.cs` | プラットフォーム別DLL検索 |
| C/C++ソース | `NativePlugins/OpenJTalk/` | ネイティブビルドソース |

### OpenJTalk音素化フロー

```
テキスト入力
  → CustomDictionary.ApplyToText()               カスタム辞書前処理
  → OpenJTalkNative.openjtalk_phonemize()         P/Invoke呼出し (ネイティブ)
  → Marshal.PtrToStringAnsi() + Split(' ')        結果マーシャリング
  → DetectRepeatedPatterns()                      Windows重複バグ対策
  → OpenJTalkToPiperMapping.ConvertToPiperPhonemes()  PUA変換
  → ApplyNPhonemeRules()                          N音素文脈依存変異
  → GetQuestionType() + 疑問マーカー追加           疑問文処理
  → PhonemeResult
```

### Prosodyフロー

```
テキスト入力
  → CustomDictionary.ApplyToText()
  → OpenJTalkNative.openjtalk_phonemize_with_prosody()  P/Invoke (Prosody付き)
  → Marshal.PtrToStructure<NativeProsodyPhonemeResult>()
  → 音素変換 (上記と同じ)
  → Marshal.Copy(prosody_a1/a2/a3)                      Prosody配列コピー
  → ProsodyResult { Phonemes[], ProsodyA1[], ProsodyA2[], ProsodyA3[] }
```

### ネイティブ側のProsody抽出

ネイティブC++コード (`openjtalk_full_wrapper.c`) は以下の処理を行う:
1. OpenJTalkの `JPCommon_make_label()` でHTSフルコンテキストラベルを生成
2. 各ラベルの `/A:a1+a2+a3/` をパースしてint配列に格納
3. ラベルの `-phoneme+` 部分から音素名を抽出

---

## 4. 統合の技術的分析

### 4.1 音素互換性: **完全互換**

| 音素カテゴリ | OpenJTalk出力 | dot-net-g2p出力 | 互換性 |
|------------|--------------|----------------|--------|
| 母音 | a, i, u, e, o | a, i, u, e, o | 完全一致 |
| 無声母音 | A, I, U, E, O | A, I, U, E, O | 完全一致 |
| 基本子音 | k, g, s, z, t, d, n, h, b, p, m, r, w, y | 同一 | 完全一致 |
| 複数文字子音 | ch, ts, sh, ky, gy, ny, hy, by, py, my, ry, dy, ty, gw, kw | 同一 | 完全一致 |
| 特殊音素 | N, cl, pau, sil | N, cl, pau, sil | 完全一致 |
| 出力形式 | スペース区切り文字列 | スペース区切り文字列 | 完全一致 |

dot-net-g2pはOpenJTalkのNJDパイプラインをC#で忠実に再実装しているため、**音素レベルでの追加マッピングは不要**。既存の `OpenJTalkToPiperMapping` がそのまま使える。

### 4.2 Prosody互換性: **直接対応（v1.1.0）**

v1.1.0で追加された `ToProsodyFeatures()` APIにより、Prosody A1/A2/A3を**構造化データとして直接取得**できる。HTSラベルのパースは不要。

#### ToProsodyFeatures API（v1.1.0で追加）

```csharp
ProsodyFeatures features = engine.ToProsodyFeatures("盆栽");
// features.Phonemes: ["sil", "b", "o", "N", "s", "a", "i", "sil"]
// features.A1:       [0,    -3,  -3,  -2,  -1,  -1,  0,   0]
// features.A2:       [0,     1,   1,   2,   3,   3,  4,   0]
// features.A3:       [0,     4,   4,   3,   2,   2,  1,   0]
// features.Count: 8 (全配列が同一長)
```

#### A値の計算ロジック (FullContextLabel.cs)

```csharp
int moraPos = current.MoraIndexInAP;  // 0始まり
int accent = NormalizeAccentForA(ap.AccentType, moraCount);
int a1 = moraPos - accent + 1;   // アクセント核からの相対位置 (signed)
int a2 = moraPos + 1;            // アクセント句内位置 (1始まり)
int a3 = moraCount - moraPos;    // 後方からのモーラ位置
```

- sil/pauのA値はすべて0
- A1は符号付き整数（-49〜+49、クランプ処理あり）
- 同一モーラ内の複数音素（例: "s","a"）は同じA値を共有

#### A3の定義差異（要検証）

| ソース | A3の定義 |
|--------|---------|
| uPiperネイティブコメント | "total morae in accent phrase"（アクセント句総モーラ数） |
| dot-net-g2p ToProsodyFeatures | `moraCount - moraPos`（後方からのモーラ位置） |
| OpenJTalk HTS仕様 | 後方からのモーラ位置 |

**piper-plusのネイティブ実装が実際にどちらの値を返しているか要検証**。HTSラベル仕様に準拠しているなら `moraCount - moraPos` であり、dot-net-g2pと一致する。

### 4.3 辞書互換性: **完全互換**

| 項目 | uPiper現行 | dot-net-g2p | 互換性 |
|------|-----------|-------------|--------|
| 辞書形式 | naist-jdic (MeCab形式) | 同一 | 完全互換 |
| 必須ファイル | sys.dic, matrix.bin, char.bin, unk.dic + left-id.def等 | sys.dic, matrix.bin, char.bin, unk.dic | **dot-net-g2pは4ファイルのみ** |
| 辞書サイズ | 107.2 MB (非圧縮) | 同一辞書を共有 | サイズ変更なし |
| 配置場所 | `StreamingAssets/uPiper/OpenJTalk/naist_jdic/` | コンストラクタでパス指定 | 同一パスを指定可能 |

uPiperの既存辞書をそのまま利用可能。dot-net-g2pでは `left-id.def`, `right-id.def`, `pos-id.def`, `rewrite.def` は不要（約167KB削減可能）。

### 4.4 英語G2P: **対象外**

dot-net-g2pは**日本語専用**。英語G2P機能は含まれていない。

uPiperの英語TTSは:
- 現状、`PiperTTS._phonemizer = null`（英語パスは未接続）
- FliteLTSPhonemizer（IPhonemizerBackend）はテスト用に存在するが本体未統合
- **dot-net-g2p統合とは独立して、英語G2P対応を別途実装する必要がある**

### 4.5 CustomDictionary互換性

- uPiperの `CustomDictionary`（JSON形式、技術用語の読み変換）はバックエンド非依存
- `OpenJTalkPhonemizer` が内部で `CustomDictionary.ApplyToText()` を呼び出している
- dot-net-g2pアダプターでも**同じパターンで統合可能**（テキスト前処理として適用）

---

## 5. 統合設計方針

### 5.1 新しいPhonemizer: `DotNetG2PPhonemizer`

`BasePhonemizer` を継承し、`OpenJTalkPhonemizer` と同じインターフェースを提供する。

```csharp
public class DotNetG2PPhonemizer : BasePhonemizer
{
    private G2PEngine _engine;
    private MeCabTokenizer _tokenizer;
    private CustomDictionary _customDictionary;

    public DotNetG2PPhonemizer(
        int cacheCapacity = 1000,
        string dictionaryPath = null,
        bool loadCustomDictionary = true)
    {
        // MeCabTokenizer + G2PEngine を初期化
        // CustomDictionary を読み込み
    }

    protected override PhonemeResult PhonemizeInternal(string text)
    {
        // 1. CustomDictionary.ApplyToText(text)
        // 2. engine.ToPhonemes(processedText)
        // 3. OpenJTalkToPiperMapping.ConvertToPiperPhonemes()
        // 4. ApplyNPhonemeRules()
        // 5. GetQuestionType() + 疑問マーカー追加
    }

    public ProsodyResult PhonemizeWithProsody(string text)
    {
        // 1. CustomDictionary.ApplyToText(text)
        // 2. engine.ToProsodyFeatures(processedText)  ← v1.1.0 API
        // 3. features.Phonemes / A1 / A2 / A3 を直接取得（パース不要）
        // 4. OpenJTalkToPiperMapping.ConvertToPiperPhonemes()
        // 5. ApplyNPhonemeRules()
        // 6. GetQuestionType() + 疑問マーカー追加
        // 7. ProsodyResult構築
    }
}
```

### 5.2 PiperTTSの言語ルーティング変更

```csharp
// Before (現行)
if (language == "ja" || language == "jp" || language == "japanese")
    _phonemizer = new OpenJTalkPhonemizer();
else
    _phonemizer = null;

// After (統合後)
if (language == "ja" || language == "jp" || language == "japanese")
    _phonemizer = new DotNetG2PPhonemizer();
else
    _phonemizer = null; // 英語対応は別Issue
```

### 5.3 保持するコンポーネント

以下は dot-net-g2p 統合後も**そのまま保持**する:

| コンポーネント | 理由 |
|--------------|------|
| `OpenJTalkToPiperMapping` | PUA変換ロジック（dot-net-g2pの出力に適用） |
| `PhonemeEncoder` | 音素→ID変換（モデル依存、バックエンド非依存） |
| `CustomDictionary` | テキスト前処理（バックエンド非依存） |
| `ApplyNPhonemeRules` | N音素文脈依存変異（バックエンド非依存） |
| `GetQuestionType` | 疑問文マーカー（バックエンド非依存） |
| `InferenceAudioGenerator` | ONNX推論（バックエンド非依存） |
| `FliteLTSPhonemizer` | 英語G2P（別系統、影響なし） |

### 5.4 非同期対応

dot-net-g2pのAPIは全て同期だが、これは正しい設計判断（実行時処理は100%CPUバウンドでawait対象がないため）。uPiperの非同期パイプラインへの統合は、呼び出し側で `Task.Run()` ラップする:

```csharp
protected override async Task<PhonemeResult> PhonemizeInternalAsync(
    string text, CancellationToken ct)
{
    return await Task.Run(() =>
    {
        lock (_engineLock)
        {
            return PhonemizeInternal(text);
        }
    }, ct);
}
```

`MeCabTokenizer` はスレッドセーフでないため、`lock` または `SemaphoreSlim` で排他制御が必要。

---

## 6. 段階的実装計画

### Phase 1: 基本統合（音素変換のみ）

1. dot-net-g2pパッケージをUPMで追加
2. `DotNetG2PPhonemizer` を `BasePhonemizer` 継承で実装
3. `ToPhonemes()` を使った基本音素変換
4. 既存の `OpenJTalkToPiperMapping` + `ApplyNPhonemeRules` + `GetQuestionType` を適用
5. `PiperTTS.InitializePhonemizerAsync()` で `DotNetG2PPhonemizer` を使用
6. 既存テストで音声生成品質を検証

### Phase 2: Prosody対応

1. `ToProsodyFeatures()` (v1.1.0 API) を使用した `PhonemizeWithProsody()` を実装
2. `ProsodyFeatures.Phonemes` / `A1` / `A2` / `A3` を直接利用（HTSラベルパース不要）
3. tsukuyomi-chanモデルでProsody音声生成を検証
4. A3値の定義差異を検証・調整

### Phase 3: ネイティブコード除去

1. `OpenJTalkPhonemizer` を `DotNetG2PPhonemizer` で完全に置き換え
2. 以下を削除:
   - `OpenJTalkNative.cs` (P/Invoke定義)
   - `NativeLibraryResolver.cs`
   - `Assets/uPiper/Plugins/` 配下の全ネイティブバイナリ
   - `NativePlugins/OpenJTalk/` (C/C++ソース)
   - `OpenJTalkDebugHelper.cs`
   - `OptimizedOpenJTalkPhonemizer.cs`
   - Android/iOS辞書パスリゾルバー
3. CI/CDから `build-openjtalk-native.yml` を削除
4. 不要な辞書ファイル (`left-id.def` 等) を削除

### Phase 4: テスト・最適化

1. 全プラットフォームでの動作確認
2. パフォーマンスベンチマーク（ネイティブ vs C#）
3. メモリ使用量の比較
4. Android/iOS特有の問題対応

---

## 7. 削除可能なコンポーネント

### ファイル一覧

| カテゴリ | ファイル/ディレクトリ | 理由 |
|---------|-------------------|------|
| P/Invoke | `Runtime/Core/Phonemizers/Native/OpenJTalkNative.cs` | 純C#で代替 |
| ネイティブバイナリ | `Plugins/Windows/x86_64/openjtalk_wrapper.dll` | 不要 |
| | `Plugins/Linux/x86_64/libopenjtalk_wrapper.so` | 不要 |
| | `Plugins/Linux/aarch64/libopenjtalk_wrapper.so` | 不要 |
| | `Plugins/macOS/openjtalk_wrapper.bundle/` | 不要 |
| | `Plugins/iOS/libopenjtalk_wrapper.a` | 不要 |
| | `Plugins/Android/libs/*/libopenjtalk_wrapper.so` | 不要 |
| C/C++ソース | `NativePlugins/OpenJTalk/` (全体) | ビルド不要 |
| プラットフォーム | `Runtime/Core/Platform/NativeLibraryResolver.cs` | ネイティブDLL検索不要 |
| | `Runtime/Core/Platform/AndroidPathResolver.cs` | APK→永続ストレージコピー不要 |
| | `Runtime/Core/Platform/IOSPathResolver.cs` | iOSパス解決不要 |
| 最適化版 | `Runtime/Core/Phonemizers/Implementations/OptimizedOpenJTalkPhonemizer.cs` | ネイティブ最適化版不要 |
| デバッグ | `Runtime/Core/Phonemizers/OpenJTalkDebugHelper.cs` | ネイティブデバッグ不要 |
| CI/CD | `.github/workflows/build-openjtalk-native.yml` | ネイティブビルド不要 |
| 辞書（部分） | `StreamingAssets/uPiper/OpenJTalk/naist_jdic/left-id.def` | C# MeCabで不使用 |
| | `StreamingAssets/uPiper/OpenJTalk/naist_jdic/right-id.def` | C# MeCabで不使用 |
| | `StreamingAssets/uPiper/OpenJTalk/naist_jdic/pos-id.def` | C# MeCabで不使用 |
| | `StreamingAssets/uPiper/OpenJTalk/naist_jdic/rewrite.def` | C# MeCabで不使用 |

### 保持するもの

| ファイル | 理由 |
|---------|------|
| `OpenJTalkToPiperMapping.cs` | PUA変換ロジック（名前変更検討） |
| `OpenJTalkPhonemizer.cs` | Phase 3完了まで並存、その後削除 |
| `StreamingAssets/uPiper/OpenJTalk/naist_jdic/sys.dic` | dot-net-g2pが使用 |
| `StreamingAssets/uPiper/OpenJTalk/naist_jdic/matrix.bin` | dot-net-g2pが使用 |
| `StreamingAssets/uPiper/OpenJTalk/naist_jdic/char.bin` | dot-net-g2pが使用 |
| `StreamingAssets/uPiper/OpenJTalk/naist_jdic/unk.dic` | dot-net-g2pが使用 |

---

## 8. リスク・課題

### 技術的リスク

| リスク | 影響度 | 対策 |
|--------|-------|------|
| A3値の定義差異（総モーラ vs 後方位置） | 中 | piper-plusネイティブ実装を検証し、必要に応じて計算式を調整 |
| パフォーマンス劣化（C# vs C++ネイティブ） | 中 | ベンチマークで比較、必要なら最適化 |
| MeCab辞書読み込み速度 | 低 | WeakReferenceキャッシュ活用、遅延初期化 |
| スレッドセーフティ | 中 | MeCabTokenizerはスレッドセーフでない。lock/SemaphoreSlimで排他制御 |
| pyopenjtalkとの出力フォーマット差異 | 低 | G2Pロジック自体は同等精度。差異は表記スタイル（無声母音の大文字/小文字、促音 `cl`/`q`）のみで、uPiperの `OpenJTalkToPiperMapping` で吸収される |

### プラットフォーム固有リスク

| プラットフォーム | リスク | 対策 |
|---------------|--------|------|
| Android | StreamingAssetsからのFile.Read不可 | 既存の `AndroidPathResolver` パターン（`persistentDataPath` へコピー）で対応可能 |
| WebGL | File.Read不可 | 現行もWebGL未対応のため影響なし |
| iOS | 特記事項なし | StreamingAssetsからの読み取りは可能 |

### 品質リスク

| リスク | 対策 |
|--------|------|
| 新しいライブラリ（v1.1.0） | uPiper側で十分なテストを実施。v1.1.0でProsodyFeatures APIとテストが大幅に追加済み |
| N音素変異ルールの互換性 | dot-net-g2pはNを単一出力。`ApplyNPhonemeRules`はuPiper側で継続適用 |
| 疑問文マーカーの互換性 | dot-net-g2pには該当機能なし。`GetQuestionType`はuPiper側で継続適用 |
| Windows重複バグ | C#実装では発生しない（ネイティブ固有のバグ）。対策コード削除可能 |

---

## 9. dot-net-g2pライブラリ側の問題点・改善提案

### 9.1 ~~Prosody A1/A2/A3の直接取得API~~ → **v1.1.0で対応済み**

**v1.1.0で `ToProsodyFeatures()` APIが追加**され、音素レベルのProsody情報（A1/A2/A3）を構造化データとして直接取得可能になった。バッチ処理版 `ToProsodyFeaturesBatch()` も同時に追加。

```csharp
ProsodyFeatures features = engine.ToProsodyFeatures("こんにちは");
// features.Phonemes, features.A1, features.A2, features.A3
```

7つのユニットテスト（`ProsodyFeaturesTests`）でA値計算の正確性が検証済み。

### 9.2 非同期APIについて

**結論: 非同期APIは不要（現状の同期APIが正しい設計）**

dot-net-g2pの実行時API（`ToPhonemes`, `ToProsody`等）は100%CPUバウンド処理（メモリ内計算のみ、I/O待ちなし）であり、`async/await`にしてもawaitする対象がない。むしろState Machine生成やヒープアロケーションのオーバーヘッドがデメリットになる。

辞書読み込み（初期化）は同期ファイルI/Oだが、アプリ起動時1回のみ（WeakReferenceキャッシュで共有）のため、非同期化の必要性は低い。

uPiper側で非同期パイプラインに組み込む場合は、呼び出し側で `Task.Run()` でラップすれば十分:
```csharp
var phonemes = await Task.Run(() => engine.ToPhonemes(text), ct);
```

### 9.3 ~~MeCabTokenizerのスレッドセーフティ~~ → **v1.1.0で対応済み**

**v1.1.0でスレッドセーフティのドキュメントが包括的に整備**された:

- `G2PEngine`: XMLドキュメントに「スレッドセーフではない、スレッドごとにインスタンスを作成」と明記
- `MeCabTokenizer`: 同様のXMLドキュメント + WeakReferenceキャッシュによるメモリ効率の説明
- `LatticeBuilder`: 内部バッファ再利用が非スレッドセーフの原因であることを記載
- `DictionaryBundle`: `Load()`/`Dispose()` はスレッドセーフであり、辞書データの読み取り専用共有が安全であることを明記
- `ITokenizer`: インターフェースレベルで排他制御の責任を呼び出し側に明示
- README: ユーザー向けスレッドセーフティセクションを追加

uPiper側では `lock` または `SemaphoreSlim` で排他制御するか、スレッド毎にインスタンスを作成する。

### 9.4 辞書読み込みのFileSystem前提について

**現状**: `DictionaryBundle.Load()` は `FileStream` + `BinaryReader` でファイルシステムからの読み込みのみ対応。

**uPiper統合への影響: なし**。uPiperは既にAndroid向けに `AndroidPathResolver` で StreamingAssets → `Application.persistentDataPath` へのコピーパターンを確立済み。dot-net-g2pの `MeCabTokenizer` はコピー後のファイルパスを受け取れば正常に動作する。

**改善提案（優先度: 低）**: dot-net-g2pを単体で使う場合や、将来的にWebGL等のファイルシステムが使えない環境をサポートする際には `Stream` ベースのAPIが有用だが、uPiper統合では不要。

### 9.5 Unity向け辞書セットアップについて

**uPiper統合への影響: なし**。uPiperは既にnaist-jdic辞書をPackage Managerサンプル経由で配布済み（`Assets/StreamingAssets/uPiper/OpenJTalk/naist_jdic/`）。dot-net-g2p統合後も同一辞書を共有するため、uPiperユーザーにとって追加のセットアップは不要。

**dot-net-g2p単体ユーザー向けの改善提案（優先度: 低）**: naist-jdic辞書の配布・セットアップ支援はdot-net-g2pを単独で使用するUnity開発者にとって有益だが、uPiper統合プロジェクトとは直接関係しない。

### 9.6 長音展開オプションのデフォルト値

**結論: 現状のデフォルト値（`expandLongVowels = true`）は正しい**

OpenJTalkのC実装では `njd_set_long_vowel` は実際にはdeprecatedで無効化されている（即座にreturn）が、pyopenjtalkの出力では長音は母音繰り返しで表記されており（例: `"t o o ky o o"`）、Piperモデルもこの形式を期待する。現状のデフォルト値はOpenJTalk互換であり変更不要。

**改善提案（優先度: 低）**: `expandLongVowels` オプションが存在しfalseに切り替え可能であることをドキュメントで明記する程度で十分。

### 9.7 テストカバレッジの可視化

**現状の問題**: `coverlet.collector` がテストプロジェクトに組み込まれているが、CIワークフロー（`ci.yml`）でカバレッジ収集・レポート生成・閾値チェックが設定されていない。

**改善提案（優先度: 中）**: GitHub ActionsにCodecov等へのカバレッジアップロードを追加。OSS公開ライブラリとしてカバレッジ可視化は信頼性の指標となり、uPiper統合時のバグ発見にも寄与する。

### 9.8 `dotnet build` の `obj/` ディレクトリがUnityコンパイルと競合する

**現状の問題**: dot-net-g2pをUPMローカルパッケージ（`file:` プロトコル）としてUnityから参照した場合、`dotnet build` や `dotnet test` の実行で `src/DotNetG2P.Core/obj/Release/netstandard2.1/` 等に自動生成される `AssemblyInfo.cs` をUnityがコンパイル対象に含めてしまい、重複アセンブリ属性エラー（CS0579）が9件発生する。

**エラー例**:
```
error CS0579: Duplicate 'System.Reflection.AssemblyCompanyAttribute' attribute
error CS0579: Duplicate 'System.Reflection.AssemblyConfigurationAttribute' attribute
（他7件）
```

**原因**: Unityは UPM パッケージディレクトリ内の全 `.cs` ファイルをコンパイル対象にするため、`obj/` 内の自動生成ファイルも含まれる。`.gitignore` で `obj/` を除外しても、ディスク上に存在すればUnityはスキャンする。

**推奨対策**: `Directory.Build.props` でビルド出力先をパッケージディレクトリ外にリダイレクトする。

```xml
<BaseIntermediateOutputPath>$(MSBuildThisFileDirectory).build\obj\$(MSBuildProjectName)\</BaseIntermediateOutputPath>
<BaseOutputPath>$(MSBuildThisFileDirectory).build\bin\$(MSBuildProjectName)\</BaseOutputPath>
```

これにより `dotnet build` / `dotnet test` を実行しても、`obj/` / `bin/` がパッケージディレクトリ内に作られなくなる。

### 9.9 ユーザー辞書（usr.dic）のサポート

**現状**: MeCabのユーザー辞書（type=1）のサポートが未実装。`DictionaryBundle` はsys.dic/matrix.bin/char.bin/unk.dicの4ファイルのみ読み込む。

**uPiper統合での影響**: uPiperには既に `CustomDictionary`（JSON形式、テキストレベルの正規表現ベース置換）が存在し、技術用語の読み変換はこれでカバー済み。ただし、複合語の分割制御やアクセント精度向上には形態素解析レベルの辞書追加が有用。

**改善提案（優先度: 低）**: `MeCabTokenizer` にユーザー辞書読み込みオプションの追加。uPiper統合で必須ではないが、将来的な音声品質向上に寄与する可能性がある。

---

## 将来展望: 英語G2Pの統合可能性

dot-net-g2pのIssue #1で「espeak-ngと同等の精度の英語G2P for C#」が計画されている。実装された場合、uPiperの現行FliteLTSバックエンドを置き換える選択肢となる可能性がある。

| 項目 | 現状 | 将来 |
|------|------|------|
| uPiper英語G2P | FliteLTS（WFST、限定的な精度） | dot-net-g2p英語G2P（espeak-ng相当）に置き換え可能 |
| 音素体系 | ARPABET | eSpeak（Piperモデルとの親和性が高い） |

---

## 10. まとめ

### 統合の実現可能性

dot-net-g2p の uPiper への統合は**技術的に十分に実現可能**。音素互換性が完全であり、辞書も共有可能なため、主な作業は `DotNetG2PPhonemizer` アダプターの実装とProsodyパーサーの追加に集約される。

### 主な利点

1. **ネイティブプラグイン依存の排除** - 6プラットフォーム分のバイナリ配布・ビルドが不要
2. **CI/CD簡素化** - ネイティブビルドワークフローの削除
3. **デバッグ容易性** - C#コードのため、ブレークポイント設定やステップ実行が容易
4. **プラットフォーム対応の簡素化** - P/Invoke、DLLパス解決、プラットフォーム別条件分岐の削除
5. **Windows重複バグの解消** - ネイティブ固有のバグが発生しない

### 注意点

1. **英語G2Pは対象外** - 別途対応が必要
2. **A3値の定義差異** - piper-plusネイティブとdot-net-g2pの値を比較検証が必要
3. **パフォーマンス検証が必要** - C# vs C++ネイティブの速度比較
4. **Androidの辞書アクセス** - FileSystem前提のため既存のpersistentDataPathコピーパターンを適用

### 推奨アクション

1. **uPiper側**: Phase 1（基本統合）から段階的に進める。dot-net-g2p v1.1.0で統合に必要なAPIが揃っている
2. **dot-net-g2pライブラリ側**: `obj/`ビルド出力リダイレクト（9.8）とCIカバレッジ可視化（9.7）が残課題。9.1（ToProsodyFeatures）と9.3（スレッドセーフティドキュメント）はv1.1.0で対応済み
3. 各Phaseで十分なテスト・ベンチマークを実施
4. piper-plusネイティブのA3定義を検証してからPhase 2に着手
