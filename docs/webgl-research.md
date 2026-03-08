# uPiper WebGL対応 調査レポート

> 調査日: 2026-03-08 (IE最新情報更新: 2026-03-09)
> 調査体制: 14専門エージェント + 4専門エージェント（IE最新情報）による並列調査
> 対象ブランチ: `feature/webgl-support` (developベース)

---

## 目次

1. [エグゼクティブサマリー](#1-エグゼクティブサマリー)
2. [過去のWebGL対応試行](#2-過去のwebgl対応試行)
3. [dot-net-g2p移行による制約解消](#3-dot-net-g2p移行による制約解消)
4. [残存する技術的課題](#4-残存する技術的課題)
5. [Unity 6000.x Web関連アップデート](#5-unity-6000x-web関連アップデート)
6. [Unity Inference Engine (Sentis) WebGL対応](#6-unity-inference-engine-sentis-webgl対応)
7. [ONNXモデル互換性](#7-onnxモデル互換性)
8. [ファイルI/O制約と辞書読み込み戦略](#8-ファイルio制約と辞書読み込み戦略)
9. [マルチスレッド制約](#9-マルチスレッド制約)
10. [メモリ制約](#10-メモリ制約)
11. [オーディオAPI制約](#11-オーディオapi制約)
12. [WebAssembly/Emscripten制約](#12-webassemblyemscripten制約)
13. [他プロジェクトの事例調査](#13-他プロジェクトの事例調査)
14. [既存コード非互換箇所一覧](#14-既存コード非互換箇所一覧)
15. [対応戦略とロードマップ](#15-対応戦略とロードマップ)
16. [WebGPU詳細調査](#16-webgpu詳細調査)

---

## 1. エグゼクティブサマリー

### 結論: WebGL対応は**実現可能**（条件付き）

| 項目 | 判定 | 理由 |
|------|------|------|
| ONNX推論 (Inference Engine) | **可能** | GPUPixelバックエンドでVITSの全オペレータがサポート済み。WebGPU有効時はGPUComputeで1.5-4倍高速化 |
| 日本語G2P (dot-net-g2p) | **対応必要** | ファイルI/O→byte[]ロードへの改修が必須 |
| 英語G2P (Flite LTS) | **対応必要** | Task.Run排除 + ファイルI/O非同期化 |
| オーディオ再生 | **可能** | 現在の実装(stream:false, SetData)はWebGL互換 |
| メモリ (デスクトップ) | **可能** | 2-4GB上限に対して~300MBで余裕あり |
| メモリ (モバイル) | **厳しい** | ~300MB上限に対して~300MBのフットプリント |

### dot-net-g2p移行の最大の効果

OpenJTalk DLLからdot-net-g2p（純C#）への移行により、過去の試行で**最大の障壁**だったP/Invoke問題が**完全に解消**された。これによりWebGL対応の実現可能性が大幅に向上した。

### 最大の残存課題

1. **MeCab辞書のファイルI/O** (103MB) → `FileStream`/`BinaryReader`をbyte[]ベースに変更必要
2. **Task.Run** (12箇所) → WebGLシングルスレッドでデッドロック
3. **StreamingAssetsアクセス** (9箇所) → `UnityWebRequest`経由に変更必要

---

## 2. 過去のWebGL対応試行

### 試行履歴

| 時期 | ブランチ | アプローチ | 結果 |
|------|---------|-----------|------|
| 2025-08 | `feature/webgl-implementation` | ONNX Runtime Web + OpenJTalk WASM (jslib経由) | 音声速度異常で未解決 |
| 2025-10 | `feature/webgl-unity-inference-engine` | Unity Inference Engine直接利用 + OpenJTalk WASM復元 | P/Invoke/コールバック問題で1日で中断 |
| 2025-08 | `deploy/webgl-pages` | GitHub Pagesデプロイ | 100MBファイル制限で困難 |

### 過去の主要問題と現在の状況

| 過去の問題 | 現在の状況 |
|-----------|-----------|
| OpenJTalk P/Invoke不可 | **解消** (dot-net-g2p純C#移行) |
| ONNX Runtime Web 4Dテンソル速度異常 | Unity Inference Engine GPUPixelで回避可能 |
| GitHub Pages 100MB制限 | CDN/Addressables戦略で対応可能 |
| OpenJTalk WASM化の複雑さ | **不要** (dot-net-g2p純C#) |
| JSLib P/Invokeブリッジの複雑さ | **不要** (Unity IE直接利用で3層構造→1層に) |
| IL2CPPデリゲートマーシャリング問題 | **不要** (JavaScript層不要) |
| Float32Arrayマーシャリング精度問題 | **不要** (Unity IE内部で完結) |

### 過去の試行から得られた技術的教訓

1. **ONNX Runtime Web方式の失敗要因**: Unity C# + JavaScript + WASMの3層構造がマーシャリング問題の温床。特に`Float32Array`の精度問題が音声速度異常の根本原因
2. **Sentis初期試行の問題**: VITSモデルの演算精度に懸念があったが、IE v2.2.2で改善されている可能性あり
3. **dot-net-g2p移行の最大効果**: ネイティブプラグイン完全排除により、過去2回の試行で最大の障壁だったP/Invoke/WASM/JSLib関連の問題が**全て解消**

### 過去ブランチの参考資産

| 資産 | ブランチ | 活用ポイント |
|------|---------|-------------|
| `docs/WEBGL_TROUBLESHOOTING_GUIDE.md` | feature/webgl-implementation | 既知問題の回避方法 |
| `WebGLSplitDataProcessor.cs` | feature/webgl-implementation | 大容量ファイル分割パターン |
| `deploy-webgl.yml` | feature/webgl-implementation | GitHub Pagesデプロイワークフロー |
| PUA文字マッピング修正記録 | feature/webgl-unity-inference-engine | 音素マッピングの正しい対応表 |

### 現在のdevelopブランチのWebGLガード

| ファイル | 内容 |
|---------|------|
| `PiperTTS.cs:1060` | `#if !UNITY_WEBGL` でDotNetG2PPhonemizer初期化スキップ |
| `TextPhonemizerAdapter.cs:1` | ファイル全体を `#if !UNITY_WEBGL` で除外 |
| `PlatformDefines.cs` | `SUPPORTS_NATIVE_PLUGINS=false`, `IS_WEBGL_PLATFORM=true` |
| `InferenceAudioGenerator.cs:464` | WebGL時GPUPixel自動選択 |
| `InferenceEngineDemo.cs` (複数) | WebGLでDotNetG2P/Flite初期化をスキップ |

---

## 3. dot-net-g2p移行による制約解消

### 解消された制約

| 制約 | 説明 |
|------|------|
| **P/Invoke完全不要化** | dot-net-g2pには`DllImport`/`extern`が一切ない（0件）。WebGLでDLLロード不可の問題が完全解消 |
| **プラットフォーム固有バイナリ不要化** | 全プラットフォームで同一C#コードが動作 |
| **WASM化の複雑さ解消** | OpenJTalkをEmscriptenでWASMコンパイルする必要がなくなった |

### 残存する制約

| 制約 | 影響度 | 説明 |
|------|--------|------|
| **MeCab辞書ファイルI/O** | **高** | `FileStream`+`BinaryReader`で4ファイル読み込み（計103MB） |
| **StreamingAssetsパス** | **高** | `Application.streamingAssetsPath`がWebGLではHTTP URL |
| **Task.Run** | **高** | `DotNetG2PPhonemizer.cs:109`で使用 |
| **埋め込みリソース** | **中** | 英語G2Pが`GetManifestResourceStream`使用（IL2CPPで不安定） |

---

## 4. 残存する技術的課題

### 4.1 ファイルI/O非互換箇所（高: 修正必須）

#### dot-net-g2p内部（MeCab辞書）

| ファイル | API | サイズ | 用途 |
|---------|-----|--------|------|
| `SystemDictionary.cs` | `FileStream`, `BinaryReader` | 99MB | sys.dic読み込み |
| `ConnectionMatrix.cs` | `FileStream`, `BinaryReader` | 3.7MB | matrix.bin読み込み |
| `CharProperty.cs` | `FileStream`, `BinaryReader` | 257KB | char.bin読み込み |
| `UnknownDictionary.cs` | `FileStream`, `BinaryReader` | 5.6KB | unk.dic読み込み |
| `DictionaryBundle.cs` | `Directory.Exists` | - | 辞書ディレクトリ検証 |
| `MeCabTokenizer.cs` | `Directory.Exists` | - | 辞書パス検証 |

#### uPiper Runtime

| ファイル | API | 用途 |
|---------|-----|------|
| `CustomDictionary.cs` (4箇所) | `Directory.Exists/GetFiles`, `File.Exists/ReadAllText` | カスタム辞書JSON読み込み |
| `DotNetG2PPhonemizer.cs` (5箇所) | `Directory.Exists` | MeCab辞書パス解決 |
| `CMUDictionary.cs` (6箇所) | `File.Exists`, `StreamReader` | CMU英語辞書読み込み |
| `FliteLexicon.cs` (2箇所) | `File.Exists`, `File.ReadAllLines` | Flite辞書読み込み |
| `FliteLTSPhonemizer.cs` (2箇所) | `File.Exists`, `File.ReadAllLines` | LTSルール読み込み |

### 4.2 Task.Run非互換箇所（高: デッドロック）

| ファイル | 行 | 影響 |
|---------|-----|------|
| `DotNetG2PPhonemizer.cs` | 109 | 日本語音素化の核心 |
| `FlitePhonemizerBackend.cs` | 52 | 英語音素化 |
| `SimpleLTSPhonemizer.cs` | 57 | 英語LTS |
| `CMUDictionary.cs` | 109 | 辞書読み込み |
| `G2PEngine.cs` | 42 | G2P予測 |
| `StatisticalG2PModel.cs` | 39 | モデル予測 |
| `FliteLTSPhonemizer.cs` | 358 | カスタム辞書読み込み |
| `MultilingualPhonemizerService.cs` | 37,55,73 | 初期化(3箇所) |
| `PhonemizerService.cs` | 120 | 初期化 |
| `UnityPhonemizerService.cs` | 63 | コルーチンブリッジ |
| `TextPhonemizerAdapter.cs` | 34 | **既に`#if !UNITY_WEBGL`で除外済み** |

### 4.3 StreamingAssetsアクセス（高: 要変更）

WebGLでは`Application.streamingAssetsPath`がHTTP URLを返すため、`File.Read`系は全て不可。
`UnityWebRequest`経由でのアクセスが必要。

| ファイル | 用途 |
|---------|------|
| `uPiperPaths.cs:56,65` | MeCab辞書パス、CMU辞書パス |
| `CustomDictionary.cs:38` | カスタム辞書パス |
| `CMUDictionary.cs:67` | CMU辞書パス |
| `FliteLTSPhonemizer.cs:283` | LTSルールパス |
| `DotNetG2PPhonemizer.cs:466` | MeCab辞書パス |

### 4.4 リフレクション（中: IL2CPP/AOT制約）

| ファイル | API | リスク |
|---------|-----|--------|
| `UnifiedPhonemizer.cs:480,488` | `Type.GetType`, `Activator.CreateInstance` | ストリッピングで消える可能性 |
| `MixedLanguagePhonemizer.cs:331,334` | `Type.GetType`, `Activator.CreateInstance` | 同上 |
| `EnhancedEnglishPhonemizer.cs:699` | `GetField(BindingFlags.NonPublic)` | privateフィールドアクセス、IL2CPPで問題の可能性 |
| `CmuDictionary.cs`, `LtsData.cs` | `GetManifestResourceStream` | `useEmbeddedResources=true`が必要 |

---

## 5. Unity 6000.x Web関連アップデート

### バージョン間の進化

| 機能 | 6000.0 (Unity 6) | 6000.1 (6.1) | 6000.2 (6.2) | 6000.3 LTS (6.3) |
|------|-------------------|--------------|--------------|-------------------|
| WebAssembly 2023 | 追加 | - | デフォルト有効 | デフォルト有効 |
| WebGPU | 内部のみ | パブリック(実験的) | 拡張追加 | バグ修正多数 |
| ビルドプロファイル | なし | - | 追加 | 継続 |
| Emscripten | 3.1.38 | - | - | Apple Silicon対応 |
| メモリ上限 | 4GB | - | - | 継続 |
| IL2CPPメタデータ | - | - | - | サイズ最適化 |
| Profiler Web接続 | - | - | - | IP接続対応 |

### WebAssembly 2023 最適化機能

| 機能 | uPiperへの効果 |
|------|---------------|
| **SIMD** | VITS推論の数学演算（Sentis CPU backend）で高速化の可能性 |
| **Native Exception Handling** | .wasmサイズ削減 + ランタイムオーバーヘッド削減 |
| **BigInt** | 64bit整数マーシャリング効率化 |
| **memcpy/memset最適化** | 辞書データ・テンソルの大容量データ転送改善 |

### C#マルチスレッド: 全バージョンで非対応

WebAssemblyのマルチスレッドGC未対応により、C#マネージドスレッドは**全Unity 6000.xで使用不可**。
Unity公式は`Awaitable`の使用を推奨。

---

## 6. Unity Sentis (旧Inference Engine) WebGL対応

> **命名の変遷**: Sentis → Inference Engine (Unity 6.2) → **Sentis** (2.4.0〜)。パッケージIDは `com.unity.ai.inference` のまま。

### バージョン履歴（2.2.2 → 2.5.0）

| バージョン | リリース日 | 主な変更 | WebGPU対応 |
|-----------|-----------|---------|-----------|
| **2.2.2** (uPiper現在) | - | 動的入力形状、Mish追加 | **ビルド失敗** (GroupConvエラー) |
| **2.3.0** | 2025-07-15 | ConvTranspose group/dilations追加、Model Visualizer | **ビルド成功** (GroupConv修正) |
| **2.4.0** | 2025-10-22 | **Sentisにリネーム**、LiteRT対応、推論精度修正、メモリリーク修正 | ビルド成功 |
| **2.4.1** | 2025-10-31 | ドキュメント修正 | ビルド成功 |
| **2.5.0** (最新) | 2026-01-23 | PyTorchインポート、3D Pool、TopK GPUCompute修正、メモリリーク修正 | ビルド成功 |

### バックエンド互換性

| BackendType | WebGL 2.0 | WebGPU (実験的) | 備考 |
|------------|-----------|-----------------|------|
| **CPU** | 動作する（低速） | 動作する（低速） | Burst→Wasm変換 |
| **GPUCompute** | **不可** | **利用可能** (IE 2.3.0+) | WebGL2はCompute Shader非対応。WebGPUでは対応 |
| **GPUPixel** | **動作する** | 動作する | uPiperのWebGLデフォルト |

### VITSモデルとの互換性: 良好

VITSが使用する主要オペレータ（Conv, ConvTranspose, MatMul, Softmax, 活性化関数等）は**全てGPUPixel/GPUComputeでサポート**。GPUPixelで非対応のオペレータ（LSTM等）はVITSでは使用されない。GroupNormalizationは全バックエンドで未サポートだが、VITSはInstanceNormalizationを使用するため問題なし。

### 2.5.0での主な改善点（uPiper関連）

| 改善 | 影響 |
|------|------|
| ConvTranspose group/dilations (2.3.0) | VITSモデルの推論品質向上の可能性 |
| 推論精度修正: 最適化パス・CPUコールバック (2.4.0) | 推論結果の正確性向上 |
| メモリリーク修正 (2.4.0, 2.5.0) | 長時間使用時の安定性 |
| TopK GPUCompute修正 (2.5.0) | GPUComputeバックエンド安定性 |
| Clipオペレータ CPUフォールバック不要化 (2.5.0) | GPU上で完結、パフォーマンス改善 |

### GPUComputeクラッシュ問題の現状

大規模モデル(600MB級)でGPUComputeバックエンドのSchedule中にクラッシュする問題がIE 2.1-2.3で報告されている。2.5.0で明示的な修正記載はないが、TopK修正やメモリリーク修正で一部改善の可能性あり。VITSモデル(~60MB)での発生は要検証。

### 推奨

| 時期 | アプローチ |
|------|-----------|
| 短期 | **Sentis 2.5.0にアップグレード** + WebGL2 + GPUPixel で基本動作検証 |
| 中期 | WebGPU有効化 + GPUCompute検証（1.5-4倍高速化見込み） |
| 長期 | WebGPU安定化後にGPUComputeをデフォルトに昇格 |

> **詳細は[16. WebGPU詳細調査](#16-webgpu詳細調査)を参照**

---

## 7. ONNXモデル互換性

### VITSアーキテクチャの主要コンポーネント

| コンポーネント | 主要レイヤー | GPUPixel対応 |
|---|---|---|
| TextEncoder | Embedding, Transformer, Conv1d | 全て対応 |
| DurationPredictor | Conv1d, DDSConv | 全て対応 |
| Flow (ResidualCoupling) | WaveNet残差ブロック (dilated Conv1d) | 全て対応 |
| Decoder (HiFi-GAN) | **ConvTranspose1d**, Conv1d, ResBlock | 全て対応 |

### モデル仕様

全モデル共通: IR version 8, ONNX Opset 15

| モデル | 入力 | 出力 | オペレータ数 |
|--------|------|------|-------------|
| ja_JP-test-medium | input(int), input_lengths(int), scales(float) | output(4D) | 51 |
| en_US-ljspeech-medium | 同上 | output(4D) | 51 |
| tsukuyomi-chan | 上記 + **prosody_features(int)** | output(4D) + **durations** | 52 |

### 使用オペレータ（全51-52種類）

```
Add, And, Cast, Ceil, Clip, Concat, Constant, ConstantOfShape, Conv, ConvTranspose,
CumSum, Div, Equal, Erf, Exp, Expand, Gather, GatherElements, GatherND, GreaterOrEqual,
LeakyRelu, Less, LessOrEqual, MatMul, Mul, Neg, NonZero, Not, Pad, Pow,
RandomNormalLike, Range, ReduceMax, ReduceMean, ReduceSum, Relu, Reshape, ScatterND,
Shape, Sigmoid, Slice, Softmax, Softplus, Split, Sqrt, Squeeze, Sub, Tanh,
Transpose, Unsqueeze, Where
(+ tsukuyomi-chanのみ: If)
```

**全オペレータがSentis (旧Inference Engine) v2.5.0で実装確認済み。**（ONNX opset 7-15サポート、VITSはopset 15）

### WebGL固有の潜在的問題

| 問題 | 説明 | リスク |
|------|------|--------|
| **RandomNormalLike CPUフォールバック** | GPUPixelでは実行不可→CPUフォールバック（`CPUFallbackCalculator.cs:115`）。CPU/GPU間切替のパフォーマンス影響 | 中 |
| **4Dテンソル出力** | 出力が`[batch, time, 1, dim]`の4Dテンソル。過去のONNX Runtime Web使用時に音声速度異常の原因に。IE の`ReadbackAndClone()`で正しく処理される可能性あるが要検証 | 中 |
| **WASM最適化MatMul** | Sentis v2.5.0にWebGL用WASM最適化MatMulカーネルが実装済み（`UNITY_WEBGL`条件付き） | 好材料 |

### モデルサイズと最適化

| モデル | 現在のサイズ | FP16量子化後 | Brotli圧縮後(推定) |
|--------|------------|-------------|-------------------|
| ja_JP-test-medium | ~60.5MB | ~30MB | ~10-15MB |
| en_US-ljspeech-medium | ~60.6MB | ~30MB | ~10-15MB |
| tsukuyomi-chan | ~60.8MB | ~30MB | ~10-15MB |

**推奨**: FP16量子化 + Brotli圧縮で転送サイズを約60MB→10-15MBに削減可能。

---

## 8. ファイルI/O制約と辞書読み込み戦略

### StreamingAssetsのファイルサイズ

| ファイル | サイズ | WebGLでの読み込み |
|---------|--------|------------------|
| sys.dic (MeCab) | **99MB** | UnityWebRequest必須 |
| matrix.bin (MeCab) | 3.7MB | UnityWebRequest必須 |
| char.bin (MeCab) | 257KB | UnityWebRequest必須 |
| unk.dic (MeCab) | 5.6KB | UnityWebRequest必須 |
| naist_jdic.zip | 23MB | 圧縮版（代替可） |
| cmudict-0.7b.txt (英語) | 3.5MB | UnityWebRequest必須 |
| Dictionaries/*.json | ~48KB | UnityWebRequest必須 |
| **合計 (非圧縮)** | **~103MB** | |

### 致命的問題: 起動時全ダウンロード

WebGLビルドではStreamingAssetsの全内容が`.data`ファイルに含まれ、**起動前に全てダウンロードされる**。103MBの辞書があると起動前に103MB+のダウンロードが必要。

### GitHub Pages配信制約

| 制約 | 値 |
|------|-----|
| 個別ファイルサイズ上限 | **100MB** (Gitの制限) |
| 公開サイト合計サイズ | **1GB** |
| 月間帯域 | 100GB (ソフトリミット) |
| Brotli事前圧縮 | **非対応** (自動gzipのみ) |
| Git LFS | **非対応** (ポインタファイルが配信される) |
| GitHub Releases CORS | **非対応** (302リダイレクト+S3がCORSヘッダーなし) |
| Content-Typeヘッダー制御 | 不可（拡張子ベースの自動設定のみ） |

### 対応戦略（GitHub Pages前提・推奨順）

#### 戦略A: ZIP圧縮辞書 + 過去の分割配信機能の再利用（推奨）

**根拠**: 各リソースのサイズが100MB以下に収まるため、過去ブランチの分割機能を活用すれば**外部サーバー不要**で実現可能。

| リソース | サイズ | 100MB制限 | 配信方法 |
|---------|--------|----------|---------|
| naist_jdic.zip (MeCab辞書ZIP) | **23MB** | OK | StreamingAssets → UnityWebRequestでDL → C#で展開 |
| ONNXモデル | **~60MB** | OK | Resources/.data に含まれる |
| CMU辞書 | 3.5MB | OK | StreamingAssets |
| カスタム辞書JSON | ~48KB | OK | StreamingAssets |
| Unity .data (全体) | **100MB超の可能性** | 要分割 | 過去実装の分割ローダーを再利用 |

**ポイント**:
- `naist_jdic.zip` (23MB) を使用すれば非圧縮99MBの辞書を個別ファイルとして持つ必要がない
- `System.IO.Compression.ZipArchive` はWebGLでも動作する
- .dataファイルが100MBを超える場合は、過去の `WebGLSplitDataProcessor.cs` + `split-file-loader.js` を再利用
- GitHub Pagesの自動gzip圧縮により、さらに転送サイズが削減される

**ランタイムフロー**:
```
1. Unity起動 → .data自動ロード（分割ローダーが透過的に結合）
2. UnityWebRequest → naist_jdic.zip (23MB) ダウンロード
3. ZipArchive → sys.dic, matrix.bin, char.bin, unk.dic をメモリ展開
4. byte[] → dot-net-g2p DictionaryBundle.Load(byte[]...)
5. 日本語G2P利用可能
```

**必要な実装**:
- dot-net-g2pに`byte[]`ベース初期化APIの追加
- `feature/webgl-implementation`ブランチからの分割配信機能移植
- ZIP展開→byte[]渡しのローダークラス作成

#### 戦略B: Addressablesによるオンデマンドロード

辞書データをAssetBundleとしてパッケージし、Addressablesで管理。GitHub Pages上に配置。

- 起動時に全ダウンロード不要（言語選択後にDL）
- IndexedDB自動キャッシュ
- Addressablesパッケージ依存の追加が必要
- GitHub Pages上にAddressablesのリモートロードパスを設定可能（`window.location.href`ベース）

#### 戦略C: Emscripten MEMFS事前書き込み

UnityWebRequestでダウンロード → MEMFS上に書き込み → 既存FileStream APIで読み込み。

- dot-net-g2p変更不要
- メモリ消費が倍増（MEMFS上 + パース後データ）

### 過去ブランチの分割配信実装（再利用可能）

`feature/webgl-implementation`ブランチに完全な実装が存在:

| コンポーネント | 役割 |
|--------------|------|
| `WebGLSplitDataProcessor.cs` | PostProcessBuildで100MB超ファイルを90MBチャンクに自動分割 |
| `split-file-loader.js` | Fetch APIをインターセプトし、分割ファイルを並列DL→透過的に結合 |
| `github-pages-adapter.js` | リポジトリ名自動検出、パス解決 |
| `deploy-webgl.yml` | gzip解凍、分割ファイル配置、index.html修正、`.nojekyll`配置 |
| `split-large-files.py` | 汎用分割ツール（process/split/combine） |

**分割ロードシーケンス**:
```
index.html → split-file-loader.js（fetch/XHRパッチ適用）
  → Unity初期化 → .dataリクエスト
  → ローダーがインターセプト → .partaa + .partab を並列DL
  → バイナリ結合 → Responseとして返却
  → Unityは通常ファイルとして認識
```

### GitHub Pagesデプロイ時の注意点

1. **`.nojekyll`ファイル必須**: `_`で始まるファイルをJekyllに無視されないように
2. **gzip/brotli解凍**: GitHub Pagesは事前圧縮ファイルを正しく配信できないため、CIでビルド後に解凍→GitHub Pagesの自動gzipに任せる
3. **Git LFS不使用**: GitHub PagesはLFSポインタファイルを配信してしまうため、実ファイルをgh-pagesブランチにコミット
4. **index.html修正**: `.data.gz`→`.data`への参照書き換え、split-loader.js挿入

### dot-net-g2pに必要な改修

MeCab辞書ローダーに`byte[]`/`Stream`ベースの初期化メソッド追加:

```csharp
// 現在: ファイルパスベース
DictionaryBundle.Load(string dictionaryPath)

// 追加: byte[]ベース（WebGL対応）
DictionaryBundle.Load(byte[] sysDic, byte[] matrix, byte[] charBin, byte[] unkDic)
```

### 段階的ロード戦略

```
Phase 1: カスタム辞書JSON (48KB)  → 即座にロード
Phase 2: CMU辞書 (3.5MB)          → 英語サポート有効化
Phase 3: MeCab辞書 (103MB)        → 日本語サポート有効化
UI: プログレスバーで各フェーズの進捗表示
```

---

## 9. マルチスレッド制約

### 根本的制約

WebGLではC#マネージドスレッドが**完全に非サポート**（WebAssemblyのマルチスレッドGC未対応）。

**使用不可**: `Thread`, `ThreadPool`, `Task.Run()`, `Task.Delay()`, `SemaphoreSlim`
**使用可能**: `async/await`, `Task.CompletedTask`, `Task.FromResult`, `Awaitable` (Unity 6推奨), Coroutine

### 対応方法

| 方法 | 説明 | 推奨度 |
|------|------|--------|
| **直接実行** | `Task.Run(() => Func())` → `Func()` に置換 | 推奨（最もシンプル） |
| **Awaitable** | Unity 6推奨のasync/awaitパターン | 推奨 |
| **UniTask** | サードパーティ。PlayerLoopベース。WebGL完全対応 | 代替 |
| **コルーチン+フレーム分割** | CPUヘビーな処理を分散 | フレーム落ち防止に |
| **WebGLThreadingPatcher** | ILパッチでTask.Runをメインスレッド実行に | 緊急対応用 |

### 条件コンパイルパターン

```csharp
#if UNITY_WEBGL && !UNITY_EDITOR
    // WebGL: メインスレッドで直接実行
    return PhonemizeInternal(text, language);
#else
    // その他: バックグラウンドスレッドで実行
    return await Task.Run(() => PhonemizeInternal(text, language));
#endif
```

---

## 10. メモリ制約

### ブラウザメモリ上限

| プラットフォーム | 理論上限 | 実用上限 |
|---------------|---------|---------|
| Chrome/Edge (デスクトップ) | 4GB | ~2-4GB |
| Firefox (デスクトップ) | 4GB | ~2-4GB |
| Safari (デスクトップ) | 4GB | ~2GB |
| Chrome (Android) | 4GB | **~300MB** |
| Safari (iOS) | 4GB | **~256-300MB** |

### uPiperメモリフットプリント見積もり

| コンポーネント | サイズ |
|--------------|--------|
| ONNXモデル (medium) | ~63-75MB |
| MeCab辞書 | ~103MB |
| Unityランタイム + Wasmコード | ~30-50MB |
| 推論時テンソルバッファ | ~20-50MB |
| AudioClipバッファ | ~1-5MB/秒 |
| GCヒープ + マネージドオブジェクト | ~10-20MB |
| **合計（ピーク時）** | **~230-300MB** |

### 判定

| プラットフォーム | 判定 | 理由 |
|---------------|------|------|
| デスクトップブラウザ | **実現可能** | 余裕あり |
| モバイル（最適化なし） | **困難** | 300MB上限に対して~300MB |
| モバイル（最適化後） | **条件付き可能** | 量子化+辞書圧縮で~150-200MBに削減 |

### 最適化ターゲット

1. **MeCab辞書 (103MB)**: 分割ロード・圧縮・IndexedDBキャッシュ
2. **ONNXモデル量子化**: FP16で30-50%削減
3. **段階的ロード**: 全リソース一括ロード回避

---

## 11. オーディオAPI制約

### 現在のuPiper実装のWebGL互換性

| 項目 | 現在の実装 | WebGL互換性 |
|------|-----------|------------|
| `AudioClip.Create(stream: false)` | 使用中 | **互換** |
| `AudioClip.SetData(audioData, 0)` | 使用中 | **互換**（全データ一括設定のため） |
| サンプルレート 22050Hz | 使用中 | **互換**（ブラウザが自動リサンプリング） |
| モノラル | 使用中 | **互換** |

### WebGL固有の制約

| 制約 | 説明 | uPiperへの影響 |
|------|------|---------------|
| `stream: true` | WebGLで動作しない | uPiperは`stream: false`なので**問題なし** |
| 自動再生ポリシー | ユーザーインタラクション後にのみ音声再生可能 | UI設計で対応必要 |
| Audio Mixer | ボリューム変更のみ対応 | 高度なエフェクトは不可 |
| SetData offset | offsetは無視される | uPiperはoffset=0なので**問題なし** |
| ピッチ | 正の値のみ | 影響小 |

### 必要な対応

- **ユーザーインタラクションゲート**: 最初のTTS再生前にボタンクリック等を要求するUI設計
- AudioClip再利用時は毎回新規作成が安全

---

## 12. WebAssembly/Emscripten制約

### IL2CPP → WebAssembly変換の制約

| 制約 | dot-net-g2pへの影響 |
|------|-------------------|
| `System.Reflection.Emit` 不可 | dot-net-g2pは未使用 → **問題なし** |
| `dynamic` 不可 | 未使用 → **問題なし** |
| ジェネリクスAOT制約 | 特殊なジェネリクス未使用 → **問題なし** |
| ファイルI/O不可 | **最大の課題**（前述） |
| `System.Net` 不可 | 未使用 → **問題なし** |

### 必要な設定

- `link.xml`にdot-net-g2pアセンブリ追加（ストリッピング防止）
- `PlayerSettings.WebGL.useEmbeddedResources = true`（英語G2Pの埋め込みリソース用）
- WebGLヒープサイズ拡大（現在512MB → 1024MB推奨）

---

## 13. 他プロジェクトの事例調査

### Unity WebGL TTSの既存アプローチ

| 方式 | 代表例 | 音質 | 実装難度 |
|------|--------|------|---------|
| Web Speech API | WebGL Speech Synthesis (Asset Store) | ブラウザ依存 | 低 |
| フォルマント合成 | Klattersynth TTS | 低 | 低 |
| **ONNX Runtime Web** | piper-tts-web (GitHub) | **高** | 高 |
| **Unity Inference Engine** | uPiper (本プロジェクト) | **高** | 中 |

### 最重要発見: piper-plus openjtalk-web

**piper-plusリポジトリ内に既にWebAssembly版OpenJTalk日本語音素化が実装済み。**

- URL: https://github.com/ayutaz/piper-plus (src/wasm/openjtalk-web/)
- WASM < 400KB + JS < 40KB で軽量
- ブラウザ内で完全に日本語TTSが動作するデモあり
- uPiperと同一オーナーによる実装

### piper-tts-webの成功パターン

音素化とONNX推論を分離する2ステップアプローチ:
1. phonemize (WASM) で音素化
2. ONNX Runtime Web で推論

→ フルWASMコンパイルより **4-8倍高速**

### uPiperでの推奨アプローチ

dot-net-g2p（純C#）移行済みのため、piper-plus openjtalk-webのWASMアプローチは不要。
**Unity Inference Engine + dot-net-g2p（byte[]ロード対応）が最もクリーンなアプローチ。**

---

## 14. 既存コード非互換箇所一覧

### 非互換度: 高（修正必須）- 45+箇所

| カテゴリ | 箇所数 | 主要ファイル |
|---------|--------|-------------|
| ファイルI/O (Runtime) | 20+ | CustomDictionary, DotNetG2PPhonemizer, CMUDictionary, FliteLexicon |
| ファイルI/O (dot-net-g2p) | 15+ | SystemDictionary, ConnectionMatrix, CharProperty, UnknownDictionary |
| Task.Run | 12 | DotNetG2PPhonemizer, FlitePhonemizerBackend, G2PEngine, CMUDictionary |
| StreamingAssets直接アクセス | 9 | uPiperPaths, CustomDictionary, CMUDictionary, FliteLTSPhonemizer |

### 非互換度: 中（対応推奨）- 15箇所

| カテゴリ | 箇所数 | 主要ファイル |
|---------|--------|-------------|
| SemaphoreSlim | 6 | ThreadSafePhonemizerPool, PhonemizerDataManager, PhonemizerService |
| リフレクション (Activator) | 5 | UnifiedPhonemizer, MixedLanguagePhonemizer |
| privateフィールドリフレクション | 1 | EnhancedEnglishPhonemizer |

### 非互換度: 低（影響小）- 50+箇所

| カテゴリ | 箇所数 | 影響 |
|---------|--------|------|
| lock文 | 40+ | シングルスレッドでは無害 |
| typeof() | 数箇所 | IL2CPP保持用、影響なし |
| iOS P/Invoke | 6 | プラットフォーム条件分岐で保護済み |

---

## 15. 対応戦略とロードマップ

### 推奨アプローチ: Unity Inference Engine + dot-net-g2p (byte[]ロード対応)

過去の試行（ONNX Runtime Web, OpenJTalk WASM）とは異なり、dot-net-g2p移行により**Unity内で完結するクリーンなアーキテクチャ**が可能になった。

```
[WebGLアーキテクチャ]

起動時:
  UnityWebRequest → MeCab辞書(byte[]) → DotNetG2PPhonemizer初期化
  UnityWebRequest → カスタム辞書(JSON) → CustomDictionary初期化
  ModelLoader → ONNXモデル → InferenceAudioGenerator初期化

テキスト入力時:
  テキスト → DotNetG2PPhonemizer(メインスレッド直接実行)
    → 音素 + Prosody
    → InferenceAudioGenerator(GPUPixel)
    → AudioClip(22050Hz)
    → AudioSource.Play()
```

### Phase 1: 基盤整備（最小動作確認）

| タスク | 対象 | 工数目安 |
|--------|------|---------|
| dot-net-g2pにbyte[]ロードAPI追加 | dot-net-g2p | 中 |
| Task.Run → WebGL条件分岐 | uPiper全体 | 小 |
| StreamingAssets非同期ローダー作成 | uPiper | 中 |
| link.xmlにdot-net-g2pアセンブリ追加 | uPiper | 小 |
| WebGLヒープサイズ拡大(1024MB) | PlayerSettings | 小 |
| Sentis 2.5.0へのアップグレード | パッケージ更新 | 中 |
| GPUPixel推論動作確認 | InferenceAudioGenerator | 小 |

### Phase 2: 日本語TTS WebGL動作

| タスク | 対象 | 工数目安 |
|--------|------|---------|
| DotNetG2PPhonemizerのWebGL初期化パス | uPiper | 中 |
| CustomDictionaryのWebGL非同期ロード | uPiper | 小 |
| PiperTTSのWebGLガード解除・統合 | uPiper | 中 |
| ユーザーインタラクションゲートUI | uPiper Demo | 小 |
| 起動時プログレスバーUI | uPiper Demo | 小 |

### Phase 3: 最適化・配信

| タスク | 対象 | 工数目安 |
|--------|------|---------|
| ONNXモデルFP16量子化 | piper-plus | 中 |
| MeCab辞書Brotli圧縮配信 | インフラ | 小 |
| IndexedDBキャッシュ（2回目以降高速化） | uPiper | 中 |
| Addressablesシステム統合 | uPiper | 大 |
| モバイルブラウザ最適化 | uPiper | 大 |

### Phase 4: WebGPU対応

| タスク | 前提条件 |
|--------|---------|
| Sentis 2.5.0へのアップグレード | Unity 6.1+ |
| WebGPU有効化（WebGL2フォールバック付き） | Player Settings設定 |
| GPUComputeバックエンド動作検証 | Sentis 2.5.0 |
| `DetermineBackendType()`にWebGPU判定追加 | コード変更 |
| パフォーマンスベンチマーク（GPUPixel vs GPUCompute） | Phase 2完了 |

→ **詳細は[16. WebGPU詳細調査](#16-webgpu詳細調査)を参照**

---

## 16. WebGPU詳細調査

> 調査日: 2026-03-09
> 調査体制: 9専門エージェントによる並列調査

### 16.1 WebGPUの概要と意義

WebGPUは、WebGL2の後継となる次世代Web向けグラフィックスAPI。Vulkan/Metal/Direct3D 12相当のローレベルGPUアクセスをブラウザから提供する。**2025年11月時点で全主要ブラウザがデフォルト出荷**しており、実用段階に入っている。

uPiperにとっての最大の意義は、WebGL2では不可能だった**Compute Shader**が利用可能になり、Unity Inference EngineのGPUComputeバックエンドで推論を実行できるようになる点にある。

### 16.2 WebGPU vs WebGL2 技術比較

#### 主要機能比較

| 項目 | WebGL2 | WebGPU | uPiperへの影響 |
|------|--------|--------|---------------|
| Compute Shader | **非対応** | ネイティブ対応 | GPUComputeバックエンド利用可能 |
| Storage Buffer | 非対応 | 最大128MB | テンソルデータ直接アクセス |
| Uniform Buffer | 16KB | 64KB (4倍) | モデル重みの効率的転送 |
| Float16 | 非対応 (mediump≠IEEE半精度) | shader-f16拡張対応 | 量子化推論でメモリ50%削減 |
| GPU同期モデル | 同期的 (getError()ブロッキング) | 完全非同期 (3タイムライン設計) | UIスレッド非ブロッキング |
| データ読み戻し | readPixels (同期、ブロッキング) | mapAsync (非同期) | 推論結果の効率的取得 |
| パイプライン | Render Pipelineのみ | Render + Compute Pipeline | 描画と推論の並行実行 |
| シェーダー言語 | GLSL ES 3.0 | WGSL (Rust風、厳密型) | クロスプラットフォーム一貫性 |
| エラーハンドリング | 同期的 getError() | 非同期エラーモデル | パフォーマンス影響最小 |
| Worker対応 | OffscreenCanvas経由 (限定的) | 複数Workerからデバイス共有可能 | 並列コマンド記録 |

#### TTS推論への影響度評価

| 制約カテゴリ | TTS推論影響度 | 主な改善点 |
|-------------|-------------|-----------|
| バッファサイズ制限 | **高** | 128MB Storage Buffer (WebGL2では非対応) |
| シェーダー制限 | **高** | Compute Shader、ランタイムサイズ配列 |
| 浮動小数点精度 | **高** | FP16サポート (ALU: 25%向上, メモリ: 50%向上) |
| 非同期処理 | **高** | 完全非同期モデル、UIスレッド非ブロッキング |
| ストレージバッファ | **高** | テクスチャ間接アクセスの排除 |
| テクスチャサイズ | **中** | Storage Buffer使用で重要性低下 |
| バインディング数 | **中** | バインドグループ構造化 (640バインディング/グループ) |
| マルチスレッド | **中** | Worker間GPU共有 |
| 間接ディスパッチ | **低〜中** | 可変長入力の動的処理 |

### 16.3 GPUCompute vs GPUPixel バックエンド

#### 技術的差異

| 項目 | GPUCompute | GPUPixel |
|------|-----------|----------|
| 実行方式 | Compute Shaderをcommand bufferで実行 | Pixel/Fragment Shaderをblitting操作で実行 |
| データアクセス | StructuredBuffer直接アクセス | テクスチャとしてエンコード/デコード |
| メモリ効率 | パディング不要、データ密度が高い | テクスチャパッキングのオーバーヘッド |
| 推定VRAM使用量 | モデルサイズの**1.0-1.3倍** | モデルサイズの**1.5-2倍** |
| WebGL2対応 | **不可** | 対応 |
| WebGPU対応 | **対応** | 対応 |
| 共有メモリ | ワークグループ共有メモリ (16KB) | なし |
| Unity公式見解 | 「GPUComputeとCPUが最速のバックエンド」 | 「compute shaderが使えないプラットフォーム向け」 |

#### VITSモデルオペレータのGPUCompute対応状況

VITSで使用される全51-52種類のオペレータは**全てGPUComputeで対応済み**:

| オペレータカテゴリ | 代表例 | GPUCompute | GPUPixel |
|------------------|--------|-----------|----------|
| 畳み込み | Conv, ConvTranspose | OK | OK |
| 行列演算 | MatMul, Gemm | OK | OK |
| 活性化関数 | Relu, Sigmoid, Tanh, LeakyRelu | OK | OK |
| 正規化 | BatchNorm, InstanceNorm | OK | OK |
| テンソル操作 | Reshape, Transpose, Concat, Slice, Gather | OK | OK |
| 削減演算 | ReduceSum, ReduceMean, Softmax | OK | OK |
| その他 | RandomNormalLike, Where, Pad, Cast | OK | OK |

> **注**: GroupNormalizationは全バックエンドで非対応だが、VITSはInstanceNormalizationを使用するため問題なし。

### 16.4 パフォーマンス改善見込み

#### 一般的なML推論ベンチマーク

| ワークロード | WebGPU vs WebGL2 | 出典 |
|-------------|-----------------|------|
| 行列乗算 (2048x2048+) | **3-8倍高速** | WebGPU-BLAS |
| LLMトークン生成 | **3-4倍高速** | ONNX Runtime Web |
| TensorFlow.js推論 (Phi-3-mini) | **3.8倍高速** (320ms→85ms/token) | Google |
| 小規模行列 (512x512以下) | <2倍 | 学術ベンチマーク |
| 256x256以下 | ≒1倍（測定誤差範囲） | 学術ベンチマーク |

#### uPiper VITSモデルへの推定影響

| 観点 | 推定 | 理由 |
|------|------|------|
| 全体推論速度 | **1.5-2倍改善** | バッチサイズ1の小規模推論はGPU dispatch overhead影響大 |
| ボコーダー部分 | **2-4倍改善** | ConvTranspose1d等の大量演算でcompute shaderの恩恵大 |
| メモリ使用量 | **30-50%削減** | テクスチャエンコーディング不要 |
| 初回推論 | **+1-5秒** | シェーダーコンパイルオーバーヘッド（2回目以降キャッシュ） |

#### 参考: TTS WebGPUベンチマーク実測値

| 条件 | スループット |
|------|-------------|
| Supertonic TTS - CPU (M4 Pro) | 912-1,263 chars/sec |
| Supertonic TTS - WebGPU (M4 Pro) | **996-2,509 chars/sec** |
| Supertonic TTS - WebGPU RTF | 0.006秒で1秒分の音声生成 |
| Kokoro TTS (82M params) - WebGPU | 100-300msレイテンシ |

> **注意**: 小規模モデル (batch=1) ではWASMの方がレイテンシ面で有利な場合がある (WASM: 8-12ms vs WebGPU: 15-25ms)。VITSのボコーダー部分（大量演算）でWebGPUの恩恵を最も受ける。

### 16.5 Sentis (旧Inference Engine) WebGPU対応状況

#### バージョン別対応

| バージョン | リリース日 | WebGPU対応 | 備考 |
|-----------|-----------|-----------|------|
| **2.2.0** | 2025-05-15 | ビルド失敗 | `GroupConv`シェーダーコンパイルエラー (`workgroupBarrier`) |
| **2.2.1** | 2025-05-28 | ビルド失敗 | 同上（未修正） |
| **2.2.2** (uPiper現在) | - | **ビルド失敗** | 同上（未修正） |
| **2.3.0** | 2025-07-15 | **ビルド成功** | `workgroupBarrier`エラー修正済み。ConvTranspose group/dilations追加 |
| **2.4.0** | 2025-10-22 | ビルド成功 | **Sentisにリネーム**。推論精度修正、メモリリーク修正 |
| **2.4.1** | 2025-10-31 | ビルド成功 | ドキュメント修正 |
| **2.5.0** (最新) | 2026-01-23 | ビルド成功 | PyTorchインポート、TopK GPUCompute修正、メモリリーク修正 |

> **結論: Sentis 2.5.0へのアップグレードを推奨**（WebGPU対応 + 推論精度改善 + メモリリーク修正）

#### 命名の変遷

```
Sentis (〜2.1) → Inference Engine (2.2.0〜2.3.0) → Sentis (2.4.0〜)
パッケージID: com.unity.ai.inference (全バージョン共通、変更なし)
```

#### 既知の問題

| 問題 | 影響 | 対策 |
|------|------|------|
| GPUComputeクラッシュ（大規模モデル600MB級） | IE 2.1-2.3で報告。VITSモデル(~60MB)での発生は要検証。2.5.0で明示的修正記載なし | CPUバックエンドへのフォールバック |
| GPUPixelメモリリーク | WebGL/WebGPUで報告あり。2.4.0/2.5.0でメモリリーク修正あり | 2.5.0へアップグレード |
| 同期GPUリードバック不可 | `ComputeBuffer.GetData`等使用不可 | `AsyncGPUReadback`使用 |
| RWBuffer非対応 | WebGPU制約 | RWStructuredBuffer使用（Sentisは対応済み） |
| 非同期Compute非対応 | WebGPU制約 | 単一キュー逐次実行 |

#### WebGPU使用時の推奨バックエンド

| 設定 | 推奨度 | 理由 |
|------|--------|------|
| **GPUCompute** | 最推奨 | Compute Shader対応、最速バックエンド。DirectML加速対応 |
| GPUPixel | フォールバック | Compute Shader非対応向け。WebGL2ではこちらを使用 |
| CPU | 非推奨 | WASM経由で低速 |

> **注意**: uPiper独自の`InferenceBackend.Auto`は公式APIではない。`SystemInfo.supportsComputeShaders`でランタイム判定する方式を推奨。

### 16.6 Unity 6 WebGPU実験的サポートの現状

#### バージョン別対応状況

| バージョン | WebGPU対応 | アクセスレベル | 備考 |
|-----------|-----------|--------------|------|
| Unity 6000.0.x (6.0) | 実験的 | 早期アクセス（限定的） | compute関連FPS劣化報告あり (40fps→7fps) |
| Unity 6000.1.x (6.1) | 実験的 | **パブリックアクセス** | WebGPU一般公開。URP 3D Sample動作確認済み |
| Unity 6000.3.x LTS (6.3) | 実験的 | パブリックアクセス | テクスチャバインディング自動処理、バグ修正多数 |

#### WebGPUで利用可能になるUnity機能

| 機能 | WebGL2 | WebGPU |
|------|--------|--------|
| Compute Shaders | 不可 | **対応** (RWStructuredBufferのみ、RWBufferは不可) |
| Indirect Rendering | 不可 | **対応** |
| GPU Skinning | 不可 | **対応** |
| VFX Graph | 不可 | **対応** |
| Async Compute | 不可 | **不可** |
| Dynamic Resolution | 不可 | **不可** |
| Cubemap Arrays | 不可 | **不可** |

#### 「実験的」の意味

- プロダクション使用は**非推奨**（Unityが明示的に記載）
- 全ブラウザ・デバイスでの動作保証なし
- 今後のバージョンでAPI変更の可能性あり
- バグや制約が多数存在

### 16.7 ブラウザ対応状況（2025年11月時点）

#### デスクトップブラウザ

| ブラウザ | バージョン | 対応状況 | 備考 |
|---------|-----------|---------|------|
| **Chrome** | 113+ | デフォルト有効 | Windows (D3D12), macOS, ChromeOS |
| **Edge** | 113+ | デフォルト有効 | Chromiumベース、Chromeと同等 |
| **Firefox** | 141+ (Win), 145+ (macOS) | デフォルト有効 | macOSはApple Silicon + macOS Tahoe 26のみ。Linux未対応(2026年予定) |
| **Safari** | 26.0+ | デフォルト有効 | macOS Tahoe 26, visionOS 26対応 |

#### モバイルブラウザ

| ブラウザ | 対応状況 | 要件 |
|---------|---------|------|
| Chrome for Android | 121+で対応 | Android 12+, Qualcomm/ARM GPU必須 |
| Safari for iOS | 26.1+で対応 | iOS 26必須 |
| Firefox for Android | **未対応** | 2026年対応予定 |

#### 全体カバレッジ

- **Can I Use**: 約70%のブラウザカバレッジ（2026年時点）
- デスクトップユーザーの約65-70%がWebGPU対応ブラウザを使用
- 残りのギャップ: Linux全般、古いAndroid、Firefox Android、Intel Mac

#### WebGPU/WebGL2 デュアル対応戦略

**推奨**: WebGPUをプライマリ + WebGL2フォールバック

```
WebGPU対応ブラウザ → WebGPU + GPUCompute → 高速推論
WebGL2フォールバック → WebGL2 + GPUPixel → 通常速度の推論
```

ランタイムでの判定:
```csharp
#if UNITY_WEBGL && !UNITY_EDITOR
    if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.WebGPU)
    {
        // WebGPU: GPUCompute使用
    }
    else
    {
        // WebGL2: GPUPixel使用
    }
#endif
```

### 16.8 uPiper既存コードへの影響分析

#### 変更が必要な箇所

| ファイル | 変更内容 | 優先度 |
|---------|---------|--------|
| **InferenceAudioGenerator.cs** (L462-499) | `DetermineBackendType()`にWebGPU判定追加。`SystemInfo.graphicsDeviceType == WebGPU`でGPUCompute選択 | **必須** |
| **InferenceAudioGenerator.cs** (L438-444) | GPUCompute→GPUPixel強制変換のWebGPU例外追加。WebGPU環境ではGPUComputeをそのまま許可 | **必須** |
| **PlatformHelper.cs** | `IsWebGPU`プロパティ追加 | **推奨** |
| **ProjectSettings.asset** (L774) | `webGLEnableWebGPU: 0` → `1` に変更 | **必須** |

#### 推奨コード変更

**InferenceAudioGenerator.cs - DetermineBackendType():**
```csharp
#if UNITY_WEBGL
    // WebGPU有効時はGPUComputeが使用可能
    if (SystemInfo.supportsComputeShaders)
    {
        PiperLogger.LogInfo("[InferenceAudioGenerator] Auto-selecting GPUCompute backend for WebGPU");
        return BackendType.GPUCompute;
    }
    // WebGL2フォールバック: GPUPixel
    PiperLogger.LogInfo("[InferenceAudioGenerator] Auto-selecting GPUPixel backend for WebGL2");
    return BackendType.GPUPixel;
#endif
```

**InferenceAudioGenerator.cs - GPUCompute強制変換の例外:**
```csharp
if (config.Backend == InferenceBackend.GPUCompute)
{
    // WebGPU Compute Shaderでは問題なく動作する可能性
    #if UNITY_WEBGL
    if (SystemInfo.supportsComputeShaders)
    {
        PiperLogger.LogInfo("[InferenceAudioGenerator] GPUCompute on WebGPU - allowing");
        return BackendType.GPUCompute;
    }
    #endif
    // 既存の警告・フォールバック
    PiperLogger.LogWarning("GPUCompute has known issues with VITS models...");
    return BackendType.GPUPixel;
}
```

**PlatformHelper.cs - IsWebGPUプロパティ追加:**
```csharp
/// <summary>WebGPUが有効かどうかを判定</summary>
public static bool IsWebGPU =>
    IsWebGL && SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.WebGPU;
```

#### 変更不要な箇所

| ファイル | 理由 |
|---------|------|
| **PiperConfig.cs** | `InferenceBackend` enumに新しい値追加不要。既存のGPUComputeがWebGPU上で動作 |
| **PiperTTS.cs** | `#if !UNITY_WEBGL`ガードは維持。ファイルI/O制約はWebGPUと無関係 |
| **PlatformDefines.cs** | `IS_WEBGL_PLATFORM=true`は正しい。WebGPUもWebGLビルド配下 |
| **InferenceEngineDemo.cs** (phonemizer部分) | ファイルI/O制約は不変 |

#### WebGPU検出方法

- **ランタイム判定**: `SystemInfo.graphicsDeviceType == GraphicsDeviceType.WebGPU`
- **Compute Shader判定**: `SystemInfo.supportsComputeShaders` (WebGPU=true, WebGL2=false)
- **コンパイル時**: `#if UNITY_WEBGL`のみ（WebGPU専用定義シンボルはない）
- → **ランタイム判定が必須**

### 16.9 WebGPU有効化手順

#### Step 1: Player Settings設定

1. **Edit > Project Settings > Player** を開く
2. **Web** タブを選択
3. **Other Settings** を展開
4. **Auto Graphics API** のチェックを**外す**
5. **+ボタン** → **WebGPU** を選択
6. **WebGPU** をリストの最上位にドラッグ（優先）
7. **WebGL 2** も残してフォールバック用に設定

#### Step 2: GitHub Pagesデプロイ対応

##### SharedArrayBuffer要件

Unity WebGLビルド（マルチスレッド使用時）は**SharedArrayBuffer**が必要で、以下のHTTPヘッダーが必須:
```
Cross-Origin-Opener-Policy: same-origin
Cross-Origin-Embedder-Policy: require-corp
```

##### 問題: GitHub PagesはカスタムHTTPヘッダー設定不可

##### 解決策: coi-serviceworker

[coi-serviceworker](https://github.com/gzuidhof/coi-serviceworker) を使用してService Worker経由でCOOP/COEPヘッダーをエミュレート:

1. `coi-serviceworker.js` をビルド出力のルートに配置
2. `index.html` の `<head>` に追加:
```html
<script src="coi-serviceworker.js"></script>
```

**注意点**:
- CDNからは読み込めない（自分のoriginから配信が必要）
- HTTPSまたはlocalhostでの配信が必要
- 初回アクセス時にページがリロードされる（Service Worker登録のため）

#### Step 3: デバッグ

```csharp
#if UNITY_WEBGL && !UNITY_EDITOR
    Debug.Log($"[WebGPU Debug] Graphics API: {SystemInfo.graphicsDeviceType}");
    Debug.Log($"[WebGPU Debug] GPU: {SystemInfo.graphicsDeviceName}");
    Debug.Log($"[WebGPU Debug] Compute Shaders: {SystemInfo.supportsComputeShaders}");
    Debug.Log($"[WebGPU Debug] Async GPU Readback: {SystemInfo.supportsAsyncGPUReadback}");
#endif
```

**推奨デバッグツール**:
- [WebGPU Inspector](https://github.com/brendan-duncan/webgpu_inspector) (Chrome拡張) - GPUオブジェクト検査、コマンドキャプチャ
- Chrome DevTools Performance Panel - ボトルネック特定

### 16.10 WebGPU対応ロードマップ（更新版）

#### 即時対応可能な項目

| タスク | 工数 | 効果 |
|--------|------|------|
| Player Settings: WebGPU有効化 + WebGL2フォールバック | 小 | WebGPU対応ブラウザで自動的にWebGPU使用 |
| `PlatformHelper.IsWebGPU` プロパティ追加 | 小 | 判定ロジックの集約 |

#### Sentis 2.5.0アップグレード後の対応

| タスク | 工数 | 効果 |
|--------|------|------|
| Sentis 2.5.0へアップグレード | 中 | WebGPUビルド対応 + 推論精度改善 + メモリリーク修正 |
| `DetermineBackendType()` WebGPU判定追加 | 小 | GPUComputeバックエンド使用可能 |
| GPUCompute強制変換のWebGPU例外追加 | 小 | VITSモデルでGPUCompute検証 |
| VITSモデルGPUCompute動作検証 | 中 | 無音/破損問題の再テスト |
| パフォーマンスベンチマーク (GPUPixel vs GPUCompute) | 中 | 実測値で戦略判断 |

#### 将来（WebGPU安定後）

| タスク | 工数 | 効果 |
|--------|------|------|
| FP16推論対応 (shader-f16拡張) | 中 | メモリ50%削減、ALU 25%向上 |
| WebGPU専用最適化 (ストレージバッファ活用) | 大 | 推論パフォーマンス最大化 |
| coi-serviceworker統合 (GitHub Pages対応) | 小 | SharedArrayBuffer有効化 |

### 16.11 WebGPU総合評価

| 観点 | 評価 | 詳細 |
|------|------|------|
| **推論速度改善** | 1.5-4倍 | ボコーダー部分で最大効果、全体では1.5-2倍 |
| **メモリ効率** | 30-50%改善 | テクスチャエンコーディング不要 |
| **ブラウザカバレッジ** | ~70% | 全主要ブラウザがデフォルト出荷 |
| **Unity対応成熟度** | 実験的 | プロダクション非推奨だが機能的には利用可能 |
| **Sentis対応** | 2.3.0+で対応（2.5.0推奨） | 2.2.2ではビルド失敗 |
| **コード変更量** | 小〜中 | 主に`DetermineBackendType()`の条件分岐追加 |
| **リスク** | 中 | GPUComputeクラッシュ（大規模モデル）、実験的ステータス |
| **推奨** | **有効化推奨** | WebGL2フォールバック付きで有効化し、対応ブラウザでは自動的に恩恵を受ける構成が最適 |

> **結論**: WebGPUは実験的ステータスだが、WebGL2フォールバック付きで有効化することでリスクを最小限に抑えつつ、対応ブラウザでの推論速度改善・メモリ効率改善の恩恵を受けられる。Sentis 2.5.0へのアップグレードが前提条件（WebGPUビルド対応 + 推論精度改善 + メモリリーク修正）。

---

## 参考ソース

### Unity公式
- [Unity Manual: Memory in Unity Web](https://docs.unity3d.com/6000.3/Documentation/Manual/webgl-memory.html)
- [Unity Manual: WebGPU (Experimental)](https://docs.unity3d.com/6000.3/Documentation/Manual/WebGPU.html)
- [Unity Manual: Web Audio](https://docs.unity3d.com/Manual/webgl-audio.html)
- [Sentis 2.5.0 Manual](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.5/manual/index.html)
- [Sentis 2.5.0 Create an engine](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.5/manual/create-an-engine.html)
- [Sentis 2.5.0 Supported ONNX operators](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.5/manual/supported-operators.html)
- [Sentis 2.5.0 Changelog](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.5/changelog/CHANGELOG.html)
- [Sentis 2.4.0 Changelog](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.4/changelog/CHANGELOG.html)
- [Inference Engine 2.3.0 Changelog](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.3/changelog/CHANGELOG.html)
- [WebGPU Build fails with IE (Issue Tracker)](https://issuetracker.unity3d.com/issues/webgpu-build-fails-when-the-inference-engine-package-is-installed)

### Unity Discussions / Blog
- [Sentis support for Unity Web platform](https://discussions.unity.com/t/sentis-support-for-the-unity-web-platform/1552688)
- [Sentis naming discussion](https://discussions.unity.com/t/did-inference-engine-package-revert-to-the-old-sentis-name/1695183)
- [IE 2.3.0 GPUCompute Crash Discussion](https://discussions.unity.com/t/inference-engine-2-3-0-gpucompute-crash-during-schedule/1682922)
- [Jets TTS on Sentis](https://discussions.unity.com/t/jets-tts-on-sentis-ai-inference-engine/1682414)
- [Piper Unity (Macoron)](https://discussions.unity.com/t/piper-unity-open-fast-and-high-quality-tts/337243)
- [Web build memory consumption on Unity 6](https://discussions.unity.com/t/web-build-memory-consumption-on-unity-6/1613334)
- [Understanding memory in Unity WebGL](https://unity.com/blog/engine-platform/understanding-memory-in-unity-webgl)

### 外部プロジェクト
- [piper-tts-web (Poket-Jony)](https://github.com/Poket-Jony/piper-tts-web) - ONNX Runtime Web + piper_phonemize WASM
- [piper-wasm (wide-video)](https://github.com/wide-video/piper-wasm) - Emscripten完全コンパイル
- [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx) - C++→Emscripten WASM TTS
- [vits-web (diffusionstudio)](https://github.com/nickspaargaren/vits-web) - ONNX Runtime Web VITS
- [UnityWebGLAudioStream (hecomi)](https://github.com/hecomi/UnityWebGLAudioStream) - Web Audio API直接操作
- [WebGLThreadingPatcher](https://github.com/nickspaargaren/WebGLThreadingPatcher) - Task.RunのWebGL対応パッチ

### WebGPU関連
- [Unity Manual: WebGPU (Experimental)](https://docs.unity3d.com/6000.3/Documentation/Manual/WebGPU.html)
- [Unity Manual: Enable WebGPU](https://docs.unity3d.com/6000.3/Documentation/Manual/WebGPU-enable.html)
- [Unity Manual: WebGPU Features](https://docs.unity3d.com/6000.3/Documentation/Manual/WebGPU-features.html)
- [Unity Manual: WebGPU Limitations](https://docs.unity3d.com/6000.2/Documentation/Manual/WebGPU-limitations.html)
- [Unity Discussions: Public access to WebGPU in Unity 6.1](https://discussions.unity.com/t/public-access-to-webgpu-experimental-in-unity-6-1/1572462)
- [WebGPU Build fails with IE (Issue Tracker)](https://issuetracker.unity3d.com/issues/webgpu-build-fails-when-the-inference-engine-package-is-installed)
- [IE 2.3.0 GPUCompute Crash Discussion](https://discussions.unity.com/t/inference-engine-2-3-0-gpucompute-crash-during-schedule/1682922)
- [Can I Use - WebGPU](https://caniuse.com/webgpu)
- [WebGPU Hits Critical Mass: All Major Browsers](https://www.webgpu.com/news/webgpu-hits-critical-mass-all-major-browsers-now-ship-it/)
- [web.dev: WebGPU supported in major browsers](https://web.dev/blog/webgpu-supported-major-browsers)
- [Chrome: From WebGL to WebGPU](https://developer.chrome.com/docs/web-platform/webgpu/from-webgl-to-webgpu)
- [WebGPU Error Handling Best Practices](https://toji.dev/webgpu-best-practices/error-handling.html)
- [WebGPU Fundamentals: Storage Buffers](https://webgpufundamentals.org/webgpu/lessons/webgpu-storage-buffers.html)
- [WGSL vs GLSL](https://dmnsgn.me/blog/from-glsl-to-wgsl-the-future-of-shaders-on-the-web/)
- [ONNX Runtime Web WebGPU](https://opensource.microsoft.com/blog/2024/02/29/onnx-runtime-web-unleashes-generative-ai-in-the-browser-using-webgpu/)
- [WebGPU 2026: 70% Browser Support](https://byteiota.com/webgpu-2026-70-browser-support-15x-performance-gains/)
- [WebGPU Inspector (Chrome Extension)](https://github.com/brendan-duncan/webgpu_inspector)
- [coi-serviceworker (COOP/COEP for GitHub Pages)](https://github.com/gzuidhof/coi-serviceworker)

### 技術資料
- [V8 Blog: Up to 4GB of memory in WebAssembly](https://v8.dev/blog/4gb-wasm-memory)
- [ONNX Runtime: Quantize ONNX models](https://onnxruntime.ai/docs/performance/model-optimizations/quantization.html)
- [Chrome: Autoplay Policy](https://developer.chrome.com/blog/autoplay)
- [MDN: Web Audio API Autoplay](https://developer.mozilla.org/en-US/docs/Web/Media/Guides/Autoplay)
