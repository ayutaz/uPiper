# T-004: CI/CD パイプライン対応

- マイルストーン: M4
- Issue: #137
- 前提チケット: T-003
- 後続チケット: T-005
- ステータス: [ ] 未着手

---

## タスク目的とゴール

WebGLInput パッケージ追加後、既存の全CIパイプラインが正常に動作することを確認する。CI定義の変更は最小限（必要な場合のみ）とし、パッケージ追加による既存ワークフローへの影響がないことを保証する。

**ゴール**: 6つの全CIワークフローがグリーンであることを確認し、必要に応じてワークフロー定義を修正する。

---

## 実装する内容の詳細

### 基本方針

WebGLInputはpublicなGitリポジトリからのUPM Git URL参照（`https://github.com/kou-yeung/WebGLInput.git?path=Assets/WebGLSupport#1.4.5`）のため、CI環境での追加設定は不要なはず。dot-net-g2pのようなローカルパス書き換え（`file:../../` → `file:../`）は不要。確認のみをスコープとする。

### 確認対象ワークフロー

#### 1. unity-tests.yml — EditModeテスト

- EditModeテスト全件パス確認
- WebGLInputパッケージの追加がテスト実行に影響しないことを確認
- トリガー: branches: main, develop
- Unity: 6000.0.58f2

#### 2. unity-build.yml — マルチプラットフォームビルド

- Windows64, OSX, Linux64, WebGLの全プラットフォームビルド通過
- WebGLビルド（常にIL2CPP）でWebGLInputのコードが正しくstrip/コンパイルされることを確認

#### 3. deploy-webgl.yml — WebGLビルド+GitHub Pagesデプロイ

- WebGLビルド成功とGitHub Pagesデプロイ成功を確認
- 現在のトリガーはbranch: `feature/webgl-support` + `workflow_dispatch`

#### 4. unity-il2cpp-build.yml — IL2CPP互換性チェック

- IL2CPPビルドでWebGLInputのコードが正しく処理されることを確認
- リフレクション関連のstrip問題がないことを確認

#### 5. unity-build-matrix.yml — PRビルドマトリクス

- PRトリガーのビルドマトリクスが全パス

#### 6. dotnet-format.yml — C#コードフォーマット

- WebGLInputパッケージのコードがフォーマットチェック対象外であることを確認
- Packages配下はdotnet-formatの対象外であることが前提だが、CI設定次第で対象に含まれる可能性あり

### 潜在的な変更箇所

#### deploy-webgl.yml のトリガーブランチ

現在 `feature/webgl-support` のみがトリガー対象。以下のいずれかの対応が必要:
- `feature/webgl-japanese-input` ブランチをトリガーに追加
- `workflow_dispatch` での手動実行で対応（ブランチ追加不要）
- 実装ブランチ名が確定した時点で判断する

#### dotnet-format の除外設定

WebGLInputパッケージのコードがフォーマットチェック対象に含まれる場合、除外設定を追加する:
- `.editorconfig` での `Packages/` ディレクトリ除外
- または dotnet-format コマンドの `--exclude` オプション追加

#### Library キャッシュキー

CI上のLibraryキャッシュキーに `manifest.json` のハッシュが含まれているか確認する。パッケージ追加でキャッシュが正しく無効化され、新パッケージが解決されることを保証する。キャッシュキーに含まれていない場合、キャッシュクリアまたはキーの更新が必要。

---

## エージェントチームの役割と人数

| 役割 | 人数 | 担当内容 |
|------|------|----------|
| CI検証エージェント | 1名 | 各ワークフロー定義の確認、手動トリガーでのテスト実行、結果の記録 |
| 修正エージェント | 1名 | 必要に応じてワークフロー定義の修正（deploy-webgl.ymlトリガー、dotnet-format除外等） |

---

## 提供範囲とテスト項目

### 提供範囲

- CIワークフロー定義の変更（必要な場合のみ）
- CI実行結果の確認・記録

### テスト項目

#### ユニットテスト
- CI上でEditModeテスト全件パス（新規テスト追加なし）

#### E2Eテスト
- CI上でWebGLビルド成功

#### CI確認チェックリスト

- [ ] unity-tests.yml: EditModeテスト全件パス
- [ ] unity-build.yml: WebGLプラットフォームビルド成功
- [ ] unity-build-matrix.yml: PRビルドマトリクス全パス
- [ ] unity-il2cpp-build.yml: IL2CPP互換性チェック通過
- [ ] deploy-webgl.yml: WebGLビルド+デプロイ成功
- [ ] dotnet-format.yml: フォーマットチェック通過

---

## 懸念事項とレビュー項目

### 懸念事項

- **CI環境でのGitHub外部リポジトリへのアクセス**: WebGLInputリポジトリへのGit cloneがrate limitやネットワークエラーで失敗する可能性がある。パブリックリポジトリのため認証は不要だが、GitHub Actionsの同時実行数が多い場合にrate limitに抵触するリスクがある
- **game-ci Dockerイメージ内でのUPM Git URL解決**: game-ciのDockerイメージにgitクライアントが含まれていることが前提。dot-net-g2pのGit URL参照が既に動作しているため問題ないはず
- **WebGLInputパッケージのダウンロードによるCI実行時間増加**: 初回パッケージ解決時にGit cloneが発生するため、CI実行時間が増加する。Libraryキャッシュにパッケージが含まれるため、初回のみ遅延が発生する
- **Libraryキャッシュ**: パッケージ追加によりキャッシュが無効化されるか、またはキャッシュに古いLibraryが残りパッケージ解決に失敗する可能性がある

### レビュー項目

- [ ] 全CIワークフローがグリーン
- [ ] CI実行時間が許容範囲内（+30秒以内の増加）
- [ ] ワークフロー定義の変更が最小限
- [ ] deploy-webgl.ymlのトリガー設定が適切

---

## もし一から作り直すなら

**CI信頼性設計**:
外部Git依存（dot-net-g2p の `actions/checkout` + `sed` によるパス書き換え）は GitHub rate limit・ネットワーク障害・タグ削除で脆い。NuGet 化して GitHub Packages or MyGet にホストし、セマンティックバージョンで固定、checksum 検証を行う。あわせて `dependency-review-action` と Dependabot によるバージョン追跡を導入する。fallback として vendored tarball を LFS に同梱し、外部取得失敗時に切り替える二段構えにする。

**デプロイパイプライン**:
現状 `deploy-webgl.yml` は `feature/webgl-support` 単一ブランチトリガで、develop マージ後の最新状態が Pages に反映されない。`main`（本番）と `develop`（プレビュー環境）の二系統に分け、PR ごとの一時 Preview URL（`gh-pages` サブパスや Cloudflare Pages）を発行する。tag push 時はリリースデプロイを別ジョブに分離し、`concurrency: group: pages-{env}` で環境別に直列化する。

**設計思想**:
Unity ビルド時間（game-ci Docker + Library キャッシュでも 15–25 分）がボトルネックのため、Library キャッシュを `targetPlatform` 単位で分離し、Reusable Workflow + Composite Action で checkout/sed/cache 共通ステップを一元化する。テストは EditMode を PR 必須、PlayMode + マルチプラットフォームビルドは nightly + tag 限定にして CI コストを抑制。

**やり直すなら変えること**:
1. dot-net-g2p を NuGet パッケージ化し `sed` 書き換えを廃止
2. Reusable Workflow 化で 6 本の YAML 重複を削減
3. WebGL E2E を Playwright で追加（Pages デプロイ後の smoke test）
4. `actions/*` のバージョンを Renovate で自動更新、SHA pin + `permissions:` 最小化
5. CodeQL + Trivy で依存セキュリティスキャンを追加

**今回のアプローチを維持する理由**:
CI 6 本はすでに安定稼働しており、T-004 スコープは「通過確認」で十分。dot-net-g2p の NuGet 化は別リポジトリ責務で本チケット範囲外。Reusable Workflow リファクタは変更リスクが高く、現在の `develop` マージフローを阻害する可能性があるため、YAML 重複は許容する。deploy-webgl の `develop` トリガ追加のみ最小改修として検討し、他は据え置きが現実的。

---

## 後続タスクへの連絡事項

- **T-005（ドキュメント・リリース）へ**: CI全パイプラインの通過結果を共有
- **T-005へ**: deploy-webgl.ymlのトリガー設定変更有無を共有
- **T-005へ**: CI上で発見された問題とその修正内容を共有