# P3-6: SynthesisRequest public API 昇格検討

**作成日**: 2026-04-08
**ステータス**: 技術調査完了 / 判定待ち
**依存**: P2-2（Prosody フラット配列化）の結果に影響を受ける

---

## 1. 現状分析

### 1.1 SynthesisRequest の全フィールド

`Assets/uPiper/Runtime/Core/AudioGeneration/SynthesisRequest.cs` (L7-42):

```csharp
internal readonly struct SynthesisRequest
{
    public readonly string[] Phonemes;
    public readonly int[] ProsodyA1;
    public readonly int[] ProsodyA2;
    public readonly int[] ProsodyA3;
    public readonly float LengthScale;
    public readonly float NoiseScale;
    public readonly float NoiseW;
    public readonly int SpeakerId;
    public readonly int LanguageId;

    public bool HasProsody => ProsodyA1 != null || ProsodyA2 != null || ProsodyA3 != null;
}
```

| フィールド | 型 | 説明 | デフォルト相当値 |
|-----------|-----|------|----------------|
| `Phonemes` | `string[]` | 音素配列（PUA 文字含む） | 必須 |
| `ProsodyA1` | `int[]` | 言語別 Prosody パラメータ 1 | null（Prosody なし） |
| `ProsodyA2` | `int[]` | 言語別 Prosody パラメータ 2 | null |
| `ProsodyA3` | `int[]` | 言語別 Prosody パラメータ 3 | null |
| `LengthScale` | `float` | 話速スケール（大きいほど遅い） | 1.0f |
| `NoiseScale` | `float` | 音声の変動性（大きいほどランダム） | 0.667f |
| `NoiseW` | `float` | 音素持続時間の変動性 | 0.8f |
| `SpeakerId` | `int` | マルチスピーカーモデルの話者 ID | 0 |
| `LanguageId` | `int` | 多言語モデルの言語 ID | 0 |

### 1.2 P2-2 適用後の想定フィールド

P2-2（Prosody フラット配列化）が適用された場合、以下に変化する:

```csharp
internal readonly struct SynthesisRequest
{
    public readonly string[] Phonemes;
    public readonly int[] ProsodyFlat;  // stride=3, length = Phonemes.Length * 3
    public readonly float LengthScale;
    public readonly float NoiseScale;
    public readonly float NoiseW;
    public readonly int SpeakerId;
    public readonly int LanguageId;

    public bool HasProsody => ProsodyFlat != null;
}
```

### 1.3 生成箇所（2 箇所）

いずれも `PiperTTS.Inference.cs` 内で構築される:

**1. `GenerateAudioWithInferenceAsync`** (L159-163) -- 単一言語パス:
```csharp
var request = new AudioGeneration.SynthesisRequest(
    phonemeResult.Phonemes,
    null, null, null,       // Prosody なし
    lengthScale, noiseScale, noiseW,
    0, 0);                  // speakerId=0, languageId=0
```

**2. `GenerateAudioWithMultilingualAsync`** (L257-261) -- 多言語パス:
```csharp
var request = new AudioGeneration.SynthesisRequest(
    phonemes,
    prosodyA1, prosodyA2, prosodyA3,
    lengthScale, noiseScale, noiseW,
    speakerId, resolvedLanguageId);
```

### 1.4 消費箇所（1 箇所）

`TTSSynthesisOrchestrator.SynthesizeAsync(SynthesisRequest request)` が唯一の消費者。内部で以下を行う:

1. `PhonemeEncoder` で音素を ID にエンコード（Prosody あり/なし分岐）
2. 句分割あり/なし分岐で `IInferenceAudioGenerator.GenerateAudioAsync` を呼び出し
3. `AudioClipBuilder` で正規化 + AudioClip 構築

### 1.5 テストでの使用（8 箇所）

`TTSSynthesisOrchestratorTests.cs` 内の 8 テストメソッドで直接構築。テストは `[assembly: InternalsVisibleTo("uPiper.Tests.Editor")]` により internal アクセスが可能。

---

## 2. 現在の public API で外部ユーザーが指定可能なパラメータ

### 2.1 IPiperTTS インターフェース

| メソッド | 指定可能パラメータ |
|---------|------------------|
| `GenerateAudioAsync(string text)` | テキストのみ |
| `GenerateAudioAsync(string text, PiperVoiceConfig)` | テキスト + 音声設定 |
| `GenerateAudioAsync(string text, string language)` | テキスト + 言語コード |
| `StreamAudioAsync(...)` | 同上（ストリーミング版） |

### 2.2 PiperTTS 固有メソッド（IPiperTTS 外）

| メソッド | 指定可能パラメータ |
|---------|------------------|
| `GenerateAudioWithInferenceAsync(string text)` | テキストのみ |
| `GenerateAudioWithInferenceAsync(string text, float lengthScale, float noiseScale, float noiseW)` | テキスト + 3 推論パラメータ |
| `GenerateAudioWithMultilingualAsync(string text, int languageId, int speakerId, float lengthScale, float noiseScale, float noiseW)` | テキスト + 全推論パラメータ |

### 2.3 外部ユーザーが指定できないもの

| パラメータ | 現状 | 備考 |
|-----------|------|------|
| `Phonemes` 直接指定 | 不可 | テキスト入力必須。外部から音素を直接渡す API がない |
| `Prosody` 直接指定 | 不可 | 内部の MultilingualPhonemizer が自動生成 |
| `SpeakerId` | `GenerateAudioWithMultilingualAsync` のみ | IPiperTTS 経由では指定不可 |
| `LanguageId` | `GenerateAudioWithMultilingualAsync` のみ | 自動検出が基本 |

---

## 3. public 昇格のメリット・デメリット分析

### 3.1 メリット

| # | メリット | 詳細 |
|---|---------|------|
| M1 | 音素直接入力 | 外部 G2P エンジンや手動音素列から直接音声生成が可能。研究用途・カスタム G2P 連携に有用 |
| M2 | Prosody 制御 | 外部からアクセント・イントネーションを明示的に指定可能。SSML 的なユースケースに対応 |
| M3 | パラメータ集約 | 6 引数のメソッド（`GenerateAudioWithMultilingualAsync`）を 1 引数に集約。将来の拡張でもシグネチャ安定 |
| M4 | テスト容易性 | 外部テストからも SynthesisRequest を構築して検証可能 |
| M5 | 上級 API | PiperTTS の高レベル API（テキスト入力）と低レベル API（音素入力）の 2 層構造が明確化 |

### 3.2 デメリット

| # | デメリット | 詳細 |
|---|-----------|------|
| D1 | API 表面積増大 | public 型が 1 つ増える。フィールドの型変更（P2-2 等）が破壊的変更になる |
| D2 | 不正入力リスク | 外部から不正な Phonemes/Prosody 配列を渡される可能性。バリデーションコスト増 |
| D3 | 内部構造の露出 | `ProsodyA1/A2/A3`（または `ProsodyFlat`）の構造が public 契約になる。将来の Prosody 表現変更が困難に |
| D4 | PUA 依存の露出 | 外部ユーザーが PUA 文字エンコーディングを理解する必要がある |
| D5 | LanguageId の不透明性 | `int` 型の `LanguageId` は外部ユーザーにとって意味が不明。モデル依存の値 |
| D6 | P2-2 との順序問題 | P2-2 でフィールド構造が変わるため、public 化のタイミングが重要 |

---

## 4. 昇格する場合の設計案

### 4.1 案 A: readonly struct + ファクトリメソッド（推奨案）

```csharp
/// <summary>
/// 音声合成リクエスト。音素列と合成パラメータを集約する不変データオブジェクト。
/// </summary>
public readonly struct SynthesisRequest
{
    public string[] Phonemes { get; }
    public int[] ProsodyFlat { get; }  // P2-2 後の形式
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
        int speakerId, int languageId)
    {
        Phonemes = phonemes ?? throw new ArgumentNullException(nameof(phonemes));
        ProsodyFlat = prosodyFlat;
        LengthScale = lengthScale;
        NoiseScale = noiseScale;
        NoiseW = noiseW;
        SpeakerId = speakerId;
        LanguageId = languageId;
    }

    /// <summary>
    /// テキストなし・音素直接入力のリクエストを作成する。
    /// </summary>
    public static SynthesisRequest FromPhonemes(
        string[] phonemes,
        float lengthScale = 1.0f,
        float noiseScale = 0.667f,
        float noiseW = 0.8f,
        int speakerId = 0,
        int languageId = 0)
    {
        if (phonemes == null || phonemes.Length == 0)
            throw new ArgumentException("Phonemes cannot be null or empty.", nameof(phonemes));
        return new SynthesisRequest(phonemes, null, lengthScale, noiseScale, noiseW, speakerId, languageId);
    }

    /// <summary>
    /// Prosody 付き音素直接入力のリクエストを作成する。
    /// </summary>
    public static SynthesisRequest FromPhonemesWithProsody(
        string[] phonemes,
        int[] prosodyFlat,
        float lengthScale = 1.0f,
        float noiseScale = 0.667f,
        float noiseW = 0.8f,
        int speakerId = 0,
        int languageId = 0)
    {
        if (phonemes == null || phonemes.Length == 0)
            throw new ArgumentException("Phonemes cannot be null or empty.", nameof(phonemes));
        if (prosodyFlat != null && prosodyFlat.Length != phonemes.Length * 3)
            throw new ArgumentException(
                $"ProsodyFlat length ({prosodyFlat.Length}) must be Phonemes.Length * 3 ({phonemes.Length * 3}).",
                nameof(prosodyFlat));
        return new SynthesisRequest(phonemes, prosodyFlat, lengthScale, noiseScale, noiseW, speakerId, languageId);
    }
}
```

**設計意図**:
- `readonly struct` で不変性を保証（既存方針の踏襲）
- コンストラクタは `internal` にし、ファクトリメソッド `FromPhonemes` / `FromPhonemesWithProsody` で構築を強制
- ファクトリメソッド内でバリデーション（null チェック、Prosody 長さ整合性）
- 内部コードは引き続き `internal` コンストラクタで直接構築可能

### 4.2 案 B: Builder パターン

```csharp
public readonly struct SynthesisRequest
{
    // ... フィールド同上 ...

    public sealed class Builder
    {
        private string[] _phonemes;
        private int[] _prosodyFlat;
        private float _lengthScale = 1.0f;
        private float _noiseScale = 0.667f;
        private float _noiseW = 0.8f;
        private int _speakerId;
        private int _languageId;

        public Builder SetPhonemes(string[] phonemes) { _phonemes = phonemes; return this; }
        public Builder SetProsody(int[] prosodyFlat) { _prosodyFlat = prosodyFlat; return this; }
        public Builder SetLengthScale(float v) { _lengthScale = v; return this; }
        public Builder SetNoiseScale(float v) { _noiseScale = v; return this; }
        public Builder SetNoiseW(float v) { _noiseW = v; return this; }
        public Builder SetSpeakerId(int v) { _speakerId = v; return this; }
        public Builder SetLanguageId(int v) { _languageId = v; return this; }

        public SynthesisRequest Build()
        {
            // バリデーション
            return new SynthesisRequest(...);
        }
    }
}
```

**評価**: v1.3.x で `PiperConfig` に対する Builder パターンが「Unity `[Serializable]` + Inspector と相性が悪い」として不採用済み。`SynthesisRequest` は Inspector 編集対象ではないため事情は異なるが、フィールド数が 7 で Builder の恩恵が薄い。不採用を推奨。

### 4.3 案 C: 段階的昇格（WithXxx メソッド）

```csharp
public readonly struct SynthesisRequest
{
    // ... フィールド同上 ...

    public SynthesisRequest WithLengthScale(float value) =>
        new SynthesisRequest(Phonemes, ProsodyFlat, value, NoiseScale, NoiseW, SpeakerId, LanguageId);

    public SynthesisRequest WithSpeakerId(int value) =>
        new SynthesisRequest(Phonemes, ProsodyFlat, LengthScale, NoiseScale, NoiseW, value, LanguageId);

    // ... 他のフィールドも同様 ...
}
```

**評価**: `readonly struct` のイミュータブル変換パターン。ファクトリメソッド（案 A）と組み合わせ可能。ただし struct のコピーコストが増える（現時点では配列参照のコピーのみで軽量）。必要に応じて案 A に追加する形で段階的に導入可能。

### 4.4 PiperTTS 側の API 追加

```csharp
public partial class PiperTTS
{
    /// <summary>
    /// SynthesisRequest を直接指定して音声を生成する（低レベル API）。
    /// 音素列は事前に取得・構築済みであること。
    /// </summary>
    public async Task<AudioClip> SynthesizeAsync(
        SynthesisRequest request,
        CancellationToken cancellationToken = default)
    {
        // 既存の初期化チェック
        if (_isDisposed) throw new ObjectDisposedException(nameof(PiperTTS));
        if (!_isInitialized || _orchestrator == null)
            throw new InvalidOperationException("Not initialized.");

        return await _orchestrator.SynthesizeAsync(request, cancellationToken);
    }
}
```

---

## 5. 昇格しない場合の代替案

### 5.1 既存 API の拡張

`GenerateAudioWithMultilingualAsync` が既に `lengthScale`, `noiseScale`, `noiseW`, `speakerId`, `languageId` を受け付けている。不足しているのは「音素直接入力」のみ。

```csharp
// 代替案: 音素直接入力メソッドを PiperTTS に追加（SynthesisRequest は internal のまま）
public async Task<AudioClip> GenerateAudioFromPhonemesAsync(
    string[] phonemes,
    int[] prosodyA1 = null, int[] prosodyA2 = null, int[] prosodyA3 = null,
    float lengthScale = 1.0f, float noiseScale = 0.667f, float noiseW = 0.8f,
    int speakerId = 0, int languageId = 0,
    CancellationToken cancellationToken = default)
{
    var request = new SynthesisRequest(phonemes, prosodyA1, prosodyA2, prosodyA3,
        lengthScale, noiseScale, noiseW, speakerId, languageId);
    return await _orchestrator.SynthesizeAsync(request, cancellationToken);
}
```

**問題点**: パラメータ数が 10 に達する。P2-2 で Prosody 構造が変わると破壊的変更になる点は同じ。

### 5.2 PiperConfig 経由のデフォルト値指定

推論パラメータ（`LengthScale`, `NoiseScale`, `NoiseW`）を `PiperConfig` に追加し、デフォルト値として使用する案:

```csharp
public class PiperConfig
{
    [Header("Synthesis Defaults")]
    public float DefaultLengthScale = 1.0f;
    public float DefaultNoiseScale = 0.667f;
    public float DefaultNoiseW = 0.8f;
    public int DefaultSpeakerId = 0;
}
```

**評価**: Inspector から設定可能になる利点はあるが、`SynthesisRequest` public 化の代替にはならない（動的なリクエスト単位の変更が必要なユースケースに対応できない）。補完的な機能として P3-6 とは独立に検討可能。

---

## 6. P2-2（Prosody フラット配列化）との整合性

### 6.1 順序依存

P2-2 は `SynthesisRequest` のフィールド構造を変更する:
- `ProsodyA1` / `ProsodyA2` / `ProsodyA3` → `ProsodyFlat` (stride=3)

P3-6 を P2-2 より先に実施した場合、P2-2 実施時に public API の破壊的変更が発生する。

### 6.2 推奨順序

```
P2-2 (Prosody フラット配列化)  ← SynthesisRequest は internal のまま変更
    ↓
P3-6 (SynthesisRequest public 昇格)  ← P2-2 後の安定した構造を public 化
```

P2-2 が完了し `ProsodyFlat` 構造が確定した後に public 化すれば、public API の安定性を確保できる。

### 6.3 P2-2 設計ドキュメントとの整合

P2-2 設計ドキュメント (P2-2_ProsodyFlatArray.md) のセクション 10 で明記:

> `SynthesisRequest` / `ProsodyEncodingResult` / `PhonemeSilenceProcessor.Phrase` は internal のため外部互換性影響なし。

P3-6 を実施する場合、この前提が覆るため P2-2 の破壊的変更まとめに `SynthesisRequest.ProsodyFlat` を追加する必要がある。ただし P2-2 → P3-6 の順序であればこの問題は発生しない。

---

## 7. リスク評価

| リスク | 影響 | 緩和策 |
|--------|------|--------|
| フィールド構造の将来変更 | public 型の破壊的変更 | P2-2 完了後に public 化。v3.0 まで構造変更しない方針 |
| PUA 音素の外部露出 | ユーザー混乱 | ドキュメント・サンプルコードの整備。`PiperTTS.PhonemizeAsync()` 公開メソッドで音素取得を容易に |
| LanguageId の不透明性 | 不正値の指定 | ファクトリメソッドで言語コード（"ja", "en"）から解決するヘルパーを提供 |
| Prosody 配列長の不整合 | ランタイムエラー | ファクトリメソッドでバリデーション |
| バージョン間互換性 | アップグレード負担 | readonly struct のため既存コードのバイナリ互換性は維持される |

---

## 8. 推奨判定

### 判定: **条件付き実施**（P2-2 完了後）

### 根拠

1. **需要の存在**: 音素直接入力は研究用途・カスタム G2P 連携で実際の需要がある。Demo (`InferenceEngineDemo.cs`) が既に `PhonemeEncoder` + `IInferenceAudioGenerator` を直接呼び出すパターンで実装しており、`PiperTTS` 経由の高レベル API では表現できないユースケースが存在する。

2. **パラメータ爆発の解決**: `GenerateAudioWithMultilingualAsync` は既に 6 パラメータ。将来のパラメータ追加（例: 音声スタイル、速度変化カーブ等）で更に増加する。`SynthesisRequest` による集約は API の拡張性を確保する。

3. **リスクは管理可能**: P2-2 完了後に public 化すれば、Prosody 構造変更による二重破壊的変更を回避できる。`readonly struct` + ファクトリメソッドでバリデーション付き構築を強制すれば不正入力リスクも緩和される。

4. **既存パターンとの整合**: v2.0 は「メジャー破壊的変更リリース」であり、public API の追加は適切なタイミング。

### 実施条件

- P2-2（Prosody フラット配列化）完了後に実施すること
- ファクトリメソッドパターン（案 A）を採用すること
- `PiperTTS.SynthesizeAsync(SynthesisRequest)` 追加と同時に `PiperTTS.PhonemizeAsync()` public メソッドも追加し、音素取得→リクエスト構築→合成の流れを外部から完結可能にすること

### 実施範囲

| 変更 | ファイル |
|------|---------|
| `SynthesisRequest` を `public readonly struct` に変更 | `SynthesisRequest.cs` |
| ファクトリメソッド `FromPhonemes` / `FromPhonemesWithProsody` 追加 | 同上 |
| `PiperTTS.SynthesizeAsync(SynthesisRequest)` 追加 | `PiperTTS.Inference.cs` |
| `PiperTTS.PhonemizeAsync(string text)` public 化 | `PiperTTS.Inference.cs` |
| テスト追加（ファクトリメソッドのバリデーション、SynthesizeAsync 経由の E2E） | `Tests/Editor/` |

### 見送る項目

- Builder パターン（フィールド数 7 で恩恵が薄い）
- `WithXxx` メソッド（初回リリースでは不要。需要があれば v2.1 で追加）
- `PiperConfig` へのデフォルト推論パラメータ追加（P3-6 とは独立の機能。別タスクで検討）

---

## 9. ユーザー要望の収集方法

現時点ではユーザーベースが限定的であるため、以下の段階的アプローチを推奨:

1. **v2.0-alpha で internal のまま P2-2 を適用** -- フィールド構造を確定
2. **v2.0-beta で public 化を実施** -- beta テスターからのフィードバックを収集
3. **フィードバックチャネル**:
   - GitHub Issues の `api-feedback` ラベル
   - GitHub Discussions の Q&A カテゴリ
   - README/ドキュメントに「低レベル API のユースケースがあれば Issue を」と記載
4. **判断基準**: beta 期間中にネガティブフィードバック（API が複雑、PUA 音素が分かりにくい等）が多い場合は GA で internal に戻すことも選択肢として保持
