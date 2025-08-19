# Basic TTS Demo

Unity向けのテキスト読み上げ（TTS）機能の基本的なデモシーンです。

## 必要な環境

### Unity バージョン
- Unity 6000.0.55f1 以降

### 入力システムについて

このデモシーンはUIの操作にUnityのEventSystemを使用しています。

**重要**: デモシーンにはInput System用のコンポーネントが含まれていますが、EventSystemAutoSetupによって自動的に適切な入力モジュールが選択されるため、**Input Systemは必須ではありません**。

対応している入力システム：

- **Input System** - Unity 6のデフォルト（シーンのデフォルト設定）
- **Input Manager** - レガシー入力システム（自動的にフォールバック）
- **Both** - ハイブリッドモード（Input Managerを優先）

#### Input Managerを使用する場合

プロジェクト設定で「Active Input Handling」を「Input Manager」に設定している場合、自動的にInput Manager用の設定が適用されます。

1. Edit > Project Settings > Player
2. Active Input Handling を "Input Manager" に設定
3. Unityを再起動（設定変更後に必要）

#### トラブルシューティング

**UIボタンがクリックできない場合：**

1. シーン内のEventSystemオブジェクトを選択
2. EventSystemAutoSetupコンポーネントを追加（まだ追加されていない場合）
3. シーンを再生して、コンソールログで以下を確認：
   - `[EventSystemAutoSetup] Input configuration: ...` - 現在の入力設定
   - `[EventSystemAutoSetup] Active module: ...` - 有効なモジュール
4. それでも動作しない場合は、手動で調整：
   - Input System使用時: InputSystemUIInputModuleを有効化、StandaloneInputModuleを無効化
   - Input Manager使用時: StandaloneInputModuleを有効化、InputSystemUIInputModuleを無効化（存在する場合）

## デモの機能

### 日本語音声合成
- OpenJTalkを使用した高品質な日本語音声合成
- 漢字、ひらがな、カタカナ、英数字の混在テキストに対応

### 英語音声合成  
- CMU辞書を使用した英語音声合成
- 正確な発音記号変換

### UI機能
- テキスト入力フィールド
- 言語選択ドロップダウン
- 音声生成ボタン
- 生成状況の表示

## 使い方

1. デモシーンを開く
2. テキスト入力フィールドに読み上げたいテキストを入力
3. 言語を選択（日本語/英語）
4. 「Generate Speech」ボタンをクリック
5. 音声が自動的に再生されます

## 注意事項

- 初回実行時は辞書データの読み込みに時間がかかる場合があります
- Android実行時は自動的に辞書データが展開されます
- WebGLビルドには対応していません

## 依存関係

- uPiperコアライブラリ
- Unity AI Inference Engine
- TextMeshPro（日本語フォント表示用）

## ライセンス

Apache License 2.0