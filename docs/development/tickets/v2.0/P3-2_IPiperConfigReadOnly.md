# P3-2: IPiperConfigReadOnly インターフェース

**マイルストーン**: M3 - データモデル + 設定整理
**設計ドキュメント**: [P3-2_IPiperConfigReadOnly.md](../../v2.0-design/P3-2_IPiperConfigReadOnly.md)
**優先度**: P2
**見積もり**: 0.5 人日
**依存チケット**: P3-1（ValidatedPiperConfig ネスト構造化）, P3-3（Validate() 純粋関数化）
**後続チケット**: なし
**ブランチ名**: `feature/v2.0-P3-2-config-readonly`

---

## 1. タスク目的とゴール

`IPiperConfigReadOnly` インターフェースを導入し、`ValidatedPiperConfig` の消費者が具象クラスではなくインターフェースに依存するように変更する。

**解決する問題**:

1. **具象クラスへの直接結合**: `TTSSynthesisOrchestrator` や `PiperTTS` が `ValidatedPiperConfig` sealed class に直接依存しており、テスト時にモック差し替えが不可能。
2. **テスト容易性の欠如**: `ValidatedPiperConfig` は sealed class であり、テスト時にモック不可。テストでは常に `PiperConfig.ToValidated()` 経由でしかインスタンス生成できない。
3. **設定の読み書き責務が型レベルで分離されていない**: `PiperConfig`（書き込み可能）と `ValidatedPiperConfig`（読み取り専用）の関係がインターフェースで明示されていない。

**完了状態（Definition of Done）**:

- `IPiperConfigReadOnly` インターフェースが `Runtime/Core/` に定義されている
- `ValidatedPiperConfig` が `IPiperConfigReadOnly` を実装している
- `TTSSynthesisOrchestrator` のコンストラクタ引数・フィールドが `IPiperConfigReadOnly` 型
- `PiperTTS._validatedConfig` のフィールド型が `IPiperConfigReadOnly` 型
- `PiperTTS.cs` の安全に移行可能な `_config` 直接参照が `_validatedConfig` 経由に変更済み
- テストが `IPiperConfigReadOnly` 型を使用するパターンに更新済み
- 全テスト通過（EditMode + PlayMode）
- `dotnet format --verify-no-changes` 通過

---

## 2. 実装する内容の詳細

### Step 1: IPiperConfigReadOnly インターフェース定義（P3-1 と同時）

対象ファイル: `Assets/uPiper/Runtime/Core/IPiperConfigReadOnly.cs`（新規作成）

P3-1 で導入される6つのネスト record struct をプロパティとして公開するインターフェースを定義する。

```csharp
namespace uPiper.Core
{
    /// <summary>
    /// 読み取り専用の設定インターフェース。
    /// バリデーション済みの不変設定にアクセスするための抽象。
    /// </summary>
    public interface IPiperConfigReadOnly
    {
        LanguageSettings Language { get; }
        PerformanceSettings Performance { get; }
        InferenceSettings Inference { get; }
        PiperAudioSettings Audio { get; }
        SilenceSettings Silence { get; }
        GeneralSettings General { get; }
    }
}
```

### Step 2: ValidatedPiperConfig への IPiperConfigReadOnly 実装追加

対象ファイル: `Assets/uPiper/Runtime/Core/ValidatedPiperConfig.cs`

```csharp
public sealed class ValidatedPiperConfig : IPiperConfigReadOnly
{
    public LanguageSettings Language { get; }
    public PerformanceSettings Performance { get; }
    public InferenceSettings Inference { get; }
    public PiperAudioSettings Audio { get; }
    public SilenceSettings Silence { get; }
    public GeneralSettings General { get; }
    // ... コンストラクタは P3-1/P3-3 で変更済み
}
```

### Step 3: TTSSynthesisOrchestrator の依存型変更

対象ファイル: `Assets/uPiper/Runtime/Core/AudioGeneration/TTSSynthesisOrchestrator.cs`

1. コンストラクタ引数: `ValidatedPiperConfig config` → `IPiperConfigReadOnly config`
2. フィールド型: `ValidatedPiperConfig _config` → `IPiperConfigReadOnly _config`
3. アクセスパスは P3-1 で既に変更済みのため、追加のアクセスパス変更は不要（`_config.Silence.ParsedPhonemeSilence` 等はインターフェース経由でも同じプロパティ名）

### Step 4: PiperTTS の依存型変更

対象ファイル: `Assets/uPiper/Runtime/Core/PiperTTS.cs`

1. `_validatedConfig` フィールド型: `ValidatedPiperConfig` → `IPiperConfigReadOnly`
2. `_config` 直接参照のうち、安全に移行可能なものを `_validatedConfig` 経由に変更:
   - `_config.EnableMultiThreadedInference` → `_validatedConfig.Performance.EnableMultiThreadedInference`
   - `_config.WorkerThreads` → `_validatedConfig.Performance.WorkerThreads`
   - `_config.Backend` → `_validatedConfig.Inference.Backend`
   - `_config.EnablePhonemeCache` → `_validatedConfig.Performance.EnablePhonemeCache`
   - `_config.MaxCacheSizeMB` → `_validatedConfig.Performance.MaxCacheSizeMB`
   - `_config.SampleRate` → `_validatedConfig.Audio.SampleRate`

3. **移行しない `_config` 参照**（`_config` がそのまま必要な箇所）:
   - `Configuration` プロパティ（`IPiperTTS` インターフェースが `PiperConfig` を返す public API）
   - `InitializeWithInferenceAsync` 内の `_config.AutoDetectLanguage`, `_config.SupportedLanguages`, `_config.DefaultLanguage`（`MultilingualPhonemizerOptions` 構築用。長期的には `_validatedConfig.Language.*` に移行すべきだが、Options 構築側の型が `string` フィールドを期待するため P3-2 のスコープでは着手しない）

### Step 5: PiperTTS.Inference.cs の引数型対応

対象ファイル: `Assets/uPiper/Runtime/Core/PiperTTS.Inference.cs`

`_orchestrator` コンストラクタへの引数が `_validatedConfig`（`IPiperConfigReadOnly` 型）になるため、型の整合性を確認。`TTSSynthesisOrchestrator` のコンストラクタが `IPiperConfigReadOnly` を受け取るようになっているため、キャスト不要で渡せる。

### Step 6: テスト更新

対象ファイル: `Assets/uPiper/Tests/Editor/AudioGeneration/TTSSynthesisOrchestratorTests.cs`

1. ヘルパーメソッド `CreateValidatedConfig()` の戻り値型を `IPiperConfigReadOnly` に変更
2. テスト内の `ValidatedPiperConfig` 型参照を `IPiperConfigReadOnly` に変更
3. 必要に応じてモック実装を追加（テストで特定のプロパティのみを設定したい場合）

### Step 7: ドキュメント更新

- `CLAUDE.md`: 主要コンポーネント表に `IPiperConfigReadOnly` を追加
- `CHANGELOG.md`: API 変更として記載

---

## 3. エージェントチームの役割と人数

**1エージェント構成**（0.5 人日）-- P3-1/P3-3 と同一エージェントが順次実施を強く推奨

| 役割 | 担当内容 | 工数 |
|------|---------|------|
| 実装エージェント | Step 1-7 の全実装 | 0.5 人日 |

**理由**: P3-1 → P3-3 → P3-2 の推奨実施順序（マイルストーンドキュメント記載）に従い、同一エージェントが `ValidatedPiperConfig.cs` を一貫して管理する。P3-2 単体の変更量は少なく（インターフェース定義20行 + フィールド型変更数行）、P3-1/P3-3 で変更した内容に上乗せする形が最も効率的。

**推奨実施順序**: P3-1 完了 → P3-3 完了 → Step 1 → Step 2 → Step 3 → Step 4 → Step 5 → 全テスト実行 → Step 6 → Step 7

---

## 4. 提供範囲とテスト項目

### 4.1 提供範囲

| カテゴリ | 範囲内 | 範囲外 |
|---------|-------|-------|
| インターフェース | `IPiperConfigReadOnly` 定義・実装 | `IPuaTokenMapper` 等の他インターフェース |
| TTSSynthesisOrchestrator | コンストラクタ引数・フィールド型変更 | ロジック変更 |
| PiperTTS | `_validatedConfig` 型変更、安全な `_config` 参照の移行 | `_config` 参照の完全排除、`IPiperTTS.Configuration` 型変更 |
| InferenceAudioGenerator | 変更なし（P3-4 BackendSelector で対応） | `InitializeAsync` の引数型変更 |
| IPiperTTS | 変更なし（public API 維持） | `ValidatedConfiguration` プロパティ追加 |
| InferenceEngineDemo | 変更なし（`PiperConfig` 直接構築のまま） | インターフェース経由への変更 |
| テスト | TTSSynthesisOrchestratorTests の型変更 | テストの大規模リファクタ |
| ドキュメント | CLAUDE.md / CHANGELOG.md の最小更新 | ARCHITECTURE ドキュメントの全面書き換え |

### 4.2 Unit テスト

**TTSSynthesisOrchestratorTests（既存テスト移行）**:

| テストメソッド | 変更種別 | 内容 |
|--------------|---------|------|
| `CreateValidatedConfig()` ヘルパー | **戻り値型変更** | `ValidatedPiperConfig` → `IPiperConfigReadOnly` |
| 7 テストケース | **型変更のみ** | `ValidatedPiperConfig` 参照を `IPiperConfigReadOnly` に |

**新規テスト（追加推奨）**:

| テストメソッド | 内容 |
|--------------|------|
| `ValidatedPiperConfig_Implements_IPiperConfigReadOnly` | `ValidatedPiperConfig` が `IPiperConfigReadOnly` を実装していることの型検証 |
| `IPiperConfigReadOnly_AllProperties_Accessible` | インターフェース経由で全6プロパティ（Language, Performance, Inference, Audio, Silence, General）がアクセス可能 |
| `TTSSynthesisOrchestrator_AcceptsIPiperConfigReadOnly` | コンストラクタに `IPiperConfigReadOnly` を渡してインスタンス化できることの確認 |

### 4.3 E2E テスト

| テスト | 内容 |
|-------|------|
| CI `unity-tests.yml` 全通過 | EditMode + PlayMode テストの GREEN 確認 |
| `InferenceEngineDemo` 手動実行 | 6言語ドロップダウンで音声生成が正常に動作することを確認（P3-2 では InferenceEngineDemo を変更しないため、回帰確認のみ） |

---

## 5. 懸念事項とレビュー項目

### 5.1 技術的懸念

| 懸念 | 深刻度 | 対策 |
|------|--------|------|
| **P3-1 / P3-3 との ValidatedPiperConfig.cs 同時変更** | 中 | 同一エージェントが P3-1 → P3-3 → P3-2 の順で実施し、マージコンフリクトを回避 |
| **PiperTTS._config 参照の移行漏れ** | 低 | P3-2 では安全に移行可能な箇所のみ対象。残りは明示的にスコープ外と文書化済み |
| **IPiperTTS.Configuration の型との不整合** | 低 | `IPiperTTS.Configuration` は `PiperConfig` を返す public API のまま維持。`ValidatedConfiguration` プロパティの追加は将来検討として明示的にスコープ外 |
| **InferenceAudioGenerator の PiperConfig 依存残存** | 低 | P3-4（BackendSelector 切り出し）で対応。P3-2 では TTSSynthesisOrchestrator のみを対象 |
| **テストでのモック需要** | 低 | 現時点では `PiperConfig.ToValidated()` で生成した実オブジェクトで十分。モック実装は必要に応じて追加 |

### 5.2 レビューチェックリスト

- [ ] `IPiperConfigReadOnly.cs` が `Runtime/Core/` に存在し、6プロパティが定義されているか
- [ ] `ValidatedPiperConfig` のクラス宣言に `: IPiperConfigReadOnly` が追加されているか
- [ ] `TTSSynthesisOrchestrator` のコンストラクタ引数が `IPiperConfigReadOnly` 型になっているか
- [ ] `TTSSynthesisOrchestrator._config` のフィールド型が `IPiperConfigReadOnly` 型になっているか
- [ ] `PiperTTS._validatedConfig` のフィールド型が `IPiperConfigReadOnly` 型になっているか
- [ ] `PiperTTS.cs` の移行対象 `_config` 参照が `_validatedConfig` 経由に変更されているか
- [ ] `PiperTTS.cs` の移行しない `_config` 参照（Configuration プロパティ、MultilingualPhonemizerOptions 構築）が維持されているか
- [ ] `PiperConfig.cs` が変更されていないか（`[Serializable]` + Inspector 互換のまま）
- [ ] `InferenceAudioGenerator.cs` / `IInferenceAudioGenerator.cs` が変更されていないか（P3-4 スコープ）
- [ ] `IPiperTTS.cs` が変更されていないか（public API 維持）
- [ ] `InferenceEngineDemo.cs` が変更されていないか
- [ ] テストが `IPiperConfigReadOnly` 型で動作しているか
- [ ] `dotnet format --verify-no-changes` が通過するか

---

## 6. 一から作り直すとしたら

### 6.1 インターフェース分離の必要性自体の再検討

P3-2 の本質的な問いは「`ValidatedPiperConfig` にインターフェースを被せる必要があるか」である。

**インターフェースが不要な場合の条件**:
- `ValidatedPiperConfig` が唯一の設定表現であり、将来的に別の設定ソース（リモート設定、プリセット、テスト用軽量スタブ等）を統一的に扱う必要がない
- テスト時に `PiperConfig.ToValidated()` で実オブジェクトを生成するコストが許容範囲内（現状は許容範囲）
- `TTSSynthesisOrchestrator` 等のコンシューマーが `ValidatedPiperConfig` の全プロパティを必要とする（実際には `Silence` の2プロパティのみ）

**インターフェースが有効な場合の条件**:
- テスト時に「特定プロパティのみを設定したモック」を注入したい（例: `Silence` だけを設定し、他はデフォルトのテスト用 `IPiperConfigReadOnly` 実装）
- 将来的に設定のホットリロード（ScriptableObject 経由のリアルタイム変更検知 → 再バリデーション → `IPiperConfigReadOnly` 再注入）を実装する可能性がある
- `ValidatedPiperConfig` を分割して、コンポーネントごとに必要なサブセットのみを注入する設計に進化させたい

**ゼロベースの設計判断**:

一から設計するなら、`IPiperConfigReadOnly` のような包括的インターフェースではなく、**コンポーネントが必要とするプロパティサブセットのインターフェースを個別に定義する** 方が ISP（Interface Segregation Principle）に準拠する:

```csharp
// 例: TTSSynthesisOrchestrator は Silence 設定のみ必要
public interface ISilenceConfig
{
    bool EnablePhonemeSilence { get; }
    IReadOnlyDictionary<string, float> ParsedPhonemeSilence { get; }
}

// InferenceAudioGenerator は Inference 設定のみ必要
public interface IInferenceConfig
{
    InferenceBackend Backend { get; }
    bool AllowFallbackToCPU { get; }
    bool EnableWarmup { get; }
    int WarmupIterations { get; }
}
```

しかし、この設計は:
1. インターフェース数が爆発する（コンシューマーごとに1つ）
2. `ValidatedPiperConfig` が5-6個のインターフェースを実装する冗長なクラスになる
3. 現状のコンシューマー数（3-4箇所）に対してオーバーエンジニアリング

したがって、P3-2 の「6プロパティを持つ単一インターフェース」は現状のコードベース規模に対して妥当なバランスと判断する。ただし、将来的にコンシューマーが増えた場合は ISP ベースの分割を検討すべき。

### 6.2 P3-1 との同時実施が必須な理由

P3-2 を P3-1 と切り離して単独実施する場合、インターフェースのプロパティが `ValidatedPiperConfig` の現在の21個の個別プロパティ（フラット構造）を列挙する形になる:

```csharp
// P3-1 なしの IPiperConfigReadOnly（非推奨）
public interface IPiperConfigReadOnly
{
    string DefaultLanguage { get; }
    bool AutoDetectLanguage { get; }
    int MaxCacheSizeMB { get; }
    int WorkerThreads { get; }
    // ... 21プロパティを列挙
}
```

これは P3-1 でネスト構造化した後に全面書き直しになるため、P3-1 完了後に P3-2 を実施する順序が必須。マイルストーンドキュメントの推奨順序（P3-1 → P3-3 → P3-2）に従うこと。

### 6.3 `IPiperTTS.Configuration` の将来設計

長期的には `IPiperTTS` インターフェースに以下を追加するのが望ましい:

```csharp
public interface IPiperTTS : IDisposable
{
    PiperConfig Configuration { get; }                    // 既存（後方互換）
    IPiperConfigReadOnly ValidatedConfiguration { get; }  // 新規（読み取り専用アクセス）
}
```

ただし、P3-2 のスコープでは実施しない。`IPiperTTS` は public API であり、プロパティ追加でも外部実装者への影響（インターフェース実装の追加が必要）がある。v2.0 の breaking change として一括で対応するか、default interface method（C# 8.0+）を使うかは、v2.0 全体の public API 設計レビュー時に決定する。

### 6.4 現設計の正直な弱点

1. **効果が限定的**: `ValidatedPiperConfig` は既に get-only プロパティで不変。インターフェース追加は「型レベルの明示」であり、実行時の安全性は向上しない。テストでモックが必要になる具体的なユースケースが現時点では存在しない。
2. **PiperTTS._config 参照の部分的移行が中途半端**: P3-2 で一部の `_config` 参照を `_validatedConfig` に移行するが、完全排除はスコープ外。結果として「一部は `_config`、一部は `_validatedConfig`」という混在状態が残る。
3. **InferenceAudioGenerator の PiperConfig 依存が残存**: P3-4 まで解消されないため、P3-2 完了時点では設定アクセスの一貫性が不完全。

### 6.5 Phase 2+3 統合設計考察

#### Config 三部作の依存チェーン内での P3-2 の位置付け

P3-2 は P3-1 → P3-3 → P3-2 の依存チェーンの末端であり、0.5 人日と最小工数のチケットである。P3-2 を独立チケットとする価値は以下の点にある:
- P3-1/P3-3 がマージされた時点で、P3-2 なしでも Config 改善の大部分（ネスト構造化 + 副作用排除）が完了している。P3-2 はインターフェース抽出という「仕上げ」であり、遅延実施しても他チケットをブロックしない。
- P3-2 は P3-4（BackendSelector）の `InferenceAudioGenerator` 側 config アクセスにも影響するため、P3-4 との順序を柔軟に調整できる利点がある。

**P3-3 との統合を推奨しない理由**: P3-3 は `Validate()` の副作用排除（テスト 27 件移行 + 12 件新規）が工数の大半を占める。P3-2 を混ぜるとレビュー範囲が「副作用排除」と「インターフェース導入」の 2 つの独立した設計判断を含むことになり、レビューの焦点がぼやける。

#### P3-4（BackendSelector）との順序関係の明確化

P3-2 と P3-4 は M3 の別グループ（Group B / Group C）に属し、並行実行可能とされている。しかし以下の間接的な関係がある:

- P3-4 は `InferenceAudioGenerator.InitializeAsync` 内で `_piperConfig.Backend` と `_piperConfig.GPUSettings.MaxMemoryMB` を参照する。P3-2 で `PiperTTS._validatedConfig` が `IPiperConfigReadOnly` 型になった場合、`InferenceAudioGenerator` への config 伝播が変わる可能性がある。
- ただし、P3-4 の `BackendSelector.Determine()` はプリミティブ値（`InferenceBackend` enum + `int`）を受け取る設計のため、`IPiperConfigReadOnly` の導入有無に関わらず `BackendSelector` 自体は影響を受けない。影響があるのは `InferenceAudioGenerator.InitializeAsync` の呼び出し箇所のみ。

**結論**: P3-2 と P3-4 は真に独立しており、マージ順序の制約はない。P3-4 のセクション 7 に記載されている「P3-2 適用後の呼び出し箇所変更」の記述は正確であり、追加の相互参照は不要。

#### Group A（P2-1/P2-2）からの影響

P3-2 は `TTSSynthesisOrchestrator` のコンストラクタ引数型を `ValidatedPiperConfig` → `IPiperConfigReadOnly` に変更する。P2-1 は同メソッドの `PhonemeSilenceProcessor.SplitAtPhonemeSilence` パススルーのシグネチャを変更し、P2-2 は `SynthesizeAsync` 内の Prosody 変数を変更する。

これらの変更は `TTSSynthesisOrchestrator.cs` 内の異なるメソッド・行に対する変更であり、3-way マージで自動解決可能。P3-2 担当エージェントが Group A のマージ後にリベースする場合、`TTSSynthesisOrchestrator.cs` のコンフリクト解決は容易。

---

## 7. ファイル一覧

### 新規作成

| ファイル | 内容 |
|---------|------|
| `Runtime/Core/IPiperConfigReadOnly.cs` | インターフェース定義（約20行） |

### 変更

| ファイル | 変更内容 |
|---------|---------|
| `Runtime/Core/ValidatedPiperConfig.cs` | `IPiperConfigReadOnly` 実装追加（クラス宣言に `: IPiperConfigReadOnly` を追加。プロパティは P3-1 で既にネスト record 型に変更済みのため追加変更なし） |
| `Runtime/Core/AudioGeneration/TTSSynthesisOrchestrator.cs` | コンストラクタ引数・フィールド型を `IPiperConfigReadOnly` に変更（2行） |
| `Runtime/Core/PiperTTS.cs` | `_validatedConfig` フィールド型変更（1行）+ 安全な `_config` 参照の `_validatedConfig` 経由への移行（数行） |
| `Runtime/Core/PiperTTS.Inference.cs` | `_orchestrator` コンストラクタへの引数型対応（型の整合性確認のみ、実質的な変更は不要の見込み） |
| `Tests/Editor/AudioGeneration/TTSSynthesisOrchestratorTests.cs` | ヘルパー戻り値型変更 + 型参照変更（2-3行） |

### 変更なし

| ファイル | 理由 |
|---------|------|
| `Runtime/Core/PiperConfig.cs` | `[Serializable]` + Inspector 互換のまま維持 |
| `Runtime/Core/AudioGeneration/InferenceAudioGenerator.cs` | P3-4（BackendSelector 切り出し）で対応 |
| `Runtime/Core/AudioGeneration/IInferenceAudioGenerator.cs` | P3-4 で対応 |
| `Runtime/Core/IPiperTTS.cs` | public API 維持。`ValidatedConfiguration` プロパティ追加は将来検討 |
| `Runtime/Demo/InferenceEngineDemo.cs` | `PiperConfig` 直接構築のまま維持 |
