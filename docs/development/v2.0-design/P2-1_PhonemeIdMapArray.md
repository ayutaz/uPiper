# P2-1: PhonemeIdMap `int` -> `int[]` 型変更 -- 設計ドキュメント

## 1. 現状分析

### 1.1 現在の型定義

**uPiper (`PiperVoiceConfig.cs`, L118)**:
```csharp
public Dictionary<string, int> PhonemeIdMap;
```

**piper-plus (`PiperConfig.cs`, L24)**:
```csharp
public Dictionary<string, int[]> PhonemeIdMap { get; set; } = null!;
```

uPiper は 1 音素 = 1 ID の前提で設計されているが、piper-plus (Python/C# 両方) は
1 音素 = 複数 ID (`int[]`) をサポートする。現行モデル（multilingual-test-medium 等）は
全エントリが 1 要素配列だが、将来のモデルやカスタムモデルで複数 ID が出現する可能性がある。

### 1.2 モデル JSON の実データ

`multilingual-test-medium.onnx.json` の `phoneme_id_map`:
```json
{
  "phoneme_id_map": {
    "_": [0],
    "^": [1],
    "$": [2],
    "?": [3],
    "\ue016": [4],
    "a": [10],
    ...
  }
}
```

**全エントリが `int[]`（1 要素配列）**。JSON としては常に配列形式。

### 1.3 現在の JSON デシリアライズ（情報損失あり）

**InferenceEngineDemo.cs (L1044-1046)** -- `idArray[0]` で最初の要素だけ取得:
```csharp
if (kvp.Value is JArray idArray && idArray.Count > 0)
{
    config.PhonemeIdMap[kvp.Key] = idArray[0].ToObject<int>();
}
```

**ProsodyInferenceIntegrationTests.cs (L136)** -- 同様:
```csharp
config.PhonemeIdMap[kvp.Key] = idArray[0].ToObject<int>();
```

現状では JSON の `int[]` を `int` に変換（情報損失）して格納している。

---

## 2. piper-plus 側の仕様

### 2.1 Python 側

`piper/config.py`:
```python
phoneme_id_map: Mapping[str, Sequence[int]]
```

`piper/voice.py` (`phonemes_to_ids`):
```python
for phoneme in phonemes:
    if phoneme not in id_map:
        continue
    ids.extend(id_map[phoneme])  # int[] を全展開
    if self.config.phoneme_type in (PhonemeType.BILINGUAL, PhonemeType.MULTILINGUAL):
        ids.extend(id_map[PAD])  # PAD の int[] も全展開
```

**重要**: `ids.extend(id_map[phoneme])` -- 複数 ID がある場合は全て展開してフラットな ID 列に追加。

`docker/python-inference/inference.py`:
```python
phoneme_id_map: dict[str, list[int]],
...
ids = phoneme_id_map[phoneme]
phoneme_ids.extend(ids)  # 全 ID 展開
for _ in ids:             # 各 ID に対応する prosody を追加
    prosody_features.append(...)
```

### 2.2 C# 側 (piper-plus)

**PiperPhonemeConverter.EspeakPostProcessIds** -- PAD/BOS/EOS も配列として扱う:
```csharp
int[] padIds = phonemeIdMap.TryGetValue("_", out int[]? padArr) ? padArr : [0];
phonemeIdMap.TryGetValue("^", out int[]? bosIds);
phonemeIdMap.TryGetValue("$", out int[]? eosIds);
ids.AddRange(bosIds);      // BOS の全 ID を追加
ids.Add(padIds[0]);        // PAD は先頭のみ使用
```

**PhonemeSilenceProcessor.BuildSilenceIdMap** -- 最後の ID をトリガーに使用:
```csharp
// Use the last ID of the phoneme's ID array as the split trigger.
if (ids.Length > 0)
{
    map[(long)ids[^1]] = seconds;
}
```

**RawPhonemeParser.Parse** -- 全 ID 展開:
```csharp
if (phonemeIdMap.TryGetValue(token, out var ids))
{
    foreach (var id in ids)
        result.Add(id);
}
```

### 2.3 int[] の意味

| ケース | 説明 |
|--------|------|
| 1 要素 `[10]` | 通常: 1 音素 = 1 ID（現行モデル全て） |
| 複数要素 `[10, 11, 12]` | 1 音素が複数の連続 ID にマッピング（将来モデル用） |

複数 ID ケースでは、prosody 情報も ID ごとに展開される（piper-plus Python 実装準拠）。

---

## 3. 型変更の設計

### 3.1 新しい型

```csharp
// PiperVoiceConfig.cs
public Dictionary<string, int[]> PhonemeIdMap;
```

### 3.2 PhonemeEncoder の変更

**現在の `InitializePhonemeMapping()`**:
```csharp
private readonly Dictionary<string, int> _phonemeToId;

foreach (var kvp in _config.PhonemeIdMap)
{
    _phonemeToId[phoneme] = id;  // 単一 int
}
```

**変更後の設計**:

PhonemeEncoder は内部で `Dictionary<string, int>` (_phonemeToId) を保持して
高速な単一 ID ルックアップを行っている。現行モデルが全て 1 要素配列であるため、
以下の段階的アプローチを取る。

**方式 A（推奨）: ids[0] 抽出を維持 + 複数 ID 対応は将来タスク**

```csharp
// InitializePhonemeMapping
foreach (var kvp in _config.PhonemeIdMap)
{
    var phoneme = kvp.Key;
    var ids = kvp.Value;
    if (ids != null && ids.Length > 0 && !_phonemeToId.ContainsKey(phoneme))
    {
        _phonemeToId[phoneme] = ids[0];  // 先頭 ID を使用
    }
}
```

理由:
- PhonemeEncoder の内部ロジック（IPA マッピング、ts 分割、PAD 挿入等）は
  全て単一 ID 前提で設計されている
- 複数 ID 展開は prosody 配列の同期拡張も必要で、別タスクとすべき
- piper-plus C# 側でも EspeakPostProcessIds の PAD は `padIds[0]` のみ使用
- 現行モデルは全て 1 要素配列

将来の完全対応（P2-1b）で `_phonemeToId` を `Dictionary<string, int[]>` に変更し、
EncodeWithProsody 内で配列展開 + prosody 複製を行う。

**方式 B（参考）: 完全展開**

```csharp
// _phonemeToIds: Dictionary<string, int[]>
// EncodeWithProsody 内:
if (_phonemeToIds.TryGetValue(phonemeToLookup, out var phonemeIds))
{
    foreach (var pid in phonemeIds)
    {
        ids.Add(pid);
        expandedA1.Add(a1);  // prosody を ID ごとに複製
        expandedA2.Add(a2);
        expandedA3.Add(a3);
    }
}
```

### 3.3 PhonemeSilenceProcessor の変更

**現在のシグネチャ**:
```csharp
public static List<Phrase> SplitAtPhonemeSilence(
    ...,
    IReadOnlyDictionary<string, int> phonemeIdMap,
    int sampleRate)
```

**変更後**:
```csharp
public static List<Phrase> SplitAtPhonemeSilence(
    ...,
    IReadOnlyDictionary<string, int[]> phonemeIdMap,
    int sampleRate)
```

**BuildSilenceIdMap の変更**:
```csharp
// 現在
private static Dictionary<int, float> BuildSilenceIdMap(
    IReadOnlyDictionary<string, float> phonemeSilence,
    IReadOnlyDictionary<string, int> phonemeIdMap)
{
    foreach (var (phoneme, seconds) in phonemeSilence)
    {
        if (phonemeIdMap.TryGetValue(phoneme, out var id))
            map[id] = seconds;
    }
}

// 変更後 (piper-plus 準拠: 最後の ID をトリガーに使用)
private static Dictionary<int, float> BuildSilenceIdMap(
    IReadOnlyDictionary<string, float> phonemeSilence,
    IReadOnlyDictionary<string, int[]> phonemeIdMap)
{
    foreach (var (phoneme, seconds) in phonemeSilence)
    {
        if (phonemeIdMap.TryGetValue(phoneme, out var ids) && ids.Length > 0)
            map[ids[^1]] = seconds;  // 最後の ID をトリガー
    }
}
```

### 3.4 SplitInferenceOrchestrator の変更

**シグネチャ変更のみ**:
```csharp
// 現在
IReadOnlyDictionary<string, int> phonemeIdMap,

// 変更後
IReadOnlyDictionary<string, int[]> phonemeIdMap,
```

パススルーで PhonemeSilenceProcessor に渡すだけなので、内部ロジックの変更は不要。

### 3.5 TTSSynthesisOrchestrator の変更

`_voiceConfig.PhonemeIdMap` の型が変わるだけで、コードの変更は最小限:

```csharp
// L75: 既存コード（型が変わるだけで動作は同じ）
&& _voiceConfig?.PhonemeIdMap != null;

// L83: パススルー（型が変わるだけ）
_voiceConfig.PhonemeIdMap,
```

### 3.6 JSON デシリアライズの変更

**InferenceEngineDemo.ParseConfig** -- 配列をそのまま保持:
```csharp
// 現在
config.PhonemeIdMap[kvp.Key] = idArray[0].ToObject<int>();

// 変更後
config.PhonemeIdMap[kvp.Key] = idArray.ToObject<int[]>();
```

初期化:
```csharp
// 現在
PhonemeIdMap = new Dictionary<string, int>()

// 変更後
PhonemeIdMap = new Dictionary<string, int[]>()
```

---

## 4. 影響ファイル完全リスト

### 4.1 Runtime ソースコード (5 files)

| # | ファイル | 行 | 変更内容 |
|---|---------|-----|---------|
| 1 | `Runtime/Core/PiperVoiceConfig.cs` | L118 | `Dictionary<string, int>` -> `Dictionary<string, int[]>` |
| 2 | `Runtime/Core/AudioGeneration/PhonemeEncoder.cs` | L39, L61, L473-478 | `_phonemeToId` 初期化で `ids[0]` 抽出、ログメッセージ調整 |
| 3 | `Runtime/Core/AudioGeneration/PhonemeSilenceProcessor.cs` | L217, L307-308, L313 | シグネチャ `IReadOnlyDictionary<string, int[]>`、`BuildSilenceIdMap` 変更 |
| 4 | `Runtime/Core/AudioGeneration/SplitInferenceOrchestrator.cs` | L32 | シグネチャ `IReadOnlyDictionary<string, int[]>` |
| 5 | `Runtime/Core/AudioGeneration/TTSSynthesisOrchestrator.cs` | L75, L83 | 型推論で自動対応（コード変更なし） |
| 6 | `Runtime/Core/PiperTTS.cs` | L380 | ログ `.Count` はそのまま動作 |
| 7 | `Runtime/Demo/InferenceEngineDemo.cs` | L986, L1044-1046 | 初期化・デシリアライズ変更 |

### 4.2 テストファイル (12 files)

| # | ファイル | 変更内容 |
|---|---------|---------|
| 8 | `Tests/Runtime/AudioGeneration/PhonemeEncoderTests.cs` | L22-33: `Dictionary<string, int>` -> `Dictionary<string, int[]>` (3箇所) |
| 9 | `Tests/Runtime/AudioGeneration/PhonemeEncoderIPATests.cs` | L27-43: PhonemeIdMap 初期化 |
| 10 | `Tests/Runtime/AudioGeneration/PhonemeEncoderESpeakTests.cs` | L21-33: PhonemeIdMap 初期化 |
| 11 | `Tests/Runtime/AudioGeneration/PhonemeEncoderMultilingualTests.cs` | L28-119, L120-205: BuildMultilingualIpaMap(), BuildMultilingualPuaMap() |
| 12 | `Tests/Runtime/AudioGeneration/PhonemeEncoderMultilingualModelTests.cs` | L34-185, L190-224, L262-292: 4つの BuildXxxMap() メソッド |
| 13 | `Tests/Runtime/MultilingualPipelineTests.cs` | L29-74: BuildMultilingualPhonemeIdMap() |
| 14 | `Tests/Runtime/MultilingualModelPipelineTests.cs` | L33-232, L238-257, L278, L289: 2つの BuildXxxPhonemeIdMap() |
| 15 | `Tests/Runtime/AudioGeneration/ProsodyInferenceIntegrationTests.cs` | L80, L111, L136: 初期化・デシリアライズ |
| 16 | `Tests/Runtime/EnglishPhonemeMappingTest.cs` | L24-38: PhonemeIdMap 初期化、L70: TryGetValue |
| 17 | `Tests/Editor/AudioGeneration/TTSSynthesisOrchestratorTests.cs` | L28-50: CreateMinimalPhonemeIdMap() |
| 18 | `Tests/Editor/AudioGeneration/SplitInferenceOrchestratorTests.cs` | L24-37: CreateMinimalPhonemeIdMap() |
| 19 | `Tests/Editor/PhonemeSilenceProcessorTests.cs` | L25-36: TestPhonemeIdMap 定義 |

**合計: 19 ファイル** (Runtime 7 + Tests 12)

---

## 5. 各使用箇所の詳細対応方法

### 5.1 PhonemeEncoder.InitializePhonemeMapping (主要変更)

```csharp
// 変更前
foreach (var kvp in _config.PhonemeIdMap)
{
    var phoneme = kvp.Key;
    var id = kvp.Value;
    if (!_phonemeToId.ContainsKey(phoneme))
        _phonemeToId[phoneme] = id;
}

// 変更後
foreach (var kvp in _config.PhonemeIdMap)
{
    var phoneme = kvp.Key;
    var ids = kvp.Value;
    if (ids != null && ids.Length > 0 && !_phonemeToId.ContainsKey(phoneme))
        _phonemeToId[phoneme] = ids[0];
}
```

`_phonemeToId` は `Dictionary<string, int>` のまま維持。PhonemeEncoder 内部の
エンコードロジック（MapPhoneme, AddToken, AddPadToken, EncodePhonemeTs 等）は
変更不要。

### 5.2 PhonemeSilenceProcessor.BuildSilenceIdMap

```csharp
// 変更前
private static Dictionary<int, float> BuildSilenceIdMap(
    IReadOnlyDictionary<string, float> phonemeSilence,
    IReadOnlyDictionary<string, int> phonemeIdMap)
{
    var map = new Dictionary<int, float>();
    foreach (var (phoneme, seconds) in phonemeSilence)
    {
        if (phonemeIdMap.TryGetValue(phoneme, out var id))
            map[id] = seconds;
    }
    return map;
}

// 変更後
private static Dictionary<int, float> BuildSilenceIdMap(
    IReadOnlyDictionary<string, float> phonemeSilence,
    IReadOnlyDictionary<string, int[]> phonemeIdMap)
{
    var map = new Dictionary<int, float>();
    foreach (var (phoneme, seconds) in phonemeSilence)
    {
        if (phonemeIdMap.TryGetValue(phoneme, out var ids) && ids.Length > 0)
            map[ids[^1]] = seconds;
    }
    return map;
}
```

### 5.3 InferenceEngineDemo.ParseConfig (JSON デシリアライズ)

```csharp
// 変更前
config.PhonemeIdMap = new Dictionary<string, int>();
...
config.PhonemeIdMap[kvp.Key] = idArray[0].ToObject<int>();

// 変更後
config.PhonemeIdMap = new Dictionary<string, int[]>();
...
config.PhonemeIdMap[kvp.Key] = idArray.ToObject<int[]>();
```

### 5.4 テストの PhonemeIdMap 初期化（機械的変換）

全テストファイルで以下のパターンを変換:

```csharp
// 変更前
PhonemeIdMap = new Dictionary<string, int>
{
    { "_", 0 },
    { "^", 1 },
    { "$", 2 },
    { "a", 3 },
};

// 変更後
PhonemeIdMap = new Dictionary<string, int[]>
{
    { "_", new[] { 0 } },
    { "^", new[] { 1 } },
    { "$", new[] { 2 } },
    { "a", new[] { 3 } },
};
```

テストの `TryGetValue` 参照も更新:

```csharp
// 変更前 (EnglishPhonemeMappingTest.cs L70)
if (config.PhonemeIdMap.TryGetValue(phoneme, out var expectedId))

// 変更後
if (config.PhonemeIdMap.TryGetValue(phoneme, out var expectedIds))
{
    var expectedId = expectedIds[0];
    ...
}
```

---

## 6. マイグレーション戦略

### 6.1 実施順序

1. **PiperVoiceConfig.cs** -- 型定義変更（これが全ての起点）
2. **PhonemeEncoder.cs** -- InitializePhonemeMapping の `ids[0]` 抽出
3. **PhonemeSilenceProcessor.cs** -- シグネチャ + BuildSilenceIdMap
4. **SplitInferenceOrchestrator.cs** -- シグネチャのみ
5. **InferenceEngineDemo.cs** -- JSON デシリアライズ
6. **ProsodyInferenceIntegrationTests.cs** -- JSON デシリアライズ + 初期化
7. **全テストファイル** -- PhonemeIdMap 初期化パターン変換（一括置換）
8. **PiperTTS.cs** -- ログ確認（変更不要の可能性高）
9. **TTSSynthesisOrchestrator.cs** -- コンパイル確認（変更不要の可能性高）

### 6.2 Breaking Change の範囲

| API | 変更前 | 変更後 | 影響 |
|-----|--------|--------|------|
| `PiperVoiceConfig.PhonemeIdMap` | `Dictionary<string, int>` | `Dictionary<string, int[]>` | public フィールド変更 |
| `PhonemeSilenceProcessor.SplitAtPhonemeSilence` | `IReadOnlyDictionary<string, int>` | `IReadOnlyDictionary<string, int[]>` | public static メソッド |

`PhonemeEncoder` は `PiperVoiceConfig` を受け取るコンストラクタのみなので、
シグネチャ自体は変わらない。

`SplitInferenceOrchestrator` は `internal class` のため、外部への影響なし。

### 6.3 コンパイルエラー駆動

型変更は `PiperVoiceConfig.PhonemeIdMap` の 1 箇所から始まり、コンパイラが
全ての不整合箇所を報告する。以下の順でエラーを解消:

1. `Dictionary<string, int>` リテラル -> `Dictionary<string, int[]>` リテラル
2. `.TryGetValue(key, out var id)` -> `.TryGetValue(key, out var ids)` + `ids[0]`
3. `IReadOnlyDictionary<string, int>` パラメータ -> `IReadOnlyDictionary<string, int[]>`

---

## 7. テスト更新計画

### 7.1 既存テストの変更

全テストは PhonemeIdMap の初期化パターン変更のみ。テストロジック自体は変更不要
（PhonemeEncoder の出力 ID 配列は同一のまま）。

| テストクラス | 変更量 | 備考 |
|------------|--------|------|
| `PhonemeEncoderTests` | 小 | 3 箇所の Dictionary 初期化 |
| `PhonemeEncoderIPATests` | 小 | 1 箇所の Dictionary 初期化 |
| `PhonemeEncoderESpeakTests` | 小 | 1 箇所の Dictionary 初期化 |
| `PhonemeEncoderMultilingualTests` | 中 | 2 つの Build メソッド（各 50+ エントリ） |
| `PhonemeEncoderMultilingualModelTests` | 中 | 4 つの Build メソッド |
| `MultilingualPipelineTests` | 中 | 1 つの Build メソッド（60+ エントリ） |
| `MultilingualModelPipelineTests` | 大 | 2 つの Build メソッド（170+ エントリ） |
| `ProsodyInferenceIntegrationTests` | 小 | 初期化 + デシリアライズ |
| `EnglishPhonemeMappingTest` | 小 | 初期化 + TryGetValue |
| `TTSSynthesisOrchestratorTests` | 小 | 1 つの CreateMinimal メソッド |
| `SplitInferenceOrchestratorTests` | 小 | 1 つの CreateMinimal メソッド |
| `PhonemeSilenceProcessorTests` | 小 | TestPhonemeIdMap static 定義 |

### 7.2 新規テスト

| テスト | 目的 |
|--------|------|
| `PhonemeEncoder_MultiIdPhoneme_UsesFirstId` | 複数 ID 音素で `ids[0]` が使われることを確認 |
| `PhonemeSilenceProcessor_MultiIdPhoneme_SplitsOnLastId` | 複数 ID 音素で最後の ID がトリガーになることを確認 |
| `ParseConfig_MultiIdPhoneme_DeserializesArray` | JSON `"a": [10, 11, 12]` が正しくデシリアライズされることを確認 |

### 7.3 回帰テスト

既存テストが全て GREEN であれば回帰なし。PhonemeEncoder の出力 ID 列は
現行モデル（全て 1 要素配列）では完全に同一になるため、既存テストのアサーション値の
変更は不要。

---

## 8. リスクと注意事項

### 8.1 `new[] { id }` の GC 圧力

テストコードでの `new[] { 0 }`, `new[] { 1 }` 等の小配列生成は GC 圧力を
若干増加させるが、テスト専用なので許容。

Runtime の JSON デシリアライズでは Newtonsoft.Json の `ToObject<int[]>()` が
配列を生成するため、現状と GC 特性は同等。

### 8.2 null チェック

`ids` が `null` のケースを考慮:
```csharp
if (ids != null && ids.Length > 0)
    _phonemeToId[phoneme] = ids[0];
```

### 8.3 将来の完全対応 (P2-1b)

PhonemeEncoder 内部を `Dictionary<string, int[]>` に変更して複数 ID 展開に
対応する場合、以下の追加変更が必要:

- `_phonemeToId` -> `_phonemeToIds`: `Dictionary<string, int[]>`
- `EncodeWithProsody`: 各 ID ごとに prosody 値を複製
- `AddToken`: BOS/EOS の複数 ID 展開
- `AddPadToken`: PAD の複数 ID 展開
- `EncodePhonemeTs`: t/s の各 ID 配列展開
- `NeedsInterspersePadding` 後の PAD 挿入ロジック調整

これは本タスク (P2-1) のスコープ外とし、複数 ID モデルが実際に登場した時点で対応。
