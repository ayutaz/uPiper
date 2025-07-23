# uPiper Documentation

## 概要

このディレクトリには、uPiper（Unity Piper TTS Plugin）のドキュメントが含まれています。

## ドキュメント構成

### 📋 プロジェクト管理
- [ROADMAP.md](./ROADMAP.md) - 実装ロードマップとフェーズ管理
- [progress/](./progress/) - 進捗管理ドキュメント
  - `PHASE1-FINAL-COMPLETION.md` - **Phase 1最終完了報告書** 🎉
  - `phase1-progress.md` - Phase 1の詳細進捗
  - `phase1-12-completion-report.md` - IL2CPPサポート完了報告
  - `phase1-13-completion-report.md` - GPU推論サポート完了報告
  - `unity-task-checklist.md` - タスクチェックリスト

### 🏗️ アーキテクチャ
- [ARCHITECTURE.md](./ARCHITECTURE.md) - システムアーキテクチャ設計
- [technical/](./technical/) - 技術仕様書
  - `GPU-INFERENCE-GUIDE.md` - GPU推論ガイド
  - `IL2CPP-COMPATIBILITY.md` - IL2CPP互換性ガイド
  - `IL2CPP-BUILD-SETTINGS.md` - IL2CPPビルド設定
  - `IL2CPP-PERFORMANCE-REPORT.md` - IL2CPPパフォーマンスレポート

### 🔧 インフラ・運用
- [CI-CD.md](./CI-CD.md) - GitHub Actionsワークフローとビルドプロセス
- [ci-cd/](./ci-cd/) - CI/CD詳細ドキュメント
  - `CI-CD-SETUP.md` - CI/CDセットアップガイド
  - `IL2CPP-CI-SOLUTIONS.md` - IL2CPP CI/CDソリューション

### 📝 実装ガイド
- [implementation/](./implementation/) - 実装関連ドキュメント（Phase 2以降）
- [archive/phase1/](./archive/phase1/) - Phase 1開発ドキュメント（アーカイブ）

### 🎮 サンプルドキュメント
- **Streaming TTS**: リアルタイムストリーミング音声生成
- **Multi-Voice TTS**: 複数音声の同時処理
- **Realtime TTS**: 低レイテンシ音声生成

各サンプルのREADMEは`Assets/uPiper/Samples~/`以下の各ディレクトリに配置されています。


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