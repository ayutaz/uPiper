# P3-3: Validate() 純粋関数化

**マイルストーン**: M3 - Data Model + Config
**優先度**: P1
**見積もり**: 1 人日
**依存チケット**: P3-1（ValidatedPiperConfig ネスト構造化）
**後続チケット**: P3-2（IPiperConfigReadOnly インターフェース）
**ブランチ名**: `feature/v2.0-P3-3-validate-pure-function`

---

## 1. タスク目的とゴール

`PiperConfig.Validate()` の副作用（フィールド直接書き換え）を排除し、`ToValidated()` を唯一のバリデーションエントリポイントとする純粋関数化を実施する。

**解決する問題**:

1. **副作用による暗黙的なフィールド変更**: `Validate()` が `PiperConfig` のフィールドを直接変更する（クランプ・正規化・自動検出値の代入等）。19個の副作用が存在し、呼び出し元の `PiperConfig` インスタンスが暗黙的に変更される。
2. **ToValidated() 経由の副作用**: `ToValidated()` 内部で `Validate()` を呼ぶため、スナップショット生成のつもりが元の設定も変更してしまう。
3. **テストの脆弱性**: テストが `config.Validate(); Assert(config.Field == clampedValue)` パターンに依存しており、副作用の存在を前提としている。

**完了状態（Definition of Done）**:

- `PiperConfig.Validate()` に `[Obsolete]` 属性が付与されている
- `PiperConfig.Validate()` がフィールドを一切変更しない
- `PiperConfig.ToValidated()` が内部で `Validate()` を呼ばない
- クランプ・正規化・自動検出ロジックが `ValidatedPiperConfig` コンストラクタに移動済み
- 例外スロー条件は現行と同一
- `GPUInferenceSettings.Validate()` のクランプが `ValidatedPiperConfig` に統合済み
- 全テストが `ToValidated()` 経由に移行済み
- 「PiperConfig が変更されないこと」を検証するテストが追加済み
- `ToValidated()` の冪等性テストが追加済み
- `ARCHITECTURE_ja.md` / `ARCHITECTURE_en.md` の記述が更新済み
- `dotnet format --verify-no-changes` 通過

---

## 2. 実装する内容の詳細

### 2.1 19副作用の具体的な対処計画

`PiperConfig.Validate()` (L208-323) に存在する19個の副作用を、種類別に対処する。

#### フィールド書き換え（クランプ）: 7件 → ValidatedPiperConfig コンストラクタに移動

| # | フィールド | 条件 | 現在の副作用 | 移動先での処理 |
|---|-----------|------|------------|---------------|
| 1 | `MaxCacheSizeMB` | `< 10` | `= 10` | `Mathf.Clamp(source.MaxCacheSizeMB, 10, 500)` |
| 2 | `MaxCacheSizeMB` | `> 500` | `= 500` | 同上（Clamp で上限も処理） |
| 13 | `InferenceBatchSize` | `< 1` | `= 1` | `Mathf.Clamp(source.InferenceBatchSize, 1, 32)` |
| 14 | `InferenceBatchSize` | `> 32` | `= 32` | 同上 |
| 15 | `TargetRMSLevel` | `NormalizeAudio && > 0` | `= 0` | `Mathf.Clamp(source.TargetRMSLevel, -40f, 0f)` |
| 16 | `TargetRMSLevel` | `NormalizeAudio && < -40` | `= -40` | 同上 |
| 17 | `WarmupIterations` | `EnableWarmup && < 1` | `= 1` | 条件付きクランプ |

#### フィールド書き換え（正規化）: 1件 → ValidatedPiperConfig コンストラクタに移動

| # | フィールド | 現在の副作用 | 移動先での処理 |
|---|-----------|------------|---------------|
| 9 | `DefaultLanguage` | `.ToLowerInvariant().Trim()` | `source.DefaultLanguage.ToLowerInvariant().Trim()` |

#### フィールド書き換え（自動検出）: 1件 → ValidatedPiperConfig コンストラクタに移動

| # | フィールド | 条件 | 現在の副作用 | 移動先での処理 |
|---|-----------|------|------------|---------------|
| 6 | `WorkerThreads` | `== 0` | `= Max(1, processorCount - 1)` | 三項演算子で自動検出 |

#### サブオブジェクト書き換え: 1件 → ValidatedPiperConfig コンストラクタに移動

| # | フィールド | 現在の副作用 | 移動先での処理 |
|---|-----------|------------|---------------|
| 19 | `GPUSettings.MaxMemoryMB` | `GPUSettings.Validate()` 委譲 | `Mathf.Clamp(source.GPUSettings.MaxMemoryMB, 128, 2048)` |

#### 例外スロー: 4件 → ValidatedPiperConfig コンストラクタ冒頭に移動

| # | フィールド | 条件 | 例外 |
|---|-----------|------|------|
| 3 | `SampleRate` | `< 8000 \|\| > 48000` | `PiperException` |
| 5 | `WorkerThreads` | `< 0` | `PiperException` |
| 8 | `DefaultLanguage` | `null/empty/whitespace` | `PiperException` |
| 11 | `TimeoutMs` | `< 0` | `PiperException` |
| 18 | `PhonemeSilenceSpec` | `EnablePhonemeSilence && パース失敗` | `PiperException` |

#### 警告ログのみ: 5件 → ValidatedPiperConfig コンストラクタ末尾に移動

| # | フィールド | 条件 | 処理 |
|---|-----------|------|------|
| 4 | `SampleRate` | 非標準値 | `PiperLogger.LogWarning(...)` |
| 7 | `WorkerThreads` | `> 16` | `PiperLogger.LogWarning(...)` |
| 10 | `DefaultLanguage` | 長さ != 2 && != 5 | `PiperLogger.LogWarning(...)` |
| 12 | `TimeoutMs` | `0 < x < 1000` | `PiperLogger.LogWarning(...)` |
| (implicit) | `MaxCacheSizeMB` | クランプ発動時 | `PiperLogger.LogWarning(...)` |

### Step 1: ValidatedPiperConfig コンストラクタにロジック移動

対象ファイル: `Assets/uPiper/Runtime/Core/ValidatedPiperConfig.cs`

P3-1 で導入されたネスト record struct のコンストラクタ呼び出し内でクランプ・正規化を実施する:

```csharp
internal ValidatedPiperConfig(PiperConfig source)
{
    // ── バリデーション (例外スロー) ──
    if (string.IsNullOrWhiteSpace(source.DefaultLanguage))
        throw new PiperException("DefaultLanguage cannot be null or empty");
    if (source.SampleRate < MinSampleRate || source.SampleRate > MaxSampleRate)
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

    // ── ネスト record 構築（正規化・クランプ込み） ──
    Language = new LanguageSettings(
        source.DefaultLanguage.ToLowerInvariant().Trim(),  // 正規化
        source.AutoDetectLanguage,
        source.SupportedLanguages,
        source.MixedLanguageMode);

    Performance = new PerformanceSettings(
        Mathf.Clamp(source.MaxCacheSizeMB, MinCacheSizeMB, MaxCacheSizeMBThreshold),  // クランプ
        source.EnablePhonemeCache,
        source.WorkerThreads == 0
            ? Mathf.Max(1, SystemInfo.processorCount - 1)  // 自動検出
            : source.WorkerThreads,
        source.EnableMultiThreadedInference,
        Mathf.Clamp(source.InferenceBatchSize, MinBatchSize, MaxBatchSize));  // クランプ

    Inference = new InferenceSettings(
        source.Backend,
        source.EnableWarmup,
        source.EnableWarmup && source.WarmupIterations < 1 ? 1 : source.WarmupIterations,  // クランプ
        source.AllowFallbackToCPU,
        source.GPUSettings != null
            ? new GPUInferenceSettings
              { MaxMemoryMB = Mathf.Clamp(source.GPUSettings.MaxMemoryMB, 128, 2048) }  // GPUクランプ
            : new GPUInferenceSettings());

    Audio = new AudioSettings(
        source.SampleRate,
        source.NormalizeAudio,
        source.NormalizeAudio
            ? Mathf.Clamp(source.TargetRMSLevel, MinRMSLevel, MaxRMSLevel)  // クランプ
            : source.TargetRMSLevel);

    Silence = new SilenceSettings(
        source.EnablePhonemeSilence,
        source.PhonemeSilenceSpec,
        source.EnablePhonemeSilence
            ? AudioGeneration.PhonemeSilenceProcessor.Parse(source.PhonemeSilenceSpec)
            : null);

    General = new GeneralSettings(
        source.EnableDebugLogging,
        source.TimeoutMs);

    // ── 警告ログ (source を変更せず、クランプ発動時にログ出力) ──
    if (source.MaxCacheSizeMB < MinCacheSizeMB)
        PiperLogger.LogWarning("MaxCacheSizeMB too small ({0}MB), clamped to {1}MB",
            source.MaxCacheSizeMB, MinCacheSizeMB);
    else if (source.MaxCacheSizeMB > MaxCacheSizeMBThreshold)
        PiperLogger.LogWarning("MaxCacheSizeMB too large ({0}MB), clamped to {1}MB",
            source.MaxCacheSizeMB, MaxCacheSizeMBThreshold);
    if (source.WorkerThreads == 0)
        PiperLogger.LogInfo("Auto-detected {0} worker threads", Performance.WorkerThreads);
    else if (source.WorkerThreads > MaxWorkerThreads)
        PiperLogger.LogWarning("WorkerThreads ({0}) exceeds recommended maximum of {1}",
            source.WorkerThreads, MaxWorkerThreads);
    // ... (他の警告ログも同様)

    PiperLogger.LogInfo("PiperConfig validated successfully");
}
```

### Step 2: 定数の移動

対象ファイル: `Assets/uPiper/Runtime/Core/ValidatedPiperConfig.cs`

`PiperConfig` の `#region Constants` にある定数を `ValidatedPiperConfig` に移動する:

| 定数 | 現在値 | アクセス修飾子 |
|------|--------|--------------|
| `MinCacheSizeMB` | 10 | `internal const` |
| `MaxCacheSizeMBThreshold` | 500 | `internal const` |
| `MinSampleRate` | 8000 | `internal const` |
| `MaxSampleRate` | 48000 | `internal const` |
| `MaxWorkerThreads` | 16 | `internal const` |
| `MinRecommendedTimeoutMs` | 1000 | `internal const` |
| `MinBatchSize` | 1 | `internal const` |
| `MaxBatchSize` | 32 | `internal const` |
| `MaxRMSLevel` | 0f | `internal const` |
| `MinRMSLevel` | -40f | `internal const` |

`internal const` とすることで、`[Obsolete] Validate()` の例外チェックや外部テストからも参照可能。

### Step 3: PiperConfig.ToValidated() の簡略化

対象ファイル: `Assets/uPiper/Runtime/Core/PiperConfig.cs`

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

### Step 4: PiperConfig.Validate() の Obsolete 化

対象ファイル: `Assets/uPiper/Runtime/Core/PiperConfig.cs`

```csharp
[Obsolete("Use ToValidated() instead. Validate() no longer modifies fields. Will be removed in v3.0.")]
public void Validate()
{
    // フィールドを変更しない。バリデーションエラーの例外は従来通りスローする。
    // クランプ・正規化ロジックは削除済み。
    ValidateThrowOnly();
}

/// <summary>
/// 例外チェックのみ実施する内部メソッド。フィールドは一切変更しない。
/// </summary>
private void ValidateThrowOnly()
{
    if (string.IsNullOrWhiteSpace(DefaultLanguage))
        throw new PiperException("DefaultLanguage cannot be null or empty");
    if (SampleRate < ValidatedPiperConfig.MinSampleRate
        || SampleRate > ValidatedPiperConfig.MaxSampleRate)
        throw new PiperException($"Invalid sample rate: {SampleRate}Hz...");
    if (WorkerThreads < 0)
        throw new PiperException($"Invalid WorkerThreads: {WorkerThreads}...");
    if (TimeoutMs < 0)
        throw new PiperException($"Invalid TimeoutMs: {TimeoutMs}...");
    if (EnablePhonemeSilence)
    {
        try { AudioGeneration.PhonemeSilenceProcessor.Parse(PhonemeSilenceSpec); }
        catch (ArgumentException ex)
        { throw new PiperException($"Invalid PhonemeSilenceSpec: {ex.Message}", ex); }
    }
}
```

### Step 5: GPUInferenceSettings.Validate() の Obsolete 化

対象ファイル: `Assets/uPiper/Runtime/Core/GPUInferenceSettings.cs`

```csharp
[Obsolete("GPU settings are now validated within ValidatedPiperConfig. Will be removed in v3.0.")]
public void Validate()
{
    // クランプロジックは ValidatedPiperConfig コンストラクタに移動済み。
    // 後方互換のためメソッドは残すが、フィールドは変更しない。
}
```

### Step 6: テスト移行

対象ファイル: `Assets/uPiper/Tests/Editor/PiperConfigTest.cs` 他

#### クランプテスト（7件）: 副作用アサーションを ValidatedPiperConfig プロパティチェックに変更

```csharp
// Before:
[Test]
public void Validate_AdjustsInvalidCacheSize()
{
    var config = new PiperConfig { MaxCacheSizeMB = 5 };
    config.Validate();
    Assert.AreEqual(10, config.MaxCacheSizeMB);
}

// After:
[Test]
public void ToValidated_ClampsInvalidCacheSize()
{
    var config = new PiperConfig { MaxCacheSizeMB = 5 };
    var validated = config.ToValidated();
    Assert.AreEqual(5, config.MaxCacheSizeMB);                       // PiperConfig は変更されない
    Assert.AreEqual(10, validated.Performance.MaxCacheSizeMB);        // ValidatedPiperConfig でクランプ済み
}
```

#### 例外テスト（5件）: Validate() → ToValidated() に変更

```csharp
// Before:
Assert.Throws<PiperException>(() => config.Validate());

// After:
Assert.Throws<PiperException>(() => config.ToValidated());
```

#### 警告テスト（5件）: LogAssert + Validate() → LogAssert + ToValidated() に変更

```csharp
// Before:
LogAssert.Expect(LogType.Warning, ...);
config.Validate();

// After:
LogAssert.Expect(LogType.Warning, ...);
config.ToValidated();
```

#### 正常系テスト（6件）: Validate() → ToValidated() に変更

```csharp
// Before:
Assert.DoesNotThrow(() => config.Validate());

// After:
Assert.DoesNotThrow(() => config.ToValidated());
```

#### GPUInferenceSettingsTest（1件）: Validate() → ToValidated() 経由に変更

```csharp
// Before:
settings.Validate();
Assert.AreEqual(128, settings.MaxMemoryMB);

// After:
var config = new PiperConfig { GPUSettings = settings };
var validated = config.ToValidated();
Assert.AreEqual(originalValue, settings.MaxMemoryMB);   // 元は変更されない
Assert.AreEqual(128, validated.Inference.GPUSettings.MaxMemoryMB);  // クランプ済み
```

#### InferenceBackendTest（1件）: Validate() → ToValidated() に変更

#### 新規テスト（追加）

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
| `ToValidated_ClampsGPUMaxMemoryMB` | GPUSettings.MaxMemoryMB 範囲外 → validated はクランプ済み、元は変更なし |
| `ToValidated_Idempotent` | 同一 PiperConfig から複数回 `ToValidated()` を呼んでも同じ結果 |

### Step 7: ドキュメント更新

- `ARCHITECTURE_ja.md` / `ARCHITECTURE_en.md`: `Validate()` の副作用警告を「純粋関数」に更新
- `CHANGELOG.md`: 破壊的変更として `Validate()` の `[Obsolete]` 化を記載

---

## 3. エージェントチームの役割と人数

**1エージェント構成**（1 人日）-- P3-1 と同一エージェントが順次実施を強く推奨

| 役割 | 担当内容 | 工数 |
|------|---------|------|
| 実装エージェント | Step 1-7 の全実装 | 1 人日 |

**理由**: P3-3 は `ValidatedPiperConfig` コンストラクタのロジックを大幅に変更する。P3-1 で同コンストラクタの構造を変更した直後に実施するため、同一エージェントが文脈を保持した状態で作業する方が効率的。テスト移行（27件）が工数の大部分を占めるが、パターンが定型的であるため機械的に実施可能。

**推奨実施順序**: P3-1 完了 → Step 1 → Step 2 → Step 3 → Step 4 → Step 5 → 全テスト実行 → Step 6 → Step 7 → P3-2 に着手

---

## 4. 提供範囲とテスト項目

### 4.1 提供範囲

| カテゴリ | 範囲内 | 範囲外 |
|---------|-------|-------|
| PiperConfig.Validate() | `[Obsolete]` 化、副作用除去 | 完全削除（v3.0 で実施） |
| ValidatedPiperConfig | コンストラクタにクランプ・正規化ロジック移動、定数移動 | プロパティ構造変更（P3-1 で実施済み） |
| GPUInferenceSettings.Validate() | `[Obsolete]` 化、クランプロジック統合 | 完全削除（v3.0 で実施） |
| PiperConfig.ToValidated() | Validate() 呼び出し除去、XML doc 更新 | シグネチャ変更 |
| PiperConfigTest | 27テストの `ToValidated()` パターン移行 + 新規12テスト追加 | テストの大規模リファクタ |
| GPUInferenceSettingsTest | `ToValidated()` 経由テストに変更 | テスト追加 |
| ランタイムコード（PiperTTS 等） | **変更なし**（既に `ToValidated()` 経由） | 呼び出し箇所の変更 |
| MultilingualPhonemizer | **変更なし**（`options.Validate()` は別クラスの Validate） | 他クラスの Validate の純粋関数化 |
| ドキュメント | ARCHITECTURE 更新、CHANGELOG 記載 | CLAUDE.md の大規模書き換え |

### 4.2 Unit テスト

**PiperConfigTest（既存27テスト移行 + 新規12テスト）**:

移行パターン別の件数:

| パターン | 件数 | 変更内容 |
|---------|------|---------|
| クランプテスト | 7 | `config.Validate()` + `Assert(config.Field)` → `config.ToValidated()` + `Assert(validated.Prop)` + `Assert(config.Field == original)` |
| 例外テスト | 5 | `Assert.Throws(() => config.Validate())` → `Assert.Throws(() => config.ToValidated())` |
| 警告テスト | 5 | `LogAssert.Expect(...); config.Validate()` → `LogAssert.Expect(...); config.ToValidated()` |
| 正常系テスト | 6 | `Assert.DoesNotThrow(() => config.Validate())` → `Assert.DoesNotThrow(() => config.ToValidated())` |
| デフォルト値テスト | 4 | **変更不要**（Validate を呼ばない） |

**新規テスト（12件）**: セクション 2 Step 6 の表を参照。

**回帰テスト（変更不要の確認）**:

| テストファイル | 変更不要の理由 |
|--------------|--------------|
| `TTSSynthesisOrchestratorTests.cs` | 既に `piperConfig.ToValidated()` を使用 |
| `MultilingualPhonemizer*Tests.cs` | `options.Validate()` は `MultilingualPhonemizerOptions.Validate()` であり、PiperConfig とは無関係 |

### 4.3 E2E テスト

| テスト | 内容 |
|-------|------|
| CI `unity-tests.yml` 全通過 | EditMode + PlayMode テストの GREEN 確認 |
| `InferenceEngineDemo` 手動実行 | 6言語ドロップダウンで音声生成が正常に動作することを確認（PiperTTS は既に `ToValidated()` 経由のため、回帰確認のみ） |

---

## 5. 懸念事項とレビュー項目

### 5.1 技術的懸念

| 懸念 | 深刻度 | 対策 |
|------|--------|------|
| **Validate() の副作用に依存する外部コード** | 中 | `[Obsolete]` 警告で段階的に移行を促す。v3.0 で完全削除。`Validate()` がフィールドを変更しなくなるため、`config.Validate(); use(config.Field)` パターンのコードは挙動が変わる |
| **テスト27件の全面書き換えによる回帰リスク** | 中 | 変更パターンが定型的（4パターン）であり、機械的に実施可能。各テストで「元の PiperConfig が変更されないこと」のアサーションを追加することで安全性を強化 |
| **ValidatedPiperConfig コンストラクタの肥大化** | 低 | P3-1 のネスト record struct 構築 + P3-3 のクランプ・正規化ロジックが同一コンストラクタに集約される。ただし、ロジック自体は単純なクランプ・条件分岐のため、可読性は許容範囲 |
| **PhonemeSilenceSpec の二重パース** | 低 | バリデーション（例外チェック）とプロパティ設定の両方で `Parse()` を呼ぶ。パフォーマンスへの影響は無視可能（初期化時1回のみ）だが、結果をキャッシュして1回のパースで済ませる最適化も可能 |
| **P3-1 との ValidatedPiperConfig.cs 同時変更** | 中 | 同一エージェントが P3-1 → P3-3 の順で実施し、マージコンフリクトを回避 |
| **GPUInferenceSettings.Validate() の [Obsolete] 化による外部影響** | 低 | `GPUInferenceSettings` は `[Serializable]` クラスであり、外部から `Validate()` を直接呼ぶケースは稀。テスト1件のみ影響 |

### 5.2 レビューチェックリスト

- [ ] `PiperConfig.Validate()` に `[Obsolete]` 属性が付与されているか
- [ ] `PiperConfig.Validate()` 内部でフィールドの書き換え（代入）が一切行われていないか
- [ ] `PiperConfig.ToValidated()` 内部で `Validate()` が呼ばれていないか
- [ ] `ValidatedPiperConfig` コンストラクタに全19副作用のロジック（クランプ・正規化・自動検出・例外・警告ログ）が移動しているか
- [ ] 例外スロー条件が現行 `Validate()` と同一か（条件値・例外メッセージの一致確認）
- [ ] 定数（`MinCacheSizeMB`, `MaxCacheSizeMBThreshold` 等）が `ValidatedPiperConfig` に移動し、`internal const` で定義されているか
- [ ] `PiperConfig` 側の旧定数が削除されているか（または `[Obsolete]` 経由で `ValidatedPiperConfig` の定数を参照しているか）
- [ ] `GPUInferenceSettings.Validate()` に `[Obsolete]` 属性が付与されているか
- [ ] `GPUInferenceSettings.Validate()` 内部でフィールドの書き換えが行われていないか
- [ ] `PiperConfigTest` の全27テストが `ToValidated()` パターンに移行されているか
- [ ] 全クランプテストで「PiperConfig が変更されないこと」のアサーションが追加されているか
- [ ] `ToValidated_DoesNotModifyOriginalConfig` テストが全フィールドを網羅しているか
- [ ] `ToValidated_Idempotent` テストが追加されているか
- [ ] `ARCHITECTURE_ja.md` / `ARCHITECTURE_en.md` が更新されているか
- [ ] `dotnet format --verify-no-changes` が通過するか

---

## 6. 一から作り直すとしたら

### 6.1 バリデーション戦略全体の設計

P3-3 の対象は `PiperConfig.Validate()` という単一メソッドの純粋関数化だが、根本的に問うべきは「`PiperConfig` と `ValidatedPiperConfig` の二層構造は最適か」という設計判断である。

**現行設計の前提**:
- `PiperConfig` は Unity Inspector で編集するための `[Serializable]` クラス（public フィールド、可変）
- `ValidatedPiperConfig` はバリデーション済みの不変スナップショット
- `ToValidated()` が変換メソッドとして両者を橋渡しする

**ゼロベースの代替案**:

#### 案 A: Builder パターン

```csharp
public class PiperConfigBuilder
{
    public PiperConfigBuilder SetDefaultLanguage(string lang) { ... return this; }
    public PiperConfigBuilder SetMaxCacheSizeMB(int mb) { ... return this; }
    // ...
    public ValidatedPiperConfig Build()  // 純粋関数。Builder は変更しない。
    {
        return new ValidatedPiperConfig(/* クランプ・正規化済みの値 */);
    }
}
```

- **メリット**: 設定の構築プロセスが明確。`Build()` が唯一の検証ポイント。Inspector バインドは `PiperConfigBuilder` の `[Serializable]` ラッパーで対応可能。
- **デメリット**: Unity Inspector との統合が複雑化。`[Serializable]` + public フィールドの `PiperConfig` は Unity エコシステムの標準パターンであり、Builder に置き換えるとユーザーの学習コストが増大。
- **結論**: Unity プラグインとしては不自然。ライブラリではなくフレームワーク寄りの設計であり、uPiper のユースケースにはオーバーキル。

#### 案 B: `PiperConfig` 自体を immutable にする

```csharp
public sealed class PiperConfig  // Serializable ではない
{
    public string DefaultLanguage { get; }
    public int MaxCacheSizeMB { get; }
    // ... 全プロパティ get-only

    public PiperConfig(string defaultLanguage, int maxCacheSizeMB, ...)
    {
        // コンストラクタでクランプ・正規化・例外チェック
        DefaultLanguage = defaultLanguage?.ToLowerInvariant().Trim()
            ?? throw new PiperException("...");
        MaxCacheSizeMB = Mathf.Clamp(maxCacheSizeMB, 10, 500);
        // ...
    }
}
```

- **メリット**: 二層構造が不要。`PiperConfig` がそのまま不変設定。`ValidatedPiperConfig` クラスが消える。
- **デメリット**: Unity Inspector でフィールドを編集できなくなる（`[Serializable]` + public フィールドが必須）。`ScriptableObject` + カスタムエディタで代替可能だが、実装コストが大きい。
- **結論**: 理想的だが、Unity の `[Serializable]` 制約との両立が困難。v3.0 以降で `ScriptableObject` ベースの設定に移行する場合に検討。

#### 案 C: Validation Result 型（関数型アプローチ）

```csharp
public static class PiperConfigValidator
{
    public static Result<ValidatedPiperConfig, IReadOnlyList<ValidationError>>
        Validate(PiperConfig config)
    {
        var errors = new List<ValidationError>();
        if (string.IsNullOrWhiteSpace(config.DefaultLanguage))
            errors.Add(new ValidationError("DefaultLanguage", "Cannot be null or empty"));
        // ...
        if (errors.Count > 0)
            return Result.Failure(errors);
        return Result.Success(new ValidatedPiperConfig(config));
    }
}
```

- **メリット**: エラーの集約的な報告（1つ目の例外で止まらず、全エラーを一覧化）。関数型スタイルで副作用なし。
- **デメリット**: C# の標準パターンではない。`Result<T, E>` 型を自前定義する必要がある（`OneOf` ライブラリ等は Unity との互換性に懸念）。Unity 開発者にとって馴染みが薄い。
- **結論**: 設計としては優れているが、C# + Unity エコシステムとの親和性が低い。`PiperException` による例外ベースのバリデーション（現行方式）が Unity プラグインとしては最も自然。

### 6.2 採用する設計（P3-3）

上記の検討を踏まえ、P3-3 では **現行の二層構造（PiperConfig + ValidatedPiperConfig）を維持しつつ、副作用を排除する** 方針を採用する。これは:

1. Unity Inspector 互換性を維持する（`PiperConfig` は `[Serializable]` + public フィールドのまま）
2. 例外ベースのバリデーションを維持する（Unity 開発者にとって自然）
3. クランプ・正規化の実行場所を `PiperConfig` から `ValidatedPiperConfig` に移動する（副作用排除の核心）
4. `Validate()` を `[Obsolete]` として段階的に廃止する（後方互換性）

### 6.3 現設計の正直な弱点

1. **ValidatedPiperConfig コンストラクタの責務過多**: P3-3 完了後、コンストラクタが「例外チェック + クランプ + 正規化 + 自動検出 + 警告ログ + ネスト record 構築」を全て担う。単一責務原則の観点からは、バリデーション・正規化・構築を分離すべき。ただし、分離するとコンストラクタ呼び出しの複雑度が上がり、「`new ValidatedPiperConfig(source)` 一発で完結」という簡潔さが失われる。
2. **PhonemeSilenceSpec の二重パース**: 例外チェック時とプロパティ設定時で `Parse()` を2回呼ぶ。コンストラクタ内でローカル変数にキャッシュすれば解決するが、`Parse()` の実行コストが低い（文字列パースのみ、初期化時1回）ため、可読性を優先して二重呼び出しを許容する判断も妥当。
3. **Validate() の Obsolete 期間（v2.0 → v3.0）の保守コスト**: `[Obsolete] Validate()` と `ValidatedPiperConfig` コンストラクタの例外チェックロジックが重複する。定数は共有するが、条件分岐のロジックは2箇所にコピーが存在する状態。v3.0 での `Validate()` 削除まで、条件変更時に2箇所の同期が必要。
4. **テスト移行のコスト**: 27件のテスト書き換え + 12件の新規テスト。パターンは定型的だが、工数の約50%がテスト移行に費やされる。

### 6.4 Phase 2+3 統合設計考察

#### P3-1 との統合実施の判断

P3-3 は P3-1 と同じ `ValidatedPiperConfig.cs` のコンストラクタを変更する。実装順序（P3-1 → P3-3）が固定されており、同一エージェントの順次実施が強く推奨されている。P3-1 と統合すべきかの判断は以下の通り:

**P3-1 + P3-3 統合を推奨する条件**:
- M3 のスケジュールが逼迫し、個別 PR のレビュー・マージ待ちが並行性の利点を上回る場合
- P3-1 のコンストラクタ構造変更と P3-3 のクランプ・正規化ロジック移動を同時にレビューした方が、最終形が見えやすい場合

**現行の分離を維持する理由**:
- P3-1 は「構造変更」（22 プロパティ → 6 ネスト record struct）、P3-3 は「ロジック移動」（19 副作用の排除）と、設計判断の性質が異なる。分離することで各 PR のレビュー焦点が明確になる。
- P3-1 単体でマージした後、P3-3 の作業中に問題が発覚した場合、P3-1 だけロールバック可能。統合 PR だとロールバック単位が大きくなる。

#### P3-3 のバリデーション定数が Phase 2 チケットに与える影響

P3-3 で `ValidatedPiperConfig` に移動する定数（`MinSampleRate`, `MaxSampleRate`, `MinCacheSizeMB` 等）は Phase 2 チケット（P2-1, P2-2）とは無関係である。P2-1 の `PhonemeIdMap` は `PiperVoiceConfig`（`PiperConfig` ではない）のフィールドであり、`Validate()` のバリデーション対象外。P2-2 の Prosody データもバリデーション対象外。

したがって、P3-3 は Phase 2 チケットと完全に独立しており、相互参照の追加は不要。

#### Group C（P3-5/P3-4）との設計一貫性

P3-3 は「ロジックの適切な場所への移動」という点で、P3-5（AudioNormalizer 切り出し）・P3-4（BackendSelector 切り出し）と同じリファクタリング原則に基づく。共通原則:

1. **副作用の排除/局所化**: P3-3 は `Validate()` の副作用を排除、P3-5/P3-4 は混在した責務を分離
2. **テスト容易性の向上**: P3-3 は `ToValidated()` 経由のテストパターン統一、P3-4 は `PlatformInfo` による注入可能な依存
3. **ロジック変更なし**: 3 チケットとも移動先でのアルゴリズム変更は行わない（振る舞い不変の保証）

ただし P3-3 は「メソッド間移動」（同一クラス内の `Validate()` → コンストラクタ）、P3-5/P3-4 は「クラス間移動」（別クラスへの切り出し）と、移動の粒度が異なる。P3-3 で移動先のコンストラクタが肥大化する弱点（6.3 項 1 番）は、P3-5/P3-4 の「static クラスへの分離」パターンでは発生しない。

---

## 7. ファイル一覧

### 変更

| ファイル | 変更内容 |
|---------|---------|
| `Runtime/Core/ValidatedPiperConfig.cs` | コンストラクタにクランプ・正規化・例外チェック・警告ログのロジック移動。定数（10個）を追加。P3-1 のネスト record 構築にクランプ済み値を使用。 |
| `Runtime/Core/PiperConfig.cs` | `Validate()` に `[Obsolete]` 付与、内部を例外チェックのみに縮退。`ToValidated()` から `Validate()` 呼び出しを除去。`ValidateThrowOnly()` private メソッド追加。旧定数の扱い（削除 or `[Obsolete]` 委譲）。 |
| `Runtime/Core/GPUInferenceSettings.cs` | `Validate()` に `[Obsolete]` 付与、クランプロジック削除。 |
| `Tests/Editor/PiperConfigTest.cs` | 27テストの `ToValidated()` パターン移行 + 新規12テスト追加。 |
| `Tests/Editor/GPUInferenceSettingsTest.cs` | `Validate()` → `ToValidated()` 経由テストに変更。 |
| `Tests/Editor/InferenceBackendTest.cs` | `config.Validate()` → `config.ToValidated()` に変更。 |
| `docs/ARCHITECTURE_ja.md` | 副作用警告を「純粋関数」に更新。 |
| `docs/ARCHITECTURE_en.md` | 同上（英語版）。 |

### 変更なし

| ファイル | 理由 |
|---------|------|
| `Runtime/Core/PiperTTS.cs` | 既に `_config.ToValidated()` を使用。`Validate()` を直接呼んでいない。 |
| `Runtime/Core/PiperTTS.Inference.cs` | `_validatedConfig` 参照のみ。変更不要。 |
| `Runtime/Core/AudioGeneration/TTSSynthesisOrchestrator.cs` | `ValidatedPiperConfig` 受取のまま。P3-2 で `IPiperConfigReadOnly` に変更。 |
| `Runtime/Core/Phonemizers/Multilingual/MultilingualPhonemizer.cs` | `options.Validate()` は `MultilingualPhonemizerOptions.Validate()` であり、`PiperConfig.Validate()` とは無関係。 |
| `Tests/Editor/AudioGeneration/TTSSynthesisOrchestratorTests.cs` | 既に `piperConfig.ToValidated()` を使用。変更不要。 |
