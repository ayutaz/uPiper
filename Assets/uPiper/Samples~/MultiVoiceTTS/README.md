# Multi-Voice TTS Sample

このサンプルは、uPiperを使用して複数の音声を同時に生成・再生する実装例です。最大4つの異なる音声モデルを並列で処理し、GPU推論を活用して高速な音声生成を実現します。

## 機能

- **最大4チャンネル同時処理**: 独立した音声生成チャンネル
- **異なるモデル/言語対応**: チャンネル毎に異なる音声モデルを使用可能
- **GPU推論活用**: 並列処理によるパフォーマンス向上
- **個別制御**: 音量、ピッチ、音声パラメータの独立制御
- **パフォーマンスモニタリング**: リアルタイムの処理統計表示

## セットアップ

1. **サンプルのインポート**
   - Unity Package Managerから「Multi-Voice Demo」をインポート

2. **シーンを開く**
   - `Assets/uPiper/Samples~/MultiVoiceTTS/Scenes/MultiVoiceDemo.unity`を開く

3. **音声モデルの設定**
   - 各チャンネルのModel Assetに異なるONNXモデルを設定
   - 例:
     - Channel 1: 日本語女性音声
     - Channel 2: 日本語男性音声
     - Channel 3: 英語音声
     - Channel 4: 中国語音声

## 使い方

### 個別チャンネル操作
1. 各チャンネルのテキストフィールドに文章を入力
2. 「Generate」ボタンで個別に音声生成
3. Volume/Pitchスライダーで調整

### 一括操作
1. 複数チャンネルにテキストを入力
2. 「Generate All」で全チャンネル同時生成
3. 「Stop All」で全チャンネル停止

## GPU推論の最適化

### 推奨設定
```csharp
_globalConfig.Backend = InferenceBackend.GPUCompute;
_globalConfig.GPUSettings.MaxBatchSize = 4;
_globalConfig.EnableMultiThreadedInference = true;
```

### メモリ管理
```csharp
// GPUメモリの制限
_globalConfig.GPUSettings.MaxMemoryMB = 1024; // 1GB
```

## カスタマイズ

### チャンネル数の変更
Inspectorで`Channels`リストのサイズを変更（最大8チャンネル推奨）

### 音声パラメータの調整
```csharp
channel.lengthScale = 1.2f;  // 話速を遅く
channel.noiseScale = 0.8f;   // より自然な音声に
```

### 3D空間配置
```csharp
// 各チャンネルを3D空間に配置
channel.audioSource.spatialBlend = 1.0f;  // 3D音源化
channel.audioSource.transform.position = new Vector3(x, y, z);
```

## パフォーマンスチューニング

### CPU/GPU負荷分散
```csharp
// 偶数チャンネルはGPU、奇数チャンネルはCPU
if (channelIndex % 2 == 0)
    config.Backend = InferenceBackend.GPUCompute;
else
    config.Backend = InferenceBackend.CPU;
```

### タスクスケジューリング
```csharp
// 優先度付きキューで管理
var priorityQueue = new PriorityQueue<GenerationTask>();
```

## トラブルシューティング

### GPU メモリ不足
- チャンネル数を減らす
- より小さいモデルを使用
- バッチサイズを1に設定

### 音声の遅延
- GPU推論を有効化
- 事前にモデルをウォームアップ

### 音声の混線
- 各AudioSourceの出力を別々のAudio Mixerグループに設定

## 実装のポイント

1. **並列処理**: Task.WhenAllによる効率的な並列実行
2. **リソース管理**: チャンネル毎の独立したTTSインスタンス
3. **状態管理**: 視覚的なステータスインジケーター
4. **エラーハンドリング**: チャンネル毎の独立したエラー処理

## 応用例

### ゲーム内NPC会話
```csharp
// NPCごとに異なる音声を割り当て
npc1.voiceChannel = _channels[0];
npc2.voiceChannel = _channels[1];
```

### 多言語アナウンス
```csharp
// 同じ内容を複数言語で同時生成
var announcement = "Welcome to Unity!";
_channels[0].inputField.text = announcement;          // 英語
_channels[1].inputField.text = "Unityへようこそ！";    // 日本語
_channels[2].inputField.text = "欢迎来到Unity！";      // 中国語
OnGenerateAll();
```

### インタラクティブ音声アシスタント
複数のアシスタントキャラクターが同時に応答する高度なインタラクション