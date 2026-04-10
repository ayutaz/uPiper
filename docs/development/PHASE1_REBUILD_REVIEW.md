# Phase 1「ゼロから作り直すとしたら」エージェントチームレビュー

> 実施日: 2026-04-11
> 対象: feature/phase1-critical ブランチ (T-P1-001 〜 T-P1-004)

---

## レビュー参加者

| 役割 | 観点 |
|------|------|
| アーキテクト | パイプライン設計、バリデーション戦略、プラットフォーム抽象化、テスタビリティ |
| API設計者 / DXスペシャリスト | ユーザー体験、API表面積、設定の複雑さ、非同期制約 |
| テスト/品質エンジニア | テストカバレッジ、脆弱性、テストダブル、テストピラミッド |

---

## 1. 初期化パイプライン

### 現状の課題（全エージェント一致）

現在 **5つの初期化パス** が混在している:

| パス | コード | 問題 |
|------|--------|------|
| A | `CreateAsync()` | InitializeAsync → LoadDefaultVoiceAsync → InitializeWithInferenceAsync（二重バリデーション） |
| B | `CreateAsync(config)` | 同上 |
| C | `CreateAsync(config, voiceConfig)` | InitializeAsync → LoadVoiceAsync |
| D | `new PiperTTS()` → `InitializeAsync()` → `LoadVoiceAsync()` | 手動3ステップ |
| E | `new PiperTTS()` → `InitializeWithInferenceAsync()` | Inference直接 |

- バリデーション・プラットフォーム初期化が2回実行される（パスA/B）
- `_isInitialized` フラグが2箇所でセットされ、状態遷移が不明確
- `LoadVoiceAsync` 内の `_inferenceGenerator ??= new InferenceAudioGenerator()` と `InitializeWithInferenceAsync` の `ShortTextMitigatingGenerator` デコレータラップに初期化の非一貫性がある

### 理想の設計

**アーキテクト提案**: 単一パイプライン + Builderパターン

```
PiperTTSBuilder.Create()
  .WithConfig(config)
  .WithModel(modelAsset, voiceConfig)
  .BuildAsync(ct)
    → [1] Validate（環境+設定+モデル+音素マップ、一括）
    → [2] Platform Init（冪等、1回限り）
    → [3] Core Init（PuaTokenMapper, PhonemeEncoder, InferenceGenerator）
    → [4] Optional Init（多言語Phonemizer, キャッシュ, ワーカープール）
    → return PiperTTS（不変の初期化済みインスタンス）
```

**API設計者提案**: `CreateAsync()` を唯一の公開入口にし、コンストラクタを `internal` に

```csharp
// 理想: 2行で完結
var tts = await PiperTTS.CreateAsync();
var clip = await tts.SpeakAsync("こんにちは");
```

### 今回採用しなかった理由

- `IPiperTTS.InitializeAsync` がインターフェースに定義済み → 削除は破壊的変更
- `InferenceEngineDemo` 等の既存コードが3ステップパターンに依存
- Phase 1 の目的は「クリティカル修正」であり、パイプライン再設計はリファクタリングフェーズの責務

---

## 2. バリデーション戦略

### 現状の課題

- `InitializationValidator` が `static class` → DI不可、テストでモック差し替え不可
- `object modelAsset` キャスト → `Unity.InferenceEngine` への依存回避だが型安全性を犠牲
- `HandleValidationResult` がバリデーション結果を `PiperInitializationException` にフラット化 → 構造化情報の損失

### 理想の設計

**アーキテクト**: Composite Validator パターン

```csharp
interface IInitializationValidator { InitializationValidationResult Validate(context); }
// RuntimeEnvironmentValidator, ModelAssetValidator, PhonemeIdMapValidator ...
// CompositeValidator が全ルールを集約
```

**API設計者**: `ValidationResult` を例外に添付してユーザーに公開

```csharp
// 案1: TryCreateAsync パターン
var result = await PiperTTS.TryCreateAsync();
if (!result.IsSuccess)
    foreach (var e in result.Error.ValidationResult.Entries) { ... }

// 案2: 例外に ValidationResult を添付
catch (PiperInitializationException ex)
    foreach (var e in ex.ValidationResult.Entries) { ... }
```

### 今回採用しなかった理由

- バリデーションルール数は7-8個 → static class の方がシンプル
- `object modelAsset` は呼び出し元が型チェック済みのため実害なし
- 構造化情報の保持は Phase 2 で対応可能

---

## 3. エラーハンドリング

### 現状の課題（全エージェント指摘）

1. **二重通知**: `OnError` イベント + 例外スロー → 呼び出し元が両方ハンドルすると重複
2. **`PiperErrorCode` と派生例外クラスの二重分類**: `catch (PiperInferenceException)` と `catch (PiperException ex) when (ex.ErrorCode == InferenceFailed)` が等価
3. **例外の汎用ラップ**: `catch (Exception) → throw new PiperException(...)` で元の型情報が失われるリスク（Phase 1 で `catch (PiperException) { throw; }` を追加して部分修正済み）

### 理想の設計

- **例外一本化** + `OnError` は「観測用」（ログ、テレメトリ、UI通知）に限定
- `PiperErrorCode` enum 廃止 → 派生クラスの型分類に一本化
- `IProgress<SynthesisProgress>` パターンで進捗・言語検出を統合

### 今回採用しなかった理由

- `PiperErrorCode` は public API として公開済み → 破壊的変更
- `OnError` イベントの契約明確化はドキュメント修正で対応可能

---

## 4. プラットフォーム抽象化

### 現状の課題

- `BackendSelector` はプリプロセッサフリー設計を達成済み
- しかし `InitializePlatformAudioSession` / `ValidatePlatformSpecific` / `InitializePhonemizerAsync` は `#if` プリプロセッサ直書き

### 理想の設計

```csharp
interface IPlatformInitializer {
    void InitializeAudioSession();
    IReadOnlyList<Entry> ValidatePlatform();
}
// IOSPlatformInitializer, WebGLPlatformInitializer, DefaultPlatformInitializer
// PlatformInitializerFactory.Create(platformInfo) で解決
```

### 今回採用しなかった理由

- iOS の `IOSAudioSessionHelper` はネイティブプラグイン依存 → コンパイル時の `#if` が必須
- `ValidatePlatformSpecific` の `#if` は `PlatformInfo.RuntimePlatform` 分岐で代替可能 → Phase 2 で対応

---

## 5. テスト品質

### テストエンジニアの指摘事項

#### 不足しているテスト（優先度順）

| 優先度 | テスト | 理由 |
|--------|--------|------|
| **高** | PhonemeEncoder 境界値50%ちょうど | 閾値の `>` vs `>=` が意図通りか未検証 |
| **高** | CancellationToken 事前キャンセル | 全 public async メソッドの基本契約 |
| **高** | Dispose 後のメソッド呼び出し | ObjectDisposedException の保証 |
| **中** | HandleValidationResult 直接テスト | Warning ログ + Error 例外の動作確認 |
| **中** | ProsodyFlat 長さ不一致 | 境界外アクセスの安全性 |
| **低** | 並行アクセス | `_lockObject` の有効性検証 |

#### テスト保守性の課題

- `CreateMinimalPhonemeIdMap()` が **3ファイルに異なる内容で重複定義**
- テスト用 `PiperVoiceConfig` の構築が `PhonemeEncoder` の内部実装に結合
- JSON テストデータがテストコードにハードコード

#### 改善提案

```
Assets/uPiper/Tests/Editor/TestHelpers/
  ├── TestVoiceConfigFactory.cs   // PiperVoiceConfig ファクトリ統一
  ├── TestPhonemeIdMapFactory.cs  // PhonemeIdMap の Minimal/Valid/Full
  └── TestJsonTemplates.cs        // テスト用JSON定数
```

---

## 6. API 表面積の問題（API設計者の主要指摘）

### 現状

- `IPiperTTS` インターフェース: **メソッド17個 + イベント4個** → 過多
- `GenerateAudioAsync` に3オーバーロード + `GenerateAudioWithInferenceAsync` + `GenerateAudioWithMultilingualAsync`（Obsolete含め5メソッド）
- `AvailableVoices` プロパティと `GetAvailableVoices()` メソッドが重複
- `PiperConfig` 21フィールド → 初見ユーザーに圧倒的

### 理想のAPI

```csharp
interface IPiperTTS : IDisposable, IAsyncDisposable
{
    // 状態
    bool IsReady { get; }
    PiperVoiceInfo CurrentVoice { get; }
    IReadOnlyList<PiperVoiceInfo> AvailableVoices { get; }

    // 基本API（90%のユーザーはこれだけ）
    Task<AudioClip> SpeakAsync(string text, CancellationToken ct = default);
    Task<AudioClip> SpeakAsync(string text, SpeakOptions options, CancellationToken ct = default);

    // 上級API
    Task<PhonemizeResult> PhonemizeAsync(string text, CancellationToken ct = default);
    Task<AudioClip> SynthesizeAsync(SynthesisRequest request, CancellationToken ct = default);
}

record SpeakOptions
{
    public float Speed { get; init; } = 1.0f;       // LengthScaleの逆数
    public string Language { get; init; } = null;     // null = 自動検出
    public IProgress<SynthesisProgress> Progress { get; init; } = null;
}
```

---

## 7. Phase 2 以降の段階的リファクタリング優先順位

### 全エージェント合意の優先順位

| 優先度 | 項目 | Phase | 難度 | 効果 |
|--------|------|-------|------|------|
| **P0** | 共有テストユーティリティ整備 | P2 | 低 | 高 |
| **P0** | PhonemeEncoder 境界値テスト追加 | P2 | 低 | 高 |
| **P1** | ParseModelConfig の分離（ModelConfigParser クラス） | P2 | 低 | 中 |
| **P1** | ValidatePlatformSpecific のプリプロセッサ除去 | P2 | 低 | 中 |
| **P1** | Dispose/Cancel テスト追加 | P2 | 低 | 高 |
| **P2** | 初期化パイプライン統合（二重バリデーション解消） | P2-3 | 中 | 高 |
| **P2** | PiperErrorCode 整理（派生例外一本化） | P3 | 中 | 中 |
| **P2** | AvailableVoices / GetAvailableVoices 重複解消 | P3 | 低 | 中 |
| **P3** | GenerateAudioAsync → SpeakAsync リネーム | v3.0 | 高 | 高 |
| **P3** | Builder パターン導入 + コンストラクタ internal 化 | v3.0 | 高 | 高 |
| **P3** | IPlatformInitializer インターフェース化 | v3.0 | 中 | 中 |
| **P3** | PiperTTS のDIポイント導入 | v3.0 | 高 | 高 |
| **P3** | IProgress<SynthesisProgress> 導入 + イベント廃止 | v3.0 | 高 | 中 |

### 推奨アプローチ

1. **P0/P1 はPhase 2で即座に着手可能** — 既存public APIを変更せず、内部リファクタリングとテスト追加として完結
2. **P2 は `IPiperTTS` の deprecation cycle と連動** — `[Obsolete]` 付与 → 1バージョン残す → 次バージョンで削除
3. **P3 は v3.0 メジャーバージョンアップの中核** — Builder パターン、SpeakAsync、IProgress を一括導入

---

## 8. 議論の結論

### 現在のPhase 1実装の評価

**良い点**:
- `CreateAsync` のtry/finally + success フラグによる確実なDispose
- `InitializationValidator` の internal 可視性（public APIを汚さない）
- PhonemeEncoder の閾値ベースエスカレーション（50%超で例外）
- `InitializePlatformAudioSession` の `#if` ガード + try-catch（TTS初期化をブロックしない）
- 各チケットに対応するテストが追加されている

**改善すべき点**:
- 初期化パスの統一（二重バリデーション・二重プラットフォーム初期化の解消）
- テストヘルパーの一元化
- 境界値テスト・エラーパステストの追加
- ValidationResult のユーザー公開

### 「ゼロから作り直す」場合の3大設計変更

1. **単一初期化パイプライン**: Builder → Validate → Init → return 初期化済みインスタンス（未初期化オブジェクトを露出しない）
2. **API表面積の最小化**: `SpeakAsync` 1メソッド + `PhonemizeAsync`/`SynthesizeAsync` 上級API
3. **DI可能なコンポーネント分離**: Validator, PlatformInitializer, ModelConfigParser, Phonemizer を注入可能に
