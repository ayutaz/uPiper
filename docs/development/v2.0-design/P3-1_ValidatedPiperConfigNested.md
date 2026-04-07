# P3-1: ValidatedPiperConfig ネスト構造化 設計ドキュメント

**作成日**: 2026-04-08
**ステータス**: 設計
**依存**: Phase 1 完了後に実施可（Phase 2 と並行可）
**後続**: P3-2 (IPiperConfigReadOnly), P3-3 (Validate() 純粋関数化)
**breaking change**: ValidatedPiperConfig の全プロパティアクセスパス変更

---

## 1. 現状分析

### 1.1 ValidatedPiperConfig の21プロパティ（全リスト）

`Assets/uPiper/Runtime/Core/ValidatedPiperConfig.cs` は現在 `sealed class` で、21個のトップレベル get-only プロパティを持つ。既にソースコード上のコメントで6カテゴリに分類されているが、言語上の型レベルでは区別されていない。

| # | プロパティ名 | 型 | コメント上のカテゴリ | PiperConfig対応フィールド |
|---|------------|---|-------------------|------------------------|
| 1 | `DefaultLanguage` | `string` | Language settings | `DefaultLanguage` |
| 2 | `AutoDetectLanguage` | `bool` | Language settings | `AutoDetectLanguage` |
| 3 | `SupportedLanguages` | `IReadOnlyList<string>` | Language settings | `SupportedLanguages` |
| 4 | `MixedLanguageMode` | `MultiLanguageMode` | Language settings | `MixedLanguageMode` |
| 5 | `MaxCacheSizeMB` | `int` | Performance settings | `MaxCacheSizeMB` |
| 6 | `EnablePhonemeCache` | `bool` | Performance settings | `EnablePhonemeCache` |
| 7 | `WorkerThreads` | `int` | Performance settings | `WorkerThreads` |
| 8 | `EnableMultiThreadedInference` | `bool` | Performance settings | `EnableMultiThreadedInference` |
| 9 | `InferenceBatchSize` | `int` | Performance settings | `InferenceBatchSize` |
| 10 | `Backend` | `InferenceBackend` | Inference settings | `Backend` |
| 11 | `EnableWarmup` | `bool` | Inference settings | `EnableWarmup` |
| 12 | `WarmupIterations` | `int` | Inference settings | `WarmupIterations` |
| 13 | `AllowFallbackToCPU` | `bool` | Inference settings | `AllowFallbackToCPU` |
| 14 | `GPUSettings` | `GPUInferenceSettings` | Inference settings | `GPUSettings` |
| 15 | `SampleRate` | `int` | Audio settings | `SampleRate` |
| 16 | `NormalizeAudio` | `bool` | Audio settings | `NormalizeAudio` |
| 17 | `TargetRMSLevel` | `float` | Audio settings | `TargetRMSLevel` |
| 18 | `EnablePhonemeSilence` | `bool` | Silence settings | `EnablePhonemeSilence` |
| 19 | `PhonemeSilenceSpec` | `string` | Silence settings | `PhonemeSilenceSpec` |
| 20 | `ParsedPhonemeSilence` | `IReadOnlyDictionary<string, float>` | Silence settings | *(派生値: PhonemeSilenceProcessor.Parse)* |
| 21 | `EnableDebugLogging` | `bool` | General settings | `EnableDebugLogging` |
| 22 | `TimeoutMs` | `int` | General settings | `TimeoutMs` |

**注**: ParsedPhonemeSilence は PiperConfig に直接対応するフィールドがなく、`ValidatedPiperConfig` コンストラクタ内で `PhonemeSilenceProcessor.Parse()` により生成される派生値。

### 1.2 PiperConfig との対応関係

`PiperConfig` は `[Serializable]` クラスで public フィールドを持ち、Unity Inspector で編集可能。`PiperConfig.ToValidated()` が内部で `Validate()` を呼び出してフィールドを正規化した後、`ValidatedPiperConfig` コンストラクタでスナップショットを作成する。

v1.4.0 の P4-1 チケットでは PiperConfig 自体のサブオブジェクト分割を検討したが、Inspector 互換性の問題から、代わりに `ValidatedPiperConfig` を不変スナップショットとして導入する方式を採用した。v2.0 ではこの `ValidatedPiperConfig` をさらにネスト構造化する。

### 1.3 現在の参照箇所

#### ValidatedPiperConfig を直接参照するファイル (3ファイル)

| ファイル | 参照パターン | 用途 |
|---------|------------|------|
| `Runtime/Core/PiperTTS.cs` | フィールド宣言 `_validatedConfig`, `_config.ToValidated()` | 初期化時にスナップショット取得 |
| `Runtime/Core/PiperTTS.Inference.cs` | `_validatedConfig` を TTSSynthesisOrchestrator に注入 | コンストラクタ引数として渡す |
| `Runtime/Core/AudioGeneration/TTSSynthesisOrchestrator.cs` | `_config.ParsedPhonemeSilence`, `_config.EnablePhonemeSilence` | 句分割判定に使用 |
| `Tests/Editor/AudioGeneration/TTSSynthesisOrchestratorTests.cs` | `CreateValidatedConfig()` ヘルパー | テスト用 ValidatedPiperConfig 生成 |

#### TTSSynthesisOrchestrator 内の具体的なプロパティアクセス

```csharp
// TTSSynthesisOrchestrator.cs L72-75
var silenceParsed = _config?.ParsedPhonemeSilence;        // Silence
var useSilenceSplit = _config is { EnablePhonemeSilence: true }  // Silence
    && silenceParsed?.Count > 0
    && _voiceConfig?.PhonemeIdMap != null;
```

TTSSynthesisOrchestrator が参照する ValidatedPiperConfig プロパティは `ParsedPhonemeSilence` と `EnablePhonemeSilence` の2つのみ。

#### PiperTTS.cs の PiperConfig 直接参照（将来の V2.0 移行対象）

PiperTTS.cs は `_config` (PiperConfig) を直接参照している。v2.0 では一部を `_validatedConfig` (ValidatedPiperConfig) 経由に移行する可能性があるが、本チケットのスコープはValidatedPiperConfig のネスト構造化に限定する。

| プロパティ | 参照箇所（PiperTTS.cs の行） | カテゴリ |
|-----------|---------------------------|---------|
| `SampleRate` | L271, L813, L821, L1328, L1335 | Audio |
| `DefaultLanguage` | L271, L1015, L1206, L1211-1212, L1229 | Language |
| `EnableMultiThreadedInference` | L302 | Performance |
| `WorkerThreads` | L302, L1247 | Performance |
| `EnablePhonemeCache` | L308, L1002 | Performance |
| `MaxCacheSizeMB` | L1055, L1298 | Performance |
| `Backend` | L1184 | Inference |
| `AutoDetectLanguage` | L77 (Inference.cs) | Language |
| `SupportedLanguages` | L80 (Inference.cs) | Language |

---

## 2. カテゴリ分類とネスト record struct 設計

### 2.1 6つの readonly record struct

```csharp
namespace uPiper.Core
{
    // ── Language ──────────────────────────────────────────
    public readonly record struct LanguageSettings(
        string DefaultLanguage,
        bool AutoDetectLanguage,
        IReadOnlyList<string> SupportedLanguages,
        MultiLanguageMode MixedLanguageMode);

    // ── Performance ──────────────────────────────────────
    public readonly record struct PerformanceSettings(
        int MaxCacheSizeMB,
        bool EnablePhonemeCache,
        int WorkerThreads,
        bool EnableMultiThreadedInference,
        int InferenceBatchSize);

    // ── Inference ────────────────────────────────────────
    public readonly record struct InferenceSettings(
        InferenceBackend Backend,
        bool EnableWarmup,
        int WarmupIterations,
        bool AllowFallbackToCPU,
        GPUInferenceSettings GPUSettings);

    // ── Audio ────────────────────────────────────────────
    public readonly record struct AudioSettings(
        int SampleRate,
        bool NormalizeAudio,
        float TargetRMSLevel);

    // ── Silence ──────────────────────────────────────────
    public readonly record struct SilenceSettings(
        bool EnablePhonemeSilence,
        string PhonemeSilenceSpec,
        IReadOnlyDictionary<string, float> ParsedPhonemeSilence);

    // ── General ──────────────────────────────────────────
    public readonly record struct GeneralSettings(
        bool EnableDebugLogging,
        int TimeoutMs);
}
```

### 2.2 プロパティ分類マッピング

| カテゴリ | struct名 | プロパティ数 | プロパティ |
|---------|----------|------------|----------|
| Language | `LanguageSettings` | 4 | DefaultLanguage, AutoDetectLanguage, SupportedLanguages, MixedLanguageMode |
| Performance | `PerformanceSettings` | 5 | MaxCacheSizeMB, EnablePhonemeCache, WorkerThreads, EnableMultiThreadedInference, InferenceBatchSize |
| Inference | `InferenceSettings` | 5 | Backend, EnableWarmup, WarmupIterations, AllowFallbackToCPU, GPUSettings |
| Audio | `AudioSettings` | 3 | SampleRate, NormalizeAudio, TargetRMSLevel |
| Silence | `SilenceSettings` | 3 | EnablePhonemeSilence, PhonemeSilenceSpec, ParsedPhonemeSilence |
| General | `GeneralSettings` | 2 | EnableDebugLogging, TimeoutMs |
| **合計** | | **22** | *(ParsedPhonemeSilence は派生値として SilenceSettings に含む)* |

### 2.3 設計判断: `readonly record struct` を採用する理由

1. **不変性**: `ValidatedPiperConfig` はバリデーション済みスナップショットであり、全フィールドが不変。`readonly record struct` は言語レベルで不変性を保証する。
2. **値セマンティクス**: 設定値のグループは値型が適切。参照型の `record class` は不要なヒープ割り当てを発生させる。
3. **構造的等価性**: `record` は `Equals` / `GetHashCode` を自動生成し、テストでの比較が容易。
4. **C# 10+ 要件**: Unity 2022.2+ で利用可能。uPiper は Unity 2022.3+ を要件としているため問題なし。

### 2.4 GPUInferenceSettings の扱い

現在の `GPUInferenceSettings` は `[Serializable] class` で `MaxMemoryMB` フィールドのみを持つ。`InferenceSettings` record struct の中では参照型プロパティとして保持する。

v2.0 で `GPUInferenceSettings` 自体を `readonly record struct` に変更するかは別途検討（PiperConfig 側が `[Serializable]` class として使用しているため、ValidatedPiperConfig 側のみ struct 化する選択肢もある）。本チケットでは既存の `GPUInferenceSettings` 型をそのまま使用する。

---

## 3. ValidatedPiperConfig の新構造

### 3.1 新しいクラス定義

```csharp
namespace uPiper.Core
{
    public sealed class ValidatedPiperConfig
    {
        // ── ネスト record プロパティ ──
        public LanguageSettings Language { get; }
        public PerformanceSettings Performance { get; }
        public InferenceSettings Inference { get; }
        public AudioSettings Audio { get; }
        public SilenceSettings Silence { get; }
        public GeneralSettings General { get; }

        internal ValidatedPiperConfig(PiperConfig source)
        {
            Language = new LanguageSettings(
                DefaultLanguage: source.DefaultLanguage,
                AutoDetectLanguage: source.AutoDetectLanguage,
                SupportedLanguages: source.SupportedLanguages != null
                    ? new List<string>(source.SupportedLanguages)
                    : (IReadOnlyList<string>)Array.Empty<string>(),
                MixedLanguageMode: source.MixedLanguageMode);

            Performance = new PerformanceSettings(
                MaxCacheSizeMB: source.MaxCacheSizeMB,
                EnablePhonemeCache: source.EnablePhonemeCache,
                WorkerThreads: source.WorkerThreads,
                EnableMultiThreadedInference: source.EnableMultiThreadedInference,
                InferenceBatchSize: source.InferenceBatchSize);

            Inference = new InferenceSettings(
                Backend: source.Backend,
                EnableWarmup: source.EnableWarmup,
                WarmupIterations: source.WarmupIterations,
                AllowFallbackToCPU: source.AllowFallbackToCPU,
                GPUSettings: new GPUInferenceSettings
                {
                    MaxMemoryMB = source.GPUSettings != null
                        ? source.GPUSettings.MaxMemoryMB : 512
                });

            Audio = new AudioSettings(
                SampleRate: source.SampleRate,
                NormalizeAudio: source.NormalizeAudio,
                TargetRMSLevel: source.TargetRMSLevel);

            Silence = new SilenceSettings(
                EnablePhonemeSilence: source.EnablePhonemeSilence,
                PhonemeSilenceSpec: source.PhonemeSilenceSpec,
                ParsedPhonemeSilence: source.EnablePhonemeSilence
                    ? AudioGeneration.PhonemeSilenceProcessor.Parse(
                        source.PhonemeSilenceSpec)
                    : null);

            General = new GeneralSettings(
                EnableDebugLogging: source.EnableDebugLogging,
                TimeoutMs: source.TimeoutMs);
        }
    }
}
```

### 3.2 ファイル配置

record struct の定義場所には2つの選択肢がある:

**選択肢A: ValidatedPiperConfig.cs 内にネスト定義** (推奨)

```
Assets/uPiper/Runtime/Core/ValidatedPiperConfig.cs
  └── ValidatedPiperConfig (sealed class)
  └── LanguageSettings (readonly record struct)
  └── PerformanceSettings (readonly record struct)
  └── InferenceSettings (readonly record struct)
  └── AudioSettings (readonly record struct)
  └── SilenceSettings (readonly record struct)
  └── GeneralSettings (readonly record struct)
```

理由: record struct は ValidatedPiperConfig と密結合しており、独立して使用されることがない。1ファイルに集約することでファイル数の増加を抑え、変更時の見通しを良くする。

**選択肢B: 個別ファイルに分離**

```
Assets/uPiper/Runtime/Core/Settings/
  ├── LanguageSettings.cs
  ├── PerformanceSettings.cs
  ├── InferenceSettings.cs
  ├── AudioSettings.cs
  ├── SilenceSettings.cs
  └── GeneralSettings.cs
```

理由: 個々の struct がP3-2 (IPiperConfigReadOnly) で独立した型として参照されるなら分離が適切。ただし型数が6と多いためファイル増加が気になる。

**判定**: 選択肢A を採用。P3-2 で IPiperConfigReadOnly を定義する際に必要に応じて分離を検討する。

---

## 4. 全参照箇所のアクセスパス変更一覧

### 4.1 TTSSynthesisOrchestrator.cs (ValidatedPiperConfig 直接参照)

| 行 | 変更前 | 変更後 |
|----|--------|--------|
| L72 | `_config?.ParsedPhonemeSilence` | `_config?.Silence.ParsedPhonemeSilence` |
| L73 | `_config is { EnablePhonemeSilence: true }` | `_config is { Silence.EnablePhonemeSilence: true }` (*) |

(*) C# のプロパティパターンは1階層のみ。ネストパターンは `_config is { Silence: { EnablePhonemeSilence: true } }` とするか、通常のプロパティアクセスに変更する。

**推奨実装**:
```csharp
// 変更後
var silenceParsed = _config?.Silence.ParsedPhonemeSilence;
var useSilenceSplit = _config != null
    && _config.Silence.EnablePhonemeSilence
    && silenceParsed?.Count > 0
    && _voiceConfig?.PhonemeIdMap != null;
```

### 4.2 TTSSynthesisOrchestratorTests.cs

テストの `CreateValidatedConfig` ヘルパーは `PiperConfig` を生成して `ToValidated()` を呼ぶため、ValidatedPiperConfig のプロパティを直接参照していない。**変更不要**。

### 4.3 PiperTTS.cs / PiperTTS.Inference.cs

PiperTTS は `_config` (PiperConfig) を直接参照しており、`_validatedConfig` のプロパティは直接アクセスしていない。`_validatedConfig` は TTSSynthesisOrchestrator への注入のみに使用。**変更不要**。

ただし、将来的に PiperTTS が `_validatedConfig` 経由でプロパティにアクセスする場合のマッピングを以下に示す:

| 変更前 (フラット) | 変更後 (ネスト) |
|------------------|----------------|
| `config.DefaultLanguage` | `config.Language.DefaultLanguage` |
| `config.AutoDetectLanguage` | `config.Language.AutoDetectLanguage` |
| `config.SupportedLanguages` | `config.Language.SupportedLanguages` |
| `config.MixedLanguageMode` | `config.Language.MixedLanguageMode` |
| `config.MaxCacheSizeMB` | `config.Performance.MaxCacheSizeMB` |
| `config.EnablePhonemeCache` | `config.Performance.EnablePhonemeCache` |
| `config.WorkerThreads` | `config.Performance.WorkerThreads` |
| `config.EnableMultiThreadedInference` | `config.Performance.EnableMultiThreadedInference` |
| `config.InferenceBatchSize` | `config.Performance.InferenceBatchSize` |
| `config.Backend` | `config.Inference.Backend` |
| `config.EnableWarmup` | `config.Inference.EnableWarmup` |
| `config.WarmupIterations` | `config.Inference.WarmupIterations` |
| `config.AllowFallbackToCPU` | `config.Inference.AllowFallbackToCPU` |
| `config.GPUSettings` | `config.Inference.GPUSettings` |
| `config.SampleRate` | `config.Audio.SampleRate` |
| `config.NormalizeAudio` | `config.Audio.NormalizeAudio` |
| `config.TargetRMSLevel` | `config.Audio.TargetRMSLevel` |
| `config.EnablePhonemeSilence` | `config.Silence.EnablePhonemeSilence` |
| `config.PhonemeSilenceSpec` | `config.Silence.PhonemeSilenceSpec` |
| `config.ParsedPhonemeSilence` | `config.Silence.ParsedPhonemeSilence` |
| `config.EnableDebugLogging` | `config.General.EnableDebugLogging` |
| `config.TimeoutMs` | `config.General.TimeoutMs` |

### 4.4 影響範囲サマリ

| ファイル | 変更内容 | 変更量 |
|---------|---------|--------|
| `ValidatedPiperConfig.cs` | 21プロパティ削除、6 record struct 追加、6ネストプロパティ追加、コンストラクタ書き換え | 大 |
| `TTSSynthesisOrchestrator.cs` | 2箇所のプロパティアクセスパス変更 | 小 |
| `TTSSynthesisOrchestratorTests.cs` | 変更不要 | なし |
| `PiperTTS.cs` | 変更不要（PiperConfig 直接参照のため） | なし |
| `PiperTTS.Inference.cs` | 変更不要（ValidatedPiperConfig を注入するのみ） | なし |

---

## 5. PiperConfig.ToValidated() の変更

`PiperConfig.ToValidated()` 自体のシグネチャは変更なし。内部で生成される `ValidatedPiperConfig` のコンストラクタが新構造に合わせて変更されるが、`PiperConfig.ToValidated()` の呼び出し側には影響しない。

```csharp
// PiperConfig.cs - 変更なし
public ValidatedPiperConfig ToValidated()
{
    Validate();
    return new ValidatedPiperConfig(this);
}
```

`ValidatedPiperConfig` コンストラクタの変更は「3.1 新しいクラス定義」を参照。

---

## 6. マイグレーションガイド

### 6.1 コード変更が必要なケース

ValidatedPiperConfig のプロパティに直接アクセスしているコードは全てアクセスパスの変更が必要。

**変更パターン**:
```csharp
// v1.x (フラット)
var backend = validatedConfig.Backend;
var sampleRate = validatedConfig.SampleRate;
var enableSilence = validatedConfig.EnablePhonemeSilence;
var parsed = validatedConfig.ParsedPhonemeSilence;

// v2.0 (ネスト)
var backend = validatedConfig.Inference.Backend;
var sampleRate = validatedConfig.Audio.SampleRate;
var enableSilence = validatedConfig.Silence.EnablePhonemeSilence;
var parsed = validatedConfig.Silence.ParsedPhonemeSilence;
```

### 6.2 コード変更が不要なケース

- `PiperConfig` のフィールドに直接アクセスしているコード（PiperConfig 自体は変更なし）
- `PiperConfig.ToValidated()` の呼び出し
- `ValidatedPiperConfig` を型としてのみ参照しているコード（注入、フィールド宣言等）

### 6.3 プロパティパターンマッチングの変更

```csharp
// v1.x
if (config is { EnablePhonemeSilence: true })

// v2.0 - ネストプロパティパターン
if (config is { Silence: { EnablePhonemeSilence: true } })

// v2.0 - 通常アクセス（推奨、可読性が高い）
if (config != null && config.Silence.EnablePhonemeSilence)
```

### 6.4 v1.x 互換プロパティの検討（不採用）

v1.4.0 の PiperConfig では後方互換プロパティ（ファサード）方式を検討したが、ValidatedPiperConfig はバリデーション済みの不変オブジェクトであり、利用箇所が限定的（現在4ファイル、プロパティ直接アクセスは TTSSynthesisOrchestrator の2箇所のみ）。互換プロパティの保守コストに見合わないため、v2.0 では互換プロパティを設けず、一括移行とする。

---

## 7. P3-2 (IPiperConfigReadOnly) との関係

### 7.1 P3-2 の概要

P3-2 では `IPiperConfigReadOnly` インターフェースを定義し、`ValidatedPiperConfig` がそれを実装する。内部コンポーネント (`TTSSynthesisOrchestrator`, `InferenceAudioGenerator` 等) の依存を `IPiperConfigReadOnly` に変更する。

### 7.2 P3-1 が P3-2 の前提となる理由

`IPiperConfigReadOnly` のプロパティ定義は P3-1 のネスト record struct に依存する:

```csharp
// P3-2 で定義するインターフェース
public interface IPiperConfigReadOnly
{
    LanguageSettings Language { get; }
    PerformanceSettings Performance { get; }
    InferenceSettings Inference { get; }
    AudioSettings Audio { get; }
    SilenceSettings Silence { get; }
    GeneralSettings General { get; }
}

// ValidatedPiperConfig が実装
public sealed class ValidatedPiperConfig : IPiperConfigReadOnly
{
    // ...
}
```

P3-1 でネスト構造を確定させてから P3-2 でインターフェースを定義することで、フラット21プロパティをインターフェースに列挙する冗長さを回避できる。

### 7.3 P3-3 (Validate() 純粋関数化) との関係

P3-3 では `PiperConfig.Validate()` の副作用（フィールド書き換え）を排除し、クランプ・正規化を `ValidatedPiperConfig` コンストラクタ内に移動する。P3-1 のコンストラクタ実装がこの移動先の受け皿となるため、P3-1 と P3-3 を同時に実施すると効率的。

ただし、P3-1 単体でも価値があるため（構造的改善）、P3-3 は独立チケットとして維持する。

---

## 8. 実装手順

### Step 1: record struct 定義 (ValidatedPiperConfig.cs)

6つの `readonly record struct` を `ValidatedPiperConfig.cs` の同一 namespace (`uPiper.Core`) に追加する。ValidatedPiperConfig クラスの外側、同一ファイル内に配置。

### Step 2: ValidatedPiperConfig のプロパティ置換

21個のトップレベルプロパティを削除し、6つのネストプロパティに置換する。コンストラクタを書き換える。

### Step 3: TTSSynthesisOrchestrator のアクセスパス修正

L72-75 の2箇所を修正する。

### Step 4: テスト実行・確認

- `TTSSynthesisOrchestratorTests` 全テストパス（ValidatedPiperConfig 経由のアクセスがテストヘルパーで吸収されているため変更不要のはず）
- `PiperConfigTest` 全テストパス（PiperConfig 自体は変更なし）
- `dotnet format --verify-no-changes` パス

### Step 5: ドキュメント更新

- CLAUDE.md の ValidatedPiperConfig セクション更新
- ARCHITECTURE_ja.md / ARCHITECTURE_en.md の該当セクション更新

---

## 9. 受け入れ条件

- [ ] 6つの `readonly record struct` が定義されている
- [ ] `ValidatedPiperConfig` が6つのネストプロパティ (`Language`, `Performance`, `Inference`, `Audio`, `Silence`, `General`) を公開している
- [ ] 旧21トップレベルプロパティが削除されている
- [ ] `TTSSynthesisOrchestrator` のアクセスパスが更新されている
- [ ] `TTSSynthesisOrchestratorTests` 全テストパス
- [ ] `PiperConfigTest` 全テストパス
- [ ] `dotnet format --verify-no-changes` パス
- [ ] CLAUDE.md 更新済み

---

## 10. 見積もり

**実装**: 0.5人日（変更箇所が少ないため）
**テスト修正・確認**: 0.5人日
**合計**: 1人日
