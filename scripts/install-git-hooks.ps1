# uPiper: リポジトリ同梱の git フック (.githooks) を有効化する。
# クローン後に1回だけ実行: pwsh scripts/install-git-hooks.ps1
$ErrorActionPreference = "Stop"
Set-Location (git rev-parse --show-toplevel)
git config core.hooksPath .githooks
Write-Host "✓ core.hooksPath を .githooks に設定しました。pre-commit が有効です。"