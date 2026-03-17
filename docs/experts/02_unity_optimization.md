# uPiper Unity最適化レポート - 2026年3月版

## 最優先改善項目

### ~~1. PCMストリーミング再生~~ — 見送り（2026-03-14）
- 調査の結果、VITSモデルがチャンク推論不可（文全体のself-attention必須）、WebGLで`stream=true`/`OnAudioFilterRead`が動作しない、`PCMReaderCallback`に500ms〜1sのレイテンシ問題がある等の理由で見送り
- 代替: 文分割+`AudioSource.PlayScheduled()`による順次再生が現実的
- **結論**: 対応しない

### 2. モデル量子化（INT8/FP16）
- Sentis Quantization APIでFloat16/Uint8に量子化
- ディスクサイズ50-75%削減、メモリ使用量大幅削減
- **難易度**: 中 / **インパクト**: 高

### 3. Addressables対応
- `Resources.LoadAsync()`からAddressablesに移行
- 必要なモデルのみダウンロード、バージョン管理とホットアップデート対応
- **難易度**: 中-高 / **インパクト**: 高

## メモリ管理

### 非同期Readback API活用
- `outputTensor.ReadbackAndClone()` → `outputTensor.ReadbackAndCloneAsync()`（`Awaitable<Tensor<T>>`を返す）
- GPUComputeバックエンド（WebGPU）では真の非同期。GPUPixelバックエンド（Desktop/WebGL2）ではSentis内部が同期ラッパーのため実質的効果は限定的
- `lock` → `SemaphoreSlim`への変更が必要（awaitはlock内で使用不可）
- **難易度**: 低〜中 / **インパクト**: 低〜中（バックエンド依存）

### テンソルプーリング
- 頻繁に使用される入力テンソルの再利用プール
- GC頻度低減、連続TTS生成時に効果大
- **難易度**: 中 / **インパクト**: 中-高

## マルチスレッド処理

### Unity Awaitable活用（Unity 6）
- `Task`より低オーバーヘッドの`Awaitable`に移行
- `Awaitable.MainThreadAsync()` / `Awaitable.BackgroundThreadAsync()`
- **難易度**: 中 / **インパクト**: 中

### DirectML高速化（Windows）
- Unity 6 + DirectX 12で自動有効化
- Windows環境での推論速度50%向上
- **難易度**: 低 / **インパクト**: 中-高

## 実装ロードマップ

### Phase 1: 即効性のある最適化（1-2週間）
1. 非同期Readback API導入
2. NativeArray活用
3. DirectML有効化確認

### Phase 2: 音声再生改善（2-4週間）
1. ~~PCMReaderCallbackストリーミング~~ 見送り
2. 文分割+`AudioSource.PlayScheduled()`による順次再生（代替案）

### Phase 3: ビルドサイズ削減（2-3週間）
1. ONNX量子化
2. Addressables対応
3. ~~WebGL IndexedDBキャッシュ~~ ✅ 実装済み（v1.3.0）

## Sources

- [Unity Sentis Overview](https://unity.com/products/sentis)
- [Quantize a Model - Sentis 2.1.3](https://docs.unity3d.com/Packages/com.unity.sentis@2.1/manual/quantize-a-model.html)
- [Unity AudioClip.Create Documentation](https://docs.unity3d.com/ScriptReference/AudioClip.Create.html)
- [Unity Awaitable](https://docs.unity3d.com/6000.3/Documentation/Manual/async-awaitable-continuations.html)
