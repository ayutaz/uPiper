# uPiper Tests

Unity AI Interface (Inference Engine) の基本動作確認テストです。

## テスト構成

### Runtime Tests
- `InferenceEngineBasicTest.cs` - Inference Engine の基本機能テスト
- `ONNXModelLoaderTest.cs` - ONNX モデル読み込みテスト

### Test Scene
- `InferenceEngineTestScene.unity` - 手動テスト用シーン
- `InferenceEngineTestManager.cs` - シーンでの動作確認スクリプト

## テスト実行方法

### 自動テスト
1. Unity Editor で Window > General > Test Runner を開く
2. PlayMode または EditMode タブを選択
3. Run All でテストを実行

### 手動テスト
1. `InferenceEngineTestScene` を開く
2. Play ボタンを押す
3. 自動的にテストが実行され、結果が表示される

## 確認項目

- ✅ Unity.InferenceEngine 名前空間の存在
- ✅ Model の作成
- ✅ Worker の作成と推論実行
- ✅ バックエンドの確認（GPU/CPU）
- ✅ テンソル操作

## 注意事項

- 実際の ONNX モデルファイルが必要な場合は、TestData フォルダに配置してください
- GPU が利用できない環境では CPU バックエンドが使用されます