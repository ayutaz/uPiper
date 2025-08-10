# Unity WebGL × ONNX Runtime Web 統合アーキテクチャ

## 概要
Unity WebGL で ONNX Runtime Web を使用して高品質な日本語音声合成を実現するためのアーキテクチャ設計書。

## 現状分析

### piper-plus の実装
piper-plus では以下の技術スタックで正常に動作している：

1. **ONNX Runtime Web**
   - CDN から直接ロード: `https://cdn.jsdelivr.net/npm/onnxruntime-web@1.17.1/dist/ort.min.js`
   - WASM 実行プロバイダーを使用
   - グラフ最適化レベル: 'all'

2. **音素処理フロー**
   ```javascript
   テキスト → OpenJTalk (WASM) → 音素ラベル → 音素ID変換 → ONNX推論 → 音声データ
   ```

3. **PUA文字マッピング**
   ```javascript
   const multiCharPhonemeMap = {
       'ch': '\ue00f',  // 複数文字音素を単一Unicode文字にマッピング
       'ts': '\ue00e',
       'ky': '\ue006',
       // ...
   };
   ```

### Unity WebGL の現状の問題
- Unity AI Inference Engine (Sentis) が VITS モデルの複雑な演算を正確に実行できない
- 特に「こんにちは」などの特定パターンで顕著な音質劣化

## 提案アーキテクチャ

### 全体構成
```
Unity C# Layer
    ↓
JSLib Bridge Layer
    ↓
JavaScript Runtime Layer
    ↓
ONNX Runtime Web
```

### レイヤー別実装

#### 1. Unity C# Layer
```csharp
public class ONNXRuntimeWebInference : IInferenceAudioGenerator
{
    // JavaScript関数の P/Invoke 宣言
    [DllImport("__Internal")]
    private static extern void InitializeONNXRuntime(string modelPath, Action<int> callback);
    
    [DllImport("__Internal")]
    private static extern void RunInference(int[] phonemeIds, int length, Action<float[], int> callback);
    
    // 非同期ラッパー
    public async Task<float[]> GenerateAudioAsync(int[] phonemeIds)
    {
        var tcs = new TaskCompletionSource<float[]>();
        RunInference(phonemeIds, phonemeIds.Length, (audioData, length) => {
            tcs.SetResult(audioData);
        });
        return await tcs.Task;
    }
}
```

#### 2. JSLib Bridge Layer
```javascript
// ONNXRuntimeBridge.jslib
mergeInto(LibraryManager.library, {
    InitializeONNXRuntime: function(modelPathPtr, callbackPtr) {
        const modelPath = UTF8ToString(modelPathPtr);
        
        // 非同期初期化
        window.UnityONNXRuntime.initialize(modelPath).then(() => {
            // Unity へコールバック
            dynCall('vi', callbackPtr, [1]); // 成功
        }).catch(error => {
            console.error('ONNX initialization failed:', error);
            dynCall('vi', callbackPtr, [0]); // 失敗
        });
    },
    
    RunInference: function(phonemeIdsPtr, length, callbackPtr) {
        // Unity から音素ID配列を受け取る
        const phonemeIds = [];
        for (let i = 0; i < length; i++) {
            phonemeIds.push(HEAP32[(phonemeIdsPtr >> 2) + i]);
        }
        
        // ONNX推論実行
        window.UnityONNXRuntime.synthesize(phonemeIds).then(audioData => {
            // Float32Array を Unity に返す
            const bufferPtr = _malloc(audioData.length * 4);
            HEAPF32.set(audioData, bufferPtr >> 2);
            
            // コールバック実行
            dynCall('vii', callbackPtr, [bufferPtr, audioData.length]);
            
            // メモリ解放
            _free(bufferPtr);
        });
    }
});
```

#### 3. JavaScript Runtime Layer
```javascript
// onnx-runtime-wrapper.js
class UnityONNXRuntime {
    constructor() {
        this.session = null;
        this.modelConfig = null;
    }
    
    async initialize(modelPath) {
        // ONNX Runtime Web をロード
        if (!window.ort) {
            await this.loadONNXRuntime();
        }
        
        // モデルをロード
        this.session = await ort.InferenceSession.create(modelPath, {
            executionProviders: ['wasm'],
            graphOptimizationLevel: 'all'
        });
        
        // モデル設定をロード
        const configResponse = await fetch(modelPath + '.json');
        this.modelConfig = await configResponse.json();
    }
    
    async loadONNXRuntime() {
        return new Promise((resolve, reject) => {
            const script = document.createElement('script');
            script.src = 'https://cdn.jsdelivr.net/npm/onnxruntime-web@1.17.1/dist/ort.min.js';
            script.onload = resolve;
            script.onerror = reject;
            document.head.appendChild(script);
        });
    }
    
    async synthesize(phonemeIds) {
        // テンソル作成
        const inputTensor = new ort.Tensor('int64', 
            new BigInt64Array(phonemeIds.map(id => BigInt(id))), 
            [1, phonemeIds.length]
        );
        
        const lengthTensor = new ort.Tensor('int64', 
            new BigInt64Array([BigInt(phonemeIds.length)]), 
            [1]
        );
        
        const scalesTensor = new ort.Tensor('float32', 
            new Float32Array([0.667, 1.0, 0.8]), 
            [3]
        );
        
        // 推論実行
        const feeds = {
            'input': inputTensor,
            'input_lengths': lengthTensor,
            'scales': scalesTensor
        };
        
        const results = await this.session.run(feeds);
        const audioTensor = results['output'] || results[Object.keys(results)[0]];
        
        return new Float32Array(audioTensor.data);
    }
}

// グローバルに公開
window.UnityONNXRuntime = new UnityONNXRuntime();
```

## データフロー

### 1. 初期化フロー
```
Unity Start()
    ↓
InitializeONNXRuntime(modelPath)
    ↓
JavaScript: ONNX Runtime Web ロード
    ↓
JavaScript: モデルロード
    ↓
Unity: 初期化完了コールバック
```

### 2. 音声生成フロー
```
Unity: テキスト入力
    ↓
Unity: OpenJTalk で音素変換（既存実装）
    ↓
Unity: 音素ID配列生成
    ↓
JSLib: RunInference(phonemeIds)
    ↓
JavaScript: ONNX推論
    ↓
JavaScript: Float32Array 生成
    ↓
Unity: AudioClip 作成
```

## メモリ管理

### Unity → JavaScript
- 音素ID配列: HEAP32 経由で直接アクセス
- 文字列: UTF8ToString() で変換

### JavaScript → Unity
- Float32Array: _malloc() でメモリ確保 → HEAPF32.set() → コールバック → _free()
- 非同期結果: コールバック関数ポインタ経由

## エラーハンドリング

### 初期化失敗時
- ONNX Runtime のロード失敗
- モデルファイルの404
- メモリ不足

### 推論失敗時
- 無効な音素ID
- テンソル形状の不一致
- タイムアウト

## パフォーマンス最適化

### 1. 遅延ロード
- ONNX Runtime は初回使用時にロード
- モデルは必要になるまでロードしない

### 2. キャッシュ
- 一度生成した音声はメモリにキャッシュ
- 同じテキストの再生成を避ける

### 3. Web Worker（将来的な拡張）
- 推論処理を Web Worker で実行
- メインスレッドのブロッキングを回避

## 実装優先順位

### Phase 1: 最小実装（MVP）
1. ONNX Runtime Web のロード
2. 単純な推論実行
3. Unity への音声データ返却

### Phase 2: 統合
1. 既存の音素処理と接続
2. エラーハンドリング
3. デバッグログ

### Phase 3: 最適化
1. キャッシュ実装
2. 遅延ロード
3. パフォーマンス測定

## テスト計画

### 単体テスト
1. JavaScript 単体での ONNX 推論
2. JSLib ブリッジの動作確認
3. メモリリークチェック

### 統合テスト
1. 「こんにちは」の正常な発音
2. 長文の処理
3. 連続実行時の安定性

### パフォーマンステスト
1. 初回ロード時間
2. 推論実行時間
3. メモリ使用量

## リスクと対策

| リスク | 影響 | 対策 |
|--------|------|------|
| CORS エラー | モデルロード失敗 | 適切な CORS ヘッダー設定 |
| メモリ不足 | ブラウザクラッシュ | 適切なメモリ管理とガベージコレクション |
| 非同期処理の複雑化 | デバッグ困難 | Promise チェーンの適切な管理 |
| ブラウザ互換性 | 一部環境で動作しない | ポリフィル使用、フォールバック実装 |

## まとめ
この設計により、Unity WebGL で piper-plus と同等の高品質な音声合成が実現可能になります。ONNX Runtime Web の実績ある実装を活用することで、Unity AI Inference Engine の制限を回避できます。