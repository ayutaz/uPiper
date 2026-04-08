# P3-5: AudioNormalizer 切り出し

**マイルストーン**: M3 - Data Model + Config
**優先度**: P2
**見積もり**: 0.5 人日
**依存チケット**: Phase 1 完了（M2 ゲート通過）
**後続チケット**: P3-4（BackendSelector 切り出し）
**ブランチ名**: `feature/v2.0-P3-5-audio-normalizer`

---

## 1. タスク目的とゴール

`AudioClipBuilder` から正規化ロジック（`NormalizeAudio` / `NormalizeAudioInPlace`）を `AudioNormalizer` static クラスに切り出す。

**解決する問題**:

1. **責務混在**: `AudioClipBuilder`（133行）が「AudioClip 構築」と「音声データ正規化」の2つの責務を持っている。`NormalizeAudio` / `NormalizeAudioInPlace` は pure な数値演算であり、`AudioClip.Create` / `SetData` に依存する `BuildAudioClip` とは本質的に無関係
2. **NativeArray 拡張の前準備**: P2-3（NativeArray 統一）で `NativeArray<float>` 版の正規化オーバーロードを追加する際、`AudioClipBuilder` に追加するのは責務の肥大化。独立した `AudioNormalizer` クラスに追加する方が自然
3. **テストの分離**: 正規化ロジックのテストが `AudioClipBuilderTests` に混在しており、AudioClip 関連テストとの責務境界が不明瞭

**完了状態（Definition of Done）**:

- `AudioNormalizer` static クラスが `Assets/uPiper/Runtime/Core/AudioGeneration/AudioNormalizer.cs` に存在する
- `AudioNormalizer.NormalizeInPlace(float[], float)` と `AudioNormalizer.Normalize(float[], float)` が実装されている
- `AudioClipBuilder` から `NormalizeAudio` / `NormalizeAudioInPlace` メソッドが削除されている
- 呼び出し元2箇所（`TTSSynthesisOrchestrator.cs` L99、`InferenceEngineDemo.cs` L849）が `AudioNormalizer` に更新されている
- `AudioNormalizerTests.cs` に9テストケースが追加・通過している
- `AudioClipBuilderTests.cs` から正規化テストが削除され、`BuildAudioClip` テストのみ残存している
- 全テスト通過（EditMode + PlayMode）
- `dotnet format --verify-no-changes` 通過

---

## 2. 実装する内容の詳細

### Step 1: AudioNormalizer static クラス新規作成

対象ファイル（新規作成）: `Assets/uPiper/Runtime/Core/AudioGeneration/AudioNormalizer.cs`

`AudioClipBuilder` の `NormalizeAudio` / `NormalizeAudioInPlace` のロジックをそのまま移植する。アルゴリズム変更なし。

```csharp
namespace uPiper.Core.AudioGeneration
{
    public static class AudioNormalizer
    {
        /// <summary>
        /// 音声データをin-placeで正規化する。
        /// </summary>
        public static void NormalizeInPlace(float[] audioData, float targetPeak = 0.95f)
        {
            // 1. null/empty チェック → 早期 return
            // 2. targetPeak を Mathf.Clamp01 でクランプ
            // 3. 最大振幅 maxAmplitude を線形スキャンで検出
            // 4. maxAmplitude <= 0 または ≈ targetPeak なら早期 return
            // 5. scale = targetPeak / maxAmplitude を計算
            // 6. 全要素に scale を乗算（in-place）
            // 7. ログ出力
        }

        /// <summary>
        /// 音声データを正規化し、新しい配列で返す。元のデータは変更しない。
        /// </summary>
        public static float[] Normalize(float[] audioData, float targetPeak = 0.95f)
        {
            // 1-4: NormalizeInPlace と同一の前処理
            // 5. scale を計算
            // 6. 新規配列 normalizedData を作成し、全要素に scale を乗算
            // 7. ログ出力後、新規配列を返却
        }
    }
}
```

**Mathf 依存について**: 現行コードは `Mathf.Clamp01`、`Mathf.Abs`、`Mathf.Approximately` を使用している。これらは `System.Math` / `System.MathF` で置換可能（`Math.Abs`、`Math.Clamp(value, 0f, 1f)`、独自近似比較）だが、Unity プロジェクト内で動作するため `Mathf` のままでも問題ない。設計ドキュメントの方針に従い、`Mathf` をそのまま使用する。

### Step 2: AudioClipBuilder からの正規化メソッド削除

対象ファイル: `Assets/uPiper/Runtime/Core/AudioGeneration/AudioClipBuilder.cs`

1. `NormalizeAudio(float[], float)` メソッド（L60-L96、37行）を **削除**
2. `NormalizeAudioInPlace(float[], float)` メソッド（L103-L130、28行）を **削除**
3. `BuildAudioClip(float[], int, string)` メソッドのみ残存

```csharp
// Before:
public class AudioClipBuilder
{
    public AudioClip BuildAudioClip(float[] audioData, int sampleRate, string clipName = null) { ... }
    public float[] NormalizeAudio(float[] audioData, float targetPeak = 0.95f) { ... }
    public void NormalizeAudioInPlace(float[] audioData, float targetPeak = 0.95f) { ... }
}

// After:
public class AudioClipBuilder
{
    public AudioClip BuildAudioClip(float[] audioData, int sampleRate, string clipName = null) { ... }
    // 正規化メソッドは AudioNormalizer に移動。
}
```

AudioClipBuilder の行数は133行 → 約68行に減少。

### Step 3: 呼び出し元2箇所の更新

**TTSSynthesisOrchestrator.cs（L99）**:

```csharp
// Before:
_audioClipBuilder.NormalizeAudioInPlace(audioData, 0.95f);

// After:
AudioNormalizer.NormalizeInPlace(audioData, 0.95f);
```

`_audioClipBuilder` はインスタンスメソッド呼び出しだったが、`AudioNormalizer` は static メソッド呼び出しに変わる。`_audioClipBuilder` フィールドの他の使用箇所（`BuildAudioClip`）は変更なし。

**InferenceEngineDemo.cs（L849）**:

```csharp
// Before:
_audioBuilder.NormalizeAudioInPlace(audioData, 0.95f);

// After:
AudioNormalizer.NormalizeInPlace(audioData, 0.95f);
```

### Step 4: テスト移行

**既存テストの移動**: `AudioClipBuilderTests.cs` の正規化テスト（L61-L81 `NormalizeAudio_ValidData_NormalizesToTarget` 等）を `AudioNormalizerTests.cs` に移動し、`_builder.NormalizeAudio(...)` → `AudioNormalizer.Normalize(...)` に変更する。

**AudioClipBuilderTests.cs の修正**: 正規化関連テストを削除し、`BuildAudioClip` 関連テストのみ残す。

---

## 3. エージェントチームの役割と人数

**1エージェント構成**（0.5人日）

| 役割 | 担当内容 | 工数 |
|------|---------|------|
| 実装エージェント | Step 1-4 の全実装 + テスト作成・移行 | 0.5 人日 |

**理由**: 新規ファイル1つ（`AudioNormalizer.cs`）+ 変更ファイル3つ（`AudioClipBuilder.cs`、`TTSSynthesisOrchestrator.cs`、`InferenceEngineDemo.cs`）+ テスト2つ（新規 `AudioNormalizerTests.cs`、修正 `AudioClipBuilderTests.cs`）。変更が機械的なメソッド移動であり、ロジック変更を伴わない。

**P3-4 との実施順序**: P3-5 を先に実施してパターン（static クラスへのロジック切り出し）を確立し、P3-4 で同パターンを適用する。マイルストーンの推奨順序に準拠。

---

## 4. 提供範囲とテスト項目

### 4.1 提供範囲

| カテゴリ | 範囲内 | 範囲外 |
|---------|-------|-------|
| AudioNormalizer | static クラス新規作成、NormalizeInPlace + Normalize | `NativeArray<float>` オーバーロード（P2-3 で追加） |
| AudioClipBuilder | 正規化メソッド削除 | BuildAudioClip の変更 |
| TTSSynthesisOrchestrator | 呼び出し変更（L99、1箇所） | 他のリファクタリング |
| InferenceEngineDemo | 呼び出し変更（L849、1箇所） | 他のリファクタリング |
| テスト | AudioNormalizerTests 新規9ケース + AudioClipBuilderTests 修正 | AudioClipBuilder E2E テスト |

### 4.2 Unit テスト

**テストファイル（新規作成）**: `Assets/uPiper/Tests/Runtime/AudioGeneration/AudioNormalizerTests.cs`

| テスト名 | 入力 | 期待結果 |
|---------|------|---------|
| `NormalizeInPlace_ValidData_NormalizesToTarget` | `{0.5f, -0.5f, 0.25f}`, target=0.95 | maxAbs == 0.95 |
| `NormalizeInPlace_SilentAudio_NoChange` | `{0f, 0f, 0f}` | 変更なし |
| `NormalizeInPlace_AlreadyNormalized_NoChange` | `{0.95f, -0.95f}`, target=0.95 | 変更なし |
| `NormalizeInPlace_NullArray_NoException` | `null` | 例外なし |
| `NormalizeInPlace_EmptyArray_NoException` | `{}` | 例外なし |
| `NormalizeInPlace_TargetPeakClamped_ClampedTo01` | target=1.5 | Clamp01 適用（1.0 として扱われる） |
| `Normalize_ValidData_ReturnsNewArray` | `{0.5f, -0.5f}`, target=0.95 | 新規配列が返却され、元の配列は変更なし |
| `Normalize_ValidData_NormalizesToTarget` | `{0.5f, -0.5f}`, target=0.95 | maxAbs == 0.95 |
| `Normalize_SilentAudio_ReturnsSameArray` | `{0f, 0f}` | 同一参照を返す（新規配列を作らない） |

**既存テストの移行**:

`AudioClipBuilderTests.NormalizeAudio_ValidData_NormalizesToTarget`（L61-L81）を:
1. `AudioNormalizerTests.Normalize_ValidData_NormalizesToTarget` に移動
2. `_builder.NormalizeAudio(...)` → `AudioNormalizer.Normalize(...)` に変更
3. `AudioClipBuilderTests` からは正規化テストを削除（`BuildAudioClip` テストのみ残す）

### 4.3 E2E テスト

| テスト | 内容 |
|-------|------|
| CI `unity-tests.yml` 全通過 | EditMode + PlayMode テストの GREEN 確認 |
| `InferenceEngineDemo` 手動実行 | 音声正規化が正常に動作し、音量レベルが変わっていないことを聴覚確認 |

---

## 5. 懸念事項とレビュー項目

### 5.1 技術的懸念

| 懸念 | 深刻度 | 対策 |
|------|--------|------|
| **Mathf.Approximately の精度** | 低 | AudioNormalizer は `Mathf.Approximately` をそのまま使用。精度要件は元のコードと同一。差異が発生しないことをテストで検証 |
| **NormalizeAudio（非in-place版）の使用状況** | 低 | 設計ドキュメントの分析によると、`TTSSynthesisOrchestrator` と `InferenceEngineDemo` は in-place 版のみ使用。非in-place版はテスト（`AudioClipBuilderTests` L68）でのみ使用。public API として残存しているが、P3-5 では `AudioNormalizer.Normalize` として移植し、互換性を維持する |
| **InferenceEngineDemo の変更** | 低 | Demo コードのため破壊リスクは低い。手動動作確認で十分 |
| **PiperLogger への依存** | 低 | 切り出し先でも `PiperLogger.LogDebug` を使用。テスト時はログ出力を無視する設計 |

### 5.2 レビューチェックリスト

- [ ] `AudioNormalizer.cs` が `Assets/uPiper/Runtime/Core/AudioGeneration/` に配置されているか
- [ ] `AudioNormalizer` が `static class` として定義されているか
- [ ] `NormalizeInPlace` と `Normalize` の2メソッドが実装されているか
- [ ] 両メソッドのアルゴリズムが元の `AudioClipBuilder` のロジックと完全に等価か
- [ ] null/empty チェック、Clamp01、Approximately による早期 return が正しく移植されているか
- [ ] `AudioClipBuilder` から `NormalizeAudio` / `NormalizeAudioInPlace` が完全に削除されているか
- [ ] `TTSSynthesisOrchestrator.cs` L99 の呼び出しが `AudioNormalizer.NormalizeInPlace` に変更されているか
- [ ] `InferenceEngineDemo.cs` L849 の呼び出しが `AudioNormalizer.NormalizeInPlace` に変更されているか
- [ ] `AudioNormalizerTests.cs` に9テストケースが存在するか
- [ ] `AudioClipBuilderTests.cs` から正規化テストが削除されているか
- [ ] `AudioClipBuilderTests.cs` に `BuildAudioClip` テストが引き続き存在し通過するか
- [ ] `dotnet format --verify-no-changes` が通過するか

---

## 6. 一から作り直すとしたら

### 6.1 オーディオ処理パイプラインの設計

現行の AudioClipBuilder は「正規化 + AudioClip 構築」を1クラスに詰め込んでいるが、これは TTS パイプラインの「音声後処理」段階が未分化であることを示している。一から設計するなら、以下のパイプライン構造がより拡張性が高い:

```csharp
// パイプライン段階の明確化
public interface IAudioPostProcessor
{
    void Process(float[] audioData);
}

public class NormalizationProcessor : IAudioPostProcessor
{
    private readonly float _targetPeak;
    public NormalizationProcessor(float targetPeak = 0.95f) => _targetPeak = targetPeak;
    public void Process(float[] audioData) { /* 正規化ロジック */ }
}

public class AudioPipeline
{
    private readonly List<IAudioPostProcessor> _processors;

    public AudioPipeline(params IAudioPostProcessor[] processors)
        => _processors = new List<IAudioPostProcessor>(processors);

    public void Execute(float[] audioData)
    {
        foreach (var processor in _processors)
            processor.Process(audioData);
    }
}
```

この設計のメリット:
- 正規化以外の後処理（ノイズ除去、フェードイン/アウト、リサンプリング等）を追加する際に `IAudioPostProcessor` 実装を追加するだけで済む
- パイプラインの順序と構成をランタイムで変更可能
- 各プロセッサが独立してテスト可能

デメリット:
- 現時点で後処理は正規化のみ。パイプライン構造は過設計
- `IAudioPostProcessor` インターフェースと `AudioPipeline` クラスの追加は、P3-5 の「static クラスへの単純切り出し」の10倍以上のコード量
- `float[]` を引き回すパイプラインは P2-3（NativeArray 統一）と整合しにくい（`NativeArray<float>` 版の `IAudioPostProcessor` が別途必要になる）

**採用判断**: P3-5 では static クラスへのシンプルな切り出しが適切。パイプライン設計は将来的に後処理が2つ以上になった場合に検討する。現状は YAGNI。

### 6.2 P2-3（NativeArray 統一）との関係

P2-3 で `NativeArray<float>` パイプラインが導入される際に、`AudioNormalizer` に以下のオーバーロードを追加する設計となっている:

```csharp
/// NativeArray版 in-place 正規化。P2-3 で追加。
public static void NormalizeInPlace(NativeArray<float> audioData, float targetPeak = 0.95f)
{
    // NativeArray 上で直接ループ。GCアロケーションなし。
}
```

P3-5 の時点では `float[]` 版のみ実装し、P2-3 実装時に NativeArray 版を追加する。P3-5 を先に完了しておくことで、P2-3 では NativeArray オーバーロード追加のみで済む。これが P3-5 を P3-4 より先に実施する理由の一つでもある（P3-4 の BackendSelector は P2-3 との接点がない）。

もし P3-5 と P2-3 を統合して一度に実施するなら、`float[]` 版と `NativeArray<float>` 版を同時に実装できるが、M3（P3-5）と M4（P2-3）でマイルストーンが分かれている以上、段階的実装が適切。

### 6.3 static class vs インスタンスクラスの選択

`AudioNormalizer` を static class とする設計は、状態を持たない純粋関数群であるという性質に基づく。一方、インスタンスクラスとして設計し、`targetPeak` をコンストラクタパラメータにする案もある:

```csharp
public class AudioNormalizer
{
    private readonly float _targetPeak;

    public AudioNormalizer(float targetPeak = 0.95f)
        => _targetPeak = Mathf.Clamp01(targetPeak);

    public void NormalizeInPlace(float[] audioData) { /* _targetPeak を使用 */ }
}
```

メリット: `targetPeak` を毎回指定する必要がなくなり、DI で設定を注入可能。
デメリット: 現行の呼び出し箇所は全て `targetPeak = 0.95f` 固定であり、設定の動的変更需要がない。インスタンス生成コストと DI 配線の手間が無駄。

`AudioClipBuilder` がインスタンスクラスだった理由は `BuildAudioClip` が `AudioClip.Create` のステート（AudioClip 名の連番管理等）を持つ可能性があったためだが、実際には状態を持っていない。正規化ロジックを分離した後の `AudioClipBuilder` もステートレスであり、将来的に static class 化を検討してもよいが、それはスコープ外。

### 6.4 NormalizeAudio（非in-place版）の要否

設計ドキュメントの分析によると、非in-place版の `NormalizeAudio` はランタイムコードでは使用されておらず、テスト（`AudioClipBuilderTests` L68）でのみ使用されている。一から設計するなら非in-place版は不要である可能性が高い。

しかし:
1. public API として公開されている以上、v2.0 リリースまでは互換性を維持する方が安全
2. 非in-place版はイミュータブルパターンを好むコードから利用される可能性がある
3. P2-3 で NativeArray 版を追加する際、NativeArray は `.ToArray()` コピーが必要なため非in-place版のセマンティクスが有用になる場合がある

P3-5 では `AudioNormalizer.Normalize` として移植し、API 互換性を維持する。v2.0 GA で非in-place版を `[Obsolete]` にするかどうかは、利用状況を見て判断する。

### 6.5 現設計の正直な弱点

1. **AudioClipBuilder のインスタンスメソッドから static メソッドへの変更**: 呼び出し元（`TTSSynthesisOrchestrator`、`InferenceEngineDemo`）で `_audioClipBuilder.NormalizeAudioInPlace(...)` → `AudioNormalizer.NormalizeInPlace(...)` に変更するため、DI で正規化ロジックを差し替える能力を失う。ただし、正規化ロジックを差し替える需要は現時点でなく、テストでも実際の正規化結果を検証する方が有用。

2. **AudioClipBuilder のクラス存在意義の希薄化**: 正規化メソッドを除くと `AudioClipBuilder` は `BuildAudioClip` 1メソッドのみのクラスになる。`AudioClipBuilder` 自体を static class にするか、`TTSSynthesisOrchestrator` に inline 化する方がシンプルだが、P3-5 のスコープを超える。

3. **ログ出力の static 依存**: `AudioNormalizer` は `PiperLogger.LogDebug` を直接呼び出す。テスト時にログ出力を制御したい場合（例: ログが期待通りに出力されることを検証したい場合）に不便だが、正規化ロジックのテストでログ内容のアサートは不要と判断。

### 6.6 Phase 2+3 統合設計考察

#### P3-4（BackendSelector）との共通リファクタリング原則

P3-5 と P3-4 は同一の「ロジック切り出し」パターン（Cut-Out Pattern）に従う。P3-5 を先に実施してパターンを確立し、P3-4 で同パターンを適用するという実施順序は、以下のパターンの確立を意図している:

**Cut-Out Pattern の定義**:
1. 新規 static クラスを `AudioGeneration/` ディレクトリに作成
2. 元クラスから対象メソッドを完全削除（互換ファサードなし）
3. 呼び出し元をインスタンスメソッド → static メソッドに変更
4. テストを新規クラスに移動/新規作成
5. ロジック変更なし（振る舞い不変の保証）

P3-5 は最もシンプルな適用例（状態なし純粋関数の移動のみ）であり、P3-4 はこのパターンに `PlatformInfo` による依存カプセル化を追加した発展形である。P3-5 の成果物が P3-4 の「テンプレート」となる。

#### Group A（P2-1/P2-2）・Group B（P3-1/P3-3/P3-2）とのファイル競合

P3-5 が変更するファイルと他グループの競合:

| P3-5 変更ファイル | Group A 競合 | Group B 競合 |
|------------------|-------------|-------------|
| `AudioNormalizer.cs`（新規） | なし | なし |
| `AudioClipBuilder.cs` | なし | なし |
| `TTSSynthesisOrchestrator.cs` | P2-1（シグネチャ）、P2-2（Prosody 変数） | P3-1（プロパティアクセス）、P3-2（型変更） |
| `InferenceEngineDemo.cs` | P2-1（JSON デシリアライズ） | なし |
| `AudioNormalizerTests.cs`（新規） | なし | なし |
| `AudioClipBuilderTests.cs` | なし | なし |

**`TTSSynthesisOrchestrator.cs` の変更箇所分析**: P3-5 は L99 の `_audioClipBuilder.NormalizeAudioInPlace(audioData, 0.95f)` を `AudioNormalizer.NormalizeInPlace(audioData, 0.95f)` に変更する 1 行のみ。他グループの変更箇所（P2-1 のシグネチャパススルー、P2-2 の Prosody 変数名、P3-1 の Silence プロパティアクセス）とは完全に独立した行であり、コンフリクトなし。

**`InferenceEngineDemo.cs` の変更箇所分析**: P3-5 は L849 の正規化呼び出し変更のみ。P2-1 は同ファイルの L986, L1044-1046（JSON デシリアライズ）を変更。変更箇所が約 200 行離れており、コンフリクトリスクなし。

#### Group C 全体の独立性

Group C（P3-5 + P3-4）は Group A、Group B と変更ファイルが部分的に重複するが、変更箇所が行レベルで非重複であることが確認できた。Group C は M3 の 3 並行グループの中で最も独立性が高く、最も早く完了する見込み（合計 1 人日）。

**推奨**: Group C を最初にマージし、Group A・Group B のリベースベースとして確定させる。これにより、Group A・Group B は Group C の変更が確定した状態で作業を継続でき、最終マージ時のコンフリクト解決が最小化される。

---

## 7. 後続タスクへの連絡事項

### P3-4（BackendSelector 切り出し）への引き継ぎ

P3-5 と P3-4 は同一パターン（既存クラスから static クラスへのロジック切り出し）に従う。P3-5 で確立したパターン:
1. 新規 static クラス作成（`AudioGeneration/` ディレクトリ内）
2. 元クラスからメソッド削除
3. 呼び出し元の変更
4. テストの新規作成・移行

P3-4 でも同じ手順を適用する。ただし P3-4 は `PlatformInfo` readonly struct の追加設計を含むため、P3-5 より若干複雑。

### P2-3（NativeArray 統一）への引き継ぎ

P3-5 で作成した `AudioNormalizer` static クラスに、P2-3 で `NativeArray<float>` オーバーロードを追加する:

```csharp
public static void NormalizeInPlace(NativeArray<float> audioData, float targetPeak = 0.95f)
{
    // NativeArray 上で直接ループ。GCアロケーションなし。
}
```

P3-5 の `float[]` 版と P2-3 の `NativeArray<float>` 版は同一アルゴリズムであり、ループ内の要素アクセスが `audioData[i]` で共通するため、コード重複が発生する。共通の内部メソッドを抽出するか、P2-3 実装時に判断する。

### ファイル変更一覧

| ファイル | 変更種別 |
|---------|---------|
| `AudioGeneration/AudioNormalizer.cs` | **新規作成** |
| `AudioGeneration/AudioClipBuilder.cs` | 修正（正規化メソッド2つ削除） |
| `AudioGeneration/TTSSynthesisOrchestrator.cs` | 修正（呼び出し変更、1箇所） |
| `Demo/InferenceEngineDemo.cs` | 修正（呼び出し変更、1箇所） |
| `Tests/Runtime/AudioGeneration/AudioNormalizerTests.cs` | **新規作成**（9テストケース） |
| `Tests/Runtime/AudioGeneration/AudioClipBuilderTests.cs` | 修正（正規化テスト削除） |
