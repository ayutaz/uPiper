# P3-1: ValidatedPiperConfig ネスト構造化

**マイルストーン**: M3 - Data Model + Config
**優先度**: P1
**見積もり**: 1 人日（実装 0.5 人日 + テスト修正・確認 0.5 人日）
**依存チケット**: Phase 1 完了（M2 ゲート通過後）
**後続チケット**: P3-3（Validate() 純粋関数化）, P3-2（IPiperConfigReadOnly インターフェース）
**ブランチ名**: `feature/v2.0-P3-1-validated-config-nested`

---

## 1. タスク目的とゴール

`ValidatedPiperConfig` の 22 個のフラットなトップレベルプロパティを、6 つの `readonly record struct` にネスト構造化する。

**解決する問題**:

1. **22 プロパティのフラット構造による認知負荷**: 既にソースコード上のコメントで 6 カテゴリに分類されているが、言語上の型レベルでは区別されていない。Language / Performance / Inference / Audio / Silence / General の意味的区分が型システムで表現されていない。
2. **P3-2 (IPiperConfigReadOnly) の前提条件**: フラットな 22 プロパティをそのままインターフェースに列挙すると冗長になる。ネスト構造化により `IPiperConfigReadOnly` を 6 プロパティの簡潔なインターフェースとして定義できる。
3. **P3-3 (Validate() 純粋関数化) の受け皿**: クランプ・正規化ロジックの移動先として、ネスト record struct のコンストラクタが自然な受け皿になる。

**完了状態（Definition of Done）**:

- 6 つの `readonly record struct`（`LanguageSettings`, `PerformanceSettings`, `InferenceSettings`, `AudioSettings`, `SilenceSettings`, `GeneralSettings`）が定義されている
- `ValidatedPiperConfig` が 6 つのネストプロパティ（`Language`, `Performance`, `Inference`, `Audio`, `Silence`, `General`）を公開している
- 旧 22 トップレベルプロパティが削除されている
- `TTSSynthesisOrchestrator` の 2 箇所のアクセスパスが更新されている
- `TTSSynthesisOrchestratorTests` 全テストパス
- `PiperConfigTest` 全テストパス
- `dotnet format --verify-no-changes` パス

---

## 2. 実装する内容の詳細

### Step 1: readonly record struct 定義（ValidatedPiperConfig.cs）

対象ファイル: `Assets/uPiper/Runtime/Core/ValidatedPiperConfig.cs`

6 つの `readonly record struct` を `ValidatedPiperConfig` クラスの外側、同一ファイル内の `uPiper.Core` namespace に追加する。

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

**設計判断: `readonly record struct` を採用する理由**:
- **不変性**: 言語レベルで不変性を保証。ValidatedPiperConfig のバリデーション済みスナップショットという性質と合致
- **値セマンティクス**: 設定値のグループは値型が適切。参照型の `record class` は不要なヒープ割り当てを発生させる
- **構造的等価性**: `record` は `Equals` / `GetHashCode` を自動生成。テストでの比較が容易
- **C# 10+ 要件**: Unity 2022.2+ で利用可能。uPiper は Unity 2022.3+ を要件としているため問題なし

**ファイル配置判断**: 選択肢 A（ValidatedPiperConfig.cs 内にネスト定義）を採用。record struct は ValidatedPiperConfig と密結合しており、独立して使用されることがない。1 ファイルに集約することでファイル数の増加を抑え、変更時の見通しを良くする。P3-2 で `IPiperConfigReadOnly` を定義する際に必要に応じて分離を検討する。

### Step 2: ValidatedPiperConfig のプロパティ置換

同一ファイル内で、22 個のトップレベル get-only プロパティを削除し、6 つのネストプロパティに置換する。コンストラクタを書き換える。

```csharp
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
```

**カテゴリ分類マッピング**:

| カテゴリ | struct 名 | プロパティ数 | プロパティ |
|---------|----------|------------|----------|
| Language | `LanguageSettings` | 4 | DefaultLanguage, AutoDetectLanguage, SupportedLanguages, MixedLanguageMode |
| Performance | `PerformanceSettings` | 5 | MaxCacheSizeMB, EnablePhonemeCache, WorkerThreads, EnableMultiThreadedInference, InferenceBatchSize |
| Inference | `InferenceSettings` | 5 | Backend, EnableWarmup, WarmupIterations, AllowFallbackToCPU, GPUSettings |
| Audio | `AudioSettings` | 3 | SampleRate, NormalizeAudio, TargetRMSLevel |
| Silence | `SilenceSettings` | 3 | EnablePhonemeSilence, PhonemeSilenceSpec, ParsedPhonemeSilence |
| General | `GeneralSettings` | 2 | EnableDebugLogging, TimeoutMs |
| **合計** | | **22** | *(ParsedPhonemeSilence は派生値として SilenceSettings に含む)* |

**GPUInferenceSettings の扱い**: 現在の `GPUInferenceSettings` は `[Serializable] class` で `MaxMemoryMB` フィールドのみを持つ。`InferenceSettings` record struct の中では参照型プロパティとして保持する。本チケットでは既存の `GPUInferenceSettings` 型をそのまま使用する。

### Step 3: TTSSynthesisOrchestrator のアクセスパス修正

対象ファイル: `Assets/uPiper/Runtime/Core/AudioGeneration/TTSSynthesisOrchestrator.cs`

TTSSynthesisOrchestrator が参照する ValidatedPiperConfig プロパティは `ParsedPhonemeSilence` と `EnablePhonemeSilence` の **2 箇所のみ**。これは影響範囲が極めて小さいことを意味する。

| 行 | 変更前 | 変更後 |
|----|--------|--------|
| L72 | `_config?.ParsedPhonemeSilence` | `_config?.Silence.ParsedPhonemeSilence` |
| L73 | `_config is { EnablePhonemeSilence: true }` | `_config != null && _config.Silence.EnablePhonemeSilence` |

**推奨実装**（C# のプロパティパターンは 1 階層のみのため、通常のプロパティアクセスに変更）:

```csharp
// 変更後
var silenceParsed = _config?.Silence.ParsedPhonemeSilence;
var useSilenceSplit = _config != null
    && _config.Silence.EnablePhonemeSilence
    && silenceParsed?.Count > 0
    && _voiceConfig?.PhonemeIdMap != null;
```

### Step 4: テスト実行・確認

- `TTSSynthesisOrchestratorTests` 全テストパス（`CreateValidatedConfig` ヘルパーは `PiperConfig` を生成して `ToValidated()` を呼ぶため、ValidatedPiperConfig のプロパティを直接参照していない。**変更不要**）
- `PiperConfigTest` 全テストパス（PiperConfig 自体は変更なし）
- `dotnet format --verify-no-changes` パス

### Step 5: ドキュメント更新

- `CLAUDE.md` の ValidatedPiperConfig セクション更新
- `ARCHITECTURE_ja.md` / `ARCHITECTURE_en.md` の該当セクション更新

### 補足: PiperConfig.ToValidated() は変更不要

`PiperConfig.ToValidated()` 自体のシグネチャは変更なし。内部で生成される `ValidatedPiperConfig` のコンストラクタが新構造に合わせて変更されるが、呼び出し側には影響しない。

```csharp
// PiperConfig.cs - 変更なし
public ValidatedPiperConfig ToValidated()
{
    Validate();
    return new ValidatedPiperConfig(this);
}
```

---

## 3. エージェントチームの役割と人数

**1 エージェント構成**（1 人日）

| 役割 | 担当内容 | 工数 |
|------|---------|------|
| 実装エージェント | Step 1-5 の全実装 + テスト確認 + ドキュメント更新 | 1 人日 |

**理由**: 影響箇所が極めて限定的（ValidatedPiperConfig.cs の書き換え + TTSSynthesisOrchestrator.cs の 2 行修正のみ）であり、複数エージェントに分割するとマージコストが工数を上回る。

**P3-2 との同時実施推奨**: P3-2（IPiperConfigReadOnly インターフェース）は P3-1 のネスト record struct に依存する。P3-1 でネスト構造を確定させてから P3-2 でインターフェースを定義することで、フラット 22 プロパティをインターフェースに列挙する冗長さを回避できる。P3-1 と P3-2 を同一エージェントが順次実施するのが最も効率的。推奨順序: **P3-1 -> P3-3 -> P3-2**（P3-3 は P3-1 のコンストラクタ変更と密結合するため先に実施）。

---

## 4. 提供範囲とテスト項目

### 4.1 提供範囲

| カテゴリ | 範囲内 | 範囲外 |
|---------|-------|-------|
| ValidatedPiperConfig | 22 プロパティ削除、6 record struct 追加、6 ネストプロパティ追加、コンストラクタ書き換え | `IPiperConfigReadOnly` の追加（P3-2）、`Validate()` 純粋関数化（P3-3） |
| TTSSynthesisOrchestrator | 2 箇所のプロパティアクセスパス変更 | コンストラクタ引数の型変更（P3-2 で対応） |
| PiperConfig | 変更なし（`ToValidated()` のシグネチャ不変） | サブオブジェクト分割（スコープ外） |
| PiperTTS | 変更なし（`_validatedConfig` はフィールド宣言・注入のみ、プロパティ直接アクセスなし） | `_config` 直接参照の移行（P3-2 で段階的に対応） |
| テスト | TTSSynthesisOrchestratorTests 変更不要の確認 | テスト大規模リファクタ |
| ドキュメント | CLAUDE.md / ARCHITECTURE の最小更新 | マイグレーションガイド完成（M5 で対応） |

### 4.2 影響範囲サマリ

| ファイル | 変更内容 | 変更量 |
|---------|---------|--------|
| `ValidatedPiperConfig.cs` | 22 プロパティ削除、6 record struct 追加、6 ネストプロパティ追加、コンストラクタ書き換え | 大 |
| `TTSSynthesisOrchestrator.cs` | 2 箇所のプロパティアクセスパス変更 | **極小** |
| `TTSSynthesisOrchestratorTests.cs` | 変更不要 | なし |
| `PiperTTS.cs` | 変更不要（PiperConfig 直接参照のため） | なし |
| `PiperTTS.Inference.cs` | 変更不要（ValidatedPiperConfig を注入するのみ） | なし |

### 4.3 Unit テスト

**TTSSynthesisOrchestratorTests（変更不要の確認）**:

テストの `CreateValidatedConfig` ヘルパーは `PiperConfig` を生成して `ToValidated()` を呼ぶため、ValidatedPiperConfig のプロパティを直接参照していない。全テストがそのまま通過することを確認する。

**PiperConfigTest（変更不要の確認）**:

PiperConfig 自体は変更なし。`ToValidated()` の呼び出しは引き続き動作する。

### 4.4 E2E テスト

| テスト | 内容 |
|-------|------|
| CI `unity-tests.yml` 全通過 | EditMode + PlayMode テストの GREEN 確認 |
| `dotnet format --verify-no-changes` | コードフォーマットチェック通過 |

---

## 5. 懸念事項とレビュー項目

### 5.1 技術的懸念

| 懸念 | 深刻度 | 対策 |
|------|--------|------|
| **breaking change: 全プロパティアクセスパス変更** | 低 | 実際の消費者は TTSSynthesisOrchestrator の 2 箇所のみ。PiperTTS.cs は `_config`（PiperConfig）を直接参照しており `_validatedConfig` のプロパティは直接アクセスしていない。影響範囲は極めて限定的 |
| **C# プロパティパターンのネスト制約** | 低 | `config is { EnablePhonemeSilence: true }` のパターンマッチングが 1 階層のみ対応。ネスト時は `config is { Silence: { EnablePhonemeSilence: true } }` とするか、通常のプロパティアクセスに変更。後者を推奨（可読性が高い） |
| **IReadOnlyDictionary / IReadOnlyList を含む record struct** | 低 | record struct の値等価性は参照型フィールドの参照等価で判定される。テストで構造的等価性を前提とする比較は行われていないため問題なし |
| **GPUInferenceSettings が参照型のまま** | 低 | `InferenceSettings` record struct 内に参照型の `GPUInferenceSettings` を保持する。PiperConfig 側が `[Serializable]` class として使用しているため、ValidatedPiperConfig 側のみの struct 化は別途検討。本チケットでは既存型をそのまま使用 |
| **P3-3 との ValidatedPiperConfig コンストラクタ競合** | 中 | P3-1 と P3-3 は同じコンストラクタを変更する。同一エージェントが順次実施を強く推奨 |

### 5.2 レビューチェックリスト

- [ ] 6 つの `readonly record struct` が `uPiper.Core` namespace に定義されているか
- [ ] 旧 22 トップレベル get-only プロパティが全て削除されているか
- [ ] `ValidatedPiperConfig` が `Language`, `Performance`, `Inference`, `Audio`, `Silence`, `General` の 6 プロパティを公開しているか
- [ ] コンストラクタ内で各 record struct が正しく初期化されているか
- [ ] `SupportedLanguages` の null ガード（`?? Array.Empty<string>()`）が維持されているか
- [ ] `ParsedPhonemeSilence` が `EnablePhonemeSilence` に応じて条件分岐しているか
- [ ] `GPUSettings` の null ガード（デフォルト `MaxMemoryMB = 512`）が維持されているか
- [ ] `TTSSynthesisOrchestrator` の `_config?.ParsedPhonemeSilence` が `_config?.Silence.ParsedPhonemeSilence` に更新されているか
- [ ] `TTSSynthesisOrchestrator` の `EnablePhonemeSilence` アクセスが `Silence.EnablePhonemeSilence` に更新されているか
- [ ] `TTSSynthesisOrchestratorTests` が変更なしで全テスト通過するか
- [ ] `PiperConfigTest` が全テスト通過するか
- [ ] `PiperConfig.ToValidated()` のシグネチャが変更されていないか
- [ ] `dotnet format --verify-no-changes` が通過するか
- [ ] record struct の定義が `ValidatedPiperConfig.cs` 内にあるか（ファイル分離されていないか）

---

## 6. 一から作り直すとしたら

### 6.1 Config 設計パターン全体の再考

現在の uPiper の設定管理は 3 層構造を取る:

1. **PiperConfig**: `[Serializable]` + public フィールド。Unity Inspector で編集可能。可変。
2. **ValidatedPiperConfig**: get-only プロパティの不変スナップショット。`PiperConfig.ToValidated()` で生成。
3. **（P3-2 で追加予定）IPiperConfigReadOnly**: 読み取り専用インターフェース。

この 3 層構造は、Unity Inspector の `[Serializable]` 制約（public フィールドが必要）と、ランタイムの不変性保証（バリデーション済みスナップショット）を両立させるための設計である。

**もしゼロから設計するなら**: Unity の `[Serializable]` 制約がなければ、`PiperConfig` と `ValidatedPiperConfig` を統合し、Builder パターンで構築する設計が最もクリーンである:

```csharp
// ゼロベース設計
public sealed class PiperConfig
{
    public LanguageSettings Language { get; }
    public PerformanceSettings Performance { get; }
    // ...

    private PiperConfig() { }

    public sealed class Builder
    {
        public string DefaultLanguage { get; set; } = "ja";
        public int MaxCacheSizeMB { get; set; } = 256;
        // ...

        public PiperConfig Build()
        {
            // バリデーション + クランプ + 不変オブジェクト生成を一箇所で実施
            return new PiperConfig
            {
                Language = new LanguageSettings(
                    DefaultLanguage: DefaultLanguage.ToLowerInvariant(),
                    // ...
                ),
                // ...
            };
        }
    }
}
```

この設計では:
- `PiperConfig` 自体が不変（`ValidatedPiperConfig` が不要）
- Builder がバリデーション + クランプを `Build()` 内で実施（`Validate()` メソッドが不要）
- Inspector 互換は `ScriptableObject` + カスタムエディタで対応

しかし、Unity Inspector の `[Serializable]` 制約は uPiper の使い勝手に直結する機能であり、Inspector から設定を変更 -> 再初期化するワークフローを維持する必要がある。現状の 3 層構造は Unity エコシステムとの統合を優先した実務的判断として妥当である。

### 6.2 record struct vs record class の選択

本チケットでは `readonly record struct`（値型）を採用するが、以下の代替案も検討に値する:

**案 A: `record class`（参照型）**
- メリット: null 許容型として自然。`IReadOnlyDictionary` / `IReadOnlyList` 等の参照型プロパティとの整合性が高い。継承による拡張が可能。
- デメリット: ヒープ割り当てが発生（6 オブジェクト）。ValidatedPiperConfig は TTS 初期化時に 1 回だけ生成されるため実質的な影響はないが、「不変の設定値グループ」という意味論に対して値型の方が適切。
- **判定**: 不採用。設定値のグループは値セマンティクスが自然であり、record struct が適切。

**案 B: 通常の `readonly struct`（record なし）**
- メリット: C# 9 以前でも使用可能。record の `ToString()` / `Equals()` 自動生成に依存しない。
- デメリット: 構造的等価性を手動実装する必要がある（テストで比較を行う場合）。positional パラメータによるコンパクトな宣言ができない。
- **判定**: 不採用。uPiper は Unity 2022.3+（C# 10+）を要件としており、record struct の利点（宣言の簡潔さ、自動等価性）を活用すべき。

**採用判断**: `readonly record struct` が最も適切。不変性・値セマンティクス・宣言の簡潔さ・自動等価性の全てを満たす。

### 6.3 6 カテゴリ分類の妥当性

現在のコメントベースの 6 カテゴリ分類はそのまま採用しているが、別の分類も考えられる:

**代替案: ライフサイクル基準の分類**
- **InitOnly**: Backend, AllowFallbackToCPU, EnableWarmup, WarmupIterations, GPUSettings（初期化時のみ参照）
- **Runtime**: SampleRate, NormalizeAudio, TargetRMSLevel, EnablePhonemeSilence, ParsedPhonemeSilence（推論時に参照）
- **LanguageRouting**: DefaultLanguage, AutoDetectLanguage, SupportedLanguages, MixedLanguageMode（言語検出時に参照）
- **Threading**: WorkerThreads, EnableMultiThreadedInference, InferenceBatchSize（スレッド管理）
- **Diagnostics**: EnableDebugLogging, TimeoutMs, MaxCacheSizeMB, EnablePhonemeCache（運用）

この分類は「いつ・どこで参照されるか」を基準としており、消費者視点では合理的である。しかし:
- 既存のソースコメント分類と一致しないため、マイグレーションコストが増大する
- 1 つのコンポーネントが複数カテゴリを参照するパターンは変わらない
- P3-2 のインターフェース設計では、消費者が必要なカテゴリだけを受け取る設計にはならない（インターフェース全体を受け取る）

**結論**: 既存のコメントベース 6 カテゴリをそのまま型に昇格させる方が、コスト最小かつ認知負荷が低い。

### 6.4 互換プロパティ（ファサード）の不採用判断

v1.x の利用者向けに、旧フラットプロパティを互換ファサードとして残す案も検討した:

```csharp
// 互換プロパティ例（不採用）
[Obsolete("Use Silence.EnablePhonemeSilence")]
public bool EnablePhonemeSilence => Silence.EnablePhonemeSilence;
```

**不採用理由**: ValidatedPiperConfig のプロパティに直接アクセスするコードは TTSSynthesisOrchestrator の 2 箇所のみであり、互換プロパティの保守コスト（22 個の `[Obsolete]` ファサード + 将来の削除作業）に見合わない。一括移行で十分。

### 6.5 P3-1 単体の価値 vs Config 三部作の統合実施

P3-1 は P3-2（IPiperConfigReadOnly）・P3-3（Validate() 純粋関数化）の前提であり、三部作として同時実施するのが最も効率的である。しかし P3-1 単体でも以下の独立した価値がある:

- 22 プロパティのフラット構造から 6 カテゴリの型レベル分類への移行
- `ValidatedPiperConfig` のコード可読性向上
- record struct の構造的等価性によるテスト容易性向上

P3-1 を単体でマージしても、後続の P3-2・P3-3 に対する blocking dependency が解消されるため、Config 三部作全体の並行性が向上する。

---

## 7. 後続タスクへの連絡事項

### P3-3（Validate() 純粋関数化）への引き継ぎ

1. **ValidatedPiperConfig コンストラクタが record struct 初期化に書き換え済み**。P3-3 では `PiperConfig.Validate()` のクランプ・正規化ロジックをこのコンストラクタ内に移動する。各 record struct のコンストラクタ引数に対してクランプを適用する形になる。
2. **PiperConfig.Validate() のシグネチャは本チケットでは変更しない**。P3-3 で `[Obsolete]` 付与 + 副作用排除を行う。
3. **P3-1 と P3-3 は同じ ValidatedPiperConfig コンストラクタを変更する**。同一エージェントが順次実施を強く推奨。P3-1 のコンストラクタ変更が P3-3 の移動先の受け皿となるため、P3-1 を先に完了させる必要がある。

### P3-2（IPiperConfigReadOnly インターフェース）への引き継ぎ

1. **6 つの record struct 型が `uPiper.Core` namespace で公開済み**。P3-2 では `IPiperConfigReadOnly` インターフェースを定義し、これらの型をプロパティとして参照する。
2. **`ValidatedPiperConfig` の 6 つのネストプロパティ名が確定済み**（`Language`, `Performance`, `Inference`, `Audio`, `Silence`, `General`）。P3-2 ではこれらと同名のプロパティをインターフェースに定義し、`ValidatedPiperConfig : IPiperConfigReadOnly` として実装する。
3. **TTSSynthesisOrchestrator のアクセスパスは P3-1 で変更済み**。P3-2 ではフィールドの型のみを `ValidatedPiperConfig` -> `IPiperConfigReadOnly` に変更する。アクセスパス自体の再変更は不要。

### マイグレーションガイド（将来の参照用）

PiperTTS.cs が将来 `_validatedConfig` 経由でプロパティにアクセスする場合のマッピング:

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
