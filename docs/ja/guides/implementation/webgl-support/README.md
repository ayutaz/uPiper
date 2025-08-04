# WebGL対応実装ドキュメント

このディレクトリには、uPiperのWebGL対応に関するドキュメントが含まれています。

## ドキュメント一覧

### 📋 [investigation-and-plan.md](./investigation-and-plan.md)
WebGL対応の調査結果と実装計画書（進捗状況付き）

- piper-plusのWeb実装調査
- 実装計画（4フェーズ）
- 現在の進捗状況
- 技術的考慮事項

## 現在の状況（2025-08-04）

### ✅ 実装済み
- WebGL用JavaScript interopファイル（.jslib）
- WebGL音素化クラス（WebGLOpenJTalkPhonemizer等）
- InferenceEngineDemoのWebGL対応
- IndexedDBキャッシュ基本実装
- Unity EditorでのWebGLプラットフォーム対応

### ⚠️ プレースホルダー実装
- `openjtalk_wrapper.jslib` - 固定値を返す
- `espeak_wrapper.jslib` - 空配列を返す

### ❌ 未実装
- wasm_open_jtalkの実際の統合
- eSpeak-ng WebAssemblyの統合
- WebGL専用デモシーン
- CI/CDパイプライン
- GitHub Pages自動デプロイ

## WebGLビルド手順

1. **プラットフォーム切り替え**
   ```
   File > Build Settings > WebGL > Switch Platform
   ```

2. **シーン選択**
   - InferenceEngineDemoシーンを選択

3. **ビルド実行**
   ```
   Build Settings > Build
   ```

4. **ローカルテスト**
   ```bash
   # ビルドディレクトリで
   python -m http.server 8000
   ```

## 注意事項

⚠️ **現在の実装はプレースホルダーです**
- 日本語音素化は常に「こんにちは」の音素を返します
- 実際のWebAssembly統合が必要です

## 今後の作業

1. wasm_open_jtalkの統合（最優先）
2. GitHub ActionsでのWebGLビルド自動化
3. パフォーマンステストとドキュメント整備

詳細は[investigation-and-plan.md](./investigation-and-plan.md)を参照してください。