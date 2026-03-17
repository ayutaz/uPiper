# ONNX推論 `lid` テンソル入力対応

## ステータス: Phase 5 実装済み

`InferenceAudioGenerator.cs` は多言語モデルの `lid`/`sid` テンソル入力に対応済み。
言語IDマッピングは `LanguageConstants.cs` で一元管理される。

## ONNX入力テンソル

`InferenceAudioGenerator.cs` の `ExecuteInference` メソッド:

| テンソル名 | 形状 | データ型 | 説明 | 設定条件 |
|-----------|------|--------|------|---------|
| `input` | `(1, phoneme_length)` | `int` | 音素ID配列 | 常に設定 |
| `input_lengths` | `(1,)` | `int` | 入力長 | 常に設定 |
| `scales` | `(3,)` | `float` | noiseScale, lengthScale, noiseW | 常に設定 |
| `sid` | `(1,)` | `int` | スピーカーID | `_supportsMultiSpeaker == true` |
| `lid` | `(1,)` | `int` | 言語ID | `_supportsLanguageId == true` |
| `prosody_features` | `(1, phoneme_length, 3)` | `int` | A1/A2/A3値 | `_supportsProsody == true` |

## モデル能力の自動検出

初期化時にモデルの入力テンソル名を検査して各機能の有無を判定:

```csharp
_supportsProsody = _model.inputs.Any(input => input.name == "prosody_features");
_supportsMultiSpeaker = _model.inputs.Any(input => input.name == "sid");
_supportsLanguageId = _model.inputs.Any(input => input.name == "lid");
```

piper-plus `export_onnx.py` での挿入条件:

```python
include_sid = num_speakers > 1 or num_languages > 1
include_lid = num_languages > 1
```

## テンソル挿入順序

名前ベースの `_worker.SetInput()` を使用するため、挿入順序に依存しない:

```csharp
// 必須入力
_worker.SetInput("input", inputTensor);
_worker.SetInput("input_lengths", inputLengthsTensor);
_worker.SetInput("scales", scalesTensor);

// 条件付き入力（モデル能力に応じて設定）
if (_supportsMultiSpeaker)
{
    sidTensor = new Tensor<int>(new TensorShape(1), new[] { speakerId });
    _worker.SetInput("sid", sidTensor);
}

if (_supportsLanguageId)
{
    lidTensor = new Tensor<int>(new TensorShape(1), new[] { languageId });
    _worker.SetInput("lid", lidTensor);
}

if (_supportsProsody)
{
    prosodyTensor = CreateProsodyTensor(sequenceLength, prosodyA1, prosodyA2, prosodyA3);
    _worker.SetInput("prosody_features", prosodyTensor);
}
```

## メソッドシグネチャ

```csharp
// 標準音声生成（languageId/speakerId対応済み）
public async Task<float[]> GenerateAudioAsync(
    int[] phonemeIds,
    float lengthScale = 1.0f, float noiseScale = 0.667f, float noiseW = 0.8f,
    int speakerId = 0,
    int languageId = 0,
    CancellationToken cancellationToken = default)

// Prosody対応音声生成（languageId/speakerId対応済み）
public async Task<float[]> GenerateAudioWithProsodyAsync(
    int[] phonemeIds,
    int[] prosodyA1, int[] prosodyA2, int[] prosodyA3,
    float lengthScale = 1.0f, float noiseScale = 0.667f, float noiseW = 0.8f,
    int speakerId = 0,
    int languageId = 0,
    CancellationToken cancellationToken = default)
```

## Dispose処理

全テンソルが `finally` ブロックで適切にDisposeされる:

```csharp
finally
{
    inputTensor?.Dispose();
    inputLengthsTensor?.Dispose();
    scalesTensor?.Dispose();
    sidTensor?.Dispose();
    lidTensor?.Dispose();
    prosodyTensor?.Dispose();
}
```

## IInferenceAudioGenerator インターフェース

```csharp
/// <summary>モデルがProsody（韻律）をサポートするか</summary>
bool SupportsProsody { get; }

/// <summary>モデルがマルチスピーカー（sid）をサポートするか</summary>
bool SupportsMultiSpeaker { get; }

/// <summary>モデルが多言語（lid）をサポートするか</summary>
bool SupportsLanguageId { get; }
```

## Prosody言語マスキング

piper-plus `models.py` (行900-910):
- `prosody_language_ids` (デフォルト: `{0}` = 日本語のみ)
- 日本語以外の言語では Prosody値が自動的にゼロ化される
- **C#側では特別な処理不要** - モデル内部で処理される
- ただしProsody非対応言語では `prosody_features` にゼロを渡すのが推奨

## 言語IDマッピング（LanguageConstants.cs で一元管理）

**実装**: `Assets/uPiper/Runtime/Core/Phonemizers/Multilingual/LanguageConstants.cs`

| 言語ID | 言語コード | 言語名 | 定数名 |
|--------|-----------|--------|--------|
| 0 | ja | 日本語 | `LanguageConstants.LanguageIdJapanese` |
| 1 | en | 英語 | `LanguageConstants.LanguageIdEnglish` |
| 2 | zh | 中国語 | `LanguageConstants.LanguageIdChinese` |
| 3 | es | スペイン語 | `LanguageConstants.LanguageIdSpanish` |
| 4 | fr | フランス語 | `LanguageConstants.LanguageIdFrench` |
| 5 | pt | ポルトガル語 | `LanguageConstants.LanguageIdPortuguese` |
| 6 | ko | 韓国語 | `LanguageConstants.LanguageIdKorean` |

### 言語グループ分類

| グループ | 言語 | 用途 |
|---------|------|------|
| `LatinLanguages` | en, es, fr, pt | Unicode範囲だけでは区別不可、言語ヒントが必要 |
| `CjkLanguages` | ja, zh, ko | スクリプト特徴で検出可能（仮名/CJK/ハングル） |

### ヘルパーメソッド

```csharp
// 言語コード → 言語ID
int id = LanguageConstants.GetLanguageId("ja");  // → 0

// 言語ID → 言語コード
string code = LanguageConstants.GetLanguageCode(0);  // → "ja"

// グループ判定
bool isLatin = LanguageConstants.IsLatinLanguage("en");  // → true
bool isCjk = LanguageConstants.IsCjkLanguage("ja");      // → true
bool isSupported = LanguageConstants.IsSupportedLanguage("ja");  // → true
```
