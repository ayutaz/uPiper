# MS2-1: Warmup 実装

**マイルストーン**: [MS2: パフォーマンス改善](../piper-plus-v1.10.0-milestones.md#ms2-パフォーマンス改善)
**優先度**: P1
**ステータス**: 未着手
**見積もり**: 3-4時間（実装2h + テスト1h + 検証0.5-1h）
**依存チケット**: なし
**後続チケット**: MS3-2（推論パイプライン変更が重なるため）

---

## 1. タスク目的とゴール

### 背景

piper-plus v1.10.0 では全実装（Python/C++/Rust/C#）で統一された Warmup 機構が導入された（`ort-session-contract.toml` の `[warmup]` セクション）。Unity.InferenceEngine（旧Sentis）においても、初回推論時にはグラフ最適化・カーネルコンパイル・メモリアロケーション等のJITオーバーヘッドが発生し、ユーザーが体感する初回レイテンシが500-800ms程度増大する。

### 目的

モデル初期化直後にダミー入力でのウォームアップ推論を実行することで、ユーザーの最初のTTS呼び出し時のコールドスタートレイテンシを排除する。

### 成功指標

| 指標 | 基準 |
|------|------|
| 初回推論レイテンシ削減 | Warmup有効時、初回`GenerateAudioAsync()`のレイテンシが2回目以降と同等（500-800ms削減） |
| 非破壊性 | Warmup失敗時も`InitializeAsync()`は正常に完了する |
| 後方互換性 | `EnableWarmup = false`（デフォルト）の場合、既存動作と完全に同一 |
| テンソルリーク無し | Warmup中に生成された全テンソルが確実にDisposeされる |

---

## 2. 実装する内容の詳細

### 2.1 PiperConfig に Warmup 設定を追加

**ファイル**: `Assets/uPiper/Runtime/Core/PiperConfig.cs`

`[Header("Advanced Settings")]` セクション（現在の行122付近）に以下のフィールドを追加する:

```csharp
/// <summary>
/// Enable warmup inference after model initialization.
/// Reduces first inference latency by ~500-800ms.
/// </summary>
[Tooltip("Run dummy inference after initialization to reduce first call latency")]
public bool EnableWarmup = false;

/// <summary>
/// Number of warmup inference iterations.
/// ORT JIT cache stabilises in 1-2 runs; 2 provides a safety margin.
/// </summary>
[Tooltip("Number of warmup iterations (piper-plus default: 2)")]
[Range(1, 5)]
public int WarmupIterations = 2;
```

**デフォルトOFF の理由**: 既存ユーザーの初期化時間を変えないため。明示的にオプトインする設計とする。

`Validate()` メソッド（行175付近）に以下のバリデーションを追加:

```csharp
// Warmup iterations validation
if (EnableWarmup && WarmupIterations < 1)
{
    PiperLogger.LogWarning("WarmupIterations ({0}) is less than 1, setting to 1", WarmupIterations);
    WarmupIterations = 1;
}
```

### 2.2 ExecuteWarmup メソッドの実装

**ファイル**: `Assets/uPiper/Runtime/Core/AudioGeneration/InferenceAudioGenerator.cs`

#### 2.2.1 定数の追加（クラス冒頭、行17付近）

```csharp
/// <summary>
/// Length of the dummy phoneme input used during warmup.
/// Matches piper-plus ort-session-contract.toml: phoneme_length = 100
/// </summary>
private const int WarmupPhonemeLength = 100;

/// <summary>BOS token ID per ort-session-contract.toml</summary>
private const int WarmupBosToken = 1;

/// <summary>EOS token ID per ort-session-contract.toml</summary>
private const int WarmupEosToken = 2;

/// <summary>Dummy phoneme ID for warmup filler per ort-session-contract.toml</summary>
private const int WarmupDummyPhoneme = 8;

/// <summary>Warmup noise scale per ort-session-contract.toml</summary>
private const float WarmupNoiseScale = 0.667f;

/// <summary>Warmup length scale per ort-session-contract.toml</summary>
private const float WarmupLengthScale = 1.0f;

/// <summary>Warmup noise W per ort-session-contract.toml</summary>
private const float WarmupNoiseW = 0.8f;
```

#### 2.2.2 WarmupAsync メソッド本体

`ExecuteInference` メソッド（行252）の直前に以下を追加:

```csharp
/// <summary>
/// Warms up the inference engine by running dummy inferences.
/// Eliminates JIT/kernel compilation overhead on the user's first real synthesis call.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors piper-plus SessionFactory.Warmup() (src/csharp/PiperPlus.Core/Inference/SessionFactory.cs).
/// Dummy input: 100 phonemes (BOS=1 + 98 x dummy(8) + EOS=2), scales=[0.667, 1.0, 0.8].
/// Optional inputs (sid, lid, prosody_features) are added based on model capability flags.
/// </para>
/// <para>
/// Must be called on the main thread (within the lock in InitializeAsync).
/// Any exception is caught and logged as a warning — warmup failure must never
/// prevent the application from starting.
/// </para>
/// </remarks>
private void ExecuteWarmup(int iterations)
{
    PiperLogger.LogInfo($"[InferenceAudioGenerator] Starting warmup ({iterations} iterations)...");

    try
    {
        // Build dummy phoneme IDs: BOS(1) + dummy(8) x 98 + EOS(2) = 100 tokens
        var dummyPhonemeIds = new int[WarmupPhonemeLength];
        dummyPhonemeIds[0] = WarmupBosToken;
        for (var i = 1; i < WarmupPhonemeLength - 1; i++)
        {
            dummyPhonemeIds[i] = WarmupDummyPhoneme;
        }
        dummyPhonemeIds[WarmupPhonemeLength - 1] = WarmupEosToken;

        // Build dummy prosody arrays if model supports prosody
        int[] dummyProsodyA1 = null;
        int[] dummyProsodyA2 = null;
        int[] dummyProsodyA3 = null;
        if (_supportsProsody)
        {
            dummyProsodyA1 = new int[WarmupPhonemeLength]; // zero-filled
            dummyProsodyA2 = new int[WarmupPhonemeLength];
            dummyProsodyA3 = new int[WarmupPhonemeLength];
        }

        for (var i = 0; i < iterations; i++)
        {
            PiperLogger.LogDebug($"[InferenceAudioGenerator] Warmup iteration {i + 1}/{iterations}");

            // ExecuteInference handles sid/lid based on _supportsMultiSpeaker/_supportsLanguageId
            // speakerId=0, languageId=0 per ort-session-contract.toml
            var warmupAudio = ExecuteInference(
                dummyPhonemeIds,
                dummyProsodyA1,
                dummyProsodyA2,
                dummyProsodyA3,
                WarmupLengthScale,
                WarmupNoiseScale,
                WarmupNoiseW,
                speakerId: 0,
                languageId: 0);

            PiperLogger.LogDebug($"[InferenceAudioGenerator] Warmup iteration {i + 1} generated {warmupAudio.Length} samples (discarded)");
        }

        PiperLogger.LogInfo($"[InferenceAudioGenerator] Warmup completed ({iterations} iterations)");
    }
    catch (Exception ex)
    {
        // Warmup failure must never prevent the application from starting
        PiperLogger.LogWarning($"[InferenceAudioGenerator] Warmup failed (non-fatal, inference will still work): {ex.Message}");
    }
}
```

**設計判断 — `ExecuteInference` の再利用**:

piper-plus の `SessionFactory.Warmup()` はORT `InferenceSession.Run()` を直接呼んでいるが（`SessionFactory.cs` 行281-285）、uPiper では既存の `ExecuteInference()` メソッド（行252-341）を再利用する。理由:

1. `ExecuteInference` は既にテンソルの生成・設定・Dispose を `try/finally` で管理しており（行275-340）、テンソルリークのリスクがない
2. `_supportsProsody`/`_supportsMultiSpeaker`/`_supportsLanguageId` に応じた条件分岐が既に実装されている（行282-304）
3. Worker APIへのテンソル入力設定（`SetInput`, `Schedule`, `PeekOutput`, `ReadbackAndClone`）の正しい呼び出し順序が保証される
4. Warmup専用のテンソル構築ロジックを重複実装する必要がない

**注意**: `ExecuteInference` は戻り値の `float[]` を割り当てるため、Warmupでは不要な音声データがGCに回る。しかし Warmup は初期化時に1-2回のみ実行されるため、パフォーマンスへの影響は無視できる。MS2-2のArrayPool対応後はこの割り当てもプール化される。

### 2.3 InitializeAsync への Warmup 呼び出し統合

**ファイル**: `Assets/uPiper/Runtime/Core/AudioGeneration/InferenceAudioGenerator.cs`

**挿入位置**: `InitializeAsync` メソッド内、モデル情報検出（`_supportsProsody` 等のフラグ設定、行150-155）完了後、`catch` ブロック（行172）の直前。具体的には行171（prosody_features検証の閉じ括弧 `}` ）の直後。

```csharp
                        // Warmup: run dummy inference to eliminate first-call JIT overhead
                        if (_piperConfig.EnableWarmup && _piperConfig.WarmupIterations > 0)
                        {
                            ExecuteWarmup(_piperConfig.WarmupIterations);
                        }
```

**挿入位置が正しい理由**:

1. この時点で `_worker` は初期化済み（行112-113）
2. `_isInitialized = true` が設定済み（行113）
3. `_supportsProsody`, `_supportsMultiSpeaker`, `_supportsLanguageId` が検出済み（行150-152）
4. まだ `lock (_lockObject)` の内側にいるため、スレッドセーフ
5. メインスレッド上（`RunOnMainThreadAsync` コールバック内）で実行される
6. `ExecuteWarmup` 内の例外は catch されるため、後続の初期化ログ出力に影響しない

### 2.4 piper-plus 仕様との対応表

| piper-plus `ort-session-contract.toml` | uPiper 実装 | 備考 |
|---------------------------------------|-------------|------|
| `phoneme_length = 100` | `WarmupPhonemeLength = 100` | 同一 |
| `bos_token = 1` | `WarmupBosToken = 1` | 同一 |
| `eos_token = 2` | `WarmupEosToken = 2` | 同一 |
| `dummy_phoneme = 8` | `WarmupDummyPhoneme = 8` | 同一 |
| `default_runs = 2` | `WarmupIterations = 2` (PiperConfig) | PiperConfigで設定可能 |
| `noise_scale = 0.667` | `WarmupNoiseScale = 0.667f` | 同一 |
| `length_scale = 1.0` | `WarmupLengthScale = 1.0f` | 同一 |
| `noise_w = 0.8` | `WarmupNoiseW = 0.8f` | 同一 |
| `sid_value = 0` | `speakerId: 0` (引数) | 同一 |
| `lid_value = 0` | `languageId: 0` (引数) | 同一 |
| `prosody_shape = "1 x phoneme_length x 3"` | `CreateProsodyTensor` が自動構築 | ExecuteInference内 |
| `prosody_fill = 0` | `new int[WarmupPhonemeLength]` (zero-filled) | 同一 |

**注意**: piper-plus の `ort-session-contract.toml` では scales 配列が `[noise_scale, length_scale, noise_w]` 順だが、`ExecuteInference` の引数順序は `(lengthScale, noiseScale, noiseW)`。名前付き定数で渡すため実装上の問題はないが、対応関係に注意。

### 2.5 piper-plus SessionFactory.Warmup() との差分

| 項目 | piper-plus (SessionFactory.cs 行209-306) | uPiper (本チケット) |
|------|----------------------------------------|-------------------|
| 推論API | `InferenceSession.Run()` (OrtValue直接操作) | `ExecuteInference()` 経由の `Worker.Schedule()` |
| テンソル型 | `long[]` (ORT int64) | `int[]` (Sentis Int) |
| テンソル生成 | `OrtValue.CreateTensorValueFromMemory` | `new Tensor<int>(TensorShape, data)` |
| テンソルDisposeパターン | 手動using / finally | `ExecuteInference`のfinally内で一括Dispose |
| モデル入力検出 | `session.InputMetadata.ContainsKey()` | `_supportsProsody` / `_supportsMultiSpeaker` / `_supportsLanguageId` フラグ |
| 出力テンソル | `using var results = session.Run(...)` | `PeekOutput` → `ReadbackAndClone` → Dispose |
| 出力名の動的判定 | `OutputMetadata.ContainsKey("durations")` | `GetOutputTensor()` が `_model.outputs[0].name` を使用 |
| 実行スレッド | 呼び出し元スレッド | メインスレッド（Unity制約） |

---

## 3. エージェントチーム構成

| ロール | 担当エージェント | 責務 |
|--------|----------------|------|
| **実装エージェント** | Claude Code | PiperConfig変更、WarmupAsyncメソッド実装、InitializeAsync統合 |
| **テストエージェント** | Claude Code | ユニットテスト作成、既存テストの回帰確認 |
| **レビューエージェント** | 人間（@yousan） | テンソルライフサイクル、スレッドセーフティ、piper-plus仕様準拠の最終確認 |

**作業フロー**:
1. 実装エージェントが PiperConfig → ExecuteWarmup → InitializeAsync の順で変更
2. テストエージェントがユニットテストを作成・実行
3. 実装エージェントが `dotnet format --verify-no-changes` でフォーマットチェック
4. レビューエージェントが差分を確認し、テンソルDispose漏れがないことを検証

---

## 4. 提供範囲・テスト項目

### 提供範囲 (Scope)

**In Scope**:
- `PiperConfig.cs`: `EnableWarmup`, `WarmupIterations` フィールド追加 + バリデーション
- `InferenceAudioGenerator.cs`: `ExecuteWarmup()` メソッド追加、`InitializeAsync()` への統合
- `InferenceAudioGeneratorTests.cs`: 新規テストファイル作成
- piper-plus `ort-session-contract.toml` `[warmup]` セクションの全パラメータ準拠

**Out of Scope**:
- ORT SessionOptions の設定（Unity.InferenceEngine が低レベル設定を公開しないため不可）
- セッションキャッシュ（`.opt.onnx`）の実装（同上の理由）
- 環境変数 `PIPER_DISABLE_WARMUP` の対応（PiperConfigで制御する設計）
- ArrayPool最適化（MS2-2で対応）

### Unit テスト

**ファイル**: `Assets/uPiper/Tests/Runtime/AudioGeneration/InferenceAudioGeneratorWarmupTests.cs`（新規作成）

**注意**: `InferenceAudioGenerator` は `Unity.InferenceEngine` の `Worker`, `ModelLoader`, `ModelAsset` に強く依存しているため、Unity Editor上でのEditModeテストとなる。実モデルを使ったテストはPlayModeテスト/E2Eテストで行い、ここではモック不可能な部分はスキップする。

| テストケース | テスト内容 | 手法 |
|------------|---------|------|
| `PiperConfig_EnableWarmup_DefaultFalse` | デフォルトのPiperConfigで`EnableWarmup`がfalseであることを確認 | `PiperConfig.CreateDefault()` の検証 |
| `PiperConfig_WarmupIterations_DefaultTwo` | デフォルトの`WarmupIterations`が2であることを確認 | `PiperConfig.CreateDefault()` の検証 |
| `PiperConfig_Validate_WarmupIterationsClampedToMin` | `EnableWarmup=true, WarmupIterations=0` でValidate後に1に補正されることを確認 | `Validate()` 呼び出し後の値チェック |
| `PiperConfig_Validate_WarmupDisabled_IterationsNotClamped` | `EnableWarmup=false` の場合、`WarmupIterations` のバリデーションがスキップされることを確認 | `Validate()` 呼び出し後の値チェック |
| `WarmupConstants_MatchPiperPlusContract` | 定数値がort-session-contract.tomlと一致することを確認 | リフレクションで定数値を検証 |

### E2E テスト

Unity Editor 上での手動テスト（CI統合は後続チケットで検討）:

| テストケース | 手順 | 期待結果 |
|------------|------|---------|
| Warmup有効 + 初回レイテンシ測定 | `EnableWarmup=true`で初期化後、`Stopwatch`で初回`GenerateAudioAsync`のレイテンシを測定 | 2回目以降と同程度（500-800ms差がない） |
| Warmup無効（デフォルト） | `EnableWarmup=false`で初期化し、通常通りTTS呼び出し | 既存動作と同一（回帰なし） |
| Warmup有効 + Prosodyモデル | `_supportsProsody=true`のモデルでWarmup実行 | Prosodyテンソル付きのダミー推論が成功 |
| Warmup有効 + Multi-speakerモデル | `_supportsMultiSpeaker=true`のモデルでWarmup実行 | sidテンソル付きのダミー推論が成功 |
| Warmup有効 + 多言語モデル | `_supportsLanguageId=true`のモデルでWarmup実行 | lidテンソル付きのダミー推論が成功 |
| Warmup中のモデルエラー（異常系） | 不正なモデルでWarmup実行 | 警告ログが出力され、`InitializeAsync`は正常完了 |

---

## 5. 懸念事項・レビュー項目

### 5.1 テンソルライフサイクル / Dispose

**懸念**: Warmup中に生成されたテンソルがリークする可能性。

**対策**: `ExecuteInference()` メソッドは既にテンソルのDispose を `finally` ブロック（行332-340）で管理している。**注意**: `finally` ブロック（行331-340）は**入力テンソル6種のみ**をDisposeする。出力テンソル（`outputTensor`, `readableTensor`）は `try` ブロック内の行321-322で手動Dispose。`ExtractAudioData()` が例外を投げた場合、出力テンソルがリークするリスクがある（既存問題）。Warmup では `ExecuteInference` を再利用するため同じリスクを継承するが、Warmup 失敗時は try-catch で捕捉するため実害は小さい。

### 5.2 スレッドセーフティ

**懸念**: `ExecuteWarmup` が並行呼び出しされた場合のWorkerの競合。

**対策**: `ExecuteWarmup` は `InitializeAsync` 内の `lock (_lockObject)` スコープ（行84-178）内から呼ばれる。また、`InitializeAsync` 自体が `RunOnMainThreadAsync` 内で実行されるため、メインスレッドの逐次実行が保証されている。外部から `ExecuteWarmup` を呼ぶ経路はない（private メソッド）。

### 5.3 エラーハンドリング

**懸念**: Warmup失敗が `InitializeAsync` 全体を失敗させてしまうリスク。

**対策**: `ExecuteWarmup` メソッド内の全処理を `try-catch` で囲み、例外時は `PiperLogger.LogWarning` で警告を出力するのみとする。これは piper-plus `SessionFactory.Warmup()` と同じパターン（`SessionFactory.cs` 行297-305）。

**重要**: `ExecuteWarmup` 内の `catch` で例外を握りつぶすため、Warmup中のバグが見逃されるリスクがある。開発時は `PiperConfig.EnableDebugLogging = true` でスタックトレースを含むログを出力すること。

### 5.4 Sentis (Unity.InferenceEngine) 固有の懸念

| 項目 | 詳細 |
|------|------|
| **GPU バッファ初期化** | GPUPixel/GPUCompute バックエンドでは、初回 `Schedule()` 時にGPUバッファの確保・シェーダーコンパイルが発生する。Warmupはこれを事前実行するため効果が大きい |
| **ReadbackAndClone のコスト** | Warmup時も `ReadbackAndClone()` が呼ばれ、GPU→CPUデータ転送が発生する。Warmup専用にこれを省略する最適化は可能だが、`ExecuteInference` の再利用を優先し、初期実装では行わない |
| **Worker の状態** | `Worker.Schedule()` 実行後の内部状態がWarmupとユーザー推論で干渉しないことを確認する必要がある。Sentisの `Worker` は毎回 `SetInput` → `Schedule` → `PeekOutput` のサイクルで動作するため、前回の推論状態は上書きされる（問題なし） |
| **Metal バックエンド** | macOSではCPUバックエンドにフォールバックするため（行452-459）、Warmupの効果はORT JITキャッシュのみ（シェーダーコンパイルなし）。それでも初回レイテンシ削減効果はある |

### 5.5 初期化時間への影響

Warmup有効時、`InitializeAsync` の所要時間は2回のダミー推論分だけ増加する。100音素のダミー入力では推論時間は概ね200-400ms/回（GPUバックエンド）であり、2回で400-800msの追加コストとなる。

この追加コストは初回推論で削減されるレイテンシと相殺されるため、ユーザー体験としてはトータルで改善する。ただし、アプリ起動からTTSが使えるまでの時間は増加するため、起動速度が重要なケースでは `EnableWarmup = false` のデフォルト動作を推奨する。

### 5.6 WebGL 環境

**WebGL 環境**: WebGL はシングルスレッドのため、Warmup の2回のダミー推論がブラウザのメインスレッドをブロックし UI フリーズを引き起こす可能性がある。`EnableWarmup` のデフォルトは `false` なので通常は問題ないが、WebGL ビルドでは `EnableWarmup = false` を推奨する旨をドキュメントに記載すること。将来的には `#if UNITY_WEBGL` で自動スキップの検討も可能。

---

## 6. ゼロから作り直すとしたら

もし `InferenceAudioGenerator` をゼロから設計できるとしたら、以下の変更を行う:

1. **推論メソッドの分離**: 現在の `ExecuteInference` はテンソル構築・推論実行・結果抽出が1メソッドに凝縮されている。テンソル構築を別メソッドに分離し、Warmupでは「テンソル構築 + 推論実行」のみ、通常推論では「テンソル構築 + 推論実行 + 結果抽出」と組み合わせられるようにする。これにより Warmup時の不要な `ReadbackAndClone` を回避できる。

2. **IWarmupStrategy インターフェース**: Warmup のダミー入力構築ロジックをストラテジーパターンで差し替え可能にする。例えば、モデル固有の代表的な入力でWarmupしたい場合や、入力長を変えたい場合に対応できる。

3. **初期化フェーズの明確化**: 現在の `InitializeAsync` はモデル読み込み → Worker作成 → 入力検出 → Warmup が1つの `RunOnMainThreadAsync` コールバック内に詰め込まれている。各フェーズを明示的なステートマシンにし、進捗通知（`IProgress<T>`）を提供する設計にする。

4. **非同期Warmup**: 現在の設計ではWarmupは同期的に（`InitializeAsync` 内のメインスレッドコールバックで）実行される。代替として、Warmupをバックグラウンドタスクとして開始し、最初の `GenerateAudioAsync` 呼び出し時にWarmup完了を待つ「遅延Warmup」パターンも検討に値する。ただし、Unity.InferenceEngineのWorkerはメインスレッドでの操作が必要なため、実装の複雑さは増す。

ただし現時点では、`ExecuteInference` の再利用が最もシンプルで安全な実装方針である。上記は将来の大規模リファクタリング時の参考として記録する。

---

## 7. 後続タスクへの連絡事項

### MS2-2 (ArrayPool によるメモリ最適化) への連絡

- Warmup は `ExecuteInference()` を再利用するため、MS2-2 で `ExtractAudioData()` や `CreateProsodyTensor()` をArrayPool化した場合、Warmupも自動的にその恩恵を受ける。追加対応は不要。
- ただし、Warmup中の `ArrayPool.Return()` が正しく呼ばれることを MS2-2 のテストで確認すること。

### MS3-2 (沈黙句分割 — PhonemeSilenceProcessor) への連絡

- Warmup は `InferenceAudioGenerator.InitializeAsync()` 内で完結し、`GenerateAudioAsync()` / `GenerateAudioWithProsodyAsync()` のインターフェースには変更を加えない。
- MS3-2 で推論パイプラインに句分割を挿入する際、Warmup との干渉はない。
- ただし、MS3-2 で `ExecuteInference` のシグネチャや内部ロジックを変更する場合、`ExecuteWarmup` が引き続き正しく動作することを確認すること。

### テストファイルの共有

- 本チケットで `Assets/uPiper/Tests/Runtime/AudioGeneration/InferenceAudioGeneratorWarmupTests.cs` を新規作成する。MS2-2 や MS3-2 で `InferenceAudioGenerator` のテストを追加する場合は、このファイルに追記すること。
