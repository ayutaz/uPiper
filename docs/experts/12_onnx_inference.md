# ONNX推論 `lid` テンソル入力対応

## 現在のONNX入力テンソル

`InferenceAudioGenerator.cs` の `ExecuteInference` メソッド (行250-279):

| テンソル名 | 形状 | データ型 | 説明 |
|-----------|------|--------|------|
| `input` | `(1, phoneme_length)` | `int` | 音素ID配列 |
| `input_lengths` | `(1,)` | `int` | 入力長 |
| `scales` | `(3,)` | `float` | noiseScale, lengthScale, noiseW |
| `prosody_features` | `(1, phoneme_length, 3)` | `int` | A1/A2/A3値（条件付き） |

## 多言語モデルの追加テンソル

piper-plus `export_onnx.py` (行253-302) より:

```python
include_sid = num_speakers > 1 or num_languages > 1
include_lid = num_languages > 1
```

| テンソル名 | 形状 | 型 | 挿入条件 |
|-----------|------|-----|---------|
| `sid` | `(1,)` | `int64` | `num_speakers > 1 OR num_languages > 1` |
| `lid` | `(1,)` | `int64` | `num_languages > 1` |

**テンソル挿入順序**: `input`, `input_lengths`, `scales`, [`sid`], [`lid`], [`prosody_features`]

## 変更が必要な箇所

### 1. 多言語/マルチスピーカー判定（行142付近）

現在の `SupportsProsody` 判定:
```csharp
_supportsProsody = _model.inputs.Any(input => input.name == "prosody_features");
```

追加する判定:
```csharp
_supportsMultilingual = _model.inputs.Any(input => input.name == "lid");
_supportsMultiSpeaker = _model.inputs.Any(input => input.name == "sid");
```

### 2. テンソル生成メソッド

```csharp
private Tensor<int> CreateLanguageIdTensor(int languageId)
{
    return new Tensor<int>(new TensorShape(1), new[] { languageId });
}

private Tensor<int> CreateSpeakerIdTensor(int speakerId)
{
    return new Tensor<int>(new TensorShape(1), new[] { speakerId });
}
```

### 3. ExecuteInference メソッド拡張（行264-279付近）

現在は固定位置でテンソルを設定しているが、名前ベースで動的に設定する方式に変更:

```csharp
// 必須入力
_worker.SetInput("input", inputTensor);
_worker.SetInput("input_lengths", inputLengthsTensor);
_worker.SetInput("scales", scalesTensor);

// 条件付き入力
if (_supportsMultiSpeaker)
{
    sidTensor = CreateSpeakerIdTensor(speakerId);
    _worker.SetInput("sid", sidTensor);
}

if (_supportsMultilingual)
{
    lidTensor = CreateLanguageIdTensor(languageId);
    _worker.SetInput("lid", lidTensor);
}

if (_supportsProsody && prosodyA1 != null)
{
    prosodyTensor = CreateProsodyTensor(sequenceLength, prosodyA1, prosodyA2, prosodyA3);
    _worker.SetInput("prosody_features", prosodyTensor);
}
```

### 4. メソッドシグネチャ拡張

```csharp
// 現在
public async Task<float[]> GenerateAudioAsync(
    int[] phonemeIds,
    float lengthScale = 1.0f, float noiseScale = 0.667f, float noiseW = 0.8f,
    CancellationToken cancellationToken = default)

// 拡張案
public async Task<float[]> GenerateAudioAsync(
    int[] phonemeIds,
    int languageId = 0,         // 新規
    int speakerId = 0,          // 新規
    float lengthScale = 1.0f, float noiseScale = 0.667f, float noiseW = 0.8f,
    CancellationToken cancellationToken = default)
```

### 5. Dispose処理（行306-313）

```csharp
finally
{
    inputTensor?.Dispose();
    inputLengthsTensor?.Dispose();
    scalesTensor?.Dispose();
    prosodyTensor?.Dispose();
    lidTensor?.Dispose();     // 追加
    sidTensor?.Dispose();     // 追加
}
```

## IInferenceAudioGenerator インターフェース拡張

```csharp
/// <summary>モデルが多言語（lid）をサポートするか</summary>
bool SupportsMultilingual { get; }

/// <summary>モデルがマルチスピーカー（sid）をサポートするか</summary>
bool SupportsMultiSpeaker { get; }
```

## Prosody言語マスキング

piper-plus `models.py` (行900-910):
- `prosody_language_ids` (デフォルト: `{0}` = 日本語のみ)
- 日本語以外の言語では Prosody値が自動的にゼロ化される
- **C#側では特別な処理不要** - モデル内部で処理される
- ただしProsody非対応言語では `prosody_features` にゼロを渡すのが推奨

## 言語IDマッピング

| 言語ID | 言語コード | 言語名 |
|--------|-----------|--------|
| 0 | ja | 日本語 |
| 1 | en | 英語 |
| 2 | zh | 中国語 |
| 3 | es | スペイン語 |
| 4 | fr | フランス語 |
| 5 | pt | ポルトガル語 |

この情報は `model.onnx.json` に含めるか、PiperVoiceConfig に追加フィールドとして管理。
