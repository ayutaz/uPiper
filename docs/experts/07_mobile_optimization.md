# uPiper モバイル最適化レポート - 2026年3月版

## 最優先: モデル量子化（INT8/FP16）
- ONNX Runtime Quantizationでモデルサイズ50-75%削減
- モバイルGPU（Mali, Adreno, Apple GPU）はFP16演算がFP32の2倍スループット
- バッテリー消費30-50%削減
- **難易度**: 中 / **インパクト**: 高

## アプリサイズ削減
- **Play Asset Delivery（Android）**: Fast-followでインストール直後自動ダウンロード
- **On-Demand Resources（iOS）**: App Storeホスティング、自動削除
- インストールコンバージョン率10-15%向上
- **難易度**: 中 / **インパクト**: 高

## モバイルGPU推論
- iOS Metal問題: CPU強制フォールバック中→Core MLバックエンド検討
- Android: TensorFlow Lite + NNAPI DelegateでNPU/DSP活用
- 推論速度2-5倍、バッテリー消費50%削減
- **難易度**: 高 / **インパクト**: 高

## メモリ管理
- `UnloadModel()`メソッド追加、アイドル30秒後に自動アンロード
- テンソルプーリングでアロケーション/デアロケーション最小化
- **難易度**: 中 / **インパクト**: 中-高

## バックグラウンド処理
- iOS: `AVAudioSession.setCategory(.playback, mode: .spokenAudio)`
- Android: Foreground Service（通知表示必須）
- **難易度**: 中 / **インパクト**: 中

## プラットフォーム固有TTS連携
- 高品質音声: uPiperニューラルTTS
- 汎用読み上げ: ネイティブTTS（AVSpeechSynthesizer / Android TTS）
- フォールバック機能（メモリ不足時等に自動切り替え）
- **難易度**: 中 / **インパクト**: 中

## ロードマップ

### Phase 1（短期）
1. モデル量子化（INT8）
2. Play Asset Delivery / On-Demand Resources
3. 動的モデルアンロード

### Phase 2（中期）
4. Android: TensorFlow Lite + NNAPI
5. iOS: Core MLバックエンド
6. バックグラウンド音声再生

### Phase 3（長期）
7. 増分重みローディング
8. ネイティブTTSハイブリッドモード

## Sources

- [Quantize ONNX models | onnxruntime](https://onnxruntime.ai/docs/performance/model-optimizations/quantization.html)
- [Play Asset Delivery - Android Developers](https://developer.android.com/guide/playcore/asset-delivery)
- [Core ML vs TensorFlow Lite](https://www.netguru.com/blog/coreml-vs-tensorflow-lite-mobile)
