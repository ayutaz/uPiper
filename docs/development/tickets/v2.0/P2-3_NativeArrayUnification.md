# P2-3: NativeArray統一

**マイルストーン**: M4 - NativeArray + Public API (beta)
**優先度**: P1
**見積もり**: 2人日（実装1.5日 + テスト・調整0.5日）
**依存チケット**: P2-2（Prosody フラット配列化）、P3-5（AudioNormalizer 切り出し）
**後続チケット**: なし
**ブランチ名**: `feature/v2.0-P2-3-nativearray-unification`

---

## 1. タスク目的とゴール

`InferenceAudioGenerator.ExtractResults()` から `AudioClip.SetData()` に至るデータフローは、現在 `float[]` 中間バッファを複数回経由しており、不要なメモリコピーとGCアロケーションが発生している。本チケットでは `NativeArray<float>` をデータパイプライン全体の統一型として採用し、中間バッファの完全排除を目指す。

**解決する問題**:

1. **GCアロケーション**: `ExtractResults()` で `new float[audioLength]`、`SplitInferenceOrchestrator` で `new float[totalLength]` が発生し、長文連続合成時にGC Gen0コレクション頻度が増加する
2. **要素単位コピー**: `ExtractResults()` の for ループによる要素単位コピーが非効率（memcpy 相当のバルクコピーに置換可能）
3. **managed marshalling**: `AudioClip.SetData(float[])` が managed → native マーシャリングを要する。Unity 2023.1+ の `SetData(NativeArray<float>)` で回避可能

**完了状態（Definition of Done）**:

- `InferenceAudioGenerator` の推論結果出力が `NativeArray<float>` になっている
- `AudioClipBuilder.BuildAudioClip` が `NativeArray<float>` を受け付ける
- `AudioNormalizer.NormalizeInPlace` が `NativeArray<float>` オーバーロードを持つ（P3-5 で作成済みクラスに追加）
- `SplitInferenceOrchestrator` のチャンク結合が `NativeArray<float>` で行われる
- `float[]` 中間バッファ経由のコピーが完全に排除されている
- メモリ所有権（Allocator / Dispose 責務）が全箇所で明確に文書化されている
- 既存テストが全て通過する
- `dotnet format --verify-no-changes` 通過

---

## 2. 実装する内容の詳細

### Step 1: IInferenceAudioGenerator インターフェース変更

対象ファイル: `Assets/uPiper/Runtime/Core/AudioGeneration/IInferenceAudioGenerator.cs`

`GenerateAudioAsync()` の戻り値型を `Task<float[]>` から `Task<NativeArray<float>>` に変更する。public インターフェースの破壊的変更。

### Step 2: InferenceAudioGenerator（推論エンジン）の NativeArray 化

対象ファイル: `Assets/uPiper/Runtime/Core/AudioGeneration/InferenceAudioGenerator.cs`

`ExtractResults()` を NativeArray 返却に変更。`ReadbackAndClone()` 後に `ToReadOnlyNativeArray()` で ReadOnlyNativeArray を取得し、`NativeArray<float>.CopyFrom` によるバルクコピーを行う。要素単位 for ループを排除。

- `ExtractResults()` 戻り値: `float[]` → `NativeArray<float>`（Allocator.Persistent）
- `ExecuteInference()` 戻り値: 連動変更
- `GenerateAudioAsync()` 戻り値: 連動変更
- 例外時は catch ブロックで確保済み NativeArray を解放（所有権移転前のため）

### Step 3: AudioClipBuilder に NativeArray オーバーロード追加

対象ファイル: `Assets/uPiper/Runtime/Core/AudioGeneration/AudioClipBuilder.cs`

- `BuildAudioClip(NativeArray<float>, int, string)` オーバーロード追加。Unity 2023.1+ の `AudioClip.SetData(NativeArray<float>)` を使用し、managed marshalling を回避
- 旧 `BuildAudioClip(float[], ...)` に `[Obsolete]` 付与（`AudioChunk.ToAudioClip()` / `PiperTTS.cs:1340` のレガシーコードが依存しているため段階的移行）

### Step 4: AudioNormalizer に NativeArray オーバーロード追加

対象ファイル: `Assets/uPiper/Runtime/Core/AudioGeneration/AudioNormalizer.cs`（P3-5 で作成済み）

- `NormalizeInPlace(NativeArray<float>, float)` オーバーロード追加。`float[]` 版と同一アルゴリズム、NativeArray 上で直接ループ。GCアロケーションなし

### Step 5: SplitInferenceOrchestrator の NativeArray 化

対象ファイル: `Assets/uPiper/Runtime/Core/AudioGeneration/SplitInferenceOrchestrator.cs`

- `GenerateWithSilenceSplitAsync()` の戻り値型と内部ロジックを NativeArray 化
- 句ごとの `NativeArray<float>` を `List<(NativeArray<float>, int)>` で蓄積
- 結合バッファは `new NativeArray<float>(totalLength, Allocator.Persistent, NativeArrayOptions.ClearMemory)` で確保（無音区間のゼロ初期化を保証）
- `NativeArray<float>.Copy` でバルクコピー結合
- finally ブロックで句ごとの NativeArray を Dispose（所有権は結合バッファに移転済み）

### Step 6: TTSSynthesisOrchestrator のパイプライン統合

対象ファイル: `Assets/uPiper/Runtime/Core/AudioGeneration/TTSSynthesisOrchestrator.cs`

- `audioData` を `NativeArray<float>` に変更
- try-finally パターンで Dispose を保証
- `AudioClip.SetData(NativeArray<float>)` は NativeArray 内容を AudioClip 内部バッファにコピーするため、SetData 完了後に Dispose しても安全

### Step 7: StubInferenceAudioGenerator + テスト更新

- `StubInferenceAudioGenerator` を NativeArray 対応に全面改修
- 既存テストの戻り値型追従 + NativeArray Dispose 追加
- 新規テスト7件追加（セクション4参照）

### Step 8: 周辺コード更新

- `AudioChunk.Samples` の NativeArray 化はスコープ外（別チケット）
- `PiperTTS.cs:1340` のレガシーコード更新
- CLAUDE.md / アーキテクチャドキュメント更新

### メモリ所有権モデル

**Allocator 選択**:

| 箇所 | Allocator | 理由 |
|------|-----------|------|
| `ExtractResults()` 戻り値 | `Persistent` | フレーム跨ぎで使用。GenerateAudioAsync → TTSSynthesisOrchestrator → BuildAudioClip のチェーンを通過 |
| `SplitInferenceOrchestrator` 結合バッファ | `Persistent` | 同上 |
| `SplitInferenceOrchestrator` 無音一時バッファ | 不要（`ClearMemory` 採用） | 結合バッファの確保時に `NativeArrayOptions.ClearMemory` を指定し、無音区間用の一時バッファを不要にする |
| テスト用スタブデータ | `Persistent` | テストメソッドのライフタイムに依存 |

`Allocator.TempJob` は不使用: async メソッドチェーンで4フレームを超える可能性があるため不適切。

**Dispose 責務チェーン**:

```
InferenceAudioGenerator.ExtractResults()
  → NativeArray<float> を確保（Allocator.Persistent）
  → 所有権を呼び出し元に移転
  ↓
InferenceAudioGenerator.GenerateAudioAsync()
  → 所有権を呼び出し元に移転
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

**例外安全性**: 全ての NativeArray 確保箇所で try-finally パターンを適用。`NativeArray<float>` は struct のため `default` 初期化で `IsCreated == false` となり、未確保 NativeArray への Dispose を `IsCreated` チェックで回避する。

---

## 3. エージェントチームの役割と人数

**1エージェント構成**（2人日）

| 役割 | 担当内容 | 工数 |
|------|---------|------|
| 実装エージェント | Step 1-8 の全実装 + テスト作成・更新 | 2 人日 |

**理由**: `IInferenceAudioGenerator` インターフェース変更がコンパイルエラーの連鎖を起こすため、Step 1-6 は一気に実施する必要がある。分割実施は中間状態のコンパイルエラーが多すぎて非効率。

**P2-2 との同時実施推奨**: P2-2 と P2-3 は `IInferenceAudioGenerator` のシグネチャを変更する点で共通。同時実施によりインターフェース変更を1回にまとめることができる。同一エージェントが P2-2 → P2-3 の順で実施するのが最も効率的。

---

## 4. 提供範囲とテスト項目

### 4.1 提供範囲

| カテゴリ | 範囲内 | 範囲外 |
|---------|-------|-------|
| IInferenceAudioGenerator | 戻り値型を `Task<NativeArray<float>>` に変更 | メソッドシグネチャの他の変更（P2-2 管轄） |
| InferenceAudioGenerator | `ExtractResults` / `ExecuteInference` / `GenerateAudioAsync` の NativeArray 化 | Sentis API の変更 |
| AudioClipBuilder | NativeArray オーバーロード追加 + 旧版 `[Obsolete]` 付与 | `AudioChunk.ToAudioClip()` の NativeArray 化 |
| AudioNormalizer | NativeArray オーバーロード追加 | `float[]` 版の削除 |
| SplitInferenceOrchestrator | 句分割結合の NativeArray 化 | ストリーミングパスの NativeArray 化 |
| TTSSynthesisOrchestrator | パイプライン統合 + Dispose 保証 | 他のリファクタリング |
| StubInferenceAudioGenerator | NativeArray 対応改修 | テストスタブの設計変更 |

### 4.2 Unit テスト

**新規テスト**:

| テスト名 | 検証内容 |
|---------|---------|
| `ExtractResults_ReturnsNativeArrayWithCorrectLength` | Tensor から NativeArray への変換が正しいサイズであること |
| `GenerateAudioAsync_ReturnsNativeArray_CallerCanDispose` | 呼び出し元が Dispose 可能であること |
| `BuildAudioClip_NativeArray_CreatesValidClip` | NativeArray 版 BuildAudioClip が正しい AudioClip を生成すること |
| `NormalizeAudioInPlace_NativeArray_NormalizesCorrectly` | NativeArray 版正規化が正しく動作すること |
| `SplitInference_NativeArray_DisposesIntermediateArrays` | 句ごとの NativeArray が結合後に Dispose されること |
| `SynthesizeAsync_DisposesNativeArrayAfterSetData` | TTSSynthesisOrchestrator が最終的に NativeArray を Dispose すること |
| `GenerateAudioAsync_Exception_DisposesNativeArray` | 例外発生時に NativeArray がリークしないこと |

**既存テスト更新**:

| テストファイル | 変更内容 |
|-------------|---------|
| `AudioClipBuilderTests.cs` | float[] テストを NativeArray 版に並行追加。TearDown で Dispose |
| `SplitInferenceOrchestratorTests.cs` | StubGenerator の戻り値型を NativeArray に更新。result の Dispose 追加 |
| `TTSSynthesisOrchestratorTests.cs` | 同上 |
| `StubInferenceAudioGenerator.cs` | NativeArray 対応に全面改修 |
| `InferenceAudioGeneratorTests.cs` | 実機テスト: 戻り値型の変更に追従 |

### 4.3 E2E テスト

| テスト | 内容 |
|-------|------|
| CI `unity-tests.yml` 全通過 | EditMode + PlayMode テストの GREEN 確認 |
| 音声合成 E2E | 移行前後で生成音声が同一であることを波形レベルで確認 |
| 句分割パス E2E | 複数句を含むテキストの合成が正常に動作し、NativeArray のメモリリークがないこと |

---

## 5. 懸念事項とレビュー項目

### 5.1 技術的懸念

| 懸念 | 深刻度 | 対策 |
|------|--------|------|
| **Sentis API の NativeArray 取得方法** | 中 | `Tensor<float>.ToReadOnlyNativeArray()` の正確な API シグネチャは Sentis 2.5.0 に依存。API が存在しない場合は `ReadbackAndClone()` + NativeArray 版 for ループ（`audioData[i] = readableTensor[i]`）にフォールバック。GCアロケーション排除のメリットは維持される |
| **AudioChunk との整合性** | 低 | `AudioChunk.Samples` が `float[]` のまま残るが、レガシーコードの位置づけ。NativeArray 化は別チケット |
| **WebGL 対応** | 低 | `NativeArray<float>` は WebGL でも利用可能。`Allocator.Persistent` も動作する。`AudioClip.SetData(NativeArray<float>)` の WebGL 対応は Unity 2023.1+ で確認が必要 |
| **IL2CPP 対応** | 低 | `NativeArray<T>` は IL2CPP 完全対応。`unsafe` コードは使用せず、`NativeArrayOptions.ClearMemory` と `NativeArray<T>.CopyFrom` のみで実装する方針 |
| **NativeArray の Dispose 漏れ** | 中 | 全箇所で try-finally パターンを適用。テストで Dispose 検証を実施。`IsCreated` チェックで二重 Dispose を防止 |
| **P2-2 との同時変更** | 低 | `IInferenceAudioGenerator` のシグネチャ変更が重複。同一エージェントが順次実施すれば問題なし |

### 5.2 レビューチェックリスト

- [ ] `IInferenceAudioGenerator.GenerateAudioAsync` の戻り値が `Task<NativeArray<float>>` に変更されているか
- [ ] `InferenceAudioGenerator.ExtractResults` が `NativeArray<float>` を返し、for ループ要素単位コピーが排除されているか
- [ ] `Allocator.Persistent` が使用されているか（`TempJob` / `Temp` ではないこと）
- [ ] `AudioClipBuilder.BuildAudioClip(NativeArray<float>, ...)` オーバーロードが追加されているか
- [ ] 旧 `float[]` 版に `[Obsolete]` が付与されているか
- [ ] `AudioNormalizer.NormalizeInPlace(NativeArray<float>, float)` が追加されているか
- [ ] `SplitInferenceOrchestrator` で句ごとの NativeArray が finally ブロックで Dispose されているか
- [ ] `SplitInferenceOrchestrator` の結合バッファが `NativeArrayOptions.ClearMemory` で確保されているか
- [ ] `TTSSynthesisOrchestrator` で try-finally パターンによる Dispose が保証されているか
- [ ] テストの TearDown で NativeArray が Dispose されているか（二重 Dispose に注意）
- [ ] `using Unity.Collections;` が必要な全ファイルに追加されているか
- [ ] `dotnet format --verify-no-changes` が通過するか

---

## 6. 一から作り直すとしたら

### 6.1 メモリ管理戦略全体の設計

現行設計は「NativeArray を確保し、Dispose 責務を呼び出し元に移転する」パターンに依存している。これは C# の `IDisposable` パターンの延長だが、NativeArray の所有権移転が暗黙的（戻り値として返すだけ）であり、コンパイラが Dispose 漏れを検出できない。

一から設計するなら、以下の戦略が考えられる:

**戦略A: NativeArray ラッパー + IDisposable（推奨案に近い）**

```csharp
public readonly struct OwnedAudioBuffer : IDisposable
{
    public NativeArray<float> Data { get; }
    public int SampleRate { get; }

    public OwnedAudioBuffer(int length, Allocator allocator)
    {
        Data = new NativeArray<float>(length, allocator, NativeArrayOptions.ClearMemory);
        SampleRate = 0;
    }

    public void Dispose()
    {
        if (Data.IsCreated) Data.Dispose();
    }
}
```

メリット: `using` ステートメントで自動 Dispose が可能。所有権の明示化。
デメリット: struct の `IDisposable` は `using var` パターンでしか安全でなく、`async` メソッドとの組み合わせで `using` スコープが複雑化する。現行の try-finally パターンと実質的に同等。

**戦略B: プール方式**

```csharp
public sealed class AudioBufferPool : IDisposable
{
    private readonly Stack<NativeArray<float>> _pool;

    public NativeArray<float> Rent(int minLength) { ... }
    public void Return(NativeArray<float> buffer) { ... }
}
```

メリット: NativeArray の確保/解放コストを削減。連続合成時のパフォーマンス向上。
デメリット: プールサイズの管理が複雑。バッファ長が音声ごとに異なるため、適切なプールサイズの選定が困難。過剰確保によるメモリ浪費のリスク。

**採用判断**: P2-3 では戦略Aに近い try-finally パターンを採用する。NativeArray ラッパー型の導入は、Dispose 漏れが実際の問題として顕在化した場合に v2.1 で検討する。プール方式は連続合成のベンチマークで確保/解放がボトルネックになった場合にのみ検討する（現時点では推論時間が支配的）。

### 6.2 Sentis API との最適接続

現行設計では `ReadbackAndClone()` → `NativeArray<float>.CopyFrom` の2段階コピーが発生する。理想的には Sentis の内部 NativeArray を直接取得し、所有権を移転できれば1回のコピーすら不要になる。しかし Sentis の公開 API はこれをサポートしていない。

将来 Sentis が `Tensor<float>.ToNativeArray(Allocator)` のような所有権移転 API を提供した場合、`ExtractResults()` の実装を差し替えるだけで追加のコピー排除が可能。現行の NativeArray パイプラインはこの将来最適化への準備としても機能する。

### 6.3 パフォーマンス改善の現実的評価

コピー回数自体は Before/After で同じ2回だが、質が改善される:
- 要素単位 for ループ → memcpy 相当のバルクコピー（~10x 高速化）
- managed marshalling → native 直接コピー（~3x 高速化）

ただし実際の推論時間（50-200ms）に比べてコピー時間は1%未満であり、**レイテンシ改善は体感できないレベル**。主なメリットは GC 圧力の排除（直接パスで 22KB/秒、句分割パスで 88KB/秒の managed allocation 排除）。長文連続合成（ナレーション等）では GC Gen0 コレクション頻度が大幅に低減される。

### 6.4 現設計の正直な弱点

1. **Dispose 責務の暗黙性**: 戻り値の `NativeArray<float>` を Dispose する責務が API 契約として明文化されていない。XML doc コメントで `<remarks>Caller must dispose the returned NativeArray</remarks>` を記載するが、コンパイラ強制ではない
2. **StubInferenceAudioGenerator の複雑化**: テストスタブが NativeArray を返すようになると、TearDown での Dispose 管理が必要になり、テストコードが複雑化する
3. **`AudioChunk` の未対応**: ストリーミングパスの `AudioChunk.Samples` が `float[]` のまま残り、NativeArray パイプラインの一貫性が損なわれる。ただし `AudioChunk` はレガシーコードの位置づけであり、v2.0 のスコープ外

---

## 7. 後続タスクへの連絡事項

### M5（ドキュメント更新）への引き継ぎ

P2-3 完了後、以下のドキュメントを更新する:
- CLAUDE.md: データフロー図の `float[]` → `NativeArray<float>` 更新
- ARCHITECTURE.md: AudioGeneration セクションの更新
- Unity 最低要件の明記: Unity 2023.1+（NativeArray API に起因）

### AudioChunk NativeArray 化（将来チケット）

`AudioChunk.Samples` の NativeArray 化は P2-3 のスコープ外。ストリーミングパスの利用状況を評価し、v2.1 以降で検討する。

### ファイル変更一覧

| ファイル | 変更種別 |
|---------|---------|
| `AudioGeneration/IInferenceAudioGenerator.cs` | 修正（戻り値型変更） |
| `AudioGeneration/InferenceAudioGenerator.cs` | 修正（ExtractResults / ExecuteInference / GenerateAudioAsync の NativeArray 化） |
| `AudioGeneration/AudioClipBuilder.cs` | 修正（NativeArray オーバーロード追加 + 旧版 `[Obsolete]`） |
| `AudioGeneration/AudioNormalizer.cs` | 修正（NativeArray オーバーロード追加） |
| `AudioGeneration/SplitInferenceOrchestrator.cs` | 修正（句分割結合の NativeArray 化） |
| `AudioGeneration/TTSSynthesisOrchestrator.cs` | 修正（パイプライン統合 + Dispose 保証） |
| `Tests/StubInferenceAudioGenerator.cs` | 修正（NativeArray 対応に全面改修） |
| `Tests/AudioClipBuilderTests.cs` | 修正（NativeArray テスト追加） |
| `Tests/SplitInferenceOrchestratorTests.cs` | 修正（戻り値型追従 + Dispose 追加） |
| `Tests/TTSSynthesisOrchestratorTests.cs` | 修正（戻り値型追従 + Dispose 追加） |
