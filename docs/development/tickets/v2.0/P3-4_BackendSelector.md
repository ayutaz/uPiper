# P3-4: BackendSelector 切り出し

**マイルストーン**: M3 - Data Model + Config
**優先度**: P2
**見積もり**: 0.5 人日
**依存チケット**: P3-5（マイルストーンではP3-5→P3-4の順）
**後続チケット**: なし
**ブランチ名**: `feature/v2.0-P3-4-backend-selector`

---

## 1. タスク目的とゴール

`InferenceAudioGenerator.DetermineBackendType`（約90行の private メソッド）を `BackendSelector` static クラスに切り出し、`PlatformInfo` readonly struct でプラットフォーム依存をカプセル化する。

**解決する問題**:

1. **テスト不能**: `DetermineBackendType` は private メソッドかつ `SystemInfo` / `#if` プリプロセッサに依存するため、単体テストできない
2. **責務混在**: プラットフォーム判定ロジック（約90行）が推論エンジン管理クラス `InferenceAudioGenerator`（L591-L680）に埋め込まれている
3. **条件分岐の複雑さ**: 6つの分岐層（Metal/GPUCompute/CPU/GPUPixel/Auto/フォールバック）、3つのプラットフォーム区分（WebGL/Mobile/Desktop）、プリプロセッサ条件が入り組んでいる

**完了状態（Definition of Done）**:

- `BackendSelector` static クラスが `Assets/uPiper/Runtime/Core/AudioGeneration/BackendSelector.cs` に存在する
- `PlatformInfo` readonly struct が同ファイル内に定義されている
- `InferenceAudioGenerator.DetermineBackendType` メソッド（L591-L680）が削除されている
- `InferenceAudioGenerator.InitializeAsync` の呼び出し元（L132）が `BackendSelector.Determine()` に置き換えられている
- `BackendSelector.Determine()` 自体がプリプロセッサフリー（`#if` なし）であり、`PlatformInfo` のフィールドで全分岐をカバーしている
- 14テストケースが `BackendSelectorTests.cs` に追加・通過している
- 全テスト通過（EditMode + PlayMode）
- `dotnet format --verify-no-changes` 通過

---

## 2. 実装する内容の詳細

### Step 1: PlatformInfo readonly struct 定義

対象ファイル（新規作成）: `Assets/uPiper/Runtime/Core/AudioGeneration/BackendSelector.cs`

プラットフォーム依存情報をカプセル化し、テスト時に任意の値を注入可能にする。

```csharp
namespace uPiper.Core.AudioGeneration
{
    public readonly struct PlatformInfo
    {
        /// <summary>現在のグラフィクスデバイスタイプ</summary>
        public GraphicsDeviceType GraphicsDeviceType { get; init; }

        /// <summary>Compute Shader サポート有無</summary>
        public bool SupportsComputeShaders { get; init; }

        /// <summary>GPU メモリサイズ (MB)</summary>
        public int GraphicsMemorySize { get; init; }

        /// <summary>WebGPU 上で動作しているか（WebGL プラットフォームのみ有効）</summary>
        public bool IsWebGPU { get; init; }

        /// <summary>WebGL プラットフォームか</summary>
        public bool IsWebGL { get; init; }

        /// <summary>モバイルプラットフォームか (iOS/Android)</summary>
        public bool IsMobile { get; init; }

        public static PlatformInfo FromCurrentEnvironment()
        {
            return new PlatformInfo
            {
                GraphicsDeviceType = SystemInfo.graphicsDeviceType,
                SupportsComputeShaders = SystemInfo.supportsComputeShaders,
                GraphicsMemorySize = SystemInfo.graphicsMemorySize,
#if UNITY_WEBGL
                IsWebGL = true,
                IsWebGPU = Platform.PlatformHelper.IsWebGPU,
#else
                IsWebGL = false,
                IsWebGPU = false,
#endif
#if UNITY_IOS || UNITY_ANDROID
                IsMobile = true,
#else
                IsMobile = false,
#endif
            };
        }
    }
}
```

**設計ポイント**:
- `FromCurrentEnvironment()` がプリプロセッサ条件を集約。`BackendSelector.Determine()` 自体は `PlatformInfo` のフィールドのみで分岐するため、プリプロセッサフリーとなりテスト容易性が確保される
- `readonly struct` + `init` プロパティにより、テスト時に任意のプラットフォーム構成を注入可能

### Step 2: BackendSelector static クラス実装

同ファイル内: `Assets/uPiper/Runtime/Core/AudioGeneration/BackendSelector.cs`

`DetermineBackendType` の全6分岐構造をそのまま移植する。ロジック変更なし。

```csharp
public static class BackendSelector
{
    public static BackendType Determine(
        InferenceBackend requested,
        PlatformInfo platform,
        int gpuMemoryThresholdMB = 512)
    {
        // [1] Metal チェック
        // [2] GPUCompute 要求時
        // [3] CPU 明示指定
        // [4] GPUPixel 明示指定
        // [5] Auto 選択 → DetermineAutoBackend()
        // [6] フォールバック → CPU
    }

    private static BackendType DetermineAutoBackend(
        PlatformInfo platform, int gpuMemoryThresholdMB)
    {
        // WebGL: WebGPU → GPUCompute, WebGL2 → GPUPixel
        // Mobile: ComputeShader対応 → GPUCompute, 非対応 → CPU
        // Desktop: Metal → CPU, VRAM十分+CS対応 → GPUPixel, その他 → CPU
    }
}
```

分岐条件の完全な対応関係:

| 分岐 | 現状の参照 | BackendSelector での参照 |
|------|-----------|------------------------|
| [1] Metal チェック | `SystemInfo.graphicsDeviceType == Metal` | `platform.GraphicsDeviceType == Metal` |
| [2a] WebGPU | `#if UNITY_WEBGL` + `PlatformHelper.IsWebGPU` | `platform.IsWebGL && platform.IsWebGPU` |
| [5a] WebGL Auto | `#if UNITY_WEBGL` | `platform.IsWebGL` |
| [5b] Mobile Auto | `#if UNITY_IOS \|\| UNITY_ANDROID` | `platform.IsMobile` |
| [5c] Desktop VRAM | `SystemInfo.supportsComputeShaders` + `graphicsMemorySize` | `platform.SupportsComputeShaders` + `platform.GraphicsMemorySize` |
| VRAM閾値 | `config.GPUSettings.MaxMemoryMB` | `gpuMemoryThresholdMB` パラメータ |

### Step 3: InferenceAudioGenerator の変更

対象ファイル: `Assets/uPiper/Runtime/Core/AudioGeneration/InferenceAudioGenerator.cs`

1. `DetermineBackendType` メソッド（L591-L680、約90行）を **削除**
2. 呼び出し元（L132）を以下に変更:

```csharp
// Before (L132):
_actualBackendType = DetermineBackendType(_piperConfig);

// After:
var platformInfo = PlatformInfo.FromCurrentEnvironment();
_actualBackendType = BackendSelector.Determine(
    _piperConfig.Backend,
    platformInfo,
    _piperConfig.GPUSettings.MaxMemoryMB);
```

変更点:
- `DetermineBackendType` private メソッドの完全削除
- 呼び出し元1箇所の置換
- `InferenceAudioGenerator` の行数が約90行減少
- `PiperConfig` からは `Backend` enum と `GPUSettings.MaxMemoryMB` のみ抽出して渡す（BackendSelector は PiperConfig に直接依存しない）

### Step 4: プリプロセッサ条件の移動確認

| 項目 | 現状の位置 | 変更後の位置 |
|------|-----------|------------|
| `#if UNITY_WEBGL`（WebGPU判定） | `DetermineBackendType` 内 | `PlatformInfo.FromCurrentEnvironment()` 内 |
| `#if UNITY_IOS \|\| UNITY_ANDROID` | `DetermineBackendType` 内 | `PlatformInfo.FromCurrentEnvironment()` 内 |
| `BackendSelector.Determine()` | — | プリプロセッサ **不要** |

InferenceAudioGenerator.cs 内に `DetermineBackendType` 関連のプリプロセッサ条件が残っていないことを確認する。

---

## 3. エージェントチームの役割と人数

**1エージェント構成**（0.5人日）

| 役割 | 担当内容 | 工数 |
|------|---------|------|
| 実装エージェント | Step 1-4 の全実装 + テスト追加 | 0.5 人日 |

**理由**: 新規ファイル1つ（`BackendSelector.cs`）+ 変更ファイル1つ（`InferenceAudioGenerator.cs`）+ テスト1つ（`BackendSelectorTests.cs`）と変更範囲が小さい。P3-5（AudioNormalizer）と同一パターンのリファクタリングであり、P3-5 を先に実施してパターンを確立した後に着手する。

**推奨実施順序**: Step 1（PlatformInfo） → Step 2（BackendSelector） → Step 3（InferenceAudioGenerator 変更） → Step 4（プリプロセッサ確認） → テスト作成 → 全テスト実行

---

## 4. 提供範囲とテスト項目

### 4.1 提供範囲

| カテゴリ | 範囲内 | 範囲外 |
|---------|-------|-------|
| BackendSelector | static クラス新規作成、Determine メソッド | 新しいバックエンド戦略の追加 |
| PlatformInfo | readonly struct 新規作成、FromCurrentEnvironment | プラットフォーム検出の拡張 |
| InferenceAudioGenerator | DetermineBackendType 削除、呼び出し変更 | 他のリファクタリング |
| テスト | BackendSelectorTests 14ケース新規作成 | InferenceAudioGenerator の E2E テスト |

### 4.2 Unit テスト

**テストファイル**: `Assets/uPiper/Tests/Runtime/AudioGeneration/BackendSelectorTests.cs`

`PlatformInfo` を直接構築できるため、Unity の `SystemInfo` に依存しないテストが可能。

| テスト名 | 入力 | 期待結果 |
|---------|------|---------|
| `Determine_MetalWithGPUCompute_ReturnsCPU` | Metal + GPUCompute | CPU |
| `Determine_MetalWithGPUPixel_ReturnsCPU` | Metal + GPUPixel | CPU |
| `Determine_MetalWithCPU_ReturnsCPU` | Metal + CPU | CPU |
| `Determine_MetalWithAuto_ReturnsCPU` | Metal + Auto | CPU |
| `Determine_GPUCompute_WebGPU_ReturnsGPUCompute` | WebGPU + GPUCompute | GPUCompute |
| `Determine_GPUCompute_NonWebGPU_ReturnsGPUPixel` | Desktop + GPUCompute | GPUPixel |
| `Determine_CPU_ReturnsCPU` | any + CPU | CPU |
| `Determine_GPUPixel_ReturnsGPUPixel` | any + GPUPixel | GPUPixel |
| `Determine_Auto_WebGPU_ReturnsGPUCompute` | WebGPU + Auto | GPUCompute |
| `Determine_Auto_WebGL2_ReturnsGPUPixel` | WebGL2 + Auto | GPUPixel |
| `Determine_Auto_Mobile_ComputeShader_ReturnsGPUCompute` | Mobile + CS対応 + Auto | GPUCompute |
| `Determine_Auto_Mobile_NoComputeShader_ReturnsCPU` | Mobile + CS非対応 + Auto | CPU |
| `Determine_Auto_Desktop_SufficientVRAM_ReturnsGPUPixel` | Desktop + VRAM十分 + Auto | GPUPixel |
| `Determine_Auto_Desktop_InsufficientVRAM_ReturnsCPU` | Desktop + VRAM不足 + Auto | CPU |

**テストコード例**:

```csharp
[Test]
public void Determine_MetalWithGPUCompute_ReturnsCPU()
{
    var platform = new PlatformInfo
    {
        GraphicsDeviceType = GraphicsDeviceType.Metal,
        SupportsComputeShaders = true,
        GraphicsMemorySize = 4096,
        IsWebGL = false,
        IsWebGPU = false,
        IsMobile = false,
    };

    var result = BackendSelector.Determine(InferenceBackend.GPUCompute, platform);

    Assert.AreEqual(BackendType.CPU, result);
}

[Test]
public void Determine_Auto_Desktop_SufficientVRAM_ReturnsGPUPixel()
{
    var platform = new PlatformInfo
    {
        GraphicsDeviceType = GraphicsDeviceType.Direct3D11,
        SupportsComputeShaders = true,
        GraphicsMemorySize = 4096,
        IsWebGL = false,
        IsWebGPU = false,
        IsMobile = false,
    };

    var result = BackendSelector.Determine(
        InferenceBackend.Auto, platform, gpuMemoryThresholdMB: 512);

    Assert.AreEqual(BackendType.GPUPixel, result);
}
```

### 4.3 E2E テスト

| テスト | 内容 |
|-------|------|
| CI `unity-tests.yml` 全通過 | EditMode + PlayMode テストの GREEN 確認 |
| `InferenceEngineDemo` 手動実行 | バックエンド選択結果が変わっていないことをログで確認 |

---

## 5. 懸念事項とレビュー項目

### 5.1 技術的懸念

| 懸念 | 深刻度 | 対策 |
|------|--------|------|
| **P3-2（IPiperConfigReadOnly）との順序** | 低 | BackendSelector は `InferenceBackend` enum と `int`（gpuMemoryThresholdMB）を受け取るため、P3-2 とは独立して実装可能。P3-2 適用時に `InferenceAudioGenerator` 側の config アクセスが変わるが、BackendSelector 自体は影響なし |
| **PiperLogger への依存** | 低 | 切り出し先でも `PiperLogger` を使用。テスト時はログ出力を無視する設計（ログ内容のアサートは行わない） |
| **BackendType 列挙値の変更** | 低 | `BackendType` は Unity.InferenceEngine の型であり、uPiper 側で制御できない。ただし現行の `CPU` / `GPUCompute` / `GPUPixel` のみを使用しており、新規値追加時は `Determine` のフォールバックが CPU を返す |
| **InferenceEngineDemo の呼び出し変更なし** | なし | Demo は `InferenceAudioGenerator.InitializeAsync` 経由で間接的に `DetermineBackendType` を使用しており、Demo コード自体の変更は不要 |

### 5.2 レビューチェックリスト

- [ ] `BackendSelector.cs` が `Assets/uPiper/Runtime/Core/AudioGeneration/` に配置されているか
- [ ] `PlatformInfo` が `readonly struct` で定義されているか
- [ ] `PlatformInfo.FromCurrentEnvironment()` がプリプロセッサ条件を集約しているか
- [ ] `BackendSelector.Determine()` 内にプリプロセッサ条件（`#if`）が存在しないか
- [ ] `InferenceAudioGenerator.DetermineBackendType` メソッド（L591-L680）が完全に削除されているか
- [ ] `InferenceAudioGenerator.InitializeAsync` の呼び出し（L132）が `BackendSelector.Determine()` に置き換えられているか
- [ ] `BackendSelector.Determine()` に渡す引数が正しいか（`_piperConfig.Backend`, `platformInfo`, `_piperConfig.GPUSettings.MaxMemoryMB`）
- [ ] 全6分岐（Metal/GPUCompute/CPU/GPUPixel/Auto/フォールバック）のロジックが元のコードと等価か
- [ ] Auto 選択の3プラットフォーム区分（WebGL/Mobile/Desktop）が正しく移植されているか
- [ ] 14テストケースが全て通過するか
- [ ] `dotnet format --verify-no-changes` が通過するか

---

## 6. 一から作り直すとしたら

### 6.1 バックエンド選択ロジックの設計

現在の `DetermineBackendType` は「ユーザー指定 → プラットフォーム制約 → フォールバック」の3層構造だが、これは歴史的に各プラットフォーム対応が追加されるたびに分岐が増えた結果である。一から設計するなら、以下の構造がよりクリーンである:

**Strategy パターン + プラットフォームごとのバリデーション**:

```csharp
public interface IBackendValidator
{
    bool IsSupported(BackendType backend);
    BackendType RecommendDefault();
}

public static class BackendSelector
{
    public static BackendType Determine(
        InferenceBackend requested,
        IBackendValidator validator)
    {
        if (requested == InferenceBackend.Auto)
            return validator.RecommendDefault();

        var mapped = MapRequestedToBackendType(requested);
        return validator.IsSupported(mapped) ? mapped : validator.RecommendDefault();
    }
}
```

各プラットフォームの `IBackendValidator` 実装:

```csharp
// WebGLBackendValidator: WebGPU → GPUCompute 推奨, WebGL2 → GPUPixel 推奨
// DesktopBackendValidator: Metal → CPU のみ, 十分なVRAM → GPUPixel 推奨
// MobileBackendValidator: ComputeShader 対応 → GPUCompute 推奨
```

この設計のメリット:
- 各プラットフォームのロジックが分離され、新プラットフォーム追加時に既存コードを変更しない（Open/Closed 原則）
- テスト時に `IBackendValidator` のモック注入が可能
- 「サポート判定」と「推奨デフォルト」が明確に分離

デメリット:
- 現在の分岐は6パターン + Auto 3パターンの計9パターンに過ぎず、Strategy パターンを導入するとクラス数が増えて過設計になる
- `InferenceBackend` enum（ユーザー指定）と `BackendType` enum（実際の値）の二重列挙が既に存在しており、さらにインターフェースを追加すると抽象化層が3段になる

**採用判断**: 現在の static メソッドへの切り出しが適切な複雑度。Strategy パターンは対応プラットフォームが5つ以上に増えた場合に検討する。P3-4 では `PlatformInfo` struct によるテスト容易性の確保が主目的であり、ロジック構造自体の変更はスコープ外。

### 6.2 PlatformInfo の設計選択: readonly struct vs record vs class

| 選択肢 | メリット | デメリット |
|--------|--------|----------|
| `readonly struct`（採用） | ゼロアロケーション、`init` で書き換え不可 | `Equals` / `GetHashCode` の自動生成がリフレクション経由（パフォーマンス影響は無視可能） |
| `record struct` | `with` 式によるコピー変更が容易 | C# 10+ 必須。Unity 2022 以降は対応だが、他の Phase 1-3 コードとの一貫性 |
| `class` / `record class` | null 許容、参照セマンティクス | ヒープアロケーション。初期化時1回の使い捨てデータに不適 |

設計ドキュメントでは「record」と記載されているが、`PlatformInfo` は初期化時に1回だけ構築し、`Determine` に渡した後は参照されない使い捨てデータである。値セマンティクスの `readonly struct` が最適。`record struct` も有力だが、`with` 式を使う場面がない（テストでは各テストメソッドで個別に構築するため）。

### 6.3 InferenceBackend enum と BackendType enum の二重管理

現行コードでは「ユーザーが指定する `InferenceBackend` enum」と「Sentis が受け取る `BackendType` enum」が別々に存在する。`InferenceBackend` は `Auto` を含むが、`BackendType` は含まない。`DetermineBackendType` / `BackendSelector.Determine` は実質的にこの2つの enum 間のマッピング + プラットフォーム制約の適用を行っている。

一から設計するなら、`InferenceBackend` enum を拡張して `Auto` 解決ロジックを内部に持たせるか、`BackendType` をラップする value type を定義して変換を型安全にする方法がある:

```csharp
public readonly struct ResolvedBackend
{
    public BackendType Value { get; }
    public string Reason { get; }  // 選択理由のログメッセージ

    public static ResolvedBackend Resolve(InferenceBackend requested, PlatformInfo platform) { ... }
}
```

しかし、`BackendType` は Sentis の公開 API であり、uPiper 側でラップする必然性は低い。現行の `BackendType` 直接返却が最もシンプル。

### 6.4 ログ出力の設計

現在の `DetermineBackendType` は各分岐で `Debug.LogWarning` / `PiperLogger.LogInfo` を出力している。切り出し後も `PiperLogger` を使用する設計だが、テスト時にログ出力が邪魔になる可能性がある。

代替案として、`Determine` メソッドが「選択理由」を戻り値に含める方法がある:

```csharp
public static (BackendType backend, string reason) Determine(...) { ... }
```

呼び出し元が `reason` をログに出力するか否かを制御できるため、関心の分離が改善する。ただし、現時点でログ出力がテストの妨げになっているケースはなく（アサート対象にしていないため）、タプル戻り値は呼び出し元のコードを冗長にする。現行の `PiperLogger` 直接呼び出しで問題なし。

### 6.5 現設計の正直な弱点

1. **PlatformInfo.FromCurrentEnvironment() のテスト不能**: `FromCurrentEnvironment()` 自体は `SystemInfo` に直接依存するため単体テストできない。ただしこのメソッドは単純なプロパティ読み取りの集約であり、テストの必要性は低い。ロジックを含む `Determine()` がテスト可能であれば十分。

2. **gpuMemoryThresholdMB のデフォルト値ハードコード**: `Determine` メソッドのデフォルト値 `512` は `PiperConfig.GPUSettings.MaxMemoryMB` のデフォルト値と一致している必要がある。この暗黙の結合は、どちらかが変更された場合に不整合を生む。定数を共有する仕組み（`BackendSelector.DefaultGpuMemoryThresholdMB` 等）が望ましいが、P3-2（IPiperConfigReadOnly）で設定値の一元管理が改善されるため、P3-4 時点では許容する。

3. **DetermineAutoBackend の private 化**: Auto 選択ロジックは `Determine` 内の private メソッドとして実装する。テストは public な `Determine(Auto, ...)` 経由で間接的にテストする。Auto ロジックを直接テストしたい場合は `internal` + `InternalsVisibleTo` が必要になるが、14テストケースで全パスを網羅しているため不要と判断。

### 6.6 Phase 2+3 統合設計考察

#### P3-5（AudioNormalizer）との共通リファクタリング原則

P3-4 と P3-5 は同じ「ロジック切り出し」パターンに従う。共通原則を明文化する:

**共通リファクタリング原則（Cut-Out Pattern）**:
1. **新規 static クラスを `AudioGeneration/` ディレクトリに作成**: 切り出し先は元クラスと同一ディレクトリに配置し、namespace を維持する
2. **元クラスから対象メソッドを完全削除**: 互換ファサード（`[Obsolete]` 委譲メソッド）は設けない。v2.0 は Breaking Change リリースのため一括移行
3. **呼び出し元をインスタンスメソッド → static メソッドに変更**: DI による差し替え能力は失われるが、状態を持たない純粋関数に対しては static が適切
4. **テストは新規クラスに移動/新規作成**: 元クラスのテストから切り出し対象のテストを削除し、新クラスのテストに移植
5. **ロジック変更なし**: 移動のみ。アルゴリズムの改善は別チケットで実施

**P3-4 固有の追加パターン**: P3-4 は上記共通原則に加え、`PlatformInfo` readonly struct によるプラットフォーム依存のカプセル化を行う。これはP3-5 にはない追加設計要素であり、「テスト不能な `SystemInfo` 依存を注入可能にする」というテスト容易性の改善パターンである。P3-5 は `Mathf` 依存のみであり（テスト時も問題なく動作する）、カプセル化の必要がない。

#### Group A（P2-1/P2-2）との `InferenceAudioGenerator.cs` 同時変更

P3-4 と P2-2 は共に `InferenceAudioGenerator.cs` を変更する:
- P3-4: `DetermineBackendType` メソッド削除（L591-L680、約 90 行）+ `InitializeAsync` 呼び出し変更（L132）
- P2-2: `GenerateAudioAsync`, `ExecuteInference`, `PrepareInputs`, `CreateProsodyTensorPooled`, `ExecuteWarmup`

変更対象メソッドが完全に独立しており、コンフリクトリスクは低い。ただし P3-4 による約 90 行の削除が行番号をシフトさせるため、後からリベースする側（P2-2 推奨）は diff の再適用に注意が必要。

**推奨マージ順序**: P3-4 を先にマージ → P2-2 がリベース。理由は P3-4 が 0.5 人日と小規模で先に完了する見込みが高いこと、および P2-2 が 3 人日と長期作業のため、途中のリベースを組み込みやすいこと。

#### P3-2（IPiperConfigReadOnly）との関係

P3-4 は `InferenceAudioGenerator` 内で `_piperConfig.Backend` と `_piperConfig.GPUSettings.MaxMemoryMB` を参照している。P3-2 で `IPiperConfigReadOnly` が導入された後は、これらのアクセスパスが変わる可能性がある:
- P3-2 前: `_piperConfig.Backend` → `BackendSelector.Determine(requested: _piperConfig.Backend, ...)`
- P3-2 後: `_config.Inference.Backend` → `BackendSelector.Determine(requested: _config.Inference.Backend, ...)`

`BackendSelector.Determine()` 自体はプリミティブ値を受け取る設計のため、`IPiperConfigReadOnly` の導入に影響されない。変更が必要なのは呼び出し側の `InferenceAudioGenerator.InitializeAsync` のみであり、P3-2 のセクション 7 に適切に記載されている。追加の相互参照は不要。

---

## 7. 後続タスクへの連絡事項

### P2-3（NativeArray 統一）への影響

P2-3 では `AudioNormalizer.NormalizeInPlace` に `NativeArray<float>` オーバーロードを追加するが、`BackendSelector` は NativeArray とは無関係。P3-4 の成果物は P2-3 に影響しない。

### P3-2（IPiperConfigReadOnly）との関係

`BackendSelector.Determine()` は `InferenceBackend` enum と `int`（gpuMemoryThresholdMB）をプリミティブ値として受け取る設計のため、P3-2 で `InferenceAudioGenerator` が `IPiperConfigReadOnly` 経由で config を参照するようになっても、`BackendSelector` 側の変更は不要。変更が必要なのは `InferenceAudioGenerator.InitializeAsync` の呼び出し箇所のみ:

```csharp
// P3-2 適用後:
_actualBackendType = BackendSelector.Determine(
    _config.Inference.Backend,           // IPiperConfigReadOnly 経由
    platformInfo,
    _config.Inference.GpuMemoryThresholdMB);  // ネストプロパティ名が変わる可能性
```

### ファイル変更一覧

| ファイル | 変更種別 |
|---------|---------|
| `AudioGeneration/BackendSelector.cs` | **新規作成**（PlatformInfo + BackendSelector） |
| `AudioGeneration/InferenceAudioGenerator.cs` | 修正（DetermineBackendType 削除、呼び出し変更） |
| `Tests/Runtime/AudioGeneration/BackendSelectorTests.cs` | **新規作成**（14テストケース） |
