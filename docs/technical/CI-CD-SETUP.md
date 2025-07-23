# CI/CD Setup Guide

## 概要

このドキュメントは、uPiperプロジェクトのCI/CDパイプラインの設定方法を説明します。

## GitHub Actions ワークフロー

### 1. Unity Build Matrix (PR Quality Check)
**ファイル**: `.github/workflows/unity-build-matrix.yml`

PR時に自動実行され、MonoとIL2CPP両方のビルドを検証します。

**特徴**:
- PR時に自動実行
- 変更ファイルに応じてビルドをスキップ（効率化）
- 8つのビルド構成（Windows/Mac/Linux × Mono/IL2CPP + Android/WebGL）
- PRへの自動コメントでビルド結果を報告
- 並列実行で高速化

### 2. Unity Build Verification (Mono & IL2CPP)
**ファイル**: `.github/workflows/unity-il2cpp-build.yml`

より詳細なビルド検証とパフォーマンステストを実行します。

### 3. Unity Tests
**ファイル**: `.github/workflows/unity-tests.yml`

ユニットテストとパフォーマンステストを実行します。

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

## トラブルシューティング

### ビルドが失敗する場合

1. **ライセンスエラー**
   ```
   Unity activation failed
   ```
   → Secrets に UNITY_LICENSE, UNITY_EMAIL, UNITY_PASSWORD を設定

2. **ディスク容量不足**
   ```
   No space left on device
   ```
   → free-disk-space アクションが適用されているか確認

3. **タイムアウト**
   ```
   The job was cancelled because it reached the timeout
   ```
   → timeout-minutes を調整（現在は60分）

### PRコメントが表示されない

- リポジトリの Settings → Actions → General で以下を確認：
  - Workflow permissions: Read and write permissions
  - Allow GitHub Actions to create and approve pull requests: ✅

## メンテナンス

### Unity バージョンアップ時

1. すべてのワークフローファイルで `unityVersion` を更新
2. `UnityBuilderAction.cs` の互換性を確認
3. テストビルドを実行

### 新しいプラットフォーム追加時

1. `unity-build-matrix.yml` のmatrixに追加
2. `UnityBuilderAction.cs` にプラットフォーム設定を追加
3. ビルド検証を実行

## セキュリティ

### Secrets の管理

必要なSecrets:
- `UNITY_LICENSE`: Unity Pro/Plus ライセンス
- `UNITY_EMAIL`: Unity アカウントメール
- `UNITY_PASSWORD`: Unity アカウントパスワード

### 権限の最小化

- ワークフローは必要最小限の権限で実行
- PRからの実行時は読み取り専用権限
- Secretsはフォークからアクセス不可