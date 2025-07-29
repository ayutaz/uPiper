# Test Runner Assembly Reload 問題の対処法

## 問題
テスト実行中に以下のエラーが発生：
```
TestRunner: Unexpected assembly reload happened while running tests
UnityEditor.AssemblyReloadEvents:OnBeforeAssemblyReload ()
```

## 原因
- テスト実行中にスクリプトやメタファイルが変更された
- Unity Editorの自動リコンパイルが発生した

## 解決方法

### 1. 自動リコンパイルを一時的に無効化
Unity Editor メニューから：
- Edit > Preferences > General
- "Auto Refresh" のチェックを外す

### 2. スクリプトコンパイル設定の調整
- Edit > Project Settings > Editor
- "Enter Play Mode Settings" で以下を確認：
  - "Reload Domain" のチェックを確認
  - "Reload Scene" のチェックを確認

### 3. テスト実行前の対策
```csharp
// テスト実行前に以下を確認
1. すべてのスクリプトがコンパイル済み
2. Console にエラーがない
3. ファイルの変更を保存済み
```

### 4. Test Runnerの設定
- Window > General > Test Runner
- 設定アイコン（歯車）をクリック
- "Enable playmode tests for all assemblies" のチェックを確認

## 推奨事項

### テスト実行時のベストプラクティス
1. **テスト実行前にすべて保存**: Ctrl+S ですべてのファイルを保存
2. **コンパイル完了を待つ**: Unityの右下のスピナーが止まるまで待機
3. **バッチモードでテスト**: 大量のテストはバッチモードで実行

### コマンドラインからのテスト実行
```bash
Unity -batchmode -projectPath "C:\Users\yuta\Desktop\Private\uPiper" -runTests -testPlatform EditMode -testResults results.xml
```

## 現在の状況での対処

1. **一時的な回避策**
   - テスト実行中はファイルを編集しない
   - テスト実行前にUnity Editorをリスタート

2. **根本的な解決**
   - ハングするテストを修正（実施済み）
   - CMUDictionaryの初期化問題を解決（次のタスク）

## 注意事項
- アセンブリリロードが発生するとテスト結果が不正確になる可能性
- 特に非同期テストやコルーチンベースのテストで問題が発生しやすい