# フォネムタイミング機能 — 実装完了サマリー

**完了日**: 2026-04-12
**ブランチ**: feature/phoneme-timing-spec

---

## 1. 機能概要

VITSモデルのONNX出力 `durations` テンソル（各音素のスペクトログラムフレーム持続時間、連続浮動小数点値）を読み取り、音素ごとの開始/終了時刻（秒単位）を計算して公開APIで返却する機能。

piper-plus の `TimingWriter.CalculateTiming()` アルゴリズムを移植し、uPiper の既存パイプライン（短テキスト緩和、句分割推論、LRUキャッシュ）と統合した。

### ユースケース

- **Live2Dリップシンク**: ParamMouthOpenY / ParamMouthForm を音素タイミングに同期して制御
- **3Dモデルのブレンドシェイプ制御**: 口形状モーフターゲットの時間駆動
- **字幕・カラオケ表示**: 音素/単語レベルのハイライト同期
- **VRM Visemeマッピング**: VRM仕様の Viseme ブレンドシェイプを音素タイミングで駆動

### 技術的背景

VITSモデルの Duration Predictor は各音素のスペクトログラムフレーム数を出力する。実際には `exp(logw) * x_mask * length_scale` で算出された連続浮動小数点値であり、整数フレーム数ではない。1フレームの時間は `hop_length / sample_rate`（デフォルト: 256 / 22050 = 約11.6ms）で、durations 値に乗算することで秒単位のタイミングを得る。

### 定数

| 定数 | 値 | 説明 |
|---|---|---|
| hop_length | 256 | VITSスペクトログラムのホップ長（サンプル数） |
| sample_rate | 22050 | piper-plus mediumモデルのサンプルレート |
| 1フレーム | 約11.6ms | (256 / 22050) * 1000 |
| PAD ID | 0 | タイミング計算でスキップ（時間カーソルは進行） |
| BOS ID | 1 | タイミング計算でスキップ（時間カーソルは進行） |
| EOS ID | 2 | タイミング計算でスキップ（時間カーソルは進行） |

---

## 2. 公開API

```csharp
// 使用例
var result = await piperTTS.PhonemizeAsync("こんにちは");
var request = SynthesisRequest.FromPhonemesWithProsody(
    result.Phonemes, result.ProsodyFlat);
var timing = await piperTTS.SynthesizeWithTimingAsync(request);

// timing.AudioClip — 音声データ
// timing.HasTimings — タイミング情報が利用可能か
// timing.Timings[i].Phoneme — 音素文字列（"a:", "k", "N_m" 等）
// timing.Timings[i].StartSeconds — 開始時刻（秒）
// timing.Timings[i].EndSeconds — 終了時刻（秒）
// timing.Timings[i].DurationSeconds — 持続時間（秒、算出プロパティ）
// timing.TotalDurationSeconds — 総再生時間
```

### 既存API互換性

| メソッド | 変更 | 備考 |
|---|---|---|
| `SynthesizeAsync(SynthesisRequest, CancellationToken)` | 変更なし | `Task<AudioClip>` を返す |
| `GenerateAudioAsync(string, ...)` | 変更なし | 3オーバーロード全て維持 |
| `PhonemizeAsync(string, CancellationToken)` | 変更なし | タイミングは合成時に生成 |
| `ClearCache()` | 変更なし | キャッシュ構造は内部変更のみ |

**破壊的変更**: 公開APIには一切なし。内部インターフェース（`IInferenceAudioGenerator`）の戻り値型変更は internal スコープのため影響なし。

### 制限事項

- 句分割推論（EnablePhonemeSilence）使用時はタイミング情報非提供（HasTimings == false）
- durations 出力テンソルを持たないモデルではタイミング情報非提供
- 高レベルAPI（GenerateAudioAsync）にはタイミング版なし（低レベルAPI専用）

---

## 3. 実装Phase構成

| Phase | 内容 | チケット数 |
|-------|------|-----------|
| Phase 1: 基盤型定義 | PhonemeTimingEntry, InferenceOutput, SynthesisWithTimingResult, TimingCalculator | 9 (impl 4 + test 5) |
| Phase 2: 推論レイヤー | IInferenceAudioGenerator 戻り値変更, durations読み取り, ShortTextMitigating追従, Stub対応 | 6 (impl 4 + test 2) |
| Phase 3: オーケストレーション | SplitInferenceOrchestrator, TTSSynthesisOrchestrator TimingCalculator統合, AudioSynthesisCache拡張 | 6 (impl 3 + test 3) |
| Phase 4: 公開API | IPiperTTS拡張, PiperTTS.Inference.cs実装, 統合テスト | 7 (impl 2 + test 5) |
| **合計** | | **28 (impl 13 + test 15)** |

### データフロー

```
ONNX outputs[1] "durations" (float32, [batch_size, phonemes])
    |
    v
InferenceAudioGenerator.ExtractResults()
    -> NativeArray<float> durations (Allocator.Persistent)
    -> InferenceOutput { Audio, Durations }
    |
    v
[ShortTextMitigatingGenerator] -- PAD位置のdurations要素を除去
    |
    v
[SplitInferenceOrchestrator] -- 句分割時はdurationsを default で返却
    |
    v
TTSSynthesisOrchestrator
    -> TimingCalculator.Calculate(phonemeIds, durations, phonemeIdMap, sampleRate, hopSize)
    -> PhonemeTimingEntry[] -> SynthesisWithTimingResult
    |
    v
PiperTTS.SynthesizeWithTimingAsync() -> SynthesisWithTimingResult
```

---

## 4. 主要な設計判断

### InferenceOutput (sealed class)

- `readonly struct` から `sealed class` に変更（defensive copy問題の解消）
- `DetachAudio()` / `DetachDurations()` による所有権移転パターン
- `#if ENABLE_UNITY_COLLECTIONS_CHECKS` 条件付きファイナライザ
- durations 未対応モデルは `Durations.IsCreated == false` で判定

### IModelCapabilities 分離

- IInferenceAudioGenerator から SupportsProsody/SupportsDurations 等を分離
- デコレータ（ShortTextMitigatingGenerator）の委譲コスト削減

### TTSSynthesisOrchestrator 2段構成

- SynthesizeWithTimingCoreAsync（private共通コア）
- SynthesizeAsync（既存、AudioClipのみ返却）
- SynthesizeWithTimingAsync（新規、SynthesisWithTimingResult返却）

### 句分割時タイミング非提供

- SplitInferenceOrchestrator は durations を結合せず default で返却
- TTSSynthesisOrchestrator.CalculateTimings は usedSilenceSplit 時に null 返却
- 将来対応: SplitInferenceResult 型導入 + 句ごとTimingCalculator呼び出し

### TimingCalculator アルゴリズム（piper-plus移植）

- `frameLength = (float)hopSize / sampleRate`（約0.01161秒）
- PAD(0)/BOS(1)/EOS(2) スキップ（時間カーソルは進行、エントリ生成なし）
- 逆引き辞書構築（`BuildReverseIdMap`）で phonemeIdMap を反転
- PUA文字は `PuaTokenMapper.UnmapChar` で人間可読文字列に変換
- エッジケース: 負 durations はクランプ、長さ不一致は `Math.Min` で打ち切り + 警告

### 短テキスト緩和のPAD除去

- `ShortTextProcessor.PadPhonemeIds` がBOS後/EOS前にPADトークンを挿入
- 推論後にパディング位置（`deficit/2`, `deficit - deficit/2`）のdurations要素を除去
- 除去後のエントリ数が元の実音素数と一致することをテストで検証

---

## 5. piper-plus 参照実装

| コンポーネント | パス | uPiperでの対応 |
|---|---|---|
| `TimingWriter.CalculateTiming()` | `PiperPlus.Core/Inference/TimingWriter.cs` L70 | `TimingCalculator.Calculate()` |
| `TimingWriter.PhonemeTimingEntry` | 同上 | `PhonemeTimingEntry` (readonly struct) |
| durations テンソル読み取り | `PiperSession.cs` L348-374 | `InferenceAudioGenerator.ExtractResults()` |
| 能力検出 | `PiperModel` コンストラクタ | `InferenceAudioGenerator._supportsDurations` |
| durations 生成（Python） | `vits/models.py` L1118-1119 | モデル側（変更なし） |
| ONNX エクスポート定義 | `export_onnx.py` L519 | モデル側（変更なし） |

---

## 6. 追加・変更ファイル一覧

### 新規ファイル

- `PhonemeTimingEntry.cs` — 公開データ型 (readonly struct)
- `SynthesisWithTimingResult.cs` — 公開結果型 (sealed class)
- `InferenceOutput.cs` — 推論出力保持型 (sealed class, IDisposable)
- `TimingCalculator.cs` — タイミング計算ロジック (static class)
- `IModelCapabilities.cs` — モデルケイパビリティインターフェース
- `TestHelpers.cs` — テスト共通ヘルパー

### 主要変更ファイル

- `IInferenceAudioGenerator.cs` — internal化、戻り値型変更、Capabilities追加
- `InferenceAudioGenerator.cs` — durations読み取り、SupportsDurations
- `ShortTextMitigatingGenerator.cs` — InferenceOutput対応、PAD除去
- `SplitInferenceOrchestrator.cs` — InferenceOutput戻り値
- `TTSSynthesisOrchestrator.cs` — TimingCalculator統合、2段構成
- `AudioSynthesisCache.cs` — PhonemeTimingEntry[]格納、CacheEntry拡張
- `IPiperTTS.cs` — SynthesizeWithTimingAsync宣言
- `PiperTTS.Inference.cs` — SynthesizeWithTimingAsync実装
- `AudioClipBuilder.cs` — SetData失敗時クリーンアップ

---

## 7. テスト（161メソッド）

| テストファイル | メソッド数 |
|--------------|-----------|
| TimingCalculatorTests.cs | 41 |
| AudioSynthesisCacheTests.cs | 27 |
| TTSSynthesisOrchestratorTests.cs | 24 |
| PiperTTSSynthesizeWithTimingTests.cs | 22 |
| SplitInferenceOrchestratorTests.cs | 16 |
| InferenceOutputTests.cs | 13 |
| ShortTextMitigatingGeneratorTimingTests.cs | 10 |
| PiperTTSSynthesizeTests.cs | 8 |

---

## 8. エッジケース対応

| # | ケース | 対応 |
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
| E10 | 沈黙句分割（2句以上） | タイミング非提供（HasTimings == false） |
| E11 | キャッシュヒット + タイミング要求 | キャッシュからタイミング情報も返却 |

---

## 9. レビューで得られた教訓

- チケット分割は「暫定コード不要な最小単位」で切る（P2-1〜P2-4統合が望ましかった）
- Phase単位レビューは有効（2回のサイクルで12件以上のテスト追加と2設計変更を実現）
- DetachAudio パターンは中間変換が必要な箇所のみで使用し、最終消費者はDispose一括解放が最適
- トップダウン設計（公開API先行）を検討すべきだった
- hop_length は onnx.json に未定義のため、デフォルト256 + PiperVoiceConfig.HopSize オプション読み取りで対応
