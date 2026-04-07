# P2-2: Prosody フラット配列化 設計ドキュメント

**作成日**: 2026-04-08
**ベース**: v2.0-plan.md P2-2 セクション
**前提**: Phase 1 完了後に実施（P1-1 ~ P1-6）

---

## 1. 現状分析

### 1.1 A1/A2/A3 別配列の定義箇所

現在 uPiper では Prosody データを A1/A2/A3 の **3本の別配列** (`int[]`) として伝播している。

| 型 | ファイル | フィールド |
|----|---------|-----------|
| `MultilingualPhonemizeResult` | `Runtime/Core/Phonemizers/Multilingual/MultilingualPhonemizer.cs` L22-38 | `int[] ProsodyA1`, `int[] ProsodyA2`, `int[] ProsodyA3` |
| `SynthesisRequest` | `Runtime/Core/AudioGeneration/SynthesisRequest.cs` L7-42 | `int[] ProsodyA1`, `int[] ProsodyA2`, `int[] ProsodyA3` |
| `ProsodyEncodingResult` | `Runtime/Core/AudioGeneration/PhonemeEncoder.cs` L11-32 | `int[] ExpandedProsodyA1`, `int[] ExpandedProsodyA2`, `int[] ExpandedProsodyA3` |
| `PhonemeResult` | `Runtime/Core/Phonemizers/Backend/PhonemeOptions.cs` L20-215 | `int[] ProsodyA1`, `int[] ProsodyA2`, `int[] ProsodyA3` |
| `PhonemeSilenceProcessor.Phrase` | `Runtime/Core/AudioGeneration/PhonemeSilenceProcessor.cs` L26-72 | `int[] ProsodyA1`, `int[] ProsodyA2`, `int[] ProsodyA3` |

### 1.2 現在のデータフロー

```
テキスト入力
    |
    v
MultilingualPhonemizer.PhonemizeWithProsodyAsync()
    |  各言語の ProcessXxx() が (phonemes, a1[], a2[], a3[]) を返す
    |  → MultilingualPhonemizeResult { Phonemes, ProsodyA1, ProsodyA2, ProsodyA3 }
    |
    v
PiperTTS.Inference.cs: GenerateAudioWithMultilingualAsync()
    |  multiResult.ProsodyA1/A2/A3 を取り出し
    |  → SynthesisRequest(phonemes, prosodyA1, prosodyA2, prosodyA3, ...)
    |
    v
TTSSynthesisOrchestrator.SynthesizeAsync()
    |  request.HasProsody → EncodeWithProsody(phonemes, A1, A2, A3)
    |  → ProsodyEncodingResult { PhonemeIds, ExpandedProsodyA1/A2/A3 }
    |
    v
[分岐: SilenceSplit あり/なし]
    |
    ├─ SplitInferenceOrchestrator.GenerateWithSilenceSplitAsync()
    |     PhonemeSilenceProcessor.SplitAtPhonemeSilence(ids, a1, a2, a3, ...)
    |     → List<Phrase> (各 Phrase に ProsodyA1/A2/A3)
    |     → 句ごとに GenerateAudioAsync(ids, a1, a2, a3, ...)
    |
    └─ IInferenceAudioGenerator.GenerateAudioAsync(ids, a1, a2, a3, ...)
          |
          v
       InferenceAudioGenerator.PrepareInputs()
          |  CreateProsodyTensorPooled(len, a1, a2, a3)
          |  → 3本の配列を stride=3 でインターリーブ
          |  → Tensor<int>(shape: [1, len, 3])
          |
          v
       ONNX "prosody_features" 入力テンソル [1, phoneme_len, 3]
```

**問題点**: A1/A2/A3 は常にセットで存在し、最終的に `[1, N, 3]` テンソルに統合される。途中経路での3本別管理は冗長であり、piper-plus との互換性もない。

### 1.3 テンソル構築の現在のロジック

`InferenceAudioGenerator.CreateProsodyTensorPooled()` (L463-484) で3本配列をフラット化:

```csharp
for (var i = 0; i < sequenceLength; i++)
{
    rentedArray[i * 3 + 0] = prosodyA1 != null && i < prosodyA1.Length ? prosodyA1[i] : 0;
    rentedArray[i * 3 + 1] = prosodyA2 != null && i < prosodyA2.Length ? prosodyA2[i] : 0;
    rentedArray[i * 3 + 2] = prosodyA3 != null && i < prosodyA3.Length ? prosodyA3[i] : 0;
}
// → Tensor<int>(shape: [1, sequenceLength, 3], exactData)
```

これは既にフラット配列 (stride=3) への変換を行っている。フラット配列化により、この変換ステップが不要になる。

---

## 2. piper-plus 側のフラット配列仕様

### 2.1 Rust (piper-core)

**`SynthesisRequest`** (`src/rust/piper-core/src/engine.rs` L32-40):
```rust
pub struct SynthesisRequest {
    pub phoneme_ids: Vec<i64>,
    pub prosody_features: Option<Vec<[i32; 3]>>,  // ← [a1, a2, a3] の配列
    ...
}
```

**テンソル構築** (`engine.rs` L369-387):
```rust
let flat: Vec<i64> = features
    .iter()
    .flat_map(|f| [f[0] as i64, f[1] as i64, f[2] as i64])
    .collect();
// → Tensor shape: [1, phoneme_len, 3]
```

### 2.2 C# (PiperPlus.Core)

**`SynthesisInput`** (`src/csharp/PiperPlus.Core/Inference/PiperSession.cs` L31-39):
```csharp
public record SynthesisInput(
    long[] PhonemeIds,
    int SpeakerId = 0,
    int LanguageId = 0,
    long[]? ProsodyFeatures = null,  // ← フラット配列 [a1_0, a2_0, a3_0, a1_1, ...]
    ...
);
```

**`PhonemeEncoder.EncodeDirect()`** (`src/csharp/PiperPlus.Core/Phonemize/PhonemeEncoder.cs` L114-159):
```csharp
var flat = new long[prosody.Count * 3];
for (int i = 0; i < prosody.Count; i++)
{
    int offset = i * 3;
    if (prosody[i] is { } p)
    {
        flat[offset] = p.A1;
        flat[offset + 1] = p.A2;
        flat[offset + 2] = p.A3;
    }
}
return (phonemeIdsLong, flat);
```

**`PhonemeSilenceProcessor`** (`src/csharp/PiperPlus.Core/Inference/PhonemeSilenceProcessor.cs`):
- `Phrase` レコードが `List<long>? ProsodyFlat` を保持
- `SplitAtPhonemeSilence()` は `long[]? prosodyFlat` (stride=3) を受け取り、3要素ずつスライスして Phrase に分配

### 2.3 Python (infer_onnx.py)

```python
# Format: [[a1, a2, a3], [a1, a2, a3], ...]
prosody_array = [[pf["a1"], pf["a2"], pf["a3"]] for pf in prosody_features_data]
prosody_features = np.expand_dims(np.array(prosody_array, dtype=np.int64), 0)
# → shape: (1, phoneme_length, 3)
```

### 2.4 ONNX テンソル仕様 (ort-session-contract.toml)

```toml
prosody_shape = "1 x phoneme_length x 3"
prosody_fill = 0  # zero-filled
```

### 2.5 統一仕様

| 項目 | piper-plus 仕様 |
|------|----------------|
| データ型 | `int[]` (stride=3) -- uPiper は Unity Sentis が int32 要求のため `int[]` |
| メモリレイアウト | `[a1_0, a2_0, a3_0, a1_1, a2_1, a3_1, ...]` |
| 長さ | `phonemeCount * 3` |
| 不在時 | `null` (prosody 未使用) |
| ゼロ埋め | 特殊トークン (BOS/EOS/PAD) は `[0, 0, 0]` |
| テンソル形状 | `[1, phonemeCount, 3]` |

---

## 3. フラット配列設計

### 3.1 メモリレイアウト

```
stride = 3
index:  [0]  [1]  [2]  [3]  [4]  [5]  ...  [N*3-3] [N*3-2] [N*3-1]
value:  a1_0 a2_0 a3_0 a1_1 a2_1 a3_1 ...  a1_N-1  a2_N-1  a3_N-1
```

### 3.2 インデックス計算

```csharp
const int ProsodyStride = 3;

// i 番目の音素の A1/A2/A3 取得
int a1 = prosodyFlat[i * ProsodyStride + 0];
int a2 = prosodyFlat[i * ProsodyStride + 1];
int a3 = prosodyFlat[i * ProsodyStride + 2];

// i 番目の音素の A1/A2/A3 設定
prosodyFlat[i * ProsodyStride + 0] = a1;
prosodyFlat[i * ProsodyStride + 1] = a2;
prosodyFlat[i * ProsodyStride + 2] = a3;

// 音素数取得
int phonemeCount = prosodyFlat.Length / ProsodyStride;
```

### 3.3 HasProsody 判定

```csharp
// 変更前
public bool HasProsody => ProsodyA1 != null || ProsodyA2 != null || ProsodyA3 != null;

// 変更後
public bool HasProsody => ProsodyFlat != null;
```

---

## 4. 影響を受ける型・メソッド一覧

### 4.1 Runtime 変更 (破壊的変更あり)

| ファイル | 型/メソッド | 変更内容 |
|---------|-----------|---------|
| `MultilingualPhonemizer.cs` | `MultilingualPhonemizeResult` | `ProsodyA1/A2/A3` → `int[] ProsodyFlat` |
| `MultilingualPhonemizer.cs` | `PhonemizeWithProsodyAsync()` | `allA1/allA2/allA3` → `allProsodyFlat` (List\<int\>) |
| `MultilingualPhonemizer.cs` | `ProcessJapanese()` 他7メソッド | 戻り値 `(phonemes, a1, a2, a3)` → `(phonemes, int[] prosodyFlat)` |
| `MultilingualPhonemizer.cs` | `ExtractProsodyArrays()` | stride=3 フラット配列を返すよう変更 |
| `MultilingualPhonemizer.cs` | `PadToLength()` | stride=3 対応（3要素ずつパディング） |
| `SynthesisRequest.cs` | `SynthesisRequest` | `ProsodyA1/A2/A3` → `int[] ProsodyFlat` |
| `PhonemeEncoder.cs` | `ProsodyEncodingResult` | `ExpandedProsodyA1/A2/A3` → `int[] ExpandedProsodyFlat` |
| `PhonemeEncoder.cs` | `EncodeWithProsody()` | 3本の `List<int>` → 1本の `List<int>` (stride=3) |
| `PhonemeEncoder.cs` | `AddToken()`, `AddPadToken()`, `EncodePhonemeTs()` | 3引数 → フラット配列操作 |
| `IInferenceAudioGenerator.cs` | `GenerateAudioAsync()` | `prosodyA1/A2/A3` → `int[] prosodyFlat` |
| `InferenceAudioGenerator.cs` | `GenerateAudioAsync()` | シグネチャ変更 |
| `InferenceAudioGenerator.cs` | `ExecuteInference()` | `prosodyA1/A2/A3` → `prosodyFlat` |
| `InferenceAudioGenerator.cs` | `PrepareInputs()` | `prosodyA1/A2/A3` → `prosodyFlat` |
| `InferenceAudioGenerator.cs` | `CreateProsodyTensorPooled()` | 3本配列受け取り → フラット配列受け取り（インターリーブ不要に） |
| `InferenceAudioGenerator.cs` | `ExecuteWarmup()` | `dummyProsodyA1/A2/A3` → `dummyProsodyFlat` |
| `InferenceAudioGenerator.cs` | `InferenceContext` | `Prosody` フィールドは変更なし（既に単一テンソル） |
| `TTSSynthesisOrchestrator.cs` | `SynthesizeAsync()` | `expandedA1/A2/A3` → `expandedProsodyFlat` |
| `SplitInferenceOrchestrator.cs` | `GenerateWithSilenceSplitAsync()` | `prosodyA1/A2/A3` → `prosodyFlat` |
| `PhonemeSilenceProcessor.cs` | `Phrase` | `ProsodyA1/A2/A3` → `int[] ProsodyFlat` |
| `PhonemeSilenceProcessor.cs` | `SplitAtPhonemeSilence()` | 3配列 → フラット配列スライス |
| `PiperTTS.Inference.cs` | `GenerateAudioWithMultilingualAsync()` | `prosodyA1/A2/A3` → `prosodyFlat` |
| `PhonemeOptions.cs` | `PhonemeResult` | `ProsodyA1/A2/A3` → `int[] ProsodyFlat` |
| `PhonemeOptions.cs` | `PhonemeResult.Clone()` / `DeepClone()` | フィールド更新 |

### 4.2 テスト変更

| ファイル | 変更概要 |
|---------|---------|
| `MultilingualPhonemizerTests.cs` | A1/A2/A3 アサーション → ProsodyFlat アサーション |
| `MultilingualPhonemizerDeepTests.cs` | 同上 |
| `MultilingualPhonemizerEosTests.cs` | EOS トリミング時の Prosody 配列操作更新 |
| `MultilingualPhonemizerPhase5Tests.cs` | 同上 |
| `ChinesePhonemizerTests.cs` | 中国語 Prosody アサーション更新 |
| `MultilingualPipelineTests.cs` | エンドツーエンドパイプライン更新 |
| `TTSSynthesisOrchestratorTests.cs` | SynthesisRequest 構築更新 |
| `SplitInferenceOrchestratorTests.cs` | Phrase 構造体更新 |
| `StubInferenceAudioGenerator.cs` | `GenerateAudioAsync` シグネチャ更新 |
| `PhonemeSilenceProcessorTests.cs` | SplitAtPhonemeSilence 引数更新 |
| `ProsodyInferenceIntegrationTests.cs` | 統合テスト更新 |
| `PhonemeEncoderMultilingualTests.cs` | EncodeWithProsody 更新 |
| `PhonemeEncoderMultilingualModelTests.cs` | 同上 |
| `DotNetG2PPhonemizerTest.cs` | ProsodyA1/A2/A3 参照更新 |

---

## 5. ONNX テンソル入力の変更

### 5.1 変更前 (`CreateProsodyTensorPooled`)

```csharp
// 3本の配列からインターリーブしてフラット化
for (var i = 0; i < sequenceLength; i++)
{
    rentedArray[i * 3 + 0] = prosodyA1?[i] ?? 0;
    rentedArray[i * 3 + 1] = prosodyA2?[i] ?? 0;
    rentedArray[i * 3 + 2] = prosodyA3?[i] ?? 0;
}
return new Tensor<int>(new TensorShape(1, sequenceLength, 3), exactData);
```

### 5.2 変更後

```csharp
// フラット配列をそのまま使用（インターリーブ済み）
private Tensor<int> CreateProsodyTensorPooled(
    int sequenceLength, int[] prosodyFlat, out int[] rentedArray)
{
    var prosodySize = sequenceLength * 3;
    if (prosodyFlat != null && prosodyFlat.Length == prosodySize)
    {
        // フラット配列をそのままコピー（stride=3 レイアウト一致）
        rentedArray = ArrayPool<int>.Shared.Rent(prosodySize);
        Array.Copy(prosodyFlat, rentedArray, prosodySize);
    }
    else
    {
        // Prosody なし or サイズ不一致: ゼロ埋め
        rentedArray = ArrayPool<int>.Shared.Rent(prosodySize);
        Array.Clear(rentedArray, 0, prosodySize);
    }

    var exactData = new int[prosodySize];
    Array.Copy(rentedArray, exactData, prosodySize);
    return new Tensor<int>(new TensorShape(1, sequenceLength, 3), exactData);
}
```

**メリット**:
- インターリーブループ (`i * 3 + 0/1/2`) が不要
- `Array.Copy` 1回で済む（null チェック3回 + 境界チェック3回のループが消える）
- piper-plus と同一のメモリレイアウトが保証される

---

## 6. 詳細変更仕様

### 6.1 MultilingualPhonemizeResult

```csharp
// 変更前
public class MultilingualPhonemizeResult
{
    public string[] Phonemes { get; set; }
    public int[] ProsodyA1 { get; set; }
    public int[] ProsodyA2 { get; set; }
    public int[] ProsodyA3 { get; set; }
    public string DetectedPrimaryLanguage { get; set; }
}

// 変更後
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

### 6.2 SynthesisRequest

```csharp
// 変更後
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

### 6.3 IInferenceAudioGenerator.GenerateAudioAsync

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

### 6.4 ProsodyEncodingResult

```csharp
// 変更後
public class ProsodyEncodingResult
{
    public int[] PhonemeIds { get; set; }

    /// <summary>
    /// Flat prosody array aligned with PhonemeIds (stride=3).
    /// Length = PhonemeIds.Length * 3.
    /// </summary>
    public int[] ExpandedProsodyFlat { get; set; }
}
```

### 6.5 PhonemeEncoder.EncodeWithProsody

```csharp
// 変更前シグネチャ
public ProsodyEncodingResult EncodeWithProsody(
    string[] phonemes, int[] prosodyA1, int[] prosodyA2, int[] prosodyA3)

// 変更後シグネチャ
public ProsodyEncodingResult EncodeWithProsody(
    string[] phonemes, int[] prosodyFlat)
```

内部実装: `expandedA1/A2/A3` の3本の `List<int>` を1本の `List<int>` (stride=3) に統合。

```csharp
// BOS トークン追加時
expandedProsody.Add(0); // a1
expandedProsody.Add(0); // a2
expandedProsody.Add(0); // a3

// 音素追加時 (i 番目)
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

### 6.6 PhonemeSilenceProcessor.Phrase

```csharp
// 変更後
public readonly struct Phrase
{
    public readonly int[] PhonemeIds;

    /// <summary>
    /// Flat prosody slice (stride=3, length = PhonemeIds.Length * 3),
    /// or null when no prosody data is available.
    /// </summary>
    public readonly int[] ProsodyFlat;

    public readonly int SilenceSamples;

    public Phrase(int[] phonemeIds, int[] prosodyFlat, int silenceSamples)
    { ... }
}
```

### 6.7 MultilingualPhonemizer 言語別メソッド

戻り値型の変更:

```csharp
// 変更前
private (string[] segPhonemes, int[] a1, int[] a2, int[] a3) ProcessJapanese(string text)

// 変更後
private (string[] segPhonemes, int[] prosodyFlat) ProcessJapanese(string text)
```

各メソッド内で A1/A2/A3 をフラット配列に統合:

```csharp
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
    var flat = new int[phonemes.Length * 3];
    for (int i = 0; i < phonemes.Length; i++)
    {
        flat[i * 3 + 0] = i < a1.Length ? a1[i] : 0;
        flat[i * 3 + 1] = i < a2.Length ? a2[i] : 0;
        flat[i * 3 + 2] = i < a3.Length ? a3[i] : 0;
    }
    return (phonemes, flat);
}
```

注: `DotNetG2PPhonemizer.PhonemizeWithProsody()` は dot-net-g2p パッケージの API であり、uPiper 側では変更しない。uPiper 側の `ProcessXxx()` 内でフラット化する。

### 6.8 PhonemizeWithProsodyAsync 内部

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

EOS トリミング時の配列スライス:

```csharp
// 変更前
segA1 = segA1.Length > 0 ? segA1[..^1] : segA1;
segA2 = segA2.Length > 0 ? segA2[..^1] : segA2;
segA3 = segA3.Length > 0 ? segA3[..^1] : segA3;

// 変更後 (stride=3 なので末尾3要素を除去)
if (segProsodyFlat.Length >= 3)
    segProsodyFlat = segProsodyFlat[..^3];
```

PadToLength:

```csharp
// 変更前
PadToLength(allA1, maxLen);
PadToLength(allA2, maxLen);
PadToLength(allA3, maxLen);

// 変更後 (stride=3 でパディング)
var targetFlatLen = maxLen * 3;
while (allProsodyFlat.Count < targetFlatLen)
{
    allProsodyFlat.Add(0); // a1
    allProsodyFlat.Add(0); // a2
    allProsodyFlat.Add(0); // a3
}
```

---

## 7. ヘルパー定数・ユーティリティ

### 7.1 定数定義

```csharp
/// <summary>
/// Prosody flat array stride (A1, A2, A3 per phoneme).
/// </summary>
internal const int ProsodyStride = 3;
```

配置候補:
- `PhonemeEncoder` 内 (既存の特殊トークン定数と並置)
- または `ProsodyConstants` static class を新設

### 7.2 ヘルパーメソッド候補

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

---

## 8. テスト更新計画

### 8.1 既存テストの更新パターン

**アサーション更新**:
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

**テストデータ構築**:
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

### 8.2 新規テスト追加

| テストケース | 内容 |
|------------|------|
| `ProsodyFlat_NullMeansNoProsody` | `ProsodyFlat = null` → `HasProsody == false` |
| `ProsodyFlat_StrideLayout` | stride=3 レイアウト検証 |
| `ProsodyFlat_EmptyPhonemes` | 空配列 → `ProsodyFlat = Array.Empty<int>()` |
| `ProsodyFlat_PadToLength` | パディング時に3要素単位 |
| `ProsodyFlat_EosTrimming` | EOS トリミング時に末尾3要素除去 |
| `ProsodyFlat_TensorShape` | テンソル `[1, N, 3]` との整合性 |

### 8.3 テストファイル一覧（14ファイル）

全テストファイルは前述の 4.2 節を参照。各ファイルで `ProsodyA1` / `ProsodyA2` / `ProsodyA3` を `ProsodyFlat` に置換する。

---

## 9. 実装順序

1. **定数・ヘルパー追加**: `ProsodyStride` 定数、`FlattenProsody()` ヘルパー
2. **データ型変更 (bottom-up)**:
   1. `ProsodyEncodingResult` → `ExpandedProsodyFlat`
   2. `SynthesisRequest` → `ProsodyFlat`
   3. `PhonemeSilenceProcessor.Phrase` → `ProsodyFlat`
   4. `MultilingualPhonemizeResult` → `ProsodyFlat`
   5. `PhonemeResult` → `ProsodyFlat`
3. **メソッドシグネチャ変更**:
   1. `IInferenceAudioGenerator.GenerateAudioAsync()`
   2. `InferenceAudioGenerator` 実装
   3. `PhonemeEncoder.EncodeWithProsody()`
   4. `SplitInferenceOrchestrator.GenerateWithSilenceSplitAsync()`
   5. `PhonemeSilenceProcessor.SplitAtPhonemeSilence()`
   6. `TTSSynthesisOrchestrator.SynthesizeAsync()`
4. **MultilingualPhonemizer 内部**:
   1. `ProcessXxx()` 7メソッドの戻り値変更
   2. `PhonemizeWithProsodyAsync()` 内部ロジック
5. **PiperTTS.Inference.cs**: 呼び出し側更新
6. **テスト更新**: 全14ファイル
7. **StubInferenceAudioGenerator**: シグネチャ更新

---

## 10. 破壊的変更まとめ

| 変更 | 影響 |
|------|------|
| `MultilingualPhonemizeResult.ProsodyA1/A2/A3` 削除 | public class のプロパティ変更 |
| `IInferenceAudioGenerator.GenerateAudioAsync()` シグネチャ | public interface の変更 |
| `PhonemeResult.ProsodyA1/A2/A3` 削除 | public class のプロパティ変更 |

`SynthesisRequest` / `ProsodyEncodingResult` / `PhonemeSilenceProcessor.Phrase` は internal のため外部互換性影響なし。

---

## 11. 非変更項目

| 項目 | 理由 |
|------|------|
| `DotNetG2PPhonemizer` の API | dot-net-g2p は別リポジトリ。uPiper 側の `ProcessJapanese()` 内でフラット化する |
| 各 DotNetG2P エンジンの `ToIpaWithProsody()` | 同上。Prosody 構造体 (`ProsodyInfo { A1, A2, A3 }`) はそのまま |
| ONNX テンソル形状 `[1, N, 3]` | 変更なし（piper-plus と既に一致） |
| `int` 型 (vs piper-plus の `long`) | Unity Sentis が `Tensor<int>` を要求。piper-plus C# は ONNX Runtime の `long` テンソルを使用するため型が異なるが、ONNX モデル側の入力定義で吸収される |
