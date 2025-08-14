# IL2CPP CI/CDソリューション

## 現状の制限

Unity BuilderのDockerベースのアプローチでは、以下の制限があります：

| プラットフォーム | Mono | IL2CPP | 理由 |
|-----------------|------|--------|------|
| Linux | ✅ | ✅ | Dockerイメージに全ツール含まれる |
| Windows | ✅ | ❌ | Visual Studio必要 |
| macOS | ✅ | ❌ | Xcode必要 |

## 解決策

### 1. プラットフォーム別GitHub Actionsランナー

**メリット**:
- 完全なIL2CPPサポート
- ネイティブツールチェーンへのアクセス

**デメリット**:
- 追加のUnityライセンスが必要（プラットフォームごと）
- ビルド時間が長い
- コストが高い

**実装例**: `.github/workflows/unity-il2cpp-native.yml`参照

### 2. Unity Cloud Build

**メリット**:
- 設定が簡単
- 全プラットフォーム対応
- Unityが管理

**デメリット**:
- 有料サービス
- ビルド時間制限あり

**設定方法**:
1. Unity Dashboardでプロジェクトを作成
2. GitHubリポジトリを連携
3. ビルド設定でIL2CPPを選択

### 3. セルフホストランナー

**メリット**:
- 完全なコントロール
- 高速ビルド（キャッシュ活用）
- コスト効率的（長期的に）

**デメリット**:
- 初期セットアップが複雑
- メンテナンスが必要
- セキュリティ考慮事項

**セットアップ手順**:
```bash
# Windows
1. Unity Hub + IL2CPPモジュールをインストール
2. Visual Studio 2019/2022をインストール
3. GitHub Actions runnerを設定

# macOS  
1. Unity Hub + IL2CPPモジュールをインストール
2. Xcodeをインストール
3. GitHub Actions runnerを設定
```

### 4. ハイブリッドアプローチ（推奨）

開発フェーズに応じて使い分け：

1. **開発中**: 
   - PR: Monoビルドのみ（高速）
   - Linux IL2CPPで基本動作確認

2. **リリース前**:
   - 手動トリガーでネイティブランナー使用
   - 全プラットフォームIL2CPPビルド

3. **本番リリース**:
   - Unity Cloud Buildで最終ビルド
   - または検証済みセルフホストランナー

## コスト比較

| ソリューション | 初期コスト | 運用コスト | ビルド時間 |
|--------------|-----------|-----------|-----------|
| Dockerのみ | 低 | 低 | 速い（Monoのみ） |
| GitHub Actions | 中 | 高 | 遅い |
| Unity Cloud | 低 | 中 | 中 |
| セルフホスト | 高 | 低 | 速い |

## 推奨事項

1. **小規模プロジェクト**: DockerでLinux IL2CPPのみ
2. **中規模プロジェクト**: Unity Cloud Build
3. **大規模プロジェクト**: セルフホストランナー
4. **オープンソース**: ハイブリッドアプローチ

## 実装状況

- ✅ Linux IL2CPP（Docker）- CIで自動実行
- ✅ 全プラットフォームMono - CIで自動実行
- ❌ Windows/macOS IL2CPP - Dockerでは実行不可
- 📝 代替案: Unity Cloud Build、セルフホストランナー（ドキュメント化済み）