# CI/CD Guide

## 概要

uPiperプロジェクトでは、GitHub Actionsを使用して継続的インテグレーション（CI）と継続的デリバリー（CD）を実現しています。このガイドでは、CI/CDパイプラインの設定方法と運用について説明します。

## ワークフロー一覧

### 1. unity-build.yml
**目的**: Unityプロジェクトのマルチプラットフォームビルド

**特徴**:
- Windows/Linux/macOS/WebGL向けビルド
- Unity 6000.0.58f2使用
- Mono2x/IL2CPPバックエンド対応
- 自動リリース作成（タグプッシュ時）

### 2. unity-build-matrix.yml (PR Quality Check)
**目的**: PR時の包括的なビルド検証

**特徴**:
- PR時に自動実行
- 変更ファイルに応じてビルドをスキップ（効率化）
- MonoとIL2CPP両方のビルド構成を検証
- PRへの自動コメントでビルド結果を報告
- 並列実行で高速化

### 3. unity-tests.yml
**目的**: Unity Test Runnerの実行

**特徴**:
- Linux/macOS: Dockerベース（game-ci/unity-test-runner）
- Windows: 現在無効化（Docker制限のため）
- PlayModeとEditModeテスト

### 4. dotnet-format.yml
**目的**: C#コードフォーマットチェック

**特徴**:
- dotnet formatによる自動チェック
- コードスタイル統一

### 5. deploy-webgl.yml
**目的**: WebGLビルドとGitHub Pagesへのデプロイ

**特徴**:
- feature/webgl-supportブランチへのpush時に自動実行
- workflow_dispatch（手動実行）対応
- dot-net-g2pサブリポジトリの自動checkout
- game-ci/unity-builder@v4によるWebGLビルド
- 100MB超ファイルの90MBチャンク自動分割（GitHub Pages制限対応）
- split-file-loader.js / github-pages-adapter.jsの自動注入
- GitHub Pagesへの自動デプロイ
- デプロイ後のHTTPステータス確認

## ブランチ保護ルールの設定

### 必須の設定手順

1. **GitHubリポジトリの Settings → Branches へ移動**

2. **Add rule をクリック**

3. **Branch name pattern**: `main` と入力

4. **以下の保護ルールを有効化**:

   ✅ **Require a pull request before merging**
   - ✅ Require approvals: 1
   - ✅ Dismiss stale pull request approvals when new commits are pushed

   ✅ **Require status checks to pass before merging**
   - ✅ Require branches to be up to date before merging
   
   **Required status checks** に以下を追加:
   - `Build Quality Gate`
   - `unity-tests / Test Results`
   - `Build Mono2x - StandaloneWindows64`
   - `Build IL2CPP - StandaloneWindows64`

   ✅ **Require conversation resolution before merging**

   ✅ **Do not allow bypassing the above settings**

5. **Create** をクリック

### オプション設定（推奨）

developブランチにも同様のルールを設定：
- Branch name pattern: `develop`
- 同じ保護ルールを適用（ただしapproval数は調整可能）

## ワークフローの品質保証

### PR作成時の自動チェック

1. **互換性チェック**
   - link.xmlの存在確認
   - IL2CPP関連ファイルの検証

2. **ビルドマトリックス**
   - 全プラットフォーム×バックエンドの組み合わせ
   - 並列実行による高速化

3. **品質ゲート**
   - すべてのビルドが成功しない限りマージ不可

### ビルド時間の最適化

- **キャッシュ活用**: Libraryフォルダをキャッシュ
- **条件付き実行**: 変更がない場合はスキップ
- **並列実行**: 最大8つのビルドを同時実行
- **ディスク容量管理**: IL2CPPビルド前に不要ファイル削除

## Unity License設定

### 必要なSecrets

1. **UNITY_LICENSE**: Unityライセンスファイルの内容
2. **UNITY_EMAIL**: Unityアカウントのメールアドレス
3. **UNITY_PASSWORD**: Unityアカウントのパスワード

### ライセンス取得方法

1. [Unity License Server](https://license.unity3d.com/)にアクセス
2. Unity Hub経由でライセンスをアクティベート
3. ライセンスファイル（.ulf）をbase64エンコード
4. GitHub SecretsにUNITY_LICENSEとして設定

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

### Unityビルドエラー

1. **ライセンスエラー**
   ```
   Unity activation failed
   ```
   → Secrets に UNITY_LICENSE, UNITY_EMAIL, UNITY_PASSWORD を設定

2. **ディスク容量不足**
   ```
   No space left on device
   ```
   → free-disk-spaceアクションを使用してクリーンアップ

3. **IL2CPPビルドエラー**
   - IL2CPP特有の問題については[IL2CPP CI/CDソリューション](il2cpp-solutions.md)を参照

### テスト失敗
- ロケール依存のテストは環境設定を確認
- タイムアウトの場合は処理時間を確認

## 関連ドキュメント

- [IL2CPP CI/CDソリューション](il2cpp-solutions.md) - IL2CPP固有のCI/CD設定
- [技術ドキュメント](../technical/) - 詳細な技術仕様