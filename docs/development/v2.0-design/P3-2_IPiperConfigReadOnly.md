# P3-2: IPiperConfigReadOnly インターフェース 設計ドキュメント

**作成日**: 2026-04-08
**前提**: P3-1（ValidatedPiperConfig ネスト構造化）完了後に実施
**依存**: P3-1

---

## 1. 現状分析

### 1.1 設定クラスの構造

現在、設定は2つのクラスで管理されている:

| クラス | 役割 | 可変性 |
|--------|------|--------|
| `PiperConfig` | Inspector編集可能な`[Serializable]`クラス。publicフィールド21個 | **可変** (publicフィールド) |
| `ValidatedPiperConfig` | `PiperConfig.ToValidated()`で生成される不変スナップショット | **読み取り専用** (getterのみ) |

`PiperConfig` は Unity Inspector との統合のために `[Serializable]` + public フィールドを使用しており、外部からの変更を防ぐ手段がない。`ValidatedPiperConfig` は sealed class + get-only プロパティで不変だが、インターフェース抽象がないため、依存先が具象クラスに直接結合している。

### 1.2 設定の読み取り箇所（Read）と変更箇所（Write）の分類

#### 1.2.1 ValidatedPiperConfig の使用箇所（全て読み取り専用）

| ファイル | 使用方法 | アクセスするプロパティ |
|---------|---------|---------------------|
| `TTSSynthesisOrchestrator.cs` | コンストラクタ注入、`_config`フィールド | `ParsedPhonemeSilence`, `EnablePhonemeSilence` |
| `PiperTTS.Inference.cs` | `_orchestrator`コンストラクタに渡す | (中継のみ) |
| `PiperTTS.cs` | `_validatedConfig`フィールドとして保持 | (中継のみ、直接プロパティアクセスなし) |
| `TTSSynthesisOrchestratorTests.cs` | テストヘルパーで生成 | `EnablePhonemeSilence`, `ParsedPhonemeSilence` |

#### 1.2.2 PiperConfig の使用箇所

**読み取りのみ（構築後にフィールドを変更しない）**:

| ファイル | フィールド参照 | 用途 |
|---------|-------------|------|
| `PiperTTS.cs` (21箇所) | `SampleRate`, `DefaultLanguage`, `EnableMultiThreadedInference`, `WorkerThreads`, `EnablePhonemeCache`, `MaxCacheSizeMB`, `Backend`, `SupportedLanguages` | 初期化・言語検出・キャッシュ・バックエンド選択 |
| `InferenceAudioGenerator.cs` (4箇所) | `AllowFallbackToCPU`, `EnableWarmup`, `WarmupIterations`, `Backend`, `GPUSettings.MaxMemoryMB` | バックエンド決定・ウォームアップ |
| `InferenceEngineDemo.cs` (1箇所) | `Backend`, `AllowFallbackToCPU`, `GPUSettings` | UI設定からの構築 |
| テスト各種 (20+箇所) | 全フィールド | テストセットアップ |

**変更する箇所（Write）**:

| ファイル | 変更内容 |
|---------|---------|
| `PiperConfig.Validate()` | フィールドをクランプ・修正（`WorkerThreads=0`自動検出、`DefaultLanguage`小文字化、`MaxCacheSizeMB`レンジ制限等） |
| `PiperConfig.CreateDefault()` | IL2CPP向けフィールド設定 |
| `InferenceEngineDemo.cs` | `new PiperConfig { Backend = ..., GPUSettings = ... }` でオブジェクト初期化時に設定 |
| テスト各種 | オブジェクト初期化子で設定 |

#### 1.2.3 重要な発見

1. **PiperTTS.cs は `_config`（PiperConfig）を直接読み取る**: `ValidatedPiperConfig`を保持しているにもかかわらず、多くの箇所で `_config` を直接参照している。これはバリデーション前の値を使用するリスクがある。
2. **InferenceAudioGenerator は PiperConfig を直接受け取る**: `IInferenceAudioGenerator.InitializeAsync` のシグネチャが `PiperConfig` 具象クラスに依存している。
3. **`ValidatedPiperConfig` の実際の消費者は TTSSynthesisOrchestrator のみ**: 他のコンポーネントは `PiperConfig` を直接読んでいる。

---

## 2. IPiperConfigReadOnly インターフェース設計

### 2.1 設計方針

- P3-1 で導入されるネスト record struct をプロパティとして公開する
- `ValidatedPiperConfig` がこのインターフェースを実装する
- 内部コンポーネントの依存を具象クラスからインターフェースに変更する
- `PiperConfig` 自体は `[Serializable]` + public フィールドのまま維持（Unity Inspector 互換）

### 2.2 インターフェース定義

```csharp
namespace uPiper.Core
{
    /// <summary>
    /// 読み取り専用の設定インターフェース。
    /// バリデーション済みの不変設定にアクセスするための抽象。
    /// </summary>
    public interface IPiperConfigReadOnly
    {
        // P3-1 で導入されるネスト record
        LanguageSettings Language { get; }
        PerformanceSettings Performance { get; }
        InferenceSettings Inference { get; }
        AudioSettings Audio { get; }
        SilenceSettings Silence { get; }
        GeneralSettings General { get; }
    }
}
```

### 2.3 ネスト record struct（P3-1 で定義、本タスクで参照）

```csharp
// P3-1 で定義される型（参考）
public readonly record struct LanguageSettings(
    string DefaultLanguage,
    bool AutoDetectLanguage,
    IReadOnlyList<string> SupportedLanguages,
    MultiLanguageMode MixedLanguageMode);

public readonly record struct PerformanceSettings(
    int MaxCacheSizeMB,
    bool EnablePhonemeCache,
    int WorkerThreads,
    bool EnableMultiThreadedInference,
    int InferenceBatchSize);

public readonly record struct InferenceSettings(
    InferenceBackend Backend,
    bool EnableWarmup,
    int WarmupIterations,
    bool AllowFallbackToCPU,
    GPUInferenceSettings GPUSettings);

public readonly record struct AudioSettings(
    int SampleRate,
    bool NormalizeAudio,
    float TargetRMSLevel);

public readonly record struct SilenceSettings(
    bool EnablePhonemeSilence,
    string PhonemeSilenceSpec,
    IReadOnlyDictionary<string, float> ParsedPhonemeSilence);

public readonly record struct GeneralSettings(
    bool EnableDebugLogging,
    int TimeoutMs);
```

### 2.4 ValidatedPiperConfig の実装

```csharp
public sealed class ValidatedPiperConfig : IPiperConfigReadOnly
{
    public LanguageSettings Language { get; }
    public PerformanceSettings Performance { get; }
    public InferenceSettings Inference { get; }
    public AudioSettings Audio { get; }
    public SilenceSettings Silence { get; }
    public GeneralSettings General { get; }

    internal ValidatedPiperConfig(PiperConfig source)
    {
        Language = new LanguageSettings(
            source.DefaultLanguage,
            source.AutoDetectLanguage,
            source.SupportedLanguages != null
                ? new List<string>(source.SupportedLanguages)
                : (IReadOnlyList<string>)Array.Empty<string>(),
            source.MixedLanguageMode);

        Performance = new PerformanceSettings(
            source.MaxCacheSizeMB,
            source.EnablePhonemeCache,
            source.WorkerThreads,
            source.EnableMultiThreadedInference,
            source.InferenceBatchSize);

        Inference = new InferenceSettings(
            source.Backend,
            source.EnableWarmup,
            source.WarmupIterations,
            source.AllowFallbackToCPU,
            source.GPUSettings != null
                ? new GPUInferenceSettings { MaxMemoryMB = source.GPUSettings.MaxMemoryMB }
                : new GPUInferenceSettings());

        Audio = new AudioSettings(
            source.SampleRate,
            source.NormalizeAudio,
            source.TargetRMSLevel);

        Silence = new SilenceSettings(
            source.EnablePhonemeSilence,
            source.PhonemeSilenceSpec,
            source.EnablePhonemeSilence
                ? AudioGeneration.PhonemeSilenceProcessor.Parse(source.PhonemeSilenceSpec)
                : null);

        General = new GeneralSettings(
            source.EnableDebugLogging,
            source.TimeoutMs);
    }
}
```

---

## 3. 使用箇所での型変更一覧

### 3.1 コンストラクタ / メソッドシグネチャ変更

| ファイル | 変更前 | 変更後 |
|---------|--------|--------|
| `TTSSynthesisOrchestrator` コンストラクタ | `ValidatedPiperConfig config` | `IPiperConfigReadOnly config` |
| `IInferenceAudioGenerator.InitializeAsync` | `PiperConfig piperConfig` | そのまま維持（後述） |
| `InferenceAudioGenerator.InitializeAsync` | `PiperConfig piperConfig` | そのまま維持（後述） |
| `InferenceAudioGenerator.DetermineBackendType` | `PiperConfig config` | `IPiperConfigReadOnly config` (P3-4 BackendSelector切り出し時に統合) |

### 3.2 フィールド型変更

| ファイル | 変更前 | 変更後 |
|---------|--------|--------|
| `TTSSynthesisOrchestrator._config` | `ValidatedPiperConfig` | `IPiperConfigReadOnly` |
| `PiperTTS._validatedConfig` | `ValidatedPiperConfig` | `IPiperConfigReadOnly` |

### 3.3 プロパティアクセスパス変更

P3-1 と同時に変更される。以下は代表的な変更:

```csharp
// TTSSynthesisOrchestrator.cs
// Before (P3-1 後):
_config.Silence.ParsedPhonemeSilence
_config.Silence.EnablePhonemeSilence

// After (P3-2 後): 同じ — インターフェース経由でも同じプロパティ名
```

アクセスパス自体は P3-1 で変更され、P3-2 ではフィールドの型だけが具象→インターフェースに変わる。

### 3.4 変更しない箇所

| ファイル | 理由 |
|---------|------|
| `PiperTTS._config` (PiperConfig) | Inspector 編集後の再バリデーション等で可変アクセスが必要。ただし初期化後の読み取りは `_validatedConfig` (IPiperConfigReadOnly) 経由に移行すべき |
| `IInferenceAudioGenerator.InitializeAsync(PiperConfig)` | 初期化メソッドは `PiperConfig` を受け取り内部で `ValidatedPiperConfig` を生成する設計が自然。ただし P3-3 (Validate純粋関数化) と連携して将来的に `IPiperConfigReadOnly` 受け取りに変更可能 |
| `InferenceEngineDemo.cs` | デモUIは `PiperConfig` を直接構築して渡すため、具象クラス依存のまま |
| テスト各種 | `PiperConfig` オブジェクト初期化子 → `ToValidated()` パターンはそのまま維持 |

---

## 4. PiperTTS.cs の `_config` 直接参照問題

### 4.1 問題

`PiperTTS.cs` は `_config` (PiperConfig) を21箇所で直接参照している。`_validatedConfig` (ValidatedPiperConfig) を保持しているが、ほとんど使っていない。これは以下の問題を引き起こす:

1. **バリデーション前の値を使用するリスク**: `Validate()` でクランプされた値ではなく、元の値を参照している
2. **不変性の保証がない**: `PiperConfig` は public フィールドのため、外部から変更可能

### 4.2 推奨対応

P3-2 のスコープとして、`PiperTTS.cs` の `_config` 直接参照を `_validatedConfig` (IPiperConfigReadOnly) 経由に段階的に移行する:

```csharp
// Before:
if (_config.EnableMultiThreadedInference && _config.WorkerThreads > 1)
_inferenceBackend = _config.Backend switch { ... }

// After:
if (_validatedConfig.Performance.EnableMultiThreadedInference
    && _validatedConfig.Performance.WorkerThreads > 1)
_inferenceBackend = _validatedConfig.Inference.Backend switch { ... }
```

**注意**: `_config` への参照を完全に排除するのは P3-2 のスコープ外。以下のケースでは `_config` が引き続き必要:

- `Configuration` プロパティ（IPiperTTS インターフェースが `PiperConfig` を返す）
- `InitializeWithInferenceAsync` 内の `_config.AutoDetectLanguage`, `_config.SupportedLanguages`, `_config.DefaultLanguage`（MultilingualPhonemizerOptions 構築用）

長期的には `IPiperTTS.Configuration` の戻り値型を `IPiperConfigReadOnly` に変更するのが望ましいが、これは public API の breaking change となるため v2.0 のスコープで検討する。

---

## 5. InferenceAudioGenerator の PiperConfig 依存

### 5.1 現状

`InferenceAudioGenerator` は初期化時に `PiperConfig` を受け取り、以下のフィールドを読み取る:

| フィールド | 用途 | 読み取りタイミング |
|-----------|------|------------------|
| `Backend` | `DetermineBackendType()` | 初期化時のみ |
| `AllowFallbackToCPU` | GPU失敗時のフォールバック判定 | 初期化時のみ |
| `EnableWarmup` | ウォームアップ実行判定 | 初期化時のみ |
| `WarmupIterations` | ウォームアップ回数 | 初期化時のみ |
| `GPUSettings.MaxMemoryMB` | Auto選択時のGPUメモリ閾値 | 初期化時のみ |

全て初期化時に読み取られ、以降は参照されない。

### 5.2 設計選択肢

**選択肢A: InitializeAsync の引数を IPiperConfigReadOnly に変更**

```csharp
Task InitializeAsync(ModelAsset modelAsset, PiperVoiceConfig config,
    IPiperConfigReadOnly piperConfig, CancellationToken ct = default);
```

利点: 依存が明確化。テスト時にモック可能。
欠点: IInferenceAudioGenerator の public API 変更。呼び出し側で `ToValidated()` が必須に。

**選択肢B: 現状維持（PiperConfig のまま）**

利点: 変更量が少ない。呼び出し側は `new PiperConfig { ... }` で簡潔に渡せる。
欠点: バリデーション前の値が渡される可能性がある。

**選択肢C: P3-4 BackendSelector 切り出しと同時に解消**

`DetermineBackendType` を `BackendSelector` に切り出し、`BackendSelector.Determine(InferenceBackend, PlatformInfo)` として設計。`InferenceAudioGenerator` は `BackendType` と `WarmupSettings` を直接受け取る。

利点: 設定オブジェクトへの依存自体を排除。
欠点: P3-4 との同時実施が前提。

### 5.3 推奨

**選択肢C を推奨**。P3-4 (BackendSelector切り出し) と組み合わせることで、`InferenceAudioGenerator` から設定オブジェクトへの依存を根本的に排除できる。P3-2 単体では `TTSSynthesisOrchestrator` のみを対象とし、`InferenceAudioGenerator` は P3-4 で対応する。

---

## 6. IPiperTTS.Configuration の型変更

### 6.1 現状

```csharp
public interface IPiperTTS : IDisposable
{
    public PiperConfig Configuration { get; }
}
```

### 6.2 検討

`Configuration` の戻り値を `IPiperConfigReadOnly` に変更すると:
- 外部ユーザーが設定を読み取り専用でアクセスできる
- ただし、現在 `PiperConfig` を返しているため breaking change
- `InferenceEngineDemo` 等で `PiperTTS.Configuration` を使って設定を変更しているケースがあれば影響大

実際には `PiperTTS.Configuration` は `_config` をそのまま返しており、外部から変更可能な状態。これは設計上の問題だが、v2.0 で修正するスコープに含めるかは要検討。

### 6.3 推奨

P3-2 では `IPiperTTS.Configuration` の型変更は**行わない**。理由:
- `PiperConfig` は `[Serializable]` で Inspector バインドされるユースケースがある
- 外部ユーザーが `Configuration` 経由で実行時に設定を変更→再初期化するパターンの有無が不明
- 代わりに `IPiperTTS` に `IPiperConfigReadOnly ValidatedConfiguration { get; }` を追加する案を P3-2 の拡張スコープとして検討

---

## 7. YAGNI 判定

### 7.1 本当に必要か

**必要性: 中**

| 観点 | 評価 |
|------|------|
| 不変性保証 | `ValidatedPiperConfig` は既にget-onlyプロパティで不変。インターフェースの追加は「型レベルの明示」であり、実行時の安全性は変わらない |
| テスト容易性 | `ValidatedPiperConfig` は sealed class であり、テスト時にモック不可。インターフェース化でモック可能になる |
| 依存の明確化 | `TTSSynthesisOrchestrator` が具象クラスではなくインターフェースに依存することで、依存関係が明確になる |
| 将来の拡張 | 異なる設定ソース（リモート設定、プリセット等）を統一的に扱える |

### 7.2 コスト対効果

**コスト**:
- インターフェース定義: 1ファイル、約20行
- `ValidatedPiperConfig` への `implements` 追加: 1行
- `TTSSynthesisOrchestrator` のフィールド型変更: 2行（フィールド + コンストラクタ引数）
- `PiperTTS._validatedConfig` のフィールド型変更: 1行
- テストの型変更: 2-3行

**効果**:
- テスト時に `IPiperConfigReadOnly` をモック可能（現在は `PiperConfig.ToValidated()` でしか生成不可）
- 将来的に設定の「読む側」と「書く側」の責務分離が型レベルで保証される
- P3-1 のネスト構造と組み合わせることで、設定APIの完成度が上がる

### 7.3 結論

**P3-1 と同時実施を推奨**。P3-1 で `ValidatedPiperConfig` のプロパティ構造が大幅に変わるため、同時にインターフェースを定義するのが最も効率的。単独実施の場合は変更量に対して得られる効果が限定的。

---

## 8. 実装計画

### 8.1 Phase A: インターフェース定義（P3-1 と同時）

1. `IPiperConfigReadOnly.cs` を `Runtime/Core/` に作成
2. P3-1 のネスト record struct を参照するプロパティを定義
3. `ValidatedPiperConfig` に `IPiperConfigReadOnly` を実装

### 8.2 Phase B: 内部コンポーネントの依存変更

1. `TTSSynthesisOrchestrator` のコンストラクタ引数・フィールドを `IPiperConfigReadOnly` に変更
2. `PiperTTS._validatedConfig` のフィールド型を `IPiperConfigReadOnly` に変更
3. `PiperTTS.cs` の `_config` 直接参照のうち、安全に移行可能なものを `_validatedConfig` 経由に変更

### 8.3 Phase C: テスト更新

1. `TTSSynthesisOrchestratorTests` のヘルパーメソッド戻り値型を変更
2. 必要に応じてモック実装を追加

### 8.4 スコープ外（将来検討）

- `IPiperTTS.Configuration` の戻り値型変更
- `IInferenceAudioGenerator.InitializeAsync` の引数変更（P3-4 で対応）
- `PiperTTS._config` の完全排除

---

## 9. ファイル一覧

### 新規作成

| ファイル | 内容 |
|---------|------|
| `Runtime/Core/IPiperConfigReadOnly.cs` | インターフェース定義 |

### 変更

| ファイル | 変更内容 |
|---------|---------|
| `Runtime/Core/ValidatedPiperConfig.cs` | `IPiperConfigReadOnly` 実装追加、プロパティをネスト record 経由に変更 (P3-1 と同時) |
| `Runtime/Core/AudioGeneration/TTSSynthesisOrchestrator.cs` | コンストラクタ引数・フィールド型を `IPiperConfigReadOnly` に変更 |
| `Runtime/Core/PiperTTS.cs` | `_validatedConfig` フィールド型変更、一部の `_config` 参照を移行 |
| `Runtime/Core/PiperTTS.Inference.cs` | `_orchestrator` コンストラクタへの引数型対応 |
| `Tests/Editor/AudioGeneration/TTSSynthesisOrchestratorTests.cs` | ヘルパー戻り値型変更 |

### 変更なし

| ファイル | 理由 |
|---------|------|
| `Runtime/Core/PiperConfig.cs` | `[Serializable]` + Inspector 互換のまま維持 |
| `Runtime/Core/AudioGeneration/InferenceAudioGenerator.cs` | P3-4 (BackendSelector) で対応 |
| `Runtime/Core/AudioGeneration/IInferenceAudioGenerator.cs` | P3-4 で対応 |
| `Runtime/Core/IPiperTTS.cs` | public API 維持。`ValidatedConfiguration` プロパティ追加は将来検討 |
| `Runtime/Demo/InferenceEngineDemo.cs` | `PiperConfig` 直接構築のまま維持 |
