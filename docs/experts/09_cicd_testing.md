# uPiper CI/CD・テスト改善レポート - 2026年3月版

## 即座に実装可能（設定のみ）

### 1. Dependabot有効化
```yaml
# .github/dependabot.yml
version: 2
updates:
  - package-ecosystem: "github-actions"
    directory: "/"
    schedule:
      interval: "weekly"
  - package-ecosystem: "nuget"
    directory: "/"
    schedule:
      interval: "weekly"
```
- **難易度**: 低 / **インパクト**: 高

### 2. OpenUPM公開
- openupm/openupmにPR作成
- `com.ayutaz.upiper`として登録
- **難易度**: 低 / **インパクト**: 高

### 3. テストカバレッジ品質ゲート
- 最低カバレッジ閾値70%
- PRで追加されたコードは80%以上
- **難易度**: 低 / **インパクト**: 高

## テスト戦略改善

### テストピラミッド実装
- Unit: 毎コミット（`-testCategory Unit`）
- Integration: PR時のみ
- E2E: 夜間実行
- **難易度**: 中 / **インパクト**: 高

### テストタイムアウト改善
- `[SetUpFixture]`で辞書を一度だけロード
- モックデータ活用
- **難易度**: 中 / **インパクト**: 高

## リリース自動化

### semantic-release導入
- Conventional Commitsから自動バージョン決定
- CHANGELOG自動生成、Gitタグ自動作成
- **難易度**: 中 / **インパクト**: 高

## パフォーマンステスト

### Unity Performance Testing Package
- Profilerマーカー/カスタムメトリクスの測定
- ベースライン比較で回帰検出
- **難易度**: 中 / **インパクト**: 高

## WebGL E2Eテスト
- Puppeteer/Playwrightでヘッドレスブラウザテスト
- デプロイ検証の自動化
- **難易度**: 中 / **インパクト**: 高

## セキュリティスキャン
- OWASP Dependency-Check統合
- ライセンス監査（FOSSA/Mend）
- **難易度**: 中 / **インパクト**: 高

## ドキュメント自動生成
- DocFX統合でAPI Reference自動生成
- GitHub Pagesへの自動デプロイ
- **難易度**: 中 / **インパクト**: 高

## 依存パッケージ管理改善
- dot-net-g2pをOpenUPM/GitHub Package Registryでホスティング
- パス書き換え不要に
- **難易度**: 中 / **インパクト**: 高

## Sources

- [GameCI Documentation](https://game.ci/docs/github/getting-started/)
- [semantic-release](https://github.com/semantic-release/semantic-release)
- [OpenUPM - Adding UPM Package](https://openupm.com/docs/adding-upm-package.html)
- [Unity Performance Testing](https://docs.unity3d.com/Packages/com.unity.test-framework.performance@1.0/manual/index.html)
