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

Git Bash:

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