# P3-6: SynthesisRequest public API昇格

**マイルストーン**: M4 - NativeArray + Public API (beta)
**優先度**: P2（条件付き実施）
**見積もり**: 1人日
**依存チケット**: P2-2（Prosody フラット配列化）
**後続チケット**: なし
**ブランチ名**: `feature/v2.0-P3-6-synthesis-request-public`

---

## 1. タスク目的とゴール

`SynthesisRequest`（internal readonly struct）を public API に昇格し、外部ユーザーが音素直接入力・Prosody 制御・推論パラメータ集約を行えるようにする。P2-2（Prosody フラット配列化）完了後の安定した構造を public 化することで、破壊的変更リスクを最小化する。

**解決する問題**:

1. **音素直接入力の不在**: 外部 G2P エンジンや手動音素列から直接音声生成を行う API がない。Demo（`InferenceEngineDemo.cs`）が `PhonemeEncoder` + `IInferenceAudioGenerator` を直接呼び出すパターンで実装しており、`PiperTTS` 経由の高レベル API では表現できないユースケースが存在する
2. **パラメータ爆発**: `GenerateAudioWithMultilingualAsync` は既に 6 パラメータ。将来のパラメータ追加（音声スタイル、速度変化カーブ等）で更に増加する
3. **Prosody 制御の不在**: 外部からアクセント・イントネーションを明示的に指定する API がない

**条件付き実施の判定**:

P2-2 完了後に `ProsodyFlat`（stride=3）構造が確定したタイミングで実施する。beta テスターからのフィードバックが否定的（API が複雑、PUA 音素が分かりにくい等）な場合は GA で internal に戻す選択肢を保持する。

**完了状態（Definition of Done）**:

- `SynthesisRequest` が `public readonly struct` として定義されている
- ファクトリメソッド `FromPhonemes` / `FromPhonemesWithProsody` が実装されている（バリデーション付き）
- コンストラクタは `internal` のまま維持されている（ファクトリ経由を強制）
- `PiperTTS.SynthesizeAsync(SynthesisRequest)` オーバーロードが追加されている
- `PiperTTS.PhonemizeAsync(string text)` が public 化されている
- テスト追加（ファクトリメソッドのバリデーション、SynthesizeAsync 経由の E2E）
- 全テスト通過（EditMode + PlayMode）
- `dotnet format --verify-no-changes` 通過

---

## 2. 実装する内容の詳細

### Step 1: SynthesisRequest を public readonly struct に変更

対象ファイル: `Assets/uPiper/Runtime/Core/AudioGeneration/SynthesisRequest.cs`

`internal` → `public` に変更。P2-2 完了後のフィールド構造（`ProsodyFlat` stride=3）を前提とする。

```csharp
/// <summary>
/// 音声合成リクエスト。音素列と合成パラメータを集約する不変データオブジェクト。
/// </summary>
public readonly struct SynthesisRequest
{
    public string[] Phonemes { get; }
    public int[] ProsodyFlat { get; }  // P2-2 後の形式、stride=3
    public float LengthScale { get; }
    public float NoiseScale { get; }
    public float NoiseW { get; }
    public int SpeakerId { get; }
    public int LanguageId { get; }
    public bool HasProsody => ProsodyFlat != null;

    // コンストラクタは internal -- 直接構築を防ぎ、ファクトリ経由を強制
    internal SynthesisRequest(
        string[] phonemes, int[] prosodyFlat,
        float lengthScale, float noiseScale, float noiseW,
        int speakerId, int languageId) { ... }
}
```

**設計意図**: コンストラクタを `internal` に維持し、外部ユーザーにはファクトリメソッド経由の構築を強制する。これによりバリデーションをバイパスする構築を防止する。内部コードは引き続き `internal` コンストラクタで直接構築可能。

### Step 2: ファクトリメソッド追加

対象ファイル: `Assets/uPiper/Runtime/Core/AudioGeneration/SynthesisRequest.cs`

```csharp
/// <summary>テキストなし・音素直接入力のリクエストを作成する。</summary>
public static SynthesisRequest FromPhonemes(
    string[] phonemes,
    float lengthScale = 1.0f, float noiseScale = 0.667f, float noiseW = 0.8f,
    int speakerId = 0, int languageId = 0)
{
    if (phonemes == null || phonemes.Length == 0)
        throw new ArgumentException("Phonemes cannot be null or empty.", nameof(phonemes));
    return new SynthesisRequest(phonemes, null, lengthScale, noiseScale, noiseW, speakerId, languageId);
}

/// <summary>Prosody 付き音素直接入力のリクエストを作成する。</summary>
public static SynthesisRequest FromPhonemesWithProsody(
    string[] phonemes, int[] prosodyFlat,
    float lengthScale = 1.0f, float noiseScale = 0.667f, float noiseW = 0.8f,
    int speakerId = 0, int languageId = 0)
{
    if (phonemes == null || phonemes.Length == 0)
        throw new ArgumentException("Phonemes cannot be null or empty.", nameof(phonemes));
    if (prosodyFlat != null && prosodyFlat.Length != phonemes.Length * 3)
        throw new ArgumentException(
            $"ProsodyFlat length ({prosodyFlat.Length}) must be Phonemes.Length * 3 ({phonemes.Length * 3}).",
            nameof(prosodyFlat));
    return new SynthesisRequest(phonemes, prosodyFlat, lengthScale, noiseScale, noiseW, speakerId, languageId);
}
```

**バリデーション**:
- `Phonemes`: null/空配列チェック
- `ProsodyFlat`: null 許容（Prosody なし）、非 null の場合は `Phonemes.Length * 3` との長さ整合性チェック

### Step 3: PiperTTS.SynthesizeAsync 追加

対象ファイル: `Assets/uPiper/Runtime/Core/PiperTTS.Inference.cs`

```csharp
/// <summary>
/// SynthesisRequest を直接指定して音声を生成する（低レベル API）。
/// 音素列は事前に取得・構築済みであること。
/// </summary>
public async Task<AudioClip> SynthesizeAsync(
    SynthesisRequest request,
    CancellationToken cancellationToken = default)
{
    if (_isDisposed) throw new ObjectDisposedException(nameof(PiperTTS));
    if (!_isInitialized || _orchestrator == null)
        throw new InvalidOperationException("Not initialized.");

    return await _orchestrator.SynthesizeAsync(request, cancellationToken);
}
```

### Step 4: PiperTTS.PhonemizeAsync public 化

対象ファイル: `Assets/uPiper/Runtime/Core/PiperTTS.Inference.cs`

音素取得 → リクエスト構築 → 合成の流れを外部から完結可能にするため、`PhonemizeAsync` を public 化する。

**ユースケース例**:
```csharp
// 外部ユーザーの利用フロー
var phonemeResult = await piperTTS.PhonemizeAsync("こんにちは");
var request = SynthesisRequest.FromPhonemesWithProsody(
    phonemeResult.Phonemes, phonemeResult.ProsodyFlat,
    lengthScale: 0.8f);
var audioClip = await piperTTS.SynthesizeAsync(request);
```

### 内部生成箇所の確認

`SynthesisRequest` の生成箇所（2箇所、いずれも `PiperTTS.Inference.cs`）は引き続き `internal` コンストラクタで直接構築する。P2-2 完了後の形式:

1. `GenerateAudioWithInferenceAsync` -- 単一言語パス
2. `GenerateAudioWithMultilingualAsync` -- 多言語パス

消費箇所は `TTSSynthesisOrchestrator.SynthesizeAsync(SynthesisRequest)` の1箇所のみ。変更なし。

---

## 3. エージェントチームの役割と人数

**1エージェント構成**（1人日）

| 役割 | 担当内容 | 工数 |
|------|---------|------|
| 実装エージェント | Step 1-4 の全実装 + テスト作成 | 1 人日 |

**理由**: 変更ファイル数が限定的（Runtime 2 + Tests 1-2）。`SynthesisRequest` のアクセス修飾子変更 + ファクトリメソッド追加 + `PiperTTS` へのメソッド追加が主な作業。ロジック変更を伴わない。

**P2-2 との順序**: P2-2 が `SynthesisRequest` のフィールド構造を `ProsodyA1/A2/A3` → `ProsodyFlat` に変更するため、P3-6 は P2-2 完了後に実施すること。P2-2 より先に public 化した場合、P2-2 実施時に public API の破壊的変更が発生する。

---

## 4. 提供範囲とテスト項目

### 4.1 提供範囲

| カテゴリ | 範囲内 | 範囲外 |
|---------|-------|-------|
| SynthesisRequest | `internal` → `public` 変更、ファクトリメソッド追加 | Builder パターン（フィールド数7で恩恵薄い）、`WithXxx` メソッド（需要あれば v2.1 で追加） |
| PiperTTS | `SynthesizeAsync(SynthesisRequest)` 追加、`PhonemizeAsync` public 化 | 既存メソッドの変更 |
| PiperConfig | なし | デフォルト推論パラメータ追加（P3-6 とは独立の機能。別タスクで検討） |
| テスト | ファクトリメソッドバリデーション + SynthesizeAsync E2E | IPiperTTS インターフェース拡張 |

### 4.2 Unit テスト

**ファクトリメソッドのバリデーションテスト**:

| テスト名 | 入力 | 期待結果 |
|---------|------|---------|
| `FromPhonemes_ValidPhonemes_CreatesRequest` | 有効な音素配列 | SynthesisRequest が構築される |
| `FromPhonemes_NullPhonemes_ThrowsArgumentException` | null | ArgumentException |
| `FromPhonemes_EmptyPhonemes_ThrowsArgumentException` | 空配列 | ArgumentException |
| `FromPhonemes_DefaultParameters_UsesDefaults` | 音素のみ指定 | LengthScale=1.0, NoiseScale=0.667, NoiseW=0.8, SpeakerId=0, LanguageId=0 |
| `FromPhonemesWithProsody_ValidProsody_CreatesRequest` | 音素3個 + ProsodyFlat 9要素 | HasProsody == true |
| `FromPhonemesWithProsody_NullProsody_CreatesRequestWithoutProsody` | Prosody null | HasProsody == false |
| `FromPhonemesWithProsody_MismatchedLength_ThrowsArgumentException` | 音素3個 + ProsodyFlat 6要素 | ArgumentException |
| `FromPhonemesWithProsody_CustomParameters_SetsCorrectly` | カスタムパラメータ | 各フィールドが指定値 |

**SynthesizeAsync 経由の E2E テスト**:

| テスト名 | 検証内容 |
|---------|---------|
| `SynthesizeAsync_WithValidRequest_ReturnsAudioClip` | SynthesisRequest 経由で AudioClip が生成されること |
| `SynthesizeAsync_NotInitialized_ThrowsInvalidOperationException` | 未初期化時に例外が投げられること |
| `SynthesizeAsync_Disposed_ThrowsObjectDisposedException` | Dispose 後に例外が投げられること |

### 4.3 E2E テスト

| テスト | 内容 |
|-------|------|
| CI `unity-tests.yml` 全通過 | EditMode + PlayMode テストの GREEN 確認 |
| 音素直接入力の手動確認 | `FromPhonemes` で構築した SynthesisRequest から音声が正常に生成されること |

---

## 5. 懸念事項とレビュー項目

### 5.1 技術的懸念

| 懸念 | 深刻度 | 対策 |
|------|--------|------|
| **フィールド構造の将来変更** | 中 | P2-2 完了後に public 化し、v3.0 まで構造変更しない方針。`readonly struct` のためバイナリ互換性は維持される |
| **PUA 音素の外部露出** | 中 | ドキュメント・サンプルコードの整備。`PiperTTS.PhonemizeAsync()` 公開メソッドで音素取得を容易にし、PUA 知識なしでも利用可能にする |
| **LanguageId の不透明性** | 低 | `int` 型の `LanguageId` は外部ユーザーにとって意味が不明。ファクトリメソッドで言語コード（"ja", "en"）から解決するヘルパーを将来提供 |
| **Prosody 配列長の不整合** | 低 | ファクトリメソッドで `prosodyFlat.Length != phonemes.Length * 3` を検証 |
| **beta テスターの否定的フィードバック** | 中 | GA で internal に戻す選択肢を保持。ファクトリメソッドパターンのため、public → internal 変更時の影響は限定的 |
| **P2-2 設計ドキュメントとの整合** | 低 | P2-2 のセクション10「SynthesisRequest は internal のため外部互換性影響なし」の前提が覆る。P2-2 → P3-6 の順序であればこの問題は発生しない |

### 5.2 レビューチェックリスト

- [ ] `SynthesisRequest` が `public readonly struct` として定義されているか
- [ ] コンストラクタが `internal` のまま維持されているか（ファクトリ経由を強制）
- [ ] `FromPhonemes` ファクトリメソッドが null/空配列チェックを行っているか
- [ ] `FromPhonemesWithProsody` ファクトリメソッドが Prosody 長さ整合性チェックを行っているか
- [ ] `PiperTTS.SynthesizeAsync(SynthesisRequest)` が初期化チェック・Dispose チェックを行っているか
- [ ] `PiperTTS.PhonemizeAsync(string text)` が public メソッドとして公開されているか
- [ ] 既存の内部生成箇所（2箇所）が引き続き `internal` コンストラクタを使用しているか
- [ ] テストの `[assembly: InternalsVisibleTo("uPiper.Tests.Editor")]` で既存テストが引き続き `internal` コンストラクタにアクセス可能か
- [ ] ファクトリメソッドのバリデーションテストが追加されているか
- [ ] XML doc コメントが `SynthesisRequest`、ファクトリメソッド、`SynthesizeAsync` に付与されているか
- [ ] `dotnet format --verify-no-changes` が通過するか

---

## 6. 一から作り直すとしたら

### 6.1 public API 設計全体の再考

現行の `PiperTTS` は高レベル API（テキスト入力）のみを提供しており、低レベル API（音素入力）は内部に閉じている。P3-6 で `SynthesisRequest` を public 化すると、PiperTTS に高レベル API と低レベル API の2層が混在することになる。

一から設計するなら、以下のアプローチが考えられる:

**アプローチA: 2クラス分離**

```csharp
// 高レベル API（テキスト入力）-- 既存
public class PiperTTS : IPiperTTS
{
    public Task<AudioClip> GenerateAudioAsync(string text) { ... }
}

// 低レベル API（音素入力）-- 新規
public class PiperSynthesizer
{
    public Task<AudioClip> SynthesizeAsync(SynthesisRequest request) { ... }
}
```

メリット: 責務分離が明確。高レベル API のユーザーは低レベル API を意識しない。
デメリット: クラスが増える。`PiperTTS` と `PiperSynthesizer` の初期化・リソース共有が複雑化。ユーザーがどちらを使うべきか混乱する可能性。

**アプローチB: 統合 API（P3-6 の採用案）**

```csharp
public class PiperTTS : IPiperTTS
{
    // 高レベル API
    public Task<AudioClip> GenerateAudioAsync(string text) { ... }

    // 低レベル API
    public Task<AudioClip> SynthesizeAsync(SynthesisRequest request) { ... }
    public Task<PhonemeResult> PhonemizeAsync(string text) { ... }
}
```

メリット: 単一クラスで完結。初期化が1回で済む。高レベル → 低レベルの段階的移行が容易。
デメリット: `PiperTTS` の責務が肥大化。API 表面積が増える。

**採用判断**: P3-6 ではアプローチ B を採用する。ユーザーの大多数は高レベル API（`GenerateAudioAsync`）を使用し、低レベル API（`SynthesizeAsync`）は研究用途・カスタム G2P 連携の上級ユーザー向け。2クラス分離はユーザーベースが拡大し、明確な需要が確認された v3.0 で検討する。

### 6.2 不採用とした設計案

**Builder パターン**: v1.3.x で `PiperConfig` に対する Builder パターンが「Unity `[Serializable]` + Inspector と相性が悪い」として不採用済み。`SynthesisRequest` は Inspector 編集対象ではないため事情は異なるが、フィールド数が 7 で Builder の恩恵が薄い。

**`WithXxx` メソッド**: `readonly struct` のイミュータブル変換パターン。ファクトリメソッドと組み合わせ可能だが、初回リリースでは不要。struct のコピーコストも考慮し、需要があれば v2.1 で追加する。

**`PiperConfig` へのデフォルト推論パラメータ追加**: Inspector から `DefaultLengthScale` / `DefaultNoiseScale` / `DefaultNoiseW` を設定可能にする案。P3-6 とは独立の機能であり、動的なリクエスト単位の変更が必要なユースケースには対応できないため、補完的な機能として別タスクで検討する。

### 6.3 現設計の正直な弱点

1. **API 表面積の増大**: `SynthesisRequest` が public 型として追加されるため、フィールドの型変更（将来の Prosody 表現変更等）が破壊的変更になる。v3.0 まで構造変更しない方針で緩和するが、長期的な API 安定性は保証できない

2. **PUA 依存の露出**: 外部ユーザーが `FromPhonemes` を使用するには PUA 文字エンコーディングを理解する必要がある。`PhonemizeAsync` で音素取得を容易にするが、手動で音素列を構築するユースケースでは PUA 知識が必要

3. **LanguageId の不透明性**: `int` 型の `LanguageId` は外部ユーザーにとって意味が不明（モデル依存の値）。言語コード → LanguageId の解決ヘルパーは P3-6 のスコープ外であり、初回リリースではユーザーが `LanguageConstants` を参照する必要がある

4. **internal コンストラクタとファクトリメソッドの二重性**: 内部コードは `internal` コンストラクタで直接構築し、外部コードはファクトリメソッドで構築する。この二重性により、内部コードがファクトリメソッドのバリデーションをバイパスするが、内部コードの入力は既にバリデーション済み（Phonemizer の出力）であるため実質的な問題はない

### 6.4 ユーザー要望の収集方法

現時点ではユーザーベースが限定的であるため、以下の段階的アプローチを採用:

1. **v2.0-alpha で internal のまま P2-2 を適用** -- フィールド構造を確定
2. **v2.0-beta で public 化を実施** -- beta テスターからのフィードバックを収集
3. **フィードバックチャネル**: GitHub Issues の `api-feedback` ラベル、GitHub Discussions の Q&A カテゴリ
4. **判断基準**: beta 期間中にネガティブフィードバックが多い場合は GA で internal に戻す

---

## 7. 後続タスクへの連絡事項

### M5（ドキュメント更新）への引き継ぎ

P3-6 完了後、以下のドキュメントを更新する:
- CLAUDE.md: `SynthesisRequest` のアクセス修飾子と API 説明の更新
- README: 低レベル API（音素直接入力）のサンプルコード追加
- API ドキュメント: `SynthesisRequest` ファクトリメソッド、`PiperTTS.SynthesizeAsync`、`PiperTTS.PhonemizeAsync` の XML doc 整備

### 将来検討タスク

| タスク | 時期 | 備考 |
|--------|------|------|
| `WithXxx` メソッド追加 | v2.1 | 需要があれば |
| 言語コード → LanguageId 解決ヘルパー | v2.1 | LanguageId の不透明性対策 |
| `PiperConfig` デフォルト推論パラメータ | v2.1 | Inspector 設定との統合 |
| 2クラス分離（PiperTTS / PiperSynthesizer） | v3.0 | ユーザーベース拡大後に検討 |

### ファイル変更一覧

| ファイル | 変更種別 |
|---------|---------|
| `AudioGeneration/SynthesisRequest.cs` | 修正（`internal` → `public` + ファクトリメソッド追加） |
| `Core/PiperTTS.Inference.cs` | 修正（`SynthesizeAsync` 追加 + `PhonemizeAsync` public 化） |
| `Tests/Editor/SynthesisRequestTests.cs` | **新規作成**（ファクトリメソッドバリデーション 8 テストケース） |
| `Tests/Editor/PiperTTSSynthesizeTests.cs` | **新規作成**（SynthesizeAsync E2E 3 テストケース） |
