# Realtime TTS Sample

このサンプルは、uPiperを使用した低レイテンシ・リアルタイム音声生成の実装例です。インタラクティブなアプリケーションやゲームでの使用を想定し、100ms以下のレイテンシを目標としています。

## 機能

- **低レイテンシ生成**: GPU推論とキャッシュによる高速応答
- **優先度付きキュー**: 重要な音声を優先的に処理
- **音声の中断**: 現在の再生を即座に中断して新しい音声を再生
- **プリロード機能**: よく使うフレーズを事前に生成
- **レイテンシ計測**: リアルタイムのパフォーマンス監視
- **クイックレスポンス**: ワンタッチで定型文を発話

## セットアップ

1. **サンプルのインポート**
   - Unity Package Managerから「Realtime TTS Demo」をインポート

2. **シーンを開く**
   - `Assets/uPiper/Samples~/RealtimeTTS/Scenes/RealtimeDemo.unity`を開く

3. **最適化設定**
   - GPU推論を有効化（必須）
   - 高速な音声モデル（small/medium）を使用

## 使い方

### 基本操作
1. テキストフィールドに文章を入力
2. 「Speak」ボタンまたはEnterキーで発話
3. 「Interrupt」ボタンで中断

### クイックレスポンス
- 事前定義されたボタンをクリックで即座に発話
- カスタマイズ可能な定型文

### 設定オプション
- **Preload**: よく使うフレーズを事前生成
- **Cache**: 生成済み音声をメモリに保持
- **Emotion**: 感情パラメータ（将来実装予定）

## パフォーマンス最適化

### 推奨設定
```csharp
config.Backend = InferenceBackend.GPUCompute;
config.GPUSettings.MaxBatchSize = 1;  // レイテンシ優先
config.EnablePhonemeCache = true;
config.WorkerThreads = 2;
```

### レイテンシ削減テクニック

1. **モデルの最適化**
   ```csharp
   // より小さいモデルを使用
   voiceConfig.VoiceId = "ja_JP-test-small";
   ```

2. **プリロード戦略**
   ```csharp
   // ゲーム起動時に頻出フレーズをプリロード
   await PreloadCommonPhrases(new[] {
       "はい", "いいえ", "わかりました",
       "ダメージを受けた！", "アイテムを取得"
   });
   ```

3. **キャッシュ管理**
   ```csharp
   // LRUキャッシュで自動管理
   if (_audioCache.Count > 100) {
       RemoveOldestEntry();
   }
   ```

## 実装詳細

### 優先度付きキュー
```csharp
public enum Priority {
    Low = 0,      // 背景音声
    Normal = 1,   // 通常の会話
    High = 2,     // クイックレスポンス
    Critical = 3  // システムメッセージ
}
```

### 中断処理
```csharp
// 即座にフェードアウトして新しい音声を再生
StartCoroutine(FadeOutAndStop(currentSource, 0.05f));
```

### レイテンシ計測
```csharp
var sw = Stopwatch.StartNew();
var audio = await GenerateAudioAsync(text);
sw.Stop();
UpdateLatency(sw.ElapsedMilliseconds);
```

## トラブルシューティング

### レイテンシが高い場合
1. GPU推論が有効か確認
2. より小さいモデルに変更
3. プリロードを活用
4. キャッシュを有効化

### 音声が途切れる場合
- Interrupt Fade Timeを調整（0.05-0.1秒）
- Audio Source優先度を上げる

### メモリ使用量が多い場合
- キャッシュサイズを制限
- 不要な音声クリップを破棄

## ゲーム統合例

### NPCダイアログシステム
```csharp
public class NPCDialogue : MonoBehaviour {
    private RealtimeTTSDemo tts;
    
    public async void Speak(string dialogue) {
        // 重要な会話は高優先度で
        await tts.EnqueueRequest(dialogue, Priority.High);
    }
}
```

### 戦闘中の音声フィードバック
```csharp
public class CombatVoice : MonoBehaviour {
    void OnDamage(float damage) {
        // ダメージ音声は即座に再生
        tts.InterruptAndSpeak($"{damage}のダメージ！");
    }
}
```

### インタラクティブチュートリアル
```csharp
public class TutorialVoice : MonoBehaviour {
    async void OnPlayerAction(string action) {
        // プレイヤーの行動に応じた音声ガイド
        var response = GetTutorialResponse(action);
        await tts.SpeakWithPriority(response, Priority.High);
    }
}
```

## パフォーマンス目標

| 指標 | 目標値 | 備考 |
|-----|-------|-----|
| 初回レイテンシ | < 150ms | GPU推論時 |
| キャッシュヒット時 | < 10ms | メモリから再生 |
| メモリ使用量 | < 100MB | 100フレーズキャッシュ時 |
| CPU使用率 | < 5% | 待機時 |

## 今後の拡張

1. **音声パラメータのリアルタイム変更**
   - 感情、速度、ピッチの動的調整

2. **ストリーミング統合**
   - 長文のリアルタイムストリーミング

3. **マルチスピーカー対応**
   - 複数キャラクターの同時発話