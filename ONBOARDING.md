# uPiper 開発オンボーディング

> このファイルは「CLAUDE.md(技術アーキテクチャ)には書いていないが、知らないと CI が赤くなる / PR が弾かれる / デプロイが静かに不発火する」運用ルールを 1 枚に集約したもの。新規participant(人間/エージェント)は作業前に必読。
> 末尾改行は付けない(`.editorconfig: insert_final_newline = false`)。改行コードは LF。

## 1. ブランチ運用 — main へは develop からのみ

- **`main` 宛の PR は `develop` ブランチからのみ許可される。** それ以外の head から main へ PR を作ると `pr-target-check.yml` が `core.setFailed` で**必ず失敗する**(`headRef !== 'develop'` 判定)。
- `feature/*` の作業は **develop に向けて** PR を出す。
- 現行作業ブランチは `git branch --show-current` で確認(本ドキュメント作成時点: `feature/webgl-japanese-input`)。
- リモートには過去の作業ブランチが多数残存している(`git branch -r` で確認)。stale なものは base にしない / push しない。

## 2. カバレッジ — Runtime を足すならテストも足す（強制ゲートではない）

- `unity-tests.yml` の ReportGenerator は `minimumCoverageThresholds:lineCoverage=50`(対象 `+uPiper.Runtime`)を持つが、
  これは**レポート/バッジ上の参考閾値であり、CI を exit 1 で失敗させる強制ゲートではない**。
  テストが失敗した場合のみ unity-test-runner ステップが赤くなる。
- ただしプロジェクト方針として **lineCoverage 50% 維持が期待される**ため、Runtime に手を入れたら EditMode テストも併せて追加すること(レビュー指摘を減らす)。
- **カバレッジ集計の対象外**(`pathFilters` / `assemblyfilters` より):
  - `Assets/uPiper/Runtime/Demo/**` / `Core/IL2CPP/**` / `Core/Performance/**`
  - `IndexedDBCache.cs` / `WebGLStreamingAssetsLoader.cs` / `WebGLLoadingPanel.cs` / `WebGLInteractionGate.cs`
  - `IOSAudioSessionHelper.cs` / `InferenceAudioGenerator.cs`(ONNX実推論) / `UnityMainThreadDispatcher.cs`
- 含まれる(=テスト推奨)主領域: `Runtime/Core/Phonemizers/**`、`Runtime/Core/AudioGeneration/**`(InferenceAudioGenerator を除く)。
- テスト実行はローカルで**クラス単位**(uLoopMCP run-tests を regex filter)。assembly 全体は MCP でタイムアウト。
  `uPiper.Tests.Runtime.asmdef` は `includePlatforms:["Editor"]` のため EditMode で動く。

## 3. コミットメッセージ・リリース

- **Conventional Commit 準拠**: `feat:` `fix:` `docs:` `ci:` `chore:` `refactor:` `style:` `test:`。scope 付き可(例 `docs(webgl):`)。
- **semantic-release は撤去済み**(`2830f3b` — 「ブランチ保護ルール競合で全回失敗・未使用のため削除。リリースは手動フロー」)。
- **commitlint も未導入** — 規約は人手 / レビューで担保。
- **`CHANGELOG.md` は手動更新**(Keep a Changelog 形式)。リリース時に自動生成されない。
- 中央バージョン定数は `Assets/uPiper/Editor/uPiperSetup.cs:22 PACKAGE_VERSION`。同期ミスが過去に発生しているので `/version-check` で整合を確認する。

## 4. WebGL デプロイは「静かに不発火」する点に注意

- `deploy-webgl.yml:5` の push 自動トリガーは **`branches: [ feature/webgl-support ]` 固定**。
- 現行ブランチに push しても **GitHub Pages デプロイは自動では走らない**(エラーも出ない=静かな不発火)。
- 確認したいときは **`workflow_dispatch` で手動実行**(Actions → Deploy WebGL to GitHub Pages → Run workflow)。
- TODO(設定見直し): 本ブランチで WebGL 作業を継続するなら `deploy-webgl.yml:5` を更新するか workflow_dispatch 運用に正式に寄せる。**本ドキュメントでは設定変更しない**。

## 5. push 前ローカル先回りチェック(CI を赤くしない)

CI のフォーマットゲートはローカルで先に潰せる。**末尾改行を追加しない**こと(`insert_final_newline = false`)。
`dotnet format` の whitespace フェーズは CI(`dotnet-format.yml`, CHECK_PHASE=whitespace_only)と同一。
`/precheck` スキルでこれら(whitespace + compile + tests)を一括で先回りできる。

```bash
dotnet format whitespace . --folder \
  --exclude "**/Library/**" --exclude "**/Temp/**" \
  --exclude "**/Packages/**" --exclude "**/obj/**" \
  --verify-no-changes
```
PowerShell(行継続はバッククォート):
```powershell
dotnet format whitespace . --folder `
  --exclude "**/Library/**" --exclude "**/Temp/**" `
  --exclude "**/Packages/**" --exclude "**/obj/**" `
  --verify-no-changes
```
- **コンパイル/テスト**: uLoopMCP `compile` → `mcp__ide__getDiagnostics` → クラス単位 `run-tests`。CS0104(Object 曖昧) / CS1061 / TestHelpers 名前空間衝突など過去 CI でしか出なかったエラーを先取り。
- **git フックの有効化**: クローン後に `bash scripts/install-git-hooks.sh`(または `pwsh scripts/install-git-hooks.ps1`)を1回実行。コミット時に whitespace ゲート(`.githooks/pre-commit`)が走る。
- **Unity アナライザー規約**: Unity オブジェクトに null 合体 `??` / null 条件 `?.` / null 合体代入 `??=` を使わない(UNT0007 / UNT0008 / UNT0023)。

## 6. 外部依存 — dot-net-g2p は別リポジトリ

- `ayutaz/dot-net-g2p`(public)は **別リポジトリ**。**uPiper 側の責務でそのコードを変更しない**。
- バージョンは `Packages/manifest.json` の Git URL `#vX.Y.Z` で管理(本ドキュメント作成時点 #v1.8.2)。README のインストール手順も同じタグに揃える。
- CI は manifest の Git URL から UPM が直接取得する(個別 checkout は不要)。