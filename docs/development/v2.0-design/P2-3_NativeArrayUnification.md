# P2-3: NativeArray 統一 設計ドキュメント

**マイルストーン**: v2.0 Phase 2 (データモデル互換性)
**優先度**: P2
**ステータス**: 設計中
**見積もり**: 2人日（実装1.5日 + テスト・調整0.5日）
**依存チケット**: なし（他項目と独立して実施可能。P2-2 Prosody フラット配列化と同時が効率的）
**後続チケット**: P3-5 AudioNormalizer 切り出し（NativeArray 版を同時実装すると効率的）
**Unity 最低要件**: Unity 2023.1+

---

## 1. タスク目的とゴール

`InferenceAudioGenerator.ExtractResults()` から `AudioClip.SetData()` に至るデータフローは、現在 `float[]` 中間バッファを複数回経由しており、不要なメモリコピーとGCアロケーションが発生している。本チケットでは `NativeArray<float>` をデータパイプライン全体の統一型として採用し、中間バッファの完全排除を目指す。

**ゴール**:
- `InferenceAudioGenerator` の推論結果出力が `NativeArray<float>` になっている
- `AudioClipBuilder.BuildAudioClip` が `NativeArray<float>` を受け付ける
- `NormalizeAudioInPlace` が `NativeArray<float>` 上で直接動作する
- `SplitInferenceOrchestrator` のチャンク結合が `NativeArray<float>` で行われる
- `float[]` 中間バッファ経由のコピーが完全に排除されている
- メモリ所有権（Allocator / Dispose 責務）が全箇所で明確に文書化されている
- 既存テストが全て通過する

---

## 2. 現状分析

### 2.1 float[] コピーチェーン全体図

```
[GPU] Sentis Worker 推論完了
  │
  ▼ (1) PeekOutput() → Worker所有 Tensor<float> (GPU上)
  │
  ▼ (2) ReadbackAndClone() → CPU上の新規 Tensor<float> (readableTensor)
  │
  ▼ (3) new float[audioLength] + for loop コピー  ← ★ GCアロケーション + 要素単位コピー
  │     readableTensor.Dispose()
  │
  ▼ (4) ExecuteInference() → float[] を返却
  │
  ├─── [直接パス] TTSSynthesisOrchestrator.SynthesizeAsync()
  │     │
  │     ▼ (5) NormalizeAudioInPlace(float[])  ← in-place なのでコピーなし
  │     │
  │     ▼ (6) BuildAudioClip(float[]) → AudioClip.Create() + AudioClip.SetData(float[])
  │           ← ★ Unity内部で float[] → AudioClip内部バッファへコピー
  │
  └─── [句分割パス] SplitInferenceOrchestrator.GenerateWithSilenceSplitAsync()
        │
        ▼ (4a) 各句の float[] を List<(float[], int)> に蓄積
        │
        ▼ (4b) new float[totalLength] + Array.Copy で結合  ← ★ GCアロケーション + バルクコピー
        │
        ▼ (5) NormalizeAudioInPlace(float[])
        │
        ▼ (6) BuildAudioClip(float[]) → AudioClip.SetData(float[])
```

### 2.2 問題箇所の特定

| # | 箇所 | ファイル:行 | 問題 |
|---|------|------------|------|
| 1 | `ExtractResults()` | `InferenceAudioGenerator.cs:439-457` | `new float[audioLength]` + for ループ要素単位コピー。Tensor の内部 NativeArray を直接取得可能であればコピー不要 |
| 2 | `SplitInferenceOrchestrator` 結合 | `SplitInferenceOrchestrator.cs:80-87` | `new float[totalLength]` + 複数 `Array.Copy`。句数に比例する GC 圧力 |
| 3 | `AudioClip.SetData(float[])` | `AudioClipBuilder.cs:45` | Unity 2023.1+ では `SetData(NativeArray<float>)` オーバーロードが利用可能。managed→native コピーを回避可能 |
| 4 | `NormalizeAudio()` (非in-place版) | `AudioClipBuilder.cs:60-96` | `new float[]` で新規配列を作成。呼び出し元は `TTSSynthesisOrchestrator` では使用されていない（in-place 版を使用）が、public API として残存 |

### 2.3 現在の float[] 使用箇所一覧

| コンポーネント | メソッド | float[] の役割 |
|--------------|---------|---------------|
| `IInferenceAudioGenerator` | `GenerateAudioAsync()` 戻り値 | 推論結果の音声データ |
| `InferenceAudioGenerator` | `ExecuteInference()` 戻り値 | 推論結果の音声データ |
| `InferenceAudioGenerator` | `ExtractResults()` 戻り値 | Tensor→float[] 変換結果 |
| `SplitInferenceOrchestrator` | `GenerateWithSilenceSplitAsync()` 戻り値 | 結合済み音声データ |
| `SplitInferenceOrchestrator` | `segments` リスト | 各句の音声データ + 無音サンプル数 |
| `TTSSynthesisOrchestrator` | `audioData` ローカル変数 | 正規化前の音声データ |
| `AudioClipBuilder` | `BuildAudioClip()` パラメータ | AudioClip に設定するデータ |
| `AudioClipBuilder` | `NormalizeAudioInPlace()` パラメータ | 正規化対象データ |
| `StubInferenceAudioGenerator` | `AudioDataToReturn` / `GenerateAudioAsync()` | テスト用固定データ |
| `AudioChunk` | `Samples` プロパティ | ストリーミング用チャンクデータ |
| `PiperTTS.cs:1340` | レガシー無音生成 | ダミーAudioClip 用 |

---

## 3. NativeArray<float> への移行設計

### 3.1 Unity Sentis/InferenceEngine の Tensor → NativeArray API

Unity InferenceEngine (Sentis) の `Tensor<float>` は以下の方法で NativeArray にアクセスできる:

```csharp
// 方法A: ReadbackAndClone() → CPU Tensor → NativeArrayへの手動コピー（現状）
var cpuTensor = gpuTensor.ReadbackAndClone();
// cpuTensor[i] でアクセス → 要素単位コピーが必要

// 方法B: CompleteOperationsAndDownload() → ReadOnlyNativeArray 取得（推奨）
var cpuTensor = gpuTensor.ReadbackAndClone();
var nativeArray = cpuTensor.ToReadOnlyNativeArray();
// NativeArray<float>.CopyFrom / NativeArray<float>.CopyTo で一括コピー

// 方法C: Tensor コンストラクタの dataOnDevice パラメータ経由
// Sentis 内部の BackendNativeArray 経由でアクセス（非公開API、使用不可）
```

**採用方針**: 方法B を基本戦略とする。`ReadbackAndClone()` 後に `ToReadOnlyNativeArray()` で ReadOnlyNativeArray を取得し、そこから `NativeArray<float>` にコピーする。ただし、`ReadbackAndClone()` が返す Tensor は Dispose 時に内部 NativeArray も解放するため、Tensor のライフタイムを呼び出し元で管理する必要がある。

**最適パス**: Tensor の内部データを直接 NativeArray として取得し、所有権を移転できれば理想的だが、Sentis の公開 API ではサポートされていない。そのため、Tensor → NativeArray への1回のバルクコピーは発生するが、現状の要素単位 for ループコピーから NativeArray.CopyFrom による memcpy レベルのバルクコピーに改善される。

### 3.2 変更対象コンポーネント

#### 3.2.1 IInferenceAudioGenerator（インターフェース変更）

**ファイル**: `Assets/uPiper/Runtime/Core/AudioGeneration/IInferenceAudioGenerator.cs`

```csharp
// Before
public Task<float[]> GenerateAudioAsync(
    int[] phonemeIds,
    int[] prosodyA1 = null, int[] prosodyA2 = null, int[] prosodyA3 = null,
    float lengthScale = 1.0f, float noiseScale = 0.667f, float noiseW = 0.8f,
    int speakerId = 0, int languageId = 0,
    CancellationToken cancellationToken = default);

// After
public Task<NativeArray<float>> GenerateAudioAsync(
    int[] phonemeIds,
    int[] prosodyA1 = null, int[] prosodyA2 = null, int[] prosodyA3 = null,
    float lengthScale = 1.0f, float noiseScale = 0.667f, float noiseW = 0.8f,
    int speakerId = 0, int languageId = 0,
    CancellationToken cancellationToken = default);
```

**breaking change**: public インターフェースの戻り値型変更。

#### 3.2.2 InferenceAudioGenerator（推論エンジン）

**ファイル**: `Assets/uPiper/Runtime/Core/AudioGeneration/InferenceAudioGenerator.cs`

`ExtractResults()` を NativeArray 返却に変更:

```csharp
// Before (L439-457)
private float[] ExtractResults()
{
    var outputTensor = GetOutputTensor();
    var readableTensor = outputTensor.ReadbackAndClone();
    try
    {
        var audioLength = readableTensor.shape.length;
        var audioData = new float[audioLength];
        for (var i = 0; i < audioLength; i++)
        {
            audioData[i] = readableTensor[i];
        }
        return audioData;
    }
    finally
    {
        readableTensor.Dispose();
    }
}

// After
private NativeArray<float> ExtractResults()
{
    var outputTensor = GetOutputTensor(); // Worker-owned; do not Dispose
    var readableTensor = outputTensor.ReadbackAndClone();
    try
    {
        var audioLength = readableTensor.shape.length;
        var audioData = new NativeArray<float>(audioLength, Allocator.Persistent);
        // ReadOnlyNativeArray 経由のバルクコピー
        var readOnlyArray = readableTensor.ToReadOnlyNativeArray();
        audioData.CopyFrom(readOnlyArray.ToArray());
        // 注: ToReadOnlyNativeArray() が NativeArray<float> を返す場合は
        // NativeArray<float>.CopyFrom(NativeArray<float>) で直接コピー可能
        return audioData;
    }
    catch
    {
        // 例外時は確保済み NativeArray を解放（所有権移転前のため）
        // audioData が未初期化の場合を考慮し、finally ではなく catch で処理
        throw;
    }
    finally
    {
        readableTensor.Dispose();
    }
}
```

**注**: Sentis API の正確な `ToReadOnlyNativeArray()` 戻り値型に応じて、最も効率的なコピー方法を選択する。`NativeArray<T>.CopyFrom(NativeArray<T>)` が使用可能であれば memcpy 相当のバルクコピーとなる。

`ExecuteInference()` および `GenerateAudioAsync()` の戻り値型も連動して変更:

```csharp
private NativeArray<float> ExecuteInference(...) { ... }
public async Task<NativeArray<float>> GenerateAudioAsync(...) { ... }
```

#### 3.2.3 AudioClipBuilder（AudioClip構築）

**ファイル**: `Assets/uPiper/Runtime/Core/AudioGeneration/AudioClipBuilder.cs`

```csharp
// NativeArray<float> オーバーロードを追加
public AudioClip BuildAudioClip(NativeArray<float> audioData, int sampleRate, string clipName = null)
{
    if (audioData.Length == 0)
        throw new ArgumentException("Audio data cannot be empty", nameof(audioData));

    if (sampleRate <= 0)
        throw new ArgumentException("Sample rate must be positive", nameof(sampleRate));

    var name = string.IsNullOrEmpty(clipName)
        ? $"GeneratedAudio_{DateTime.Now:yyyyMMddHHmmss}" : clipName;

    var audioClip = AudioClip.Create(
        name: name,
        lengthSamples: audioData.Length,
        channels: 1,
        frequency: sampleRate,
        stream: false);

    // Unity 2023.1+ の NativeArray オーバーロード使用
    if (!audioClip.SetData(audioData, 0))
        throw new InvalidOperationException("Failed to set audio data to AudioClip");

    PiperLogger.LogDebug($"Created AudioClip: {name}, {audioData.Length} samples, {sampleRate}Hz");
    return audioClip;
}

// NativeArray 版 NormalizeAudioInPlace
public void NormalizeAudioInPlace(NativeArray<float> audioData, float targetPeak = 0.95f)
{
    if (audioData.Length == 0)
        return;

    targetPeak = Mathf.Clamp01(targetPeak);

    var maxAmplitude = 0f;
    for (var i = 0; i < audioData.Length; i++)
    {
        var absValue = Mathf.Abs(audioData[i]);
        if (absValue > maxAmplitude)
            maxAmplitude = absValue;
    }

    if (maxAmplitude <= 0f || Mathf.Approximately(maxAmplitude, targetPeak))
        return;

    var scale = targetPeak / maxAmplitude;
    for (var i = 0; i < audioData.Length; i++)
    {
        audioData[i] *= scale;
    }

    PiperLogger.LogDebug($"Normalized audio in-place: max amplitude {maxAmplitude:F3} -> {targetPeak:F3}");
}
```

**旧 float[] オーバーロードの扱い**: `BuildAudioClip(float[], ...)` と `NormalizeAudioInPlace(float[], ...)` は `[Obsolete]` を付与して残し、v2.x 中に削除する。`AudioChunk.ToAudioClip()` / `PiperTTS.cs:1340` のレガシーコードが依存しているため、段階的移行が必要。

#### 3.2.4 SplitInferenceOrchestrator（句分割結合）

**ファイル**: `Assets/uPiper/Runtime/Core/AudioGeneration/SplitInferenceOrchestrator.cs`

```csharp
// Before
public async Task<float[]> GenerateWithSilenceSplitAsync(...)
{
    ...
    var segments = new List<(float[] Audio, int SilenceSamples)>();
    ...
    foreach (...) {
        var phraseAudio = await _generator.GenerateAudioAsync(...);
        segments.Add((phraseAudio, phrase.SilenceSamples));
    }
    var result = new float[totalLength];
    var offset = 0;
    foreach (var (audio, silenceSamples) in segments) {
        Array.Copy(audio, 0, result, offset, audio.Length);
        offset += audio.Length + silenceSamples;
    }
    return result;
}

// After
public async Task<NativeArray<float>> GenerateWithSilenceSplitAsync(...)
{
    ...
    var segments = new List<(NativeArray<float> Audio, int SilenceSamples)>();
    ...
    try
    {
        foreach (...) {
            var phraseAudio = await _generator.GenerateAudioAsync(...);
            segments.Add((phraseAudio, phrase.SilenceSamples));
            totalLength += phraseAudio.Length + phrase.SilenceSamples;
        }

        // 一括確保 + NativeSlice でコピー
        var result = new NativeArray<float>(totalLength, Allocator.Persistent);
        var offset = 0;
        foreach (var (audio, silenceSamples) in segments) {
            NativeArray<float>.Copy(audio, 0, result, offset, audio.Length);
            offset += audio.Length;
            // 無音区間は NativeArray のゼロ初期化に依存
            // Allocator.Persistent はゼロ初期化されないため、明示的にクリアが必要
            if (silenceSamples > 0)
            {
                // NativeArray は部分クリアの直接 API がないため、
                // ゼロ埋めの NativeArray.Copy または unsafe memset を検討
                var silence = new NativeArray<float>(silenceSamples, Allocator.Temp);
                NativeArray<float>.Copy(silence, 0, result, offset, silenceSamples);
                silence.Dispose();
            }
            offset += silenceSamples;
        }
        return result;
    }
    finally
    {
        // 句ごとの NativeArray を解放（所有権は result に移転済み）
        foreach (var (audio, _) in segments)
        {
            if (audio.IsCreated)
                audio.Dispose();
        }
    }
}
```

**無音区間のゼロ埋め**: `Allocator.Persistent` で確保した `NativeArray` はゼロ初期化が保証されない。`NativeArrayOptions.ClearMemory` を指定するか、無音区間を明示的にクリアする必要がある。

```csharp
// オプション1: 確保時にゼロ初期化（推奨、最もシンプル）
var result = new NativeArray<float>(totalLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
// → 無音区間の明示的クリアが不要になる

// オプション2: UnsafeUtility.MemClear で部分クリア
unsafe
{
    UnsafeUtility.MemClear(
        (byte*)result.GetUnsafePtr() + offset * sizeof(float),
        silenceSamples * sizeof(float));
}
```

**採用**: オプション1（`NativeArrayOptions.ClearMemory`）を推奨。コードの明瞭さを優先し、ゼロ初期化のオーバーヘッドは音声合成の推論時間に比べて無視できるレベル。

#### 3.2.5 TTSSynthesisOrchestrator（パイプライン統合）

**ファイル**: `Assets/uPiper/Runtime/Core/AudioGeneration/TTSSynthesisOrchestrator.cs`

```csharp
// Before (L77-109)
float[] audioData;
if (useSilenceSplit) {
    audioData = await _splitOrchestrator.GenerateWithSilenceSplitAsync(...);
} else {
    audioData = await _generator.GenerateAudioAsync(...);
}
_audioClipBuilder.NormalizeAudioInPlace(audioData, 0.95f);
var clip = _audioClipBuilder.BuildAudioClip(audioData, ...);

// After
NativeArray<float> audioData = default;
try
{
    if (useSilenceSplit) {
        audioData = await _splitOrchestrator.GenerateWithSilenceSplitAsync(...);
    } else {
        audioData = await _generator.GenerateAudioAsync(...);
    }
    _audioClipBuilder.NormalizeAudioInPlace(audioData, 0.95f);
    var clip = _audioClipBuilder.BuildAudioClip(audioData, ...);
    return clip;
}
finally
{
    if (audioData.IsCreated)
        audioData.Dispose();
}
```

**Dispose タイミング**: `AudioClip.SetData(NativeArray<float>)` は NativeArray の内容を AudioClip 内部バッファにコピーするため、SetData 完了後に NativeArray を Dispose しても安全。

---

## 4. メモリ所有権モデル

### 4.1 Allocator 選択

| 箇所 | Allocator | 理由 |
|------|-----------|------|
| `ExtractResults()` 戻り値 | `Persistent` | フレーム跨ぎで使用される。GenerateAudioAsync → TTSSynthesisOrchestrator → BuildAudioClip のチェーンを通過するため |
| `SplitInferenceOrchestrator` 結合バッファ | `Persistent` | 同上 |
| `SplitInferenceOrchestrator` 無音一時バッファ | `Temp` | Copy 完了後に即 Dispose |
| テスト用スタブデータ | `Persistent` | テストメソッドのライフタイムに依存 |

**`Allocator.TempJob` は不使用**: TempJob は4フレーム以内の解放が必要。音声合成は async メソッドチェーンで複数フレームにまたがる可能性があるため不適切。

### 4.2 Dispose 責務チェーン

```
InferenceAudioGenerator.ExtractResults()
  → NativeArray<float> を確保（Allocator.Persistent）
  → 所有権を呼び出し元に移転
  ↓
InferenceAudioGenerator.GenerateAudioAsync()
  → 所有権を呼び出し元に移転（SplitInferenceOrchestrator or TTSSynthesisOrchestrator）
  ↓
┌──────────────────────────────────────┐
│ [句分割パス]                          │
│ SplitInferenceOrchestrator            │
│   → 各句の NativeArray を蓄積        │
│   → 結合 NativeArray を確保          │
│   → 各句の NativeArray を Dispose    │
│   → 結合 NativeArray の所有権を移転   │
└──────────────────────────────────────┘
  ↓
TTSSynthesisOrchestrator.SynthesizeAsync()
  → NormalizeAudioInPlace（in-place、所有権変更なし）
  → BuildAudioClip（SetData 後にコピー完了）
  → NativeArray を Dispose  ★ 最終 Dispose 地点
  → AudioClip を返却
```

### 4.3 例外安全性

全ての NativeArray 確保箇所で try-finally パターンを適用し、例外時のメモリリークを防止する:

```csharp
NativeArray<float> audioData = default;
try
{
    audioData = await _generator.GenerateAudioAsync(...);
    // ... 処理 ...
}
finally
{
    if (audioData.IsCreated)
        audioData.Dispose();
}
```

**`default` 初期化**: `NativeArray<float>` は struct のため、`default` で初期化すると `IsCreated == false` となる。try ブロックの途中で例外が発生しても、未確保の NativeArray に対して Dispose が呼ばれるリスクを `IsCreated` チェックで回避する。

---

## 5. AudioClip.SetData() との接続

### 5.1 Unity API

Unity 2023.1+ では `AudioClip.SetData` に以下のオーバーロードが存在する:

```csharp
// 既存（Unity 全バージョン）
public bool SetData(float[] data, int offsetSamples);

// Unity 2023.1+ 追加
public bool SetData(NativeArray<float> data, int offsetSamples);
```

`NativeArray<float>` オーバーロードは managed → native のマーシャリングコストを回避し、NativeArray のメモリ領域から直接 AudioClip 内部バッファにコピーする。

### 5.2 データフロー（移行後）

```
[GPU] Sentis Worker 推論完了
  │
  ▼ PeekOutput() → Worker所有 Tensor<float>
  │
  ▼ ReadbackAndClone() → CPU Tensor<float>
  │
  ▼ NativeArray<float>(Allocator.Persistent) + バルクコピー  ← ★ 1回の memcpy
  │  readableTensor.Dispose()
  │
  ▼ NormalizeAudioInPlace(NativeArray<float>)  ← in-place
  │
  ▼ AudioClip.SetData(NativeArray<float>)  ← ★ managed marshalling 回避
  │
  ▼ audioData.Dispose()  ← 最終解放
```

**コピー回数の比較**:

| ステップ | Before (float[]) | After (NativeArray) |
|---------|-----------------|---------------------|
| Tensor → CPU | ReadbackAndClone (GPU→CPU DMA) | ReadbackAndClone (GPU→CPU DMA) |
| CPU Tensor → バッファ | for ループ要素単位コピー | バルクコピー (memcpy 相当) |
| 正規化 | in-place (コピーなし) | in-place (コピーなし) |
| バッファ → AudioClip | SetData(float[]) managed marshalling | SetData(NativeArray) 直接コピー |
| **合計コピー** | **2回 (要素単位 + marshalling)** | **2回 (バルク + 直接)** |

コピー回数自体は同じ2回だが、要素単位ループが memcpy 相当のバルクコピーに、managed marshalling が native 直接コピーに改善される。

### 5.3 句分割パスのコピー比較

| ステップ | Before | After |
|---------|--------|-------|
| 各句の Tensor → バッファ | N回の for ループコピー | N回のバルクコピー |
| 句バッファの結合 | new float[total] + N回の Array.Copy | new NativeArray + N回の NativeArray.Copy |
| 結合バッファ → AudioClip | SetData(float[]) | SetData(NativeArray) |
| **GC アロケーション** | **N+1 個の float[]** | **0 個 (NativeArray は unmanaged)** |

GC アロケーションが完全に排除される点が最大のメリット。

---

## 6. Unity バージョン要件

### 6.1 必要 API

| API | 最低バージョン |
|-----|-------------|
| `AudioClip.SetData(NativeArray<float>, int)` | Unity 2023.1 |
| `NativeArray<T>` (Unity.Collections) | Unity 2018.1+ (安定版は 2020.1+) |
| `NativeArray<T>.CopyFrom(NativeArray<T>)` | Unity 2018.1+ |
| `NativeArrayOptions.ClearMemory` | Unity 2019.1+ |
| `Allocator.Persistent` | Unity 2018.1+ |
| Unity InferenceEngine (Sentis) | Unity 2023.1+ (パッケージ要件) |

### 6.2 判断

Unity InferenceEngine (Sentis) 自体が Unity 2023.1+ を要求するため、`AudioClip.SetData(NativeArray<float>)` の利用に追加のバージョン制約は発生しない。`#if` によるバージョン分岐は不要。

---

## 7. パフォーマンス改善の見積もり

### 7.1 コピー速度の改善

| 操作 | Before | After | 改善率 (目安) |
|------|--------|-------|-------------|
| Tensor → バッファコピー (1秒音声 = 22,050 samples) | for ループ: ~50us | memcpy: ~5us | ~10x |
| SetData | managed marshalling: ~30us | native copy: ~10us | ~3x |
| 句分割結合 (3句) | 3x Array.Copy + new float[]: ~80us + GC | 3x NativeArray.Copy: ~15us, GC=0 | ~5x + GC排除 |

**注**: 上記は概算値。実際の推論時間 (50-200ms) に比べてコピー時間は1%未満であり、レイテンシ改善は限定的。主なメリットは GC 圧力の排除。

### 7.2 GC アロケーション削減

| パス | Before | After |
|------|--------|-------|
| 直接パス | 1x float[] (22KB/秒) | 0 managed allocations |
| 句分割パス (3句) | 4x float[] (22KB x 4 = 88KB) | 0 managed allocations |

長文の連続合成（ナレーション等）では、GC Gen0 コレクションの頻度が大幅に低減される。

---

## 8. プラットフォーム制約

### 8.1 WebGL

- `NativeArray<float>` は WebGL でも利用可能（Unity.Collections パッケージが対応）
- `Allocator.Persistent` は WebGL でも動作する
- `AudioClip.SetData(NativeArray<float>)` の WebGL 対応は Unity 2023.1+ で確認が必要
- **リスク**: WebGL のシングルスレッド環境では、大きな NativeArray の確保/解放がフレーム落ちを引き起こす可能性がある。ただし、現状の `float[]` 確保でも同等のコストが発生しているため、追加のリスクは限定的

### 8.2 IL2CPP

- `NativeArray<T>` は IL2CPP 完全対応
- `unsafe` コードを使用する場合（`UnsafeUtility.MemClear` 等）は `Allow 'unsafe' Code` の有効化が必要
- **推奨**: `unsafe` を回避し、`NativeArrayOptions.ClearMemory` と `NativeArray<T>.CopyFrom` のみで実装する

### 8.3 Android / iOS

- `NativeArray<float>` はモバイルプラットフォームでも問題なく動作
- `Allocator.Persistent` はヒープメモリを使用するため、メモリ制約の厳しい端末では確保失敗のリスクがある（ただし float[] でも同等）

---

## 9. テスト計画

### 9.1 StubInferenceAudioGenerator の更新

```csharp
// Before
public float[] AudioDataToReturn { get; set; }
public Task<float[]> GenerateAudioAsync(...) { ... }

// After
public NativeArray<float> AudioDataToReturn { get; set; }
private bool _audioDataIsCreated;

public Task<NativeArray<float>> GenerateAudioAsync(...)
{
    GenerateCallCount++;
    ...
    if (_audioDataIsCreated)
        return Task.FromResult(AudioDataToReturn);
    return Task.FromResult(CreateDefaultAudioData());
}

private static NativeArray<float> CreateDefaultAudioData()
{
    var data = new NativeArray<float>(100, Allocator.Persistent);
    for (var i = 0; i < data.Length; i++)
        data[i] = 0.1f;
    return data;
}
```

**注意**: テストの TearDown で NativeArray を Dispose する必要がある。テストスタブが返す NativeArray は呼び出し元（TTSSynthesisOrchestrator）が Dispose するため、テスト側で二重 Dispose しないよう注意。

### 9.2 新規テスト

| テスト名 | 検証内容 |
|---------|---------|
| `ExtractResults_ReturnsNativeArrayWithCorrectLength` | Tensor から NativeArray への変換が正しいサイズであること |
| `GenerateAudioAsync_ReturnsNativeArray_CallerCanDispose` | 呼び出し元が Dispose 可能であること |
| `BuildAudioClip_NativeArray_CreatesValidClip` | NativeArray 版 BuildAudioClip が正しい AudioClip を生成すること |
| `NormalizeAudioInPlace_NativeArray_NormalizesCorrectly` | NativeArray 版正規化が正しく動作すること |
| `SplitInference_NativeArray_DisposesIntermediateArrays` | 句ごとの NativeArray が結合後に Dispose されること |
| `SynthesizeAsync_DisposesNativeArrayAfterSetData` | TTSSynthesisOrchestrator が最終的に NativeArray を Dispose すること |
| `GenerateAudioAsync_Exception_DisposesNativeArray` | 例外発生時に NativeArray がリークしないこと |

### 9.3 既存テストの更新

| テストファイル | 変更内容 |
|-------------|---------|
| `AudioClipBuilderTests.cs` | float[] テストを NativeArray 版に並行追加。TearDown で Dispose |
| `SplitInferenceOrchestratorTests.cs` | StubGenerator の戻り値型を NativeArray に更新。result の Dispose 追加 |
| `TTSSynthesisOrchestratorTests.cs` | 同上 |
| `StubInferenceAudioGenerator.cs` | NativeArray 対応に全面改修 |
| `InferenceAudioGeneratorTests.cs` | 実機テスト: 戻り値型の変更に追従 |

---

## 10. 移行手順

### Step 1: InferenceAudioGenerator (内部変更)
1. `ExtractResults()` を `NativeArray<float>` 返却に変更
2. `ExecuteInference()` の戻り値型を変更
3. `GenerateAudioAsync()` の戻り値型を変更
4. `using Unity.Collections;` は既にインポート済み

### Step 2: IInferenceAudioGenerator (インターフェース変更)
1. `GenerateAudioAsync()` の戻り値型を `Task<NativeArray<float>>` に変更
2. コンパイルエラーが全依存箇所で発生 → 順次修正

### Step 3: AudioClipBuilder (オーバーロード追加)
1. `BuildAudioClip(NativeArray<float>, ...)` 追加
2. `NormalizeAudioInPlace(NativeArray<float>, ...)` 追加
3. 旧 float[] 版に `[Obsolete]` 付与

### Step 4: SplitInferenceOrchestrator (句分割対応)
1. `GenerateWithSilenceSplitAsync()` の戻り値型と内部ロジックを NativeArray 化
2. 句ごとの NativeArray の Dispose 責務を明確化

### Step 5: TTSSynthesisOrchestrator (パイプライン統合)
1. `audioData` を `NativeArray<float>` に変更
2. try-finally で Dispose を保証

### Step 6: StubInferenceAudioGenerator + テスト更新
1. スタブを NativeArray 対応に改修
2. 既存テストの更新
3. 新規テストの追加

### Step 7: 周辺コード更新
1. `AudioChunk.Samples` の NativeArray 化（スコープ外の場合は別チケット）
2. `PiperTTS.cs:1340` のレガシーコード更新
3. CLAUDE.md / architecture-improvement-roadmap.md 更新

---

## 11. リスクと判断事項

### 11.1 Sentis API の NativeArray 取得方法

**リスク**: `Tensor<float>.ToReadOnlyNativeArray()` の正確な API シグネチャと動作は、使用する Sentis バージョン（現在 2.5.0）に依存する。API が存在しない、または ReadOnlyNativeArray が直接 CopyFrom に使用できない場合は、代替手段（インデクサアクセスまたは unsafe ポインタ経由）が必要。

**対策**: 実装開始前に Sentis 2.5.0 の API ドキュメントを確認し、最も効率的なコピーパスを特定する。最悪の場合、`ReadbackAndClone()` + for ループコピーの NativeArray 版（`audioData[i] = readableTensor[i]`）にフォールバック可能。この場合でも GC アロケーション排除のメリットは維持される。

### 11.2 AudioChunk との整合性

**リスク**: `AudioChunk.Samples` が `float[]` のまま残る場合、`AudioChunk` 経由のストリーミングパスは NativeArray の恩恵を受けられない。

**判断**: `AudioChunk` は現在のメインパス（`TTSSynthesisOrchestrator`）からは使用されておらず、レガシーコードの位置づけ。NativeArray 化は別チケットとする。

### 11.3 P2-2 (Prosody フラット配列化) との同時実施

**推奨**: P2-2 と P2-3 は `IInferenceAudioGenerator` のシグネチャを変更する点で共通している。同時実施により、インターフェース変更を1回にまとめることができる。

---

## 12. 不採用とした代替案

| 代替案 | 不採用理由 |
|--------|----------|
| `IMemoryOwner<float>` 戻り値 | `AudioClip.SetData` が NativeArray/float[] を要求するため、Memory → NativeArray/float[] 変換コストが発生。v2.0-plan.md で既に不採用決定済み |
| `Span<float>` / `Memory<float>` ベース | Unity API が `float[]` / `NativeArray<float>` を要求。Span/Memory は Unity API との接続に変換が必要（v1.3.x で不採用決定済み） |
| `Allocator.TempJob` の使用 | async メソッドチェーンで4フレームを超える可能性がある。Persistent が安全 |
| `unsafe` ポインタ直接操作 | IL2CPP 設定の追加要件 + コードの可読性低下。NativeArray API で十分な性能が得られる |
| `#if UNITY_2023_1_OR_NEWER` 分岐 | Sentis 自体が Unity 2023.1+ 必須のため分岐不要。保守コスト削減 |
| Tensor のライフタイム延長 (Dispose 遅延) | Worker が次の推論で出力テンソルを上書きするため、ReadbackAndClone は必須。Tensor の所有権モデルを変更するとメモリリークのリスクが増大 |
