# uPiper WebGL/Web技術レポート - 2026年3月版

## WebGL基本対応 ✅ 完了（v1.3.0, PR #122）

以下のWebGL/WebGPU基本対応はv1.3.0で実装済み:

| 機能 | ファイル | 状態 |
|---|---|---|
| WebGPU自動検出 | `PlatformHelper.IsWebGPU` | ✅ |
| WebGPU時GPUCompute自動選択 | `InferenceAudioGenerator.cs` | ✅ |
| WebGL2時GPUPixel自動選択 | `InferenceAudioGenerator.cs` | ✅ |
| Sentis 2.5.0アップグレード | `manifest.json` | ✅ |
| 非同期StreamingAssets読み込み | `WebGLStreamingAssetsLoader.cs` | ✅ |
| IndexedDBキャッシュ | `IndexedDBCache.cs` + `.jslib` | ✅ |
| 大容量ファイル分割配信 | `WebGLSplitDataProcessor.cs` + JS | ✅ |
| GitHub Pages対応 | `github-pages-adapter.js` | ✅ |
| ローディングUI | `WebGLLoadingPanel.cs` | ✅ |
| AudioContext起動ゲート | `WebGLInteractionGate.cs` | ✅ |
| deploy-webgl.yml CI/CD | `.github/workflows/` | ✅ |
| 非同期辞書初期化 | `DotNetG2PPhonemizer.InitializeAsync()` | ✅ |

## 今後の最適化（未実装）

### WebGPU Compute Shader最適化（Sentis依存）
- WebGPU固有の直接バッファアクセス、共有メモリ、ワークグループ同期はSentis内部の推論カーネル最適化に依存
- uPiper側の追加実装は不要。Sentisのバージョンアップで自動的に恩恵を受ける
- 2026年1月に全主要ブラウザでWebGPUサポート達成
- **難易度**: N/A（Sentis依存） / **インパクト**: 高（将来的）

### WebAssembly最適化 — 調査完了（2026-03-14）
- **SIMD（WebAssembly 2023）**: 有効化可能（低リスク）。ただしuPiperはGPU推論主体のため劇的改善なし。AudioClipBuilderのfloat配列処理で中程度の効果。副次メリット（例外処理オーバーヘッド削減・コードサイズ削減）あり。現在 `webWasm2023: 0`（無効）→ 有効化推奨
- ~~**スレッディング**: SharedArrayBuffer + Atomicsで1.8-2.9倍追加高速化~~ → **見送り**。C#スレッド非対応（`Task.Run()`不可）、GPU推論への恩恵なし、GitHub Pagesでヘッダー設定不可
- **Memory64**: 大規模モデル（>4GB）サポート準備
- **結論**: SIMDは有効化推奨、Threadingは見送り

### Web Audio API / AudioWorklet
- メインスレッドから分離した超低遅延音声処理
- Emscripten Wasm Audio Worklets APIでC++/WASMをWorklet内実行
- **難易度**: 中-高 / **インパクト**: 中-高

### PWA対応
- Service Worker: ONNXモデル=Cache-First、辞書=Network-First
- オフライン動作、2回目以降の起動高速化
- **難易度**: 低-中 / **インパクト**: 中

### Cross-Origin対応 — Threading見送りに伴い優先度低下
- COOP/COEPヘッダー設定でSharedArrayBuffer有効化
- GitHub Pages制約→代替ホスティング（Netlify/Vercel/Cloudflare Pages）検討
- Threading見送りのため現時点では不要。将来Unity C#スレッドがWebGL対応した際に再評価
- **難易度**: 高 / **インパクト**: 低（現時点）

### モバイルブラウザ対応
- iOS Safari: WebGL 2.0有効化ガイダンスUI
- Android: WebGL 2.0サポート検出とフォールバック
- メモリ最適化（低解像度モデル自動選択）
- **難易度**: 中 / **インパクト**: 高

## Sources

- [WebGPU API - MDN](https://developer.mozilla.org/en-US/docs/Web/API/WebGPU_API)
- [WebGPU Hits Critical Mass: All Major Browsers Now Ship It](https://www.webgpu.com/news/webgpu-hits-critical-mass-all-major-browsers/)
- [Boosting WebAssembly Performance with SIMD and Multi-Threading](https://www.infoq.com/articles/webassembly-simd-multithreading-performance-gains/)
- [AudioWorklet - MDN](https://developer.mozilla.org/en-US/docs/Web/API/AudioWorklet)
