# uPiper パフォーマンスチューニングガイド

uPiper のパフォーマンスを最適化するための設定ガイド。`PiperConfig` の各パラメータと、ユースケース別の推奨設定をまとめる。

---

## Quick Start: 推奨設定テンプレート

```csharp
// チャットボット向け（応答性重視）
var config = new PiperConfig
{
    // キャッシュ
    EnableAudioCache = true,
    MaxAudioCacheEntries = 30,
    EnablePhonemeCache = true,
    MaxCacheSizeMB = 50,

    // バックエンド
    Backend = InferenceBackend.Auto,

    // ウォームアップ（初回レイテンシ削減）
    EnableWarmup = true,
    WarmupIterations = 2,

    // 沈黙句分割（短文は不要）
    EnablePhonemeSilence = false,

    // 音声
    NormalizeAudio = true,
    TargetRMSLevel = -20f,
};
```

---

## 1. キャッシュ設定

uPiper には2種類のキャッシュがある。

| キャッシュ | 設定フィールド | 内容 |
|-----------|--------------|------|
| 音素キャッシュ | `EnablePhonemeCache` / `MaxCacheSizeMB` | テキスト→音素変換結果をキャッシュ。G2P処理をスキップ |
| 音声合成キャッシュ | `EnableAudioCache` / `MaxAudioCacheEntries` | 音素ID+合成パラメータ→音声データ(float[])をキャッシュ。ONNX推論をスキップ |

### 1.1 EnableAudioCache

音声合成結果（float[]音声サンプル）をLRUキャッシュに保持する。同一テキスト・同一パラメータでの再合成時にONNX推論を完全にスキップできるため、効果は非常に大きい。

| 状況 | 推奨値 |
|------|--------|
| 同じテキストを繰り返し再生する（ナビゲーション、案内） | `true` |
| 毎回異なるテキストを生成する（ストリーミングチャット） | `false`（メモリ節約） |
| デフォルト | `true` |

### 1.2 MaxAudioCacheEntries

キャッシュに保持する最大エントリ数。各エントリはfloat[]音声データを保持するため、音声の長さに応じてメモリを消費する。

| ユースケース | 推奨値 | 理由 |
|------------|--------|------|
| チャットボット | 20-50 | 直近の応答を高速再生 |
| ナビゲーション・案内 | 100-200 | 定型フレーズが多く、キャッシュヒット率が高い |
| キオスク端末 | 10-20 | メニュー項目は限定的、メモリ節約 |
| ストリーミング（毎回異なるテキスト） | 1-10 | ヒット率が低いため最小限に |

有効範囲: 1～200（`ValidatedPiperConfig` でクランプ）

### 1.3 MaxCacheSizeMB（音素キャッシュ）

音素キャッシュのメモリ上限。デフォルトは100MB。

- 有効範囲: 10～500MB（`ValidatedPiperConfig` でクランプ）
- 音素キャッシュは合成キャッシュに比べてエントリあたりのメモリが小さい
- RAM に余裕がある環境では100-200MBに設定すると、長時間利用時にG2P処理を削減できる

### 1.4 キャッシュヒット率の監視

`PiperTTS` のプロパティでキャッシュの効果を確認できる。

```csharp
// 音声合成キャッシュ
long hits = piperTTS.AudioCacheHitCount;
long misses = piperTTS.AudioCacheMissCount;
int entries = piperTTS.AudioCacheEntryCount;
float hitRate = (hits + misses) > 0
    ? (float)hits / (hits + misses) * 100f
    : 0f;
Debug.Log($"Audio Cache: {hitRate:F1}% hit rate ({entries} entries)");

// 音素キャッシュ
var stats = piperTTS.GetCacheStatistics();
Debug.Log($"Phoneme Cache: {stats.HitCount} hits, {stats.MissCount} misses");
```

ヒット率が低い場合は `EnableAudioCache = false` にしてメモリを節約する方が有利。

### 1.5 ClearCache()

キャッシュのクリアが必要な場面:

- モデルの切り替え時（異なるモデルの音声データが混在するのを防ぐ）
- メモリ警告受信時（`Application.lowMemory` イベント）
- シーン切り替え時（前のシーンのキャッシュが不要になった場合）

```csharp
// メモリ警告時にキャッシュをクリア
Application.lowMemory += () => piperTTS.ClearCache();
```

---

## 2. バックエンド選択ガイド

### 2.1 プラットフォーム別推奨バックエンド

`InferenceBackend.Auto` を指定すると、`BackendSelector` がプラットフォーム条件に基づいて最適なバックエンドを自動選択する。

| プラットフォーム | Auto選択結果 | 条件 |
|-----------------|-------------|------|
| Windows / Linux | GPUPixel | VRAM 512MB以上 + ComputeShader対応 |
| Windows / Linux | CPU | VRAM不足またはComputeShader非対応 |
| macOS | CPU | Metal環境ではGPUバックエンドに既知の問題あり |
| iOS / Android | GPUPixel | ComputeShader対応時 |
| iOS / Android | CPU | ComputeShader非対応時 |
| WebGL (WebGPU) | GPUCompute | WebGPUのCompute Shaderを活用 |
| WebGL (WebGL2) | GPUPixel | VITS互換性のためGPUPixelを選択 |

### 2.2 Auto選択の動作

`BackendSelector.Determine()` は以下の優先順位で判定を行う:

1. **Metal検出**: macOS/iOSのMetal環境では、GPUバックエンドに既知のシェーダコンパイル問題があるため、明示指定でもCPUにフォールバック
2. **GPUCompute要求**: WebGPU以外ではVITSモデルとの互換性問題があるため、GPUPixelにリダイレクト
3. **Auto選択**: プラットフォーム情報（VRAM、ComputeShader対応、WebGL/モバイル判定）から最適なバックエンドを決定

### 2.3 GPU メモリ閾値

Auto選択時のGPUメモリ閾値はデフォルト512MB。VRAM 512MB以上かつComputeShader対応のデスクトップ環境でGPUPixelが選択される。

`GPUInferenceSettings.MaxMemoryMB` でGPU側のメモリ割り当て上限を制御できる（範囲: 128～2048MB、デフォルト: 512MB）。

### 2.4 手動オーバーライドが有効な場面

| 場面 | 推奨設定 |
|------|---------|
| GPUが他の処理（レンダリング等）で逼迫している | `InferenceBackend.CPU` |
| VRAM不足でも強制的にGPUを使いたい | `InferenceBackend.GPUPixel` |
| macOSでCPU性能を確認したい | `InferenceBackend.CPU`（Metal環境では自動的にCPU） |
| デバッグ時にバックエンドを固定したい | 任意のバックエンドを明示指定 |

### 2.5 AllowFallbackToCPU

`AllowFallbackToCPU = true`（デフォルト）にしておくと、GPU初期化失敗時に自動的にCPUにフォールバックする。本番環境では `true` を推奨。

---

## 3. ウォームアップ推論

### 3.1 概要

`EnableWarmup = true` にすると、モデル初期化後にダミー推論を実行する。ORT（ONNX Runtime）のJITキャッシュが安定し、初回の実際の推論レイテンシを約500-800ms削減できる。

| 設定 | デフォルト | 推奨値 |
|------|-----------|--------|
| `EnableWarmup` | `false` | 初回レイテンシが重要な場合は `true` |
| `WarmupIterations` | `2` | 1-2（JITキャッシュは1-2回で安定） |

**注意**: WebGL環境ではウォームアップは自動的に無効化される（UIフリーズ防止のため）。

---

## 4. 沈黙句分割の影響

### 4.1 EnablePhonemeSilence

長文を沈黙トークン（読点、句点等）の位置で分割し、句ごとに独立推論を行う。句間にゼロサンプルの無音区間を挿入して結合する。

```csharp
config.EnablePhonemeSilence = true;
config.PhonemeSilenceSpec = "_ 0.5";       // 読点で0.5秒の間
// config.PhonemeSilenceSpec = "_ 0.5,# 0.3"; // 読点0.5秒 + 句点0.3秒
```

### 4.2 パフォーマンスへの影響

沈黙句分割を有効にすると、**N個の句 = N回のONNX推論呼び出し** となる。

| テキスト | 推論回数（無効時） | 推論回数（有効時） |
|---------|-------------------|-------------------|
| 「こんにちは」 | 1回 | 1回 |
| 「今日は天気がいいですね、散歩に行きましょう」 | 1回 | 2回（読点で分割） |
| 3文のパラグラフ | 1回 | 3-6回 |

### 4.3 推奨

| 状況 | 推奨 |
|------|------|
| 短い発話（1文以下） | `EnablePhonemeSilence = false`（オーバーヘッド回避） |
| パラグラフ・長文 | `EnablePhonemeSilence = true`（自然な息継ぎ） |
| リアルタイム応答が必要 | `EnablePhonemeSilence = false`（レイテンシ最小化） |

---

## 5. メモリ使用量の目安

### 5.1 モデルサイズ

| モデル | サイズ | 備考 |
|--------|--------|------|
| multilingual-test-medium (fp16) | 約38MB | 6言語対応（ja/en/zh/es/fr/pt） |

モデルはメモリ上に常駐する。複数モデルを同時にロードしない限り、この値は固定。

### 5.2 推論時のメモリ割り当て

音声データは `NativeArray<float>` で管理され、サンプル数に比例する。

```
メモリ(bytes) = サンプルレート(Hz) x 音声長(秒) x sizeof(float)
             = 22050 x 秒数 x 4
```

| 音声長 | NativeArrayサイズ | 備考 |
|--------|------------------|------|
| 1秒 | 約86KB | 短い発話 |
| 5秒 | 約430KB | 通常の文 |
| 30秒 | 約2.6MB | 長いパラグラフ |

### 5.3 キャッシュメモリ

音声合成キャッシュのメモリ使用量は `AudioSynthesisCache` によって管理される。各エントリは float[] 音声データ + 64バイトのオーバーヘッドを持つ。

```
キャッシュメモリ ≒ エントリ数 x 平均音声長(秒) x 22050 x 4 (bytes)
```

例: 平均3秒の音声を50エントリキャッシュする場合

```
50 x 3 x 22050 x 4 ≒ 13.2MB
```

`MaxCacheSizeMB` はキャッシュ全体のメモリ上限として機能し、超過するとLRU方式で古いエントリが排除される。

### 5.4 辞書メモリ

カスタム辞書（`StreamingAssets/uPiper/Dictionaries/`）のメモリ使用量はエントリ数に比例する。セキュリティ上、1ファイルあたりの上限は10MBに制限されている。通常の辞書サイズ（数百～数千エントリ）ではメモリへの影響は無視できる程度。

### 5.5 メモリ合計の目安

| 構成要素 | 目安 |
|---------|------|
| ONNXモデル | 38MB |
| 推論バッファ（1発話分） | 0.1-3MB |
| 音声合成キャッシュ（50エントリ） | 5-20MB |
| 音素キャッシュ | 1-10MB |
| G2P辞書（MeCab等） | 5-15MB |
| カスタム辞書 | < 1MB |
| **合計** | **50-90MB（典型的な構成）** |

---

## 6. 合成パラメータのチューニング

### 6.1 LengthScale（話速）

`SynthesisRequest` で指定する音声の速度パラメータ。

| 値 | 効果 |
|----|------|
| 0.5 | 2倍速（高速、明瞭さ低下の可能性） |
| 0.8 | やや速い（推奨下限） |
| 1.0 | 標準速度（デフォルト） |
| 1.2 | やや遅い（聞き取りやすさ向上） |
| 1.5 | 1.5倍遅い |

- 値を小さくすると推論で生成されるサンプル数が減り、生成時間もわずかに短縮される
- 極端に小さい値（< 0.5）は品質劣化を招く

### 6.2 NoiseScale / NoiseW（安定性 vs 自然さ）

| パラメータ | デフォルト | 効果 |
|-----------|-----------|------|
| `noiseScale` | 0.667 | 音声波形のランダム性。低い値 = 安定的だが単調。高い値 = 自然だがノイズ増加 |
| `noiseW` | 0.8 | Duration Predictor のランダム性。低い値 = 発話リズムが均一。高い値 = 自然なリズム変動 |

安定した出力を求める場合（機械的な読み上げでよい場合）:
```csharp
var request = SynthesisRequest.FromPhonemes(phonemes);
// noiseScale = 0.3, noiseW = 0.3 で安定した出力
```

自然な音声を求める場合（デフォルトを推奨）:
```csharp
// デフォルト値: noiseScale = 0.667, noiseW = 0.8
```

### 6.3 短テキストの品質（自動緩和）

VITSモデルでは音素IDが40未満の短テキストで品質劣化が発生する構造的制限がある。`ShortTextMitigatingGenerator` デコレータが自動的に以下の緩和策を適用するため、ユーザー側での対応は不要。

**Strategy A: 音素IDパディング + 無音トリム**
- 音素ID列が40未満の場合、PADトークン(ID=0)をBOS後/EOS前に均等挿入して40要素に拡張
- 推論後、RMSベースの無音検出（閾値=0.01、ウィンドウ=256サンプル）で先頭/末尾の無音をトリム
- トリム後の最小サンプル数: 2205（22050Hz x 0.1秒）

**Strategy B: noise_scale / noise_w 動的低減**
- `ratio = 元の音素数 / 40`
- `noiseScale *= max(0.5, ratio)`, `noiseW *= max(0.4, ratio)`
- length_scale は調整しない

これらの処理は `ShortTextMitigatingGenerator` がINNX推論の前後に透過的に適用するため、`TTSSynthesisOrchestrator` や `SplitInferenceOrchestrator` は短テキスト処理を意識しない。

---

## 7. プラットフォーム別最適化

### 7.1 デスクトップ (Windows / Linux)

```csharp
var config = new PiperConfig
{
    Backend = InferenceBackend.Auto,           // GPUPixel自動選択
    EnableAudioCache = true,
    MaxAudioCacheEntries = 50,                 // 積極的にキャッシュ
    MaxCacheSizeMB = 200,                      // RAMに余裕がある
    EnableWarmup = true,                       // 初回レイテンシ削減
    WarmupIterations = 2,
    EnablePhonemeSilence = true,               // 長文対応
    PhonemeSilenceSpec = "_ 0.5",
    GPUSettings = new GPUInferenceSettings
    {
        MaxMemoryMB = 1024                     // VRAMに余裕があれば拡大
    }
};
```

### 7.2 macOS

macOSではMetal環境のGPUバックエンドに既知の問題（シェーダコンパイルエラー、音声データ破損）があるため、CPUバックエンドが自動選択される。

```csharp
var config = new PiperConfig
{
    Backend = InferenceBackend.Auto,           // CPU自動選択（Metal制約）
    EnableAudioCache = true,
    MaxAudioCacheEntries = 50,
    MaxCacheSizeMB = 100,
    EnableWarmup = true,
    WarmupIterations = 2,
};
```

### 7.3 モバイル (iOS / Android)

メモリ圧迫に注意し、キャッシュサイズを控えめに設定する。

```csharp
var config = new PiperConfig
{
    Backend = InferenceBackend.Auto,           // GPUPixel（CS対応時）
    EnableAudioCache = true,
    MaxAudioCacheEntries = 20,                 // メモリ節約
    MaxCacheSizeMB = 50,                       // モバイル向け控えめ
    EnableWarmup = true,
    WarmupIterations = 1,                      // 最小限
    AllowFallbackToCPU = true,                 // GPU失敗時のフォールバック必須
    EnablePhonemeSilence = false,              // レイテンシ最小化
    GPUSettings = new GPUInferenceSettings
    {
        MaxMemoryMB = 256                      // モバイルGPUメモリ節約
    }
};

// メモリ警告時にキャッシュクリア
Application.lowMemory += () => piperTTS.ClearCache();
```

### 7.4 WebGL

WebGL環境には以下の固有の制約と最適化手法がある。

**制約:**
- `Task.Run` が使用不可（シングルスレッド）
- 同期ファイルI/Oが使用不可（`WebGLStreamingAssetsLoader` で非同期読み込み）
- ウォームアップ推論は自動的に無効化（UIフリーズ防止）

**プリロード戦略:**

WebGLではファイルのダウンロードがネットワーク経由になるため、初期化時間が長くなる。`PreloadTextAsync()` を使って事前にキャッシュを温めることが有効。

```csharp
// 初期化完了後、よく使うフレーズをプリロード
await piperTTS.InitializeAsync();
await piperTTS.PreloadTextAsync("よくある質問への回答です");
await piperTTS.PreloadTextAsync("ご利用ありがとうございます");
```

**IndexedDBキャッシュ:**

WebGLビルドでは `IndexedDBCache` を使い、辞書データやG2PリソースをブラウザのIndexedDBに永続キャッシュする。2回目以降のアクセスではネットワークリクエストを省略できる。

```csharp
var config = new PiperConfig
{
    Backend = InferenceBackend.Auto,           // WebGPU→GPUCompute, WebGL2→GPUPixel
    EnableAudioCache = true,
    MaxAudioCacheEntries = 30,
    MaxCacheSizeMB = 50,                       // ブラウザメモリ制約を考慮
    EnableWarmup = false,                      // WebGLでは自動無効
    EnablePhonemeSilence = false,              // レイテンシ優先
};
```

---

## 8. 音声正規化

`AudioNormalizer` はピーク正規化を in-place で実行する。`NativeArray<float>` 版はGCアロケーションなし。

| 設定 | デフォルト | 説明 |
|------|-----------|------|
| `NormalizeAudio` | `true` | 音声正規化の有効/無効 |
| `TargetRMSLevel` | `-20f` dB | 目標RMSレベル（範囲: -40～0 dB） |

- 正規化を無効にするとわずかにCPU処理を節約できるが、音量が不均一になる可能性がある
- 通常は `true` のまま運用することを推奨

---

## 9. チューニングチェックリスト

1. `InferenceBackend.Auto` で期待通りのバックエンドが選択されているかログで確認
2. `EnableAudioCache` を有効にし、ヒット率が十分か `AudioCacheHitCount` / `AudioCacheMissCount` で監視
3. ヒット率が低い場合は `EnableAudioCache = false` でメモリを節約
4. 長文を扱う場合のみ `EnablePhonemeSilence = true` を有効化
5. 初回レイテンシが問題になる場合は `EnableWarmup = true`（WebGL除く）
6. モバイル環境では `Application.lowMemory` イベントで `ClearCache()` を呼ぶ
7. `EnableDebugLogging = true` でバックエンド選択理由やキャッシュ統計を確認
8. `GPUSettings.MaxMemoryMB` をプラットフォームのVRAMに合わせて調整