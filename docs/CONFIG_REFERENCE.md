# 設定パラメータリファレンス

uPiper の全設定パラメータの包括的なリファレンスドキュメント。

---

## 目次

1. [概要](#概要)
2. [一般設定 (General Settings)](#一般設定-general-settings)
3. [言語設定 (Language Settings)](#言語設定-language-settings)
4. [フォールバック設定 (Fallback Settings)](#フォールバック設定-fallback-settings)
5. [パフォーマンス設定 (Performance Settings)](#パフォーマンス設定-performance-settings)
6. [沈黙句分割設定 (Sentence Silence Settings)](#沈黙句分割設定-sentence-silence-settings)
7. [音声設定 (Audio Settings)](#音声設定-audio-settings)
8. [詳細設定 (Advanced Settings)](#詳細設定-advanced-settings)
9. [GPU設定 (GPU Inference Settings)](#gpu設定-gpu-inference-settings)
10. [合成パラメータ (Synthesis Parameters)](#合成パラメータ-synthesis-parameters)
11. [プリセット比較表](#プリセット比較表)
12. [InferenceBackend 列挙型](#inferencebackend-列挙型)
13. [MultiLanguageMode 列挙型](#multilanguagemode-列挙型)
14. [PiperVoiceConfig（音声モデル設定）](#pipervoiceconfig音声モデル設定)
15. [バリデーションの仕組み](#バリデーションの仕組み)

---

## 概要

### 設定のライフサイクル

uPiper の設定は以下の3段階で管理される。

```
PiperConfig (mutable)
  Inspector で編集可能な可変クラス。
  [Serializable] で Unity のシリアライズ対象。
      │
      │ PiperConfig.ToValidated()
      ▼
ValidatedPiperConfig (immutable)
  バリデーション・クランプ・正規化済みの不変スナップショット。
  IPiperConfigReadOnly インターフェースを実装。
  6つの readonly record struct に分類:
    Language / Performance / Inference / Audio / Silence / General
      │
      │ InitializeAsync() 内で使用
      ▼
ランタイム (immutable)
  初期化後は設定変更不可。
```

### PiperConfigAsset（ScriptableObject ラッパー）

`PiperConfigAsset` は `PiperConfig` の ScriptableObject ラッパーであり、プロジェクトアセットとして永続化し Inspector で編集できる。

```csharp
// ScriptableObject アセットの作成
// メニュー: Assets > Create > uPiper > Config Asset

// ランタイムでの使用
PiperConfig runtimeCopy = configAsset.CreateRuntimeCopy(); // ディープコピー
ValidatedPiperConfig validated = configAsset.ToValidated(); // バリデーション済みスナップショット
```

**重要**: `CreateRuntimeCopy()` でディープコピーを取得すること。`Config` プロパティを直接変更すると ScriptableObject の元データが書き換わる。

---

## 一般設定 (General Settings)

| パラメータ | 型 | デフォルト | 範囲 | 説明 |
|---|---|---|---|---|
| `EnableDebugLogging` | `bool` | `false` | - | 詳細なデバッグログの出力を有効にする。開発・トラブルシューティング時に使用。 |
| `DefaultLanguage` | `string` | `"ja"` | ISO 639-1（2文字）または BCP 47（5文字: `"ja-JP"`） | テキスト処理のデフォルト言語コード。バリデーション時に小文字化・トリムされる。2文字または5文字以外の場合は警告ログが出力される。 |
| `AutoDetectLanguage` | `bool` | `false` | - | 入力テキストからの自動言語検出を有効にする。多言語モデルを使用する場合に `true` に設定する。 |

---

## 言語設定 (Language Settings)

| パラメータ | 型 | デフォルト | 範囲 | 説明 |
|---|---|---|---|---|
| `SupportedLanguages` | `List<string>` | `["ja", "en"]` | - | 多言語モードで使用する対応言語リスト。`AutoDetectLanguage` が `true` の場合に参照される。 |
| `MixedLanguageMode` | `MultiLanguageMode` | `SegmentByLanguage` | 列挙型（後述） | 複数言語が混在するテキストの処理方針。 |

---

## フォールバック設定 (Fallback Settings)

| パラメータ | 型 | デフォルト | 範囲 | 説明 |
|---|---|---|---|---|
| `FallbackLanguage` | `string` | `null` | 言語コード or `null` | 非対応言語セグメントのフォールバック言語。検出された言語に対応する G2P ハンドラがない場合、この言語のハンドラで処理する。`null`（デフォルト）の場合、非対応セグメントはスキップされる。 |

---

## パフォーマンス設定 (Performance Settings)

| パラメータ | 型 | デフォルト | 範囲 | バリデーション | 説明 |
|---|---|---|---|---|---|
| `MaxCacheSizeMB` | `int` | `100` | 10 - 500 | `Mathf.Clamp(value, 10, 500)` | 音素キャッシュの最大サイズ（MB）。範囲外の場合はクランプされ警告ログが出力される。 |
| `EnablePhonemeCache` | `bool` | `true` | - | - | 音素変換結果のキャッシュを有効にする。同一テキストの再変換をスキップしてパフォーマンスを向上させる。 |
| `EnableAudioCache` | `bool` | `true` | - | - | 音声合成結果のキャッシュを有効にする。同一テキスト・パラメータでの再合成時に ONNX 推論をスキップする。 |
| `MaxAudioCacheEntries` | `int` | `50` | 1 - 200 | `Mathf.Clamp(value, 1, 200)` | 音声合成キャッシュの最大エントリ数。各エントリは `float[]` 音声データを保持するため、メモリ使用量は音声の長さに依存する。 |
| `WorkerThreads` | `int` | `0` | 0 - 16 | `0` = `Max(1, processorCount - 1)` に自動設定。1-16 にクランプ。 | 並列処理のワーカースレッド数。`0` を指定すると CPU コア数に基づいて自動検出される。 |
| `EnableMultiThreadedInference` | `bool` | `false` | - | - | マルチスレッド推論を有効にする（実験的機能）。 |
| `InferenceBatchSize` | `int` | `1` | 1 - 32 | `Mathf.Clamp(value, 1, 32)` | ニューラルネットワーク推論のバッチサイズ。 |
| `Backend` | `InferenceBackend` | `Auto` | 列挙型（後述） | - | 推論バックエンドの種類。詳細は [InferenceBackend 列挙型](#inferencebackend-列挙型) を参照。 |

---

## 沈黙句分割設定 (Sentence Silence Settings)

| パラメータ | 型 | デフォルト | 範囲 | 説明 |
|---|---|---|---|---|
| `EnablePhonemeSilence` | `bool` | `false` | - | 沈黙トークンによる句分割を有効にする。有効にすると長文の自然さが向上する（句切りで息継ぎ風の間を挿入）。 |
| `PhonemeSilenceSpec` | `string` | `"_ 0.5"` | `"<音素> <秒数>"` 形式（カンマ区切り） | 沈黙トークンと無音秒数の指定。バリデーション時に `PhonemeSilenceProcessor.Parse()` でパースされ、不正な場合は `PiperException` がスローされる。 |

### PhonemeSilenceSpec の記法

```
# 単一指定: 読点で0.5秒の無音
"_ 0.5"

# 複数指定: 読点0.5秒 + 句点0.3秒
"_ 0.5,# 0.3"
```

バリデーション済みの `SilenceSettings` には `ParsedPhonemeSilence`（`IReadOnlyDictionary<string, float>`）としてパース結果が格納される。

---

## 音声設定 (Audio Settings)

| パラメータ | 型 | デフォルト | 範囲 | バリデーション | 説明 |
|---|---|---|---|---|---|
| `SampleRate` | `int` | `22050` | 8000 - 48000 | 範囲外は `PiperException`。16000/22050/44100/48000 以外は警告ログ出力。 | 音声出力のサンプルレート（Hz）。モデルのネイティブサンプルレートに合わせること（通常 22050Hz）。 |
| `NormalizeAudio` | `bool` | `true` | - | - | 音声出力の音量正規化を有効にする。 |
| `TargetRMSLevel` | `float` | `-20.0` | -40.0 - 0.0（dB） | `NormalizeAudio` が `true` の場合のみクランプ適用: `Mathf.Clamp(value, -40, 0)` | 音声正規化のターゲット RMS レベル（dB）。`-20dB` が標準的な音量レベル。 |

### TargetRMSLevel の目安

| 値 | 音量 | 用途 |
|---|---|---|
| `-40dB` | 非常に小さい | バックグラウンド音声 |
| `-20dB` | 標準 | 通常の音声出力（デフォルト） |
| `-10dB` | 大きい | 強調音声 |
| `0dB` | 最大 | クリッピングに注意 |

---

## 詳細設定 (Advanced Settings)

| パラメータ | 型 | デフォルト | 範囲 | バリデーション | 説明 |
|---|---|---|---|---|---|
| `EnableWarmup` | `bool` | `false` | - | - | モデル初期化後にウォームアップ推論を実行する。初回推論のレイテンシを約 500-800ms 削減する。 |
| `WarmupIterations` | `int` | `2` | 1 - 5 | `EnableWarmup` が `true` かつ値が 1 未満の場合、1 にクランプ。 | ウォームアップ推論の反復回数。ORT JIT キャッシュは 1-2 回で安定する。2 回が安全マージン込みの推奨値（piper-plus デフォルト）。 |
| `TimeoutMs` | `int` | `30000` | 0 以上 | 負値は `PiperException`。`0` = タイムアウトなし。1000ms 未満の場合は警告ログ出力。 | 操作のタイムアウト時間（ミリ秒）。 |
| `AllowFallbackToCPU` | `bool` | `true` | - | - | GPU 初期化に失敗した場合、自動的に CPU にフォールバックする。 |

---

## GPU設定 (GPU Inference Settings)

`PiperConfig.GPUSettings` フィールド（`GPUInferenceSettings` 型）でグループ化される。

| パラメータ | 型 | デフォルト | 範囲 | バリデーション | 説明 |
|---|---|---|---|---|---|
| `GPUSettings.MaxMemoryMB` | `int` | `512` | 128 - 2048 | `Mathf.Clamp(value, 128, 2048)`。`GPUSettings` が `null` の場合は 512 にフォールバック。 | GPU 推論に割り当てる最大メモリ（MB）。`BackendSelector` の Auto 選択時、VRAM がこの閾値以上の場合に GPU バックエンドが選択される。 |

---

## 合成パラメータ (Synthesis Parameters)

合成パラメータは `PiperVoiceConfig`（モデル設定のデフォルト値）と `SynthesisRequest`（リクエストごとのオーバーライド）の両方で指定できる。

### LengthScale（話速スケール）

VITS の `length_scale` パラメータ。音素の発話時間をスケーリングして全体の話速を制御する。

| 値 | 効果 | 速度変化 |
|---|---|---|
| `0.5` | 2倍速 | 100% 高速化 |
| `0.8` | やや速い | 25% 高速化 |
| `1.0` | 標準速度 | - |
| `1.2` | やや遅い | 20% 低速化 |
| `2.0` | 半分の速度 | 50% 低速化 |

- **型**: `float`
- **デフォルト**: `1.0`（`PiperVoiceConfig`）、`1.0`（`SynthesisRequest.FromPhonemes`）
- **推奨範囲**: `0.7` - `1.3`（極端な値は音質劣化の原因になる）
- **Inspector 範囲**: `0.1` - `2.0`

### NoiseScale（ノイズスケール）

VITS の `noise_scale` パラメータ。音素レベルでのピッチ・声質の変動量を制御する。

| 値 | 効果 |
|---|---|
| `0.0` | 変動なし（単調・ロボット的） |
| `0.4` | 控えめな変動（安定した出力） |
| `0.667` | デフォルト（自然な変動） |
| `0.8` | 表現豊かな変動 |
| `1.0+` | 非常に表現的（不安定になりうる） |

- **型**: `float`
- **デフォルト**: `0.667`（`PiperVoiceConfig`）、`0.667`（`SynthesisRequest.FromPhonemes`）
- **Inspector 範囲**: `0.0` - `2.0`

### NoiseW（ノイズ幅）

VITS の `noise_w` パラメータ。音素の発話時間（リズム・タイミング）の揺らぎを制御する。`NoiseScale`（声質変動）とは独立したパラメータ。

| 値 | 効果 |
|---|---|
| `0.0` | 均一なリズム（機械的） |
| `0.5` | 控えめなリズム変動 |
| `0.8` | デフォルト（自然なリズム） |
| `1.0+` | リズム変動大 |

- **型**: `float`
- **デフォルト**: `0.8`（`PiperVoiceConfig`）、`0.8`（`SynthesisRequest.FromPhonemes`）
- **Inspector 範囲**: `0.0` - `2.0`

### SpeakerId / LanguageId

| パラメータ | 型 | デフォルト | 説明 |
|---|---|---|---|
| `SpeakerId` | `int` | `0` | マルチスピーカーモデルの話者 ID。`PiperVoiceConfig.SpeakerIdMap` で名前から ID を取得可能。 |
| `LanguageId` | `int` | `0` | 多言語モデルの言語 ID。`0` = 日本語（注意: デフォルト値が日本語）。`PiperVoiceConfig.LanguageIdMap` で言語コードから ID を取得可能。 |

---

## プリセット比較表

用途に応じた推奨パラメータの組み合わせ。

| プリセット | LengthScale | NoiseScale | NoiseW | 用途 |
|---|---|---|---|---|
| **Fast** | `0.8` | `0.4` | `0.5` | リアルタイム応答。チャットボット、ゲーム内音声など即時性を重視する場面。 |
| **Natural**（デフォルト） | `1.0` | `0.667` | `0.8` | バランス型。通常の TTS 出力に最適。 |
| **HighQuality** | `1.1` | `0.8` | `1.0` | ナレーション、オーディオブック。自然さ・表現力を重視する場面。 |
| **Robotic** | `1.0` | `0.0` | `0.0` | 機械的な音声。アナウンス、システム音声向け。 |

### コード例

```csharp
// Fast プリセット
var request = SynthesisRequest.FromPhonemes(phonemes,
    lengthScale: 0.8f, noiseScale: 0.4f, noiseW: 0.5f);

// HighQuality プリセット
var request = SynthesisRequest.FromPhonemes(phonemes,
    lengthScale: 1.1f, noiseScale: 0.8f, noiseW: 1.0f);

// PhonemizeAsync 経由（Prosody 付き）
var result = await piperTTS.PhonemizeAsync("こんにちは");
var request = SynthesisRequest.FromPhonemesWithProsody(
    result.Phonemes, result.ProsodyFlat,
    lengthScale: 1.1f, noiseScale: 0.8f, noiseW: 1.0f);
var clip = await piperTTS.SynthesizeAsync(request);
```

---

## InferenceBackend 列挙型

推論バックエンドの選択肢。`PiperConfig.Backend` で指定する。

| 値 | 説明 | 推奨場面 |
|---|---|---|
| `Auto` | プラットフォーム・GPU 環境に応じて最適なバックエンドを自動選択する（デフォルト）。 | 特別な理由がない限り `Auto` を推奨。 |
| `CPU` | CPU バックエンド。安定性が最も高い。 | GPU が利用できない環境、macOS（Metal は GPU 推論に非対応）、デバッグ時。 |
| `GPUCompute` | GPU Compute Shader バックエンド。明示指定時は VITS 互換性の問題により `GPUPixel` にフォールバックされる（WebGPU 環境を除く）。 | WebGPU 環境でのみ使用。通常は直接指定しない。 |
| `GPUPixel` | GPU Pixel Shader バックエンド。VITS モデルとの互換性が高い。 | GPU 推論を明示的に使用したい場合。 |

### Auto 選択時のプラットフォーム別動作

| プラットフォーム | 条件 | 選択されるバックエンド |
|---|---|---|
| **Windows / Linux** | VRAM >= 512MB かつ ComputeShader 対応 | `GPUPixel` |
| **Windows / Linux** | 上記以外 | `CPU` |
| **macOS** | Metal 検出時（常に） | `CPU` |
| **iOS / Android** | ComputeShader 対応 | `GPUPixel` |
| **iOS / Android** | ComputeShader 非対応 | `CPU` |
| **WebGL** | WebGPU | `GPUCompute` |
| **WebGL** | WebGL2 | `GPUPixel` |

### Metal に関する注意

macOS の Metal バックエンドでは GPU 推論に既知の問題（シェーダーコンパイルエラー、音声データの破損）があるため、`Auto` 選択時は常に CPU にフォールバックされる。`GPUCompute` または `GPUPixel` を明示指定した場合も CPU に強制変更され、警告ログが出力される。

---

## MultiLanguageMode 列挙型

複数言語が混在するテキストの処理方針。`PiperConfig.MixedLanguageMode` で指定する。

| 値 | 説明 |
|---|---|
| `SegmentByLanguage`（デフォルト） | テキストを言語ごとに自動分割し、各セグメントを対応する G2P ハンドラで処理する。 |
| `ForceDefault` | 言語検出を無視し、すべてのテキストを `DefaultLanguage` として処理する。 |
| `AutoDetectWhole` | テキスト全体の支配的な言語を検出し、単一言語として処理する。 |
| `VoiceConfigPrimary` | 現在の `VoiceConfig` で指定された言語を主言語として使用する。 |

---

## PiperVoiceConfig（音声モデル設定）

モデルごとの設定を管理する。通常はモデルの `.onnx.json` から自動読み込みされるが、手動設定も可能。

### 基本設定

| パラメータ | 型 | デフォルト | 説明 |
|---|---|---|---|
| `VoiceId` | `string` | - | 音声の一意識別子。必須。 |
| `DisplayName` | `string` | - | 人間が読める音声名。 |
| `Language` | `string` | - | 言語コード（ISO 639-1）。必須。 |
| `ModelPath` | `string` | - | ONNX モデルファイルのパス。必須。 |
| `ConfigPath` | `string` | - | モデル設定 JSON のパス。 |
| `SampleRate` | `int` | `22050` | モデルのネイティブサンプルレート。 |

### 音声特性

| パラメータ | 型 | デフォルト | 説明 |
|---|---|---|---|
| `Gender` | `VoiceGender` | `Neutral` | 音声の性別（`Neutral` / `Male` / `Female`）。 |
| `AgeGroup` | `VoiceAge` | `Adult` | 年齢グループ（`Child` / `Teen` / `Adult` / `Senior`）。 |
| `Style` | `SpeakingStyle` | `Normal` | デフォルトの話し方スタイル。 |
| `Quality` | `ModelQuality` | `Medium` | モデル品質（`Low` ~10MB / `Medium` ~50MB / `High` ~100MB / `Ultra` ~200MB+）。 |

### メタデータ

| パラメータ | 型 | デフォルト | 説明 |
|---|---|---|---|
| `Version` | `string` | - | モデルバージョン文字列。 |
| `ModelSizeMB` | `float` | `0` | モデルファイルの概算サイズ（MB）。 |
| `SupportsStreaming` | `bool` | `true` | ストリーミング対応の有無。 |
| `NumSpeakers` | `int` | `1` | モデルがサポートする話者数。 |
| `NumLanguages` | `int` | `1` | モデルがサポートする言語数。 |

### 内部マッピング（自動設定）

| パラメータ | 型 | 説明 |
|---|---|---|
| `PhonemeIdMap` | `Dictionary<string, int[]>` | 音素からモデル ID への変換マップ。モデル JSON から自動読み込み。 |
| `LanguageIdMap` | `Dictionary<string, int>` | 言語コードから ID への変換マップ（例: `{"ja": 0, "en": 1}`）。 |
| `SpeakerIdMap` | `Dictionary<string, int>` | 話者名から ID への変換マップ。 |
| `PhonemeType` | `string` | 音素エンコーディングタイプ（`"espeak"`, `"openjtalk"`, `"multilingual"` 等）。`"multilingual"` の場合、PAD トークンが音素間に挿入される。 |

### 推論パラメータ（デフォルト値）

| パラメータ | 型 | デフォルト | 範囲 | 説明 |
|---|---|---|---|---|
| `NoiseScale` | `float` | `0.667` | 0.0 - 2.0 | 音素レベルの声質・ピッチ変動（VITS noise_scale）。 |
| `LengthScale` | `float` | `1.0` | 0.1 - 2.0 | 話速スケール（VITS length_scale）。 |
| `NoiseW` | `float` | `0.8` | 0.0 - 2.0 | 音素の発話時間の揺らぎ（VITS noise_w）。 |

---

## バリデーションの仕組み

### バリデーションフロー

`PiperConfig.ToValidated()` を呼び出すと `ValidatedPiperConfig` コンストラクタが以下を実行する。

1. **例外スロー**: 不正な値に対して `PiperException` をスロー
2. **クランプ・正規化**: 範囲外の値を自動的に有効範囲に収める
3. **警告ログ**: クランプが発動した場合や非標準値の場合にログを出力
4. **パース**: `PhonemeSilenceSpec` のパース、`DefaultLanguage` の正規化

### 例外がスローされるケース

| 条件 | エラーメッセージ |
|---|---|
| `DefaultLanguage` が null/空/空白 | `"DefaultLanguage cannot be null or empty"` |
| `SampleRate` が 8000-48000 の範囲外 | `"Invalid sample rate: {value}Hz. Must be between 8000-48000Hz"` |
| `WorkerThreads` が負値 | `"Invalid WorkerThreads: {value}. Must be >= 0"` |
| `TimeoutMs` が負値 | `"Invalid TimeoutMs: {value}. Must be >= 0"` |
| `EnablePhonemeSilence` が `true` かつ `PhonemeSilenceSpec` が不正 | `"Invalid PhonemeSilenceSpec: {details}"` |

### クランプ一覧

| パラメータ | 最小値 | 最大値 | 備考 |
|---|---|---|---|
| `MaxCacheSizeMB` | 10 | 500 | 範囲外で警告ログ |
| `MaxAudioCacheEntries` | 1 | 200 | |
| `WorkerThreads` | 1 | 16 | `0` の場合は `Max(1, processorCount - 1)` に自動設定 |
| `InferenceBatchSize` | 1 | 32 | 範囲外で警告ログ |
| `TargetRMSLevel` | -40.0 dB | 0.0 dB | `NormalizeAudio` が `true` の場合のみ |
| `GPUSettings.MaxMemoryMB` | 128 | 2048 | `GPUSettings` が null の場合は 512 |
| `WarmupIterations` | 1 | - | `EnableWarmup` が `true` の場合のみ（1 未満を 1 に補正） |

### 警告ログが出力されるケース

- `SampleRate` が 16000/22050/44100/48000 以外（非標準値）
- `DefaultLanguage` が 2 文字でも 5 文字でもない
- `TimeoutMs` が 0 より大きく 1000 未満（推奨最小値を下回る）
- 各パラメータのクランプが発動した場合

### IL2CPP 環境での自動調整

`PiperConfig.CreateDefault()` は IL2CPP ビルド時に以下を自動調整する。

- `WorkerThreads`: `IL2CPPCompatibility.PlatformSettings.GetRecommendedWorkerThreads()` の値
- `MaxCacheSizeMB`: `IL2CPPCompatibility.PlatformSettings.GetRecommendedCacheSizeMB()` の値