# フォネムタイミング機能 — 技術調査レポート

**調査日**: 2026-04-11
**対象仕様**: `docs/phoneme-timing-spec.md`

---

## 1. piper-plus 参考実装

### 1.1 TimingWriter.cs

**パス**: `piper-plus/src/csharp/PiperPlus.Core/Inference/TimingWriter.cs`

`CalculateTiming()` (L70) のアルゴリズム:

```csharp
float frameLength = (float)hopSize / sampleRate;  // 256/22050 ≈ 0.01161 sec
for (int i = 0; i < count; i++) {
    long id = phonemeIds[i];
    if (id is 0 or 1 or 2) {           // PAD=0, BOS=1, EOS=2
        currentTime += durations[i] * frameLength;
        continue;                        // 時間は進めるが出力エントリは生成しない
    }
    float startTime = currentTime;
    currentTime += durations[i] * frameLength;
    // PhonemeTimingEntry(phoneme, start, end, duration) を生成
}
```

**重要ポイント**:
- 特殊トークン（PAD/BOS/EOS）は時間カーソルを進めるが、出力エントリには含めない
- `frameLength` の単位は**秒**（仕様書のミリ秒変換は呼び出し側の責務）

### 1.2 durations テンソル読み取り

**パス**: `piper-plus/src/csharp/PiperPlus.Core/Inference/PiperSession.cs` (L348-374)

```csharp
// 出力名 "durations" を "output" と共にリクエスト
ReadOnlySpan<float> durSpan = results[1].GetTensorDataAsSpan<float>();
durations = durSpan.ToArray();
```

**能力検出**: `PiperModel` コンストラクタ内で:
```csharp
HasDurationOutput = session.OutputMetadata.ContainsKey("durations");
```

### 1.3 VITS モデル側の durations 生成

**パス**: `piper-plus/src/python/piper_train/vits/models.py` (L1118-1119)

```python
w = torch.exp(logw) * x_mask * length_scale
durations = w.squeeze(1)   # shape: [batch, phoneme_length]
```

- durations は **連続浮動小数点値**（整数フレーム数ではない）
- Duration Predictor の出力を `exp()` で変換した値
- `length_scale` パラメータで話速調整済み

### 1.4 定数定義箇所

| 定数 | 値 | 定義箇所 |
|---|---|---|
| `hop_length` | 256 | C++: `piper.cpp` L323 (`DEFAULT_HOP_SIZE`), C#: デフォルトパラメータ `hopSize = 256` |
| `sample_rate` | 22050 | `config.json` の `audio.sample_rate`、`PiperConfig.Audio.SampleRate` |

`hop_size` は `config.json["audio"]["hop_size"]` からも読み取り可能（オプション）。

---

## 2. ONNXモデル出力テンソル

### 2.1 multilingual-test-medium モデル

**設定ファイル**: `Assets/uPiper/Resources/Models/multilingual-test-medium.onnx.json`

モデルは **2つの出力テンソル** を持つ:

| 出力名 | 形状 | データ型 | 説明 |
|---|---|---|---|
| `output` | `[batch_size, 1, time]` | float32 | 生成音声波形 |
| `durations` | `[batch_size, phonemes]` | float32 | 音素ごとのフレーム持続時間 |

両軸とも動的軸（`time`, `phonemes`）。

### 2.2 エクスポート定義

**パス**: `piper-plus/src/python/piper_train/export_onnx.py` (L519)

```python
output_names = ["output", "durations"]
```

`build_infer_forward()` が `model.infer()` の5要素出力 `(audio, attn, y_mask, latents, durations)` から `(audio, durations)` のみを返す。

### 2.3 onnx.json 内の音声パラメータ

- `sample_rate`: **22050** — 定義あり
- `hop_length`: **定義なし** — onnx.json に含まれていない → ハードコード(256)または設定追加が必要

### 2.4 Python側での使用例

**パス**: `piper-plus/src/python/infer_onnx.py` (L756-759)

```python
outputs = model.run(None, inputs)
audio = outputs[0].squeeze(0)
durations = outputs[1] if len(outputs) > 1 else None
```

---

## 3. uPiper 推論パイプライン現状

### 3.1 InferenceAudioGenerator.cs

**パス**: `Assets/uPiper/Runtime/Core/AudioGeneration/InferenceAudioGenerator.cs`

#### 出力名キャッシュ (L176-182)

現在 `_model.outputs[0].name` のみをキャッシュ。出力が0個なら例外。

```csharp
_cachedOutputName = _model.outputs[0].name;
```

**変更方針**: `_model.outputs.Count >= 2` で2番目の出力名もキャッシュ。

#### ExtractResults() (L436-463)

3段階処理:
1. `_worker.PeekOutput(_cachedOutputName)` で Worker 所有の `Tensor<float>` を取得（Dispose不要）
2. `outputTensor.ReadbackAndClone()` で GPU→CPU 読み戻しコピー
3. `readableTensor.DownloadToNativeArray()` → `NativeArray<float>.Copy()` で `Allocator.Persistent` にバルクコピー

**変更方針**: 同じパターンで `durations` テンソルも抽出。

#### GenerateAudioAsync() (L234-267)

- 戻り値: `Task<NativeArray<float>>`
- `MainThreadDispatcher` 経由で `ExecuteInference` を呼び出し
- 3段階パイプライン: `PrepareInputs` → `RunInference (_worker.Schedule())` → `ExtractResults`

### 3.2 IInferenceAudioGenerator.cs

```csharp
Task<NativeArray<float>> GenerateAudioAsync(
    int[] phonemeIds, float[] prosodyFlat, float lengthScale,
    float noiseScale, float noiseW, int speakerId, int languageId,
    CancellationToken cancellationToken);
```

**変更必須**: 戻り値を `InferenceResult` 構造体に拡張。

### 3.3 ShortTextMitigatingGenerator.cs

- 純粋なデコレータパターン
- `_inner.GenerateAudioAsync()` の前後でパディング/トリム適用 (L67-89)
- **影響**: 戻り値型変更に追従が必要
- **注意**: パディング追加時に durations 配列の対応関係が崩れる可能性 → パディングで追加した音素IDに対応する durations エントリの扱いを検討

### 3.4 SplitInferenceOrchestrator.cs

- `_generator.GenerateAudioAsync()` を句ごとに呼び出し (L80-84)
- `NativeArray<float>` を結合 (L102-110)
- **影響**: 句ごとの durations 配列の結合ロジックが必要
- `PhonemeSilenceProcessor.SplitAtPhonemeSilence` の分割境界に合わせてマージ

---

## 4. uPiper 公開API現状

### 4.1 IPiperTTS.cs (L1-152)

主要メソッド:

| メソッド | 戻り値 | 説明 |
|---|---|---|
| `PhonemizeAsync(text, ct)` | `Task<PhonemizeResult>` | 音素化 |
| `SynthesizeAsync(request, ct)` | `Task<AudioClip>` | 低レベル合成 |
| `GenerateAudioAsync(text, ...)` | `Task<AudioClip>` | 高レベルTTS（3オーバーロード） |

**全メソッドが `AudioClip` のみ返却。タイミングデータのパスは存在しない。**

### 4.2 TTSSynthesisOrchestrator.cs (L66-177)

パイプライン:
```
Encode phonemes (L73-87)
    → Cache check (L90-115)
    → generator.GenerateAudioAsync() or splitOrchestrator (L127-145)
    → AudioNormalizer (L148-151)
    → Cache set (L154-157)
    → AudioClipBuilder.BuildAudioClip() (L159-162)
    → return AudioClip
```

### 4.3 AudioSynthesisCache.cs (L1-221)

- LRUキャッシュ、FNV-1a 64bit ハッシュ
- `CacheEntry` (L16-27): `float[] Samples` + `int SampleRate` のみ
- **影響**: タイミングデータをキャッシュに含める場合、`CacheEntry` の拡張 + `TryGet`/`Set` のシグネチャ変更が必要

### 4.4 SynthesisRequest.cs (L1-201)

`public readonly struct`: `Phonemes[]`, `ProsodyFlat[]`, `LengthScale`, `NoiseScale`, `NoiseW`, `SpeakerId`, `LanguageId`
タイミング関連フィールドなし。

### 4.5 PhonemizeResult.cs (L1-40)

`public sealed class`: `Phonemes[]`, `ProsodyFlat[]`, `DetectedLanguage`, `ResolvedLanguageId`
タイミング関連フィールドなし。

---

## 5. 仕様書との差分・追加発見事項

### 5.1 仕様書の正確性確認

| 仕様書の記述 | 調査結果 | 判定 |
|---|---|---|
| durations テンソルが存在する | multilingual-test-medium で確認済み（2出力） | **正確** |
| 形状: `[1, phoneme_length]` | `[batch_size, phonemes]` (動的軸) | **正確** |
| データ型: float32 | float32 確認済み | **正確** |
| PAD=0, BOS=1, EOS=2 スキップ | piper-plus で確認。ただし時間カーソルは進める | **補足必要** |
| `_model.outputs[0].name` でキャッシュ | L176-182 で確認 | **正確** |
| hop_length=256, sample_rate=22050 | piper-plus で確認。onnx.json に hop_length なし | **補足必要** |

### 5.2 仕様書に記載のない発見事項

#### (A) durations は連続浮動小数点値

仕様書では「各音素のフレーム数」と記載されているが、実際には Duration Predictor の出力を `exp()` 変換した**連続浮動小数点値**。整数フレーム数ではないため、`TimingCalculator` で直接乗算して問題ない（切り捨て/丸め不要）。

#### (B) hop_length の設定ソース

onnx.json に `hop_length` は含まれていない。piper-plus C++ 側では `DEFAULT_HOP_SIZE = 256` をハードコードし、`config.json["audio"]["hop_size"]` があればオーバーライドする設計。uPiper 側でも同様にデフォルト256 + onnx.json オプション読み取りとすべき。

#### (C) ShortTextMitigatingGenerator のパディング影響

仕様書では言及されていないが、短テキスト緩和で音素IDにPADを挿入した場合、ONNX推論結果の durations 配列にはパディング分の要素も含まれる。タイミング計算時にパディング位置のエントリを除外するか、パディング前の元の音素配列との対応関係を保持する仕組みが必要。

#### (D) SplitInferenceOrchestrator の句分割

沈黙句分割時、各句ごとに `GenerateAudioAsync()` が呼ばれるため、durations も句ごとに分割される。最終的なタイミング結果を構築するには:
1. 各句の durations を取得
2. 時間オフセットを累積しながら句ごとのタイミングを結合
3. 句間の無音時間（`SilenceSamples`）も考慮

#### (E) キャッシュ拡張の設計判断

`AudioSynthesisCache.CacheEntry` にタイミングデータを追加すると:
- メモリ使用量が増加（音素数 × エントリサイズ）
- ただし音声データ（`float[] Samples`）に比べれば微小
- **推奨**: `CacheEntry` に `PhonemeTimingEntry[]?` フィールドを追加。キャッシュヒット時もタイミングデータを返却可能に。

#### (F) 既存API互換性の戦略

`SynthesizeAsync()` の戻り値を `AudioClip` から `SynthesisResult` に変更すると破壊的変更になる。仕様書の方針（既存メソッド維持 + 新規 `SynthesizeWithTimingAsync` 追加）は妥当。ただし内部的には `TTSSynthesisOrchestrator` は常に `SynthesisResult` を返し、既存メソッドは `.AudioClip` のみを返却するラッパーとするのが効率的。

---

## 6. 変更影響範囲まとめ

```
レイヤー              変更内容                            影響度
─────────────────────────────────────────────────────────────
IInferenceAudioGenerator  戻り値: NativeArray<float> → InferenceResult   破壊的(internal)
InferenceAudioGenerator   durations テンソル読み取り追加                  中
ShortTextMitigatingGenerator  戻り値型追従 + パディング対応               中
SplitInferenceOrchestrator    句ごと durations 結合ロジック追加           高
TTSSynthesisOrchestrator  TimingCalculator呼び出し + SynthesisResult返却  中
AudioSynthesisCache       CacheEntry拡張（タイミングデータ格納）          低
IPiperTTS                 SynthesizeWithTimingAsync 追加                  非破壊的(public)
PiperTTS                  新メソッド実装                                  中
新規: PhonemeTimingResult  データ構造定義                                 新規
新規: TimingCalculator     フレーム→時間変換                              新規
新規: InferenceResult      推論結果構造体                                 新規
```

---

## 7. リスクと推奨事項

| リスク | 影響 | 推奨対応 |
|---|---|---|
| ShortTextMitigatingGenerator のパディングが durations の音素対応を崩す | タイミング不正確 | パディング位置のマスク配列を保持し、タイミング計算時に除外 |
| SplitInferenceOrchestrator の句間無音がタイミングに反映されない | 累積時間のズレ | 句間 SilenceSamples を時間オフセットとして加算 |
| hop_length が onnx.json に未定義 | 将来のモデルで値が異なる可能性 | デフォルト256 + onnx.json `audio.hop_size` オプション読み取り |
| durations 出力がないモデル | 機能無効化が必要 | `_hasDurationsOutput` フラグで分岐（仕様書通り） |
| CacheEntry 拡張によるメモリ増加 | 微小（音声データ比で無視可能） | そのまま実装して問題なし |
