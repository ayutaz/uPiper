# uPiper フォネムタイミング対応仕様書

## 概要

uPiperの音声合成時に、各音素の開始時刻・終了時刻（ミリ秒）を取得できるようにする。
piper-plusのVITSモデルはONNX出力として `audio` テンソルに加えて `durations` テンソル（各音素のフレーム数）を出力する。現在のuPiperはこの `durations` テンソルを読み捨てているため、これを読み取りタイミング情報に変換して公開APIに露出させる。

### ユースケース

- Live2Dリップシンク（ParamMouthOpenY / ParamMouthForm制御）
- 3Dモデルのブレンドシェイプ制御
- 字幕・カラオケ表示
- VRM Visemeマッピング

---

## 変更対象ファイル

### 1. 新規: `PhonemeTimingResult.cs`

**パス:** `Assets/uPiper/Runtime/Core/AudioGeneration/PhonemeTimingResult.cs`

音素タイミング情報を保持するデータクラスを新規作成する。

```csharp
public readonly struct PhonemeTimingEntry
{
    public string Phoneme { get; }     // 音素文字列（"a", "k", "^" など）
    public float StartMs { get; }      // 開始時刻（ミリ秒）
    public float EndMs { get; }        // 終了時刻（ミリ秒）
    public float DurationMs { get; }   // 持続時間（ミリ秒）
}

public class PhonemeTimingResult
{
    public PhonemeTimingEntry[] Entries { get; }
    public float TotalDurationMs { get; }
    public int SampleRate { get; }
}
```

### 2. 新規: `TimingCalculator.cs`

**パス:** `Assets/uPiper/Runtime/Core/AudioGeneration/TimingCalculator.cs`

`durations` テンソル（フレーム数配列）からミリ秒タイミングへの変換ロジック。

**変換式:**

```
1フレームの時間(ms) = (hop_length / sample_rate) * 1000
デフォルト: (256 / 22050) * 1000 ≈ 11.6ms
```

**必要なメソッド:**

```csharp
public static class TimingCalculator
{
    public static PhonemeTimingResult Calculate(
        string[] phonemes,    // 音素文字列配列（PhonemizeResultから）
        float[] durations,    // ONNXモデルのdurationsテンソル出力
        int sampleRate,       // モデルのサンプルレート（通常22050）
        int hopLength = 256)  // ホップ長（VITSデフォルト: 256）
}
```

**アルゴリズム:**

1. `frameLengthMs = (hopLength / (float)sampleRate) * 1000f` を計算
2. `durations` 配列を走査し、累積和で各音素の `startMs` / `endMs` を算出
3. PAD(0), BOS(1), EOS(2) の特殊トークンはスキップ（piper-plus C#実装と同等）
4. `PhonemeTimingEntry` 配列を生成して返す

**参考実装:** piper-plus `src/csharp/PiperPlus.Core/Inference/TimingWriter.cs` の `CalculateTiming()` メソッド

### 3. 変更: `InferenceAudioGenerator.cs`

**パス:** `Assets/uPiper/Runtime/Core/AudioGeneration/InferenceAudioGenerator.cs`

#### 3a. `durations` テンソル出力名のキャッシュ追加

現在 `_cachedOutputName` で1つ目の出力（audio）のみキャッシュしている（L182）。
2つ目の出力（durations）もキャッシュする。

```csharp
// 既存
private string _cachedOutputName;     // "output" (audio)

// 追加
private string _cachedDurationsName;  // "durations" (フレーム数)
private bool _hasDurationsOutput;     // モデルがdurations出力を持つか
```

`InitializeAsync()` 内（L177-182付近）で、モデル出力が2つ以上あれば2番目の出力名をキャッシュする:

```csharp
_cachedOutputName = _model.outputs[0].name;
_hasDurationsOutput = _model.outputs.Count >= 2;
if (_hasDurationsOutput)
{
    _cachedDurationsName = _model.outputs[1].name;
}
```

#### 3b. `ExtractResults()` でdurations読み取り追加

現在の `ExtractResults()` （L436-463）は audio のみ抽出している。
durations テンソルも同時に抽出する。

```csharp
// 既存: audio抽出
var outputTensor = _worker.PeekOutput(_cachedOutputName) as Tensor<float>;

// 追加: durations抽出
float[] durations = null;
if (_hasDurationsOutput)
{
    var durationsTensor = _worker.PeekOutput(_cachedDurationsName) as Tensor<float>;
    if (durationsTensor != null)
    {
        var readableDurations = durationsTensor.ReadbackAndClone();
        durations = readableDurations.DownloadToNativeArray().ToArray();
        readableDurations.Dispose();
    }
}
```

#### 3c. 戻り値の変更

`GenerateAudioAsync()` の戻り値を `NativeArray<float>` から新しい構造体に変更する。

```csharp
// 新規: 推論結果を保持する構造体
public readonly struct InferenceResult : IDisposable
{
    public NativeArray<float> AudioData { get; }
    public float[] Durations { get; }  // nullの場合あり（モデル非対応時）

    public void Dispose()
    {
        if (AudioData.IsCreated) AudioData.Dispose();
    }
}
```

### 4. 変更: `IInferenceAudioGenerator.cs`

**パス:** `Assets/uPiper/Runtime/Core/AudioGeneration/IInferenceAudioGenerator.cs`

戻り値の型を変更する:

```csharp
// 変更前
Task<NativeArray<float>> GenerateAudioAsync(...);

// 変更後
Task<InferenceResult> GenerateAudioAsync(...);
```

### 5. 変更: `TTSSynthesisOrchestrator.cs`

**パス:** `Assets/uPiper/Runtime/Core/AudioGeneration/TTSSynthesisOrchestrator.cs`

`SynthesizeAsync()` で `InferenceResult` を受け取り、durations がある場合は `TimingCalculator` でタイミングを計算する。

戻り値を `AudioClip` から `SynthesisResult`（新規クラス）に変更する:

```csharp
public class SynthesisResult
{
    public AudioClip AudioClip { get; }
    public PhonemeTimingResult Timing { get; }  // nullの場合あり
}
```

### 6. 変更: `IPiperTTS.cs` / `PiperTTS.cs`

**パス:**
- `Assets/uPiper/Runtime/Core/IPiperTTS.cs`
- `Assets/uPiper/Runtime/Core/PiperTTS.cs`

#### 6a. 新規メソッド追加

```csharp
// インターフェースに追加
Task<SynthesisResult> SynthesizeWithTimingAsync(
    SynthesisRequest request,
    CancellationToken cancellationToken = default);

Task<SynthesisResult> GenerateAudioWithTimingAsync(
    string text,
    string language = null,
    PiperConfig config = null,
    CancellationToken cancellationToken = default);
```

#### 6b. 既存メソッドの互換性維持

既存の `SynthesizeAsync()` / `GenerateAudioAsync()` は `AudioClip` のみを返す現行の振る舞いを維持する（破壊的変更を避ける）。

### 7. 確認: ONNXモデルのdurations出力

piper-plusのVITSモデルが `durations` テンソルを出力するか確認が必要。

**確認方法:**

```python
import onnxruntime as ort
session = ort.InferenceSession("tsukuyomi.onnx")
for output in session.get_outputs():
    print(f"{output.name}: {output.shape} {output.type}")
```

期待される出力:

```
output: [1, 1, audio_length]    float32   # 音声波形
durations: [1, phoneme_length]  float32   # 各音素のフレーム数
```

`durations` 出力がない場合は、uPiper側で `_hasDurationsOutput = false` となり、タイミング機能は無効（`PhonemeTimingResult` が null）になる。

---

## 定数

| 定数 | 値 | 説明 |
|---|---|---|
| hop_length | 256 | VITSスペクトログラムのホップ長（サンプル数） |
| sample_rate | 22050 | piper-plus mediumモデルのサンプルレート |
| 1フレーム | ≈11.6ms | (256 / 22050) * 1000 |
| PAD ID | 0 | タイミング出力でスキップ |
| BOS ID | 1 | タイミング出力でスキップ |
| EOS ID | 2 | タイミング出力でスキップ |

---

## 変更ファイル一覧

| ファイル | 変更種別 | 内容 |
|---|---|---|
| `AudioGeneration/PhonemeTimingResult.cs` | 新規 | タイミングデータ構造 |
| `AudioGeneration/TimingCalculator.cs` | 新規 | フレーム→ミリ秒変換ロジック |
| `AudioGeneration/InferenceAudioGenerator.cs` | 変更 | durationsテンソル読み取り追加 |
| `AudioGeneration/IInferenceAudioGenerator.cs` | 変更 | 戻り値型を `InferenceResult` に変更 |
| `AudioGeneration/TTSSynthesisOrchestrator.cs` | 変更 | タイミング計算のパイプライン組み込み |
| `Core/IPiperTTS.cs` | 変更 | `SynthesizeWithTimingAsync` メソッド追加 |
| `Core/PiperTTS.cs` | 変更 | 上記メソッドの実装 |

---

## 処理フロー

```
テキスト入力
    ↓
PhonemizeAsync() → PhonemizeResult (phonemes[] + prosodyFlat[])
    ↓
PhonemeEncoder.Encode() → phonemeIds[]
    ↓
InferenceAudioGenerator.GenerateAudioAsync()
    ↓ ONNX推論
    ├→ audio テンソル → NativeArray<float> → AudioClip
    └→ durations テンソル → float[] (フレーム数)
        ↓
    TimingCalculator.Calculate(phonemes, durations, sampleRate, hopLength)
        ↓
    PhonemeTimingResult (各音素のstartMs/endMs/durationMs)
        ↓
SynthesisResult { AudioClip, PhonemeTimingResult }
```

---

## テスト項目

1. `durations` 出力のあるモデルでタイミングが正しく計算されること
2. `durations` 出力のないモデルで `PhonemeTimingResult` が null になること
3. 特殊トークン（PAD/BOS/EOS）がタイミング出力に含まれないこと
4. `TotalDurationMs` と AudioClip の長さが概ね一致すること（誤差1フレーム以内）
5. 既存の `SynthesizeAsync()` / `GenerateAudioAsync()` の動作に影響がないこと
6. phonemes配列とdurations配列の長さが一致しない場合のエラーハンドリング
