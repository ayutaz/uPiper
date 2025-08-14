# GPU推論ガイド

## 概要
uPiperは、Unity.InferenceEngineを使用してGPU推論をサポートしています。このガイドでは、GPU推論の設定、最適化、トラブルシューティングについて説明します。

## サポートされているバックエンド

### 1. CPU (BackendType.CPU)
- **特徴**: 最も互換性が高い
- **利点**: すべてのプラットフォームで動作
- **欠点**: 比較的遅い
- **推奨**: 互換性が最優先の場合

### 2. GPUCompute (BackendType.GPUCompute)
- **特徴**: Compute Shaderを使用
- **利点**: 理論上は高速な推論が可能
- **欠点**: VITSモデルとの互換性問題あり
- **重要**: 現在、日本語音声モデルで音声が正しく生成されない問題があるため、GPU Pixelまたは CPUの使用を推奨

### 3. GPUPixel (BackendType.GPUPixel)
- **特徴**: Pixel Shaderを使用
- **利点**: WebGL互換、VITSモデルとの互換性良好
- **欠点**: 一部の高度な機能に制限あり
- **推奨**: WebGLビルド、GPU推論が必要な場合の第一選択

## 設定方法

### 基本設定
```csharp
var config = new PiperConfig
{
    Backend = InferenceBackend.Auto,  // 自動選択
    AllowFallbackToCPU = true,        // CPU自動フォールバック
    GPUSettings = new GPUInferenceSettings
    {
        MaxBatchSize = 1,
        UseFloat16 = false,
        MaxMemoryMB = 512
    }
};

var tts = new PiperTTS();
await tts.InitializeAsync(config);
```

### 手動バックエンド選択
```csharp
// GPU Computeを強制
config.Backend = InferenceBackend.GPUCompute;

// CPUを強制（互換性重視）
config.Backend = InferenceBackend.CPU;
```

## プラットフォーム別推奨設定

### Windows/Linux (Desktop)
```csharp
config.Backend = InferenceBackend.Auto;  // GPUPixelが選択される（VITSモデル互換性のため）
config.GPUSettings.MaxBatchSize = 4;
config.GPUSettings.UseFloat16 = true;   // RTX系GPUで高速化

// GPU Computeを明示的に使用したい場合（非推奨）
// config.Backend = InferenceBackend.GPUCompute;
// ※VITSモデルで音声が正しく生成されない可能性があります
```

### macOS
```csharp
config.Backend = InferenceBackend.CPU;   // Metal問題回避
// 注意: 現在MetalはUnity.InferenceEngineで問題があるためCPU推奨
```

### モバイル (iOS/Android)
```csharp
config.Backend = InferenceBackend.Auto;
config.GPUSettings.MaxMemoryMB = 256;    // メモリ制限
config.GPUSettings.MaxBatchSize = 1;    // バッチサイズ制限
```

### WebGL
```csharp
config.Backend = InferenceBackend.GPUPixel;  // WebGL専用
config.GPUSettings.UseFloat16 = false;       // WebGL非対応
```

## パフォーマンスチューニング

### 1. バッチ処理
```csharp
// 複数テキストの同時処理
config.GPUSettings.MaxBatchSize = 8;  // GPU性能に応じて調整
```

### 2. メモリ管理
```csharp
// GPUメモリ使用量の制限
config.GPUSettings.MaxMemoryMB = 1024;  // 1GB制限
```

### 3. 精度設定
```csharp
// FP16使用（品質とのトレードオフ）
config.GPUSettings.UseFloat16 = true;
```

## トラブルシューティング

### 問題: GPU Computeで日本語音声が「ぶー」音になる
**症状**: GPU Computeバックエンドで日本語音声が正しく生成されない（「ぶー」という短音になる）
**原因**: Unity Inference Engine (Sentis)のGPU ComputeバックエンドとVITSモデルの互換性問題
**解決策**: 
```csharp
// GPU PixelまたはCPUを使用
config.Backend = InferenceBackend.GPUPixel;  // 推奨
// または
config.Backend = InferenceBackend.CPU;       // 最も安定
```
**備考**: この問題は自動的に検出され、GPU ComputeからGPU Pixelへ自動フォールバックされます。

### 問題: Metal shader compilation error
**症状**: macOSで「'metal_stdlib' file not found」エラー
**解決策**: 
```csharp
config.Backend = InferenceBackend.CPU;
```

### 問題: GPU初期化失敗
**症状**: GPU backendで初期化エラー
**解決策**:
```csharp
config.AllowFallbackToCPU = true;  // 自動CPUフォールバック有効
```

### 問題: メモリ不足
**症状**: GPU out of memory エラー
**解決策**:
```csharp
config.GPUSettings.MaxMemoryMB = 256;  // メモリ制限を下げる
config.GPUSettings.MaxBatchSize = 1;   // バッチサイズを下げる
```

## デバッグ方法

### GPU推論テストツール
1. Unity Editorで `Window > uPiper > GPU Inference Test` を開く
2. バックエンドを選択してテスト実行
3. 結果を確認

### ログ確認
```csharp
// 詳細ログを有効化
config.EnableDebugLogging = true;
```

ログ出力例:
```
[InferenceAudioGenerator] Auto-selecting GPUCompute backend for desktop
[InferenceAudioGenerator] Successfully initialized with backend: GPUCompute
```

## ベストプラクティス

### 1. 自動選択を活用
```csharp
config.Backend = InferenceBackend.Auto;
```
プラットフォームに最適なバックエンドが自動選択されます。

### 2. フォールバック有効化
```csharp
config.AllowFallbackToCPU = true;
```
GPU初期化失敗時に自動的にCPUにフォールバックします。

### 3. プロファイリング
```csharp
config.GPUSettings.EnableProfiling = true;
```
パフォーマンス計測を有効化します。

### 4. 実機テスト
開発環境と実機で異なる動作をする可能性があるため、必ず実機でテストしてください。

## パフォーマンス比較

典型的なパフォーマンス比較（相対値）:

| バックエンド | 初期化時間 | 推論速度 | メモリ使用量 |
|------------|-----------|---------|-------------|
| CPU        | 1.0x      | 1.0x    | 低          |
| GPUCompute | 2.0x      | 3-5x    | 中          |
| GPUPixel   | 1.5x      | 2-3x    | 中          |

※実際のパフォーマンスはハードウェアとモデルサイズに依存します。

## 既知の制限事項

1. **GPU Compute**: VITSモデル（日本語音声モデル）との互換性問題により、音声が正しく生成されない
2. **Metal (macOS)**: シェーダーコンパイルエラーのためCPU推奨
3. **WebGL**: GPUPixelのみサポート、FP16非対応
4. **モバイル**: メモリ制限によりバッチサイズ制限あり

### Unity Inference Engine (Sentis)の既知の問題
- **GPU Compute**: 特定のONNXオペレーターが未対応
- **テンソル転送**: GPU-CPU間のデータ転送時にデータ破損の可能性
- **VITSモデル**: GPU ComputeバックエンドでVITSアーキテクチャが正しく処理されない

## 今後の改善予定

1. Metal対応の改善
2. Dynamic batchingの実装
3. Multi-GPU対応
4. より詳細なプロファイリング機能