# テスト実行ガイド

## テスト実行手順

### 1. 実行前の準備
1. **Unity Editorの設定**
   - Edit > Preferences > General
   - "Auto Refresh" を無効化（重要）

2. **コンパイル確認**
   - Console ウィンドウでエラーがないことを確認
   - Unity右下のスピナーが停止していることを確認

3. **ファイルの保存**
   - Ctrl+S ですべての変更を保存

### 2. Test Runnerの使用
1. **Test Runnerを開く**
   - Window > General > Test Runner

2. **テストの実行**
   - PlayMode タブを選択
   - "Run All" ボタンをクリック

### 3. 個別テストの実行（推奨）
問題を特定しやすくするため、以下の順序で個別に実行：

#### 基本的なテスト（まず実行）
1. SimpleLTSPhonemizerTests
2. EnhancedEnglishPhonemizerTests
3. FliteLTSPhonemizerTests

#### 統合テスト（次に実行）
4. EnglishPhonemizerTests
5. MixedLanguagePhonemizerTests
6. UnifiedPhonemizerTests

#### エラーハンドリングテスト（最後に実行）
7. PhonemizerErrorHandlingTests
8. PhonemizerIntegrationTests

### 4. テストが失敗した場合の対処

#### ケース1: 初期化エラー
```
Failed to initialize [Phonemizer名]
```
**対処法**: 
- CMUDictionaryのログを確認
- 最小辞書へのフォールバックが動作しているか確認

#### ケース2: タイムアウト
```
Test exceeded Timeout value of X ms
```
**対処法**:
- 個別にテストを実行
- Unity Editorを再起動

#### ケース3: アサーションエラー
```
Expected: X
But was: Y
```
**対処法**:
- 期待値と実際の値を比較
- 音素化ロジックの確認

### 5. デバッグ情報の確認

Consoleで以下のログを確認：
```
[CMUDictionary] Attempting to load from: [パス]
[CMUDictionary] File exists: False
[CMUDictionary] Using minimal built-in dictionary
Loaded minimal dictionary with 36 words
```

### 6. トラブルシューティング

#### Unity Editorが重い場合
1. Library フォルダを削除
2. Unity Editorを再起動
3. プロジェクトを再インポート

#### 特定のテストだけ失敗する場合
1. そのテストクラスだけを実行
2. SetUpメソッドにブレークポイントを設定
3. 初期化プロセスをステップ実行

#### すべてのテストが失敗する場合
1. CMUDictionary.csのLoadMinimalDictionaryが呼ばれているか確認
2. PhonemizerBackendBaseの初期化を確認

### 7. 期待される結果

✅ **成功するべきテスト**
- 基本的な音素化テスト
- 初期化テスト（最小辞書使用）
- タイムアウトテスト

⚠️ **失敗する可能性があるテスト**
- 完全な辞書を期待するテスト
- 特定の音素精度を期待するテスト
- ネットワーク依存のテスト

### 8. レポート作成

テスト実行後、以下の情報を記録：
1. 成功したテスト数
2. 失敗したテスト数
3. 失敗の原因（初期化、タイムアウト、アサーション）
4. Consoleのエラーメッセージ