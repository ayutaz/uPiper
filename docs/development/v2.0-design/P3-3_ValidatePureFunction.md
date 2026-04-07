# P3-3: Validate() 純粋関数化

## 1. 概要

`PiperConfig.Validate()` の副作用（フィールド直接書き換え）を排除し、純粋関数化する。
戻り値で `ValidatedPiperConfig` を返し、元の `PiperConfig` インスタンスは一切変更しない。

**現状の問題点**:
- `Validate()` が `PiperConfig` のフィールドを直接変更する（クランプ・正規化・自動検出値の代入等）
- `ToValidated()` 内部で `Validate()` を呼ぶため、呼び出し元の `PiperConfig` も暗黙的に変更される
- XML doc に副作用警告を追加済みだが、API としての安全性は不十分

---

## 2. 現状分析

### 2.1 Validate() の全副作用リスト

`PiperConfig.Validate()` (L208-323) のフィールド別分類:

| # | フィールド | 副作用の種類 | 条件 | 変更内容 | 行 |
|---|-----------|-------------|------|---------|-----|
| 1 | `MaxCacheSizeMB` | **クランプ (下限)** | `< 10` | `= 10` | L213-215 |
| 2 | `MaxCacheSizeMB` | **クランプ (上限)** | `> 500` | `= 500` | L218-220 |
| 3 | `SampleRate` | **例外** | `< 8000 \|\| > 48000` | `PiperException` | L224-226 |
| 4 | `SampleRate` | 警告のみ | 非標準値 | ログ出力 (変更なし) | L228-231 |
| 5 | `WorkerThreads` | **例外** | `< 0` | `PiperException` | L236-238 |
| 6 | `WorkerThreads` | **自動検出代入** | `== 0` | `= Max(1, processorCount - 1)` | L239-243 |
| 7 | `WorkerThreads` | 警告のみ | `> 16` | ログ出力 (変更なし) | L244-247 |
| 8 | `DefaultLanguage` | **例外** | `null/empty/whitespace` | `PiperException` | L250-253 |
| 9 | `DefaultLanguage` | **正規化** | 常時 | `.ToLowerInvariant().Trim()` | L255 |
| 10 | `DefaultLanguage` | 警告のみ | 長さ != 2 && != 5 | ログ出力 (変更なし) | L256-259 |
| 11 | `TimeoutMs` | **例外** | `< 0` | `PiperException` | L262-265 |
| 12 | `TimeoutMs` | 警告のみ | `0 < x < 1000` | ログ出力 (変更なし) | L267-270 |
| 13 | `InferenceBatchSize` | **クランプ (下限)** | `< 1` | `= 1` | L274-277 |
| 14 | `InferenceBatchSize` | **クランプ (上限)** | `> 32` | `= 32` | L279-282 |
| 15 | `TargetRMSLevel` | **クランプ (上限)** | `NormalizeAudio && > 0` | `= 0` | L287-290 |
| 16 | `TargetRMSLevel` | **クランプ (下限)** | `NormalizeAudio && < -40` | `= -40` | L292-295 |
| 17 | `WarmupIterations` | **クランプ (下限)** | `EnableWarmup && < 1` | `= 1` | L300-304 |
| 18 | `PhonemeSilenceSpec` | **例外** | `EnablePhonemeSilence && パース失敗` | `PiperException` | L308-316 |
| 19 | `GPUSettings.*` | **クランプ** | `GPUSettings != null` | `GPUSettings.Validate()` 委譲 | L320 |

### 2.2 副作用の分類サマリ

| 種類 | 件数 | 対象フィールド |
|------|------|---------------|
| **フィールド書き換え (クランプ)** | 7 | MaxCacheSizeMB(2), InferenceBatchSize(2), TargetRMSLevel(2), WarmupIterations(1) |
| **フィールド書き換え (正規化)** | 1 | DefaultLanguage |
| **フィールド書き換え (自動検出)** | 1 | WorkerThreads |
| **サブオブジェクト書き換え** | 1 | GPUSettings.MaxMemoryMB |
| **例外スロー** | 4 | SampleRate, WorkerThreads, DefaultLanguage, TimeoutMs, PhonemeSilenceSpec |
| **警告ログのみ** | 5 | (フィールド変更なし) |

### 2.3 GPUInferenceSettings.Validate() の副作用

```csharp
// GPUInferenceSettings.cs L22-25
public void Validate()
{
    MaxMemoryMB = Mathf.Clamp(MaxMemoryMB, 128, 2048);
}
```

| フィールド | 副作用 | 条件 | 変更内容 |
|-----------|--------|------|---------|
| `MaxMemoryMB` | **クランプ** | `< 128 \|\| > 2048` | `Clamp(128, 2048)` |

### 2.4 ToValidated() の現在の動作

```csharp
// PiperConfig.cs L331-335
public ValidatedPiperConfig ToValidated()
{
    Validate();                        // ← PiperConfig のフィールドを直接変更
    return new ValidatedPiperConfig(this);  // ← 変更後の値でスナップショット
}
```

`ValidatedPiperConfig` コンストラクタ (L55-91) は `source` の各フィールドをコピーするだけで、追加のクランプ・正規化は行わない (PhonemeSilence のパースのみ例外)。

---

## 3. 純粋関数化設計

### 3.1 方針

`PiperConfig.Validate()` を廃止し、`ToValidated()` を唯一の検証エントリポイントとする。
クランプ・正規化ロジックを `ValidatedPiperConfig` コンストラクタ内に移動し、`PiperConfig` のフィールドは一切変更しない。

### 3.2 API 変更

```csharp
// ── Before ──
public class PiperConfig
{
    public void Validate()                    // 副作用あり
    public ValidatedPiperConfig ToValidated()  // 内部で Validate() を呼ぶ
}

// ── After ──
public class PiperConfig
{
    [Obsolete("Use ToValidated() instead. This method will be removed in v3.0.")]
    public void Validate() { /* 非推奨ラッパー */ }

    public ValidatedPiperConfig ToValidated()  // 純粋関数。PiperConfig を変更しない。
}
```

### 3.3 Validate() の Obsolete 戦略

`Validate()` を即削除するとテスト・外部利用コードの破壊的変更になるため、段階的に非推奨化する:

1. **v2.0**: `[Obsolete]` 属性を付与。内部実装は `ToValidated()` に委譲（戻り値は破棄）。フィールド書き換えは行わない。
2. **v3.0**: `Validate()` メソッドを完全削除。

```csharp
[Obsolete("Use ToValidated() instead. Validate() no longer modifies fields. Will be removed in v3.0.")]
public void Validate()
{
    // フィールドを変更しない。バリデーションエラーの例外は従来通りスローする。
    // クランプ・正規化は行わず、例外チェックのみ実施。
    ValidateThrowOnly();
}
```

### 3.4 ValidatedPiperConfig コンストラクタへのロジック移動

**現在** `Validate()` にある「フィールド書き換え」ロジックを `ValidatedPiperConfig` コンストラクタに移動する。
`PiperConfig` のフィールドは読み取り専用で参照し、正規化・クランプした結果を `ValidatedPiperConfig` のプロパティに設定する。

```csharp
internal ValidatedPiperConfig(PiperConfig source)
{
    // ── バリデーション (例外スロー) ──
    if (string.IsNullOrWhiteSpace(source.DefaultLanguage))
        throw new PiperException("DefaultLanguage cannot be null or empty");
    if (source.SampleRate < 8000 || source.SampleRate > 48000)
        throw new PiperException($"Invalid sample rate: {source.SampleRate}Hz...");
    if (source.WorkerThreads < 0)
        throw new PiperException($"Invalid WorkerThreads: {source.WorkerThreads}...");
    if (source.TimeoutMs < 0)
        throw new PiperException($"Invalid TimeoutMs: {source.TimeoutMs}...");
    if (source.EnablePhonemeSilence)
    {
        try { AudioGeneration.PhonemeSilenceProcessor.Parse(source.PhonemeSilenceSpec); }
        catch (ArgumentException ex)
        { throw new PiperException($"Invalid PhonemeSilenceSpec: {ex.Message}", ex); }
    }

    // ── 正規化 (source を変更せずコピー時に正規化) ──
    DefaultLanguage = source.DefaultLanguage.ToLowerInvariant().Trim();

    // ── クランプ (source を変更せずクランプ済み値を設定) ──
    MaxCacheSizeMB = Mathf.Clamp(source.MaxCacheSizeMB, 10, 500);
    WorkerThreads = source.WorkerThreads == 0
        ? Mathf.Max(1, SystemInfo.processorCount - 1)
        : source.WorkerThreads;
    InferenceBatchSize = Mathf.Clamp(source.InferenceBatchSize, 1, 32);
    WarmupIterations = source.EnableWarmup && source.WarmupIterations < 1
        ? 1 : source.WarmupIterations;
    TargetRMSLevel = source.NormalizeAudio
        ? Mathf.Clamp(source.TargetRMSLevel, -40f, 0f)
        : source.TargetRMSLevel;

    // ── GPU 設定クランプ ──
    GPUSettings = new GPUInferenceSettings
    {
        MaxMemoryMB = source.GPUSettings != null
            ? Mathf.Clamp(source.GPUSettings.MaxMemoryMB, 128, 2048)
            : 512
    };

    // ── そのままコピー ──
    AutoDetectLanguage = source.AutoDetectLanguage;
    SupportedLanguages = source.SupportedLanguages != null
        ? new List<string>(source.SupportedLanguages)
        : Array.Empty<string>();
    MixedLanguageMode = source.MixedLanguageMode;
    EnablePhonemeCache = source.EnablePhonemeCache;
    EnableMultiThreadedInference = source.EnableMultiThreadedInference;
    Backend = source.Backend;
    EnableWarmup = source.EnableWarmup;
    AllowFallbackToCPU = source.AllowFallbackToCPU;
    SampleRate = source.SampleRate;
    NormalizeAudio = source.NormalizeAudio;
    EnablePhonemeSilence = source.EnablePhonemeSilence;
    PhonemeSilenceSpec = source.PhonemeSilenceSpec;
    ParsedPhonemeSilence = source.EnablePhonemeSilence
        ? AudioGeneration.PhonemeSilenceProcessor.Parse(source.PhonemeSilenceSpec)
        : null;
    EnableDebugLogging = source.EnableDebugLogging;
    TimeoutMs = source.TimeoutMs;

    // ── 警告ログ (副作用だがログ出力のみ。source は変更しない) ──
    if (source.MaxCacheSizeMB < 10)
        PiperLogger.LogWarning("MaxCacheSizeMB too small ({0}MB), clamped to 10MB", source.MaxCacheSizeMB);
    else if (source.MaxCacheSizeMB > 500)
        PiperLogger.LogWarning("MaxCacheSizeMB too large ({0}MB), clamped to 500MB", source.MaxCacheSizeMB);
    if (source.WorkerThreads == 0)
        PiperLogger.LogInfo("Auto-detected {0} worker threads", WorkerThreads);
    else if (source.WorkerThreads > 16)
        PiperLogger.LogWarning("WorkerThreads ({0}) exceeds recommended maximum of 16", source.WorkerThreads);
    // ... (他の警告ログも同様)

    PiperLogger.LogInfo("PiperConfig validated successfully");
}
```

### 3.5 ToValidated() の新実装

```csharp
/// <summary>
/// この PiperConfig を検証し、不変スナップショットとして返す。
/// PiperConfig のフィールドは一切変更しない（純粋関数）。
/// </summary>
/// <returns>バリデーション済みの不変設定オブジェクト</returns>
/// <exception cref="PiperException">設定値が不正な場合</exception>
public ValidatedPiperConfig ToValidated()
{
    return new ValidatedPiperConfig(this);
    // Validate() を呼ばない。ロジックは ValidatedPiperConfig コンストラクタに移動済み。
}
```

---

## 4. バリデーションエラーの扱い

### 4.1 選択肢の比較

| 方式 | メリット | デメリット | 採用 |
|------|---------|-----------|------|
| **例外 (現行方式)** | 不正な設定で動作しない安全性。既存コードとの互換性 | 呼び出し元で try-catch が必要 | **採用** |
| Result型 (`Result<ValidatedPiperConfig, ValidationError>`) | 関数型スタイル。エラーの明示的なハンドリング | C# の標準ではない。Unity との親和性低 | 不採用 |
| ログ + 全クランプ (例外なし) | 呼び出し元が簡潔 | 無効値を黙って修正するのは危険 (例: SampleRate=0) | 不採用 |

### 4.2 採用方針: 例外 + クランプのハイブリッド (現行踏襲)

- **致命的エラー** (値として意味をなさない): `PiperException` をスロー
  - `SampleRate` 範囲外、`WorkerThreads` 負値、`DefaultLanguage` null/空、`TimeoutMs` 負値、`PhonemeSilenceSpec` パースエラー
- **範囲外だが妥当なデフォルトに修正可能**: クランプしてログ警告
  - `MaxCacheSizeMB`, `InferenceBatchSize`, `TargetRMSLevel`, `WarmupIterations`, `GPUSettings.MaxMemoryMB`
- **自動検出**: 特殊値 (0) を実行時に解決
  - `WorkerThreads = 0` → `processorCount - 1`

この分類は現行の `Validate()` と同一。変更点は「クランプ・正規化の実行場所」のみ (PiperConfig → ValidatedPiperConfig)。

---

## 5. 呼び出し箇所の変更一覧

### 5.1 Runtime コード

| ファイル | 現在の呼び出し | 変更内容 |
|---------|---------------|---------|
| `PiperConfig.cs` L333 | `Validate(); return new ValidatedPiperConfig(this);` | `return new ValidatedPiperConfig(this);` に簡略化 |
| `PiperConfig.cs` L208 | `public void Validate()` | `[Obsolete]` 付与。例外チェックのみ残す (クランプ・書き換えロジック削除) |
| `PiperTTS.cs` L268 | `_validatedConfig = _config.ToValidated();` | **変更不要** (ToValidated() 経由のため) |
| `PiperTTS.Inference.cs` L59 | `_validatedConfig` 参照 | **変更不要** |
| `TTSSynthesisOrchestrator.cs` L21,29 | `ValidatedPiperConfig` 受取 | **変更不要** |
| `GPUInferenceSettings.cs` L22 | `public void Validate()` | `[Obsolete]` 付与 or 削除。クランプロジックは ValidatedPiperConfig に移動 |
| `MultilingualPhonemizer.cs` L92 | `options.Validate()` | **変更不要** (別クラスの Validate。PiperConfig とは無関係) |
| `PiperTTS.cs` L353 | `voice.Validate()` | **変更不要** (PiperVoiceConfig の Validate。PiperConfig とは無関係) |

### 5.2 テストコード

| ファイル | テスト数 | 変更内容 |
|---------|---------|---------|
| `PiperConfigTest.cs` | 27テスト | `config.Validate()` → `config.ToValidated()` に変更。副作用アサーション (config のフィールド値チェック) を `ValidatedPiperConfig` のプロパティチェックに変更 |
| `GPUInferenceSettingsTest.cs` | 1テスト | `settings.Validate()` を直接テストから `ToValidated()` 経由テストに変更 |
| `InferenceBackendTest.cs` | 1テスト | `config.Validate()` → `config.ToValidated()` |
| `TTSSynthesisOrchestratorTests.cs` | 7テスト | **変更不要** (既に `piperConfig.ToValidated()` を使用) |

### 5.3 テスト変更の具体例

```csharp
// ── Before ──
[Test]
public void Validate_AdjustsInvalidCacheSize()
{
    var config = new PiperConfig { MaxCacheSizeMB = 5 };
    config.Validate();
    Assert.AreEqual(10, config.MaxCacheSizeMB);  // PiperConfig 自体が変更されたことを検証
}

// ── After ──
[Test]
public void ToValidated_ClampsInvalidCacheSize()
{
    var config = new PiperConfig { MaxCacheSizeMB = 5 };
    var validated = config.ToValidated();
    Assert.AreEqual(5, config.MaxCacheSizeMB);      // PiperConfig は変更されない
    Assert.AreEqual(10, validated.MaxCacheSizeMB);   // ValidatedPiperConfig でクランプ済み
}

// ── Before ──
[Test]
public void Validate_SetsWorkerThreadsAutomatically()
{
    var config = new PiperConfig { WorkerThreads = 0 };
    config.Validate();
    Assert.Greater(config.WorkerThreads, 0);  // PiperConfig が変更された
}

// ── After ──
[Test]
public void ToValidated_SetsWorkerThreadsAutomatically()
{
    var config = new PiperConfig { WorkerThreads = 0 };
    var validated = config.ToValidated();
    Assert.AreEqual(0, config.WorkerThreads);         // PiperConfig は変更されない
    Assert.Greater(validated.WorkerThreads, 0);        // ValidatedPiperConfig で自動検出
}

// ── Before (例外テスト) ──
[Test]
public void Validate_ThrowsForInvalidSampleRate()
{
    var config = new PiperConfig { SampleRate = 7999 };
    Assert.Throws<PiperException>(() => config.Validate());
}

// ── After (例外テスト) ──
[Test]
public void ToValidated_ThrowsForInvalidSampleRate()
{
    var config = new PiperConfig { SampleRate = 7999 };
    Assert.Throws<PiperException>(() => config.ToValidated());
}
```

---

## 6. 定数の配置

現在 `PiperConfig` の `#region Constants` にある定数 (MinCacheSizeMB, MaxCacheSizeMBThreshold 等) は、バリデーションロジックとともに `ValidatedPiperConfig` に移動する。

### 6.1 移動する定数一覧

| 定数 | 現在値 | 移動先 |
|------|--------|--------|
| `MinCacheSizeMB` | 10 | `ValidatedPiperConfig` |
| `MaxCacheSizeMBThreshold` | 500 | `ValidatedPiperConfig` |
| `MinSampleRate` | 8000 | `ValidatedPiperConfig` |
| `MaxSampleRate` | 48000 | `ValidatedPiperConfig` |
| `MaxWorkerThreads` | 16 | `ValidatedPiperConfig` |
| `MinRecommendedTimeoutMs` | 1000 | `ValidatedPiperConfig` |
| `MinBatchSize` | 1 | `ValidatedPiperConfig` |
| `MaxBatchSize` | 32 | `ValidatedPiperConfig` |
| `MaxRMSLevel` | 0f | `ValidatedPiperConfig` |
| `MinRMSLevel` | -40f | `ValidatedPiperConfig` |

`[Obsolete] Validate()` 内の例外チェックでも同じ定数が必要なため、`internal const` として `ValidatedPiperConfig` に定義し、`PiperConfig.Validate()` から参照する。

---

## 7. P3-1 との関係

### 7.1 依存方向

P3-1 (ValidatedPiperConfig ネスト構造化) は `ValidatedPiperConfig` の構造を変更する。P3-3 は `ValidatedPiperConfig` コンストラクタのロジックを変更する。両者は同じファイル・同じコンストラクタに影響するため、**同時に実施すると効率的**。

### 7.2 P3-1 先行の場合

P3-1 が先に完了している場合、P3-3 では以下が変わる:

- クランプ・正規化ロジックの移動先が、ネスト record struct のコンストラクタになる
- 例: `MaxCacheSizeMB` のクランプは `PerformanceSettings` record struct のコンストラクタ内
- `ValidatedPiperConfig` のトップレベルコンストラクタは各ネスト record を組み立てるだけ

```csharp
// P3-1 後の ValidatedPiperConfig コンストラクタ (P3-3 適用後)
internal ValidatedPiperConfig(PiperConfig source)
{
    Language = new LanguageSettings(
        source.DefaultLanguage.ToLowerInvariant().Trim(),
        source.AutoDetectLanguage,
        source.SupportedLanguages,
        source.MixedLanguageMode);

    Performance = new PerformanceSettings(
        Mathf.Clamp(source.MaxCacheSizeMB, 10, 500),
        source.EnablePhonemeCache,
        source.WorkerThreads == 0 ? Mathf.Max(1, SystemInfo.processorCount - 1) : source.WorkerThreads,
        source.EnableMultiThreadedInference,
        Mathf.Clamp(source.InferenceBatchSize, 1, 32));
    // ...
}
```

### 7.3 推奨実施順序

1. **P3-1** (ネスト構造化) -- 構造変更のみ、ロジック変更なし
2. **P3-3** (Validate 純粋関数化) -- P3-1 の構造にクランプ・正規化ロジックを移動
3. **P3-2** (IPiperConfigReadOnly) -- P3-1 のネスト構造をインターフェース化

---

## 8. テスト戦略

### 8.1 新規テスト

| テスト名 | 検証内容 |
|---------|---------|
| `ToValidated_DoesNotModifyOriginalConfig` | 全フィールドについて、`ToValidated()` 呼び出し後に `PiperConfig` のフィールドが変更されていないことを検証 |
| `ToValidated_ClampsMaxCacheSizeMB_Low` | `MaxCacheSizeMB=5` → validated=10, original=5 |
| `ToValidated_ClampsMaxCacheSizeMB_High` | `MaxCacheSizeMB=1000` → validated=500, original=1000 |
| `ToValidated_ClampsInferenceBatchSize_Low` | `InferenceBatchSize=0` → validated=1, original=0 |
| `ToValidated_ClampsInferenceBatchSize_High` | `InferenceBatchSize=50` → validated=32, original=50 |
| `ToValidated_ClampsTargetRMSLevel_High` | `TargetRMSLevel=10` → validated=0, original=10 |
| `ToValidated_ClampsTargetRMSLevel_Low` | `TargetRMSLevel=-50` → validated=-40, original=-50 |
| `ToValidated_ClampsWarmupIterations` | `WarmupIterations=0, EnableWarmup=true` → validated=1, original=0 |
| `ToValidated_NormalizesDefaultLanguage` | `DefaultLanguage=" JA "` → validated="ja", original=" JA " |
| `ToValidated_AutoDetectsWorkerThreads` | `WorkerThreads=0` → validated>0, original=0 |
| `ToValidated_ClampsGPUMaxMemoryMB` | GPUSettings.MaxMemoryMB 範囲外 → validated はクランプ済み |
| `ToValidated_Idempotent` | 同一 PiperConfig から複数回 `ToValidated()` を呼んでも同じ結果 |

### 8.2 既存テストの移行

27件の `PiperConfigTest.Validate_*` テストを以下のパターンで移行:

- **クランプテスト** (7件): `config.Validate()` + `Assert(config.Field == clampedValue)` → `config.ToValidated()` + `Assert(validated.Field == clampedValue)` + `Assert(config.Field == originalValue)`
- **例外テスト** (5件): `Assert.Throws(() => config.Validate())` → `Assert.Throws(() => config.ToValidated())`
- **警告テスト** (5件): `LogAssert.Expect(...)` + `config.Validate()` → `LogAssert.Expect(...)` + `config.ToValidated()`
- **正常系テスト** (6件): `Assert.DoesNotThrow(() => config.Validate())` → `Assert.DoesNotThrow(() => config.ToValidated())`
- **デフォルト値テスト** (4件): 変更不要 (Validate を呼ばない)

### 8.3 回帰テスト

- `TTSSynthesisOrchestratorTests` の `CreateValidatedConfig()` ヘルパーは既に `piperConfig.ToValidated()` を使用しているため、変更不要
- `InferenceBackendTest.GPUSettings_AreValidatedWithConfig` は `config.Validate()` → `config.ToValidated()` に変更

---

## 9. 実装手順

### Step 1: ValidatedPiperConfig コンストラクタにロジック移動

- バリデーション (例外スロー) + クランプ + 正規化ロジックを `ValidatedPiperConfig(PiperConfig source)` に移動
- 定数を `ValidatedPiperConfig` に移動 (`internal const`)
- 警告ログもコンストラクタ内で出力

### Step 2: PiperConfig.ToValidated() の簡略化

- `Validate()` 呼び出しを削除し、直接 `new ValidatedPiperConfig(this)` を返す
- XML doc を更新 (「PiperConfig のフィールドは変更しない」明記)

### Step 3: PiperConfig.Validate() の Obsolete 化

- `[Obsolete]` 属性を付与
- 内部実装を「例外チェックのみ」に縮退 (クランプ・正規化ロジック削除)
- または `ToValidated()` に委譲して戻り値を破棄 (既存コードの挙動互換)

### Step 4: GPUInferenceSettings.Validate() の扱い

- `GPUInferenceSettings.Validate()` のクランプロジックは `ValidatedPiperConfig` コンストラクタに統合
- `GPUInferenceSettings.Validate()` 自体は `[Obsolete]` or 削除

### Step 5: テスト移行

- `PiperConfigTest` の全 `Validate_*` テストを `ToValidated_*` に書き換え
- 「PiperConfig が変更されないこと」のアサーションを追加
- 新規テスト (8.1 参照) を追加

### Step 6: ドキュメント更新

- `ARCHITECTURE_ja.md` / `ARCHITECTURE_en.md` の副作用警告を「純粋関数」に更新

---

## 10. 破壊的変更の影響

### 10.1 API 互換性

| 変更 | 影響 | 対策 |
|------|------|------|
| `Validate()` が `[Obsolete]` に | コンパイル警告 | v3.0 まで維持 |
| `Validate()` がフィールドを変更しなくなる | `Validate()` 後に変更されたフィールドを参照していたコードが壊れる | `ToValidated()` の戻り値を使うよう移行ガイド提供 |
| `GPUInferenceSettings.Validate()` の `[Obsolete]` | テスト・外部コードに影響 | 同上 |

### 10.2 リスク

- `Validate()` でフィールドが変更されることに依存している外部コードがある場合、挙動が変わる
- 現在のテストは `config.Validate(); Assert(config.Field == clampedValue)` パターンが多いため、テストの全面書き換えが必要
- `PiperTTS` コンストラクタは `_config.ToValidated()` を使用しているため Runtime コードへの影響は限定的

---

## 11. 完了の定義

- [ ] `PiperConfig.Validate()` に `[Obsolete]` 属性が付与されている
- [ ] `PiperConfig.Validate()` がフィールドを一切変更しない
- [ ] `PiperConfig.ToValidated()` が内部で `Validate()` を呼ばない
- [ ] クランプ・正規化・自動検出ロジックが `ValidatedPiperConfig` コンストラクタに移動済み
- [ ] 例外スロー条件は現行と同一
- [ ] `GPUInferenceSettings.Validate()` のクランプが `ValidatedPiperConfig` に統合済み
- [ ] 全テストが `ToValidated()` 経由に移行済み
- [ ] 「PiperConfig が変更されないこと」を検証するテストが追加済み
- [ ] `ToValidated()` の冪等性テストが追加済み
- [ ] `ARCHITECTURE_ja.md` / `ARCHITECTURE_en.md` の記述が更新済み
- [ ] dotnet format チェックが通る
