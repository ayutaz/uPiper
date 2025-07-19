# uPiper デモシーン

このフォルダにはuPiperのデモシーンが含まれます。

## Inference Engine Demo

Phase 1.9のUnity.InferenceEngineを使用した音声生成デモです。

### 初回セットアップ

1. Unity Editorで「**uPiper > Demo > Create Inference Demo Scene**」を一度実行してください
2. シーンが`InferenceEngineDemo.unity`として作成されます

### シーンを開く

- メニューから「**uPiper > Demo > Open Inference Demo Scene**」を選択

### 実行方法

1. シーンを開く
2. Playボタンをクリック
3. テキストを入力
4. 「音声生成」ボタンをクリック

### 必要なパッケージ

- com.unity.ai.inference (Unity.InferenceEngine)
- Piper TTSモデル（Resources/Models/に配置済み）