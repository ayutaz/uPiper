# uPiper CI/CD・テスト改善レポート - 2026年3月版

## 現在のテスト状況

### テスト統計（Phase 5完了時点）
- テストファイル数: 51個（既存45 + Phase 5で6個追加）
- テスト数: 685個（既存455 + Phase 5で230個追加）
- Phase 5テストは全てEditMode（`Tests/Editor/Phonemizers/`配置）
- 純粋C#音素化バックエンドのためPlayModeテスト不要

### Phase 5 追加テストファイル

| ファイル | テスト数 | 内容 |
|---------|---------|------|
| `SpanishPhonemizerTests.cs` | 36 | スペイン語G2P（ストレス、二重字、異音規則、seseo、Prosody） |
| `FrenchPhonemizerTests.cs` | 38 | フランス語G2P（鼻母音、e muet、-er/-ille例外、uvular r） |
| `PortuguesePhonemizerTests.cs` | 41 | ポルトガル語G2P（鼻母音、後処理4規則、coda-l母音化、t/d口蓋化） |
| `ChinesePhonemizerTests.cs` | 38 | 中国語G2P（ピンイン→IPA、声調、声調変化、PUAマッピング） |
| `KoreanPhonemizerTests.cs` | 48 | 韓国語G2P（ハングル分解、IPA表、音韻規則4種、PUAマッピング） |
| `MultilingualPhonemizerPhase5Tests.cs` | 29 | 多言語統合（言語ルーティング、混合テキスト、CJK曖昧性解消） |

### CI実行時の注意点
- Phase 5テストはOneTimeSetUp/OneTimeTearDownパターンを使用（バックエンド初期化を1回に集約）
- ChinesePhonemizerTests: 環境依存でskipされる場合あり（`Assert.Ignore`使用）
- KoreanPhonemizerTests: 同上
- MCPテストランナー使用時はクラス単位でのフィルタ実行を推奨（タイムアウト回避）

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
- カバレッジ対象にPhase 5バックエンドを追加:
  - `Assets/uPiper/Runtime/Core/Phonemizers/Backend/Spanish/`
  - `Assets/uPiper/Runtime/Core/Phonemizers/Backend/French/`
  - `Assets/uPiper/Runtime/Core/Phonemizers/Backend/Portuguese/`
  - `Assets/uPiper/Runtime/Core/Phonemizers/Backend/Chinese/`
  - `Assets/uPiper/Runtime/Core/Phonemizers/Backend/Korean/`
- **難易度**: 低 / **インパクト**: 高

## テスト戦略改善

### テストピラミッド実装
- Unit: 毎コミット（`-testCategory Unit`）
  - Phase 5の言語別バックエンドテストはUnitカテゴリに該当
- Integration: PR時のみ
  - MultilingualPhonemizerPhase5TestsはIntegrationカテゴリに該当
- E2E: 夜間実行
- **難易度**: 中 / **インパクト**: 高

### テストタイムアウト改善
- `[SetUpFixture]`で辞書を一度だけロード
- モックデータ活用
- Phase 5テストはOneTimeSetUpパターンを既に採用済み
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
