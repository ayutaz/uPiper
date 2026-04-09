# uPiper アーキテクチャドキュメント

## 概要

uPiperは、Unity環境でPiper TTSを使用するためのプラグインです。ニューラルネットワークベースの音声合成（VITS）を採用し、高品質な多言語音声合成を実現しています。C# 10.0（`csc.rsp -langversion:10.0`）を使用。

## アーキテクチャ概要

```
┌─────────────────┐     ┌──────────────────┐     ┌──────────────────────┐
│   Text Input    │ --> │ CustomDictionary │ --> │ MultilingualPhonemizer│
│  (7 languages)  │     │ (前処理)         │     │ (ILanguageG2PHandler) │
└─────────────────┘     └──────────────────┘     └──────────────────────┘
                                                           │
                                                           ↓
                        ┌──────────────────┐     ┌──────────────────────┐
                        │ PuaTokenMapper   │ --> │ PhonemeEncoder       │
                        │ (pua.json対応)    │     │ (ProsodyFlat stride=3)│
                        └──────────────────┘     └──────────────────────┘
                                                           │
                                                           ↓
                        ┌──────────────────────────────────────────────┐
                        │ TTSSynthesisOrchestrator                     │
                        │   → IInferenceAudioGenerator (NativeArray)  │
                        │   → AudioNormalizer → AudioClipBuilder      │
                        └──────────────────────────────────────────────┘
                                                           │
                                                           ↓
                                                  ┌─────────────────┐
                                                  │  AudioClip      │
                                                  │  (22050Hz)      │
                                                  └─────────────────┘
```

## コンポーネント詳細

### 1. テキスト入力層

- **日本語**: 漢字・ひらがな・カタカナ混じりのテキスト（DotNetG2P / MeCab辞書）
- **英語**: アルファベットテキスト（DotNetG2P.English / CMU辞書 + LTS + 同形異義語解決）
- **スペイン語**: DotNetG2P.Spanish（ルールベースG2P）
- **フランス語**: DotNetG2P.French（ルールベースG2P）
- **ポルトガル語**: DotNetG2P.Portuguese（ルールベースG2P、ブラジル変種）
- **中国語**: DotNetG2P.Chinese（44K文字 + 412Kフレーズ辞書）
- **韓国語**: DotNetG2P.Korean（Hangul分解 + 音韻規則）

### 2. 音素化層（Phonemizer）

#### MultilingualPhonemizer
- **場所**: `Runtime/Core/Phonemizers/Multilingual/MultilingualPhonemizer.cs`
- **役割**: テキストを言語検出で分割し、ILanguageG2PHandler Strategyで各言語のG2Pエンジンに委譲
- **コンストラクタ**: `MultilingualPhonemizerOptions` を受け取り、languages/defaultLatinLanguage/enableTrigramDetection/LanguageDetector/handlersを設定
- **InitializeAsync**: 未登録言語のデフォルトハンドラ生成 → 全ハンドラ初期化 → Trigram検出有効時にHybridLanguageDetectorへアップグレード

##### 言語検出（ILanguageDetector）

| 実装 | 場所 | 役割 |
|------|------|------|
| `ILanguageDetector` | `Multilingual/` | 言語検出インターフェース（public）。`SegmentText()` → `IReadOnlyList<(string language, string text)>` |
| `UnicodeLanguageDetector` | `Multilingual/` | Unicode文字範囲ベース言語検出（CJK, Hangul, Latin等）。デフォルト |
| `HybridLanguageDetector` | `Multilingual/` | Unicode + Trigram複合言語検出（internal sealed）。Latin言語の曖昧さをTrigramで解消 |
| `TrigramLanguageDetector` | `Multilingual/` | Trigram頻度分析による言語検出（internal sealed class）。en/es/fr/pt区別 |
| `LatinSegmentRefiner` | `Multilingual/` | Latin文字セグメントのTrigram精緻化（internal） |

##### ILanguageG2PHandler Strategy パターン

言語別G2P処理は `ILanguageG2PHandler` インターフェースで統一:

```csharp
public interface ILanguageG2PHandler : IDisposable
{
    string LanguageCode { get; }
    bool IsInitialized { get; }
    Task InitializeAsync(CancellationToken cancellationToken = default);
    (string[] Phonemes, int[] ProsodyFlat) Process(string text);
}
```

- **ProsodyFlat**: stride=3 フラット配列 `[a1_0, a2_0, a3_0, a1_1, a2_1, a3_1, ...]`
- **HandlerEntry**: `internal readonly struct`。ハンドラ + `IsOwned` 所有権フラグを保持

| ハンドラ | 言語 | 委譲先 |
|---------|------|--------|
| `JapaneseG2PHandler` | ja | DotNetG2PPhonemizer（MeCab辞書、Prosody対応） |
| `EnglishG2PHandler` | en | EnglishG2PEngine（CMU dict + LTS + 同形異義語解決） |
| `SpanishG2PHandler` | es | SpanishG2PEngine（ルールベースG2P） |
| `FrenchG2PHandler` | fr | FrenchG2PEngine（ルールベースG2P） |
| `PortugueseG2PHandler` | pt | PortugueseG2PEngine（ルールベースG2P、ブラジル変種） |
| `ChineseG2PHandler` | zh | ChineseG2PEngine（44K文字 + 412Kフレーズ辞書） |
| `KoreanG2PHandler` | ko | KoreanG2PEngine（Hangul分解 + 音韻規則） |

##### MultilingualPhonemizeResult

```csharp
public class MultilingualPhonemizeResult
{
    public string[] Phonemes { get; }
    public int[] ProsodyFlat { get; }        // stride=3, Length = Phonemes.Length * 3
    public string DetectedPrimaryLanguage { get; }
    public bool HasProsody => ProsodyFlat != null;
}
```

#### dot-net-g2p（日本語）
- **役割**: 日本語テキストを音素列に変換
- **実装**: 純粋C#実装（MeCab辞書使用）
- **辞書**: mecab-naist-jdic（789,120エントリ）を使用
- **処理フロー**:
  ```
  テキスト → MeCab解析 → G2P変換 → 音素列
  ```

#### 重要な設計判断：音素タイミング
```csharp
// 全ての音素に50ms固定の継続時間を設定
// VITSモデルがDuration Predictorで自動的に最適化するため
```

**理由**:
1. VITSモデルはDuration Predictorを内蔵
2. 入力された継続時間は参考程度に使用される
3. 実際のタイミングはモデルが自動的に最適化
4. HTS Engineとの統合は不要（Piperがニューラル音声合成のため）

### 3. 音素エンコーディング層

#### PuaTokenMapper（インスタンスクラス）

複数文字の音素を単一のUnicode PUA文字にマッピング:

```csharp
// 例：「きょう」の処理
"ky" + "o" + "u" → "\ue006" + "o" + "u"
```

- **固定マッピング**: `FixedPuaMapping`（`IReadOnlyDictionary<string, int>`、96固定エントリ、0xE000〜0xE061）
- **pua.json読み込み**: `InitializeAsync()` / `InitializeFromFile()` で `StreamingAssets/uPiper/pua.json` から動的ロード。copy-on-writeでアトミックに置換
- **動的割当**: `Register(token)` で未登録トークンに新PUAコードポイントを自動割当（0xE062〜0xF8FF）
- **スレッドセーフ**: `ConcurrentDictionary` + ロックベース動的割当

#### PhonemeEncoder

- **場所**: `Runtime/Core/AudioGeneration/PhonemeEncoder.cs`
- **コンストラクタ**: `PiperVoiceConfig` + `PuaTokenMapper` を受け取る
- **ProsodyFlat stride=3 対応**: `EncodeWithProsody(phonemes, prosodyFlat)` → `ProsodyEncodingResult { PhonemeIds, ExpandedProsodyFlat }`
- **BOS/EOS/PAD展開**: BOS/EOS/PADトークン挿入に合わせてProsodyFlatを自動展開（境界にゼロ値を挿入）
- **`ProsodyStride = 3`**: 公開定数。SynthesisRequest構築時に使用
- **モデルタイプ自動判定**:
  - IPA判定: `_useIpaMapping = !_isMultilingualModel && _phonemeToId.ContainsKey("ɕ")`
  - 多言語判定: `_isMultilingualModel` で `phoneme_type: "multilingual"` を検出
  - 多言語モデルではPUA文字パススルー（IPA/PUA変換なし）

### 3.5 設定管理層

#### IPiperConfigReadOnly

```csharp
public interface IPiperConfigReadOnly
{
    LanguageSettings Language { get; }
    PerformanceSettings Performance { get; }
    InferenceSettings Inference { get; }
    PiperAudioSettings Audio { get; }
    SilenceSettings Silence { get; }
    GeneralSettings General { get; }
}
```

#### ValidatedPiperConfig
- **場所**: `Runtime/Core/ValidatedPiperConfig.cs`
- **役割**: `PiperConfig.ToValidated()` で生成される不変（immutable）の設定スナップショット。`IPiperConfigReadOnly` を実装
- **6つのネスト readonly record struct**: `LanguageSettings`, `PerformanceSettings`, `InferenceSettings`, `PiperAudioSettings`, `SilenceSettings`, `GeneralSettings`
- **取得方法**: `PiperConfig.ToValidated()` を呼び出すと検証済みの `ValidatedPiperConfig` が返る。`PiperTTS` 内部では `_validatedConfig` として保持される
- **純粋関数 `ToValidated()`**: `PiperConfig` のフィールドを一切変更しない。クランプ・正規化・自動検出は `ValidatedPiperConfig` コンストラクタ内で実行
- **GPUSettings の不変性**: 防御的コピーにより保証
- **主要プロパティ例**:
  - `Silence.ParsedPhonemeSilence: IReadOnlyDictionary<string, float>` — `EnablePhonemeSilence=true` 時にパース済みマップを提供
  - `Audio.NormalizeAudio`, `Audio.SampleRate` — AudioNormalizerの動作制御
  - `Inference.Backend` — BackendSelectorへの入力

### 4. 音声合成層（VITS Model）

#### Unity.InferenceEngine統合
- **モデル形式**: ONNX
- **推論エンジン**: Unity.InferenceEngine（旧Sentis）
- **入力**: 音素ID配列 + オプションProsodyFlat
- **出力**: 音声波形（`NativeArray<float>`）

#### VITSアーキテクチャ
```
音素ID → TextEncoder → Duration Predictor → Flow Decoder → 音声波形
         ↓
         潜在表現 → Stochastic Duration Predictor
                    （音素タイミング自動推定）
```

#### BackendSelector + PlatformInfo
- **場所**: `Runtime/Core/AudioGeneration/BackendSelector.cs`
- **BackendSelector**: 推論バックエンド選択ロジック（`public static class`）。`Determine(requested, platform, gpuMemoryThresholdMB)` → `BackendType`
- **PlatformInfo**: プラットフォーム依存情報カプセル化（`public readonly struct`）。`FromCurrentEnvironment()` ファクトリでプリプロセッサ条件を閉じ込め
- **プリプロセッサフリー**: `Determine()` は `PlatformInfo` のフィールドのみで分岐。テスト容易

#### IInferenceAudioGenerator
- **場所**: `Runtime/Core/AudioGeneration/IInferenceAudioGenerator.cs`
- **統合API**: `GenerateAudioAsync(phonemeIds, prosodyFlat, ...)` → `NativeArray<float>`。`prosodyFlat=null` でProsodyなしパス
- **NativeArray出力**: 呼び出し元がDisposeする責任を持つ

#### InferenceAudioGenerator と InferenceContext
- **場所**: `Runtime/Core/AudioGeneration/InferenceAudioGenerator.cs`
- **出力テンソル名キャッシュ**: 初期化時に `_model.outputs[0].name` を `_cachedOutputName` にキャッシュ
- **Prosodyテンソル構築**: `new int[prosodySize]` で直接アロケーション（Tensorコンストラクタが正確な配列サイズを要求するためArrayPoolは未使用）
- **InferenceContext** (`private sealed class`、`IDisposable`):
  - `using var ctx = PrepareInputs(...)` パターンにより全入力テンソルを一括解放

#### TTSSynthesisOrchestrator
- **場所**: `Runtime/Core/AudioGeneration/TTSSynthesisOrchestrator.cs`（`internal sealed`）
- **役割**: 音素列 → `AudioClip` 変換パイプライン全体を一元管理
- **コンストラクタ**: `IInferenceAudioGenerator`, `SplitInferenceOrchestrator`, `PhonemeEncoder`, `AudioClipBuilder`, `IPiperConfigReadOnly`（nullable）, `PiperVoiceConfig` を受け取る
- **NativeArrayパイプライン**:
  1. **PhonemeEncoder** — 音素をIDにエンコード（`EncodeWithProsody` / `Encode`）
  2. **IInferenceAudioGenerator** — ONNX推論 → `NativeArray<float>`
  3. **AudioNormalizer** — `NativeArray<float>` in-place正規化（設定で無効化可能）
  4. **AudioClipBuilder** — `NativeArray<float>` → `AudioClip`（managed marshallingなし）
  5. **NativeArray Dispose** — `finally` で `audioData.Dispose()`（AudioClip.SetDataはコピー済み）

#### SynthesisRequest（public readonly struct）

```csharp
public readonly struct SynthesisRequest
{
    public string[] Phonemes { get; }
    public int[] ProsodyFlat { get; }    // stride=3, null可
    public float LengthScale { get; }
    public float NoiseScale { get; }
    public float NoiseW { get; }
    public int SpeakerId { get; }
    public int LanguageId { get; }
    public bool HasProsody => ProsodyFlat != null;
}
```

- **ファクトリメソッド**:
  - `FromPhonemes(phonemes, ...)` — Prosodyなし（防御的コピーあり）
  - `FromPhonemesWithProsody(phonemes, prosodyFlat, ...)` — Prosody付き（防御的コピーあり）
  - `CreateInternal(...)` — 内部用（防御的コピーなし）

#### PhonemizeResult（public sealed class）

```csharp
public sealed class PhonemizeResult
{
    public string[] Phonemes { get; }
    public int[] ProsodyFlat { get; }
    public string DetectedLanguage { get; }
    public int ResolvedLanguageId { get; }
    public bool HasProsody => ProsodyFlat != null;
}
```

#### SplitInferenceOrchestrator
- **場所**: `Runtime/Core/AudioGeneration/SplitInferenceOrchestrator.cs`（`internal class`）
- **役割**: 沈黙句分割のオーケストレーション。音素列を沈黙トークンの位置で分割し、句ごとに独立推論を行い、句間にゼロサンプルの無音区間を挿入して結合する
- **パラメータ**: `phonemeSilence` は `GenerateWithSilenceSplitAsync()` メソッドの引数として `IReadOnlyDictionary<string, float>` で受け取る（コンストラクタ引数ではない）

#### AudioNormalizer
- **場所**: `Runtime/Core/AudioGeneration/AudioNormalizer.cs`
- **`public static class`**: GCアロケーションなしの音声正規化
- **API**:
  - `NormalizeInPlace(NativeArray<float>, targetPeak)` — GCゼロ、NativeArrayを直接変更
  - `NormalizeInPlace(float[], targetPeak)` — float[]版in-place
  - `Normalize(float[], targetPeak)` — 新しい配列で返す（非破壊）

#### AudioClipBuilder
- **場所**: `Runtime/Core/AudioGeneration/AudioClipBuilder.cs`
- **`public class`**: `NativeArray<float>` → AudioClip変換（推奨）、`float[]` → AudioClip（`[Obsolete]`）

### 5. 音声出力層

- **AudioClipBuilder**: `NativeArray<float>` からUnity AudioClipを生成（`float[]` 版は `[Obsolete]`）
- **AudioNormalizer**: `NativeArray<float>` in-place正規化（GCアロケーションなし）
- **サンプルレート**: 22050Hz（標準）
- **NativeArrayライフサイクル**: `TTSSynthesisOrchestrator.SynthesizeAsync` の `finally` で `Dispose`

## データフロー例

### 日本語テキスト「こんにちは」の処理

1. **入力**: "こんにちは"

2. **ILanguageG2PHandler.Process()** (JapaneseG2PHandler → DotNetG2PPhonemizer):
   ```
   Phonemes: [k, o, N_uvular, n, i, ch, i, w, a, $]
   ProsodyFlat: [0,2,1, 1,2,1, 2,2,1, 3,2,1, 4,2,1, 5,2,1, 6,2,1, 7,2,1, 8,2,1, 0,0,0]
   ```

3. **PhonemeEncoder.EncodeWithProsody()**:
   ```
   PhonemeIds: [BOS, PAD, id_k, PAD, id_o, PAD, ..., EOS]
   ExpandedProsodyFlat: [0,0,0, 0,0,0, 0,2,1, 0,0,0, 1,2,1, ...]  (BOS/PADにゼロ挿入)
   ```

4. **IInferenceAudioGenerator.GenerateAudioAsync()**:
   - 入力: phoneme_ids + prosodyFlat → a1, a2, a3テンソルに分離
   - 出力: `NativeArray<float>` (音声波形)

5. **AudioNormalizer → AudioClipBuilder → AudioClip**

## Prosody（韻律）サポート

### 概要

Prosody対応モデル（multilingual-test-medium等）では、ILanguageG2PHandlerから取得したProsodyFlat（stride=3）を使用してより自然なイントネーションの音声を生成できます。

### データフロー（Prosody対応）

```
┌─────────────────┐     ┌──────────────────┐     ┌──────────────────────────┐
│   Text Input    │ --> │ CustomDictionary │ --> │ MultilingualPhonemizer   │
│   "Dockerを..."  │     │ (前処理)         │     │ (ILanguageDetector +     │
└─────────────────┘     └──────────────────┘     │  ILanguageG2PHandler)    │
                                                  └──────────────────────────┘
                                                             │
                                    ┌────────────────────────┼────────────────────────┐
                                    ↓                        ↓                        ↓
                            ┌──────────────┐  ┌────────────────────┐  ┌──────────────────┐
                            │ ja: Japanese │  │ en: English        │  │ es/fr/pt/zh/ko:  │
                            │  G2PHandler  │  │  G2PHandler        │  │ *G2PHandler      │
                            └──────┬───────┘  └────────┬───────────┘  └────────┬─────────┘
                                   │                   │                       │
                                   │   Process() → (Phonemes[], ProsodyFlat[]) │
                                   └───────────────────┼───────────────────────┘
                                                       │
                                                       ↓
                                        ┌──────────────────────────┐
                                        │ MultilingualPhonemize    │
                                        │ Result                   │
                                        │ .Phonemes[]              │
                                        │ .ProsodyFlat[] (stride=3)│
                                        └──────────────────────────┘
                                                       │
                                                       ↓
                                        ┌──────────────────────────┐
                                        │ PhonemeEncoder           │
                                        │ .EncodeWithProsody()     │
                                        │ → PhonemeIds[]           │
                                        │ → ExpandedProsodyFlat[]  │
                                        └──────────────────────────┘
                                                       │
                                                       ↓
                        ┌─────────────────────────────────────────────────────────┐
                        │              VITS Model (ONNX)                          │
                        │  入力: phoneme_ids, a1, a2, a3 (ProsodyFlatから分離)    │
                        │  出力: NativeArray<float> 音声波形                      │
                        └─────────────────────────────────────────────────────────┘
                                                       │
                                                       ↓
                        ┌─────────────────────────────────────────────────────────┐
                        │  AudioNormalizer.NormalizeInPlace(NativeArray<float>)   │
                        │  → AudioClipBuilder.BuildAudioClip(NativeArray<float>) │
                        │  → AudioClip (TTS_{Guid:N})                            │
                        └─────────────────────────────────────────────────────────┘
```

### Prosodyデータ形式（ProsodyFlat stride=3）

v2.0ではProsodyデータをフラット配列で統一管理:
```
ProsodyFlat = [a1_0, a2_0, a3_0, a1_1, a2_1, a3_1, ...]
Length = Phonemes.Length * PhonemeEncoder.ProsodyStride (=3)
```

### 言語別Prosodyマッピング

Prosodyパラメータは言語ごとに異なる意味を持ちます：

| 言語 | A1 | A2 | A3 |
|------|----|----|-----|
| ja | モーラ位置 | アクセント核位置 | アクセント句位置 |
| en | 0 | 0 | 0 |
| zh | tone(1-5) | 音節位置 | 単語長 |
| ko | 0 | 0 | 音節数 |
| es/fr/pt | 0 | stress (0/2) | 語内音素数 |

### 使用例（v2.0 API）

```csharp
// PhonemizeAsync → SynthesisRequest → SynthesizeAsync パイプライン
var result = await piperTTS.PhonemizeAsync("こんにちは");
// result.Phonemes: 音素配列
// result.ProsodyFlat: stride=3フラット配列
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

## カスタム辞書

### 概要

技術用語や固有名詞（英単語・アルファベット）を日本語の読みに変換する前処理機能。

### 処理フロー

```
入力テキスト
    │
    ↓
┌─────────────────────────────────────┐
│ CustomDictionary.ApplyToText()      │
│                                     │
│ "DockerとGitHubを使った開発"         │
│          ↓                          │
│ "ドッカーとギットハブを使った開発"     │
└─────────────────────────────────────┘
    │
    ↓
dot-net-g2p音素化
```

### 辞書ファイル

辞書は `StreamingAssets/uPiper/Dictionaries/` に配置：

| ファイル | 内容 |
|---------|------|
| `default_tech_dict.json` | 技術用語（プログラミング言語、開発ツール等） |
| `default_common_dict.json` | IT/ビジネス用語 |
| `additional_tech_dict.json` | AI/LLM関連用語 |
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

## 重要な設計判断の根拠

### 1. HTS Engine非使用の判断

**背景**:
- 従来のOpenJTalkはHTS Engine（HMMベース）で音声合成
- PiperはVITS（ニューラルネット）で音声合成

**決定**:
- dot-net-g2p（純粋C#実装）で音素化を実現
- HTS Engineは完全に除外
- 結果：軽量化、ネイティブ依存の完全排除

### 2. 音素タイミング簡略化の判断

**調査結果**:
- Piperの実装を確認した結果、HTS Engineを使用していない
- VITSモデルが音素タイミングを自動推定

**決定**:
- 固定50msで十分（モデルが再計算するため）
- 実装の簡略化と保守性の向上

### 3. PUA文字使用の判断

**課題**:
- 日本語には複数文字の音素が存在（"ky", "ch"など）
- Piperモデルは1音素=1文字を期待

**解決策**:
- Unicode PUA領域を使用
- pyopenjtalkと互換性のあるマッピング

## プラットフォームサポート

### サポート済みプラットフォーム
- **Windows**: x64 (Windows 10/11)
- **macOS**: Intel/Apple Silicon (macOS 11+)
- **Linux**: x64 (Ubuntu 20.04+)
- **Android**: arm64-v8a, armeabi-v7a, x86, x86_64 (API 21+)
- **iOS**: arm64 (iOS 11.0+)
- **WebGL**: 対応済み（専用コンポーネントによるファイルI/O・スレッド制約の回避。WebGPU環境ではGPUCompute、WebGL2環境ではGPUPixelを使用）

### プラットフォーム固有の実装

dot-net-g2pは純粋C#実装のため、プラットフォーム固有のネイティブライブラリは不要です。

#### Windows
- Unityバックエンド: Mono/IL2CPP

#### macOS
- Universal Binaryサポート（マネージドコードのため自動対応）

#### Android
- アーキテクチャ: arm64-v8a, armeabi-v7a, x86, x86_64

#### iOS
- Xcode: 14+
- アーキテクチャ: arm64 (iOS 11.0+)

#### WebGL
- ファイルシステムアクセス不可のため、専用の非同期ロード機構を使用
- マルチスレッド不可のため、`Task.Run` をメインスレッド直接実行に置換
- WebGPU対応: `PlatformHelper.IsWebGPU`でWebGPU環境を判定し、Inference Backendを自動切替
- 詳細は下記「WebGL対応」セクションを参照

## WebGL対応

### 概要

uPiperはWebGLプラットフォームでの動作をサポートしています。
WebGLではファイルシステムへの直接アクセスやマルチスレッドが使用できないため、専用の代替実装が用意されています。

### WebGL固有コンポーネント

| コンポーネント | 場所 | 役割 |
|--------------|------|------|
| `WebGLStreamingAssetsLoader` | `Runtime/Core/Platform/` | `UnityWebRequest`経由の非同期ファイルロード（進捗レポート対応） |
| `IndexedDBCache` | `Runtime/Core/Platform/` + `Plugins/WebGL/` | ブラウザIndexedDBへの辞書データキャッシュ（jslib連携） |
| `WebGLLoadingPanel` | `Runtime/Core/Platform/` | ローディング進捗表示UI（プログレスバー + ステータステキスト） |
| `WebGLSplitDataProcessor` | `Editor/WebGL/` | 大容量ファイルの自動分割（`PostProcessBuild`、GitHub Pages 100MB制限対応） |

### 辞書ロードフロー

非WebGLプラットフォームでは`File.ReadAllBytes`等で直接ファイルを読み込みますが、WebGLでは以下のフローになります：

```
┌──────────────────────────┐
│ IndexedDBCache.HasKeyAsync│  キャッシュ確認
└───────────┬──────────────┘
            │
     ┌──────┴──────┐
     │キャッシュあり│  → IndexedDBCache.LoadAsync → byte[]
     └─────────────┘
     │キャッシュなし│
     └──────┬──────┘
            ↓
┌──────────────────────────────────────┐
│ WebGLStreamingAssetsLoader           │
│ UnityWebRequest.Get() → byte[]       │
└───────────┬──────────────────────────┘
            ↓
┌──────────────────────────────────────┐
│ IndexedDBCache.StoreAsync            │  次回用にキャッシュ保存
└───────────┬──────────────────────────┘
            ↓
        DictionaryBundle.Load(byte[], byte[], byte[], byte[])
```

### Inference Backend選択（WebGPU対応）

WebGL環境では、ブラウザのグラフィックスAPIに応じてInference Backendが自動選択されます：

| 環境 | `InferenceBackend.Auto`の選択結果 | 理由 |
|------|----------------------------------|------|
| WebGPU | GPUCompute | Compute Shader対応により高パフォーマンス |
| WebGL2 | GPUPixel | Compute Shader非対応のためPixelシェーダで推論 |

この判定には `PlatformHelper.IsWebGPU` プロパティを使用しています：

```csharp
// PlatformHelper.cs
public static bool IsWebGPU =>
    IsWebGL && SystemInfo.graphicsDeviceType == GraphicsDeviceType.WebGPU;
```

明示的に `GPUCompute` を指定した場合も、WebGPU環境ではそのまま許可されますが、WebGL2環境では互換性のため `GPUPixel` にフォールバックされます。

### 条件分岐パターン

`#if UNITY_WEBGL && !UNITY_EDITOR` を使用してプラットフォーム固有の処理を分岐します：

- **ファイルI/O**: `File.ReadAllBytes` → `WebGLStreamingAssetsLoader.LoadBytesAsync`
- **スレッド**: `Task.Run` → メインスレッド直接実行（`await Task.Yield()`）
- **キャッシュ**: ファイルシステム → `IndexedDBCache`（jslib経由でブラウザIndexedDBを使用）

### GitHub Pagesデプロイ

`WebGLSplitDataProcessor`がビルド後処理（`PostProcessBuild`）で以下を自動実行します：

1. Build/ディレクトリ内の100MB超ファイルを90MBチャンクに分割
2. `split-file-loader.js`と`github-pages-adapter.js`をビルド出力にコピー
3. `index.html`にローダースクリプトタグを注入

## パフォーマンス特性

### メモリ使用量
- モデル読み込み: ~100MB (VITSモデル)
- 辞書: ~30MB (圧縮済み)
- ランタイム: ~50-200MB (使用状況による)

### 処理速度
- 音素化: 一般的な文章で <10ms
- 推論: ~100-500ms (ハードウェアによる)
- 合計レイテンシー: ほとんどのケースで <1秒

### 最適化戦略
1. **音素キャッシュ**: 頻繁に使用されるテキストをキャッシュ
2. **モデル量子化**: オプションでINT8量子化
3. **GPUアクセラレーション**: Unity AI Inference Engine経由でサポート
4. **ストリーミング**: 長いテキストのチャンク処理

## 拡張ポイント

### カスタム言語ハンドラ（推奨）
`ILanguageG2PHandler`インターフェースを実装し、`MultilingualPhonemizerOptions.Handlers`で登録：
```csharp
public interface ILanguageG2PHandler : IDisposable
{
    string LanguageCode { get; }
    bool IsInitialized { get; }
    Task InitializeAsync(CancellationToken cancellationToken = default);
    (string[] Phonemes, int[] ProsodyFlat) Process(string text);
}
```

### カスタム言語検出
`ILanguageDetector`を実装し、`MultilingualPhonemizerOptions.LanguageDetector`で注入：
```csharp
public interface ILanguageDetector
{
    IReadOnlyList<(string language, string text)> SegmentText(string text);
    string DefaultLatinLanguage { get; }
    IReadOnlyList<string> Languages { get; }
}
```

### レガシーバックエンド（テストスタブ）
`IPhonemizerBackend`インターフェース（テストスタブのみ使用）

### 音声モデルサポート
- ONNXモデルを`Resources/Models/`に配置（Unity InferenceEngine使用）
- `PiperVoiceConfig`で設定

### 言語拡張
1. `ILanguageG2PHandler`を実装（Process()でProsodyFlat stride=3を返す）
2. `MultilingualPhonemizerOptions.Handlers`で登録
3. PuaTokenMapperに音素マッピングを追加（pua.jsonまたはRegister()）
4. 互換性のあるVITSモデルをトレーニングまたは取得

## セキュリティ上の考慮事項

- すべての処理はローカルで実行（クラウド依存なし）
- 個人データの収集なし
- モデルと辞書は読み取り専用
- Unity環境内でサンドボックス実行