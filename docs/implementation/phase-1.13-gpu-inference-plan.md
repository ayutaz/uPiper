# Phase 1.13: GPU推論サポートとサンプル実装計画

## 概要
Phase 1.13では、Unity.InferenceEngineのGPU推論サポートを実装し、高度な音声生成サンプルを作成します。

## 1. GPU推論サポート

### 1.1 現状の問題分析
- **現在の状況**: InferenceAudioGenerator.cs line 73-74
  - BackendType.CPUを強制使用
  - Metal shader compilation エラー: "metal_stdlib file not found"
  - GPU推論が利用できない

### 1.2 解決策
1. **BackendType設定の柔軟化**
   - PiperConfigにBackendType設定を追加
   - プラットフォーム別のデフォルト設定
   - 実行時のフォールバック機構

2. **Metal対応**
   - Unity.InferenceEngineのGPU要件確認
   - MetalPerformanceShaders.frameworkへの依存確認
   - プラットフォーム別の最適化設定

3. **エラーハンドリング**
   - GPU初期化失敗時の自動CPU切り替え
   - パフォーマンス警告の実装

### 1.3 実装タスク
- [ ] PiperConfigへのBackendType設定追加
- [ ] InferenceAudioGeneratorの改修
- [ ] プラットフォーム検出とデフォルト設定
- [ ] GPU/CPUフォールバック機構
- [ ] パフォーマンス計測とログ出力

## 2. 高度なサンプル実装

### 2.1 ストリーミング音声生成サンプル
**場所**: `Assets/uPiper/Samples~/StreamingTTS/`

**特徴**:
- リアルタイムテキスト入力対応
- 文節単位での音声生成
- バッファリングとスムーズな再生
- UI: プログレスバー、チャンク表示

**実装内容**:
```csharp
public class StreamingTTSDemo : MonoBehaviour
{
    // 文節分割
    // 非同期ストリーミング生成
    // AudioSourceプール管理
    // クロスフェード処理
}
```

### 2.2 複数音声同時処理サンプル
**場所**: `Assets/uPiper/Samples~/MultiVoiceTTS/`

**特徴**:
- 最大4音声の同時生成
- 異なるモデル/言語の組み合わせ
- CPU/GPU負荷分散
- 個別音量/ピッチ制御

**実装内容**:
```csharp
public class MultiVoiceTTSDemo : MonoBehaviour
{
    // 複数Workerインスタンス管理
    // タスクスケジューリング
    // リソース管理
    // 3D空間配置（オプション）
}
```

### 2.3 リアルタイム音声生成サンプル
**場所**: `Assets/uPiper/Samples~/RealtimeTTS/`

**特徴**:
- 低レイテンシ音声生成（< 100ms目標）
- 音声コマンド応答
- ゲーム内NPCダイアログ
- 感情パラメータ制御

**実装内容**:
```csharp
public class RealtimeTTSDemo : MonoBehaviour
{
    // 優先度付きキュー
    // プリロード機構
    // キャッシュ戦略
    // レイテンシ計測
}
```

## 3. API設計

### 3.1 GPU推論設定API
```csharp
// PiperConfig拡張
public class PiperConfig
{
    public InferenceBackendType PreferredBackend { get; set; } = InferenceBackendType.Auto;
    public bool AllowFallbackToCPU { get; set; } = true;
    public GPUSettings GPUSettings { get; set; } = new GPUSettings();
}

public class GPUSettings
{
    public int MaxBatchSize { get; set; } = 1;
    public bool UseFloat16 { get; set; } = false;
    public int MaxMemoryMB { get; set; } = 512;
}

public enum InferenceBackendType
{
    Auto,       // プラットフォーム自動選択
    CPU,        // CPU強制
    GPUCompute, // Compute Shader
    GPUPixel    // Pixel Shader (WebGL用)
}
```

### 3.2 ストリーミングAPI拡張
```csharp
public interface IStreamingOptions
{
    int ChunkSize { get; set; }
    float OverlapDuration { get; set; }
    bool EnableCrossfade { get; set; }
}
```

## 4. テスト計画

### 4.1 GPU推論テスト
- [ ] 各プラットフォームでのGPU初期化
- [ ] CPU/GPUパフォーマンス比較
- [ ] メモリ使用量測定
- [ ] フォールバック動作確認

### 4.2 サンプルテスト
- [ ] ストリーミング品質評価
- [ ] 同時処理数の限界測定
- [ ] レイテンシ測定
- [ ] リソース使用量プロファイリング

## 5. ドキュメント

### 5.1 GPU推論ガイド
- プラットフォーム別設定
- パフォーマンスチューニング
- トラブルシューティング

### 5.2 サンプルドキュメント
- 各サンプルのREADME
- APIリファレンス
- ベストプラクティス

## 6. 実装スケジュール

### Week 1: GPU推論基盤
- Day 1-2: BackendType API設計と実装
- Day 3-4: プラットフォーム検出とフォールバック
- Day 5: テストとドキュメント

### Week 2: サンプル実装
- Day 1-2: ストリーミングサンプル
- Day 3-4: 複数音声サンプル
- Day 5: リアルタイムサンプル

### Week 3: 統合とテスト
- Day 1-2: 統合テスト
- Day 3-4: パフォーマンス最適化
- Day 5: ドキュメント完成

## 7. リスクと対策

### リスク
1. Unity.InferenceEngineのGPU制限
2. プラットフォーム互換性問題
3. パフォーマンス目標未達成

### 対策
1. Unity公式ドキュメント確認とサポート問い合わせ
2. 段階的な実装とテスト
3. 代替ソリューション（Barracuda等）の検討

## 8. 成功基準

- [ ] GPU推論が最低1プラットフォームで動作
- [ ] CPU比で1.5倍以上の高速化
- [ ] 全サンプルが正常動作
- [ ] ドキュメント完備
- [ ] CI/CDパイプライン統合