# CI/CD Guide

## 概要

uPiperプロジェクトでは、GitHub Actionsを使用して継続的インテグレーション（CI）と継続的デリバリー（CD）を実現しています。このガイドでは、CI/CDパイプラインの設定方法と運用について説明します。

## ワークフロー一覧

### 1. build-openjtalk-native.yml
**目的**: OpenJTalkネイティブライブラリのマルチプラットフォームビルドと検証

**特徴**:
- Windows: Dockerを使用したクロスコンパイル（Ubuntu上でMinGW-w64使用）
- Linux: ネイティブビルド（x86_64）
- macOS: Universal Binary（Intel + Apple Silicon）
- Android: NDKクロスコンパイル（arm64-v8a, armeabi-v7a, x86, x86_64）
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
- Mono2x/IL2CPPバックエンド対応
- 自動リリース作成（タグプッシュ時）

### 4. unity-build-matrix.yml (PR Quality Check)
**目的**: PR時の包括的なビルド検証

**特徴**:
- PR時に自動実行
- 変更ファイルに応じてビルドをスキップ（効率化）
- MonoとIL2CPP両方のビルド構成を検証
- PRへの自動コメントでビルド結果を報告
- 並列実行で高速化

### 5. unity-tests.yml
**目的**: UnityのEdit Mode/Play Modeテスト実行

**特徴**:
- コードカバレッジ測定（Codecov連携）
- Windows/Linux/macOS環境でのテスト
- テスト結果のアップロード

### 6. locale-tests.yml
**目的**: 異なるロケール環境でのテスト（特に日本語環境）

**特徴**:
- 日本語Windows環境でのテスト
- CultureInfo関連の問題検出
- 数値フォーマット互換性確認

### 7. performance-regression.yml
**目的**: パフォーマンス回帰テスト

**特徴**:
- OpenJTalkの処理時間測定
- 100ms以下の基準確認
- ベンチマーク結果の記録

### 8. dotnet-format.yml
**目的**: C#コードフォーマットチェック

**特徴**:
- 段階的な有効化（現在: whitespace_only）
- PR時は変更ファイルのみチェック
- .editorconfig準拠

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

### OpenJTalkビルドエラー
- Dockerイメージのビルドログを確認
- クロスコンパイル環境の依存関係を確認

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

### Android固有の問題
- NDKバージョンの互換性を確認
- ターゲットAPIレベルを確認

## 関連ドキュメント

- [IL2CPP CI/CDソリューション](il2cpp-solutions.md) - IL2CPP固有のCI/CD設定
- [技術ドキュメント](../technical/) - 詳細な技術仕様