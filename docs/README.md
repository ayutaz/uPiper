# uPiper Documentation

## ドキュメント構造

### platforms/ - プラットフォーム別実装
- **android/** - Android実装ガイド
  - build-settings.md - ビルド設定
  - implementation-guide.md - 実装ガイド
  - performance-optimization.md - パフォーマンス最適化
  - technical-report.md - 技術レポート

### features/ - 機能別ドキュメント
- **phonemization/** - 音素化システム
  - README.md - 音素化システム概要（技術仕様・実装・ライセンス評価）
- **gpu/** - GPU推論
  - gpu-inference.md - GPU推論ガイド

### development/ - 開発者向け
- CI_CD_GUIDE.md - CI/CDガイド
- additional-language-support.md - 追加言語サポート
- test-execution-guide.md - テスト実行ガイド

### ルートドキュメント
- ARCHITECTURE_ja.md - アーキテクチャ（日本語）
- ARCHITECTURE_en.md - アーキテクチャ（英語）
- DEVELOPMENT_LOG.md - 開発ログ
- setup-guide.md - セットアップガイド
- webgl-architecture.md - WebGLアーキテクチャ
- webgl-research.md - WebGL調査

## ドキュメントの探し方

### プラットフォーム対応を調べたい
→ `platforms/` フォルダを確認

### 特定機能の実装詳細を知りたい
→ `features/` フォルダを確認

### 開発・ビルド方法を知りたい
→ `development/` フォルダを確認

### 全体アーキテクチャを理解したい
→ `ARCHITECTURE_ja.md` または `ARCHITECTURE_en.md` を確認

## ドキュメント更新ガイドライン

1. **言語**: 基本的に日本語で記述（必要に応じて英語版も作成）
2. **配置**: トピックに応じて適切なフォルダに配置
3. **命名**: わかりやすく一貫性のあるファイル名を使用
4. **更新**: 実装変更時は関連ドキュメントも更新

## 関連リンク

- [メインREADME](../README.md)
- [GitHub リポジトリ](https://github.com/ayutaz/uPiper)
- [piper-plus](https://github.com/ayutaz/piper-plus)