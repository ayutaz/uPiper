# Streaming TTS Sample

このサンプルは、uPiperを使用したストリーミング音声生成の実装例です。長いテキストを文節単位で分割し、リアルタイムに音声を生成・再生します。

## 機能

- **文節単位の音声生成**: テキストを自動的に分割して順次処理
- **並列処理**: 複数の文節を同時に生成してレイテンシを削減
- **クロスフェード**: 音声チャンク間のスムーズな切り替え
- **プログレス表示**: 生成進捗をリアルタイムで表示
- **GPU推論対応**: 高速な音声生成のためのGPU活用

## セットアップ

1. **サンプルのインポート**
   - Unity Package Managerから「Streaming TTS Demo」をインポート
   
2. **シーンを開く**
   - `Assets/uPiper/Samples~/StreamingTTS/Scenes/StreamingDemo.unity`を開く

3. **音声モデルの配置**
   - 日本語音声モデル（ONNX形式）を`Resources/Models/`に配置
   - モデル名を`StreamingTTSDemo`コンポーネントの`Voice Id`に設定

## 使い方

1. **テキスト入力**
   - Input Fieldに生成したいテキストを入力

2. **設定調整**
   - Auto Play: 自動再生の有効/無効
   - Overlap: クロスフェードの長さ（0.0-0.5秒）

3. **音声生成**
   - 「Generate」ボタンをクリック
   - 生成中は「Stop」ボタンで中断可能

## カスタマイズ

### 文節分割のカスタマイズ
```csharp
private List<string> SplitTextIntoChunks(string text)
{
    // カスタム分割ロジックを実装
    // 例: MeCabを使用した高度な文節分割
}
```

### 並列度の調整
```csharp
var semaphore = new SemaphoreSlim(4); // 最大4並列に変更
```

### バッファサイズの変更
```csharp
[SerializeField] private int _audioSourcePoolSize = 8; // プールサイズを増やす
```

## パフォーマンスチューニング

### GPU推論の有効化
```csharp
_piperConfig.Backend = InferenceBackend.GPUCompute;
_piperConfig.GPUSettings.MaxBatchSize = 4;
```

### メモリ使用量の削減
```csharp
// 使用済みAudioClipの即座の破棄
Destroy(audioClip);
```

## トラブルシューティング

### 音声が途切れる場合
- Overlapの値を増やす（0.2-0.3秒推奨）
- バッファリング時間を増やす

### 生成が遅い場合
- GPU推論を有効化
- 並列度を上げる
- より小さいモデルを使用

## 実装のポイント

1. **非同期処理**: async/awaitとコルーチンの組み合わせ
2. **リソース管理**: AudioSourceプールによる効率的な管理
3. **エラーハンドリング**: CancellationTokenによる適切な中断処理
4. **UI更新**: メインスレッドでの安全な更新

## 拡張アイデア

- 感情パラメータの動的変更
- 音声エフェクトの適用
- ネットワーク経由のストリーミング対応
- 複数話者の切り替え