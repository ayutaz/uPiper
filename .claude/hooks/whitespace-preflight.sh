#!/usr/bin/env bash
# PostToolUse(Edit|Write) hook: 編集直後の C# 1ファイルに CI(dotnet-format.yml)と同一基準の
# whitespace 検証をかけ、違反があれば即座に Claude へ返す（その場で修正させる）。
# 検証のみ・非破壊。insert_final_newline=false / end_of_line=lf を壊さない（違反は「削除」方向）。
set -uo pipefail

input="$(cat)"

# Claude Code は PostToolUse の入力を stdin に JSON で渡す。tool_input.file_path を取り出す。
# 優先: jq → python → sed（forward-slash 化された JSON 前提のフォールバック）。
if command -v jq >/dev/null 2>&1; then
  file_path="$(printf '%s' "$input" | jq -r '.tool_input.file_path // empty')"
elif command -v python >/dev/null 2>&1; then
  file_path="$(printf '%s' "$input" | python -c 'import sys,json
try:
    d=json.load(sys.stdin); print((d.get("tool_input") or {}).get("file_path","") or "")
except Exception:
    print("")' 2>/dev/null)"
else
  file_path="$(printf '%s' "$input" \
    | sed -n 's/.*"file_path"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -n1)"
fi

# 対象外は黙って終了（.cs のみ。CI の paths: **.cs に整合）。
[ -z "${file_path:-}" ] && exit 0
case "$file_path" in
  *.cs) : ;;
  *) exit 0 ;;
esac
[ -f "$file_path" ] || exit 0
command -v dotnet >/dev/null 2>&1 || exit 0

proj_dir="${CLAUDE_PROJECT_DIR:-$(pwd)}"

# 検証修正(致命的): 絶対パス + --exclude グロブ併用は何もマッチせず exit 0(false-pass)になる。
# proj_dir 基準の相対パスへ変換してから --include に渡す。
fp_norm=$(printf '%s' "$file_path" | tr '\\' '/')
proj_norm=$(printf '%s' "$proj_dir" | tr '\\' '/')
rel="${fp_norm#${proj_norm%/}/}"
# rel が絶対のまま(=プロジェクト外)なら誤検出回避のためスキップ。
case "$rel" in /*|?:/*) exit 0 ;; esac

out="$(cd "$proj_dir" && dotnet format whitespace . --folder \
  --include "$rel" \
  --exclude "**/Library/**" --exclude "**/Temp/**" \
  --exclude "**/Packages/**" --exclude "**/obj/**" \
  --verify-no-changes 2>&1)"
status=$?

[ $status -eq 0 ] && exit 0

{
  echo "WHITESPACE違反を検出: $rel (CI の whitespace_only フェーズと同一基準)"
  echo "原因候補: 末尾余分改行(FINALNEWLINE) / 行末空白(WHITESPACE) / CRLF(ENDOFLINE) / BOM(CHARSET)"
  echo "重要: .editorconfig insert_final_newline=false のため「末尾改行は追加せず削除」する。"
  echo "ローカル自動修正(検証フラグ無し=削除方向):"
  echo "  dotnet format whitespace . --folder --include \"$rel\" --exclude \"**/Library/**\" --exclude \"**/Temp/**\" --exclude \"**/Packages/**\" --exclude \"**/obj/**\""
  echo "--- dotnet format 出力 ---"
  echo "$out"
} 1>&2
exit 2