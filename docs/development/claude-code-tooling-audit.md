# uPiper Claude Code 運用改善 調査レポート（優先順位付き）

## 1. 冒頭サマリー

uPiper（412コミット）の `.claude/` 配下は**完全なグリーンフィールド**である。実測で確認した現状は次のとおり:

- `.claude/skills` / `.claude/hooks` / `.claude/commands` は**いずれも存在しない**（`ls .claude/` は `scheduled_tasks.lock` / `settings.local.json` / `worktrees/` のみ）。
- `.claude/settings.local.json` は **allow 241エントリに肥大化**（16,561バイト）。約1/3が使い捨てリテラル（heredocコミット全文・PRRT_スレッドID列・末尾スペース付きの非コマンドパス文字列など）。
- `.gitignore:13` が `.claude/` を**丸ごと無視**しており（`git ls-files .claude/` は空）、チーム共有の設定/フック/コマンドが現状では一切配布できない構造的制約がある。

git履歴に繰り返し現れる手戻りトイルも実測で裏付けた:

- **FINALNEWLINE専用修正コミット 8件**（`45fcf78` / `0db0f24` / `2f3f87d` / `d39b065` / `1e45270` / `922a361` / `98d48b1` / `303c244`）。根本原因は `.editorconfig` の `insert_final_newline=false`（:12）+ `end_of_line=lf`（:9）を Windows で運用しているため、一般整形ツールの「末尾改行追加」癖と衝突すること。
- CIでしか出ないコンパイルエラー（CS0104 等）、Unity Test Framework の async デッドロック、チケットmd量産、PRレビュースレッド手動一括解決、バージョン文字列ドリフト（**BasicTTSDemo が 1.4.0 / 本体 1.5.0**、**manifest #v1.8.2 / README #v1.5.0** を実測確認）、.meta churn。

**提案の全体像**: CIの `dotnet format whitespace . --folder --verify-no-changes`（`dotnet-format.yml:67` と逐語一致、ローカルで再現可能）を中核に、編集時/コミット時の先回り検証を整備する。加えて先回り検証スキル、チケット/PRレビューの定型コマンド化、バージョンドリフト検証、settings剪定＋共有解禁、死んだCIステップ削除、運用ルールのドキュメント化を行う。すべて Windows（Git Bash/PowerShell）・game-ci・dot-net-g2p別repo制約下で動作し、CIの whitespace_only フェーズ・uLoopMCP・semantic-release（撤去済み）と非衝突であることを各設計で確認済み。

⚠️ **本レポートは調査・提案であり、実ファイルの作成・編集はユーザー承認後に行う**（後述 §6）。

---

## 2. 即採用すべき提案（優先度順）

### 【1位】opp-precommit-final-gate — git pre-commit 最終ゲート（手動編集も捕捉）

- **type**: precommit / **工数**: S / **netValue**: medium / **検証 confidence**: 0.90 / **判定**: adopt-with-changes
- **解決する問題（証拠付き）**: CI(`dotnet-format.yml` whitespace_only フェーズ)でしかフォーマット崩れを検出できず、手動エディタ編集がそのままコミットされCIで弾かれる経路が残る。FINALNEWLINE/フォーマット起因の手戻り9件超を実測（`922a361`「CIフォーマット修正 + v2.0.0→v1.5.0」/`d39b065`「フォーマット + double-dispose」/`45fcf78`「FINALNEWLINE 21ファイル」/`2f3f87d`/`0db0f24`/`98d48b1`/`303c244`/`1e45270`）。Claude経由の編集しか捕捉しない PostToolUse では塞げない。`.githooks` 不在・`core.hooksPath` 既定・husky/lefthook/pre-commit いずれも不在のグリーンフィールド。
- **具体策**: リポジトリにコミットできる `.githooks/pre-commit` を追加し、`git config core.hooksPath .githooks` で配布。ステージ済みC#のみ抽出し、対象があるときだけ **CIと同一の `dotnet format whitespace . --folder --verify-no-changes`** をかける。`.gitattributes` が `*.cs text eol=lf` 固定済みのため Windows でも CRLF 差分は出ない。
- **検証者の留意点（修正必須・反映済みドラフト）**:
  1. **【最重要・致命的】`--include` のカンマ区切りは false-pass する**（実機で `--include "a.cs,b.cs"` が「0個中0個」EXIT 0 を確認。違反があっても素通り＝ゲート無効化）。→ **ファイルごとに `--include` フラグを反復付与する配列方式**に変更。
  2. **【速度・CI乖離】ソリューションモード（`uPiper.sln` 引数）は1ファイル32.8秒。**→ **CIと完全同一の `. --folder` フォルダモード**に統一（実測3.4秒、restore不要）。
  3. エラー検出は末尾改行を「削除」方向（`FINALNEWLINE: 1文字削除`）で出すことをコメント明記（editorconfig非衝突を担保、実機確認済み）。
  4. 配布漏れ対策として CONTRIBUTING/README への1行追記を必須化（`core.hooksPath` はローカル設定でコミット不可）。

**実物ドラフト（検証者修正を反映済み）**:

`.githooks/pre-commit`（chmod +x 推奨。末尾改行なしで保存）
```bash
#!/usr/bin/env bash
# uPiper pre-commit: ステージ済み C# に CI と同一の whitespace 検証をかける最終ゲート。
# CI(dotnet-format.yml の whitespace_only フェーズ)を先回りし、手動編集も含めフォーマット崩れを阻止。
# insert_final_newline=false / end_of_line=lf は dotnet format whitespace が .editorconfig を尊重し
# 「末尾改行を削除」する方向(FINALNEWLINE: 1文字削除)なので壊さない。Unity コンパイルは伴わない。
set -euo pipefail

# 緊急バイパス: EMERGENCY_SKIP_FORMAT=1 git commit ...
if [ "${EMERGENCY_SKIP_FORMAT:-0}" = "1" ]; then
  echo "[pre-commit] EMERGENCY_SKIP_FORMAT=1 のため whitespace 検証をスキップしました。" >&2
  exit 0
fi

# ステージ済み C# (追加/コピー/変更) のみ対象。リネーム旧名・削除は除外。
mapfile -t STAGED_CS < <(git diff --cached --name-only --diff-filter=ACM -- '*.cs')
if [ "${#STAGED_CS[@]}" -eq 0 ]; then
  exit 0  # C# 変更なし → 何もしない
fi

# dotnet が無い環境ではゲートを諦め、CI に委ねる(コミットは通す)。
if ! command -v dotnet >/dev/null 2>&1; then
  echo "[pre-commit] dotnet が見つからないため whitespace 検証をスキップします（CI で検証されます）。" >&2
  exit 0
fi

# 【修正1】カンマ区切りは false-pass するため、ファイルごとに --include を反復付与する。
INCLUDE_ARGS=()
for f in "${STAGED_CS[@]}"; do
  INCLUDE_ARGS+=(--include "$f")
done

echo "[pre-commit] whitespace 検証中 (${#STAGED_CS[@]} C# ファイル)..." >&2

# 【修正2】CI(dotnet-format.yml L67)と完全同一の `. --folder` フォルダモード(restore不要・高速)。
set +e
dotnet format whitespace . --folder \
  "${INCLUDE_ARGS[@]}" \
  --exclude "**/Library/**" \
  --exclude "**/Temp/**" \
  --exclude "**/Packages/**" \
  --exclude "**/obj/**" \
  --verify-no-changes \
  --verbosity quiet
STATUS=$?
set -e

if [ "$STATUS" -ne 0 ]; then
  echo "" >&2
  echo "[pre-commit] whitespace フォーマット違反があります（CI の dotnet-format で弾かれます）。" >&2
  echo "   .editorconfig は insert_final_newline=false なので「末尾改行は追加せず削除」されます。" >&2
  echo "   次で自動修正してから再度ステージ＆コミットしてください(検証フラグ無し=削除方向):" >&2
  echo "" >&2
  echo "     dotnet format whitespace . --folder \\" >&2
  for f in "${STAGED_CS[@]}"; do echo "       --include \"$f\" \\" >&2; done
  echo "       --exclude \"**/Library/**\" --exclude \"**/Temp/**\" --exclude \"**/Packages/**\" --exclude \"**/obj/**\"" >&2
  echo "" >&2
  echo "   緊急時のみ:  EMERGENCY_SKIP_FORMAT=1 git commit ..." >&2
  echo "" >&2
  exit 1
fi

echo "[pre-commit] whitespace 検証 OK" >&2
exit 0
```

`scripts/install-git-hooks.sh`（Git Bash / mac / Linux）
```bash
#!/usr/bin/env bash
set -euo pipefail
REPO_ROOT="$(git rev-parse --show-toplevel)"
cd "$REPO_ROOT"
git config core.hooksPath .githooks
chmod +x .githooks/* 2>/dev/null || true
echo "core.hooksPath を .githooks に設定しました（現在: $(git config --get core.hooksPath)）"
```

`scripts/install-git-hooks.ps1`（Windows PowerShell）
```powershell
$repoRoot = (git rev-parse --show-toplevel).Trim()
Set-Location $repoRoot
git config core.hooksPath .githooks
Write-Host "core.hooksPath を .githooks に設定しました（現在: $(git config --get core.hooksPath)）"
```

> 導入手順（管理者1回 + 各コントリビューター1回）: Windows `pwsh -File scripts/install-git-hooks.ps1` / Git Bash `bash scripts/install-git-hooks.sh`。README/CONTRIBUTING に1行追記必須。

---

### 【2位】opp-pr-resolve-threads-command — PRレビュースレッド一括解決 /resolve-threads

- **type**: command / **工数**: S / **netValue**: high / **検証 confidence**: 0.85 / **判定**: adopt-with-changes
- **解決する問題（証拠付き）**: PRレビュー対応コミットが常態化（Copilot関連の指摘対応コミット実測38件: `39e2601`「6件」/`5530ca4`「11件」/`5ca7678`「6件」/`73701e0`「4件」/`2381e09`「4件」など）。未解決スレッドの一括解決スクリプト断片が `settings.local.json` に使い捨てで堆積（実測: `for thread_id in PRRT_...` ループ3グループ、PRRT_ID **25個**、PRRC_ID **7個**、`do echo`/`done`/`read tid:*` 等の制御断片）。これが allowlist 肥大（実測16,561バイト）の主要因の一つ。GraphQLスレッドIDは毎回変わり再利用不能。
- **具体策**: 「未解決スレッド取得 → 一覧表示 → resolveReviewThread mutation で一括解決」を `.claude/commands/resolve-threads.md` の単一コマンドに閉じる。全処理を既許可の `Bash(gh api:*)` で完結させ、PRRT_ID は一時変数として扱う（settingsに染み出さない）。読み取り→提示→確認→解決の3フェーズ、デフォルトは一覧表示のみで停止。新規permission追加ゼロ（実PR #178でクエリ実行成功・gh 2.76.1 内蔵 `--jq` のみ）。
- **検証者の留意点（修正反映済み）**:
  1. expectedImpact の「導入と同時に既存32件を安全削除可能」は誤解を招く。本コマンド自体は既存エントリを削除しない → 「今後の堆積停止が本コマンドの効果、既存32エントリの削除は §3の settings 整理で別途実施」と整理。
  2. フェーズ2の resolve デフォルト例を**全件無条件 resolve から「outdatedのみ」に変更**（`select(.isResolved==false and .isOutdated==true)`）。outdated=コードが既に変わった=安全に閉じやすい。全件resolveは明示オプト時のみ。
  3. `reviewThreads(first:100)` のページネーション欠如は本リポジトリ規模（実測最大6スレッド）では実害なし。コメントで「100件超は要pageInfo対応」を明記。

**実物ドラフト（修正反映済み・`.claude/commands/resolve-threads.md`）**:
````markdown
---
description: PRの未解決レビュースレッド(GraphQL PRRT_)を取得・一覧表示し、確認後に一括でresolveする。settings汚染を防ぐためコマンド内に閉じる。
argument-hint: "[PR番号] (省略時は現在ブランチのPRを自動検出)"
allowed-tools: Bash(gh pr view:*), Bash(gh api:*), Bash(git rev-parse:*)
---

# PRレビュースレッド一括解決 (/resolve-threads)

このリポジトリ (`ayutaz/uPiper`) のPRレビュー指摘対応では、未解決(unresolved)レビュースレッドを
一括で resolve する作業が頻発する。従来はスレッドID(`PRRT_...`)を毎回手で貼っていたため
`.claude/settings.local.json` の allow が肥大化していた。このコマンドは全処理を `gh api graphql`
（既に許可済みの `Bash(gh api:*)`）に閉じ込め、PRRT_ID を一時変数として扱う。

引数 `$ARGUMENTS` = 解決対象のPR番号。省略された場合は現在のブランチに紐づくPRを自動検出する。

## 重要ルール（必ず守る）
- **コード/ファイルは一切変更しない。** GitHub上のレビュースレッド状態のみを操作する。
- **いきなり全部 resolve しない。** まずフェーズ1で未解決スレッドを一覧表示し、ユーザー/会話の文脈から
  解決対象の指示を得てからフェーズ2を実行する。
- **PRRT_ などのIDをコマンド外(settings等)に書き出さない。** すべてシェルの一時変数で完結させる。
- `--jq` は gh 内蔵。外部の `python`/`node`/`jq` には依存しない（新規 permission を避けるため）。

## フェーズ0: 対象PRの確定
```bash
PR="$ARGUMENTS"
if [ -z "$PR" ]; then
  PR=$(gh pr view --json number --jq '.number')
fi
echo "対象PR: #$PR"
```
検出できない場合はユーザーにPR番号を尋ねて停止する。

## フェーズ1: 未解決スレッドの一覧（読み取りのみ・破壊操作なし）
```bash
gh api graphql -f query='
query($owner:String!, $repo:String!, $pr:Int!) {
  repository(owner:$owner, name:$repo) {
    pullRequest(number:$pr) {
      reviewThreads(first:100) {
        nodes {
          id
          isResolved
          isOutdated
          path
          comments(first:1) { nodes { author { login } body } }
        }
      }
    }
  }
}' -F owner=ayutaz -F repo=uPiper -F pr="$PR" \
  --jq '.data.repository.pullRequest.reviewThreads.nodes[]
        | select(.isResolved==false)
        | "\(.id)\t\(if .isOutdated then "[outdated]" else "[active]  " end)\t\(.path)\t\(.comments.nodes[0].author.login // "?"): \(.comments.nodes[0].body // "" | gsub("\n";" ") | .[0:80])"'
```
出力が空なら「未解決スレッドはありません」と報告して終了。
出力がある場合は path / outdated判定 / 1件目コメント要約を提示し、**どれを resolve するか**を確認する
（「全部」「outdatedのみ」「このpathだけ」）。未対応の指摘は resolve しないよう注意喚起する。

## フェーズ2: 選択されたスレッドを resolve（確認後のみ実行）
デフォルトは安全な **outdated のみ**。全件 resolve はユーザーが明示的に指示した時だけ
`select(.isResolved==false)` に切り替える。
```bash
# 既定: outdated(コードが既に変わった=安全に閉じやすい)のみを一時変数へ。settingsには残さない。
THREAD_IDS=$(gh api graphql -f query='
query($owner:String!, $repo:String!, $pr:Int!) {
  repository(owner:$owner, name:$repo) {
    pullRequest(number:$pr) {
      reviewThreads(first:100) { nodes { id isResolved isOutdated } }
    }
  }
}' -F owner=ayutaz -F repo=uPiper -F pr="$PR" \
  --jq '.data.repository.pullRequest.reviewThreads.nodes[] | select(.isResolved==false and .isOutdated==true) | .id')

if [ -z "$THREAD_IDS" ]; then
  echo "resolve対象(outdated)がありません"
else
  for TID in $THREAD_IDS; do
    echo "Resolving $TID ..."
    gh api graphql -f query='
    mutation($id:ID!) {
      resolveReviewThread(input:{threadId:$id}) {
        thread { id isResolved }
      }
    }' -F id="$TID" \
      --jq '"  -> resolved=\(.data.resolveReviewThread.thread.isResolved)"'
  done
fi
```
（全件 resolve を指示された場合のみ `--jq` の select を `select(.isResolved==false)` に変更する。
 `reviewThreads(first:100)` は100件上限。100件超のPRは pageInfo/endCursor 対応が必要だが本プロジェクト規模では通常不要。）

## フェーズ3: 後処理
- 解決した件数を報告する。
- コミット/プッシュはこのコマンドでは行わない。
- 誤って解決した場合は `unresolveReviewThread(input:{threadId:$id})` で戻せる旨を案内する。

## PowerShell環境での注意
`gh api graphql` 自体はシェル非依存。PowerShellで回す場合は `for ... in` を foreach に置き換える:
```powershell
$ids = gh api graphql -f query='query($owner:String!,$repo:String!,$pr:Int!){repository(owner:$owner,name:$repo){pullRequest(number:$pr){reviewThreads(first:100){nodes{id isResolved isOutdated}}}}}' -F owner=ayutaz -F repo=uPiper -F pr=$PR --jq '.data.repository.pullRequest.reviewThreads.nodes[] | select(.isResolved==false and .isOutdated==true) | .id'
foreach ($tid in $ids) {
  gh api graphql -f query='mutation($id:ID!){resolveReviewThread(input:{threadId:$id}){thread{id isResolved}}}' -F id=$tid
}
```
````

---

### 【3位】opp-uloop-preflight-skill — /precheck 先回り検証スキル

- **type**: skill / **工数**: S / **netValue**: high / **検証 confidence**: 0.85 / **判定**: adopt-with-changes
- **解決する問題（証拠付き）**: CIでしか露見しないコンパイルエラーとテストデッドロックが手戻りを生む。CS0104(Object曖昧)/CS0019/CS1061/TestHelpers名前空間衝突/AudioClip.Create引数違い: `45fcf78` / `2158fde` / `36e21f8` / `99f727c` / `901fa3a` で実証。async/await デッドロック・ハング: `344f9d9`(Task.Yield デッドロック除去) / `2fdf77a`(MixedLanguageE2ETests デッドロック) / `acd1b2c`(timeout-minutes:30 後付け) / `6414d62`(ハング調査ログ)。CIの whitespace_only フェーズは型・構文・実行を一切見ないため先回りできていない。
- **具体策**: `.claude/skills/precheck/SKILL.md` に明示起動スキルを新設し、CIと同一の3段検証を束ねる。Step1: `dotnet format whitespace . --folder --verify-no-changes`（FINALNEWLINE/whitespace 先回り）/ Step2: `mcp__uLoopMCP__compile` で CS00xx 先回り（差分コンパイル・軽量）+ `mcp__ide__getDiagnostics` 補完 / Step3: `mcp__uLoopMCP__run-tests` を**必ずクラス単位フィルタ**で実行（assembly全体はタイムアウト）。30分CI(`unity-tests.yml`)を待たずローカル数分で緑を確認。自動hookは重く誤爆するため明示起動skillが妥当。
- **検証者の留意点（修正反映済み）**:
  1. **実在しない `PhonemizerIntegrationTests` を削除**し、実在する `ProsodyInferenceIntegrationTests` / `MixedLanguageE2ETests` / `*E2ETests` に差し替え（MEMORY.md由来の陳腐化を持ち込まない）。
  2. Step1 の自動修正後は **`--verify-no-changes` を再実行して冪等性を確認**する一文を追加。
  3. dotnetバージョン差（CI 6.0.x / ローカル 10.x）を前提に明記し「whitespace判定は通常一致するが最終確定はCI」と保険。
  4. Step3 のテストクラス推定で `git diff` を使う際、`Tests/Editor` と `Tests/Runtime`(includePlatforms:[Editor]) 両方を走査対象にする（BackendSelectorTests のように同名クラスが両方に存在しうるため）。
  5. uLoopMCP の `get-diagnostics` は実在しない。コンパイル診断は `mcp__uLoopMCP__compile` の戻り値、IDE診断は `mcp__ide__getDiagnostics`（許可済み）を使う。

**実物ドラフト（修正反映済み・`.claude/skills/precheck/SKILL.md`）**:
````markdown
---
name: precheck
description: コミット/PR前のローカル先回り検証。CIと同一の dotnet format whitespace + uLoopMCP compile(CS00xx先回り) + クラス単位 run-tests を束ねて実行し、unity-tests.yml(EditMode, 30分)を待たずにコンパイル・whitespace・テスト緑を確認する。コミット前/PR前/「CIが落ちそう」「先回り検証して」と言われたら使う。引数にテストクラスのregex(例 /precheck CustomDictionary)を渡すと run-tests のフィルタになる。
---

# /precheck — コミット/PR前 先回り検証スキル

CIでしか露見しないコンパイルエラー(CS0104/CS0019/CS1061/名前空間衝突/AudioClip.Create引数)・
whitespace違反(FINALNEWLINE)・async デッドロックを、`unity-tests.yml`(EditMode・timeout 30min)を
待たずにローカルで先回り検出する。

## 前提（この環境固有・厳守）
- OS は Windows 11。シェルは PowerShell が主。POSIX スクリプトは Git Bash 経由。
- `.editorconfig` は `insert_final_newline=false` / `end_of_line=lf`。**末尾改行は「追加」せず「検証/除去」方向**でのみ扱う。
- uLoopMCP は起動済み Unity Editor に接続して**差分コンパイル**する（フル再コンパイル不要・軽量）。
  Editor が未起動なら uloop-launch スキルで起動してから。
- `mcp__uLoopMCP__run-tests` は**必ずクラス単位の name フィルタ**で呼ぶ。assembly 全体はタイムアウトする(MEMORY.md 既知)。
- dotnet バージョンは CI(6.0.x)とローカル(10.x)で差があるが whitespace 判定は .editorconfig 駆動のため通常一致する。
  **最終確定はCI**(本スキルは先回りであって置換ではない)。
- **dot-net-g2p は別リポジトリ。本スキルからは絶対に触れない。**

## 実行手順（この順で。失敗したら以降を止めて報告）

### Step 1: whitespace 検証（CI dotnet-format.yml と完全同一コマンド）
```bash
dotnet format whitespace . --folder \
  --exclude "**/Library/**" --exclude "**/Temp/**" \
  --exclude "**/Packages/**" --exclude "**/obj/**" \
  --verify-no-changes
```
PowerShell では同一引数を1行で:
```powershell
dotnet format whitespace . --folder --exclude "**/Library/**" --exclude "**/Temp/**" --exclude "**/Packages/**" --exclude "**/obj/**" --verify-no-changes
```
- 非ゼロ終了したら CI(`dotnet-format.yml` の whitespace_only フェーズ)も同じく落ちる。
- 修正は `--verify-no-changes` を外して同コマンドを再実行（`insert_final_newline=false` なので末尾改行は追加されない＝FINALNEWLINE を増やさない）。
- **修正後は必ず `--verify-no-changes` を再実行して冪等性を確認**する(整形ツールが別の差分を生まないことの担保)。

### Step 2: コンパイル先回り（CS00xx を CI 前に潰す）
1. `mcp__uLoopMCP__compile` を呼ぶ（差分で十分。`forceRecompile` は通常不要）。
2. 返ったエラー/警告を確認。CS0104(System.Object vs UnityEngine.Object 曖昧)・CS0019・CS1061・名前空間衝突・引数違いが頻出。
3. 補完として `mcp__ide__getDiagnostics`(許可済み)で IDE 側診断を突き合わせる。
   ※ uLoopMCP に `get-diagnostics` は無い。コンパイル診断は `compile` 戻り値、IDE診断は `mcp__ide__getDiagnostics`。
4. **既知の地雷チェック**:
   - `Object` の曖昧参照 → `UnityEngine.Object` か `System.Object` を明示(45fcf78/2158fde)。
   - テストの `TestHelpers` 名前空間衝突 → 完全修飾 or using alias(2158fde)。
   - `AudioClip.Create` の引数シグネチャ(2158fde)。
   - 旧シグネチャを参照したテスト(901fa3a)。
- コンパイルが通るまで Step 3 に進まない。

### Step 3: 影響範囲のテスト（クラス単位フィルタ必須）
1. 変更内容から関連テストクラスを推定。`git diff --name-only HEAD` で触ったソースを見て、
   **Tests/Editor と Tests/Runtime(includePlatforms:[Editor]) の両方**を走査対象にする
   (BackendSelectorTests のように同名クラスが両方に存在しうるため取りこぼし防止)。
2. `mcp__uLoopMCP__run-tests` を**クラス名フィルタ**で呼ぶ(EditMode 実行＝CI と同一条件)。
3. **async デッドロック疑いの重いテスト**(`MixedLanguageE2ETests`, `ProsodyInferenceIntegrationTests`,
   `*E2ETests`)は**単独クラスで**実行し、ハングしたら即報告(過去: 344f9d9/2fdf77a)。
4. `/precheck <regex>` で引数が渡されたらそれをフィルタに使う。
5. ハングして戻らない場合は `mcp__uLoopMCP__get-logs` で直近ログを取得し原因(await/Task.Yield デッドロック等)を切り分けて報告。

## 完了条件と報告フォーマット
3 ステップすべて緑なら「先回り検証 OK（whitespace / compile / tests[クラス名列挙]）」と一言で報告。
いずれか失敗なら失敗ステップ・エラーコード(CS00xx 等)・該当ファイル/行・推奨修正を提示する。
**勝手に commit/push はしない**。

## やらないこと
- assembly 全体の run-tests（タイムアウト）。
- 末尾改行の「追加」（insert_final_newline=false に反する）。
- フル Unity 再コンパイルの強制（uLoopMCP 差分で十分）。
- dot-net-g2p（別repo）のコード変更。
- semantic-release / バージョン定数(`Assets/uPiper/Editor/uPiperSetup.cs`) の自動書き換え。
````

---

### 【4位】opp-settings-prune-and-share — settings剪定 + 共有 settings.json 解禁

- **type**: settings / **工数**: S / **netValue**: high / **検証 confidence**: 0.82 / **判定**: adopt-with-changes
- **解決する問題（証拠付き）**: `settings.local.json` の allow が**実測241件**（16,561バイト）。約80件が再利用不能な使い捨て: heredocコミット全文6件 / PRRT_/PRRC_ループ断片（`for f:*`/`do sed:*`/`for pkg:*` 含め約10件）/ 末尾スペース付き path-as-command 18件 / Unity.exeフルコマンド3件 / VRChat/booth/canny/wikidot 系 WebFetch 8件 / ssh/tailscale 9件。構造的根因は `.gitignore:13` の `.claude/` 丸無視で、**他の hook/command/skill 提案を配布する前提が成立しない**（実測 `git ls-files .claude/` は空）。
- **具体策（3点セット）**: (1) **.gitignore carve-out** — `.claude/` 丸無視を `settings.local.json` / `worktrees/` / `*.lock` のみ除外に変更（否定パターンではなく置換が正解、実証済み）。(2) **共有 settings.json への昇格** — 汎用ワイルドカードのみ集約。(3) **settings.local.json の剪定** — heredoc6・PRループ約10・path-as-command18・Unity.exeフル3・VRChat8・upiper-test を削除（`Bash(git commit:*)` 等が包含するため安全）。
- **検証者の留意点（修正必須・反映済み）**:
  1. **【必須】共有 settings.json から `additionalDirectories` の絶対パス（ユーザー名 `yuta` を含む）を削除**。チームメンバー環境で MISSING 警告/破綻。piper-plus パスは local 側のみに残置。
  2. **剪定は「全置換」ではなく named カテゴリ削除に限定**し、他の既存granular許可は触らない（宣言と実装の一致）。
  3. 適用後に `dotnet format whitespace . --folder --verify-no-changes` をローカル実行し、新規追跡した `.claude/settings.json` が検証を落とさないことを先回り確認（FINALNEWLINE最後の穴）。
  4. 実装は既存スキル `/fewer-permission-prompts` / `/update-config` に乗せられる。

**実物ドラフト（修正反映済み）**:

`.gitignore` carve-out（現在の 12-13 行目を置換。末尾改行は触らない）
```
# Ignore Claude per-user config only; share team config (settings.json/hooks/skills/commands)
.claude/settings.local.json
.claude/worktrees/
.claude/*.lock
```

`.claude/settings.json`（新規・git追跡対象。**絶対パスは含めない**）
```json
{
  "permissions": {
    "allow": [
      "Bash(git:*)",
      "Bash(gh:*)",
      "Bash(dotnet build:*)",
      "Bash(dotnet restore:*)",
      "Bash(dotnet clean:*)",
      "Bash(dotnet test:*)",
      "Bash(dotnet format:*)",
      "Bash(uv run:*)",
      "Bash(uv add:*)",
      "Bash(uv pip install:*)",
      "Bash(uv pip show:*)",
      "Bash(uloop launch:*)",
      "Bash(cat:*)",
      "Bash(ls:*)",
      "Bash(find:*)",
      "Bash(grep:*)",
      "Bash(mkdir:*)",
      "Bash(wc:*)",
      "Bash(tail:*)",
      "Bash(xargs:*)",
      "mcp__uLoopMCP__compile",
      "mcp__uLoopMCP__get-logs",
      "mcp__uLoopMCP__run-tests",
      "mcp__uLoopMCP__clear-console",
      "mcp__uLoopMCP__unity-search",
      "mcp__uLoopMCP__find-game-objects",
      "WebSearch",
      "WebFetch(domain:github.com)",
      "WebFetch(domain:raw.githubusercontent.com)",
      "WebFetch(domain:docs.unity3d.com)",
      "WebFetch(domain:discussions.unity.com)",
      "WebFetch(domain:game.ci)",
      "WebFetch(domain:onnxruntime.ai)",
      "WebFetch(domain:openupm.com)",
      "WebFetch(domain:package.openupm.com)",
      "WebFetch(domain:www.nuget.org)"
    ],
    "deny": [],
    "ask": [],
    "additionalDirectories": []
  },
  "enableAllProjectMcpServers": true,
  "enabledMcpjsonServers": [
    "uLoopMCP"
  ]
}
```

`.claude/settings.local.json`（剪定後。絶対パス依存/秘匿/環境固有のみ残す。piper-plus パスはここに残置）
```json
{
  "permissions": {
    "allow": [
      "Bash(powershell:*)",
      "Bash(powershell.exe:*)",
      "Bash(python:*)",
      "Bash(python3:*)",
      "Bash(python -c:*)",
      "Bash(node -e:*)",
      "Bash(curl:*)",
      "Bash(docker:*)",
      "Bash(cargo check:*)",
      "Bash(cargo test:*)",
      "Bash(npm view:*)",
      "Bash(where:*)",
      "Bash(dir:*)",
      "Bash(del:*)",
      "Bash(findstr:*)",
      "Bash(xxd:*)",
      "Bash(du:*)",
      "Bash(test:*)",
      "Bash(taskkill //F //IM Unity.exe)",
      "Bash(\"C:/Program Files/Unity/Hub/Editor/6000.0.58f2/Editor/Unity.exe\":*)",
      "Bash(build.bat)",
      "mcp__uLoopMCP__execute-dynamic-code",
      "mcp__uLoopMCP__focus-window",
      "mcp__uLoopMCP__control-play-mode",
      "mcp__uLoopMCP__capture-window",
      "mcp__uLoopMCP__capture-unity-window",
      "mcp__ide__getDiagnostics",
      "Read(//c/Users/yuta/.claude/**)",
      "Read(//c/Users/yuta/Desktop/Private/uPiper/**)",
      "Read(//c/Users/yuta/Desktop/Private/dot-net-g2p/src/DotNetG2P.$pkg/**)",
      "Read(//c/Program Files/Unity/Hub/Editor/**)"
    ],
    "deny": [],
    "ask": [],
    "additionalDirectories": [
      "C:\\Users\\yuta\\Desktop\\Private\\piper-plus"
    ]
  },
  "enableAllProjectMcpServers": true,
  "enabledMcpjsonServers": [
    "uLoopMCP"
  ]
}
```

> 削除対象（named カテゴリのみ）: heredoc 6 / PRループ約10(`for thread_id`/`for f`/`do sed`/`for pkg`/`do echo`/`done`/`read tid`) / path-as-command 18 / Unity.exeフル3 / VRChat系8 / upiper-test / ssh・tailscale 9。
> 適用検証: `git check-ignore -v .claude/settings.json` が carve-out後にヒットしない → `git add` 可。`git status` で settings.local.json が現れないことを確認。

---

### 【5位】opp-whitespace-preflight — PostToolUse(Edit|Write) whitespace 先回りフック

- **type**: hook / **工数**: S / **netValue**: high / **検証 confidence**: 0.85 / **判定**: adopt-with-changes
- **解決する問題（証拠付き）**: Claude経由の Edit/Write 直後に CI同一の whitespace 違反を検出できれば、FINALNEWLINE専用修正コミット8件とCI往復（`d39b065`/`922a361`）を編集時点で撲滅できる。1編集あたり数百ms〜数秒（Unityコンパイル不要）。
- **具体策**: `.claude/settings.json` の hooks.PostToolUse matcher `Edit|Write` に、CIと同一基準の `dotnet format whitespace` を「編集した .cs 1ファイルだけ」に scope して走らせる。デフォルトは `--verify-no-changes`（非破壊）。検出時 exit 2 + stderr で Claude に block フィードバック。
- **検証者の留意点（修正必須・反映済み）**:
  1. **【致命的・修正必須】PostToolUse が渡す絶対パスを `--include` にそのまま渡し + `--exclude` グロブを併用すると、何もマッチせず exit 0（false-pass）になる**（実機3/3回 exit 0、静かなno-op）。→ **`--include` に渡す前に絶対パスを proj_dir 基準の相対パスへ変換**する（実機で全除外併用でも exit 2 を確認）。
  2. **【grounding訂正】`.claude/` は `.gitignore:13` で無視されるため settings.json はチーム共有されない**。本フックの効果は §4 の carve-out 適用後にのみチームへ展開可能。それまではローカル限定。
  3. jq不在時の sed フォールバックは forward-slash JSON のみ堅牢 → jq推奨を明記。スクリプト本体も末尾改行なしで保存。
  4. **本フックは pre-commit(1位)とは検証方向で冪等共存する**。pre-commit が全編集を捕捉する最終ゲートのため、本フックは「即時フィードバックの上乗せ」。pre-commit 単独でも価値が成立するため、本フックは pre-commit 導入後に追加するのが安全。

**実物ドラフト（修正反映済み）**:

`.claude/settings.json` の hooks ブロック（§4 の共有 settings.json にマージ）
```json
{
  "hooks": {
    "PostToolUse": [
      {
        "matcher": "Edit|Write",
        "hooks": [
          {
            "type": "command",
            "command": "bash \"$CLAUDE_PROJECT_DIR/.claude/hooks/whitespace-preflight.sh\""
          }
        ]
      }
    ]
  }
}
```

`.claude/hooks/whitespace-preflight.sh`（末尾改行なしで保存）
```bash
#!/usr/bin/env bash
# PostToolUse(Edit|Write): CIと同一基準で編集ファイル1つだけ dotnet format whitespace 検証
# insert_final_newline=false / end_of_line=lf を壊さない（検証のみ・非破壊。違反は「削除」方向）
set -uo pipefail

input="$(cat)"

# file_path 抽出（jq 推奨。無ければ sed フォールバック=forward-slash JSON前提）
if command -v jq >/dev/null 2>&1; then
  file_path="$(printf '%s' "$input" | jq -r '.tool_input.file_path // empty')"
else
  file_path="$(printf '%s' "$input" \
    | sed -n 's/.*"file_path"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -n1)"
fi

# 対象外は黙って終了（.cs のみ。CIの paths: **.cs に整合）
[ -z "${file_path:-}" ] && exit 0
case "$file_path" in
  *.cs) : ;;
  *) exit 0 ;;
esac
[ -f "$file_path" ] || exit 0
command -v dotnet >/dev/null 2>&1 || exit 0

proj_dir="${CLAUDE_PROJECT_DIR:-$(pwd)}"

# 【修正・致命的】絶対パス + --exclude グロブ併用は何もマッチせず exit 0(false-pass)。
# proj_dir 基準の相対パスへ変換してから --include に渡す。
fp_norm=$(printf '%s' "$file_path" | tr '\\' '/')
proj_norm=$(printf '%s' "$proj_dir" | tr '\\' '/')
rel="${fp_norm#${proj_norm%/}/}"
# rel が絶対のまま(=proj外)なら誤検出回避のためスキップ
case "$rel" in /*|?:/*) exit 0 ;; esac

out="$(cd "$proj_dir" && dotnet format whitespace . --folder \
  --include "$rel" \
  --exclude "**/Library/**" --exclude "**/Temp/**" \
  --exclude "**/Packages/**" --exclude "**/obj/**" \
  --verify-no-changes 2>&1)"
status=$?

[ $status -eq 0 ] && exit 0

{
  echo "WHITESPACE違反を検出: $rel (CIのwhitespace_onlyフェーズと同一基準)"
  echo "原因候補: 末尾余分改行(FINALNEWLINE) / 行末空白(WHITESPACE) / CRLF(ENDOFLINE) / BOM(CHARSET)"
  echo "重要: .editorconfig insert_final_newline=false なので「末尾改行は追加せず削除」する。"
  echo "ローカル自動修正コマンド(検証フラグ無し=削除方向):"
  echo "  dotnet format whitespace . --folder --include \"$rel\" --exclude \"**/Library/**\" --exclude \"**/Temp/**\" --exclude \"**/Packages/**\" --exclude \"**/obj/**\""
  echo "--- dotnet format 出力 ---"
  echo "$out"
} 1>&2
exit 2
```

---

### 【6位】opp-version-sync-check — /version-check バージョンドリフト検証

- **type**: command / **工数**: S / **netValue**: medium / **検証 confidence**: 0.86 / **判定**: adopt-with-changes
- **解決する問題（証拠付き）**: バージョン文字列が複数系統に分散。**実機で2件のドリフトを確認**: (1) `Assets/uPiper/Samples~/BasicTTSDemo/package.json:4` version=`1.4.0` / `:7` `"com.ayutaz.upiper":"1.4.0"` が本体 1.5.0 とズレ。(2) `Packages/manifest.json` の dot-net-g2p `#v1.8.2`（8件全一致）vs `README.md` インストール手順 `#v1.5.0`（古い）。過去に `922a361`「v2.0.0→v1.5.0」手修正の実績。semantic-release は `2830f3b` で撤去済みの手動運用。
- **具体策**: `uPiperSetup.cs:22 PACKAGE_VERSION` を正本とし、各所を正規表現抽出して突合する**検証専用**ツール。形態A（主役）= `/version-check` コマンド。形態B（任意）= `scripts/check-version-sync.ps1`。自動書換は一切しない（撤去後の手動運用で誤った自動bumpを避ける）。
- **検証者の留意点（修正反映済み）**:
  1. command frontmatter の allowed-tools を **`Bash(pwsh:*), Bash(pwsh.exe:*), Bash(powershell.exe:*)` に拡張**（`Bash(powershell:*)` は powershell.exe 起動とパターン不一致で権限プロンプトを誘発）。
  2. **形態B（pre-commitフック）はデフォルト導入せず手動/任意のまま**（Samples版数はリリースまで遅延が許容され、全コミットブロックは `--no-verify` 常用化を招く）。リリース前の `/version-check` 明示実行を主運用とする。
  3. スクリプトは `.editorconfig` 準拠（LF・末尾改行なし）で保存。Samples 版数が意図的固定でないかをユーザーに一応確認（ただしドリフトの蓋然性が高い）。

**実物ドラフト**:

`.claude/commands/version-check.md`
```markdown
---
description: uPiper のバージョン定数ドリフトを検証（uPiperSetup.cs を正本に各 package.json / CHANGELOG / README を突合。検証のみ・自動書換なし）
allowed-tools: Bash(pwsh:*), Bash(pwsh.exe:*), Bash(powershell.exe:*), Read, Edit
---

# /version-check — バージョン同期検証

uPiper 内に分散するバージョン文字列のドリフトを検出する。**検証のみ。自動書換はしない**
（semantic-release は撤去済み・手動運用のため、誤った自動bumpを避ける）。

## 手順
1. 検証スクリプトを実行する（PowerShell 7 優先、無ければ Windows PowerShell）:
   ```
   pwsh -NoProfile -File scripts/check-version-sync.ps1
   ```
   `pwsh` が見つからない場合:
   ```
   powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-version-sync.ps1
   ```
2. 終了コードが 0 なら「全バージョン整合」と報告して終了。
3. 終了コード 1（ドリフト検出）なら:
   - スクリプト出力の `MISMATCH` 行を `ファイル:行 抽出値 → 期待値` の表で要約する。
   - 正本は `Assets/uPiper/Editor/uPiperSetup.cs:22` の `PACKAGE_VERSION`。
   - **ユーザーに修正可否を確認してから** Edit で各ファイルを正本値へ合わせる。
     dot-net-g2p のタグ（manifest.json / README のインストール手順）は uPiper の版数とは別系統なので、
     「manifest と README の dot-net-g2p タグが互いに不一致」の場合のみどちらに揃えるかをユーザーに確認する
     （**dot-net-g2p は別リポジトリ。そのリポジトリのコードは絶対に触らない**）。

## 注意
- 末尾改行を足さないこと（`.editorconfig`: `insert_final_newline=false`, `end_of_line=lf`）。
  Edit はピンポイント置換なので末尾改行に影響しないが、ファイル全体を Write し直さないこと。
- CHANGELOG の `[Unreleased]` / `- Unreleased` サフィックスは版数のみ比較対象（日付は無視）。
```

`scripts/check-version-sync.ps1`（LF・末尾改行なしで保存）
```powershell
#!/usr/bin/env pwsh
# uPiper バージョン同期検証（検証のみ・自動書換なし）
# 正本: Assets/uPiper/Editor/uPiperSetup.cs の PACKAGE_VERSION
# 終了コード: 0=整合 / 1=ドリフト検出 / 2=正本が読めない
$ErrorActionPreference = 'Stop'
$repo = (& git rev-parse --show-toplevel 2>$null)
if (-not $repo) { $repo = (Get-Location).Path }

$problems = [System.Collections.Generic.List[string]]::new()
function Add-Problem([string]$m) { $script:problems.Add($m) }

function Find-First($relPath, $pattern) {
    $full = Join-Path $repo $relPath
    if (-not (Test-Path $full)) { return $null }
    $i = 0
    foreach ($line in [System.IO.File]::ReadLines($full)) {
        $i++
        $m = [regex]::Match($line, $pattern)
        if ($m.Success) { return [pscustomobject]@{ File=$relPath; Line=$i; Value=$m.Groups[1].Value } }
    }
    return $null
}
function Find-All($relPath, $pattern) {
    $full = Join-Path $repo $relPath
    $res = [System.Collections.Generic.List[object]]::new()
    if (-not (Test-Path $full)) { return $res }
    $i = 0
    foreach ($line in [System.IO.File]::ReadLines($full)) {
        $i++
        foreach ($m in [regex]::Matches($line, $pattern)) {
            $res.Add([pscustomobject]@{ File=$relPath; Line=$i; Value=$m.Groups[1].Value })
        }
    }
    return $res
}

$src = Find-First 'Assets/uPiper/Editor/uPiperSetup.cs' 'PACKAGE_VERSION\s*=\s*"([0-9]+\.[0-9]+\.[0-9]+)"'
if (-not $src) {
    Write-Host "ERROR: 正本 PACKAGE_VERSION を Assets/uPiper/Editor/uPiperSetup.cs から抽出できません" -ForegroundColor Red
    exit 2
}
$canon = $src.Value
Write-Host ("CANONICAL  {0}:{1}  PACKAGE_VERSION = {2}" -f $src.File, $src.Line, $canon) -ForegroundColor Cyan

$checks = @(
    (Find-First 'Assets/uPiper/package.json' '"version"\s*:\s*"([0-9]+\.[0-9]+\.[0-9]+)"'),
    (Find-First 'CHANGELOG.md' '^##\s*\[([0-9]+\.[0-9]+\.[0-9]+)\]'),
    (Find-First 'README.md' '"com\.ayutaz\.upiper"\s*:\s*"([0-9]+\.[0-9]+\.[0-9]+)"'),
    (Find-First 'Assets/uPiper/Samples~/BasicTTSDemo/package.json' '"version"\s*:\s*"([0-9]+\.[0-9]+\.[0-9]+)"'),
    (Find-First 'Assets/uPiper/Samples~/BasicTTSDemo/package.json' '"com\.ayutaz\.upiper"\s*:\s*"([0-9]+\.[0-9]+\.[0-9]+)"')
)
foreach ($c in $checks) {
    if ($null -eq $c) { continue }
    $tag = if ($c.Value -eq $canon) { 'OK      ' } else { 'MISMATCH' }
    $line = "{0}  {1}:{2}  {3}" -f $tag, $c.File, $c.Line, $c.Value
    if ($c.Value -eq $canon) { Write-Host $line -ForegroundColor Green }
    else {
        Write-Host ("{0}  (expected {1})" -f $line, $canon) -ForegroundColor Yellow
        Add-Problem ("{0}:{1} = {2} (expected {3})" -f $c.File, $c.Line, $c.Value, $canon)
    }
}

$manifestTags = Find-All 'Packages/manifest.json' 'dot-net-g2p\.git\?path=[^#"]+#v([0-9]+\.[0-9]+\.[0-9]+)'
$readmeTags   = Find-All 'README.md'             'dot-net-g2p\.git\?path=[^#"]+#v([0-9]+\.[0-9]+\.[0-9]+)'
$manifestSet = $manifestTags | Select-Object -Expand Value -Unique
$readmeSet   = $readmeTags   | Select-Object -Expand Value -Unique
if ($manifestSet.Count -gt 1) {
    Add-Problem ("Packages/manifest.json 内の dot-net-g2p タグが不一致: {0}" -f ($manifestSet -join ', '))
    Write-Host ("MISMATCH  Packages/manifest.json dot-net-g2p tags: {0}" -f ($manifestSet -join ', ')) -ForegroundColor Yellow
}
if ($readmeSet.Count -gt 1) {
    Add-Problem ("README.md 内の dot-net-g2p タグが不一致: {0}" -f ($readmeSet -join ', '))
}
if ($manifestSet.Count -ge 1 -and $readmeSet.Count -ge 1) {
    $mv = ($manifestSet | Sort-Object | Select-Object -First 1)
    $rv = ($readmeSet   | Sort-Object | Select-Object -First 1)
    if ($mv -ne $rv) {
        Add-Problem ("dot-net-g2p タグが manifest(#v{0}) と README(#v{1}) で乖離（README のインストール手順が古い可能性）" -f $mv, $rv)
        Write-Host ("MISMATCH  dot-net-g2p: manifest=#v{0} README=#v{1}" -f $mv, $rv) -ForegroundColor Yellow
    } else {
        Write-Host ("OK        dot-net-g2p tags consistent: #v{0}" -f $mv) -ForegroundColor Green
    }
}

Write-Host ""
if ($problems.Count -eq 0) {
    Write-Host "[version-check] 全バージョン整合 (canonical $canon)" -ForegroundColor Green
    exit 0
}
Write-Host ("[version-check] ドリフト {0} 件:" -f $problems.Count) -ForegroundColor Red
foreach ($p in $problems) { Write-Host ("  - {0}" -f $p) -ForegroundColor Red }
Write-Host "正本は Assets/uPiper/Editor/uPiperSetup.cs:22 PACKAGE_VERSION。/version-check で修正候補を確認してください。" -ForegroundColor Red
exit 1
```

---

### 【7位】opp-cleanup-dead-g2p-sed — 死んだ dot-net-g2p checkout + sed 削除

- **type**: ci-local / **工数**: S / **netValue**: medium / **検証 confidence**: 0.85 / **判定**: adopt-with-changes
- **解決する問題（証拠付き）**: `Packages/manifest.json` は dot-net-g2p を Git URL `#v1.8.2` で直参照（実測8件全一致）。一方、**5ワークフロー6箇所に「dot-net-g2p checkout + `file:../../dot-net-g2p/src` を書き換える sed」が残存**（実測: `deploy-webgl.yml:44` / `unity-build-matrix.yml:111` / `unity-build.yml:65,192` / `unity-il2cpp-build.yml:85` / `unity-tests.yml:32`）。sed の対象文字列は Packages/ 配下に一切存在せず**真の no-op**。死因は `6f97287`（manifest のみ Git URL 化し workflows を放置）。
- **具体策**: 6箇所の8行ブロック（checkout + Fix local package paths(sed)）を削除。manifest が Git URL 直参照のため UPM が直接 fetch でき checkout/sed 不要（dot-net-g2p は public）。リグレッション防止に軽量ガード `scripts/verify-no-dead-g2p-sed.sh`。
- **検証者の留意点（修正反映済み）**:
  1. **【必須】`docs/development/CI_CD_GUIDE.md:50`「dot-net-g2pサブリポジトリの自動checkout」記載を削除**（削除しないと doc が stale 化）。
  2. ガードスクリプトの **part(c)（ref ドリフト検出）は削除**。checkout 完全削除後は workflows から `ref:` が消えるため part(c) は dead code。part(a) manifest の file: 混入 + part(b) workflow への sed 再混入の2点に絞る。
  3. `.sh` は `.editorconfig` で `indent_size=2` → ドラフトの4スペースを2スペースに修正。末尾余分改行を入れない。
  4. 期待効果の文言修正: 「毎回数百MB級 fetch が消える」は未検証 → 「冗長な dot-net-g2p の二重 clone（CI checkout 分）が1回消える」に正確化（UPM 側 clone は残る）。
  5. 初回 CI 実行で UPM が Git URL から解決できることを1回だけ確認。

**実物ドラフト（修正反映済み）**:

[A] 6箇所すべてから削除する「死んだブロック」（各ワークフロー内で同一）
```yaml
    - uses: actions/checkout@v6
      with:
        repository: ayutaz/dot-net-g2p
        ref: v1.8.2
        path: dot-net-g2p

    - name: Fix local package paths for CI
      shell: bash
      run: sed -i 's|file:../../dot-net-g2p/src|file:../dot-net-g2p/src|g' Packages/manifest.json
```
> 削除すると各ワークフローは「lfs: true の checkout」→「Cache Library / Free Disk Space」と直結する。
> 削除対象行: unity-tests.yml:24-32 / deploy-webgl.yml:36-44 / unity-build.yml:57-65 と 184-192 / unity-build-matrix.yml:103-111 / unity-il2cpp-build.yml:77-85

[B] `scripts/verify-no-dead-g2p-sed.sh`（part(c) 削除済み・2スペースインデント・末尾改行なし）
```bash
#!/usr/bin/env bash
# Guards against reintroduction of the dead dot-net-g2p checkout + sed.
# manifest.json references dot-net-g2p via Git URL (#vX.Y.Z), so the old
# "file:../../dot-net-g2p/src" local path and the sed that rewrote it are no-ops.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
MANIFEST="$ROOT/Packages/manifest.json"
WF_DIR="$ROOT/.github/workflows"
fail=0

# (a) manifest must NOT contain local file: references for dot-net-g2p
if grep -nE 'file:.*dot-net-g2p' "$MANIFEST" >/dev/null 2>&1; then
  echo "ERROR: Packages/manifest.json contains a local 'file:' dot-net-g2p reference."
  echo "       It must use the Git URL form (https://github.com/ayutaz/dot-net-g2p.git?path=src/...#vX.Y.Z)."
  grep -nE 'file:.*dot-net-g2p' "$MANIFEST" || true
  fail=1
fi

# (b) workflows must NOT contain the dead sed rewrite step
sed_hits="$(grep -rln 'file:../../dot-net-g2p/src' "$WF_DIR" 2>/dev/null || true)"
if [ -n "$sed_hits" ]; then
  echo "ERROR: Dead 'sed' rewrite of dot-net-g2p paths found in workflow(s):"
  echo "$sed_hits"
  echo "       manifest uses Git URLs; this sed is a no-op. Remove the step."
  fail=1
fi

if [ "$fail" -ne 0 ]; then
  echo "verify-no-dead-g2p-sed: FAILED"
  exit 1
fi
echo "verify-no-dead-g2p-sed: OK (no dead sed/checkout)"
```

---

### 【8位】opp-meta-orphan-guard — .meta 欠落/孤立 検出 pre-commit ガード

- **type**: precommit / **工数**: M / **netValue**: medium / **検証 confidence**: 0.82 / **判定**: adopt-with-changes
- **解決する問題（証拠付き）**: 新規 .cs/asmdef/jslib/フォルダ追加時の .meta コミット漏れ、削除済みアセットの孤立 .meta が反復。実測9コミット（`49093b8`=.meta 41件追加 / `901fa3a`=.meta 44件 / `c889acb` / `3e5a5ab`=孤立削除 / `a2c04be` / `18a1abc` / `28c4841` / `afe62e5` 等）。**最新3件は2026-04（2ヶ月前）で現在進行形**。現ツリーは cs=192 / cs.meta=192 で均衡だが churn が反復。GUID参照崩れの原因になりCIで露見しやすい。
- **具体策**: Unity起動不要のファイル存在チェック（`test -e`）で .meta の欠落/孤立を pre-commit 時に検出。**.meta は生成しない**（GUID整合は Unity の責務、誤った空GUIDを量産しない）。双方向チェック（追加→.meta欠落 / 削除→孤立 / 単独.meta追加→孤立）。`.git/hooks` 共有不可のため `.githooks/pre-commit`（1位と同一ファイルに同梱可能）。
- **検証者の留意点（修正必須・反映済み）**:
  1. **【必須】`.gitattributes` に `/.githooks/** text eol=lf` を追加**（`core.autocrlf=true` 環境で再checkout時にCRLF混入→shebang破壊→フック無音失敗を防ぐ）。
  2. **【必須】gitignore尊重**: meta欠落判定の前に `git check-ignore -q "$meta"` で意図的にignoreされたmeta（naist_jdic / aa / link.xml / *.pidb 等）をスキップ（誤ブロック防止）。
  3. solution_note の「.meta 末尾改行なし（検証済み）」は**偽**（実測でフォルダmetaは0aで終わる）→ 記述を削除/訂正。フック自体は無害。
  4. `core.hooksPath` 有効化は各cloneで手動1回。CONTRIBUTING/README に明記。`--no-verify` 退避手段を周知。
  5. このフックは現行ワークフロー（.cs先行コミット→後でUnity起動→meta追補）を変える（初回コミット前にUnity起動を強制）。意図通りだが摩擦あり。

**実物ドラフト（修正反映済み・`.githooks/pre-commit` に gitignore尊重を追加した版）**:
```bash
#!/usr/bin/env bash
# .githooks/pre-commit (meta-guard)
# 新規アセットの .meta 欠落 / 孤立 .meta を検出する。
# - Unity Editor 起動不要。ファイル存在チェックのみ。.meta は生成しない(GUID整合は Unity の責務)。
# - .meta の中身・末尾改行には一切触れない(insert_final_newline=false / lf を尊重)。
# - .gitignore で意図的に除外された .meta はスキップ(naist_jdic/aa/link.xml/*.pidb 等)。
# - bypass: git commit --no-verify
set -u

ASSET_ROOT="Assets/"
should_skip() { case "$1" in *.meta) return 0 ;; *) return 1 ;; esac; }
red()   { printf '\033[31m%s\033[0m\n' "$1"; }
yellow(){ printf '\033[33m%s\033[0m\n' "$1"; }

# 意図的に gitignore された meta はスキップ(誤ブロック防止)
is_ignored() { git check-ignore -q "$1" 2>/dev/null; }

errors=0; missing_meta=""; orphan_meta=""; missing_dir_meta=""

while IFS= read -r -d '' status && IFS= read -r -d '' path; do
  case "$status" in R*|C*) IFS= read -r -d '' newpath; path="$newpath" ;; esac
  case "$path" in "$ASSET_ROOT"*) : ;; *) continue ;; esac
  should_skip "$path" && continue
  case "$status" in
    A*|M*|R*|C*)
      meta="${path}.meta"
      if ! is_ignored "$meta" \
         && ! git ls-files --error-unmatch -- "$meta" >/dev/null 2>&1 \
         && [ ! -e "$meta" ]; then
        missing_meta="${missing_meta}\n  ${path}  ->  欠落: ${meta}"; errors=1
      fi
      dir="$(dirname "$path")"
      while [ "$dir" != "." ] && [ "$dir" != "${ASSET_ROOT%/}" ] && [ "$dir" != "/" ]; do
        dmeta="${dir}.meta"
        if ! is_ignored "$dmeta" \
           && ! git ls-files --error-unmatch -- "$dmeta" >/dev/null 2>&1 \
           && [ ! -e "$dmeta" ]; then
          case "$missing_dir_meta" in
            *"$dmeta"*) : ;;
            *) missing_dir_meta="${missing_dir_meta}\n  ${dir}/  ->  欠落: ${dmeta}"; errors=1 ;;
          esac
        fi
        dir="$(dirname "$dir")"
      done
      ;;
    D*)
      meta="${path}.meta"
      if ! is_ignored "$meta" \
         && { git ls-files --error-unmatch -- "$meta" >/dev/null 2>&1 || [ -e "$meta" ]; }; then
        orphan_meta="${orphan_meta}\n  削除: ${path}  ->  残存(孤立): ${meta}"; errors=1
      fi
      ;;
  esac
done < <(git diff --cached -z --name-status --diff-filter=ACMRD --)

while IFS= read -r -d '' status && IFS= read -r -d '' metapath; do
  case "$metapath" in "$ASSET_ROOT"*.meta) : ;; *) continue ;; esac
  case "$status" in A*|M*|R*|C*) : ;; *) continue ;; esac
  is_ignored "$metapath" && continue
  base="${metapath%.meta}"
  if [ ! -e "$base" ] && ! git ls-files --error-unmatch -- "$base" >/dev/null 2>&1; then
    if [ -d "$base" ] || git ls-files --error-unmatch -- "${base}/*" >/dev/null 2>&1; then :;
    else orphan_meta="${orphan_meta}\n  孤立 .meta: ${metapath}  ->  対応アセット/フォルダなし"; errors=1; fi
  fi
done < <(git diff --cached -z --name-status --diff-filter=ACMR -- "*.meta")

if [ "$errors" -ne 0 ]; then
  echo ""
  red "[meta-guard] Unity .meta の不整合を検出しました。commit を中止します。"
  [ -n "$missing_meta" ] && { echo ""; yellow "■ .meta が欠落しているアセット(Unity Editor で一度開くと自動生成):"; printf "$missing_meta\n"; }
  [ -n "$missing_dir_meta" ] && { echo ""; yellow "■ .meta が欠落しているフォルダ:"; printf "$missing_dir_meta\n"; }
  [ -n "$orphan_meta" ] && { echo ""; yellow "■ 孤立した .meta(対応アセット/フォルダがない。削除し忘れの可能性):"; printf "$orphan_meta\n"; }
  echo ""
  yellow "対処: 欠落は Unity Editor でアセットを認識させ生成された .meta を 'git add'。"
  yellow "      孤立は 'git rm <metaパス>' で削除。.meta の中身は手書きせず Unity に生成させること。"
  yellow "      どうしても通す場合のみ: git commit --no-verify"
  echo ""
  exit 1
fi
exit 0
```
`.gitattributes` に追記（必須）
```
/.githooks/** text eol=lf
```

> 注: 1位(opp-precommit-final-gate)と本フックを同一 `.githooks/pre-commit` に同梱する場合は、各チェックを関数化し終了コードを集約する（`set -u`/`exit` の二重定義に注意）。

---

### 【9位】opp-onboarding-ci-rules-doc — CI制約・運用ルールを ONBOARDING.md に明文化

- **type**: doc / **工数**: S / **netValue**: medium / **検証 confidence**: 0.85 / **判定**: adopt-with-changes
- **解決する問題（証拠付き）**: CIでしか顕在化しない「静かな制約」を実証。(1) `pr-target-check.yml` が `headRef !== 'develop'` で `core.setFailed`（main宛PRは develop からのみ）。(2) `deploy-webgl.yml:5` の push トリガーが `branches: [ feature/webgl-support ]` 固定 → 現行 `feature/webgl-japanese-input` では**静かに不発火**。(3) semantic-release 撤去済み（`2830f3b`）・commitlint未導入・CHANGELOG手動。(4) stale な feature/* ブランチ多数。CLAUDE.md は技術アーキ中心で28.9KB に肥大。
- **具体策**: リポジトリ直下に `ONBOARDING.md` を新規作成し、CIでしか分からない運用ルールを1枚に集約。設定は一切変更しない純ドキュメント。
- **検証者の留意点（修正必須・反映済み）**:
  1. **【必須・grounding誤り】カバレッジゲートの表現を訂正**。実機確認で `unity-tests.yml` には coverage で exit 1/setFailed する gating ステップは存在しない（`minimumCoverageThresholds:lineCoverage=50` は ReportGenerator のレポート/バッジ用閾値マーキングのみ、ジョブを失敗させない）。→「lineCoverage=50 は CIを失敗させる強制ゲートではなくレポート/バッジ用の参考閾値。ただし方針として50%維持が期待される」と正確化。「テスト未追加でカバレッジCI赤→追記の往復」防止効果は削除。
  2. 揮発しやすい値（現行ブランチ名・dot-net-g2p ref・CHANGELOGバージョン・stale一覧）はベタ書きではなく**確認コマンド併記**（`git branch --show-current` / `git branch -r` 等）で陳腐化を緩和。
  3. WebGLデプロイ不発火・PR target制約・手動CHANGELOG・dot-net-g2p別repo の4点が本ドキュメントの正味価値の中核。維持する。

**実物ドラフト（修正反映済み・`ONBOARDING.md`）**:
````markdown
# uPiper 開発オンボーディング

> このファイルは「CLAUDE.md(技術アーキテクチャ)には書いていないが、知らないと CI が赤くなる / PR が弾かれる / デプロイが静かに不発火する」運用ルールを 1 枚に集約したもの。新規participant(人間/エージェント)は作業前に必読。
> 末尾改行は付けない(`.editorconfig: insert_final_newline = false`)。改行コードは LF。

## 1. ブランチ運用 — main へは develop からのみ

- **`main` 宛の PR は `develop` ブランチからのみ許可される。** それ以外の head から main へ PR を作ると `pr-target-check.yml` が `core.setFailed` で**必ず失敗する**(`headRef !== 'develop'` 判定)。
- `feature/*` の作業は **develop に向けて** PR を出す。
- 現行作業ブランチは `git branch --show-current` で確認(本レポート時点: `feature/webgl-japanese-input`)。
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
- **Unity アナライザー規約**: Unity オブジェクトに null 合体 `??` / null 条件 `?.` / null 合体代入 `??=` を使わない(UNT0007 / UNT0008 / UNT0023)。

## 6. 外部依存 — dot-net-g2p は別リポジトリ

- `ayutaz/dot-net-g2p`(public)は **別リポジトリ**。**uPiper 側の責務でそのコードを変更しない**。
- バージョンは `Packages/manifest.json` の Git URL `#vX.Y.Z` で管理(本レポート時点 #v1.8.2)。README のインストール手順も同じタグに揃える。
````

---

## 3. 見送り / 要検討

| 提案 | 判定 | netValue | 理由 |
|------|------|----------|------|
| **opp-ticket-scaffold-command**（/ticket チケット雛形） | adopt-with-changes | medium | 採用自体は可だが、目玉の効果主張「FINALNEWLINE再発防止」が**捏造**。実証でFINALNEWLINE系コミット7件は全て.cs専用でmd 0件、`dotnet format whitespace --folder` は .md を一切フラグしない。価値の主軸を「7章の抜け/順序ブレ/連番採番ミスのゼロ化」に一本化し、効果主張を訂正すれば採用に値する。templateB(Phase系 frontmatter)は削除済みワークフロー対象のためデフォルトから外す。**優先度は低い**（純ドキュメント生成・トイル削減は中程度）。 |
| **opp-ticket-index-sync-command**（/ticket-index, /docs-archive） | adopt-with-changes | **low** | 機構は健全だが**現状価値が低い**。現行WebGLチケットには index.md が存在せず、解決対象の二重管理問題が現時点で顕在化していない（index新規導入になる）。旧形式は「表ベース」でなく**YAMLフロントマター**で提案パーサが読めず黙ってWARN無しに全件[ ]デフォルトする欠陥。/docs-archive（破壊的 git mv）は複雑度が高い。**次のチケット駆動機能（複数チケット+完了マーク運用が実際に発生する機能）が始まるタイミングで /ticket-index 単体のみ導入する**のが費用対効果が高く、今すぐ導入する強い理由はない。 |

> いずれも reject ではないが、即採用枠（§2）から外し「機が熟したら」枠とした。チケット駆動が次に動き出す際にまとめて検討する。

---

## 4. ROI 優先順位表

| rank | 施策 | type | 工数 | 期待効果 | netValue | confidence |
|------|------|------|------|----------|----------|------------|
| 1 | pre-commit 最終ゲート（whitespace） | precommit | S | FINALNEWLINE/フォーマット手戻り9件超を**コミット段階で阻止**（手動編集も捕捉） | medium | 0.90 |
| 2 | /resolve-threads（PRレビュー一括解決） | command | S | スレッドID手貼り撤廃 + settings堆積（PRRT_25/PRRC_7）を**今後ゼロ化** | high | 0.85 |
| 3 | /precheck（uLoop先回り検証） | skill | S | CSエラー/whitespace/デッドロックを30分CI前に検出、CI往復を1往復に短縮 | high | 0.85 |
| 4 | settings剪定 + 共有解禁（.gitignore carve-out） | settings | S | allow 241→大幅圧縮、**他提案の配布前提を解禁** | high | 0.82 |
| 5 | PostToolUse whitespace 先回りフック | hook | S | Edit/Write直後の即時フィードバック（pre-commit の上乗せ） | high | 0.85 |
| 6 | /version-check（バージョンドリフト検証） | command | S | 現存2ドリフト即可視化、リリース時の取り違え再発防止 | medium | 0.86 |
| 7 | 死んだ g2p checkout + sed 削除 | ci-local | S | 冗長な二重 clone 除去、CI簡素化、再混入ガード | medium | 0.85 |
| 8 | .meta 欠落/孤立 ガード | precommit | M | GUID参照崩れ手戻り9件超を防止 | medium | 0.82 |
| 9 | ONBOARDING.md（CI制約明文化） | doc | S | main←develop/WebGL不発火/手動CHANGELOG/別repo を事前可視化 | medium | 0.85 |
| — | /ticket（チケット雛形） | command | S | 章ブレ/連番ミスゼロ化（FINALNEWLINE効果は誇張・要訂正） | medium | 0.83 |
| — | /ticket-index, /docs-archive | command | M | 二重管理排除（現状未顕在化・機が熟したら） | low | 0.82 |

---

## 5. 推奨ロードマップ

**Phase 0（前提解禁・最初に1回）— settings剪定 + .gitignore carve-out【rank4】**
他の hook/command/skill 提案を**チームへ配布する前提**を成立させる。`.gitignore` carve-out で `.claude/settings.json`・`hooks/`・`skills/`・`commands/` を追跡可能にし、`settings.local.json` の使い捨て約80件を named カテゴリ単位で剪定する。**ここを最初にやらないと以降のフック/コマンドはローカル限定にとどまる**。適用後に `dotnet format whitespace . --folder --verify-no-changes` を1回回し、新規追跡した settings.json が検証を落とさないことを確認。

**Phase 1（フォーマット先回り・最大の苦痛の根治）— pre-commit + PostToolUse hook【rank1, 5】**
FINALNEWLINE手戻り（実測8件）の根治。まず pre-commit 最終ゲート（手動編集も捕捉、**カンマ区切り→反復--include / フォルダモード必須修正を反映**）を導入。続いて PostToolUse フック（**絶対パス→相対パス変換の必須修正を反映**）で Claude経由編集の即時フィードバックを上乗せ。両者は検証方向で冪等共存。`.gitattributes` に `/.githooks/** text eol=lf` を追加。

**Phase 2（CIでしか出ない問題の先回り）— /precheck skill【rank3】**
CSコンパイルエラー・async デッドロックを30分CI前に潰す。uLoopMCP compile + クラス単位 run-tests を束ねる（**実在しないテストクラス名の差し替え修正を反映**）。Phase 1 と合わせて「push前に CI 赤を構造的に防ぐ」体制が完成。

**Phase 3（反復プロセスの定型化）— /resolve-threads command【rank2】**
PRレビュー対応（実測38件）の機械作業を1コマンドに閉じ、settings堆積を今後ゼロ化（**outdated既定の必須修正を反映**）。Phase 0 の剪定と対になる。

**Phase 4（ドリフト・クリーンアップ・ドキュメント）— /version-check, dead g2p削除, ONBOARDING.md【rank6, 7, 9】**
バージョンドリフト検証（現存2件を即修正）、死んだ g2p checkout+sed の一括削除（**CI_CD_GUIDE.md更新 + part(c)削除の必須修正を反映**）、運用ルールのドキュメント化（**カバレッジゲートの grounding訂正を反映**）。

**Phase 5（任意・摩擦の大きいもの）— .meta ガード【rank8】**
工数M・現行ワークフローを変える（初回コミット前にUnity起動を強制）摩擦があるため最後に。**gitignore尊重と .gitattributes の必須修正を反映**してから導入。チケット駆動が次に動き出したら /ticket・/ticket-index も併せて検討。

---

## 6. 重要な注意事項

- **本レポートは調査・提案であり、実ファイルの作成・編集（`.claude/settings.json`、`.githooks/pre-commit`、`.claude/commands/*.md`、`.claude/skills/precheck/SKILL.md`、`scripts/*`、`.gitignore`/`.gitattributes` 編集、ワークフロー削除、`ONBOARDING.md`）はすべて未実施である。** いずれの適用もユーザーの承認後に行う。
- 各ドラフトには**敵対的検証で指摘された必須修正を既に反映済み**（pre-commit の反復--include/フォルダモード、PostToolUse の相対パス変換、settings.json から絶対パス削除、/precheck のテストクラス名差し替え、meta-guard の gitignore尊重+.gitattributes、g2p削除の CI_CD_GUIDE更新+part(c)削除、ONBOARDING のカバレッジ訂正）。検証で**捏造/誇張**と判定された箇所（/ticket の FINALNEWLINE効果、settings の「導入と同時に既存削除」、g2p の「数百MB fetch」、coverage の「CI赤」）は採用文言から除外・訂正済み。
- すべての提案は **CIの whitespace_only フェーズ・uLoopMCP・semantic-release（撤去済み）・pr-target-check と非衝突**であることを確認済み。**dot-net-g2p（別リポジトリ）のコードには一切触れない**。

---

## 追加調査推奨（Gaps）

追加調査を推奨する点:

1. **pre-commit / meta-guard の同一ファイル同梱設計**: rank1(whitespace)とrank8(meta-guard)をどちらも .githooks/pre-commit に置く場合、各チェックを関数化し set -u/exit を集約する統合版の実機検証が未実施。両者を別ファイルにするか1ファイルに束ねるかを、実際の .githooks ディレクトリ構成と合わせて確定する必要がある。

2. **settings.local.json 剪定の正確な削除リスト確定**: 検証で『PRループ断片は提案の4件ではなく実測約10件(for f:*/do sed:*/for pkg:* 含む)』『Unity.exeフルは4件でなく3件』と数値差が判明。実際の剪定前に settings.local.json 全241エントリを1件ずつ精査し、Bash(git:*)/Bash(gh:*) で包含されるgranular許可と、local に残すべき環境固有許可を明確に仕分けた最終削除リストを作成すべき(全置換でなく差分削除に限定)。

3. **共有 settings.json と CI dotnet format --folder の相互作用**: carve-out で .claude/settings.json を git追跡すると、CI(dotnet-format.yml)の `dotnet format whitespace . --folder`(除外はLibrary/Temp/Packages/objのみ、.claude除外なし)が新規JSONを拾い --verify-no-changes を落とす可能性が dotnetバージョン依存で残る。CI環境(ubuntu/dotnet 6.0.x)で実際に .claude/settings.json が whitespace チェック対象になるか/落ちるかを、ローカルではなくCI上で一度確認することを推奨。

4. **uLoopMCP run-tests の実テストクラス名の網羅確認**: /precheck の Step3 で参照する実在テストクラス(ProsodyInferenceIntegrationTests / MixedLanguageE2ETests / BackendSelectorTests 等)が Tests/Editor と Tests/Runtime のどちらに存在し、同名衝突があるかを Glob で全列挙して SKILL.md のガイダンスに正確に反映する作業が残る(MEMORY.md由来の陳腐化を再び持ち込まないため)。

5. **WebGLデプロイ deploy-webgl.yml:5 のブランチ固定**: ONBOARDING.md ではTODOに留めたが、現行 feature/webgl-japanese-input で WebGL 作業を継続するなら push トリガーのブランチ更新 or workflow_dispatch 正式運用への移行という別途の設定変更opp が必要。本レポートのスコープ外だが優先度の判断が要る。

6. **ci-log.txt / ci-log2.txt の git追跡**: g2p削除の検証中に、これらの古いログアーティファクトが git追跡されており当該文字列を含むことが判明(problem記述の『追跡ソース0件』と矛盾)。.gitignore追加や git rm の要否を別issueとして検討推奨。

7. **Samples~/BasicTTSDemo の 1.4.0 が意図的固定か**: version-check のドリフト2件のうちSamples版数1.4.0が、サンプルパッケージの独立バージョニング方針による意図的なものか、単なる更新漏れかをユーザーに確認する必要がある(本体/CHANGELOG/READMEが全て1.5.0である以上ドリフトの蓋然性が高いが断定不可)。