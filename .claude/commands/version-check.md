---
description: uPiper のバージョン定数ドリフトを検証（uPiperSetup.cs を正本に各 package.json / CHANGELOG / README を突合。検証のみ・自動書換なし）
allowed-tools: Bash(pwsh:*), Bash(pwsh.exe:*), Bash(powershell.exe:*), Read, Edit
---

# /version-check — バージョン同期検証

uPiper 内に分散するバージョン文字列のドリフトを検出する。**検証のみ。自動書換はしない**
（semantic-release は撤去済み・手動運用のため、誤った自動bumpを避ける）。

## 手順
1. 検証スクリプトを実行する（PowerShell 7 優先、無ければ Windows PowerShell）:

```bash
pwsh scripts/check-version-sync.ps1
```

   `pwsh` が見つからない場合:

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/check-version-sync.ps1
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