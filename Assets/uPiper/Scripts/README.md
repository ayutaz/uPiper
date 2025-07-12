# uPiper Scripts

Unity 向け Piper TTS 実装のスクリプト群です。

## Phase 0: 最小音声合成プロトタイプ

### MinimalTTSPrototype.cs
固定音素IDから波形を生成する最小限の実装です。
- 固定音素ID配列を使用
- ダミー波形生成（サイン波）
- AudioClip 作成と再生

使い方：
1. `uPiper > Setup Minimal TTS Prototype` でGameObjectを作成
2. Play ボタンで自動再生、またはGUIで手動再生

### PiperTTSPrototype.cs
実際のPiper TTSモデルを使用する実装です（簡易版）。
- ONNXモデルのロード（Resources フォルダから）
- 簡易的な音素変換
- 波形生成と再生

使い方：
1. ONNXモデルを `Assets/uPiper/Resources/` に配置
2. `uPiper > Setup Piper TTS Prototype` でGameObjectを作成
3. テキストを入力して "Generate TTS" ボタン

## 必要なファイル

- `ja_JP-test-medium.onnx` - 日本語TTSモデル
- `ja_JP-test-medium.onnx.json` - モデル設定ファイル

これらは `/Users/s19447/Desktop/total-piper/piper/test/models/` から取得できます。

## 注意事項

- 現在の実装は簡易版で、実際のONNX推論はまだ実装されていません
- 音素化は簡易的なマッピングで行っています（実際にはOpenJTalkが必要）
- Phase 1 以降で本格的な実装を行います