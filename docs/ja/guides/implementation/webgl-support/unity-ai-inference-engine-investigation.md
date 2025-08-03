# Unity AI Inference Engine WebGL制限調査結果

## 調査日: 2025年8月

## エグゼクティブサマリー

Unity AI Inference Engine（旧Sentis）は**WebGLをサポートしています**が、いくつかの制限があります。現在のuPiper実装では、WebGL向けに自動的にGPUPixelバックエンドを選択する仕組みが既に実装されています。

## 現在の実装状況

### 1. WebGLバックエンドサポート（実装済み）

`InferenceAudioGenerator.cs`では、WebGL向けの自動バックエンド選択が実装されています：

```csharp
#if UNITY_WEBGL
    // WebGL typically works better with GPUPixel
    PiperLogger.LogInfo("[InferenceAudioGenerator] Auto-selecting GPUPixel backend for WebGL");
    return BackendType.GPUPixel;
#endif
```

### 2. サポートされるバックエンド

| バックエンド | WebGLサポート | 備考 |
|------------|--------------|------|
| **GPUPixel** | ✅ 推奨 | WebGLで最も安定して動作 |
| **CPU** | ⚠️ 可能 | WebAssembly（Burst）にコンパイルされるため低速 |
| **GPUCompute** | ❌ 非推奨 | Compute Shader非対応環境で問題 |

### 3. 公式ドキュメントの見解

Unity Sentis公式ドキュメントより：
- WebGLでCPUバックエンドを使用すると、BurstがWebAssemblyにコンパイルされ**パフォーマンスが低下**
- GPUPixelは**Compute Shaderをサポートしないプラットフォーム向け**
- WebGLではCompute Shaderサポートが限定的

## 技術的制限と課題

### 1. メモリ制限
- デフォルト512MBヒープ設定（ONNXモデル用）
- 大規模モデルでメモリ不足の可能性

### 2. パフォーマンス制限
- GPUPixelバックエンドはGPUComputeより低速
- FP16（半精度浮動小数点）非サポート

### 3. 既知の問題
- GPUPixelでのメモリリーク報告（2024年11月コミュニティ討論）
- VITSモデルのGPUCompute互換性問題

## ONNX Runtime Web代替案の検討

### ONNX Runtime Webの現状
- **WebGL**: メンテナンスモードで新機能追加なし
- **WebGPU**: 推奨される新しいGPUアクセラレーション方式
- **WASM**: 全オペレータサポートだが低速

### Unity WebGLとの統合課題
- ONNX Runtime WebはUnity外部のJavaScriptライブラリ
- Unity-JavaScript間のデータ転送オーバーヘッド
- 二重のメモリ管理（Unity側とブラウザ側）

## 推奨される技術的解決策

### 方針1: Unity AI Inference Engine継続使用（推奨）

**メリット**:
- 既存実装の活用が可能
- Unityネイティブ統合で開発効率が高い
- GPUPixelバックエンドで基本的な動作は保証

**実装方針**:
1. 現在のGPUPixel自動選択を維持
2. メモリ使用量の最適化（512MB→1GB）
3. モデルサイズの最適化（量子化検討）

### 方針2: ハイブリッドアプローチ

**アーキテクチャ**:
```
テキスト → WebAssembly音素化（wasm_open_jtalk）
        → Unity AI Inference Engine（GPUPixel）
        → 音声出力
```

**メリット**:
- 音素化処理をWebAssemblyで高速化
- 音声合成はUnity内で完結
- 段階的な実装が可能

### 方針3: ONNX Runtime Web直接統合（将来的オプション）

**条件**:
- Unity AI Inference Engineで許容できないパフォーマンスの場合のみ
- WebGPUが主要ブラウザで安定した後

## 結論と次のステップ

### 結論
Unity AI Inference EngineのWebGL制限は**回避可能**です。GPUPixelバックエンドで基本的な音声合成は実現できます。

### 推奨アクション
1. **Phase 0は不要**: Unity AI Inference Engineで進行可能
2. **GPUPixel最適化**: 現在の実装をベースに最適化
3. **段階的リリース**: まず基本機能、次に最適化

### 実装優先順位
1. WebAssembly音素化（wasm_open_jtalk）統合
2. 既存のGPUPixelバックエンドでの動作確認
3. パフォーマンス測定と最適化
4. 必要に応じてONNX Runtime Web検討

## 参考情報

- Unity Sentis WebGL対応状況（2024年）
- コミュニティでのGPUPixelメモリリーク報告
- ONNX Runtime WebのWebGL非推奨化