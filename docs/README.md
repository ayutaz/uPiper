# uPiper Documentation

## 📚 ドキュメント構造

### 🎯 platforms/ - プラットフォーム別実装
- **android/** - Android実装ガイド
  - build-settings.md - ビルド設定
  - implementation-guide.md - 実装ガイド
  - performance-optimization.md - パフォーマンス最適化
  - technical-report.md - 技術レポート
- **ios/** - iOS実装（Phase 5）
  - phase5-ios-detailed-implementation-plan.md - 詳細実装計画
  - phase5-ios-implementation-plan.md - 実装計画
  - phase5-ios-technical-research.md - 技術調査
- **webgl/** - WebGL対応
  - webgl-investigation.md - 技術調査報告書

### 🚀 features/ - 機能別ドキュメント
- **phonemization/** - 音素化システム
  - implementation-summary.md - 実装サマリー
  - license-evaluation-report.md - ライセンス評価
  - technical-specification.md - 技術仕様
- **chinese-support/** - 中国語サポート
  - current-state-analysis.md - 現状分析
  - implementation-plan.md - 実装計画
  - phase1-implementation-report.md - Phase1実装レポート
  - technical-research.md - 技術調査
- **gpu/** - GPU推論
  - gpu-inference.md - GPU推論ガイド
- asian-language-support.md - アジア言語サポート概要

### 🔧 development/ - 開発者向け
- **il2cpp/** - IL2CPP関連
  - il2cpp.md - IL2CPP基本ガイド
  - il2cpp-compatibility.md - 互換性ガイド
  - il2cpp-solutions.md - ソリューション集
- **setup/** - セットアップガイド
  - additional-language-support.md - 追加言語サポート
  - cmu-dictionary-setup.md - CMU辞書セットアップ
  - flite-build-guide.md - Fliteビルドガイド
- **usage/** - 使用ガイド
  - phonemization-guide.md - 音素化ガイド
  - test-execution-guide.md - テスト実行ガイド
- **ci-cd/** - CI/CD
  - README.md - CI/CDガイド

### 📋 ルートドキュメント
- ARCHITECTURE_ja.md - アーキテクチャ（日本語）
- ARCHITECTURE_en.md - アーキテクチャ（英語）
- DEVELOPMENT_LOG.md - 開発ログ

### 📦 archives/ - アーカイブ
旧言語別構造（ja/en）のバックアップ

## 🔍 ドキュメントの探し方

### プラットフォーム対応を調べたい
→ `platforms/` フォルダを確認

### 特定機能の実装詳細を知りたい
→ `features/` フォルダを確認

### 開発・ビルド方法を知りたい
→ `development/` フォルダを確認

### 全体アーキテクチャを理解したい
→ `ARCHITECTURE_ja.md` または `ARCHITECTURE_en.md` を確認

## 📝 ドキュメント更新ガイドライン

1. **言語**: 基本的に日本語で記述（必要に応じて英語版も作成）
2. **配置**: トピックに応じて適切なフォルダに配置
3. **命名**: わかりやすく一貫性のあるファイル名を使用
4. **更新**: 実装変更時は関連ドキュメントも更新

## 🔗 関連リンク

- [メインREADME](../README.md)
- [GitHub リポジトリ](https://github.com/ayutaz/uPiper)
- [piper-plus](https://github.com/ayutaz/piper-plus)