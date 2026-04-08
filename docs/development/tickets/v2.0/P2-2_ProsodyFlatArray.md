# P2-2: Prosodyフラット配列化

**マイルストーン**: M3 - Data Model + Config
**優先度**: P0（クリティカルパス: P1-4 → P1-3 → P2-2 → P3-6）
**見積もり**: 3 人日（Step1: 0.5 + Step2: 1.0 + Step3: 0.5 + Step4: 1.0）
**依存チケット**: P2-1（PhonemeIdMap int[] 型変更。同一エージェントが P2-1 → P2-2 の順で実施推奨）
**後続チケット**: P3-6（SynthesisRequest public 昇格、条件付き実施）
**ブランチ名**: `feature/v2.0-P2-2-prosody-flat-array`

---

## 1. タスク目的とゴール

### なぜこのタスクが必要か

現在 uPiper では Prosody データを A1/A2/A3 の **3本の別配列** (`int[]`) として全パイプラインで伝播している。しかし最終的な ONNX テンソル入力は `[1, phoneme_len, 3]` の単一テンソルであり、`CreateProsodyTensorPooled` で3本配列をインターリーブしている。この3本別管理には以下の問題がある:

1. **冗長なデータ伝播**: A1/A2/A3 は常にセットで存在し、単独で使われることがない。にもかかわらず、`MultilingualPhonemizeResult`, `SynthesisRequest`, `ProsodyEncodingResult`, `PhonemeResult`, `PhonemeSilenceProcessor.Phrase` の5つの型で3プロパティ × 5 = 15のフィールドとして管理されている。
2. **piper-plus との非互換**: piper-plus（Rust/C#/Python）は全て stride=3 のフラット配列 `[a1_0, a2_0, a3_0, a1_1, a2_1, a3_1, ...]` で統一管理している。uPiper の3本別管理は piper-plus との互換性を損なう。
3. **テンソル構築コスト**: `CreateProsodyTensorPooled` 内のインターリーブループ（`i * 3 + 0/1/2` を各配列から読み出し）が、フラット配列なら `Array.Copy` 1回で済む。
4. **EOS トリミング・パディングの3重実行**: EOS 除去時に `segA1[..^1]`, `segA2[..^1]`, `segA3[..^1]` の3回スライス、パディング時に `PadToLength` を3回呼び出しなど、操作が冗長。

### 完了の定義

- 全ての Prosody データが `int[] ProsodyFlat`（stride=3, `[a1_0, a2_0, a3_0, a1_1, ...]`）で統一されている
- `ProsodyA1` / `ProsodyA2` / `ProsodyA3` プロパティが Runtime コードから完全に削除されている
- `ProsodyStride = 3` 定数と `FlattenProsody` ヘルパーが定義されている
- `CreateProsodyTensorPooled` がインターリーブループなしで `Array.Copy` ベースに簡素化されている
- DotNetG2P エンジンの API は変更していない（uPiper 側の `ProcessXxx` / ハンドラ内でフラット化）
- 全既存テスト（EditMode + PlayMode）がパス
- `dotnet format --verify-no-changes` パス

---

## 2. 実装する内容の詳細

### Step 1: 定数・ヘルパー追加 + データ型変更（0.5 人日）

#### 2.1.1 ProsodyStride 定数

`PhonemeEncoder` 内（既存の特殊トークン定数と並置）に定数を追加:

```csharp
/// <summary>
/// Prosody flat array stride (A1, A2, A3 per phoneme).
/// </summary>
internal const int ProsodyStride = 3;
```

#### 2.1.2 FlattenProsody ヘルパーメソッド

DotNetG2P 出力（3本配列）をフラット配列に変換する境界メソッド:

```csharp
/// <summary>
/// Flatten separate A1/A2/A3 arrays into a single stride=3 array.
/// Used at the boundary where dot-net-g2p output (3 separate arrays)
/// meets the flat-array internal representation.
/// </summary>
internal static int[] FlattenProsody(int[] a1, int[] a2, int[] a3, int phonemeCount)
{
    var flat = new int[phonemeCount * ProsodyStride];
    for (int i = 0; i < phonemeCount; i++)
    {
        flat[i * ProsodyStride + 0] = i < a1.Length ? a1[i] : 0;
        flat[i * ProsodyStride + 1] = i < a2.Length ? a2[i] : 0;
        flat[i * ProsodyStride + 2] = i < a3.Length ? a3[i] : 0;
    }
    return flat;
}
```

#### 2.1.3 データ型変更（bottom-up、5型）

| 順序 | 型 | ファイル | 変更内容 |
|------|-----|---------|---------|
| 1 | `ProsodyEncodingResult` | `PhonemeEncoder.cs` | `ExpandedProsodyA1/A2/A3` → `int[] ExpandedProsodyFlat` |
| 2 | `SynthesisRequest` | `SynthesisRequest.cs` | `ProsodyA1/A2/A3` → `int[] ProsodyFlat` + `HasProsody => ProsodyFlat != null` |
| 3 | `PhonemeSilenceProcessor.Phrase` | `PhonemeSilenceProcessor.cs` | `ProsodyA1/A2/A3` → `int[] ProsodyFlat` |
| 4 | `MultilingualPhonemizeResult` | `MultilingualPhonemizer.cs` | `ProsodyA1/A2/A3` → `int[] ProsodyFlat` + `HasProsody => ProsodyFlat != null` |
| 5 | `PhonemeResult` | `PhonemeOptions.cs` | `ProsodyA1/A2/A3` → `int[] ProsodyFlat` + `Clone()` / `DeepClone()` 更新 |

**MultilingualPhonemizeResult 変更後**:
```csharp
public class MultilingualPhonemizeResult
{
    public string[] Phonemes { get; set; }

    /// <summary>
    /// Flat prosody array (stride=3): [a1_0, a2_0, a3_0, a1_1, a2_1, a3_1, ...].
    /// Length = Phonemes.Length * 3. Null when prosody is not available.
    /// </summary>
    public int[] ProsodyFlat { get; set; }

    public string DetectedPrimaryLanguage { get; set; }

    /// <summary>Prosody data is available.</summary>
    public bool HasProsody => ProsodyFlat != null;
}
```

**SynthesisRequest 変更後**:
```csharp
internal readonly struct SynthesisRequest
{
    public readonly string[] Phonemes;
    public readonly int[] ProsodyFlat;  // stride=3, length = Phonemes.Length * 3
    public readonly float LengthScale;
    public readonly float NoiseScale;
    public readonly float NoiseW;
    public readonly int SpeakerId;
    public readonly int LanguageId;

    public SynthesisRequest(
        string[] phonemes,
        int[] prosodyFlat,  // null = no prosody
        float lengthScale, float noiseScale, float noiseW,
        int speakerId, int languageId)
    { ... }

    public bool HasProsody => ProsodyFlat != null;
}
```

**PhonemeSilenceProcessor.Phrase 変更後**:
```csharp
public readonly struct Phrase
{
    public readonly int[] PhonemeIds;
    public readonly int[] ProsodyFlat;  // stride=3, or null
    public readonly int SilenceSamples;

    public Phrase(int[] phonemeIds, int[] prosodyFlat, int silenceSamples) { ... }
}
```

### Step 2: メソッドシグネチャ変更 + 内部ロジック更新（1.0 人日）

#### 2.2.1 IInferenceAudioGenerator.GenerateAudioAsync

```csharp
// 変更前
Task<float[]> GenerateAudioAsync(
    int[] phonemeIds,
    int[] prosodyA1 = null, int[] prosodyA2 = null, int[] prosodyA3 = null,
    float lengthScale = 1.0f, float noiseScale = 0.667f, float noiseW = 0.8f,
    int speakerId = 0, int languageId = 0,
    CancellationToken cancellationToken = default);

// 変更後
Task<float[]> GenerateAudioAsync(
    int[] phonemeIds,
    int[] prosodyFlat = null,  // stride=3, length = phonemeIds.Length * 3
    float lengthScale = 1.0f, float noiseScale = 0.667f, float noiseW = 0.8f,
    int speakerId = 0, int languageId = 0,
    CancellationToken cancellationToken = default);
```

#### 2.2.2 InferenceAudioGenerator 実装

- `GenerateAudioAsync()`: シグネチャ変更（3引数 → 1引数）
- `ExecuteInference()`: `prosodyA1/A2/A3` → `prosodyFlat`
- `PrepareInputs()`: `prosodyA1/A2/A3` → `prosodyFlat`
- `CreateProsodyTensorPooled()`: インターリーブループを `Array.Copy` に簡素化
- `ExecuteWarmup()`: `dummyProsodyA1/A2/A3` → `dummyProsodyFlat`（`new int[seqLen * 3]` のゼロ埋め配列1本）

**CreateProsodyTensorPooled 変更後**:
```csharp
private Tensor<int> CreateProsodyTensorPooled(
    int sequenceLength, int[] prosodyFlat, out int[] rentedArray)
{
    var prosodySize = sequenceLength * 3;
    if (prosodyFlat != null && prosodyFlat.Length == prosodySize)
    {
        rentedArray = ArrayPool<int>.Shared.Rent(prosodySize);
        Array.Copy(prosodyFlat, rentedArray, prosodySize);
    }
    else
    {
        rentedArray = ArrayPool<int>.Shared.Rent(prosodySize);
        Array.Clear(rentedArray, 0, prosodySize);
    }

    var exactData = new int[prosodySize];
    Array.Copy(rentedArray, exactData, prosodySize);
    return new Tensor<int>(new TensorShape(1, sequenceLength, 3), exactData);
}
```

#### 2.2.3 PhonemeEncoder.EncodeWithProsody

```csharp
// 変更前
public ProsodyEncodingResult EncodeWithProsody(
    string[] phonemes, int[] prosodyA1, int[] prosodyA2, int[] prosodyA3)

// 変更後
public ProsodyEncodingResult EncodeWithProsody(
    string[] phonemes, int[] prosodyFlat)
```

内部ロジック: 3本の `List<int>` (`expandedA1`, `expandedA2`, `expandedA3`) を1本の `List<int>` (`expandedProsody`, stride=3) に統合。

```csharp
// BOS トークン追加時
expandedProsody.Add(0); // a1
expandedProsody.Add(0); // a2
expandedProsody.Add(0); // a3

// 音素追加時 (phonemeIndex 番目)
int baseIdx = phonemeIndex * ProsodyStride;
int a1 = prosodyFlat != null && baseIdx < prosodyFlat.Length ? prosodyFlat[baseIdx + 0] : 0;
int a2 = prosodyFlat != null && baseIdx + 1 < prosodyFlat.Length ? prosodyFlat[baseIdx + 1] : 0;
int a3 = prosodyFlat != null && baseIdx + 2 < prosodyFlat.Length ? prosodyFlat[baseIdx + 2] : 0;
expandedProsody.Add(a1);
expandedProsody.Add(a2);
expandedProsody.Add(a3);

// PAD トークン追加時
expandedProsody.Add(0);
expandedProsody.Add(0);
expandedProsody.Add(0);
```

#### 2.2.4 その他のメソッドシグネチャ変更

| ファイル | メソッド | 変更内容 |
|---------|--------|---------|
| `SplitInferenceOrchestrator.cs` | `GenerateWithSilenceSplitAsync()` | `prosodyA1/A2/A3` → `prosodyFlat` |
| `PhonemeSilenceProcessor.cs` | `SplitAtPhonemeSilence()` | 3配列受け取り → フラット配列スライス（3要素単位） |
| `TTSSynthesisOrchestrator.cs` | `SynthesizeAsync()` | `expandedA1/A2/A3` → `expandedProsodyFlat` |

### Step 3: MultilingualPhonemizer + PiperTTS 更新（0.5 人日）

#### 2.3.1 言語別 ProcessXxx / ハンドラの戻り値変更

P1-4 完了後はハンドラクラス（`JapaneseG2PHandler` 等）に移行済みのため、各ハンドラの `Process` メソッド戻り値を変更:

```csharp
// 変更前（P1-4 後の状態）
(string[] Phonemes, int[] A1, int[] A2, int[] A3) Process(string text)

// 変更後
(string[] Phonemes, int[] ProsodyFlat) Process(string text)
```

各ハンドラ内で DotNetG2P 出力の A1/A2/A3 を `FlattenProsody` でフラット化:

```csharp
// 例: JapaneseG2PHandler.Process
private (string[] segPhonemes, int[] prosodyFlat) ProcessJapanese(string text)
{
    var result = _jaPhonemizer.PhonemizeWithProsody(text);
    var phonemes = result.Phonemes ?? Array.Empty<string>();
    var a1 = result.ProsodyA1 ?? Array.Empty<int>();
    var a2 = result.ProsodyA2 ?? Array.Empty<int>();
    var a3 = result.ProsodyA3 ?? Array.Empty<int>();

    // Strip leading PAD
    if (phonemes.Length > 0 && phonemes[0] == "_") { ... }

    // Flatten A1/A2/A3 → stride=3 flat array
    var flat = FlattenProsody(a1, a2, a3, phonemes.Length);
    return (phonemes, flat);
}
```

**注意**: `DotNetG2PPhonemizer.PhonemizeWithProsody()` は dot-net-g2p パッケージの API であり、uPiper 側では変更しない。フラット化は uPiper 側のハンドラ内で行う。

#### 2.3.2 PhonemizeWithProsodyAsync 内部

```csharp
// 変更前
var allA1 = new List<int>();
var allA2 = new List<int>();
var allA3 = new List<int>();
...
allA1.AddRange(segA1);
allA2.AddRange(segA2);
allA3.AddRange(segA3);

// 変更後
var allProsodyFlat = new List<int>();
...
allProsodyFlat.AddRange(segProsodyFlat);
```

EOS トリミング（stride=3 で末尾3要素除去）:
```csharp
// 変更前
segA1 = segA1.Length > 0 ? segA1[..^1] : segA1;
segA2 = segA2.Length > 0 ? segA2[..^1] : segA2;
segA3 = segA3.Length > 0 ? segA3[..^1] : segA3;

// 変更後
if (segProsodyFlat.Length >= 3)
    segProsodyFlat = segProsodyFlat[..^3];
```

PadToLength（stride=3 でパディング）:
```csharp
// 変更前
PadToLength(allA1, maxLen);
PadToLength(allA2, maxLen);
PadToLength(allA3, maxLen);

// 変更後
var targetFlatLen = maxLen * 3;
while (allProsodyFlat.Count < targetFlatLen)
{
    allProsodyFlat.Add(0); // a1
    allProsodyFlat.Add(0); // a2
    allProsodyFlat.Add(0); // a3
}
```

#### 2.3.3 PiperTTS.Inference.cs

`GenerateAudioWithMultilingualAsync()` 内の `prosodyA1/A2/A3` → `prosodyFlat` に更新:

```csharp
// 変更前
var request = new SynthesisRequest(
    phonemes, multiResult.ProsodyA1, multiResult.ProsodyA2, multiResult.ProsodyA3,
    lengthScale, noiseScale, noiseW, speakerId, languageId);

// 変更後
var request = new SynthesisRequest(
    phonemes, multiResult.ProsodyFlat,
    lengthScale, noiseScale, noiseW, speakerId, languageId);
```

### Step 4: テスト更新（1.0 人日）

#### 2.4.1 既存テストのアサーション更新パターン

```csharp
// 変更前
Assert.AreEqual(expectedA1, result.ProsodyA1[i]);
Assert.AreEqual(expectedA2, result.ProsodyA2[i]);
Assert.AreEqual(expectedA3, result.ProsodyA3[i]);

// 変更後
Assert.AreEqual(expectedA1, result.ProsodyFlat[i * 3 + 0]);
Assert.AreEqual(expectedA2, result.ProsodyFlat[i * 3 + 1]);
Assert.AreEqual(expectedA3, result.ProsodyFlat[i * 3 + 2]);
```

テストデータ構築パターン:
```csharp
// 変更前
var request = new SynthesisRequest(
    phonemes, prosodyA1, prosodyA2, prosodyA3, 1.0f, 0.667f, 0.8f, 0, 0);

// 変更後
var prosodyFlat = new int[phonemes.Length * 3];
for (int i = 0; i < phonemes.Length; i++)
{
    prosodyFlat[i * 3 + 0] = a1Values[i];
    prosodyFlat[i * 3 + 1] = a2Values[i];
    prosodyFlat[i * 3 + 2] = a3Values[i];
}
var request = new SynthesisRequest(
    phonemes, prosodyFlat, 1.0f, 0.667f, 0.8f, 0, 0);
```

#### 2.4.2 テストファイル一覧（14ファイル）

| ファイル | 変更概要 |
|---------|---------|
| `MultilingualPhonemizerTests.cs` | A1/A2/A3 アサーション → ProsodyFlat アサーション |
| `MultilingualPhonemizerDeepTests.cs` | 同上 |
| `MultilingualPhonemizerEosTests.cs` | EOS トリミング時の Prosody 配列操作更新（末尾3要素除去） |
| `MultilingualPhonemizerPhase5Tests.cs` | 同上 |
| `ChinesePhonemizerTests.cs` | 中国語 Prosody アサーション更新 |
| `MultilingualPipelineTests.cs` | エンドツーエンドパイプライン更新 |
| `TTSSynthesisOrchestratorTests.cs` | SynthesisRequest 構築更新 |
| `SplitInferenceOrchestratorTests.cs` | Phrase 構造体更新 |
| `StubInferenceAudioGenerator.cs` | `GenerateAudioAsync` シグネチャ更新（3引数 → 1引数） |
| `PhonemeSilenceProcessorTests.cs` | `SplitAtPhonemeSilence` 引数更新 |
| `ProsodyInferenceIntegrationTests.cs` | 統合テスト更新 |
| `PhonemeEncoderMultilingualTests.cs` | `EncodeWithProsody` 更新 |
| `PhonemeEncoderMultilingualModelTests.cs` | 同上 |
| `DotNetG2PPhonemizerTest.cs` | `ProsodyA1/A2/A3` 参照更新 |

#### 2.4.3 新規テスト追加

| テストケース | 内容 |
|------------|------|
| `ProsodyFlat_NullMeansNoProsody` | `ProsodyFlat = null` → `HasProsody == false` |
| `ProsodyFlat_StrideLayout` | stride=3 レイアウト検証（`flat[i*3+0]` = A1, `flat[i*3+1]` = A2, `flat[i*3+2]` = A3） |
| `ProsodyFlat_EmptyPhonemes` | 空配列 → `ProsodyFlat = Array.Empty<int>()` |
| `ProsodyFlat_PadToLength` | パディング時に3要素単位でゼロ埋め |
| `ProsodyFlat_EosTrimming` | EOS トリミング時に末尾3要素除去 |
| `ProsodyFlat_TensorShape` | テンソル `[1, N, 3]` との整合性検証 |

---

## 3. エージェントチームの役割と人数

### 推奨構成: エージェント 1名

P2-2 は影響範囲が最大（Runtime 21ファイル + テスト 14ファイル）だが、変更は機械的（`ProsodyA1/A2/A3` → `ProsodyFlat`）であり、全ファイルを一貫した理解のもとで変更する必要がある。複数エージェントに分割するとマージコンフリクトのリスクが高い。

同一エージェントが P2-1 → P2-2 の順で実施するのが最も効率的（P2-1 の `PiperVoiceConfig.PhonemeIdMap` 型変更が P2-2 のテストコードにも影響するため）。

### 実施順序

```
Time ──────────────────────────────────────>

Agent 1: [Step1: 定数+型 0.5d] → [Step2: メソッド 1.0d] → [Step3: MP+PiperTTS 0.5d] → [Step4: テスト 1.0d]

合計: 3.0 人日
```

### 実装順序の詳細

1. **定数・ヘルパー追加**: `ProsodyStride` 定数、`FlattenProsody()` ヘルパー
2. **データ型変更（bottom-up）**: ProsodyEncodingResult → SynthesisRequest → Phrase → MultilingualPhonemizeResult → PhonemeResult
3. **メソッドシグネチャ変更**: IInferenceAudioGenerator → InferenceAudioGenerator → PhonemeEncoder → SplitInferenceOrchestrator → PhonemeSilenceProcessor → TTSSynthesisOrchestrator
4. **MultilingualPhonemizer 内部**: ProcessXxx / ハンドラ戻り値変更 → PhonemizeWithProsodyAsync 内部ロジック
5. **PiperTTS.Inference.cs**: 呼び出し側更新
6. **テスト更新**: 全14ファイル + 新規テスト追加
7. **StubInferenceAudioGenerator**: シグネチャ更新

---

## 4. 提供範囲とテスト項目

### 4.1 提供範囲

#### スコープ内

**Runtime 変更（21ファイル）**:

| ファイル | 型/メソッド | 変更内容 |
|---------|-----------|---------|
| `MultilingualPhonemizer.cs` | `MultilingualPhonemizeResult` | `ProsodyA1/A2/A3` → `int[] ProsodyFlat` |
| `MultilingualPhonemizer.cs` | `PhonemizeWithProsodyAsync()` | `allA1/allA2/allA3` → `allProsodyFlat` (List\<int\>) |
| `MultilingualPhonemizer.cs` | `ProcessJapanese()` 他7メソッド / ハンドラ | 戻り値 `(phonemes, a1, a2, a3)` → `(phonemes, int[] prosodyFlat)` |
| `MultilingualPhonemizer.cs` | `ExtractProsodyArrays()` | stride=3 フラット配列を返すよう変更（または FlattenProsody に置換） |
| `MultilingualPhonemizer.cs` | `PadToLength()` | stride=3 対応（3要素ずつパディング） |
| `SynthesisRequest.cs` | `SynthesisRequest` | `ProsodyA1/A2/A3` → `int[] ProsodyFlat` |
| `PhonemeEncoder.cs` | `ProsodyEncodingResult` | `ExpandedProsodyA1/A2/A3` → `int[] ExpandedProsodyFlat` |
| `PhonemeEncoder.cs` | `EncodeWithProsody()` | 3本の `List<int>` → 1本の `List<int>` (stride=3) |
| `PhonemeEncoder.cs` | `AddToken()`, `AddPadToken()`, `EncodePhonemeTs()` | 3引数 → フラット配列操作 |
| `IInferenceAudioGenerator.cs` | `GenerateAudioAsync()` | `prosodyA1/A2/A3` → `int[] prosodyFlat` |
| `InferenceAudioGenerator.cs` | `GenerateAudioAsync()` | シグネチャ変更 |
| `InferenceAudioGenerator.cs` | `ExecuteInference()` | `prosodyA1/A2/A3` → `prosodyFlat` |
| `InferenceAudioGenerator.cs` | `PrepareInputs()` | `prosodyA1/A2/A3` → `prosodyFlat` |
| `InferenceAudioGenerator.cs` | `CreateProsodyTensorPooled()` | 3本配列受け取り → フラット配列受け取り（`Array.Copy` ベースに簡素化） |
| `InferenceAudioGenerator.cs` | `ExecuteWarmup()` | `dummyProsodyA1/A2/A3` → `dummyProsodyFlat` |
| `TTSSynthesisOrchestrator.cs` | `SynthesizeAsync()` | `expandedA1/A2/A3` → `expandedProsodyFlat` |
| `SplitInferenceOrchestrator.cs` | `GenerateWithSilenceSplitAsync()` | `prosodyA1/A2/A3` → `prosodyFlat` |
| `PhonemeSilenceProcessor.cs` | `Phrase` | `ProsodyA1/A2/A3` → `int[] ProsodyFlat` |
| `PhonemeSilenceProcessor.cs` | `SplitAtPhonemeSilence()` | 3配列 → フラット配列スライス（3要素単位） |
| `PiperTTS.Inference.cs` | `GenerateAudioWithMultilingualAsync()` | `prosodyA1/A2/A3` → `prosodyFlat` |
| `PhonemeOptions.cs` | `PhonemeResult` | `ProsodyA1/A2/A3` → `int[] ProsodyFlat` + `Clone()` / `DeepClone()` 更新 |

**テスト変更（14ファイル）**: セクション 2.4.2 参照

#### スコープ外（後続タスクで対応）

| 項目 | 理由 |
|------|------|
| `DotNetG2PPhonemizer` の API | dot-net-g2p は別リポジトリ。uPiper 側のハンドラ内でフラット化する |
| 各 DotNetG2P エンジンの `ToIpaWithProsody()` | 同上。Prosody 構造体 (`ProsodyInfo { A1, A2, A3 }`) はそのまま |
| ONNX テンソル形状 `[1, N, 3]` | 変更なし（piper-plus と既に一致） |
| `int` 型 (vs piper-plus の `long`) | Unity Sentis が `Tensor<int>` 要求。ONNX モデル側の入力定義で吸収される |
| `ILanguageG2PHandler` インターフェース自体の変更 | P2-2 では `Process` 戻り値のタプル型のみ変更。インターフェース定義の A1/A2/A3 → ProsodyFlat 変更は本チケットのスコープ |
| NativeArray 統一 | **P2-3**（P2-2 と同時推奨だが別チケット） |
| SynthesisRequest public 昇格 | **P3-6**（P2-2 完了後の条件付き実施） |

### 4.2 Unit テスト

#### 4.2.1 新規テスト

| テストクラス | テストメソッド | 検証内容 |
|------------|--------------|---------|
| `ProsodyFlatTests` | `ProsodyFlat_NullMeansNoProsody` | `ProsodyFlat = null` → `HasProsody == false` |
| 同上 | `ProsodyFlat_StrideLayout` | `flat[i*3+0]` = A1, `flat[i*3+1]` = A2, `flat[i*3+2]` = A3 |
| 同上 | `ProsodyFlat_EmptyPhonemes` | 空配列 → `ProsodyFlat = Array.Empty<int>()` |
| 同上 | `ProsodyFlat_PadToLength` | パディング時に3要素単位でゼロ埋め |
| 同上 | `ProsodyFlat_EosTrimming` | EOS トリミング時に末尾3要素除去 |
| 同上 | `ProsodyFlat_TensorShape` | テンソル `[1, N, 3]` との整合性 |
| `FlattenProsodyTests` | `FlattenProsody_NormalCase` | 正常な3配列 → stride=3 フラット配列 |
| 同上 | `FlattenProsody_UnequalLengths` | A1/A2/A3 の長さが異なる場合ゼロ埋め |
| 同上 | `FlattenProsody_EmptyArrays` | 空配列 → 空フラット配列 |

#### 4.2.2 既存テスト振る舞い不変確認

14ファイルの全既存テストが `ProsodyFlat` パターンへの書き換え後に全てパスすること。

### 4.3 E2E テスト

- `MultilingualPipelineTests`: フラット配列がパイプライン全体を通じて正しく伝播し、最終的に `[1, N, 3]` テンソルに変換されること
- `ProsodyInferenceIntegrationTests`: Prosody 付き音声生成の振る舞い不変確認

---

## 5. 懸念事項とレビュー項目

### 5.1 技術的懸念

| 懸念 | リスク | 緩和策 |
|------|--------|--------|
| **影響範囲の広さ（21 Runtime + 14 テスト = 35ファイル）** | 回帰バグの見落とし | bottom-up でデータ型変更 → コンパイルエラー駆動で全変更箇所を網羅。テスト全パスを確認 |
| **P1-4 ハンドラとの相互作用** | P1-4 で作成した `ILanguageG2PHandler.Process` の戻り値型が変わる。P1-4 のハンドラ実装を熟知している必要がある | P2-2 担当は P2-1 担当と同一エージェント。P1-4 の設計ドキュメントを事前に参照すること |
| **EOS トリミングの stride=3 対応** | 末尾3要素除去 (`segProsodyFlat[..^3]`) でオフバイワンエラーの可能性 | `ProsodyFlat_EosTrimming` テストで検証。`segProsodyFlat.Length >= 3` のガード条件を必須とする |
| **PhonemeSilenceProcessor のフラット配列スライス** | 3要素単位でのスライスインデックス計算ミス | 既存テスト `PhonemeSilenceProcessorTests` のアサーションで検証 |
| **CreateProsodyTensorPooled の Array.Copy** | `prosodyFlat.Length != prosodySize` 時のゼロ埋めフォールバック | サイズ不一致ケースのテスト追加 |

### 5.2 レビューチェックリスト

- [ ] `ProsodyA1` / `ProsodyA2` / `ProsodyA3` が Runtime コードに残存していないこと（grep で確認）
- [ ] `ProsodyStride = 3` 定数が一箇所で定義され、マジックナンバー `3` がハードコードされていないこと
- [ ] `FlattenProsody` ヘルパーが DotNetG2P 出力の A1/A2/A3 → フラット変換の唯一の境界として使用されていること
- [ ] `CreateProsodyTensorPooled` がインターリーブループなしで `Array.Copy` ベースに簡素化されていること
- [ ] `HasProsody` プロパティが `ProsodyFlat != null` で統一されていること（`ProsodyA1 != null || ...` の3重チェックが消滅）
- [ ] EOS トリミングが `segProsodyFlat[..^3]` で正しく動作すること（末尾1要素ではなく3要素除去）
- [ ] PadToLength が3要素単位（`Add(0); Add(0); Add(0);`）でパディングしていること
- [ ] `IInferenceAudioGenerator.GenerateAudioAsync` の public interface シグネチャが正しいこと
- [ ] `PhonemeResult.Clone()` / `DeepClone()` が `ProsodyFlat` を正しくコピーしていること
- [ ] DotNetG2P エンジンの API（`PhonemizeWithProsody()`, `ToIpaWithProsody()` 等）が変更されていないこと
- [ ] Assembly Definition への変更が不要であることの確認
- [ ] `dotnet format --verify-no-changes` パス
- [ ] テスト全パス（EditMode + PlayMode）

---

## 6. 一から作り直すとしたら

### 6.1 現設計が抱える構造的妥協

本チケットの設計は「DotNetG2P エンジンが A1/A2/A3 を別配列で返す」という外部制約を前提とし、uPiper 内部でフラット化する方針をとっている。この前提自体が以下の妥協を含む:

1. **フラット化の境界が分散する可能性**: 7言語のハンドラそれぞれの `Process` メソッド内で `FlattenProsody` を呼ぶ。共通ヘルパーを使うものの、呼び出し箇所は7箇所に分散する。`MultilingualPhonemizer.PhonemizeWithProsodyAsync` でハンドラ戻り値を受け取った後に一括フラット化する方が集約的だが、ハンドラ戻り値のタプルが4要素（`phonemes, a1, a2, a3`）のままになり、フラット化の恩恵が途中経路に及ばない。

2. **stride=3 のマジックナンバーリスク**: `ProsodyStride` 定数を定義するが、`i * 3 + 0/1/2` パターンがコードベース全体に広がる。将来 Prosody パラメータが増えた場合（例: A4 追加）、`ProsodyStride = 4` に変更するだけでは済まず、全てのインデックス計算を見直す必要がある。

3. **piper-plus との型の不一致**: piper-plus C# は `long[]` を使用するが、uPiper は Unity Sentis の制約で `int[]` を使用する。フラット配列化で「レイアウトは互換」になるが「型は非互換」のままである。

### 6.2 Prosody データモデルのゼロベース設計

ゼロから Prosody データを設計するなら、フラット配列ではなく **構造化された型** を使う方が型安全性で優れる:

#### 案 A: readonly record struct

```csharp
/// <summary>Per-phoneme prosody features.</summary>
public readonly record struct ProsodyFeature(int A1, int A2, int A3)
{
    public static readonly ProsodyFeature Zero = new(0, 0, 0);
}

// 使用箇所
public class MultilingualPhonemizeResult
{
    public string[] Phonemes { get; set; }
    public ProsodyFeature[]? Prosody { get; set; }  // 1:1 aligned with Phonemes
}
```

**利点**:
- `prosody[i].A1` のようにフィールド名でアクセスでき、`flat[i * 3 + 0]` より可読性が高い
- stride 計算のオフバイワンエラーが構造的に発生しない
- `ProsodyFeature.Zero` で特殊トークンのゼロ埋めが明示的

**不採用の理由**:
- ONNX テンソル構築時に `ProsodyFeature[]` → `int[]` (stride=3) への変換が依然として必要。struct 配列のメモリレイアウトは CLR 実装依存であり、`MemoryMarshal.Cast<ProsodyFeature, int>()` による zero-copy 変換が安全とは限らない（パディングの可能性）。結局 `CreateProsodyTensorPooled` 内での手動変換が残る
- piper-plus C# が `long[]` フラット配列を使用しており、型の互換性を重視するなら `int[]` フラット配列が最も直接的

#### 案 B: Span ベースのアクセサ

```csharp
public readonly struct ProsodyFlat
{
    private readonly int[] _data;  // stride=3

    public ProsodyFlat(int[] data) => _data = data;

    public int Count => (_data?.Length ?? 0) / 3;
    public ProsodyFeature this[int index] => new(
        _data[index * 3 + 0],
        _data[index * 3 + 1],
        _data[index * 3 + 2]);

    /// <summary>Raw flat data for tensor construction (zero-copy).</summary>
    public ReadOnlySpan<int> AsSpan() => _data.AsSpan();

    public bool IsNull => _data == null;
}
```

**利点**: フラット配列のメモリ効率を保ちつつ、型安全なアクセサを提供。テンソル構築時は `AsSpan()` で zero-copy に近いアクセスが可能。

**不採用の理由**: `ReadOnlySpan<int>` は Unity の `Tensor<int>` コンストラクタが受け取れない（`int[]` が必要）。`AsSpan().ToArray()` ではコピーが発生し、フラット配列を直接渡すのと変わらない。また、`ProsodyFlat` struct を全パイプラインで伝播させると、`int[]?` の nullable 判定が `ProsodyFlat.IsNull` に変わるだけで本質的な改善にならない。

#### 案 C: IPA-aware Prosody（将来構想）

```csharp
public readonly record struct ProsodyFeature(
    int MoraPosition,       // A1: 言語非依存のモーラ/音節位置
    int AccentNucleus,      // A2: アクセント核/ストレス
    int PhrasePosition,     // A3: 句位置
    string LanguageCode);   // 言語タグ（Prosody 解釈に必要）
```

Prosody の意味は言語依存（A1 = 日本語ならモーラ位置、中国語なら声調）であり、フラット配列では言語情報が失われる。言語タグ付き Prosody はデバッグと解釈に有用だが、ONNX モデルは数値のみを受け取るため、ランタイムでの実用性は低い。

### 6.3 フラット化境界の最適配置

ゼロから設計するなら、フラット化の境界を以下のように引く:

```
DotNetG2P エンジン
  (A1[], A2[], A3[] を返す — 変更不可)
        |
        v
  ★ フラット化境界 ★
  FlattenProsody(a1, a2, a3, count) → int[]
        |
        v
  以降は全て int[] ProsodyFlat (stride=3)
        |
        v
  ONNX テンソル [1, N, 3]
```

現設計はこの理想に近い。フラット化は各ハンドラの `Process` メソッド内で行われ、それ以降のパイプラインは全て `int[] ProsodyFlat` で統一される。改善の余地があるとすれば、フラット化を `MultilingualPhonemizer` 内の一箇所（ハンドラ戻り値受け取り直後）に集約することだが、これはハンドラの戻り値型が `(string[], int[], int[], int[])` のまま残ることを意味し、P1-4 で定義した `ILanguageG2PHandler` インターフェースの変更量が増える。

現設計（ハンドラ内でフラット化）は、ハンドラの責務として「piper-plus 互換のフラット Prosody を返す」を明確にする点で、むしろ適切である。

### 6.4 piper-plus との将来的な完全互換

piper-plus の Rust 側は `Vec<[i32; 3]>` （`[i32; 3]` の配列）を使用しており、C# 側は `long[]` フラット配列を使用している。uPiper が `int[]` フラット配列を採用することで:

- **メモリレイアウト**: piper-plus C# と同一（stride=3）
- **型**: `int` vs `long` の不一致が残る（Unity Sentis 制約）
- **null セマンティクス**: `null` = Prosody 未使用、piper-plus と同一

将来 Unity Sentis が `Tensor<long>` をサポートした場合、`int[]` → `long[]` への型変更は P2-2 のフラット配列化完了後であれば、影響箇所が「`ProsodyFlat` のフィールド型 + テンソル構築1箇所」に限定され、低コストで実施可能。

### 6.5 見送った代替案

| 代替案 | 検討内容 | 見送り理由 |
|--------|---------|-----------|
| **段階的移行（internal のみ先行）** | `SynthesisRequest` 等の internal 型を先にフラット化し、public 型は後で変更 | 3本配列→フラット配列の変換レイヤーが一時的に増え、かえって複雑。一括変更の方がコンパイルエラー駆動で安全 |
| **`ProsodyFlat` wrapper struct（案 B）** | フラット配列を struct で包んで型安全なアクセサを提供 | `int[]?` → `ProsodyFlat` の変更が追加の破壊的変更になる。フラット配列が十分にシンプルな設計であり、wrapper のオーバーヘッドに見合わない |
| **piper-plus C# と同じ `long[]` を採用** | 型完全互換 | Unity Sentis が `Tensor<int>` を要求。`long[]` → `int[]` のキャストが全パイプラインに入り込む |
| **DotNetG2P エンジン側でフラット配列を返す API 追加** | 境界をエンジン側に押し込む | dot-net-g2p は別リポジトリ。uPiper 側の都合で API を変更すべきでない。また、他の消費者（piper-plus Python 等）が A1/A2/A3 別配列を期待している可能性 |

---

## 7. 後続タスクへの連絡事項

### P3-6（SynthesisRequest public 昇格）への連絡

- P2-2 完了時点で `SynthesisRequest` は `int[] ProsodyFlat` をフィールドとして持つ `internal readonly struct`
- P3-6 で `public` に昇格する際、`ProsodyFlat` の stride=3 レイアウトが public API の契約となる
- **重要**: P3-6 では `SynthesisRequest` のコンストラクタを `internal` のまま維持し、ファクトリ経由の構築を強制する設計。`ProsodyFlat` の検証（`Length == Phonemes.Length * 3`）をファクトリ内で行うことを推奨
- beta テスターからのフィードバックで internal に戻す選択肢を保持する（条件付き実施）

### P2-3（NativeArray 統一）への連絡

- P2-2 完了後、`int[] ProsodyFlat` は managed 配列。P2-3 で `NativeArray<int>` に変換する際、stride=3 レイアウトはそのまま維持
- `CreateProsodyTensorPooled` の `ArrayPool<int>` 使用部分が P2-3 で `NativeArray` ベースに置換される可能性あり
- P2-2 と P2-3 は同時推奨だが、P2-2 完了後に P2-3 着手が安全

### P2-1（PhonemeIdMap int[] 型変更）との関係

- P2-1 と P2-2 は独立して実施可能だが、P2-1 の `PiperVoiceConfig.PhonemeIdMap` 型変更が P2-2 のテストコード（テストデータ内の `Dictionary<string, int>` → `Dictionary<string, int[]>`）にも影響する
- 同一エージェントが P2-1 → P2-2 の順で実施することで、テストの二重書き換えを回避

### 破壊的変更まとめ

| 変更 | 影響 |
|------|------|
| `MultilingualPhonemizeResult.ProsodyA1/A2/A3` 削除 | public class のプロパティ変更 |
| `IInferenceAudioGenerator.GenerateAudioAsync()` シグネチャ | public interface の変更 |
| `PhonemeResult.ProsodyA1/A2/A3` 削除 | public class のプロパティ変更 |

`SynthesisRequest` / `ProsodyEncodingResult` / `PhonemeSilenceProcessor.Phrase` は internal のため外部互換性影響なし。