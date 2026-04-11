# フォネムタイミング機能 — 要件定義書

**作成日**: 2026-04-11
**参照**: `docs/phoneme-timing-spec.md`, `docs/phoneme-timing-investigation.md`

---

## 1. 機能概要

VITSモデルのONNX出力 `durations` テンソル（各音素のスペクトログラムフレーム数）を読み取り、音素ごとの開始/終了時刻（秒単位）を計算して公開APIで返却する。

### ユースケース

- Live2Dリップシンク（ParamMouthOpenY / ParamMouthForm制御）
- 3Dモデルのブレンドシェイプ制御
- 字幕・カラオケ表示
- VRM Visemeマッピング

---

## 2. 新規型定義

### 2.1 PhonemeTimingEntry

```csharp
// Assets/uPiper/Runtime/Core/AudioGeneration/PhonemeTimingResult.cs

/// <summary>1音素のタイミング情報。</summary>
public readonly struct PhonemeTimingEntry
{
    public string Phoneme { get; }          // 人間可読音素文字列（"a:", "k", "N_m" 等）
    public float StartSeconds { get; }      // 開始時刻（秒）
    public float EndSeconds { get; }        // 終了時刻（秒）
    public float DurationSeconds { get; }   // EndSeconds - StartSeconds（算出プロパティ）
}
```

**設計判断**:
- `readonly struct`: SynthesisRequestと同じパターン。GCフリー、値型。
- 単位は**秒**: piper-plus `TimingWriter.PhonemeTimingEntry` と同一。ミリ秒変換はユーザー側の責務。
- `DurationSeconds` は算出プロパティ（冗長だがAPI利便性のため保持）。

### 2.2 SynthesisWithTimingResult

```csharp
// Assets/uPiper/Runtime/Core/AudioGeneration/SynthesisWithTimingResult.cs

/// <summary>タイミング情報付き音声合成結果。</summary>
public sealed class SynthesisWithTimingResult
{
    public AudioClip AudioClip { get; }
    public IReadOnlyList<PhonemeTimingEntry> Timings { get; }  // null: モデル非対応時
    public float TotalDurationSeconds { get; }                 // 最終エントリのEndSeconds

    internal SynthesisWithTimingResult(
        AudioClip audioClip,
        IReadOnlyList<PhonemeTimingEntry> timings) { ... }
}
```

**設計判断**:
- `sealed class`: PhonemizeResultと同じパターン。参照型配列を含むためstructは不適。
- `internal` コンストラクタ: 外部からのインスタンス化を防止（PhonemizeResultに倣う）。
- `IReadOnlyList<T>`: 防御的コピーで不変性を保証。

### 2.3 InferenceOutput（内部型）

```csharp
// Assets/uPiper/Runtime/Core/AudioGeneration/InferenceOutput.cs

/// <summary>ONNX推論の生出力を保持する内部構造体。</summary>
internal readonly struct InferenceOutput : IDisposable
{
    public NativeArray<float> Audio { get; }
    public NativeArray<float> Durations { get; }  // IsCreated == false: モデル非対応

    public void Dispose()
    {
        if (Audio.IsCreated) Audio.Dispose();
        if (Durations.IsCreated) Durations.Dispose();
    }
}
```

**設計判断**:
- `internal`: 推論レイヤーの内部型。公開APIには露出しない。
- `NativeArray<float>`: Sentis APIとの整合性。`Allocator.Persistent` で確保。
- `IDisposable`: 両NativeArrayの確実なDispose。
- durations非対応モデルは `Durations.IsCreated == false` で判定。

---

## 3. 公開API

### 3.1 新規メソッド

```csharp
// IPiperTTS.cs に追加

/// <summary>音声合成とフォネムタイミングを同時に取得する。</summary>
/// <param name="request">音素・Prosody・合成パラメータを含むリクエスト。</param>
/// <param name="cancellationToken">キャンセルトークン。</param>
/// <returns>AudioClipとタイミング情報を含む結果。モデルがdurations非対応の場合、Timingsはnull。</returns>
Task<SynthesisWithTimingResult> SynthesizeWithTimingAsync(
    SynthesisRequest request,
    CancellationToken cancellationToken = default);
```

**設計判断**:
- 既存 `SynthesizeAsync` は `Task<AudioClip>` のまま維持（破壊的変更なし）。
- 高レベルAPI（`GenerateAudioAsync` テキスト入力版）にはタイミング版を追加しない。低レベルAPI（`SynthesizeWithTimingAsync`）のみ提供。理由: タイミング情報が必要なユースケースでは音素レベルの制御が前提。
- 命名: 既存 `SynthesizeAsync` に `WithTiming` を付加。`PhonemizeAsync` / `SynthesizeAsync` の命名慣例に整合。

### 3.2 既存API互換性

| メソッド | 変更 | 備考 |
|---|---|---|
| `SynthesizeAsync(SynthesisRequest, CancellationToken)` | 変更なし | `Task<AudioClip>` を返す |
| `GenerateAudioAsync(string, ...)` | 変更なし | 3オーバーロード全て維持 |
| `PhonemizeAsync(string, CancellationToken)` | 変更なし | タイミングは合成時に生成 |
| `ClearCache()` | 変更なし | キャッシュ構造は内部変更のみ |

**破壊的変更**: 公開APIには一切なし。内部インターフェース（`IInferenceAudioGenerator`）の戻り値型変更は internal スコープのため影響なし。

---

## 4. 内部パイプライン変更要件

### 4.1 データフロー

```
ONNX outputs[1] "durations" (float32, [1, phonemes])
    │
    ▼
InferenceAudioGenerator.ExtractResults()
    → NativeArray<float> durations (Allocator.Persistent)
    → InferenceOutput { Audio, Durations }
    │
    ▼
[ShortTextMitigatingGenerator] — PAD位置のdurations要素を除去
    │
    ▼
[SplitInferenceOrchestrator] — 句ごとのdurationsを結合
    │
    ▼
TTSSynthesisOrchestrator
    → TimingCalculator.Calculate(phonemes, durations, phonemeIdMap, sampleRate, hopLength)
    → PhonemeTimingEntry[] → SynthesisWithTimingResult
    │
    ▼
PiperTTS.SynthesizeWithTimingAsync() → SynthesisWithTimingResult
```

### 4.2 IInferenceAudioGenerator

**変更**: `GenerateAudioAsync` の戻り値を `Task<NativeArray<float>>` → `Task<InferenceOutput>` に変更。

**影響範囲**:

| 実装クラス | 変更内容 |
|---|---|
| `InferenceAudioGenerator` | durations テンソル読み取り追加 |
| `ShortTextMitigatingGenerator` | 戻り値型追従 + PAD位置除去 |
| `SplitInferenceOrchestrator` | 戻り値型追従 + 句ごとdurations結合 |

### 4.3 InferenceAudioGenerator

#### InitializeAsync

```csharp
// 既存 (L176-182)
_cachedOutputName = _model.outputs[0].name;

// 追加
_supportsDurations = _model.outputs.Count >= 2;
if (_supportsDurations)
    _cachedDurationsOutputName = _model.outputs[1].name;
```

#### ExtractResults

既存のaudio抽出パターン（PeekOutput → ReadbackAndClone → DownloadToNativeArray）と同一手順でdurationsを抽出:

```csharp
NativeArray<float> durations = default;
if (_supportsDurations)
{
    var durTensor = _worker.PeekOutput(_cachedDurationsOutputName) as Tensor<float>;
    var durReadable = durTensor.ReadbackAndClone();
    durations = new NativeArray<float>(durReadable.shape.length, Allocator.Persistent);
    NativeArray<float>.Copy(durReadable.DownloadToNativeArray(), durations, durations.Length);
    durReadable.Dispose();
}
return new InferenceOutput(audioData, durations);
```

### 4.4 ShortTextMitigatingGenerator

**問題**: `ShortTextProcessor.PadPhonemeIds` が BOS後/EOS前に PADトークン(ID=0) を挿入する。推論結果の durations 配列にはパディング分の要素も含まれるため、元の音素との対応が崩れる。

**対応**: 推論後にパディング位置のdurations要素を除去。

```
パディング前: [BOS, p1, p2, p3, EOS]           (5要素)
パディング後: [BOS, PAD, PAD, p1, p2, p3, PAD, PAD, EOS]  (9要素)
durations:    [d0,  d1,  d2,  d3, d4, d5, d6,  d7,  d8]   (9要素)
除去後:       [d0,  d3,  d4,  d5, d8]           (5要素 = 元の音素に対応)
```

**必要情報**: パディング挿入位置（afterBos, beforeEos）を `PadPhonemeIds` から取得または再計算（`deficit/2`, `deficit - deficit/2`）。

### 4.5 SplitInferenceOrchestrator

**問題**: 沈黙句分割時、各句ごとに `GenerateAudioAsync` が呼ばれる。durations も句ごとに分割されるため、最終的なタイミング結果の構築時に句間の無音時間を考慮する必要がある。

**対応**:
1. 各句の durations を取得・結合
2. 句間の無音時間（`silenceSamples / sampleRate` 秒）を時間オフセットとして加算
3. durations 自体には無音エントリを追加しない（無音はAudioClipにのみ存在）

**句間オフセット情報の伝搬**: `InferenceOutput` に句間メタデータ（各句の無音サンプル数）を含めるか、TTSSynthesisOrchestrator 側で計算する。

### 4.6 TTSSynthesisOrchestrator

**変更**: 内部的に常に `InferenceOutput` を受け取り、durations がある場合は `TimingCalculator` でタイミング計算。

```
既存パイプライン:
  Encode → Cache check → Generate(NativeArray<float>) → Normalize → Cache set → BuildAudioClip → AudioClip

変更後パイプライン:
  Encode → Cache check → Generate(InferenceOutput) → Normalize(Audio部分のみ)
    → TimingCalculator.Calculate(durations部分) → Cache set(Audio+Timing)
    → BuildAudioClip → SynthesisWithTimingResult
```

- 既存の `SynthesizeAsync` は内部で `SynthesizeWithTimingAsync` を呼び、`.AudioClip` のみ返却。

---

## 5. TimingCalculator 仕様

### 5.1 メソッドシグネチャ

```csharp
// Assets/uPiper/Runtime/Core/AudioGeneration/TimingCalculator.cs

public static class TimingCalculator
{
    public static List<PhonemeTimingEntry> Calculate(
        int[] phonemeIds,                          // モデルに渡した音素ID配列
        float[] durations,                         // ONNXのdurations出力（フレーム数、float）
        Dictionary<string, int[]> phonemeIdMap,    // onnx.json の phoneme_id_map
        int sampleRate,                            // Hz（22050）
        int hopSize = 256)                         // デフォルト256
}
```

**piper-plus `TimingWriter.CalculateTiming` からの差分**:
- 引数型: `long[]` → `int[]`（uPiper内部では `int[]` を使用）
- 戻り値: `List<PhonemeTimingEntry>`（同一）

### 5.2 アルゴリズム

```
1. frameLength = (float)hopSize / sampleRate   // ≈ 0.01161秒
2. reverseIdMap = BuildReverseIdMap(phonemeIdMap)  // int[] → string 逆引き辞書
3. count = Math.Min(phonemeIds.Length, durations.Length)
4. currentTime = 0f
5. for i = 0 to count-1:
     id = phonemeIds[i]
     frameDuration = durations[i]
     if id is 0 or 1 or 2:        // PAD, BOS, EOS
         currentTime += frameDuration * frameLength
         continue                   // エントリ生成せず、時間のみ進める
     phoneme = reverseIdMap[id] ?? "?"
     startTime = currentTime
     currentTime += frameDuration * frameLength
     entries.Add(new PhonemeTimingEntry(phoneme, startTime, currentTime, currentTime - startTime))
6. return entries
```

### 5.3 音素文字列の逆引き

`BuildReverseIdMap()` が `phoneme_id_map`（`Dictionary<string, int[]>`）を反転して `Dictionary<int, string>` を構築。

- PUA単文字キー: `PuaTokenMapper` の逆引き（`UnmapChar`）で多文字トークンに変換（例: `\uE000` → `a:`, `\uE019` → `N_m`）
- 不明ID: printable ASCII (32-126) ならその文字、それ以外は `"?"` にフォールバック

### 5.4 hop_length の取得

| ソース | 優先度 | 備考 |
|---|---|---|
| onnx.json `audio.hop_size` | 1（最優先） | 現在の multilingual-test-medium には未定義 |
| デフォルト値 256 | 2（フォールバック） | VITSモデル標準値 |

**対応**: `PiperVoiceConfig` に `HopSize` プロパティを追加（nullable int）。未定義時はデフォルト256を使用。

---

## 6. キャッシュ拡張

### 6.1 CacheEntry 変更

```csharp
private readonly struct CacheEntry
{
    public readonly float[] Samples;
    public readonly int SampleRate;
    public readonly PhonemeTimingEntry[] Timings;  // 追加（nullable）
    public readonly long MemoryBytes;
}
```

### 6.2 設計判断

- **ハッシュキー**: 変更不要。durations は入力ではなく出力であり、同一入力（phonemeIds + prosody + パラメータ）なら同一 durations が再現される。
- **メモリ影響**: 音素数 × `PhonemeTimingEntry` サイズ（~32bytes）。100音素で ~3.2KB。音声データ（数万~数十万float）に比べて微小。
- **キャッシュヒット時**: タイミング情報も返却。`TryGet` の out パラメータに `PhonemeTimingEntry[]` を追加。

---

## 7. エッジケース一覧

| # | ケース | 期待動作 |
|---|---|---|
| E1 | durations 出力なしモデル（outputs.Count == 1） | `_supportsDurations = false`、タイミング null 返却、例外なし |
| E2 | durations.Length != phonemeIds.Length | `Math.Min(len)` で打ち切り、警告ログ出力 |
| E3 | durations 全ゼロ | 全エントリ `DurationSeconds == 0`、クラッシュしない |
| E4 | durations に負値 | `Math.Max(0, frameDuration)` でクランプ |
| E5 | 音素1個（BOS + 1音素 + EOS） | タイミングエントリ1件 |
| E6 | 空テキスト（音素0個） | 空リスト返却 |
| E7 | sampleRate == 0 / hopSize == 0 | `ArgumentOutOfRangeException` |
| E8 | Intersperse PAD 付き多言語モデル | PAD の durations はスキップ（id == 0） |
| E9 | 短テキスト（< 40 phonemeIds）+ durations | パディング位置の durations を除去後に計算 |
| E10 | 沈黙句分割（2句以上） | 句間無音オフセットが累積時間に反映 |
| E11 | キャッシュヒット + タイミング要求 | キャッシュからタイミング情報も返却 |

---

## 8. テスト要件

### 8.1 単体テスト

**ファイル**: `Assets/uPiper/Tests/Editor/AudioGeneration/TimingCalculatorTests.cs`

| テストケース | 検証内容 |
|---|---|
| 基本タイミング計算 | durations `[10.0, 5.0]` × `frameLength` で startSeconds/endSeconds が正確 |
| 特殊トークンスキップ | PAD(0), BOS(1), EOS(2) はエントリ生成せず時間のみ進行 |
| PUA 逆引き | PUA文字 `\uE000` → `"a:"` 等が正しく解決 |
| 未知ID フォールバック | phonemeIdMap に存在しないID → `"?"` |
| 長さ不一致 | phonemeIds.Length != durations.Length → Min で打ち切り |
| 空入力 | 空配列 → 空リスト |
| null 入力 | `ArgumentNullException` |
| 負の durations | クランプされてクラッシュしない |
| sampleRate/hopSize == 0 | `ArgumentOutOfRangeException` |

### 8.2 統合テスト

| テストケース | 検証内容 |
|---|---|
| パイプライン全体 | PhonemizeAsync → SynthesizeWithTimingAsync で結果取得可能 |
| タイミング精度 | 最終エントリの EndSeconds と AudioClip 長が概ね一致（許容 50ms） |
| キャッシュ整合 | キャッシュヒット時もタイミング情報が返却 |
| 短テキスト + durations | パディング除去後のエントリ数 == 元の実音素数 |
| 句分割 + タイミング | 句2のタイミングに句1の長さ + 無音オフセットが加算 |
| durations 非対応モデル | Timings == null、例外なし |

### 8.3 テスト構造

- 既存パターン: `[TestFixture]` + NUnit `[Test]` / `[SetUp]`
- Stub 拡張: `StubInferenceAudioGenerator` に `float[] DurationsToReturn` プロパティ追加
- NativeArray: `try/finally` で Dispose

---

## 9. 非機能要件

| 項目 | 要件 |
|---|---|
| GC アロケーション | `TimingCalculator.Calculate` は `List<T>` 1回のみ確保 |
| パフォーマンス | 100音素のタイミング計算 < 0.1ms（O(n) ループ） |
| メモリ | durations の float[] コピーは最大数百要素（~数KB） |
| スレッドセーフ | `TimingCalculator` は static メソッド、状態なし |
| AudioNormalizer との独立性 | 正規化は振幅スケーリングのみ。サンプル数不変、タイミング影響なし |

---

## 10. 変更ファイル一覧

| ファイル | 変更種別 | 内容 |
|---|---|---|
| `AudioGeneration/PhonemeTimingResult.cs` | **新規** | PhonemeTimingEntry readonly struct |
| `AudioGeneration/SynthesisWithTimingResult.cs` | **新規** | AudioClip + タイミング結果 sealed class |
| `AudioGeneration/InferenceOutput.cs` | **新規** | 内部推論結果構造体（IDisposable） |
| `AudioGeneration/TimingCalculator.cs` | **新規** | フレーム→秒変換、逆引き辞書構築 |
| `AudioGeneration/IInferenceAudioGenerator.cs` | **変更** | 戻り値型: `NativeArray<float>` → `InferenceOutput` |
| `AudioGeneration/InferenceAudioGenerator.cs` | **変更** | durations テンソル読み取り追加 |
| `AudioGeneration/ShortTextMitigatingGenerator.cs` | **変更** | 戻り値型追従 + PAD位置 durations 除去 |
| `AudioGeneration/SplitInferenceOrchestrator.cs` | **変更** | 戻り値型追従 + 句ごと durations 結合 |
| `AudioGeneration/TTSSynthesisOrchestrator.cs` | **変更** | TimingCalculator 呼び出し + SynthesisWithTimingResult 構築 |
| `AudioGeneration/AudioSynthesisCache.cs` | **変更** | CacheEntry にタイミング格納 |
| `Core/IPiperTTS.cs` | **変更** | `SynthesizeWithTimingAsync` 追加 |
| `Core/PiperTTS.Inference.cs` | **変更** | 新メソッド実装 |
| `Tests/Editor/AudioGeneration/TimingCalculatorTests.cs` | **新規** | 単体テスト |
| `Tests/Editor/AudioGeneration/StubInferenceAudioGenerator.cs` | **変更** | durations 対応 |

---

## 11. 受入基準

1. `TimingCalculatorTests` が piper-plus `TimingWriterTests` と同等のカバレッジを持つ（基本計算、特殊トークンスキップ、PUA 逆引き、空入力、境界値）
2. `ShortTextMitigatingGenerator` にパディング除去テストが追加され、除去後のタイミングエントリ数 == 元の実音素数
3. `SplitInferenceOrchestrator` に複数句タイミングオフセットテストが追加
4. durations 出力なしモデルで例外が発生しないことを確認するテスト
5. 既存テスト全件パス（リグレッションなし）
6. 既存公開API（`SynthesizeAsync`, `GenerateAudioAsync`）の動作に変更がないこと

---

## 12. 実装順序（推奨）

```
Phase 1: 基盤型定義
  ├─ PhonemeTimingEntry (readonly struct)
  ├─ SynthesisWithTimingResult (sealed class)
  ├─ InferenceOutput (internal struct)
  └─ TimingCalculator (static class) + 単体テスト

Phase 2: 推論レイヤー
  ├─ IInferenceAudioGenerator 戻り値型変更
  ├─ InferenceAudioGenerator durations 読み取り
  └─ ShortTextMitigatingGenerator 追従

Phase 3: オーケストレーション
  ├─ SplitInferenceOrchestrator durations 結合
  ├─ TTSSynthesisOrchestrator タイミング計算パイプライン
  └─ AudioSynthesisCache 拡張

Phase 4: 公開API
  ├─ IPiperTTS.SynthesizeWithTimingAsync 追加
  ├─ PiperTTS.Inference.cs 実装
  └─ 統合テスト
```
