# CI/CD ドキュメント

## 概要

uPiperプロジェクトでは、GitHub Actionsを使用して継続的インテグレーション（CI）と継続的デリバリー（CD）を実現しています。

## ワークフロー一覧

### 1. build-openjtalk-native.yml
**目的**: OpenJTalkネイティブライブラリのマルチプラットフォームビルドと検証

**特徴**:
- Windows: Dockerを使用したクロスコンパイル（Ubuntu上でMinGW-w64使用）
- Linux: ネイティブビルド（x86_64）
- macOS: Universal Binary（Intel + Apple Silicon）
- ビルド成果物の自動検証
- リリース時の自動パッケージング

**トリガー**:
- main/developブランチへのプッシュ
- Pull Request
- 手動実行（workflow_dispatch）

### 2. native-tests.yml
**目的**: OpenJTalkネイティブライブラリのテスト実行と互換性マトリクス生成

**特徴**:
- 全プラットフォームでのCTest実行
- パフォーマンスレポート生成
- 互換性マトリクスの自動生成

### 3. unity-build.yml
**目的**: Unityプロジェクトのマルチプラットフォームビルド

**特徴**:
- Windows/Linux/macOS向けビルド
- Unity 6000.0.35f1使用
- 自動リリース作成（タグプッシュ時）

### 4. unity-tests.yml
**目的**: UnityのEdit Mode/Play Modeテスト実行

**特徴**:
- コードカバレッジ測定（Codecov連携）
- Windows/Linux/macOS環境でのテスト
- テスト結果のアップロード

### 5. locale-tests.yml
**目的**: 異なるロケール環境でのテスト（特に日本語環境）

**特徴**:
- 日本語Windows環境でのテスト
- CultureInfo関連の問題検出
- 数値フォーマット互換性確認

### 6. performance-regression.yml
**目的**: パフォーマンス回帰テスト

**特徴**:
- OpenJTalkの処理時間測定
- 100ms以下の基準確認
- ベンチマーク結果の記録

### 7. dotnet-format.yml
**目的**: C#コードフォーマットチェック

**特徴**:
- 段階的な有効化（現在: whitespace_only）
- PR時は変更ファイルのみチェック
- .editorconfig準拠

## ブランチ戦略

- **main**: 安定版リリース
- **develop**: 開発版統合
- **feature/**: 機能開発ブランチ

## リリースプロセス

1. タグをプッシュ（例: `v0.1.0`）
2. unity-build.ymlが自動的にビルドを開始
3. 全プラットフォームのビルドが完了後、GitHubリリースを作成
4. ビルド成果物が自動的にリリースに添付

## セキュリティ

- シークレット管理: GitHub Secretsを使用
- 権限: 最小権限の原則に従う
- 依存関係: Dependabotによる自動更新

## トラブルシューティング

### OpenJTalkビルドエラー
- Dockerイメージのビルドログを確認
- クロスコンパイル環境の依存関係を確認

### Unityビルドエラー
- Unity Licenseの有効性を確認
- プラットフォーム固有の設定を確認

### テスト失敗
- ロケール依存のテストは環境設定を確認
- タイムアウトの場合は処理時間を確認