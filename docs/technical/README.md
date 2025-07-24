# Technical Documentation

このディレクトリには、uPiperの技術的な詳細に関するドキュメントが含まれています。

## ドキュメント一覧

### 1. [IL2CPP.md](IL2CPP.md)
**IL2CPPサポート総合ガイド**
- IL2CPPの概要と利点・制約
- ビルド設定（自動/手動）
- プラットフォーム別の設定
- ネイティブライブラリの配置
- トラブルシューティング
- 最適化のヒント
- パフォーマンス期待値

### 2. [IL2CPP-COMPATIBILITY.md](IL2CPP-COMPATIBILITY.md)
**IL2CPP互換性レポート**
- P/Invoke実装の検証結果
- マーシャリング属性の分析
- AOT制約への対応状況
- Unity AI Inference Engineとの互換性
- CI/CD環境での制限事項
- 実際のコード分析結果

### 3. [CI-CD-SETUP.md](CI-CD-SETUP.md)
**CI/CD設定ガイド**
- GitHub Actionsワークフロー設定
- ブランチ保護ルール
- 自動テストの構成
- ビルドの最適化戦略
- リリース自動化

### 4. [GPU-INFERENCE-GUIDE.md](GPU-INFERENCE-GUIDE.md)
**GPU推論ガイド**
- バックエンドタイプの選択
- プラットフォーム別のGPU設定
- パフォーマンス最適化
- トラブルシューティング

## ドキュメントの使い方

### 新規開発者向け
1. まず[IL2CPP.md](IL2CPP.md)でIL2CPPの基本を理解
2. [CI-CD-SETUP.md](CI-CD-SETUP.md)で開発環境のセットアップ
3. [GPU-INFERENCE-GUIDE.md](GPU-INFERENCE-GUIDE.md)でパフォーマンス最適化

### IL2CPP対応作業
1. [IL2CPP.md](IL2CPP.md)で設定方法を確認
2. [IL2CPP-COMPATIBILITY.md](IL2CPP-COMPATIBILITY.md)で既知の問題を確認
3. ビルド設定は[IL2CPP.md](IL2CPP.md)の「ビルド手順」セクションを参照

### CI/CD設定
1. [CI-CD-SETUP.md](CI-CD-SETUP.md)でワークフローの全体像を把握
2. IL2CPP特有のCI制限は[IL2CPP-COMPATIBILITY.md](IL2CPP-COMPATIBILITY.md)を参照

## メンテナンス指針

### 更新頻度
- **IL2CPP.md**: Unity/uPiperの大きなアップデート時
- **IL2CPP-COMPATIBILITY.md**: 新しいプラットフォームテスト時、Unity更新時
- **CI-CD-SETUP.md**: ワークフロー変更時
- **GPU-INFERENCE-GUIDE.md**: Unity AI Inference Engine更新時

### ドキュメント作成ルール
1. 実装前に設計ドキュメントを作成しない（コードが正）
2. 実装後に実際の動作を基にドキュメント化
3. 具体的なコード例を含める
4. プラットフォーム固有の情報は明確に区別

## 関連リソース

- [Unity IL2CPP公式ドキュメント](https://docs.unity3d.com/Manual/IL2CPP.html)
- [Unity AI Inference Engine](https://docs.unity3d.com/Packages/com.unity.sentis@latest)
- [GitHub Actions](https://docs.github.com/en/actions)