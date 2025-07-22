# uPiper Documentation

## 概要

このディレクトリには、uPiper（Unity Piper TTS Plugin）のドキュメントが含まれています。

## ドキュメント構成

### 📋 プロジェクト管理
- [ROADMAP.md](./ROADMAP.md) - 実装ロードマップとフェーズ管理
- [progress/](./progress/) - 進捗管理ドキュメント
  - `phase1-progress.md` - Phase 1の詳細進捗
  - `unity-task-checklist.md` - タスクチェックリスト

### 🏗️ アーキテクチャ
- [ARCHITECTURE.md](./ARCHITECTURE.md) - システムアーキテクチャ設計
- [technical/](./technical/) - 技術仕様書
  - `IL2CPP.md` - IL2CPPサポートガイド

### 🔧 インフラ・運用
- [CI-CD.md](./CI-CD.md) - GitHub Actionsワークフローとビルドプロセス

### 📝 実装ガイド
- [implementation/](./implementation/) - 実装関連ドキュメント
  - `phase1-implementation-guide-ja.md` - Phase 1実装ガイド
  - `phase1-kickoff.md` - Phase 1キックオフ
  - `phase2-android-implementation-guide-ja.md` - Phase 2 Android実装ガイド
  - `phase2-android-technical-report.md` - Phase 2 Android技術調査報告
  - `phase2-android-build-settings.md` - Android ビルド設定ガイド
  - `completion-reports/` - 各フェーズの完了報告書

### 📊 計画
- [planning/](./planning/) - 計画ドキュメント

## 関連リポジトリ

- [piper](https://github.com/rhasspy/piper) - Piper TTSコアプロジェクト
- [piper/docs/unity](https://github.com/rhasspy/piper/tree/master/docs/unity) - 一般的なUnity統合ドキュメント

## ドキュメント管理方針

1. **uPiper固有の情報**はこのディレクトリで管理
2. **一般的なUnity統合情報**はpiperリポジトリで管理
3. **進捗・タスク管理**はuPiperで一元管理
4. **技術仕様・実装詳細**はuPiperで管理

## 貢献方法

ドキュメントの改善や追加は歓迎します。PRを作成する際は：
1. 適切なディレクトリに配置
2. README.mdのインデックスを更新
3. Markdownフォーマットに従う