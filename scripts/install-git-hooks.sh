#!/usr/bin/env bash
# uPiper: リポジトリ同梱の git フック (.githooks) を有効化する。
# クローン後に1回だけ実行: bash scripts/install-git-hooks.sh
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"
git config core.hooksPath .githooks
chmod +x .githooks/* 2>/dev/null || true
echo "✓ core.hooksPath を .githooks に設定しました。pre-commit が有効です。"