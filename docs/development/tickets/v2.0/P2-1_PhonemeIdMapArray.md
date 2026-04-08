# P2-1: PhonemeIdMap int → int[] 型変更

**マイルストーン**: M3 - Data Model + Config
**優先度**: P0（Phase 2起点）
**見積もり**: 1.5 人日
**依存チケット**: Phase 1完了（M2ゲート通過）
**後続チケット**: P2-2（Prosody フラット配列化）
**ブランチ名**: `feature/v2.0-P2-1-phonemeidmap-array`

---

## 1. タスク目的とゴール

`PiperVoiceConfig.PhonemeIdMap` の型を `Dictionary<string, int>` から `Dictionary<string, int[]>` に変更し、piper-plus との完全互換を実現する。

**解決する問題**:

1. **piper-plus との型不一致**: piper-plus（Python/C# 両方）は `Dictionary<string, int[]>` を採用しており、1音素に複数IDを割り当て可能。uPiper は `Dictionary<string, int>` で、JSON デシリアライズ時に `idArray[0]` で先頭要素のみを抽出し、情報損失が発生している。
2. **将来モデルへの非対応**: 現行モデル（multilingual-test-medium等）は全エントリが1要素配列だが、将来のカスタムモデルで複数IDマッピングが出現した場合に対応できない。
3. **JSON デシリアライズの情報損失**: `InferenceEngineDemo.ParseConfig` と `ProsodyInferenceIntegrationTests` の2箇所で `idArray[0].ToObject<int>()` による情報損失が発生している。

**完了状態（Definition of Done）**:

- `PiperVoiceConfig.PhonemeIdMap` が `Dictionary<string, int[]>` 型に変更済み
- `PhonemeEncoder` 内部の `_phonemeToId` は `Dictionary<string, int>` のまま維持（`ids[0]` 抽出）
- `PhonemeSilenceProcessor.BuildSilenceIdMap` が piper-plus 準拠で最後の ID（`ids[^1]`）をトリガーに使用
- JSON デシリアライズが `idArray.ToObject<int[]>()` に変更され、情報損失が解消
- テスト12ファイルの `Dictionary<string, int>` 初期化が `Dictionary<string, int[]>` に変換済み
- 新規テスト3件（複数IDの先頭ID使用、最後のIDトリガー、JSON配列デシリアライズ）が追加済み
- 全テスト通過（EditMode + PlayMode）
- `dotnet format --verify-no-changes` 通過

---

## 2. 実装する内容の詳細

### Step 1: PiperVoiceConfig 型定義変更（起点）

**ファイル**: `Assets/uPiper/Runtime/Core/PiperVoiceConfig.cs` (L118)

```csharp
// 変更前
public Dictionary<string, int> PhonemeIdMap;

// 変更後
public Dictionary<string, int[]> PhonemeIdMap;
```

この1行の変更がコンパイルエラーを19ファイルに波及させる。以降、コンパイルエラー駆動で全箇所を修正する。

### Step 2: PhonemeEncoder.InitializePhonemeMapping 変更（主要ロジック変更）

**ファイル**: `Assets/uPiper/Runtime/Core/AudioGeneration/PhonemeEncoder.cs` (L39, L61, L473-478)

内部の `_phonemeToId` は `Dictionary<string, int>` のまま維持し、`ids[0]` を抽出する方式を採用する。

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

**理由**: PhonemeEncoder の内部エンコードロジック（MapPhoneme, AddToken, AddPadToken, EncodePhonemeTs, PAD挿入等）は全て単一ID前提で設計されている。複数ID展開は prosody 配列の同期拡張も必要であり、将来タスク（P2-1b）とする。現行モデルは全て1要素配列のため、動作に影響なし。

### Step 3: PhonemeSilenceProcessor.BuildSilenceIdMap 変更

**ファイル**: `Assets/uPiper/Runtime/Core/AudioGeneration/PhonemeSilenceProcessor.cs` (L217, L307-308, L313)

シグネチャと内部ロジックの両方を変更する。piper-plus 準拠で最後の ID をトリガーに使用。

```csharp
// 変更前
public static List<Phrase> SplitAtPhonemeSilence(
    ...,
    IReadOnlyDictionary<string, int> phonemeIdMap,
    int sampleRate)

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

// 変更後
public static List<Phrase> SplitAtPhonemeSilence(
    ...,
    IReadOnlyDictionary<string, int[]> phonemeIdMap,
    int sampleRate)

private static Dictionary<int, float> BuildSilenceIdMap(
    IReadOnlyDictionary<string, float> phonemeSilence,
    IReadOnlyDictionary<string, int[]> phonemeIdMap)
{
    foreach (var (phoneme, seconds) in phonemeSilence)
    {
        if (phonemeIdMap.TryGetValue(phoneme, out var ids) && ids.Length > 0)
            map[ids[^1]] = seconds;  // 最後の ID をトリガー（piper-plus 準拠）
    }
}
```

### Step 4: SplitInferenceOrchestrator シグネチャ変更

**ファイル**: `Assets/uPiper/Runtime/Core/AudioGeneration/SplitInferenceOrchestrator.cs` (L32)

```csharp
// 変更前
IReadOnlyDictionary<string, int> phonemeIdMap,

// 変更後
IReadOnlyDictionary<string, int[]> phonemeIdMap,
```

パススルーで PhonemeSilenceProcessor に渡すだけなので、内部ロジックの変更は不要。`SplitInferenceOrchestrator` は `internal class` のため外部への影響なし。

### Step 5: TTSSynthesisOrchestrator の確認

**ファイル**: `Assets/uPiper/Runtime/Core/AudioGeneration/TTSSynthesisOrchestrator.cs` (L75, L83)

`_voiceConfig.PhonemeIdMap` の型が変わるだけで、`!= null` チェックとパススルーの動作は同一。コード変更は最小限（型推論で自動対応する可能性が高い）。コンパイル確認のみ。

### Step 6: PiperTTS ログ確認

**ファイル**: `Assets/uPiper/Runtime/Core/PiperTTS.cs` (L380)

`.Count` プロパティは `Dictionary<string, int[]>` でもそのまま動作するため、コード変更は不要の可能性が高い。コンパイル確認のみ。

### Step 7: InferenceEngineDemo JSON デシリアライズ変更

**ファイル**: `Assets/uPiper/Runtime/Demo/InferenceEngineDemo.cs` (L986, L1044-1046)

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

### Step 8: テストファイルの機械的変換（12ファイル）

全テストファイルで以下の2パターンを変換する。

**パターン A: PhonemeIdMap 初期化の変換**

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

**パターン B: TryGetValue 参照の変換**

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

**対象テストファイル一覧**:

| # | ファイル | 変更量 | 変更内容 |
|---|---------|--------|---------|
| 1 | `PhonemeEncoderTests.cs` | 小 | 3箇所の Dictionary 初期化 |
| 2 | `PhonemeEncoderIPATests.cs` | 小 | 1箇所の Dictionary 初期化 |
| 3 | `PhonemeEncoderESpeakTests.cs` | 小 | 1箇所の Dictionary 初期化 |
| 4 | `PhonemeEncoderMultilingualTests.cs` | 中 | 2つの Build メソッド（各50+エントリ） |
| 5 | `PhonemeEncoderMultilingualModelTests.cs` | 中 | 4つの Build メソッド |
| 6 | `MultilingualPipelineTests.cs` | 中 | 1つの Build メソッド（60+エントリ） |
| 7 | `MultilingualModelPipelineTests.cs` | 大 | 2つの Build メソッド（170+エントリ） |
| 8 | `ProsodyInferenceIntegrationTests.cs` | 小 | 初期化 + デシリアライズ |
| 9 | `EnglishPhonemeMappingTest.cs` | 小 | 初期化 + TryGetValue |
| 10 | `TTSSynthesisOrchestratorTests.cs` | 小 | 1つの CreateMinimal メソッド |
| 11 | `SplitInferenceOrchestratorTests.cs` | 小 | 1つの CreateMinimal メソッド |
| 12 | `PhonemeSilenceProcessorTests.cs` | 小 | TestPhonemeIdMap static 定義 |

### Step 9: 新規テスト追加（3件）

| テスト | 目的 |
|--------|------|
| `PhonemeEncoder_MultiIdPhoneme_UsesFirstId` | `{ "a", new[] { 10, 11, 12 } }` のような複数ID音素で `ids[0]` (=10) が使われることを確認 |
| `PhonemeSilenceProcessor_MultiIdPhoneme_SplitsOnLastId` | 複数ID音素で最後の ID（`ids[^1]`）がトリガーになることを確認 |
| `ParseConfig_MultiIdPhoneme_DeserializesArray` | JSON `"a": [10, 11, 12]` が `int[] { 10, 11, 12 }` として正しくデシリアライズされることを確認 |

### Step 10: ドキュメント更新

- `CHANGELOG.md`: 破壊的変更として `PiperVoiceConfig.PhonemeIdMap` 型変更を記載

### 影響ファイル完全リスト（合計19ファイル）

**Runtime ソースコード（7ファイル）**:

| # | ファイル | 変更内容 |
|---|---------|---------|
| 1 | `Runtime/Core/PiperVoiceConfig.cs` | `Dictionary<string, int>` → `Dictionary<string, int[]>` |
| 2 | `Runtime/Core/AudioGeneration/PhonemeEncoder.cs` | `_phonemeToId` 初期化で `ids[0]` 抽出、ログ調整 |
| 3 | `Runtime/Core/AudioGeneration/PhonemeSilenceProcessor.cs` | シグネチャ + `BuildSilenceIdMap` ロジック変更 |
| 4 | `Runtime/Core/AudioGeneration/SplitInferenceOrchestrator.cs` | シグネチャのみ |
| 5 | `Runtime/Core/AudioGeneration/TTSSynthesisOrchestrator.cs` | 型推論で自動対応（コード変更なしの可能性） |
| 6 | `Runtime/Core/PiperTTS.cs` | ログ `.Count` はそのまま動作（変更不要の可能性） |
| 7 | `Runtime/Demo/InferenceEngineDemo.cs` | 初期化 + デシリアライズ変更 |

**テストファイル（12ファイル）**:

| # | ファイル | 変更内容 |
|---|---------|---------|
| 8 | `Tests/Runtime/AudioGeneration/PhonemeEncoderTests.cs` | Dictionary 初期化3箇所 |
| 9 | `Tests/Runtime/AudioGeneration/PhonemeEncoderIPATests.cs` | Dictionary 初期化 |
| 10 | `Tests/Runtime/AudioGeneration/PhonemeEncoderESpeakTests.cs` | Dictionary 初期化 |
| 11 | `Tests/Runtime/AudioGeneration/PhonemeEncoderMultilingualTests.cs` | BuildXxxMap() 2メソッド |
| 12 | `Tests/Runtime/AudioGeneration/PhonemeEncoderMultilingualModelTests.cs` | BuildXxxMap() 4メソッド |
| 13 | `Tests/Runtime/MultilingualPipelineTests.cs` | BuildMultilingualPhonemeIdMap() |
| 14 | `Tests/Runtime/MultilingualModelPipelineTests.cs` | BuildXxxPhonemeIdMap() 2メソッド |
| 15 | `Tests/Runtime/AudioGeneration/ProsodyInferenceIntegrationTests.cs` | 初期化 + デシリアライズ |
| 16 | `Tests/Runtime/EnglishPhonemeMappingTest.cs` | 初期化 + TryGetValue |
| 17 | `Tests/Editor/AudioGeneration/TTSSynthesisOrchestratorTests.cs` | CreateMinimalPhonemeIdMap() |
| 18 | `Tests/Editor/AudioGeneration/SplitInferenceOrchestratorTests.cs` | CreateMinimalPhonemeIdMap() |
| 19 | `Tests/Editor/PhonemeSilenceProcessorTests.cs` | TestPhonemeIdMap static 定義 |

### 推奨実施順序

1. `PiperVoiceConfig.cs` -- 型定義変更（全エラーの起点）
2. `PhonemeEncoder.cs` -- InitializePhonemeMapping の `ids[0]` 抽出
3. `PhonemeSilenceProcessor.cs` -- シグネチャ + BuildSilenceIdMap
4. `SplitInferenceOrchestrator.cs` -- シグネチャのみ
5. `InferenceEngineDemo.cs` -- JSON デシリアライズ
6. `ProsodyInferenceIntegrationTests.cs` -- JSON デシリアライズ + 初期化
7. 全テストファイル -- PhonemeIdMap 初期化パターン変換（一括置換）
8. `PiperTTS.cs` -- ログ確認（変更不要の可能性）
9. `TTSSynthesisOrchestrator.cs` -- コンパイル確認（変更不要の可能性）
10. 新規テスト3件追加
11. 全テスト実行 + dotnet format

---

## 3. エージェントチームの役割と人数

**1エージェント構成**（1.5人日）

| 役割 | 担当内容 | 工数 |
|------|---------|------|
| 実装エージェント | Step 1-10 の全実装 + 全テスト実行 + ドキュメント更新 | 1.5 人日 |

**理由**: 変更の大半が機械的な型変換（`int` → `int[]`、`{ 0 }` → `new[] { 0 }`）であり、ロジック変更は `PhonemeEncoder.InitializePhonemeMapping` と `PhonemeSilenceProcessor.BuildSilenceIdMap` の2箇所のみ。変更ファイルは19あるが、テスト12ファイルの変更はパターンが統一されており、正規表現による一括置換で効率的に処理できる。複数エージェントに分割するとマージコスト（特にテストファイルのコンフリクト解決）が工数を上回る。

**推奨ワークフロー**:
1. Runtime 7ファイルを順次変更（Step 1-7）
2. コンパイル確認
3. テスト12ファイルを一括変換（Step 8）-- 正規表現 `\{ "([^"]+)", (\d+) \}` → `{ "$1", new[] { $2 } }` で大半をカバー
4. 新規テスト3件追加（Step 9）
5. 全テスト実行・修正
6. dotnet format 確認

---

## 4. 提供範囲とテスト項目

### 4.1 提供範囲

| カテゴリ | 範囲内 | 範囲外 |
|---------|-------|-------|
| PiperVoiceConfig | `PhonemeIdMap` の型変更 | 他フィールドの変更 |
| PhonemeEncoder | `InitializePhonemeMapping` の `ids[0]` 抽出 | `_phonemeToId` の `Dictionary<string, int[]>` 化（P2-1b） |
| PhonemeEncoder | null チェック追加 | 複数ID展開 + prosody複製ロジック（P2-1b） |
| PhonemeSilenceProcessor | シグネチャ + `BuildSilenceIdMap` の `ids[^1]` | 他メソッドの変更 |
| JSON デシリアライズ | `int[]` 対応 | JSON スキーマバリデーション |
| テスト | 既存12ファイルの型変換 + 新規3件 | テストロジック自体の変更（既存アサーション値は不変） |
| ドキュメント | CHANGELOG.md 更新 | ARCHITECTURE / CLAUDE.md の全面書き換え |

### 4.2 Unit テスト

**既存テストの移行（変更なし = アサーション値は同一）**:

全テストは PhonemeIdMap の初期化パターン変更のみ。PhonemeEncoder の出力 ID 配列は現行モデル（全て1要素配列）では完全に同一のため、既存テストのアサーション値の変更は不要。

| テストクラス | 変更量 | 備考 |
|------------|--------|------|
| `PhonemeEncoderTests` | 小 | 3箇所の Dictionary 初期化 |
| `PhonemeEncoderIPATests` | 小 | 1箇所の Dictionary 初期化 |
| `PhonemeEncoderESpeakTests` | 小 | 1箇所の Dictionary 初期化 |
| `PhonemeEncoderMultilingualTests` | 中 | 2つの Build メソッド（各50+エントリ） |
| `PhonemeEncoderMultilingualModelTests` | 中 | 4つの Build メソッド |
| `MultilingualPipelineTests` | 中 | 1つの Build メソッド（60+エントリ） |
| `MultilingualModelPipelineTests` | 大 | 2つの Build メソッド（170+エントリ） |
| `ProsodyInferenceIntegrationTests` | 小 | 初期化 + デシリアライズ |
| `EnglishPhonemeMappingTest` | 小 | 初期化 + TryGetValue |
| `TTSSynthesisOrchestratorTests` | 小 | 1つの CreateMinimal メソッド |
| `SplitInferenceOrchestratorTests` | 小 | 1つの CreateMinimal メソッド |
| `PhonemeSilenceProcessorTests` | 小 | TestPhonemeIdMap static 定義 |

**新規テスト（3件）**:

| テストメソッド | 配置クラス | 内容 |
|--------------|-----------|------|
| `PhonemeEncoder_MultiIdPhoneme_UsesFirstId` | `PhonemeEncoderTests` | `{ "a", new[] { 10, 11, 12 } }` で PhonemeEncoder に渡し、エンコード結果に `10` のみが含まれることを検証 |
| `PhonemeSilenceProcessor_MultiIdPhoneme_SplitsOnLastId` | `PhonemeSilenceProcessorTests` | `{ ",", new[] { 5, 6, 7 } }` で `BuildSilenceIdMap` に渡し、`map[7]` にサイレンス秒数が設定されることを検証 |
| `ParseConfig_MultiIdPhoneme_DeserializesArray` | `ProsodyInferenceIntegrationTests` 又は新規 | JSON `"a": [10, 11, 12]` を ParseConfig に渡し、`config.PhonemeIdMap["a"]` が `new[] { 10, 11, 12 }` と等価であることを検証 |

### 4.3 E2E テスト

| テスト | 内容 |
|-------|------|
| `InferenceEngineDemo` 手動実行 | 6言語ドロップダウンで音声生成が正常に動作することを確認 |
| CI `unity-tests.yml` 全通過 | EditMode + PlayMode テストの GREEN 確認 |
| 回帰確認 | 既存テストのアサーション値が変更されていないことを確認（現行モデルは全て1要素配列のため、出力は同一） |

---

## 5. 懸念事項とレビュー項目

### 5.1 技術的懸念

| 懸念 | 深刻度 | 対策 |
|------|--------|------|
| **`new[] { id }` のGC圧力（テストコード）** | 低 | テスト専用であり、ランタイムには影響なし。許容範囲内 |
| **`new[] { id }` のGC圧力（JSONデシリアライズ）** | 低 | `ToObject<int[]>()` が配列を生成するため、現状の `ToObject<int>()` と GC 特性は同等 |
| **`ids` が null のケース** | 中 | `PhonemeEncoder` で `ids != null && ids.Length > 0` のガードを追加。JSON デシリアライズ側では Newtonsoft.Json が空配列 `[]` を `int[0]` として返すため、`Length > 0` チェックで対応 |
| **P2-2（Prosody フラット配列化）との並行作業** | 中 | P2-1 と P2-2 は独立して実施可能だが、P2-1 の `PiperVoiceConfig.PhonemeIdMap` 型変更が P2-2 のテストコードにも影響する。同一エージェントが P2-1 → P2-2 の順で実施を推奨 |
| **Breaking Change の影響範囲** | 中 | `PiperVoiceConfig.PhonemeIdMap`（public フィールド）と `PhonemeSilenceProcessor.SplitAtPhonemeSilence`（public static メソッド）が外部APIとして破壊的変更。v2.0 は破壊的変更リリースのため許容 |
| **PhonemeEncoder コンストラクタシグネチャは不変** | なし | `PhonemeEncoder(PiperVoiceConfig config)` は型が変わるだけで引数数は変わらない |
| **DotNetG2P エンジンへの影響** | なし | 別リポジトリであり uPiper 側の責務外。各エンジンの出力（音素文字列）は変更なし |

### 5.2 レビューチェックリスト

- [ ] `PiperVoiceConfig.PhonemeIdMap` が `Dictionary<string, int[]>` に変更されているか
- [ ] `PhonemeEncoder._phonemeToId` が `Dictionary<string, int>` のまま維持されているか（内部は単一ID）
- [ ] `PhonemeEncoder.InitializePhonemeMapping` で `ids != null && ids.Length > 0` ガードがあるか
- [ ] `PhonemeSilenceProcessor.BuildSilenceIdMap` で `ids[^1]`（最後のID）をトリガーに使用しているか（piper-plus 準拠）
- [ ] `SplitInferenceOrchestrator` のシグネチャが `IReadOnlyDictionary<string, int[]>` に変更されているか
- [ ] `InferenceEngineDemo.ParseConfig` で `idArray.ToObject<int[]>()` に変更されているか
- [ ] テスト12ファイルの `Dictionary<string, int>` が全て `Dictionary<string, int[]>` に変換されているか
- [ ] テストの `{ "key", value }` が全て `{ "key", new[] { value } }` に変換されているか
- [ ] `EnglishPhonemeMappingTest` の `TryGetValue` が `expectedIds[0]` パターンに更新されているか
- [ ] 新規テスト3件が追加されているか
- [ ] 既存テストのアサーション値が変更されていないか（回帰なし）
- [ ] `dotnet format --verify-no-changes` が通過するか
- [ ] `PiperTTS.cs` のログ出力が正常にコンパイルされるか

---

## 6. 一から作り直すとしたら

### 6.1 phoneme_id_map の設計自体の妥当性

piper-plus の `phoneme_id_map` は `Mapping[str, Sequence[int]]` として設計されており、1音素に複数IDを割り当て可能である。しかし、現時点で公開されている全モデルは全エントリが1要素配列である。複数IDマッピングの具体的ユースケースは以下が想定されるが、実際に使用されたモデルは存在しない:

- **合成音素**: 1つの音素記号が複数の内部表現IDに展開されるケース（例: 二重母音を2つの単母音IDに分解）
- **サブワードトークン化**: BPE的なトークン分割で1音素が複数サブトークンIDに対応するケース

ゼロから設計するなら、`Dictionary<string, int>` で十分であり、1音素=1IDのシンプルなマッピングが保守性・可読性・パフォーマンスの全てで優れている。`int[]` の必要性は、piper-plus との互換性要件から生じている。

### 6.2 piper-plus との互換性要件の再考

piper-plus との互換性を維持する理由は明確である:

1. **モデル共有**: piper-plus で学習・エクスポートされたモデル（`.onnx` + `.onnx.json`）をそのまま uPiper で使用する
2. **JSON スキーマ互換**: `phoneme_id_map` の JSON 形式（値が配列）を変更せずにデシリアライズする
3. **推論互換**: `ids.extend(id_map[phoneme])` と同等の展開ロジックにより、同一の入力テンソルを生成する

しかし、互換性のレベルには段階がある:

**レベル1（本チケットの範囲）**: 型を `int[]` にし、`ids[0]` を使用。現行モデルと完全互換。
**レベル2（P2-1b）**: 複数ID展開 + prosody複製。将来モデルとの完全互換。

レベル1で十分な理由は、piper-plus 側でも複数IDモデルが実用化されていないためである。レベル2は YAGNI の可能性が高いが、型を `int[]` にしておくことでレベル2への移行パスが開かれる。

### 6.3 代替設計案

**案 A: アダプターパターン**

JSON デシリアライズ時に `Dictionary<string, int[]>` で受け取り、`PhonemeEncoder` 用に `Dictionary<string, int>` に変換するアダプターレイヤーを挟む。`PiperVoiceConfig.PhonemeIdMap` の型は変更しない。

- メリット: 既存コードの変更が最小（アダプター1クラス + デシリアライズ2箇所のみ）。テスト12ファイルの変更が不要。
- デメリット: 情報損失が「明示的に行われる」だけで、根本的には解決しない。将来の P2-1b 実装時に結局 `int[]` 化が必要になり、二度手間。
- **不採用理由**: v2.0 は破壊的変更リリースであり、中途半端な互換レイヤーを挟むより、型を正しく合わせる方が長期的に健全。

**案 B: ReadOnlyMemory\<int\> の使用**

`int[]` の代わりに `ReadOnlyMemory<int>` を使用し、スライスアクセスを可能にする。

- メリット: 配列コピーなしでサブセットを参照可能。将来の複数ID展開時にパフォーマンス有利。
- デメリット: JSON デシリアライズで `ReadOnlyMemory<int>` への直接変換が困難。`Dictionary<string, ReadOnlyMemory<int>>` は Newtonsoft.Json がネイティブサポートしない。
- **不採用理由**: オーバーエンジニアリング。現行モデルは全て1要素配列であり、`int[]` で十分。

### 6.4 現設計の正直な弱点

1. **`ids[0]` と `ids[^1]` の非対称性**: `PhonemeEncoder` は先頭ID（`ids[0]`）を使用し、`PhonemeSilenceProcessor` は最後のID（`ids[^1]`）を使用する。1要素配列では同じ値だが、複数ID配列では異なる値を参照する。この非対称性は piper-plus 準拠だが、コードレビューで「なぜ違うのか」という質問が予想される。設計ドキュメントとコードコメントでの説明が必要。
2. **将来の P2-1b との二段階変更**: 型だけ `int[]` にして内部ロジックは `ids[0]` のままにすることで、「型は配列だが実質的にスカラーとして使用」という中間状態が生まれる。コードの意図が読み取りにくくなるリスクがある。ただし、これは現行モデルが1要素配列であるという現実に対するプラグマティックな判断であり、過度な先行実装を避ける正当な理由がある。

---

## 7. 後続タスクへの連絡事項

### P2-2（Prosody フラット配列化）への連絡

1. **PhonemeIdMap の型が変更済み**: P2-2 のテストコードで `Dictionary<string, int>` を使用している箇所は、P2-1 完了後は全て `Dictionary<string, int[]>` に変換済みである。P2-2 の変更対象テストファイルが P2-1 と重複する場合（`PhonemeEncoderMultilingualTests` 等）、P2-1 の変更をベースに作業すること。
2. **P2-1 → P2-2 の推奨順序**: M3 グループ A 内で P2-1 を先に完了してから P2-2 に着手すること。逆順だとテストファイルのコンフリクトが発生する。
3. **`_phonemeToId` は `Dictionary<string, int>` のまま**: PhonemeEncoder 内部は単一ID前提。P2-2 で prosody フラット配列化を行う際も、`_phonemeToId` の型は変更不要。

### P2-1b（将来タスク: 複数ID完全対応）への連絡

1. **本チケットでは `ids[0]` 抽出のみ**: PhonemeEncoder 内部の `_phonemeToId` は `Dictionary<string, int>` のまま。複数IDモデルが登場した時点で `Dictionary<string, int[]>` に変更し、`EncodeWithProsody` 内で配列展開 + prosody 複製を実装する。
2. **変更が必要な追加箇所**: `AddToken`（BOS/EOS展開）、`AddPadToken`（PAD展開）、`EncodePhonemeTs`（t/s展開）、`NeedsInterspersePadding` 後のPAD挿入ロジック。
3. **新規テスト3件が回帰テストとして機能**: `PhonemeEncoder_MultiIdPhoneme_UsesFirstId` は P2-1b 実装時に「先頭IDのみ → 全ID展開」に変更される。テストのアサーション値も更新が必要。