# MS2-2: ArrayPool によるメモリ最適化

**マイルストーン**: [MS2: パフォーマンス改善](../piper-plus-v1.10.0-milestones.md#ms2-パフォーマンス改善)
**優先度**: P1
**ステータス**: 未着手
**見積もり**: 1.5日（実装1日 + テスト・IL2CPPビルド検証0.5日）
**依存チケット**: なし (MS2-1と並行可)
**後続チケット**: MS3-2（推論パイプライン変更が重なるため、本チケット完了後の着手を推奨）

---

## 1. タスク目的とゴール

### 背景

uPiperの推論パイプラインでは、1回の推論ごとに約530KBの一時配列が `new` で割り当てられ、即座にGC対象となっている。リアルタイムTTSでは連続的な音声生成が行われるため、これがGen0 GCの頻発を引き起こし、フレーム落ちやオーディオグリッチの原因となりうる。

### 現状の割り当て量（22050Hz, 3秒テキスト = 約66,150サンプル想定）

| 箇所 | 型 | 要素数 | サイズ |
|------|-----|--------|--------|
| `ExtractAudioData` | `float[]` | 66,150 | ~258KB |
| `NormalizeAudio` | `float[]` | 66,150 | ~258KB |
| `CreateProsodyTensor` | `int[]` | 90～900 | ~0.4～3.5KB |
| **合計** | | | **~530KB** |

### ゴール

- `System.Buffers.ArrayPool<T>.Shared` を使用して上記3箇所の配列をプーリングし、推論ごとのGC割り当てを **~530KB → ~272KB（約49%削減）** に削減する
  - ※ `ExtractAudioData` は public API (`GenerateAudioAsync` / `GenerateAudioWithProsodyAsync`) の戻り値として `float[]` を返すため、ArrayPool 化は安全でない。~100KB 以下を達成するには `ExtractAudioData` の NativeArray 統一等の破壊的変更が必要であり、本チケットのスコープ外。
- 既存の推論結果（音声出力）に一切の変化がないことをテストで保証する
- IL2CPPビルド（iOS/Android）で `ArrayPool<T>` のジェネリック型がストリップされないことを保証する

### piper-plus v1.10.0 での参考実装

`PiperPlus.Core/Inference/PiperSession.cs`（行232-301）で以下のパターンが確立されている:

```csharp
// prosody_features: サイズが64を超える場合にPoolを使用
long[]? rentedProsody = null;
if (prosodySize > 64)
{
    rentedProsody = ArrayPool<long>.Shared.Rent(prosodySize);
    Array.Clear(rentedProsody, 0, prosodySize);
    prosodyArray = rentedProsody;
}
// ...
finally
{
    prosodyTensor?.Dispose();
    if (rentedProsody is not null)
        ArrayPool<long>.Shared.Return(rentedProsody);
}
```

uPiperでもこのパターンを踏襲するが、Unity.InferenceEngine (Sentis) の `Tensor<T>` がONNX Runtimeの `OrtValue` とは異なるライフサイクルを持つため、Tensorのメモリ参照に関して追加の注意が必要。

---

## 2. 実装する内容の詳細

**実装優先順位**: (1) NormalizeAudio の in-place 化（~258KB削減、最大効果）→ (2) CreateProsodyTensor の ArrayPool 化（~0.4-3.5KB削減、Sentis 動作検証後に判断）

### 2-1. ExtractAudioData() の ArrayPool 化（最大効果: ~258KB/推論）

**ファイル**: `Assets/uPiper/Runtime/Core/AudioGeneration/InferenceAudioGenerator.cs`
**メソッド**: `ExtractAudioData()` (行396-407)

**現在のコード**:
```csharp
private float[] ExtractAudioData(Tensor<float> tensor)
{
    var audioLength = tensor.shape.length;
    var audioData = new float[audioLength];   // ← 毎回 new float[66150] (~258KB)

    for (var i = 0; i < audioLength; i++)
    {
        audioData[i] = tensor[i];
    }

    return audioData;
}
```

**変更方針**:

このメソッドは `ExecuteInference()` (行252-341) 内の行316から呼ばれ、その戻り値 `float[]` はそのまま `ExecuteInference` の戻り値として呼び出し元（`PiperTTS.Inference.cs` 行155, 280-288 / `PiperTTS.cs` 行706, 712）に渡される。呼び出し元では `NormalizeAudio()` → `BuildAudioClip()` → `AudioClip.SetData()` の後に参照が不要になる。

しかし、**`GenerateAudioAsync` / `GenerateAudioWithProsodyAsync` のpublic APIが `float[]` を返す**ため、呼び出し元が任意のタイミングで配列を参照し続ける可能性がある。ArrayPoolの配列をそのままpublic APIの戻り値にすると、返却後に別のRentで上書きされるリスクがある。

**対処法**: `ExtractAudioData` 自体のArrayPool化は **行わない**。代わりに、内部パイプライン（`PiperTTS` 側）で `NormalizeAudio` と組み合わせてin-place正規化を行うことでコピー配列の割り当てを削減する（2-3参照）。

ただし、**`ExecuteInference` 内部のスコープに限定された一時バッファ**としてArrayPoolを使う方法は有効:

```csharp
private float[] ExtractAudioData(Tensor<float> tensor)
{
    var audioLength = tensor.shape.length;
    var audioData = new float[audioLength]; // public APIの戻り値なのでnewのまま

    // Tensor要素アクセスの最適化（将来的にTensor.ToArray()が使えればさらに改善）
    for (var i = 0; i < audioLength; i++)
    {
        audioData[i] = tensor[i];
    }

    return audioData;
}
```

**結論**: `ExtractAudioData` の配列はpublic APIの戻り値であり、ArrayPool化は安全でない。この箇所の最適化は2-3のin-place正規化で間接的に達成する。

---

### 2-2. CreateProsodyTensor() の ArrayPool 化（効果: ~0.4～3.5KB/推論）

**ファイル**: `Assets/uPiper/Runtime/Core/AudioGeneration/InferenceAudioGenerator.cs`
**メソッド**: `CreateProsodyTensor()` (行346-359)

**現在のコード**:
```csharp
private Tensor<int> CreateProsodyTensor(int sequenceLength, int[] prosodyA1, int[] prosodyA2, int[] prosodyA3)
{
    var prosodyData = new int[sequenceLength * 3];  // ← 毎回 new int[90～900]
    for (var i = 0; i < sequenceLength; i++)
    {
        prosodyData[i * 3 + 0] = prosodyA1 != null && i < prosodyA1.Length ? prosodyA1[i] : 0;
        prosodyData[i * 3 + 1] = prosodyA2 != null && i < prosodyA2.Length ? prosodyA2[i] : 0;
        prosodyData[i * 3 + 2] = prosodyA3 != null && i < prosodyA3.Length ? prosodyA3[i] : 0;
    }

    return new Tensor<int>(new TensorShape(1, sequenceLength, 3), prosodyData);
}
```

**問題点**: `Tensor<int>` コンストラクタがデータ配列の参照をコピーするかインターナルバッファにコピーするかは `Unity.InferenceEngine` の実装依存。**Tensorが内部でデータ配列への参照を保持する場合**、Tensor.Dispose() 前にArrayPool.Return()すると未定義動作になる。

**変更方針**: piper-plus と同様、以下のパターンで実装する。

1. **Tensor内部バッファ所有権の確認**: `Tensor<T>(TensorShape, T[])` コンストラクタのドキュメントを確認。Sentis (Unity.InferenceEngine) では配列がコピーされるため、Tensor作成後に即座にReturn可能。ただし、バージョンアップでの挙動変更リスクがあるため防御的に実装する。
2. **安全側に倒す**: Tensorが Dispose されるまでReturnしない（piper-plusの `OrtValue` と同じパターン）。

**重要**: 既存の `finally` ブロック（行331-340）で `prosodyTensor?.Dispose()` が呼ばれている。ArrayPool の `Return` は `prosodyTensor.Dispose()` の直後に配置する。既存の `finally` ブロック内に `ArrayPool.Return()` を追加する形とし、外側に新たな try/finally をラップしない。具体的には、行337の `prosodyTensor?.Dispose();` の直後に `if (rentedProsody != null) ArrayPool<int>.Shared.Return(rentedProsody);` を追加する。

```csharp
// ExecuteInference() メソッド内の変更
int[]? rentedProsody = null;
try
{
    if (_supportsProsody)
    {
        int prosodySize = phonemeIds.Length * 3;
        prosodyTensor = CreateProsodyTensorPooled(
            phonemeIds.Length, prosodyA1, prosodyA2, prosodyA3,
            out rentedProsody);
        _worker.SetInput("prosody_features", prosodyTensor);
    }
    // ... 推論実行 ...
}
finally
{
    // 既存のfinallyブロック（行331-340）にReturn処理を追加
    prosodyTensor?.Dispose();
    if (rentedProsody != null)
        ArrayPool<int>.Shared.Return(rentedProsody);
}
```

```csharp
private Tensor<int> CreateProsodyTensorPooled(
    int sequenceLength, int[] prosodyA1, int[] prosodyA2, int[] prosodyA3,
    out int[]? rentedArray)
{
    int prosodySize = sequenceLength * 3;

    int[] prosodyData;
    if (prosodySize > 64)
    {
        rentedArray = ArrayPool<int>.Shared.Rent(prosodySize);
        Array.Clear(rentedArray, 0, prosodySize);
        prosodyData = rentedArray;
    }
    else
    {
        rentedArray = null;
        prosodyData = new int[prosodySize];
    }

    for (var i = 0; i < sequenceLength; i++)
    {
        prosodyData[i * 3 + 0] = prosodyA1 != null && i < prosodyA1.Length ? prosodyA1[i] : 0;
        prosodyData[i * 3 + 1] = prosodyA2 != null && i < prosodyA2.Length ? prosodyA2[i] : 0;
        prosodyData[i * 3 + 2] = prosodyA3 != null && i < prosodyA3.Length ? prosodyA3[i] : 0;
    }

    // Note: Tensor<int>はデータをコピーするため安全だが、
    // 防御的にTensor.Dispose()後にReturnする（piper-plus準拠）
    return new Tensor<int>(new TensorShape(1, sequenceLength, 3), prosodyData);
}
```

**注意**: `ArrayPool.Rent()` は要求サイズ以上の配列を返す可能性がある。`Tensor<int>(TensorShape, int[])` に渡す際、Sentisはshapeに基づいてアクセスするため、配列が大きくても問題ない（shape = `[1, sequenceLength, 3]` なので `sequenceLength * 3` 要素のみ参照される）。ただし、**Sentisの内部実装が `array.Length` を使う場合**は問題になりうるため、実装時に動作検証が必要。もし問題が出る場合は `AsSpan(0, prosodySize).ToArray()` でコピーする（ArrayPoolの効果は減るがCreateProsodyTensor単体のサイズは小さいため許容可能）。

---

### 2-3. NormalizeAudio() のin-place化 + ArrayPool 化（効果: ~258KB/推論）

**ファイル**: `Assets/uPiper/Runtime/Core/AudioGeneration/AudioClipBuilder.cs`
**メソッド**: `NormalizeAudio()` (行60-96)

**現在のコード**:
```csharp
public float[] NormalizeAudio(float[] audioData, float targetPeak = 0.95f)
{
    // ...
    var normalizedData = new float[audioData.Length];  // ← 毎回 new float[66150] (~258KB)
    for (var i = 0; i < audioData.Length; i++)
    {
        normalizedData[i] = audioData[i] * scale;
    }
    return normalizedData;
}
```

**呼び出し箇所**:
- `PiperTTS.Inference.cs` 行165: `var normalizedAudio = _audioClipBuilder.NormalizeAudio(audioData, 0.95f);`
- `PiperTTS.Inference.cs` 行296: 同上
- `PiperTTS.cs` 行719付近: `audioBuilder.BuildAudioClip(audioData, ...)` (正規化なしで直接渡し)
- `InferenceEngineDemo.cs` 行848: `processedAudio = _audioBuilder.NormalizeAudio(audioData, 0.95f);`

**変更方針**: 2段階で改善する。

#### 方法A: in-place正規化メソッドの追加（推奨）

元の `audioData` は `ExtractAudioData` で作成されたコピーであり、正規化後は `BuildAudioClip` → `AudioClip.SetData()` に渡されてから不要になる。したがって、**元の配列をin-placeで正規化**すれば、新しい配列の割り当て自体が不要になる。

```csharp
/// <summary>
/// 音声データをin-placeで正規化する。元の配列が直接変更される。
/// </summary>
/// <param name="audioData">音声データ（変更される）</param>
/// <param name="targetPeak">目標ピーク値（0-1）</param>
public void NormalizeAudioInPlace(float[] audioData, float targetPeak = 0.95f)
{
    if (audioData == null || audioData.Length == 0)
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

**呼び出し側の変更** (`PiperTTS.Inference.cs` 行164-169):
```csharp
// Before:
var normalizedAudio = _audioClipBuilder.NormalizeAudio(audioData, 0.95f);
var audioClip = _audioClipBuilder.BuildAudioClip(normalizedAudio, ...);

// After:
_audioClipBuilder.NormalizeAudioInPlace(audioData, 0.95f);
var audioClip = _audioClipBuilder.BuildAudioClip(audioData, ...);
```

同様に `PiperTTS.Inference.cs` 行296 と `InferenceEngineDemo.cs` 行848 も変更。

#### 方法B: 既存 NormalizeAudio の後方互換維持

既存の `NormalizeAudio()` メソッドはpublic APIであるため、**削除せず残す**。ただし内部呼び出しは全て `NormalizeAudioInPlace()` に切り替える。外部利用者向けにコピー版も維持する。

---

### 2-4. IL2CPP 互換性の確保

**ファイル**: `Assets/uPiper/Runtime/Core/IL2CPP/IL2CPPCompatibility.cs`
**メソッド**: `PreserveCollectionTypes()` (行29-48)

IL2CPPのジェネリック型ストリッピングにより、`ArrayPool<float>` および `ArrayPool<int>` のシェアードインスタンスが除去される可能性がある。`[Preserve]` アノテーション付きのダミーコードで明示的にインスタンス化を強制する。

**追加コード** (`PreserveCollectionTypes()` 内、行47付近に追加):
```csharp
// ArrayPool types for memory optimization
_ = System.Buffers.ArrayPool<float>.Shared;
_ = System.Buffers.ArrayPool<int>.Shared;
```

**注意**: `System.Buffers` は .NET Standard 2.0 に含まれるが、Unity 2021以前では `System.Buffers.dll` がない場合がある。uPiperの最小サポートUnityバージョンを確認し、必要に応じてNuGetパッケージ参照を追加する。Unity 2022.3以降であれば標準で利用可能。

---

### 2-5. using ディレクティブの追加

以下のファイルに `using System.Buffers;` を追加:

- `Assets/uPiper/Runtime/Core/AudioGeneration/InferenceAudioGenerator.cs` (行1付近)
- `Assets/uPiper/Runtime/Core/IL2CPP/IL2CPPCompatibility.cs` (行1付近)

`AudioClipBuilder.cs` は方法A（in-place）のみであればArrayPoolを使用しないため追加不要。

---

## 3. エージェントチーム構成

| 役割 | 担当範囲 | 備考 |
|------|---------|------|
| **実装エージェント** | InferenceAudioGenerator.cs, AudioClipBuilder.cs, IL2CPPCompatibility.cs の変更 | 2-1～2-5の実装。Tensorのメモリ所有権に関するSentisドキュメント確認を含む |
| **テストエージェント** | ユニットテスト追加、既存テストの回帰確認 | AudioClipBuilderTests.cs, InferenceAudioGeneratorTests.cs への追加 |
| **レビューエージェント** | コードレビュー、IL2CPPビルド検証 | ArrayPool Return漏れ、Tensor lifetime、例外安全性のチェック |

**想定作業フロー**:
1. 実装エージェントが2-2 (CreateProsodyTensor) → 2-3 (NormalizeAudio) → 2-4 (IL2CPP) の順で実装
2. テストエージェントが並行してテストケースを作成
3. レビューエージェントが全変更をレビュー後、IL2CPPビルドで動作確認

---

## 4. 提供範囲・テスト項目

### 提供範囲 (Scope)

**変更対象ファイル**:
| ファイル | 変更内容 |
|---------|---------|
| `Assets/uPiper/Runtime/Core/AudioGeneration/InferenceAudioGenerator.cs` | `CreateProsodyTensor` → `CreateProsodyTensorPooled` 追加、`ExecuteInference` 内のfinallyブロック修正 |
| `Assets/uPiper/Runtime/Core/AudioGeneration/AudioClipBuilder.cs` | `NormalizeAudioInPlace()` メソッド追加 |
| `Assets/uPiper/Runtime/Core/PiperTTS.Inference.cs` | `NormalizeAudio` → `NormalizeAudioInPlace` 呼び出しに変更（行165, 296） |
| `Assets/uPiper/Runtime/Core/PiperTTS.cs` | 行719付近: 必要に応じてin-place正規化の適用 |
| `Assets/uPiper/Runtime/Demo/InferenceEngineDemo.cs` | 行848: `NormalizeAudio` → `NormalizeAudioInPlace` に変更 |
| `Assets/uPiper/Runtime/Core/IL2CPP/IL2CPPCompatibility.cs` | `ArrayPool<float>.Shared` / `ArrayPool<int>.Shared` の `[Preserve]` 追加 |

**提供範囲外**:
- `ExtractAudioData` のArrayPool化（public API戻り値のため安全でない）
- `BuildAudioClip` 内部の最適化（Unity API側の制約）
- 他のマイルストーンの機能（Warmup、PUAマッピング等）

### Unit テスト

**ファイル**: `Assets/uPiper/Tests/Runtime/AudioGeneration/AudioClipBuilderTests.cs`

| テストケース | 検証内容 |
|------------|---------|
| `NormalizeAudioInPlace_ValidData_NormalizesToTarget` | in-place正規化後の最大振幅がtargetPeakと一致 |
| `NormalizeAudioInPlace_SameResult_AsNormalizeAudio` | `NormalizeAudio` と `NormalizeAudioInPlace` の出力が全サンプルで一致 |
| `NormalizeAudioInPlace_LargeData_NormalizesCorrectly` | 66,150サンプル（3秒分）での正規化結果が正しい |
| `NormalizeAudioInPlace_NullOrEmpty_DoesNotThrow` | null/空配列で例外が発生しない |
| `NormalizeAudioInPlace_AlreadyNormalized_NoChange` | 既に正規化済みのデータが変更されない |

**ファイル**: `Assets/uPiper/Tests/Runtime/AudioGeneration/InferenceAudioGeneratorTests.cs`

| テストケース | 検証内容 |
|------------|---------|
| `CreateProsodyTensorPooled_SmallSize_UsesNewArray` | prosodySize <= 64 のとき `rentedArray` が null |
| `CreateProsodyTensorPooled_LargeSize_UsesPool` | prosodySize > 64 のとき `rentedArray` が non-null |
| `CreateProsodyTensorPooled_ValuesMatch_OriginalMethod` | Pooled版とオリジナル版で同一のテンソルデータが生成される |

**注意**: `InferenceAudioGenerator` のprosodyテンソルテストには `Tensor<int>` の検証が必要。Unity.InferenceEngineがテスト環境で利用可能であることが前提。利用不可の場合は、内部のデータ配列構築ロジックを分離してテスト可能にする。

### E2E テスト

| テスト項目 | 環境 | 検証内容 |
|-----------|------|---------|
| IL2CPPビルド（Android） | GitHub Actions / unity-build.yml | ビルドが成功し、`ArrayPool<float>` / `ArrayPool<int>` がストリップされない |
| IL2CPPビルド（iOS） | GitHub Actions / unity-build.yml | 同上 |
| WebGLビルド | GitHub Actions / deploy-webgl.yml | `System.Buffers` がWebGL環境で利用可能であることを確認 |
| 音声出力比較 | Editor PlayMode | ArrayPool導入前後で同一テキストの音声出力バイナリが一致（ビットパーフェクト） |

---

## 5. 懸念事項・レビュー項目

### 5-1. Tensor ライフサイクル vs ArrayPool Return タイミング（最重要）

**リスク**: `Tensor<int>(TensorShape, int[])` が渡された配列への参照を保持したまま、その配列が `ArrayPool.Return()` されると、別の `Rent()` で上書きされてTensorが不正なデータを参照する。

**対策**:
- Sentis (Unity.InferenceEngine) の `Tensor<T>(TensorShape, T[])` コンストラクタは**データをコピーする**ことが確認されている（内部で `NativeArray` へコピー）。ただし、これはドキュメント上の保証ではなく実装の挙動。
- **防御的実装**: piper-plusと同様に `Tensor.Dispose()` 後に `ArrayPool.Return()` する順序を厳守する。
- レビュー時に `finally` ブロック内の Dispose → Return の順序を必ず確認すること。

### 5-2. ArrayPool のクリアリング

**リスク**: `ArrayPool.Rent()` は前回使用時のデータが残った配列を返す可能性がある。`CreateProsodyTensorPooled` ではゼロフィルが必要。

**対策**:
- Rent直後に `Array.Clear(rentedArray, 0, prosodySize)` を呼ぶ（piper-plus準拠）。
- `Return` 時に `clearArray: true` は指定しない（次のRentでClearするため二重処理になる）。
- レビュー時に Clear の範囲が `rentedArray.Length` ではなく `prosodySize` であることを確認すること（Rentは要求より大きい配列を返すため）。

### 5-3. ArrayPool の Rent サイズ不一致

**リスク**: `ArrayPool.Rent(prosodySize)` は `prosodySize` 以上の配列を返す。`Tensor<int>(TensorShape, int[])` に渡す際、Sentisが `array.Length` を見てshapeとの不一致を検出しエラーになる可能性がある。

**対策**:
- 実装時にSentisの挙動をテストで確認する。
- 問題が発生した場合: `rentedArray.AsSpan(0, prosodySize).ToArray()` で正確なサイズの配列にコピーする。この場合ArrayPoolの効果は減るが、prosodyData自体は小さい（最大3.5KB）ため影響は限定的。
- **検証優先度: 高**。実装の最初のステップでこの動作を確認する。

### 5-4. IL2CPP ジェネリック型ストリッピング

**リスク**: IL2CPPのマネージドコードストリッピングが `ArrayPool<float>` / `ArrayPool<int>` の内部実装を除去する可能性がある。

**対策**:
- `IL2CPPCompatibility.GenericTypePreservation.PreserveCollectionTypes()` に明示的なインスタンス参照を追加。
- `link.xml` への追加は不要（`[Preserve]` アトリビュートで十分）。
- IL2CPPビルドでの動作確認をE2Eテストに含める。

### 5-5. スレッドセーフティ

**リスク**: `ArrayPool<T>.Shared` はスレッドセーフだが、`ExecuteInference` は `lock (_lockObject)` 内で実行されるため、並行アクセスの問題はない。

**確認事項**: `NormalizeAudioInPlace` は `AudioClipBuilder` のインスタンスメソッドだが、ステートレスなので並行呼び出しでも安全。呼び出し元がメインスレッド限定であることは `UnityMainThreadDispatcher.RunOnMainThreadAsync` で保証されている。

### 5-6. 後方互換性

**リスク**: `NormalizeAudio()` のpublic APIシグネチャを変更すると、外部利用者のコードが壊れる。

**対策**: `NormalizeAudio()` は廃止せず残す。新しい `NormalizeAudioInPlace()` を追加し、内部呼び出しのみ切り替える。

### 5-7. 既存問題の確認: 出力テンソルの例外時リーク

**既存問題の確認**: `ExecuteInference` 内の `outputTensor` と `readableTensor` は `try` ブロック内（行321-322）で手動 Dispose されているが、`finally` ブロックには含まれていない。`ExtractAudioData` が例外を投げた場合にリークする。ArrayPool ライフサイクル管理の見直しと合わせて修正を検討すること。

---

## 6. ゼロから作り直すとしたら

もし推論パイプライン全体を再設計するなら:

1. **バッファプール管理クラスの導入**: `AudioBufferPool` のような専用クラスを作り、推論パイプライン全体の一時バッファを統合管理する。`ArrayPool<T>.Shared` はアプリ全体で共有されるため、TTS専用のプールがあれば予測可能性が向上する。

2. **Span/Memory ベースのAPI設計**: public APIを `float[]` ではなく `ReadOnlyMemory<float>` で返す設計にすれば、内部でArrayPoolを使いつつ呼び出し元にはコピー不要な参照を渡せる。ただし、Unity APIの多くが `float[]` を要求するため、実用上は変換コストが発生する。

3. **NativeArray 統一**: Unity.InferenceEngine (Sentis) は内部で `NativeArray` を使用している。`ExtractAudioData` → `NormalizeAudio` → `AudioClip.SetData` の全フローを `NativeArray<float>` で統一すれば、マネージドヒープへの割り当てを完全に排除できる。`AudioClip.SetData(NativeArray<float>)` オーバーロードはUnity 2023.1以降で利用可能。

4. **Tensor.ToReadOnlyNativeArray() の活用**: `ExtractAudioData` の要素ごとコピーループを `Tensor<float>.ReadbackAndClone()` → `NativeArray` 直接参照に置き換える。現在は `readableTensor[i]` で1要素ずつコピーしているが、これ自体がパフォーマンスボトルネック。

現在のチケットでは既存のpublic APIを維持しつつ最小限の変更で最大効果を得る方針を取るが、将来的にbreaking changeを許容できるリリースでは上記の再設計を検討すべき。

---

## 7. 後続タスクへの連絡事項

### MS2-1 (Warmup実装) への影響

- `ExecuteInference` メソッドの `finally` ブロック構造が変更される。MS2-1がWarmup用に同メソッドを再利用する場合、ArrayPoolのReturn処理が正しく維持されていることを確認すること。
- MS2-1とMS2-2を並行実装する場合、`ExecuteInference` メソッドでのマージコンフリクトが発生する可能性がある。

### MS3-2 (沈黙句分割) への影響

- 句分割により各句の推論が独立実行される場合、ArrayPoolの恩恵がより大きくなる（短い句ごとに小さい配列がRent/Returnされるため、プール内の配列サイズが適正化される）。
- `NormalizeAudioInPlace` は句ごとの正規化にも使用可能。

### API変更の周知

- `AudioClipBuilder.NormalizeAudioInPlace()` が新規public APIとして追加される。`CHANGELOG.md` への記載が必要。
- 既存の `NormalizeAudio()` は非推奨 (Obsolete) にはしない（互換性維持のため）。ただし、内部使用は全て `NormalizeAudioInPlace` に切り替える。

### パフォーマンス計測

- ArrayPool導入後のGC割り当て量を `Unity Profiler` の `GC.Alloc` で計測し、期待される削減量（~430KB = NormalizeAudioの258KB + CreateProsodyTensorの3.5KB）と比較すること。
- `ExtractAudioData` の258KBは引き続き割り当てられるため、合計は **~530KB → ~272KB** の削減が現実的な目標。~100KB以下を目指すには `ExtractAudioData` のNativeArray化（ゼロから設計 #4）が必要。
