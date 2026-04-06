# MS3-2: 沈黙句分割 (PhonemeSilenceProcessor)

**マイルストーン**: [MS3: 新機能追加](../piper-plus-v1.10.0-milestones.md#ms3-新機能追加)
**優先度**: P2
**ステータス**: 未着手
**見積もり**: 3人日
**依存チケット**: MS2-1, MS2-2 推奨 (推論パイプラインの変更が重なるため)
**後続チケット**: なし

---

## 1. タスク目的とゴール

長文テキストをTTS変換する際、現在のuPiperは音素列全体を一括でONNX推論に渡している（`InferenceAudioGenerator.ExecuteInference()`、`Assets/uPiper/Runtime/Core/AudioGeneration/InferenceAudioGenerator.cs` 252行目）。この方式には以下の問題がある:

1. **メモリピーク**: 長文ほど入力テンソルが大きくなり、VITSモデルの中間状態がGPU/CPUメモリを圧迫する
2. **音声品質**: VITSモデルは長い入力シーケンスで推論精度が劣化する傾向がある（特に末尾部分のイントネーション崩れ）
3. **句間の沈黙**: 自然な発話では句の区切りに適切な無音区間が必要だが、現在はモデル任せで制御不能

**ゴール**: piper-plus v1.10.0 の `PhonemeSilenceProcessor`（`piper-plus/src/csharp/PiperPlus.Core/Inference/PhonemeSilenceProcessor.cs`）と互換性のある句分割・沈黙挿入機能をuPiperに実装する。沈黙トークン（例: `_`, `#`）の検出位置で音素列を分割し、句ごとに独立推論を行い、句間にゼロサンプルの無音区間を挿入して結合する。

---

## 2. 実装する内容の詳細

### 2.1 PhonemeSilenceProcessor クラスの新規作成

**ファイル**: `Assets/uPiper/Runtime/Core/AudioGeneration/PhonemeSilenceProcessor.cs` (新規)

piper-plus のリファレンス実装（`PiperPlus.Core/Inference/PhonemeSilenceProcessor.cs`）をベースに、uPiper の型システムに適合させた `static` クラスを作成する。

#### 2.1.1 型の差異への対応

piper-plus とuPiper の間には以下の型差異がある:

| 項目 | piper-plus | uPiper |
|------|-----------|--------|
| PhonemeIdMap の値型 | `Dictionary<string, int[]>` | `Dictionary<string, int>` |
| Phoneme ID の型 | `long` | `int` |
| Prosody 配列の型 | `long[]` | `int[]` (A1/A2/A3 が別配列) |

**PhonemeIdMap の値型**: piper-plus の `phoneme_id_map` はJSON由来で `{"a": [5, 6]}` の形式（1音素が複数IDに対応）。uPiperでは `InferenceEngineDemo.cs` 1044行目で `idArray[0].ToObject<int>()` として最初の要素のみ取得し `Dictionary<string, int>` に格納している。piper-plus の `BuildSilenceIdMap()` は `ids[^1]`（最後のID）をスプリットトリガーとしているが、uPiper では1音素1IDなのでそのまま値を使用すればよい。

**Phoneme ID の型**: uPiper 全体で `int` を使用している（`PhonemeEncoder.Encode()` の戻り値が `int[]`、`InferenceAudioGenerator.GenerateAudioAsync()` の引数が `int[]`）。piper-plus の `long` を `int` に置き換える。

**Prosody の型**: piper-plus はフラット配列 `long[]`（`[a1_0, a2_0, a3_0, a1_1, ...]`）を使用するが、uPiper は A1/A2/A3 を別配列 `int[]` で管理する（`GenerateAudioWithProsodyAsync()` の引数参照、`InferenceAudioGenerator.cs` 207-211行目）。分割時にA1/A2/A3それぞれを同期的にスライスする必要がある。

#### 2.1.2 Phrase レコードの定義

```csharp
namespace uPiper.Core.AudioGeneration
{
    public static class PhonemeSilenceProcessor
    {
        /// <summary>
        /// 分割された1句分のデータ
        /// </summary>
        public readonly struct Phrase
        {
            public readonly int[] PhonemeIds;
            public readonly int[] ProsodyA1;  // nullable
            public readonly int[] ProsodyA2;  // nullable
            public readonly int[] ProsodyA3;  // nullable
            public readonly int SilenceSamples;
        }
    }
}
```

piper-plus の `ProsodyFlat` (フラット `List<long>`) を、uPiper の慣習に合わせて `ProsodyA1` / `ProsodyA2` / `ProsodyA3` の3配列に分離する。これは `InferenceAudioGenerator.GenerateAudioWithProsodyAsync()` のシグネチャ（`int[] prosodyA1, int[] prosodyA2, int[] prosodyA3`）に直接対応する。

`record struct` は Unity の C# バージョン制約（Unity 2022.x では C# 9.0 まで）により使用不可の場合があるため、通常の `readonly struct` にコンストラクタを持たせる。

#### 2.1.3 Parse メソッド

piper-plus と同一のシグネチャ・動作:

```csharp
public static Dictionary<string, float> Parse(string specification)
```

入力形式: `"_ 0.5"` または `"_ 0.5,# 0.3"`（カンマ区切り）。piper-plus の実装をそのまま移植する。`CultureInfo.InvariantCulture` による float パースを必ず使用すること（ロケール依存のカンマ小数点問題を回避）。

#### 2.1.4 SplitAtPhonemeSilence メソッド

uPiper の型に適合させたシグネチャ:

```csharp
public static List<Phrase> SplitAtPhonemeSilence(
    int[] phonemeIds,
    int[] prosodyA1,      // nullable
    int[] prosodyA2,      // nullable
    int[] prosodyA3,      // nullable
    Dictionary<string, float> phonemeSilence,
    Dictionary<string, int> phonemeIdMap,  // uPiper: int (not int[])
    int sampleRate)
```

**BuildSilenceIdMap の簡略化**: uPiper の `PhonemeIdMap` は `Dictionary<string, int>` であるため、piper-plus のように `ids[^1]` で最後のIDを取る処理は不要。代わりに直接 `phonemeIdMap[phoneme]` の値をマッピングキーとして使用する:

```csharp
private static Dictionary<int, float> BuildSilenceIdMap(
    Dictionary<string, float> phonemeSilence,
    Dictionary<string, int> phonemeIdMap)
{
    var map = new Dictionary<int, float>();
    foreach (var (phoneme, seconds) in phonemeSilence)
    {
        if (phonemeIdMap.TryGetValue(phoneme, out int id))
        {
            map[id] = seconds;
        }
    }
    return map;
}
```

**Prosody スライスの分離**: piper-plus は `prosodyFlat[i*3+0..2]` のオフセット計算でスライスするが、uPiper では3つの独立配列を同じインデックスでスライスする:

```csharp
// piper-plus: prosodyFlat[i*3], prosodyFlat[i*3+1], prosodyFlat[i*3+2]
// uPiper:     prosodyA1[i],     prosodyA2[i],        prosodyA3[i]
```

分割時は `List<int>` で各句のA1/A2/A3を蓄積し、最後に `ToArray()` する。

#### 2.1.5 Prosody 有効判定

piper-plus は `prosodyFlat.Length == phonemeIds.Length * 3` で判定しているが、uPiper では:

```csharp
bool hasProsody = prosodyA1 != null
    && prosodyA1.Length == phonemeIds.Length
    && prosodyA2 != null
    && prosodyA2.Length == phonemeIds.Length
    && prosodyA3 != null
    && prosodyA3.Length == phonemeIds.Length;
```

長さ不一致時は piper-plus と同様に Prosody なしとして扱う（全 Phrase の Prosody 配列を `null` にする）。

### 2.2 PiperConfig への設定追加

**ファイル**: `Assets/uPiper/Runtime/Core/PiperConfig.cs`

以下のフィールドを `[Header("Sentence Silence Settings")]` セクションとして追加:

```csharp
[Header("Sentence Silence Settings")]

/// <summary>
/// 沈黙トークンによる句分割を有効にする
/// </summary>
[Tooltip("Split phoneme sequences at silence tokens and insert silence between phrases")]
public bool EnablePhonemeSilence = false;

/// <summary>
/// 沈黙トークンと沈黙秒数の設定文字列
/// 形式: "<phoneme> <seconds>" (カンマ区切りで複数指定可)
/// 例: "_ 0.5" または "_ 0.5,# 0.3"
/// </summary>
[Tooltip("Phoneme silence specification: '<phoneme> <seconds>' (comma-separated for multiple)")]
public string PhonemeSilenceSpec = "_ 0.5";
```

**配置場所**: 既存の `[Header("Advanced Settings")]` セクション（128行目付近）の前に挿入する。

**Validate() への追加**: `EnablePhonemeSilence = true` 時に `PhonemeSilenceSpec` をパースし、不正な場合は `PiperException` をスローする。パース結果をキャッシュするプロパティ `ParsedPhonemeSilence` を追加:

```csharp
/// <summary>
/// パース済みの沈黙トークンマップ（Validate後に利用可能）
/// </summary>
[NonSerialized]
public Dictionary<string, float> ParsedPhonemeSilence;
```

**Validate() タイミング**: `PiperConfig.Validate()` は `PiperTTS` のコンストラクタ（`PiperTTS.cs` 行267）で呼ばれるが、`InitializeWithInferenceAsync()` では呼ばれない。`ParsedPhonemeSilence` のパース（本チケットで `Validate()` 内に追加）が確実に実行されるよう、`InitializeWithInferenceAsync()` の冒頭にも `_config.Validate()` 呼び出しを追加する。これは本チケットのスコープ内で対応する。

### 2.3 InferenceAudioGenerator への統合

**ファイル**: `Assets/uPiper/Runtime/Core/AudioGeneration/InferenceAudioGenerator.cs`

#### 2.3.1 新しい公開メソッド

既存の `GenerateAudioAsync()` / `GenerateAudioWithProsodyAsync()` はそのまま維持し、句分割対応の新メソッドを追加する:

```csharp
/// <summary>
/// 沈黙句分割付きで音声を生成する
/// </summary>
public async Task<float[]> GenerateAudioWithSilenceSplitAsync(
    int[] phonemeIds,
    int[] prosodyA1,
    int[] prosodyA2,
    int[] prosodyA3,
    Dictionary<string, float> phonemeSilence,
    Dictionary<string, int> phonemeIdMap,
    float lengthScale = 1.0f,
    float noiseScale = 0.667f,
    float noiseW = 0.8f,
    int speakerId = 0,
    int languageId = 0,
    CancellationToken cancellationToken = default)
```

#### 2.3.2 処理フロー

```
phonemeIds, prosodyA1/A2/A3
    ↓
PhonemeSilenceProcessor.SplitAtPhonemeSilence()
    ↓
List<Phrase> phrases
    ↓
foreach phrase (空フレーズはスキップ):
    ├─ phrase.ProsodyA1 != null && _supportsProsody
    │   → ExecuteInference(phrase.PhonemeIds, prosodyA1, prosodyA2, prosodyA3, ...)
    └─ else
        → ExecuteInference(phrase.PhonemeIds, null, null, null, ...)
    ↓
    float[] phraseAudio + phrase.SilenceSamples 分のゼロサンプル
    ↓
全セグメントを連結して返却
```

**重要**: `ExecuteInference()` はメインスレッドで呼ばれる必要がある（`UnityMainThreadDispatcher.RunOnMainThreadAsync` で囲む）。句ごとの推論はループ内で逐次実行する（GPU Worker を共有しているため並列不可）。

**スレッド/ロック設計**: 句ごとに `RunOnMainThreadAsync` を呼ぶ（案B）を採用する。これによりメインスレッドを句間で解放でき、UIフリーズを防止する。`lock(_lockObject)` は既存の `ExecuteInference` 内部（行275）で取得されるため、呼び出し側で追加のロックは不要。処理フロー:
```
foreach (phrase in phrases):
    audioChunk = await RunOnMainThreadAsync(() => {
        // lock は ExecuteInference 内部で取得
        return ExecuteInference(phrase.PhonemeIds, ...);
    });
    chunks.Add(audioChunk);
```
**理由**: 案A（全句を1つの `RunOnMainThreadAsync` 内でループ）はメインスレッドを長時間占有し、長文（10句以上）でUIフリーズのリスクがある。

#### 2.3.3 音声結合ロジック

piper-plus CLI の `SynthesizeWithPhonemeSilence()` メソッド（`Program.cs` 1301-1369行目）と同様のパターン:

```csharp
// 各句の音声とサイレンスサンプル数を蓄積
var segments = new List<(float[] Audio, int SilenceSamples)>();
int totalLength = 0;

foreach (var phrase in phrases)
{
    if (phrase.PhonemeIds.Length == 0)
        continue;

    float[] phraseAudio = ExecuteInference(phrase.PhonemeIds, ...);
    segments.Add((phraseAudio, phrase.SilenceSamples));
    totalLength += phraseAudio.Length + phrase.SilenceSamples;
}

// ゼロ初期化された配列に順次コピー
var result = new float[totalLength];
int offset = 0;
foreach (var (audio, silenceSamples) in segments)
{
    Array.Copy(audio, 0, result, offset, audio.Length);
    offset += audio.Length;
    offset += silenceSamples; // ゼロ初期化済み
}
```

**注意**: piper-plus は `short[]` (int16 PCM) で結合しているが、uPiper は `float[]` で結合する（AudioClip 生成前の段階であるため）。ゼロサンプル区間は `new float[totalLength]` のデフォルト値 `0.0f` がそのまま無音になる。

### 2.4 IInferenceAudioGenerator インターフェース更新

**ファイル**: `Assets/uPiper/Runtime/Core/AudioGeneration/IInferenceAudioGenerator.cs`

`GenerateAudioWithSilenceSplitAsync()` メソッドを `IInferenceAudioGenerator` インターフェースに追加する必要がある。`PiperTTS.Inference.cs` の `_inferenceGenerator` フィールドは `IInferenceAudioGenerator` 型（行16）として宣言されているため、インターフェースに新メソッドが定義されていないと呼び出せない。

```csharp
// IInferenceAudioGenerator.cs に追加
Task<float[]> GenerateAudioWithSilenceSplitAsync(
    int[] phonemeIds,
    int[] prosodyA1, int[] prosodyA2, int[] prosodyA3,
    float lengthScale, float noiseScale, float noiseW,
    int speakerId, int languageId,
    Dictionary<string, float> silenceMap,
    int sampleRate);
```

### 2.5 PiperTTS.Inference.cs への統合

**ファイル**: `Assets/uPiper/Runtime/Core/PiperTTS.Inference.cs`

**PhonemeIdMap アクセス**: `_currentVoiceConfig` はクラスフィールド（行20で宣言、行60で代入）として利用可能。`PhonemeSilenceProcessor.SplitAtPhonemeSilence()` に必要な `phonemeIdMap` は `_currentVoiceConfig.PhonemeIdMap` から取得する。

`GenerateAudioWithMultilingualAsync()` メソッド（194行目）内の推論呼び出し部分（276-292行目）を修正し、`PiperConfig.EnablePhonemeSilence` が `true` の場合に `GenerateAudioWithSilenceSplitAsync()` を呼び出すように分岐を追加する:

```csharp
float[] audioData;
if (_config.EnablePhonemeSilence && _config.ParsedPhonemeSilence != null
    && _config.ParsedPhonemeSilence.Count > 0)
{
    audioData = await _inferenceGenerator.GenerateAudioWithSilenceSplitAsync(
        phonemeIds, prosodyA1, prosodyA2, prosodyA3,
        _config.ParsedPhonemeSilence,
        _currentVoiceConfig.PhonemeIdMap,
        lengthScale, noiseScale, noiseW,
        speakerId, resolvedLanguageId,
        cancellationToken);
}
else if (prosodyA1 != null && _inferenceGenerator.SupportsProsody)
{
    // 既存パス（変更なし）
}
else
{
    // 既存パス（変更なし）
}
```

同様に `GenerateAudioWithInferenceAsync()` メソッド（117行目）にも同じ分岐を追加する。

---

## 3. エージェントチーム構成

| エージェント | 役割 | 担当ファイル |
|-------------|------|-------------|
| Agent A (Core) | PhonemeSilenceProcessor の実装 + 単体テスト | `PhonemeSilenceProcessor.cs` (新規), `PhonemeSilenceProcessorTests.cs` (新規) |
| Agent B (Integration) | PiperConfig 拡張 + IInferenceAudioGenerator 更新 + InferenceAudioGenerator 統合 + PiperTTS.Inference.cs 統合 | `PiperConfig.cs`, `IInferenceAudioGenerator.cs`, `InferenceAudioGenerator.cs`, `PiperTTS.Inference.cs` |
| Agent C (Test) | E2E テスト + 回帰テスト | `InferenceAudioGeneratorTests.cs`, 新規統合テスト |

Agent A は Agent B と独立して作業可能。Agent B は Agent A の `PhonemeSilenceProcessor` クラスの完成を待つ（インターフェースの型定義さえ確定していれば並行可能）。Agent C は Agent A・B の両方が完了後に着手。

---

## 4. 提供範囲・テスト項目

### 提供範囲 (Scope)

**含む**:
- `PhonemeSilenceProcessor` クラス（Parse + SplitAtPhonemeSilence）
- `PiperConfig` への `EnablePhonemeSilence` / `PhonemeSilenceSpec` フィールド追加
- `IInferenceAudioGenerator` インターフェースへの `GenerateAudioWithSilenceSplitAsync()` メソッド追加
- `InferenceAudioGenerator.GenerateAudioWithSilenceSplitAsync()` メソッド
- `PiperTTS.Inference.cs` の推論パスへの分岐追加
- `InitializeWithInferenceAsync()` への `_config.Validate()` 呼び出し追加
- EditMode ユニットテスト + E2E テスト

**含まない**:
- `sentence_silence`（piper-plus Python の `voice.py` 299行目にある句末固定沈黙）。これは PhonemeSilenceProcessor とは独立した機能であり、別チケットで対応する。
- ストリーミング対応（`StreamAudioAsync`）への統合。現時点では一括生成のみ。
- `InferenceEngineDemo.cs` の UI からの設定変更。Inspector での PiperConfig 設定のみ。
- C++ 側の `--phoneme_silence` CLI オプション互換性（Unity プラグインでは不要）。

### Unit テスト

**ファイル**: `Assets/uPiper/Tests/Editor/PhonemeSilenceProcessorTests.cs` (新規)

piper-plus のテスト（`PhonemeSilenceProcessorTests.cs`、35テストケース）を移植し、uPiper の型システム（`int` vs `long`、`int[]` vs `int[]` 3配列）に適合させる。

| テストID | テスト内容 | 対応するpiper-plusテスト |
|---------|-----------|----------------------|
| Parse_SingleEntry | `"_ 0.5"` → `{"_": 0.5f}` | #1 Parse_SingleEntry_ReturnsSingleMapping |
| Parse_MultipleEntries | `"_ 0.5,# 0.3"` → 2エントリ | #2 Parse_MultipleEntries_ReturnsAllMappings |
| Parse_WhitespaceTrimmed | 前後空白のトリム | #3 Parse_WhitespaceAroundEntries_Trimmed |
| Parse_DecimalPrecision | `0.125` の精度保持 | #4 Parse_DecimalPrecision_Preserved |
| Parse_IntegerAccepted | `"_ 1"` → `1.0f` | #5 Parse_IntegerSeconds_Accepted |
| Parse_DuplicateLastWins | `"_ 0.5,_ 0.8"` → `0.8f` | #6 Parse_DuplicatePhoneme_LastValueWins |
| Parse_Null_Throws | null → ArgumentException | #7 |
| Parse_Empty_Throws | `""` → ArgumentException | #8 |
| Parse_WhitespaceOnly_Throws | `"   "` → ArgumentException | #9 |
| Parse_MissingSeconds_Throws | `"_"` → ArgumentException | #10 |
| Parse_NonNumeric_Throws | `"_ abc"` → ArgumentException | #11 |
| Split_NoSilence_SinglePhrase | 沈黙トークンなし → 1句 | #13 |
| Split_OneSilence_TwoPhrases | 1箇所分割 → 2句 + サイレンスサンプル検証 | #14 |
| Split_MultipleSilence_MultiplePhrases | 2箇所分割 → 3句 | #15 |
| Split_SilenceAtEnd_TrailingEmpty | 末尾沈黙 → 空フレーズ | #16 |
| Split_EmptyInput | 空入力 → 空フレーズ1つ | #17 |
| Split_WithProsody_SlicedCorrectly | A1/A2/A3 が正しくスライスされる | #21（型変換） |
| Split_NullProsody_AllNull | Prosody null → 全句 null | #22 |
| Split_WrongLengthProsody_TreatedAsNull | 長さ不一致 → null 扱い | #23 |
| Split_SampleRate_Calculation | 各サンプルレート (22050/44100/16000) でのサイレンスサンプル数 | #25 |
| Split_PhonemeNotInIdMap_NoSplit | ID マップにない音素 → 無視 | #26 |
| Split_EmptySilenceMap_SinglePhrase | 空 silence map → 分割なし | #32 |
| Parse_Then_Split_RoundTrip | Parse 出力を Split に直接入力 | #34 |

**piper-plus から追加移植すべきテスト（13件）**:

| テストID | テスト内容 | 対応するpiper-plusテスト |
|---------|-----------|----------------------|
| Parse_SpaceOnlyEntry_ThrowsArgumentException | スペースのみのエントリ → ArgumentException | piper-plus対応テスト |
| Split_SinglePhonemeNonSilence_SinglePhrase | 非沈黙音素1つ → 1句 | piper-plus対応テスト |
| Split_SinglePhonemeSilence_TwoPhrasesOneEmpty | 沈黙音素1つのみ → 2句（1つ空） | piper-plus対応テスト |
| Split_AllSilencePhonemes_EachSplits | 全音素が沈黙 → 各位置で分割 | piper-plus対応テスト |
| Split_ProsodyOnTrailingEmptyPhrase_IsEmptyList | 末尾空フレーズのProsodyが空リスト | piper-plus対応テスト |
| Split_MultiIdPhoneme_SplitsOnLastId | 複数ID音素の最後のIDで分割 | piper-plus対応テスト |
| Split_EmptyPhonemeIdMap_NoSplits | 空PhonemeIdMap → 分割なし | piper-plus対応テスト |
| Split_NullPhonemeIds_ThrowsArgumentNullException | null phonemeIds → ArgumentNullException | piper-plus対応テスト |
| Split_NullSilenceMap_ThrowsArgumentNullException | null silenceMap → ArgumentNullException | piper-plus対応テスト |
| Split_NullPhonemeIdMap_ThrowsArgumentNullException | null phonemeIdMap → ArgumentNullException | piper-plus対応テスト |
| Phrase_RecordEquality_Works | readonly struct の ValueType.Equals 比較 | piper-plus対応テスト |
| Split_ConsecutiveDifferentSilenceMarkers_EachSplits | 連続する異なる沈黙マーカーで各位置分割 | piper-plus対応テスト |

**注意**: `Split_MultiIdPhoneme_SplitsOnLastId` は、uPiper が `Dictionary<string, int>`（1音素1ID）であるため piper-plus（`Dictionary<string, int[]>` で `ids[^1]` をトリガー）とは動作が異なる。この差異を検証するテストが必要。`Phrase_RecordEquality_Works` は、uPiper が `readonly struct` のため `ValueType.Equals` による比較になる点に注意。

合計: 22（既存） + 13（追加） = 35テスト（piper-plus と同数）

### E2E テスト

**ファイル**: `Assets/uPiper/Tests/Runtime/AudioGeneration/PhonemeSilenceIntegrationTests.cs` (新規)

| テストID | テスト内容 |
|---------|-----------|
| SilenceSplit_DisabledByDefault | `PiperConfig.EnablePhonemeSilence = false` 時、既存動作と完全一致 |
| SilenceSplit_EnabledNoSilenceToken | 有効だが入力に沈黙トークンなし → 単一推論と同一結果 |
| SilenceSplit_AudioLength | 句分割あり → 出力音声長 = 各句音声長の合計 + サイレンスサンプル数の合計 |
| SilenceSplit_SilenceRegionIsZero | サイレンス区間のサンプル値が全て 0.0f であること |
| PiperConfig_Validate_InvalidSpec | `PhonemeSilenceSpec = "invalid"` → Validate() で例外 |
| PiperConfig_Validate_ValidSpec | `PhonemeSilenceSpec = "_ 0.5"` → `ParsedPhonemeSilence` が設定される |

---

## 5. 懸念事項・レビュー項目

### 5.1 Prosody スライスの整合性

piper-plus は Prosody をフラット配列 (`long[]`) で管理するが、uPiper は A1/A2/A3 の3配列 (`int[]`) で管理する。スライス時のインデックスずれが発生しないよう、`phonemeIds.Length` と `prosodyA1.Length` / `prosodyA2.Length` / `prosodyA3.Length` の一致を厳密にチェックすること。

**レビューポイント**: `SplitAtPhonemeSilence()` 内の `for` ループで `prosodyA1[i]` のアクセスが `phonemeIds` のインデックスと同期していることを確認する。

### 5.2 沈黙秒数の精度

`(int)(seconds * sampleRate)` の計算で浮動小数点誤差が生じる可能性がある。piper-plus と同じ `(int)` キャスト（切り捨て）を使用し、互換性を保つ。例: `0.5f * 22050 = 11025.0f → 11025` (正確)、`0.3f * 22050 = 6614.9995f → 6614` (1サンプルの誤差)。これは piper-plus と同一の動作なので許容する。

### 5.3 沈黙トークンなし時のパフォーマンス

`EnablePhonemeSilence = true` だが入力テキストに沈黙トークンが含まれない場合、`SplitAtPhonemeSilence()` は1句のみを返す。この場合のオーバーヘッドは `BuildSilenceIdMap()` のO(N)走査と `List<Phrase>` の1要素追加のみで、無視できるレベル。ただし句分割なしでも `List<int>` への蓄積と `ToArray()` 変換が走るため、大量の音素（1000+）の場合は初期容量を適切に設定すること（piper-plus は `Math.Max(10, phonemeIds.Length / 4)` を使用）。

### 5.4 複数回推論のパフォーマンスオーバーヘッド

句分割によりONNX推論が複数回呼ばれる。各推論でテンソルの生成・破棄・GPU同期が発生する。3句分割の場合、推論オーバーヘッドは約3倍（ただし各推論の入力サイズは1/3程度になるため、実際のモデル推論時間は線形増加とは限らない）。

**緩和策**: 長文TTS では元々メモリ不足やモデル精度劣化のリスクがあり、句分割のメリットがオーバーヘッドを上回る。短文テキストでは `EnablePhonemeSilence = false`（デフォルト）を推奨。

### 5.5 uPiper の PhonemeIdMap 型 (`int` vs `int[]`)

uPiper は `InferenceEngineDemo.cs` 1044行目で JSON の `phoneme_id_map` をパースする際、配列の最初の要素のみを取得して `Dictionary<string, int>` に格納している:

```csharp
config.PhonemeIdMap[kvp.Key] = idArray[0].ToObject<int>();
```

piper-plus は `Dictionary<string, int[]>` で全要素を保持し、`BuildSilenceIdMap()` で `ids[^1]`（最後のID）をトリガーとしている。現行の piper-plus モデルでは多くの音素が1要素配列（例: `"_": [0]`）であるため、`[0]` と `[^1]` は同値。ただし将来的に複数ID音素（例: `"x": [50, 51]`）が沈黙トークンに指定される場合は不整合が生じる。

**対応方針**: 現時点では uPiper の `Dictionary<string, int>` をそのまま使用し、`BuildSilenceIdMap()` を簡略化する。将来の互換性問題が発生した場合は、`PhonemeIdMap` の型変更を別チケットで対応する。

### 5.6 WebGL 対応

`PhonemeSilenceProcessor` は純粋な C# ロジック（ファイルI/O なし、Task.Run なし）であり、WebGL 環境での制約に抵触しない。推論ループ内の `ExecuteInference()` は既にメインスレッドで実行されるため、WebGL でも追加の考慮は不要。

### 5.7 音量正規化のバランス

句ごとに独立推論するため、各句の音量が異なる場合がある。結合後に一括正規化を行う設計では、特定句の音量バランスが崩れる可能性がある。piper-plus も同じ動作のため互換性の観点では問題ないが、将来的には句ごとの正規化オプションの検討余地がある。

---

## 6. ゼロから作り直すとしたら

1. **PhonemeIdMap の型統一**: uPiper の `Dictionary<string, int>` を piper-plus と同じ `Dictionary<string, int[]>` に変更する。これにより `BuildSilenceIdMap()` を piper-plus からそのまま移植でき、複数ID音素への対応も完了する。ただし影響範囲が `PhonemeEncoder`、`InferenceEngineDemo`、`PiperVoiceConfig` 等に波及するため、本チケットのスコープ外とした。

2. **Prosody のフラット配列化**: uPiper の Prosody を A1/A2/A3 別配列ではなく、piper-plus と同じフラット配列 `int[]`（`[a1_0, a2_0, a3_0, ...]`）に統一する。これにより `PhonemeSilenceProcessor` のコードが piper-plus とほぼ同一になり、保守性が向上する。ただし `InferenceAudioGenerator.CreateProsodyTensor()` 内で既にフラット化処理を行っている（346-358行目）ため、入力段階でのフラット化は推論パイプライン全体の設計変更を伴う。

3. **InferenceAudioGenerator への直接統合ではなく、パイプラインパターン**: 句分割 → 推論 → 結合を `InferenceAudioGenerator` に直接書くのではなく、`ISynthesisPipeline` のようなパイプライン抽象を導入し、`SilenceSplitPipeline` として実装する。これにより将来的なストリーミング対応やバッチ推論への拡張が容易になる。ただし現時点では YAGNI の原則に従い、シンプルなメソッド追加とした。

---

## 7. 後続タスクへの連絡事項

1. **`sentence_silence` の独立実装**: piper-plus Python 実装（`voice.py` 299行目）にある `sentence_silence`（句末に固定秒数の無音を付加）は、`PhonemeSilenceProcessor` とは独立した機能。`PiperSession.SentenceSilenceSeconds`（`PiperSession.cs` 85行目）に相当する機能を uPiper に追加する場合は別チケットで対応すること。

2. **ストリーミング TTS への統合**: `IPiperTTS.StreamAudioAsync()` に沈黙句分割を統合する場合、各句の推論完了時点で `AudioChunk` を yield return できるため、句分割とストリーミングは相性が良い。ただし `InferenceAudioGenerator.GenerateAudioWithSilenceSplitAsync()` は現在一括返却であるため、ストリーミング対応には `IAsyncEnumerable<float[]>` を返す新メソッドが必要。

3. **InferenceEngineDemo の UI 対応**: Inspector 経由で `PiperConfig.EnablePhonemeSilence` を設定できるが、デモ UI（`InferenceEngineDemo.cs`）には句分割のトグルやスペック入力フィールドが存在しない。ユーザー向けのデモ対応が必要な場合は別チケットで対応する。

4. **PhonemeIdMap 型変更の検討**: セクション5.5で述べた `Dictionary<string, int>` vs `Dictionary<string, int[]>` の型差異は、本チケットでは回避策で対応している。piper-plus との完全互換性が求められる場合は、別チケットで `PhonemeIdMap` の型変更を検討すること。影響ファイル: `PiperVoiceConfig.cs` (118行目), `PhonemeEncoder.cs` (38行目), `InferenceEngineDemo.cs` (1044行目)。
