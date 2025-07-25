# uPiper Documentation

## 概要

このディレクトリには、uPiper（Unity Piper TTS Plugin）のドキュメントが含まれています。

## ドキュメント構成

### 📋 プロジェクト管理
- [ROADMAP.md](./ROADMAP.md) - 実装ロードマップとフェーズ管理
- [ARCHITECTURE.md](./ARCHITECTURE.md) - システムアーキテクチャ設計

### 📚 ガイド
- [guides/](./guides/) - 開発者向けガイド集
  - [ci-cd/](./guides/ci-cd/) - CI/CD設定とワークフロー
    - `README.md` - CI/CD総合ガイド
    - `il2cpp-solutions.md` - IL2CPP特有のCI/CDソリューション
  - [technical/](./guides/technical/) - 技術仕様
    - `README.md` - 技術ドキュメントの概要
    - `il2cpp.md` - IL2CPPサポート総合ガイド
    - `il2cpp-compatibility.md` - IL2CPP互換性レポート
    - `gpu-inference.md` - GPU推論ガイド
  - [implementation/](./guides/implementation/) - 実装ガイド
    - [android/](./guides/implementation/android/) - Android固有の実装
      - `implementation-guide.md` - Android実装ガイド
      - `build-settings.md` - Androidビルド設定
      - `technical-report.md` - Android技術調査報告
      - `performance-optimization.md` - Androidパフォーマンス最適化
    - [phase-completion-reports/](./guides/implementation/phase-completion-reports/) - フェーズ完了報告
      - `phase2-1-completion.md` - Phase 2.1完了報告
      - `phase2-2-android-native-build-completion.md` - Phase 2.2完了報告

### 📦 アーカイブ
- [archives/](./archives/) - 過去のドキュメント（参考用）
  - `phase2-progress.md` - Phase 2進捗管理（完了済み）

### 🎮 サンプルドキュメント
- **Streaming TTS**: リアルタイムストリーミング音声生成
- **Multi-Voice TTS**: 複数音声の同時処理
- **Realtime TTS**: 低レイテンシ音声生成

各サンプルのREADMEは`Assets/uPiper/Samples~/`以下の各ディレクトリに配置されています。

## クイックスタート

### 新規開発者向け
1. [ARCHITECTURE.md](./ARCHITECTURE.md)でシステム全体像を把握
2. [guides/technical/](./guides/technical/)で技術仕様を確認
3. [guides/ci-cd/](./guides/ci-cd/)でCI/CD環境をセットアップ

### IL2CPP対応作業
1. [guides/technical/il2cpp.md](./guides/technical/il2cpp.md)で設定方法を確認
2. [guides/technical/il2cpp-compatibility.md](./guides/technical/il2cpp-compatibility.md)で既知の問題を確認
3. [guides/ci-cd/il2cpp-solutions.md](./guides/ci-cd/il2cpp-solutions.md)でCI/CD特有の対応を確認

### Android開発
1. [guides/implementation/android/](./guides/implementation/android/)のドキュメントを順に確認
2. ビルド設定、実装ガイド、最適化の順に進める

## 関連リポジトリ

- [piper](https://github.com/rhasspy/piper) - Piper TTSコアプロジェクト
- [piper/docs/unity](https://github.com/rhasspy/piper/tree/master/docs/unity) - 一般的なUnity統合ドキュメント

## ドキュメント管理方針

1. **uPiper固有の情報**はこのディレクトリで管理
2. **一般的なUnity統合情報**はpiperリポジトリで管理
3. **技術仕様・実装詳細**はuPiperで管理
4. **ドキュメントは実装後に作成**（コードが正）

## 貢献方法

ドキュメントの改善や追加は歓迎します。PRを作成する際は：
1. 適切なディレクトリに配置
2. 関連するREADME.mdのインデックスを更新
3. Markdownフォーマットに従う
4. 実装に基づいた正確な情報を記載