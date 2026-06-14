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