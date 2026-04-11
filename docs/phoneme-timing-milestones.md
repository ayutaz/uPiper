# フォネムタイミング機能 — マイルストーン

**作成日**: 2026-04-11
**参照**: `phoneme-timing-spec.md`, `phoneme-timing-investigation.md`, `phoneme-timing-requirements.md`
**チケット一覧**: [tickets/index.md](./tickets/index.md)

---

## 全体構成

```
Phase 1: 基盤型定義          ← 依存なし
Phase 2: 推論レイヤー        ← Phase 1 に依存
Phase 3: オーケストレーション ← Phase 1, 2 に依存
Phase 4: 公開API             ← Phase 1, 2, 3 に依存
```

---

## Phase 1: 基盤型定義

**目標**: タイミング計算に必要な全データ型と変換ロジックを定義し、単体テストで正確性を保証する。

### 実装タスク

#### P1-1: PhonemeTimingEntry 定義 ([ticket](./tickets/P1-1.md))

- **新規**: `Assets/uPiper/Runtime/Core/AudioGeneration/PhonemeTimingResult.cs`
- **内容**:
  - `public readonly struct PhonemeTimingEntry` を定義
  - フィールド: `Phoneme`(string), `StartSeconds`(float), `EndSeconds`(float), `DurationSeconds`(float, 算出)
  - piper-plus `TimingWriter.PhonemeTimingEntry` と同等、秒単位
- **依存**: なし
- **完了条件**: コンパイル通過、readonly struct として定義済み

#### P1-2: InferenceOutput 定義 ([ticket](./tickets/P1-2.md))

- **新規**: `Assets/uPiper/Runtime/Core/AudioGeneration/InferenceOutput.cs`
- **内容**:
  - `internal readonly struct InferenceOutput : IDisposable` を定義
  - `NativeArray<float> Audio` + `NativeArray<float> Durations`
  - durations 未対応時は `Durations.IsCreated == false`
  - `Dispose()` で両 NativeArray を安全に破棄
- **依存**: なし
- **完了条件**: コンパイル通過、IDisposable パターン実装

#### P1-3: SynthesisWithTimingResult 定義 ([ticket](./tickets/P1-3.md))

- **新規**: `Assets/uPiper/Runtime/Core/AudioGeneration/SynthesisWithTimingResult.cs`
- **内容**:
  - `public sealed class SynthesisWithTimingResult` を定義
  - フィールド: `AudioClip`, `IReadOnlyList<PhonemeTimingEntry> Timings`(nullable), `TotalDurationSeconds`
  - `internal` コンストラクタ（PhonemizeResult パターン踏襲）
- **依存**: P1-1
- **完了条件**: コンパイル通過

#### P1-4: TimingCalculator 実装 ([ticket](./tickets/P1-4.md))

- **新規**: `Assets/uPiper/Runtime/Core/AudioGeneration/TimingCalculator.cs`
- **内容**:
  - `public static class TimingCalculator`
  - `Calculate(int[] phonemeIds, float[] durations, Dictionary<string, int[]> phonemeIdMap, int sampleRate, int hopSize = 256)` → `List<PhonemeTimingEntry>`
  - piper-plus `TimingWriter.CalculateTiming` (L70-120) のアルゴリズムを移植
    - `frameLength = (float)hopSize / sampleRate` (≈0.01161秒)
    - PAD(0)/BOS(1)/EOS(2) スキップ（時間カーソルは進行、エントリ生成なし）
    - 逆引き辞書構築（`BuildReverseIdMap`）、PUA 解決（`PuaTokenMapper.UnmapChar`）
  - エッジケース: 負 durations → クランプ、長さ不一致 → `Math.Min` 打ち切り + 警告、sampleRate/hopSize <= 0 → `ArgumentOutOfRangeException`
- **依存**: P1-1
- **完了条件**: piper-plus 同等ロジック実装済み

### テストタスク

#### T1-1: TimingCalculator 基本計算テスト ([ticket](./tickets/T1-1.md))

- **新規**: `Assets/uPiper/Tests/Editor/AudioGeneration/TimingCalculatorTests.cs`
- **検証**: durations `[10.0, 5.0]` × `frameLength` で `startSeconds`/`endSeconds` が正確
- **依存**: P1-4
- **完了条件**: piper-plus TimingWriterTests と同等の精度検証パス

#### T1-2: 特殊トークンスキップテスト ([ticket](./tickets/T1-2.md))

- **ファイル**: 同上
- **検証**: PAD(0), BOS(1), EOS(2) がエントリ生成せず時間のみ進行
- **依存**: P1-4

#### T1-3: PUA 逆引きテスト ([ticket](./tickets/T1-3.md))

- **ファイル**: 同上
- **検証**: PUA 文字 `\uE000` → `"a:"` 等、未知 ID → `"?"` フォールバック
- **依存**: P1-4

#### T1-4: エッジケーステスト ([ticket](./tickets/T1-4.md))

- **ファイル**: 同上
- **検証**: 空入力、null 入力、長さ不一致、負 durations、sampleRate/hopSize == 0
- **依存**: P1-4
- **完了条件**: 全エッジケース(E2-E8)で例外またはグレースフル動作

#### T1-5: PhonemeTimingEntry 構造テスト ([ticket](./tickets/T1-5.md))

- **ファイル**: 同上
- **検証**: readonly struct の値一致、`DurationSeconds == EndSeconds - StartSeconds`
- **依存**: P1-1

### Phase 1 完了検証

- [ ] 新規4ファイルがコンパイル通過
- [ ] TimingCalculatorTests 全件パス
- [ ] 既存テストにリグレッションなし（既存コード変更なし）

---

## Phase 2: 推論レイヤー

**目標**: `IInferenceAudioGenerator` の戻り値を `InferenceOutput` に拡張し、durations テンソル読み取りと短テキストデコレータ追従を完了する。

### 実装タスク

#### P2-1: IInferenceAudioGenerator 戻り値型変更 ([ticket](./tickets/P2-1.md))

- **変更**: `Assets/uPiper/Runtime/Core/AudioGeneration/IInferenceAudioGenerator.cs` (L47)
- **内容**:
  - `Task<NativeArray<float>> GenerateAudioAsync(...)` → `Task<InferenceOutput> GenerateAudioAsync(...)`
  - remarks ドキュメント更新（"Caller owns and must Dispose the returned InferenceOutput"）
- **依存**: P1-2
- **完了条件**: インターフェース変更済み（コンパイルエラーは後続タスクで解消）

#### P2-2: InferenceAudioGenerator durations 読み取り ([ticket](./tickets/P2-2.md))

- **変更**: `Assets/uPiper/Runtime/Core/AudioGeneration/InferenceAudioGenerator.cs`
- **内容**:
  - フィールド追加: `_cachedDurationsOutputName`(string), `_supportsDurations`(bool)
  - `InitializeAsync` (L176-182 付近): `_model.outputs.Count >= 2` で2番目出力名キャッシュ
  - `ExtractResults` (L436-463): durations テンソルを audio と同パターンで抽出
    - `PeekOutput` → `ReadbackAndClone` → `DownloadToNativeArray` → `NativeArray.Copy`
    - `InferenceOutput` を返却
  - `ExecuteInference` (L337-378): 戻り値を `InferenceOutput` に変更、例外時に Dispose
  - `GenerateAudioAsync` (L234-267): 戻り値型を `Task<InferenceOutput>` に変更
  - `DisposeWorker`: `_cachedDurationsOutputName = null` 追加
  - `SupportsDurations` プロパティ追加
- **依存**: P2-1
- **完了条件**: durations あり/なしモデル両方で正常動作

#### P2-3: ShortTextMitigatingGenerator 追従 ([ticket](./tickets/P2-3.md))

- **変更**: `Assets/uPiper/Runtime/Core/AudioGeneration/ShortTextMitigatingGenerator.cs` (L53-92)
- **内容**:
  - `GenerateAudioAsync` 戻り値を `Task<InferenceOutput>` に変更
  - パディング時: `_inner.GenerateAudioAsync()` から取得した `InferenceOutput.Durations` に対し、PAD 挿入位置（`deficit/2`, `deficit - deficit/2`）のエントリを除去して元の音素数に復元
  - `TrimSilence` 後の audio とクリーン済み durations で新 `InferenceOutput` を構築
  - 非パディング時: `InferenceOutput` をそのまま透過
- **依存**: P2-1, P2-2
- **完了条件**: 短テキスト（phonemeIds < 40）で元の音素数と一致する durations が返却

#### P2-4: StubInferenceAudioGenerator durations 対応 ([ticket](./tickets/P2-4.md))

- **変更**: `Assets/uPiper/Tests/Editor/AudioGeneration/StubInferenceAudioGenerator.cs`
- **内容**:
  - `float[] DurationsToReturn` プロパティ追加
  - `GenerateAudioAsync` 戻り値を `InferenceOutput` に変更
  - `DurationsToReturn != null` なら `NativeArray<float>` にコピーして返却
- **依存**: P2-1
- **完了条件**: 既存テスト + 新規テストで Stub が正常動作

### テストタスク

#### T2-1: ShortTextMitigatingGenerator PAD 除去テスト ([ticket](./tickets/T2-1.md))

- **新規**: `Assets/uPiper/Tests/Editor/AudioGeneration/ShortTextMitigatingGeneratorTimingTests.cs`
- **検証**: パディング 5→40 のケースで durations 要素が正しくフィルタされ、除去後エントリ数 == 元の実音素数
- **依存**: P2-3, P2-4
- **完了条件**: パディング挿入/除去の往復で音素対応が維持

#### T2-2: InferenceOutput Dispose テスト ([ticket](./tickets/T2-2.md))

- **新規**: `Assets/uPiper/Tests/Editor/AudioGeneration/InferenceOutputTests.cs`
- **検証**: Audio/Durations 両方の NativeArray が Dispose される。Durations 未作成時も安全に Dispose
- **依存**: P1-2
- **完了条件**: NativeArray リークなし

### Phase 2 完了検証

- [ ] 全ソースがコンパイル通過
- [ ] 既存テスト全件パス（Stub の InferenceOutput 対応により）
- [ ] InferenceAudioGenerator が durations 対応/非対応モデルで正常初期化
- [ ] ShortTextMitigatingGenerator のパディング除去ロジックがテストで検証済み

---

## Phase 3: オーケストレーション

**目標**: `InferenceOutput`（durations）をパイプライン全体に伝搬し、`TimingCalculator` で秒タイミングに変換してキャッシュに格納する。

### 実装タスク

#### P3-1: SplitInferenceOrchestrator durations 結合 ([ticket](./tickets/P3-1.md))

- **変更**: `Assets/uPiper/Runtime/Core/AudioGeneration/SplitInferenceOrchestrator.cs`, `ISplitInferenceOrchestrator.cs`
- **内容**:
  - 戻り値を `Task<NativeArray<float>>` → `Task<InferenceOutput>` に変更
  - 句ごとの `_generator.GenerateAudioAsync()` が返す `InferenceOutput.Durations` を `List<float>` に累積結合
  - 句間の無音サンプル（`SilenceSamples`）は durations に含めない（タイミング計算時に TTSSynthesisOrchestrator 側でオフセット加算）
  - 結合した durations を `NativeArray<float>(Allocator.Persistent)` に詰めて `InferenceOutput` を構築
  - 句ごとの `InferenceOutput` を正しく Dispose
- **依存**: P2-1, P2-3
- **完了条件**: 2句以上の入力で、結合後の durations 長が各句の合計と一致

#### P3-2: TTSSynthesisOrchestrator タイミング計算統合 ([ticket](./tickets/P3-2.md))

- **変更**: `Assets/uPiper/Runtime/Core/AudioGeneration/TTSSynthesisOrchestrator.cs`
- **内容**:
  - `SynthesizeAsync` 内部を2段構成に分割: 共通ロジック（エンコード/推論/正規化/キャッシュ）+ AudioClip 返却ラッパー
  - 新規 internal メソッド `SynthesizeWithTimingCoreAsync` 追加
    - `InferenceOutput` から durations 取得
    - `TimingCalculator.Calculate()` 呼び出し
    - `SynthesisWithTimingResult` を構築して返却
  - `_generator.GenerateAudioAsync()` / `_splitOrchestrator.GenerateWithSilenceSplitAsync()` の戻り値を `InferenceOutput` で受け取り
  - Audio 部分のみ正規化・AudioClip 構築
  - 既存 `SynthesizeAsync` は内部で共通ロジックを呼び `.AudioClip` のみ返却（後方互換）
- **依存**: P1-4, P3-1
- **完了条件**: タイミング付き合成で `Timings` が非 null、既存 `SynthesizeAsync` が AudioClip のみ返却

#### P3-3: AudioSynthesisCache タイミング格納拡張 ([ticket](./tickets/P3-3.md))

- **変更**: `Assets/uPiper/Runtime/Core/AudioGeneration/AudioSynthesisCache.cs`
- **内容**:
  - `CacheEntry` に `PhonemeTimingEntry[] Timings` フィールド追加（nullable）
  - `MemoryBytes` 計算にタイミングサイズ加算（エントリ数 × ~32bytes）
  - `TryGet` シグネチャ拡張: `out PhonemeTimingEntry[] timings` パラメータ追加
  - `Set` シグネチャ拡張: `PhonemeTimingEntry[] timings = null` パラメータ追加
  - ハッシュキー生成は変更不要（同一入力なら同一 durations）
- **依存**: P1-1
- **完了条件**: キャッシュヒット時にタイミングデータも返却

### テストタスク

#### T3-1: SplitInferenceOrchestrator 句分割タイミングテスト ([ticket](./tickets/T3-1.md))

- **変更**: `Assets/uPiper/Tests/Editor/AudioGeneration/SplitInferenceOrchestratorTests.cs`
- **検証**: 2句分の `InferenceOutput` を Stub で返し、結合後の Audio.Length と Durations.Length を検証。句間無音オフセットが累積
- **依存**: P3-1, P2-4

#### T3-2: AudioSynthesisCache タイミング格納テスト ([ticket](./tickets/T3-2.md))

- **変更**: `Assets/uPiper/Tests/Editor/AudioGeneration/AudioSynthesisCacheTests.cs`
- **検証**: `Set`(with timings) → `TryGet` で timings が復元されること
- **依存**: P3-3

#### T3-3: durations 非対応モデルテスト ([ticket](./tickets/T3-3.md))

- **変更**: `Assets/uPiper/Tests/Editor/AudioGeneration/TTSSynthesisOrchestratorTests.cs`
- **検証**: Stub の Durations を `default` にして `Timings == null`、例外なし
- **依存**: P3-2, P2-4

### Phase 3 完了検証

- [ ] 全ソースがコンパイル通過
- [ ] SplitInferenceOrchestrator の durations 結合テストパス
- [ ] キャッシュのタイミング格納/取得テストパス
- [ ] durations 非対応モデルで例外が発生しない
- [ ] 既存テスト全件パス

---

## Phase 4: 公開API

**目標**: `IPiperTTS.SynthesizeWithTimingAsync` を公開し、エンドツーエンドでタイミング情報を取得可能にする。

### 実装タスク

#### P4-1: IPiperTTS インターフェース拡張 ([ticket](./tickets/P4-1.md))

- **変更**: `Assets/uPiper/Runtime/Core/IPiperTTS.cs`
- **内容**:
  - `Task<SynthesisWithTimingResult> SynthesizeWithTimingAsync(SynthesisRequest request, CancellationToken cancellationToken = default)` を追加
  - XML doc に「モデルが durations 非対応の場合 Timings は null」を明記
  - 既存メソッドは一切変更しない
- **依存**: P1-3
- **完了条件**: インターフェースがコンパイル通過

#### P4-2: PiperTTS.Inference.cs 実装 ([ticket](./tickets/P4-2.md))

- **変更**: `Assets/uPiper/Runtime/Core/PiperTTS.Inference.cs`
- **内容**:
  - `SynthesizeWithTimingAsync` を実装
  - Dispose/初期化チェック後、`_orchestrator.SynthesizeWithTimingCoreAsync(request, ct)` に委譲
  - 既存 `SynthesizeAsync` は変更不要
- **依存**: P4-1, P3-2
- **完了条件**: `PhonemizeAsync` → `SynthesizeWithTimingAsync` の呼び出しチェーンでコンパイルエラーなし

### テストタスク

#### T4-1: 統合テスト — タイミング付き合成 ([ticket](./tickets/T4-1.md))

- **新規**: `Assets/uPiper/Tests/Editor/PiperTTSSynthesizeWithTimingTests.cs`
- **検証**:
  - Stub 経由で `SynthesizeWithTimingAsync` を呼び、`Timings` エントリ数 > 0
  - `StartSeconds` / `EndSeconds` の値が妥当
- **依存**: P4-2, P2-4

#### T4-2: 統合テスト — タイミング精度 ([ticket](./tickets/T4-2.md))

- **ファイル**: 同上
- **検証**: 最終エントリの `EndSeconds` と `AudioClip.length` の差が 50ms 以内
- **依存**: P4-2

#### T4-3: 統合テスト — キャッシュ整合 ([ticket](./tickets/T4-3.md))

- **ファイル**: 同上
- **検証**: 1回目 miss → 2回目 hit でタイミングが同一
- **依存**: P4-2, P3-3

#### T4-4: 統合テスト — durations 非対応 ([ticket](./tickets/T4-4.md))

- **ファイル**: 同上
- **検証**: Stub の Durations を `default` にして `Timings == null`、例外なし
- **依存**: P4-2

#### T4-5: リグレッションテスト ([ticket](./tickets/T4-5.md))

- **ファイル**: 既存テスト全体
- **検証**: 既存の `SynthesizeAsync` / `GenerateAudioAsync` の動作に変更がないこと
- **依存**: P4-2

### Phase 4 完了検証

- [ ] CI (`unity-tests.yml`) で EditMode + PlayMode テスト全件パス
- [ ] 新規統合テスト 5件以上が GREEN
- [ ] 既存公開 API にリグレッションなし
- [ ] `IPiperTTS` に破壊的変更がないこと（新規メソッド追加のみ）

---

## リスク対策

| ID | リスク | 対策 | 紐付きタスク |
|---|---|---|---|
| R1 | ShortTextMitigatingGenerator の PAD パディングが durations の音素対応を崩す | パディング挿入位置(afterBos/beforeEos)を記録し、推論後に durations 配列から該当インデックスを除去 | P2-3, T2-1 |
| R2 | SplitInferenceOrchestrator の句間無音がタイミングに反映されない | 句ごとの durations 結合時に `silenceSamples/sampleRate` 秒を時間オフセットとして累積加算 | P3-1, T3-1 |
| R3 | hop_length が onnx.json に未定義で将来モデルと不整合 | `PiperVoiceConfig` に `HopSize` nullable プロパティ追加、未定義時はデフォルト 256 使用 | P1-4 |
| R4 | durations 出力がないモデルで例外発生 | `_supportsDurations` フラグで分岐、`InferenceOutput.Durations.IsCreated == false` で判定 | P2-2, T3-3 |
| R5 | durations.Length != phonemeIds.Length（モデルバグ等） | `Math.Min` で打ち切り + 警告ログ出力 | P1-4, T1-4 |

---

## 受入基準

全 Phase 完了後の最終検証項目:

| # | 基準 | 検証方法 |
|---|---|---|
| A1 | タイミング精度 | 最終エントリの `EndSeconds` と `AudioClip.length` の差が 50ms 以内 |
| A2 | リグレッションなし | 既存 EditMode/PlayMode テスト全件パス（CI green） |
| A3 | 短テキスト + タイミング | 5音素入力でパディング除去後のタイミングエントリ数 == 元の実音素数 |
| A4 | 句分割 + タイミング | 2句以上の入力で句2のタイミングに句1長 + 無音オフセットが正しく加算 |
| A5 | durations 非対応モデル | `SynthesizeWithTimingAsync` が `Timings == null` を返し例外なし |
| A6 | キャッシュ整合 | 同一入力の2回目呼び出しでキャッシュヒットし、タイミング情報も返却 |

---

## タスク依存関係図

```
P1-1 ──────────────────────────────────────────┐
  │                                             │
P1-2 ─────────────┐                            │
  │                │                            │
P1-3 ─── (P1-1) ──│──────────── P4-1 ── P4-2  │
  │                │                 │          │
P1-4 ─── (P1-1) ──│──── P3-2 ───────┘          │
  │                │      │                     │
T1-1~5 ── (P1-4)  │      │                     │
                   │      │                     │
P2-1 ─── (P1-2) ──┘      │                     │
  │                       │                     │
P2-2 ─── (P2-1)          │                     │
  │                       │                     │
P2-3 ─── (P2-1, P2-2)    │                     │
  │                       │                     │
P2-4 ─── (P2-1)          │                     │
  │                       │                     │
T2-1~2 ── (P2-3, P2-4)   │                     │
                          │                     │
P3-1 ─── (P2-1, P2-3) ──→│                     │
                          │                     │
P3-3 ─── (P1-1) ─────────│─────────────────────┘
                          │
T3-1~3 ── (P3-1, P3-2, P3-3)
                          │
T4-1~5 ── (P4-2, P3-3)
```

---

## 変更ファイル一覧（全Phase）

### 新規ファイル（7件）

| ファイル | Phase | 内容 |
|---|---|---|
| `AudioGeneration/PhonemeTimingResult.cs` | P1-1 | PhonemeTimingEntry readonly struct |
| `AudioGeneration/InferenceOutput.cs` | P1-2 | 内部推論結果構造体 |
| `AudioGeneration/SynthesisWithTimingResult.cs` | P1-3 | 公開結果クラス |
| `AudioGeneration/TimingCalculator.cs` | P1-4 | フレーム→秒変換 |
| `Tests/Editor/AudioGeneration/TimingCalculatorTests.cs` | T1 | 単体テスト |
| `Tests/Editor/AudioGeneration/ShortTextMitigatingGeneratorTimingTests.cs` | T2-1 | PAD除去テスト |
| `Tests/Editor/AudioGeneration/InferenceOutputTests.cs` | T2-2 | Dispose テスト |

### 変更ファイル（10件）

| ファイル | Phase | 内容 |
|---|---|---|
| `AudioGeneration/IInferenceAudioGenerator.cs` | P2-1 | 戻り値型変更 |
| `AudioGeneration/InferenceAudioGenerator.cs` | P2-2 | durations 読み取り追加 |
| `AudioGeneration/ShortTextMitigatingGenerator.cs` | P2-3 | 戻り値型追従 + PAD 除去 |
| `AudioGeneration/ISplitInferenceOrchestrator.cs` | P3-1 | 戻り値型変更 |
| `AudioGeneration/SplitInferenceOrchestrator.cs` | P3-1 | durations 結合 |
| `AudioGeneration/TTSSynthesisOrchestrator.cs` | P3-2 | タイミング計算統合 |
| `AudioGeneration/AudioSynthesisCache.cs` | P3-3 | CacheEntry 拡張 |
| `Core/IPiperTTS.cs` | P4-1 | メソッド追加 |
| `Core/PiperTTS.Inference.cs` | P4-2 | 実装 |
| `Tests/Editor/AudioGeneration/StubInferenceAudioGenerator.cs` | P2-4 | durations 対応 |
