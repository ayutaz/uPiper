# フェーズ1.9-1.11: Unity.InferenceEngine統合と機能完成

## 概要

本PRはuPiper TTSの実装フェーズ1.9-1.11を完了し、Unity.InferenceEngine（Sentis v2.2.1）の完全統合とフェーズ1の残り機能を実装しています。

## 主な変更点

### フェーズ1.9: Sentis音声合成 ✅
- すべてのSentis APIをUnity.InferenceEngine v2.2.1へ更新
- テンソル型の変更を修正（TensorInt → Tensor<int>など）
- Worker APIを更新（Execute → SetInput + Schedule）
- モデルなしでのテスト用モックモードサポートを追加

### フェーズ1.10: プラットフォーム抽象化 ✅
- クロスプラットフォームネイティブライブラリ読み込み
- プラットフォーム検出とパス解決
- アーキテクチャ検出（x86/x64/ARM）

### フェーズ1.11: 統合とサンプル ✅
- 完全なTTSパイプライン統合
- サンプルシーンとコントローラー
- デバッグツールとエディタユーティリティ

### 追加改善
- **音声読み込みシステム**: 音声ごとのオーディオジェネレーターによる複数音声サポート
- **オーディオキャッシュシステム**: 生成音声の完全なLRUキャッシュ実装
- **モデルアセットサポート**: ファイルパスとUnity ModelAsset参照の両方に対応
- **ONNX統合ガイド**: Unity.InferenceEngineモデル使用法のドキュメント

## 破壊的変更
- Unity.InferenceEngine 2.2.1が必要（以前はSentis 1.4）
- ONNXモデルは.sentisアセットとしてインポートする必要があります

## テスト
- すべてのユニットテストをモックモードで更新し合格
- 統合テストに音声読み込みを含む
- プラットフォームテストはネイティブライブラリ読み込みを適切に処理

## ドキュメント
- モデル設定用のONNX_INTEGRATION_GUIDE.mdを追加
- 新しいAPIのインラインドキュメントを更新
- phase1-9-to-11-summary.mdにフェーズ完了の概要を記載

## 変更ファイルの概要

### コア実装
- `Assets/uPiper/Runtime/Core/AudioGeneration/SentisAudioGenerator.cs` - Unity.InferenceEngine API更新
- `Assets/uPiper/Runtime/Core/AudioGeneration/ModelLoader.cs` - モックモードサポート
- `Assets/uPiper/Runtime/Core/PiperTTS.cs` - 音声読み込みとキャッシュシステム
- `Assets/uPiper/Runtime/Core/PiperVoiceConfig.cs` - ModelAssetサポート

### テスト
- `Assets/uPiper/Tests/Runtime/Integration/*.cs` - すべてのテストに音声読み込みを追加
- `Assets/uPiper/Tests/Runtime/Platform/NativeLibraryLoaderTests.cs` - LogAssertの期待値

### ドキュメント
- `Assets/uPiper/Docs/ONNX_INTEGRATION_GUIDE.md` - 新しい統合ガイド
- `Assets/uPiper/Docs/phase1-9-to-11-summary.md` - フェーズ完了の概要

## 次のステップ
1. 実際のPiper ONNXモデルを.sentisアセットとしてインポート
2. 実際の音声モデルでテスト
3. パフォーマンスとメモリ使用量の最適化

Fixes #17 (Unity.InferenceEngine統合)

🤖 Generated with [Claude Code](https://claude.ai/code)

Co-Authored-By: Claude <noreply@anthropic.com>